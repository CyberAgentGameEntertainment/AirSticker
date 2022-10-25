using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Debug = System.Diagnostics.Debug;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    /// デカールプロジェクター
    /// </summary>
    public class CyDecalProjector : MonoBehaviour
    {
        /// <summary>
        /// 分割平面
        /// </summary>
        enum ClipPlane
        {
            Left,
            Right,
            Bottom,
            Top,
            Front,
            Back,
            Num
        }
        
        [SerializeField] private float width;                               // デカールボックスの幅
        [SerializeField] private float height;                              // デカールボックスの高さ
        [SerializeField] private float projectionDepth;                     // デカールボックスの奥行
        [SerializeField] private GameObject receiverObject;                 // デカールを貼り付けるターゲットとなるオブジェクト
        [SerializeField] private Material decalMaterial;                    // デカールマテリアル
        private float projectionDepthRange = 0.0f;                          // デカールを貼り付ける平面からデカールを貼り付ける奥行の範囲。
        private float basePointToFarClipDistance = 0.0f;                    // デカールを貼り付ける基準地点から、ファークリップまでの距離。
        private float basePointToNearClipDistance = 0.0f;                   // デカールを貼り付ける基準地点から、ニアクリップまでの距離。
        private readonly Vector4[] _clipPlanes = new Vector4[(int)ClipPlane.Num];    // 分割平面
        private MeshFilter _receiverMeshFilter;                             // デカールを受けるメッシュフィルタ
        static private List<ConvexPolygonInfo> _convexPolygonInfos;         // ブロードフェーズのジョブワーク用の凸ポリゴンのリスト
        private List<ConvexPolygonInfo> _broadPhaseConvexPolygonInfos = new List<ConvexPolygonInfo>();
        private CyDecalMesh _cyDecalMesh;                       // デカールメッシュ。
        private Vector3 _decalSpaceNormalWS;                    // デカール空間の法線( ワールドスペース )
        private Vector3 _decalSpaceTangentWS;                   // デカール空間の接ベクトル( ワールドスペース )
        private Vector3 _decalSpaceBiNormalWS;                  // デカール空間の従ベクトル( ワールドスペース )

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
        //
        // Start is called before the first frame update
        void Start()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            _cyDecalMesh = CyRenderDecalFeature.Instance.GetDecalMesh(
                receiverObject, decalMaterial, out bool isNew);
            if (isNew)
            {
                // レシーバーオブジェクトにデカール描画用のオブジェクトを追加する。
                GameObject decalRenderer = new GameObject("CyDecalRenderer");
                var meshRenderer = decalRenderer.AddComponent<MeshRenderer>();
                meshRenderer.material = decalMaterial;
                var meshFilter = decalRenderer.AddComponent<MeshFilter>();
                meshFilter.mesh = _cyDecalMesh.Mesh;
                // TODO:とりあえず仮。
                decalRenderer.transform.position = Vector3.zero;
            }
            _receiverMeshFilter = receiverObject.GetComponent<MeshFilter>();
            
            ExecuteBroadphase();
            Vector3 hitPoint = new Vector3();
            if (IntersectRayToTrianglePolygons(ref hitPoint))
            {
                InitializeOriginAxisInDecalSpace();
                BuildClipPlanes(hitPoint);
                SplitConvexPolygonsByPlanes();
                BuildDecalMeshFromConvexPolygons(hitPoint);
            }
            sw.Stop();
            CyRenderDecalFeature.splitMeshTotalTime += sw.ElapsedMilliseconds;
        }

        // Update is called once per frame
        void Update()
        {
        }
        /// <summary>
        /// デカール空間での規定軸を初期化。
        /// </summary>
        private void InitializeOriginAxisInDecalSpace()
        {
            var trans = transform;
            _decalSpaceNormalWS = trans.forward * -1.0f;
            _decalSpaceTangentWS = trans.right;
            _decalSpaceBiNormalWS = trans.up;
        }
        /// <summary>
        /// デカールテクスチャを貼り付けるためのメッシュを多角形情報から構築する
        /// </summary>
        private void BuildDecalMeshFromConvexPolygons( Vector3 originPosInDecalSpace)
        {
            var convexPolygons = new List<CyConvexPolygon>();
            foreach (var convexPolyInfo in _broadPhaseConvexPolygonInfos)
            {
                if (convexPolyInfo.IsOutsideClipSpace)
                {
                    continue;
                }
                convexPolygons.Add(convexPolyInfo.ConvexPolygon);
            }
            _cyDecalMesh.AddPolygonsToDecalMesh(
                convexPolygons, 
                originPosInDecalSpace,
                _decalSpaceNormalWS,
                _decalSpaceTangentWS,
                _decalSpaceBiNormalWS,
                width,
                height);
        }
        
        /// <summary>
        /// 大まかな当たり判定を行い、デカール対象となるメッシュを大幅に枝切りする。
        /// </summary>
        /// <remarks>
        /// デカールボックスの座標からデカールボックスの全範囲を内包する円の外に含まれているメッシュを枝切りします。<br/>
        /// また、メッシュの向きがデカールボックスと逆向きになっているメッシュも枝切りします。<br/>
        /// 枝切りはUnityのジョブシステムを利用して並列に実行されます。
        /// </remarks>
        void ExecuteBroadphase()
        {
            // オリジナルのメッシュを取得する。
            CyRenderDecalFeature.Instance.RegisterDecalTargetObject(receiverObject);
            _convexPolygonInfos = CyRenderDecalFeature.Instance.GetTrianglePolygons(receiverObject);

            Vector3 originPosInDecalSpace = transform.position;
            var threshold = Mathf.Max(width, height, projectionDepth);
            threshold *= threshold;
            _broadPhaseConvexPolygonInfos.Clear();
            _broadPhaseConvexPolygonInfos.Capacity = _convexPolygonInfos.Count;
            
            int numThread = 4;    // 4スレッド使う
            if (_convexPolygonInfos.Count < 20)
            {
                // 20ポリゴン以下ならシングルスレッドで実行する。
                numThread = 1;
            }
            int numWorkOne = _convexPolygonInfos.Count / numThread;
            
            var workJobs = new BuildBroadPhaseConvexPolygonInfosJob[numThread];
            int startIndex = 0;
            for (int i = 0; i < numThread; i++)
            {
                workJobs[i] = new BuildBroadPhaseConvexPolygonInfosJob();
                workJobs[i].beginIndex = startIndex ;
                workJobs[i].endIndex = Mathf.Min(workJobs[i].beginIndex + numWorkOne, _convexPolygonInfos.Count);
                workJobs[i].thresholdRange = threshold;
                workJobs[i].decalSpaceOriginPosisionWS = originPosInDecalSpace;
                workJobs[i].decalSpaceNormalWS = _decalSpaceNormalWS;
                startIndex += numWorkOne + 1;
            }
            
            JobHandle[] jobHandles = new JobHandle[numThread];
            // ジョブをスケジューリング
            for (int i = 0; i < numThread; i++)
            {
                jobHandles[i] = workJobs[i].Schedule();
            }
            // ジョブの完了待ち
            for (int i = 0; i < numThread; i++)
            {
                jobHandles[i].Complete();
            }
            
            foreach (var convexPolygonInfo in _convexPolygonInfos)
            {
                if (!convexPolygonInfo.IsOutsideClipSpace)
                {
                    _broadPhaseConvexPolygonInfos.Add(new ConvexPolygonInfo
                    {
                       ConvexPolygon = new CyConvexPolygon(convexPolygonInfo.ConvexPolygon),
                       IsOutsideClipSpace = convexPolygonInfo.IsOutsideClipSpace
                    });
                }

                convexPolygonInfo.IsOutsideClipSpace = false;
            }
            
            _convexPolygonInfos = null;
        }
        /// <summary>
        /// デカールボックスを構成している６平面の情報を使って、凸多角形を分割していく。
        /// </summary>
        void SplitConvexPolygonsByPlanes()
        {
            // 凸多角形をクリップ平面で分割していく。
            foreach (var clipPlane in _clipPlanes)
            {
                foreach (var convexPolyInfo in _broadPhaseConvexPolygonInfos)
                {
                    // 平面の外側なので調査の対象外なのでスキップ
                    if ( convexPolyInfo.IsOutsideClipSpace)
                    {
                        continue;
                    }
                    
                    convexPolyInfo.ConvexPolygon.SplitAndRemoveByPlane(
                        clipPlane, out var isOutsideClipSpace);
                    convexPolyInfo.IsOutsideClipSpace = isOutsideClipSpace;
                }    
            }
        }
        /// <summary>
        /// デカールテクスチャを貼り付けるための頂点をクリッピングする平面を構築する。
        /// </summary>
        /// <param name="basePoint">デカールテクスチャを貼り付ける基準座標</param>
        void BuildClipPlanes(Vector3 basePoint)
        {
            var trans = transform;

            // Build left plane.
            _clipPlanes[(int)ClipPlane.Left] = new Vector4
            {
                x = _decalSpaceTangentWS.x,
                y = _decalSpaceTangentWS.y,
                z = _decalSpaceTangentWS.z,
                w = (width/2.0f) - Vector3.Dot(_decalSpaceTangentWS, basePoint)
            };
            // Build right plane.
            _clipPlanes[(int)ClipPlane.Right] = new Vector4
            {
                x = -_decalSpaceTangentWS.x,
                y = -_decalSpaceTangentWS.y,
                z = -_decalSpaceTangentWS.z,
                w = (width/2.0f) + Vector3.Dot(_decalSpaceTangentWS, basePoint)
            };
            // Build bottom plane.
            _clipPlanes[(int)ClipPlane.Bottom] = new Vector4
            {
                x = _decalSpaceBiNormalWS.x,
                y = _decalSpaceBiNormalWS.y,
                z = _decalSpaceBiNormalWS.z,
                w = (height/2.0f) - Vector3.Dot(_decalSpaceBiNormalWS, basePoint)
            };
            // Build top plane.
            _clipPlanes[(int)ClipPlane.Top] = new Vector4
            {
                x = -_decalSpaceBiNormalWS.x,
                y = -_decalSpaceBiNormalWS.y,
                z = -_decalSpaceBiNormalWS.z,
                w = (height/2.0f) + Vector3.Dot(_decalSpaceBiNormalWS, basePoint)
            };
            // Build front plane.
            _clipPlanes[(int)ClipPlane.Front] = new Vector4
            {
                x = -_decalSpaceNormalWS.x,
                y = -_decalSpaceNormalWS.y,
                z = -_decalSpaceNormalWS.z,
                w = basePointToNearClipDistance + Vector3.Dot(_decalSpaceNormalWS, basePoint)
            };
            // Build back plane.
            _clipPlanes[(int)ClipPlane.Back] = new Vector4
            {
                x = _decalSpaceNormalWS.x,
                y = _decalSpaceNormalWS.y,
                z = _decalSpaceNormalWS.z,
                w = basePointToFarClipDistance - Vector3.Dot(_decalSpaceNormalWS, basePoint)
            };
        }
        /// <summary>
        /// デカールボックスの中心を通るレイとレシーバーオブジェクトの三角形オブジェクトの衝突判定を行う。
        /// </summary>
        /// <param name="hitPoint">衝突点の格納先</param>
        /// <returns>trueが帰ってきたら衝突している</returns>
        private bool IntersectRayToTrianglePolygons( ref Vector3 hitPoint)
        {
            var trans = transform;
            var rayStartPos = trans.position;
            var rayEndPos = rayStartPos + trans.forward * projectionDepth;
            
            foreach( var triPolyInfo in _broadPhaseConvexPolygonInfos)
            {
                if (triPolyInfo.ConvexPolygon.IsIntersectRayToTriangle(ref hitPoint, rayStartPos, rayEndPos))
                {
                    // 衝突した。
                    basePointToNearClipDistance = Vector3.Distance(rayStartPos, hitPoint);
                    basePointToFarClipDistance = projectionDepth - basePointToNearClipDistance;
                    return true;
                }
            }
            // 衝突しなかった。
            return false;
        }
    }
}
