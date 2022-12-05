using System.Collections;
using System.Collections.Generic;
using CyDecal.Runtime.Scripts.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    ///     デカールプロジェクター
    /// </summary>
    /// <remarks>
    ///     デカールの投影が完了するには複数フレームかかる場合があり、インスタンスを生成後すぐに処理が終わるわけではありません。
    ///     投影完了をチェックしたい場合はコールバック関数を設定して監視を行ってください。
    /// </remarks>
    public sealed class CyDecalProjector : MonoBehaviour
    {
        [SerializeField] private float width; // デカールボックスの幅
        [SerializeField] private float height; // デカールボックスの高さ
        [SerializeField] private float depth; // デカールボックスの奥行
        [SerializeField] private GameObject receiverObject; // デカールを貼り付けるターゲットとなるオブジェクト
        [SerializeField] private Material decalMaterial; // デカールマテリアル

        [Tooltip("このチェックを外す場合は、デカールを投影するためには明示的にLaunchメソッドを呼び出す必要があります。")] [SerializeField]
        private bool launchOnAwake; // インスタンスが生成されると、自動的にデカールの投影処理も開始する。

        [FormerlySerializedAs("onCompleteLaunch")] [SerializeField]
        private UnityEvent onCompletedLaunch; //　デカールの投影が完了したときに呼ばれるイベント。

        private readonly Vector4[] _clipPlanes = new Vector4[(int)ClipPlane.Num]; // 分割平面
        private float _basePointToFarClipDistance; // デカールを貼り付ける基準地点から、ファークリップまでの距離。
        private float _basePointToNearClipDistance; // デカールを貼り付ける基準地点から、ニアクリップまでの距離。
        private List<ConvexPolygonInfo> _broadPhaseConvexPolygonInfos = new List<ConvexPolygonInfo>();
        private CyDecalSpace _decalSpace; // デカール空間。
        private bool _destroy;
        public bool IsCompletedLaunch { get; private set; }

        /// <summary>.
        ///     生成されたデカールメッシュのリストのプロパティ
        /// </summary>
        public List<CyDecalMesh> DecalMeshes { get; } = new List<CyDecalMesh>();

        private void Start()
        {
            if (launchOnAwake) Launch(null);
        }

        private void OnDestroy()
        {
            // 投影完了せずに削除された場合もコールバックを呼び出すために完了時の処理をコールする。
            OnCompleted();
        }

        /// <summary>
        ///     投影完了時に呼び出される処理
        /// </summary>
        private void OnCompleted()
        {
            onCompletedLaunch?.Invoke();
            IsCompletedLaunch = true;
            onCompletedLaunch = null;
        }

        /// <summary>
        ///     デカールの投影処理を実行。
        /// </summary>
        /// <remarks>
        ///     この処理は複数フレームにわたって実行されます。
        ///     投影の完了の監視はコールバック関数を利用するか、IsFinishedLaunchプロパティーをチェックすることで行えます。
        /// </remarks>
        /// <returns></returns>
        private IEnumerator ExecuteLaunch()
        {
            InitializeOriginAxisInDecalSpace();
            
            // 編集するデカールメッシュを収集する。
            CyDecalSystem.CollectEditDecalMeshes(DecalMeshes, receiverObject, decalMaterial);

            List<ConvexPolygonInfo> convexPolygonInfos;
            if (CyDecalSystem.ContainsTrianglePolygonsInPool(receiverObject) == false)
            {
                // 新規登録
                convexPolygonInfos = new List<ConvexPolygonInfo>();
                // 三角形ポリゴン情報を構築する。
                yield return CyDecalSystem.BuildTrianglePolygonsFromReceiverObject(
                    receiverObject.GetComponentsInChildren<MeshFilter>(),
                    receiverObject.GetComponentsInChildren<MeshRenderer>(),
                    receiverObject.GetComponentsInChildren<SkinnedMeshRenderer>(),
                    convexPolygonInfos);
                CyDecalSystem.RegisterTrianglePolygonsToPool(receiverObject, convexPolygonInfos);
            }

            if (!receiverObject)
            {
                // レシーバーオブジェクトが死亡しているのでここで処理を打ち切る。
                OnCompleted();
                yield break;
            }

            convexPolygonInfos = CyDecalSystem.GetTrianglePolygonsFromPool(
                receiverObject);
            _broadPhaseConvexPolygonInfos = CyBroadPhaseConvexPolygonsDetection.Execute(
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

            OnCompleted();
            yield return null;
        }

        /// <summary>
        ///     編集するデカールメッシュのリストを構築する。
        /// </summary>
        private void BuildEditDecalMeshes()
        {
            
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
        /// <param name="launchOnAwake">
        ///     trueが設定されている場合は、コンポーネントの追加と同時にデカールの投影処理が始まります。
        ///     falseを指定している場合は、明示的にLaunchメソッドを呼び出すことで、デカールの投影処理が始まります。
        /// </param>
        /// <param name="onCompletedLaunch">デカール投影完了時に呼び出されるコールバック関数</param>
        public static CyDecalProjector CreateAndLaunch(
            GameObject owner,
            GameObject receiverObject,
            Material decalMaterial,
            float width,
            float height,
            float depth,
            bool launchOnAwake,
            UnityAction onCompletedLaunch)
        {
            var projector = owner.AddComponent<CyDecalProjector>();
            projector.width = width;
            projector.height = height;
            projector.depth = depth;
            projector.receiverObject = receiverObject;
            projector.decalMaterial = decalMaterial;
            projector.launchOnAwake = false;
            projector.onCompletedLaunch = new UnityEvent();

            if (launchOnAwake) // コンポーネント追加と同時にプロジェクション開始。
                projector.Launch(onCompletedLaunch);
            else if (onCompletedLaunch != null) projector.onCompletedLaunch.AddListener(onCompletedLaunch);

            return projector;
        }

        /// <summary>
        ///     デカール投影を始める。
        /// </summary>
        /// <remarks>
        ///     この処理は非同期処理となっており、デカールの投影が完了するまで数フレームの遅延が発生します。
        ///     デカールの投影の完了を監視する場合は、コールバック関数を指定してください。
        /// </remarks>
        public void Launch(UnityAction onCompletedLaunch)
        {
            if (onCompletedLaunch != null) this.onCompletedLaunch.AddListener(onCompletedLaunch);
            // リクエストキューに積む。
            CyDecalSystem.EnqueueRequestLaunchDecalProjector(
                this,
                () =>
                {
                    if (receiverObject)
                        StartCoroutine(ExecuteLaunch());
                    else
                        // レシーバーオブジェクトが削除されているので、ここで打ち切り。
                        OnCompleted();
                });
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

            foreach (var cyDecalMesh in DecalMeshes)
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
            var decalSpaceTangentWs = _decalSpace.Ex;
            var decalSpaceBiNormalWs = _decalSpace.Ey;
            var decalSpaceNormalWs = _decalSpace.Ez;
            // Build left plane.
            _clipPlanes[(int)ClipPlane.Left] = new Vector4
            {
                x = decalSpaceTangentWs.x,
                y = decalSpaceTangentWs.y,
                z = decalSpaceTangentWs.z,
                w = width / 2.0f - Vector3.Dot(decalSpaceTangentWs, basePoint)
            };
            // Build right plane.
            _clipPlanes[(int)ClipPlane.Right] = new Vector4
            {
                x = -decalSpaceTangentWs.x,
                y = -decalSpaceTangentWs.y,
                z = -decalSpaceTangentWs.z,
                w = width / 2.0f + Vector3.Dot(decalSpaceTangentWs, basePoint)
            };
            // Build bottom plane.
            _clipPlanes[(int)ClipPlane.Bottom] = new Vector4
            {
                x = decalSpaceBiNormalWs.x,
                y = decalSpaceBiNormalWs.y,
                z = decalSpaceBiNormalWs.z,
                w = height / 2.0f - Vector3.Dot(decalSpaceBiNormalWs, basePoint)
            };
            // Build top plane.
            _clipPlanes[(int)ClipPlane.Top] = new Vector4
            {
                x = -decalSpaceBiNormalWs.x,
                y = -decalSpaceBiNormalWs.y,
                z = -decalSpaceBiNormalWs.z,
                w = height / 2.0f + Vector3.Dot(decalSpaceBiNormalWs, basePoint)
            };
            // Build front plane.
            _clipPlanes[(int)ClipPlane.Front] = new Vector4
            {
                x = -decalSpaceNormalWs.x,
                y = -decalSpaceNormalWs.y,
                z = -decalSpaceNormalWs.z,
                w = _basePointToNearClipDistance + Vector3.Dot(decalSpaceNormalWs, basePoint)
            };
            // Build back plane.
            _clipPlanes[(int)ClipPlane.Back] = new Vector4
            {
                x = decalSpaceNormalWs.x,
                y = decalSpaceNormalWs.y,
                z = decalSpaceNormalWs.z,
                w = _basePointToFarClipDistance - Vector3.Dot(decalSpaceNormalWs, basePoint)
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
