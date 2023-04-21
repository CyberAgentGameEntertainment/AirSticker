#define TEST_START_PROJECTION_METHOD // If this symbol is defined, Test the StartProjection method.

using System.Collections.Generic;
using AirSticker.Runtime.Scripts;
using AirSticker.Runtime.Scripts.Core;
using UnityEngine;
#if UNITY_2021_1_OR_NEWER
using UnityEngine.Rendering.Universal;
#endif
using UnityEngine.UI;

namespace Demo.Demo_00.Scripts
{
    public class Demo00 : MonoBehaviour
    {
        [SerializeField] private Material[] decalMaterials;
        [SerializeField] private GameObject[] receiverObjects;
        [SerializeField] private GameObject[] moveImageObjects;
        [SerializeField] private Material[] urpDecalMaterials;
        [SerializeField] private Vector3[] projectorSize;
        [SerializeField] private GameObject _collectPolyInputFieldTextObject;
        private readonly List<List<DecalMesh>> _decalMeshesList = new List<List<DecalMesh>>();
        private AgingTest _agingTest;
        private GameObject _currentProjectorObject;
        private int _currentReceiverObjectNo;
        private bool _isMouseLButtonPress;
        private Mode _mode = Mode.Normal;
        private Vector3 _projectorSize;
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
            TrianglePolygonsFactory.MaxGeneratedPolygonPerFrame = int.Parse(text.text);
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
                
                projectorObj.transform.localPosition =
                    hit_info.point + Camera.main.transform.forward * -0.1f;
                AirStickerProjector.CreateAndLaunch(
                    projectorObj,
                    receiverObjects[_currentReceiverObjectNo],
                    decalMaterials[decalMaterialIndex],
                    projectorSize[decalMaterialIndex].x,
                    projectorSize[decalMaterialIndex].y,
                    projectorSize[decalMaterialIndex].z,
                    true,
                    result => { Destroy(projectorObj); });
            }
        }

        private void UpdateAgingTest()
        {
            _agingTest.Update();
        }

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
                        // Create the Decal Projector.
                        if (_currentProjectorObject == null)
                        {
                            _currentProjectorObject = new GameObject("Decal Projector");
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
                    var projector = AirStickerProjector.AddTo(
                        _currentProjectorObject,
                        receiverObjects[_currentReceiverObjectNo],
                        decalMaterials[CurrentDecalMaterialIndex],
                        _projectorSize.x,
                        _projectorSize.y,
                        _projectorSize.z,
                        true,
                        () =>
                        {
                            // The projector is deleted when the launching process is finished.
                            Object.Destroy(projectorObj);
                        });
#else
                    var projector = AirStickerProjector.CreateAndLaunch(
                        _currentProjectorObject,
                        receiverObjects[_currentReceiverObjectNo],
                        decalMaterials[CurrentDecalMaterialIndex],
                        projectorSize[CurrentDecalMaterialIndex].x,
                        projectorSize[CurrentDecalMaterialIndex].y,
                        projectorSize[CurrentDecalMaterialIndex].z,
                        false,
                        null);
                    projector.Launch(
                        result =>
                        {
                            // The projector is deleted when the launching process is finished.
                            Destroy(projectorObj);
                        });
#endif
                    _decalMeshesList.Add(projector.DecalMeshes);
                }

                moveImageObjects[CurrentDecalMaterialIndex].SetActive(false);
                IsLaunchReady = false;
                _currentProjectorObject = null;
            }
        }

        public void ClearDecalMesh()
        {
            foreach (var decalMeshes in _decalMeshesList)
            foreach (var decalMesh in decalMeshes)
                decalMesh.Clear();
            _decalMeshesList.Clear();
        }


        public bool HasReceiverObjectsDeleted()
        {
            return !receiverObjects[0] && !receiverObjects[1];
        }

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

        public bool HasAnimatorInCurrentReceiverObject()
        {
            if (receiverObjects[_currentReceiverObjectNo])
                return receiverObjects[_currentReceiverObjectNo].GetComponent<Animator>() != null;
            return false;
        }

        public void DeleteCurrentReceiverObject()
        {
            Destroy(receiverObjects[_currentReceiverObjectNo]);
        }

        public void PlayAnimationToReceiverObject()
        {
            var animator = receiverObjects[_currentReceiverObjectNo].GetComponent<Animator>();
            if (animator) animator.enabled = true;
        }

        public void StopAnimationToReceiverObject()
        {
            var animator = receiverObjects[_currentReceiverObjectNo].GetComponent<Animator>();
            if (animator) animator.enabled = false;
        }

        public void PlayRotateToCurrentReceiverObject()
        {
            receiverObjects[_currentReceiverObjectNo].GetComponent<Rotate>().enabled = true;
        }

        public void StopRotateToCurrentReceiverObject()
        {
            receiverObjects[_currentReceiverObjectNo].GetComponent<Rotate>().enabled = false;
        }

        public void StartAgingTest()
        {
            _mode = Mode.AgingTest;
        }

        public void StopAgingTest()
        {
            _mode = Mode.Normal;
        }

        private enum Mode
        {
            Normal,
            AgingTest
        }
    }
}
