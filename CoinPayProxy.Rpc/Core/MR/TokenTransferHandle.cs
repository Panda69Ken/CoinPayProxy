using CoinPayProxy.Domain.Aggregates;
using CoinPayProxy.Domain.DomainService;
using CoinPayProxy.Infrastructure.Blockchain;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Infrastructure.Extensions;
using MediatR;
using TronNet;

namespace CoinPayProxy.Rpc.Core.MR
{
    public class TokenTransferCommand : IRequest<bool>
    {
        public long MemberId { get; set; }
        public decimal Amount { get; set; }
        public string FromAddress { get; set; }
        public string FromPrivateKey { get; set; }
        public string ToAdress { get; set; }
        public AgreementTypeEnum AgreementType { get; set; }
        public TransactionTypeEnum TransactionType { get; set; }
    }

    //地址转Token交易逻辑
    public class TokenTransferHandle(ILogger<TokenTransferHandle> logger,
        IServiceScopeFactory serviceProvider,
        TronNetRecord tron,
        Func<AgreementTypeEnum, InfuraNetRecord> infuraFactory) : IRequestHandler<TokenTransferCommand, bool>
    {
        readonly ILogger<TokenTransferHandle> _logger = logger;
        readonly IServiceScopeFactory _serviceProvider = serviceProvider;
        readonly TronNetRecord _tron = tron;
        readonly Func<AgreementTypeEnum, InfuraNetRecord> _infuraFactory = infuraFactory;

        public async Task<bool> Handle(TokenTransferCommand command, CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();

                var transactionRecord = scope.ServiceProvider.GetRequiredService<TransactionRecordDomainService>();

                var result = false;
                var hashId = "";
                var blockNumber = 0L;

                if (command.AgreementType == AgreementTypeEnum.TRC20)
                {
                    (result, hashId, blockNumber) = await TronToken(command);
                }
                else
                {
                    (result, hashId, blockNumber) = await EvmToken(command);
                }

                if (result == false) return false;

                var currency = "";
                switch (command.AgreementType)
                {
                    case AgreementTypeEnum.TRC20:
                        currency = CurrencyEnum.TRX.ToString();
                        break;
                    case AgreementTypeEnum.ERC20:
                        currency = CurrencyEnum.ETH.ToString();
                        break;
                    case AgreementTypeEnum.BEP20:
                        currency = CurrencyEnum.BNB.ToString();
                        break;
                }

                var dateTime = DateTime.UtcNow.GetTimeStamp();
                var insertResult = await transactionRecord.AddOrUpdateTransaction(new TransactionRecord
                {
                    HashId = hashId,
                    MemberId = command.MemberId,
                    Amount = command.Amount,
                    AgreementType = command.AgreementType,
                    FromAddress = command.FromAddress,
                    ToAddress = command.ToAdress,
                    Currency = currency,
                    Status = TransactionStatusEnum.Processing,
                    TransactionType = command.TransactionType,
                    CreateTime = dateTime,
                    ModifyTime = dateTime,
                    TransactionTime = dateTime,
                    BlockNumber = blockNumber
                });

                if (insertResult.Item1 != ErrorCodeEnum.成功)
                {
                    _logger.LogWarning($"{command.AgreementType}发起Token交易失败,插入交易记录数据失败,HashId:{hashId},param:{command.ToJson()}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{command.AgreementType}发起Token交易异常,param:{command.ToJson()},error:{ex.Message}");
                return false;
            }
        }

        private async Task<(bool, string, long)> EvmToken(TokenTransferCommand command)
        {
            var result = (false, "", 0);

            var transaction = await _infuraFactory(command.AgreementType).TransferOfTokenAsync(command.FromPrivateKey, command.ToAdress, command.Amount);

            if (transaction == null)
            {
                _logger.LogWarning($"{command.AgreementType}创建Token交易失败,transaction返回null,param:{command.ToJson()}");
                return result;
            }

            if (transaction.Status.Value == 0)
            {
                _logger.LogWarning($"{command.AgreementType}执行Token交易失败,transaction:{transaction.ToJson()},param:{command.ToJson()}");
                return result;
            }

            return (true, transaction.TransactionHash, transaction.BlockNumber.Value.ToInt64OrDefault());
        }

        private async Task<(bool, string, long)> TronToken(TokenTransferCommand command)
        {
            var result = (false, "", 0);

            var (transaction, blockNumber) = await _tron.TransferOfTokenAsync(command.FromPrivateKey, command.ToAdress, command.Amount);

            if (transaction == null)
            {
                _logger.LogWarning($"{command.AgreementType}创建Token交易失败,transaction返回null,param:{command.ToJson()}");
                return result;
            }

            return (true, transaction.GetTxid(), blockNumber);
        }

    }
}
