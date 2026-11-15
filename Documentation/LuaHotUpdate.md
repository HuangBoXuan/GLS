# Lua 与热更新

更新：2026-10-28。项目已安装平台交付的 SDK 四个 UPM 归档：核心 SDK、共用宿主、开发工具均为 `0.2.0-localtest.9730783e9569c94e`，另含 Localization 1.4.4。依赖已由 Unity Package Manager 解析，版本以 `Packages/manifest.json` 与 `packages-lock.json` 为准。

xLua 托管代码和 Windows/Android 原生插件由 `com.minigame.platform-host` 提供，Lua **5.4.8**、xLua ABI **105**、Host API **7**。原工程自带的 Lua 5.3/xLua、生成 Wrap 和旧生成器已移出工程；不要再导入另一份 Lua DLL、libxlua.so 或旧生成代码。许可证和原生来源记录随固定 SDK 包保留。

**当前仍是独立游戏的过渡接入。** 安装 SDK 不等于平台纯 Lua 模块已经交付。游戏仍通过反射调用本 APK 编入的 `LoopSort0.Core`、`LoopSort0.Runtime` 等 C# 类型；目标综合包不会因为下载 Lua/AssetBundle 而获得这些类型。后续模块迁移、物理/渲染能力和生命周期要求见 [PlatformIntegration.md](PlatformIntegration.md)，登记项见 [game-intake.md](game-intake.md)。

**按用户决定，本工程固定继续使用 Unity 2022.3.62f2，不升级编辑器。** SDK 文档中的 2022.3.76f1 保留为参考基线；后续迁移和构建按 62f2 执行，不把升级列为前置步骤。编辑器编译记录不代表新 SDK 在该版本下已完成 Android 运行验收。

## Lua 与 C# 的当前边界

Lua 源码位于 `Assets/StreamingAssets/LoopSort/lua/`，当前共 18 个模块：

| 模块 | 职责 |
| --- | --- |
| `game/rules.lua` | 独立版兼容入口，向共享规则注入现有 C# 数据模型 |
| `game/rule_engine.lua` | 唯一规则算法：碎块密度、整块归属、释放/接收、机关条件、胜负、打乱、快照和撤回；自身无 CS/Unity 依赖 |
| `game/rule_model.lua`、`game/rule_session.lua` | 纯 Lua 状态、零基集合和快照；将已解码的关卡表转换并创建独立规则实例 |
| `game/conveyor_spline.lua` | 纯 Lua B-spline 位置采样、长度与按距离定位；尚不包含切线、网格和物理 |
| `game/json.lua`、`game/level_repository.lua` | 严格 JSON 解码、平台关卡索引和分片按需加载，最多缓存 2 片 |
| `game/module_runtime.lua` | 宿主生命周期工厂；加载门闩、暂停叠加、旧回调失效及可重试清理，需要真实 Lua 表现层接入 |
| `game/flow.lua` | 主页与关卡流程、选关、教程、道具和进度 |
| `game/config.lua`、`game/guide_levels.lua` | 数值参数与机制首次出现关卡 |
| `game/peripheral.lua`、`game/peripheral_config.lua` | 生命、每日挑战、奖励、区域等外围业务和配置 |
| `game/activities.lua`、`game/activity_config.lua` | 任务、活动和通行证规则与配置 |
| `ui/pages.lua`、`ui/skin.lua`、`ui/assets.lua` | 页面、按钮行为、HUD、布局、配色、图集和材质选择 |

`GameState.cs` 是现有独立版的数据/调用接口。共享规则已抽出，同一份算法可绑定现有 C# 数据模型或新的纯 Lua 模型；规则正文与拆分前逐字一致。独立版仍通过 `game.rules` 绑定原 C# DTO，避免把 Lua 表直接传给强类型视图而破坏返回值。纯 Lua 会话尚未接管游戏画面。C# 仍负责 Unity 生命周期、HUD 工厂、实际场景中的物理/轨道、模型显示、动画驱动、音频、文件/网络和存档序列化；这些仍是后续平台模块迁移的真实依赖，不能随可下载模块下发。

当前 18 个 Lua 文件已仅做语法解析，未执行代码、游戏或测试；内置 manifest 已同步。C# 游戏公开接口未变，桥接指纹不变。纯 Lua 会话、数据分片产物和待完成的表现层接入见 `PlatformIntegration.md`。

现有 `LoopSort0.Player.v1` JSON 存档继续保留，关卡、金币、道具、教程、设置和活动字段不因 SDK 安装而清空。Lua 启动错误不能当成存档损坏而重置；无法解析的旧 JSON 保留在同键 `.corrupt` 备份。`luaData` 为 C# 字符串字典，Lua 使用 `get_Item`/`set_Item` 访问。

## 独立包反射桥接流程

新宿主的 xLua 运行时与静态回调由包拥有，项目不再向另一程序集生成 xLua partial 类。

- `Assets/LoopSort/Editor/LuaBindingConfig.cs` 现在记录独立包所需类型、委托、SDK/Lua 版本和桥接模式，不再是旧 xLua 代码生成配置。
- `Assets/LoopSort/Runtime/HotUpdate/link.xml` 保留独立包的游戏程序集及使用的集合等类型，支持反射访问。它只服务于独立包，不会向共用平台宿主注册游戏专属能力。
- 常见 Lua 回调使用宿主已有的静态 `NativeDelegateBridge`。新增委托签名或泛型调用仍需核实 IL2CPP 支持；link.xml 的类型保留不能替代静态回调实现。
- `BindingId.json` 包含 SDK、Lua、桥接模式及 `standaloneOnly` 标记。指纹覆盖 SDK/Lua 版本、保留清单和公开游戏接口；不是“生成了 Wrap”的证明。
- `GameHud.OnBuyBooster` 的 `Func<string,bool>` 由 C# 创建后供 Lua 调用，不是把 Lua 函数转换成宿主未承诺的带参 Func。

修改 C# 桥接、委托、保留清单或 SDK/原生库后，刷新桥接清单并重建基础 APK。Lua 热更不能替换手机上已安装的原生运行库或新增 C# 能力。

## 首次使用新版运行库

1. 若当前 Unity 进程曾运行旧 Lua 5.3，完全退出并重新打开编辑器。域重载不会卸载已装入进程的旧原生 DLL。启动代码检查 Lua 主版本，避免误用旧库。
2. 等待 UPM 导入和项目编译完成，检查三包为同一固定版本。不要把旧 `Assets/Plugins` 中的 xLua 插件复制回来。
3. 使用下述菜单刷新独立包桥接清单，然后重新构建 SDK/Lua 5.4 基础 APK。现有旧 APK 不能仅靠下载脚本升级为新版运行库。
4. 独立版可在 `Assets/LoopSort/Resources/LoopSort/HotUpdateConfig.json` 配置自己的 HTTPS `manifestUrl`。当前值为空，未部署外部更新服务器。平台模块后续使用宿主目录和加载器，不把此独立更新地址当作平台发布配置。
5. 对同一个新版基包，仅修改既有桥接范围内的 Lua、JSON 或图片，可重新生成内容包；更改原生能力仍需新的 APK。

未配置独立更新地址时使用内置内容；“设置 → 检查更新”提示暂未开放在线更新。SDK 安装不会初始化真实账号、广告、支付或生产服务器。

## 生成独立版更新包

平台模块的数据准备使用另一个菜单：`LoopSort → 平台迁移 → 导出关卡分片`。它把主关卡与每日关卡导出到 `Builds/PlatformData/<唯一目录>/levels/`，解决原主关卡 JSON 超过宿主 `ReadDataText` 4 MiB 上限的问题，不创建 Lua VM。该产物是平台模块的数据输入，不是下列独立版热更包，也不是完整平台模块。正式打包时须使用宿主的真实游戏 ID 放入 `data/<gameId>/levels/` 并声明所有文件。

Unity 菜单：

- `LoopSort → 热更 → 1. 刷新独立包桥接清单`：写入 link.xml、BindingId 和内置 Lua manifest；不生成旧版 Wrap。桥接变化后需重建 APK。
- `LoopSort → 热更 → 2. 生成 Lua、关卡和图片更新包`：生成普通内容更新包。
- `LoopSort → 热更 → 3. 生成含模型、字体和音频的完整更新包`：额外构建当前平台 AssetBundle，适用于模型、字体、音频、Shader 等原生 Unity 资源变更。
- `LoopSort → 热更 → 4. 构建 Lua Android APK`：以 IL2CPP、ARM64 构建独立基包，输出 `Builds/Android/LoopSort-Lua-arm64.apk`。

构建前核对桥接清单与指纹；不一致时先刷新清单。Android 基包构建会做 Lua 语法加载编译，不执行游戏 Lua 代码块。

已刷新桥接清单后，也可在项目根目录生成普通内容包：

```powershell
python Tools/build_lua_content.py
```

此 Python 工具仅使用标准库，不运行游戏。`--version-code` 指定递增版本号，`--out` 指定新输出目录，`--embedded-only` 仅更新随 APK 的 Lua 清单。工具不能生成或验证宿主平台模块，也不能替代 C# 桥接清单刷新。

独立内容输出通常位于 `Builds/HotUpdate/<platform>/<versionCode>/`。上传时保留整个版本目录及相对路径，不能只上传 manifest；版本不可覆盖，必须高于已安装/下载版本。普通 Lua/JSON/图片内容可面向同一桥接的 Windows/Android；AssetBundle 必须匹配目标平台和兼容的 Unity/资源环境。

启动时在 Lua 初始化前选择完整可用内容。游戏内手动检查仅下载并保存，重新启动后生效，不在运输途中替换 Lua VM。没有执行过真实联网热更，生成内容包不代表下载、回退与实际行为已验证。

## 独立加载与校验

- Android 的 StreamingAssets 位于 APK 内，使用 UnityWebRequest 读取，不直接对 jar 路径调用 File.ReadAllText。
- 下载到独立暂存目录，文件长度及 SHA-256 全部通过后才提交安装指针；失败保留原版本。
- 校验 schema、桥接版本、指纹、APK 版本、渠道和资源平台，并限制路径、数量和总大小。
- 网络失败保留当前内容；损坏缓存尝试上一份完整内容；Lua 初始化或 AssetBundle 加载失败具有回退路径。
- 图片和 JSON 可按内容清单覆盖；模型、字体、音频及 Shader 使用可选 AssetBundle。
- 远程内容默认仅接受 HTTPS；编辑器本机开发可使用回环 HTTP/file 地址。没有部署或发布远程服务。

这些是既有独立加载器的行为。后续平台模块由 host 管理 LuaEnv、清单、资源租约和退出回收；不能在模块内再创建第二套平台 SDK/VM 或全局卸载 Bundle。

## 当前记录与历史产物

2026-10-28 18:01:55，本轮 SDK 接入后的编辑器 Runtime/Editor 程序集已完成编译，Unity 不在 Play，控制台没有 error。随后已刷新独立包桥接清单，新指纹为 `1965b10bb495ee2ce653036413100e38ee4008b057567d8ca40496e2573f3d1e`；SDK/Lua/桥接模式已写入 `BindingId.json`，11 个 Lua 模块的内置 manifest 同步。该记录只证明编译和清单生成，当前 Unity 进程仍需完整重启以卸载旧原生 DLL。

`Builds/Android/LoopSort0-Lua-arm64.apk` 是此前 Lua 5.3 阶段的历史产物：2026-10-28 00:25 左右生成，包含 6 个 Lua 模块、旧桥接指纹和旧 xLua 库。它曾完成 Unity 2022.3.62f2 编辑器/Android 玩家程序集、IL2CPP ARM64 与 Gradle 构建，但未安装或启动。这些构建结果不能证明当前 SDK/Lua 5.4 版本通过 Android 构建或运行。详情保留在旁边的 `build-info.json` 和历史 `LuaMigration.json`。

`Builds/HotUpdate/any/1793115270/` 与 `1793118284/` 同为旧桥接内容包，不是当前 SDK 更新包，也不是平台审核产物。旧报告中的文件数、大小和绑定值仅用于追溯；当前值以重新生成的 BindingId、manifest 和构建记录为准。

旧独立构建曾记录 xLua 的 16KB 对齐观察，新 SDK 原生库另有自己的 provenance/静态校验记录；二者不能互相替代，更不能据静态对齐认定设备兼容。

按用户要求，本轮未进入 Play Mode、运行 SDK 检查菜单或测试、自动试玩、安装/启动 APK，也未进行设备、真实广告或服务器/CDN 联调。项目编译、Lua 语法检查及 UPM 解析结果与实际运行验收分开记录。下一阶段先完成合规平台模块的真实首关切片，再安排目标产品的验证与交付。
