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
 *
*/

using System;
using NodaTime;
using System.IO;
using QuantConnect.Data;
using System.Collections.Generic;
using System.Globalization;

namespace QuantConnect.DataSource
{
    /// <summary>
    /// Coin Gecko data which contains Price, Volume, and Market Cap in USD for cryptocurrencies
    /// </summary>
    public class CoinGecko : BaseData
    {
        private static readonly TimeSpan _period = TimeSpan.FromDays(1);

        /// <summary>
        /// Coin Name
        /// </summary>
        public string Coin => Symbol.Value;

        /// <summary>
        /// Volume in USD of the coin for that day
        /// </summary>
        public decimal Volume { get; set; }

        /// <summary>
        /// Market Cap in USD of the coin for that day
        /// </summary>
        public decimal MarketCap { get; set; }

        /// <summary>
        /// Time the data became available
        /// </summary>
        public override DateTime EndTime
        {
            get => Time + _period; 
            set => Time = value - _period;
        }

        /// <summary>
        /// Return the URL string source of the file. This will be converted to a stream
        /// </summary>
        /// <param name="config">Configuration object</param>
        /// <param name="date">Date of this source file</param>
        /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
        /// <returns>String URL of source file.</returns>
        public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            return new SubscriptionDataSource(
                Path.Combine(
                    Globals.DataFolder,
                    "alternative",
                    "coingecko",
                    $"{config.Symbol.Value.ToLowerInvariant()}.csv"
                ),
                SubscriptionTransportMedium.LocalFile
            );
        }

        /// <summary>
        /// Parses the data from the line provided and loads it into LEAN
        /// </summary>
        /// <param name="config">Subscription configuration</param>
        /// <param name="line">Line of data</param>
        /// <param name="date">Date</param>
        /// <param name="isLiveMode">Is live mode</param>
        /// <returns>New instance</returns>
        public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        {
            var csv = line.Split(',');

            return new CoinGecko
            {
                Symbol = config.Symbol,
                EndTime = Parse.DateTimeExact(csv[0], "yyyyMMdd"),
                Value = decimal.Parse(csv[1], NumberStyles.Any, CultureInfo.InvariantCulture),
                Volume = decimal.Parse(csv[2], NumberStyles.Any, CultureInfo.InvariantCulture),
                MarketCap = decimal.Parse(csv[3], NumberStyles.Any, CultureInfo.InvariantCulture)
            };
        }

        /// <summary>
        /// Clones the data
        /// </summary>
        /// <returns>A clone of the object</returns>
        public override BaseData Clone()
        {
            return new CoinGecko
            {
                Symbol = Symbol,
                Time = Time,
                EndTime = EndTime,
                MarketCap = MarketCap,
                Value = Value,
                Volume = Volume
            };
        }

        /// <summary>
        /// Creates a Symbol object for a given market and quote currency
        /// </summary>
        /// <param name="market">The market the ticker resides in</param>
        /// <param name="quoteCurrency">The quote currency of the crypto-currency pair. E.g. USD for BTCUSD</param>
        /// <param name="securityType">The security type of the ticker resides in</param>
        /// <returns>A new Symbol object for the specified ticker</returns>
        public Symbol CreateSymbol(string market, string quoteCurrency = "USD", SecurityType securityType = SecurityType.Crypto)
        {
            return Symbol.Create($"{Coin}{quoteCurrency}", securityType, market);
        }

        /// <summary>
        /// Indicates whether the data source is tied to an underlying symbol and requires that corporate events be applied to it as well, such as renames and delistings
        /// </summary>
        /// <returns>false</returns>
        public override bool RequiresMapping() => false;

        /// <summary>
        /// Indicates whether the data is sparse.
        /// If true, we disable logging for missing files
        /// </summary>
        /// <returns>true</returns>
        public override bool IsSparseData() => false;

        /// <summary>
        /// Converts the instance to string
        /// </summary>
        public override string ToString() => $"{Coin} Market Cap: {MarketCap} Price: {Price} Volume: {Volume}";

        /// <summary>
        /// Gets the default resolution for this data and security type
        /// </summary>
        public override Resolution DefaultResolution() => Resolution.Daily;

        /// <summary>
        /// Gets the supported resolution for this data and security type
        /// </summary>
        public override List<Resolution> SupportedResolutions() => DailyResolution;

        /// <summary>
        /// Specifies the data time zone for this data type. This is useful for custom data types
        /// </summary>
        /// <returns>The <see cref="T:NodaTime.DateTimeZone" /> of this data type</returns>
        public override DateTimeZone DataTimeZone() => DateTimeZone.Utc;
    }
}