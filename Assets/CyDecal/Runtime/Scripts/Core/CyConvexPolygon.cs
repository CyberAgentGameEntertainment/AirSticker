using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     凸多角形ポリゴン
    /// </summary>
    public sealed class CyConvexPolygon
    {
        public const int DefaultMaxVertex = 64;
        private readonly BoneWeight[] _boneWeightBuffer; // ボーンウェイトバッファ
        private readonly CyLine[] _lineBuffer; // 凸多角形を構成するエッジ情報のバッファ
        private readonly Vector3[] _localNormalBuffer;
        private readonly Vector3[] _localPositionBuffer;
        private readonly int _maxVertex; //  凸多角形の最大頂点
        private readonly Vector3[] _normalBuffer; // 頂点法線バッファ
        private readonly Vector3[] _positionBuffer; // 頂点座標バッファ
        private readonly int _startOffsetInBuffer;
        private bool _existsRootBone;
        private Vector3 _faceNormal; // 面法線
        private readonly bool _isSkinnedMeshRenderer;

        private Matrix4x4 _localToWorldMatrix;
        private readonly int _rendererNo;

        /// <summary>
        ///     コンストラクタ
        /// </summary>
        /// <param name="positionBuffer">頂点の座標バッファ</param>
        /// <param name="normalBuffer">頂点の法線バッファ</param>
        /// <param name="boneWeightBuffer">頂点のボーンウェイトバッファ</param>
        /// <param name="lineBuffer">多角形の辺を表すラインのバッファ</param>
        /// <param name="receiverMeshRenderer">デカールメッシュを貼り付けメッシュのレンダラ</param>
        /// <param name="startOffsetInBuffer">各種バッファ内での開始オフセット。この変数の値がこの多角形ポリゴンの頂点情報が格納されている開始位置です。</param>
        /// <param name="initVertexCount">凸多角形の初期の頂点数</param>
        /// <param name="maxVertex">凸多角形の最大頂点数。この引数の値まで凸多角形を分割できます。指定されていない場合はDefaltMaxVertexの値が最大頂点数になります。</param>
        public CyConvexPolygon(
            Vector3[] positionBuffer,
            Vector3[] normalBuffer,
            BoneWeight[] boneWeightBuffer,
            CyLine[] lineBuffer,
            Vector3[] localPositionBuffer,
            Vector3[] localNormalBuffer,
            Renderer receiverMeshRenderer,
            int startOffsetInBuffer,
            int initVertexCount,
            int rendererNo,
            int maxVertex = DefaultMaxVertex)
        {
            _isSkinnedMeshRenderer = receiverMeshRenderer is SkinnedMeshRenderer;
            _rendererNo = rendererNo;
            _localPositionBuffer = localPositionBuffer;
            _localNormalBuffer = localNormalBuffer;
            _maxVertex = maxVertex;
            _boneWeightBuffer = boneWeightBuffer;
            _positionBuffer = positionBuffer;
            _normalBuffer = normalBuffer;
            _lineBuffer = lineBuffer;
            _startOffsetInBuffer = startOffsetInBuffer;
            ReceiverMeshRenderer = receiverMeshRenderer;
            VertexCount = initVertexCount;
        }

        /// <summary>
        ///     コピーコンストラクタ
        /// </summary>
        /// <param name="srcConvexPolygon">コピー元となる凸多角形</param>
        /// <param name="maxVertex">
        ///     凸多角形の最大頂点数。分割可能頂点数を変更したい場合に最大頂点数を指定して下さい。
        ///     最大頂点数がsrcConvexPolygonのmaxVertexの値より小さい場合は無視されます。
        /// </param>
        public CyConvexPolygon(CyConvexPolygon srcConvexPolygon,
            Vector3[] positionBuffer,
            Vector3[] normalBuffer,
            BoneWeight[] boneWeightBuffer,
            CyLine[] lineBuffer,
            Vector3[] localPositionBuffer,
            Vector3[] localNormalBuffer,
            int startOffsetInBuffer,
            int maxVertex = DefaultMaxVertex)
        {
            ReceiverMeshRenderer = srcConvexPolygon.ReceiverMeshRenderer;
            VertexCount = srcConvexPolygon.VertexCount;

            _isSkinnedMeshRenderer = srcConvexPolygon._isSkinnedMeshRenderer;
            _maxVertex = Mathf.Max(maxVertex, srcConvexPolygon._maxVertex);
            _rendererNo = srcConvexPolygon._rendererNo;
            _localPositionBuffer = localPositionBuffer;
            _localNormalBuffer = localNormalBuffer;
            _positionBuffer = positionBuffer;
            _normalBuffer = normalBuffer;
            _boneWeightBuffer = boneWeightBuffer;
            _lineBuffer = lineBuffer;
            _startOffsetInBuffer = startOffsetInBuffer;

            Array.Copy(srcConvexPolygon._positionBuffer, srcConvexPolygon._startOffsetInBuffer,
                _positionBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);
            Array.Copy(srcConvexPolygon._normalBuffer, srcConvexPolygon._startOffsetInBuffer,
                _normalBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);

            Array.Copy(srcConvexPolygon._localPositionBuffer, srcConvexPolygon._startOffsetInBuffer,
                _localPositionBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);
            Array.Copy(srcConvexPolygon._localNormalBuffer, srcConvexPolygon._startOffsetInBuffer,
                _localNormalBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);

            Array.Copy(srcConvexPolygon._boneWeightBuffer, srcConvexPolygon._startOffsetInBuffer,
                _boneWeightBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);
            Array.Copy(srcConvexPolygon._lineBuffer, srcConvexPolygon._startOffsetInBuffer,
                _lineBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);

            _faceNormal = srcConvexPolygon._faceNormal;
        }

        /// <summary>
        ///     面法線プロパティ
        /// </summary>
        public Vector3 FaceNormal => _faceNormal;

        /// <summary>
        ///     頂点数プロパティ
        /// </summary>
        public int VertexCount { get; private set; }

        /// <summary>
        ///     デカールを貼り付けられるレシーバーメッシュのレンダラー。
        /// </summary>
        public Renderer ReceiverMeshRenderer { get; }

        /// <summary>
        ///     分割平面によって生成された新しい二つの頂点のデータを計算する。
        /// </summary>
        /// <param name="newVert0">頂点座標１の格納先</param>
        /// <param name="newVert1">頂点座標２の格納先</param>
        /// <param name="newNormal0">頂点法線１の格納先</param>
        /// <param name="newNormal1">頂点法線２の格納先</param>
        /// <param name="newBoneWeight0">ボーンウェイト１の格納先</param>
        /// <param name="newBoneWeight1">ボーンウェイト２の格納先</param>
        /// <param name="l0">分割されるライン０</param>
        /// <param name="l1">分割されるライン１</param>
        /// <param name="clipPlane">分割平面</param>
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
            CyLine l0,
            CyLine l1,
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

            // 重みを正規化
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
        ///     頂点番号からバッファ内のインデックスを取得します。
        /// </summary>
        /// <param name="vertNo">頂点番号</param>
        /// <returns>バッファ内インデックス</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetIndexInBufferFromVertexNo(int vertNo)
        {
            Assert.IsTrue(vertNo < _maxVertex, "The vertex number is over. MaxVertex should be checked.");
            return _startOffsetInBuffer + vertNo;
        }

        /// <summary>
        ///     頂点座標を取得。
        /// </summary>
        /// <param name="vertNo">頂点番号</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVertexPosition(int vertNo)
        {
            return _positionBuffer[GetIndexInBufferFromVertexNo(vertNo)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVertexPosition_Direct(int vertNo)
        {
            return _positionBuffer[vertNo];
        }

        /// <summary>
        ///     頂点座標を設定
        /// </summary>
        /// <param name="vertNo"></param>
        /// <param name="position"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetVertexPosition(int vertNo, Vector3 position)
        {
            _positionBuffer[GetIndexInBufferFromVertexNo(vertNo)] = position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetVertexPosition_Direct(int vertNo, Vector3 position)
        {
            _positionBuffer[vertNo] = position;
        }

        /// <summary>
        ///     頂点座標をコピー
        /// </summary>
        /// <param name="destVertNo">コピー先の頂点番号</param>
        /// <param name="srcVertNo">コピー元の頂点番号</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyVertexPosition(int destVertNo, int srcVertNo)
        {
            _positionBuffer[GetIndexInBufferFromVertexNo(destVertNo)] =
                _positionBuffer[GetIndexInBufferFromVertexNo(srcVertNo)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyVertexPosition_Direct(int destVertNo, int srcVertNo)
        {
            _positionBuffer[destVertNo] = _positionBuffer[srcVertNo];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVertexLocalPosition(int vertNo)
        {
            return _localPositionBuffer[GetIndexInBufferFromVertexNo(vertNo)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVertexLocalPosition_Direct(int vertNo)
        {
            return _localPositionBuffer[vertNo];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertexLocalPosition(int vertNo, Vector3 position)
        {
            _localPositionBuffer[GetIndexInBufferFromVertexNo(vertNo)] = position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertexLocalPosition_Direct(int vertNo, Vector3 position)
        {
            _localPositionBuffer[vertNo] = position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyVertexLocalPosition(int destVertNo, int srcVertNo)
        {
            _localPositionBuffer[GetIndexInBufferFromVertexNo(destVertNo)] =
                _localPositionBuffer[GetIndexInBufferFromVertexNo(srcVertNo)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyVertexLocalPosition_Direct(int destVertNo, int srcVertNo)
        {
            _localPositionBuffer[destVertNo] = _localPositionBuffer[srcVertNo];
        }

        /// <summary>
        ///     頂点法線を取得
        /// </summary>
        /// <param name="vertNo">頂点番号</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVertexNormal(int vertNo)
        {
            return _normalBuffer[GetIndexInBufferFromVertexNo(vertNo)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVertexNormal_Direct(int vertNo)
        {
            return _normalBuffer[vertNo];
        }

        /// <summary>
        ///     頂点法線を設定
        /// </summary>
        /// <param name="vertNo"></param>
        /// <param name="normal"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetVertexNormal(int vertNo, Vector3 normal)
        {
            _normalBuffer[GetIndexInBufferFromVertexNo(vertNo)] = normal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetVertexNormal_Direct(int vertNo, Vector3 normal)
        {
            _normalBuffer[vertNo] = normal;
        }

        /// <summary>
        ///     頂点法線をコピー
        /// </summary>
        /// <param name="destVertNo">コピー先の頂点番号</param>
        /// <param name="srcVertNo">コピー元の頂点番号</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyVertexNormal(int destVertNo, int srcVertNo)
        {
            _normalBuffer[GetIndexInBufferFromVertexNo(destVertNo)] =
                _normalBuffer[GetIndexInBufferFromVertexNo(srcVertNo)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyVertexNormal_Direct(int destVertNo, int srcVertNo)
        {
            _normalBuffer[destVertNo] = _normalBuffer[srcVertNo];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVertexLocalNormal(int vertNo)
        {
            return _localNormalBuffer[GetIndexInBufferFromVertexNo(vertNo)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVertexLocalNormal_Direct(int vertNo)
        {
            return _localNormalBuffer[vertNo];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetVertexLocalNormal(int vertNo, Vector3 normal)
        {
            _localNormalBuffer[GetIndexInBufferFromVertexNo(vertNo)] = normal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetVertexLocalNormal_Direct(int vertNo, Vector3 normal)
        {
            _localNormalBuffer[vertNo] = normal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyVertexLocalNormal(int destVertNo, int srcVertNo)
        {
            _localNormalBuffer[GetIndexInBufferFromVertexNo(destVertNo)] =
                _localNormalBuffer[GetIndexInBufferFromVertexNo(srcVertNo)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyVertexLocalNormal_Direct(int destVertNo, int srcVertNo)
        {
            _localNormalBuffer[destVertNo] = _localNormalBuffer[srcVertNo];
        }

        /// <summary>
        ///     頂点番号を指定してその頂点から伸びているラインを取得します。
        /// </summary>
        /// <param name="vertNo">頂点番号</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CyLine GetLine(int vertNo)
        {
            return _lineBuffer[GetIndexInBufferFromVertexNo(vertNo)];
        }

        /// <summary>
        ///     頂点番号を指定してその頂点から伸びているラインの参照を取得します。
        /// </summary>
        /// <param name="vertNo">頂点番号</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref CyLine GetLineRef(int vertNo)
        {
            return ref _lineBuffer[GetIndexInBufferFromVertexNo(vertNo)];
        }

        /// <summary>
        ///     指定された頂点から伸びているラインをコピーします。
        /// </summary>
        /// <param name="destVertNo">コピー先のラインの始点になる頂点の番号</param>
        /// <param name="srcVertNo">コピー元のラインの始点になる頂点の番号</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyLine(int destVertNo, int srcVertNo)
        {
            _lineBuffer[GetIndexInBufferFromVertexNo(destVertNo)] =
                _lineBuffer[GetIndexInBufferFromVertexNo(srcVertNo)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyLine_Direct(int destVertNo, int srcVertNo)
        {
            _lineBuffer[destVertNo] = _lineBuffer[srcVertNo];
        }

        /// <summary>
        ///     Vector3からVector4(w＝1)に変換します。
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 Vector3ToVector4(Vector3 v)
        {
            return new Vector4(v.x, v.y, v.z, 1.0f);
        }

        /// <summary>
        ///     凸多角形を平面で分割/削除する。
        /// </summary>
        /// <remarks>
        ///     平面により凸多角形を分割できる場合に分割処理を行い、新しい凸多角形を作成します。
        ///     また、この際に平面の外側(負の側)にある頂点は破棄されます。
        ///     例えば、三角形を平面で分割する場合、四角形と三角形に分割されますが、
        ///     この時、分割後のどちらかの多角形（平面の外側の多角形）の情報は失われます。
        ///     また、凸多角形を構成する全ての頂点が平面の外側にあった場合はallVertexIsOutsideにtrueを設定します。
        /// </remarks>
        /// <param name="clipPlane">分割平面</param>
        /// <param name="allVertexIsOutside">凸多角形の全ての頂点が平面の外の場合にtrueが設定されます。</param>
        public void SplitAndRemoveByPlane(Vector4 clipPlane, out bool allVertexIsOutside)
        {
            allVertexIsOutside = false;
            // クリップ平面の外側にある頂点を調べる。
            var numOutsideVertex = 0;
            var removeVertStartNo = -1;
            var removeVertEndNo = 0;
            var remainVertStartNo = -1;
            var remainVertEndNo = 0;
            for (var no = 0; no < VertexCount; no++)
            {
                var t = Vector4.Dot(clipPlane, Vector3ToVector4(GetVertexPosition(no)));
                if (t < 0)
                {
                    // 外側
                    if (removeVertStartNo == -1) removeVertStartNo = no;

                    removeVertEndNo = no;
                    numOutsideVertex++;
                }
                else
                {
                    // 内側
                    if (remainVertStartNo == -1) remainVertStartNo = no;

                    remainVertEndNo = no;
                }
            }

            if (numOutsideVertex == VertexCount)
            {
                // 全ての頂点がクリップ平面の外側にいるので分割は行えない。
                allVertexIsOutside = true;
                return;
            }

            if (numOutsideVertex == 0)
                // 全ての頂点が内側なので分割は行えない。
                return;

            // ここから多角形分割。
            // 多角形の辺と平面が交差する箇所に新しい頂点が二つ増える。
            // また、平面の外側の頂点は除外するので、多角形の頂点の増減値は 2 - numOutsideVertex となる。 
            var deltaVerticesSize = 2 - numOutsideVertex;

            if (removeVertStartNo == 0)
            {
                // 0番目の頂点が除外される
                // 平面と交差する二つのラインの情報をバックアップ。
                var l0 = GetLine(remainVertStartNo - 1);
                var l1 = GetLine(remainVertEndNo);
                // 残る頂点を前方に詰める。
                var vertNo = 0;
                for (var i = remainVertStartNo; i < remainVertEndNo + 1; i++)
                {
                    var destVertNo = GetIndexInBufferFromVertexNo(vertNo);
                    var srcVertNo = GetIndexInBufferFromVertexNo(i);
                    CopyVertexPosition_Direct(destVertNo, srcVertNo);
                    CopyVertexNormal_Direct(destVertNo, srcVertNo);
                    CopyVertexLocalPosition_Direct(destVertNo, srcVertNo);
                    CopyVertexLocalNormal_Direct(destVertNo, srcVertNo);
                    CopyVertexBoneWeight_Direct(destVertNo, srcVertNo);
                    CopyLine_Direct(destVertNo, srcVertNo);

                    vertNo++;
                }


                // 頂点を二つ追加する。
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

                // 頂点を二つ追加する。
                var newVertNo0 = vertNo;
                var newVertNo1 = vertNo + 1;

                var newVertNo0_Direct = GetIndexInBufferFromVertexNo(newVertNo0);
                var newVertNo1_Direct = GetIndexInBufferFromVertexNo(newVertNo1);
                SetVertexPosition_Direct(newVertNo0_Direct, newVert0);
                SetVertexPosition_Direct(newVertNo1_Direct, newVert1);

                SetVertexNormal_Direct(newVertNo0_Direct, newNormal0);
                SetVertexNormal_Direct(newVertNo1_Direct, newNormal1);

                SetVertexLocalPosition_Direct(newVertNo0_Direct, newLocalVert0);
                SetVertexLocalPosition_Direct(newVertNo1_Direct, newLocalVert1);

                SetVertexLocalNormal_Direct(newVertNo0_Direct, newLocalNormal0);
                SetVertexLocalNormal_Direct(newVertNo1_Direct, newLocalNormal1);

                SetVertexBoneWeight_Direct(newVertNo0_Direct, newBoneWeight0);
                SetVertexBoneWeight_Direct(newVertNo1_Direct, newBoneWeight1);

                // ライン情報の構築。
                VertexCount += deltaVerticesSize;
                ref var line_0 = ref GetLineRef(newVertNo0 - 1);
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
                var endVertNo1_Direct = GetIndexInBufferFromVertexNo((newVertNo1 + 1) % VertexCount);
                line_2.SetStartEndAndCalcStartToEnd(
                    newVert1,
                    GetVertexPosition_Direct(endVertNo1_Direct),
                    newNormal1,
                    GetVertexNormal_Direct(endVertNo1_Direct),
                    newLocalVert1,
                    GetVertexLocalPosition_Direct(endVertNo1_Direct),
                    newLocalNormal1,
                    GetVertexLocalNormal_Direct(endVertNo1_Direct));

                line_0.SetEndBoneWeight(newBoneWeight0);
                line_1.SetStartEndBoneWeights(newBoneWeight0, newBoneWeight1);
                line_2.SetStartEndBoneWeights(
                    newBoneWeight1,
                    GetVertexBoneWeight_Direct(endVertNo1_Direct));
            }
            else
            {
                // それ以外
                // 平面と交差する二つのラインの情報をバックアップ。
                var l0 = GetLine(removeVertStartNo - 1);
                var l1 = GetLine(removeVertEndNo);
                if (deltaVerticesSize > 0)
                    // 頂点が増える。
                    for (var i = VertexCount - 1; i > removeVertEndNo; i--)
                    {
                        var destVertNo = GetIndexInBufferFromVertexNo(i + deltaVerticesSize);
                        var srcVertNo = GetIndexInBufferFromVertexNo(i);
                        CopyVertexPosition_Direct(destVertNo, srcVertNo);
                        CopyVertexNormal_Direct(destVertNo, srcVertNo);
                        CopyVertexLocalPosition_Direct(destVertNo, srcVertNo);
                        CopyVertexLocalNormal_Direct(destVertNo, srcVertNo);
                        CopyVertexBoneWeight_Direct(destVertNo, srcVertNo);
                        CopyLine_Direct(destVertNo, srcVertNo);
                    }
                else
                    // 頂点が減る or 同じ
                    for (var i = removeVertEndNo + 1; i < VertexCount; i++)
                    {
                        var destVertNo = GetIndexInBufferFromVertexNo(i + deltaVerticesSize);
                        var srcVertNo = GetIndexInBufferFromVertexNo(i);

                        CopyVertexPosition_Direct(destVertNo, srcVertNo);
                        CopyVertexNormal_Direct(destVertNo, srcVertNo);
                        CopyVertexLocalPosition_Direct(destVertNo, srcVertNo);
                        CopyVertexLocalNormal_Direct(destVertNo, srcVertNo);
                        CopyVertexBoneWeight_Direct(destVertNo, srcVertNo);
                        CopyLine_Direct(destVertNo, srcVertNo);
                    }

                // 頂点を二つ追加する。
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
                var newVertNo_0 = removeVertStartNo;
                var newVertNo_1 = removeVertStartNo + 1;

                var newVertNo0_Direct = GetIndexInBufferFromVertexNo(newVertNo_0);
                var newVertNo1_Direct = GetIndexInBufferFromVertexNo(newVertNo_1);
                SetVertexPosition_Direct(newVertNo0_Direct, newVert0);
                SetVertexPosition_Direct(newVertNo1_Direct, newVert1);

                SetVertexNormal_Direct(newVertNo0_Direct, newNormal0);
                SetVertexNormal_Direct(newVertNo1_Direct, newNormal1);

                SetVertexLocalPosition_Direct(newVertNo0_Direct, newLocalVert0);
                SetVertexLocalPosition_Direct(newVertNo1_Direct, newLocalVert1);

                SetVertexLocalNormal_Direct(newVertNo0_Direct, newLocalNormal0);
                SetVertexLocalNormal_Direct(newVertNo1_Direct, newLocalNormal1);

                SetVertexBoneWeight_Direct(newVertNo0_Direct, newBoneWeight0);
                SetVertexBoneWeight_Direct(newVertNo1_Direct, newBoneWeight1);

                // ライン情報の構築。
                VertexCount += deltaVerticesSize;
                ref var line_0 = ref GetLineRef(newVertNo_0 - 1);
                ref var line_1 = ref GetLineRef(newVertNo_0);
                ref var line_2 = ref GetLineRef(newVertNo_1);
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

                var endVertNo1_Direct = GetIndexInBufferFromVertexNo((newVertNo_1 + 1) % VertexCount);
                line_2.SetStartEndAndCalcStartToEnd(
                    newVert1,
                    GetVertexPosition_Direct(endVertNo1_Direct),
                    newNormal1,
                    GetVertexNormal_Direct(endVertNo1_Direct),
                    newLocalVert1,
                    GetVertexLocalPosition_Direct(endVertNo1_Direct),
                    newLocalNormal1,
                    GetVertexLocalNormal_Direct(endVertNo1_Direct));

                line_0.SetEndBoneWeight(newBoneWeight0);
                line_1.SetStartEndBoneWeights(newBoneWeight0, newBoneWeight1);
                line_2.SetStartEndBoneWeights(
                    newBoneWeight1,
                    GetVertexBoneWeight_Direct(endVertNo1_Direct));
            }
        }

        /// <summary>
        ///     レイと三角形の衝突判定。
        /// </summary>
        /// <remarks>
        ///     凸多角形が三角形意外の時はfalseを返します。
        /// </remarks>
        /// <param name="hitPoint">衝突している場合は衝突点の座標が記憶されます。</param>
        /// <param name="rayStartPos">レイの始点の座標</param>
        /// <param name="rayEndPos">レイの終点の座標</param>
        /// <returns>衝突している場合はtrueを返します。</returns>
        public bool IsIntersectRayToTriangle(out Vector3 hitPoint, Vector3 rayStartPos, Vector3 rayEndPos)
        {
            hitPoint = Vector3.zero;
            if (VertexCount != 3) return false;

            var v0Pos = GetVertexPosition(0);
            var v1Pos = GetVertexPosition(1);
            var v2Pos = GetVertexPosition(2);

            // 平面とレイの交差を調べる。
            var v0ToRayStart = rayStartPos - v0Pos;
            var v0ToRayEnd = rayEndPos - v0Pos;
            var v0ToRayStartNorm = v0ToRayStart.normalized;
            var v0ToRayEndNorm = v0ToRayEnd.normalized;
            var t = Vector3.Dot(v0ToRayStartNorm, FaceNormal)
                    * Vector3.Dot(v0ToRayEndNorm, FaceNormal);
            if (t < 0.0f)
            {
                // 交差している。
                // 次は交点を計算。
                var t0 = Mathf.Abs(Vector3.Dot(v0ToRayStart, FaceNormal));
                var t1 = Mathf.Abs(Vector3.Dot(v0ToRayEnd, FaceNormal));
                var intersectPoint = Vector3.Lerp(rayStartPos, rayEndPos, t0 / (t0 + t1));
                // 続いて、交点が三角形の中かどうかを調べる。
                var v0ToIntersectPos = intersectPoint - v0Pos;
                var v1ToIntersectPos = intersectPoint - v1Pos;
                var v2ToIntersectPos = intersectPoint - v2Pos;
                v0ToIntersectPos.Normalize();
                v1ToIntersectPos.Normalize();
                v2ToIntersectPos.Normalize();
                var v0ToV1 = GetLineRef(0).StartToEndVec;
                var v1ToV2 = GetLineRef(1).StartToEndVec;
                var v2ToV0 = GetLineRef(2).StartToEndVec;
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
                    // 三角形の中だったので、交差していることが確定。
                    hitPoint = intersectPoint;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     頂点ボーンウェイトを取得
        /// </summary>
        /// <param name="vertNo">頂点番号</param>
        /// <returns>ボーンウェイト</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BoneWeight GetVertexBoneWeight(int vertNo)
        {
            return _boneWeightBuffer[GetIndexInBufferFromVertexNo(vertNo)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BoneWeight GetVertexBoneWeight_Direct(int vertNo)
        {
            return _boneWeightBuffer[vertNo];
        }

        /// <summary>
        ///     頂点ボーンウェイトを設定
        /// </summary>
        /// <param name="vertNo"></param>
        /// <param name="boneWeight"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetVertexBoneWeight(int vertNo, BoneWeight boneWeight)
        {
            _boneWeightBuffer[GetIndexInBufferFromVertexNo(vertNo)] = boneWeight;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetVertexBoneWeight_Direct(int vertNo, BoneWeight boneWeight)
        {
            _boneWeightBuffer[vertNo] = boneWeight;
        }

        /// <summary>
        ///     頂点ボーンウェイトをコピー
        /// </summary>
        /// <param name="destVertNo">コピー先の頂点番号</param>
        /// <param name="srcVertNo">コピー元の頂点番号</param>
        private void CopyVertexBoneWeight(int destVertNo, int srcVertNo)
        {
            _boneWeightBuffer[GetIndexInBufferFromVertexNo(destVertNo)] =
                _boneWeightBuffer[GetIndexInBufferFromVertexNo(srcVertNo)];
        }

        private void CopyVertexBoneWeight_Direct(int destVertNo, int srcVertNo)
        {
            _boneWeightBuffer[destVertNo] = _boneWeightBuffer[srcVertNo];
        }

        /// <summary>
        ///     行列をスカラー倍する
        /// </summary>
        /// <remarks>
        ///     下記の計算が行われます。<br />
        ///     mOut = m * s;
        /// </remarks>
        /// <param name="mOut">計算結果の格納先</param>
        /// <param name="m">行列</param>
        /// <param name="s">スカラー倍</param>
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

        /// <summary>
        ///     行列をスカラー倍して加算する。
        /// </summary>
        /// <remarks>
        ///     下記の計算が行われます。<br />
        ///     mOut *= m * s;
        /// </remarks>
        /// <param name="mOut">計算結果の格納先</param>
        /// <param name="m">行列</param>
        /// <param name="s">スカラー倍</param>
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
        ///     ワーカースレッドで処理を走らせる前の準備処理。
        /// </summary>
        public void PrepareRunActionByWorkerThread()
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
            var vertNo0_Direct = GetIndexInBufferFromVertexNo(0);
            var vertNo1_Direct = GetIndexInBufferFromVertexNo(1);
            var vertNo2_Direct = GetIndexInBufferFromVertexNo(2);
            if (_isSkinnedMeshRenderer)
            {
                if (_existsRootBone)
                {
                    boneWeights[0] = GetVertexBoneWeight_Direct(vertNo0_Direct);
                    boneWeights[1] = GetVertexBoneWeight_Direct(vertNo1_Direct);
                    boneWeights[2] = GetVertexBoneWeight_Direct(vertNo2_Direct);

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

            SetVertexPosition_Direct(vertNo0_Direct,
                localToWorldMatrices[0].MultiplyPoint3x4(GetVertexLocalPosition_Direct(vertNo0_Direct)));
            SetVertexPosition_Direct(vertNo1_Direct,
                localToWorldMatrices[1].MultiplyPoint3x4(GetVertexLocalPosition_Direct(vertNo1_Direct)));
            SetVertexPosition_Direct(vertNo2_Direct,
                localToWorldMatrices[2].MultiplyPoint3x4(GetVertexLocalPosition_Direct(vertNo2_Direct)));

            SetVertexNormal_Direct(vertNo0_Direct,
                localToWorldMatrices[0].MultiplyVector(GetVertexLocalNormal_Direct(vertNo0_Direct)));
            SetVertexNormal_Direct(vertNo1_Direct,
                localToWorldMatrices[1].MultiplyVector(GetVertexLocalNormal_Direct(vertNo1_Direct)));
            SetVertexNormal_Direct(vertNo2_Direct,
                localToWorldMatrices[2].MultiplyVector(GetVertexLocalNormal_Direct(vertNo2_Direct)));

            for (var vertNo = 0; vertNo < VertexCount; vertNo++)
            {
                var startVertNo_Direct = GetIndexInBufferFromVertexNo(vertNo);
                var nextVertNo_Direct = GetIndexInBufferFromVertexNo((vertNo + 1) % VertexCount);
                ref var line = ref GetLineRef(vertNo);
                line.Initialize(
                    GetVertexPosition_Direct(startVertNo_Direct),
                    GetVertexPosition_Direct(nextVertNo_Direct),
                    GetVertexNormal_Direct(startVertNo_Direct),
                    GetVertexNormal_Direct(nextVertNo_Direct),
                    GetVertexBoneWeight_Direct(startVertNo_Direct),
                    GetVertexBoneWeight_Direct(nextVertNo_Direct),
                    GetVertexLocalPosition_Direct(startVertNo_Direct),
                    GetVertexLocalPosition_Direct(nextVertNo_Direct),
                    GetVertexLocalNormal_Direct(startVertNo_Direct),
                    GetVertexLocalNormal_Direct(nextVertNo_Direct));
            }

            _faceNormal = Vector3.Cross(
                GetVertexPosition_Direct(vertNo1_Direct) - GetVertexPosition_Direct(vertNo0_Direct),
                GetVertexPosition_Direct(vertNo2_Direct) - GetVertexPosition_Direct(vertNo0_Direct));
            _faceNormal.Normalize();
        }
    }
}
