using BOA.Types.InternetBanking.FX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BOA.Common.Types;
namespace VK.Fx.DataFeed.Application.Contract
{
    public interface ICustomerService
    {
        public GenericResponse<StockCustomerContract> GetStockCustomer(int customerId);
    }
}
