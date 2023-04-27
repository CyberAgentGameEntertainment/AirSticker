using AirSticker.Runtime.Scripts.Core;
using Demo.Demo_02.Scripts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Demo.Demo_03.Scripts
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

        public void OnClickChange()
        {
            var launcher = decalProjectorLauncherObject.GetComponent<Demo03>();
            launcher.SetNextReceiverObject();

            if (_isPlayAnim)
                launcher.PlayAnimationToReceiverObject();
            else
                launcher.StopAnimationToReceiverObject();

            if (_isPlayRot)
                launcher.PlayRotateToCurrentReceiverObject();
            else
                launcher.StopRotateToCurrentReceiverObject();
        }

        public void OnClickPlayAnim()
        {
            var launcher = decalProjectorLauncherObject.GetComponent<Demo03>();
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
            var launcher = decalProjectorLauncherObject.GetComponent<Demo03>();
            launcher.ClearDecalMesh();
        }

        public void OnClickRotate()
        {
            var text = playRotTextObject.GetComponent<Text>();
            text.text = _isPlayRot ? "Play Rot" : "Stop Rot";
            _isPlayRot = !_isPlayRot;
            var launcher = decalProjectorLauncherObject.GetComponent<Demo03>();
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
            var launcher = decalProjectorLauncherObject.GetComponent<Demo03>();
            launcher.DeleteCurrentReceiverObject();
        }

        public void OnEndEdit_CollectPolyInputField(Object textObj)
        {
            var go = (GameObject)textObj;
            var text = go.GetComponent<Text>();

            var result = int.TryParse(text.text, out var value);
            if (result) TrianglePolygonsFactory.MaxGeneratedPolygonPerFrame = value;
        }

        public void StopAnimation()
        {
            if (!_isPlayAnim) return;
            _isPlayAnim = false;
            var launcher = decalProjectorLauncherObject.GetComponent<Demo03>();
            if (launcher.HasAnimatorInCurrentReceiverObject() == false) return;
            var text = playAnimTextObject.GetComponent<Text>();
            text.text = "Play Anim";
            launcher.StopAnimationToReceiverObject();
        }

        public void StopRotation()
        {
            if (!_isPlayRot) return;
            var text = playRotTextObject.GetComponent<Text>();
            text.text = "Play Rot";
            _isPlayRot = false;
            var launcher = decalProjectorLauncherObject.GetComponent<Demo03>();
            launcher.StopRotateToCurrentReceiverObject();
        }

        public void OnClickRunAgingTest()
        {
            _isRunningAgingTest = !_isRunningAgingTest;
            SetupAgingTest();
        }

        private void SetupAgingTest()
        {
            var launcher = decalProjectorLauncherObject.GetComponent<Demo03>();
            var text = runningAgingTestTextObject.GetComponent<Text>();
            if (_isRunningAgingTest)
            {
                launcher.StartAgingTest();
                text.text = "Stop Aging Test";
            }
            else
            {
                launcher.StopAgingTest();
                text.text = "Run Aging Test";
            }
        }
    }
}
