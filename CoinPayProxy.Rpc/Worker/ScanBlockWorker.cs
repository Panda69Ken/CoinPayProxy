using CoinPayProxy.Domain.DomainService;
using CoinPayProxy.Domain.QueryServices;
using CoinPayProxy.Infrastructure.Blockchain;
using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Rpc.Core.Block;
using CoinPayProxy.Rpc.Core.Service;

namespace CoinPayProxy.Rpc.Worker
{
    //扫区块记录转U记录
    public class ScanBlockWorker(ILogger<ScanBlockWorker> logger,
        IConfigService config,
        ICacheService cache,
        IServiceScopeFactory scopeFactory,
        TronNetRecord tron,
        Func<AgreementTypeEnum, InfuraNetRecord> infuraFactory,
        TronTransferBlock tronTransferBlock,
        EvmTransferBlock evmTransferBlock) : BackgroundService
    {
        private readonly ILogger<ScanBlockWorker> _logger = logger;
        private readonly IConfigService _config = config;
        private readonly ICacheService _cache = cache;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly TronNetRecord _tron = tron;
        private readonly Func<AgreementTypeEnum, InfuraNetRecord> _infuraFactory = infuraFactory;
        private readonly TronTransferBlock _tronTransferBlock = tronTransferBlock;
        private readonly EvmTransferBlock _evmTransferBlock = evmTransferBlock;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScanBlockWorker running at: {time}", DateTimeOffset.Now);

            var types = new List<AgreementTypeEnum>();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var queries = scope.ServiceProvider.GetRequiredService<BlockchainQueries>();
                var blockNumberDomain = scope.ServiceProvider.GetRequiredService<BlockNumberDomainService>();

                foreach (AgreementTypeEnum @enum in Enum.GetValues(typeof(AgreementTypeEnum)))
                {
                    if (@enum == AgreementTypeEnum.None) continue;

                    types.Add(@enum);

                    var key = _cache.GetBlockNumberKey(@enum);

                    var exist = await queries.BlockNumberKeyExist(key);
                    if (!exist)
                    {
                        long number = 1L;
                        switch (@enum)
                        {
                            case AgreementTypeEnum.TRC20:
                                number = await _tron.GetNowBlock(stoppingToken);
                                break;
                            case AgreementTypeEnum.ERC20:
                            case AgreementTypeEnum.BEP20:
                                number = await _infuraFactory(@enum).GetNowBlock();
                                break;
                        }

                        number--;   //redis自增每次请求读取都会自增加1，区块从上一个开始计算

                        await blockNumberDomain.StringIncrement(key, number);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ScanBlockWorker 初始化错误: {ex.Message}");
            }

            //开始处理每种协议类型和节点的任务
            var workerTasks = new List<Task>();

            foreach (var @enum in types)
            {
                foreach (var node in _config.NodeList)
                {
                    workerTasks.Add(Task.Run(async () =>
                    {
                        var interval = 1000;

                        using var scope = _scopeFactory.CreateScope();
                        var queries = scope.ServiceProvider.GetRequiredService<BlockchainQueries>();
                        var blockNumberDomain = scope.ServiceProvider.GetRequiredService<BlockNumberDomainService>();

                        var key = _cache.GetBlockNumberKey(@enum);

                        var lastNumber = await queries.GetNodeBlockNumber(key, node);

                        if (lastNumber == 0)
                        {
                            lastNumber = await blockNumberDomain.StringIncrement(key);
                            lastNumber = lastNumber + _config.NodeList.IndexOf(node) - 1;
                            await blockNumberDomain.SetNodeBlockNumber(key, node, lastNumber);
                        }

                        while (!stoppingToken.IsCancellationRequested)
                        {
                            try
                            {
                                var latestHeight = 0L;

                                switch (@enum)
                                {
                                    case AgreementTypeEnum.TRC20:
                                        latestHeight = await _tron.GetNowBlock(stoppingToken);
                                        break;
                                    case AgreementTypeEnum.ERC20:
                                    case AgreementTypeEnum.BEP20:
                                        latestHeight = await _infuraFactory(@enum).GetNowBlock();
                                        break;
                                }
                                if (latestHeight == 0)
                                {
                                    _logger.LogWarning($"{@enum},{node}--GetNowBlock 返回了0; 正在重试......");
                                    await Task.Delay(interval * 2, stoppingToken);
                                    continue;
                                }

                                if (lastNumber >= latestHeight)
                                {
                                    _logger.LogDebug($"{@enum},{node}--等待新区块. 当前的:{lastNumber} 最新的:{latestHeight}");
                                    await Task.Delay(interval * 2, stoppingToken);
                                    continue;
                                }

                                switch (@enum)
                                {
                                    case AgreementTypeEnum.TRC20:
                                        var transactions1 = await _tron.GetUsdtTransactions(lastNumber, stoppingToken);
                                        if (transactions1 != null && transactions1.Count > 0)
                                        {
                                            //_logger.LogInformation($"Task:{@enum},{node}--当前区块:{lastNumber},交易笔数:{transactions1.Count}");
                                            _tronTransferBlock.Post(new TronTransferModel
                                            {
                                                Node = node,
                                                Transactions = transactions1
                                            });
                                        }
                                        break;
                                    case AgreementTypeEnum.ERC20:
                                    case AgreementTypeEnum.BEP20:
                                        var transactions2 = await _infuraFactory(@enum).GetUsdtTransactions(lastNumber);
                                        if (transactions2 != null && transactions2.Count > 0)
                                        {
                                            //_logger.LogInformation($"Task:{@enum},{node}--当前区块:{lastNumber},交易笔数:{transactions2.Count}");
                                            _evmTransferBlock.Post(new EvmTransferModel
                                            {
                                                Node = node,
                                                AgreementType = @enum,
                                                Transactions = transactions2,
                                                BlockNumber = lastNumber
                                            });
                                        }
                                        break;
                                }

                                lastNumber = await blockNumberDomain.StringIncrement(key);
                                await blockNumberDomain.SetNodeBlockNumber(key, node, lastNumber);
                            }
                            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                            {
                                break;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"获取区块数据异常,{@enum},{node}--error:{ex.Message}");
                            }

                            try
                            {
                                await Task.Delay(interval * 2, stoppingToken);
                            }
                            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                            {
                                break;
                            }
                        }
                    }, stoppingToken));
                }
            }

            try
            {
                await Task.WhenAll(workerTasks);
            }
            catch (OperationCanceledException e) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning($"ScanBlockWorker关闭异常：{e.Message}");
            }
        }

    }
}
