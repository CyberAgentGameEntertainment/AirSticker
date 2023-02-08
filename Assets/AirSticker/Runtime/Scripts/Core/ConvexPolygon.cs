using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;

namespace AirSticker.Runtime.Scripts.Core
{
    /// <summary>
    ///     Convex polygons with more vertices possible.
    /// </summary>
    public sealed class ConvexPolygon
    {
        public const int DefaultMaxVertex = 64;
        private readonly BoneWeight[] _boneWeightBuffer;
        private readonly bool _isSkinnedMeshRenderer;
        private readonly Line[] _lineBuffer;
        private readonly int _maxVertex;
        private readonly Vector3[] _normalInModelSpaceBuffer;
        private readonly Vector3[] _normalInWorldSpaceBuffer;
        private readonly Vector3[] _positionInModelSpaceBuffer;
        private readonly Vector3[] _positionInWorldSpaceBuffer;
        private readonly int _rendererNo;
        private readonly int _startOffsetInBuffer;
        private bool _existsRootBone;
        private Vector3 _faceNormal;
        private Matrix4x4 _localToWorldMatrix;

        /// <summary>
        ///     Constructor.<br />
        ///     Buffers for the various arguments must be allocated for the required size.<br />
        /// </summary>
        /// <param name="positionInWorldSpaceBuffer">Buffer of vertex position in world space.</param>
        /// <param name="normalInWorldSpaceBuffer">Buffer of vertex normal in world space.</param>
        /// <param name="boneWeightBuffer">Buffer of vertex bone weights.</param>
        /// <param name="lineBuffer">Buffer of lines of convex polygons.</param>
        /// <param name="positionInModelSpaceBuffer">Buffer of vertex position in model space.</param>
        /// <param name="normalInModelSpaceBuffer">Buffer of vertex normal in model space.</param>
        /// <param name="receiverMeshRenderer">Receiver mesh renderer which the decal mesh will be attached.</param>
        /// <param name="startOffsetInBuffer">
        ///     offset of start index in buffer.<br />
        ///     The value of this variable is the starting position where the vertex information of this polygon is stored.
        /// </param>
        /// <param name="initVertexCount">Initial count of vertices of convex polygon</param>
        /// <param name="rendererNo">Number of receiver mesh renderer.</param>
        /// <param name="maxVertex">
        ///     Maximum number of vertices in a convex polygon.<br />
        ///     A convex polygon can be divided up to the value of this argument.<br />
        ///     If not specified, the value of DefaultMaxVertex is the maximum number of vertices.<br />
        /// </param>
        public ConvexPolygon(
            Vector3[] positionInWorldSpaceBuffer,
            Vector3[] normalInWorldSpaceBuffer,
            BoneWeight[] boneWeightBuffer,
            Line[] lineBuffer,
            Vector3[] positionInModelSpaceBuffer,
            Vector3[] normalInModelSpaceBuffer,
            Renderer receiverMeshRenderer,
            int startOffsetInBuffer,
            int initVertexCount,
            int rendererNo,
            int maxVertex = DefaultMaxVertex)
        {
            _isSkinnedMeshRenderer = receiverMeshRenderer is SkinnedMeshRenderer;
            _rendererNo = rendererNo;
            _positionInModelSpaceBuffer = positionInModelSpaceBuffer;
            _normalInModelSpaceBuffer = normalInModelSpaceBuffer;
            _maxVertex = maxVertex;
            _boneWeightBuffer = boneWeightBuffer;
            _positionInWorldSpaceBuffer = positionInWorldSpaceBuffer;
            _normalInWorldSpaceBuffer = normalInWorldSpaceBuffer;
            _lineBuffer = lineBuffer;
            _startOffsetInBuffer = startOffsetInBuffer;
            ReceiverMeshRenderer = receiverMeshRenderer;
            VertexCount = initVertexCount;
        }

        /// <summary>
        ///     Copy Constructor.<br />
        ///     The contents of the various buffers held by the source convex polygon are copied only as needed, not all.<br />
        ///     Also, Buffers for the various arguments must be allocated for the required size.<br />
        /// </summary>
        /// <param name="srcConvexPolygon">Convex polygon of copy source.</param>
        /// <param name="startOffsetInBuffer">offset of start index in buffer.</param>
        /// <param name="maxVertex">
        ///     max vertex of convex polygon.<br />
        ///     Specify the maximum number of vertices when you want to change the number of vertices that can be divided.<br />
        ///     If the maximum number of vertices is less than the value of maxVertex in srcConvexPolygon, it is ignored.<br />
        /// </param>
        /// <param name="positionInWorldSpaceBuffer">Buffer of vertex position in world space.</param>
        /// <param name="normalInWorldSpaceBuffer">Buffer of vertex normal in world space.</param>
        /// <param name="boneWeightBuffer">Buffer of vertex bone weights.</param>
        /// <param name="lineBuffer">Buffer of lines of convex polygons.</param>
        /// <param name="positionInModelSpaceBuffer">Buffer of vertex position in model space.</param>
        /// <param name="normalInModelSpaceBuffer">Buffer of vertex normal in model space.</param>
        public ConvexPolygon(ConvexPolygon srcConvexPolygon, Vector3[] positionInWorldSpaceBuffer,
            Vector3[] normalInWorldSpaceBuffer, BoneWeight[] boneWeightBuffer, Line[] lineBuffer,
            Vector3[] positionInModelSpaceBuffer, Vector3[] normalInModelSpaceBuffer, int startOffsetInBuffer,
            int maxVertex = DefaultMaxVertex)
        {
            ReceiverMeshRenderer = srcConvexPolygon.ReceiverMeshRenderer;
            VertexCount = srcConvexPolygon.VertexCount;

            _isSkinnedMeshRenderer = srcConvexPolygon._isSkinnedMeshRenderer;
            _maxVertex = Mathf.Max(maxVertex, srcConvexPolygon._maxVertex);
            _rendererNo = srcConvexPolygon._rendererNo;
            _positionInModelSpaceBuffer = positionInModelSpaceBuffer;
            _normalInModelSpaceBuffer = normalInModelSpaceBuffer;
            _positionInWorldSpaceBuffer = positionInWorldSpaceBuffer;
            _normalInWorldSpaceBuffer = normalInWorldSpaceBuffer;
            _boneWeightBuffer = boneWeightBuffer;
            _lineBuffer = lineBuffer;
            _startOffsetInBuffer = startOffsetInBuffer;

            Array.Copy(srcConvexPolygon._positionInWorldSpaceBuffer, srcConvexPolygon._startOffsetInBuffer,
                _positionInWorldSpaceBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);
            Array.Copy(srcConvexPolygon._normalInWorldSpaceBuffer, srcConvexPolygon._startOffsetInBuffer,
                _normalInWorldSpaceBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);

            Array.Copy(srcConvexPolygon._positionInModelSpaceBuffer, srcConvexPolygon._startOffsetInBuffer,
                _positionInModelSpaceBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);
            Array.Copy(srcConvexPolygon._normalInModelSpaceBuffer, srcConvexPolygon._startOffsetInBuffer,
                _normalInModelSpaceBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);

            Array.Copy(srcConvexPolygon._boneWeightBuffer, srcConvexPolygon._startOffsetInBuffer,
                _boneWeightBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);
            Array.Copy(srcConvexPolygon._lineBuffer, srcConvexPolygon._startOffsetInBuffer,
                _lineBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);

            _faceNormal = srcConvexPolygon._faceNormal;
        }

        public Vector3 FaceNormal => _faceNormal;
        public int VertexCount { get; private set; }

        /// <summary>
        ///     The receiver mesh renderer which the decal mesh will be attached.
        /// </summary>
        public Renderer ReceiverMeshRenderer { get; }

        private void CalculateNewVertexDataBySplitPlane(
            out Vector3 newVert0,
            out Vector3 newVert1,
            out Vector3 newNormal0,
            out Vector3 newNormal1,
            out BoneWeight newBoneWeight0,
            out BoneWeight newBoneWeight1,
            out Vector3 newLocalVert0,
            out Vector3 newLocalVert1,
            out Vector3 newLocalNormal0,
            out Vector3 newLocalNormal1,
            Line l0,
            Line l1,
            Vector4 clipPlane)
        {
            var t = Vector4.Dot(clipPlane, Vector3ToVector4(l0.EndPosition))
                    / Vector4.Dot(clipPlane, l0.StartToEndVec);
            newVert0 = Vector3.Lerp(l0.EndPosition, l0.StartPosition, t);
            newNormal0 = Vector3.Lerp(l0.EndNormal, l0.StartNormal, t);
            newNormal0.Normalize();

            newLocalVert0 = Vector3.Lerp(l0.EndLocalPosition, l0.StartLocalPosition, t);
            newLocalNormal0 = Vector3.Lerp(l0.EndLocalNormal, l0.StartLocalNormal, t);
            newLocalNormal0.Normalize();

            t = Vector4.Dot(clipPlane, Vector3ToVector4(l1.StartPosition))
                / Vector4.Dot(clipPlane, l1.StartPosition - l1.EndPosition);

            newVert1 = Vector3.Lerp(l1.StartPosition, l1.EndPosition, t);
            newNormal1 = Vector3.Lerp(l1.StartNormal, l1.EndNormal, t);
            newNormal1.Normalize();

            newLocalVert1 = Vector3.Lerp(l1.StartLocalPosition, l1.EndLocalPosition, t);
            newLocalNormal1 = Vector3.Lerp(l1.StartLocalNormal, l1.EndLocalNormal, t);
            newLocalNormal1.Normalize();

            newBoneWeight0 = new BoneWeight();
            newBoneWeight1 = new BoneWeight();

            newBoneWeight0 = l0.StartWeight;
            newBoneWeight1 = l1.EndWeight;

            newBoneWeight0.weight0 = l0.StartWeight.boneIndex0 == l0.EndWeight.boneIndex0
                ? Mathf.Lerp(l0.EndWeight.weight0, l0.StartWeight.weight0, t)
                : l0.StartWeight.weight0;

            newBoneWeight0.weight1 = l0.StartWeight.boneIndex1 == l0.EndWeight.boneIndex1
                ? Mathf.Lerp(l0.EndWeight.weight1, l0.StartWeight.weight1, t)
                : l0.StartWeight.weight1;

            newBoneWeight0.weight2 = l0.StartWeight.boneIndex2 == l0.EndWeight.boneIndex2
                ? Mathf.Lerp(l0.EndWeight.weight2, l0.StartWeight.weight2, t)
                : l0.StartWeight.weight2;

            newBoneWeight0.weight3 = l0.StartWeight.boneIndex3 == l0.EndWeight.boneIndex3
                ? Mathf.Lerp(l0.EndWeight.weight3, l0.StartWeight.weight3, t)
                : l0.StartWeight.weight3;

            newBoneWeight0.boneIndex0 = l0.StartWeight.boneIndex0;
            newBoneWeight0.boneIndex1 = l0.StartWeight.boneIndex1;
            newBoneWeight0.boneIndex2 = l0.StartWeight.boneIndex2;
            newBoneWeight0.boneIndex3 = l0.StartWeight.boneIndex3;

            newBoneWeight1.weight0 = l1.StartWeight.boneIndex0 == l1.EndWeight.boneIndex0
                ? Mathf.Lerp(l1.StartWeight.weight0, l1.EndWeight.weight0, t)
                : l1.EndWeight.weight0;

            newBoneWeight1.weight1 = l1.StartWeight.boneIndex1 == l1.EndWeight.boneIndex1
                ? Mathf.Lerp(l1.StartWeight.weight1, l1.EndWeight.weight1, t)
                : l1.EndWeight.weight1;

            newBoneWeight1.weight2 = l1.StartWeight.boneIndex2 == l1.EndWeight.boneIndex2
                ? Mathf.Lerp(l1.StartWeight.weight2, l1.EndWeight.weight2, t)
                : l1.EndWeight.weight2;

            newBoneWeight1.weight3 = l1.StartWeight.boneIndex3 == l1.EndWeight.boneIndex3
                ? Mathf.Lerp(l1.StartWeight.weight3, l1.EndWeight.weight3, t)
                : l1.EndWeight.weight3;

            newBoneWeight1.boneIndex0 = l1.EndWeight.boneIndex0;
            newBoneWeight1.boneIndex1 = l1.EndWeight.boneIndex1;
            newBoneWeight1.boneIndex2 = l1.EndWeight.boneIndex2;
            newBoneWeight1.boneIndex3 = l1.EndWeight.boneIndex3;

            // Normalize bone weights.
            var total = newBoneWeight0.weight0 + newBoneWeight0.weight1 + newBoneWeight0.weight2 +
                        newBoneWeight0.weight3;
            if (total > 0.0f)
            {
                newBoneWeight0.weight0 /= total;
                newBoneWeight0.weight1 /= total;
                newBoneWeight0.weight2 /= total;
                newBoneWeight0.weight3 /= total;
            }

            total = newBoneWeight1.weight0 + newBoneWeight1.weight1 + newBoneWeight1.weight2 +
                    newBoneWeight1.weight3;
            if (total > 0.0f)
            {
                newBoneWeight1.weight0 /= total;
                newBoneWeight1.weight1 /= total;
                newBoneWeight1.weight2 /= total;
                newBoneWeight1.weight3 /= total;
            }
        }

        /// <summary>
        ///     Get the real vertex no from the virtual vertex no.
        /// </summary>
        /// <remarks>
        ///     If you get the 0th vertex number, call GetRealVertexNo( 0 ),
        ///     and the function will return a real vertex number.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetRealVertexNo(int virtualVertNo)
        {
            Assert.IsTrue(virtualVertNo < _maxVertex, "The vertex number is over. MaxVertex should be checked.");
            return _startOffsetInBuffer + virtualVertNo;
        }

        /// <summary>
        ///     Get the vertex position in world space from the buffer.
        /// </summary>
        /// <param name="vertNo">
        ///     real vertex no.
        ///     This value should be obtained using the GetRealVertexNo function.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVertexPositionInWorldSpace(int vertNo)
        {
            return _positionInWorldSpaceBuffer[vertNo];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetVertexPositionInWorldSpace(int vertNo, Vector3 position)
        {
            _positionInWorldSpaceBuffer[vertNo] = position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyVertexPositionInWorldSpace(int destVertNo, int srcVertNo)
        {
            _positionInWorldSpaceBuffer[destVertNo] = _positionInWorldSpaceBuffer[srcVertNo];
        }

        /// <summary>
        ///     Get the vertex position in the model space.
        /// </summary>
        /// <param name="vertNo">
        ///     Real vertex no.
        ///     This value should be obtained using the GetRealVertexNo function.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVertexPositionInModelSpace(int vertNo)
        {
            return _positionInModelSpaceBuffer[vertNo];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetVertexPositionInModelSpace(int vertNo, Vector3 position)
        {
            _positionInModelSpaceBuffer[vertNo] = position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyVertexPositionInModelSpace(int destVertNo, int srcVertNo)
        {
            _positionInModelSpaceBuffer[destVertNo] = _positionInModelSpaceBuffer[srcVertNo];
        }

        /// <summary>
        ///     Get the vertex normal in the world space.
        /// </summary>
        /// <param name="vertNo">
        ///     Real vertex no.
        ///     This value should be obtained using the GetRealVertexNo function.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVertexNormalInWorldSpace(int vertNo)
        {
            return _normalInWorldSpaceBuffer[vertNo];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetVertexNormalInWorldSpace(int vertNo, Vector3 normal)
        {
            _normalInWorldSpaceBuffer[vertNo] = normal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyVertexNormalInWorldSpace(int destVertNo, int srcVertNo)
        {
            _normalInWorldSpaceBuffer[destVertNo] = _normalInWorldSpaceBuffer[srcVertNo];
        }

        /// <summary>
        ///     Get the vertex normal in the model space.
        /// </summary>
        /// <param name="vertNo">
        ///     Real vertex no.
        ///     This value should be obtained using the GetRealVertexNo function.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVertexNormalInModelSpace(int vertNo)
        {
            return _normalInModelSpaceBuffer[vertNo];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetVertexNormalInModelSpace(int vertNo, Vector3 normal)
        {
            _normalInModelSpaceBuffer[vertNo] = normal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyVertexNormalInModelSpace(int destVertNo, int srcVertNo)
        {
            _normalInModelSpaceBuffer[destVertNo] = _normalInModelSpaceBuffer[srcVertNo];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Line GetLine(int startVertNo)
        {
            return _lineBuffer[startVertNo];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref Line GetLineRef(int startVertNo)
        {
            return ref _lineBuffer[startVertNo];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyLine(int destStartVertNo, int srcStartVertNo)
        {
            _lineBuffer[destStartVertNo] = _lineBuffer[srcStartVertNo];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 Vector3ToVector4(Vector3 v)
        {
            return new Vector4(v.x, v.y, v.z, 1.0f);
        }

        /// <summary>
        ///     Split and remove to convex polygon by plane.
        /// </summary>
        /// <remarks>
        ///     If a convex polygon can be divided by a plane, the division process is performed to create a new convex polygon.
        ///     Also, vertices outside (on the negative side) of the plane are discarded in this case.
        ///     For example, if a triangle is divided by a plane, it will be divided into a quadrangle and a triangle, but In this
        ///     case,
        ///     information about either of the polygons (polygons outside the plane) will be lost after the division.
        ///     Also, set allVertexIsOutside to true if all vertices that make up the convex polygon are outside the plane.
        /// </remarks>
        /// <param name="clipPlane"></param>
        /// <param name="allVertexIsOutside">If all vertices of the convex polygon are outside the plane, True is set.</param>
        public void SplitAndRemoveByPlane(Vector4 clipPlane, out bool allVertexIsOutside)
        {
            allVertexIsOutside = false;

            var numOutsideVertex = 0;
            var removeVertStartNo = -1;
            var removeVertEndNo = 0;
            var remainVertStartNo = -1;
            var remainVertEndNo = 0;
            for (var no = 0; no < VertexCount; no++)
            {
                var t = Vector4.Dot(clipPlane, Vector3ToVector4(GetVertexPositionInWorldSpace(GetRealVertexNo(no))));
                if (t < 0)
                {
                    // outside.
                    if (removeVertStartNo == -1) removeVertStartNo = no;

                    removeVertEndNo = no;
                    numOutsideVertex++;
                }
                else
                {
                    // inside
                    if (remainVertStartNo == -1) remainVertStartNo = no;

                    remainVertEndNo = no;
                }
            }

            if (numOutsideVertex == VertexCount)
            {
                // Since all vertices are outside the clip plane, no division can be performed.
                allVertexIsOutside = true;
                return;
            }

            if (numOutsideVertex == 0)
                // Since all vertices are inside the clip plane, no division can be performed.
                return;

            // Split processing from here.
            // Two new vertices are added at the intersection of the polygon's edges and planes.
            // Also, since vertices outside the plane are excluded, the increase/decrease value of the polygon's vertices is 2 - numOutsideVertex. 
            var deltaVerticesSize = 2 - numOutsideVertex;

            if (removeVertStartNo == 0)
            {
                // Remove the 0th vertex.
                // Two line 
                // Back up information on two lines that intersect with a plane.
                var l0 = GetLine(GetRealVertexNo(remainVertStartNo - 1));
                var l1 = GetLine(GetRealVertexNo(remainVertEndNo));
                // Pack the remaining vertex forward.
                var vertNo = 0;
                for (var i = remainVertStartNo; i < remainVertEndNo + 1; i++)
                {
                    var destVertNo = GetRealVertexNo(vertNo);
                    var srcVertNo = GetRealVertexNo(i);
                    CopyVertexPositionInWorldSpace(destVertNo, srcVertNo);
                    CopyVertexNormalInWorldSpace(destVertNo, srcVertNo);
                    CopyVertexPositionInModelSpace(destVertNo, srcVertNo);
                    CopyVertexNormalInModelSpace(destVertNo, srcVertNo);
                    CopyVertexBoneWeight(destVertNo, srcVertNo);
                    CopyLine(destVertNo, srcVertNo);

                    vertNo++;
                }

                CalculateNewVertexDataBySplitPlane(
                    out var newVert0,
                    out var newVert1,
                    out var newNormal0,
                    out var newNormal1,
                    out var newBoneWeight0,
                    out var newBoneWeight1,
                    out var newLocalVert0,
                    out var newLocalVert1,
                    out var newLocalNormal0,
                    out var newLocalNormal1,
                    l1,
                    l0,
                    clipPlane
                );

                // Added two vertex.
                var newVertNo0_local = vertNo;
                var newVertNo1_local = vertNo + 1;

                var newVertNo0 = GetRealVertexNo(newVertNo0_local);
                var newVertNo1 = GetRealVertexNo(newVertNo1_local);
                SetVertexPositionInWorldSpace(newVertNo0, newVert0);
                SetVertexPositionInWorldSpace(newVertNo1, newVert1);

                SetVertexNormalInWorldSpace(newVertNo0, newNormal0);
                SetVertexNormalInWorldSpace(newVertNo1, newNormal1);

                SetVertexPositionInModelSpace(newVertNo0, newLocalVert0);
                SetVertexPositionInModelSpace(newVertNo1, newLocalVert1);

                SetVertexNormalInModelSpace(newVertNo0, newLocalNormal0);
                SetVertexNormalInModelSpace(newVertNo1, newLocalNormal1);

                SetVertexBoneWeight(newVertNo0, newBoneWeight0);
                SetVertexBoneWeight(newVertNo1, newBoneWeight1);

                // Build the lines.
                VertexCount += deltaVerticesSize;
                ref var line_0 = ref GetLineRef(GetRealVertexNo(newVertNo0_local - 1));
                ref var line_1 = ref GetLineRef(newVertNo0);
                ref var line_2 = ref GetLineRef(newVertNo1);
                line_0.SetEndAndCalcStartToEnd(newVert0, newNormal0,
                    newLocalVert0, newLocalNormal0);
                line_1.SetStartEndAndCalcStartToEnd(
                    newVert0,
                    newVert1,
                    newNormal0,
                    newNormal1,
                    newLocalVert0,
                    newLocalVert1,
                    newLocalNormal0,
                    newLocalNormal1);
                var endVertNo1 = GetRealVertexNo((newVertNo1_local + 1) % VertexCount);
                line_2.SetStartEndAndCalcStartToEnd(
                    newVert1,
                    GetVertexPositionInWorldSpace(endVertNo1),
                    newNormal1,
                    GetVertexNormalInWorldSpace(endVertNo1),
                    newLocalVert1,
                    GetVertexPositionInModelSpace(endVertNo1),
                    newLocalNormal1,
                    GetVertexNormalInModelSpace(endVertNo1));

                line_0.SetEndBoneWeight(newBoneWeight0);
                line_1.SetStartEndBoneWeights(newBoneWeight0, newBoneWeight1);
                line_2.SetStartEndBoneWeights(
                    newBoneWeight1,
                    GetVertexBoneWeight(endVertNo1));
            }
            else
            {
                // Remove non 0th vertex.
                // Back up information on two lines that intersect with a plane.
                var l0 = GetLine(GetRealVertexNo(removeVertStartNo - 1));
                var l1 = GetLine(GetRealVertexNo(removeVertEndNo));
                if (deltaVerticesSize > 0)
                    // The vertex increases.
                    for (var i = VertexCount - 1; i > removeVertEndNo; i--)
                    {
                        var destVertNo = GetRealVertexNo(i + deltaVerticesSize);
                        var srcVertNo = GetRealVertexNo(i);
                        CopyVertexPositionInWorldSpace(destVertNo, srcVertNo);
                        CopyVertexNormalInWorldSpace(destVertNo, srcVertNo);
                        CopyVertexPositionInModelSpace(destVertNo, srcVertNo);
                        CopyVertexNormalInModelSpace(destVertNo, srcVertNo);
                        CopyVertexBoneWeight(destVertNo, srcVertNo);
                        CopyLine(destVertNo, srcVertNo);
                    }
                else
                    // The vertex decrease or stays the same.
                    for (var i = removeVertEndNo + 1; i < VertexCount; i++)
                    {
                        var destVertNo = GetRealVertexNo(i + deltaVerticesSize);
                        var srcVertNo = GetRealVertexNo(i);

                        CopyVertexPositionInWorldSpace(destVertNo, srcVertNo);
                        CopyVertexNormalInWorldSpace(destVertNo, srcVertNo);
                        CopyVertexPositionInModelSpace(destVertNo, srcVertNo);
                        CopyVertexNormalInModelSpace(destVertNo, srcVertNo);
                        CopyVertexBoneWeight(destVertNo, srcVertNo);
                        CopyLine(destVertNo, srcVertNo);
                    }

                // Add two vertex.
                CalculateNewVertexDataBySplitPlane(
                    out var newVert0,
                    out var newVert1,
                    out var newNormal0,
                    out var newNormal1,
                    out var newBoneWeight0,
                    out var newBoneWeight1,
                    out var newLocalVert0,
                    out var newLocalVert1,
                    out var newLocalNormal0,
                    out var newLocalNormal1,
                    l0,
                    l1,
                    clipPlane);
                var newVertNo0_local = removeVertStartNo;
                var newVertNo1_local = removeVertStartNo + 1;

                var newVertNo0 = GetRealVertexNo(newVertNo0_local);
                var newVertNo1 = GetRealVertexNo(newVertNo1_local);
                SetVertexPositionInWorldSpace(newVertNo0, newVert0);
                SetVertexPositionInWorldSpace(newVertNo1, newVert1);

                SetVertexNormalInWorldSpace(newVertNo0, newNormal0);
                SetVertexNormalInWorldSpace(newVertNo1, newNormal1);

                SetVertexPositionInModelSpace(newVertNo0, newLocalVert0);
                SetVertexPositionInModelSpace(newVertNo1, newLocalVert1);

                SetVertexNormalInModelSpace(newVertNo0, newLocalNormal0);
                SetVertexNormalInModelSpace(newVertNo1, newLocalNormal1);

                SetVertexBoneWeight(newVertNo0, newBoneWeight0);
                SetVertexBoneWeight(newVertNo1, newBoneWeight1);

                // Build the lines.
                VertexCount += deltaVerticesSize;
                ref var line_0 = ref GetLineRef(GetRealVertexNo(newVertNo0_local - 1));
                ref var line_1 = ref GetLineRef(newVertNo0);
                ref var line_2 = ref GetLineRef(newVertNo1);
                line_0.SetEndAndCalcStartToEnd(newVert0, newNormal0,
                    newLocalVert0, newLocalNormal0);
                line_1.SetStartEndAndCalcStartToEnd(
                    newVert0,
                    newVert1,
                    newNormal0,
                    newNormal1,
                    newLocalVert0,
                    newLocalVert1,
                    newLocalNormal0,
                    newLocalNormal1);

                var endVertNo1_Direct = GetRealVertexNo((newVertNo1_local + 1) % VertexCount);
                line_2.SetStartEndAndCalcStartToEnd(
                    newVert1,
                    GetVertexPositionInWorldSpace(endVertNo1_Direct),
                    newNormal1,
                    GetVertexNormalInWorldSpace(endVertNo1_Direct),
                    newLocalVert1,
                    GetVertexPositionInModelSpace(endVertNo1_Direct),
                    newLocalNormal1,
                    GetVertexNormalInModelSpace(endVertNo1_Direct));

                line_0.SetEndBoneWeight(newBoneWeight0);
                line_1.SetStartEndBoneWeights(newBoneWeight0, newBoneWeight1);
                line_2.SetStartEndBoneWeights(
                    newBoneWeight1,
                    GetVertexBoneWeight(endVertNo1_Direct));
            }
        }

        public bool IsIntersectRayToTriangle(out Vector3 hitPoint, Vector3 rayStartPos, Vector3 rayEndPos)
        {
            hitPoint = Vector3.zero;
            if (VertexCount != 3) return false;
            var vertNo0 = GetRealVertexNo(0);
            var vertNo1 = GetRealVertexNo(1);
            var vertNo2 = GetRealVertexNo(2);
            var v0Pos = GetVertexPositionInWorldSpace(vertNo0);
            var v1Pos = GetVertexPositionInWorldSpace(vertNo1);
            var v2Pos = GetVertexPositionInWorldSpace(vertNo2);

            // Check to intersect plane to ray.
            var v0ToRayStart = rayStartPos - v0Pos;
            var v0ToRayEnd = rayEndPos - v0Pos;
            var v0ToRayStartNorm = v0ToRayStart.normalized;
            var v0ToRayEndNorm = v0ToRayEnd.normalized;
            var t = Vector3.Dot(v0ToRayStartNorm, FaceNormal)
                    * Vector3.Dot(v0ToRayEndNorm, FaceNormal);
            if (t < 0.0f)
            {
                // Hit.
                // Next Calculate hit point.
                var t0 = Mathf.Abs(Vector3.Dot(v0ToRayStart, FaceNormal));
                var t1 = Mathf.Abs(Vector3.Dot(v0ToRayEnd, FaceNormal));
                var intersectPoint = Vector3.Lerp(rayStartPos, rayEndPos, t0 / (t0 + t1));
                // Check to hit point is inside the triangle.
                var v0ToIntersectPos = intersectPoint - v0Pos;
                var v1ToIntersectPos = intersectPoint - v1Pos;
                var v2ToIntersectPos = intersectPoint - v2Pos;
                v0ToIntersectPos.Normalize();
                v1ToIntersectPos.Normalize();
                v2ToIntersectPos.Normalize();
                var v0ToV1 = GetLineRef(vertNo0).StartToEndVec;
                var v1ToV2 = GetLineRef(vertNo1).StartToEndVec;
                var v2ToV0 = GetLineRef(vertNo2).StartToEndVec;
                v0ToV1.Normalize();
                v1ToV2.Normalize();
                v2ToV0.Normalize();

                var a0 = Vector3.Cross(v0ToV1, v0ToIntersectPos);
                var a1 = Vector3.Cross(v1ToV2, v1ToIntersectPos);
                var a2 = Vector3.Cross(v2ToV0, v2ToIntersectPos);
                a0.Normalize();
                a1.Normalize();
                a2.Normalize();

                if (Vector3.Dot(a0, a1) > 0.0f
                    && Vector3.Dot(a0, a2) > 0.0f)
                {
                    // Since the hit point is inside, intersected is determined
                    hitPoint = intersectPoint;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Get the vertex bone weight from the buffer.
        /// </summary>
        /// <param name="vertNo">
        ///     Real vertex no.
        ///     This value should be obtained using the GetRealVertexNo function.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BoneWeight GetVertexBoneWeight(int vertNo)
        {
            return _boneWeightBuffer[vertNo];
        }

        /// <summary>
        ///     Set the vertex bone weight to the buffer.
        /// </summary>
        /// <param name="vertNo">
        ///     Real vertex no.
        ///     This value should be obtained using the GetRealVertexNo function.
        /// </param>
        /// <param name="boneWeight"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetVertexBoneWeight(int vertNo, BoneWeight boneWeight)
        {
            _boneWeightBuffer[vertNo] = boneWeight;
        }

        /// <summary>
        ///     Copy the vertex bone weight to buffer.
        /// </summary>
        /// <param name="destVertNo">
        ///     the vertex no of destination.
        ///     real vertex no.
        ///     This value should be obtained using the GetRealVertexNo function.
        /// </param>
        /// <param name="srcVertNo">
        ///     the vertex no of source.
        ///     real vertex no.
        ///     This value should be obtained using the GetRealVertexNo function.
        /// </param>
        private void CopyVertexBoneWeight(int destVertNo, int srcVertNo)
        {
            _boneWeightBuffer[destVertNo] = _boneWeightBuffer[srcVertNo];
        }

        private static void Multiply(ref Matrix4x4 mOut, Matrix4x4 m, float s)
        {
            mOut = m;
            mOut.m00 *= s;
            mOut.m01 *= s;
            mOut.m02 *= s;
            mOut.m03 *= s;

            mOut.m10 *= s;
            mOut.m11 *= s;
            mOut.m12 *= s;
            mOut.m13 *= s;

            mOut.m20 *= s;
            mOut.m21 *= s;
            mOut.m22 *= s;
            mOut.m23 *= s;

            mOut.m30 *= s;
            mOut.m31 *= s;
            mOut.m32 *= s;
            mOut.m33 *= s;
        }

        private static void MultiplyAdd(ref Matrix4x4 mOut, Matrix4x4 m, float s)
        {
            mOut.m00 += m.m00 * s;
            mOut.m01 += m.m01 * s;
            mOut.m02 += m.m02 * s;
            mOut.m03 += m.m03 * s;

            mOut.m10 += m.m10 * s;
            mOut.m11 += m.m11 * s;
            mOut.m12 += m.m12 * s;
            mOut.m13 += m.m13 * s;

            mOut.m20 += m.m20 * s;
            mOut.m21 += m.m21 * s;
            mOut.m22 += m.m22 * s;
            mOut.m23 += m.m23 * s;

            mOut.m30 += m.m30 * s;
            mOut.m31 += m.m31 * s;
            mOut.m32 += m.m32 * s;
            mOut.m33 += m.m33 * s;
        }

        /// <summary>
        ///     Prepare to run on worker threads.
        /// </summary>
        /// <remarks>
        ///     Some unity APIs aren't working on the worker threads.
        ///     Therefore, cache the necessary data in the worker thread.
        /// </remarks>
        public void PrepareToRunOnWorkerThread()
        {
            _localToWorldMatrix = ReceiverMeshRenderer.localToWorldMatrix;
            if (_isSkinnedMeshRenderer)
            {
                var skinnedMeshRenderer = ReceiverMeshRenderer as SkinnedMeshRenderer;
                _existsRootBone = skinnedMeshRenderer.rootBone != null;
            }
            else
            {
                _existsRootBone = false;
            }
        }

        public void CalculatePositionsAndNormalsInWorldSpace(Matrix4x4[][] boneMatricesPallet,
            Matrix4x4[] localToWorldMatrices, BoneWeight[] boneWeights)
        {
            var vertNo0 = GetRealVertexNo(0);
            var vertNo1 = GetRealVertexNo(1);
            var vertNo2 = GetRealVertexNo(2);
            if (_isSkinnedMeshRenderer)
            {
                if (_existsRootBone)
                {
                    boneWeights[0] = GetVertexBoneWeight(vertNo0);
                    boneWeights[1] = GetVertexBoneWeight(vertNo1);
                    boneWeights[2] = GetVertexBoneWeight(vertNo2);

                    var boneMatrices = boneMatricesPallet[_rendererNo];
                    Multiply(ref localToWorldMatrices[0],
                        boneMatrices[boneWeights[0].boneIndex0],
                        boneWeights[0].weight0);
                    MultiplyAdd(
                        ref localToWorldMatrices[0],
                        boneMatrices[boneWeights[0].boneIndex1],
                        boneWeights[0].weight1);
                    MultiplyAdd(
                        ref localToWorldMatrices[0],
                        boneMatrices[boneWeights[0].boneIndex2],
                        boneWeights[0].weight2);
                    MultiplyAdd(
                        ref localToWorldMatrices[0],
                        boneMatrices[boneWeights[0].boneIndex3],
                        boneWeights[0].weight3);

                    Multiply(ref localToWorldMatrices[1],
                        boneMatrices[boneWeights[1].boneIndex0],
                        boneWeights[1].weight0);
                    MultiplyAdd(
                        ref localToWorldMatrices[1],
                        boneMatrices[boneWeights[1].boneIndex1],
                        boneWeights[1].weight1);
                    MultiplyAdd(
                        ref localToWorldMatrices[1],
                        boneMatrices[boneWeights[1].boneIndex2],
                        boneWeights[1].weight2);
                    MultiplyAdd(
                        ref localToWorldMatrices[1],
                        boneMatrices[boneWeights[1].boneIndex3],
                        boneWeights[1].weight3);
                    Multiply(ref localToWorldMatrices[2],
                        boneMatrices[boneWeights[2].boneIndex0],
                        boneWeights[2].weight0);
                    MultiplyAdd(
                        ref localToWorldMatrices[2],
                        boneMatrices[boneWeights[2].boneIndex1],
                        boneWeights[2].weight1);
                    MultiplyAdd(
                        ref localToWorldMatrices[2],
                        boneMatrices[boneWeights[2].boneIndex2],
                        boneWeights[2].weight2);
                    MultiplyAdd(
                        ref localToWorldMatrices[2],
                        boneMatrices[boneWeights[2].boneIndex3],
                        boneWeights[2].weight3);
                }
                else
                {
                    localToWorldMatrices[0] = _localToWorldMatrix;
                    localToWorldMatrices[1] = _localToWorldMatrix;
                    localToWorldMatrices[2] = _localToWorldMatrix;
                }
            }
            else
            {
                localToWorldMatrices[0] = _localToWorldMatrix;
                localToWorldMatrices[1] = _localToWorldMatrix;
                localToWorldMatrices[2] = _localToWorldMatrix;
            }

            SetVertexPositionInWorldSpace(vertNo0,
                localToWorldMatrices[0].MultiplyPoint3x4(GetVertexPositionInModelSpace(vertNo0)));
            SetVertexPositionInWorldSpace(vertNo1,
                localToWorldMatrices[1].MultiplyPoint3x4(GetVertexPositionInModelSpace(vertNo1)));
            SetVertexPositionInWorldSpace(vertNo2,
                localToWorldMatrices[2].MultiplyPoint3x4(GetVertexPositionInModelSpace(vertNo2)));

            SetVertexNormalInWorldSpace(vertNo0,
                localToWorldMatrices[0].MultiplyVector(GetVertexNormalInModelSpace(vertNo0)));
            SetVertexNormalInWorldSpace(vertNo1,
                localToWorldMatrices[1].MultiplyVector(GetVertexNormalInModelSpace(vertNo1)));
            SetVertexNormalInWorldSpace(vertNo2,
                localToWorldMatrices[2].MultiplyVector(GetVertexNormalInModelSpace(vertNo2)));

            for (var virtualVertNo = 0; virtualVertNo < VertexCount; virtualVertNo++)
            {
                var startVertNo = GetRealVertexNo(virtualVertNo);
                var nextVertNo = GetRealVertexNo((virtualVertNo + 1) % VertexCount);
                ref var line = ref GetLineRef(startVertNo);
                line.Initialize(
                    GetVertexPositionInWorldSpace(startVertNo),
                    GetVertexPositionInWorldSpace(nextVertNo),
                    GetVertexNormalInWorldSpace(startVertNo),
                    GetVertexNormalInWorldSpace(nextVertNo),
                    GetVertexBoneWeight(startVertNo),
                    GetVertexBoneWeight(nextVertNo),
                    GetVertexPositionInModelSpace(startVertNo),
                    GetVertexPositionInModelSpace(nextVertNo),
                    GetVertexNormalInModelSpace(startVertNo),
                    GetVertexNormalInModelSpace(nextVertNo));
            }

            _faceNormal = Vector3.Cross(
                GetVertexPositionInWorldSpace(vertNo1) - GetVertexPositionInWorldSpace(vertNo0),
                GetVertexPositionInWorldSpace(vertNo2) - GetVertexPositionInWorldSpace(vertNo0));
            _faceNormal.Normalize();
        }
    }
}
