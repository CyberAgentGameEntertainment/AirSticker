using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     大まかな当たり判定を行い、デカール対象となるメッシュを大幅に枝切りする。
    /// </summary>
    /// <remarks>
    ///     デカールボックスの座標からデカールボックスの全範囲を内包する円の外に含まれているメッシュを枝切りします。<br />
    ///     また、メッシュの向きがデカールボックスと逆向きになっているメッシュも枝切りします。<br />
    ///     枝切りはUnityのジョブシステムを利用して並列に実行されます。
    /// </remarks>
    internal static class CyBroadPhaseConvexPolygonsDetection
    {
        /// <summary>
        ///     ブロードフェーズを実行。
        /// </summary>
        public static List<ConvexPolygonInfo> Execute(
            Vector3 centerPosInDecalBox,
            Vector3 decalSpaceNormalWs,
            float width,
            float height,
            float projectionDepth,
            List<ConvexPolygonInfo> convexPolygonInfos)
        {
            var threshold = Mathf.Max(width/2, height/2, projectionDepth);
            // ボックスの対角線の長さにする。
            threshold *= 1.414f;
            threshold *= threshold;
            
            int broadPhaseConvexPolygonCount = 0;
            for (var i = 0; i < convexPolygonInfos.Count; i++)
            {
                var convexPolygonInfo = convexPolygonInfos[i];
                if (Vector3.Dot(decalSpaceNormalWs, convexPolygonInfo.ConvexPolygon.FaceNormal) < 0)
                {
                    // 枝切りの印をつける。
                    convexPolygonInfo.IsOutsideClipSpace = true;
                    continue;
                }

                var vertNo_0 = convexPolygonInfo.ConvexPolygon.GetRealVertexNo(0);
                var v0 = convexPolygonInfo.ConvexPolygon.GetVertexPositionInWorldSpace(vertNo_0);
                v0 -= centerPosInDecalBox;
                if (v0.sqrMagnitude > threshold)
                {
                    var vertNo_1 = convexPolygonInfo.ConvexPolygon.GetRealVertexNo(1);
                    var v1 = convexPolygonInfo.ConvexPolygon.GetVertexPositionInWorldSpace(vertNo_1);
                    v1 -= centerPosInDecalBox;
                    if (v1.sqrMagnitude > threshold)
                    {
                        var vertNo_2 = convexPolygonInfo.ConvexPolygon.GetRealVertexNo(2);
                        var v2 = convexPolygonInfo.ConvexPolygon.GetVertexPositionInWorldSpace(vertNo_2);
                        v2 -= centerPosInDecalBox;
                        if (v2.sqrMagnitude > threshold)
                        {
                            // 枝切りの印をつける。
                            convexPolygonInfo.IsOutsideClipSpace = true;
                            continue;
                        }
                    }
                }

                broadPhaseConvexPolygonCount++;
            }
            
            var broadPhaseConvexPolygonInfos = new List<ConvexPolygonInfo>(broadPhaseConvexPolygonCount);
            var positionBuffer = new Vector3[CyConvexPolygon.DefaultMaxVertex * broadPhaseConvexPolygonCount];
            var normalBuffer = new Vector3[CyConvexPolygon.DefaultMaxVertex * broadPhaseConvexPolygonCount];
            var localPositionBuffer = new Vector3[CyConvexPolygon.DefaultMaxVertex * broadPhaseConvexPolygonCount];
            var localNormalBuffer = new Vector3[CyConvexPolygon.DefaultMaxVertex * broadPhaseConvexPolygonCount];
            var boneWeightBuffer = new BoneWeight[CyConvexPolygon.DefaultMaxVertex * broadPhaseConvexPolygonCount];
            var lineBuffer = new CyLine[CyConvexPolygon.DefaultMaxVertex * broadPhaseConvexPolygonCount];
            var startOffsetInBuffer = 0;
             
            for (var i = 0; i < convexPolygonInfos.Count; i++)
            {
                var convexPolygonInfo = convexPolygonInfos[i];
                if (!convexPolygonInfo.IsOutsideClipSpace)
                {
                    broadPhaseConvexPolygonInfos.Add(new ConvexPolygonInfo
                    {
                        ConvexPolygon = new CyConvexPolygon(convexPolygonInfo.ConvexPolygon, positionBuffer,
                            normalBuffer, boneWeightBuffer, lineBuffer,localPositionBuffer, localNormalBuffer,
                            startOffsetInBuffer),
                        IsOutsideClipSpace = convexPolygonInfo.IsOutsideClipSpace
                    });
                    
                    startOffsetInBuffer += CyConvexPolygon.DefaultMaxVertex;
                }

                convexPolygonInfo.IsOutsideClipSpace = false;
            }
            return broadPhaseConvexPolygonInfos;
        }
    }
}
