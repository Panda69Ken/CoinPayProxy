using CoinPayProxy.Domain.DomainService;
using CoinPayProxy.Domain.QueryServices;
using CoinPayProxy.Infrastructure.Extensions;
using CoinPayProxy.Rpc.Core.MR;
using MediatR;

namespace CoinPayProxy.Rpc.Worker
{
    //后台处理批量归集的地址
    public class SweepingWorker(ILogger<SweepingWorker> logger,
        IMediator mediator,
        IServiceScopeFactory scopeFactory) : BackgroundService
    {
        private readonly ILogger<SweepingWorker> _logger = logger;
        private readonly IMediator _mediator = mediator;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SweepingWorker running at: {time}", DateTimeOffset.Now);

            var interval = 1000;

            using var scope = _scopeFactory.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<BlockchainQueries>();
            var walletAddress = scope.ServiceProvider.GetRequiredService<WalletAddressDomainService>();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var list = await queries.GetSweepingAddress();

                    foreach (var item in list)
                    {
                        var wallet = await queries.GetMemberWallet(item.MemberId, item.AgreementType);

                        if (wallet == null) continue;

                        var (result, msg) = await _mediator.Send(new UsdtTransferCommand
                        {
                            MemberId = item.MemberId,
                            Amount = wallet.UsdtBalance,
                            AgreementType = item.AgreementType,
                            SweepingAddress = item.SweepingAddress
                        }, stoppingToken);

                        if (result == false)
                        {
                            _logger.LogWarning($"USDT归集交易失败,msg:{msg},param:{item.ToJson()}");
                        }
                        else
                        {
                            _ = await walletAddress.UpdateSweepingStatus(item.MemberId, item.AgreementType, false, "");
                        }

                        await Task.Delay(interval * 2, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"USDT归集交易异常,error:{ex.Message}");
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
        }

    }
}
