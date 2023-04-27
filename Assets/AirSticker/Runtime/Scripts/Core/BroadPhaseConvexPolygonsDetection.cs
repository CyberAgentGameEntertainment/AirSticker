using System.Collections.Generic;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core
{
    /// <summary>
    ///     This class run broad phase convex polygons detection.
    /// </summary>
    internal static class BroadPhaseConvexPolygonsDetection
    {
        /// <summary>
        ///     Execute broad phase.
        /// </summary>
        /// <remarks>
        ///     Remove polygons outside the circle encompassing the decal box.<br />
        ///     Also, remove polygons whose mesh orientation is opposite the decal box.<br />
        /// </remarks>
        public static List<ConvexPolygonInfo> Execute(
            Vector3 centerPosInDecalBox,
            Vector3 decalSpaceNormalWs,
            float decalBoxWidth,
            float decalBoxHeight,
            float decalBoxDepth,
            List<ConvexPolygonInfo> convexPolygonInfos,
            bool projectionBackside)
        {
            var threshold = Mathf.Max(decalBoxWidth / 2, decalBoxHeight / 2, decalBoxDepth);
            threshold *= 1.414f;
            threshold *= threshold;

            var broadPhaseConvexPolygonCount = 0;
            for (var i = 0; i < convexPolygonInfos.Count; i++)
            {
                var convexPolygonInfo = convexPolygonInfos[i];
                if (!projectionBackside && Vector3.Dot(decalSpaceNormalWs, convexPolygonInfo.ConvexPolygon.FaceNormal) < 0)
                {
                    // Set the flag of outside the clip space.
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
                            // Set the flag of outside the clip space.
                            convexPolygonInfo.IsOutsideClipSpace = true;
                            continue;
                        }
                    }
                }

                broadPhaseConvexPolygonCount++;
            }

            var broadPhaseConvexPolygonInfos = new List<ConvexPolygonInfo>(broadPhaseConvexPolygonCount);
            var positionBuffer = new Vector3[ConvexPolygon.DefaultMaxVertex * broadPhaseConvexPolygonCount];
            var normalBuffer = new Vector3[ConvexPolygon.DefaultMaxVertex * broadPhaseConvexPolygonCount];
            var localPositionBuffer = new Vector3[ConvexPolygon.DefaultMaxVertex * broadPhaseConvexPolygonCount];
            var localNormalBuffer = new Vector3[ConvexPolygon.DefaultMaxVertex * broadPhaseConvexPolygonCount];
            var boneWeightBuffer = new BoneWeight[ConvexPolygon.DefaultMaxVertex * broadPhaseConvexPolygonCount];
            var lineBuffer = new Line[ConvexPolygon.DefaultMaxVertex * broadPhaseConvexPolygonCount];
            var startOffsetInBuffer = 0;

            for (var i = 0; i < convexPolygonInfos.Count; i++)
            {
                var convexPolygonInfo = convexPolygonInfos[i];
                if (!convexPolygonInfo.IsOutsideClipSpace)
                {
                    broadPhaseConvexPolygonInfos.Add(new ConvexPolygonInfo
                    {
                        ConvexPolygon = new ConvexPolygon(convexPolygonInfo.ConvexPolygon, positionBuffer,
                            normalBuffer, boneWeightBuffer, lineBuffer, localPositionBuffer, localNormalBuffer,
                            startOffsetInBuffer),
                        IsOutsideClipSpace = convexPolygonInfo.IsOutsideClipSpace
                    });

                    startOffsetInBuffer += ConvexPolygon.DefaultMaxVertex;
                }

                convexPolygonInfo.IsOutsideClipSpace = false;
            }

            return broadPhaseConvexPolygonInfos;
        }
    }
}
