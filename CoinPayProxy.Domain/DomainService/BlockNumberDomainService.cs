using CoinPayProxy.Domain.Aggregates;
using CoinPayProxy.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace CoinPayProxy.Domain.DomainService
{
    public class BlockNumberDomainService(ILogger<BlockNumberDomainService> logger,
        IFreeSql freeSql,
        Repository<BlockNumber> blockNumber)
    {
        private readonly ILogger<BlockNumberDomainService> _logger = logger;
        public readonly IFreeSql _freeSql = freeSql;
        private readonly Repository<BlockNumber> _blockNumber = blockNumber;

        /// <summary>
        /// 类似redis的StringIncrement函数
        /// </summary>
        /// <param name="key"></param>
        /// <param name="step"></param>
        /// <returns></returns>
        public async Task<long> StringIncrement(string key, long step = 1)
        {
            using var uow = _freeSql.CreateUnitOfWork();

            var exists = await uow.Orm.Select<BlockNumber>().AnyAsync(a => a.Key == key);

            if (!exists)
            {
                await uow.Orm.Insert(new BlockNumber
                {
                    Key = key,
                    Value = step
                }).ExecuteAffrowsAsync();

                uow.Commit();

                return step;
            }

            await uow.Orm.Update<BlockNumber>()
                .Set(a => a.Value + step)
                .Where(a => a.Key == key)
                .ExecuteAffrowsAsync();

            var value = await uow.Orm.Select<BlockNumber>()
                .Where(a => a.Key == key)
                .FirstAsync(a => a.Value);

            uow.Commit();

            return value;
        }

        public async Task SetNodeBlockNumber(string key, string node, long number)
        {
            var k = $"{key}:{node}";

            var exist = await _blockNumber.Select.AnyAsync(a => a.Key == k);

            if (!exist)
            {
                await _blockNumber.InsertAsync(new BlockNumber
                {
                    Key = k,
                    Value = number
                });
            }
            else
            {
                await _blockNumber.UpdateDiy
                    .Set(a => a.Value == number)
                    .Where(a => a.Key == k)
                    .ExecuteAffrowsAsync();
            }
        }

    }
}
