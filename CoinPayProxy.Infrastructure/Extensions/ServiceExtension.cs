using CoinPayProxy.Infrastructure.Blockchain;
using CoinPayProxy.Infrastructure.Enums;
using FreeSql;
using FreeSql.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Web;
using Polly;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Loader;
using TronNet;

namespace CoinPayProxy.Infrastructure.Extensions
{
    public static class ServiceExtension
    {
        private static readonly List<Assembly> AssemblyList = [];

        static ServiceExtension()
        {
            //热插拔请求与处理解耦
            var types = new[] { "package", "referenceassembly" };
            var libs = DependencyContext.Default.CompileLibraries.Where(lib =>
                !lib.Serviceable && !types.Contains(lib.Type));

            foreach (var lib in libs)
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(lib.Name));
                AssemblyList.Add(assembly);
            }
        }

        public static void RegisterServices(this IHostApplicationBuilder hostBuilder)
        {
            hostBuilder.Configuration();

            hostBuilder.Logging();

            hostBuilder.Services();
        }

        static void Configuration(this IHostApplicationBuilder hostBuilder)
        {
            //if (hostBuilder.Environment.IsDevelopment())
            //{
            //    hostBuilder.Configuration.AddJsonFile($"appsettings.{hostBuilder.Environment.EnvironmentName}.json");
            //}
            //else
            //{
            hostBuilder.Configuration.AddJsonFile("appsettings.json");
            //}
        }

        static void Logging(this IHostApplicationBuilder hostBuilder)
        {
            if (hostBuilder.Environment.IsDevelopment())
            {
                hostBuilder.Logging
                    .AddFilter("System.Net.Http.HttpClient.*", Microsoft.Extensions.Logging.LogLevel.Warning)
                    .AddConsole().AddEventSourceLogger();
            }

            var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();

            //hostBuilder.Logging.ClearProviders();
            hostBuilder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            hostBuilder.UseNLog();
        }

        static void Services(this IHostApplicationBuilder hostBuilder)
        {
            hostBuilder.Services.AddMemoryCache();

            hostBuilder.Services.AddLogging();

            hostBuilder.Services.AddNetClient();

            hostBuilder.Services.AddFreeSql(hostBuilder.Environment.IsDevelopment());

            hostBuilder.Services.AddMediatR(hostBuilder.Configuration);

            hostBuilder.Services.AddBlockchain(hostBuilder.Configuration);
        }

        static void AddNetClient(this IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddHttpClient();
            services.AddHttpClient("ReTry", c =>  //重试
            {
                c.Timeout = new TimeSpan(0, 0, 10);
                c.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MaxAge = new TimeSpan(0),
                    MustRevalidate = true
                };
            })
           .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
           {
               AllowAutoRedirect = true,
               MaxConnectionsPerServer = 100
           })
            .AddTransientHttpErrorPolicy(b => b.WaitAndRetryAsync([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)]));
        }

        static void AddFreeSql(this IServiceCollection services, bool dev)
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, "data");
            if (!Directory.Exists(dbPath))
            {
                Directory.CreateDirectory(dbPath);
            }

            var fsql = new FreeSqlBuilder()
                  .UseConnectionString(DataType.Sqlite, $"Data Source={Path.Combine(dbPath, "app.db")};BusyTimeout=5000;Pooling=true;Cache=Shared")
                  //.UseAutoSyncStructure(true) // 自动建表
                  .UseNoneCommandParameter(true) // SQLite 提升性能
                  .UseLazyLoading(true)
                  .UseMonitorCommand(cmd =>
                  {
                      //if (dev) Console.WriteLine(cmd.CommandText);
                  }).Build();

            //自动建表
            fsql.CodeFirst.SyncStructure(GetTypesByNameSpace());

            services.AddSingleton(fsql);

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            //services.AddFreeRepository();
            services.AddFreeRepository(Assembly.Load("CoinPayProxy.Domain"));
        }

        //获取所有表
        static Type[] GetTypesByNameSpace()
        {
            List<Type> tableAssembies = [];
            List<string> entitiesFullName =
            [
                "CoinPayProxy.Domain.Aggregates"
            ];
            foreach (Type type in Assembly.Load("CoinPayProxy.Domain")!.GetExportedTypes())
                foreach (var fullname in entitiesFullName)
                    if (type.FullName!.StartsWith(fullname) && type.IsClass && type.GetCustomAttributes().Any(
                        x => x is not TableAttribute || x is TableAttribute attribute && !attribute.DisableSyncStructure))
                        tableAssembies.Add(type);

            return [.. tableAssembies];
        }

        static void AddMediatR(this IServiceCollection services, IConfiguration configuration)
        {
            var mediatorAssembly = new List<Assembly>();
            foreach (var assembly in AssemblyList)
            {
                if (assembly.ExportedTypes.Any(m =>
                    m.GetInterfaces().Any(i => i.FullName != null && (i.FullName.StartsWith("MediatR.IRequestHandler")
                    || i.FullName.StartsWith("MediatR.INotificationHandler")))))
                {
                    mediatorAssembly.Add(assembly);
                }
            }

            if (mediatorAssembly.Count != 0) services.AddMediatR(o =>
            {
                o.RegisterServicesFromAssemblies([.. mediatorAssembly]);

                var licenseKey = configuration.GetSection("MediatRKey").Value;
                if (string.IsNullOrEmpty(licenseKey) == false)
                {
                    o.LicenseKey = licenseKey;
                }
            });
        }

        static void AddBlockchain(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<MnemonicConfig>(configuration.GetSection("MnemonicConfig"));
            services.Configure<TronConfig>(configuration.GetSection("TronConfig"));

            services.AddTronNet(x =>
            {
                var tronConfig = configuration.GetSection("TronConfig").Get<TronConfig>();
                x.Network = TronNetwork.MainNet;
                x.Channel = new GrpcChannelOption { Host = "grpc.trongrid.io", Port = 50051 };
                x.SolidityChannel = new GrpcChannelOption { Host = "grpc.trongrid.io", Port = 50052 };
                x.ApiKeys = tronConfig.GridApiKeys;
            });
            services.AddSingleton<TronNetRecord>();

            services.Configure<EvmConfig>(configuration.GetSection("EVMConfig"));
            services.AddSingleton<Func<AgreementTypeEnum, InfuraNetRecord>>(sp => (agreementType) => 
                ActivatorUtilities.CreateInstance<InfuraNetRecord>(sp, agreementType));
        }

    }
}
