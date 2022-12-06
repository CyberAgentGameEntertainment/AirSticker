#define TEST_START_PROJECTION_METHOD // 有効でStartProjectionメソッドをテストする。

using System.Collections.Generic;
using CyDecal.Runtime.Scripts;
using CyDecal.Runtime.Scripts.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Demo.Demo_00.Scripts
{
    public class Demo00 : MonoBehaviour
    {
        [SerializeField] private Material[] decalMaterials;
        [SerializeField] private GameObject[] receiverObjects;
        [SerializeField] private GameObject[] moveImageObjects;
        [SerializeField] private Material[] urpDecalMaterials;
        [SerializeField] private GameObject _collectPolyInputFieldTextObject;
        private readonly List<List<CyDecalMesh>> _cyDecalMeshesList = new List<List<CyDecalMesh>>();
        private AgingTest _agingTest;
        private GameObject _currentProjectorObject;
        private int _currentReceiverObjectNo;
        private bool _isMouseLButtonPress;
        private Mode _mode = Mode.Normal;
        private Vector3 _projectorSize;
        private bool _runningAgingTest = false;
#if UNITY_2021_1_OR_NEWER
        private DecalProjector _urpDecalProjector;
#endif
        public int CurrentDecalMaterialIndex { get; set; }

        public bool IsLaunchReady { get; set; }

        // Start is called before the first frame update
        private void Start()
        {
            _agingTest = new AgingTest(this);
            var text = _collectPolyInputFieldTextObject.GetComponent<Text>();
            CyTrianglePolygonsFactory.MaxGeneratedPolygonPerFrame = int.Parse(text.text);
        }

        // Update is called once per frame
        private void Update()
        {
            switch (_mode)
            {
                case Mode.Normal:
                    UpdateNormal();
                    break;
                case Mode.AgingTest:
                    UpdateAgingTest();
                    break;
            }
        }

        /// <summary>
        ///     デカールを貼り付ける
        /// </summary>
        public void Launch(Vector2 screenPos, int decalMaterialIndex)
        {
            var ray = Camera.main.ScreenPointToRay(screenPos);
            var hit_info = new RaycastHit();
            var max_distance = 100f;

            var is_hit = Physics.Raycast(ray, out hit_info, max_distance);

            if (is_hit)
            {
                var projectorObj = new GameObject("Decal Projector");
                var projectorSize = new Vector3();
                projectorSize.x = 0.05f;
                projectorSize.y = 0.05f;
                if (decalMaterialIndex == 3) projectorSize.x *= 4.496f;
                projectorSize.z = 0.2f;
                projectorObj.transform.localPosition =
                    hit_info.point + Camera.main.transform.forward * -0.1f;
                CyDecalProjector.CreateAndLaunch(
                    projectorObj,
                    receiverObjects[_currentReceiverObjectNo],
                    decalMaterials[decalMaterialIndex],
                    projectorSize.x,
                    projectorSize.y,
                    projectorSize.z,
                    true,
                    () => { Destroy(projectorObj); });
            }
        }

        /// <summary>
        ///     エージングテスト中の更新処理。
        /// </summary>
        private void UpdateAgingTest()
        {
            _agingTest.Update();
        }

        /// <summary>
        ///     通常モードのときの更新処理。
        /// </summary>
        private void UpdateNormal()
        {
            if (Input.GetMouseButtonDown(0)) _isMouseLButtonPress = true;
            if (Input.GetMouseButtonUp(0)) _isMouseLButtonPress = false;
            if (_isMouseLButtonPress)
            {
                if (IsLaunchReady)
                {
                    moveImageObjects[CurrentDecalMaterialIndex].transform.position = Input.mousePosition;
                    moveImageObjects[CurrentDecalMaterialIndex].SetActive(true);
                    var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    var hit_info = new RaycastHit();
                    var max_distance = 100f;

                    var is_hit = Physics.Raycast(ray, out hit_info, max_distance);

                    if (is_hit)
                    {
#if UNITY_2021_1_OR_NEWER
                        moveImageObjects[CurrentDecalMaterialIndex].SetActive(false);
#endif
                        // デカールプロジェクターを生成
                        if (_currentProjectorObject == null)
                        {
                            _currentProjectorObject = new GameObject("Decal Projector");
                            _projectorSize = new Vector3();
                            _projectorSize.x = 0.05f;
                            _projectorSize.y = 0.05f;
                            if (CurrentDecalMaterialIndex == 3) _projectorSize.x *= 4.496f;
                            _projectorSize.z = 0.2f;
#if UNITY_2021_1_OR_NEWER
                            _urpDecalProjector = _currentProjectorObject.AddComponent<DecalProjector>();
                            _urpDecalProjector.size = _projectorSize;
                            var pivot = new Vector3();
                            pivot.z = _projectorSize.z * 0.5f;
                            _urpDecalProjector.pivot = pivot;
                            _urpDecalProjector.material = urpDecalMaterials[CurrentDecalMaterialIndex];
#endif
                        }

                        _currentProjectorObject.transform.localPosition =
                            hit_info.point + Camera.main.transform.forward * -0.1f;
                    }
                }
            }
            else
            {
                if (_currentProjectorObject != null)
                {
                    var projectorObj = _currentProjectorObject;
#if !TEST_START_PROJECTION_METHOD
                    var projector = CyDecalProjector.AddTo(
                        _currentProjectorObject,
                        receiverObjects[_currentReceiverObjectNo],
                        decalMaterials[CurrentDecalMaterialIndex],
                        _projectorSize.x,
                        _projectorSize.y,
                        _projectorSize.z,
                        true,
                        () =>
                        {
                            // 投影完了とともにオブジェクトを削除する。
                            Object.Destroy(projectorObj);
                        });
#else
                    var projector = CyDecalProjector.CreateAndLaunch(
                        _currentProjectorObject,
                        receiverObjects[_currentReceiverObjectNo],
                        decalMaterials[CurrentDecalMaterialIndex],
                        _projectorSize.x,
                        _projectorSize.y,
                        _projectorSize.z,
                        false,
                        null);
                    projector.Launch(
                        () =>
                        {
                            // 投影完了とともにオブジェクトも削除する。
                            Destroy(projectorObj);
                        });
#endif
                    _cyDecalMeshesList.Add(projector.DecalMeshes);
                }

                moveImageObjects[CurrentDecalMaterialIndex].SetActive(false);
                IsLaunchReady = false;
                _currentProjectorObject = null;
            }
        }

        /// <summary>
        ///     デカールメッシュをクリア
        /// </summary>
        public void ClearDecalMesh()
        {
            foreach (var decalMeshes in _cyDecalMeshesList)
            foreach (var decalMesh in decalMeshes)
                decalMesh.Clear();
            _cyDecalMeshesList.Clear();
        }

        /// <summary>
        ///     全てのレシーバーオブジェクトが削除されているか判定。
        /// </summary>
        /// <returns></returns>
        public bool IsDeleteAllReceiverObjects()
        {
            return !receiverObjects[0] && receiverObjects[1];
        }

        /// <summary>
        ///     レシーバーオブジェクトを次のオブジェクトにする。
        /// </summary>
        public void SetNextReceiverObject()
        {
            if (HasAnimatorInCurrentReceiverObject())
            {
                var animator = receiverObjects[_currentReceiverObjectNo].GetComponent<Animator>();
                animator.Rebind();
            }

            if (receiverObjects[_currentReceiverObjectNo]) receiverObjects[_currentReceiverObjectNo].SetActive(false);

            _currentReceiverObjectNo = (_currentReceiverObjectNo + 1) % receiverObjects.Length;
            if (receiverObjects[_currentReceiverObjectNo]) receiverObjects[_currentReceiverObjectNo].SetActive(true);
        }

        /// <summary>
        ///     現在のレシーバーオブジェクトがアニメーターを保持しているか調べる。
        /// </summary>
        /// <returns></returns>
        public bool HasAnimatorInCurrentReceiverObject()
        {
            if (receiverObjects[_currentReceiverObjectNo])
                return receiverObjects[_currentReceiverObjectNo].GetComponent<Animator>() != null;
            return false;
        }

        /// <summary>
        ///     現在のレシーバーオブジェクトを破棄。
        /// </summary>
        public void DeleteCurrentReceiverObject()
        {
            Destroy(receiverObjects[_currentReceiverObjectNo]);
        }

        /// <summary>
        ///     レシーバーオブジェクトのアニメーションを再生する。
        /// </summary>
        public void PlayAnimationToReceiverObject()
        {
            var animator = receiverObjects[_currentReceiverObjectNo].GetComponent<Animator>();
            if (animator) animator.enabled = true;
        }

        /// <summary>
        ///     レシーバーオブジェクトのアニメーションを再生する。
        /// </summary>
        public void StopAnimationToReceiverObject()
        {
            var animator = receiverObjects[_currentReceiverObjectNo].GetComponent<Animator>();
            if (animator)
            {
                animator.enabled = false;
                animator.Rebind();
            }
        }

        public void PlayRotateToCurrentReceiverObject()
        {
            receiverObjects[_currentReceiverObjectNo].GetComponent<Rotate>().enabled = true;
        }

        public void StopRotateToCurrentReceiverObject()
        {
            receiverObjects[_currentReceiverObjectNo].GetComponent<Rotate>().enabled = false;
            CyDecalSystem.ClearReceiverObjectTrianglePolygonsPool();
        }

        /// <summary>
        ///     エージングテストの開始
        /// </summary>
        public void StartAgingTest()
        {
            _mode = Mode.AgingTest;
        }

        /// <summary>
        ///     エージングテストの停止
        /// </summary>
        public void StopAgingTest()
        {
            _mode = Mode.Normal;
        }

        /// <summary>
        ///     デモのモード
        /// </summary>
        private enum Mode
        {
            Normal,
            AgingTest
        }
    }
}
