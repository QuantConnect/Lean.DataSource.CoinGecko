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

using QuantConnect.Data;
using QuantConnect.Util;
using QuantConnect.Orders;
using QuantConnect.Algorithm;
using QuantConnect.Indicators;
using QuantConnect.DataSource;

namespace QuantConnect.DataLibrary.Tests
{
    /// <summary>
    /// Example algorithm using the custom data type as a source of alpha
    /// </summary>
    public class CoinGeckoAlgorithm : QCAlgorithm
    {
        private Symbol _cryptoSymbol;
        private Symbol _customDataSymbol;
        private RollingWindow<CoinGecko> _window;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2018, 4, 4);  //Set Start Date
            SetEndDate(2018, 4, 6);    //Set End Date

            _cryptoSymbol = AddCrypto("BTCUSD").Symbol;
            _customDataSymbol = AddData<CoinGecko>("BTC").Symbol;
            _window = new RollingWindow<CoinGecko>(2);
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="slice">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice slice)
        {
            var data = slice.Get<CoinGecko>();
            if (!data.IsNullOrEmpty() && data.ContainsKey(_customDataSymbol))
            {
                _window.Add(data[_customDataSymbol]);
                if (_window.IsReady)
                {
                    return;
                }

                // Buy BTCUSD if the market cap of BTC is increasing
                if (_window[0].MarketCap > _window[1].MarketCap)
                {
                    SetHoldings(_cryptoSymbol, 1);
                }
                else
                {
                    SetHoldings(_cryptoSymbol, -1);
                }
            }
        }

        /// <summary>
        /// Order fill event handler. On an order fill update the resulting information is passed to this method.
        /// </summary>
        /// <param name="orderEvent">Order event details containing details of the events</param>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.Status.IsFill())
            {
                Debug($"Purchased Stock: {orderEvent.Symbol}");
            }
        }
    }
}
