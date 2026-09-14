using CoinPayProxy.Infrastructure.Enums;

namespace CoinPayProxy.Infrastructure.Blockchain
{
    public class EvmConfig
    {
        public Dictionary<AgreementTypeEnum, EvmChainOptions> Chains { get; set; }

        public List<string> InfuraApiKeys { get; set; }
    }

    public class EvmChainOptions
    {
        public long ChainId { get; init; }

        public string InfuraApi { get; init; } = "";

        public string UsdtContract { get; init; } = "";
    }

}
