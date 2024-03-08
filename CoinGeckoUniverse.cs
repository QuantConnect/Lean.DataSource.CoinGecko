﻿/*
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

using NodaTime;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using System;
using System.Globalization;
using System.IO;

namespace QuantConnect.DataSource
{
    /// <summary>
    /// Universe Selection Data for Coin Gecko data which contains Price, Volume, and Market Cap in USD for cryptocurrencies
    /// </summary>
    public class CoinGeckoUniverse : BaseDataCollection
    {
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
                    "universe",
                    $"{date.ToStringInvariant(DateFormat.EightCharacter)}.csv"
                ),
                SubscriptionTransportMedium.LocalFile,
                FileFormat.FoldingCollection
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
            var coin = csv[0].ToUpperInvariant();
            var sid = SecurityIdentifier.GenerateBase(typeof(CoinGecko), coin, Market.USA);

            return new CoinGecko
            {
                Symbol = new Symbol(sid, coin),
                EndTime = date,
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
            return new CoinGeckoUniverse
            {
                Symbol = Symbol,
                Time = Time,
                EndTime = EndTime,
                Data = Data
            };
        }

        /// <summary>
        /// Specifies the data time zone for this data type. This is useful for custom data types
        /// </summary>
        /// <returns>The <see cref="T:NodaTime.DateTimeZone" /> of this data type</returns>
        public override DateTimeZone DataTimeZone() => DateTimeZone.Utc;
    }
}
