using System.Collections;
using PurrNet;
using UnityEngine;
using UnityEngine.UI;

public sealed class SliceAbility : MonoBehaviour, IAbility
{
    public const float CooldownSeconds = 1f;
    public const float Damage = 7f;
    public const float Radius = 4f;
    public const float ArcDegrees = 120f;
    public const float SwingDuration = 0.14f;
    public const float ScreenSliceDuration = 0.75f;

    public AbilityId Id => AbilityId.Slice;
    public float CooldownDuration => CooldownSeconds;
    public void Activate() { }

    public static bool IsInSlash(Vector3 attacker, Vector3 forward, Vector3 target)
    {
        Vector3 offset = target - attacker;
        offset.y = 0f;
        if (offset.sqrMagnitude > Radius * Radius)
            return false;
        if (offset.sqrMagnitude < 0.0001f)
            return true;
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        return Vector3.Dot(flatForward, offset.normalized) >= Mathf.Cos(ArcDegrees * 0.5f * Mathf.Deg2Rad);
    }

    public static bool ShouldShowScreenOverlay(bool hit, bool presentationOwnedByCaster)
    {
        return hit && presentationOwnedByCaster;
    }
}

public sealed class SlicePresentation : MonoBehaviour
{
    private static readonly Color SlicePurple = new Color(0.88f, 0.035f, 0.62f, 1f);
    private GameObject slashPrefab;
    private GameObject circlePrefab;
    private Material distortionMaterial;
    private Texture2D energyTexture;
    private AudioClip swingClip;
    private AudioClip hitClip;

    public void Configure(GameObject slash, GameObject circle, Material distortion, Texture2D energy,
        AudioClip swing, AudioClip hit)
    {
        slashPrefab = slash;
        circlePrefab = circle;
        distortionMaterial = distortion;
        energyTexture = energy;
        swingClip = swing;
        hitClip = hit;
    }

    public void Play(Vector3 origin, Vector3 direction, bool hit, bool showScreenOverlay = true)
    {
        if (swingClip != null)
            AudioSource.PlayClipAtPoint(swingClip, origin, 0.9f);
        SpawnSlash(origin, direction, hit);
        if (hit && hitClip != null)
            AudioSource.PlayClipAtPoint(hitClip, origin, 1f);
        if (SliceAbility.ShouldShowScreenOverlay(hit, showScreenOverlay))
            SliceScreenOverlay.Show();
    }

    private void SpawnMagicCircle(Vector3 origin)
    {
        if (circlePrefab == null)
            return;
        Vector3 ground = origin;
        RaycastHit[] groundHits = Physics.RaycastAll(origin + Vector3.up * 2f,
            Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        foreach (RaycastHit candidate in groundHits)
        {
            if (candidate.collider == null || candidate.collider.transform.root == transform.root ||
                candidate.distance >= nearest)
                continue;
            nearest = candidate.distance;
            ground = candidate.point;
        }
        GameObject circle = UnityProxy.InstantiateDirectly(circlePrefab,
            ground + Vector3.up * 0.025f, Quaternion.Euler(90f, 0f, 0f));
        circle.name = "Slice Purple Magic Circle";
        circle.transform.localScale = Vector3.one * 2.4f;
        foreach (ParticleSystem particles in circle.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.transform.position = ground + Vector3.up * 0.03f;
            particles.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        TintParticles(circle, SlicePurple, SlicePurple, true);
        CreateGroundRing(circle.transform, ground);
        Object.Destroy(circle, 1.1f);
    }

    private static void CreateGroundRing(Transform parent, Vector3 ground)
    {
        GameObject ringObject = new GameObject("Guaranteed Purple Ground Circle");
        ringObject.transform.SetParent(parent, true);
        ringObject.transform.position = ground + Vector3.up * 0.035f;
        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 72;
        ring.widthMultiplier = 0.075f;
        ring.sharedMaterial = AbilityRuntimeMaterialOwner.Track(ringObject,
            new Material(Shader.Find("Sprites/Default")));
        ring.startColor = ring.endColor = SlicePurple;
        for (int index = 0; index < ring.positionCount; index++)
        {
            float angle = index / (float)ring.positionCount * Mathf.PI * 2f;
            ring.SetPosition(index, ground + Vector3.up * 0.035f +
                new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.55f);
        }
    }

    private void SpawnSlash(Vector3 origin, Vector3 direction, bool hit)
    {
        Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (flat.sqrMagnitude < 0.001f) flat = transform.forward;
        Quaternion rotation = Quaternion.LookRotation(flat, Vector3.up);
        // The imported slash carries an orange color animation that ignores
        // ParticleSystem tinting. Use the same fixed-color flame ribbon as the
        // sword so the slash never changes hue or exposes the orange asset.
        StartCoroutine(AnimateDimensionalTear(origin + Vector3.up, rotation, hit));
    }

    private IEnumerator AnimateDimensionalTear(Vector3 origin, Quaternion rotation, bool hit)
    {
        GameObject root = new GameObject("Slice Dimensional Crescent Tear");
        root.transform.SetPositionAndRotation(origin, rotation);
        LineRenderer magentaBorder = CreateArcLine(root.transform, "Magenta Rift Border",
            0.92f, new Color(1f, 0.02f, 0.72f, 1f), 0, energyTexture);
        LineRenderer violetBorder = CreateArcLine(root.transform, "Blue-violet Rift Glow",
            0.70f, new Color(0.34f, 0.08f, 1f, 0.9f), 1, energyTexture);
        LineRenderer blackCenter = CreateArcLine(root.transform, "Pitch Black Rift Interior",
            0.50f, new Color(0.002f, 0f, 0.012f, 1f), 0);
        LineRenderer whiteCore = CreateArcLine(root.transform, "White-hot Rift Edge",
            0.075f, Color.white, -1, energyTexture);
        LineRenderer chromaBlue = CreateArcLine(root.transform, "Chromatic Blue Separation",
            0.055f, new Color(0.05f, 0.45f, 1f, 0.48f), 3, energyTexture);
        LineRenderer chromaRed = CreateArcLine(root.transform, "Chromatic Red Separation",
            0.045f, new Color(1f, 0.02f, 0.3f, 0.40f), -3, energyTexture);
        LineRenderer distortion = distortionMaterial == null ? null : CreateArcLine(root.transform,
            "Vefects Heat Haze Distortion", 1.15f, new Color(1f, 1f, 1f, 0.22f), 0,
            null, distortionMaterial);
        LineRenderer[] lightning = new LineRenderer[5];
        for (int index = 0; index < lightning.Length; index++)
            lightning[index] = CreateLightningBranch(root.transform, index, energyTexture);
        Light flash = root.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = SlicePurple;
        flash.range = 12f;
        flash.intensity = 26f;
        flash.shadows = LightShadows.None;
        float started = Time.time;
        const float collapseDuration = 0.4f;
        float totalDuration = SliceAbility.SwingDuration + collapseDuration;
        while (root != null && Time.time - started < totalDuration)
        {
            float elapsed = Time.time - started;
            float progress = Mathf.Clamp01(elapsed / SliceAbility.SwingDuration);
            float sweep = Mathf.SmoothStep(0f, 1f, progress);
            float collapse = elapsed <= SliceAbility.SwingDuration ? 0f :
                Mathf.Clamp01((elapsed - SliceAbility.SwingDuration) / collapseDuration);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, collapse);
            SetLineAlpha(magentaBorder, alpha);
            SetLineAlpha(violetBorder, alpha * 0.9f);
            SetLineAlpha(blackCenter, alpha);
            SetLineAlpha(whiteCore, alpha);
            SetLineAlpha(chromaBlue, alpha * 0.48f);
            SetLineAlpha(chromaRed, alpha * 0.40f);
            if (distortion != null) SetLineAlpha(distortion, alpha * 0.20f);
            for (int index = 0; index < lightning.Length; index++)
            {
                SetLineAlpha(lightning[index], alpha * (0.45f + 0.45f *
                    Mathf.Abs(Mathf.Sin(Time.time * 24f + index))));
                AnimateLightningBranch(lightning[index], index, Time.time);
            }
            float openScale = Mathf.Lerp(0.72f, 1.55f, progress);
            root.transform.localScale = new Vector3(openScale,
                openScale * Mathf.Lerp(1f, 0.02f, Mathf.SmoothStep(0f, 1f, collapse)), openScale);
            // Preserve the player's captured facing rotation. The earlier code
            // replaced it, causing every slash to use a world-axis direction.
            root.transform.rotation = rotation *
                Quaternion.Euler(0f, Mathf.Lerp(-52f, 52f, sweep), 0f);
            flash.intensity = 26f * alpha * (1f - collapse);
            yield return null;
        }
        if (hit)
            StartCoroutine(AnimateImpactVortex(origin + rotation * Vector3.forward * 2.8f, rotation));
        if (root != null) Object.Destroy(root);
    }

    private static LineRenderer CreateArcLine(Transform parent, string name, float width, Color color,
        int ribbonIndex = 0, Texture texture = null, Material overrideMaterial = null)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 48;
        line.widthMultiplier = width;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.12f, 0.65f),
            new Keyframe(0.5f, 1f), new Keyframe(0.88f, 0.65f), new Keyframe(1f, 0f));
        line.numCapVertices = 16;
        line.numCornerVertices = 14;
        line.sharedMaterial = AbilityRuntimeMaterialOwner.Track(lineObject, overrideMaterial == null
            ? CreateSmoothLineMaterial(texture)
            : new Material(overrideMaterial));
        line.startColor = line.endColor = color;
        for (int index = 0; index < line.positionCount; index++)
        {
            float t = index / (line.positionCount - 1f);
            float angle = Mathf.Lerp(-60f, 60f, t) * Mathf.Deg2Rad;
            // Callers now provide offsets centered around zero. The previous
            // fixed -7 bias belonged to the old fifteen-ribbon flame and
            // displaced the dimensional tear away from the actual swing.
            float layer = ribbonIndex;
            float flameWave = Mathf.Sin(t * Mathf.PI * (2.5f + Mathf.Abs(ribbonIndex) * 0.13f) +
                ribbonIndex) * 0.10f;
            line.SetPosition(index, new Vector3(
                Mathf.Sin(angle) * SliceAbility.Radius + layer * 0.06f,
                Mathf.Sin(t * Mathf.PI) * (0.9f + ribbonIndex * 0.08f) + flameWave + layer * 0.10f,
                Mathf.Cos(angle) * SliceAbility.Radius + layer * 0.18f));
        }
        return line;
    }

    private static Material CreateSmoothLineMaterial(Texture texture)
    {
        Material material = new Material(Shader.Find("Sprites/Default"));
        if (texture != null) material.mainTexture = texture;
        if (material.HasProperty("_ZTest"))
            material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
        return material;
    }

    private static void SetLineAlpha(LineRenderer line, float alpha)
    {
        Color start = line.startColor;
        start.a = alpha;
        line.startColor = line.endColor = start;
    }

    private static LineRenderer CreateLightningBranch(Transform parent, int branch, Texture texture)
    {
        LineRenderer line = CreateArcLine(parent, "Blue-violet Rift Lightning " + branch,
            0.035f, new Color(0.30f, 0.55f, 1f, 0.8f), branch - 2, texture);
        line.positionCount = 9;
        return line;
    }

    private static void AnimateLightningBranch(LineRenderer line, int branch, float time)
    {
        float anchor = 0.18f + branch * 0.15f;
        for (int point = 0; point < line.positionCount; point++)
        {
            float t = point / (line.positionCount - 1f);
            float arcT = Mathf.Clamp01(anchor + (t - 0.5f) * 0.18f);
            float angle = Mathf.Lerp(-60f, 60f, arcT) * Mathf.Deg2Rad;
            float fork = Mathf.Sin(point * 8.71f + branch * 4.3f + Mathf.Floor(time * 22f)) * 0.11f;
            line.SetPosition(point, new Vector3(Mathf.Sin(angle) * SliceAbility.Radius + fork,
                Mathf.Sin(arcT * Mathf.PI) * 1.15f + (t - 0.5f) * (0.5f + branch * 0.08f),
                Mathf.Cos(angle) * SliceAbility.Radius + fork * 0.5f));
        }
    }

    private IEnumerator AnimateImpactVortex(Vector3 origin, Quaternion rotation)
    {
        GameObject vortex = new GameObject("Slice Impact Pull And Burst");
        vortex.transform.SetPositionAndRotation(origin, rotation);
        const int strandCount = 12;
        LineRenderer[] strands = new LineRenderer[strandCount];
        for (int index = 0; index < strandCount; index++)
            strands[index] = CreateArcLine(vortex.transform, "Impact Energy Strand " + index,
                0.055f, index % 3 == 0 ? Color.white : SlicePurple, index - 6, energyTexture);
        float started = Time.time;
        const float duration = 0.32f;
        while (vortex != null && Time.time - started < duration)
        {
            float progress = (Time.time - started) / duration;
            float pullThenBurst = progress < 0.42f ? Mathf.Lerp(1.4f, 0.18f, progress / 0.42f) :
                Mathf.Lerp(0.18f, 2.2f, (progress - 0.42f) / 0.58f);
            vortex.transform.localScale = Vector3.one * pullThenBurst;
            float alpha = 1f - Mathf.SmoothStep(0.58f, 1f, progress);
            foreach (LineRenderer strand in strands) SetLineAlpha(strand, alpha);
            yield return null;
        }
        if (vortex != null) Object.Destroy(vortex);
    }

    public static void TintParticles(GameObject root, Color purple, Color blue,
        bool replaceMaterial = false)
    {
        foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(purple, blue);
            if (replaceMaterial)
            {
                ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
                Material material = AbilityRuntimeMaterialOwner.Track(root,
                    new Material(Shader.Find("Sprites/Default")));
                material.color = purple;
                renderer.sharedMaterial = material;
            }
            particles.Play(true);
        }
    }
}

public sealed class SliceSwordFlames : MonoBehaviour
{
    private static readonly Color FlameColor = new Color(0.88f, 0.035f, 0.62f, 1f);
    private static readonly Color FlameViolet = new Color(0.35f, 0.08f, 1f, 1f);
    private static Texture2D configuredEnergyTexture;
    private LineRenderer[] flames;
    private LineRenderer[] highlights;
    private LineRenderer[] fractures;
    private LineRenderer[] stars;
    private LineRenderer[] cracks;
    private Vector3 bladeAxis = Vector3.up;
    private Vector3 bladeSide = Vector3.right;
    private Vector3 bladeNormal = Vector3.forward;
    private Vector3 bladeCenterLocal;
    private float bladeLength = 0.8f;
    private Vector3 previousTipWorld;
    private bool hasPreviousTip;
    private int airFractureSequence;
    private float nextAirFractureAt;
    private bool airFractureEmissionEnabled = true;
    private bool emitInitialAirFracture;
    private System.Action<Vector3, Vector3> airFractureEmitter;

    public static void Attach(GameObject sword)
    {
        if (sword == null || sword.GetComponent<SliceSwordFlames>() != null) return;
        sword.AddComponent<SliceSwordFlames>().Build();
    }

    public static void ConfigureVisualAssets(Texture2D energyTexture)
    {
        configuredEnergyTexture = energyTexture;
    }

    public void SetAirFractureEmission(bool enabled)
    {
        airFractureEmissionEnabled = enabled;
        hasPreviousTip = false;
        emitInitialAirFracture = enabled;
    }

    public void ConfigureAirFractureEmitter(System.Action<Vector3, Vector3> emitter)
    {
        airFractureEmitter = emitter;
    }

    public static void PrepareMaterials(GameObject sword)
    {
        if (sword == null) return;
        Color[] colors =
        {
            new Color(0.21999297f, 0.21999297f, 0.21999297f, 1f),
            new Color(0.95999354f, 0.95999354f, 0.95999354f, 1f),
            new Color(0.4666533f, 0.21119559f, 0.20525342f, 1f)
        };
        Shader swordShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (swordShader == null) swordShader = Shader.Find("Universal Render Pipeline/Lit");
        if (swordShader == null) swordShader = Shader.Find("Sprites/Default");
        foreach (Renderer renderer in sword.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            Material[] source = renderer.sharedMaterials;
            Material[] visible = new Material[Mathf.Max(1, source.Length)];
            for (int index = 0; index < visible.Length; index++)
            {
                Material material = new Material(swordShader);
                Color color = colors[index % colors.Length];
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
                if (material.HasProperty("_ZTest"))
                    material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
                visible[index] = AbilityRuntimeMaterialOwner.Track(sword, material);
            }
            renderer.sharedMaterials = visible;
            renderer.enabled = true;
            renderer.forceRenderingOff = false;
            renderer.sortingOrder = 100;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private void Build()
    {
        // KatsunesiSword ships with legacy Standard materials that are not
        // reliable in the Metal/URP game view. Rebuild its three material
        // slots as opaque mobile-safe surfaces with strong visual separation.
        PrepareMaterials(gameObject);
        // Disable the prefab's multicolor electricity; the requested effect is
        // one stable flame color shared by the blade and slash.
        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
            particles.gameObject.SetActive(false);

        bladeAxis = BullseyeAbility.GetVisualLongAxisLocal(gameObject).normalized;
        bladeSide = Vector3.Cross(bladeAxis, Mathf.Abs(Vector3.Dot(bladeAxis, Vector3.forward)) > 0.9f
            ? Vector3.up : Vector3.forward).normalized;
        bladeNormal = Vector3.Cross(bladeAxis, bladeSide).normalized;
        bool hasBladeBounds = false;
        Bounds bladeBounds = default;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.gameObject.name.IndexOf("Blade", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (!hasBladeBounds) { bladeBounds = renderer.bounds; hasBladeBounds = true; }
            else bladeBounds.Encapsulate(renderer.bounds);
        }
        if (hasBladeBounds)
        {
            bladeCenterLocal = transform.InverseTransformPoint(bladeBounds.center);
            bladeLength = Mathf.Max(bladeBounds.size.x, Mathf.Max(bladeBounds.size.y, bladeBounds.size.z)) /
                Mathf.Max(0.001f, transform.lossyScale.magnitude / Mathf.Sqrt(3f));
        }
        else if (BullseyeAbility.TryGetVisualBounds(gameObject, out Bounds fullBounds))
        {
            bladeCenterLocal = transform.InverseTransformPoint(fullBounds.center);
            bladeLength = Mathf.Max(fullBounds.size.x, Mathf.Max(fullBounds.size.y, fullBounds.size.z)) /
                Mathf.Max(0.001f, transform.lossyScale.magnitude / Mathf.Sqrt(3f));
        }

        // Three tapered wisps on each edge leave the center of the sword clear
        // so KatsunesiSword's red, silver and dark-gray surfaces remain visible.
        flames = new LineRenderer[6];
        for (int index = 0; index < flames.Length; index++)
        {
            flames[index] = CreateBladeRibbon("Smooth Purple Blade Energy " + index, 0.040f,
                index % 2 == 0 ? FlameColor : FlameViolet, false);
        }
        highlights = new[]
        {
            CreateBladeRibbon("Magenta Fracture Rim A", 0.018f, new Color(1f, 0.03f, 0.72f, 1f), true),
            CreateBladeRibbon("Magenta Fracture Rim B", 0.018f, new Color(1f, 0.03f, 0.72f, 1f), true)
        };
        fractures = new[]
        {
            CreateBladeRibbon("Black Blade-edge Fracture A", 0.010f, new Color(0f, 0f, 0.008f, 1f), true),
            CreateBladeRibbon("Black Blade-edge Fracture B", 0.010f, new Color(0f, 0f, 0.008f, 1f), true)
        };
        foreach (LineRenderer fracture in fractures)
            fracture.startColor = fracture.endColor = new Color(0f, 0f, 0.008f, 1f);
        stars = new LineRenderer[9];
        for (int index = 0; index < stars.Length; index++)
        {
            stars[index] = CreateBladeRibbon("Tiny Rift Star " + index,
                index % 3 == 0 ? 0.014f : 0.008f,
                index % 2 == 0 ? Color.white : new Color(0.25f, 0.62f, 1f, 1f), true);
            stars[index].positionCount = 2;
        }
        cracks = new LineRenderer[6];
        for (int index = 0; index < cracks.Length; index++)
        {
            cracks[index] = CreateBladeRibbon("Sealing Dimensional Crack " + index, 0.007f,
                index % 2 == 0 ? new Color(1f, 0.10f, 0.75f, 1f) :
                    new Color(0.18f, 0.48f, 1f, 1f), true);
            cracks[index].positionCount = 5;
        }
    }

    private LineRenderer CreateBladeRibbon(string name, float width, Color color, bool highlight)
    {
        GameObject ribbon = new GameObject(name);
        ribbon.transform.SetParent(transform, false);
        LineRenderer line = ribbon.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 32;
        line.widthMultiplier = width;
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.08f),
            new Keyframe(0.18f, highlight ? 0.55f : 0.35f),
            new Keyframe(0.72f, highlight ? 0.8f : 1f),
            new Keyframe(1f, 0f));
        line.numCapVertices = 12;
        line.numCornerVertices = 10;
        Material material = AbilityRuntimeMaterialOwner.Track(ribbon,
            new Material(Shader.Find("Sprites/Default")));
        if (configuredEnergyTexture != null) material.mainTexture = configuredEnergyTexture;
        if (material.HasProperty("_ZTest"))
            material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
        line.sharedMaterial = material;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(FlameViolet, 0f),
                new GradientColorKey(color, 0.58f),
                new GradientColorKey(highlight ? Color.white : new Color(1f, 0.18f, 0.78f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(highlight ? 0.72f : 0.28f, 0.14f),
                new GradientAlphaKey(highlight ? 0.92f : 0.68f, 0.62f),
                new GradientAlphaKey(0f, 1f)
            });
        line.colorGradient = gradient;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.sortingOrder = 120;
        return line;
    }

    private void Update()
    {
        if (flames == null) return;
        for (int ribbon = 0; ribbon < flames.Length; ribbon++)
        {
            float edge = ribbon < flames.Length / 2 ? -1f : 1f;
            float strand = ribbon % (flames.Length / 2);
            for (int point = 0; point < flames[ribbon].positionCount; point++)
            {
                float t = point / (flames[ribbon].positionCount - 1f);
                float along = Mathf.Lerp(-bladeLength * 0.48f, bladeLength * 0.48f, t);
                float tipGrowth = Mathf.SmoothStep(0.15f, 1f, t);
                float wave = Mathf.Sin(Time.time * (10f + strand) - t * 17f + ribbon * 1.9f) *
                    Mathf.Lerp(0.008f, 0.070f, tipGrowth);
                float curl = Mathf.Cos(Time.time * 13f - t * 21f + ribbon) *
                    Mathf.Lerp(0.004f, 0.035f, tipGrowth);
                Vector3 position = bladeCenterLocal + bladeAxis * along +
                    bladeSide * (edge * (0.042f + strand * 0.012f + Mathf.Abs(wave))) +
                    bladeNormal * (curl + wave * 0.35f);
                flames[ribbon].SetPosition(point, position);
            }
        }
        for (int edge = 0; edge < highlights.Length; edge++)
        {
            for (int point = 0; point < highlights[edge].positionCount; point++)
            {
                float t = point / (highlights[edge].positionCount - 1f);
                float along = Mathf.Lerp(-bladeLength * 0.48f, bladeLength * 0.48f, t);
                highlights[edge].SetPosition(point, bladeCenterLocal + bladeAxis * along +
                    bladeSide * (edge == 0 ? -0.038f : 0.038f) +
                    bladeNormal * Mathf.Sin(Time.time * 15f - t * 19f + edge) * 0.008f);
            }
        }
        for (int edge = 0; edge < fractures.Length; edge++)
        {
            for (int point = 0; point < fractures[edge].positionCount; point++)
            {
                float t = point / (fractures[edge].positionCount - 1f);
                float along = Mathf.Lerp(-bladeLength * 0.47f, bladeLength * 0.47f, t);
                float fractureJitter = Mathf.Sin(t * 41f + Mathf.Floor(Time.time * 18f) + edge) * 0.004f;
                fractures[edge].SetPosition(point, bladeCenterLocal + bladeAxis * along +
                    bladeSide * ((edge == 0 ? -0.037f : 0.037f) + fractureJitter));
            }
        }
        for (int index = 0; index < stars.Length; index++)
        {
            float travel = Mathf.Repeat(Time.time * (0.34f + index * 0.018f) + index * 0.113f, 1f);
            float along = Mathf.Lerp(-bladeLength * 0.43f, bladeLength * 0.45f, travel);
            float edge = index % 2 == 0 ? -1f : 1f;
            Vector3 star = bladeCenterLocal + bladeAxis * along + bladeSide * edge * 0.039f +
                bladeNormal * Mathf.Sin(index * 5.7f + Time.time * 3f) * 0.009f;
            stars[index].SetPosition(0, star - bladeAxis * 0.003f);
            stars[index].SetPosition(1, star + bladeAxis * 0.003f);
            float pulse = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 7f + index));
            Color starColor = index % 2 == 0 ? Color.white : new Color(0.25f, 0.62f, 1f, 1f);
            starColor.a = pulse;
            stars[index].startColor = stars[index].endColor = starColor;
        }
        for (int index = 0; index < cracks.Length; index++)
        {
            float cycle = Mathf.Repeat(Time.time * 1.7f + index * 0.19f, 1f);
            float open = Mathf.Sin(cycle * Mathf.PI);
            float anchor = Mathf.Lerp(-0.30f, 0.40f, index / (cracks.Length - 1f));
            float edge = index % 2 == 0 ? -1f : 1f;
            for (int point = 0; point < cracks[index].positionCount; point++)
            {
                float branch = point / (cracks[index].positionCount - 1f);
                cracks[index].SetPosition(point, bladeCenterLocal + bladeAxis *
                    ((anchor + (branch - 0.5f) * 0.13f) * bladeLength) + bladeSide * edge *
                    (0.040f + branch * 0.075f * open) + bladeNormal *
                    Mathf.Sin(point * 5.4f + index) * 0.012f * open);
            }
            Color crackColor = index % 2 == 0 ? new Color(1f, 0.10f, 0.75f, open) :
                new Color(0.18f, 0.48f, 1f, open);
            cracks[index].startColor = cracks[index].endColor = crackColor;
        }
        EmitAirFracturesFromTip();
    }

    private void OnEnable()
    {
        hasPreviousTip = false;
        nextAirFractureAt = Time.time;
    }

    private void OnDisable()
    {
        hasPreviousTip = false;
    }

    private void EmitAirFracturesFromTip()
    {
        if (!airFractureEmissionEnabled)
            return;
        Vector3 tipWorld = transform.TransformPoint(bladeCenterLocal + bladeAxis * bladeLength * 0.49f);
        if (!hasPreviousTip)
        {
            previousTipWorld = tipWorld;
            hasPreviousTip = true;
            if (emitInitialAirFracture)
            {
                Vector3 seedHalfWidth = transform.TransformDirection(bladeSide).normalized * 0.12f;
                EmitAirFracture(tipWorld - seedHalfWidth, tipWorld + seedHalfWidth);
                nextAirFractureAt = Time.time + 0.05f;
                emitInitialAirFracture = false;
            }
            return;
        }

        float travel = Vector3.Distance(previousTipWorld, tipWorld);
        if (travel > 1.5f)
        {
            // Camera teleports and scene transitions must not draw a fracture
            // across the whole arena.
            previousTipWorld = tipWorld;
            return;
        }
        if (travel < 0.015f || Time.time < nextAirFractureAt)
            return;
        // Sample at 20 Hz for a smooth connected cut. All emitted scars share
        // their materials, allowing Unity to batch them instead of creating
        // unique material instances every frame.
        nextAirFractureAt = Time.time + 0.05f;
        EmitAirFracture(previousTipWorld, tipWorld);
        previousTipWorld = tipWorld;
    }

    private void EmitAirFracture(Vector3 start, Vector3 end)
    {
        if (airFractureEmitter != null)
            airFractureEmitter(start, end);
        else
            SliceAirFracture.Create(start, end, configuredEnergyTexture, airFractureSequence++);
    }
}

public sealed class SliceAirFracture : MonoBehaviour
{
    public const float FullyVisibleSeconds = 3f;
    public const float FadeSeconds = 0.35f;
    public const float LifetimeSeconds = FullyVisibleSeconds + FadeSeconds;
    private static Shader configuredFractureShader;
    private static Material sharedGlowMaterial;
    private static Material sharedPlainMaterial;
    private static Material sharedDimensionMaterial;
    private static readonly Color Magenta = new Color(1f, 0.025f, 0.72f, 1f);
    private static readonly Color Violet = new Color(0.24f, 0.16f, 1f, 1f);
    private LineRenderer border;
    private LineRenderer center;
    private LineRenderer highlight;
    private LineRenderer[] branches;
    private float spawnedAt;

    public static void ConfigureShader(Shader shader)
    {
        configuredFractureShader = shader;
    }

    public static float RemainingLifetime(float elapsed)
    {
        return Mathf.Max(0f, LifetimeSeconds - Mathf.Max(0f, elapsed));
    }

    public static void Create(Vector3 start, Vector3 end, Texture texture, int seed,
        float elapsed = 0f)
    {
        if ((end - start).sqrMagnitude < 0.0001f) return;
        GameObject root = new GameObject("Suspended Three-Second Inverted Dimension Fracture");
        // These coordinates are captured in arena/world space and the root is
        // deliberately never parented to the camera, player, hand, or sword.
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        SliceAirFracture fracture = root.AddComponent<SliceAirFracture>();
        fracture.Build(start, end, texture, seed, elapsed);
    }

    private void Build(Vector3 start, Vector3 end, Texture texture, int seed, float elapsed)
    {
        spawnedAt = Time.time - (LifetimeSeconds - RemainingLifetime(elapsed));
        border = CreateWorldLine("Purple-magenta Fracture Rim", 0.24f, Magenta, texture);
        center = CreateWorldLine("Inverted Dimensional Interior", 0.17f,
            Color.white, null, true);
        highlight = CreateWorldLine("White-hot Fracture Highlight", 0.026f, Color.white, texture);
        Vector3 direction = (end - start).normalized;
        Vector3 side = Vector3.Cross(direction, Vector3.up).normalized;
        if (side.sqrMagnitude < 0.001f) side = Vector3.Cross(direction, Vector3.forward).normalized;
        Vector3 fractureNormal = Vector3.Cross(direction, side).normalized;
        const int points = 7;
        Vector3[] path = new Vector3[points];
        for (int point = 0; point < points; point++)
        {
            float t = point / (points - 1f);
            float jagged = point == 0 || point == points - 1 ? 0f :
                Mathf.Sin(seed * 3.17f + point * 8.73f) * 0.013f;
            path[point] = Vector3.Lerp(start, end, t) + side * jagged;
        }
        border.positionCount = center.positionCount = highlight.positionCount = points;
        border.SetPositions(path);
        center.SetPositions(path);
        for (int point = 0; point < points; point++)
            path[point] += side * 0.018f;
        highlight.SetPositions(path);

        branches = new LineRenderer[3];
        for (int branchIndex = 0; branchIndex < branches.Length; branchIndex++)
        {
            Color color = branchIndex == 0 ? Violet : Magenta;
            branches[branchIndex] = CreateWorldLine("Branching Air Crack " + branchIndex,
                0.036f, color, texture);
            branches[branchIndex].positionCount = 4;
            float anchorT = 0.20f + branchIndex * 0.29f;
            Vector3 anchor = Vector3.Lerp(start, end, anchorT);
            float branchSign = branchIndex % 2 == 0 ? -1f : 1f;
            for (int point = 0; point < 4; point++)
            {
                float t = point / 3f;
                Vector3 fork = side * branchSign * t * (0.13f + branchIndex * 0.025f) +
                    direction * t * 0.045f;
                fork += fractureNormal * Mathf.Sin(seed + point * 4.9f) * 0.015f;
                branches[branchIndex].SetPosition(point, anchor + fork);
            }
        }
    }

    private LineRenderer CreateWorldLine(string objectName, float width, Color color, Texture texture,
        bool useDimensionShader = false)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = width;
        // Adjacent sampled segments overlap instead of tapering to zero. This
        // keeps the rolling three-second history visually continuous.
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.62f), new Keyframe(0.12f, 1f),
            new Keyframe(0.88f, 1f), new Keyframe(1f, 0.62f));
        line.numCapVertices = 12;
        line.numCornerVertices = 10;
        line.sharedMaterial = GetSharedMaterial(texture, useDimensionShader);
        line.startColor = line.endColor = color;
        return line;
    }

    private void Update()
    {
        float age = Time.time - spawnedAt;
        float fade = 1f - Mathf.SmoothStep(FullyVisibleSeconds, LifetimeSeconds, age);
        float pulse = age < FullyVisibleSeconds
            ? 1f
            : 0.86f + Mathf.Abs(Mathf.Sin(Time.time * 11f)) * 0.14f;
        SetAlpha(border, fade * pulse);
        SetAlpha(center, fade);
        SetAlpha(highlight, fade * pulse);
        for (int index = 0; index < branches.Length; index++)
        {
            float branchPulse = age < FullyVisibleSeconds ? 1f :
                0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 17f + index * 1.8f));
            SetAlpha(branches[index], fade * branchPulse);
        }
        if (age >= LifetimeSeconds)
            Destroy(gameObject);
    }

    private static void SetAlpha(LineRenderer line, float alpha)
    {
        Color color = line.startColor;
        color.a = alpha;
        line.startColor = line.endColor = color;
    }

    private static Material GetSharedMaterial(Texture texture, bool useDimensionShader)
    {
        if (useDimensionShader && configuredFractureShader != null)
        {
            if (sharedDimensionMaterial == null || sharedDimensionMaterial.shader != configuredFractureShader)
            {
                sharedDimensionMaterial = new Material(configuredFractureShader)
                {
                    name = "Shared Slice Dimension Inversion",
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 40
                };
            }
            return sharedDimensionMaterial;
        }

        if (texture == null)
        {
            if (sharedPlainMaterial == null)
            {
                sharedPlainMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "Shared Slice Plain Fracture",
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50
                };
            }
            return sharedPlainMaterial;
        }

        if (sharedGlowMaterial == null || sharedGlowMaterial.mainTexture != texture)
        {
            sharedGlowMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                name = "Shared Slice Smooth Glow",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = texture,
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50
            };
        }
        return sharedGlowMaterial;
    }
}

public sealed class SliceScreenOverlay : MonoBehaviour
{
    public static void Show()
    {
        Canvas canvas = new GameObject("Slice Screen Split Canvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        canvas.gameObject.AddComponent<CanvasScaler>();
        SliceScreenOverlay overlay = canvas.gameObject.AddComponent<SliceScreenOverlay>();
        overlay.StartCoroutine(overlay.Animate(canvas.transform));
    }

    private IEnumerator Animate(Transform parent)
    {
        Image top = CreateHalf(parent, "Black Screen Upper Slice", true);
        Image bottom = CreateHalf(parent, "Black Screen Lower Slice", false);
        float started = Time.unscaledTime;
        while (Time.unscaledTime - started < SliceAbility.ScreenSliceDuration)
        {
            float progress = (Time.unscaledTime - started) / SliceAbility.ScreenSliceDuration;
            float split = Mathf.SmoothStep(0f, 1f, progress);
            top.rectTransform.anchoredPosition = new Vector2(-split * Screen.width, split * 80f);
            bottom.rectTransform.anchoredPosition = new Vector2(split * Screen.width, -split * 80f);
            yield return null;
        }
        Destroy(gameObject);
    }

    private static Image CreateHalf(Transform parent, string name, bool upper)
    {
        GameObject half = new GameObject(name, typeof(RectTransform), typeof(Image));
        half.transform.SetParent(parent, false);
        Image image = half.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, upper ? 0.5f : 0f);
        rect.anchorMax = new Vector2(1f, upper ? 1f : 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localRotation = Quaternion.Euler(0f, 0f, -7f);
        return image;
    }
}
