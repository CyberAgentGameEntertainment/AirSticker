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
            var sw = new Stopwatch();
            sw.Start();
            var threshold = Mathf.Max(width/2, height/2, projectionDepth);
            // ボックスの対角線の長さにする。
            threshold *= 1.414f;
            threshold *= threshold;
            
            int broacphaseConvexPolygonCount = 0;
            for (var i = 0; i < convexPolygonInfos.Count; i++)
            {
                var convexPolygonInfo = convexPolygonInfos[i];
                if (Vector3.Dot(decalSpaceNormalWs, convexPolygonInfo.ConvexPolygon.FaceNormal) < 0)
                {
                    // 枝切りの印をつける。
                    convexPolygonInfo.IsOutsideClipSpace = true;
                    continue;
                }

                var v0 = convexPolygonInfo.ConvexPolygon.GetVertexPosition(0);
                v0 -= centerPosInDecalBox;
                if (v0.sqrMagnitude > threshold)
                {
                    var v1 = convexPolygonInfo.ConvexPolygon.GetVertexPosition(1);
                    v1 -= centerPosInDecalBox;
                    if (v1.sqrMagnitude > threshold)
                    {
                        var v2 = convexPolygonInfo.ConvexPolygon.GetVertexPosition(2);
                        v2 -= centerPosInDecalBox;
                        if (v2.sqrMagnitude > threshold)
                        {
                            // 枝切りの印をつける。
                            convexPolygonInfo.IsOutsideClipSpace = true;
                            continue;
                        }
                    }
                }

                broacphaseConvexPolygonCount++;
            }
            
            var broadPhaseConvexPolygonInfos = new List<ConvexPolygonInfo>(broacphaseConvexPolygonCount);
            var positionBuffer = new Vector3[CyConvexPolygon.DefaultMaxVertex * broacphaseConvexPolygonCount];
            var normalBuffer = new Vector3[CyConvexPolygon.DefaultMaxVertex * broacphaseConvexPolygonCount];
            var boneWeightBuffer = new BoneWeight[CyConvexPolygon.DefaultMaxVertex * broacphaseConvexPolygonCount];
            var lineBuffer = new CyLine[CyConvexPolygon.DefaultMaxVertex * broacphaseConvexPolygonCount];
            var startOffsetInBuffer = 0;
            var needDispose = true; 
            for (var i = 0; i < convexPolygonInfos.Count; i++)
            {
                var convexPolygonInfo = convexPolygonInfos[i];
                if (!convexPolygonInfo.IsOutsideClipSpace)
                {
                    broadPhaseConvexPolygonInfos.Add(new ConvexPolygonInfo
                    {
                        ConvexPolygon = new CyConvexPolygon(convexPolygonInfo.ConvexPolygon, positionBuffer,
                            normalBuffer, boneWeightBuffer, lineBuffer, startOffsetInBuffer),
                        IsOutsideClipSpace = convexPolygonInfo.IsOutsideClipSpace
                    });
                    needDispose = false;
                    startOffsetInBuffer += CyConvexPolygon.DefaultMaxVertex;
                }

                convexPolygonInfo.IsOutsideClipSpace = false;
            }

            sw.Stop();
            UnityEngine.Debug.Log(sw.ElapsedMilliseconds);
            return broadPhaseConvexPolygonInfos;
        }
    }
}
