using CyDecal.Runtime.Scripts;
using CyDecal.Runtime.Scripts.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Demo.Demo_00.Scripts
{
    public class DemoGUIs : MonoBehaviour
    {
        private static bool _isRunningAgingTest;
        [SerializeField] private GameObject playAnimTextObject;
        [SerializeField] private GameObject playRotTextObject;
        [SerializeField] private GameObject decalProjectorLauncherObject;
        [SerializeField] private GameObject runningAgingTestTextObject;
        private bool _isPlayAnim;

        private bool _isPlayRot;

        // Start is called before the first frame update
        private void Start()
        {
            SetupAgingTest();
        }

        // Update is called once per frame
        private void Update()
        {
        }

        public void OnClickChange()
        {
            // 止まった。
            CyDecalSystem.ReceiverObjectTrianglePolygonsPool.Clear();
            StopAnimation();
            StopRotation();
            var launcher = decalProjectorLauncherObject.GetComponent<Demo00>();
            launcher.SetNextReceiverObject();
        }

        public void OnClickPlayAnim()
        {
            var launcher = decalProjectorLauncherObject.GetComponent<Demo00>();
            if (launcher.HasAnimatorInCurrentReceiverObject() == false) return;
            var text = playAnimTextObject.GetComponent<Text>();
            text.text = _isPlayAnim ? "Play Anim" : "Stop Anim";
            _isPlayAnim = !_isPlayAnim;

            if (_isPlayAnim)
                launcher.PlayAnimationToReceiverObject();
            else
                launcher.StopAnimationToReceiverObject();
        }

        public void OnClickClear()
        {
            var launcher = decalProjectorLauncherObject.GetComponent<Demo00>();
            launcher.ClearDecalMesh();
        }

        public void OnClickRotate()
        {
            var text = playRotTextObject.GetComponent<Text>();
            text.text = _isPlayRot ? "Play Rot" : "Stop Rot";
            _isPlayRot = !_isPlayRot;
            var launcher = decalProjectorLauncherObject.GetComponent<Demo00>();
            if (_isPlayRot)
                launcher.PlayRotateToCurrentReceiverObject();
            else
                launcher.StopRotateToCurrentReceiverObject();
        }

        public void OnClickResetScene()
        {
            SceneManager.LoadScene("Demo_00");
        }

        public void OnClickDeleteObject()
        {
            var launcher = decalProjectorLauncherObject.GetComponent<Demo00>();
            launcher.DeleteCurrentReceiverObject();
        }

        public void OnEndEdit_CollectPolyInputField(Object textObj)
        {
            var go = (GameObject)textObj;
            var text = go.GetComponent<Text>();

            var result = int.TryParse(text.text, out var value);
            if (result) CyTrianglePolygonsFactory.MaxGeneratedPolygonPerFrame = value;
        }

        public void StopAnimation()
        {
            /*if (!_isPlayAnim) return;
            _isPlayAnim = false;
            var launcher = decalProjectorLauncherObject.GetComponent<Demo00>();
            if (launcher.HasAnimatorInCurrentReceiverObject() == false) return;
            var text = playAnimTextObject.GetComponent<Text>();
            text.text = "Play Anim";
            launcher.StopAnimationToReceiverObject();*/
        }

        public void StopRotation()
        {
            /*if (!_isPlayRot) return;
            var text = playRotTextObject.GetComponent<Text>();
            text.text = "Play Rot";
            _isPlayRot = false;
            var launcher = decalProjectorLauncherObject.GetComponent<Demo00>();
            launcher.StopRotateToCurrentReceiverObject();*/
        }

        public void OnClickRunAgingTest()
        {
            _isRunningAgingTest = !_isRunningAgingTest;
            SetupAgingTest();
        }

        private void SetupAgingTest()
        {
            var launcher = decalProjectorLauncherObject.GetComponent<Demo00>();
            var text = runningAgingTestTextObject.GetComponent<Text>();
            if (_isRunningAgingTest)
            {
                // エージングテスト開始
                launcher.StartAgingTest();
                text.text = "Stop Aging Test";
            }
            else
            {
                // エージングテスト終了
                launcher.StopAgingTest();
                text.text = "Run Aging Test";
            }
        }
    }
}
