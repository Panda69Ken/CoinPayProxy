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
                TokenAddress = "TDsaxktUT2rSWXfEGz6h5dHSkuin2hni7k",
                AgreementType = RpcAgreementTypeEnum.Trc20

                //TokenAddress = "0xfb26a58c724eb5791d7070e23309ff62146fc11d",
                //AgreementType = RpcAgreementTypeEnum.Erc20

                //TokenAddress = "0xfb26a58c724eb5791d7070e23309ff62146fc11d",
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
                RecycleAddress = "TDsaxktUT2rSWXfEGz6h5dHSkuin2hni7k",
                AgreementType = RpcAgreementTypeEnum.Trc20

                //RecycleAddress = "0xfb26a58c724eb5791d7070e23309ff62146fc11d",
                //AgreementType = RpcAgreementTypeEnum.Erc20

                //RecycleAddress = "0xfb26a58c724eb5791d7070e23309ff62146fc11d",
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
                SweepingAddress = "TDsaxktUT2rSWXfEGz6h5dHSkuin2hni7k",
                AgreementType = RpcAgreementTypeEnum.Trc20

                //SweepingAddress = "0xfb26a58c724eb5791d7070e23309ff62146fc11d",
                //AgreementType = RpcAgreementTypeEnum.Erc20

                //SweepingAddress = "0xfb26a58c724eb5791d7070e23309ff62146fc11d",
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

                SweepingAddress = "TDsaxktUT2rSWXfEGz6h5dHSkuin2hni7k",
                AgreementType = RpcAgreementTypeEnum.Trc20

                //SweepingAddress = "0xfb26a58c724eb5791d7070e23309ff62146fc11d",
                //AgreementType = RpcAgreementTypeEnum.Erc20

                //SweepingAddress = "0xfb26a58c724eb5791d7070e23309ff62146fc11d",
                //AgreementType = RpcAgreementTypeEnum.Bep20
            });

            Console.WriteLine(result);
        }

        [TestMethod]
        public async Task BatchTokenRecycle()
        {
            var result = await _coinPayProxyerClient.BatchTokenRecycleAsync(new BatchTokenRecycleRequest
            {
                RecycleAddress = "TDsaxktUT2rSWXfEGz6h5dHSkuin2hni7k",
                AgreementType = RpcAgreementTypeEnum.Trc20

                //RecycleAddress = "0xfb26a58c724eb5791d7070e23309ff62146fc11d",
                //AgreementType = RpcAgreementTypeEnum.Erc20

                //RecycleAddress = "0xfb26a58c724eb5791d7070e23309ff62146fc11d",
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
