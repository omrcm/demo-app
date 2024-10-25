using System;

namespace VK.Fx.DataFeed.Application.Contract
{
    public enum FXIndexContract
    {
        TellerPrice = 0,
        MarketPrice = 2,
        CentralBankPrice = 3,
        InterBankPrice = 4,
        BanksPrice = 5,
        SmallAmountRate = 7,
        RateReasonability = 10,
        MarketData = 101,
        FxOnlineMarketData = 201,
        FxOnlineMarketPrice = 202
    }
}
