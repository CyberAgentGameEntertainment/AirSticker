using System;
using System.Collections.Generic;
using System.Threading;
using AirSticker.Runtime.Scripts.Core.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core
{
    /// <summary>
    ///     Create the source triangle polygons (<see cref="ReceiverConvexPolygonsMesh" />) of a receiver
    ///     object from its mesh renderers, skinned mesh renderers and terrains.
    /// </summary>
    public class TrianglePolygonsFactory : IDisposable
    {
        private static readonly int VertexCountOfTrianglePolygon = 3;
        private static readonly int MaxWorkingVertexCountForTerrain = 128 * 128; // 16,384
        private static readonly int MaxWorkingVertexCount = 65536;
        private static readonly int MaxWorkingTriangleCount = 65536;

        private NativeArray<int> _workingTriangles =
            new NativeArray<int>(MaxWorkingTriangleCount, Allocator.Persistent);

        private NativeArray<Vector3> _workingVertexNormals =
            new NativeArray<Vector3>(MaxWorkingVertexCount, Allocator.Persistent);

        private NativeArray<Vector3> _workingVertexPositions =
            new NativeArray<Vector3>(MaxWorkingVertexCount, Allocator.Persistent);

        // Reused across launches. It is created on the first skinned receiver instead of in the constructor,
        // because its capacity is worth ~2 MB and projects without skinned receivers never need it.
        private List<BoneWeight> _workingBoneWeights;

        private bool _disposed;
        private static int _maxGeneratedPolygonPerFrame = 100000;

        // The write cursor into the result's per-triangle SoA arrays, shared across the fill methods.
        // Like the working buffers above it belongs to whichever extraction is running now, which is why a
        // canceled fill must return before it writes anything (see FillFromMeshRenderersAsync).
        private int _writeTriangleCursor;

        /// <summary>
        ///     Maximum number of polygons processed per frame.
        ///     Values less than 1 are clamped to 1, because this value is used as a modulo divisor.
        /// </summary>
        public static int MaxGeneratedPolygonPerFrame
        {
            get => _maxGeneratedPolygonPerFrame;
            set => _maxGeneratedPolygonPerFrame = Mathf.Max(1, value);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _workingVertexPositions.Dispose();
            _workingVertexNormals.Dispose();
            _workingTriangles.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Build the receiver's source triangle polygons. On success the result is written to
        ///     resultHolder[0]; it is left null if a mesh is not Read/Write enabled (nothing to build) or if
        ///     <paramref name="cancellation" /> was requested during the frame-sliced fill.
        /// </summary>
        /// <remarks>
        ///     This method owns the result until it returns: the fill runs across frames and nothing can stop
        ///     it from the outside, so it publishes the result to the holder only once it is complete and
        ///     disposes it itself otherwise. The token is only polled, never handed to
        ///     <c>Awaitable.NextFrameAsync</c>, because a cancelable await registers a callback on the token's
        ///     source and would allocate on every frame of the fill.
        /// </remarks>
        internal async Awaitable BuildFromReceiverObjectAsync(
            IReadOnlyList<MeshRenderer> meshRenderers,
            IReadOnlyList<SkinnedMeshRenderer> skinnedMeshRenderers,
            IReadOnlyList<Terrain> terrains,
            ReceiverConvexPolygonsMesh[] resultHolder,
            CancellationToken cancellation)
        {
            resultHolder[0] = null;
            var numTerrainMeshPolygon = GetNumPolygonsFromTerrains(terrains, 1.0f);
            var terrainMeshResolutionScale = numTerrainMeshPolygon > 0
                ? Mathf.Sqrt(MaxWorkingVertexCountForTerrain / (float)numTerrainMeshPolygon)
                : 1.0f;

            var meshTriangleCount = GetNumPolygonsFromMeshRenderers(meshRenderers);
            var skinTriangleCount = GetNumPolygonsFromSkinModelRenderers(skinnedMeshRenderers);
            var terrainTriangleCount = GetNumPolygonsFromTerrains(terrains, terrainMeshResolutionScale);

            // A negative count means a mesh is not readable; that source contributes no geometry (its decal
            // mesh stays empty), matching the old per-source behavior.
            var buildMesh = meshTriangleCount > 0;
            var buildSkin = skinTriangleCount > 0;
            if (!buildMesh) meshTriangleCount = 0;
            if (!buildSkin) skinTriangleCount = 0;

            var triangleCount = meshTriangleCount + skinTriangleCount + terrainTriangleCount;
            var componentCount = meshRenderers.Count + skinnedMeshRenderers.Count + terrains.Count;

            var result = new ReceiverConvexPolygonsMesh(triangleCount, componentCount, Allocator.Persistent);

            // Unify the receiver components into one global index space: mesh renderers, then skinned mesh
            // renderers, then terrains.
            var skinnedBase = meshRenderers.Count;
            var terrainBase = meshRenderers.Count + skinnedMeshRenderers.Count;
            for (var i = 0; i < meshRenderers.Count; i++) result.ComponentByIndex[i] = meshRenderers[i];
            for (var j = 0; j < skinnedMeshRenderers.Count; j++)
            {
                result.ComponentByIndex[skinnedBase + j] = skinnedMeshRenderers[j];
                result.ComponentIsSkinned[skinnedBase + j] = true;
            }

            for (var k = 0; k < terrains.Count; k++) result.ComponentByIndex[terrainBase + k] = terrains[k];

            _writeTriangleCursor = 0;
            if (buildMesh) await FillFromMeshRenderersAsync(meshRenderers, result, cancellation);
            if (buildSkin && !cancellation.IsCancellationRequested)
                await FillFromSkinnedMeshRenderersAsync(skinnedMeshRenderers, skinnedBase, result, cancellation);
            if (!cancellation.IsCancellationRequested)
                await FillFromTerrainsAsync(terrains, terrainBase, terrainMeshResolutionScale, result, cancellation);

            if (_writeTriangleCursor != triangleCount)
            {
                // The fill was canceled, or a source was destroyed during it, so the SoA is only partially
                // written. Discard it instead of handing over a mesh whose uninitialized regions the jobs
                // would read. The holder is still null, so the caller cancels and never sees this result.
                result.Dispose();
                return;
            }

            resultHolder[0] = result;
        }

        // Driven by the mesh renderers (not the mesh filter array) so componentIndex == rendererNo always
        // matches the mesh-renderer region of ComponentByIndex, even when a child has a MeshFilter without a
        // MeshRenderer or vice versa. The polygon count (GetNumPolygonsFromMeshRenderers) is driven the same
        // way, so the total stays consistent with BuildFromReceiverObjectAsync's completeness check.
        private async Awaitable FillFromMeshRenderersAsync(
            IReadOnlyList<MeshRenderer> meshRenderers, ReceiverConvexPolygonsMesh result,
            CancellationToken cancellation)
        {
            var polygonNoInFill = 0;
            for (var rendererNo = 0; rendererNo < meshRenderers.Count; rendererNo++)
            {
                var meshRenderer = meshRenderers[rendererNo];
                if (!meshRenderer) continue;
                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (!meshFilter || meshFilter.sharedMesh == null) continue;
                var mesh = meshFilter.sharedMesh;
                // The mesh renderers are the first components, so componentIndex == rendererNo.
                var componentIndex = rendererNo;

                using var meshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
                var meshData = meshDataArray[0];
                meshData.GetVertices(_workingVertexPositions);
                meshData.GetNormals(_workingVertexNormals);
                var subMeshCount = meshData.subMeshCount;
                for (var meshNo = 0; meshNo < subMeshCount; meshNo++)
                {
                    meshData.GetIndices(_workingTriangles, meshNo);
                    var numPoly = (int)(mesh.GetIndexCount(meshNo) / 3);
                    for (var i = 0; i < numPoly; i++)
                    {
                        if (polygonNoInFill != 0 && polygonNoInFill % MaxGeneratedPolygonPerFrame == 0)
                        {
                            await Awaitable.NextFrameAsync();
                            // Checked before anything else. Unlike a coroutine this is not stopped by the
                            // projector's destruction, and the working buffers plus _writeTriangleCursor
                            // belong to whichever extraction is running now, so a canceled fill must return
                            // without writing into the next launch's extraction.
                            if (cancellation.IsCancellationRequested) return;
                            if (!meshRenderer || !meshFilter || meshFilter.sharedMesh == null) return;
                        }

                        polygonNoInFill++;
                        var v0 = _workingTriangles[i * 3];
                        var v1 = _workingTriangles[i * 3 + 1];
                        var v2 = _workingTriangles[i * 3 + 2];
                        WriteTriangle(result, componentIndex,
                            _workingVertexPositions[v0], _workingVertexPositions[v1], _workingVertexPositions[v2],
                            _workingVertexNormals[v0], _workingVertexNormals[v1], _workingVertexNormals[v2],
                            default, default, default);
                    }
                }
            }
        }

        private async Awaitable FillFromSkinnedMeshRenderersAsync(
            IReadOnlyList<SkinnedMeshRenderer> skinnedMeshRenderers, int componentBase,
            ReceiverConvexPolygonsMesh result, CancellationToken cancellation)
        {
            var workingBoneWeights = _workingBoneWeights ??= new List<BoneWeight>(MaxWorkingVertexCount);
            var polygonNoInFill = 0;
            for (var rendererNo = 0; rendererNo < skinnedMeshRenderers.Count; rendererNo++)
            {
                var skinnedMeshRenderer = skinnedMeshRenderers[rendererNo];
                if (!skinnedMeshRenderer || skinnedMeshRenderer.sharedMesh == null) return;
                var mesh = skinnedMeshRenderer.sharedMesh;
                if (mesh.isReadable == false) return;
                var componentIndex = componentBase + rendererNo;
                var hasRootBone = skinnedMeshRenderer.rootBone != null;

                using var meshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
                var meshData = meshDataArray[0];
                meshData.GetVertices(_workingVertexPositions);
                meshData.GetNormals(_workingVertexNormals);
                mesh.GetBoneWeights(workingBoneWeights);
                var subMeshCount = meshData.subMeshCount;
                for (var meshNo = 0; meshNo < subMeshCount; meshNo++)
                {
                    meshData.GetIndices(_workingTriangles, meshNo);
                    var numPoly = (int)(mesh.GetIndexCount(meshNo) / 3);
                    for (var i = 0; i < numPoly; i++)
                    {
                        if (polygonNoInFill != 0 && polygonNoInFill % MaxGeneratedPolygonPerFrame == 0)
                        {
                            await Awaitable.NextFrameAsync();
                            if (cancellation.IsCancellationRequested) return;
                            if (!skinnedMeshRenderer || skinnedMeshRenderer.sharedMesh == null) return;
                        }

                        polygonNoInFill++;
                        var v0 = _workingTriangles[i * 3];
                        var v1 = _workingTriangles[i * 3 + 1];
                        var v2 = _workingTriangles[i * 3 + 2];
                        var w0 = hasRootBone ? workingBoneWeights[v0] : default;
                        var w1 = hasRootBone ? workingBoneWeights[v1] : default;
                        var w2 = hasRootBone ? workingBoneWeights[v2] : default;
                        WriteTriangle(result, componentIndex,
                            _workingVertexPositions[v0], _workingVertexPositions[v1], _workingVertexPositions[v2],
                            _workingVertexNormals[v0], _workingVertexNormals[v1], _workingVertexNormals[v2],
                            w0, w1, w2);
                    }
                }
            }
        }

        private async Awaitable FillFromTerrainsAsync(
            IReadOnlyList<Terrain> terrains, int componentBase, float terrainMeshResolutionScale,
            ReceiverConvexPolygonsMesh result, CancellationToken cancellation)
        {
            for (var terrainNo = 0; terrainNo < terrains.Count; terrainNo++)
            {
                var terrain = terrains[terrainNo];
                if (!terrain || terrain == null) return;
                var componentIndex = componentBase + terrainNo;
                var terrainData = terrain.terrainData;
                var vertexCountW = Math.Max(2, (int)(terrainData.heightmapResolution * terrainMeshResolutionScale));
                var vertexCountH = Math.Max(2, (int)(terrainData.heightmapResolution * terrainMeshResolutionScale));
                var size = terrainData.size;

                var vertexCount = vertexCountW * vertexCountH;
                var positions = new Vector3[vertexCount];
                var normals = new Vector3[vertexCount];
                var vertexNo = 0;
                for (var y = 0; y < vertexCountH; y++)
                for (var x = 0; x < vertexCountW; x++)
                {
                    var normalizedX = x / (float)(vertexCountW - 1);
                    var normalizedY = y / (float)(vertexCountH - 1);
                    normals[vertexNo] = terrainData.GetInterpolatedNormal(normalizedX, normalizedY);
                    var height = terrainData.GetInterpolatedHeight(normalizedX, normalizedY);
                    positions[vertexNo] = new Vector3(size.x * normalizedX, height, size.z * normalizedY);
                    vertexNo++;
                }

                var polygonNoInFill = 0;
                for (var y = 0; y < vertexCountH - 1; y++)
                for (var x = 0; x < vertexCountW - 1; x++)
                {
                    if (polygonNoInFill != 0 && polygonNoInFill % MaxGeneratedPolygonPerFrame == 0)
                    {
                        await Awaitable.NextFrameAsync();
                        if (cancellation.IsCancellationRequested) return;
                        if (!terrain || terrain == null) return;
                    }

                    polygonNoInFill += 2;

                    var i00 = y * vertexCountW + x;
                    var i10 = (y + 1) * vertexCountW + x;
                    var i01 = y * vertexCountW + x + 1;
                    var i11 = (y + 1) * vertexCountW + x + 1;

                    WriteTriangle(result, componentIndex,
                        positions[i00], positions[i10], positions[i01],
                        normals[i00], normals[i10], normals[i01],
                        default, default, default);
                    WriteTriangle(result, componentIndex,
                        positions[i10], positions[i11], positions[i01],
                        normals[i10], normals[i11], normals[i01],
                        default, default, default);
                }
            }
        }

        private void WriteTriangle(ReceiverConvexPolygonsMesh result, int componentIndex,
            float3 p0, float3 p1, float3 p2, float3 n0, float3 n1, float3 n2,
            BoneWeight w0, BoneWeight w1, BoneWeight w2)
        {
            var t = _writeTriangleCursor * VertexCountOfTrianglePolygon;
            result.SourcePositionsMs[t] = p0;
            result.SourcePositionsMs[t + 1] = p1;
            result.SourcePositionsMs[t + 2] = p2;
            result.SourceNormalsMs[t] = n0;
            result.SourceNormalsMs[t + 1] = n1;
            result.SourceNormalsMs[t + 2] = n2;
            result.SourceBoneWeights[t] = w0;
            result.SourceBoneWeights[t + 1] = w1;
            result.SourceBoneWeights[t + 2] = w2;
            result.TriangleComponentIndices[_writeTriangleCursor] = componentIndex;
            _writeTriangleCursor++;
        }

        private static int GetNumPolygonsFromSkinModelRenderers(IReadOnlyList<SkinnedMeshRenderer> skinnedMeshRenderers)
        {
            var numPolygon = 0;
            for (var rendererNo = 0; rendererNo < skinnedMeshRenderers.Count; rendererNo++)
            {
                var renderer = skinnedMeshRenderers[rendererNo];
                if (!renderer || renderer.sharedMesh == null) return -1;
                var mesh = renderer.sharedMesh;
                if (mesh.isReadable == false)
                {
                    Debug.LogError(
                        $"The mesh of the skinned mesh renderer named {renderer.name} is not readable. Please set the Read/Write Enabled flag in the model import settings.");
                    return -1;
                }

                numPolygon += GetNumPolygonsFromMesh(mesh);
            }

            return numPolygon;
        }

        // Counted from the index counts instead of mesh.triangles, which copies the whole index buffer into a
        // managed array just to be measured (360 KB of garbage for a 30k-polygon model). The per-submesh
        // division matches how the Fill* methods walk the submeshes, so the count and the fill always agree.
        private static int GetNumPolygonsFromMesh(Mesh mesh)
        {
            var numPolygon = 0;
            var subMeshCount = mesh.subMeshCount;
            for (var meshNo = 0; meshNo < subMeshCount; meshNo++)
                numPolygon += (int)(mesh.GetIndexCount(meshNo) / 3);

            return numPolygon;
        }

        private static int GetNumPolygonsFromTerrain(Terrain terrain, float terrainMeshResolutionScale)
        {
            var terrainData = terrain.terrainData;
            var vertexCountX = (int)(terrainData.heightmapResolution * terrainMeshResolutionScale);
            var vertexCountY = (int)(terrainData.heightmapResolution * terrainMeshResolutionScale);
            return (vertexCountX - 1) * (vertexCountY - 1) * 2;
        }

        private static int GetNumPolygonsFromTerrains(
            IReadOnlyList<Terrain> terrains, float terrainMeshResolutionScale)
        {
            var numPolygon = 0;
            for (var terrainNo = 0; terrainNo < terrains.Count; terrainNo++)
                numPolygon += GetNumPolygonsFromTerrain(terrains[terrainNo], terrainMeshResolutionScale);
            return numPolygon;
        }

        // Renderer-driven so it stays consistent with FillFromMeshRenderersAsync: a renderer without a MeshFilter
        // (or without a mesh) contributes nothing, and only a non-readable mesh aborts the whole mesh path.
        private static int GetNumPolygonsFromMeshRenderers(IReadOnlyList<MeshRenderer> meshRenderers)
        {
            var numPolygon = 0;
            for (var rendererNo = 0; rendererNo < meshRenderers.Count; rendererNo++)
            {
                var meshRenderer = meshRenderers[rendererNo];
                if (!meshRenderer) continue;
                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (!meshFilter || meshFilter.sharedMesh == null) continue;
                var mesh = meshFilter.sharedMesh;
                if (mesh.isReadable == false)
                {
                    Debug.LogError(
                        $"The mesh of the mesh filter named {meshFilter.name} is not readable. Please set the Read/Write Enabled flag in the model import settings.");
                    return -1;
                }

                numPolygon += GetNumPolygonsFromMesh(mesh);
            }

            return numPolygon;
        }
    }
}
