using CoinPayProxy.Infrastructure.Enums;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using TronNet.Crypto;
using TronNet.Protocol;

namespace CoinPayProxy.Infrastructure.Extensions
{
    public static class Utils
    {
        static readonly DateTime startTime = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long GetTimeStamp(this DateTime time)
        {
            return (time.Ticks - startTime.Ticks) / 10000000;
        }

        public static DateTime GetTime(this long value)
        {
            return startTime.AddSeconds(value);
        }

        public static string ReplaceFirst(this string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            var reg = new Regex("0x");
            return reg.Replace(value, "41", 1).EncodeFromHex();
        }

        static string EncodeFromHex(this string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            var hexByte = Enumerable.Range(0, value.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(value.Substring(x, 2), 16))
            .ToArray(); ;

            return Base58Encoder.EncodeFromHex(hexByte, 65);
        }

        #region ParameterValueToJson
        public static string ParameterValueToJson(this Google.Protobuf.WellKnownTypes.Any param)
        {
            if (param == null) return "";

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };

                if (param.Is(TransferContract.Descriptor))
                {
                    var transfer = param.Unpack<TransferContract>();
                    var result = new Dictionary<string, object?>
                    {
                        ["fromAddress"] = transfer.OwnerAddress != null ? Convert.ToHexString(transfer.OwnerAddress.ToByteArray()).ToLowerInvariant().ReplaceFirst() : null,
                        ["toAddress"] = transfer.ToAddress != null ? Convert.ToHexString(transfer.ToAddress.ToByteArray()).ToLowerInvariant().ReplaceFirst() : null,
                        ["amount"] = transfer.Amount,
                        ["symbol"] = CurrencyEnum.TRX.ToString(),
                    };
                    return JsonSerializer.Serialize(result, options);
                }
                if (param.Is(TriggerSmartContract.Descriptor))
                {
                    var sc = param.Unpack<TriggerSmartContract>();
                    var decoded = DecodeTriggerSmartContract(sc);
                    return JsonSerializer.Serialize(decoded, options);
                }
            }
            catch
            {
                //忽略解包失败并继续执行
            }

            try
            {
                var text = param.Value.ToStringUtf8();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
            catch
            {
                Console.WriteLine("无法将参数解码为 JSON, 回退到十六进制");
            }

            return Convert.ToHexString(param.Value.ToByteArray()).ToLowerInvariant();
        }

        private static object DecodeTriggerSmartContract(TriggerSmartContract sc)
        {
            var result = new Dictionary<string, object?>
            {
                ["fromAddress"] = sc.OwnerAddress != null ? Convert.ToHexString(sc.OwnerAddress.ToByteArray()).ToLowerInvariant().ReplaceFirst() : null
            };

            // 识别合约地址（用于区分 USDT、USDC 等不同的 token）
            var contractAddressHex = sc.ContractAddress != null ? Convert.ToHexString(sc.ContractAddress.ToByteArray()).ToLowerInvariant() : null;
            var contractAddress = contractAddressHex != null ? ("41" + contractAddressHex).ReplaceFirst() : null;

            if (contractAddress == "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t")
            {
                var data = sc.Data?.ToByteArray() ?? [];

                var selector = Convert.ToHexString(data.AsSpan(0, 4)).ToLowerInvariant();
                if (selector == "a9059cbb") //transfer(address,uint256)
                {
                    if (data.Length < 4 + 32 + 32) result["error"] = "data too short";

                    var toBytes32 = data.AsSpan(4, 32).ToArray();
                    var valueBytes32 = data.AsSpan(36, 32).ToArray();
                    var to20 = toBytes32.Skip(12).Take(20).ToArray();
                    var tronHex = "41" + Convert.ToHexString(to20).ToLowerInvariant();
                    var amount = BigIntegerFromUnsignedBigEndian(valueBytes32);

                    result["toAddress"] = tronHex.ReplaceFirst();
                    result["amount"] = amount.ToString();
                    result["symbol"] = CurrencyEnum.USDT.ToString();
                    result["contract"] = contractAddress;
                }
            }

            return result;
        }

        private static BigInteger BigIntegerFromUnsignedBigEndian(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return BigInteger.Zero;
            var le = bytes.Reverse().ToArray().Concat(new byte[] { 0 }).ToArray();
            return new BigInteger(le);
        }
        #endregion

        // 判断是否为合理的 Unix 毫秒时间戳
        public static bool IsPlausibleUnixMilliseconds(long ms, TimeSpan? maxPast = null, TimeSpan? maxFuture = null)
        {
            try
            {
                var dto = DateTimeOffset.FromUnixTimeMilliseconds(ms);
                var now = DateTimeOffset.UtcNow;

                // 默认：允许过去 100 年，允许未来 1 天（可按场景调整）
                maxPast ??= TimeSpan.FromDays(365 * 100);
                maxFuture ??= TimeSpan.FromDays(1);

                if (dto < DateTimeOffset.UnixEpoch) return false;
                if (dto < now - maxPast.Value) return false;
                if (dto > now + maxFuture.Value) return false;

                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

    }
}
