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

using Newtonsoft.Json;
using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.DataSource;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.DataLibrary.Tests
{
    [TestFixture]
    public class CoinGeckoMarketCapUniverseTests
    {
        [Test]
        public void Selection()
        {
            var datum = CreateNewSelection();

            var expected = from d in datum.OfType<CoinGecko>()
                           where d.MarketCap > 7
                           select d.Symbol;
            var result = new List<Symbol> { Symbol.Create("BTC", SecurityType.Base, Market.USA) };

            AssertAreEqual(expected, result);
        }

        [Test]
        public void ReaderProducesCorrectSymbolAndTimeForUniverseHistory()
        {
            var universe = new CoinGeckoUniverse();
            var config = new SubscriptionDataConfig(
                typeof(CoinGeckoUniverse),
                Symbol.Create("CoinGeckoUniverse", SecurityType.Base, Market.USA),
                Resolution.Daily,
                TimeZones.Utc,
                TimeZones.Utc,
                false, false, false);
            var date = new DateTime(2025, 11, 6);

            var btc = (CoinGecko)universe.Reader(config, "BTC,50000,123456789,1000000000000", date, false);
            var eth = (CoinGecko)universe.Reader(config, "ETH,3000,987654321,500000000000", date, false);

            Assert.AreEqual(date, btc.Time);

            // must match the Crypto ID that CreateSymbol returns
            Assert.AreEqual(Symbol.Create("BTCUSD", SecurityType.Crypto, Market.Coinbase), btc.Symbol);

            // simulate the symbol filter applied during universe history selection
            var filteredSymbols = new HashSet<Symbol> { btc.CreateSymbol(Market.Coinbase) };
            var selected = new[] { btc, eth }.Where(x => filteredSymbols.Contains(x.Symbol)).ToList();
            Assert.AreEqual(1, selected.Count);
            Assert.AreEqual("BTC", selected[0].Coin);
        }

        private void AssertAreEqual(object expected, object result, bool filterByCustomAttributes = false)
        {
            foreach (var propertyInfo in expected.GetType().GetProperties())
            {
                // we skip Symbol which isn't protobuffed
                if (filterByCustomAttributes && propertyInfo.CustomAttributes.Count() != 0)
                {
                    Assert.AreEqual(propertyInfo.GetValue(expected), propertyInfo.GetValue(result));
                }
            }
            foreach (var fieldInfo in expected.GetType().GetFields())
            {
                Assert.AreEqual(fieldInfo.GetValue(expected), fieldInfo.GetValue(result));
            }
        }

        private BaseDataCollection CreateNewSelection()
        {
            return new BaseDataCollection(DateTime.Today, Symbol.None, new[]
            {
                new CoinGecko
                {
                    Symbol = Symbol.Create("BTC", SecurityType.Base, Market.USA),
                    Time = DateTime.Today,
                    Value = 10m,
                    MarketCap = 10m
                },
                new CoinGecko
                {
                    Symbol = Symbol.Create("ETH", SecurityType.Base, Market.USA),
                    Time = DateTime.Today,
                    Value = 5m,
                    MarketCap = 5m
                }
            });
        }
    }
}