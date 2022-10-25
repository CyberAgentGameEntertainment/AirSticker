using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyCamera : MonoBehaviour
{
    // Start is called before the first frame update
    float TouchZoomSpeed = 0.002f;

    private Camera cam;
    void Start()
    {
        cam= Camera.main;
    }

    

    void Update()
    {
        CameraZoom();
    }

    void CameraZoom()
    {
        if (Input.touchCount == 2)
        {
            // get current touch positions
            Touch tZero = Input.GetTouch(0);
            Touch tOne = Input.GetTouch(1);
            // get touch position from the previous frame
            Vector2 tZeroPrevious = tZero.position - tZero.deltaPosition;
            Vector2 tOnePrevious = tOne.position - tOne.deltaPosition;

            float oldTouchDistance = Vector2.Distance(tZeroPrevious, tOnePrevious);
            float currentTouchDistance = Vector2.Distance(tZero.position, tOne.position);

            // get offset value
            float deltaDistance = oldTouchDistance - currentTouchDistance;
            Zoom(deltaDistance, TouchZoomSpeed);
        }else if (Input.touchCount == 1)
        {
            Touch tZero = Input.GetTouch(0);
            cam.transform.Translate(Vector3.right * tZero.deltaPosition.x * -0.002f);
            
        }else if (Input.touchCount == 3)
        {
            Touch tZero = Input.GetTouch(0);
            cam.transform.Translate(Vector3.up * tZero.deltaPosition.y * -0.002f);
        }
    }


    void Zoom(float deltaMagnitudeDiff, float speed)
    {
        cam.transform.Translate(Vector3.forward * deltaMagnitudeDiff * speed);
    }
}
