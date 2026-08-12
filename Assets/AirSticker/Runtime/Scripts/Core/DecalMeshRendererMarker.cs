using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core
{
    /// <summary>
    ///     Marks the GameObject spawned by <see cref="DecalMeshRenderer" /> as a decal renderer.
    /// </summary>
    /// <remarks>
    ///     The projector gathers the receiver's renderers with GetComponentsInChildren, which also picks up the
    ///     decal renderers hanging under the receiver. They are identified by this component instead of by the
    ///     GameObject's name, because Renderer.name allocates a string on every access and the check runs once
    ///     per renderer on every launch. The GameObject is still named "AirStickerRenderer" so that it stays
    ///     recognizable in the hierarchy and to user code.
    /// </remarks>
    [DisallowMultipleComponent]
    internal sealed class DecalMeshRendererMarker : MonoBehaviour
    {
    }
}
