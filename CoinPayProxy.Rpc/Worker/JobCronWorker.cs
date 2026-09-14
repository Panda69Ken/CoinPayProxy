using CoinPayProxy.Rpc.Core.Service;
using Quartz;
using Quartz.Impl;
using System.Collections.Specialized;
using System.Reflection;

namespace CoinPayProxy.Rpc.Worker
{
    public class JobCronWorker(ILogger<JobCronWorker> logger,
        IServiceProvider serviceProvider,
        IConfigService config) : BackgroundService
    {
        private readonly ILogger<JobCronWorker> _logger = logger;
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly IConfigService _config = config;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new StdSchedulerFactory(new NameValueCollection
            {
                { "quartz.serializer.type", "binary" }
            });

            var scheduler = await factory.GetScheduler();

            scheduler.JobFactory = new JobFactory(_serviceProvider);

            await scheduler.Start();

            var assembly = Assembly.Load("CoinPayProxy.Rpc");

            var entityTypes = assembly.GetTypes().Where(p => p.GetInterfaces().Contains(typeof(IJob)));

            foreach (var type in entityTypes)
            {
                var className = type.Name;

                if (_config.JobCrons.Any(m => m.Code == className))
                {
                    var jobCron = _config.JobCrons.FirstOrDefault(a => a.Code == className);

                    var job = JobBuilder.Create(type)
                        .WithIdentity($"job_{className}", $"group_{className}")
                        .Build();

                    var trigger = TriggerBuilder.Create()
                        .WithIdentity($"trigger_{className}", $"group_{className}")
                        .StartNow()
                        .WithCronSchedule(jobCron.Cron, cron =>
                        {
                            cron.InTimeZone(TimeZoneInfo.Utc);
                        })
                        .Build();

                    _logger.LogInformation(className + "开启任务");

                    await scheduler.ScheduleJob(job, trigger);
                }
            }

        }
    }
}
