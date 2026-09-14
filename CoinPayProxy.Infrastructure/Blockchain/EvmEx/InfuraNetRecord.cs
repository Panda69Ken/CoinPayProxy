using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Infrastructure.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.HdWallet;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.Numerics;
using static Nethereum.RPC.Eth.DTOs.BlockParameter;

namespace CoinPayProxy.Infrastructure.Blockchain
{
    public class InfuraNetRecord(ILogger<InfuraNetRecord> logger,
        IOptions<EvmConfig> options,
        IOptions<MnemonicConfig> mnemonic,
        AgreementTypeEnum agreementType)
    {
        private readonly ILogger<InfuraNetRecord> _logger = logger;
        private readonly IOptions<EvmConfig> _options = options;
        private readonly IOptions<MnemonicConfig> _mnemonic = mnemonic;

        private readonly Random _rand = new();
        private readonly long _chainId = options.Value.Chains[agreementType].ChainId;
        private readonly AgreementTypeEnum _agreementType = agreementType;

        string GetInfuraUrl()
        {
            string url = _options.Value.Chains[_agreementType].InfuraApi;

            if (_options.Value.InfuraApiKeys.Count == 1)
                return $"{url}/v3/{_options.Value.InfuraApiKeys[0]}";

            var num = _rand.Next(0, _options.Value.InfuraApiKeys.Count);

            var apiKey = _options.Value.InfuraApiKeys[num];

            return $"{url}/v3/{apiKey}";
        }

        public Account GetAccount(string privateKey) => new(privateKey, _chainId);

        public string GetAddress(string privateKey)
        {
            var account = GetAccount(privateKey);

            return account.Address;
        }

        public Web3 GetWeb3(string privateKey) => new(GetAccount(privateKey), GetInfuraUrl());

        public Web3 GetWeb3(Account account) => new(account, GetInfuraUrl());

        public Web3 GetWeb3() => new(GetInfuraUrl());

        public IClient GetClient() => new RpcClient(new Uri(GetInfuraUrl()));

        public Account GenerateAccount(int index)
        {
            var wallet = new Wallet(_mnemonic.Value.Mnemonic, _mnemonic.Value.Seed);
            return wallet.GetAccount(index, _chainId);
        }

        public Account GenerateAccount()
        {
            var ecKey = EthECKey.GenerateKey();
            var account = new Account(ecKey, _chainId);
            return account;
        }

        public async Task<long> GetNowBlock()
        {
            var web3 = GetWeb3();

            var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            return blockNumber.Value.ToInt64OrDefault();
        }

        public async Task<Block> GetBlockInfo(long blockNumber)
        {
            var web3 = GetWeb3();

            return await web3.Eth.Blocks.GetBlockWithTransactionsByNumber
                .SendRequestAsync(new HexBigInteger(blockNumber));
        }

        public async Task<List<EvmTransactionDTO>> GetUsdtTransactions(long blockNumber)
        {
            var web3 = GetWeb3();

            var transferEventHandler = web3.Eth.GetEvent<TransferEventDTO>();

            var filterInput = transferEventHandler.CreateFilterInput(new BlockParameter(new HexBigInteger(blockNumber)), new BlockParameter(new HexBigInteger(blockNumber)));

            var contract = _options.Value.Chains[_agreementType].UsdtContract;

            filterInput.Address = [contract];

            var logs = await transferEventHandler.GetAllChangesAsync(filterInput);

            int decimals = await GetTokenDecimalsAsync(contract);

            return [.. logs.Select(a => new EvmTransactionDTO {
                HashId = a.Log.TransactionHash,
                //LogIndex = a.Log.LogIndex.Value.ToInt64OrDefault(),
                //BlockHash = a.Log.BlockHash,
                BlockNumber = a.Log.BlockNumber.Value.ToInt64OrDefault(),
                FromAddress = a.Event.From,
                ToAddress = a.Event.To,
                Amount = Web3.Convert.FromWei(a.Event.Value, decimals)
            })];
        }

        public async Task<TransactionReceipt> GetTransactionInfo(string hashId)
        {
            var web3 = GetWeb3();

            var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hashId);

            //注意合约类型交易 receipt.To 是合约地址，To要看Transfer Event的Transfer.To
            //原生币交易正常赋值
            return receipt;
        }

        /// <summary>
        /// 判断交易所在区块是否已经 finalized
        /// </summary>
        /// <param name="blockNumber"></param>
        /// <returns></returns>
        public async Task<bool> FinalizedBlockNumber(BigInteger blockNumber)
        {
            var web3 = GetWeb3();

            var bp = new BlockParameter();

            bp.SetValue(BlockParameterType.finalized);

            var finalizedBlock = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(bp);

            if (finalizedBlock != null)
            {
                var finalizedBlockNumber = finalizedBlock.Number.Value;

                return blockNumber <= finalizedBlockNumber;
            }

            return false;
        }

        public async Task<decimal> BalanceOfUSDTAsync(Account account)
        {
            return await BalanceOfUSDTAsync(account.Address);
        }

        public async Task<decimal> BalanceOfUSDTAsync(string address)
        {
            try
            {
                var web3 = GetWeb3();

                var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();

                var balanceMessage = new BalanceOfFunction() { Owner = address };

                var contract = _options.Value.Chains[_agreementType].UsdtContract;

                BigInteger balance = await balanceHandler.QueryAsync<BigInteger>(contract, balanceMessage);

                int decimals = await GetTokenDecimalsAsync(contract);

                return Web3.Convert.FromWei(balance, decimals);
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取地址钱包USDT资产异常,Address:{address},error:{ex.Message}");
            }

            return 0;
        }

        public async Task<decimal> BalanceOfTokenAsync(Account account)
        {
            return await BalanceOfTokenAsync(account.Address);
        }

        public async Task<decimal> BalanceOfTokenAsync(string address)
        {
            try
            {
                var web3 = GetWeb3();

                var balanceWei = await web3.Eth.GetBalance.SendRequestAsync(address);

                // 将 Wei 转换为 ETH（十进制更易读）
                return Web3.Convert.FromWei(balanceWei);
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取地址EHT异常,Address:{address},error:{ex.Message}");
            }

            return 0;
        }

        public async Task<TransactionReceipt> TransferOfUSDTAsync(Account account, string toAddress, decimal amount, decimal tokenAmount)
        {
            var web3 = GetWeb3(account);

            var contractHandler = web3.Eth.GetContractHandler(_options.Value.Chains[_agreementType].UsdtContract);

            var amountRaw = Web3.Convert.ToWei(amount, 6); // USDT 通常有 6 位小数

            var transferFunction = new TransferFunction
            {
                To = toAddress,
                Value = amountRaw
            };

            //估算Gas，大概在0.000225–0.006之间
            var gasEstimate = await contractHandler.EstimateGasAsync(transferFunction);
            var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();

            //计算本次转账所需Gas
            var requiredWei = gasEstimate.Value * gasPrice;
            var requiredEth = Web3.Convert.FromWei(requiredWei);

            //加安全缓冲10%
            decimal requiredWithBuffer = requiredEth * 1.1M;

            if (tokenAmount < requiredWithBuffer)
            {
                _logger.LogWarning($"USDT转账交易失败,用户Token不足以支付手续费,余额:{tokenAmount}Token,预计消耗(含缓冲):{requiredWithBuffer}Token");
                return null;
            }

            transferFunction.Gas = gasEstimate.Value;
            transferFunction.GasPrice = gasPrice;

            var transactionReceipt = await contractHandler.SendRequestAndWaitForReceiptAsync(transferFunction);

            return transactionReceipt;
        }

        public async Task<TransactionReceipt> TransferOfTokenAsync(string privateKey, string toAddress, decimal amount)
        {
            var web3 = GetWeb3(privateKey);

            return await web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(toAddress, amount);
        }

        async Task<int> GetTokenDecimalsAsync(string contractAddress)
        {
            try
            {
                var web3 = GetWeb3();
                var decimalsHandler = web3.Eth.GetContractQueryHandler<DecimalsFunction>();
                var decimals = await decimalsHandler.QueryAsync<int>(contractAddress, new DecimalsFunction());
                return decimals;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"读取代币 decimals 失败, contract:{contractAddress}, use default 18. error:{ex.Message}");
                return 18;
            }
        }
    }
}
