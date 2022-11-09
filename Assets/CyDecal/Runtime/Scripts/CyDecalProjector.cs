using System;
using System.Collections;
using System.Collections.Generic;
using CyDecal.Runtime.Scripts.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    ///     デカールプロジェクター
    /// </summary>
    public class CyDecalProjector : MonoBehaviour
    {
        [SerializeField] private float width; // デカールボックスの幅
        [SerializeField] private float height; // デカールボックスの高さ
        [SerializeField] private float depth; // デカールボックスの奥行
        [SerializeField] private GameObject receiverObject; // デカールを貼り付けるターゲットとなるオブジェクト
        [SerializeField] private Material decalMaterial; // デカールマテリアル
        [SerializeField] private Material urpDecalMaterial; // URPのデカールマテリアル
        [SerializeField] private bool projectionOnAwake; // インスタンスが生成されると、自動的にデカールの投影処理も開始する。
        [SerializeField] private UnityEvent onCompleteProjection; //　デカールの投影が完了したときに呼ばれるイベント。
        private readonly Vector4[] _clipPlanes = new Vector4[(int)ClipPlane.Num]; // 分割平面
        private float _basePointToFarClipDistance; // デカールを貼り付ける基準地点から、ファークリップまでの距離。
        private float _basePointToNearClipDistance; // デカールを貼り付ける基準地点から、ニアクリップまでの距離。
        private List<ConvexPolygonInfo> _broadPhaseConvexPolygonInfos = new List<ConvexPolygonInfo>();
        private readonly List<CyDecalMesh> _cyDecalMeshes = new List<CyDecalMesh>(); // デカールメッシュ。
        private CyDecalSpace _decalSpace; // デカール空間。
        private bool _destroy = false;
        /// <summary>
        /// 生成されたデカールメッシュのリストのプロパティ
        /// </summary>
        public List<CyDecalMesh> DecalMeshes => _cyDecalMeshes;
        private CyDecalProjector()
        {
            CyRenderDecalFeature.DecalProjectorCount++;
        }

        ~CyDecalProjector()
        {
            if (!_destroy)
            {
                // OnDestroyが呼ばれていない場合のために。
                CyRenderDecalFeature.DecalProjectorCount--;
            }
        }
        private IEnumerator ExecuteProjection()
        {
            InitializeOriginAxisInDecalSpace();

            // レシーバーオブジェクトのレンダラーのみを収集したいのだが、
            // レシーバーオブジェクトにデカールメッシュのレンダラーがぶら下がっているので
            // 一旦無効にする。
            CyRenderDecalFeature.DisableDecalMeshRenderers();
            CyRenderDecalFeature.GetDecalMeshes(_cyDecalMeshes, gameObject, receiverObject, decalMaterial);
            
            // 無効にしたレンダラーを戻す。
            CyRenderDecalFeature.EnableDecalMeshRenderers();

            if (CyRenderDecalFeature.ExistTrianglePolygons(receiverObject) == false)
            {
                yield return CyRenderDecalFeature.RegisterTrianglePolygons(
                    receiverObject, 
                    receiverObject.GetComponentsInChildren<MeshFilter>(), 
                    receiverObject.GetComponentsInChildren<MeshRenderer>(), 
                    receiverObject.GetComponentsInChildren<SkinnedMeshRenderer>());
            }

            if (!receiverObject)
            {
                // レシーバーオブジェクトが死亡しているのでここで処理を打ち切る。
                if (onCompleteProjection != null)
                {
                    onCompleteProjection.Invoke();
                }

                yield break;
            }
            var convexPolygonInfos = CyRenderDecalFeature.GetTrianglePolygons(
                receiverObject);
            _broadPhaseConvexPolygonInfos = CyBroadPhaseDetectionConvexPolygons.Execute(
                transform.position,
                _decalSpace.Ez,
                width,
                height,
                depth,
                convexPolygonInfos);
            if (IntersectRayToTrianglePolygons(out var hitPoint))
            {
                BuildClipPlanes(hitPoint);
                SplitConvexPolygonsByPlanes();
                AddTrianglePolygonsToDecalMeshFromConvexPolygons(hitPoint);
            }

            if (onCompleteProjection != null)
            {
                onCompleteProjection.Invoke();
            }

            yield return null;
        }
        //
        // Start is called before the first frame update
        private void Start()
        {
            if (projectionOnAwake)
            {
                StartProjection(null);
            }
        }
        private void OnDestroy()
        {
            _destroy = true;
            CyRenderDecalFeature.DecalProjectorCount--;
        }

        /// <summary>
        ///     CyDecalProjectorコンポーネントをゲームオブジェクトに追加作成
        /// </summary>
        /// <param name="owner">コンポーネントを追加するゲームオブジェクト</param>
        /// <param name="receiverObject">デカールを貼り付けるレシーバーオブジェクト</param>
        /// <param name="decalMaterial">デカールマテリアル</param>
        /// <param name="width">プロジェクターの幅</param>
        /// <param name="height">プロジェクターの高さ</param>
        /// <param name="depth">プロジェクターの深度</param>
        /// <param name="projectionOnAwake">コンポーネントの生成と同時にデカールの投影を始める/param>
        /// <param name="onCompletedProjection">デカール投影完了時に呼び出されるコールバック関数</param>
        public static CyDecalProjector AddTo(
            GameObject owner,
            GameObject receiverObject,
            Material decalMaterial,
            float width,
            float height,
            float depth,
            bool projectionOnAwake,
            UnityAction onCompletedProjection)
        {
            var projector = owner.AddComponent<CyDecalProjector>();
            projector.width = width;
            projector.height = height;
            projector.depth = depth;
            projector.receiverObject = receiverObject;
            projector.decalMaterial = decalMaterial;
            projector.projectionOnAwake = projectionOnAwake;
            projector.onCompleteProjection = new UnityEvent();
            if (onCompletedProjection != null)
            {
                projector.onCompleteProjection.AddListener(onCompletedProjection);
            }

            return projector;
        }
        /// <summary>
        /// デカール投影を始める。
        /// </summary>
        /// <remarks>
        /// この処理は非同期処理となっており、デカールの投影が完了するまで数フレームの遅延が発生します。
        /// デカールの投影の完了を監視する場合は、コールバック関数を指定してください。
        /// </remarks>
        public void StartProjection(UnityAction onCompletedProjection)
        {
            if (onCompletedProjection != null)
            {
                this.onCompleteProjection.AddListener(onCompletedProjection);
            }
            StartCoroutine(ExecuteProjection());
        }
        /// <summary>
        ///     デカール空間での規定軸を初期化。
        /// </summary>
        private void InitializeOriginAxisInDecalSpace()
        {
            var trans = transform;
            _decalSpace = new CyDecalSpace(trans.right, trans.up, trans.forward * -1.0f);
        }

        /// <summary>
        ///     デカールテクスチャを貼り付けるためのメッシュを多角形情報から構築する
        /// </summary>
        private void AddTrianglePolygonsToDecalMeshFromConvexPolygons(Vector3 originPosInDecalSpace)
        {
            var convexPolygons = new List<CyConvexPolygon>();
            foreach (var convexPolyInfo in _broadPhaseConvexPolygonInfos)
            {
                if (convexPolyInfo.IsOutsideClipSpace) continue;

                convexPolygons.Add(convexPolyInfo.ConvexPolygon);
            }

            foreach (var cyDecalMesh in _cyDecalMeshes)
                cyDecalMesh.AddPolygonsToDecalMesh(
                    convexPolygons,
                    originPosInDecalSpace,
                    _decalSpace.Ez,
                    _decalSpace.Ex,
                    _decalSpace.Ey,
                    width,
                    height);
        }

        /// <summary>
        ///     デカールボックスを構成している６平面の情報を使って、凸多角形を分割していく。
        /// </summary>
        private void SplitConvexPolygonsByPlanes()
        {
            // 凸多角形をクリップ平面で分割していく。
            foreach (var clipPlane in _clipPlanes)
            foreach (var convexPolyInfo in _broadPhaseConvexPolygonInfos)
            {
                // 平面の外側なので調査の対象外なのでスキップ
                if (convexPolyInfo.IsOutsideClipSpace) continue;

                convexPolyInfo.ConvexPolygon.SplitAndRemoveByPlane(
                    clipPlane, out var isOutsideClipSpace);
                convexPolyInfo.IsOutsideClipSpace = isOutsideClipSpace;
            }
        }

        /// <summary>
        ///     デカールテクスチャを貼り付けるための頂点をクリッピングする平面を構築する。
        /// </summary>
        /// <param name="basePoint">デカールテクスチャを貼り付ける基準座標</param>
        private void BuildClipPlanes(Vector3 basePoint)
        {
            var trans = transform;
            var decalSpaceTangentWS = _decalSpace.Ex;
            var decalSpaceBiNormalWS = _decalSpace.Ey;
            var decalSpaceNormalWS = _decalSpace.Ez;
            // Build left plane.
            _clipPlanes[(int)ClipPlane.Left] = new Vector4
            {
                x = decalSpaceTangentWS.x,
                y = decalSpaceTangentWS.y,
                z = decalSpaceTangentWS.z,
                w = width / 2.0f - Vector3.Dot(decalSpaceTangentWS, basePoint)
            };
            // Build right plane.
            _clipPlanes[(int)ClipPlane.Right] = new Vector4
            {
                x = -decalSpaceTangentWS.x,
                y = -decalSpaceTangentWS.y,
                z = -decalSpaceTangentWS.z,
                w = width / 2.0f + Vector3.Dot(decalSpaceTangentWS, basePoint)
            };
            // Build bottom plane.
            _clipPlanes[(int)ClipPlane.Bottom] = new Vector4
            {
                x = decalSpaceBiNormalWS.x,
                y = decalSpaceBiNormalWS.y,
                z = decalSpaceBiNormalWS.z,
                w = height / 2.0f - Vector3.Dot(decalSpaceBiNormalWS, basePoint)
            };
            // Build top plane.
            _clipPlanes[(int)ClipPlane.Top] = new Vector4
            {
                x = -decalSpaceBiNormalWS.x,
                y = -decalSpaceBiNormalWS.y,
                z = -decalSpaceBiNormalWS.z,
                w = height / 2.0f + Vector3.Dot(decalSpaceBiNormalWS, basePoint)
            };
            // Build front plane.
            _clipPlanes[(int)ClipPlane.Front] = new Vector4
            {
                x = -decalSpaceNormalWS.x,
                y = -decalSpaceNormalWS.y,
                z = -decalSpaceNormalWS.z,
                w = _basePointToNearClipDistance + Vector3.Dot(decalSpaceNormalWS, basePoint)
            };
            // Build back plane.
            _clipPlanes[(int)ClipPlane.Back] = new Vector4
            {
                x = decalSpaceNormalWS.x,
                y = decalSpaceNormalWS.y,
                z = decalSpaceNormalWS.z,
                w = _basePointToFarClipDistance - Vector3.Dot(decalSpaceNormalWS, basePoint)
            };
        }

        /// <summary>
        ///     デカールボックスの中心を通るレイとレシーバーオブジェクトの三角形オブジェクトの衝突判定を行う。
        /// </summary>
        /// <param name="hitPoint">衝突点の格納先</param>
        /// <returns>trueが帰ってきたら衝突している</returns>
        private bool IntersectRayToTrianglePolygons(out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            var trans = transform;
            var rayStartPos = trans.position;
            var rayEndPos = rayStartPos + trans.forward * depth;

            foreach (var triPolyInfo in _broadPhaseConvexPolygonInfos)
                if (triPolyInfo.ConvexPolygon.IsIntersectRayToTriangle(out hitPoint, rayStartPos, rayEndPos))
                {
                    _basePointToNearClipDistance = Vector3.Distance(rayStartPos, hitPoint);
                    _basePointToFarClipDistance = depth - _basePointToNearClipDistance;
                    return true;
                }

            return false;
        }

        /// <summary>
        ///     分割平面
        /// </summary>
        private enum ClipPlane
        {
            Left,
            Right,
            Bottom,
            Top,
            Front,
            Back,
            Num
        }
    }
}
