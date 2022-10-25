// --------------------------------------------------------------
// Copyright 2022 CyberAgent, Inc.
// --------------------------------------------------------------

using System.Collections.Generic;
using CyDecal.Runtime.Scripts;
using UnityEngine;
using UnityEngine.UI;

public class FPSDisplay : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private GameObject fpsTextGameObject;
    private readonly List<float> _fpsCounts = new List<float>();
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
        if (_frameCount > 1000)
        {
            var fpsAverage = 0.0f;
            foreach (var fps in _fpsCounts) fpsAverage += fps;

            fpsAverage /= _fpsCounts.Count;
            _fpsText.text = $"FPS(Average) = {fpsAverage:0.00}";
        }
        else
        {
            var fps = 1.0f / Time.deltaTime;
            _fpsText.text = $"FPS = {fps:0.00}";
            _fpsCounts.Add(fps);
        }
       /*var time = CyRenderDecalFeature.splitMeshTotalTime;
       _fpsText.text = $"{time} msec";*/
    }
}