using Demo.Demo_02.Scripts;
using UnityEngine;

namespace Demo.Demo_03.Scripts
{
    public class DecalButtons : MonoBehaviour
    {
        [SerializeField] private GameObject decalProjectorLauncher;
        [SerializeField] private GameObject playerButtonsGameObject;
        private Demo03 _decalProjector;

        // Start is called before the first frame update
        private void Start()
        {
            _decalProjector = decalProjectorLauncher.GetComponent<Demo03>();
        }

        public void OnSelectImage(int imageNo)
        {
            _decalProjector.CurrentDecalMaterialIndex = imageNo;
            _decalProjector.IsLaunchReady = true;
        }
    }
}
