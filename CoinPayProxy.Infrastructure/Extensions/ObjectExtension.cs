using Newtonsoft.Json;

namespace CoinPayProxy.Infrastructure.Extensions
{
    /// <summary>
    /// 与Object相关的扩展函数
    /// </summary>
    public static class ObjectExtension
    {
        /// <summary>
        /// 将某个对象转换为某种类型对象数据。
        /// 如果类型转换失败则返回对应目标类型的默认值，如果目标类型是枚举值，则返回第一个枚举值
        /// </summary>
        /// <typeparam name="T">需要转换的目标类型</typeparam>
        /// <param name="obj">需要转换的对象</param>
        /// <returns>转换后的类型数据</returns>
        public static T As<T>(this object obj)
        {
            if (obj is T variable) return variable;

            var t = typeof(T);

            var convertible = typeof(System.IConvertible);
            if (convertible.IsInstanceOfType(obj) && convertible.IsAssignableFrom(t))
            {
                try
                {
                    return (T)Convert.ChangeType(obj, t);
                }
                catch { }
            }

            T replacement = default(T);
            if (t.IsEnum)
            {
                //枚举类型。则获取第一个默认项
                replacement = (T)Enum.GetValues(t).GetValue(0);
            }
            //else if (t == typeof(string))
            //{
            //    //字符串，则以空字符串为默认值
            //    //replacement = (T)(object)string.Empty;
            //}
            return As<T>(obj, replacement);
        }

        /// <summary>
        /// 将某个对象转换为某种类型对象数据。 如果类型转换失败则返回替换值
        /// </summary>
        /// <typeparam name="T">需要转换的目标类型</typeparam>
        /// <param name="obj">对象</param>
        /// <param name="replacement">如果转换失败则返回此替换值</param>
        /// <returns>转换后的类型数据, 如果类型转换失败则返回<paramref name="replacement"/>表示的替换值</returns>
        public static T As<T>(this object obj, T replacement)
        {
            if (obj is T variable) return variable;

            return (T)obj.As(typeof(T), replacement);
        }

        /// <summary>
        /// 转换为某种类型
        /// </summary>
        /// <param name="obj">对象</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="replacement">如果转换失败则返回此替换值</param>
        /// <returns></returns>
        public static object As(this object obj, Type targetType, object replacement)
        {
            if (obj == null) return replacement;

            var convertible = typeof(System.IConvertible);
            if (convertible.IsInstanceOfType(obj) && convertible.IsAssignableFrom(targetType))
            {
                try
                {
                    return Convert.ChangeType(obj, targetType);
                }
                catch { }
            }

            Type sourceType = obj.GetType();

            if (!targetType.IsInstanceOfType(obj))
            {
                if (sourceType.IsEnum)
                {
                    //枚举类型，则特殊对待
                    try
                    {
                        return Convert.ChangeType(obj, targetType);
                    }
                    catch
                    {
                        return replacement;
                    }
                }
                else
                {
                    var targetTC = Type.GetTypeCode(targetType);
                    switch (targetTC)
                    {
                        case TypeCode.Empty:
                            return null;
                        case TypeCode.Object:
                            return replacement;
                        case TypeCode.DBNull:
                            return DBNull.Value;
                        case TypeCode.String:
                            return obj.ToString();
                        default:
                            bool error;
                            var v = obj.ToString().ConvertTo(targetType, out error);
                            return error ? replacement : v;
                    }
                }
            }
            else
            {
                return obj;
            }
        }

        /// <summary>
        /// 将字符串转换为某个类型值，如果转换失败则返回对应类型的默认值
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="error">是否转换有错误</param>
        /// <returns></returns>
        public static object ConvertTo(this string text, Type targetType, out bool error)
        {
            object result = null;
            error = false;

            TypeCode tc = Type.GetTypeCode(targetType);
            if (targetType.IsEnum)
            {
                //枚举
                try
                {
                    result = Enum.Parse(targetType, text, true);
                }
                catch
                {
                    result = Enum.GetValues(targetType).GetValue(0);
                    error = true;
                }
            }
            else if (tc == TypeCode.String)
            {
                return text;
            }
            else if (tc == TypeCode.DBNull)
            {
                return DBNull.Value;
            }
            else if (tc == TypeCode.Empty)
            {
                return null;
            }
            else if (tc == TypeCode.Char)
            {
                return text[0];
            }
            else if (tc == TypeCode.Boolean)
            {
                //布尔值。则处理两种特殊的值：yes/no; 1/0
                if ("yes".Equals(text, StringComparison.OrdinalIgnoreCase)
                    || "1".Equals(text, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                else if ("no".Equals(text, StringComparison.OrdinalIgnoreCase)
                   || "0".Equals(text, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                else
                {
                    bool b;
                    error = true;
                    result = false;
                    if (bool.TryParse(text, out b))
                    {
                        error = false;
                        result = b;
                    }
                }
            }
            else if (tc == TypeCode.SByte)
            {
                sbyte sb;
                error = true;
                result = (sbyte)0;
                if (sbyte.TryParse(text, out sb))
                {
                    error = false;
                    result = sb;
                }
            }
            else if (tc == TypeCode.Byte)
            {
                byte by;
                error = true;
                result = (byte)0;
                if (byte.TryParse(text, out by))
                {
                    error = false;
                    result = by;
                }
            }
            else if (tc == TypeCode.Int16)
            {
                short i16;
                error = true;
                result = (short)0;
                if (short.TryParse(text, out i16))
                {
                    error = false;
                    result = i16;
                }
            }
            else if (tc == TypeCode.UInt16)
            {
                ushort ui16;
                error = true;
                result = (ushort)0;
                if (ushort.TryParse(text, out ui16))
                {
                    error = false;
                    result = ui16;
                }
            }
            else if (tc == TypeCode.Int32)
            {
                int i32;
                error = true;
                result = 0;
                if (int.TryParse(text, out i32))
                {
                    error = false;
                    result = i32;
                }
            }
            else if (tc == TypeCode.UInt32)
            {
                uint ui32;
                error = true;
                result = (uint)0;
                if (uint.TryParse(text, out ui32))
                {
                    error = false;
                    result = ui32;
                }
            }
            else if (tc == TypeCode.Int64)
            {
                long i64;
                error = true;
                result = (long)0;
                if (long.TryParse(text, out i64))
                {
                    error = false;
                    result = i64;
                }
            }
            else if (tc == TypeCode.UInt64)
            {
                ulong ui64;
                error = true;
                result = (ulong)0;
                if (ulong.TryParse(text, out ui64))
                {
                    error = false;
                    result = ui64;
                }
            }
            else if (tc == TypeCode.Single)
            {
                Single s;
                error = true;
                result = (Single)0;
                if (Single.TryParse(text, out s))
                {
                    error = false;
                    result = s;
                }
            }
            else if (tc == TypeCode.Double)
            {
                Double d;
                error = true;
                result = (Double)0;
                if (Double.TryParse(text, out d))
                {
                    error = false;
                    result = d;
                }
            }
            else if (tc == TypeCode.Decimal)
            {
                Decimal d1;
                error = true;
                result = (Decimal)0;
                if (Decimal.TryParse(text, out d1))
                {
                    error = false;
                    result = d1;
                }
            }
            else if (tc == TypeCode.DateTime)
            {
                DateTime dt;
                error = true;
                result = DateTime.MinValue;
                if (DateTime.TryParse(text, out dt))
                {
                    error = false;
                    result = dt;
                }
            }
            else
            {
                try
                {
                    result = Convert.ChangeType(text, targetType, System.Globalization.CultureInfo.InvariantCulture);
                }
                catch
                {
                    error = true;
                    result = null;
                }
            }
            return result;
        }

        public static string ToJson(this object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.None, new JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                //DateFormatString = "yyy-MM-dd HH:mm:ss"
            });
        }
    }
}
