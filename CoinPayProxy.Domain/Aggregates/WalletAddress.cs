using CoinPayProxy.Infrastructure.Enums;
using FreeSql.DataAnnotations;
using Newtonsoft.Json;

namespace CoinPayProxy.Domain.Aggregates
{
    /// <summary>
    /// 钱包地址表
    /// </summary>
    [JsonObject(MemberSerialization.OptIn), Table(Name = "wallet_address")]
    public class WalletAddress
    {
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty, Column(Name = "id", IsPrimary = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        [JsonProperty, Column(Name = "member_id")]
        public long MemberId { get; set; }

        /// <summary>
        /// 钱包地址
        /// </summary>
        [JsonProperty, Column(Name = "address")]
        public string Address { get; set; } = "";

        /// <summary>
        /// 私钥Key
        /// </summary>
        [JsonProperty, Column(Name = "private_key")]
        public string PrivateKey { get; set; } = "";

        /// <summary>
        /// 协议类型 1.TRC20 2.ERC20 3.BEP20
        /// </summary>
        [JsonProperty, Column(Name = "agreement_type")]
        public AgreementTypeEnum AgreementType { get; set; }

        /// <summary>
        /// 添加时间
        /// </summary>
        [JsonProperty, Column(Name = "create_time")]
        public long CreateTime { get; set; }

        /// <summary>
        /// 是否归集中 true-是，false-否
        /// </summary>
        [JsonProperty, Column(Name = "is_sweeping")]
        public bool IsSweeping { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        [JsonProperty, Column(Name = "modify_time")]
        public long ModifyTime { get; set; }

        /// <summary>
        /// 归集地址
        /// </summary>
        [JsonProperty, Column(Name = "sweeping_address")]
        public string SweepingAddress { get; set; } = "";

    }
}
