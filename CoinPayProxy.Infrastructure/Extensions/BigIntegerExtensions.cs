using System.Numerics;

namespace CoinPayProxy.Infrastructure.Extensions
{
    public static class BigIntegerExtensions
    {
        /// <summary>
        /// 尝试将 BigInteger 转换为 long，成功返回 true。
        /// </summary>
        public static bool TryToInt64(this BigInteger value, out long result)
        {
            if (value >= long.MinValue && value <= long.MaxValue)
            {
                result = (long)value;
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>
        /// 安全转换为 long，超出范围时返回默认值（默认为 0）。
        /// </summary>
        public static long ToInt64OrDefault(this BigInteger value, long defaultValue = 0)
        {
            return value.TryToInt64(out long result) ? result : defaultValue;
        }

        /// <summary>
        /// 尝试将 BigInteger 转换为 int。
        /// </summary>
        public static bool TryToInt32(this BigInteger value, out int result)
        {
            if (value >= int.MinValue && value <= int.MaxValue)
            {
                result = (int)value;
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>
        /// 安全转换为 int，超出范围返回默认值（默认为 0）。
        /// </summary>
        public static int ToInt32OrDefault(this BigInteger value, int defaultValue = 0)
        {
            return value.TryToInt32(out int result) ? result : defaultValue;
        }

        /// <summary>
        /// 判断 BigInteger 是否在 long 范围内。
        /// </summary>
        public static bool IsInInt64Range(this BigInteger value)
        {
            return value >= long.MinValue && value <= long.MaxValue;
        }

        /// <summary>
        /// 判断 BigInteger 是否在 int 范围内。
        /// </summary>
        public static bool IsInInt32Range(this BigInteger value)
        {
            return value >= int.MinValue && value <= int.MaxValue;
        }
    }
}
