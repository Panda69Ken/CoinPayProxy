using CoinPayProxy.Domain.DomainService;
using CoinPayProxy.Domain.QueryServices;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Infrastructure.Extensions;
using System.Threading.Tasks.Dataflow;

namespace CoinPayProxy.Rpc.Core.Block
{
    public class UsdtFinalizedModel
    {
        public AgreementTypeEnum Type { get; set; }
        public string HashId { get; set; } = "";
        public TransactionStatusEnum Status { get; set; }
        public decimal Fee { get; set; }
        public string FeeCurrency { get; set; } = "";
        public long BlockNumber { get; set; }
    }

    //更新归集的USDT交易状态
    public class UsdtFinalizedBlock
    {
        readonly ActionBlock<UsdtFinalizedModel> _action;
        readonly ILogger<UsdtFinalizedBlock> _logger;
        readonly IServiceScopeFactory _scopeFactory;

        public UsdtFinalizedBlock(ILogger<UsdtFinalizedBlock> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;

            _action = new ActionBlock<UsdtFinalizedModel>(async (item) => { await Handler(item); });
        }

        public bool Post(UsdtFinalizedModel request)
        {
            return _action.Post(request);
        }

        private async Task Handler(UsdtFinalizedModel request)
        {
            try
            {
                if (request.Status == TransactionStatusEnum.None || request.Status == TransactionStatusEnum.Processing)
                {
                    _logger.LogWarning($"{request.Type}交易订单未完成,HashId:{request.HashId},Status:{request.Status}");
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var queries = scope.ServiceProvider.GetRequiredService<BlockchainQueries>();
                var transactionRecord = scope.ServiceProvider.GetRequiredService<TransactionRecordDomainService>();

                var transaction = await queries.GetTransactionRecord(request.HashId);
                if (transaction == null)
                {
                    _logger.LogWarning($"{request.Type}转U订单不存在,HashId:{request.HashId}");
                    return;
                }
                if (transaction.Status != TransactionStatusEnum.Processing)
                {
                    _logger.LogWarning($"{request.Type}转U订单已处理,HashId:{request.HashId},Status:{transaction.Status}");
                    return;
                }

                var result = await transactionRecord.UpdateStatusAndFee(transaction.Id, request.Status, request.Fee, request.FeeCurrency, request.BlockNumber);
                if (result != ErrorCodeEnum.成功)
                {
                    _logger.LogWarning($"{request.Type}更新转U订单失败,param:{request.ToJson()}");
                    return;
                }

                if (request.Status == TransactionStatusEnum.Success)
                {
                    //处理用户余额业务
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"UsdtFinalizedBlock--{request.Type}处理转U订单状态异常,param:{request.ToJson()},error:{ex.Message}");
            }
        }

    }
}
