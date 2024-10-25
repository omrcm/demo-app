using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using VK.Emar.Modularity;
using VK.Fx.DataFeed.ApiHost;

namespace VK.Emar.Extensions.DependencyInjection
{
    [DependsOn(
    typeof(EmarAspNetCoreMvcModule),
    typeof(FxDataFeedApplicationModule),
    //typeof(EmarAspNetCoreDocsModule),
    typeof(EmarAspNetCoreHealthCheckModule),
    typeof(EmarLoggingModule),
    typeof(EmarAspnetCoreAuthenticationIdentityServerModule),
    typeof(EmarHealthCheckRabbitMQModule),
    typeof(EmarHealthCheckRedisModule),
        typeof(EmarBackgroundWorkersModule),
        typeof(EmarDistributedLockRedisModule)
    )]
    public class FxDataFeedApiHostModule : EmarModule
    {
        public override void ConfigureServices(IEmarServiceBuilder builder)
        {
            builder.ConfigureIdentityResource(identityClientBuilder =>
            {

            });
            var _configuration = builder.GetConfiguration();
            var rabbitHostName = _configuration.GetSection("Emar:RabbitMQ:Connections:Default:HostName").Value;
            var rabbitPort = _configuration.GetSection("Emar:RabbitMQ:Connections:Default:Port").Value;
            var rabbitUserName = _configuration.GetSection("Emar:RabbitMQ:Connections:Default:UserName").Value;
            var rabbitPassword = _configuration.GetSection("Emar:RabbitMQ:Connections:Default:Password").Value;
            string conn2 = string.Format("amqp://{0}:{1}@{2}:{3}", rabbitUserName, rabbitPassword, rabbitHostName, rabbitPort);
            builder.ConfigureHealthCheck(_healthCheckBuilder =>
            {
                //string conn = "amqp://rabbitmq-user:8QxVvH5QWFk62dJ@rabbitmq-infra.fx-emar-ops.svc.cluster.local:5672";
                _healthCheckBuilder.AddRabbitMQ(rabbitConnectionString: conn2 );
            });
            builder.ConfigureMapping(builder => builder.ConfigureAutoMapper(Assembly.GetExecutingAssembly()));
            builder.AddBackgroundWorker<PeriodicBackgroundWorker>();
            //builder.Services.AddHostedService<HostedServiceDataFeed>();
        }

        public override void Configure(IEmarApplicationBuilder builder)
        {
            base.Configure(builder);
        }
    }
}
