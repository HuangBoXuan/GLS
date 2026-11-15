# LoopSort0 平台接入与本地交付

更新：2026-10-29。依据[飞书 SDK 交付页](https://vcn5vzll02xu.feishu.cn/wiki/COmowpoWCiwcQUkvzkKcPJB8nPb)修订 25，以及 `lua-sdk-api7-20261020.zip` **包内** `START-HERE.md`、`GameRegistrationAndVersioning.md`。页面单独附带的旧说明不作为当前基线。

## 固定基线与执行范围

Unity 按用户决定保持 **2022.3.62f2**；Built-In、UGUI、Android。SDK 三包 `0.2.0-localtest.9730783e9569c94e`、Localization 1.4.4、Host API 7、Lua 5.4.8、xLua ABI 105。SDK 文档参考 Unity 2022.3.76f1，工程没有升级；Android Bundle 清单记录实际构建版本，宿主必须兼容实际版本。

用户要求不运行游戏和测试。本轮仅编辑源码、处理静态数据/资产、编译和构建。官方模块打包器的 `ModuleLuaValidator` 创建 Lua 环境并调用 `load(source)` 编译语法，**不调用被编译出的游戏 chunk**；不运行 SDK 检查套件、Play Mode、APK 或设备联调。语法/构建通过不代表运行、AOT、机制效果或帧率验收通过。

四个原始 tgz 位于 `Packages/MiniGameSdk/`；旧独立 xLua 托管代码/Wrap/原生插件已移到工程外备份，统一引用 `MiniGamePlatform.XLua`。旧 APK 是迁移前产物，不能用作新 SDK 的运行证据。

## 真实模块的源码分层

| 源码 | 责任 |
| --- | --- |
| `lua/game/rule_engine.lua` | 唯一玩法算法；独立版 `rules.lua` 注入原 C# 数据模型，平台版注入纯 Lua 模型，不复制两套规则 |
| `rule_model.lua`、`rule_session.lua` | 零基集合、枚举、状态/快照、完整关卡数据归一与会话；可选 JSON null 与数组槽位分别处理 |
| `conveyor_spline.lua` | 原中点细分和 B-spline 位置/距离采样，共用真实关卡几何；不是折线估算长度 |
| `json.lua`、`level_repository.lua` | 严格 JSON 解析、每文件低于 4 MiB 的分片、ID/版本/路径校验、两片 LRU；加载失败不缓存，关卡返回独立副本 |
| `module_runtime.lua` | `enter/update/late_update/pause/resume/exit`，加载门闩、宿主/菜单暂停叠加、旧关卡回调失效、可重试退出 |
| `lua/platform/` | 原生资产读取、真实场景/运输/机制动画、中文 UGUI、存档与服务；不调用 `CS.LoopSort0` |
| `Editor/PlatformAssetBuilder.cs` | 原有模型/图集/字体/材质/音频与机制数据的原生资产准备；可下载 Prefab 不携带游戏 MonoBehaviour |
| `Editor/PlatformDataBuilder.cs` | 完整导出 1299 个主关卡、30 个每日关卡，索引和 13 个分片，SHA-256 清单；每关所有源字段保留 |
| `Editor/PlatformModuleBuilder.cs` | 白名单收集纯 Lua、require 命名空间改写、关卡映射、官方 Android 模块构建与本地审核草案 |

平台构建入口取 `platform/main.lua`，转为 `games/<gameId>/main.lua`；其余文件位于 `games/<gameId>/platform/` 和 `games/<gameId>/game/`。所有游戏内 `require` 改写到同一游戏根，并检查目标实际存在。独立版 `game/rules.lua`、`config.lua`、旧 UI 工厂、C# 桥接文件不进入模块；构建前直接拒绝 `CS.LoopSort0`。模块只包含 Lua、声明的数据和 `<gameId>.bundle`。

## 场景与宿主边界

平台表现层使用模块独立 PhysicsScene 固定步进，保留 Rigidbody 碰撞与原运输参数；模块自己拥有相机、光源和对象。世界渲染使用 **layer 30**，相机与光源只覆盖该层，宿主需为此模块预留该层。该约定不是 SDK 自动分配功能。不要修改宿主 `Time`、全局 Physics、RenderSettings 或其他游戏相机。

SDK `unity_native_v1` 的 link.xml 全量保留 CoreModule/PhysicsModule；因此 SceneManager、CreateSceneParameters、PhysicsScene、Graphics 类型在其保留范围。但 `System.Array.CreateInstance` 形成实例绘制数组的 IL2CPP AOT 行为仍没有设备证据，不把编辑器静态保留推断成真机已验证。模块 UI 使用宿主 ModulePointerRelay，避免依赖未声明的旧输入模块。

实例放 `host.Root` 下或 `TrackInstance`；退出停止物理、动画、音频，销毁本模块创建的 Mesh/Material/Sprite/Texture，借用 Bundle 资产不销毁。外部事件用原委托解绑并登记 `TrackCallback`。SDK 退出回调仅提供一次额外清理机会；失败上抛并保留失败项，不能声明资源已经回收。产品壳等待 `HasPendingExit=false` 再重入。

## 本地构建与审核材料

菜单：**LoopSort → 平台迁移 → 构建本地 Android 模块（不运行游戏）**。

可供编辑器调用的入口：

- `PlatformModuleBuilder.PrepareLocal()`：准备新不可覆盖目录，冻结 Lua/数据和资产哈希。
- `PlatformModuleBuilder.PrepareLocalFromAssets(indexPath)`：显式使用已完成的原生资产烘焙结果，避免重复烘焙。只接受本工程 `Assets/LoopSort/PlatformAssets/index.json`；验证 schema、Bundle 名称、每项资源存在且已导入，再冻结全部资源依赖与哈希。它不会自动猜测缓存是否有效。
- `PlatformModuleBuilder.BuildPrepared(directory)`：确认冻结内容未变，调用官方 `AndroidModuleBuilder.Build`，生成模块和审核草案。
- `PlatformModuleBuilder.BuildLocal()`：连续执行上述两步。
- `PlatformModuleBatchBuild.Build()`：单独隔离 Unity 工程的无参批处理入口；使用 `-executeMethod LoopSort0.Editor.PlatformModuleBatchBuild.Build -platformBuildReceipt <绝对路径.json>`。仅 batch Edit Mode 可调用，复用已烘焙索引，写不可覆盖的成功/失败回执与真实 manifest 哈希。回执不包含伪造的运行结果或 APK 签名证明。

路径为 `Builds/PlatformModules/local_<UTC时间>_<随机短码>/`。`source/` 是明确白名单的打包输入；`module-definition.json` 描述能力和数据根；`preparation.json` 记录冻结源数据/资产哈希。最终 `output/modules/loopsort0/<version>/` 包含官方 manifest、Lua、数据和 Android Bundle。`output/review/loopsort0/<version>/` 包含原生资产与 Lua 源码、模块定义、配置模板和 `review-status.json`。

`loopsort0` 明确为**未登记的本地草案身份**，不是声称已经注册的永久 gameId。规则源码版本 `1`、存档 schema `2` 不代表服务器已登记。版本目录生成后不可覆盖。构建器不改 Git 暂存、不运行游戏、不签署产品目录、不部署服务器或上传 CDN。

`review-status.json` 区分完成的构建检查与未运行的验证。此草案不生成伪造 `gate-report.json`、APK/设备日志或正式 `delivery.json`；没有双包证据时不冒用 `PrepareModuleDelivery.ps1` 的最终交付资格。构建执行路径和实际 manifest 哈希以本次工具结果/产物为准。

### 本轮已生成的模块

隔离批处理于 **2026-10-29 03:53:01 UTC** 完成官方 SDK Android 模块构建，真实回执状态为 `built`。模块版本为 **`local_20261029_035203_702_08ca56ff`**，manifest SHA-256 为 **`86854cc651da2e467efcbbb684eb8b1c9c01187c803c674f1ac3ba3ae82261bc`**。

本地交付目录：`D:/MiniProject/workspace/games/LoopSort/Builds/PlatformDelivery/local_20261029_035203_702_08ca56ff/`。

| 材料 | 实际内容 |
| --- | --- |
| `module/` | 完整可加载模块：37 个清单内文件及官方 `manifest.json`，共 38 个文件 |
| `module.zip` | 54,323,105 字节；解压根目录直接含 `manifest.json` |
| `review-source.zip` | 84,505,840 字节，5,378 个审核草案文件；直接压缩原审核目录，避免展开后的 Windows 长路径 |
| `module-build-receipt.json` | 原始成功回执副本，保留实际构建位置、版本、固定哈希和未运行范围 |
| `LoopSort0-Platform-LocalOnly-arm64.apk` | 本地离线独立 APK，109,638,318 字节；使用上面的相同模块 manifest 哈希 |
| `apk-build-receipt.json` | 真实独立 APK 构建成功回执副本，含 APK 哈希与离线构建配置 |
| `artifact-integrity.json`、`SHA256SUMS.txt` | 最终材料的静态完整性记录和统一 SHA-256 清单，不是运行验收或正式发布门禁 |
| `README.md` | 材料用途、真实构建结果与尚待完成的正式发布事项 |

原始准备目录为 `D:/MiniProject/Builds/LSM/Builds/PlatformModules/local_20261029_035203_702_08ca56ff/`；其 `output/modules/loopsort0/<version>/` 与 `output/review/loopsort0/<version>/` 仍保留。复制前按官方 manifest 逐文件确认大小和 SHA-256，未运行任何游戏逻辑。模块和 APK 均已构建成功；`review-source.zip` 保持模块构建时的冻结内容，APK 回执和最终静态校验材料单独提供，未改写模块字节、版本或审核压缩包。

## 本地独立 APK

宿主源码位于 `Assets/LoopSort/PlatformHost/`，入口为 `PlatformLocalBootstrap`，构建器为其 `Editor/PlatformLocalPlayerBuilder.cs`。构建器创建独立 Unity 工程，只复制 SDK、宿主基础设施、字体及已经固定 manifest SHA-256 的平台模块；原工程的 `LoopSort0.Core`、`LoopSort0.Runtime` 和旧场景不进入这个 APK。编译设置为固定 Unity **2022.3.62f2、Android ARM64、IL2CPP、OpenGLES3**，输出 APK，不输出 AAB。

隔离工程包含官方 `com.minigame.platform-authoring` 包，最终通过官方 `ModuleHostKeyBuild.BuildPlayer` 包装入口构建。SDK 生成空内容密钥表，对内容密钥请求执行 deny-all；回执记录 `nativeContentKeyMode=sdk-generated-empty-keyring-deny-all`。这是满足本地壳原生构建契约的空实现，不配置、导入或冒用生产内容密钥，也不表示获得正式发布授权。

2026-10-29 的上一轮构建曾遇到 UPM 初始化读取计数停滞和目录枚举阻塞。本次使用 `PrepareFromCommandLine()` 先准备工程，再配置 11 个同版本本地包引用；启动 Unity 前已重新逐字节核对 4 个固定 tgz 的 1,995 个文件。未改包源码、Unity 或 SDK 版本。`dependency-cache-recovery.json` 保留实际缓存位置、版本、tgz 哈希和核对数量；本次实际 APK 构建日志为 `unity-build.log`。

显式 `Always Included Shaders` 仅加入 **`UI/Default`、`Sprites/Default`**。字体内部 `GUI/Text Shader` 来自 `Library/unity default resources`，标记为 `HideAndDontSave`，不能把该内部对象写入 Always Included；字体依赖通过 `Resources/PlatformLocal/NotoSansSC-Medium.otf` 与场景中未激活的 `UGUI.Text` 字体引用保留。模块依赖清单中的字体 Shader 名称声明不等于显式序列化其内部对象。回执分别记录 `explicitIncludedShaders` 与 `fontBuiltinShader`，字体运行呈现尚未实机验证。

该壳明确使用离线设备身份 **`local-only:default`**，通过 `OfflineLocalServiceFactory` 提供按设备身份和 gameId 隔离的本地存档。`InitializeWithHost` 传入空 `PlatformSession`，不会制造平台登录凭据、正式 productId 或云账号；`loopsort0-local-only` 只是本地产品标识。网络、广告、排行榜、队伍、云存档和支付不可用时如实返回不可用。`OpenInstalled` 校验本地固定哈希，不能代替正式平台的模块选择、签名目录或发布授权。

可用构建入口均属于 `LoopSort.PlatformLocalHost.Editor.PlatformLocalPlayerBuilder`：

- `PreparePrepared(modulePath, manifestSha, outputRoot = null)`：接收已构建模块目录及其真实 manifest SHA-256，校验模块后准备不可覆盖的独立工程，并返回工程目录。`outputRoot` 省略时为当前工程下 `Builds/PlatformLocalPlayers`；需要缩短 Windows 构建路径时显式指定本轮短输出根 **`D:/MiniProject/Builds/LoopSort0Platform`**。
- `StartPreparedProject(projectDirectory)`：使用同一版本 Unity 启动隐藏的 batch 编译进程，返回进程对象。准备目录中的 `platform-local-project.json` 记录预定 APK 和日志路径；进程启动不表示构建已经成功。
- `BuildPrepared(modulePath, manifestSha)`：使用默认输出根，同步等待独立编译进程，成功后返回 APK 路径。
- `BuildFromCommandLine()`：仅 batch 模式可用的无参启动入口。`-executeMethod` 取完整名称 `LoopSort.PlatformLocalHost.Editor.PlatformLocalPlayerBuilder.BuildFromCommandLine`，下表三个参数均必须显式提供一次。它只准备工程并启动独立编译进程，外层启动器退出不代表 APK 完成。
- `PrepareFromCommandLine()`：接受相同三个参数，仅准备独立工程并退出；用于在首次启动该工程前配置已经核对的本地包路径，再调用 `BuildIsolatedFromCommandLine()` 完成实际 APK 编译。

| 命令行参数 | 值 |
| --- | --- |
| `-platformModuleDirectory` | 已构建模块目录的绝对路径，目录内含官方 `manifest.json` |
| `-platformManifestSha256` | 上述 manifest 文件的真实 64 位十六进制 SHA-256 |
| `-platformLocalOutputRoot` | 专用输出目录的绝对路径；本轮选择 `D:/MiniProject/Builds/LoopSort0Platform` |

独立编译只有在官方 `ModuleHostKeyBuild.BuildPlayer` 返回成功后才写 `build-receipt.json`，记录 APK 路径、大小、SHA-256、固定模块哈希与 `buildSucceeded`，同时明确 `localOnly=true`、`platformRegistered=false`、`productionReady=false`、`gameplayExecuted=false`、`automatedTestsRun=false`。

本次真实回执位于 `D:/MiniProject/Builds/LoopSort0Platform/20261029_035353_645_a1bb336e/build-receipt.json`，`buildSucceeded=true`。APK 原始路径为同目录下 `LoopSort0-Platform-LocalOnly-arm64.apk`，大小 **109,638,318 字节**，SHA-256 为 **`b4b9bb0baede3d3593f28ecd1c7e13aa4d65527de0a7b629afae550fe17bdd3b`**；固定模块 manifest 哈希仍为 **`86854cc651da2e467efcbbb684eb8b1c9c01187c803c674f1ac3ba3ae82261bc`**。构建成功不表示真机效果、帧率、AOT 运行或独立包/综合包双包门禁通过；未运行 Play Mode、游戏、APK、SDK 测试套件或设备验收。

## 最后一轮静态复查修正（2026-10-28）

- 普通启动从有效存档选择当前关卡，首页预载不再将进度覆盖为第 1 关；显式 `Launch.LevelId` 仍优先。
- 超时失败时禁用扩容续玩，UI 和动作入口均拒绝该操作，避免扣除道具后立即再次超时。
- 每日挑战保留开始日期，重试沿用该日期；跨 UTC 午夜结算不会误记到第二天。
- 传送带容量按原始整体占格计算；接收途中重新释放同一整体的碎块不会重复占格，新整体和完整粘连链仍受容量限制。顶部显示使用同一占格数。
- 接收动画开始即切换为组装碎块尺寸，避免到达货车后突然缩放。
- 幕布材质按颜色和图案复用，换关和撤销不再持续新增同样的原生 Material；模块退出统一释放。
- 撤销重建机制时，已完成车辆的障碍和颜色旗帜保持隐藏。
- 本地 APK 菜单同时查找 `Builds/PlatformModules` 和 `Builds/PlatformDelivery` 的完整模块，支持隔离构建后回传的交付目录。

复查仅包括源码与调用链、Lua 语法编译、构建和文件完整性。没有运行游戏、Play Mode、测试或 APK；运行效果、AOT 实机行为和帧率仍待实际设备验证。

## 手机启动错误修复（2026-10-29）

用户提供的旧 APK 截图显示 `platform.ui:21` 调用 `Object.op_Equality` 为 nil，并在模块清理时重复触发。SDK 内 xLua 的 `Runtime/Utils.cs` 将 C# 运算符注册到 Lua 元方法表，不导出同名普通静态字段；CoreModule 已完整保留，因此增加裁剪白名单不能解决该错误。

UI 统一复用 `platform.native.alive`，不再直接调用运算符字段。首次修复仅检查 `Equals(nil)`，用户随后报告进入关卡时 `Non-static method requires a target`；后续修正如下。

## 进入关卡后的对象回收错误修复（2026-10-29）

SDK xLua 的 `LuaEnv.Tick` 会检查 Unity 对象，`ObjectPool.Check` 将已销毁对象的托管目标替换为 null，但 Lua userdata 仍可能存活。此时直接调用 `v:Equals(nil)` 会通过反射调用空目标，触发截图中的 `Non-static method requires a target`。主页销毁后残留的金币、体力文字引用和布局记录会走到这条路径。

`Native.alive` 现在先通过静态 `System.Object.ReferenceEquals(v,nil)` 检查托管目标，再调用实例 `Equals(nil)` 处理尚未被 xLua 清理的 Unity 已销毁对象。静态调用的参数转换经过 SDK `ObjectCasters.getObject` / `SafeGetCSObj`，不依赖 userdata 中存在可调用的实例目标。本地壳 link.xml 保留 `System.Object`，正式平台宿主也需要保留该反射 API。

主页销毁同步清空文字与内容引用，关卡中停止刷新主页文字；UI 层销毁前移除所属布局记录，并释放层级根引用。修正不吞掉异常、不自动重启关卡。已做源码复核和 27 个 Lua 文件的语法编译；新版本构建与静态材料校验结果见上方交付记录。未运行游戏、测试或真机复验。

## Lua 表现层高频调用优化（2026-10-29）

这轮仅修改 Lua 表现层，规则仍可热更，保留物理碰撞、约 30 Hz 的手动物理步进、原传送速度和收集判定。宿主原本已设置 60 FPS 目标；目标帧率不代表设备能稳定达到该值。

- 轨道样本和线段预存数值坐标，空间搜索、投影、侧向纠偏和入车距离计算改用 Lua 标量运算。搜索内部不再为每个候选点跨 xLua 创建 Vector3、调用运算符和读取 sqrMagnitude；每次物理步只读取一次起始 Rigidbody.position。保持原搜索半径、样本顺序、纠偏阈值和施力方式。
- 放出碎块直接使用已计算的车口轨道点和切线，不再对每个碎块重新搜索整条轨道。
- 碎块 Mesh、Renderer 状态、材质颜色和消融进度建立 Lua 缓存。只在状态变化或消融推进时写入原生材质/PropertyBlock；暂停仍启用普通 Renderer，恢复后回到实例绘制。
- 实例绘制按 Mesh 和每批 1023 个实例复用矩阵/颜色缓冲区，一次遍历可见碎块。矩阵按位置修订号更新，静止拼装碎块复用；颜色转换、Vector4 创建与颜色数组上传只在数据变化时发生。换关清空缓冲，撤销、飞行、物理移动和组装更新位置修订号。
- 旧 C# `PieceView` 使用 Rigidbody Interpolate，而手动独立物理场景原先直接显示约 30 Hz 的位置。实例绘制现按物理累计时间插值前后两个位置/旋转，仅生成绘制矩阵，不回写碰撞体；入带、撤销和传送门跳转重置样本，防止插值穿过场景。非实例绘制设备继续使用普通 Renderer，不虚报其具备这一插值优化。
- 文字、锚点矩形和提示底图只在变化时写入 Unity；缓存以弱键保存，不延长已关闭 UI 的寿命。教程保持逐帧手势动画，车辆屏幕投影只在目标或相机布局变化时重新计算。完成勾选的朝向仅在动画角度/相机变化时更新。

已完成源码和失效路径复核、27 个 Lua 文件语法编译与构建校验。不运行游戏、性能测试或 APK；以上是可确认的调用量和分配路径优化，没有虚构优化前后 FPS、CPU 时间或内存数据。

## 按文档最后一步的剩余外部条件

本地源代码、资源、定义和模块可独立完成；正式第 6 步需要平台/服务器/发行方提供真实信息：

1. 登记永久 gameId，以及独立包、综合包各自的 productId；模块内不能写 productId。
2. 服务器确认规则版本、关卡命名空间、存档 schema、统一 userId 和幂等结算；本地 Storage 不等于云存档。
3. 在产品壳配置 HTTPS 服务/CDN、真实广告/支付适配与签名信任目录；缺失服务不能返回假成功或发假广告奖励。
4. 按用户后续安排提供真实独立包/综合包 APK 和设备证据，两包使用相同 manifest SHA-256，再执行 SDK 的 `VerifyModuleRelease.ps1` / `PrepareModuleDelivery.ps1`。
5. 后台测试环境审核发布，完成验证后晋级相同哈希到正式环境。当前没有凭据、正式登记、CDN 或发布记录，不能替用户伪造这一步已完成。

素材来源与权属记录由游戏团队提供，原 APK 提取记录不代替发行许可。详见 [game-intake.md](game-intake.md)。旧独立版仍保留作为现有项目可运行路径，在新平台模块经过实际运行验收前不删除其必要代码。


## 2026-10-29 最新 UI 模块与产品 SDK 准备

最新模块为 `local_20261029_122117_035_5a71831d`，39 个清单内文件，manifest SHA-256 为 `e73fa0b8c6cd926af4568460b3299c52f59036a667d01fb55e2b2a7198d6131b`。重新烘焙资产，补齐共享 UI 主题的白名单及命名空间；原历史包不覆盖。

产品已确认独立于 Wool Rush。MAX、AF 产品层代码和临时配置的使用方式、未分配项及真实验收边界统一见 [ProductIntegration.md](ProductIntegration.md)。本轮不运行游戏/测试，不新增 APK，不声明正式登记或发布成功。

## 2026-10-30：图鉴、道具过渡与火山机关配色

平台图鉴卡片字号 35→44、详情说明 38→46，扩展文字区域并删除首次出现关卡文案；旧独立版图鉴同步移除此文案并放大文字。

新增 `lua/platform/boosters.lua`，仍由模块 update 驱动，不增加游戏专属 C# 或全局协程。原 APK 静态读取依据为 ShuffleGroup MoveNext `0x23D9B98`（抬起、侧移、换位、对齐、落回）、ShuffleSphere `0x23DA6B0`（独立碎块运动）、Capacity MoveNext `0x23D09DC`（缩放入场）、容量图标 MoveNext `0x23D17A4`（飞向容量栏）和 Undo `0x23DC940`（返回 Carrier.AddBlock）。整块打乱保留原整块网格，以 0.2 秒抬高 2 单位、0.3 秒侧移 0.75、距离×0.16 秒换位、0.15 秒对齐、0.1 秒落回实现；零距离段保留最小过渡。未恢复的碎块随机球形轨迹、曲线与容量专用粒子不宣称逐帧相同。

平台版撤回增加 0.45 秒回飞、扩容增加 0.75 秒图标运动和容量栏反馈、加车增加 0.4 秒缩放入场与已导出的 spawn 粒子。仍使用原有逻辑状态与撤回快照；这些过渡是按现有资源重建的表现，不是执行原 APK 的动画组件。道具动画期间禁止重复使用和货车释放，暂停物理和规则计时；切关/退出清理副本与 UI，返回首页先落定已接受操作。平台打包器自动收集新增的 platform Lua 文件。

传送门使用熔岩橙、玄武岩暗色和铜金；地毯使用砖红、深红与暖金边。平台资源生成器和独立版材质映射同步更新；平台运行时对旧 Bundle 预制体使用模块自有缓存材质副本，避免修改宿主借出的共享材质。原图鉴栅格插图未重新绘制。

按用户要求，本次仅修改源码和说明，没有运行游戏、测试、语法检查、编译验证或构建 APK，也没有生成临时分析文件。重新构建平台模块和 APK 后才会包含这些修改；实际画面效果仍待用户查看。
