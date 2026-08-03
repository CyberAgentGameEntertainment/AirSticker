# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2026-08-03

### Added

- `AirStickerProjector.CreateAndLaunch()` overload that takes an array of receiver objects
  (`GameObject[]`), so a decal created at runtime can span the boundary of multiple receiver
  objects. All receivers are clipped in the same decal space, so the decal texture is
  continuous across the boundary. One decal mesh is built per receiver (per renderer and
  decal material), so the more receivers the decal spans, the more draw calls are made.
  The single-receiver overload is unchanged and delegates to the new one.
- Demo_05: click near the boundary of two walls and compare the multiple receivers overload
  (the sticker spans the boundary) with the single receiver overload (the sticker is cut at
  the boundary).

### Known limitations

- Call sites passing a `null` literal as the receiver argument of `CreateAndLaunch()` no
  longer compile because the call is ambiguous between the two overloads. Cast to
  `(GameObject)null` if needed (such a call pastes nothing); calls passing a
  `GameObject`-typed variable are unaffected.

## [2.0.0] - 2026-07-31

Decal mesh generation was rewritten on the Unity Job System + Burst. This is a major
release: the minimum supported Unity version is raised and some previously public types
are removed. If you need to support an older Unity version, keep using Air Sticker 1.x.

### Changed (breaking)

- The minimum supported Unity version is now **Unity 6.0** (was 2020.3).
- Added dependencies on `com.unity.burst` and `com.unity.mathematics`. Previous versions
  had no package dependencies.
- Removed the public types `ConvexPolygon`, `Line`, and `BroadPhaseConvexPolygonsDetection`.
  The managed convex-polygon geometry they represented is replaced by the job pipeline's
  struct-of-arrays (`NativeArray`) data. The `AirStickerSystem` / `AirStickerProjector`
  entry points are unchanged.

### Changed

- Skinning, broad-phase culling, and clipping now run as parallel Burst-compiled jobs
  (`IJobParallelFor`) instead of a single ThreadPool worker, scaling with core count and
  using SIMD.
- Mesh upload uses the writable `MeshData` API (a single `Apply`), the index buffer format
  is chosen automatically (UInt16/UInt32), and `Mesh.Optimize()` is no longer called on the
  per-launch path.
- Tangents are computed on a worker (job), removing that cost from the main thread.
- Broad-phase working buffers are pooled, eliminating the large per-launch managed
  allocations that previously caused GC spikes.

### Added

- `AirStickerPerformanceLog.Enabled`: an opt-in diagnostic switch that logs per-stage
  pipeline timings via `Debug.Log`. Disabled by default; intended for profiling only (it
  completes the projection jobs synchronously while enabled).
