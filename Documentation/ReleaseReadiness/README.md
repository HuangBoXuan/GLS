# LoopSort 上线准备（2026-11-02）

当前结论：**尚不能声明正式上线就绪**。已接入参数表中能在客户端落地的 Android 参数并校验；未伪造平台登记、服务器地址、玩家同意、签名或广告后台验收。实际检查结果以本目录 JSON、模块构建回执和测试报告为准。

本轮设置/暂停界面、四种语言、每次失败先看广告复活及激励跳关的行为与验证见 [SettingsAndRevive.md](SettingsAndRevive.md)。

插屏时机与频率已对齐 WoolRush 当前源码默认规则，下一关不再触发广告，当前说明见 [StandaloneAdvertising.md](StandaloneAdvertising.md)，检查结果见 [ad-cadence-validation.json](ad-cadence-validation.json)。

广告可以先做独立 Android 验证，不必等待正式平台登录、API/CDN 和登记。当前 Gameplay 已改为自动初始化和预加载，新增暂停横幅与道具激励入口，见 [StandaloneAdvertising.md](StandaloneAdvertising.md)。之前的专用测试面板已移除；[AdTesting.md](AdTesting.md) 仅保留历史记录。客户端功能与调试签名预览包不代替正式发布门槛，也不证明真机或后台验收已完成。

本次可复查的最新本地模块为 `Builds/PlatformDelivery/local_20261101_a480955f/`，含 `module.zip`、`review-source.zip`、真实构建回执和完整性校验。manifest SHA-256 为 `4bde56bc1ba7ed1e50a6c780915ab77e8e0c381793d0574e9ee2ca439d95e053`。这是本地审核材料，不是已登记、已发布的产品包。32 个 Lua 文件语法、签到/广告流程检查与 Android 模块构建通过；正式 APK 预检仍被真实生产缺项拦截，详见 `apk-preflight.json`。

## 已完成的本地接入

- UI 已重新导出至 `Exports/Restored-UI-Models/`：78 张独立图片、56 张实际界面截图；原有 326 张模型/网格/轨道图片保留。460 张 PNG 的路径、尺寸及散列已校验，浏览入口为 `index.html`。
- 从指定飞书工作表 QvT1Xg 读取 LoopSort 参数。正式 Android 包名为 `com.falcogames.loopsort`；发行 ID `1006000` 通过 SDK 的 `ProductContext.serverGameIds` 映射到现有 Lua 模块 `loopsort0`。
- MAX SDK Key、AppsFlyer Dev Key、激励/插屏/Banner 广告位进入产品配置。激励入口为 `daily_gift`；通关点击下一关尝试 `level_complete`，未加载成功时继续游戏；Banner 通过 SDK 原生协调器管理并按真实原生广告尺寸给主界面让位，弹窗和全屏广告期间隐藏。
- 新增 Google AdMob MAX Adapter 24.6.0.0 并解析原生依赖；AdMob 应用 ID 写入 Android Manifest。AdMob 的三个广告单元 ID 是 MAX 后台网络映射输入，不能当成 MAX 单元 ID 调用。
- 法律页面接入客户端设置与产品配置；三页 HTTP 请求均返回 200。这是可访问性结果，不是法律内容审核。
- Ad Review Key 只作为私有 Gradle 构建输入；Report Key、Event Key 只保存在项目外后台配置，未写入客户端资源。运行时客户端配置按类型白名单序列化，输入文件及含 SDK Key 的本机资产均被 Git 忽略。
- Android 设置为 API 36、最低 API 24、ARM64、IL2CPP；Android 36 平台与构建工具已安装。Unity 自带 ARM64 的 libunity.so、libmain.so 及 SDK 的 libxlua.so 的 LOAD 段为 16 KiB 对齐；按 Android 官方给旧版 AGP 的方案设置 native 库压缩与 App Bundle 压缩选项。最终安装包和真实 16 KiB 设备仍须验证。
- 修正模块打包白名单，补入 `game/signin.lua`、`game/visual_clearance.lua` 和 `ui/signin.lua`，避免只在编辑器可运行、模块包中缺文件。
- 已刷新独立包 C# 桥接清单及内置 Lua 清单。旧版 APK 不能当作新桥接的验证结果，正式包仍需重新构建。
- GM 启动按钮、GM 页面/跳关与原生道具注入均限制为编辑器或 Development 构建，正式版不能通过这些开发入口获取道具。

## 本机参数位置

`ProjectSettings/LoopSortProduct.local.json` 是本机产品输入；`ProjectSettings/LoopSortProductBuild.local.json` 是仅供构建的 Ad Review 输入。`ProductBuildInputs.ApplyLocal` 同步白名单客户端配置到被忽略的 Resources 资产，并配置 MAX、AdMob 应用 ID 和 Android 设置。产品宿主通过 `ProductHostComposition.Initialize(player, consent, authenticatedSession)` 读取该配置；仍必须传入真实的认证会话与 CMP 结果。

可提交模板为 `Configuration/Products/loopsort-android.release.template.json`，其中 SDK Key 为空；不要将这个模板视为完整生产配置。后台 Report/Event Key 和 AdMob 网络映射私有输入位于项目外 `D:/MiniProject/BuildTemp/LoopSort-release-inputs/LoopSort-backoffice.private.json`。凭据不在本报告中展示。

重导入：`python Tools/import_release_parameters.py --snapshot <私有 cells-get JSON> --private-dir <项目外私有目录>`。不要把完整参数表快照放进仓库或资源导出包。

## 正式发布还需完成

1. 平台提供并确认正式 `productId`、API/CDN、认证与可信目录配置、模块登记状态，以及广告奖励验证/云存档等真实 API 路由。当前只提供了广告参数，不能据此编造服务器地址或将 registrationConfirmed 改为 true。现有 Gameplay 场景和离线壳不是已完成认证/可信目录加载的正式产品宿主；需在正式宿主接入本次产品装配入口。
2. 将发行确定的真实 CMP/年龄策略接到 `ProductConsent`。当前不会伪造“已同意”；隐私选项的 CMP 协调器仍需正式宿主提供。真实同意结果未到位前不启动广告/归因。
3. 在 MAX/AdMob 后台确认三种广告单元的网络映射、SDK Bidding/瀑布流、广告奖励服务端回调、app-ads.txt；在 AppsFlyer 后台确认同包名应用、MAX 收入连接和事件对账。客户端填入 Key 不能替代后台开通和验收。
4. 提供正式上传签名证书/别名与安全密码输入，确认版本号；生成并检查用于 Google Play 的签名 AAB。当前没有使用调试签名冒充正式签名。广告测试实际发现 Google 24.6 的 WebKit 要求高于 Unity 默认 AGP 7.4.2，已用隔离 AGP 8.10.1 / Gradle 8.11.1 / JDK 17 成功打出测试 APK；正式 AAB 同样必须采用兼容工具链，不能继续假定 Unity 默认工具可直接打包。
5. 连接 Android 真机，完成广告播放结束/取消/奖励只发一次、账号切换、存档、16 KiB 兼容、离线/重连、关卡与性能回归。本次已在在线应用宝模拟器验证 MAX 初始化、三种格式加载和激励测试广告展示；广告结束页无法关闭且未收到奖励回调，尚未通过完整广告验收。真机暂不方便连接，按用户要求保留测试包和结果，详见 `ad-test-validation.json`。
6. 完成 Google Play 的应用资料、分级、目标受众、数据安全/广告声明、隐私与删除入口、测试轨道/审核及最终发布。未在发行账号执行上传或提交审核。

只有实际登记、配置与验收完成后才关闭 developmentOnly、开启 registrationConfirmed；发布检查还会拦截缺少签名、API 36 等情况。

## 清理与恢复

旧 UI-Elements、UI-Screens 和过期 undo 回执已从项目中移至 `D:/MiniProject/BuildTemp/LoopSort-ui-export-archive-20261102/`，保留可恢复备份。没有删除正在使用的 Unity Library/Temp、原始素材、SDK/字体许可和有效模型资源；额外扫描未发现自有目录中的 .bak/.tmp/.orig/.rej 临时文件或孤立 meta。

## 官方校验依据

- [Google Play 目标 API 要求](https://developer.android.com/google/play/requirements/target-sdk)：2026-08-31 起新应用和更新要求 API 36。
- [Android 16 KiB 页面说明](https://developer.android.com/guide/practices/page-sizes)：必须同时验证原生库对齐、安装包打包方式及设备行为。
- [MAX 网络准备](https://legacy-support.axon.ai/en/max/unity/preparing-mediated-networks/)：AdMob 需要适配器及对应应用 ID，后台网络开通不能由客户端占位代替。
- [AppsFlyer Unity 接入](https://dev.appsflyer.com/hc/docs/basicintegration)：使用真实 Dev Key 和实际同意流程；Android 不应编造 iOS App ID。
