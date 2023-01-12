using System;
using Unity.Collections;
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
        private readonly Vector3 _faceNormal; // 面法線
        private readonly CyLine[] _lineBuffer; // 凸多角形を構成するエッジ情報のバッファ
        private readonly Vector3[] _normalBuffer; // 頂点法線バッファ
        private readonly Vector3[] _positionBuffer; // 頂点座標バッファ
        private readonly int _startOffsetInBuffer;
        private readonly int _maxVertex; //  凸多角形の最大頂点

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
            Renderer receiverMeshRenderer,
            int startOffsetInBuffer,
            int initVertexCount,
            int maxVertex = DefaultMaxVertex)
        {
            _maxVertex = maxVertex;
            _boneWeightBuffer = boneWeightBuffer;
            _positionBuffer = positionBuffer;
            _normalBuffer = normalBuffer;
            _lineBuffer = lineBuffer;
            _startOffsetInBuffer = startOffsetInBuffer;
            ReceiverMeshRenderer = receiverMeshRenderer;
            VertexCount = initVertexCount;

            for (var vertNo = 0; vertNo < VertexCount; vertNo++)
            {
                var nextVertNo = (vertNo + 1) % VertexCount;
                ref var line = ref GetLineRef(vertNo);
                line.Initialize(
                    GetVertexPosition(vertNo),
                    GetVertexPosition(nextVertNo),
                    GetVertexNormal(vertNo),
                    GetVertexNormal(nextVertNo),
                    GetVertexBoneWeight(vertNo),
                    GetVertexBoneWeight(nextVertNo));
            }

            _faceNormal = Vector3.Cross(
                GetVertexPosition(1) - GetVertexPosition(0),
                GetVertexPosition(2) - GetVertexPosition(0));
            _faceNormal.Normalize();
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
            int startOffsetInBuffer,
            int maxVertex = DefaultMaxVertex)
        {
            ReceiverMeshRenderer = srcConvexPolygon.ReceiverMeshRenderer;
            VertexCount = srcConvexPolygon.VertexCount;

            _maxVertex = Mathf.Max(maxVertex, srcConvexPolygon._maxVertex);
            
            _positionBuffer = positionBuffer;
            _normalBuffer = normalBuffer;
            _boneWeightBuffer = boneWeightBuffer;
            _lineBuffer = lineBuffer;
            _startOffsetInBuffer = startOffsetInBuffer;
            
            Array.Copy(srcConvexPolygon._positionBuffer, srcConvexPolygon._startOffsetInBuffer,
                _positionBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);
            Array.Copy(srcConvexPolygon._normalBuffer, srcConvexPolygon._startOffsetInBuffer,
                _normalBuffer, _startOffsetInBuffer, srcConvexPolygon.VertexCount);
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
            CyLine l0,
            CyLine l1,
            Vector4 clipPlane)
        {
            var t = Vector4.Dot(clipPlane, Vector3ToVector4(l0.EndPosition))
                    / Vector4.Dot(clipPlane, l0.StartToEndVec);
            newVert0 = Vector3.Lerp(l0.EndPosition, l0.StartPosition, t);
            newNormal0 = Vector3.Lerp(l0.EndNormal, l0.StartNormal, t);
            newNormal0.Normalize();

            t = Vector4.Dot(clipPlane, Vector3ToVector4(l1.StartPosition))
                / Vector4.Dot(clipPlane, l1.StartPosition - l1.EndPosition);

            newVert1 = Vector3.Lerp(l1.StartPosition, l1.EndPosition, t);
            newNormal1 = Vector3.Lerp(l1.StartNormal, l1.EndNormal, t);
            newNormal1.Normalize();

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
        public Vector3 GetVertexPosition(int vertNo)
        {
            return _positionBuffer[GetIndexInBufferFromVertexNo(vertNo)];
        }

        /// <summary>
        ///     頂点座標を設定
        /// </summary>
        /// <param name="vertNo"></param>
        /// <param name="position"></param>
        private void SetVertexPosition(int vertNo, Vector3 position)
        {
            _positionBuffer[GetIndexInBufferFromVertexNo(vertNo)] = position;
        }

        /// <summary>
        ///     頂点座標をコピー
        /// </summary>
        /// <param name="destVertNo">コピー先の頂点番号</param>
        /// <param name="srcVertNo">コピー元の頂点番号</param>
        private void CopyVertexPosition(int destVertNo, int srcVertNo)
        {
            _positionBuffer[GetIndexInBufferFromVertexNo(destVertNo)] =
                _positionBuffer[GetIndexInBufferFromVertexNo(srcVertNo)];
        }

        /// <summary>
        ///     頂点法線を取得
        /// </summary>
        /// <param name="vertNo">頂点番号</param>
        /// <returns></returns>
        public Vector3 GetVertexNormal(int vertNo)
        {
            return _normalBuffer[GetIndexInBufferFromVertexNo(vertNo)];
        }

        /// <summary>
        ///     頂点法線を設定
        /// </summary>
        /// <param name="vertNo"></param>
        /// <param name="normal"></param>
        private void SetVertexNormal(int vertNo, Vector3 normal)
        {
            _normalBuffer[GetIndexInBufferFromVertexNo(vertNo)] = normal;
        }

        /// <summary>
        ///     頂点法線をコピー
        /// </summary>
        /// <param name="destVertNo">コピー先の頂点番号</param>
        /// <param name="srcVertNo">コピー元の頂点番号</param>
        private void CopyVertexNormal(int destVertNo, int srcVertNo)
        {
            _normalBuffer[GetIndexInBufferFromVertexNo(destVertNo)] =
                _normalBuffer[GetIndexInBufferFromVertexNo(srcVertNo)];
        }

        /// <summary>
        ///     頂点番号を指定してその頂点から伸びているラインを取得します。
        /// </summary>
        /// <param name="vertNo">頂点番号</param>
        /// <returns></returns>
        private CyLine GetLine(int vertNo)
        {
            return _lineBuffer[GetIndexInBufferFromVertexNo(vertNo)];
        }

        /// <summary>
        ///     頂点番号を指定してその頂点から伸びているラインの参照を取得します。
        /// </summary>
        /// <param name="vertNo">頂点番号</param>
        /// <returns></returns>
        private ref CyLine GetLineRef(int vertNo)
        {
            return ref _lineBuffer[GetIndexInBufferFromVertexNo(vertNo)];
        }

        /// <summary>
        ///     指定された頂点から伸びているラインを設定します。
        /// </summary>
        /// <param name="vertNo">頂点番号</param>
        /// <param name="line">ライン</param>
        private void SetLine(int vertNo, CyLine line)
        {
            _lineBuffer[GetIndexInBufferFromVertexNo(vertNo)] = line;
        }

        /// <summary>
        ///     指定された頂点から伸びているラインをコピーします。
        /// </summary>
        /// <param name="destVertNo">コピー先のラインの始点になる頂点の番号</param>
        /// <param name="srcVertNo">コピー元のラインの始点になる頂点の番号</param>
        private void CopyLine(int destVertNo, int srcVertNo)
        {
            _lineBuffer[GetIndexInBufferFromVertexNo(destVertNo)] =
                _lineBuffer[GetIndexInBufferFromVertexNo(srcVertNo)];
        }

        /// <summary>
        ///     Vector3からVector4(w＝1)に変換します。
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
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
                    CopyVertexPosition(vertNo, i);
                    CopyVertexNormal(vertNo, i);
                    CopyVertexBoneWeight(vertNo, i);
                    CopyLine(vertNo, i);

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
                    l1,
                    l0,
                    clipPlane
                );

                // 頂点を二つ追加する。
                var newVertNo0 = vertNo;
                var newVertNo1 = vertNo + 1;

                SetVertexPosition(newVertNo0, newVert0);
                SetVertexPosition(newVertNo1, newVert1);

                SetVertexNormal(newVertNo0, newNormal0);
                SetVertexNormal(newVertNo1, newNormal1);

                SetVertexBoneWeight(newVertNo0, newBoneWeight0);
                SetVertexBoneWeight(newVertNo1, newBoneWeight1);

                // ライン情報の構築。
                VertexCount += deltaVerticesSize;
                ref var line_0 = ref GetLineRef(newVertNo0 - 1);
                ref var line_1 = ref GetLineRef(newVertNo0);
                ref var line_2 = ref GetLineRef(newVertNo1);
                line_0.SetEndAndCalcStartToEnd(newVert0, newNormal0);
                line_1.SetStartEndAndCalcStartToEnd(
                    newVert0,
                    newVert1,
                    newNormal0,
                    newNormal1);
                line_2.SetStartEndAndCalcStartToEnd(
                    newVert1,
                    GetVertexPosition((newVertNo1 + 1) % VertexCount),
                    newNormal1,
                    GetVertexNormal((newVertNo1 + 1) % VertexCount));

                line_0.SetEndBoneWeight(newBoneWeight0);
                line_1.SetStartEndBoneWeights(newBoneWeight0, newBoneWeight1);
                line_2.SetStartEndBoneWeights(
                    newBoneWeight1,
                    GetVertexBoneWeight((newVertNo1 + 1) % VertexCount));
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
                        CopyVertexPosition(i + deltaVerticesSize, i);
                        CopyVertexNormal(i + deltaVerticesSize, i);
                        CopyVertexBoneWeight(i + deltaVerticesSize, i);
                        CopyLine(i + deltaVerticesSize, i);
                    }
                else
                    // 頂点が減る or 同じ
                    for (var i = removeVertEndNo + 1; i < VertexCount; i++)
                    {
                        CopyVertexPosition(i + deltaVerticesSize, i);
                        CopyVertexNormal(i + deltaVerticesSize, i);
                        CopyVertexBoneWeight(i + deltaVerticesSize, i);
                        CopyLine(i + deltaVerticesSize, i);
                    }

                // 頂点を二つ追加する。
                CalculateNewVertexDataBySplitPlane(
                    out var newVert0,
                    out var newVert1,
                    out var newNormal0,
                    out var newNormal1,
                    out var newBoneWeight0,
                    out var newBoneWeight1,
                    l0,
                    l1,
                    clipPlane);
                var newVertNo_0 = removeVertStartNo;
                var newVertNo_1 = removeVertStartNo + 1;

                SetVertexPosition(newVertNo_0, newVert0);
                SetVertexPosition(newVertNo_1, newVert1);

                SetVertexNormal(newVertNo_0, newNormal0);
                SetVertexNormal(newVertNo_1, newNormal1);

                SetVertexBoneWeight(newVertNo_0, newBoneWeight0);
                SetVertexBoneWeight(newVertNo_1, newBoneWeight1);

                // ライン情報の構築。
                VertexCount += deltaVerticesSize;
                ref var line_0 = ref GetLineRef(newVertNo_0 - 1);
                ref var line_1 = ref GetLineRef(newVertNo_0);
                ref var line_2 = ref GetLineRef(newVertNo_1);
                line_0.SetEndAndCalcStartToEnd(newVert0, newNormal0);
                line_1.SetStartEndAndCalcStartToEnd(
                    newVert0,
                    newVert1,
                    newNormal0,
                    newNormal1);
                line_2.SetStartEndAndCalcStartToEnd(
                    newVert1,
                    GetVertexPosition((newVertNo_1 + 1) % VertexCount),
                    newNormal1,
                    GetVertexNormal((newVertNo_1 + 1) % VertexCount));

                line_0.SetEndBoneWeight(newBoneWeight0);
                line_1.SetStartEndBoneWeights(newBoneWeight0, newBoneWeight1);
                line_2.SetStartEndBoneWeights(
                    newBoneWeight1,
                    GetVertexBoneWeight((newVertNo_1 + 1) % VertexCount));
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
                var v0ToV1 = GetLine(0).StartToEndVec;
                var v1ToV2 = GetLine(1).StartToEndVec;
                var v2ToV0 = GetLine(2).StartToEndVec;
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
        public BoneWeight GetVertexBoneWeight(int vertNo)
        {
            return _boneWeightBuffer[GetIndexInBufferFromVertexNo(vertNo)];
        }

        /// <summary>
        ///     頂点ボーンウェイトを設定
        /// </summary>
        /// <param name="vertNo"></param>
        /// <param name="boneWeight"></param>
        private void SetVertexBoneWeight(int vertNo, BoneWeight boneWeight)
        {
            _boneWeightBuffer[GetIndexInBufferFromVertexNo(vertNo)] = boneWeight;
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
    }
}
