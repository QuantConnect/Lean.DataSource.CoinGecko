/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using Newtonsoft.Json;
using QuantConnect.DataSource;
using QuantConnect.Logging;
using QuantConnect.Securities;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace QuantConnect.DataProcessing
{
    /// <summary>
    /// CoinGeckoUniverseDataDownloader implementation.
    /// </summary>
    public class CoinGeckoUniverseDataDownloader : IDisposable
    {
        public const string VendorName = "coingecko";
        
        private readonly string _destinationFolder;
        private readonly string _processedFolder;
        private readonly string _universeFolder;
        private readonly string _processedUniverseFolder;
        private readonly int _maxRetries = 5;
        private readonly HttpClient _client;

        /// <summary>
        /// Control the rate of download per unit of time.
        /// </summary>
        private readonly RateGate _indexGate;

        /// <summary>
        /// Creates a new instance of <see cref="CoinGeckoUniverseDataDownloader"/>
        /// </summary>
        /// <param name="destinationFolder">The folder where the data will be saved</param>
        /// <param name="processedFolder">The folder where the existing data being stored</param>
        public CoinGeckoUniverseDataDownloader(string destinationFolder, string processedFolder)
        {
            _destinationFolder = destinationFolder;
            _processedFolder = processedFolder;
            _universeFolder = Path.Combine(_destinationFolder, "universe");
            _processedUniverseFolder = Path.Combine(_processedFolder, "universe");

            // CoinGecko: Our Free API* has a rate limit of 10-50 calls/minute
            // Represents rate limits of 5 requests per 1 minute
            _indexGate = new RateGate(5, TimeSpan.FromMinutes(1));

            Directory.CreateDirectory(_destinationFolder);
            Directory.CreateDirectory(_processedFolder);
            Directory.CreateDirectory(_universeFolder);
            Directory.CreateDirectory(_processedUniverseFolder);
            
            _client = new HttpClient();
            _client.BaseAddress = new Uri("https://api.coingecko.com/api/v3/coins/");
            _client.DefaultRequestHeaders.Clear();

            // Responses are in JSON: you need to specify the HTTP header Accept: application/json
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Runs the instance of the object.
        /// </summary>
        /// <returns>True if process all downloads successfully</returns>
        public bool Run()
        {
            var stopwatch = Stopwatch.StartNew();

            var coinGeckoList = GetCoinGeckoList();
            var supported = GetSupportedCryptoCurrencies();
            var universeData = new Dictionary<DateTime, List<string>>();

            foreach (var coinGeckoItem in coinGeckoList)
            {
                if (!supported.Remove(coinGeckoItem.Symbol)) continue;

                var exists = File.Exists(Path.Combine(_processedFolder, $"{coinGeckoItem.Symbol.ToLowerInvariant()}.csv"));
                var coinGeckoMarketChartsForId = GetCoinGeckoMarketChartsForId(coinGeckoItem.Id, exists);
                var coinGeckoDictionary = coinGeckoMarketChartsForId.GetCoinGeckoDictionary();

                string coinGeckoToString(CoinGecko coinGecko)
                    => $"{coinGecko.EndTime:yyyyMMdd},{coinGecko.Price},{coinGecko.Volume},{coinGecko.MarketCap}";

                SaveContentToFile(_destinationFolder, _processedFolder, coinGeckoItem.Symbol,
                    coinGeckoDictionary.Select(x => coinGeckoToString(x.Value)));

                foreach (var (key, coinGecko) in coinGeckoDictionary)
                {
                    if (!universeData.ContainsKey(key))
                    {
                        universeData[key] = new List<string>();
                    }

                    universeData[key].Add($"{coinGeckoItem.Symbol.ToUpperInvariant()},{coinGecko.Price},{coinGecko.Volume},{coinGecko.MarketCap}");
                }

                Log.Trace($"CoinGeckoUniverseDataDownloader.Run(): Processed: {coinGeckoItem}");
            }

            foreach (var (date, content) in universeData)
            {
                SaveContentToFile(_universeFolder, _processedUniverseFolder, $"{date:yyyyMMdd}", content);
            }

            Log.Trace($"CoinGeckoUniverseDataDownloader.Run(): Finished in {stopwatch.Elapsed.ToStringInvariant(null)}");
            return true;
        }

        /// <summary>
        /// Gets the list of crypto currencies supported by QuantConnect
        /// </summary>
        /// <returns>List of crypto currencies supported by QuantConnect</returns>
        private static HashSet<string> GetSupportedCryptoCurrencies()
        {
            var coins = new HashSet<string>();

            var symbolPropertiesDatabase = SymbolPropertiesDatabase.FromDataFolder();

            var fiat = symbolPropertiesDatabase.GetSymbolPropertiesList(Market.Oanda, SecurityType.Forex)
                .Select(x => x.Value.QuoteCurrency.ToLowerInvariant()).ToHashSet();

            foreach (var symbolPropertiesFromMarket in Market.SupportedMarkets()
                         .Select(market => symbolPropertiesDatabase.GetSymbolPropertiesList(market, SecurityType.Crypto)))
            {
                foreach (var (key, symbolProperties) in symbolPropertiesFromMarket)
                {
                    var coin = key.Symbol.ToLowerInvariant();
                    var quote = symbolProperties.QuoteCurrency.ToLowerInvariant();
                    var end = coin.Length - quote.Length;
                    coin = coin[..end];

                    if (!fiat.Contains(coin)) coins.Add(coin);
                    if (!fiat.Contains(quote)) coins.Add(quote);
                }
            }

            return coins;
        }

        /// <summary>
        /// Get the list of coins supported by CoinGecko
        /// This method saves the data locally since we might need to rerun
        /// this data downloader and the rate limit of CoinGecko API is high 
        /// </summary>
        /// <returns>List of coins supported by CoinGecko</returns>
        private List<CoinGeckoItem> GetCoinGeckoList()
        {
            string value;

            if (File.Exists("list.json"))
            {
                value = File.ReadAllText("list.json");
            }
            else
            {
                value = HttpRequester("list").Result;
                File.WriteAllText("list.json", value);
            }

            // Remove Wormhole duplicates
            var coinGeckoList = JsonConvert.DeserializeObject<List<CoinGeckoItem>>(value)
                .GroupBy(x => x.Symbol)
                .SelectMany(x => x.Where(y => !y.Name.ToLowerInvariant().Contains("wormhole")))
                .ToList();

            return coinGeckoList;
        }

        /// <summary>
        /// Gets the data for a given coin Id
        /// This method saves the data locally since we might need to rerun
        /// this data downloader and the rate limit of CoinGecko API is high 
        /// </summary>
        /// <param name="id">Coin Id</param>
        /// <param name="exists">is the processed file exists</param>
        /// <returns>Data from the MarketCharts endpoint for a given Id</returns>
        private CoinGeckoMarketChart GetCoinGeckoMarketChartsForId(string id, bool exists)
        {
            string value;
            if (File.Exists($"{id}.json"))
            {
                value = File.ReadAllText($"{id}.json");
            }
            else
            {
                var days = exists ? "1" : "max";    // get all data available by "max" if not exist
                var url = $"{id}/market_chart?vs_currency=usd&days={days}&interval=daily";
                value = HttpRequester(url).Result;
                File.WriteAllText($"{id}.json", value);
            }

            return JsonConvert.DeserializeObject<CoinGeckoMarketChart>(value);
        }

        /// <summary>
        /// Sends a GET request for the provided URL
        /// </summary>
        /// <param name="url">URL to send GET request for</param>
        /// <returns>Content as string</returns>
        /// <exception cref="Exception">Failed to get data after exceeding retries</exception>
        private async Task<string> HttpRequester(string url)
        {
            for (var retries = 1; retries <= _maxRetries; retries++)
            {
                try
                {
                    // Makes sure we don't overrun Quiver rate limits accidentally
                    _indexGate.WaitToProceed();

                    var response = await _client.GetAsync(Uri.EscapeUriString(url));
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        Log.Error($"CoinGeckoUniverseDataDownloader.HttpRequester(): Files not found at url: {Uri.EscapeUriString(url)}");
                        response.DisposeSafely();
                        return string.Empty;
                    }

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        var finalRequestUri = response.RequestMessage?.RequestUri; // contains the final location after following the redirect.
                        response = _client.GetAsync(finalRequestUri).Result; // Reissue the request. The DefaultRequestHeaders configured on the client will be used, so we don't have to set them again.
                    }

                    response.EnsureSuccessStatusCode();

                    var result =  await response.Content.ReadAsStringAsync();
                    response.DisposeSafely();

                    return result;
                }
                catch (Exception e)
                {
                    Log.Error(e, $"CoinGeckoUniverseDataDownloader.HttpRequester(): Error at HttpRequester. (retry {retries}/{_maxRetries})");
                    Thread.Sleep(1000);
                }
            }

            throw new Exception($"Request failed with no more retries remaining (retry {_maxRetries}/{_maxRetries})");
        }

        /// <summary>
        /// Saves contents to disk, deleting existing zip files
        /// </summary>
        /// <param name="destinationFolder">Final destination of the data</param>
        /// <param name="processedFolder">Processed data folder</param>
        /// <param name="name">file name</param>
        /// <param name="contents">Contents to write</param>
        private static void SaveContentToFile(string destinationFolder, string processedFolder, string name, IEnumerable<string> contents)
        {
            name = $"{name.ToLowerInvariant()}.csv";
            var isUniverse = destinationFolder.Contains("universe");

            var filePath = isUniverse
                ? Path.Combine(processedFolder, "universe", name)
                : Path.Combine(processedFolder, name);

            var lines = new HashSet<string>(contents);
            if (File.Exists(filePath))
            {
                foreach (var line in File.ReadAllLines(filePath))
                {
                    lines.Add(line);
                }
            }

            var finalLines = isUniverse
                ? lines.OrderBy(x => x.Split(',').First()).ToList()
                : lines.OrderBy(x => DateTime.ParseExact(x.Split(',').First(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal)).ToList();

            var finalPath = Path.Combine(destinationFolder, name);
            File.WriteAllLines(finalPath, finalLines);
        }
        
        /// <summary>
        /// Disposes of unmanaged resources
        /// </summary>
        public void Dispose()
        {
            _indexGate?.Dispose();
            _client?.Dispose();
        }

        /// <summary>
        /// Represents items of the list of coins supported by CoinGecko
        /// https://api.coingecko.com/api/v3/coins/list
        /// </summary>
        private class CoinGeckoItem
        {
            [JsonProperty("id")]
            public string Id { get; set; }
            
            [JsonProperty("symbol")]
            public string Symbol { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            public override string ToString() => $"{Id},{Symbol},{Name}";
        }

        /// <summary>
        /// Represents the data from the Market Chart endpoint
        /// This data needs to be manipulated to create a list of <see cref="CoinGecko"/>
        /// </summary>
        private class CoinGeckoMarketChart
        {
            [JsonProperty("prices")]
            public List<List<double?>> Prices { get; set; }

            [JsonProperty("market_caps")]
            public List<List<double?>> MarketCaps { get; set; }

            [JsonProperty("total_volumes")]
            public List<List<double?>> TotalVolumes { get; set; }

            public Dictionary<DateTime, CoinGecko> GetCoinGeckoDictionary()
            {
                var coinGeckoDictionary = new Dictionary<DateTime, CoinGecko>();

                foreach (var marketCap in MarketCaps)
                {
                    if (!marketCap[0].HasValue) continue;
                    var endTime = Time.UnixTimeStampToDateTime(marketCap[0].Value / 1000d);

                    // Only record midnight data and discard data before 2013
                    if (endTime.TimeOfDay > TimeSpan.Zero || endTime.Year < 2013) continue; 

                    coinGeckoDictionary[endTime] = new CoinGecko
                    {
                        EndTime = endTime,
                        MarketCap = (marketCap[1] ?? 0).SafeDecimalCast()
                    };
                }

                foreach (var price in Prices)
                {
                    if (!price[0].HasValue) continue;
                    var endTime = Time.UnixTimeStampToDateTime(price[0].Value / 1000d);

                    if (coinGeckoDictionary.TryGetValue(endTime, out var coinGecko))
                    {
                        coinGecko.Value = (price[1] ?? 0).SafeDecimalCast();
                    }
                }

                foreach (var totalVolume in TotalVolumes)
                {
                    if (!totalVolume[0].HasValue) continue;
                    var endTime = Time.UnixTimeStampToDateTime(totalVolume[0].Value / 1000d);

                    if (coinGeckoDictionary.TryGetValue(endTime, out var coinGecko))
                    {
                        coinGecko.Volume = (totalVolume[1] ?? 0).SafeDecimalCast();
                    }
                }

                return coinGeckoDictionary;
            }
        }
    }
}
