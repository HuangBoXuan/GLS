# LoopSort 自动广告与奖励入口

当前 Gameplay 独立入口采用 WoolRush 的启动方式：Android 玩家进入游戏后自动请求 UMP 隐私状态，在原生 `canRequestAds` 允许后初始化 MAX，随后预加载激励、插屏和底部横幅。用户不再操作广告测试面板、读取设备 ID 或手动初始化 SDK。需要同意表单的地区仍显示真实表单。

## 游戏内行为

- 首页原广告入口改为“暂停横幅”。观看完整激励并收到奖励和关闭回调后，横幅暂停 900 秒。到期恢复；时间写入现有玩家进度，关闭应用不重置。暂停不影响道具激励或通关插屏。
- 撤回、打乱、传送带扩容、增加矿车库存为零时显示 AD 标记。点击打开对应道具说明与“免费 ×1”弹窗，完成广告后将该道具库存增加一。领取不自动消耗道具。
- 原有七日签到的奖励分配保留。每次广告请求只能结算一次，取消或展示失败不发奖；旧提供者或旧玩家进度的回调不会给当前玩家发奖。
- 横幅使用 MAX 原生视图，加载成功才显示。根据实际 SDK 布局预留屏幕底部空间，游戏 UI 和矿车棋盘避开横幅；弹窗、全屏广告、应用后台和暂停期隐藏横幅。
- 在本局最终结算后、结果按钮出现前检查一次插屏；“下一关”“重试”“开始”不再触发。未准备好或展示失败直接显示结算，不等待加载，也不在结果页轮询重试。具体频率见下方“当前策略”。

激励功能沿用本产品已分配的 MAX 激励单元作为共享库存，通过 `ad_break`、`booster_undo`、`booster_shuffle`、`booster_belt`、`booster_truck` 区分业务场景。没有复制 WoolRush 的包名、SDK Key 或广告位。独立本地模式没有平台认证会话，AppsFlyer 和收入对账仍由正式产品宿主单独验收。

## 构建和清理

使用 `Tools/build_android_player.ps1`，在已有隔离工程导出 Android Gradle，再以已校验的 AGP 8.10.1 / Gradle 8.11.1 / JDK 17 生成调试签名预览 APK。输出位于 `Builds/AndroidPlayer`；该包使用正常游戏界面和自动广告入口，没有专用测试启动器或测试面板。正式发布签名和生产配置检查继续保留。

`AdIntegrationTestBootstrap`、`AdIntegrationTestSettings`、原专用测试构建器和 Resources 测试标记已从当前项目及隔离构建工程移除；必要构建工具改为正常玩家命名。退休源文件保存在项目外 `D:/MiniProject/BuildTemp/LoopSort-retired-ad-diagnostics-20261102`。此前用户要求保留的旧测试 APK 和历史测试结果保留，不能用来代替新版本验收。

## 验证边界

初次接入以 `standalone-ad-validation.json` 为准，设置与复活版本见 `settings-revive-validation.json`，当前广告策略版本见 `ad-cadence-validation.json`。编辑器中的可控回调只验证奖励逻辑、取消、重复回调和 UI，不证明原生广告播放完成。此前应用宝模拟器存在测试广告结束页无法关闭的问题；在新版本取得原生关闭和奖励证据前，不标记该问题已解决，也不宣称正式上线就绪。

自动广告接入时的预览 APK：`Builds/AndroidPlayer/LoopSort-20261102-130657.apk`，SHA-256：`5f2777f9bc7510c3c779fda6f1c00c5cdc350b4c84ba5b053642e3af68d956c1`。750×1650 界面、取消与重复回调、旧进度回调隔离、暂停保存/到期及横幅预留区域回归通过；32 个 Lua 文件语法和既有签到/广告回归通过。APK 签名、真实原生 SDK 类定义、功能保留及专用测试代码移除检查通过；后台管理密钥匹配数为 0。后续设置、多语言、失败复活及激励跳关版本见 [SettingsAndRevive.md](SettingsAndRevive.md)。真机原生播放/关闭/发奖未验收。

清理后，该历史预览包已移至 `D:/MiniProject/BuildTemp/LoopSort-cleanup-20261102-ads-review/Builds/AndroidPlayer/`。最新统一样式 APK 和用户要求保留的专用广告测试包仍在项目 Builds 中，清理明细见 `cleanup.json`。

## 当前策略：对齐 WoolRush（2026-11-02）

独立 APK 与平台 Lua 入口共用 `game/ad_cadence.lua`。主关卡 1—5 不显示插屏；首次需要累计至少 5 次胜利、180 秒有效游玩。后续同时要求距上次插屏关闭至少 120 秒、累计至少 120 秒有效游玩。第 11 关前默认每 2 次有效结算检查，第 11 关起每次检查；两种频率都受时间门槛限制。广告关闭后才重置已消费的结算次数和游玩时间；无广告或展示失败保留额度，但本次结算不再重试。

暂停、弹窗、进场、后台、广告播放不计入有效游玩；冷却、胜利数、有效结算次数和失败记录随现有玩家进度保存。旧存档从已完成主关卡记录保守迁移胜利数，不虚构历史游玩时长。每日挑战、已完成/已跳过关卡的历史回看不生成插屏机会。应用在后台或窗口失焦时结算直接显示结果，不等待恢复前台后补弹广告。

失败仍先显示复活提示，提示和广告复活过程不插屏。关闭复活提示后才记录最终失败，并在第 8 关起、同关至少第二次失败、本次有效游玩至少 45 秒且此前已有插屏时参与检查。首次插屏不能来自失败。看完或关闭激励广告后至少 120 秒不插屏；未加载或展示失败不冒充关闭回调，也不发放奖励。15 分钟暂停横幅仍只影响 Banner。

本轮用 WoolRush 当前 `HotUpdate/games/garden/ad_cadence.lua` 做了 12,000 组对照，结果一致；29 项门槛/迁移边界检查及 22 项隔离 Unity 流程回归通过，33 个 Lua 文件语法和既有签到/广告回归通过。平台入口的直接下一关、取消激励后保护也有单独回归。可控提供器不进入构建工程。实际原生广告关闭和发奖的真机验收仍单独保留。

## 历史：此前策略差异（本轮已替换）

此前 LoopSort 由 `game.flow.next_level` 调用 `ContinueWithAdvertising`，普通通关点击下一关时尝试 `level_complete` 插屏；没有加载或仍在冷却时直接进入下一关。当时客户端配置 `interstitialCooldownSeconds` 为 60。`ProductAdvertising.Show` 在请求展示插屏时设置下一次允许时间，使用 `Time.realtimeSinceStartup`，并非按有效游玩时间累计，广告展示时间也计入间隔。首次没有累计胜利、保护关卡或首次游玩时长门槛，激励关闭也不更新插屏保护期，重启后该内存计时重新开始。

这与 WoolRush 当前工程 `games/TapGarden` 的完整广告策略不一致。其 Lua `HotUpdate/games/garden/app.lua` 在最终结算后、结果按钮可用前检查一次 `round_end`；`host_ads.lua` 的关卡进入方法直接继续，不在开始、下一关、重试时再请求插屏。`ad_cadence.lua` 同时检查前五关保护、累计胜利、有效游玩时间、结算次数、实际时间间隔及激励关闭后的保护。源码默认参数为首次累计 5 胜及 180 秒有效游玩，间隔 120 秒有效游玩并满足 120 秒时间间隔，第 11 关前每 2 次有效结算、从第 11 关起每次结算；激励关闭后至少 120 秒不插屏。有效失败还要求从第 8 关起、同关至少第二次失败、本次实际游玩至少 45 秒，且不能是首条插屏。

WoolRush 的 `Assets/Resources/MAX/max_ads_local.json` 及已导出的 `data/garden/product.json` 不在当前源码目录中，因此对齐数值来自源码默认值，不能据此断言某个已安装 WoolRush 包的实际配置。LoopSort 没有加入 `testInterstitialOnEveryNextLevel` 强制每关广告开关。

当前策略 APK：`Builds/AndroidPlayer/LoopSort-20261102-165318.apk`，SHA-256：`98b0505c525717e2b1fe952d8b483b8105f101655b90d98ed3a9e941e289fb8a`。签名、33 个内置 Lua 与源码一致性、120 秒原生冷却、后台/焦点保护代码、原生 SDK 类及 16 KiB 对齐核验通过；原生真机关闭和发奖仍待单独验收。中间包及上一版界面包已移至项目外 `D:/MiniProject/BuildTemp/LoopSort-ad-cadence-qa-20261102/superseded-packages`。
