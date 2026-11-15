# Fragment density and colored curtains

Reference: the supplied Loop Sort 4.6.0 XAPK, SHA-256 `444456c6dc8948c1a4c7cec19c3a18a94883b0265f21af8f5d37e7467d0430e7`. The observations below come from its ARM64 `libil2cpp.so`, source tables and serialized hierarchy, not from the sibling LoopSort project.

## Fragment density

`LevelBuildSystem.SetCarrierBlockArgs` at VA `0x248A6D8` builds the XY candidate planes `(3,3), (4,3), (4,4), (5,4), (5,5), (6,5), (6,6), (7,6), (7,7)`. The single-color depth multiplier is 2; four groups give total depth 8. It chooses a candidate by comparing its group count to spline length divided by slot count (with an additional LoseConfig adjustment), multiplied by 8.

The new `FragmentLayout` uses this candidate family and a control-polygon length estimate. It does not recover Dreamteck's exact spline length, remote cohort configuration or the LoseConfig adjustment. Thus it restores the variable density behavior without claiming exact fragment counts for every APK level. Level 1 currently selects 5 × 4 × 2 = 40 fragments per color group.

## Curtain unlock

`HiddenCarrierSystem.HandleComplete` at VA `0x23EBBA4` handles `CarrierCompleteMessage`. It compares the completed carrier's color (`GetNextColorType`, VA `0x23E3738`) with the hidden carrier's required color and invokes `Reveal` (`0x23EB808`) on a match. This supports opening matching curtains on full truck completion, rather than on partial intake.

The importer reads source `H_color` features, for example level 20's `C-H_Y`, `D-H_B`, `E-H_B`. These become yellow, blue and blue curtain requirements. Closed carriers hide their cargo and reject interaction/transfer; completion opens matching carriers. Undo restores the previous closed/open state.

The view uses source mesh 395 (Curtain-v7), including the hierarchy's combined position `(0, .181, -2.7)` and X rotation −90 degrees. The former scale-only reveal has been superseded: source blend-shape deltas and the streamed curves of `CurtainOpening-2` are imported and sampled directly. The native clip starts at scale `(103,106,100)` and runs for 1.583333 seconds, with two morph channels and a renderer-enable curve.

## Runtime check

On the unmodified source level 20 layout, add one declared spare truck and release the yellow groups from A and B. The spare collects four yellow groups, completes, and opens C's yellow curtain. D and E remain covered in blue. This path is covered by a PlayMode regression, including undo back to the covered state, in the retained `Validation/fragment-badge-playmode-results.json` report.

## Carrier completion artwork

The recovered hierarchy's `Carrier/Pivot/View/Checkmark` GameObject (path ID 3727) has Image component 11635. Its recovered serialized tree references sprite path ID **687**, `checkerPurple`, with white tint, preserved aspect and no custom material. This is a standalone purple check with transparent surroundings. The earlier implementation incorrectly used `day-base-played`, a green daily-reward button. The importer now maps `check` to `checkerPurple`; the original export's hash is recorded in ReferenceImport.json.

## Movement body recovery

ARM64 inspection has now recovered the shared Block jump, BlockPhysicsSystem bake/search/constraint/force path, physical entry parameters and runtime time-step setters. The implementation and remaining boundaries are documented in [NativeMovement.md](NativeMovement.md). This supersedes earlier statements that only physics-related component metadata had been inspected.

## Additional carrier and block mechanics — 2026-10-27

The imported catalog now retains barrier, ice dependencies, colored locks, keys, color tags, rope groups, screw bonds and carpet groups as executable rule data. Parsing normalizes feature aliases/case and splits newline-combined block features, such as `R_H\nSticky`. The source `raw` annotations remain available. `BL_G` is still unrecognized; it is not silently treated as a glass block.

Implemented behavior:

- Barrier carriers receive matching cargo but cannot emit it.
- Frozen carriers reject interaction and transfers until **any** declared dependency completes. Native `IceCarrier.MatchesRequiredCarrierType` (0x23EC080) calls the list membership operation; `IceCarrierSystem.HandleComplete` (0x23EC994) calls `BreakDelay` immediately on a match, without an all-dependencies counter.
- Revealing a key block collects its key color and unlocks all matching locks. Key color is independent of cargo color (`Y_K_G` carries a green key on yellow cargo). `KeyBlockGroupSystem.OnBlockNextTransferUpdate` / `TryCollectKey` at 0x23CD19C / 0x23CD1B0 establish collection on becoming the next outgoing block, rather than waiting for a completed truck.
- Color-tag carriers reject other colors even when empty. Normal occupied-carrier matching still applies.
- Rope-connected carriers can receive but cannot emit. `ConnectedCarrierGroupSystem.OnCarrierComplete` (0x23EA780) finds the completed carrier's group and invokes `Complete` (0x23E9678); completion of any member unlocks the shared rope.
- A screw binds its source block to the preceding/deeper block. Selection follows the whole bond chain, which may include different colors. Capacity is reserved for the entire selected chain before changing state. Mixed-color release now updates occupancy separately for each color. Bonds are consumed on the accepted emission; undo restores them.
- All carts in a carpet group begin covered. `CarpetCarrierGroupV2.Initialize` (0x23DF878) disables interaction, entering and exiting for every member. `CarpetCarrierGroupSystem.OnCarrierComplete` (0x23DF07C) advances **every group** on a truck completion. `RevealNext` (0x23E05E8) enables and removes the last member of the sorted list. This implementation sorts by the source carts' local right axis. Complex group orientation/animation remains an approximation.

Every new lock, dependency and bond is copied into the existing stable undo snapshot. Hidden groups remain covered inside inaccessible carriers. Exposing a partial incoming block does not reveal or collect a key from the preceding buried block. Adding an ordinary spare cart creates empty feature lists.

Source meshes and textures for the barrier, ice, flag, screw, key, padlock, knot and carpet are selected from the recovered hierarchy. `feature-visuals.json` preserves their ancestor transforms and renderer/material bindings. Materials retain source base texture, UV tiling and tint through Standard-shader adaptation; this does not reproduce the original TCP2 shader. Reveal/break/key-flight animations are not recovered.

### Group artwork correction

`CarrierGroupFeatureView` now builds the complete source group models around the resolved carrier pivots. Lock groups use the original left/right frame, center strips, roof/front shutter panels and one large central colored padlock, including its separate dark keyhole and purple attachment. Width and forward offsets come from the recovered `LockCarrierGroup` fields and native `Initialize` (0x23ECCA8). The individual small padlocks are removed. Cargo keys are enlarged to 1.18 world units and outlined in black.

Rope groups now use the original RopeMid/RopeL/RopeR multi-wrap geometry and knot, stretched between the endpoint carriers using the original 1.4 width offset (`ConnectedCarrierGroup.Initialize`, 0x23E8E2C). Straight center-to-center lines and repeated individual knots are removed. Carpet groups now retain the original continuous inner pattern, distinct left border, gold divider elements and right roll, using width offset 2.1 and forward/back offsets 1.7/2.5 (`CarpetCarrierGroupV2.CreateModel`, 0x23DFCF8; `UpdateModel`, 0x23E0734). Inner artwork tiles horizontally by covered-carrier count; rope texture tiles along the other UV axis. The roll and covered span move as the last covered member becomes accessible. Color-tag trucks tint all body/deck surfaces in the required color while retaining their wheels.

OBJ imports split some source material groups into separate mesh children. `GameAssets.CompleteMesh` retains those children and their submeshes for features and trucks; previously only the first child was displayed. Undo immediately restores group covers. The later layout/animation revision below supersedes the former renderer-bound framing and scale-only shutter retraction. No gameplay runs or visual comparison tests were performed for these corrections.

Catalog import now reports 1,297 main records without unhandled feature markers. This is a parser/implementation coverage count, **not a count of solved or validated levels**. Levels 450 and 972 retain the unrecognized `block:G` annotation and an explicit approximation notice/gold numbers. Daily-challenge long trucks and remote-config-only features remain outside this change.

No tests, play-mode runs, automated game input, APK builds or visual verification were performed for this revision, as requested. Exact APK equivalence and Android performance have not been established.

## Error report and complete static mechanics review

The user's Unity console reported `CreateImpl is not allowed ... from a MonoBehaviour constructor`, followed by repeated `NullReferenceException` stacks in `CarrierFeatureView.Tint` / `Refresh`. Its `MaterialPropertyBlock` field initializer called a native Unity API during construction. The block is now created lazily inside `Tint`, on the normal component execution path. Existing console records are retained.

The following corrections come from further source-level/native inspection, without automated gameplay:

- **Screw boundaries:** `Carrier.FindNextTransferBlocks` (0x23E392C), especially 0x23E3A30–0x23E3B18, distinguishes marked and unmarked groups even when their colors match. An ordinary outgoing run stops before a new marked unit. A bonded unit can cross colors; after crossing, selection follows the new inner color. A separate `sticky` marker preserves a marked first group that has no deeper partner. Shuffle rotates complete connected units instead of rejecting every screw-bearing cart; this unit-preserving shuffle is a reconstruction, not a recovered native shuffle algorithm.
- **Receiver preference:** `BlockTransferSystem.HasBetterCarrier` (0x23C8754) searches all carriers, not just carriers whose gates are nearby. Matching nonfull tag carriers and already-uniform matching barrier carriers now keep their color available until it reaches their gate. Rope membership alone does not create a special preference. `RockCarrier.IsBetterCarrier` (0x23F2690) supplies the uniform-cargo condition. Preference results are cached per color and invalidated when reservations, outgoing batches, locks or cargo order change. Remote-config-dependent general uniform-truck preferences are still not reproduced.
- **Key collection:** `KeyBlockGroupSystem.OnBlockNextTransferUpdate` (0x23CD19C) checks `IsNextTransfer`, not an absolute final slot. Collection now scans the full eligible outgoing prefix, including matching groups already eligible behind the front group. Partially receiving groups remain excluded. Undo restores consumed key markers and colored locks.
- **Carpet direction:** `CarpetCarrierGroupV2.Initialize` (0x23DF878) selects local right or its negation from the angle between the cart's forward and world forward. Its comparator (0x23E12FC) sorts the projected positions. The importer now applies the direction reversal for back-facing carts before assigning reveal order.
- **Generator:** `TunnelCarrier.ProcessFeature` (0x245B548, store at 0x245B5C4) sets `_customMaxBlockCount` to `Conveyor.GroupBlockCount`. `Carrier.IsFull` (0x23E362C) and `FindNextTransferBlocks` honor that one-group limit; `GetCompleteBlockCount` (0x23E3700) retains the ordinary completion target. `TunnelCarrierSystem.SetBlockPositions` maps queued fragments modulo the group count, and `EnableNextTransferBlocks` controls their visibility. The implementation retains every source fragment in a finite queue, exposes/emits one group, then exposes the next. It can temporarily accept at most one group after depletion and does not complete at that buffer size. It never invents additional blocks. Source spawner geometry replaces the ordinary truck body, and the source transfer point and collider bounds are imported. The displayed block is centered inside those bounds using this project's packed block dimensions; exact native cell coordinates, cover motions and instant/native transfer-motion differences remain approximations.

Curtain color-completion matching, ice's any-declared-dependency rule, rope's member-completion trigger, color-tag filtering, hidden reveal and the new state fields' snapshot copies were reviewed alongside these changes. This does not establish complete behavioral or visual parity. No tests or automatic game runs were performed; only editor compilation/error diagnostics are used for the reported script fault.

## Whole-block intake correction

Following the user's clarification, the first fragment's gate contact reserves a destination and capacity for its complete source block, without changing the other fragments' movement. Stable fragment IDs identify the original block (`id / GroupSize`); identical colors from different blocks remain separate units. `PieceState.receiveTarget` stores the assigned truck, and `CarrierState.incoming` reserves space for the members still on the belt or still releasing. Those fragments retain their locations, velocities, emission order and conveyor occupancy until each reaches the assigned truck's normal gate range and passes its existing cooldown. `CanReceivePiece` rejects every other truck for that block, even if another matching truck becomes available or has higher priority.

Only the nearby fragment starts the existing intake jump. Remote fragments continue normal physics on the conveyor; unlaunched fragments continue the original emission queue. No group suction, remote freezing or group intake delay remains. Capacity checks include the outstanding reservations, and failure evaluation recognizes already-owned fragments even when a truck has no unreserved capacity. Re-emitting an incoming block clears its old destination once no members remain in that truck; otherwise its remaining members retain their shared destination. Piece targets and reservation lists are copied by undo snapshots. This behavior implements the user's stated rule rather than claiming a recovered native transfer body. No tests or automatic gameplay were run for this correction.

## Mechanism motion and original placement revision

The previous avoid-overlap search in `BoardPresentation.ResolveCarrier` is removed. It could push a truck by tens of world units and then shrink the entire board to fit. Source `LevelBuildSystem.CreateCarriers` (0x2489898), `ParseWorldOffset` (0x2489DA0) and `ApplyCameraRotation` (0x248A638) instead establish these rules:

- A cell's third token line is a local XYZ offset rotated by that truck's yaw before adding the grid position. The main catalog contains 1,186 such offsets; they were previously ignored.
- Carrier height is offset by `-(ConveyorHeight - 0.1)`, which is -1 with the recovered 1.1 height. The root then subtracts its rotated source Pivot offset. There is no subsequent spacing/avoidance relocation.
- The level and spline camera rotations are added and applied to the complete board. The importer now transforms points, carriers and spares consistently, including truck yaw. Previously the spline's rotation was omitted.
- `CameraBoundsSystem.CalculateCameraBounds` / `SetGroupFramingSettings` (0x2482A18 / 0x24837EC), CameraConfig and the source camera establish pitch 70, framing 0.99, top/bottom offsets 3 and per-spline normalized center offsets. `CreateTarget` (0x2483928) adds members with weight 1 and radius 1; that camera-space radius is retained. Framing uses the spline and carrier root/pivot positions, then includes static physical slot/enclosure edges only if they exceed that source envelope. Flying keys and particles do not change the camera. Original relative coordinates, sizes and rotations are preserved; exact Cinemachine target-group implementation/pixel parity across devices is not claimed.

`Tools/import_animations.py` reads only the same-build `data.unity3d`. It preserves the original vertices, indices, UVs and blend-shape position deltas for meshes 393 (top shutter), 427 (front shutter, seven channels) and 395 (curtain, two channels). `SourceMorphView` creates per-renderer blend weights on cached meshes, avoiding the vertex-index reordering of OBJ imports. The native curtain clips 557/558 retain their streamed cubic coefficients, constant values and duration. Source provenance is recorded in `AnimationImport.json`.

Implemented motion paths:

- Completion check: native `Carrier.ApplyCheckmarkMotion` (0x23E1D04), 0.5 seconds, InOutBack scale 2 to 1 and Z angle 10 to 0. The truck stays in its slot and the final check remains visible.
- Key/lock: `LockCarrierGroup.<Unlock>d__17.MoveNext` (0x23EE6F8) supplies the sequence and constants: 0.7-second key travel, 0.2-second 90-degree turn after a 0.1 delay, key/lock scale-down stages of 0.1 seconds each, front morph channels from 6 to 0 at 0.04 seconds each, top morph over 0.45 seconds, then enclosure Y collapse over 0.25 seconds. The key travel arc is reconstructed; the door morph deltas are original. Rule-level key collection records an origin event so a newly exposed key can animate even though its logical marker is consumed immediately.
- Curtain: original opening clip curves and mesh deltas replace the shrinking-cloth approximation.
- Rope: native `ConnectedCarrierGroup.Complete` (0x23E9678) supplies the 0.25-second Y collapse. Undo restores the complete wrapped model.
- Carpet: retained source endpoints, divider elements and native 0.5-second span/roll movement; the final roll now retracts and disappears instead of vanishing at the start of the last reveal. Position interpolation and removal scale remain reconstructed.
- Barrier and tag completion: barrier model moves down to source Y -0.2 over 0.5 seconds; tag shrinks with InBack over 0.5 seconds after a 0.25-second delay, from native `RockCarrier.ApplyCompleteMotion` and `TagCarrier.ApplyCompleteMotion`.
- Ice: a short crack/shake transition and cosmetic ice-chip burst replace immediate disappearance. Particles and shake are reconstructed visual effects, not the source particle system.
- Screws: native-style 0.325-second, five-cycle selection jiggle plus a reconstructed unscrew/lift transition when a bonded unit releases.
- Generator: source cover transforms animate scale X 1 to 0 and back over two 0.25-second stages. The next block is concealed during the first half; queue inventory remains finite and unchanged.
- Hidden cargo: the later reveal revision replaces the flip with a reconstructed 0.7-second noise dissolve of the question-mark cover. Whole cargo, peel remainders and emitted fragments share their original block's reveal timeline, including immediate release. See `CodeReview.md`.

All motion follows the existing pause flag. Undo cancels running tracks, removes transient key/particle/screw objects and restores the static appearance from rule snapshots without replaying unlock events. A logically unlocked truck cannot transfer through a door/curtain still opening; only the relevant opening animation blocks that truck. Results wait for outstanding cosmetic motion before showing the dialog. None of these changes modifies the per-fragment gate-distance requirement or causes remote fragments to be sucked off the belt. This revision uses editor compilation/source inspection only; no tests, automated gameplay or visual comparison runs were performed.

## 2026-10-30：原 APK 入车路径的静态对比

本节读取的是本机 4.6.0 原 APK 的 ARM64 方法体和同版 IL2CPP 元数据，不是根据方法名猜测。没有运行 APK、游戏、测试或性能采样。以下结论按默认 BlockTransferConfig.FirstAvailable=false（构造器未赋值）的分支描述；线上 RemoteConfig 可覆盖该值，不能声称已知原版当前服务器配置。

### 原生证据

| 原包方法 / RVA | 方法体中的行为 |
| --- | --- |
| BlockTriggerSystem.AddCarrierTriggers / 0x23CA208 | 投影货车 TransferProjectPoint 至样条，再按 CarrierTriggerOffset 沿轨道偏移；创建 BoxCollider，设 isTrigger，按样条位置与方向摆放，使用 CarrierTriggerSize。不是以投影点为中心的圆形半径检测。 |
| BlockTransferSystem.HandleCarrierTrigger / 0x23C7FFC | 先检查游戏状态、来源车和已绑定组目标；有效目标仅允许目标车接收，目标满/不活跃/颜色不兼容时移除绑定；再查车满、禁止入车、颜色兼容、颜色拒收计时、优选车、机制规则与车正在放出等条件，接受后调用 Carrier.AddBlock。已有组目标会跳过重新竞争优选车，但不会跳过后续基础门槛。 |
| Block.IsCompatibleWith / 0x23BDAF0 | 空车允许颜色；非空车比较待入车方块颜色与 GetNextColorType。 |
| Carrier.GetNextColorType / 0x23E3738 | 读取 _blocks 最后一个方块的颜色，不是任意一个内部颜色。 |
| BlockTransferSystem.HasBetterCarrier / 0x23C8754 | 扫描车集合；优先机制处理器 IsBetterCarrier。FirstAvailable=false 时，还比较未满、可入车、全车同色且颜色匹配的普通货车，选择 _blocks.Count 更大者；相同时保留 referenceCarrier。比较的是原始方块数，未使用装满比例或空间距离。 |
| Carrier.CanTransferBlock / 0x23E4024、IsBetterCarrier / 0x23E4280 | 分别转发给 IBlockTransferHandler 集合：接收条件逐项通过；优选条件任一处理器满足即可。 |
| TagCarrier / 0x23F2A84、0x23F2AA4 | 指定颜色约束与同色优选。 |
| RockCarrier.IsBetterCarrier / 0x23F2690 | 入车未锁、未满、已有方块全同色且颜色匹配时优选。 |
| LongCarrier / 0x23F2120、0x23F2150 | 目标颜色存在时限制接收并优选该颜色。 |
| Carrier.AddRejectedColorType / 0x23E28D0 | 使用 CarrierConfig.RejectedColorSeconds 创建 UniTimer；IsColorRejected / 0x23E45B8 查询计时器是否运行。不是“只要轨道上还有其他颜色就无限期拒收”。数值来自序列化配置，本轮未把猜测的秒数写入代码。 |
| BlockTransferSystem.TransferBlocks.MoveNext / 0x23C8AF4 | 在本次释放序列里按组大小分配递增的 BlockGroup.Id，并在重新释放时 RemoveGroupTargets；不是终身固定的 floor(piece.id / groupSize)。 |
| Carrier.IsFull / 0x23E362C | 使用 customMaxBlockCount（例如生成器）或正常完成容量，与已接受的 _blocks.Count 比较。入车逻辑接受之后就占位，不等飞行动画结束才占用。 |

### 当前实现的差异与风险

1. C# FixedUpdate 与平台 Lua physics_step 都使用半径 1.15 的水平圆形范围、独立高度容差；货车 gate 是最近轨道点。缺少原包触发盒的位置偏移和方向，因此可能提前吸入或跨相邻轨道吸入。
2. rule_engine.native_priority 只区分彩色/目标车与障碍车，没有普通同色车之间的原始方块数比较；两个相同优先级候选通常由车索引先后决定。不能据此声称与原包优选车算法相同。
3. receiveTarget 根据固定碎块 id 分组，首次绑定预留一个完整 group_size；原包按本次释放重新分组。重排、部分入车后再次释放等情形下，两种组身份不再必然一致。
4. 当前 can_receive_piece 的已绑定分支只检查 Accessible、packed 和 releasing，没有重新检查颜色、容量、拒收等条件。表现层又只遍历 receiveTarget 这辆车；失效绑定缺少原包的清除重选路径，存在碎块长期绕圈的风险。
5. 当前对 lastReleasedColor 的过滤检查轨道上任何其他颜色是否存在。它是用户定制“优先接收其他颜色”的实现，不是原包的计时拒收；若其他颜色一直存在但不能进入该车，该颜色可能被长期排除。
6. 当前 LastCarrier + Travelled 接近一整圈才解除限制，与原包 BlockTransferCarrier 来源标记和颜色计时不等价。标记在其他原生系统中的完整清除时机尚未完全追踪，不能把“一圈”称为解包得到的参数。
7. 点击取消入车时，当前仅在该固定 id 组已无成员留车时清除目标；原包在重新释放流程中清除车的旧组目标。这一差异可能与同组剩余碎块被旧目标牵制有关。用户定制“已回部分再出去、优先其他颜色”必须独立保留并原子更新预留。

### 建议的选择流程

先处理真正进入本车触发区域的碎块。若其本次释放组已有有效目标，只能进入该目标，且仍需通过车状态/颜色/容量/拒收/机关门槛。目标失效则释放旧预留和绑定，再重新选择。无绑定时，在允许的车集合中先处理机制优选；FirstAvailable=false 时优先全车同色、颜色匹配且原始方块数更多的货车，同数保留当前触发车。不在目标车的触发区域时继续正常跑道运动，不能远距离吸入。接受时原子占位并绑定组，只有该碎块启动飞行动画，其余成员继续物理运动；动画落位后更新完成计数，最后一块到位再触发满车表现。

为保留用户“一个大块归同一辆车”的要求，可在原生逐碎块占位之上增加组预留，但应使用本次释放组的剩余成员数，并在取消回车、重发、撤回、切关时同步释放；这是定制规则，不应冒称原包已有完全相同的预留算法。本轮仅分析该接收规则，没有直接改变接收算法，等待用户选择按原包基线修正的范围。

### 同轮已经修改的表现与 GM

地毯表面/卷面统一为传送门的 #F46B2D，生成带波峰的细分几何；按覆盖车数缓存，不逐帧变形。普通车接入已有原生形变关门，关门约 0.95 秒后再播放原版风格的 0.5 秒打勾；原包 SetCompleteTransferDelay.MoveNext / 0x23E70B4 是等待全部 AddBlock 后在同一阶段启动打勾与关门，本次严格先后次序是用户要求的调整。胜利界面等待这段完成表现。

平台版与普通独立版加入右侧 GM 入口：四种道具直接设为 99、11 种图鉴机制首次主关卡快捷按钮、任意已存在主关卡的数字输入跳转。机制快捷复用 guide_levels.lua（4/7/20/60/81/101/150/201/250/300/334），不在图鉴正文恢复首次出现文本。领取不伪造任务或购买事件。

只修改源码与说明，并重新生成内置 Lua 文件哈希清单；未运行游戏、测试、语法/编译验证或构建 APK。本次没有生成临时分析文件。


## 2026-11-06: full-catalog layout restoration against the supplied APK

Reference: `LoopSort-4.6.0-LevelMenu-v3-arm64.apk`. Its `assets/bin/Data/data.unity3d` SHA-256 is `80fa374e5de34e9a8a298797779ea2779232b781bb8c6d9c3787ee25b0506348`, identical to the same-build extraction used by the importer. A fresh levels-only import to an external temporary folder matched all 1,299 main levels' authored points, carrier/spare coordinates, rotations, camera offsets, closure and subdivision settings. This confirms the source records; it does not by itself validate the importer's visual interpretation.

Removed the later `TrackVisualClearance` row translations, `RopeClearance.Apply` call and level-specific track widening. Both initial and added carriers now use the original mouth position minus the rotated native pivot. No cart root is resized to create clearance. Ordinary cart bodies/wheels were fitted to the original mesh's local XZ silhouette: native width approximately 2.2206 and Z range -2.33273 to 2.47829. Cargo meshes and authored cargo centers remain unchanged. The rear lantern is folded inward over the tail so it no longer enlarges the slot's longitudinal footprint. Standing caves use the original portal's approximate width/height envelope while retaining the volcanic arch art.

Restored source pitch 70, framing 0.99, radius 1 and top/bottom padding 3. The portrait usable band is 0.16 to 0.94. A cached static envelope provides minimal additional camera distance only where real cart/mechanism geometry would be clipped by the screen or UI; transient animations do not reframe the board. Camera fitting never changes world positions. Cinemachine pixel parity across devices is not claimed.

The native `ConnectedCarrierGroup.Initialize` comparison at 0x23E9CDC sorts pivot positions by X+Z. Its initialization at 0x23E8E2C chooses its two visual ends using the first sorted pivot's right axis. Dependency members can occupy a different row: main levels 548, 775, 1061 and 1275 contain mixed orientations. Using the first spatially enumerated member's axis and fitting all members had incorrectly made the illustrated chain span multiple rows. Rope placement now uses native end selection and fits its artwork to those two endpoint carts; dependency membership and gameplay rules remain unchanged.

`Tools/LoopSort/Validate all APK level layouts` runs in the Editor only. It loads each main level, adds each available spare in sequence, compares root positions/yaw/scale to authored poses, and checks visible cart framing at 900x1600 and 750x1650. It restores the prior in-memory progress and does not save test progress. Detailed results are in `Exports/Validation/NativeLayout.json`. Geometry checks and sampled screenshots are not a full playthrough.
