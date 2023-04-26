using AirSticker.Runtime.Scripts;
using UnityEngine;

namespace Demo.Demo_00.Scripts
{
    public class Demo03 : MonoBehaviour
    {
        [SerializeField] private GameObject effectObject;
        
        // Update is called once per frame
        private void Update()
        {
            if (Input.GetMouseButtonUp(0))
            {
                var screenPos = Input.mousePosition;
                screenPos.z = 1.0f;
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                var newEffectObject = GameObject.Instantiate(effectObject);
                var hit_info = new RaycastHit();
                var max_distance = 100f;
                var is_hit = Physics.Raycast(ray, out hit_info, max_distance);
                if (is_hit)
                {
                    newEffectObject.transform.position = hit_info.point;
                    newEffectObject.SetActive(true);
                    var _particleSystem = newEffectObject.GetComponent<ParticleSystem>();
                    _particleSystem.Play();
                }
            }
        }
    }
}
