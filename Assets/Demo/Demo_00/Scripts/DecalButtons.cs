using UnityEngine;

namespace Demo.Demo_00.Scripts
{
    public class DecalButtons : MonoBehaviour
    {
        [SerializeField] private GameObject decalProjectorLauncher;
        [SerializeField] private GameObject playerButtonsGameObject;
        private DecalProjectorLauncher _decalProjector;

        // Start is called before the first frame update
        private void Start()
        {
            _decalProjector = decalProjectorLauncher.GetComponent<DecalProjectorLauncher>();
        }

        // Update is called once per frame
        private void Update()
        {
        }

        public void OnSelectImage(int imageNo)
        {
            _decalProjector.CurrentDecalMaterialIndex = imageNo;
            _decalProjector.IsLaunchReady = true;
            var playerButtons = playerButtonsGameObject.GetComponent<PlayerButtons>();
            playerButtons.StopAnimation();
            playerButtons.StopRotation();
        }
    }
}
