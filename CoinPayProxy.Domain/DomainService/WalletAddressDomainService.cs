using CoinPayProxy.Domain.Aggregates;
using CoinPayProxy.Domain.Repositories;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Infrastructure.Extensions;
using Microsoft.Extensions.Logging;

namespace CoinPayProxy.Domain.DomainService
{
    public class WalletAddressDomainService(ILogger<WalletAddressDomainService> logger,
        Repository<WalletAddress> walletAddress)
    {
        private readonly ILogger<WalletAddressDomainService> _logger = logger;
        private readonly Repository<WalletAddress> _walletAddress = walletAddress;

        public async Task<ErrorCodeEnum> GenerateAddress(WalletAddress walletAddress)
        {
            var existInStance = await _walletAddress.Where(n => n.MemberId == walletAddress.MemberId
                && n.Address == walletAddress.Address
                && n.AgreementType == walletAddress.AgreementType).Master().FirstAsync();
            if (existInStance != null)
                return ErrorCodeEnum.数据已存在;

            var result = await _walletAddress.InsertAsync(walletAddress);

            return result.Id > 0 ? ErrorCodeEnum.成功 : ErrorCodeEnum.None;
        }

        public async Task<ErrorCodeEnum> UpdateSweepingStatus(long memberId, AgreementTypeEnum agreementType, bool isSweeping, string sweepingAddress)
        {
            var result = await _walletAddress.UpdateDiy
                .Set(m => new WalletAddress
                {
                    IsSweeping = isSweeping,
                    SweepingAddress = sweepingAddress,
                    ModifyTime = DateTime.UtcNow.GetTimeStamp()
                })
                .Where(m => m.MemberId == memberId && m.AgreementType == agreementType)
                .ExecuteAffrowsAsync();

            if (result == 0)
            {
                _logger.LogWarning($"更新归集状态失败,memberId:{memberId},isSweeping:{isSweeping},sweepingAddress:{sweepingAddress}");
                return ErrorCodeEnum.None;
            }

            return ErrorCodeEnum.成功;
        }

        public async Task<ErrorCodeEnum> UpdateCollectStatus(List<long> memberIds, AgreementTypeEnum agreementType, bool isSweeping, string sweepingAddress)
        {
            var result = await _walletAddress.UpdateDiy
                .Set(m => new WalletAddress
                {
                    IsSweeping = isSweeping,
                    SweepingAddress = sweepingAddress,
                    ModifyTime = DateTime.UtcNow.GetTimeStamp()
                })
                 .Where(m => memberIds.Contains(m.MemberId) && m.AgreementType == agreementType)
                 .ExecuteAffrowsAsync();

            if (result == 0)
            {
                _logger.LogWarning($"更新归集状态失败,memberIds:{memberIds.ToJson()},isSweeping:{isSweeping},sweepingAddress:{sweepingAddress}");
                return ErrorCodeEnum.None;
            }

            return ErrorCodeEnum.成功;
        }

    }
}
