using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VK.Fx.DataFeed.Domain.Shared
{
    public class FXSymbolDto
    {
        //remove prop.
        //public int ID { get; set; }
        public string UniqueKey { get; set; }
        public int Fec1 { get; set; } //USD 1
        public int Fec2 { get; set; } //TRY 0 or EUR 19
                                      //   public string Name { get; set; } // USD/TRY   ISOCode ISOCode
        public decimal Bid { get; set; } //Fec1 bid 7.88 // realtime data besle //satış
        public decimal Ask { get; set; } //Fec2 bid 7.98 // realtime data besle //alış
        //public decimal StartBid { get; set; } //Fec 1 in Fec2 ye göre Günün İlk Satış Kuru 
        //public decimal EndBid { get; set; } //Fec 1 in Fec2 ye göre Bir Önceki Gün Son Satış Kuru
        //public decimal Low { get; set; } // Fec 1 in Fec2 ye göre Gün içerisindeki Min  Satış Değerleri
        //public decimal High { get; set; } //Fec 1 in Fec2 ye göre Gün içerisindeki  Max Satış Değerleri
        //public decimal WeeklyLow { get; set; } //Fec 1 in Fec2 ye göre hafta içerisindeki Min Satış Değerleri
        //public decimal WeeklyHigh { get; set; } //Fec 1 in Fec2 ye göre hafta içerisindeki  Max Satış Değerleri
        ////public decimal MonthlyLow { get; set; } //Fec 1 in Fec2 ye göre Aylık içerisindeki Min Satış Değerleri
        ////public decimal MonthlyHigh { get; set; } //Fec 1 in Fec2 ye göre Aylık içerisindeki  Max Satış Değerleri
        //public decimal TotalBid { get; set; } // ?? Fec 2 den Fec1 nin Alış İşlemleri Toplamı (Fecbid:Fec2) 
        //public decimal TotalAsk { get; set; } // ?? 
        //public decimal PercentBid { get; set; } // ?? 
        //public decimal PercentAsk { get; set; } //??
        //public decimal Volume { get; set; } // ??
        public string CreateDate { get; set; } // datetime now. 
        public decimal BaseFECTLRate { get; set; } // Base kurun tl karşılıgı
        public decimal OtherFECTLRate { get; set; } // Diğer kurun tl karşılıgı
        // public decimal? PercentageChange { get; set; }
    }
}
