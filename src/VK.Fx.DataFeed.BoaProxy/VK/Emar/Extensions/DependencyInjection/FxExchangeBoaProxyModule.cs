using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VK.Emar.Modularity;
using VK.Fx.DataFeed.BoaProxy;

namespace VK.Emar.Extensions.DependencyInjection
{
    [DependsOn(typeof(EmarBoaClientModule))]
    public class FxExchangeBoaProxyModule : EmarModule
    {
        public override void ConfigureServices(IEmarServiceBuilder builder)
        {
            builder.TryAddTransient<IStockCustomerBoaService, StockCustomerBoaService>();
            builder.TryAddTransient<IFxOnlineBoaService, FxOnlineBoaService>();
        }
    }
}
