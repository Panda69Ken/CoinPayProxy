using CoinPayProxy.Domain.Aggregates;
using CoinPayProxy.Domain.DomainService;
using CoinPayProxy.Domain.QueryServices;
using CoinPayProxy.Infrastructure.Blockchain;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Infrastructure.Extensions;
using CoinPayProxy.Rpc.Core.MR;
using CoinPayProxy.Rpc.Core.Service;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using System.Globalization;

namespace CoinPayProxy.Rpc.Services
{
    public class CoinPayProxyerService(ILogger<CoinPayProxyerService> logger,
        IMediator mediator,
        IConfigService config,
        BlockchainQueries queries,
        WalletAddressDomainService walletAddress,
        TronNetRecord tron,
        Func<AgreementTypeEnum, InfuraNetRecord> infuraFactory) : CoinPayProxyer.CoinPayProxyerBase
    {
        private readonly ILogger<CoinPayProxyerService> _logger = logger;
        readonly IMediator _mediator = mediator;
        readonly IConfigService _config = config;
        readonly BlockchainQueries _queries = queries;
        readonly WalletAddressDomainService _walletAddress = walletAddress;
        readonly TronNetRecord _tron = tron;
        readonly Func<AgreementTypeEnum, InfuraNetRecord> _infuraFactory = infuraFactory;

        public override async Task<GetAgreementConfigReply> GetAgreementConfig(Empty request, ServerCallContext context)
        {
            var transactions1 = await _infuraFactory(AgreementTypeEnum.ERC20).GetUsdtTransactions(23767938L);
            var trans1 = transactions1.FirstOrDefault(a => a.HashId == "0xcc23f92cf7560afabaa5c43a4103fcfbd4fd2e3546038b117b3e23443e80681f");
            //var receipt1 = await _infuraFactory(AgreementTypeEnum.ERC20).GetUsdtTransaction("0xcc23f92cf7560afabaa5c43a4103fcfbd4fd2e3546038b117b3e23443e80681f");
            //var gasUsed1 = receipt1.GasUsed.Value;
            //var effectiveGasPrice1 = receipt1.EffectiveGasPrice?.Value ?? 0;
            //var gasCostWei1 = gasUsed1 * effectiveGasPrice1;
            //var fee1 = Web3.Convert.FromWei(gasCostWei1);


            var transactions2 = await _infuraFactory(AgreementTypeEnum.BEP20).GetUsdtTransactions(121251415L);
            var trans2 = transactions2.FirstOrDefault(a => a.HashId == "0xe550f2189e40df27c9015be2a5f6d12b0467c64b814c27a1e43950042bb344ee");
            //var receipt2 = await _infuraFactory(AgreementTypeEnum.BEP20).GetUsdtTransaction("0xe550f2189e40df27c9015be2a5f6d12b0467c64b814c27a1e43950042bb344ee");
            //var gasUsed2 = receipt2.GasUsed.Value;
            //var effectiveGasPrice2 = receipt2.EffectiveGasPrice?.Value ?? 0;
            //var gasCostWei2 = gasUsed2 * effectiveGasPrice2;
            //var fee2 = Web3.Convert.FromWei(gasCostWei2);



            var reply = new GetAgreementConfigReply();

            var agreementConfig = _config.AgreementConfig;

            foreach (var item in agreementConfig)
            {
                var entity = new RpcAgreementConfig
                {
                    Contract = item.Value.Contract,
                    WebsiteUrl = item.Value.WebsiteUrl,
                    SweepingAddress = { item.Value.SweepingAddress },
                    RechargeTokens = { item.Value.RechargeTokens.Select(a => new RpcRechargeTokensInfo {
                        TokenAddress = a.TokenAddress,
                        PrivateKey = a.PrivateKey
                    })},
                };

                switch (item.Key)
                {
                    case AgreementTypeEnum.TRC20:
                        entity.Currency = RpcCurrencyEnum.Trx;
                        break;
                    case AgreementTypeEnum.ERC20:
                        entity.Currency = RpcCurrencyEnum.Eth;
                        break;
                    case AgreementTypeEnum.BEP20:
                        entity.Currency = RpcCurrencyEnum.Bnb;
                        break;
                }

                reply.Items.Add(item.Key.ToString(), entity);
            }

            reply.Result = true;

            return await Task.FromResult(reply);
        }

        public override async Task<GenerateAddressReply> GenerateAddress(GenerateAddressRequest request, ServerCallContext context)
        {
            var reply = new GenerateAddressReply() { Result = false };

            if (request.AgreementType == RpcAgreementTypeEnum.Default) return new GenerateAddressReply();

            var entity = await _queries.GetWalletAddress(request.MemberId, (AgreementTypeEnum)request.AgreementType);

            if (entity == null)
            {
                string address = "", privateKey = "";

                switch (request.AgreementType)
                {
                    case RpcAgreementTypeEnum.Trc20:
                        if (request.MemberId <= int.MaxValue)
                        {
                            var iAccount = _tron.GenerateAccount((int)request.MemberId);
                            address = iAccount.Address;
                            privateKey = iAccount.PrivateKey;
                        }
                        else
                        {
                            var iAccount = _tron.GenerateAccount();
                            address = iAccount.Address;
                            privateKey = iAccount.PrivateKey;
                        }
                        break;
                    case RpcAgreementTypeEnum.Erc20:
                    case RpcAgreementTypeEnum.Bep20:
                        if (request.MemberId <= int.MaxValue)
                        {
                            var iAccountEvm = _infuraFactory((AgreementTypeEnum)request.AgreementType).GenerateAccount((int)request.MemberId);
                            address = iAccountEvm.Address.ToLower();
                            privateKey = iAccountEvm.PrivateKey;
                        }
                        else
                        {
                            var iAccountEvm = _infuraFactory((AgreementTypeEnum)request.AgreementType).GenerateAccount();
                            address = iAccountEvm.Address;
                            privateKey = iAccountEvm.PrivateKey;
                        }
                        break;
                }

                if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(privateKey))
                {
                    _logger.LogWarning($"生成钱包地址失败，param:{request.ToJson()}");
                    return reply;
                }

                entity = new WalletAddress
                {
                    MemberId = request.MemberId,
                    Address = address,
                    PrivateKey = privateKey,
                    AgreementType = (AgreementTypeEnum)request.AgreementType,
                    CreateTime = DateTime.UtcNow.GetTimeStamp()
                };

                var result = await _walletAddress.GenerateAddress(entity);

                if (result != ErrorCodeEnum.成功)
                {
                    _logger.LogWarning($"保存钱包地址失败，result:{result}");
                    return reply;
                }
            }

            reply.Result = true;

            reply.Items = new RpcWalletAddress
            {
                MemberId = request.MemberId,
                Address = entity.Address,
                PrivateKey = entity.PrivateKey,
                AgreementType = request.AgreementType,
                CreateTime = entity.CreateTime
            };

            return reply;
        }

        public override async Task<GetBalanceOfTokenReply> GetBalanceOfToken(GetBalanceOfTokenRequest request, ServerCallContext context)
        {
            var reply = new GetBalanceOfTokenReply
            {
                MemberId = request.MemberId,
                Amount = "0"
            };

            if (request.AgreementType == RpcAgreementTypeEnum.Default) return reply;

            var walletAddress = await _queries.GetWalletAddress(request.MemberId, (AgreementTypeEnum)request.AgreementType);
            if (walletAddress == null)
            {
                _logger.LogWarning($"用户不存在{request.AgreementType.ToString().ToUpper()}地址,param:{request.ToJson()}");
                return reply;
            }

            reply.Address = walletAddress.Address;

            switch (request.AgreementType)
            {
                case RpcAgreementTypeEnum.Trc20:
                    var result = await _tron.BalanceOfTokenAsync(walletAddress.Address);
                    reply.Currency = RpcCurrencyEnum.Trx;
                    reply.Amount = result.ToString(CultureInfo.InvariantCulture);
                    break;
                case RpcAgreementTypeEnum.Erc20:
                case RpcAgreementTypeEnum.Bep20:
                    var resultEvm = await _infuraFactory((AgreementTypeEnum)request.AgreementType).BalanceOfTokenAsync(walletAddress.Address);
                    reply.Currency = request.AgreementType == RpcAgreementTypeEnum.Erc20 ? RpcCurrencyEnum.Eth : RpcCurrencyEnum.Bnb;
                    reply.Amount = resultEvm.ToString(CultureInfo.InvariantCulture);
                    break;
            }

            return reply;
        }

        public override async Task<GetBalanceOfUSDTReply> GetBalanceOfUSDT(GetBalanceOfUSDTRequest request, ServerCallContext context)
        {
            var reply = new GetBalanceOfUSDTReply
            {
                MemberId = request.MemberId,
                Amount = "0"
            };

            if (request.AgreementType == RpcAgreementTypeEnum.Default) return reply;

            var walletAddress = await _queries.GetWalletAddress(request.MemberId, (AgreementTypeEnum)request.AgreementType);
            if (walletAddress == null)
            {
                _logger.LogWarning($"用户不存在{request.AgreementType.ToString().ToUpper()}地址,param:{request.ToJson()}");
                return reply;
            }

            reply.Address = walletAddress.Address;

            switch (request.AgreementType)
            {
                case RpcAgreementTypeEnum.Trc20:
                    var result = await _tron.BalanceOfUSDTAsync(walletAddress.Address);
                    reply.Amount = result.ToString(CultureInfo.InvariantCulture);
                    break;
                case RpcAgreementTypeEnum.Erc20:
                case RpcAgreementTypeEnum.Bep20:
                    var resultEvm = await _infuraFactory((AgreementTypeEnum)request.AgreementType)
                        .BalanceOfUSDTAsync(walletAddress.Address);
                    reply.Amount = resultEvm.ToString(CultureInfo.InvariantCulture);
                    break;
            }

            return reply;
        }

        public override async Task<RpcReply> TokenTransfer(TokenTransferRequest request, ServerCallContext context)
        {
            var reply = new RpcReply() { Result = false };

            if (request.AgreementType == RpcAgreementTypeEnum.Default) return reply;

            if (string.IsNullOrEmpty(request.TokenAddress))
            {
                _logger.LogWarning($"{request.TokenAddress}转Token地址为空,param:{request.ToJson()}");
                return reply;
            }

            string privateKey = _config.AgreementConfig[(AgreementTypeEnum)request.AgreementType]
                .RechargeTokens.FirstOrDefault(a => a.TokenAddress == request.TokenAddress)?.PrivateKey;

            if (string.IsNullOrEmpty(privateKey))
            {
                _logger.LogWarning($"{request.TokenAddress}转Token地址错误,param:{request.ToJson()}");
                return reply;
            }

            //转Token地址
            string from = "";
            switch (request.AgreementType)
            {
                case RpcAgreementTypeEnum.Trc20:
                    from = _tron.GetAddress(privateKey);
                    break;
                case RpcAgreementTypeEnum.Erc20:
                case RpcAgreementTypeEnum.Bep20:
                    from = _infuraFactory((AgreementTypeEnum)request.AgreementType).GetAddress(privateKey);
                    break;
            }

            if (from != request.TokenAddress)
            {
                _logger.LogWarning($"{request.TokenAddress}转Token地址密钥错误,param:{request.ToJson()}");
                return reply;
            }

            //到账地址
            var walletAddress = await _queries.GetWalletAddress(request.MemberId, (AgreementTypeEnum)request.AgreementType);
            if (walletAddress == null)
            {
                _logger.LogWarning($"用户不存在{request.AgreementType.ToString().ToUpper()}地址,param:{request.ToJson()}");
                return reply;
            }

            var to = walletAddress.Address;

            var amount = decimal.Parse(request.Amount);

            var accountToken = 0M;

            //查询转Token地址的资产
            switch (request.AgreementType)
            {
                case RpcAgreementTypeEnum.Trc20:
                    var iAccount = _tron.GetAccount(privateKey);
                    accountToken = await _tron.BalanceOfTokenAsync(iAccount);
                    break;
                case RpcAgreementTypeEnum.Erc20:
                case RpcAgreementTypeEnum.Bep20:
                    var iAccountEvm = _infuraFactory((AgreementTypeEnum)request.AgreementType).GetAccount(privateKey);
                    accountToken = await _infuraFactory((AgreementTypeEnum)request.AgreementType).BalanceOfTokenAsync(iAccountEvm);
                    break;
            }

            if (accountToken < amount)
            {
                _logger.LogWarning($"发起Token交易失败,转币地址Token不足:{from},param:{request.ToJson()}");
                return reply;
            }

            reply.Result = await _mediator.Send(new TokenTransferCommand
            {
                MemberId = request.MemberId,
                Amount = amount,
                FromAddress = from,
                FromPrivateKey = privateKey,
                ToAdress = to,
                AgreementType = (AgreementTypeEnum)request.AgreementType,
                TransactionType = TransactionTypeEnum.RechargeToken,
            });

            return reply;
        }

        public override async Task<RpcReply> TokenRecycle(TokenRecycleRequest request, ServerCallContext context)
        {
            var reply = new RpcReply() { Result = false };

            if (request.AgreementType == RpcAgreementTypeEnum.Default) return reply;

            if (string.IsNullOrEmpty(request.RecycleAddress))
            {
                _logger.LogWarning($"{request.RecycleAddress}收币地址为空,param:{request.ToJson()}");
                return reply;
            }

            string privateKey = _config.AgreementConfig[(AgreementTypeEnum)request.AgreementType]
                .RechargeTokens.FirstOrDefault(a => a.TokenAddress == request.RecycleAddress)?.PrivateKey;

            if (string.IsNullOrEmpty(privateKey))
            {
                _logger.LogWarning($"{request.RecycleAddress}收币地址错误,param:{request.ToJson()}");
                return reply;
            }

            //收币地址
            string to = "";
            switch (request.AgreementType)
            {
                case RpcAgreementTypeEnum.Trc20:
                    to = _tron.GetAddress(privateKey);
                    break;
                case RpcAgreementTypeEnum.Erc20:
                case RpcAgreementTypeEnum.Bep20:
                    to = _infuraFactory((AgreementTypeEnum)request.AgreementType).GetAddress(privateKey);
                    break;
            }

            if (to != request.RecycleAddress)
            {
                _logger.LogWarning($"{request.RecycleAddress}收币地址密钥错误,param:{request.ToJson()}");
                return reply;
            }

            //回收地址
            var walletAddress = await _queries.GetWalletAddress(request.MemberId, (AgreementTypeEnum)request.AgreementType);

            if (walletAddress == null)
            {
                _logger.LogWarning($"用户不存在{request.AgreementType.ToString().ToUpper()}地址,param:{request.ToJson()}");
                return reply;
            }

            var from = walletAddress.Address;

            var amount = decimal.Parse(request.Amount);

            var accountToken = 0M;

            //查询回收地址的资产
            switch (request.AgreementType)
            {
                case RpcAgreementTypeEnum.Trc20:
                    var iAccount = _tron.GetAccount(walletAddress.PrivateKey);
                    accountToken = await _tron.BalanceOfTokenAsync(iAccount);
                    break;
                case RpcAgreementTypeEnum.Erc20:
                case RpcAgreementTypeEnum.Bep20:
                    var iAccountEvm = _infuraFactory((AgreementTypeEnum)request.AgreementType).GetAccount(walletAddress.PrivateKey);
                    accountToken = await _infuraFactory((AgreementTypeEnum)request.AgreementType).BalanceOfTokenAsync(iAccountEvm);
                    break;
            }

            if (accountToken < amount)
            {
                _logger.LogWarning($"发起Token交易失败,回收地址Token不足:{from},param:{request.ToJson()}");
                return reply;
            }

            reply.Result = await _mediator.Send(new TokenTransferCommand
            {
                MemberId = request.MemberId,
                Amount = amount,
                FromAddress = from,
                FromPrivateKey = walletAddress.PrivateKey,
                ToAdress = to,
                AgreementType = (AgreementTypeEnum)request.AgreementType,
                TransactionType = TransactionTypeEnum.RecycleToken,
            });

            return reply;
        }

        public override async Task<RpcReply> UsdtSweeping(UsdtSweepingRequest request, ServerCallContext context)
        {
            var reply = new RpcReply() { Result = false };

            if (request.AgreementType == RpcAgreementTypeEnum.Default) return reply;

            (reply.Result, reply.Msg) = await _mediator.Send(new UsdtTransferCommand
            {
                MemberId = request.MemberId,
                Amount = decimal.Parse(request.Amount),
                AgreementType = (AgreementTypeEnum)request.AgreementType,
                SweepingAddress = request.SweepingAddress
            });

            return reply;
        }

        public override async Task<RpcReply> BatchUsdtSweeping(BatchUsdtSweepingRequest request, ServerCallContext context)
        {
            var reply = new RpcReply() { Result = false };

            if (request.AgreementType == RpcAgreementTypeEnum.Default) return reply;

            if (request.UsdtNum <= 0 || request.TokenNum <= 0 || string.IsNullOrEmpty(request.SweepingAddress))
            {
                reply.Msg = ErrorCodeEnum.参数错误.ToString();
                return reply;
            }

            var reply1 = await _queries.GetUSDTAndTokenGteNumList((decimal)request.UsdtNum, (decimal)request.TokenNum, (AgreementTypeEnum)request.AgreementType);
            if (reply1.Count == 0)
            {
                reply.Result = true;
                return reply;
            }

            var memberIds = reply1.Select(a => a.MemberId).ToList();

            var result = await _walletAddress.UpdateCollectStatus(memberIds, (AgreementTypeEnum)request.AgreementType, true, request.SweepingAddress);

            reply.Result = result == ErrorCodeEnum.成功;

            return reply;
        }

        public override async Task<RpcReply> BatchTokenRecycle(BatchTokenRecycleRequest request, ServerCallContext context)
        {
            var reply = new RpcReply() { Result = false };

            if (request.AgreementType == RpcAgreementTypeEnum.Default) return reply;

            if (string.IsNullOrEmpty(request.RecycleAddress))
            {
                reply.Msg = ErrorCodeEnum.参数错误.ToString();
                return reply;
            }

            var token = 0M;
            switch (request.AgreementType)
            {
                case RpcAgreementTypeEnum.Trc20:
                    token = 1M;
                    break;
                case RpcAgreementTypeEnum.Erc20:
                    token = 0.0001M;
                    break;
                case RpcAgreementTypeEnum.Bep20:
                    token = 0.00002M;
                    break;
            }

            var reply1 = await _queries.GetTokenGteNumList(token, (AgreementTypeEnum)request.AgreementType);
            if (reply1.Count == 0)
            {
                reply.Result = true;
                return reply;
            }

            var memberIds = reply1.Select(a => a.MemberId).ToList();

            var dic = new Dictionary<long, decimal>();

            foreach (var item in memberIds)
            {
                var tokenAmount = await _queries.GetToken(item, (AgreementTypeEnum)request.AgreementType);

                //TRC20可以利用24小时内免费的600带宽用于TRX回收交易的手续费
                //ERC20预留0.0002用于ERT回收交易的手续费
                //BEP20预留0.0001用于BNB回收交易的手续费
                if (request.AgreementType == RpcAgreementTypeEnum.Erc20)
                {
                    tokenAmount -= 0.0002M;
                }
                if (request.AgreementType == RpcAgreementTypeEnum.Bep20)
                {
                    tokenAmount -= 0.0001M;
                }

                if (tokenAmount > 0) dic.TryAdd(item, tokenAmount);
            }

            foreach (var item in dic)
            {
                var param = new TokenRecycleRequest
                {
                    MemberId = item.Key,
                    Amount = item.Value.ToString(),
                    RecycleAddress = request.RecycleAddress,
                    AgreementType = request.AgreementType
                };

                var result = await TokenRecycle(param, context);

                if (result.Result == false)
                {
                    _logger.LogWarning($"回收能量失败,msg:{result.Msg},param:{param.ToJson()}");
                }

                //降低触发Coin接口限制风控，可以考虑优化为类似USDT归集的后台服务
                await Task.Delay(200);
            }

            reply.Result = true;

            return reply;
        }

        public override async Task<GetMemberWalletListReply> GetMemberWalletList(GetMemberWalletListRequest request, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(request.Address) == false)
            {
                var wallet = await _queries.GetWalletAddress(request.Address, (AgreementTypeEnum)request.AgreementType);

                if (wallet == null)
                {
                    return new GetMemberWalletListReply
                    {
                        Total = 0,
                    };
                }

                request.MemberId = wallet.MemberId;
            }

            var data = await _queries.GetMemberWalletList(request.Page, request.Limit, request.MemberId, [.. request.MemberIds],
                (AgreementTypeEnum)request.AgreementType, request.OrderBy, request.Desc);


            var reply = new GetMemberWalletListReply
            {
                Items = { data.Item1.Select(a => new RpcMemberWalletDto {
                    MemberId = a.MemberId,
                    AgreementType = (RpcAgreementTypeEnum)a.AgreementType,
                    Address = a.Address,
                    UsdtBalance = a.UsdtBalance.As<string>(),
                    TokenBalance = a.TokenBalance.As<string>(),
                    SweepingBalance = a.SweepingBalance.As<string>(),
                    SweepingTime = a.SweepingTime,
                    IsSweeping = a.IsSweeping,
                    LastRechargeAmount = a.LastRechargeAmount.As<string>(),
                    LastRechargeTime = a.LastRechargeTime
                }) },
                Total = data.Item2
            };

            return reply;
        }

    }
}
