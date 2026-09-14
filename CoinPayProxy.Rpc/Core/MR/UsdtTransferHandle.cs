using CoinPayProxy.Domain.Aggregates;
using CoinPayProxy.Domain.DomainService;
using CoinPayProxy.Domain.QueryServices;
using CoinPayProxy.Infrastructure.Blockchain;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Infrastructure.Extensions;
using CoinPayProxy.Rpc.Core.Service;
using MediatR;
using TronNet;

namespace CoinPayProxy.Rpc.Core.MR
{
    public class UsdtTransferCommand : IRequest<(bool, string)>
    {
        public long MemberId { get; set; }
        public decimal Amount { get; set; }
        public AgreementTypeEnum AgreementType { get; set; }
        public string SweepingAddress { get; set; }
    }

    //地址转U交易逻辑
    public class UsdtTransferHandle(ILogger<UsdtTransferHandle> logger,
        IConfigService config,
        IServiceScopeFactory serviceProvider,
        TronNetRecord tron,
        Func<AgreementTypeEnum, InfuraNetRecord> infuraFactory) : IRequestHandler<UsdtTransferCommand, (bool, string)>
    {
        readonly ILogger<UsdtTransferHandle> _logger = logger;
        readonly IConfigService _config = config;
        readonly IServiceScopeFactory _serviceProvider = serviceProvider;
        readonly TronNetRecord _tron = tron;
        readonly Func<AgreementTypeEnum, InfuraNetRecord> _infuraFactory = infuraFactory;

        public async Task<(bool, string)> Handle(UsdtTransferCommand command, CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();

                var queries = scope.ServiceProvider.GetRequiredService<BlockchainQueries>();

                var transactionRecord = scope.ServiceProvider.GetRequiredService<TransactionRecordDomainService>();

                var agreementConfig = _config.AgreementConfig[command.AgreementType];
                if (agreementConfig == null)
                {
                    string error = $"{command.AgreementType}归集转账交易失败,协议配置错误,param:{command.ToJson()}";
                    _logger.LogWarning(error);
                    return (false, error);
                }

                var toAddress = agreementConfig.SweepingAddress.FirstOrDefault(a => a == command.SweepingAddress);
                if (string.IsNullOrEmpty(toAddress))
                {
                    string error = $"{command.AgreementType}归集转账交易失败,归集地址错误,param:{command.ToJson()}";
                    _logger.LogWarning(error);
                    return (false, error);
                }

                var walletAddress = await queries.GetWalletAddress(command.MemberId, command.AgreementType);
                if (walletAddress == null)
                {
                    string error = $"{command.AgreementType}归集转账交易失败,用户不存在{command.AgreementType}地址,param:{command.ToJson()}";
                    _logger.LogWarning(error);
                    return (false, error);
                }

                var result = false;
                var hashId = "";
                var blockNumber = 0L;

                var tokenAmount = await queries.GetToken(command.MemberId, command.AgreementType);

                if (command.AgreementType == AgreementTypeEnum.TRC20)
                {
                    (result, hashId, blockNumber) = await TronUSDT(command, walletAddress.PrivateKey, toAddress, tokenAmount);
                }
                else
                {
                    (result, hashId, blockNumber) = await EvmUSDT(command, walletAddress.PrivateKey, toAddress, tokenAmount);
                }

                if (result == false) return (false, hashId);

                var dateTime = DateTime.UtcNow.GetTimeStamp();
                var insertResult = await transactionRecord.AddOrUpdateTransaction(new TransactionRecord
                {
                    HashId = hashId,
                    MemberId = command.MemberId,
                    Amount = -1 * command.Amount,
                    AgreementType = command.AgreementType,
                    FromAddress = walletAddress.Address,
                    ToAddress = toAddress,
                    ContractAddress = agreementConfig.Contract,
                    Currency = CurrencyEnum.USDT.ToString(),
                    Status = TransactionStatusEnum.Processing,
                    TransactionType = TransactionTypeEnum.SweepingUSDT,
                    CreateTime = dateTime,
                    TransactionTime = dateTime,
                    BlockNumber = blockNumber
                });

                if (insertResult.Item1 != ErrorCodeEnum.成功)
                {
                    string error = $"{command.AgreementType}归集转账交易失败,插入交易记录数据失败,hashId:{hashId},param:{command.ToJson()}";
                    _logger.LogWarning(error);
                    return (false, error);
                }

                return (true, "");
            }
            catch (Exception ex)
            {
                string error = $"{command.AgreementType}归集转账交易异常,param:{command.ToJson()},error:{ex.Message}";
                _logger.LogError(error);
                return (false, error);
            }
        }

        private async Task<(bool, string, long)> TronUSDT(UsdtTransferCommand command, string privateKey, string toAddress, decimal tokenAmount)
        {
            var ownerAccount = _tron.GetAccount(privateKey);

            //查询用户的TRX资产
            var resultToken = await _tron.BalanceOfTokenAsync(ownerAccount);
            if (resultToken == 0)
            {
                string error = $"{command.AgreementType}归集转账交易失败,用户的Token不足,param:{command.ToJson()}";
                _logger.LogWarning(error);
                return (false, error, 0);
            }

            //Tron合约交易需要消耗能量和带宽，能量和带宽可以通过质押或代理获得；有600免费带宽，使用后再24小时内逐渐恢复
            //没有能量的情况下需要燃烧15--28 TRX
            //带宽不足的情况下需要燃烧0.4 TRX 
            //Token消耗费用最大值，固定30TRX
            var limitFee = 30M;
            if (tokenAmount < limitFee)
            {
                string error = $"{command.AgreementType}归集转账交易失败,用户Token不足以支付手续费,余额:{tokenAmount}Token,预计消耗(含缓冲){limitFee}TRX,param:{command.ToJson()}";
                _logger.LogWarning(error);
                return (false, error, 0);
            }

            //获取用户的USDT的余额
            var resultUsdt = await _tron.BalanceOfUSDTAsync(ownerAccount);
            if (resultUsdt < command.Amount)
            {
                string error = $"{command.AgreementType} 归集转账交易失败,用户的USDT不足,余额为{resultUsdt},param:{command.ToJson()}";
                _logger.LogWarning(error);
                return (false, error, 0);
            }

            var transaction = await _tron.TransferOfUSDTAsync(ownerAccount, toAddress, command.Amount, limitFee);

            if (transaction == null)
            {
                return (false, $"{command.AgreementType} 归集转账交易失败", 0);
            }

            return (true, transaction.GetTxid(), transaction.RawData.RefBlockNum);
        }

        private async Task<(bool, string, long)> EvmUSDT(UsdtTransferCommand command, string privateKey, string toAdress, decimal tokenAmount)
        {
            var ownerAccount = _infuraFactory(command.AgreementType).GetAccount(privateKey);

            //查询用户的ETH\BNB资产
            var resultToken = await _infuraFactory(command.AgreementType).BalanceOfTokenAsync(ownerAccount);
            if (resultToken == 0)
            {
                string error = $"{command.AgreementType} 归集转账交易失败,用户的Token不足,param:{command.ToJson()}";
                _logger.LogWarning(error);
                return (false, error, 0);
            }

            //获取用户的USDT的余额
            var resultUsdt = await _infuraFactory(command.AgreementType).BalanceOfUSDTAsync(ownerAccount);
            if (resultUsdt < command.Amount)
            {
                string error = $"{command.AgreementType}归集转账交易失败,用户的USDT不足,余额为{resultUsdt},param:{command.ToJson()}";
                _logger.LogWarning(error);
                return (false, error, 0);
            }

            var transaction = await _infuraFactory(command.AgreementType)
                .TransferOfUSDTAsync(ownerAccount, toAdress, command.Amount, tokenAmount);

            if (transaction == null)
            {
                return (false, $"{command.AgreementType} 归集转账交易失败,用户Token不足以支付手续费,余额:{tokenAmount}Token", 0);
            }

            return (true, transaction.TransactionHash, transaction.BlockNumber.Value.ToInt64OrDefault());
        }

    }
}
