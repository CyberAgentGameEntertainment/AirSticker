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
    private Text _logText;
    public float FirstDecalTime { get; set; }
    public float BuildTrianglePolygons { get; set; }
    public static DisplayLogForDemo2 Instance { get; set; }

    public DisplayLogForDemo2()
    {
        Instance = this;
    }
    private void Start()
    {
        Application.targetFrameRate = 120;
        _logText = fpsTextGameObject.GetComponent<Text>();
    }

    // Update is called once per frame
    private void Update()
    {
        var fps = 1.0f / Time.deltaTime;
        _logText.text = $"FPS = {fps:0.00}\n";
        _logText.text += $"First decal time = {FirstDecalTime}\n";
        _logText.text += $"Build tri poly time = {BuildTrianglePolygons}";
        /*var time = CyRenderDecalFeature.splitMeshTotalTime;
        _fpsText.text = $"{time} msec";*/
    }
}
