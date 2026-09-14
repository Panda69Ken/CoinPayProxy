using CoinPayProxy.Domain.Aggregates;
using CoinPayProxy.Domain.Repositories;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Infrastructure.Extensions;
using Microsoft.Extensions.Logging;

namespace CoinPayProxy.Domain.DomainService
{
    public class TransactionRecordDomainService(ILogger<TransactionRecordDomainService> logger,
        Repository<TransactionRecord> transactionRecord,
        Repository<WalletAddress> walletAddress)
    {
        private readonly ILogger<TransactionRecordDomainService> _logger = logger;
        private readonly Repository<TransactionRecord> _transactionRecord = transactionRecord;
        private readonly Repository<WalletAddress> _walletAddress = walletAddress;

        /// <summary>
        /// 添加或修改交易流水
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public async Task<(ErrorCodeEnum, long)> AddOrUpdateTransaction(TransactionRecord record)
        {
            var addressStr = record.TransactionType == TransactionTypeEnum.SweepingUSDT || record.TransactionType == TransactionTypeEnum.RecycleToken ?
                record.FromAddress : record.ToAddress;

            //通过地址得到用信息
            var address = await _walletAddress.Where(n => n.Address == addressStr && n.AgreementType == record.AgreementType).Master().FirstAsync();

            if (address == null)
            {
                _logger.LogWarning($"充值地址错误:param:{record.ToJson()}");
                return (ErrorCodeEnum.数据不存在, 0);
            }

            if (record.TransactionType == TransactionTypeEnum.RechargeUSDT)
                record.MemberId = address.MemberId;

            var transaction = await _transactionRecord.Where(n => n.HashId == record.HashId).Master().FirstAsync();

            if (transaction != null)
            {
                //归集数据在ConsolidationUSDTJob中处理
                if (transaction.TransactionType == TransactionTypeEnum.SweepingUSDT) return (ErrorCodeEnum.None, 0);

                transaction.Status = record.Status;

                transaction.ModifyTime = DateTime.UtcNow.GetTimeStamp();

                var result = await _transactionRecord.UpdateAsync(transaction);

                if (result > 0) return (ErrorCodeEnum.成功, transaction.Id);

                _logger.LogError($"TransactionRecord添加失败,param:{record.ToJson()}");

                return (ErrorCodeEnum.数据库执行异常错误, 0);
            }

            record.AgreementType = address.AgreementType;

            transaction = await _transactionRecord.InsertAsync(record);

            if (transaction == null || transaction.Id == 0)
            {
                _logger.LogWarning($"保存交易记录失败,param:{record.ToJson()}");
                return (ErrorCodeEnum.数据库执行异常错误, 0);
            }

            return (ErrorCodeEnum.成功, transaction.Id);
        }

        /// <summary>
        /// 更新交易状态
        /// </summary>
        /// <param name="transactionId"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        public async Task<ErrorCodeEnum> UpdateStatusAndFee(long transactionId, TransactionStatusEnum status, decimal fee, string feeCurrency, long blockNumber)
        {
            var transaction = await _transactionRecord.Where(n => n.Id == transactionId).Master().FirstAsync();

            if (transaction == null)
            {
                _logger.LogWarning($"交易记录不存在:transactionId:{transactionId}");
                return ErrorCodeEnum.数据不存在;
            }

            var result = await _transactionRecord.UpdateDiy
                .Set(m => new TransactionRecord
                {
                    ModifyTime = DateTime.UtcNow.GetTimeStamp(),
                    Status = status,
                    Fee = fee,
                    FeeCurrency = feeCurrency,
                    BlockNumber = blockNumber
                })
                .Where(m => m.Id == transactionId)
                .ExecuteAffrowsAsync();

            if (result == 0)
            {
                _logger.LogWarning($"更新交易状态失败:transactionId:{transactionId}");
                return ErrorCodeEnum.数据库执行异常错误;
            }

            return ErrorCodeEnum.成功;
        }

    }
}
