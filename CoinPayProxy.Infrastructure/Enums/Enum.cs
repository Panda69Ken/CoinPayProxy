namespace CoinPayProxy.Infrastructure.Enums
{
    public enum ErrorCodeEnum
    {
        None = 0,
        成功 = 1,
        参数错误 = 1000,
        状态错误 = 1001,
        数据不存在 = 2001,
        数据已存在 = 2002,
        数据库执行异常错误 = 5001,
    }

    public enum AgreementTypeEnum
    {
        None = 0,
        TRC20 = 1,
        ERC20 = 2,
        BEP20 = 3,
    }

    public enum TransactionStatusEnum
    {
        None = 0,
        Processing = 1,
        Success = 2,
        Fail = 3,
        SweepingFail = 4,
    }

    public enum TransactionTypeEnum
    {
        None = 0,
        /// <summary>
        /// USDT充值
        /// </summary>
        RechargeUSDT = 1,
        /// <summary>
        /// 充值Token
        /// </summary>
        RechargeToken = 2,
        /// <summary>
        /// 归集USDT
        /// </summary>
        SweepingUSDT = 3,
        /// <summary>
        /// 回收Token
        /// </summary>
        RecycleToken = 4,
    }

    public enum CurrencyEnum
    {
        USDT,
        TRX,
        ETH,
        BNB
    }

}
