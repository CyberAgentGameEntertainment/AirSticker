using CyDecal.Runtime.Scripts;
using UnityEngine;

namespace Demo.Demo_00.Scripts
{
    public class Demo01 : MonoBehaviour
    {
        [SerializeField] private Material[] shotDecalMaterials;
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

                    var projector = CyDecalProjector.CreateAndLaunch(
                        projectorObject,
                        receiverObject,
                        shotDecalMaterials[Random.Range(0, shotDecalMaterials.Length)],
                        0.08f,
                        0.08f,
                        2.0f,
                        true,
                        result => { Destroy(projectorObject); });
                }
            }
        }
    }
}
