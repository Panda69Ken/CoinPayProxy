using CoinPayProxy.Infrastructure.Enums;
using FreeSql.DataAnnotations;
using Newtonsoft.Json;

namespace CoinPayProxy.Domain.Aggregates
{
    /// <summary>
    /// 交易流水表
    /// </summary>
    [JsonObject(MemberSerialization.OptIn), Table(Name = "transaction_record")]
    public class TransactionRecord
    {
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty, Column(Name = "id", IsPrimary = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// 用户ID  充U、充Gas(到账人)、归集U(出账人)、回收Gas
        /// </summary>
        [JsonProperty, Column(Name = "member_id")]
        public long MemberId { get; set; }

        /// <summary>
        /// 交易哈希ID
        /// </summary>
        [JsonProperty, Column(Name = "hash_id")]
        public string HashId { get; set; } = "";

        /// <summary>
        /// 发送人地址
        /// </summary>
        [JsonProperty, Column(Name = "from_address")]
        public string FromAddress { get; set; } = "";

        /// <summary>
        /// 接收人地址
        /// </summary>
        [JsonProperty, Column(Name = "to_address")]
        public string ToAddress { get; set; } = "";

        /// <summary>
        /// 协议类型 1.TRC20 2.ERC20 3.BEP20
        /// </summary>
        [JsonProperty, Column(Name = "agreement_type")]
        public AgreementTypeEnum AgreementType { get; set; }

        /// <summary>
        /// 合约地址
        /// </summary>
        [JsonProperty, Column(Name = "contract_address")]
        public string ContractAddress { get; set; } = "";

        /// <summary>
        /// USDT\Token数量
        /// </summary>
        [JsonProperty, Column(Name = "amount")]
        public decimal Amount { get; set; }

        /// <summary>
        /// 状态 1.成功 2.转账中 3.失败 4.归集失败
        /// </summary>
        [JsonProperty, Column(Name = "status")]
        public TransactionStatusEnum Status { get; set; }

        /// <summary>
        /// 币种单位
        /// </summary>
        [JsonProperty, Column(Name = "currency")]
        public string Currency { get; set; } = "";

        /// <summary>
        /// Token手续费
        /// </summary>
        [JsonProperty, Column(Name = "fee")]
        public decimal Fee { get; set; }

        /// <summary>
        /// Token手续费币种单位
        /// </summary>
        [JsonProperty, Column(Name = "fee_currency")]
        public string FeeCurrency { get; set; } = "";

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonProperty, Column(Name = "create_time")]
        public long CreateTime { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        [JsonProperty, Column(Name = "modify_time")]
        public long ModifyTime { get; set; }

        /// <summary>
        /// 交易时间
        /// </summary>
        [JsonProperty, Column(Name = "transaction_time")]
        public long TransactionTime { get; set; }

        /// <summary>
        /// 交易类型  1.充币 2.充值能量 3.归集 4.回收能量
        /// </summary>
        [JsonProperty, Column(Name = "transaction_type")]
        public TransactionTypeEnum TransactionType { get; set; }

        /// <summary>
        /// 区块编号
        /// </summary>
        [JsonProperty, Column(Name = "block_number")]
        public long BlockNumber { get; set; }

    }
}
