using System;
using CyDecal.Runtime.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Demo.Demo_00.Scripts
{
    public class SceneInitializer : MonoBehaviour
    {
        [SerializeField]private GameObject _collectPolyInputFieldTextObject;
        // Start is called before the first frame update
        void Start()
        {
            var text = _collectPolyInputFieldTextObject.GetComponent<Text>();
            CyTrianglePolygonsFactory.MaxGeneratedPolygonPerFrame = Int32.Parse(text.text);
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
