using CoinPayProxy.Domain.Aggregates;
using CoinPayProxy.Domain.DomainService;
using CoinPayProxy.Domain.QueryServices;
using CoinPayProxy.Infrastructure.Blockchain;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Infrastructure.Extensions;
using CoinPayProxy.Rpc.Core.Service;
using System.Threading.Tasks.Dataflow;

namespace CoinPayProxy.Rpc.Core.Block
{
    public class EvmTransferModel
    {
        public long BlockNumber { get; set; }
        public AgreementTypeEnum AgreementType { get; set; }
        public List<EvmTransactionDTO> Transactions { get; set; }
        public string Node { get; set; } = "";
    }

    //记录EVM转U的交易记录
    public class EvmTransferBlock
    {
        readonly ActionBlock<EvmTransferModel> _action;

        readonly ILogger<EvmTransferBlock> _logger;
        readonly IServiceScopeFactory _scopeFactory;
        readonly IConfigService _config;
        readonly Func<AgreementTypeEnum, InfuraNetRecord> _infuraFactory;

        public EvmTransferBlock(ILogger<EvmTransferBlock> logger,
            IServiceScopeFactory scopeFactory,
            IConfigService config,
            Func<AgreementTypeEnum, InfuraNetRecord> infuraFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _config = config;
            _infuraFactory = infuraFactory;

            _action = new ActionBlock<EvmTransferModel>(async (item) =>
            {
                await Handler(item);
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 2 });

        }

        public bool Post(EvmTransferModel request)
        {
            return _action.Post(request);
        }

        private async Task Handler(EvmTransferModel request)
        {
            using var scope = _scopeFactory.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<BlockchainQueries>();
            var transactionRecord = scope.ServiceProvider.GetRequiredService<TransactionRecordDomainService>();

            var contract = _config.AgreementConfig[request.AgreementType].Contract;

            var block = await _infuraFactory(request.AgreementType).GetBlockInfo(request.BlockNumber);

            foreach (var item in request.Transactions)
            {
                var wallet = await queries.GetWalletAddress(item.ToAddress, request.AgreementType);
                if (wallet == null)
                {
                    //_logger.LogWarning($"钱包地址不存在,param:{request.ToJsonEx()}");
                    return;
                }

                var time = DateTime.UtcNow.GetTimeStamp();

                var currency = "";
                switch (request.AgreementType)
                {
                    case AgreementTypeEnum.ERC20:
                        currency = CurrencyEnum.ETH.ToString();
                        break;
                    case AgreementTypeEnum.BEP20:
                        currency = CurrencyEnum.BNB.ToString();
                        break;
                }

                var reply = await transactionRecord.AddOrUpdateTransaction(new TransactionRecord
                {
                    HashId = item.HashId,
                    MemberId = wallet.MemberId,
                    FromAddress = item.FromAddress,  //链上转U地址,系统中不一定有
                    ToAddress = item.ToAddress,  //充U用户地址
                    ContractAddress = contract,
                    Amount = item.Amount,
                    Currency = currency,
                    Status = TransactionStatusEnum.Processing,
                    TransactionTime = block.Timestamp.Value.ToInt64OrDefault(),
                    AgreementType = request.AgreementType,
                    TransactionType = TransactionTypeEnum.RechargeUSDT,
                    BlockNumber = item.BlockNumber,
                    CreateTime = time,
                    ModifyTime = time
                });

                if (reply.Item1 == ErrorCodeEnum.None)
                {
                    //归集数据在UsdtSweepingVerifyJob中处理
                    return;
                }

                if (reply.Item1 != ErrorCodeEnum.成功)
                {
                    _logger.LogWarning($"Evm添加USDT交易记录失败,param:{request.ToJson()}");
                    return;
                }
            }
        }

    }
}
