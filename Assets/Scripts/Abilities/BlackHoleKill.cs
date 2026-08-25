using PurrNet;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BlackHoleKill : MonoBehaviour
{
    // This is deliberately presentation-only. Black-hole damage still uses the
    // projectile's own contact collider and is not enlarged by this field.
    public const float DarknessRadius = 30f;
    // The visual weight is capped below, so this richer darkening never becomes
    // a full-screen blackout when a player is close to the singularity.
    public const float DarknessExposureStops = -2.2f;
    [Header("Owner Immunity")]
    [SerializeField] private float ownerImmunitySeconds = 0.75f;

    [Header("Lifetime")]
    [SerializeField] private float lifetimeSeconds = 5f;

    private PlayerMovement ownerPm;
    private float armedTime;
    private float destroyTime;
    private Material blackHoleVfxMaterial;
    private Material eventHorizonMaterial;
    private VolumeProfile darknessProfile;
    private LineRenderer[] accretionRings;
    private LineRenderer[] blackLightningBolts;
    private LineRenderer blackLightningRing;
    private ParticleSystem orbitingSparks;
    private ParticleSystem escapingParticles;
    private ParticleSystem blackSparks;
    private Transform eventHorizon;
    private float nextOrbitSparkTime;
    private float nextEscapingParticleTime;
    private bool implosionSpawned;

    private static readonly Color BlackGlow = new Color(0.16f, 0.015f, 0.32f, 0.9f);
    private static readonly Color AccretionGlow = new Color(0.62f, 0.08f, 1f, 0.96f);
    private static readonly Color BlackParticle = new Color(0.002f, 0f, 0.004f, 0.98f);
    private static readonly Color DeepBlack = new Color(0.001f, 0f, 0.003f, 0.96f);

    /// <summary>
    /// Call this right after spawning the black hole.
    /// This resets immunity and lifetime every time.
    /// </summary>
    public void Init(PlayerMovement owner, float immunitySeconds = 0.75f)
    {
        ownerPm = owner;
        ownerImmunitySeconds = immunitySeconds;

        armedTime = Time.time;
        destroyTime = Time.time + lifetimeSeconds;

        Debug.Log("[BlackHoleKill] Init owner=" + (ownerPm != null ? ownerPm.name : "NULL"));
    }

    private void Start()
    {
        // Every client receives the same networked projectile, making its spawn
        // sound universal without changing the multiplayer RPC layout.
        SfxManager.PlayBlackHoleThrow();

        if (armedTime <= 0f)
            armedTime = Time.time;

        if (destroyTime <= 0f)
            destroyTime = Time.time + lifetimeSeconds;

        CreateBlackHoleVisual();
        CreateDarknessField();
    }

    private void Update()
    {
        UpdateAccretionVisual();
        if (Time.time >= destroyTime)
        {
            SpawnImplosion(transform.position);
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterServerContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterServerContact(other);
    }

    private void RegisterServerContact(Collider other)
    {
        NetworkManager net = NetworkManager.main;
        if (net == null || !net.isServer || other == null ||
            Time.time - armedTime < ownerImmunitySeconds)
            return;

        BoundaryPlayerState state = other.GetComponentInParent<BoundaryPlayerState>();
        if (state != null)
            state.ServerRegisterBlackHoleContact(gameObject.GetInstanceID());
    }

    private void OnDestroy()
    {
        if (!implosionSpawned && Application.isPlaying)
            SpawnImplosion(transform.position);
        if (blackHoleVfxMaterial != null)
            Destroy(blackHoleVfxMaterial);
        if (eventHorizonMaterial != null)
            Destroy(eventHorizonMaterial);
        if (darknessProfile != null)
            Destroy(darknessProfile);
    }

    // Networked projectile copies run this presentation locally on every peer;
    // no client is trusted for damage, expiry, or match results.
    private void CreateBlackHoleVisual()
    {
        blackHoleVfxMaterial = CreateMobileSafeVfxMaterial();
        if (blackHoleVfxMaterial == null)
            return;

        eventHorizon = CreateEventHorizon();
        CreateCoreParticle("Black Hole Dark Halo", BlackGlow, 1.28f);

        accretionRings = new LineRenderer[4];
        for (int index = 0; index < accretionRings.Length; index++)
        {
            GameObject ringObject = new GameObject("Neon Accretion Ring", typeof(LineRenderer));
            ringObject.transform.SetParent(transform, false);
            LineRenderer ring = ringObject.GetComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.positionCount = 49;
            ring.widthMultiplier = 0.048f + index * 0.014f;
            ring.numCornerVertices = 4;
            ring.material = blackHoleVfxMaterial;
            ring.startColor = index == 0 ? AccretionGlow : BlackGlow;
            ring.endColor = new Color(BlackGlow.r, BlackGlow.g, BlackGlow.b, 0.08f);
            accretionRings[index] = ring;
        }

        GameObject lightningRingObject = new GameObject("Black Lightning Event Ring", typeof(LineRenderer));
        lightningRingObject.transform.SetParent(transform, false);
        blackLightningRing = lightningRingObject.GetComponent<LineRenderer>();
        blackLightningRing.useWorldSpace = false;
        blackLightningRing.positionCount = 37;
        blackLightningRing.widthMultiplier = 0.16f;
        blackLightningRing.numCornerVertices = 0;
        blackLightningRing.material = blackHoleVfxMaterial;
        blackLightningRing.startColor = BlackParticle;
        blackLightningRing.endColor = DeepBlack;

        blackLightningBolts = new LineRenderer[5];
        for (int index = 0; index < blackLightningBolts.Length; index++)
        {
            GameObject boltObject = new GameObject("Black Hole Lightning Bolt", typeof(LineRenderer));
            boltObject.transform.SetParent(transform, false);
            LineRenderer bolt = boltObject.GetComponent<LineRenderer>();
            bolt.useWorldSpace = false;
            bolt.positionCount = 7;
            bolt.widthMultiplier = 0.22f;
            bolt.numCornerVertices = 0;
            bolt.material = blackHoleVfxMaterial;
            bolt.startColor = BlackParticle;
            bolt.endColor = DeepBlack;
            blackLightningBolts[index] = bolt;
        }

        GameObject sparksObject = new GameObject("Black Hole Orbiting Sparks", typeof(ParticleSystem));
        sparksObject.transform.SetParent(transform, false);
        orbitingSparks = sparksObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = orbitingSparks.main;
        main.loop = false;
        main.startLifetime = 0.3f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
        main.maxParticles = 54;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = orbitingSparks.emission;
        emission.enabled = false;
        orbitingSparks.GetComponent<ParticleSystemRenderer>().sharedMaterial = blackHoleVfxMaterial;
        escapingParticles = CreateParticleEmitter("Black Hole Escaping Particles", 240);
        blackSparks = CreateParticleEmitter("Black Hole Black Sparks", 180);
        nextOrbitSparkTime = Time.time;
        nextEscapingParticleTime = Time.time;
        UpdateAccretionVisual();
    }

    private void CreateCoreParticle(string effectName, Color color, float size)
    {
        GameObject coreObject = new GameObject(effectName, typeof(ParticleSystem));
        coreObject.transform.SetParent(transform, false);
        ParticleSystem core = coreObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = core.main;
        main.loop = false;
        main.startLifetime = lifetimeSeconds + 1f;
        main.startSpeed = 0f;
        main.startSize = size;
        main.startColor = color;
        main.maxParticles = 2;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = core.emission;
        emission.rateOverTime = 0f;
        emission.SetBurst(0, new ParticleSystem.Burst(0f, 1));
        core.GetComponent<ParticleSystemRenderer>().sharedMaterial = blackHoleVfxMaterial;
        core.Play();
    }

    private Transform CreateEventHorizon()
    {
        GameObject coreObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        coreObject.name = "Black Hole Event Horizon";
        coreObject.transform.SetParent(transform, false);
        coreObject.transform.localScale = Vector3.one * 0.9f;
        Collider coreCollider = coreObject.GetComponent<Collider>();
        if (coreCollider != null)
        {
            coreCollider.enabled = false;
            Destroy(coreCollider);
        }

        Renderer renderer = coreObject.GetComponent<Renderer>();
        eventHorizonMaterial = new Material(blackHoleVfxMaterial)
        {
            name = "BlackHoleEventHorizon"
        };
        eventHorizonMaterial.SetColor("_Color", DeepBlack);
        eventHorizonMaterial.SetColor("_BaseColor", DeepBlack);
        renderer.sharedMaterial = eventHorizonMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return coreObject.transform;
    }

    private ParticleSystem CreateParticleEmitter(string effectName, int maximumParticles)
    {
        GameObject particlesObject = new GameObject(effectName, typeof(ParticleSystem));
        particlesObject.transform.SetParent(transform, false);
        ParticleSystem particles = particlesObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.65f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.08f);
        main.maxParticles = maximumParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = blackHoleVfxMaterial;
        return particles;
    }

    private void UpdateAccretionVisual()
    {
        if (eventHorizon != null)
        {
            float pulse = 0.88f + Mathf.Sin(Time.time * 7f) * 0.06f;
            eventHorizon.localScale = Vector3.one * pulse;
            eventHorizon.Rotate(0f, 95f * Time.deltaTime, 0f, Space.Self);
        }

        if (accretionRings != null)
        {
            for (int ringIndex = 0; ringIndex < accretionRings.Length; ringIndex++)
            {
                LineRenderer ring = accretionRings[ringIndex];
                if (ring == null)
                    continue;

                float radius = 0.76f + ringIndex * 0.22f;
                float phase = Time.time * (7f + ringIndex * 2.75f) * (ringIndex % 2 == 0 ? 1f : -1f);
                for (int pointIndex = 0; pointIndex < ring.positionCount; pointIndex++)
                {
                    float angle = pointIndex / (float)(ring.positionCount - 1) * Mathf.PI * 2f + phase;
                    float tilt = 0.16f + ringIndex * 0.045f;
                    ring.SetPosition(pointIndex, new Vector3(Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle * 2f + ringIndex) * tilt,
                        Mathf.Sin(angle) * radius * 0.52f));
                }
            }
        }

        UpdateLightningRing();
        UpdateLightningBolts();

        if (orbitingSparks == null || Time.time < nextOrbitSparkTime)
            return;

        nextOrbitSparkTime = Time.time + 0.035f;
        float orbitAngle = Time.time * 11f;
        Vector3 position = new Vector3(Mathf.Cos(orbitAngle) * 1.28f,
            Mathf.Sin(orbitAngle * 2.5f) * 0.16f, Mathf.Sin(orbitAngle) * 0.66f);
        ParticleSystem.EmitParams spark = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = -position.normalized * 1.6f,
            startColor = AccretionGlow,
            startSize = 0.06f,
            startLifetime = 0.28f
        };
        orbitingSparks.Emit(spark, 1);

        if (escapingParticles == null || Time.time < nextEscapingParticleTime)
            return;

        nextEscapingParticleTime = Time.time + 0.012f;
        float escapeAngle = Time.time * -8.5f + Mathf.PI * 0.5f;
        Vector3 escapeDirection = new Vector3(Mathf.Cos(escapeAngle),
            Mathf.Sin(escapeAngle * 2f) * 0.16f, Mathf.Sin(escapeAngle) * 0.58f).normalized;
        ParticleSystem.EmitParams escapingParticle = new ParticleSystem.EmitParams
        {
            position = escapeDirection * 0.72f,
            velocity = escapeDirection * 4.8f,
            startColor = BlackParticle,
            startSize = 0.18f,
            startLifetime = 0.78f
        };
        escapingParticles.Emit(escapingParticle, 1);

        EmitBlackSparks();
    }

    private void UpdateLightningRing()
    {
        if (blackLightningRing == null)
            return;

        const float radius = 1.46f;
        int finalPoint = blackLightningRing.positionCount - 1;
        float flickerTime = Time.time * 18f;
        for (int pointIndex = 0; pointIndex <= finalPoint; pointIndex++)
        {
            float angle = pointIndex / (float)finalPoint * Mathf.PI * 2f;
            float noise = Mathf.PerlinNoise(pointIndex * 0.43f, flickerTime) - 0.5f;
            float jaggedRadius = radius + noise * 0.34f;
            blackLightningRing.SetPosition(pointIndex, new Vector3(Mathf.Cos(angle) * jaggedRadius,
                Mathf.Sin(angle * 4f + flickerTime) * 0.14f,
                Mathf.Sin(angle) * jaggedRadius * 0.58f));
        }
    }

    private void UpdateLightningBolts()
    {
        if (blackLightningBolts == null)
            return;

        float time = Time.time * 15f;
        for (int boltIndex = 0; boltIndex < blackLightningBolts.Length; boltIndex++)
        {
            LineRenderer bolt = blackLightningBolts[boltIndex];
            if (bolt == null)
                continue;

            float angle = boltIndex / (float)blackLightningBolts.Length * Mathf.PI * 2f + time * 0.18f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(time + boltIndex) * 0.16f,
                Mathf.Sin(angle) * 0.62f).normalized;
            Vector3 tangent = Vector3.Cross(direction, Vector3.up).normalized;
            float length = 2.6f + Mathf.PerlinNoise(boltIndex, time) * 2.2f;
            for (int pointIndex = 0; pointIndex < bolt.positionCount; pointIndex++)
            {
                float progress = pointIndex / (float)(bolt.positionCount - 1);
                float zigZag = pointIndex == 0 || pointIndex == bolt.positionCount - 1 ? 0f :
                    Mathf.Sin(pointIndex * 8.7f + boltIndex * 14.3f + time) * 0.42f;
                bolt.SetPosition(pointIndex, direction * Mathf.Lerp(0.36f, length, progress) + tangent * zigZag);
            }
        }
    }

    private void EmitBlackSparks()
    {
        if (blackSparks == null)
            return;

        for (int sparkIndex = 0; sparkIndex < 4; sparkIndex++)
        {
            float angle = Time.time * 17f + sparkIndex * Mathf.PI;
            Vector3 direction = new Vector3(Mathf.Cos(angle),
                Mathf.Sin(angle * 3f) * 0.22f, Mathf.Sin(angle) * 0.64f).normalized;
            ParticleSystem.EmitParams spark = new ParticleSystem.EmitParams
            {
                position = direction * 1.12f,
                velocity = direction * 1.9f,
                startColor = BlackParticle,
                startSize = 0.36f,
                startLifetime = 0.78f
            };
            blackSparks.Emit(spark, 1);
        }
    }

    private void CreateDarknessField()
    {
        GameObject fieldObject = new GameObject("Black Hole Darkness Field", typeof(Volume),
            typeof(BlackHoleDarknessVisual));
        fieldObject.transform.SetParent(transform, false);
        Volume volume = fieldObject.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 800f;
        volume.weight = 0f;
        darknessProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = darknessProfile;
        ColorAdjustments color = darknessProfile.Add<ColorAdjustments>(true);
        color.postExposure.Override(DarknessExposureStops);
        color.contrast.Override(28f);
        color.colorFilter.Override(new Color(0.08f, 0.03f, 0.12f, 1f));
        fieldObject.GetComponent<BlackHoleDarknessVisual>().Configure(volume, DarknessRadius);
    }

    private void SpawnImplosion(Vector3 center)
    {
        if (implosionSpawned || !Application.isPlaying)
            return;
        implosionSpawned = true;
        SfxManager.PlayBlackHoleImplosion();

        Material material = CreateMobileSafeVfxMaterial();
        if (material == null)
            return;

        GameObject implosionRoot = new GameObject("Black Hole Implosion");
        implosionRoot.transform.position = center;
        CreateZipEffect(implosionRoot.transform, material);
        CreateFlashRing(implosionRoot.transform, material, 2.9f, 0.1f, DeepBlack);
        EmitImplosionParticles(implosionRoot.transform, material, 64, 3.2f, -13f, DeepBlack);
        EmitImplosionParticles(implosionRoot.transform, material, 30, 0.12f, 10f, BlackGlow);
        Destroy(implosionRoot, 0.7f);
        Destroy(material, 0.7f);
    }

    private static void CreateZipEffect(Transform parent, Material material)
    {
        GameObject zipObject = new GameObject("Black Hole Zip Collapse", typeof(BlackHoleZipVisual));
        zipObject.transform.SetParent(parent, false);
        zipObject.GetComponent<BlackHoleZipVisual>().Configure(material, BlackParticle, DeepBlack);
    }

    private static void CreateFlashRing(Transform parent, Material material, float radius,
        float width, Color color)
    {
        GameObject ringObject = new GameObject("Black Hole Flash Ring", typeof(LineRenderer));
        ringObject.transform.SetParent(parent, false);
        LineRenderer ring = ringObject.GetComponent<LineRenderer>();
        const int points = 48;
        ring.useWorldSpace = false;
        ring.positionCount = points + 1;
        ring.widthMultiplier = width;
        ring.numCornerVertices = 2;
        ring.material = material;
        ring.startColor = color;
        ring.endColor = color;
        for (int index = 0; index <= points; index++)
        {
            float angle = index / (float)points * Mathf.PI * 2f;
            ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, 0.04f,
                Mathf.Sin(angle) * radius));
        }
    }

    private static void EmitImplosionParticles(Transform parent, Material material, int count,
        float startRadius, float signedSpeed, Color color)
    {
        GameObject particlesObject = new GameObject("Black Hole Implosion Particles", typeof(ParticleSystem));
        particlesObject.transform.SetParent(parent, false);
        ParticleSystem particles = particlesObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.48f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.1f);
        main.maxParticles = count;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        for (int index = 0; index < count; index++)
        {
            float angle = index / (float)count * Mathf.PI * 2f;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0.12f + (index % 3) * 0.08f,
                Mathf.Sin(angle)).normalized;
            ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
            {
                position = parent.position + radial * startRadius,
                velocity = radial * signedSpeed,
                startColor = color,
                startSize = 0.07f,
                startLifetime = 0.4f
            };
            particles.Emit(particle, 1);
        }
    }

    private static Material CreateMobileSafeVfxMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        if (shader == null)
            return null;

        Material material = new Material(shader) { name = "BlackHoleVfxMobileSafe" };
        material.SetColor("_Color", Color.white);
        material.SetColor("_BaseColor", Color.white);
        return material;
    }
}

internal sealed class BlackHoleDarknessVisual : MonoBehaviour
{
    private const float MaximumDarknessWeight = 0.8f;
    private Volume volume;
    private float radius;
    private Camera targetCamera;

    public void Configure(Volume target, float fieldRadius)
    {
        volume = target;
        radius = Mathf.Max(0.01f, fieldRadius);
    }

    private void Update()
    {
        if (volume == null)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
        {
            volume.weight = 0f;
            return;
        }

        float distance = Vector3.Distance(targetCamera.transform.position, transform.position);
        float proximity = Mathf.Clamp01(1f - distance / radius);
        volume.weight = Mathf.SmoothStep(0f, MaximumDarknessWeight, proximity);
    }
}

internal sealed class BlackHoleZipVisual : MonoBehaviour
{
    private const float Duration = 0.62f;
    private const int LineCount = 11;

    private LineRenderer[] lines;
    private float startedAt;

    public void Configure(Material material, Color blackColor, Color edgeColor)
    {
        startedAt = Time.time;
        lines = new LineRenderer[LineCount];
        for (int index = 0; index < LineCount; index++)
        {
            GameObject lineObject = new GameObject("Black Zip Streak", typeof(LineRenderer));
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 6;
            line.widthMultiplier = 0.25f - index * 0.012f;
            line.numCapVertices = 0;
            line.material = material;
            line.startColor = blackColor;
            line.endColor = edgeColor;
            lines[index] = line;
        }
    }

    private void Update()
    {
        if (lines == null)
            return;

        float progress = Mathf.Clamp01((Time.time - startedAt) / Duration);
        float length = Mathf.Lerp(5.2f, 0f, Mathf.SmoothStep(0f, 1f, progress));
        for (int index = 0; index < lines.Length; index++)
        {
            LineRenderer line = lines[index];
            if (line == null)
                continue;

            float angle = index / (float)lines.Length * Mathf.PI * 2f + Time.time * 1.6f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), (index % 2 == 0 ? 0.18f : -0.18f),
                Mathf.Sin(angle)).normalized;
            Vector3 tangent = Vector3.Cross(direction, Vector3.up).normalized;
            for (int pointIndex = 0; pointIndex < line.positionCount; pointIndex++)
            {
                float pointProgress = pointIndex / (float)(line.positionCount - 1);
                float jag = pointIndex == 0 || pointIndex == line.positionCount - 1 ? 0f :
                    Mathf.Sin(index * 13.7f + pointIndex * 9.1f + Time.time * 24f) * length * 0.16f;
                line.SetPosition(pointIndex, direction * length * (1f - pointProgress) + tangent * jag);
            }
        }
    }
}
