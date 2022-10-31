using System.Collections;
using System.Collections.Generic;
using CyDecal.Runtime.Scripts;
using UnityEngine;
using UnityEngine.UI;

public class PlayerButtons : MonoBehaviour
{
    [SerializeField] private GameObject receiverObject;
    [SerializeField] private GameObject playAnimTextObject;
    [SerializeField] private GameObject playRotTextObject;
    private bool _isPlayAnim;

    private bool _isPlayRot;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnClickPlayAnim()
    {
        var text = playAnimTextObject.GetComponent<Text>();
        text.text = _isPlayAnim ? "Play Anim" : "Stop Anim"; 
        _isPlayAnim = !_isPlayAnim;
        var animator = receiverObject.GetComponent<Animator>(); 
        animator.enabled = _isPlayAnim;
        if(!_isPlayAnim) animator.Rebind();
    }

    public void OnClickRotate()
    {   
        var text = playRotTextObject.GetComponent<Text>();
        text.text = _isPlayRot ? "Play Rot" : "Stop Rot"; 
        _isPlayRot = !_isPlayRot;
        var rotate = receiverObject.GetComponent<Rotate>();
        rotate.enabled = _isPlayRot;
        if (_isPlayRot)
        {
            // 止まった。
            CyRenderDecalFeature.ClearReceiverObjectTrianglePolygonsPool();
        }
    }
}
