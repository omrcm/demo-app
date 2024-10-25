using BOA.Common.Types;
using BOA.Proxy;
using BOA.Types.InternetBanking.FX;
using System.Collections.Generic;

namespace VK.Fx.DataFeed.BoaProxy
{
    public class StockCustomerBoaService : IStockCustomerBoaService
    {

        private IBoaClient boaClient;
        public StockCustomerBoaService(IBoaClient boaClient)
        {
            this.boaClient = boaClient;
        }


        public GenericResponse<StockCustomerContract> GetStockCustomer(int CustomerId)
        {
            FXStockCustomerRequest request = new FXStockCustomerRequest
            {
                MethodName = "GetStockCustomer",
                CustomerId = CustomerId,
            };
            var resp = boaClient.Execute<FXStockCustomerRequest, StockCustomerContract>(request);
            return resp;
        }

    }
}
