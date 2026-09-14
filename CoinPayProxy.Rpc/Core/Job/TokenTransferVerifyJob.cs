using CoinPayProxy.Domain.DomainService;
using CoinPayProxy.Domain.QueryServices;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Infrastructure.Extensions;
using CoinPayProxy.Rpc.Core.MR;
using MediatR;
using Quartz;

namespace CoinPayProxy.Rpc.Core.Job
{
    //Token记录状态检验
    [DisallowConcurrentExecution]
    public class TokenTransferVerifyJob(ILogger<TokenTransferVerifyJob> logger,
        IMediator mediator,
        IServiceScopeFactory scopeFactory) : IJob
    {
        private readonly ILogger<TokenTransferVerifyJob> _logger = logger;
        private readonly IMediator _mediator = mediator;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var queries = scope.ServiceProvider.GetRequiredService<BlockchainQueries>();
                var transactionRecord = scope.ServiceProvider.GetRequiredService<TransactionRecordDomainService>();

                var transactions = await queries.GetTransferTokens();

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
                        _logger.LogWarning($"Token交易订单未完成,HashId:{item.HashId},Status:{status}");
                        continue;
                    }

                    //tron的token交易当宽带不够也是要消耗token的
                    //EVM的token交易也会消耗token
                    var result = await transactionRecord.UpdateStatusAndFee(item.Id, status, fee, fee > 0 ? feeCurrency : "", blockNumber);

                    if (result != ErrorCodeEnum.成功)
                        _logger.LogWarning($"更新Token交易订单状态失败,param:{item.ToJson()},status:{status}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理Token交易订单通知异常,Error:{ex.Message}");
            }
        }
    }
}
