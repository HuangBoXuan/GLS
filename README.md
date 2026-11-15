# LoopSort

2026-11-03: Only runtime artwork and required SDK icons remain in this project. Historical design images, the original UI reference archive and rendered exports have moved outside the project. See [current image inventory](Exports/Image-Usage/README.md) and [archive location](Exports/image-archive.json). Older paths below describe historical exports.

2026-11-02: Current UI exports are refreshed in `Exports/Restored-UI-Models/`, including the enlarged seven-day sign-in screen. Android MAX/AppsFlyer/AdMob inputs from the LoopSort parameter sheet are wired into private product/build configuration; the publisher game ID maps to the existing Lua module ID. API 36 and release gates are configured. This is not a published or release-approved game; the remaining production-service, consent, signing, device and store steps are recorded in [Documentation/ReleaseReadiness/README.md](Documentation/ReleaseReadiness/README.md).

The Unity project is located at `games/LoopSort`, renamed from `games/LoopSort0`. The earlier prototype has been removed from the workspace; its UI artwork, native layouts and visual dependencies are preserved in `loosort糖果ui/`, with an image index and original GUID metadata. Editor menus and future Android build filenames use `LoopSort`. Runtime assembly identities, Android package identity and save keys retain compatibility with previous builds.

A Unity implementation using the supplied Loop Sort **4.6.0 (510)** package as the resource and level-data reference. Gameplay was implemented independently from the earlier prototype.

Gameplay rules, screen construction/navigation, economy, tutorials and theme configuration now run from Lua 5.4.8 through the SDK-provided xLua. Unity rendering, physics, animated views and platform I/O remain C# bridges. See `Documentation/LuaHotUpdate.md` for the migration and publishing workflow. The first Lua-enabled APK must be rebuilt; the update server URL is currently unconfigured.

Open with **Unity 2022.3.62f2**, open `Assets/LoopSort/Scenes/Gameplay.unity`, and press Play. Normal startup opens the home screen; press the green level button to continue playing. The build scene remains Gameplay.

The current mechanism artwork uses the user's volcanic mining theme: mine carts, basalt cave entrances, ore, mining gates, chains and fireproof blankets. Carts and mechanisms now use solid 3D meshes with the same plum iron, copper fittings and lava seams as the earlier illustrations. Scene rendering and native platform prefabs share the model builder; UI illustrations remain in their existing style. See `Documentation/VolcanoMineArt.md` for assets and actual Unity gameplay captures. This art revision has not been built into a new APK.

## Implemented

- Individually identified fragments with adaptive source-style density (40 per color group in level 1), four groups per ordinary truck.
- A click releases the outer eligible blocks up to available belt capacity, keeping complete blocks and screw-connected units whole. Bonded units may cross colors. Clicking during intake can cancel incoming reservations and re-emit the already returned fragments when space permits.
- Unloading reveals fragments only at their scheduled launch time. Later blocks keep their original solid appearance; the current block peels from its outlet-facing layers while its unlaunched volume stays solid.
- Different trucks unload concurrently; the same truck ignores further clicks until every fragment of its current batch has landed on the belt. Overlapping actions can be undone together without leaving pending flights behind.
- Empty or matching-front-color intake; an emptied truck remembers its last emitted color and prioritizes other colors on the belt or being launched. If none are available, it accepts the emitted color again. Same-color full-truck completion, victory and conveyor-deadlock checks remain in effect.
- Track geometry now follows the source BSpline, height suffixes, Subdivide count and open/closed state; open routes use end portals. The earlier invented overpass geometry is removed. See Documentation/NativeMovement.md.
- Track geometry now follows the source BSpline, height suffixes, Subdivide count and open/closed state; open routes use end portals. The earlier invented overpass geometry is removed. See Documentation/NativeMovement.md.
- One static MeshCollider reconstructs the source floor, two solid wall channels and ceiling at ceil(spline length * 4) spans. Rendered rails can remain coarse independently of collision geometry.
- Cargo displays reuse four block models, two solid peel-remainder models and one outline per truck. Ordinary arrivals mark only their receiving truck dirty; visual updates are coalesced in LateUpdate while rule and fragment arrival state update immediately.
- Fixed updates use an active belt-view list and the recovered native target-force and lane/height constraints. A static 256-sample 3D spatial hash replaces the invented moving-neighbor grid and separation logic.
- Verified APK runtime settings are restored: 0.0333-second physics steps, 0.05-second maximum delta, Discrete collisions and one solver iteration. Entry uses the original 20-unit impulse; native mass, damping, sphere radius, bounce and speed limits are restored. Results have not been profiled after editing.
- Retry, next level, pause/resume, settings, level selection, undo, truck shuffle, conveyor expansion, and extra trucks at declared spare positions.
- A consistent toy-workshop skin based on the user's style reference: plum surrounds, cream raised panels, mint conveyors and amber tiled scenery. Three new atlases provide 36 icons/illustrations, 12 scalable panels and four gameplay/home backgrounds. Home includes profile, coins/life, play/level selection and navigation. Local pages include a coin-based booster shop, three home cities, nickname, recorded completions and daily practice. The team page remains unavailable.
- Nine-sliced toy panels/buttons, in-level header, booster stock/cost plates, settings and result dialogs. All 1,299 levels remain selectable, including by directly entered level number.
- Eleven Chinese mechanic cards use newly generated matching illustrations. Unread mechanisms pause the level on entry and appear in sequence; acknowledgement is saved. The in-game help button and home gallery reopen any guide. No gameplay tests or automatic runs were performed for this skin revision.
- Independent local save key `LoopSort0.Player.v1` for level progress, settings and booster inventory. At the user's request, fresh and existing saves receive a one-time balance of 500,000 coins; later spending is preserved. Fresh booster inventory still has two free uses per booster. No production ad, purchase or account SDK is initialized.
- Source cart meshes and sounds, with newly themed UI, cargo palette and matte surface rendering. UI and material art no longer samples the original APK imagery; earlier imported assets are retained as reference inputs.
- Chinese menus, instructions, results and feedback use an embedded Noto Sans SC Medium font; the level selector has a visible button background. Font provenance and its bundled OFL license are recorded in `Documentation/ChineseFont.md`.
- Truck placement uses source cell offsets, height, Pivot alignment and the combined level/spline rotation, without the former avoid-overlap relocation. Camera framing uses source margins and per-level center offsets; transient effects do not shrink the board. Outgoing/incoming fragments retain their existing gate-distance and jump behavior.
- Completion-check pop/rotation, animated key/lock/shutter sequence, original curtain opening clip/morphs, rope collapse, carpet rolling/removal, ice break effects, screw transitions, generator covers and barrier/tag completion effects. Motions pause and reset on undo; see `Documentation/NativeMechanics.md` for recovered data versus reconstructed effects and timing.
- A black outline around exposed cargo selected by the same color and capacity rules as unloading, animated green completion badges with cream checks, and flowing caramel question marks on plum hidden cargo. HUD dialogs draw above completion badges. The new skin supersedes the earlier APK-purple check and blue bitmap cover.
- Belt fragments use the source normal-block scale and radius-0.21 SphereColliders; returning fragments switch to the reconstructed packed-cell size.
- Colored curtains hide and lock their slots until a truck of the required color completes; the original curtain mesh rolls back to reveal cargo.

## Scope of the imported levels

All **1,299 main** levels remain selectable, independent of progress. Imported rules now cover hidden cargo, curtains, barriers, ice, keys/locks, color tags, ropes, screw units, carpets and finite generator queues. **1,297 records** have no remaining unhandled feature markers; this is implementation/import coverage, not a count of validated or solved levels. Levels 450 and 972 retain an unknown block annotation and are marked with gold numbers and an approximation notice. All **30 daily-challenge** records remain data only. See `Documentation/NativeMechanics.md` for native evidence, the error-driven review and remaining reconstruction limits.

Complete visual and behavioral equivalence with the APK has not been established. Group enclosures/animations, exact packed-cell geometry, daily-challenge long trucks, advanced materials, activities, ads, purchasing and server behavior still have gaps. The latest revision was checked through source/native inspection and Unity compilation; no automated gameplay or tests were run, as requested. Original level numbers are preserved.

## Code

- `Assets/StreamingAssets/LoopSort/lua`: authoritative gameplay rules, UI pages, flow, economy and skin modules.
- `Assets/LoopSort/Core`: typed rule/view data and the thin C# interface to Lua state transitions and snapshots.
- `Assets/LoopSort/Runtime`: camera framing, procedural track, source cart reconstruction, physical transport and transfer animations, HUD, save and audio.
- `Assets/LoopSort/Editor`: import settings, scene setup, offscreen rendering and data audit.
- `Assets/LoopSort/Tests`: rule regression tests and isolated PlayMode flow tests.
- `Tools/import_reference.py`: reproducible import from this machine's 4.6.0 extraction. Does not read the old gameplay project. Output provenance is in `Documentation/ReferenceImport.json`.
- `Tools/build_lua_content.py` and `Assets/LoopSort/Editor/HotUpdateBuilder.cs`: Lua/data/image update packaging, optional native AssetBundles, APK Lua catalog and IL2CPP binding preparation.
- `Assets/LoopSort/Resources/LoopSort/Theme`: the active icon, panel and environment atlases. `Tools/import_theme.py` reads alpha bounds into sprite metadata without editing images. Skin scope and generation prompts are in `Documentation/ArtTheme.md` and `Documentation/ArtThemeGeneration.json`.

Package manifests and the package lock are included so the new project's dependencies can be restored after checkout. Unity caches and build intermediates stay ignored.

For validation and known limitations see `Documentation/Implementation.md`.

The final static review, controller/content-loader structure and cleanup restrictions are recorded in `Documentation/CodeReview.md`.

The first Lua-enabled Android APK was built on 2026-10-28 with IL2CPP/ARM64: `Builds/Android/LoopSort0-Lua-arm64.apk`. Build metadata is beside it in `build-info.json`. No installation, gameplay or tests were run. The hot-update server URL is still unconfigured; setup and publishing steps are in `Documentation/LuaHotUpdate.md`.

The recovered APK movement bodies now drive the native-style rigidbody implementation. See `Documentation/NativeMovement.md` for native addresses, equations, local defaults and boundaries: engine-version differences, customized transfer rules and remote/special systems still prevent claiming complete APK equivalence. Earlier neighbor-separation and staggered-guidance experiments are removed.


Current product integration and the exact temporary-to-production replacement checklist are in [Documentation/ProductIntegration.md](Documentation/ProductIntegration.md). MAX/AppsFlyer are product-owned and disabled by the development template. The latest UI module is `local_20261029_122117_035_5a71831d`; no online registration or publication is claimed.


## 2026-11-06 UI、道具和广告更新

- 方块材质增加柔和高光，保留实例合批；机制说明标题下移，新机制说明不再叠加底部机制提示。
- 撤回与加车使用现有 click 音效；转换道具通过遮罩选车，空白或不可用车位取消且不扣库存，成功后方块转动并重新排列。
- 插屏改为下一关入场触发：第 17 关完成后，从第 18 关入场起，每次完成主线关卡产生一次机会。当局为新机制关卡，或完成过复活/道具激励广告，则消费并跳过下一关的机会，再下一关恢复。无填充不在局中补播，结算页和失败页不播插屏。
- BGM 替换为原创 Gem Trail，108 BPM、32 小节、约 71 秒无缝循环；连续和弦底层与延长尾音改善短音断续感。源配方保留在 `Tools/compose_bgm.py`。
- 本次验证记录位于 `Exports/Validation/Release-20261106`。GM 安装包使用 `LuaAndroidBuild.BuildGm(output)` 构建；普通 BuildMenu 保持非 Development 构建。

广告依赖使用 JDK 17、Gradle 8.11.1、AGP 8.10.1 打包，保留广告 SDK 版本。构建入口自动先导出，再调用兼容工具链，避免改动 Unity 内置安装。默认工具链在 `../../../BuildTemp/LoopSort-ad-toolchain`，也可通过 `GEMLOOPSORT_ANDROID_TOOLCHAIN` 指定。参考 Google 官方 Android 构建说明：https://developers.google.com/admob/unity/android 。
