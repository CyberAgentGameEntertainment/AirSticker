// --------------------------------------------------------------
// Copyright 2022 CyberAgent, Inc.
// --------------------------------------------------------------

using CyDecal.Runtime.Scripts;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace Demo.Demo_00.Scripts
{
    public class DisplayLogForDemo2 : MonoBehaviour
    {

        // Start is called before the first frame update
        [SerializeField] private GameObject fpsTextGameObject;
        private Text _logText;
        public DisplayLogForDemo2()
        {
            Instance = this;
        }

        public static DisplayLogForDemo2 Instance { get; set; }

        private void Start()
        {
            Application.targetFrameRate = 120;
            _logText = fpsTextGameObject.GetComponent<Text>();
        }

        // Update is called once per frame
        private void Update()
        {
            var usedMemory = ( Profiler.GetTotalAllocatedMemoryLong() ) / 1024f / 1024f;
            
            var fps = 1.0f / Time.deltaTime;
            _logText.text = $"FPS = {fps:0.00}\n"
                            + $"Decal Mesh Pool Size = {CyRenderDecalFeature.GetDecalMeshPoolSize()}\n"
                            + $"Triangle Polygons Pool Size = {CyRenderDecalFeature.GetReceiverObjectTrianglePolygonsPoolSize()}\n"
                            + $"Used Memory = {usedMemory:0.0} MB\n"
                            + $"Running CyDecalProjector = {CyRenderDecalFeature.DecalProjectorCount}\n";
        }
    }
}
