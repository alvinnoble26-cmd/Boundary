using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Small local-only impact bursts, pooled by the feedback voice.</summary>
public static class AbilityImpactVfx
{
    private const int CopiesPerVoice = 4;
    private const int VoiceCount = (int)AbilityHitVoice.Thud + 1;

    private static readonly ParticleSystem[,] Pool = new ParticleSystem[VoiceCount, CopiesPerVoice];
    private static readonly int[] Next = new int[VoiceCount];
    private static Transform root;
    private static Material glowMaterial;
    private static Material dustMaterial;
    private static Texture2D particleTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Prewarm()
    {
        if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            return;
        EnsurePool();
    }

    public static void PlayImpact(AbilityId id, Vector3 position) =>
        Play(AbilityFeedbackCatalog.Get(id), position);

    public static void PlayUnattributed(Vector3 position) =>
        Play(AbilityFeedbackCatalog.Unattributed, position);

    // The pool survives scene loads, but a rematch must not carry old bursts.
    public static void Reset()
    {
        if (root == null)
            return;
        for (int voice = 0; voice < VoiceCount; voice++)
        for (int copy = 0; copy < CopiesPerVoice; copy++)
            Pool[voice, copy].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static void Play(AbilityFeedbackProfile profile, Vector3 position)
    {
        if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            return;
        EnsurePool();
        if (root == null)
            return;

        int voice = (int)profile.Voice;
        if (voice < 0 || voice >= VoiceCount)
            return;
        int copy = Next[voice];
        Next[voice] = (copy + 1) % CopiesPerVoice;
        ParticleSystem particles = Pool[voice, copy];
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        // Feedback supplies the other player's position, not the exact contact
        // point. A small upward offset also avoids hard floor intersections
        // while the pipeline has no depth texture for soft particles.
        particles.transform.position = position + Vector3.up * 0.55f;
        ParticleSystem.MainModule main = particles.main;
        Color color = profile.Accent;
        if (profile.Voice != AbilityHitVoice.Thud && profile.Voice != AbilityHitVoice.Whoosh)
        {
            color.r *= 1.7f;
            color.g *= 1.7f;
            color.b *= 1.7f;
        }
        main.startColor = color;
        particles.Play(true);
    }

    private static void EnsurePool()
    {
        if (root != null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return;

        root = new GameObject("Ability Impact VFX Pool").transform;
        Object.DontDestroyOnLoad(root.gameObject);
        particleTexture = CreateParticleTexture();
        glowMaterial = CreateMaterial(shader, true);
        dustMaterial = CreateMaterial(shader, false);
        for (int voice = 0; voice < VoiceCount; voice++)
        for (int copy = 0; copy < CopiesPerVoice; copy++)
            Pool[voice, copy] = CreateBurst((AbilityHitVoice)voice, copy);
    }

    private static ParticleSystem CreateBurst(AbilityHitVoice voice, int copy)
    {
        GameObject effect = new GameObject("Impact " + voice + " " + copy, typeof(ParticleSystem));
        effect.transform.SetParent(root, false);
        ParticleSystem particles = effect.GetComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.12f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 16;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, BurstCount(voice)) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;

        ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
        fade.enabled = true;
        fade.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f) }
        });

        ParticleSystemRenderer renderer = effect.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = voice == AbilityHitVoice.Thud || voice == AbilityHitVoice.Whoosh
            ? dustMaterial : glowMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        switch (voice)
        {
            case AbilityHitVoice.EnergyBlast:
                main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
                break;
            case AbilityHitVoice.Blade:
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 2.4f;
                renderer.velocityScale = 0.16f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
                break;
            case AbilityHitVoice.Electric:
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 2.8f;
                renderer.velocityScale = 0.22f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.3f);
                ParticleSystem.NoiseModule noise = particles.noise;
                noise.enabled = true;
                noise.quality = ParticleSystemNoiseQuality.Low;
                noise.strength = 0.5f;
                noise.frequency = 5f;
                break;
            case AbilityHitVoice.Gravity:
                shape.radius = 0.32f;
                main.startSpeed = 0f;
                ParticleSystem.VelocityOverLifetimeModule gravity = particles.velocityOverLifetime;
                gravity.enabled = true;
                gravity.radial = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, -3f), new Keyframe(0.45f, -1f),
                    new Keyframe(0.6f, 4f), new Keyframe(1f, 1f)));
                break;
            case AbilityHitVoice.Whoosh:
                main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.18f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
                break;
            case AbilityHitVoice.Shimmer:
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                ParticleSystem.VelocityOverLifetimeModule rise = particles.velocityOverLifetime;
                rise.enabled = true;
                rise.y = 1.8f;
                break;
            case AbilityHitVoice.Ice:
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 1.3f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                break;
            case AbilityHitVoice.Thud:
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
                break;
        }
        return particles;
    }

    private static short BurstCount(AbilityHitVoice voice)
    {
        switch (voice)
        {
            case AbilityHitVoice.EnergyBlast: return 12;
            case AbilityHitVoice.Blade: return 8;
            case AbilityHitVoice.Electric: return 7;
            case AbilityHitVoice.Gravity: return 10;
            case AbilityHitVoice.Whoosh: return 6;
            case AbilityHitVoice.Shimmer: return 9;
            case AbilityHitVoice.Ice: return 8;
            default: return 5;
        }
    }

    private static Material CreateMaterial(Shader shader, bool additive)
    {
        Material material = new Material(shader) { name = additive ? "Impact Glow" : "Impact Dust" };
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", particleTexture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", particleTexture);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", additive ? 1f : 0f);
        material.renderQueue = 3100;
        return material;
    }

    private static Texture2D CreateParticleTexture()
    {
        const int size = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Impact Particle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
            float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - dx * dx - dy * dy), 2f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply(false, true);
        return texture;
    }
}
