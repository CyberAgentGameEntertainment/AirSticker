using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    ///     デカールプロジェクター
    /// </summary>
    public class CyDecalProjector : MonoBehaviour
    {
        private static List<ConvexPolygonInfo> _convexPolygonInfos; // ブロードフェーズのジョブワーク用の凸ポリゴンのリスト
        
        [SerializeField] private float width; // デカールボックスの幅
        [SerializeField] private float height; // デカールボックスの高さ
        [FormerlySerializedAs("projectionDepth")] [SerializeField] private float depth; // デカールボックスの奥行
        [SerializeField] private GameObject receiverObject; // デカールを貼り付けるターゲットとなるオブジェクト
        [SerializeField] private Material decalMaterial; // デカールマテリアル
        private readonly Vector4[] _clipPlanes = new Vector4[(int)ClipPlane.Num]; // 分割平面
        private float _basePointToFarClipDistance; // デカールを貼り付ける基準地点から、ファークリップまでの距離。
        private float _basePointToNearClipDistance; // デカールを貼り付ける基準地点から、ニアクリップまでの距離。
        private List<ConvexPolygonInfo> _broadPhaseConvexPolygonInfos = new();
        private List<CyDecalMesh> _cyDecalMeshes; // デカールメッシュ。
        private CyDecalSpace _decalSpace; // デカール空間。
        /// <summary>
        /// 初期化
        /// </summary>
        /// <param name="receiverObject">デカールを貼り付けるレシーバーオブジェクト</param>
        /// <param name="decalMaterial">デカールマテリアル</param>
        /// <param name="width">プロジェクターの幅</param>
        /// <param name="height">プロジェクターの高さ</param>
        /// <param name="depth">プロジェクターの深度</param>
        public void Initialyze(
            GameObject receiverObject,
            Material decalMaterial, 
            float width, 
            float height, 
            float depth)
        {
            this.width = width;
            this.height = height;
            this.depth = depth;
            this.receiverObject = receiverObject;
            this.decalMaterial = decalMaterial;
        }
        //
        // Start is called before the first frame update
        private void Start()
        {
            var urpProjector = gameObject.GetComponent<DecalProjector>();
            if (urpProjector != null) urpProjector.enabled = false;

            // デカール空間の規定軸と原点を初期化する。
            InitializeOriginAxisInDecalSpace();
            // 作業を行うデカールメッシュの編集を開始する。
            _cyDecalMeshes = CyRenderDecalFeature.BeginEditDecalMeshes(gameObject, receiverObject, decalMaterial);
            // デカールを貼り付けるレシーバーオブジェクトから三角形ポリゴン情報を取得する。
            _convexPolygonInfos = CyRenderDecalFeature.GetTrianglePolygons(receiverObject);
            // デカールを貼り付ける三角形ポリゴンの早期枝切を行う。
            _broadPhaseConvexPolygonInfos = CyBroadPhaseDetectionConvexPolygons.Execute(
                transform.position,
                _decalSpace.Ez,
                width,
                height,
                depth,
                _convexPolygonInfos);
            // 三角形ポリゴンとレイとの衝突判定
            if (IntersectRayToTrianglePolygons(out var hitPoint))
            {
                // クリップ平面を構築。
                BuildClipPlanes(hitPoint);
                // クリップ平面で凸多角形ポリゴンを分割していく。
                SplitConvexPolygonsByPlanes();
                // 分割された凸多角形情報から三角形ポリゴンをデカールメッシュに追加していく。
                AddTrianglePolygonsToDecalMeshFromConvexPolygons(hitPoint);
            }

            // デカールメッシュの編集終了。
            CyRenderDecalFeature.EndEditDecalMeshes(_cyDecalMeshes);
            
            Object.Destroy(gameObject);
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
