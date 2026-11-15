# LoopSort 产品接入与待替换配置

2026-11-02 更新：已读取 LoopSort 工作表 QvT1Xg 并接入 Android 包名、发行 ID 映射、MAX/AF、三类广告单元、AdMob 应用 ID、Ad Review 构建输入和法律链接；此前“正式参数尚未分配”是历史状态。真实平台登记/API/CDN、认证/CMP 宿主、签名和设备/后台验收仍未完成，不能宣称上线就绪。当前配置与交付说明见 [ReleaseReadiness/README.md](ReleaseReadiness/README.md)，本机私有参数不会提交到仓库。

更新时间：2026-10-29。用户已确认这是独立的 LoopSort 产品；正式参数尚未分配。Wool Rush 的包名、密钥、广告位及法律地址均未复用。

## 已准备的客户端

- Unity 保持 2022.3.62f2，平台 SDK API 7 三包版本保持原固定基线。
- 平台模块打包器显式收集 ui/menu_theme.lua 并改写 require 命名空间；不收集依赖 CS.LoopSort0 的旧 UI 工厂。
- 批处理增加 -rebuildPlatformAssets，重新准备字体、图集、模型与轨道后构建不可变模块。
- 产品壳安装 MAX Unity 8.6.6（原生 Android SDK 13.6.4）、AppsFlyer 6.18.1、其附带的 EDM4U 1.2.186；版本、来源、原归档 SHA-256 在 ProductSdkPackages.json。
- ProductAdvertising 提供 SDK HostAdBroker 驱动：Placement 映射、激励/插屏互斥、主线程回调、插屏冷却、仅收到奖励且广告已关闭才完成、加载重试、单次回调、账号切换失效及回收。测试广告状态传回 Lua，服务端校验后才能发正式奖励。
- AppsFlyer 只在真实同意状态允许且 MAX 初始化完成后启动；采用认证 userId，收入仅从 MAX 回调使用原始 USD 数值转发。账号切换/销毁停止归因。
- ProductConfigurationChecks 在非 Development 的产品 APK 构建前拦截临时身份、缺参、包名不符。显式离线壳仍沿用自己的 localOnly 校验。

## 临时配置在哪里

已安装的本机配置：ProjectSettings/LoopSortProduct.local.json（Git 忽略）。可提交的空模板：Configuration/Products/loopsort0-development.template.json。

临时值：gameId=loopsort0，productId=loopsort0-development，androidPackageName=com.local.loopsort0.development，clientVersion=0.1.0；API、CDN、法律链接使用保留的 .invalid 域。developmentOnly=true、registrationConfirmed=false；MAX、AF 和广告收入默认关闭。这些值不代表平台已登记；也不会被离线壳用于外网连接。

## 正式接入时集中替换

| 位置 / 字段 | 由谁提供 | 要改什么 |
| --- | --- | --- |
| 本机配置 gameId | 平台 | 确认永久游戏 ID；如不是 loopsort0，同时修改 PlatformModuleBuilder.LocalGameId 并重建模块/关卡数据根，旧模块不改字节 |
| productId、hostMode | 平台/发行 | 独立包和每个综合包各用自己的产品配置；共享模块不写 productId |
| androidPackageName、clientVersion | 发行/客户端 | 与 Unity PlayerSettings、MAX、AF 和商店包一致 |
| apiBaseUrl、cdnBaseUrl | 平台/服务器 | 测试环境先用真实 HTTPS 地址；正式环境单独配置 |
| maxSdkKey、placements[].adUnitId | 广告负责人 | 为 LoopSort 独立创建 MAX 应用和 daily_gift 激励广告位；其他格式有实际需求再添加映射 |
| appsFlyerDevKey、AF 应用和后台 MAX 收入连接 | 发行/数据 | 使用 LoopSort 应用，不能复用其他游戏；确认是否启用广告收入及业务事件表 |
| privacyPolicyUrl、termsUrl、accountDeletionUrl | 发行 | LoopSort 真实法律页面，另填写游戏内 peripheral-services.json 的入口地址 |
| module-services.json 的 leaderboard/teams/cloud_save/cloud_load/ad_reward | 服务端 | 实际认证游戏 API 相对路径；不是任意猜测的端点 |
| CMP/年龄策略及 ProductConsent | 发行/隐私/宿主 | 使用真实 CMP 结果；模板不伪造玩家同意、年龄或可测量状态 |
| 签名、可信目录、公钥、后台、服务器规则/结算 | 平台/发行/服务器 | 在宿主/CI 和测试平台配置；服务端令牌、签名私钥不写入模块或此仓库 |
| developmentOnly、registrationConfirmed 和功能开关 | 平台确认后 | 真实登记/配置完成后才切换；不能只改标记就宣称发布完成 |

## 产品壳怎样使用已准备代码

本项目的现有 PlatformLocalBootstrap 是明确的离线壳，继续打开固定哈希的本地模块。它不会因模板存在而启用外网或广告。

正式宿主在完成认证和真实 CMP 后，读取产品配置，通过 ProductHostComposition.Initialize(modulePlayerBehaviour, configuration, consent, authenticatedSession) 装配 CommonModuleServiceFactory。每个产品使用自己的配置。随后按官方 SDK 的服务器选版/可信目录流程打开模块，不能将离线 OpenInstalled 直接当作正式发布。

正式配置是产品壳的构建输入，不是把本机 JSON 拷进可下载模块。账号会话、可信目录和真实 CMP 必须由正式宿主提供；本轮只提供可编译的装配入口，没有伪造这些服务。

MAX 和 AF 的配置只在产品层，第三方 SDK 不在可下载游戏 Bundle 内。当前没有真实 AdMob/Meta 等网络方案，因此没有随意安装它们的 Adapter；如发行给出网络清单，按固定版本解析并构建验证。

## 本轮模块交付

版本：local_20261029_122117_035_5a71831d。
manifest SHA-256：e73fa0b8c6cd926af4568460b3299c52f59036a667d01fb55e2b2a7198d6131b。
目录：Builds/PlatformDelivery/local_20261029_122117_035_5a71831d/。
39 个清单内文件已逐项验证大小和 SHA-256；module.zip 根部直接包含 manifest.json。review-source.zip 保留官方构建时的审核快照，module-build-receipt.json、artifact-integrity.json 和 SHA256SUMS.txt 如实记录范围。

这份模块包含最后一次 UI 调整。它是未注册的本地交付，不是已上线版本；本轮没有生成 APK，也没有运行游戏或测试。

## 尚不能用临时值完成的验收

真实广告加载/播放、奖励和 AF 后台对账；认证/云存档/结算；独立包及目标综合包同哈希设备证据；发行签名、平台审核、测试 CDN 发布、回退及正式晋级。所有状态保持 pending，拿到正式参数后才执行，不写伪造的门禁通过报告。

## 参考

- SDK 交付：https://vcn5vzll02xu.feishu.cn/wiki/COmowpoWCiwcQUkvzkKcPJB8nPb
- 技术文档：https://vcn5vzll02xu.feishu.cn/wiki/FSdNwI7L4ibwqpkJ77Mc7ApsnSa
- IAA 参数/素材：https://youzu.feishu.cn/wiki/UNa5w9UPKiUxEikEbjPcIQSGnvg
- Wool Rush 参考（非本游戏参数）：https://youzu.feishu.cn/wiki/KA8ewKltSi6MbCkQfv9c1Sj8ncg?sheet=rdEksX
- MAX 官方安装：https://developers.axon.ai/en/max/unity/overview/integration/
- AF 官方安装：https://dev.appsflyer.com/hc/docs/installation


## 原生依赖准备与本轮验证边界

菜单 `LoopSort → 平台迁移 → 准备广告 Android 依赖（不运行游戏）` 会查找有效 JDK、临时给当前 Unity 进程设置 JAVA_HOME、创建 Android 插件目录，调用固定 EDM4U 的 ResolveSync，最后恢复环境变量并写入不可覆盖回执。

本轮在隔离 Unity 中实际执行成功，生成 mainTemplate.gradle、settingsTemplate.gradle、gradleTemplate.properties。它们已复制到本项目 Assets/Plugins/Android，并启用相应自定义模板开关。Android 设置为最低 API 24、ARM64、IL2CPP；此最低版本依据 [MAX 官方 Android 要求](https://support.applovin.com/en/max/android/overview/integration)。ARM64 模板不排除 arm64-v8a。

EDM 的成功表示 Gradle 依赖声明/模板解析成功；当前使用模板注入模式，不表示已经生成 APK、检查 DEX 或完成 Gradle 原生打包兼容性验证。正式原生 APK/设备/后台证据仍为 pending。项目新增的产品运行时与编辑器程序集已在 Unity 2022.3.62f2 编译。

SDK 文档还记录了审核工具对 shader/include 源码白名单的既有缺口；本游戏使用自定义 Shader，正式执行 PrepareModuleDelivery 前需要平台确认工具版本已修正。不能通过重命名源码绕过。
