using CoinPayProxy.Infrastructure.Blockchain;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Infrastructure.Extensions;
using MediatR;
using Nethereum.Web3;
using TronNet.Protocol;

namespace CoinPayProxy.Rpc.Core.MR
{
    public class TransferVerifyCommand : IRequest<(TransactionStatusEnum, long, decimal, string)>
    {
        public string HashId { get; set; }
        public AgreementTypeEnum AgreementType { get; set; }
    }

    //地址交易状态校验逻辑
    public class TransferVerifyHandle(ILogger<TransferVerifyHandle> logger,
        TronNetRecord tron,
        Func<AgreementTypeEnum, InfuraNetRecord> infuraFactory)
        : IRequestHandler<TransferVerifyCommand, (TransactionStatusEnum, long, decimal, string)>
    {
        readonly ILogger<TransferVerifyHandle> _logger = logger;
        readonly TronNetRecord _tron = tron;
        readonly Func<AgreementTypeEnum, InfuraNetRecord> _infuraFactory = infuraFactory;

        public async Task<(TransactionStatusEnum, long, decimal, string)> Handle(TransferVerifyCommand command, CancellationToken cancellationToken)
        {
            var status = TransactionStatusEnum.None;
            var fee = 0M;
            var feeCurrency = "";
            var blockNumber = 0L;

            try
            {
                switch (command.AgreementType)
                {
                    case AgreementTypeEnum.TRC20:
                        //通过walletsolidity获取已经固化交易 + 合约执行成功，才能确定交易成功
                        var transactionInfo = await _tron.GetTransactionInfo(command.HashId);
                        if (transactionInfo.Result == TransactionInfo.Types.code.Sucess)
                        {
                            if (transactionInfo.Receipt.Result == Transaction.Types.Result.Types.contractResult.Success)
                            {
                                status = TransactionStatusEnum.Success;
                            }
                            else
                            {
                                status = TransactionStatusEnum.Fail;
                            }

                            fee = transactionInfo.Fee;
                            feeCurrency = CurrencyEnum.TRX.ToString();
                            blockNumber = transactionInfo.BlockNumber;
                        }
                        break;
                    case AgreementTypeEnum.ERC20:
                    case AgreementTypeEnum.BEP20:
                        //合约执行成功 + BlockNumber Finalized，才能确定交易成功
                        var receipt = await _infuraFactory(command.AgreementType).GetTransactionInfo(command.HashId);
                        if (receipt != null)
                        {
                            if (receipt.Status.Value == 1)
                            {
                                var isFinalized = await _infuraFactory(command.AgreementType).FinalizedBlockNumber(receipt.BlockNumber.Value);
                                if (isFinalized)
                                {
                                    status = TransactionStatusEnum.Success;
                                }
                            }
                            else
                            {
                                status = TransactionStatusEnum.Fail;
                            }

                            var gasUsed = receipt.GasUsed.Value;
                            var effectiveGasPrice = receipt.EffectiveGasPrice?.Value ?? 0;
                            var gasCostWei = gasUsed * effectiveGasPrice;
                            fee = Web3.Convert.FromWei(gasCostWei);
                            blockNumber = receipt.BlockNumber.Value.ToInt64OrDefault();
                            switch (command.AgreementType)
                            {
                                case AgreementTypeEnum.ERC20:
                                    feeCurrency = CurrencyEnum.ETH.ToString();
                                    break;
                                case AgreementTypeEnum.BEP20:
                                    feeCurrency = CurrencyEnum.BNB.ToString();
                                    break;
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"交易检验异常,param:{command.ToJson()},error:{ex.Message}");
            }

            return (status, blockNumber, fee, feeCurrency);
        }

    }
}
