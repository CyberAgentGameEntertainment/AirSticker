// --------------------------------------------------------------
// Copyright 2022 CyberAgent, Inc.
// --------------------------------------------------------------

using AirSticker.Runtime.Scripts;
using AirSticker.Runtime.Scripts.Core;
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
            var usedMemory = Profiler.GetTotalAllocatedMemoryLong() / 1024f / 1024f;

            var fps = 1.0f / Time.deltaTime;
            _logText.text = $"FPS = {fps:0.00}\n"
                            + $"Decal Mesh Pool Size = {AirStickerSystem.DecalMeshPool.GetPoolSize()}\n"
                            + $"Triangle Polygons Pool Size = {AirStickerSystem.ReceiverObjectTrianglePolygonsPool.GetPoolSize()}\n"
                            + $"Used Memory = {usedMemory:0.0} MB\n"
                            + $"Collect poly per frame = {TrianglePolygonsFactory.MaxGeneratedPolygonPerFrame}\n";
            /*_logText.text = $"timer[0] = {CyTrianglePolygonsFactory.Time_BuildFromSkinMeshRenderer[0]}\n"
                                 + $"timer[1] = {CyTrianglePolygonsFactory.Time_BuildFromSkinMeshRenderer[1]}\n";*/
        }
    }
}
