using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Adds a red silhouette shell to every third-person player skin. Normal shells
/// use the depth buffer; Void can temporarily make the opponent shell brighter
/// and visible through arena geometry on the caster's client.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerOutlinePresentation : MonoBehaviour
{
    public const float NormalWidth = 0.05f;
    public const float BullseyeWidth = 0.085f;
    public const float VoidWidth = 0.12f;
    public const float VoidPulseMinimumWidth = 0.086f;
    public const float VoidPulseMaximumWidth = 0.186f;
    public const float VoidPulseFrequency = 8f;
    private const float DepthLessEqual = 4f;
    private const float DepthAlways = 8f;

    private readonly List<GameObject> outlineObjects = new List<GameObject>();
    private Material outlineMaterial;
    private bool voidReveal;
    private bool bullseyeTargetReveal;
    private Color voidRevealColor = new Color(15.75f, 0.015f, 0.01f, 1f);

    private void Awake()
    {
        EnsureMaterial();
        Refresh();
    }

    private void Start()
    {
        // Skin synchronization can finish after PlayerMovement.Awake.
        Refresh();
    }

    private void Update()
    {
        if (!voidReveal || outlineMaterial == null)
            return;
        float elapsed = Time.unscaledTime;
        float pulse = 0.76f + Mathf.Sin(elapsed * VoidPulseFrequency) * 0.24f;
        outlineMaterial.SetColor("_OutlineColor", new Color(
            voidRevealColor.r * pulse,
            voidRevealColor.g,
            voidRevealColor.b,
            voidRevealColor.a));
        outlineMaterial.SetFloat("_OutlineWidth", VoidOutlineWidthAt(elapsed));
    }

    public static float VoidOutlineWidthAt(float elapsed)
    {
        float pulse01 = 0.5f + 0.5f * Mathf.Sin(Mathf.Max(0f, elapsed) * VoidPulseFrequency);
        return Mathf.Lerp(VoidPulseMinimumWidth, VoidPulseMaximumWidth, pulse01);
    }

    public void Refresh()
    {
        ClearOutlineObjects();
        if (!EnsureMaterial())
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer source = renderers[index];
            if (!CanOutline(source))
                continue;
            CreateOutlineClone(source);
        }
        ApplyMode();
    }

    public void SetVoidReveal(bool enabled)
    {
        voidReveal = enabled;
        ApplyMode();
    }

    public void SetBullseyeTargetReveal(bool enabled)
    {
        bullseyeTargetReveal = enabled;
        ApplyMode();
    }

    public void SetVoidRevealColor(Color color)
    {
        if (!voidReveal)
            return;
        // Void heavily lowers exposure, so keep the silhouette emissive enough to
        // remain readable while preserving the ability's intentional alpha fade.
        voidRevealColor = new Color(
            Mathf.Max(4.5f, color.r * 2.55f),
            color.g,
            color.b,
            color.a);
        if (outlineMaterial != null)
            outlineMaterial.SetColor("_OutlineColor", voidRevealColor);
    }

    private bool EnsureMaterial()
    {
        if (outlineMaterial != null)
            return true;
        Shader shader = Shader.Find("Boundary/Void Enemy Outline");
        if (shader == null)
            return false;
        outlineMaterial = new Material(shader) { name = "Player Red Outline (Runtime)" };
        return true;
    }

    private bool CanOutline(Renderer renderer)
    {
        if (renderer == null || renderer is ParticleSystemRenderer || renderer is LineRenderer ||
            renderer is TrailRenderer || renderer.GetComponentInParent<Canvas>() != null ||
            renderer.GetComponentInParent<PlayerOutlineCloneMarker>() != null ||
            renderer.GetComponentInParent<FirstPersonArmPresentation>() != null)
            return false;

        Transform visualRoot = transform.Find("Visual");
        Transform eyeRoot = transform.Find("eye");
        bool belongsToSkin = visualRoot != null &&
            (renderer.transform == visualRoot || renderer.transform.IsChildOf(visualRoot));
        belongsToSkin |= eyeRoot != null &&
            (renderer.transform == eyeRoot || renderer.transform.IsChildOf(eyeRoot));
        if (!belongsToSkin)
            return false;

        return renderer is SkinnedMeshRenderer ||
            (renderer is MeshRenderer && renderer.GetComponent<MeshFilter>() != null);
    }

    private void CreateOutlineClone(Renderer source)
    {
        GameObject clone = new GameObject(source.name + " Red Outline");
        Transform sourceTransform = source.transform;
        clone.layer = source.gameObject.layer;
        clone.transform.SetParent(sourceTransform.parent, false);
        clone.transform.localPosition = sourceTransform.localPosition;
        clone.transform.localRotation = sourceTransform.localRotation;
        clone.transform.localScale = sourceTransform.localScale;

        Renderer outlineRenderer = null;
        int subMeshCount = 1;
        if (source is SkinnedMeshRenderer sourceSkin)
        {
            SkinnedMeshRenderer skin = clone.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = sourceSkin.sharedMesh;
            skin.bones = sourceSkin.bones;
            skin.rootBone = sourceSkin.rootBone;
            skin.localBounds = sourceSkin.localBounds;
            skin.updateWhenOffscreen = true;
            outlineRenderer = skin;
            if (sourceSkin.sharedMesh != null)
                subMeshCount = Mathf.Max(1, sourceSkin.sharedMesh.subMeshCount);
        }
        else if (source.GetComponent<MeshFilter>() is MeshFilter sourceFilter)
        {
            clone.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            outlineRenderer = clone.AddComponent<MeshRenderer>();
            if (sourceFilter.sharedMesh != null)
                subMeshCount = Mathf.Max(1, sourceFilter.sharedMesh.subMeshCount);
        }

        if (outlineRenderer == null)
        {
            Destroy(clone);
            return;
        }

        Material[] materials = new Material[subMeshCount];
        for (int index = 0; index < materials.Length; index++)
            materials[index] = outlineMaterial;
        outlineRenderer.sharedMaterials = materials;
        outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
        outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        clone.AddComponent<PlayerOutlineCloneMarker>().Initialize(source, outlineRenderer);
        outlineObjects.Add(clone);
    }

    private void ApplyMode()
    {
        if (outlineMaterial == null)
            return;
        outlineMaterial.SetFloat("_ZTest", voidReveal ? DepthAlways : DepthLessEqual);
        outlineMaterial.SetFloat("_OutlineWidth", voidReveal
            ? VoidWidth
            : bullseyeTargetReveal ? BullseyeWidth : NormalWidth);
        outlineMaterial.SetColor("_OutlineColor", voidReveal
            ? new Color(15.75f, 0.015f, 0.01f, 1f)
            : bullseyeTargetReveal
                ? new Color(2.7f, 0.12f, 0.08f, 0.95f)
                : new Color(2.7f, 0.01f, 0.008f, 0.92f));
    }

    private void ClearOutlineObjects()
    {
        for (int index = 0; index < outlineObjects.Count; index++)
            if (outlineObjects[index] != null)
                Destroy(outlineObjects[index]);
        outlineObjects.Clear();
    }

    private void OnDestroy()
    {
        ClearOutlineObjects();
        if (outlineMaterial != null)
            Destroy(outlineMaterial);
    }
}

public sealed class PlayerOutlineCloneMarker : MonoBehaviour
{
    private Renderer source;
    private Renderer clone;
    private PlayerMovement player;

    public void Initialize(Renderer sourceRenderer, Renderer cloneRenderer)
    {
        source = sourceRenderer;
        clone = cloneRenderer;
        player = sourceRenderer != null
            ? sourceRenderer.GetComponentInParent<PlayerMovement>()
            : null;
        SyncVisibility();
    }

    private void LateUpdate()
    {
        if (source == null)
        {
            Destroy(gameObject);
            return;
        }
        SyncVisibility();
    }

    private void SyncVisibility()
    {
        if (clone != null)
            clone.enabled = source != null && source.enabled && source.gameObject.activeInHierarchy &&
                (player == null || !player.isOwner || player.IsCpuControlled);
    }
}
