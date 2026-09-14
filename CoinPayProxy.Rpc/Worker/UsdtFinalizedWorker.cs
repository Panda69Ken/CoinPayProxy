using CoinPayProxy.Domain.DomainService;
using CoinPayProxy.Domain.QueryServices;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Rpc.Core.Block;
using CoinPayProxy.Rpc.Core.MR;
using CoinPayProxy.Rpc.Core.Service;
using MediatR;

namespace CoinPayProxy.Rpc.Worker
{
    //转U订单状态确认
    public class UsdtFinalizedWorker(ILogger<UsdtFinalizedWorker> logger,
        IMediator mediator,
        IConfigService config,
        IServiceScopeFactory scopeFactory,
        UsdtFinalizedBlock usdtFinalizedBlock) : BackgroundService
    {
        private readonly ILogger<UsdtFinalizedWorker> _logger = logger;
        private readonly IMediator _mediator = mediator;
        private readonly IConfigService _config = config;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly UsdtFinalizedBlock _usdtFinalizedBlock = usdtFinalizedBlock;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("UsdtFinalizedWorker running at: {time}", DateTimeOffset.Now);

            var types = new List<AgreementTypeEnum>();
            try
            {
                foreach (AgreementTypeEnum @enum in Enum.GetValues(typeof(AgreementTypeEnum)))
                {
                    if (@enum == AgreementTypeEnum.None) continue;

                    types.Add(@enum);
                }

                var workerTasks = new List<Task>();

                foreach (var @enum in types)
                {
                    foreach (var node in _config.NodeList)
                    {
                        workerTasks.Add(Task.Run(async () =>
                        {
                            var interval = 2000;

                            using var scope = _scopeFactory.CreateScope();
                            var queries = scope.ServiceProvider.GetRequiredService<BlockchainQueries>();
                            var blockNumberDomain = scope.ServiceProvider.GetRequiredService<BlockNumberDomainService>();

                            while (!stoppingToken.IsCancellationRequested)
                            {
                                try
                                {
                                    var list = await queries.GetTransferUsdts(@enum);

                                    foreach (var item in list)
                                    {
                                        var (status, blockNumber, fee, feeCurrency) = await _mediator.Send(new TransferVerifyCommand
                                        {
                                            HashId = item.HashId,
                                            AgreementType = item.AgreementType
                                        });

                                        if (status > TransactionStatusEnum.Processing)
                                        {
                                            _usdtFinalizedBlock.Post(new UsdtFinalizedModel
                                            {
                                                Type = @enum,
                                                HashId = item.HashId,
                                                Status = status,
                                                Fee = fee,
                                                FeeCurrency = feeCurrency,
                                                BlockNumber = blockNumber
                                            });
                                        }
                                    }
                                }
                                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                                {
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"处理USDT交易记录异常,{@enum},{node}--error:{ex.Message}");
                                }

                                try
                                {
                                    await Task.Delay(interval * 2, stoppingToken);
                                }
                                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                                {
                                    break;
                                }
                            }
                        }, stoppingToken));
                    }
                }

            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError($"UsdtFinalizedWorker 初始化错误: {ex.Message}");
            }
        }

    }
}
