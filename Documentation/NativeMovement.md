# APK 4.6.0 block movement recovery

Source: the supplied Loop Sort 4.6.0 ARM64 `libil2cpp.so`, same-build IL2CPP metadata and recovered Unity resources in `D:/MiniProject/analysis/LoopSort-4.6.0-20261026`. XAPK SHA-256: `444456c6dc8948c1a4c7cec19c3a18a94883b0265f21af8f5d37e7467d0430e7`.

The movement bodies were inspected as ARM64 instructions, including direct Unity API calls, immediate enum arguments, floating-point literals and the imported `sinf` call. This goes beyond the empty C# method stubs in the IL2CPP dump. Temporary disassembly and analysis dependencies are confined to `Temp/apk-motion-reader-20261027`; automatic approval rejected deletion with `blocked by policy`, including deletion limited to that verified directory, so cleanup could not be completed. No post-edit Unity compilation, tests, visual comparison or profiling was requested or run.

## Recovered methods

| Method | Native address | Observed behavior |
| --- | --- | --- |
| `Block.ApplyMoveToCarrierMotion` | `0x23BCD48` | Cancels the previous motion, reads initial pose and invokes the shared jump. |
| `Block.ApplyJumpMotion` | `0x23BCE24` | Stores endpoints and rotations, creates a 0-to-1 LitMotion animation lasting `0.7 * MotionDurationMultiplier`, with ease value 3 (`InOutSine`). |
| `Block.ApplyMoveToSlotMotion` | `0x23BD03C` | Uses the same jump implementation as intake. |
| `Block.UpdateJumpMotion` | `0x23BD118` | Computes the jump arc and quaternion lerp, then calls `Transform.SetLocalPositionAndRotation`. No Rigidbody movement calls occur in this update. |
| `Carrier.<AddBlock>d__116.MoveNext` | Calls at `0x23E4CBC`, `0x23E4CDC`, `0x23E4CFC` | Intake sets kinematic=true, collisions=false and interpolation=None before animation. |
| `ConveyorSlot.<AddBlock>d__17.MoveNext` | Jump call at `0x2461C68` | Computes alternating lateral landing offsets, detaches the block from its parent, sets scale, then invokes the jump; physical activation follows arrival. |
| `BlockPhysicsSystem.BuildMessages` | `0x23C44B8` | Calls `Time.set_fixedDeltaTime(0.0333)` at `0x23C45AC`, and `Time.set_maximumDeltaTime(0.05)` at `0x23C45BC`. These are runtime overrides, not just unused constants. |
| `BlockPhysicsSystem.OnConveyorSlotAddBlock` | `0x23C47D8` | Sets mass, damping, maximum velocity, kinematic=false, collisions=true, interpolation=1 and collision detection=0; adds the initial impulse. |
| `BlockPhysicsSystem.GetTargetForcePosition` | `0x23C49E8` | Searches the static 3D spatial hash, expanding from radius 1 to radius 2, then falls back to all baked positions if no bucket contains samples. Returns the corresponding baked target. |
| `BlockPhysicsSystem.OnConveyorSlotRemoveBlock` | `0x23C4F00` | Sets kinematic=true and interpolation=None. |
| `BlockPhysicsSystem.BakeSpline` | `0x23C4F54` | Bakes positions, up/forward vectors and one-unit-ahead targets, then builds a spatial hash once. |
| `BlockPhysicsSystem.OnUpdate` | `0x23C5378` | Constrains lateral/vertical coordinates and applies force toward the baked target; no moving-block neighbor grid or explicit separation loop was found in this body. |

The generated `dump.cs` remains the authority for complete async method start addresses. The listed call addresses identify the actual observed state transitions independently of async state-machine prologues.

## Local default parameters

| Setting | Recovered value |
| --- | --- |
| Fixed timestep / maximum delta | 0.0333 / 0.05 seconds, set by `BuildMessages` |
| Solver / velocity solver iterations | 1 / 1 in the source PhysicsManager; no direct runtime override was found in the inspected game code |
| Interpolation / collision detection | Interpolate / Discrete on belt entry, confirmed by immediate values 1 / 0 |
| Mass / linear drag / angular drag | 4 / 0.1 / 10, from BlockConfig and entry setters |
| Maximum linear / angular speed | 20 from BlockConfig / 50 from PhysicsManager |
| Gravity / rotation constraints | Enabled / none in block Rigidbody resources |
| SphereCollider radius | 0.21 in block resources |
| Block physics material | Static/dynamic friction 0; bounciness 0.6; both combine modes Maximum |
| Track physics material | Default material in the source collider prefab |
| Contact offset / sleep threshold / maximum depenetration speed | 0.01 / 0.005 / 10 |
| Collision callbacks / transform auto-sync / reuse callbacks | Disabled / disabled / enabled |
| Spline type / sample rate | Uniform cubic B-spline / 8 (7 intervals per control segment) |
| Collider extrusion count | ceil(spline length * 4), from SplineMeshAutoCount |
| Collision channels | Floor x ±0.65, y -0.1..0; walls x ±0.61..±0.71, y 0..0.8; ceiling x ±0.65, y 0.75..0.85 |
| Baked samples / cell size / search radius | 256 / 0.33 / 2 |
| Target travel offset / force multiplier / initial impulse | 1 / 12 / 20 |
| Base conveyor speed | 5 in ConveyorConfig; local PaceConfig speed multiplier 1.1 gives 5.5 |
| Local animation multiplier | 0.5 in `PaceConfig` constructor, giving duration 0.35 seconds |
| Lateral / height clamp | -0.4 to +0.4 / 0 to 0.55 relative to a baked frame |
| Hard recovery threshold | Absolute lateral displacement >0.8 or height >1.1 |

The original asset TimeManager has a different initial timestep; gameplay initialization overrides it. This is why the native setter calls, rather than just asset defaults or class constants, determine the restored timestep.

## Jump formula

Let `u` be elapsed time divided by `0.7 * multiplier`, and `t = (1 - cos(pi * clamp01(u))) / 2`.

```text
outQuint(t) = 1 - (1 - t)^5
position = lerp(from, to, t)
         + up * sin(pi*t) * outQuint(t) * (2*multiplier*pi)
rotation = Quaternion.Lerp(fromRotation, toRotation, t)
```

The prior fixed outlet-feed stage and its separate low arc are removed. Slot landing uses local up +0.25 and alternating local right -0.25/+0.25, from `SlotElementOffset = 0.5`. The native block detaches to the world before this local-transform animation, so the port's level root serves as the identity parent. Flight disables physical collision response, and belt entry switches back to dynamic simulation and adds `normalize(target-position) * 20` with ForceMode.Impulse.

For the ordinary transfer path, per-fragment delay is slot collision distance divided by unscaled conveyor speed and multiplied by the animation-duration multiplier. The local defaults give `0.25 / 5.5 * 0.5` seconds. Remote sand-flow and cancel-intake branches can alter the original scheduling; those branches are not reproduced as a general replacement for the project's customized transfer rules.

## Belt step

Project the displacement from the nearest baked position onto its forward, up and `cross(forward,up)` axes. Clamp the side and height coordinates to the ranges above, preserving the forward component. For ordinary corrections, remove velocity components only on axes that were clamped. For hard recovery, clear linear and angular velocity and skip force application for that step. Otherwise apply:

```text
force = normalize(bakedTarget - originalBodyPosition) * (12 * conveyorSpeed)
ForceMode = Force
```

The port removes the previously invented desired-velocity feedback, moving-neighbor separation and two-group steering cache. All active belt bodies use this simpler native step at the restored physics rate. Flights update Transform each animation frame, while Rigidbody mode/pose initialization happens only at transitions; the old per-animation-frame Rigidbody property writes are removed.

## Source spline and collision mesh

`SplineComputer` resource 12273 uses BSpline type 1, sampleRate 8, default sampling and world-up control-point normals. `LevelBuildSystem.CreateSplinePoints` (`0x248940C`) parses the ordered grid tokens and optional height suffix; `CreateSplinePoint` (`0x24897CC`) adds that height to the conveyor's 1.1-unit transform height. `ApplySplineSubdivide` (`0x248A3B0`) inserts adjacent midpoints, repeating the source Subdivide count, including the closing edge only for closed splines. Both imported catalogs and the importer now preserve height and Subdivide.

`NativeSpline` replaces the circular fillets and invented overpasses with the same uniform cubic B-spline polynomial and open-end extrapolated controls (`ComputeCatPoints`, `0x23481D0`; BSpline position, `0x2348E34`). The raw cache uses `(controlSegments * 7) + 1` samples. BSpline forward vectors use the one-sample finite difference from `Spline.Evaluate` (`0x23463B4`), including wrapped differences for closed ends and its open-end Slerp branch. Cached position/forward interpolation follows SplineSample.Lerp (linear position, spherical forward). The 256 physics samples are now evaluated at `i / 255`, rather than evenly spaced by arc length. Targets travel one unit along that raw polyline, wrapping closed routes and clamping open routes as observed in BakeSpline's TravelUnclamped/Travel branches.

Collider prefab 1440 has MeshCollider 4779: nonconvex, cooking flags 30, default material. SplineMesh 11473 extrudes four cube channels: floor scale (1.3,0.1,1), offset (0,-0.05); walls scale (0.1,0.8,1), offsets (-0.66,0.4)/(0.66,0.4); ceiling scale (1.3,0.1,1), offset (0,0.8). All channels receive the visual channel's count in ConveyorColliderSystem.OnLevelBuildComplete (`0x2460098`). SplineMeshAutoCount.UpdateMeshCount (`0x24594A4`) applies ceil(length * LengthMultiplier), with serialized multiplier 4. The port rebuilds these physical channel surfaces into one static MeshCollider using that density, independently of the simpler rendered rails. It does not reuse the old U-shaped coarse collision mesh.

Open splines keep distinct endpoints. ConveyorEndMeshSystem.OnLevelBuildComplete (`0x2460304`) creates end prefabs; its block-trigger callback (`0x24607E0`) rotates the relative position about world up, adds the start position, and redirects velocity along the start forward while preserving speed. Angular velocity/body rotation are not rewritten by that callback. The port uses the source end trigger size (1.7287674,1.7751122,1.1545888), center (0.004192114,0.30314088,-0.16386585), the 180-degree end orientation, and the serialized end-boundary box transform. The former regressions demanding an invented elevated crossing in native open level 18 have been retired; remaining route test constructors consume LevelData so that open/closed and Subdivide fields are honored. No tests were run.

## Equivalence boundaries

### Intake packing-order review (2026-10-28)

Read the original `Carrier.<AddBlock>d__116.MoveNext`: it reserves a block index, calls `Carrier.GetBlockCoordinate(int)` at `0x23E4DB0`, then `Carrier.CoordinateToLocalPosition` at `0x23E5084`. Inspected the native coordinate/index bodies at `0x23E2CAC`, `0x23E2CEC`, `0x23E2D58`, `0x23E2DE8`, the carrier Block Parent transforms, and the separate `SandFlowRenderer.GetPileLayout`, `ReformJob.Execute`, and `DepositJob.Execute` paths. The ordinary logical-block coordinate conversion is not sufficient by itself to prove the final fine-grain visual deposition order; the source has a separate sand-flow rendering path.

The user's latest clarification supersedes the earlier horizontal-layer interpretation: fill the innermost vertical depth plane from bottom to top, then approach the opening one plane at a time. The current configurable fragment grid is encoded as depth/height/column, with adjacent height rows alternating horizontal direction as seen in `Carrier.GetBlockCoordinate(int)`. Its depth direction is derived from the actual carrier outlet in local space; the imported body mesh has its closed central end wall at +Z and an opening at -Z. Emission preserves its prior physical peel order through the inverse mapping. No original-app screen automation, gameplay execution or visual comparison was performed in this review; only source/native inspection and C# compilation.

This restores the inspected ordinary block movement algorithms, local physics defaults, spline inputs and collision cross-sections. The original APK uses Unity 6; this project uses Unity 2022.3.62f2. Matching algorithms and inputs does not establish identical PhysX contact ordering or bitwise trajectories across engine versions. The generated mesh is a surface reconstruction from recovered SplineMesh channels, not a recovered runtime mesh binary.

The source's complete moving logical-slot scheduling, remote cohort values, speed-boost state and special-carrier behaviors are not fully ported. Existing customized whole-block release, reversal, color-priority, gate intake selection and undo rules remain. Fragment density still uses the existing candidate-selection estimate. These differences prevent claiming that the entire game or every runtime APK configuration is identical.

No post-edit Unity compilation, tests, visual comparison or profiling was run, following the user's explicit instruction. No frame-rate improvement or full visual equivalence is claimed. The superseded moving-neighbor grid, staggered guidance, fabricated overpasses and coarse U-shaped collision mesh are removed.
