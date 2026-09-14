using CoinPayProxy.Domain.DomainService;
using CoinPayProxy.Domain.QueryServices;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Infrastructure.Extensions;
using CoinPayProxy.Rpc.Core.MR;
using MediatR;
using Quartz;

namespace CoinPayProxy.Rpc.Core.Job
{
    //归集记录状态检验
    [DisallowConcurrentExecution]
    public class UsdtSweepingVerifyJob(ILogger<UsdtSweepingVerifyJob> logger,
        IServiceScopeFactory scopeFactory,
        IMediator mediator) : IJob
    {
        private readonly ILogger<UsdtSweepingVerifyJob> _logger = logger;
        private readonly IMediator _mediator = mediator;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var queries = scope.ServiceProvider.GetRequiredService<BlockchainQueries>();
                var transactionRecord = scope.ServiceProvider.GetRequiredService<TransactionRecordDomainService>();

                var transactions = await queries.GetSweepingList();

                if (transactions == null || transactions.Count == 0) return;

                foreach (var item in transactions)
                {
                    var (status, blockNumber, fee, feeCurrency) = await _mediator.Send(new TransferVerifyCommand
                    {
                        HashId = item.HashId,
                        AgreementType = item.AgreementType
                    });

                    if (status == TransactionStatusEnum.None || status == TransactionStatusEnum.Processing)
                    {
                        _logger.LogWarning($"归集订单未完成,HashId:{item.HashId},Status:{status}");
                        continue;
                    }

                    //就算归集失败了，但是token还是需要消耗的
                    if (status == TransactionStatusEnum.Fail)
                        status = TransactionStatusEnum.SweepingFail;

                    //归集失败手续费也是要消耗的
                    var result = await transactionRecord.UpdateStatusAndFee(item.Id, status, fee, feeCurrency, blockNumber);

                    if (result != ErrorCodeEnum.成功)
                        _logger.LogWarning($"CollectMoneyJob--更新归集订单状态失败,param:{item.ToJson()},status:{status}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理归集订单通知异常,Error:{ex.Message}");
            }
        }
    }
}
