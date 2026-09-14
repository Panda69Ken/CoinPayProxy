using CoinPayProxy.Domain.DomainService;
using CoinPayProxy.Domain.QueryServices;
using CoinPayProxy.Infrastructure.Extensions;
using CoinPayProxy.Rpc.Core.Block;
using CoinPayProxy.Rpc.Core.Service;
using CoinPayProxy.Rpc.Services;
using CoinPayProxy.Rpc.Worker;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

builder.RegisterServices();

builder.Services.AddScoped<BlockchainQueries>();
builder.Services.AddScoped<BlockNumberDomainService>();
builder.Services.AddScoped<WalletAddressDomainService>();
builder.Services.AddScoped<TransactionRecordDomainService>();

builder.Services.AddSingleton<IConfigService, ConfigService>();
builder.Services.AddSingleton<ICacheService, CacheService>();

builder.Services.AddSingleton<EvmTransferBlock>();
builder.Services.AddSingleton<TronTransferBlock>();
builder.Services.AddSingleton<UsdtFinalizedBlock>();

var assembly = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()
      .Where(t => t.GetInterfaces().Contains(typeof(IJob)))).ToArray();
foreach (var item in assembly)
{
    builder.Services.AddSingleton(item);
}

builder.Services.AddHostedService<ScanBlockWorker>();   //扫区块记录转U记录
builder.Services.AddHostedService<UsdtFinalizedWorker>();   //转U记录状态确认
builder.Services.AddHostedService<SweepingWorker>();    //后台处理批量归集的地址
builder.Services.AddHostedService<JobCronWorker>(); //归集记录状态确认

// Add services to the container.
builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<CoinPayProxyerService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
