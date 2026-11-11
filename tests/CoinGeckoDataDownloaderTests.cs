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
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using QuantConnect.DataProcessing;

namespace QuantConnect.DataLibrary.Tests
{
    [TestFixture]
    public class CoinGeckoDataDownloaderTests
    {
        private string _root;
        private string _destination;
        private string _processed;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "coingecko-tests-" + Guid.NewGuid().ToString("N"));
            _destination = Path.Combine(_root, "output", "alternative", "coingecko");
            _processed = Path.Combine(_root, "processed", "alternative", "coingecko");
            Directory.CreateDirectory(Path.Combine(_destination, "universe"));
            Directory.CreateDirectory(Path.Combine(_processed, "universe"));
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, true);
        }

        [Test]
        public void RevisedValueReplacesTheStoredRowOfTheSameDate()
        {
            File.WriteAllLines(Path.Combine(_processed, "btc.csv"), new[]
            {
                "20260807,64000,20000000000,1290000000000",
                "20260808,64872.6007,21662714670.77,1301810026226.84"
            });

            CoinGeckoUniverseDataDownloader.SaveContentToFile(_destination, _processed, "btc", new[]
            {
                "20260808,64879.756,21708815799.29,1301953613966.19",
                "20260809,64916.0054,12371853387.29,1302709015809.28"
            });

            CollectionAssert.AreEqual(new[]
            {
                "20260807,64000,20000000000,1290000000000",
                "20260808,64879.756,21708815799.29,1301953613966.19",
                "20260809,64916.0054,12371853387.29,1302709015809.28"
            }, File.ReadAllLines(Path.Combine(_destination, "btc.csv")));
        }

        [Test]
        public void RevisedValueReplacesTheStoredUniverseRowOfTheSameTicker()
        {
            File.WriteAllLines(Path.Combine(_processed, "universe", "20260808.csv"), new[]
            {
                "BTC,64872.6007,21662714670.77,1301810026226.84",
                "ETH,3000,10000000000,360000000000"
            });

            CoinGeckoUniverseDataDownloader.SaveContentToFile(Path.Combine(_destination, "universe"), _processed, "20260808", new[]
            {
                "BTC,64879.756,21708815799.29,1301953613966.19",
                "ETH,3000,10000000000,360000000000"
            });

            CollectionAssert.AreEqual(new[]
            {
                "BTC,64879.756,21708815799.29,1301953613966.19",
                "ETH,3000,10000000000,360000000000"
            }, File.ReadAllLines(Path.Combine(_destination, "universe", "20260808.csv")));
        }

        // The previous merge put the fetched rows before the stored ones and sorted by date with a stable sort,
        // so a stored duplicate pair has the newest value first
        [Test]
        public void DuplicatesAlreadyStoredKeepTheNewestRow()
        {
            File.WriteAllLines(Path.Combine(_processed, "btc.csv"), new[]
            {
                "20260808,64879.756,21708815799.29,1301953613966.19",
                "20260808,64872.6007,21662714670.77,1301810026226.84"
            });

            CoinGeckoUniverseDataDownloader.SaveContentToFile(_destination, _processed, "btc", new[]
            {
                "20260809,64916.0054,12371853387.29,1302709015809.28"
            });

            CollectionAssert.AreEqual(new[]
            {
                "20260808,64879.756,21708815799.29,1301953613966.19",
                "20260809,64916.0054,12371853387.29,1302709015809.28"
            }, File.ReadAllLines(Path.Combine(_destination, "btc.csv")));
        }

        [Test]
        public void DuplicatesAlreadyStoredInTheUniverseKeepTheNewestRowOfSkippedCoins()
        {
            File.WriteAllLines(Path.Combine(_processed, "universe", "20260808.csv"), new[]
            {
                "BTC,64879.756,21708815799.29,1301953613966.19",
                "BTC,64872.6007,21662714670.77,1301810026226.84",
                "ETH,3000,10000000000,360000000000"
            });

            CoinGeckoUniverseDataDownloader.SaveContentToFile(Path.Combine(_destination, "universe"), _processed, "20260808", new[]
            {
                "ETH,3001,10000000000,360100000000"
            });

            CollectionAssert.AreEqual(new[]
            {
                "BTC,64879.756,21708815799.29,1301953613966.19",
                "ETH,3001,10000000000,360100000000"
            }, File.ReadAllLines(Path.Combine(_destination, "universe", "20260808.csv")));
        }

        [Test]
        public void FetchStartsAtTheLastStoredDateWhenTheJobMissedDays()
        {
            File.WriteAllLines(Path.Combine(_processed, "btc.csv"), new[]
            {
                "20250810,116510.08,27638785406.21,2318815857401.68",
                "20250811,119266.92,37157754754.85,2374097296837.89"
            });

            var start = CoinGeckoUniverseDataDownloader.GetFetchStart(Path.Combine(_processed, "btc.csv"), new DateTime(2026, 8, 3));

            Assert.AreEqual(new DateTime(2025, 8, 11), start);
        }

        [Test]
        public void FetchStartsAtTheProcessingDateWhenTheFileIsUpToDate()
        {
            File.WriteAllLines(Path.Combine(_processed, "btc.csv"), new[]
            {
                "20261104,100,1,1",
                "20261105,101,1,1"
            });

            var start = CoinGeckoUniverseDataDownloader.GetFetchStart(Path.Combine(_processed, "btc.csv"), new DateTime(2026, 11, 4));

            Assert.AreEqual(new DateTime(2026, 11, 4), start);
        }

        [Test]
        public void FetchHasNoStartWhenTheFileDoesNotExist()
        {
            var start = CoinGeckoUniverseDataDownloader.GetFetchStart(Path.Combine(_processed, "new.csv"), new DateTime(2026, 11, 4));

            Assert.IsNull(start);
        }
    }
}
