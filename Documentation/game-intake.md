# LoopSort0 游戏接入信息表

更新：2026-10-29。本地 Android Lua 模块与离线独立 APK 均已构建成功；以下本地协议值不表示后台已经登记，未运行游戏、测试或设备验收。

| 项目 | 记录 |
| --- | --- |
| 展示名 / 工程 | Loop Sort / LoopSort0 |
| 本地模块命名空间 | `loopsort0`，未注册的本地草案；正式发布前由平台确认永久 gameId |
| 独立包 productId / 综合包 productId | 未提供；模块不包含 productId，两产品配置在各自宿主壳 |
| Unity / 渲染 | 按用户决定保持 2022.3.62f2；Built-In、UGUI。SDK 参考为 2022.3.76f1，实际 Bundle 记录 62f2，不伪报版本 |
| SDK | 三包 `0.2.0-localtest.9730783e9569c94e`、Host API 7、Lua 5.4.8、xLua ABI 105、Localization 1.4.4 |
| 原交付 ZIP | `D:/MiniProject/analysis/MiniGameSdk-api7-20261020/lua-sdk-api7-20261020.zip`；SHA-256 `6ab605cf5d59aeb97714b07d21155d26089b02a676b254e6b495478233380227` |
| 模块入口 | 构建时 `platform/main.lua` → `games/loopsort0/main.lua`，entry=`games.loopsort0.main`；共享纯 Lua 与平台表现层均在本游戏 namespace |
| 模块版本 | 本次成功版本 `local_20261029_035203_702_08ca56ff`；不可覆盖，正式模块版本与登记信息仍需平台确认 |
| 实际 manifest SHA-256 | `86854cc651da2e467efcbbb684eb8b1c9c01187c803c674f1ac3ba3ae82261bc`；真实回执 `status=built`，2026-10-29 03:53:01 UTC |
| 实际本地 APK | `LoopSort0-Platform-LocalOnly-arm64.apk`，109,638,318 字节；Unity 2022.3.62f2、ARM64、IL2CPP、OpenGLES3；回执 `buildSucceeded=true` |
| APK SHA-256 | `b4b9bb0baede3d3593f28ecd1c7e13aa4d65527de0a7b629afae550fe17bdd3b`；所用模块 manifest 哈希与上行一致 |
| APK 原始回执 | `D:/MiniProject/Builds/LoopSort0Platform/20261029_035353_645_a1bb336e/build-receipt.json`；同目录保留原始 APK |
| ruleVersion / saveSchemaVersion | 当前源码规则 `1`、存档 schema `2`；未声明服务器注册成功 |
| 关卡 | 主关 1299 个、每日模板 30 个；`data/loopsort0/levels/index.json` + 13片，完整关卡对象；level-map.json 显式区分 main/daily |
| 可下载内容 | Lua、声明的数据、Android `<gameId>.bundle`；不带游戏 C#、DLL、原生库或旧独立版桥接 |
| 原生资源 | 原模型、机制 Morph 网格/动画数据、奶油/紫/薄荷主题图集、书本图鉴图标、Noto Sans SC、原音频、项目 Shader；由 PlatformAssetBuilder 生成原生资产 |
| API 能力 | `lua`、`lua_late_update_v1`、`assetbundle`、`module_owned_resources_v1`、`unity_native_v1`、`ugui`、`mesh3d`、`data_text`；以最终 definition 为准 |
| 场景约定 | 模块独立 PhysicsScene 手动固定步进；世界层30由宿主预留；模块自己的相机/光源只覆盖该层，不修改宿主 Time/Physics/RenderSettings |
| AOT 与性能 | Unity 原生程序集由 SDK 保留；System.Array.CreateInstance/实例绘制与真实 Android 性能尚无设备证据；构建不能替代帧率验收 |
| 回收 | 自建对象由模块清理，借用 Bundle 资产由宿主卸载；TrackCallback解绑，退出等待HasPendingExit=false；不销毁平台对象 |
| 存档 | 使用宿主 Services.Storage 按userId+gameId隔离；金币/道具/教程/活动存档由Lua维护。Storage 不等于跨设备云存档 |
| 本地壳身份 | `local-only:default` 离线设备存档；空 `PlatformSession`，不伪造登录、正式产品注册或云账号；网络与广告服务明确不可用 |
| 本地壳内容密钥 | 包含官方 authoring 包，通过 `ModuleHostKeyBuild.BuildPlayer` 生成空 keyring、拒绝全部内容密钥请求；`sdk-generated-empty-keyring-deny-all`，不是生产密钥配置 |
| 本地壳字体 Shader | 显式 Always Included 仅 `UI/Default`、`Sprites/Default`；内部 `GUI/Text Shader` 为 HideAndDontSave，经 Resources 字体与未激活 UGUI.Text 引用保留，不写入 Always Included；运行呈现未验证 |
| 初始金币 | 新档500000，仅首次默认；不能覆盖现有合法存档，也不视为平台共有钱包 |
| 账号 / 云存档 / 排行 / 结算 | 已留可配置服务接口；缺少真实统一账号与服务器配置，失败/不可用不得返回假成功；在线成绩和奖励需服务端验证 |
| 广告 / 支付 | 产品壳配置实际driver、placement和产品；广告奖励需观看完成且后端确认后才发放。未配置服务明确不可用，不模拟观看成功、奖励结算或购买成功 |
| CDN / 签名 / 后台 | 未提供真实地址、产品信任目录、公钥和发布权限；配置模板留空，不包含密钥 |
| 本地构建材料 | 实际准备目录 `D:/MiniProject/Builds/LSM/Builds/PlatformModules/local_20261029_035203_702_08ca56ff/`；source、definition、preparation 快照以及官方 output/modules、output/review 均保留 |
| 已整理交付目录 | `D:/MiniProject/workspace/games/LoopSort/Builds/PlatformDelivery/local_20261029_035203_702_08ca56ff/`；完整 `module/`、两个 ZIP、模块与 APK 成功回执、本地 APK、`artifact-integrity.json`、`SHA256SUMS.txt` 和说明 |
| 正式交付材料 | 已有本地离线独立 APK；仍缺正式产品配置、综合包、设备日志、同manifest哈希双包门禁与权属说明。当前review为草案，不伪造正式delivery.json |
| 已执行范围 | 源码/静态资产、C#编译；官方 SDK 定义/依赖检查、Lua语法编译、Android AssetBundle、不可变模块打包与 ARM64 IL2CPP 独立 APK 构建成功；静态文件清单/哈希确认 |
| 最后静态复查 | 修复启动存档关卡覆盖、超时扩容误消耗、每日跨日结算、同整体碎块重复占格、容量显示、入车尺寸跳变、幕布材质累积及撤回后的障碍/标签显示；APK 菜单支持隔离构建的交付目录。27 个 Lua 文件仅语法编译，并交叉复核调用链 |
| 手机启动及入关修复 | 移除未导出的静态 `Object.op_Equality` 调用；`Native.alive` 先用静态 `System.Object.ReferenceEquals` 检查 xLua 已回收的托管目标，再用 `Equals(nil)` 判断 Unity 已销毁对象。本地壳保留该反射 API；销毁主页时清理文字引用和布局记录，修复入关后的空反射目标路径。新包尚未真机复验 |
| 未执行范围 | Play Mode、游戏运行、SDK测试套件、APK运行、设备/AOT/帧率验收、在线联调、正式发布 |
| Lua 性能优化 | 轨道搜索/投影/入车距离使用缓存的 Lua 数值；复用车口投影、材质状态、颜色转换和分批绘制缓冲，静止碎块按位置修订号复用矩阵；实例绘制插值前后物理位置/旋转，传送门与撤销重置样本；文字/布局/教程投影/打勾朝向仅在变化时写入。保留碰撞、速度和热更，不以调整目标帧率冒充性能提升；尚无真机优化前后帧率数据 |

素材包括用户提供 APK 中提取的数据/模型/音频、生成图集和字体。实际发行权属与许可由游戏团队提供；提取记录不是授权证明。

本地工具和模块的完成不等于飞书文档第6步的线上发布完成。后续平台/服务器/发行方需要登记身份与规则，配置服务与可信目录；按用户安排补真实双包证据，再通过 SDK `VerifyModuleRelease.ps1`、`PrepareModuleDelivery.ps1` 和后台审核发布。旧 APK 或其他工程证据不能补作本项目的新基线证明。

实现分层、构建入口和边界见 [PlatformIntegration.md](PlatformIntegration.md)。
