namespace CoinPayProxy.Rpc.Core.Config
{
    public class AgreementConfig
    {
        /// <summary>
        /// 合约地址
        /// </summary>
        public string Contract { get; set; }

        /// <summary>
        /// 区块链浏览器地址
        /// </summary>
        public string WebsiteUrl { get; set; } = "";

        /// <summary>
        /// 归集地址
        /// </summary>
        public List<string> SweepingAddress { get; set; }

        /// <summary>
        /// 给地址充值Token的地址
        /// </summary>
        public List<RechargeWallet> RechargeTokens { get; set; }
    }

    public class RechargeWallet
    {
        public string TokenAddress { get; set; } = "";
        public string PrivateKey { get; set; } = "";
    }
}
