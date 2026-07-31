using System;
using System.Collections;
using System.Collections.Generic;
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

        private bool _disposed;
        private static int _maxGeneratedPolygonPerFrame = 100000;

        // The write cursor into the result's per-triangle SoA arrays, shared across the fill coroutines.
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
        ///     resultHolder[0]; it is left null if a mesh is not Read/Write enabled (nothing to build).
        /// </summary>
        internal IEnumerator BuildFromReceiverObject(
            MeshRenderer[] meshRenderers,
            SkinnedMeshRenderer[] skinnedMeshRenderers,
            Terrain[] terrains,
            ReceiverConvexPolygonsMesh[] resultHolder)
        {
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
            var componentCount = meshRenderers.Length + skinnedMeshRenderers.Length + terrains.Length;

            var result = new ReceiverConvexPolygonsMesh(triangleCount, componentCount, Allocator.Persistent);

            // Unify the receiver components into one global index space: mesh renderers, then skinned mesh
            // renderers, then terrains.
            var skinnedBase = meshRenderers.Length;
            var terrainBase = meshRenderers.Length + skinnedMeshRenderers.Length;
            for (var i = 0; i < meshRenderers.Length; i++) result.ComponentByIndex[i] = meshRenderers[i];
            for (var j = 0; j < skinnedMeshRenderers.Length; j++)
            {
                result.ComponentByIndex[skinnedBase + j] = skinnedMeshRenderers[j];
                result.ComponentIsSkinned[skinnedBase + j] = true;
            }

            for (var k = 0; k < terrains.Length; k++) result.ComponentByIndex[terrainBase + k] = terrains[k];

            _writeTriangleCursor = 0;
            if (buildMesh) yield return FillFromMeshRenderers(meshRenderers, result);
            if (buildSkin) yield return FillFromSkinnedMeshRenderers(skinnedMeshRenderers, skinnedBase, result);
            yield return FillFromTerrains(terrains, terrainBase, terrainMeshResolutionScale, result);

            if (_writeTriangleCursor != triangleCount)
            {
                // A source was destroyed during the frame-sliced fill, so the SoA is only partially written.
                // Discard it instead of registering a mesh whose uninitialized regions the jobs would read.
                result.Dispose();
                yield break;
            }

            resultHolder[0] = result;
        }

        // Driven by the mesh renderers (not the mesh filter array) so componentIndex == rendererNo always
        // matches the mesh-renderer region of ComponentByIndex, even when a child has a MeshFilter without a
        // MeshRenderer or vice versa. The polygon count (GetNumPolygonsFromMeshRenderers) is driven the same
        // way, so the total stays consistent with BuildFromReceiverObject's completeness check.
        private IEnumerator FillFromMeshRenderers(MeshRenderer[] meshRenderers, ReceiverConvexPolygonsMesh result)
        {
            var polygonNoInFill = 0;
            for (var rendererNo = 0; rendererNo < meshRenderers.Length; rendererNo++)
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
                            yield return null;
                            if (!meshRenderer || !meshFilter || meshFilter.sharedMesh == null) yield break;
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

        private IEnumerator FillFromSkinnedMeshRenderers(
            SkinnedMeshRenderer[] skinnedMeshRenderers, int componentBase, ReceiverConvexPolygonsMesh result)
        {
            var workingBoneWeights = new List<BoneWeight>(MaxWorkingVertexCount);
            var polygonNoInFill = 0;
            for (var rendererNo = 0; rendererNo < skinnedMeshRenderers.Length; rendererNo++)
            {
                var skinnedMeshRenderer = skinnedMeshRenderers[rendererNo];
                if (!skinnedMeshRenderer || skinnedMeshRenderer.sharedMesh == null) yield break;
                var mesh = skinnedMeshRenderer.sharedMesh;
                if (mesh.isReadable == false) yield break;
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
                            yield return null;
                            if (!skinnedMeshRenderer || skinnedMeshRenderer.sharedMesh == null) yield break;
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

        private IEnumerator FillFromTerrains(
            Terrain[] terrains, int componentBase, float terrainMeshResolutionScale, ReceiverConvexPolygonsMesh result)
        {
            for (var terrainNo = 0; terrainNo < terrains.Length; terrainNo++)
            {
                var terrain = terrains[terrainNo];
                if (!terrain || terrain == null) yield break;
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
                        yield return null;
                        if (!terrain || terrain == null) yield break;
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

        private static int GetNumPolygonsFromSkinModelRenderers(SkinnedMeshRenderer[] skinnedMeshRenderers)
        {
            var numPolygon = 0;
            foreach (var renderer in skinnedMeshRenderers)
            {
                if (!renderer || renderer.sharedMesh == null) return -1;
                var mesh = renderer.sharedMesh;
                if (mesh.isReadable == false)
                {
                    Debug.LogError(
                        $"The mesh of the skinned mesh renderer named {renderer.name} is not readable. Please set the Read/Write Enabled flag in the model import settings.");
                    return -1;
                }

                numPolygon += mesh.triangles.Length / 3;
            }

            return numPolygon;
        }

        private static int GetNumPolygonsFromTerrain(Terrain terrain, float terrainMeshResolutionScale)
        {
            var terrainData = terrain.terrainData;
            var vertexCountX = (int)(terrainData.heightmapResolution * terrainMeshResolutionScale);
            var vertexCountY = (int)(terrainData.heightmapResolution * terrainMeshResolutionScale);
            return (vertexCountX - 1) * (vertexCountY - 1) * 2;
        }

        private static int GetNumPolygonsFromTerrains(Terrain[] terrains, float terrainMeshResolutionScale)
        {
            var numPolygon = 0;
            foreach (var terrain in terrains) numPolygon += GetNumPolygonsFromTerrain(terrain, terrainMeshResolutionScale);
            return numPolygon;
        }

        // Renderer-driven so it stays consistent with FillFromMeshRenderers: a renderer without a MeshFilter
        // (or without a mesh) contributes nothing, and only a non-readable mesh aborts the whole mesh path.
        private static int GetNumPolygonsFromMeshRenderers(MeshRenderer[] meshRenderers)
        {
            var numPolygon = 0;
            foreach (var meshRenderer in meshRenderers)
            {
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

                numPolygon += mesh.triangles.Length / 3;
            }

            return numPolygon;
        }
    }
}
