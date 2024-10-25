using BOA.Types.Kernel.FX;
using System;
using System.Collections.Generic;

namespace VK.Fx.DataFeed.Domain.Shared
{
    public class FXSymbolGroupDto
    {
        public int Id { get; set; }
        public int GroupId { get; set; }

        public string GroupCode { get; set; }

        public BOA.Types.Kernel.FX.FXSpecificBranchRateGroupContract GroupAdvantage { get; set; }

        public List<FXSymbolRateDto> FxRateList { get; set; }
    }

    public class FXSymbolBaseGroupDto
    {
        public int GroupId { get; set; }

        public string GroupCode { get; set; }

        public List<FXSymbolBaseRateDto> FxRateList { get; set; }
    }
    public class FXSymbolBaseRateDto
    {
        public string UniqueKey { get; set; }

        public short BaseFECId { get; set; }

        public short Fec1 { get; set; }

        public short Fec2 { get; set; }

        public decimal Buy { get; set; }

        public decimal Sell { get; set; }
        public string FecName { get; set; }
    }
    public class FXSymbolRateDto
    {
        public string UniqueKey { get; set; }

        public short BaseFECId { get; set; }

        public decimal BaseFECTLRate { get; set; }

        public decimal OtherFECTLRate { get; set; }

        public short Fec1 { get; set; }

        public short Fec2 { get; set; }

        public decimal Buy { get; set; }

        public decimal Sell { get; set; }

        public decimal BuyBranchCost { get; set; }

        public decimal SellBranchCost { get; set; }

        public FXComponentContract BuyFullRate { get; set; }

        public FXComponentContract SellFullRate { get; set; }
        public string FecName { get; set; }
    }
}
