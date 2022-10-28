using UnityEngine;

public class MyCamera : MonoBehaviour
{
    // Start is called before the first frame update
    private readonly float TouchZoomSpeed = 0.002f;
    private Camera _cam;

    private void Start()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        CameraZoom();
    }

    private void CameraZoom()
    {
        if (Input.touchCount == 2)
        {
            // get current touch positions
            var tZero = Input.GetTouch(0);
            var tOne = Input.GetTouch(1);
            // get touch position from the previous frame
            var tZeroPrevious = tZero.position - tZero.deltaPosition;
            var tOnePrevious = tOne.position - tOne.deltaPosition;

            var oldTouchDistance = Vector2.Distance(tZeroPrevious, tOnePrevious);
            var currentTouchDistance = Vector2.Distance(tZero.position, tOne.position);

            // get offset value
            var deltaDistance = oldTouchDistance - currentTouchDistance;
            Zoom(deltaDistance, TouchZoomSpeed);
        }
        else if (Input.touchCount == 1)
        {
            var tZero = Input.GetTouch(0);
            _cam.transform.Translate(Vector3.right * (tZero.deltaPosition.x * -0.002f));
        }
        else if (Input.touchCount == 3)
        {
            var tZero = Input.GetTouch(0);
            _cam.transform.Translate(Vector3.up * (tZero.deltaPosition.y * -0.002f));
        }
    }

    private void Zoom(float deltaMagnitudeDiff, float speed)
    {
        _cam.transform.Translate(Vector3.forward * (deltaMagnitudeDiff * speed));
    }
}
