using CoinPayProxy.Rpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;

namespace TestProject1
{
    [TestClass]
    public sealed class ChainApierTest
    {
        CoinPayProxyer.CoinPayProxyerClient _coinPayProxyerClient;

        [TestInitialize]
        public void Init()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            GrpcChannel channel = GrpcChannel.ForAddress("http://localhost:5007");
            _coinPayProxyerClient = new CoinPayProxyer.CoinPayProxyerClient(channel);
        }

        [TestMethod]
        public async Task GetAgreementConfig()
        {
            var result = await _coinPayProxyerClient.GetAgreementConfigAsync(new Empty());

            Console.WriteLine(result);
        }

        [TestMethod]
        public async Task GenerateAddress()
        {
            var result = await _coinPayProxyerClient.GenerateAddressAsync(new GenerateAddressRequest
            {
                MemberId = 1,
                AgreementType = RpcAgreementTypeEnum.Trc20
                //AgreementType = RpcAgreementTypeEnum.Erc20
                //AgreementType = RpcAgreementTypeEnum.Bep20
            });

            Console.WriteLine(result);
        }

        [TestMethod]
        public async Task GetBalanceOfToken()
        {
            var result = await _coinPayProxyerClient.GetBalanceOfTokenAsync(new GetBalanceOfTokenRequest
            {
                MemberId = 1000,
                AgreementType = RpcAgreementTypeEnum.Trc20
                //AgreementType = RpcAgreementTypeEnum.Erc20
                //AgreementType = RpcAgreementTypeEnum.Bep20
            });

            Console.WriteLine(result);

        }

        [TestMethod]
        public async Task GetBalanceOfUSDT()
        {
            var result = await _coinPayProxyerClient.GetBalanceOfUSDTAsync(new GetBalanceOfUSDTRequest
            {
                MemberId = 1000,
                AgreementType = RpcAgreementTypeEnum.Trc20
                //AgreementType = RpcAgreementTypeEnum.Erc20
                //AgreementType = RpcAgreementTypeEnum.Bep20
            });

            Console.WriteLine(result);

        }

        [TestMethod]
        public async Task TokenTransfer()
        {
            var result = await _coinPayProxyerClient.TokenTransferAsync(new TokenTransferRequest
            {
                MemberId = 2000,
                Amount = "0.1",
                TokenAddress = "TAkyQrhTAu8gm9Z4n9QmiwXXKec2Av1VTz",
                AgreementType = RpcAgreementTypeEnum.Trc20
                //AgreementType = RpcAgreementTypeEnum.Erc20
                //AgreementType = RpcAgreementTypeEnum.Bep20
            });

            Console.WriteLine(result);
        }

        [TestMethod]
        public async Task TokenRecycle()
        {
            var result = await _coinPayProxyerClient.TokenRecycleAsync(new TokenRecycleRequest
            {
                MemberId = 2000,
                Amount = "0.1",
                RecycleAddress = "TAkyQrhTAu8gm9Z4n9QmiwXXKec2Av1VTz",
                AgreementType = RpcAgreementTypeEnum.Trc20
                //AgreementType = RpcAgreementTypeEnum.Erc20
                //AgreementType = RpcAgreementTypeEnum.Bep20
            });

            Console.WriteLine(result);
        }

        [TestMethod]
        public async Task UsdtSweeping()
        {
            var result = await _coinPayProxyerClient.UsdtSweepingAsync(new UsdtSweepingRequest
            {
                MemberId = 2000,
                Amount = "0.1",
                SweepingAddress = "TAkyQrhTAu8gm9Z4n9QmiwXXKec2Av1VTz",
                AgreementType = RpcAgreementTypeEnum.Trc20
                //AgreementType = RpcAgreementTypeEnum.Erc20
                //AgreementType = RpcAgreementTypeEnum.Bep20
            });

            Console.WriteLine(result);
        }

        [TestMethod]
        public async Task BatchUsdtSweeping()
        {
            var result = await _coinPayProxyerClient.BatchUsdtSweepingAsync(new BatchUsdtSweepingRequest
            {
                UsdtNum = 10,
                TokenNum = 30,

                SweepingAddress = "TAkyQrhTAu8gm9Z4n9QmiwXXKec2Av1VTz",
                AgreementType = RpcAgreementTypeEnum.Trc20

                //SweepingAddress = "0x2635c7F735EE7Ede6D0B3baE4Be93EBA7EAFd76c",
                //AgreementType = RpcAgreementTypeEnum.Erc20

                //SweepingAddress = "0x53a037C8e9646459b6179A67155d396bCD50c07D",
                //AgreementType = RpcAgreementTypeEnum.Bep20
            });

            Console.WriteLine(result);
        }

        [TestMethod]
        public async Task BatchTokenRecycle()
        {
            var result = await _coinPayProxyerClient.BatchTokenRecycleAsync(new BatchTokenRecycleRequest
            {
                RecycleAddress = "TAkyQrhTAu8gm9Z4n9QmiwXXKec2Av1VTz",
                AgreementType = RpcAgreementTypeEnum.Trc20

                //RecycleAddress = "0x2635c7F735EE7Ede6D0B3baE4Be93EBA7EAFd76c",
                //AgreementType = RpcAgreementTypeEnum.Erc20

                //RecycleAddress = "0x53a037C8e9646459b6179A67155d396bCD50c07D",
                //AgreementType = RpcAgreementTypeEnum.Bep20
            });

            Console.WriteLine(result);
        }

        [TestMethod]
        public async Task GetMemberWalletList()
        {
            var result = await _coinPayProxyerClient.GetMemberWalletListAsync(new GetMemberWalletListRequest
            {
                MemberId = 0,
                MemberIds = { },
                AgreementType = RpcAgreementTypeEnum.Default,
                Page = 1,
                Limit = 10,
                OrderBy = 1,
                Desc = true,
                Address = ""
            });

            Console.WriteLine(result);
        }
    }
}
