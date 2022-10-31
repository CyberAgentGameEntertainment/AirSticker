using System.Collections;
using System.Collections.Generic;
using CyDecal.Runtime.Scripts;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DecalProjectorLuncher : MonoBehaviour
{
    [SerializeField] private Material[] decalMaterials;
    [SerializeField] private Material[] urpDecalMaterials;
    
    [SerializeField] private GameObject receiverObject;
    [SerializeField] private GameObject[] moveImageObjects;
    public int CurrentDecalMaterialIndex { get; set; }
    private GameObject _currentProjectorObject;
    public bool IsLaunchReady { get; set; }

    private bool _isMouseLButtonPress;

    private Vector3 _projectorSize;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _isMouseLButtonPress = true;
        }
        if(Input.GetMouseButtonUp(0))
        {
            _isMouseLButtonPress = false;
        }
        if (_isMouseLButtonPress)
        {
            if (IsLaunchReady)
            {
                moveImageObjects[CurrentDecalMaterialIndex].transform.position = Input.mousePosition;
                moveImageObjects[CurrentDecalMaterialIndex].SetActive(true);
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit_info = new RaycastHit();
                float max_distance = 100f;

                bool is_hit = Physics.Raycast(ray, out hit_info, max_distance);

                if (is_hit)
                {
                    moveImageObjects[CurrentDecalMaterialIndex].SetActive(false);
                    // デカールプロジェクターを生成
                    if (_currentProjectorObject == null)
                    {
                        _currentProjectorObject = new GameObject("Decal Projector");
                        var urpDecaleProjector = _currentProjectorObject.AddComponent<DecalProjector>();
                        _projectorSize = new Vector3();
                        _projectorSize.x = 0.05f;
                        _projectorSize.y = 0.05f;
                        if (CurrentDecalMaterialIndex == 3)
                        {
                            _projectorSize.x *= 4.496f;
                        }
                        
                        
                        _projectorSize.z = 0.2f;
                        urpDecaleProjector.size = _projectorSize;
                        var pivot = new Vector3();
                        pivot.z = _projectorSize.z * 0.5f;
                        urpDecaleProjector.pivot = pivot;
                        urpDecaleProjector.material = urpDecalMaterials[CurrentDecalMaterialIndex];
                    }
                    
                    _currentProjectorObject.transform.localPosition =
                        hit_info.point + Camera.main.transform.forward * -0.1f;
                }
            }
        }else
        {
            if (_currentProjectorObject != null)
            {
                var projector = _currentProjectorObject.AddComponent<CyDecalProjector>();
                projector.Initialyze(
                    receiverObject, 
                    decalMaterials[CurrentDecalMaterialIndex], 
                    _projectorSize.x, 
                    _projectorSize.y, 
                    _projectorSize.z);
            }
            moveImageObjects[CurrentDecalMaterialIndex].SetActive(false);
            IsLaunchReady = false;
            _currentProjectorObject = null;
        }
    }
}
