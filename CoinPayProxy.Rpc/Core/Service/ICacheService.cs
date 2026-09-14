using CoinPayProxy.Infrastructure.Enums;
using Microsoft.Extensions.Caching.Memory;

namespace CoinPayProxy.Rpc.Core.Service
{
    public interface ICacheService
    {
        string CreateKey(params object[] args);

        string GetBlockNumberKey(AgreementTypeEnum @enum);
    }

    public class CacheService : ICacheService
    {
        readonly IMemoryCache _memory;

        public CacheService(IMemoryCache memory, IServiceScopeFactory scopeFactory)
        {
            _memory = memory;
        }

        public string CreateKey(params object[] args)
        {
            return $"CoinPayProxy.Svc:{string.Join(".", args)}".ToLower();
        }

        public string GetBlockNumberKey(AgreementTypeEnum @enum)
        {
            return CreateKey($"BlockNumber:{@enum}");
        }

    }
}
