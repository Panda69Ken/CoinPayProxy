# CoinPayProxy

**CoinPayProxy** 是一个基于 **.NET** 开发的虚拟币代理充值系统，主要面向数字资产充值、区块链地址管理及链上交易监听等场景。

目前已集成：

- **TRC20**
- **ERC20**
- **BEP20**

支持地址生成、USDT充值、原生币充值、区块扫描、交易确认、Token 归集等核心功能。

项目采用模块化设计，将不同区块链网络的 **地址管理、区块扫描、交易解析、充值确认、资产归集** 等功能进行抽象，方便后续扩展更多 EVM 链及其他区块链网络。

---


# ⚠️ 安全特别提醒

> ### 🚨 特别注意！特别注意！特别注意！
>
> **本项目为了方便开发、调试和测试，配置文件中的部分敏感信息默认没有进行加密处理。**
>
> 其中可能涉及：
>
> - 🔑 私钥（Private Key）
> - 📝 助记词（Mnemonic）
> - 🌐 RPC / API Key
> - 💰 钱包相关敏感配置
> - 🔐 其他链上操作凭证
>
> **请不要直接将当前测试配置用于生产环境！**
>
> 一旦私钥、助记词等核心凭证泄露，攻击者可能直接控制对应的钱包资产，**项目本身无法挽回由密钥泄露造成的链上资产损失。**
>
> ### 🛡️ 正式生产环境必须做好安全加固
>
> 建议至少做到：
>
> - 敏感配置加密存储
> - 私钥 / 助记词与普通业务配置隔离
> - 生产环境禁止提交真实密钥到 Git
> - 使用环境变量或专业 Secret 管理方案
> - 限制生产服务器及配置文件访问权限
> - 对核心钱包进行权限隔离
> - 做好密钥备份及轮换机制
> - 日志中严禁输出私钥、助记词等敏感信息
> - 对归集钱包、运营钱包等进行分层管理
>
> **千万不要因为测试环境能跑，就直接把配置复制到生产环境。**
>
> ### ⚡ 不注意安全，链上的资产可不会给你第二次机会。
>
> **不注意安全会摔得很疼的。😅**
>
> **请务必先做好安全加固，再用于生产环境。**

---

## ✨ 核心功能

### 🔗 多链支持

目前支持以下网络：

| 网络              | Token      | 原生币 | 地址      |
| --------------- | ---------- | --- | ------- |
| TRON            | USDT-TRC20 | TRX | `T...`  |
| Ethereum        | USDT-ERC20 | ETH | `0x...` |
| BNB Smart Chain | USDT-BEP20 | BNB | `0x...` |

项目后续可以基于现有架构继续扩展其他 EVM 网络。

---

### 💰 充值功能

支持用户生成独立充值地址，并通过区块扫描实时检测链上资产变化。

目前支持：

- USDT-TRC20 充值
- USDT-ERC20 充值
- USDT-BEP20 充值
- TRX 原生币充值
- ETH 原生币充值
- BNB 原生币充值
- 地址余额查询
- 链上交易记录解析
- 充值交易状态确认

---

### 🔍 区块扫描

通过持续扫描区块的方式监听链上交易。

主要流程：

```text
最新区块
   ↓
区块扫描
   ↓
解析交易
   ↓
匹配充值地址
   ↓
写入充值记录
   ↓
交易状态确认
   ↓
更新用户余额
```

支持：

- 连续区块扫描
- USDT Transfer 事件解析
- 原生币交易解析
- 充值地址匹配
- 交易 Hash 记录
- 区块高度记录
- 交易状态确认
- 已确认 / 已固化交易处理

通过 Redis（这里使用Sqlite代替，自用需自行替换） 自增机制维护扫描进度，保证服务重启或异常情况下能够继续从上次区块位置进行扫描，降低漏扫区块的风险。

---

### ✅ 交易确认

不同区块链采用对应的交易最终性判断机制。

#### TRON

通过 Solidity 节点查询交易固化状态。

只有满足：

```text
交易存在
+
receipt.result == SUCCESS
+
交易已经 Solidified
```

才将交易视为最终成功的充值交易。

#### Ethereum / EVM

对于 Ethereum 等 EVM 网络，通过交易 Receipt 判断执行结果：

```text
Receipt != null
+
Receipt.Status == 1
```

确认交易执行成功。

同时结合区块 Finalized 状态判断交易是否已经达到最终确认状态。

这种方式避免仅依赖 `latest` 区块造成充值交易在链发生重组时出现错误确认。

---

### 🏦 Token 归集

支持将用户充值地址中的 Token 归集到主钱包。

例如：

```text
用户充值地址
      │
      │ USDT
      ▼
┌──────────────┐
│  充值地址    │
└──────────────┘
      │
      │ Token Sweeping
      ▼
┌──────────────┐
│   主钱包     │
└──────────────┘
```

主要功能包括：

- USDT 余额检测
- 原生币 Gas 检测
- Token 转账
- 归集交易广播
- 交易 Hash 记录
- 归集状态跟踪
- 失败交易处理

---

### ♻️ Token 回收

支持对指定地址进行 Token 回收。

主要用于：

- 用户地址资产回收
- 地址余额整理
- 归集失败重试
- Token 统一转入主钱包

---

## 🔐 地址生成

支持基于 HD Wallet 的方式生成区块链地址。

通过助记词 / 私钥派生地址，实现：

```text
Mnemonic
   │
   ├── Private Key
   │       │
   │       ├── TRON Address
   │       │
   │       └── EVM Address
   │
   └── HD Wallet
```

TRON 与 Ethereum / BSC 等 EVM 网络虽然地址表现形式不同，但底层均可以基于相应的算法及 HD Wallet 派生机制生成地址。

---

## 多链统一抽象

对于 Ethereum、BSC 等 EVM 网络，尽可能复用统一的区块扫描、交易解析及充值处理流程。

```text
                 ┌──────────────┐
                 │ EVM Blockchain│
                 └───────┬──────┘
                         │
              ┌──────────┴──────────┐
              │                     │
          Ethereum                 BSC
              │                     │
              └──────────┬──────────┘
                         │
                  EVM统一处理层
                         │
              ┌──────────┼──────────┐
              │          │          │
           扫区块      解析交易    状态确认
```

只需要根据不同网络配置：

- RPC Endpoint
- Chain ID
- Token Contract
- Block Confirmation / Finality 规则

即可复用大量 EVM 相关代码。

---

## 🚀 项目定位

CoinPayProxy 并不是单纯的区块链 RPC 调用封装，而是一个围绕 **「充值地址 → 链上监听 → 交易确认 → 用户入账 → Token 归集」** 建立起来的完整链上充值处理系统。

核心业务链路：

```text
生成充值地址
      ↓
用户充值
      ↓
区块扫描
      ↓
发现链上交易
      ↓
解析 Token / Native Transfer
      ↓
确认交易执行成功
      ↓
确认交易 Finalized / Solidified
      ↓
充值入账
      ↓
余额达到归集条件
      ↓
Token Sweeping
      ↓
主钱包
```
---

 ### 注意事项

dll文件是[TronNet - Panda69Ken](https://github.com/Panda69Ken/TronNet)的发布版DLL，可自行下载补发替换

[MediatR](https://github.com/LuckyPennySoftware/MediatR)使用了有限制版本，可使用低版本代替

项目使用 `Sqlite` 代替数据存储，自行根据情况替换数据中间件

---

## ⚙️ 技术栈

项目基于 **.NET / C#** 开发。

主要使用的开源库：

### FreeSql

轻量、功能丰富的 .NET ORM。
[FreeSql](https://github.com/dotnetcore/FreeSql?utm_source=chatgpt.com)

### NLog

.NET 日志组件。
[NLog](https://github.com/NLog/NLog?utm_source=chatgpt.com)

### Quartz.NET

.NET 定时任务调度框架。
[Quartz.NET](https://github.com/quartznet/quartznet?utm_source=chatgpt.com)

### Newtonsoft.Json

.NET JSON 序列化与反序列化组件。
[Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json?utm_source=chatgpt.com)

### MediatR

.NET 中常用的 Mediator 实现，用于降低模块之间的耦合。
[MediatR](https://github.com/LuckyPennySoftware/MediatR?utm_source=chatgpt.com)

### Nethereum

Ethereum / EVM 生态的 .NET 开发库。
[Nethereum](https://github.com/Nethereum/Nethereum?utm_source=chatgpt.com)

### TronNet

由于原始 TronNet 项目长期缺少维护，因此项目使用了基于原项目进行维护和扩展的 Fork 版本。
[TronNet - Panda69Ken Fork](https://github.com/Panda69Ken/TronNet?utm_source=chatgpt.com)

---

## AD -- Telegram机器人推广
能量租赁交易监控机器人：[USDT、TRX交易监控](https://t.me/TronListen_bot)
> 监控波场地址余额变化，小额能量租赁！！！
主要功能：

- TRX 交易监控
- USDT 交易监控
- 地址余额变化监控
- 能量租赁交易监控
- TRON 资源变化监控