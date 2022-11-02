// --------------------------------------------------------------
// Copyright 2022 CyberAgent, Inc.
// --------------------------------------------------------------

using UnityEngine;

public class Rotate : MonoBehaviour
{
    // Start is called before the first frame update
    private void Start()
    {
    }

    // Update is called once per frame
    private void Update()
    {
        var rot = Quaternion.AngleAxis(0.25f, Vector3.up);
        transform.localRotation = rot * transform.localRotation;
    }
}
