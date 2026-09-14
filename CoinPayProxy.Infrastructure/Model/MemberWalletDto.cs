using CoinPayProxy.Infrastructure.Enums;

namespace CoinPayProxy.Infrastructure.Model
{
    public class MemberWalletDto
    {
        public long MemberId { get; set; }
        public AgreementTypeEnum AgreementType { get; set; }
        public string Address { get; set; } = string.Empty;
        /// <summary>
        /// USDT余额 单位USDT
        /// </summary>
        public decimal UsdtBalance { get; set; }
        /// <summary>
        /// Token余额 单位TRX\EHT\BNB
        /// </summary>
        public decimal TokenBalance { get; set; }
        /// <summary>
        /// 已归集数量 单位USDT
        /// </summary>
        public decimal SweepingBalance { get; set; }
        /// <summary>
        /// 最近归集时间
        /// </summary>
        public long SweepingTime { get; set; }
        /// <summary>
        /// 是否归集中
        /// </summary>
        public bool IsSweeping { get; set; } = false;

        /// <summary>
        /// 最近一次充U
        /// </summary>
        public decimal LastRechargeAmount { get; set; }
        /// <summary>
        /// 最近一次充U时间
        /// </summary>
        public long LastRechargeTime { get; set; }
    }

}
