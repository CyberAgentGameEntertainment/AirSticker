using AirSticker.Runtime.Scripts.Core;
using AirSticker.Runtime.Scripts.Core.Jobs;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Tests
{
    /// <summary>
    ///     Differential test that asserts the NativeArray tangent port (DecalMeshTangents) matches the legacy
    ///     managed tangent calculator (DecalMeshTangentCalculator) for the same input.
    /// </summary>
    public class TestDecalMeshTangents
    {
        [Test]
        public void NativeTangentsMatchLegacyForAppendedRange()
        {
            // A convex polygon (pentagon) placed at a non-zero vertex/index offset, to catch offset handling.
            const int total = 8;
            const int vertexStart = 2;
            const int vertexCount = 5;
            const int indexStart = 3;

            var localPositions = new[]
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(1.0f, 0.0f, 0.2f),
                new Vector3(1.2f, 1.0f, -0.1f),
                new Vector3(0.5f, 1.5f, 0.3f),
                new Vector3(-0.2f, 1.0f, 0.1f)
            };
            var localNormals = new[]
            {
                new Vector3(0.0f, 0.0f, 1.0f).normalized,
                new Vector3(0.1f, 0.0f, 1.0f).normalized,
                new Vector3(0.0f, 0.1f, 1.0f).normalized,
                new Vector3(-0.1f, 0.05f, 1.0f).normalized,
                new Vector3(0.05f, -0.1f, 1.0f).normalized
            };
            var localUvs = new[]
            {
                new Vector2(0.1f, 0.1f),
                new Vector2(0.9f, 0.15f),
                new Vector2(0.95f, 0.9f),
                new Vector2(0.5f, 1.0f),
                new Vector2(0.05f, 0.85f)
            };

            var positions = new Vector3[total];
            var normals = new Vector3[total];
            var uvs = new Vector2[total];
            for (var i = 0; i < vertexCount; i++)
            {
                positions[vertexStart + i] = localPositions[i];
                normals[vertexStart + i] = localNormals[i];
                uvs[vertexStart + i] = localUvs[i];
            }

            // Triangle fan over the pentagon, using real (offset) vertex indices, placed at indexStart.
            var indices = new int[indexStart + (vertexCount - 2) * 3];
            var indexWrite = indexStart;
            for (var triNo = 0; triNo < vertexCount - 2; triNo++)
            {
                indices[indexWrite++] = vertexStart;
                indices[indexWrite++] = vertexStart + triNo + 1;
                indices[indexWrite++] = vertexStart + triNo + 2;
            }

            var indexCount = (vertexCount - 2) * 3;

            // --- Legacy managed tangents ---
            var legacyTangents = new Vector4[total];
            DecalMeshTangentCalculator.CalculateTangents(
                positions, normals, uvs, indices, legacyTangents,
                vertexStart, vertexCount, indexStart, indexCount);

            // --- NativeArray port ---
            var nativePositions = new NativeArray<float3>(total, Allocator.Temp);
            var nativeNormals = new NativeArray<float3>(total, Allocator.Temp);
            var nativeUvs = new NativeArray<float2>(total, Allocator.Temp);
            var nativeIndices = new NativeArray<int>(indices.Length, Allocator.Temp);
            var nativeTangents = new NativeArray<float4>(total, Allocator.Temp);
            var tangentAccum = new NativeArray<float3>(vertexCount, Allocator.Temp);
            var bitangentAccum = new NativeArray<float3>(vertexCount, Allocator.Temp);
            try
            {
                for (var i = 0; i < total; i++)
                {
                    nativePositions[i] = positions[i];
                    nativeNormals[i] = normals[i];
                    nativeUvs[i] = uvs[i];
                }

                for (var i = 0; i < indices.Length; i++) nativeIndices[i] = indices[i];

                DecalMeshTangents.ComputeTangents(
                    nativePositions, nativeNormals, nativeUvs, nativeIndices, nativeTangents,
                    tangentAccum, bitangentAccum,
                    vertexStart, vertexCount, indexStart, indexCount);

                for (var i = 0; i < vertexCount; i++)
                {
                    var expected = legacyTangents[vertexStart + i];
                    var actual = nativeTangents[vertexStart + i];
                    Assert.AreEqual(expected.x, actual.x, 1e-4f, $"tangent[{i}].x");
                    Assert.AreEqual(expected.y, actual.y, 1e-4f, $"tangent[{i}].y");
                    Assert.AreEqual(expected.z, actual.z, 1e-4f, $"tangent[{i}].z");
                    Assert.AreEqual(expected.w, actual.w, 1e-4f, $"tangent[{i}].w");
                }
            }
            finally
            {
                nativePositions.Dispose();
                nativeNormals.Dispose();
                nativeUvs.Dispose();
                nativeIndices.Dispose();
                nativeTangents.Dispose();
                tangentAccum.Dispose();
                bitangentAccum.Dispose();
            }
        }
    }
}
