using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CyDecal.Runtime.Scripts.Core;
using UnityEngine;
using UnityEngine.Events;

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
        public enum State
        {
            NotLaunch,
            Launching,
            LaunchingCompleted,
            LaunchingCanceled
        }

        [SerializeField] private float width; // デカールボックスの幅
        [SerializeField] private float height; // デカールボックスの高さ
        [SerializeField] private float depth; // デカールボックスの奥行
        [SerializeField] private GameObject receiverObject; // デカールを貼り付けるターゲットとなるオブジェクト
        [SerializeField] private Material decalMaterial; // デカールマテリアル

        [Tooltip("このチェックをつけるとインスタンスの生成と同時にデカールの投影処理が開始されます。")] [SerializeField]
        private bool launchOnAwake; // インスタンスが生成されると、自動的にデカールの投影処理も開始する。

        [SerializeField] private UnityEvent<State> onFinishedLaunch; //　デカールの投影処理が終了したときに呼ばれるイベント。

        private readonly Vector4[] _clipPlanes = new Vector4[(int)ClipPlane.Num]; // 分割平面
        private List<ConvexPolygonInfo> _broadPhaseConvexPolygonInfos = new List<ConvexPolygonInfo>();
        private List<ConvexPolygonInfo> _convexPolygonInfos;
        private CyDecalSpace _decalSpace; // デカール空間。
        private bool _executeLaunchingOnWorkerThread;
        public State NowState { get; private set; } = State.NotLaunch;

        /// <summary>
        ///     .
        ///     生成されたデカールメッシュのリストのプロパティ
        /// </summary>
        public List<CyDecalMesh> DecalMeshes { get; } = new List<CyDecalMesh>();

        private void Start()
        {
            if (launchOnAwake) Launch(null);
        }

        private void OnDestroy()
        {
            // 投影終了せずに削除された場合もコールバックを呼び出すために完了時の処理をコールする。
            OnFinished(State.LaunchingCanceled);
        }

        private void OnDrawGizmosSelected()
        {
            var cache = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            // Draw the decal box.
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, height, depth));
            Gizmos.matrix = cache;
            Gizmos.color = Color.white;
            // Draw the arrow of the projection's direction.
            var arrowStart = transform.position;
            var arrowEnd = transform.position + transform.forward * depth;
            Gizmos.DrawLine(arrowStart, arrowEnd);
            Vector3 arrowTangent;
            if (Mathf.Abs(transform.forward.y) > 0.999f)
                arrowTangent = Vector3.Cross(transform.forward, Vector3.right);
            else
                arrowTangent = Vector3.Cross(transform.forward, Vector3.up);
            var rotAxis = Vector3.Cross(transform.forward, arrowTangent);
            var rotQuat = Quaternion.AngleAxis(45.0f, rotAxis.normalized);
            var arrowLeft = rotQuat * transform.forward * depth * -0.2f;
            Gizmos.DrawLine(arrowEnd, arrowEnd + arrowLeft);
            rotQuat = Quaternion.AngleAxis(-45.0f, rotAxis.normalized);
            var arrowRight = rotQuat * transform.forward * depth * -0.2f;
            Gizmos.DrawLine(arrowEnd, arrowEnd + arrowRight);
            Gizmos.matrix = cache;
        }

        /// <summary>
        ///     投影終了時に呼び出される処理
        /// </summary>
        private void OnFinished(State finishedState)
        {
            if (onFinishedLaunch == null) return;

            onFinishedLaunch.Invoke(finishedState);
            NowState = finishedState;
            onFinishedLaunch = null;
        }

        /// <summary>
        ///     スキニングのための行列パレットを計算
        /// </summary>
        /// <param name="skinnedMeshRenderers">レシーバーオブジェクトのスキンメッシュレンダラー</param>
        /// <returns>計算された行列パレット</returns>
        private static Matrix4x4[][] CalculateMatricesPallet(SkinnedMeshRenderer[] skinnedMeshRenderers)
        {
            var boneMatricesPallet = new Matrix4x4[skinnedMeshRenderers.Length][];
            var skindMeshRendererNo = 0;
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (!skinnedMeshRenderer) continue;
                if (skinnedMeshRenderer.rootBone != null)
                {
                    var mesh = skinnedMeshRenderer.sharedMesh;
                    var numBone = skinnedMeshRenderer.bones.Length;

                    var boneMatrices = new Matrix4x4[numBone];
                    for (var boneNo = 0; boneNo < numBone; boneNo++)
                        boneMatrices[boneNo] = skinnedMeshRenderer.bones[boneNo].localToWorldMatrix
                                               * mesh.bindposes[boneNo];

                    boneMatricesPallet[skindMeshRendererNo] = boneMatrices;
                }

                skindMeshRendererNo++;
            }

            return boneMatricesPallet;
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

            var skinnedMeshRenderers = receiverObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            skinnedMeshRenderers = skinnedMeshRenderers.Where(s => s.name != "CyDecalRenderer").ToArray();

            if (CyDecalSystem.ReceiverObjectTrianglePolygonsPool.Contains(receiverObject) == false)
            {
                // 新規登録
                _convexPolygonInfos = new List<ConvexPolygonInfo>();
                // 三角形ポリゴン情報を構築する。
                yield return CyDecalSystem.BuildTrianglePolygonsFromReceiverObject(
                    receiverObject.GetComponentsInChildren<MeshFilter>(),
                    receiverObject.GetComponentsInChildren<MeshRenderer>(),
                    skinnedMeshRenderers,
                    _convexPolygonInfos);
                CyDecalSystem.ReceiverObjectTrianglePolygonsPool.RegisterConvexPolygons(receiverObject,
                    _convexPolygonInfos);
            }

            if (!receiverObject)
            {
                // レシーバーオブジェクトが死亡しているのでここで処理を打ち切る。
                OnFinished(State.LaunchingCanceled);
                yield break;
            }

            #region Prepare to run on worker threads.

            _convexPolygonInfos = CyDecalSystem.GetTrianglePolygonsFromPool(
                receiverObject);
            // Calculate bone matrix pallet.
            var boneMatricesPallet = CalculateMatricesPallet(skinnedMeshRenderers);

            var transform1 = transform;
            var projectorPosition = transform1.position;
            // basePosition is center of the decal box.
            var centerPositionOfDecalBox = projectorPosition + transform1.forward * (depth * 0.5f);

            for (var polyNo = 0; polyNo < _convexPolygonInfos.Count; polyNo++)
                _convexPolygonInfos[polyNo].ConvexPolygon.PrepareToRunOnWorkerThread();

            #endregion // Prepare to run on worker threads.

            #region Run worker thread.

            // Split Convex Polygon.
            _executeLaunchingOnWorkerThread = true;
            ThreadPool.QueueUserWorkItem(RunActionByWorkerThread, new Action(() =>
            {
                var localToWorldMatrices = new Matrix4x4[3];
                var boneWeights = new BoneWeight[3];
                for (var polyNo = 0; polyNo < _convexPolygonInfos.Count; polyNo++)
                    _convexPolygonInfos[polyNo].ConvexPolygon.CalculatePositionsAndNormalsInWorldSpace(
                        boneMatricesPallet, localToWorldMatrices, boneWeights);

                _broadPhaseConvexPolygonInfos = CyBroadPhaseConvexPolygonsDetection.Execute(
                    projectorPosition,
                    _decalSpace.Ez,
                    width,
                    height,
                    depth,
                    _convexPolygonInfos);

                BuildClipPlanes(centerPositionOfDecalBox);
                SplitConvexPolygonsByPlanes();
                AddTrianglePolygonsToDecalMeshFromConvexPolygons(centerPositionOfDecalBox);
                _executeLaunchingOnWorkerThread = false;
            }));

            #endregion // Run worker thread. 

            // Waiting to worker thread.
            while (_executeLaunchingOnWorkerThread) yield return null;

            foreach (var cyDecalMesh in DecalMeshes) cyDecalMesh.ExecutePostProcessingAfterWorkerThread();
            OnFinished(State.LaunchingCompleted);
            _convexPolygonInfos = null;

            yield return null;
        }

        /// <summary>
        ///     This function is called by worker thread.
        /// </summary>
        /// <param name="action"></param>
        private static void RunActionByWorkerThread(object action)
        {
            ((Action)action)();
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
            UnityAction<State> onCompletedLaunch)
        {
            var projector = owner.AddComponent<CyDecalProjector>();
            projector.width = width;
            projector.height = height;
            projector.depth = depth;
            projector.receiverObject = receiverObject;
            projector.decalMaterial = decalMaterial;
            projector.launchOnAwake = false;
            projector.onFinishedLaunch = new UnityEvent<State>();

            if (launchOnAwake) // コンポーネント追加と同時にプロジェクション開始。
                projector.Launch(onCompletedLaunch);
            else if (onCompletedLaunch != null) projector.onFinishedLaunch.AddListener(onCompletedLaunch);

            return projector;
        }

        /// <summary>
        ///     デカール投影を始める。
        /// </summary>
        /// <remarks>
        ///     この処理は非同期処理となっており、デカールの投影が完了するまで数フレームの遅延が発生します。
        ///     デカールの投影の完了を監視する場合は、コールバック関数を指定してください。
        /// </remarks>
        public void Launch(UnityAction<State> onFinishedLaunch)
        {
            if (NowState != State.NotLaunch)
                Debug.LogError("This function can be called only once, but it was called multiply.");

            NowState = State.Launching;
            if (onFinishedLaunch != null) this.onFinishedLaunch.AddListener(onFinishedLaunch);
            // Request the launching of the decal.
            CyDecalSystem.DecalProjectorLauncher.Request(
                this,
                () =>
                {
                    if (receiverObject)
                        StartCoroutine(ExecuteLaunch());
                    else
                        // レシーバーオブジェクトが削除されているので、ここで打ち切り。
                        OnFinished(State.LaunchingCanceled);
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
                cyDecalMesh.AddTrianglePolygonsToDecalMesh(
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
            var basePointToNearClipDistance = depth * 0.5f;
            var basePointToFarClipDistance = depth * 0.5f;
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
                w = basePointToNearClipDistance + Vector3.Dot(decalSpaceNormalWs, basePoint)
            };
            // Build back plane.
            _clipPlanes[(int)ClipPlane.Back] = new Vector4
            {
                x = decalSpaceNormalWs.x,
                y = decalSpaceNormalWs.y,
                z = decalSpaceNormalWs.z,
                w = basePointToFarClipDistance - Vector3.Dot(decalSpaceNormalWs, basePoint)
            };
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
