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
class CoinGeckoUniverseSelectionModelAlgorithm(QCAlgorithm): 
    def Initialize(self):
        ''' Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized. '''

        # Data ADDED via universe selection is added with Daily resolution.
        self.UniverseSettings.Resolution = Resolution.Daily;

        self.SetStartDate(2018, 4, 4)   #Set Start Date
        self.SetEndDate(2018, 4, 6)     #Set End Date
        self.SetCash(100000);

        # add a custom universe data source
        self.SetUniverseSelection(CoinGeckoUniverseSelectionModel(self.UniverseSelection))

    def UniverseSelection(self, data):
        ''' Selected the securities
        
        :param List of MyCustomUniverseType data: List of MyCustomUniverseType
        :return: List of Symbol objects '''

        for datum in data:
            self.Log(f'{datum.Coin},{datum.MarketCap},{datum.Price}')

        # define our selection criteria
        selected = sorted(data, key=lambda x: x.MarketCap, reverse=True)[:3]
                    
        # Use the CreateSymbol method to generate the Symbol object for
        # the desired market (Coinbase) and quote currency (e.g. USD)
        return [x.CreateSymbol(Market.GDAX, "USD") for x in selected]
    
    def OnSecuritiesChanged(self, changes):
        self.Log(f'{changes}')