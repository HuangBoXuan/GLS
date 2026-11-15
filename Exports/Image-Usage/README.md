# 当前游戏图片

2026-11-04 更新：下方清单与网页是 11 月 3 日的历史采样，未包含后来导入的 `Theme` 新 UI。旧 `game-board.png`、`tutorial-hand.png` 已分别被 `gameBackground.png`、`tutorialHand.png` 替换并移出工程；主页使用 `homeBackground.png`。旧主页插画 `home-conveyor.png` 仍用于失败弹窗，因此保留。最新清理与运行时加载验证见 [清理记录](../../Documentation/Performance-Cleanup-20261105.json)。

项目：`D:/MiniProject/workspace/games/LoopSort`

[打开图片浏览页](index.html)：仅展示当前游戏资源加载代码调用的 29 张源图片，直接引用 Assets 文件，不生成重复预览图。

- Theme：22 张游戏 UI 图集、机制插画及矿石材质。
- Textures：7 张由粒子配置动态加载的特效纹理。
- 另有 5 张 MAX / AppsFlyer 工具图，是 SDK 依赖，不计入游戏美术。

`runtime-images.csv` 是这 29 张图的完整路径和调用依据；`project-image-assets.csv` 包含 SDK 工具图。图集可能有未使用小图块，但整张图集仍在使用，不能直接删除整张文件。

## 2026-11-03 Unity 实时采样

已直接连接当前 Unity 编辑器读取运行数据，见 [运行时图片路径清单](live-runtime-images.csv) 和 [完整采样报告](live-runtime-report.json)。正常 Play 状态的主页/图鉴共实际加载 **22 张图片**；**7 张粒子特效**在这次采样中没有触发，但其启用材质配置及正式调用路径仍存在，已单独标注。5 张 SDK 工具图不计入游戏资源。

最初会话 Lua 未初始化，图鉴调用未生效；已重新进入 Play 并确认 LuaReady=true，关卡仍为 201，采样后已关闭图鉴回到主页。本轮没有发现可安全继续删除的图片，新增删除数量为 0。检查工具与原始快照存于项目外，未向游戏加入采样脚本，也未重打包 APK。

## 先前清理

按用户“只保留游戏真正使用的图片”的要求，本轮移出 742 张图片，约 341.40 MiB。包括旧设计/截图、全部导出预览、原始 UI 参考档案，以及旧 panels、worlds、mine-key、mine-lock 图片和 9 张旧人物头像。当前游戏没有人物头像选择入口；旧接口已改为使用现有通用图标，保存和服务器用的头像 ID 保持兼容。相关参考目录的元数据、说明和索引一起归档，以保持原预览页面完整。

项目外档案：`D:/MiniProject/BuildTemp/LoopSort-runtime-images-only-20261103-150127`。
原导出预览：`D:/MiniProject/BuildTemp/LoopSort-runtime-images-only-20261103-150127/Exports/Restored-UI-Models/index.html`。
`export-ui-source-map.csv` 仅记录项目外历史导出的来源，不代表当前游戏图片清单。

上一轮另外移出了 74 张旧 UI/贴图，记录仍保存在 `cleanup-manifest.json`。

已同步清理无效 Lua 图集初始化、旧城市背景字段、图集元数据和编辑器导入依赖，重新生成内置 Lua manifest。检查了剩余 75 个精灵映射未变化、两个 UI 路径映射一致、33 个 Lua 模块语法、外部副本上的图集导入、全部归档文件哈希及 Lua manifest。未进入 Unity Play Mode，未重新打包 APK。
