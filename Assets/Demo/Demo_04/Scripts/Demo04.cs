using AirSticker.Runtime.Scripts;
using UnityEngine;

namespace Demo.Demo_04.Scripts
{
    /// <summary>
    ///     Demo of removing decals by the group ID.
    ///     Click on the receiver objects to paste a sticker as the current group,
    ///     and use the GUI buttons to remove the decals of each group.
    /// </summary>
    public class Demo04 : MonoBehaviour
    {
        private const int GroupCount = 3;
        private const float GuiScale = 2.0f;
        [SerializeField] private GameObject receiverObject;
        [SerializeField] private Material[] decalMaterials; // The decal material of each group.
        [SerializeField] private Vector3[] projectorSizes; // The projector size of each group.

        private readonly Rect _panelRect = new Rect(10, 10, 250, 260);
        private int _currentGroupId;

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

            var size = projectorSizes[_currentGroupId];
            var projectorObject = new GameObject("Decal Projector");
            // Install the projector at the position pushed back in the normal direction.
            projectorObject.transform.position = hitInfo.point + hitInfo.normal * (size.z * 0.5f);
            // Projector is oriented in the opposite direction of the normal.
            projectorObject.transform.rotation = Quaternion.LookRotation(hitInfo.normal * -1.0f);

            AirStickerProjector.CreateAndLaunch(
                projectorObject,
                receiverObject,
                decalMaterials[_currentGroupId],
                size.x,
                size.y,
                size.z,
                true,
                result => Destroy(projectorObject),
                0.005f,
                _currentGroupId);
        }

        private void OnGUI()
        {
            // Scale up the whole GUI because the default IMGUI is too small to read.
            GUI.matrix = Matrix4x4.Scale(new Vector3(GuiScale, GuiScale, 1.0f));
            GUILayout.BeginArea(_panelRect, GUI.skin.box);
            GUILayout.Label($"Current Group : {_currentGroupId}");
            GUILayout.Label("Click on the objects to paste a sticker.");
            for (var groupId = 0; groupId < GroupCount; groupId++)
                if (GUILayout.Button($"Select Group {groupId}"))
                    _currentGroupId = groupId;

            GUILayout.Space(10);
            for (var groupId = 0; groupId < GroupCount; groupId++)
                if (GUILayout.Button($"Remove Group {groupId}"))
                    AirStickerProjector.RemoveDecalMeshes(groupId);
            GUILayout.EndArea();
        }
    }
}
