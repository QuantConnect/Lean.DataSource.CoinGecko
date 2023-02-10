# QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
# Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

from AlgorithmImports import *

### <summary>
### Example algorithm using the custom data type as a source of alpha
### </summary>
class CoinGeckoAlgorithm(QCAlgorithm):
    def Initialize(self):

        self.SetStartDate(2018, 4, 4)   # Set Start Date
        self.SetEndDate(2018, 4, 6)    # Set End Date

        self.crypto_symbol = self.AddCrypto("BTCUSD").Symbol
        self.custom_data_symbol = self.AddData(CoinGecko, "BTC").Symbol
        self.window = RollingWindow[CoinGecko](2)

    def OnData(self, slice):
        data = slice.Get(CoinGecko)
        if data and self.custom_data_symbol in data:
            self.window.Add(data[self.custom_data_symbol])
            if not self.window.IsReady:
                return

            # Buy BTCUSD if the market cap of BTC is increasing
            if self.window[0].MarketCap > self.window[1].MarketCap:
                self.SetHoldings(self.crypto_symbol, 1)
            else:
                self.SetHoldings(self.crypto_symbol, -1)

    def OnOrderEvent(self, orderEvent):
        if orderEvent.Status == OrderStatus.Filled:
            self.Debug(f'Purchased Stock: {orderEvent.Symbol}')