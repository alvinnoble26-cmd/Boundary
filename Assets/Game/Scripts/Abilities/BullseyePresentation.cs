using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class BullseyeKnifeEffects
{
    private static readonly Color FlamePink = new Color(1f, 0.015f, 0.48f, 1f);
    private static readonly Color FlameMagenta = new Color(0.88f, 0f, 0.72f, 1f);
    private static readonly Color FlameCore = new Color(1f, 0.82f, 1f, 1f);

    public static void AttachRedFlames(GameObject knife, bool flying)
    {
        if (knife == null || knife.transform.Find("Bullseye Red Flame Tip") != null ||
            !BullseyeAbility.TryGetVisualBounds(knife, out Bounds bounds))
            return;

        Vector3 bladeAxis = knife.transform.TransformDirection(
            BullseyeAbility.GetVisualLongAxisLocal(knife)).normalized;
        float bladeExtent = Mathf.Abs(bladeAxis.x) * bounds.extents.x +
            Mathf.Abs(bladeAxis.y) * bounds.extents.y +
            Mathf.Abs(bladeAxis.z) * bounds.extents.z;
        float bladeLength = bladeExtent * 2f;
        Vector3 tipWorld = bounds.center + bladeAxis * bladeExtent;
        GameObject tip = new GameObject("Bullseye Red Flame Tip");
        tip.transform.SetParent(knife.transform, true);
        tip.transform.position = tipWorld;

        CreateFlameLayer(tip.transform, "Fuchsia Flame Tongues", FlamePink,
            flying ? 76f : 48f, flying ? 0.22f : 0.17f, flying ? 1.15f : 0.72f, 0.44f);
        CreateFlameLayer(tip.transform, "Magenta Flame Body", FlameMagenta,
            flying ? 58f : 38f, flying ? 0.16f : 0.13f, flying ? 0.82f : 0.52f, 0.30f);
        CreateFlameLayer(tip.transform, "White Hot Pink Core", FlameCore,
            flying ? 38f : 25f, flying ? 0.09f : 0.075f, flying ? 0.48f : 0.32f, 0.18f);
        CreateBladeFlameBand(knife.transform, tipWorld - bladeAxis * bladeLength * 0.18f,
            flying, 0.88f, "Upper Blade Pink Fire");
        CreateBladeFlameBand(knife.transform, tipWorld - bladeAxis * bladeLength * 0.36f,
            flying, 0.76f, "Middle Blade Pink Fire");
        CreateBladeFlameBand(knife.transform, tipWorld - bladeAxis * bladeLength * 0.54f,
            flying, 0.62f, "Lower Blade Pink Fire");

        TrailRenderer trail = tip.AddComponent<TrailRenderer>();
        trail.time = flying ? 0.28f : 0.12f;
        trail.minVertexDistance = 0.025f;
        trail.startWidth = flying ? 0.15f : 0.09f;
        trail.endWidth = 0f;
        trail.material = CreateEffectMaterial();
        trail.startColor = FlameCore;
        trail.endColor = new Color(0.95f, 0f, 0.62f, 0f);

        Light glow = tip.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = FlamePink;
        glow.intensity = flying ? 8.5f : 5.5f;
        glow.range = flying ? 3.2f : 2.1f;
        glow.shadows = LightShadows.None;
        tip.AddComponent<BullseyeFlamePulse>().Initialize(glow, flying ? 8.5f : 5.5f);
    }

    private static void CreateBladeFlameBand(Transform knife, Vector3 worldPosition,
        bool flying, float scale, string name)
    {
        GameObject band = new GameObject(name);
        band.transform.SetParent(knife, true);
        band.transform.position = worldPosition;
        CreateFlameLayer(band.transform, name + " Tongues", FlamePink,
            (flying ? 54f : 32f) * scale, (flying ? 0.16f : 0.12f) * scale,
            (flying ? 0.88f : 0.56f) * scale, 0.34f);
        CreateFlameLayer(band.transform, name + " Core", FlameCore,
            (flying ? 26f : 16f) * scale, (flying ? 0.075f : 0.06f) * scale,
            (flying ? 0.42f : 0.28f) * scale, 0.18f);
    }

    public static void SpawnWindBurst(Vector3 position, Vector3 direction)
    {
        GameObject root = new GameObject("Bullseye Launch Wind Burst", typeof(ParticleSystem));
        root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
        ParticleSystem particles = root.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 0.28f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 14f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.085f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.86f, 1f, 0.95f), new Color(1f, 0.02f, 0.58f, 0.9f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 52) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 38f;
        shape.radius = 0.18f;
        shape.position = Vector3.back * 0.08f;
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(FlameCore, 0f), new GradientColorKey(FlamePink, 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateEffectMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.12f;
        renderer.lengthScale = 2.8f;
        particles.Play();
    }

    private static void CreateFlameLayer(Transform parent, string name, Color color,
        float rate, float size, float speed, float tongueLength)
    {
        GameObject layer = new GameObject(name, typeof(ParticleSystem));
        layer.transform.SetParent(parent, false);
        ParticleSystem particles = layer.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, tongueLength);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size);
        main.startColor = color;
        // Flames belong to the blade. Local simulation makes every tongue
        // move with the held/fast-flying knife instead of being left behind.
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 140;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = rate;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = size * 0.34f;
        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(size * 1.8f, size * 3.6f);
        noise.frequency = 1.35f;
        noise.scrollSpeed = 1.8f;
        noise.quality = ParticleSystemNoiseQuality.High;
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve lick = new AnimationCurve(
            new Keyframe(0f, 0.12f), new Keyframe(0.18f, 1f),
            new Keyframe(0.68f, 0.62f), new Keyframe(1f, 0f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, lick);
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(FlameCore, 0f),
                new GradientColorKey(color, 0.28f), new GradientColorKey(FlameMagenta, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = fade;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateEffectMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.18f;
        renderer.lengthScale = 1.7f;
        particles.Play();
    }

    private static Material CreateEffectMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        Texture2D softFlame = CreateSoftFlameTexture();
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", softFlame);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", softFlame);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 1f);
        material.renderQueue = 3100;
        return material;
    }

    private static Texture2D CreateSoftFlameTexture()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Bullseye Soft Flame Particle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.42f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 delta = new Vector2((x - center.x) / (size * 0.42f),
                (y - center.y) / (size * 0.56f));
            float radial = Mathf.Clamp01(1f - delta.magnitude);
            float pointedTop = Mathf.Clamp01(1f - Mathf.Max(0f, delta.y) * 0.45f);
            float alpha = radial * radial * pointedTop;
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply(false, true);
        return texture;
    }
}

public sealed class BullseyeFlamePulse : MonoBehaviour
{
    private Light flameLight;
    private float baseIntensity;
    private float phase;

    public void Initialize(Light lightSource, float intensity)
    {
        flameLight = lightSource;
        baseIntensity = intensity;
        phase = Random.value * 10f;
    }

    private void Update()
    {
        if (flameLight == null)
            return;
        float flicker = Mathf.PerlinNoise(phase, Time.time * 11f);
        float pulse = 0.88f + Mathf.Sin(Time.time * 7f + phase) * 0.12f;
        flameLight.intensity = baseIntensity * pulse * Mathf.Lerp(0.72f, 1.28f, flicker);
    }
}

public sealed class BullseyeTargetPresentation : MonoBehaviour
{
    private LineRenderer outerRing;
    private LineRenderer centerCircle;
    private Camera viewer;

    public void Initialize(Camera camera)
    {
        viewer = camera;
        outerRing = CreateRing("Bullseye Outer Ring", 1.74f, 0.055f, Color.white);
        centerCircle = CreateRing("Bullseye Center Circle", 0.46f, 0.045f,
            new Color(1f, 0.12f, 0.08f, 0.95f));
    }

    private void LateUpdate()
    {
        if (viewer == null)
            return;
        transform.rotation = Quaternion.LookRotation(viewer.transform.forward, viewer.transform.up);
    }

    private LineRenderer CreateRing(string ringName, float radius, float width, Color color)
    {
        GameObject ring = new GameObject(ringName, typeof(LineRenderer));
        ring.transform.SetParent(transform, false);
        LineRenderer line = ring.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 65;
        line.startWidth = line.endWidth = width;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        for (int index = 0; index < line.positionCount; index++)
        {
            float angle = index / 64f * Mathf.PI * 2f;
            line.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }
        return line;
    }
}

public sealed class BullseyeScreenFeedback : MonoBehaviour
{
    public static void Show(Sprite dragon)
    {
        GameObject host = new GameObject("Bullseye Dragon Hit Feedback");
        host.AddComponent<BullseyeScreenFeedback>().Begin(dragon);
    }

    private void Begin(Sprite dragon)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        CanvasGroup group = gameObject.AddComponent<CanvasGroup>();
        Image flash = CreateImage("Hit Blink", null, Color.white);
        Image dragonImage = CreateImage("Dragon", dragon, Color.white);
        dragonImage.preserveAspect = true;
        RectTransform dragonRect = dragonImage.rectTransform;
        dragonRect.anchorMin = new Vector2(0.18f, 0.12f);
        dragonRect.anchorMax = new Vector2(0.82f, 0.88f);
        dragonRect.offsetMin = dragonRect.offsetMax = Vector2.zero;
        StartCoroutine(Fade(group, flash));
    }

    private Image CreateImage(string imageName, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(imageName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(transform, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return image;
    }

    private IEnumerator Fade(CanvasGroup group, Image flash)
    {
        float startedAt = Time.unscaledTime;
        const float duration = 1f;
        while (Time.unscaledTime - startedAt < duration)
        {
            float progress = (Time.unscaledTime - startedAt) / duration;
            flash.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.65f, 0f, Mathf.Min(1f, progress * 4f)));
            group.alpha = 1f - Mathf.SmoothStep(0f, 1f, progress);
            yield return null;
        }
        Destroy(gameObject);
    }
}
