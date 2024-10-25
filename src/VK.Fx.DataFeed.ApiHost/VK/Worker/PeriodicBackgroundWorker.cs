using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using VK.Emar.BackgroundWorkers;
using VK.Emar.DistributedLock;
using VK.Emar.Extensions.DependencyInjection;
using VK.Fx.DataFeed.Application.Contract;

namespace VK.Fx.DataFeed.ApiHost
{
    public class PeriodicBackgroundWorker : AsyncPeriodicBackgroundWorkerBase
    {
        private readonly IFxOnlineService _fxOnlineService;
        private readonly ILogger<PeriodicBackgroundWorker> _logger;
        private readonly IDistributedLock _distributedLock;
        public PeriodicBackgroundWorker(ILazyServiceProvider lazyServiceProvider, IFxOnlineService fxOnlineService, IDistributedLock distributedLock, ILogger<PeriodicBackgroundWorker> logger) : base(lazyServiceProvider)
        {
            _logger = logger;
            _fxOnlineService = fxOnlineService;
            _distributedLock = distributedLock;
            Timer.RunOnStart = true;
            Timer.Period = (int)TimeSpan.FromSeconds(1).TotalMilliseconds; //1 seconds

        }
        protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
        {

            using (var handle = await _distributedLock.AcquireAsync("DataFeedDistributedLock", workerContext.CancellationToken))
            {
                string date = DateTime.Now.ToString();
                try
                {
                    _logger.LogInformation("Feed Start: [" + date + "] ");
                    await _fxOnlineService.GetSymbolFeedAsync();
                    _logger.LogInformation("Feed End: [" + DateTime.Now.ToString() + "] ");

                }
                catch (Exception ex)
                {

                    Logger.LogError("Error: [" + date + "] " + ex.Message);
                }
            }


        }
    }
}
