using AirSticker.Runtime.Scripts;
using UnityEngine;

namespace Demo.Demo_05.Scripts
{
    /// <summary>
    ///     Demo of pasting a decal across multiple receiver objects.
    ///     Click near the boundary of the two walls to paste a sticker.
    ///     Switch the mode with the GUI buttons to compare the multiple receivers overload
    ///     (the sticker spans the boundary) with the single receiver overload
    ///     (the sticker is cut at the boundary).
    /// </summary>
    public class Demo05 : MonoBehaviour
    {
        private const float GuiScale = 2.0f;
        [SerializeField] private GameObject[] receiverObjects; // The receiver objects that the decal spans.
        [SerializeField] private Material decalMaterial;
        [SerializeField] private Vector3 projectorSize = new Vector3(0.6f, 0.6f, 1.0f);

        private readonly Rect _panelRect = new Rect(10, 10, 280, 150);
        private bool _useMultipleReceivers = true;

        // The panel rect in actual screen coordinates, because OnGUI draws it scaled by GuiScale.
        private Rect ScaledPanelRect => new Rect(
            _panelRect.x * GuiScale,
            _panelRect.y * GuiScale,
            _panelRect.width * GuiScale,
            _panelRect.height * GuiScale);

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            // Ignore clicks on the GUI panel.
            var guiPos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (ScaledPanelRect.Contains(guiPos)) return;

            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hitInfo, 100.0f))
            {
                Debug.Log("Raycast miss");
                return;
            }

            var projectorObject = new GameObject("Decal Projector");
            // Install the projector at the position pushed back in the normal direction.
            projectorObject.transform.position = hitInfo.point + hitInfo.normal * (projectorSize.z * 0.5f);
            // Projector is oriented in the opposite direction of the normal.
            projectorObject.transform.rotation = Quaternion.LookRotation(hitInfo.normal * -1.0f);

            if (_useMultipleReceivers)
                // The multiple receivers overload: the decal is projected onto all the receiver
                // objects in the same decal space, so it spans the boundary of the walls.
                AirStickerProjector.CreateAndLaunch(
                    projectorObject,
                    receiverObjects,
                    decalMaterial,
                    projectorSize.x,
                    projectorSize.y,
                    projectorSize.z,
                    true,
                    result => Destroy(projectorObject));
            else
                // The single receiver overload: only the clicked wall receives the decal,
                // so the decal is cut at the boundary of the walls.
                AirStickerProjector.CreateAndLaunch(
                    projectorObject,
                    hitInfo.collider.gameObject,
                    decalMaterial,
                    projectorSize.x,
                    projectorSize.y,
                    projectorSize.z,
                    true,
                    result => Destroy(projectorObject));
        }

        private void OnGUI()
        {
            // Scale up the whole GUI because the default IMGUI is too small to read.
            GUI.matrix = Matrix4x4.Scale(new Vector3(GuiScale, GuiScale, 1.0f));
            GUILayout.BeginArea(_panelRect, GUI.skin.box);
            GUILayout.Label($"Mode : {(_useMultipleReceivers ? "Multiple Receivers" : "Single Receiver")}");
            GUILayout.Label("Click near the boundary of the walls.");
            if (GUILayout.Button("Multiple Receivers (spans the boundary)"))
                _useMultipleReceivers = true;
            if (GUILayout.Button("Single Receiver (cut at the boundary)"))
                _useMultipleReceivers = false;

            GUILayout.Space(10);
            if (GUILayout.Button("Remove All Decals"))
                AirStickerProjector.RemoveDecalMeshes(0);
            GUILayout.EndArea();
        }
    }
}
