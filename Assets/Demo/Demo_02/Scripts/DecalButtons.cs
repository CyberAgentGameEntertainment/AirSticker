using UnityEngine;

public class DecalButtons : MonoBehaviour
{
    [SerializeField] private GameObject decalProjectorLauncher;

    private DecalProjectorLuncher _decalProjector;

    // Start is called before the first frame update
    private void Start()
    {
        _decalProjector = decalProjectorLauncher.GetComponent<DecalProjectorLuncher>();
    }

    // Update is called once per frame
    private void Update()
    {
    }

    public void OnSelectImage_0()
    {
        _decalProjector.CurrentDecalMaterialIndex = 0;
        _decalProjector.IsLaunchReady = true;
    }

    public void OnSelectImage_1()
    {
        _decalProjector.CurrentDecalMaterialIndex = 1;
        _decalProjector.IsLaunchReady = true;
    }

    public void OnSelectImage_2()
    {
        _decalProjector.CurrentDecalMaterialIndex = 2;
        _decalProjector.IsLaunchReady = true;
    }

    public void OnSelectImage_3()
    {
        _decalProjector.CurrentDecalMaterialIndex = 3;
        _decalProjector.IsLaunchReady = true;
    }
}