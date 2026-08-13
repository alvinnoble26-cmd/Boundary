using UnityEngine;

/// <summary>
/// Marks the only geometry that is allowed to limit a teleport. Ordinary arena
/// obstacles deliberately do not use this component, even when tagged Wall.
/// </summary>
[DisallowMultipleComponent]
public sealed class TeleportArenaBoundary : MonoBehaviour
{
    public enum SurfaceType
    {
        OuterWall,
        Floor,
        Ceiling
    }

    [SerializeField] private SurfaceType surface = SurfaceType.OuterWall;

    public SurfaceType Surface => surface;
}
