using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core.Jobs
{
    /// <summary>
    ///     Source triangle polygons of a receiver object, laid out as a struct-of-arrays for the job pipeline.
    /// </summary>
    /// <remarks>
    ///     This replaces the managed ConvexPolygon soup that the old pipeline cached.
    ///     It is built once by <c>TrianglePolygonsFactory</c>, cached per receiver object in the triangle
    ///     polygons pool, and disposed when the receiver dies. All geometry is in model space and static;
    ///     the world-space positions are recomputed every launch by the skinning job.
    ///
    ///     The receiver components (MeshRenderer / SkinnedMeshRenderer / Terrain) are unified into a single
    ///     global index space (<see cref="TriangleComponentIndices" />), so the jobs never hold a managed
    ///     Component reference. The main thread maps a DecalMesh back to its component index through
    ///     <see cref="ComponentByIndex" />.
    /// </remarks>
    internal sealed class ReceiverConvexPolygonsMesh : IDisposable
    {
        public const int VerticesPerTriangle = 3;

        private bool _disposed;

        public ReceiverConvexPolygonsMesh(int triangleCount, int componentCount, Allocator allocator)
        {
            TriangleCount = triangleCount;
            ComponentCount = componentCount;
            var vertexCount = triangleCount * VerticesPerTriangle;

            SourcePositionsMs =
                new NativeArray<float3>(vertexCount, allocator, NativeArrayOptions.UninitializedMemory);
            SourceNormalsMs =
                new NativeArray<float3>(vertexCount, allocator, NativeArrayOptions.UninitializedMemory);
            SourceBoneWeights =
                new NativeArray<BoneWeight>(vertexCount, allocator, NativeArrayOptions.UninitializedMemory);
            TriangleComponentIndices =
                new NativeArray<int>(triangleCount, allocator, NativeArrayOptions.UninitializedMemory);
            ComponentIsSkinned =
                new NativeArray<bool>(math.max(1, componentCount), allocator, NativeArrayOptions.ClearMemory);
            ComponentByIndex = new Component[componentCount];
        }

        /// <summary>
        ///     True while a launch's jobs are reading this mesh. The pool defers disposing it (even after its
        ///     receiver died) until this is cleared, so the jobs never read freed NativeArrays. Main thread only.
        /// </summary>
        internal bool InUse;

        /// <summary>Number of source triangles.</summary>
        public int TriangleCount { get; }

        /// <summary>Number of receiver components under the receiver object.</summary>
        public int ComponentCount { get; }

        // Model-space source geometry. One entry per triangle vertex (TriangleCount * 3),
        // laid out as (tri0.v0, tri0.v1, tri0.v2, tri1.v0, ...).
        public NativeArray<float3> SourcePositionsMs;
        public NativeArray<float3> SourceNormalsMs;
        public NativeArray<BoneWeight> SourceBoneWeights;

        // Per triangle (TriangleCount): the global receiver-component index it belongs to.
        public NativeArray<int> TriangleComponentIndices;

        // Per component (ComponentCount): whether it is a SkinnedMeshRenderer.
        public NativeArray<bool> ComponentIsSkinned;

        // Per component (ComponentCount): the actual receiver Component. Main thread only —
        // used to map a DecalMesh to its component index at build time. The jobs never touch this.
        public Component[] ComponentByIndex;

        /// <summary>
        ///     The global component index of the given receiver component, or -1 if it is not one of this
        ///     receiver's components (in which case the decal mesh gets no geometry).
        /// </summary>
        public int IndexOfComponent(Component component)
        {
            if (component == null) return -1;
            for (var i = 0; i < ComponentByIndex.Length; i++)
                if (ReferenceEquals(ComponentByIndex[i], component))
                    return i;
            return -1;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (SourcePositionsMs.IsCreated) SourcePositionsMs.Dispose();
            if (SourceNormalsMs.IsCreated) SourceNormalsMs.Dispose();
            if (SourceBoneWeights.IsCreated) SourceBoneWeights.Dispose();
            if (TriangleComponentIndices.IsCreated) TriangleComponentIndices.Dispose();
            if (ComponentIsSkinned.IsCreated) ComponentIsSkinned.Dispose();
            ComponentByIndex = null;
        }
    }
}
