using FreeSql.DataAnnotations;

namespace CoinPayProxy.Domain.Aggregates
{
    /// <summary>
    /// 最新区块号
    /// </summary>
    [Table(Name = "block_number")]
    public class BlockNumber
    {
        [Column(Name = "key", IsPrimary = true)]
        public string Key { get; set; } = "";

        [Column(Name = "value")]
        public long Value { get; set; }
    }
}
