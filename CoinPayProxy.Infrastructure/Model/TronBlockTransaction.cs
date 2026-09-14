using CoinPayProxy.Infrastructure.Enums;

namespace CoinPayProxy.Infrastructure.Model
{
    public class TronBlockTransaction
    {
        public string FromAddress { get; set; } = "";
        public string ToAddress { get; set; } = "";
        public string Contract { get; set; } = "";
        public long Amount { get; set; }
        public CurrencyEnum Symbol { get; set; }
    }
}
