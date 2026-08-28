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
    public const float ProjectileSpeed = 95f;
    public const float ProjectileRadius = 0.08f;
    public const float MaximumLifetime = 30f;
    public const float CenterRadius = 0.30f;
    public const float RingRadius = 1f;
    public const float CenterDamage = 12f;
    public const float RingDamage = 7f;
    public const float TargetCenterHeight = 0.8f;

    public AbilityId Id => AbilityId.Bullseye;
    public float CooldownDuration => CooldownSeconds;

    public void Activate() { }

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

    public static float NormalizedTargetOffset(Vector3 hitPoint, Bounds targetBounds, Vector3 shotDirection)
    {
        Vector3 direction = shotDirection.sqrMagnitude > 0.0001f
            ? shotDirection.normalized : Vector3.forward;
        Vector3 offset = hitPoint - targetBounds.center;
        Vector3 radialOffset = offset - Vector3.Project(offset, direction);
        float targetRadius = Mathf.Max(0.01f,
            Mathf.Max(targetBounds.extents.x, targetBounds.extents.y));
        return radialOffset.magnitude / targetRadius;
    }
}
