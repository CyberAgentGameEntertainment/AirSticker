using CyDecal.Runtime.Scripts;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class DecalProjectorLuncher : MonoBehaviour
{
    [SerializeField] private Material[] decalMaterials;
    [SerializeField] private Material[] urpDecalMaterials;

    [FormerlySerializedAs("receiverObject")] [SerializeField]
    private GameObject[] receiverObjects;

    [SerializeField] private GameObject[] moveImageObjects;
    private GameObject _currentProjectorObject;
    private int _currentReceiverObjectNo;

    private bool _isMouseLButtonPress;
    private Vector3 _projectorSize;

    public int CurrentDecalMaterialIndex { get; set; }

    public bool IsLaunchReady { get; set; }

    // Start is called before the first frame update
    private void Start()
    {
    }

    // Update is called once per frame
    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) _isMouseLButtonPress = true;
        if (Input.GetMouseButtonUp(0)) _isMouseLButtonPress = false;
        if (_isMouseLButtonPress)
        {
            if (IsLaunchReady)
            {
                moveImageObjects[CurrentDecalMaterialIndex].transform.position = Input.mousePosition;
                moveImageObjects[CurrentDecalMaterialIndex].SetActive(true);
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                var hit_info = new RaycastHit();
                var max_distance = 100f;

                var is_hit = Physics.Raycast(ray, out hit_info, max_distance);

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
                        if (CurrentDecalMaterialIndex == 3) _projectorSize.x *= 4.496f;


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
        }
        else
        {
            if (_currentProjectorObject != null)
            {
                var projector = _currentProjectorObject.AddComponent<CyDecalProjector>();
                projector.Initialize(
                    receiverObjects[_currentReceiverObjectNo],
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

    /// <summary>
    ///     レシーバーオブジェクトを次のオブジェクトにする。
    /// </summary>
    public void SetNextReceiverObject()
    {
        if (HasAnimatorInCurrentReceiverObject())
        {
            var animator = receiverObjects[_currentReceiverObjectNo].GetComponent<Animator>();
            animator.Rebind();
        }

        receiverObjects[_currentReceiverObjectNo].SetActive(false);
        _currentReceiverObjectNo = (_currentReceiverObjectNo + 1) % receiverObjects.Length;
        receiverObjects[_currentReceiverObjectNo].SetActive(true);
    }

    /// <summary>
    ///     現在のレシーバーオブジェクトがアニメーターを保持しているか調べる。
    /// </summary>
    /// <returns></returns>
    public bool HasAnimatorInCurrentReceiverObject()
    {
        return receiverObjects[_currentReceiverObjectNo].GetComponent<Animator>() != null;
    }

    /// <summary>
    ///     レシーバーオブジェクトのアニメーションを再生する。
    /// </summary>
    public void PlayAnimationToReceiverObject()
    {
        var animator = receiverObjects[_currentReceiverObjectNo].GetComponent<Animator>();
        if (animator) animator.enabled = true;
    }

    /// <summary>
    ///     レシーバーオブジェクトのアニメーションを再生する。
    /// </summary>
    public void StopAnimationToReceiverObject()
    {
        var animator = receiverObjects[_currentReceiverObjectNo].GetComponent<Animator>();
        if (animator)
        {
            animator.enabled = false;
            animator.Rebind();
        }
    }

    public void PlayRotateToCurrentReceiverObject()
    {
        receiverObjects[_currentReceiverObjectNo].GetComponent<Rotate>().enabled = true;
    }

    public void StopRotateToCurrentReceiverObject()
    {
        receiverObjects[_currentReceiverObjectNo].GetComponent<Rotate>().enabled = false;
        CyRenderDecalFeature.ClearReceiverObjectTrianglePolygonsPool();
    }
}
