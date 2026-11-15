# LoopSort Android 广告集成测试

> 本文记录上一版专用测试包，测试包按用户要求保留。当前版本已移除该测试面板与测试构建器，改为启动时自动接入；请使用 [StandaloneAdvertising.md](StandaloneAdvertising.md) 和 `Tools/build_android_player.ps1`。下文命令和操作描述仅供历史核查。

本流程验证测试广告，不依赖正式平台登录、API/CDN 或发布登记。正式产品的配置与签名检查保持有效。Unity 编辑器不提供真实 Android 广告播放证据。

## 生成测试包

在项目根目录运行 `powershell -ExecutionPolicy Bypass -File Tools/build_ad_test.ps1`。脚本把变更同步到 `D:/MiniProject/BuildTemp/LoopSort-3D-20261101`，使用独立 Unity 批处理进程准备、解析依赖并导出 Gradle 工程，不关闭当前编辑器。Google 广告 24.6 的 WebKit 依赖要求 AGP 至少 8.1.1，Unity 默认 AGP 7.4.2 的实际构建未通过。因此导出后使用隔离的 AGP 8.10.1 / Gradle 8.11.1 / JDK 17 打包；下载的 Gradle/JDK 按官方 SHA-256 校验，不更改 Unity 的全局外部工具设置。APK 位于 `Builds/AdIntegrationTest/LoopSort-AdTest-<时间>.apk`，使用 Development 与调试签名，不可当作商店发布包。相邻 JSON 保存实际构建结果与 SHA-256。

本机现有参数足够开始客户端集成测试。测试配置只写入隔离工程，不把正式产品标记改为已登记。MAX Ad Review 是可选能力，基础测试包不包含该构建插件；AppsFlyer 测量与收入上报在本测试入口关闭。

## 设备操作

当前已生成并通过签名与包内依赖检查的 APK：`Builds/AdIntegrationTest/LoopSort-AdTest-20261102-110233.apk`，SHA-256：`e84cc83aa9c2c9629cd0f14f0ad089fed3ce30395268188d736e75eb64328a2a`。它已安装到本机应用宝模拟器，真实初始化与测试广告显示结果记录在 `ad-test-validation.json`；关闭和发奖的验证状态以该回执为准。

2026-11-02 实测：MAX 原生初始化、真实测试模式、UMP 请求资格及激励/插屏/Banner 三种格式加载通过；从原有签到奖励流程成功展示 AppLovin 测试广告。单次宿主启动下，广告停留在结束页，原生关闭控件不可见，未收到关闭或奖励回调，因此关闭、返回游戏、发奖及只发一次尚未通过。该次未发现 Houdini 原生翻译故障，不能将问题归因于此前的重复实例；具体原因尚未确定。当前没有可用真机，按用户要求保留测试包和结果，待连接手机后对比。没有用本地模拟回调补发奖励。

1. 在有 Google Play 服务的安卓手机上安装 APK，或先在在线模拟器检查启动和诊断。安装前核对是否已有同包名正式游戏；不要卸载、覆盖或清除玩家数据来测试。
2. 打开游戏内的“广告集成测试”面板，点击“读取本机测试 ID”。只在当前设备保留 GAID，不写入公开日志。广告 ID 不可用时，可启动诊断，但游戏不会在 SDK 未确认测试模式时请求广告。
3. 点击“启动 MAX 与隐私流程”，完成实际显示的隐私表单。MAX 使用自动 Consent Flow，随后刷新 Google UMP 状态，只有原生 `canRequestAds` 允许且 SDK 返回测试模式时才加载游戏广告。没有表单不等于已经同意，也不会把 AF 同意字段全部设为 true。
4. 查看面板中的 `sdk=ready`、`testMode=True`、`privacyCanRequest=True` 和 `rewarded loaded`。有异常时进入 Mediation Debugger 检查网络 SDK/Adapter、CMP 和广告位。错误摘要只含阶段和数字错误码。
5. 点击“观看激励 · 今日签到奖励 ×2”，或返回游戏，通过首页原有“广告 → 观看广告”入口操作。观察奖励回调、关闭结果及道具库存；只有真实奖励回调且广告已关闭才完成当前游戏请求。提前关闭、展示失败、重复点击不得重复发奖。
6. 再测试插屏和 Banner、广告外跳返回、后台恢复、失败重试。插屏测试按钮只验证格式，不代表 LoopSort 的正式频控策略已完成产品验收。

测试进度和奖励只在内存保留，退出应用不会写入玩家的正常进度。测试面板只在带明确标记的 Development 包启动；Release 构建如果夹带该标记，会直接失败。

应用宝多窗口模拟器需要从应用宝打开游戏，让宿主分配正确显示窗口。不要在默认 ADB display 与应用宝中重复启动 Unity Activity；遇到 SDK 要求重启测试模式时，关闭该测试应用，再仅从应用宝重新打开。模拟器无 GAID 时，可以在 Mediation Debugger → AppLovin → Test Mode 中启用测试，重启后以 SDK 的实际 `testMode=True` 为准。

## 后台配合

- MAX/AdMob 负责人核对 `com.falcogames.loopsort` 对应应用、三种广告单元、Google 网络映射与竞价/瀑布流状态；不得把 AdMob App ID 当成 MAX Ad Unit ID。
- 在 AdMob Privacy & messaging 为本应用配置并发布适用的消息，检查广告合作伙伴范围；发布地区和年龄策略由发行确认。
- AF 的安装/会话、业务事件、MAX 收入桥接和后台对账另行验收；AF 失败不能阻断广告或游戏。测试广告不证明商业收入或 ROAS。

## 当前验证边界

代码和配置准备、APK 构建、原生包检查、设备初始化、加载、播放、奖励与后台验收应分别记录。没有真实设备或后台证据时，不能标记广告集成完成。本次实际执行结果见同目录的 `ad-test-validation.json`（产生后），不要把本文步骤当作已通过的报告。

依据：[用户提供的 MAX 接入资料](C:/Users/huangbx/Desktop/开发步骤/integrating-applovin-max-unity%20%281%29.zip)、[MAX 测试模式](https://support.applovin.com/en/max/unity/testing-networks/test-mode)、[MAX 隐私流程](https://support.applovin.com/en/max/unity/overview/terms-and-privacy-policy-flow)、[Google UMP](https://developers.google.com/admob/android/privacy)。
