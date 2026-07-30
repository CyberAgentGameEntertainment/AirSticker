# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Air Sticker is CyberAgent's open-source Unity decal library (MIT). It creates decals by generating a mesh at runtime that conforms to the receiver model, instead of projection-based URP/DBuffer decals. This makes it work on both URP and the built-in render pipeline, supports skinned (animated) meshes, and allows arbitrary user materials — at the cost of mesh generation taking several frames.

- Committed editor version: **Unity 2020.3.40f1** with **URP 10.10.1** (`ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`). The package declares `"unity": "2020.3"` as its minimum.
- The distributable UPM package is **only** `Assets/AirSticker` (`jp.co.cyberagent.air-sticker`), installed by users via git URL with `?path=/Assets/AirSticker`. Everything else under `Assets/` (Demo, OtherAssets, Tests, Settings, Polytope Studio) is the development/demo project and is not shipped.
- Releases are consumed via git tags (e.g. `#1.0.0`); bump `version` in `Assets/AirSticker/package.json` when releasing.
- Documentation comes in EN/JA pairs: `README.md`/`README_JA.md` (usage) and `README_DEVELOPERS.md`/`README_DEVELOPERS_JA.md` (algorithm details, references "Mathematics for 3D Game Programming & Computer Graphics" §9.2). Keep both languages in sync when editing docs. Commit messages and PRs are often written in Japanese.

## Commands

There is no CI, lint config, or build script in this repo; everything runs through the Unity editor.

- Open the project (repo root) in Unity 2020.3.40f1. Compilation happens on editor focus; demo scenes are `Assets/Demo/Demo_01..03/*.unity`.
- Tests are EditMode NUnit tests in `Assets/Tests` (assembly `Tests`, editor-only). Run via **Window > General > Test Runner** in the editor, or headless:

  ```
  Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -logFile -
  ```

  To run a single test, use the Test Runner window or add `-testFilter <Namespace.Class.Method>` to the CLI call.

## Architecture

All runtime code is in `Assets/AirSticker/Runtime/Scripts` (single asmdef `AirSticker`, no dependencies, no Editor assembly — ~2200 lines total). Two public MonoBehaviours form the API; everything in `Scripts/Core` supports them.

### The two entry points

- **`AirStickerSystem`** — singleton facade. Exactly one must exist in the scene. Owns the three shared services and pumps them every `Update()`:
  - `DecalMeshPool` — caches `DecalMesh` instances keyed by hash of (receiver object, renderer, decal material). One `DecalMesh` = one draw call, so identical (receiver, renderer, material) combinations share a mesh and successive decals append to it.
  - `ReceiverObjectTrianglePolygonsPool` — caches the receiver's triangle-polygon soup (`ConvexPolygonInfo` lists) keyed by receiver GameObject, so repeat projections skip mesh extraction. Both pools garbage-collect entries whose receivers died, each frame.
  - `DecalProjectorLauncher` — FIFO queue that runs **one projector launch at a time**; the next request starts only when the current projector reaches `LaunchingCompleted` or dies.
- **`AirStickerProjector`** — one per decal. Configured in the inspector or created in code via `CreateAndLaunch()`. State machine: `NotLaunch → Launching → LaunchingCompleted/LaunchingCanceled`, observable via `NowState` or the `onFinishedLaunch` callback. `Launch()` may only be called once per instance.

### Projection pipeline (per launch)

`AirStickerProjector.ExecuteLaunch()` is a coroutine that, per receiver object:

1. Collects/creates target `DecalMesh`es from the pool (`AirStickerSystem.CollectEditDecalMeshes`). Decal renderers already hanging under the receiver are temporarily disabled so they aren't picked up as receivers themselves.
2. Builds the triangle-polygon list via `TrianglePolygonsFactory` if not pooled — frame-sliced (`MaxGeneratedPolygonPerFrame` polygons per frame, `yield return null` between chunks) to avoid spikes. Handles `MeshFilter`, `SkinnedMeshRenderer`, and `Terrain` sources.
3. Hands off to a **ThreadPool worker thread**: skinning matrices applied, broad-phase cull (`BroadPhaseConvexPolygonsDetection` — face-normal + distance rejection), six clip planes built from the decal box (width/height/depth in decal space), convex polygons split against them (`ConvexPolygon.SplitAndRemoveByPlane`), and resulting triangle fans appended to the decal meshes. The coroutine polls a flag until the worker finishes.
4. Back on the main thread, `DecalMesh.ExecutePostProcessingAfterWorkerThread()` uploads the results to Unity `Mesh` objects.

`DecalMesh` spawns a child GameObject named **`"AirStickerRenderer"`** under the receiver (`DecalMeshRenderer`), with a `MeshRenderer` or `SkinnedMeshRenderer` (bones copied from the receiver) matching the source. That literal name is load-bearing: `ExecuteLaunch()` filters out skinned renderers named `AirStickerRenderer` when gathering receiver geometry.

### Constraints to keep in mind

- Anything touched inside the worker-thread action must not call the Unity API — gather Unity-side data (`PrepareToRunOnWorkerThread`, bone matrices, transforms) before queueing the work item.
- Receiver models must have **Read/Write enabled** in import settings; the code paths that read mesh data error out otherwise (see commit history for the error-message handling).
- Z-fighting is inherent to the technique; `zOffsetInDecalSpace` (default 0.005) is the mitigation knob.

## Repo cautions

- The working tree accumulates noisy diffs (ProjectSettings, material assets, `packages-lock.json`) when the project is opened in a newer Unity editor than 2020.3.40f1. Don't commit editor-generated churn unrelated to your change; check `git diff` before staging.
- `Assets/OtherAssets/SD_unitychan` (© Unity Technologies Japan/UC) and `Assets/Polytope Studio` are third-party demo assets — don't modify them as part of library changes.
