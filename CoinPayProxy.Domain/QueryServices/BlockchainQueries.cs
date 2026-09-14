using CoinPayProxy.Domain.Aggregates;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Infrastructure.Extensions;
using CoinPayProxy.Infrastructure.Model;

namespace CoinPayProxy.Domain.QueryServices
{
    public class BlockchainQueries(IFreeSql freeSql)
    {
        private readonly IFreeSql _freeSql = freeSql;

        public async Task<bool> BlockNumberKeyExist(string key)
        {
            return await _freeSql.Select<BlockNumber>().AnyAsync(a => a.Key == key);
        }

        public async Task<long> GetNodeBlockNumber(string key, string node)
        {
            var value = await _freeSql.Select<BlockNumber>()
               .Where(a => a.Key == $"{key}:{node}")
               .FirstAsync(a => a.Value);

            return value;
        }

        public async Task<WalletAddress> GetWalletAddress(long memberId, AgreementTypeEnum agreementType, bool isMaster = false)
        {
            var select = _freeSql.Select<WalletAddress>().Where(m => m.MemberId == memberId && m.AgreementType == agreementType);

            if (isMaster == false)
                return await select.FirstAsync();
            else
                return await select.Master().FirstAsync();
        }

        public async Task<WalletAddress> GetWalletAddress(string address, AgreementTypeEnum agreementType)
        {
            return await _freeSql.Select<WalletAddress>()
                .Where(m => m.Address == address && m.AgreementType == agreementType)
                .FirstAsync();
        }

        public async Task<(List<MemberWalletDto>, long)> GetMemberWalletList(int page, int limit, long memberId, List<long> memberIds,
            AgreementTypeEnum agreementType, int orderBy, bool desc = true)
        {
            var select = _freeSql.Select<TransactionRecord>()
                .Where(m => m.Status == TransactionStatusEnum.Success || m.Status == TransactionStatusEnum.SweepingFail)
                .WhereIf(memberId > 0, m => m.MemberId == memberId)
                .WhereIf(memberIds != null && memberIds.Count != 0, m => memberIds.Contains(m.MemberId))
                .WhereIf(agreementType > 0, m => m.AgreementType == agreementType)
                .GroupBy(m => new { m.MemberId, m.AgreementType });

            select.Count(out var total);

            //1.余额 2.能量余额 3.已归集数量 4.已归集时间
            switch (orderBy)
            {
                case 1:
                    if (desc)
                        select = select.OrderByDescending(m => m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeUSDT ? m.Value.Amount : 0)
                            + m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT && m.Value.Status == TransactionStatusEnum.Success ? m.Value.Amount : 0));
                    else
                        select = select.OrderBy(m => m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeUSDT ? m.Value.Amount : 0)
                            + m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT && m.Value.Status == TransactionStatusEnum.Success ? m.Value.Amount : 0));
                    break;
                case 2:
                    if (desc)
                        select = select.OrderByDescending(m => m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeToken ? m.Value.Amount : 0)
                            - m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT ? m.Value.Fee : 0)
                            - m.Sum(m.Value.TransactionType == TransactionTypeEnum.RecycleToken ? m.Value.Amount + m.Value.Fee : 0));
                    else
                        select = select.OrderBy(m => m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeToken ? m.Value.Amount : 0)
                            - m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT ? m.Value.Fee : 0)
                            - m.Sum(m.Value.TransactionType == TransactionTypeEnum.RecycleToken ? m.Value.Amount + m.Value.Fee : 0));
                    break;
                case 3:
                    if (desc)
                        select = select.OrderByDescending(m => Math.Abs(m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT
                            && m.Value.Status == TransactionStatusEnum.Success ? m.Value.Amount : 0)));
                    else
                        select = select.OrderBy(m => Math.Abs(m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT
                            && m.Value.Status == TransactionStatusEnum.Success ? m.Value.Amount : 0)));
                    break;
                case 4:
                    if (desc)
                        select = select.OrderByDescending(m => m.Max(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT
                            && m.Value.Status == TransactionStatusEnum.Success ? m.Value.TransactionTime : 0));
                    else
                        select = select.OrderBy(m => m.Max(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT
                            && m.Value.Status == TransactionStatusEnum.Success ? m.Value.TransactionTime : 0));
                    break;
            }

            var walletList = await select.Page(page, limit)
                .ToListAsync(m => new MemberWalletDto
                {
                    MemberId = m.Key.MemberId,
                    AgreementType = m.Key.AgreementType,
                    Address = m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT || m.Value.TransactionType == TransactionTypeEnum.RecycleToken
                        ? m.Value.FromAddress : m.Value.ToAddress,
                    UsdtBalance = m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeUSDT ? m.Value.Amount : 0)
                        + m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT && m.Value.Status == TransactionStatusEnum.Success ? m.Value.Amount : 0),
                    TokenBalance = m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeToken ? m.Value.Amount : 0)
                        - m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT ? m.Value.Fee : 0)
                        - m.Sum(m.Value.TransactionType == TransactionTypeEnum.RecycleToken ? m.Value.Amount + m.Value.Fee : 0),
                    SweepingBalance = m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT
                        && m.Value.Status == TransactionStatusEnum.Success ? m.Value.Amount : 0),
                    SweepingTime = m.Max(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT
                        && m.Value.Status == TransactionStatusEnum.Success ? m.Value.TransactionTime : 0),
                    LastRechargeAmount = m.Max(m.Value.TransactionType == TransactionTypeEnum.RechargeToken
                        && m.Value.Status == TransactionStatusEnum.Success ? m.Value.Amount : 0M),
                    LastRechargeTime = m.Max(m.Value.TransactionType == TransactionTypeEnum.RechargeUSDT
                        && m.Value.Status == TransactionStatusEnum.Success ? m.Value.TransactionTime : 0)
                });

            if (total > 0)
            {
                var ids = walletList.Select(a => a.MemberId);

                var sweepingList = await _freeSql.Select<WalletAddress>().Where(m => ids.Contains(m.MemberId))
                    .WhereIf(agreementType > 0, m => m.AgreementType == agreementType)
                    .ToListAsync();

                walletList.ForEach(a =>
                {
                    a.IsSweeping = sweepingList.FirstOrDefault(m => m.MemberId == a.MemberId && m.AgreementType == a.AgreementType).IsSweeping;
                });
            }

            return new(walletList, total);
        }

        public async Task<MemberWalletDto> GetMemberWallet(long memberId, AgreementTypeEnum agreementType)
        {
            var select = _freeSql.Select<TransactionRecord>()
                .Where(m => m.Status == TransactionStatusEnum.Success || m.Status == TransactionStatusEnum.SweepingFail)
                .Where(m => m.MemberId == memberId)
                .Where(m => m.AgreementType == agreementType)
                .GroupBy(m => new { m.MemberId, m.AgreementType });

            var wallet = await select.ToListAsync(m => new MemberWalletDto
            {
                MemberId = m.Key.MemberId,
                AgreementType = m.Key.AgreementType,
                Address = m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT || m.Value.TransactionType == TransactionTypeEnum.RecycleToken
                    ? m.Value.FromAddress : m.Value.ToAddress,
                UsdtBalance = m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeUSDT ? m.Value.Amount : 0)
                    + m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT && m.Value.Status == TransactionStatusEnum.Success ? m.Value.Amount : 0),
                TokenBalance = m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeToken ? m.Value.Amount : 0)
                - m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT ? m.Value.Fee : 0)
                - m.Sum(m.Value.TransactionType == TransactionTypeEnum.RecycleToken ? m.Value.Amount + m.Value.Fee : 0)
            });

            return wallet.FirstOrDefault();
        }

        public async Task<List<MemberWalletDto>> GetUSDTAndTokenGteNumList(decimal usdrNum, decimal token, AgreementTypeEnum agreementType)
        {
            var select = _freeSql.Select<TransactionRecord>()
                .Where(a => a.Status == TransactionStatusEnum.Success || a.Status == TransactionStatusEnum.SweepingFail)
                .WhereIf(agreementType > 0, m => m.AgreementType == agreementType)
                .GroupBy(m => new { m.MemberId, m.AgreementType })
                .Having(m => (m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeUSDT ? m.Value.Amount : 0)
                    + m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT && m.Value.Status == TransactionStatusEnum.Success ? m.Value.Amount : 0))
                    >= usdrNum)
                .Having(m => (m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeToken ? m.Value.Amount : 0)
                    - m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT ? m.Value.Fee : 0)
                    - m.Sum(m.Value.TransactionType == TransactionTypeEnum.RecycleToken ? m.Value.Amount + m.Value.Fee : 0)) >= token);

            var walletList = await select.ToListAsync(m => new MemberWalletDto
            {
                MemberId = m.Key.MemberId,
                AgreementType = m.Key.AgreementType,
                Address = m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT
                    || m.Value.TransactionType == TransactionTypeEnum.RecycleToken
                    ? m.Value.FromAddress : m.Value.ToAddress,
                UsdtBalance = m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeUSDT ? m.Value.Amount : 0)
                    + m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT && m.Value.Status == TransactionStatusEnum.Success ? m.Value.Amount : 0),
                TokenBalance = m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeToken ? m.Value.Amount : 0)
                    - m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT ? m.Value.Fee : 0)
                    - m.Sum(m.Value.TransactionType == TransactionTypeEnum.RecycleToken ? m.Value.Amount + m.Value.Fee : 0),
            });

            if (walletList.Count > 0)
            {
                var ids = walletList.Select(a => a.MemberId);

                var sweepingtList = await _freeSql.Select<WalletAddress>().Where(m => ids.Contains(m.MemberId)).ToListAsync();

                walletList.ForEach(a =>
                {
                    a.IsSweeping = sweepingtList.FirstOrDefault(m => m.MemberId == a.MemberId && m.AgreementType == a.AgreementType).IsSweeping;
                });
            }

            return [.. walletList.Where(m => m.IsSweeping == false)];
        }

        public async Task<List<MemberWalletDto>> GetTokenGteNumList(decimal token, AgreementTypeEnum agreementType)
        {
            var select = _freeSql.Select<TransactionRecord>()
                .Where(a => a.Status == TransactionStatusEnum.Success || a.Status == TransactionStatusEnum.SweepingFail)
                .WhereIf(agreementType > 0, m => m.AgreementType == agreementType)
                .GroupBy(m => new { m.MemberId, m.AgreementType })
                .Having(m => (m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeUSDT ? m.Value.Amount : 0)
                    + m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT && m.Value.Status == TransactionStatusEnum.Success ? m.Value.Amount : 0)) == 0)
                .Having(m => (m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeToken ? m.Value.Amount : 0)
                    - m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT ? m.Value.Fee : 0)
                    - m.Sum(m.Value.TransactionType == TransactionTypeEnum.RecycleToken ? m.Value.Amount + m.Value.Fee : 0)) >= token);

            var walletList = await select.ToListAsync(m => new MemberWalletDto
            {
                MemberId = m.Key.MemberId,
                AgreementType = m.Key.AgreementType,
                Address = m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT || m.Value.TransactionType == TransactionTypeEnum.RecycleToken
                    ? m.Value.FromAddress : m.Value.ToAddress,
                UsdtBalance = m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeUSDT ? m.Value.Amount : 0)
                    + m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT && m.Value.Status == TransactionStatusEnum.Success ? m.Value.Amount : 0),
                TokenBalance = m.Sum(m.Value.TransactionType == TransactionTypeEnum.RechargeToken ? m.Value.Amount : 0)
                    - m.Sum(m.Value.TransactionType == TransactionTypeEnum.SweepingUSDT ? m.Value.Fee : 0)
                    - m.Sum(m.Value.TransactionType == TransactionTypeEnum.RecycleToken ? m.Value.Amount + m.Value.Fee : 0),
            });

            if (walletList.Count > 0)
            {
                var ids = walletList.Select(a => a.MemberId);

                var collectList = await _freeSql.Select<WalletAddress>().Where(m => ids.Contains(m.MemberId)).ToListAsync();

                walletList.ForEach(a =>
                {
                    a.IsSweeping = collectList.FirstOrDefault(m => m.MemberId == a.MemberId && m.AgreementType == a.AgreementType).IsSweeping;
                });
            }

            return [.. walletList.Where(m => m.IsSweeping == false)];
        }

        public async Task<decimal> GetToken(long memberId, AgreementTypeEnum agreementType)
        {
            var select = _freeSql.Select<TransactionRecord>()
                .Where(m => m.MemberId == memberId && m.AgreementType == agreementType)
                .Where(m => m.TransactionType == TransactionTypeEnum.SweepingUSDT
                    || m.TransactionType == TransactionTypeEnum.RechargeToken
                    || m.TransactionType == TransactionTypeEnum.RecycleToken)
                .Where(a => a.Status == TransactionStatusEnum.Success || a.Status == TransactionStatusEnum.SweepingFail);

            return await select.SumAsync(m => (m.TransactionType == TransactionTypeEnum.RechargeToken ? m.Amount : 0)
                - (m.TransactionType == TransactionTypeEnum.SweepingUSDT ? m.Fee : 0)
                - (m.TransactionType == TransactionTypeEnum.RecycleToken ? m.Amount + m.Fee : 0));
        }

        public async Task<TransactionRecord> GetTransactionRecord(string hashId)
        {
            return await _freeSql.Select<TransactionRecord>().Where(m => m.HashId == hashId).FirstAsync();
        }

        /// <summary>
        /// 获取充值token和回收token处理中交易记录
        /// </summary>
        /// <returns></returns>
        public async Task<List<TransactionRecord>> GetTransferTokens()
        {
            var now = DateTime.UtcNow.GetTimeStamp();

            var select = _freeSql.Select<TransactionRecord>().Master()
                .Where(m => m.Status == TransactionStatusEnum.Processing)
                .Where(m => m.TransactionType == TransactionTypeEnum.RechargeToken || m.TransactionType == TransactionTypeEnum.RecycleToken)
                .Where(m => (now - m.CreateTime) <= 3600);

            return await select.OrderByDescending(o => o.Id).Page(1, 10).ToListAsync();
        }

        /// <summary>
        /// 获取处理中的归集记录
        /// </summary>
        /// <returns></returns>
        public async Task<List<TransactionRecord>> GetSweepingList()
        {
            var now = DateTime.UtcNow.GetTimeStamp();

            var select = _freeSql.Select<TransactionRecord>().Master()
                .Where(m => m.Status == TransactionStatusEnum.Processing)
                .Where(m => m.TransactionType == TransactionTypeEnum.SweepingUSDT)
                .Where(m => now - m.CreateTime <= 3600);

            return await select.OrderByDescending(o => o.Id).Page(1, 10).ToListAsync();
        }

        /// <summary>
        /// 获取需要归集的地址
        /// </summary>
        /// <returns></returns>
        public async Task<List<WalletAddress>> GetSweepingAddress()
        {
            return await _freeSql.Select<WalletAddress>().Where(m => m.IsSweeping == true).ToListAsync();   //&& (now - m.ModifyTime) <= 7200
        }

        /// <summary>
        /// 获取等待中得转U记录
        /// </summary>
        /// <returns></returns>
        public async Task<List<TransactionRecord>> GetTransferUsdts(AgreementTypeEnum agreementType)
        {
            var now = DateTime.UtcNow.GetTimeStamp();

            var select = _freeSql.Select<TransactionRecord>().Master()
                .Where(m => m.AgreementType == agreementType)
                .Where(m => m.Status == TransactionStatusEnum.Processing)
                .Where(m => m.TransactionType == TransactionTypeEnum.RechargeUSDT)
                .Where(m => (now - m.CreateTime) <= 3600);

            return await select.OrderByDescending(o => o.Id).Page(1, 10).ToListAsync();
        }

    }
}
