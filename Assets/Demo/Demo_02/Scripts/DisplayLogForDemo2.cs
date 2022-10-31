// --------------------------------------------------------------
// Copyright 2022 CyberAgent, Inc.
// --------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DisplayLogForDemo2 : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private GameObject fpsTextGameObject;
    private readonly List<float> _fpsCounts = new();
    private Text _fpsText;
    private int _frameCount;

    private void Start()
    {
        Application.targetFrameRate = 120;
        _fpsText = fpsTextGameObject.GetComponent<Text>();
    }

    // Update is called once per frame
    private void Update()
    {
        _frameCount++;
  
        var fps = 1.0f / Time.deltaTime;
        _fpsText.text = $"FPS = {fps:0.00}";
        
        /*var time = CyRenderDecalFeature.splitMeshTotalTime;
        _fpsText.text = $"{time} msec";*/
    }
}
