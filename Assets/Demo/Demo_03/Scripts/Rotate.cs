// --------------------------------------------------------------
// Copyright 2022 CyberAgent, Inc.
// --------------------------------------------------------------

using UnityEngine;

namespace Demo.Demo_03.Scripts
{
    public class Rotate : MonoBehaviour
    {
        // Update is called once per frame
        private void Update()
        {
            var rot = Quaternion.AngleAxis(0.25f, Vector3.up);
            var transform1 = transform;
            transform1.localRotation = rot * transform1.localRotation;
        }
    }
}
