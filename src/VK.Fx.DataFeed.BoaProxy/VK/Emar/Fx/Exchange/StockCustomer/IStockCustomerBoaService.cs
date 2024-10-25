using BOA.Common.Types;
using BOA.Types.InternetBanking.FX;
using System.Collections.Generic;

namespace VK.Fx.DataFeed.BoaProxy
{
    public interface IStockCustomerBoaService
    {
         GenericResponse<StockCustomerContract> GetStockCustomer(int CustomerId);
    }
}
