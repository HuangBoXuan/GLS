# 主页、奖励与客户端服务

这部分恢复了原包有数据依据的本地规则。原游戏服务没有接入；排行榜、云存档、广告和支付不会用本地假结果冒充联网成功。保留当前项目的 500000 初始金币、全部主线关卡可选、中文黑色大字、自定义美术和 Lua 热更新。

## 配置依据

`Assets/StreamingAssets/LoopSort/lua/game/peripheral_config.lua` 来自 APK 4.6.0 的 Area、Booster、Constants、DailyChallengeRewards、DailyRewards 表；`game/activity_config.lua` 来自 WeeklyMissions、WeeklyMissionProgress、BattlePass、LiveOps 表。原始导出在 `D:/MiniProject/analysis/LoopSort-4.6.0-20261026/exports/tables/`。

- 生命上限 5，每 30 分钟恢复 1，金币补满价格 900。失败扣除一次，限时无限生命不扣除。后台/重启后根据保存的时间补算恢复。
- 普通/困难/超难通关分别奖励 10/30/50 金币。每日挑战使用 `daily-challenge.json` 独立关卡，不复用主线随机编号；同一日期重复通关不重复领取奖励。
- 月历按原 DailyChallenge.GetMonthOrder 的 SplitMix64 + Fisher–Yates 排序，种子为 `year * 12 + month`。月内累计完成 3/7/14/25 天可分别领取原表里程碑。
- 四种道具原表均在第 1 关解锁、初始库存 2、每次购买 1 个；价格为撤回 300、打乱 300、扩容 800、加车 900。第一次使用有说明，随后正常扣库存，没有另外虚构免费试用次数。
- 19 个地区使用原解锁关号，9 个头像读取原包对应图片。地区列表完整，但目前仅有纽约、伦敦、巴黎三套主题背景；其他地区沿用现有主题背景。原 19 套城市 Spine 场景尚未恢复。
- 每日奖励使用原表权重抽取金币、道具或限时无限生命。每天只能领取一次；广告加倍必须由真实广告服务返回完成结果。
- 任务周期 5760 分钟（4 天），通行证周期 20160 分钟（14 天）。任务、目标值、阶段奖励和通行证 36 行奖励均按源表。通关获得 3/6/9 把通行证钥匙；终档仍是每周期一次领取，不作为无限奖励循环。

原生行为核对记录在 `D:/MiniProject/analysis/LoopSort0-parity-20261028/native/` 的 DailyChallenge、WeeklyMissions、BattlePass、BattlePassSubMonitor 对应 `.asm` 文件。活动当前采用本机首次进入时间启动的周期和设备时钟，没有模拟服务端排期。`GameLoseState__OnLevelLose.asm` 以及全可执行段 SpendLife 直接调用扫描支持仅在失败事件扣生命，未对主动退出/重试另外扣除。该原生函数还有传入时间戳距今满 30 分钟时跳过扣命的分支；传入时间的来源尚需核对，不能据此猜测退出扣命。

## 存档与运行入口

`PlayerProgress` 沿用 `LoopSort0.Player.v1` PlayerPrefs JSON。新增生命时间、每日记录、地区、头像和首次道具说明记录；活动进度及领取标记写入 `luaData`。旧存档通过 `peripheralVersion` 迁移，不清空主线进度或重发初始金币。

隐藏块揭示等高频任务计数只更新内存脏标记，外围每秒合并一次磁盘保存；暂停、应用进入后台、退出、释放控制器时补刷。金币扣除、领取奖励等显式操作立即保存。入场动画期间关闭道具交互和教学手势，完成入场后再启动引导。

主控制器入口包括 `OnPeripheralLevelLoaded`、`TickPeripheral`、`CompletePeripheralRun`、`RecordActivity`、`FlushPeripheralProgress`、`ReleasePeripheralAssets`。每天的挑战通过 `StartDaily("yyyy-MM-dd")` 加载，并与主线 `currentLevel` 分离。

## 排行榜与云存档接入

配置文件：`Assets/LoopSort/Resources/LoopSort/Data/peripheral-services.json`。

```json
{
  "baseUrl": "",
  "timeoutSeconds": 15,
  "premiumPassProductId": ""
}
```

`baseUrl` 必须为自己的 HTTPS API 根地址，可包含路径前缀，不带查询参数或片段。没有地址时 UI 明确显示尚未配置。也可由宿主在运行时调用 `ConfigurePeripheralService(httpsUrl, bearerToken)`。随后由正式登录 SDK 调用 `ConfigurePeripheralIdentity(stableAccountId, bearerToken)`；跨设备必须提供相同的稳定账户 ID。未接入登录时不生成“已登录”状态；自动生成的本地 profileId 仅标识本机存档。

令牌只在内存中，不写 PlayerPrefs、不导出云存档。切换服务、切换身份、销毁控制器都会中止旧 HTTP 请求和旧商业回调，防止旧账户返回数据应用到新账户。配置身份不是登录实现；生产服务仍需验证令牌及资源所有权。

所有响应使用 UTF-8 JSON。客户端提供以下接口，服务端需实际实现：

| 请求 | 用途与约定 |
| --- | --- |
| `GET /leaderboard?scope=world&limit=30` | scope 可为 world/country/weekly；返回最多 30 条真实排序结果，可公开读取 |
| `POST /leaderboard/scores` | 需身份；提交 profileId、playerName、avatar、level、completed，服务器验证并计算排名 |
| `GET /players/{escapedAccountId}/save` | 需身份；返回对应账号的存档信封 |
| `PUT /players/{escapedAccountId}/save` | 需身份；上传信封；首次用 `If-None-Match: *`，已获取版本后用 `If-Match: revision` |

排行榜响应示例结构（示例值不用于游戏内假排行）：

```json
{"entries":[{"rank":1,"profileId":"account-id","playerName":"玩家","avatar":"default","level":20}]}
```

云存档信封：

```json
{"schema":1,"revision":"opaque-server-revision","profileId":"account-id","progress":{}}
```

`progress` 必须是完整的 `PlayerProgress` JSON，空对象仅用于说明信封结构。服务器返回不透明 revision；客户端原样发送给 If-Match。成功 PUT 应返回更新后的信封（至少含 revision）。版本冲突返回 409 或 412，客户端提示先下载后决定。下载验证 schema 和账号后只暂存候选数据；玩家在主页确认恢复时，先备份本机 JSON 到 `LoopSort0.Player.beforeCloud` 再应用。不会静默覆盖本机进度。

当前是手动上传/下载客户端，不包含服务部署、账号登录 SDK、自动云冲突合并、可信设备时钟或防作弊校验；服务器必须自行做身份授权、成绩验证、支付凭据校验和备份。无服务器时所有本地功能仍可运行。

## 广告与支付宿主接口

`IPeripheralCommerceProvider` 是 C# 宿主注入接口，调用 `SetCommerceProvider(provider)` 接入真实 SDK；项目没有附带模拟广告/支付实现。

```csharp
bool RewardedReady { get; }
bool PurchasesReady { get; }
string PriceFor(string productId);
void ShowRewarded(string placement, Action<bool> completed);
void Purchase(string productId, Action<string> verifiedTransaction);
```

接口回调必须在 Unity 主线程调用。广告完成才返回 true；关闭、错误、取消均返回 false。购买回调只可返回经正式 SDK/服务端验证的交易 ID，取消和失败返回空字符串。宿主保证每次调用最终完成回调；客户端会防止重复回调及已失效身份的回调发奖。`PriceFor` 返回正式商店本地化价格，不能填假价格。

当前广告位是 `daily_reward`，完成后领取当日双倍奖励并推进广告加倍任务。支付商品来自 `premiumPassProductId`，验证成功后解锁当前通行证高级轨道；交易 ID 持久化去重。购买期间若活动周期已经变化，保存待处理交易凭据并明确提示，需正式支付服务退款或补发，不给错误周期直接发奖。

未注入 provider、未配置商品 ID 时按钮清楚显示未配置，免费奖励不受影响。原商业 SDK、商店商品、广告位 ID、正式登录和服务端部署仍是外部接入前提。
