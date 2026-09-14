using CoinPayProxy.Domain.Aggregates;
using CoinPayProxy.Domain.DomainService;
using CoinPayProxy.Domain.QueryServices;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Infrastructure.Extensions;
using CoinPayProxy.Infrastructure.Model;
using CoinPayProxy.Rpc.Core.Service;
using Google.Protobuf.Collections;
using Newtonsoft.Json;
using System.Threading.Tasks.Dataflow;
using TronNet;
using TronNet.Protocol;
using static TronNet.Protocol.Transaction.Types.Contract.Types;

namespace CoinPayProxy.Rpc.Core.Block
{
    public class TronTransferModel
    {
        public RepeatedField<TransactionExtention> Transactions { get; set; }
        public string Node { get; set; } = "";
    }

    //记录Tron转U的交易记录
    public class TronTransferBlock
    {
        readonly ActionBlock<TronTransferModel> _action;

        readonly ILogger<TronTransferBlock> _logger;
        readonly IConfigService _config;
        readonly IServiceScopeFactory _scopeFactory;

        public TronTransferBlock(ILogger<TronTransferBlock> logger,
            IConfigService config,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _config = config;
            _scopeFactory = scopeFactory;

            _action = new ActionBlock<TronTransferModel>(async (item) =>
            {
                await Handler(item);
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 2 });

        }

        public bool Post(TronTransferModel request)
        {
            return _action.Post(request);
        }

        private async Task Handler(TronTransferModel request)
        {
            using var scope = _scopeFactory.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<BlockchainQueries>();
            var transactionRecord = scope.ServiceProvider.GetRequiredService<TransactionRecordDomainService>();

            foreach (var item in request.Transactions)
            {
                if (item.Transaction.Ret[0].ContractRet != Transaction.Types.Result.Types.contractResult.Success) continue;

                var txid = (item.Txid != null && item.Txid.Length > 0)
                    ? Convert.ToHexString(item.Txid.ToByteArray()).ToLowerInvariant()
                    : item.Transaction?.GetTxid();

                try
                {
                    var type = item.Transaction.RawData.Contract[0].Type;

                    if (type == ContractType.TriggerSmartContract)
                    {
                        string json = item.Transaction.RawData.Contract[0].Parameter.ParameterValueToJson();

                        var parameter = JsonConvert.DeserializeObject<TronBlockTransaction>(json);

                        if (parameter.Contract != _config.AgreementConfig[AgreementTypeEnum.TRC20].Contract) continue;

                        var wallet = await queries.GetWalletAddress(parameter.ToAddress, AgreementTypeEnum.TRC20);
                        if (wallet == null)
                        {
                            //_logger.LogWarning($"钱包地址不存在,param:{request.ToJsonEx()}");
                            return;
                        }

                        var time = DateTime.UtcNow.GetTimeStamp();
                        var transactionTime = item.Transaction.RawData.Timestamp;
                        if (Utils.IsPlausibleUnixMilliseconds(transactionTime) == false)
                        {
                            transactionTime = time;
                        }

                        var reply = await transactionRecord.AddOrUpdateTransaction(new TransactionRecord
                        {
                            HashId = txid ?? "",
                            MemberId = wallet.MemberId,
                            FromAddress = parameter.FromAddress,  //链上转U地址,系统中不一定有
                            ToAddress = parameter.ToAddress,  //充U用户地址
                            ContractAddress = parameter.Contract,
                            Amount = parameter.Amount,
                            Currency = parameter.Symbol.ToString(),
                            Status = TransactionStatusEnum.Processing,
                            TransactionTime = transactionTime,
                            AgreementType = AgreementTypeEnum.TRC20,
                            TransactionType = TransactionTypeEnum.RechargeUSDT,
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
                            _logger.LogWarning($"Tron添加USDT交易记录失败,param:{request.ToJson()}");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Tron记录USDT交易异常,node:{request.Node},Txid:{txid},error:{ex.Message}");
                }
            }
        }
    }
}
