using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    /// 大まかな当たり判定を行い、デカール対象となるメッシュを大幅に枝切りする。
    /// </summary>
    /// <remarks>
    /// デカールボックスの座標からデカールボックスの全範囲を内包する円の外に含まれているメッシュを枝切りします。<br/>
    /// また、メッシュの向きがデカールボックスと逆向きになっているメッシュも枝切りします。<br/>
    /// 枝切りはUnityのジョブシステムを利用して並列に実行されます。
    /// </remarks>
    public static class CyBroadPhaseDetectionConvexPolygons
    {
        private static List<ConvexPolygonInfo> _convexPolygonInfos;
        
        /// <summary>
        /// ブロードフェーズを実行。
        /// </summary>
        public static List<ConvexPolygonInfo> Execute(
            Vector3 originPosInDecalSpace, 
            Vector3 decalSpaceNormalWS,
            float width, 
            float height,
            float projectionDepth,
            List<ConvexPolygonInfo> convexPolygonInfos)
        {
            _convexPolygonInfos = convexPolygonInfos;
            var broadPhaseConvexPolygonInfos = new List<ConvexPolygonInfo>();
            var threshold = Mathf.Max(width, height, projectionDepth);
            threshold *= threshold;
            broadPhaseConvexPolygonInfos.Capacity = convexPolygonInfos.Count;
            
            var numThread = 4;    // 4スレッド使う
            if (convexPolygonInfos.Count < 20)
            {
                // 20ポリゴン以下ならシングルスレッドで実行する。
                numThread = 1;
            }
            var numWorkOne = convexPolygonInfos.Count / numThread;
            
            var workJobs = new BuildBroadPhaseConvexPolygonInfosJob[numThread];
            var startIndex = 0;
            for (var i = 0; i < numThread; i++)
            {
                workJobs[i] = new BuildBroadPhaseConvexPolygonInfosJob();
                workJobs[i].beginIndex = startIndex ;
                workJobs[i].endIndex = Mathf.Min(workJobs[i].beginIndex + numWorkOne, convexPolygonInfos.Count);
                workJobs[i].thresholdRange = threshold;
                workJobs[i].decalSpaceOriginPosisionWS = originPosInDecalSpace;
                workJobs[i].decalSpaceNormalWS = decalSpaceNormalWS;
                startIndex += numWorkOne + 1;
            }
            
            JobHandle[] jobHandles = new JobHandle[numThread];
            // ジョブをスケジューリング
            for (var i = 0; i < numThread; i++)
            {
                jobHandles[i] = workJobs[i].Schedule();
            }
            // ジョブの完了待ち
            for (var i = 0; i < numThread; i++)
            {
                jobHandles[i].Complete();
            }
            
            foreach (var convexPolygonInfo in convexPolygonInfos)
            {
                if (!convexPolygonInfo.IsOutsideClipSpace)
                {
                    broadPhaseConvexPolygonInfos.Add(new ConvexPolygonInfo
                    {
                       ConvexPolygon = new CyConvexPolygon(convexPolygonInfo.ConvexPolygon),
                       IsOutsideClipSpace = convexPolygonInfo.IsOutsideClipSpace
                    });
                }

                convexPolygonInfo.IsOutsideClipSpace = false;
            }
            
            _convexPolygonInfos = null;
            return broadPhaseConvexPolygonInfos;
        }
        /// <summary>
        /// ジョブから凸ポリゴンのリストにアクセスするためのヘルパー関数
        /// </summary>
        /// <returns></returns>
        private static List<ConvexPolygonInfo> GetConvexPolygonInfosHelper()
        {
            return _convexPolygonInfos;
        }
         /// <summary>
        /// 処理を行う凸多角形の早期枝切りを行うためのジョブ。
        /// </summary>
        struct BuildBroadPhaseConvexPolygonInfosJob : IJob
        {
            public int beginIndex;                      // 枝切り調査の開始インデックス
            public int endIndex;                        // 枝切り調査の終端インデックス（終端は含まない)
            public Vector3 decalSpaceOriginPosisionWS;  // デカール空間の起点の座標(ワールドスペース)
            public float thresholdRange;                // 枝切りの閾値となる境界球の範囲
            public Vector3 decalSpaceNormalWS;          // デカール空間の法線。
            /// <summary>
            /// 枝切りを実行。
            /// </summary>
            public void Execute()
            {
                // static関数を利用すると、ジョブからマネージドオブジェクトにアクセスできるので
                // ヘルパー関数を利用する。
                var convexPolygonInfos = GetConvexPolygonInfosHelper();
                for(int i = beginIndex; i < endIndex; i++)
                {
                    var convexPolyInfo = convexPolygonInfos[i];
                    if (Vector3.Dot(decalSpaceNormalWS, convexPolyInfo.ConvexPolygon.FaceNormal) < 0)
                    {
                        // 枝切りの印をつける。
                        convexPolyInfo.IsOutsideClipSpace = true;
                        continue;
                    }
                    var v0 = convexPolyInfo.ConvexPolygon.GetVertexPosition(0);
                    v0 -= decalSpaceOriginPosisionWS;
                    if (v0.sqrMagnitude > thresholdRange)
                    {
                        var v1 = convexPolyInfo.ConvexPolygon.GetVertexPosition(1);
                        v1 -= decalSpaceOriginPosisionWS;
                        if (v1.sqrMagnitude > thresholdRange)
                        {
                            var v2 = convexPolyInfo.ConvexPolygon.GetVertexPosition(2);
                            v2 -= decalSpaceOriginPosisionWS;
                            if (v2.sqrMagnitude > thresholdRange)
                            {
                                // 枝切りの印をつける。
                                convexPolyInfo.IsOutsideClipSpace = true;
                            }
                        }
                    }
                }
            }
        }
    }
}
