using UnityEngine;

/// <summary>
/// Rules and local physics representation for Base. PlayerAbilities owns the
/// authoritative charge state and tells every peer to create the same platform.
/// </summary>
public sealed class BaseAbility : MonoBehaviour, IAbility
{
    public const int ChargeCount = 2;
    public const float CooldownSeconds = 4f;
    public const float PlatformLifetime = 1f;
    public const float PlatformRadius = 8.25f;
    public const float PlatformThickness = 0.16f;
    public const float SurfaceGap = 0.025f;

    public AbilityId Id => AbilityId.Base;
    public float CooldownDuration => CooldownSeconds;

    public void Activate()
    {
        // Networked activation is coordinated by PlayerAbilities so charge
        // validation and placement always come from the server.
    }

    public static int FindReadyCharge(float now, float[] cooldownEnds)
    {
        if (cooldownEnds == null)
            return -1;

        int count = Mathf.Min(ChargeCount, cooldownEnds.Length);
        for (int charge = 0; charge < count; charge++)
        {
            if (now >= cooldownEnds[charge])
                return charge;
        }
        return -1;
    }

    public static Vector3 PlatformCenterForPlayer(
        Vector3 submittedPlayerPosition, Vector3 authoritativePlayerPosition, Bounds playerBounds)
    {
        float feetOffset = playerBounds.min.y - authoritativePlayerPosition.y;
        return new Vector3(
            submittedPlayerPosition.x,
            submittedPlayerPosition.y + feetOffset - SurfaceGap - PlatformThickness * 0.5f,
            submittedPlayerPosition.z);
    }

    public static GameObject SpawnPlatform(Vector3 center, float lifetime, bool playCastSound)
    {
        if (lifetime <= 0f)
            return null;

        GameObject platform = new GameObject("Base Ability Platform");
        platform.transform.position = center;

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            platform.layer = groundLayer;

        BoxCollider collider = platform.AddComponent<BoxCollider>();
        collider.size = new Vector3(
            PlatformRadius * 2f,
            PlatformThickness,
            PlatformRadius * 2f);

        Vector3 visualPosition = center + Vector3.up * (PlatformThickness * 0.5f + 0.002f);
        BaseAbilityVisual.Spawn(
            visualPosition,
            PlatformRadius,
            lifetime,
            playCastSound);

        Destroy(platform, lifetime);
        return platform;
    }
}
