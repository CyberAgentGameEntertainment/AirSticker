using CyDecal.Runtime.Scripts;
using UnityEngine;
using UnityEngine.UI;

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

        var launcher = decalProjectorLauncherObject.GetComponent<DecalProjectorLuncher>();
        launcher.SetNextReceiverObject();
    }

    public void OnClickPlayAnim()
    {
        var launcher = decalProjectorLauncherObject.GetComponent<DecalProjectorLuncher>();
        if (launcher.HasAnimatorInCurrentReceiverObject() == false) return;
        var text = playAnimTextObject.GetComponent<Text>();
        text.text = _isPlayAnim ? "Play Anim" : "Stop Anim";
        _isPlayAnim = !_isPlayAnim;

        if (_isPlayAnim)
            launcher.PlayAnimationToReceiverObject();
        else
            launcher.StopAnimationToReceiverObject();
    }

    public void OnClickRotate()
    {
        var text = playRotTextObject.GetComponent<Text>();
        text.text = _isPlayRot ? "Play Rot" : "Stop Rot";
        _isPlayRot = !_isPlayRot;
        var launcher = decalProjectorLauncherObject.GetComponent<DecalProjectorLuncher>();
        if (_isPlayRot)
            launcher.PlayRotateToCurrentReceiverObject();
        else
            launcher.StopRotateToCurrentReceiverObject();
    }
}
