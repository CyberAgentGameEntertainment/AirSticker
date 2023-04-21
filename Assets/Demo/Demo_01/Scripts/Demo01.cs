using AirSticker.Runtime.Scripts;
using UnityEngine;

namespace Demo.Demo_00.Scripts
{
    public class Demo01 : MonoBehaviour
    {
        [SerializeField] private Material[] shotDecalMaterials;
        [SerializeField] private Vector3[] projectorSize;
        [SerializeField] private GameObject receiverObject;
        private void Start()
        {
            var animator = receiverObject.GetComponent<Animator>();
            animator.Play("Walking(loop)");
        }

        // Update is called once per frame
        private void Update()
        {
            if (Input.GetMouseButtonUp(0))
            {
                var screenPos = Input.mousePosition;
                screenPos.z = 1.0f;
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                var hit_info = new RaycastHit();
                var max_distance = 100f;
                var is_hit = Physics.Raycast(ray, out hit_info, max_distance);
                if (is_hit)
                {
                    var projectorObject = new GameObject("Decal Projector");
                    projectorObject.transform.position = hit_info.point + Camera.main.transform.forward * -0.1f;
                    var matNo = Random.Range(0, shotDecalMaterials.Length);
                    var size = projectorSize[matNo] * 0.8f;
                               var projector = AirStickerProjector.CreateAndLaunch(
                        projectorObject,
                        receiverObject,
                        shotDecalMaterials[matNo],
                        size.x,
                        size.y,
                        size.z,
                        true,
                        result => { Destroy(projectorObject); });
                }
            }
        }
    }
}
