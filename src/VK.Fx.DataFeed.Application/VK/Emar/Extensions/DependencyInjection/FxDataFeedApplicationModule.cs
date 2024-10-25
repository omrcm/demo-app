using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VK.Emar.Extensions.DependencyInjection;
using VK.Emar.Modularity;
using VK.Fx.DataFeed.Application;
using VK.Fx.DataFeed.Application.Contract;

namespace VK.Emar.Extensions.DependencyInjection
{
    [DependsOn(
   typeof(EmarApplicationModule),
   typeof(EmarCachingModule),
       typeof(EmarJsonModule),
        typeof(FxExchangeBoaProxyModule),
        typeof(FxExchangeApplicationContractModule),
        typeof(EmarCachingRedisModule),
        typeof(EmarMappingAutoMapperModule),
        typeof(EmarJsonNewtonsoftModule)
   )]
    public class FxDataFeedApplicationModule : EmarModule
    {

        public override void ConfigureServices(IEmarServiceBuilder builder)
        {
            builder.ConfigureMapping(builder => builder.ConfigureAutoMapper(Assembly.GetExecutingAssembly()));
            builder.TryAddTransient<IRabbitService, RabbitService>();
            builder.TryAddTransient<IFxOnlineService, FxOnlineService>();
        }
    }

}
