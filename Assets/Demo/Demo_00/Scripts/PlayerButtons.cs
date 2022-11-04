using CyDecal.Runtime.Scripts;
using CyDecal.Runtime.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.WSA;

namespace Demo.Demo_00.Scripts
{
    public class PlayerButtons : MonoBehaviour
    {
        [SerializeField] private GameObject receiverObject;
        [SerializeField] private GameObject playAnimTextObject;
        [SerializeField] private GameObject playRotTextObject;
        [SerializeField] private GameObject decalProjectorLauncherObject;
        private bool _isPlayAnim;

        private bool _isPlayRot;

        // Start is called before the first frame update
        private void Start()
        {
        }

        // Update is called once per frame
        private void Update()
        {
        }

        public void OnClickChange()
        {
            // 止まった。
            CyRenderDecalFeature.ClearReceiverObjectTrianglePolygonsPool();
            StopAnimation();
            StopRotation();
            var launcher = decalProjectorLauncherObject.GetComponent<DecalProjectorLauncher>();
            launcher.SetNextReceiverObject();
        }

        public void OnClickPlayAnim()
        {
            var launcher = decalProjectorLauncherObject.GetComponent<DecalProjectorLauncher>();
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
            var launcher = decalProjectorLauncherObject.GetComponent<DecalProjectorLauncher>();
            launcher.ClearDecalMesh();
        }
        public void OnClickRotate()
        {
            var text = playRotTextObject.GetComponent<Text>();
            text.text = _isPlayRot ? "Play Rot" : "Stop Rot";
            _isPlayRot = !_isPlayRot;
            var launcher = decalProjectorLauncherObject.GetComponent<DecalProjectorLauncher>();
            if (_isPlayRot)
                launcher.PlayRotateToCurrentReceiverObject();
            else
                launcher.StopRotateToCurrentReceiverObject();
        }

        public void StopAnimation()
        {
            if (!_isPlayAnim) return;
            _isPlayAnim = false;
            var launcher = decalProjectorLauncherObject.GetComponent<DecalProjectorLauncher>();
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
            var launcher = decalProjectorLauncherObject.GetComponent<DecalProjectorLauncher>();
            launcher.StopRotateToCurrentReceiverObject();
        }
    }
}
