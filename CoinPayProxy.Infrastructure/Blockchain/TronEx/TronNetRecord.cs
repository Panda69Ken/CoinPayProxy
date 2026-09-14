using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using TronNet;
using TronNet.Accounts;
using TronNet.Contracts;
using TronNet.Protocol;
using Transaction = TronNet.Protocol.Transaction;

namespace CoinPayProxy.Infrastructure.Blockchain
{
    public class TronNetRecord(ILogger<TronNetRecord> logger,
        ITronClient tronClient,
        ITronAccountWallet tronAccountWallet,
        IContractClientFactory contractClient,
        IOptions<TronConfig> tronConfig,
        IOptions<MnemonicConfig> mnemonic)
    {
        private readonly ILogger<TronNetRecord> _logger = logger;
        private readonly ITronClient _tronClient = tronClient;
        private readonly ITronAccountWallet _tronAccountWallet = tronAccountWallet;
        private readonly IContractClientFactory _contractClient = contractClient;
        private readonly TronConfig _tronConfig = tronConfig.Value;
        private readonly MnemonicConfig _mnemonic = mnemonic.Value;

        public string GetAddress(string privateKey)
        {
            var tronKey = new TronECKey(privateKey, TronNetwork.MainNet);

            return tronKey.GetPublicAddress();
        }

        public ITronAccount GetAccount(string privateKey)
        {
            return _tronClient.GetWallet().GetAccount(privateKey);
        }

        public ITronAccount GenerateAccount(int index)
        {
            var mnemonicObj = new Mnemonic(_mnemonic.Mnemonic, Wordlist.English);

            byte[] seed = mnemonicObj.DeriveSeed();

            ExtKey masterKey = ExtKey.CreateFromSeed(seed);

            // 派生路径 m/44'/195'/0'/0/0
            var path = new KeyPath($"44'/195'/0'/0/{index}");

            ExtKey childKey = masterKey.Derive(path);

            var privateKey = childKey.PrivateKey.ToBytes();
            var privateKeyHex = BitConverter.ToString(privateKey).Replace("-", "").ToLower();

            var iAccount = _tronClient.GetWallet().GetAccount(privateKeyHex);

            return iAccount;
        }

        public ITronAccount GenerateAccount()
        {
            return _tronClient.GetWallet().GenerateAccount();
        }

        public async Task<long> GetNowBlock(CancellationToken stoppingToken = default)
        {
            var wallet = _tronClient.GetWallet();

            var nowBlockExt = await wallet.GetProtocol().GetNowBlock2Async(new EmptyMessage(), headers: wallet.GetHeaders(), cancellationToken: stoppingToken);

            if (nowBlockExt == null || nowBlockExt.BlockHeader == null)
            {
                return 0;
            }

            return nowBlockExt.BlockHeader.RawData.Number;
        }

        public async Task<RepeatedField<TransactionExtention>> GetUsdtTransactions(long blockNumber, CancellationToken stoppingToken = default)
        {
            var wallet = _tronClient.GetWallet();

            var transactions = await wallet.GetProtocol().GetBlockByNum2Async(new NumberMessage { Num = blockNumber }, headers: wallet.GetHeaders(), cancellationToken: stoppingToken);

            return transactions.Transactions;
        }

        public async Task<TransactionInfo> GetTransactionInfo(string hashId, CancellationToken stoppingToken = default)
        {
            var wallet = _tronClient.GetWallet();

            var transactionInfo = await wallet.GetSolidityProtocol().GetTransactionInfoByIdAsync(new BytesMessage
            {
                Value = wallet.ParseAddress(hashId)
            }, headers: wallet.GetHeaders(), cancellationToken: stoppingToken);

            return transactionInfo;
        }

        public async Task<decimal> BalanceOfUSDTAsync(ITronAccount account)
        {
            return await _contractClient.CreateClient(ContractProtocol.TRC20).BalanceOfAsync(_tronConfig.UsdtContract, account);
        }

        public async Task<decimal> BalanceOfUSDTAsync(string address)
        {
            return await _contractClient.CreateClient(ContractProtocol.TRC20).BalanceOfAsync(_tronConfig.UsdtContract, address);
        }

        public async Task<decimal> BalanceOfTokenAsync(ITronAccount account)
        {
            return await _tronAccountWallet.BalanceOfAsync(account);
        }

        public async Task<decimal> BalanceOfTokenAsync(string address)
        {
            return await _tronAccountWallet.BalanceOfAsync(address);
        }

        public async Task<Transaction> TransferOfUSDTAsync(ITronAccount account, string toAddress, decimal amount, decimal feeAmount)
        {
            var contractClient = _contractClient.CreateClient(ContractProtocol.TRC20);

            var (result, transaction) = await contractClient.TransferAsync(_tronConfig.UsdtContract, account, toAddress, amount, string.Empty, TronUnit.TRXToSun(feeAmount));

            if (result == null || result.Code != Return.Types.response_code.Success)
            {
                _logger.LogWarning($"USDT转账交易失败,code:{result.Code},msg{result.Message.ToStringUtf8()}");
                return null;
            }

            return transaction;
        }

        public async Task<(Transaction, long)> TransferOfTokenAsync(string privateKey, string toAddress, decimal amount)
        {
            var ownerAccount = _tronClient.GetWallet().GetAccount(privateKey);

            var (blockNumber, transaction) = await _tronAccountWallet.TransferAsync(ownerAccount, toAddress, amount);

            return (transaction, blockNumber);
        }

    }
}
