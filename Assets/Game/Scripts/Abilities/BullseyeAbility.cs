using System.Collections.Generic;
using UnityEngine;

internal sealed class AbilityRuntimeMaterialOwner : MonoBehaviour
{
    private readonly List<Material> materials = new List<Material>();

    public static Material Track(GameObject owner, Material material)
    {
        if (owner == null || material == null)
            return material;

        AbilityRuntimeMaterialOwner tracker = owner.GetComponent<AbilityRuntimeMaterialOwner>();
        if (tracker == null)
            tracker = owner.AddComponent<AbilityRuntimeMaterialOwner>();
        tracker.materials.Add(material);
        return material;
    }

    private void OnDestroy()
    {
        for (int index = 0; index < materials.Count; index++)
        {
            if (materials[index] != null)
                Destroy(materials[index]);
        }
        materials.Clear();
    }
}

public sealed class BullseyeAbility : MonoBehaviour, IAbility
{
    public const float CooldownSeconds = 2f;
    public const float ProjectileSpeed = 570f;
    public const float ProjectileRadius = 0.08f;
    public const float MaximumLifetime = 30f;
    // Normalized against TargetRadius below (1.0 = exactly the edge of the ring drawn on
    // screen by BullseyeTargetPresentation). Previously this was normalized against
    // whichever collider the knife's raycast happened to connect with (a body capsule, a
    // head sphere, or a shorter capsule - three different sizes/centers on the same
    // player), which almost never matched where the ring was actually drawn, making the
    // ability read as much harder to hit than the reticle suggested.
    public const float CenterRadius = 0.30f;
    public const float RingRadius = 1f;
    public const float CenterDamage = 12f;
    public const float RingDamage = 7f;
    public const float TargetCenterHeight = 0.8f;
    // The real-world radius (in metres) that CenterRadius/RingRadius above are normalized
    // against - kept identical to the ring's own drawn radius so the reticle is WYSIWYG.
    public const float TargetRadius = BullseyeTargetPresentation.OuterRingRadius;

    public AbilityId Id => AbilityId.Bullseye;
    public float CooldownDuration => CooldownSeconds;

    public void Activate() { }

    public static void ResolveCrosshairShot(Vector3 cameraPosition, Vector3 cameraForward,
        Vector3 fallbackOrigin, Vector3 fallbackDirection, out Vector3 origin, out Vector3 direction)
    {
        origin = cameraPosition;
        direction = cameraForward.sqrMagnitude > 0.0001f
            ? cameraForward.normalized
            : fallbackDirection.sqrMagnitude > 0.0001f
                ? fallbackDirection.normalized
                : Vector3.forward;

        if (!IsFinite(origin))
            origin = fallbackOrigin;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    public static Quaternion KnifeRotationForDirection(Vector3 direction)
    {
        Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.forward;
        return Quaternion.FromToRotation(Vector3.up, normalizedDirection);
    }

    public static void PrepareKnifeVisual(GameObject knife)
    {
        if (knife == null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        foreach (Renderer renderer in knife.GetComponentsInChildren<Renderer>(true))
        {
            Material[] sourceMaterials = renderer.sharedMaterials;
            Material[] compatibleMaterials = new Material[sourceMaterials.Length];
            for (int index = 0; index < sourceMaterials.Length; index++)
            {
                Material source = sourceMaterials[index];
                Material compatible = new Material(shader);
                Texture albedo = source != null ? source.GetTexture("_MainTex") : null;
                Color color = source != null && source.HasProperty("_Color")
                    ? source.GetColor("_Color") : Color.white;
                if (compatible.HasProperty("_BaseMap"))
                    compatible.SetTexture("_BaseMap", albedo);
                if (compatible.HasProperty("_MainTex"))
                    compatible.SetTexture("_MainTex", albedo);
                if (compatible.HasProperty("_BaseColor"))
                    compatible.SetColor("_BaseColor", color);
                if (compatible.HasProperty("_Color"))
                    compatible.SetColor("_Color", color);
                if (compatible.HasProperty("_Metallic"))
                    compatible.SetFloat("_Metallic", 0.72f);
                if (compatible.HasProperty("_Smoothness"))
                    compatible.SetFloat("_Smoothness", 0.48f);
                compatibleMaterials[index] = AbilityRuntimeMaterialOwner.Track(knife, compatible);
            }
            renderer.sharedMaterials = compatibleMaterials;
        }
    }

    public static bool TryGetVisualBounds(GameObject visual, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return found;
    }

    public static Vector3 GetVisualLongAxisLocal(GameObject visual)
    {
        if (visual == null)
            return Vector3.up;

        Vector3 localMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 localMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        bool found = false;
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Bounds rendererBounds = renderer.localBounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 rendererCorner = rendererBounds.center + Vector3.Scale(
                    rendererBounds.extents, new Vector3(x, y, z));
                Vector3 localCorner = visual.transform.InverseTransformPoint(
                    renderer.transform.TransformPoint(rendererCorner));
                localMin = Vector3.Min(localMin, localCorner);
                localMax = Vector3.Max(localMax, localCorner);
                found = true;
            }
        }
        if (!found)
            return Vector3.up;
        Vector3 size = localMax - localMin;
        if (size.x >= size.y && size.x >= size.z) return Vector3.right;
        if (size.z >= size.y) return Vector3.forward;
        return Vector3.up;
    }

    public static float DamageForNormalizedTargetOffset(float normalizedOffset)
    {
        if (normalizedOffset <= CenterRadius)
            return CenterDamage;
        if (normalizedOffset <= RingRadius)
            return RingDamage;
        return 0f;
    }

    public static bool IsCenterHit(float normalizedOffset) => normalizedOffset <= CenterRadius;

    public static Vector3 TargetCenter(Vector3 playerPosition)
    {
        return playerPosition + Vector3.up * PlayerMovement.ScaleDistance(TargetCenterHeight);
    }

    /// <summary>
    /// Distance of the hit point from a fixed target centre (perpendicular to the shot),
    /// normalized against TargetRadius. Deliberately takes a caller-supplied centre/radius
    /// instead of the struck collider's own bounds - the player has several overlapping
    /// colliders (body capsule, head sphere, a second shorter capsule) with different sizes
    /// and centres, and scoring against whichever one the raycast happened to hit made the
    /// result depend on hit collider rather than on how close the shot was to the target
    /// the player actually sees on screen.
    /// </summary>
    public static float NormalizedTargetOffset(Vector3 hitPoint, Vector3 targetCenter, float targetRadius, Vector3 shotDirection)
    {
        Vector3 direction = shotDirection.sqrMagnitude > 0.0001f
            ? shotDirection.normalized : Vector3.forward;
        Vector3 offset = hitPoint - targetCenter;
        Vector3 radialOffset = offset - Vector3.Project(offset, direction);
        return radialOffset.magnitude / Mathf.Max(0.01f, targetRadius);
    }
}
