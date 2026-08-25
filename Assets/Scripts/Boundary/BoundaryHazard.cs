using System.Collections;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class BoundaryHazard : NetworkBehaviour
{
    private static readonly List<BoundaryHazard> ActiveHazards = new List<BoundaryHazard>();
    private static bool hollowGlowActive;
    private static bool darknessGlowActive;

    private readonly SyncVar<BoundaryHazardKind> kind = new(BoundaryHazardKind.Cube, ownerAuth: false);
    private readonly SyncVar<uint> spawnTick = new(0u, ownerAuth: false);
    private readonly SyncVar<float> lifetime = new(20f, ownerAuth: false);
    private readonly SyncVar<int> variant = new(0, ownerAuth: false);
    private readonly SyncVar<bool> arenaMass = new(false, ownerAuth: false);
    private readonly SyncVar<bool> survivesInner = new(false, ownerAuth: false);
    private readonly SyncVar<float> networkScale = new(1f, 0.01f, ownerAuth: false);

    [Header("Orbit")]
    [SerializeField] private float orbitAcceleration = 18f;
    [SerializeField] private float radialSpring = 4.5f;
    [SerializeField] private float verticalSpring = 5f;
    [SerializeField] private float maximumSpeed = 22f;

    [Header("Impact")]
    [SerializeField] private float cubeImpact = 5.5f;
    [SerializeField] private float meteorImpact = 11f;

    private Rigidbody body;
    private BoxCollider boxCollider;
    private SphereCollider sphereCollider;
    private Renderer cubeRenderer;
    private Renderer sphereRenderer;
    private Vector3 serverTarget;
    private Vector3 pendingVelocity;
    private Vector3 initialPosition;
    private Vector3 absorptionStartPosition;
    private Vector3 arenaMassStartScale;
    private bool absorptionStarted;
    private float absorptionStartedAt;
    private float absorptionDuration;
    private float abilityInfluenceUntil;
    private int lastPlatformIndex = -1;
    private float lastPlatformContactAt = -10f;
    private bool visualApplied;
    private bool buildVisuals;
    private float desiredOrbitRadius;
    private float desiredOrbitHeight;
    private Material cubeMaterial;
    private Material sphereMaterial;
    private Material tesseractMaterial;
    private Material tesseractCoreMaterial;
    private Transform tesseractRig;
    private Transform tesseractInnerFrame;
    private Transform blackHoleRig;
    private readonly List<Transform> blackHoleRings = new List<Transform>();
    private readonly List<float> blackHoleRingSpeeds = new List<float>();
    private readonly List<Material> blackHoleMaterials = new List<Material>();

    private static readonly Vector2Int[] TesseractEdges =
    {
        new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(0, 4),
        new Vector2Int(1, 3), new Vector2Int(1, 5), new Vector2Int(2, 3),
        new Vector2Int(2, 6), new Vector2Int(3, 7), new Vector2Int(4, 5),
        new Vector2Int(4, 6), new Vector2Int(5, 7), new Vector2Int(6, 7)
    };

    private static readonly Color TesseractBlue = new Color(0.025f, 0.38f, 1f);
    private Color tesseractGlowColor = TesseractBlue;
    private float tesseractGlowIntensity = 18f;

    public BoundaryHazardKind Kind => kind.value;
    public int Variant => variant.value;
    public bool IsArenaMass => arenaMass.value;
    public bool SurvivesInnerRing => arenaMass.value && survivesInner.value;
    public bool AbilityInfluenceActive => Time.time < abilityInfluenceUntil;
    public bool IsRealSingularity => kind.value == BoundaryHazardKind.BlackRainSingularity ||
                                     kind.value == BoundaryHazardKind.ArenaBlackHole ||
                                     (kind.value == BoundaryHazardKind.FalseSingularity && variant.value == 1);

    private uint CurrentTick
    {
        get
        {
            NetworkManager manager = NetworkManager.main;
            return manager != null && manager.tickModule != null
                ? manager.tickModule.syncedTick
                : (uint)Mathf.Max(0, Mathf.RoundToInt(Time.unscaledTime * 20f));
        }
    }

    private float Age
    {
        get
        {
            NetworkManager manager = NetworkManager.main;
            int rate = manager != null && manager.tickModule != null ? manager.tickModule.tickRate : 20;
            return spawnTick.value == 0u ? 0f : (CurrentTick - spawnTick.value) / (float)rate;
        }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        sphereCollider = GetComponent<SphereCollider>();
        buildVisuals = !Application.isBatchMode &&
                       SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        if (buildVisuals)
        {
            EnsureVisuals();
            cubeRenderer = transform.Find("CubeVisual")?.GetComponent<Renderer>();
            sphereRenderer = transform.Find("SphereVisual")?.GetComponent<Renderer>();
        }
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        initialPosition = transform.position;
        ActiveHazards.Add(this);
    }

    private void EnsureVisuals()
    {
        if (transform.Find("CubeVisual") == null)
            CreateRuntimeVisual(PrimitiveType.Cube, "CubeVisual", Vector3.one);
        if (transform.Find("SphereVisual") == null)
        {
            GameObject sphere = CreateRuntimeVisual(PrimitiveType.Sphere, "SphereVisual", Vector3.one * 1.65f);
            sphere.SetActive(false);
        }
        EnsureTesseractRig();
        EnsureBlackHoleRig();
    }

    private GameObject CreateRuntimeVisual(PrimitiveType primitive, string visualName, Vector3 scale)
    {
        GameObject visual = GameObject.CreatePrimitive(primitive);
        visual.name = visualName;
        visual.transform.SetParent(transform, false);
        visual.transform.localScale = scale;
        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
            Destroy(visualCollider);
        return visual;
    }

    private void EnsureTesseractRig()
    {
        Transform cubeVisual = transform.Find("CubeVisual");
        if (cubeVisual == null)
            return;

        Transform existing = cubeVisual.Find("Tesseract Wireframe");
        if (existing != null)
        {
            tesseractRig = existing;
            return;
        }

        tesseractRig = new GameObject("Tesseract Wireframe").transform;
        tesseractRig.SetParent(cubeVisual, false);
        tesseractMaterial = CreateMaterial(Color.black, TesseractBlue, 18f);
        tesseractCoreMaterial = CreateMaterial(
            new Color(0.001f, 0.006f, 0.025f), TesseractBlue, 5.5f);

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cube);
        core.name = "Compressed Blue Void Core";
        core.transform.SetParent(tesseractRig, false);
        core.transform.localScale = Vector3.one * 0.34f;
        Collider coreCollider = core.GetComponent<Collider>();
        if (coreCollider != null)
            Destroy(coreCollider);
        core.GetComponent<Renderer>().sharedMaterial = tesseractCoreMaterial;

        Vector3 innerOffset = new Vector3(0.16f, -0.12f, 0.14f);
        CreateTesseractCube("Outer Frame", Vector3.zero, 0.56f, 0.052f, 0.070f);
        tesseractInnerFrame = CreateTesseractCube(
            "Inner Frame", innerOffset, 0.285f, 0.035f, 0.048f);
        for (int corner = 0; corner < 8; corner++)
        {
            CreateTesseractLine(
                "Dimensional Link " + (corner + 1),
                TesseractCorner(corner, Vector3.zero, 0.56f),
                TesseractCorner(corner, innerOffset, 0.285f),
                0.032f);
        }
    }

    private Transform CreateTesseractCube(string frameName, Vector3 center, float halfSize,
        float lineWidth, float nodeRadius)
    {
        Transform frame = new GameObject(frameName).transform;
        frame.SetParent(tesseractRig, false);
        for (int edge = 0; edge < TesseractEdges.Length; edge++)
        {
            Vector2Int endpoints = TesseractEdges[edge];
            CreateTesseractLine("Edge " + (edge + 1),
                TesseractCorner(endpoints.x, center, halfSize),
                TesseractCorner(endpoints.y, center, halfSize), lineWidth, frame);
        }
        for (int corner = 0; corner < 8; corner++)
            CreateTesseractNode("Node " + (corner + 1), TesseractCorner(corner, center, halfSize), nodeRadius, frame);
        return frame;
    }

    private void CreateTesseractLine(string lineName, Vector3 start, Vector3 end, float width,
        Transform parent = null)
    {
        LineRenderer line = new GameObject(lineName, typeof(LineRenderer)).GetComponent<LineRenderer>();
        line.transform.SetParent(parent != null ? parent : tesseractRig, false);
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.startColor = line.endColor = TesseractBlue;
        line.sharedMaterial = tesseractMaterial;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private void CreateTesseractNode(string nodeName, Vector3 position, float radius, Transform parent)
    {
        GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        node.name = nodeName;
        node.transform.SetParent(parent, false);
        node.transform.localPosition = position;
        node.transform.localScale = Vector3.one * radius * 2f;
        Collider nodeCollider = node.GetComponent<Collider>();
        if (nodeCollider != null)
            Destroy(nodeCollider);
        node.GetComponent<Renderer>().sharedMaterial = tesseractMaterial;
    }

    private static Vector3 TesseractCorner(int index, Vector3 center, float halfSize)
    {
        return center + new Vector3(
            (index & 1) == 0 ? -halfSize : halfSize,
            (index & 2) == 0 ? -halfSize : halfSize,
            (index & 4) == 0 ? -halfSize : halfSize);
    }

    private void EnsureBlackHoleRig()
    {
        Transform existing = transform.Find("BlackHoleAccretion");
        if (existing != null)
        {
            blackHoleRig = existing;
            return;
        }

        blackHoleRig = new GameObject("BlackHoleAccretion").transform;
        blackHoleRig.SetParent(transform, false);
        CreateBlackHoleRing("Molten Accretion", 2.05f, 0.20f,
            new Color(1f, 0.20f, 0.035f), new Vector3(16f, 0f, 4f), 92f);
        CreateBlackHoleRing("White Photon Crown", 1.72f, 0.105f,
            new Color(1f, 0.86f, 0.60f), new Vector3(7f, 0f, -3f), 128f);
        CreateBlackHoleRing("Violet Lensing", 2.45f, 0.12f,
            new Color(0.72f, 0.08f, 1f), new Vector3(-11f, 0f, 18f), -68f);
        CreateBlackHoleRing("Photon Orbit", 2.82f, 0.065f,
            new Color(0.08f, 0.72f, 1f), new Vector3(24f, 0f, -12f), 48f);
        CreateBlackHoleRing("Polar Lens", 2.18f, 0.055f,
            new Color(0.82f, 0.32f, 1f), new Vector3(76f, 0f, 0f), -31f);
        CreateBlackHoleRing("Crimson Outer Lens", 3.12f, 0.045f,
            new Color(1f, 0.045f, 0.14f), new Vector3(-19f, 0f, -8f), 27f);
        CreateBlackHoleJet("North Micro Jet", 1f);
        CreateBlackHoleJet("South Micro Jet", -1f);
        CreateBlackHoleParticles();
        blackHoleRig.gameObject.SetActive(false);
    }

    private void CreateBlackHoleRing(
        string ringName,
        float radius,
        float width,
        Color emission,
        Vector3 tilt,
        float speed)
    {
        LineRenderer line = new GameObject(ringName, typeof(LineRenderer)).GetComponent<LineRenderer>();
        line.transform.SetParent(blackHoleRig, false);
        line.transform.localRotation = Quaternion.Euler(tilt);
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 72;
        line.startWidth = width;
        line.endWidth = width * 0.55f;
        Material material = CreateMaterial(Color.black, emission, 7.5f);
        blackHoleMaterials.Add(material);
        line.sharedMaterial = material;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = Mathf.PI * 2f * i / line.positionCount;
            float noise = 1f + Mathf.Sin(angle * 5f + blackHoleRings.Count) * 0.06f;
            line.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius * noise,
                Mathf.Sin(angle * 3f) * 0.055f,
                Mathf.Sin(angle) * radius * noise));
        }
        blackHoleRings.Add(line.transform);
        blackHoleRingSpeeds.Add(speed);
    }

    private void CreateBlackHoleJet(string jetName, float direction)
    {
        LineRenderer jet = new GameObject(jetName, typeof(LineRenderer)).GetComponent<LineRenderer>();
        jet.transform.SetParent(blackHoleRig, false);
        jet.useWorldSpace = false;
        jet.positionCount = 5;
        jet.startWidth = 0.18f;
        jet.endWidth = 0.012f;
        Material material = CreateMaterial(Color.black, new Color(0.22f, 0.78f, 1f), 8f);
        blackHoleMaterials.Add(material);
        jet.sharedMaterial = material;
        for (int i = 0; i < jet.positionCount; i++)
        {
            float t = i / (float)(jet.positionCount - 1);
            jet.SetPosition(i, new Vector3(
                Mathf.Sin(t * Mathf.PI) * 0.10f,
                direction * Mathf.Lerp(1.1f, 4.4f, t),
                0f));
        }
    }

    private void CreateBlackHoleParticles()
    {
        GameObject particleObject = new GameObject("Orbiting Photon Sparks", typeof(ParticleSystem));
        particleObject.transform.SetParent(blackHoleRig, false);
        ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.085f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.26f, 0.045f, 0.9f),
            new Color(0.16f, 0.76f, 1f, 0.82f));
        main.maxParticles = 64;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 26f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 2.55f;
        shape.radiusThickness = 0.28f;
        shape.rotation = new Vector3(76f, 0f, 0f);

        Material material = CreateMaterial(Color.black, new Color(0.20f, 0.72f, 1f), 7f);
        blackHoleMaterials.Add(material);
        ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    protected override void OnSpawned()
    {
        kind.onChanged += OnKindChanged;
        networkScale.onChanged += OnNetworkScaleChanged;
        OnNetworkScaleChanged(networkScale.value);
        ApplyVisual();
        StartCoroutine(ResolveAuthority());
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ActiveHazards.Remove(this);
        if (cubeMaterial != null) Destroy(cubeMaterial);
        if (sphereMaterial != null) Destroy(sphereMaterial);
        if (tesseractMaterial != null) Destroy(tesseractMaterial);
        if (tesseractCoreMaterial != null) Destroy(tesseractCoreMaterial);
        foreach (Material material in blackHoleMaterials)
        {
            if (material != null)
                Destroy(material);
        }
    }

    private void Update()
    {
        // NetworkTransform can apply its initial snapshot after the SyncVar
        // callback. Hold arena-mass replicas at the server-authored scale
        // until an intentional absorption animation changes it.
        if (arenaMass.value && !absorptionStarted && networkScale.value > 0.01f &&
            Mathf.Abs(transform.localScale.x - networkScale.value) > 0.01f)
        {
            transform.localScale = Vector3.one * networkScale.value;
        }

        if (tesseractRig != null && tesseractRig.gameObject.activeSelf)
        {
            tesseractRig.Rotate(new Vector3(9f, 17f, 5f) * Time.deltaTime, Space.Self);
            if (tesseractInnerFrame != null)
                tesseractInnerFrame.Rotate(new Vector3(-21f, 13f, -16f) * Time.deltaTime, Space.Self);
            float pulse = 0.88f + Mathf.Sin(Time.time * 5.4f + variant.value * 0.37f) * 0.12f;
            ApplyTesseractEmission(pulse);
        }

        if (blackHoleRig == null || !blackHoleRig.gameObject.activeSelf)
            return;

        for (int i = 0; i < blackHoleRings.Count; i++)
            blackHoleRings[i].Rotate(Vector3.up, blackHoleRingSpeeds[i] * Time.deltaTime, Space.Self);

        if (sphereRenderer != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 4.1f + variant.value) * 0.045f;
            sphereRenderer.transform.localScale = Vector3.one * (1.65f * pulse);
        }
    }

    public void ServerConfigure(
        BoundaryHazardKind hazardKind,
        uint serverSpawnTick,
        float secondsAlive,
        int hazardVariant,
        Vector3 target,
        Vector3 velocity)
    {
        kind.value = hazardKind;
        spawnTick.value = serverSpawnTick;
        lifetime.value = Mathf.Max(1f, secondsAlive);
        variant.value = hazardVariant;
        arenaMass.value = false;
        survivesInner.value = false;
        serverTarget = target;
        pendingVelocity = velocity;
        initialPosition = transform.position;
        ConfigureOrbitLane();
        ApplyVisual();
    }

    public void ServerConfigureArenaMass(
        BoundaryHazardKind hazardKind,
        uint serverSpawnTick,
        int populationIndex,
        bool innerRingSurvivor,
        Vector3 restingPosition)
    {
        kind.value = hazardKind;
        spawnTick.value = serverSpawnTick;
        lifetime.value = 250f;
        variant.value = populationIndex;
        arenaMass.value = true;
        survivesInner.value = innerRingSurvivor;
        networkScale.value = transform.localScale.x;
        serverTarget = restingPosition;
        pendingVelocity = Vector3.zero;
        initialPosition = transform.position;
        arenaMassStartScale = transform.localScale;
        ConfigureOrbitLane();
        ApplyVisual();
    }

    public void RegisterAbilityInfluence()
    {
        if (isServer && arenaMass.value)
            abilityInfluenceUntil = Time.time + 1.4f;
    }

    public void ServerApplyAbilityVelocity(Vector3 velocityChange)
    {
        if (!isServer || !arenaMass.value || absorptionStarted || body == null || body.isKinematic)
            return;

        RegisterAbilityInfluence();
        body.WakeUp();
        body.AddForce(velocityChange, ForceMode.VelocityChange);
        if (body.linearVelocity.magnitude > 100f)
            body.linearVelocity = body.linearVelocity.normalized * 100f;
    }

    public static int ServerApplyArenaMassField(
        Vector3 center,
        float radius,
        bool outward,
        float fieldForce,
        float fieldAcceleration,
        AnimationCurve distanceFalloff)
    {
        if (radius <= 0f)
            return 0;

        int affected = 0;
        for (int i = ActiveHazards.Count - 1; i >= 0; i--)
        {
            BoundaryHazard hazard = ActiveHazards[i];
            if (hazard == null)
            {
                ActiveHazards.RemoveAt(i);
                continue;
            }

            if (!hazard.isServer || !hazard.arenaMass.value || hazard.absorptionStarted ||
                hazard.body == null)
            {
                continue;
            }

            Vector3 offset = hazard.body.position - center;
            float distance = offset.magnitude;
            if (distance < 0.01f || distance > radius)
                continue;

            float normalizedDistance = Mathf.Clamp01(distance / radius);
            float influence = distanceFalloff != null
                ? Mathf.Clamp01(distanceFalloff.Evaluate(normalizedDistance))
                : 1f - BoundaryMath.EaseInOut(normalizedDistance);
            Vector3 direction = offset / distance;
            if (!outward)
                direction = -direction;

            // Authority resolution may briefly leave a newly spawned arena
            // mass kinematic. Since this executes on the server and arena
            // masses are always dynamic outside absorption, restore it here.
            if (hazard.body.isKinematic)
            {
                hazard.body.isKinematic = false;
                hazard.body.useGravity = false;
            }

            hazard.ServerApplyAbilityVelocity(
                direction * BoundaryMath.FieldVelocityChange(
                    fieldForce,
                    fieldAcceleration,
                    influence,
                    hazard.body.mass));
            affected++;
        }

        return affected;
    }

    public static int ServerApplyArenaMassGravity(Vector3 center, float radius, float acceleration)
    {
        if (radius <= 0f || acceleration <= 0f)
            return 0;

        int affected = 0;
        for (int index = ActiveHazards.Count - 1; index >= 0; index--)
        {
            BoundaryHazard hazard = ActiveHazards[index];
            if (hazard == null)
            {
                ActiveHazards.RemoveAt(index);
                continue;
            }
            if (!hazard.isServer || !hazard.arenaMass.value || hazard.absorptionStarted ||
                hazard.body == null ||
                (hazard.kind.value != BoundaryHazardKind.Cube &&
                 hazard.kind.value != BoundaryHazardKind.ArenaBlackHole))
                continue;

            Vector3 delta = center - hazard.body.worldCenterOfMass;
            float distance = delta.magnitude;
            if (distance <= 0.05f || distance >= radius)
                continue;

            if (hazard.body.isKinematic)
            {
                hazard.body.isKinematic = false;
                hazard.body.useGravity = false;
            }
            hazard.RegisterAbilityInfluence();
            hazard.body.WakeUp();
            hazard.body.AddForce(delta.normalized * acceleration *
                Mathf.Clamp01(1f - distance / radius), ForceMode.Acceleration);
            affected++;
        }

        return affected;
    }

    public static void SetHollowGlowForAll(bool enabled)
    {
        hollowGlowActive = enabled;
        for (int index = ActiveHazards.Count - 1; index >= 0; index--)
        {
            BoundaryHazard hazard = ActiveHazards[index];
            if (hazard == null)
            {
                ActiveHazards.RemoveAt(index);
                continue;
            }
            hazard.ApplyArenaMassGlow();
        }
    }

    public static void SetDarknessGlowForAll(bool enabled)
    {
        darknessGlowActive = enabled;
        for (int index = ActiveHazards.Count - 1; index >= 0; index--)
        {
            BoundaryHazard hazard = ActiveHazards[index];
            if (hazard == null)
            {
                ActiveHazards.RemoveAt(index);
                continue;
            }
            hazard.ApplyArenaMassGlow();
        }
    }

    public static bool ShouldGlowDuringHollow(int populationIndex)
    {
        return populationIndex >= 0;
    }

    public void ServerPulse(bool outward)
    {
        if (!isServer || body == null || body.isKinematic)
            return;

        BoundaryMatchController match = BoundaryMatchController.Instance;
        Vector3 center = match != null ? match.ArenaCenter : Vector3.zero;
        Vector3 direction = body.position - center;
        direction.y = 0.4f;
        if (!outward) direction = -direction;
        body.AddForce(direction.normalized *
            (16f * BoundaryMath.DisasterPower(BoundaryDisaster.UnstableMass)),
            ForceMode.VelocityChange);
        SetEmission(outward ? new Color(1f, 0.2f, 0.8f) : new Color(0.2f, 0.85f, 1f), 5f);
    }

    private IEnumerator ResolveAuthority()
    {
        NetworkManager manager = NetworkManager.main;
        while (manager == null)
        {
            yield return null;
            manager = NetworkManager.main;
        }

        if (body == null)
            yield break;

        bool kinematicHazard = IsKinematicSingularity(kind.value);
        body.isKinematic = !manager.isServer || kinematicHazard;
        body.useGravity = manager.isServer && !kinematicHazard && !arenaMass.value;
        if (manager.isServer && !body.isKinematic)
        {
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = pendingVelocity;
            body.angularVelocity = new Vector3(1.5f, 2f, -1f);
        }
    }

    private void FixedUpdate()
    {
        if (!visualApplied)
            ApplyVisual();

        BoundaryMatchController activeMatch = BoundaryMatchController.Instance;
        if (arenaMass.value && activeMatch != null && ShouldAbsorbDuring(activeMatch.Transition))
            SetCollisionEnabled(false);

        if (!isServer)
            return;

        if (Age >= lifetime.value)
        {
            Despawn();
            return;
        }

        if (arenaMass.value)
        {
            TickArenaMass();
            return;
        }

        switch (kind.value)
        {
            case BoundaryHazardKind.BlackRainSingularity:
                TickRainSingularity();
                break;
            case BoundaryHazardKind.FalseSingularity:
                TickFalseSingularity();
                break;
            case BoundaryHazardKind.OrbitalDebris:
            case BoundaryHazardKind.TornadoDebris:
                TickOrbit();
                break;
            case BoundaryHazardKind.Meteor:
                if (body != null && !body.isKinematic)
                    body.AddForce(Vector3.down *
                        (11f * BoundaryMath.DisasterPower(BoundaryDisaster.MeteorBreak)),
                        ForceMode.Acceleration);
                break;
        }
    }

    private void TickArenaMass()
    {
        BoundaryMatchController match = BoundaryMatchController.Instance;
        if (match == null || body == null)
            return;

        bool absorbingNow = absorptionStarted || ShouldAbsorbDuring(match.Transition);

        if (absorbingNow)
        {
            if (!absorptionStarted)
            {
                absorptionStarted = true;
                absorptionStartPosition = body.position;
                arenaMassStartScale = transform.localScale;
                absorptionStartedAt = Time.time;
                absorptionDuration = Mathf.Max(0.6f, match.TransitionRemaining);
                body.isKinematic = true;
                body.useGravity = false;
                SetCollisionEnabled(false);
            }

            float stagger = (variant.value % 5) * 0.055f;
            float elapsed = Time.time - absorptionStartedAt;
            float flight = BoundaryMath.EaseInOut(Mathf.InverseLerp(
                absorptionDuration * stagger,
                absorptionDuration,
                elapsed));
            Vector3 target = match.SingularityPosition;
            Vector3 tangent = Vector3.Cross(Vector3.up, target - absorptionStartPosition).normalized;
            Vector3 control = Vector3.Lerp(absorptionStartPosition, target, 0.52f) +
                              Vector3.up * (6f + variant.value % 4 * 1.8f) +
                              tangent * Mathf.Lerp(-6f, 6f, (variant.value % 7) / 6f);
            body.MovePosition(QuadraticBezier(absorptionStartPosition, control, target, flight));
            body.MoveRotation(Quaternion.Euler(
                flight * (320f + variant.value * 7f),
                flight * (480f + variant.value * 5f),
                flight * 210f));
            transform.localScale = Vector3.Lerp(arenaMassStartScale, arenaMassStartScale * 0.025f, flight);
            if (flight >= 0.995f)
                Despawn();
            return;
        }

        if (body.isKinematic)
            return;

        Vector3 offset = body.position - match.ArenaCenter;
        Vector3 flat = new Vector3(offset.x, 0f, offset.z);
        float maximumRadius = Mathf.Max(8f, match.RingRadius * 0.72f);
        if (flat.magnitude > maximumRadius)
        {
            float inwardForce = Mathf.Min(15f, 2f + (flat.magnitude - maximumRadius) * 0.8f);
            body.AddForce(-flat.normalized * inwardForce, ForceMode.Acceleration);
        }

        float bodyClearance = kind.value == BoundaryHazardKind.ArenaBlackHole
            ? sphereCollider.radius * transform.lossyScale.y
            : boxCollider.size.y * transform.lossyScale.y * 0.5f;
        float desiredY = BoundaryMatchController.IsFloatingArenaMass(variant.value)
            ? serverTarget.y
            : match.PlatformSurfaceYAtRadius(flat.magnitude) + bodyClearance;
        body.AddForce(Vector3.up * ((desiredY - body.position.y) * 5.2f), ForceMode.Acceleration);
        if (body.linearVelocity.magnitude > 30f)
            body.linearVelocity = body.linearVelocity.normalized * 30f;
    }

    private bool ShouldAbsorbDuring(BoundaryTransition activeTransition)
    {
        if (!arenaMass.value || survivesInner.value)
            return false;

        bool survivesInitialCollapse = BoundaryMath.SurvivesInitialBoundaryCollapse(variant.value);
        return (!survivesInitialCollapse && activeTransition == BoundaryTransition.ClosingOuterRing) ||
               (survivesInitialCollapse && activeTransition == BoundaryTransition.ClosingMiddleRing);
    }

    private void TickRainSingularity()
    {
        BoundaryMatchController match = BoundaryMatchController.Instance;
        if (match == null)
            return;

        float age = Age;
        Vector3 position;
        if (age < 1.6f)
        {
            position = Vector3.Lerp(initialPosition, serverTarget, BoundaryMath.EaseInOut(age / 1.6f));
        }
        else if (age < 6.5f)
        {
            position = serverTarget + Vector3.up * (Mathf.Sin(age * 3f) * 0.18f);
        }
        else
        {
            float rise = Mathf.InverseLerp(6.5f, Mathf.Max(6.6f, lifetime.value), age);
            position = Vector3.Lerp(serverTarget, match.SingularityPosition, BoundaryMath.EaseInOut(rise));
        }

        body.MovePosition(position);
        float power = BoundaryMath.DisasterPower(BoundaryDisaster.BlackRain);
        PullServerObjects(IsRealSingularity ? 16f * power : 4f,
            7f * transform.localScale.x * power);
    }

    private void TickFalseSingularity()
    {
        body.MovePosition(serverTarget + Vector3.up * (Mathf.Sin(Age * 2.7f + variant.value) * 0.12f));
        PullServerObjects(IsRealSingularity ? 18f : 2.2f, IsRealSingularity ? 8f : 4f);
    }

    private void TickOrbit()
    {
        if (body == null || body.isKinematic)
            return;

        BoundaryMatchController match = BoundaryMatchController.Instance;
        if (match == null)
            return;

        Vector3 offset = body.position - match.ArenaCenter;
        Vector3 flat = new Vector3(offset.x, 0f, offset.z);
        if (flat.sqrMagnitude < 0.1f)
            return;

        Vector3 radialDirection = flat.normalized;
        Vector3 tangent = Vector3.Cross(Vector3.up, radialDirection) * match.CurrentDirection;
        float radialError = flat.magnitude - desiredOrbitRadius;
        float heightError = body.position.y - (match.ArenaFloorY + desiredOrbitHeight);

        float orbitForce = kind.value == BoundaryHazardKind.TornadoDebris
            ? orbitAcceleration * 1.2f
            : orbitAcceleration;
        body.AddForce(tangent * orbitForce, ForceMode.Acceleration);
        body.AddForce(-radialDirection * radialError * radialSpring, ForceMode.Acceleration);
        body.AddForce(Vector3.down * heightError * verticalSpring, ForceMode.Acceleration);

        if (kind.value == BoundaryHazardKind.TornadoDebris)
        {
            body.AddForce(Vector3.up * 1.4f, ForceMode.Acceleration);
            desiredOrbitRadius = Mathf.Max(2.5f, desiredOrbitRadius - Time.fixedDeltaTime * 0.08f);
            desiredOrbitHeight += Time.fixedDeltaTime * 0.16f;
        }

        if (body.linearVelocity.magnitude > maximumSpeed)
            body.linearVelocity = body.linearVelocity.normalized * maximumSpeed;
    }

    private void PullServerObjects(float strength, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
        var handled = new HashSet<Rigidbody>();
        foreach (Collider hit in hits)
        {
            Rigidbody otherBody = hit != null ? hit.attachedRigidbody : null;
            if (otherBody == null || otherBody == body || otherBody.isKinematic || !handled.Add(otherBody))
                continue;
            if (otherBody.GetComponentInParent<PlayerMovement>() != null)
                continue;
            if (otherBody.GetComponent<NetworkArenaCubePhysics>() == null &&
                otherBody.GetComponent<BoundaryHazard>() == null &&
                otherBody.GetComponent<NetworkProjectilePhysics>() == null)
                continue;

            Vector3 delta = transform.position - otherBody.position;
            float distance = Mathf.Max(0.5f, delta.magnitude);
            otherBody.AddForce(delta.normalized * strength * (1f - Mathf.Clamp01(distance / radius)), ForceMode.Acceleration);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        if (isServer && arenaMass.value &&
            (kind.value == BoundaryHazardKind.ArenaBlackHole || kind.value == BoundaryHazardKind.Cube))
        {
            BoundaryBreakawayPlatform platform = collision.collider.GetComponentInParent<BoundaryBreakawayPlatform>();
            if (platform != null &&
                (platform.PlatformIndex != lastPlatformIndex || Time.time - lastPlatformContactAt >= 0.45f))
            {
                lastPlatformIndex = platform.PlatformIndex;
                lastPlatformContactAt = Time.time;
                BoundaryMatchController.Instance?.ServerRegisterPlatformContact(platform.PlatformIndex);
            }
        }

        PlayerMovement movement = collision.collider.GetComponentInParent<PlayerMovement>();
        if (movement == null)
            return;

        if (isServer && (kind.value == BoundaryHazardKind.ArenaBlackHole ||
                         kind.value == BoundaryHazardKind.Cube))
        {
            movement.GetComponent<BoundaryPlayerState>()?.ServerRegisterBlackHoleContact(gameObject.GetInstanceID());
            return;
        }

        if (!movement.isOwner)
            return;

        if (BoundaryMath.IsLethalContactHazard(kind.value, arenaMass.value))
        {
            BoundaryPlayerState playerState = movement.GetComponent<BoundaryPlayerState>();
            if (playerState != null)
                playerState.ConsumeFromHazard("You were consumed by the black hole.");
            else
            {
                SfxManager.PlayLethalHit();
                GameManager.I?.ReportLocalPlayerLost("You were consumed by the black hole.");
            }
            return;
        }

        Vector3 direction = movement.transform.position - transform.position;
        direction.y = Mathf.Max(0.45f, direction.y);
        float impact = kind.value == BoundaryHazardKind.Meteor
            ? meteorImpact * BoundaryMath.DisasterPower(BoundaryDisaster.MeteorBreak)
            : cubeImpact;
        if (kind.value == BoundaryHazardKind.OrbitalDebris || kind.value == BoundaryHazardKind.TornadoDebris)
            impact *= 1.2f * (kind.value == BoundaryHazardKind.OrbitalDebris
                ? BoundaryMath.DisasterPower(BoundaryDisaster.OrbitalStrike)
                : 1f);
        movement.ApplyBoundaryImpulse(direction.normalized * impact);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!isServer || collision == null ||
            (kind.value != BoundaryHazardKind.ArenaBlackHole && kind.value != BoundaryHazardKind.Cube))
            return;

        BoundaryPlayerState state = collision.collider.GetComponentInParent<BoundaryPlayerState>();
        if (state != null)
            state.ServerRegisterBlackHoleContact(gameObject.GetInstanceID());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer || !IsRealSingularity || other == null)
            return;

        NetworkArenaCubePhysics cube = other.GetComponentInParent<NetworkArenaCubePhysics>();
        if (cube == null)
            return;

        float nextScale = Mathf.Min(2.25f, transform.localScale.x + 0.12f);
        transform.localScale = Vector3.one * nextScale;
    }

    public static void ApplyLocalFields(PlayerMovement movement)
    {
        if (movement == null || !movement.isOwner || movement.rb == null)
            return;

        for (int i = ActiveHazards.Count - 1; i >= 0; i--)
        {
            BoundaryHazard hazard = ActiveHazards[i];
            if (hazard == null)
            {
                ActiveHazards.RemoveAt(i);
                continue;
            }

            if (!IsSingularityVisual(hazard.kind.value))
                continue;

            Vector3 delta = hazard.transform.position - movement.rb.position;
            bool arenaBlackHole = hazard.kind.value == BoundaryHazardKind.ArenaBlackHole;
            float baseRadius = arenaBlackHole ? 5.5f : hazard.IsRealSingularity ? 8f : 4f;
            float radius = baseRadius * Mathf.Max(1f, hazard.transform.localScale.x);
            float distance = delta.magnitude;
            if (distance >= radius || distance < 0.05f)
                continue;

            float strength = arenaBlackHole ? 6f : hazard.IsRealSingularity
                ? 22f * BoundaryMath.DisasterPower(BoundaryDisaster.BlackRain)
                : 3f;
            float falloff = 1f - Mathf.Clamp01(distance / radius);
            movement.rb.AddForce(delta.normalized * strength * falloff, ForceMode.Acceleration);
        }
    }

    private static bool IsSingularityVisual(BoundaryHazardKind value)
    {
        return value == BoundaryHazardKind.BlackRainSingularity ||
               value == BoundaryHazardKind.FalseSingularity ||
               value == BoundaryHazardKind.ArenaBlackHole;
    }

    private static bool IsKinematicSingularity(BoundaryHazardKind value)
    {
        return value == BoundaryHazardKind.BlackRainSingularity ||
               value == BoundaryHazardKind.FalseSingularity;
    }

    private void ConfigureOrbitLane()
    {
        BoundaryMatchController match = BoundaryMatchController.Instance;
        Vector3 center = match != null ? match.ArenaCenter : serverTarget;
        Vector3 flat = transform.position - center;
        flat.y = 0f;
        desiredOrbitRadius = Mathf.Max(2f, flat.magnitude);
        desiredOrbitHeight = Mathf.Max(1f, transform.position.y - center.y);
    }

    private void OnKindChanged(BoundaryHazardKind _)
    {
        visualApplied = false;
        ApplyVisual();
    }

    private void OnNetworkScaleChanged(float scale)
    {
        if (scale > 0.01f)
            transform.localScale = Vector3.one * scale;
    }

    private void ApplyVisual()
    {
        if (boxCollider == null || sphereCollider == null)
            return;

        bool sphere = IsSingularityVisual(kind.value);
        boxCollider.enabled = !sphere;
        sphereCollider.enabled = sphere;
        sphereCollider.isTrigger = kind.value != BoundaryHazardKind.ArenaBlackHole;

        // Collision shape selection is required on headless servers even when
        // all visual components and shaders have been stripped.
        if (!buildVisuals || cubeRenderer == null || sphereRenderer == null)
        {
            visualApplied = true;
            return;
        }

        cubeRenderer.gameObject.SetActive(!sphere);
        bool tesseractCube = kind.value == BoundaryHazardKind.Cube;
        cubeRenderer.enabled = !tesseractCube;
        if (tesseractRig != null)
            tesseractRig.gameObject.SetActive(tesseractCube);
        sphereRenderer.gameObject.SetActive(sphere);
        if (blackHoleRig != null)
            blackHoleRig.gameObject.SetActive(sphere);

        if (cubeMaterial == null)
            cubeMaterial = CreateMaterial(new Color(0.012f, 0.002f, 0.022f), new Color(0.22f, 0.015f, 0.32f), 1.35f);
        if (sphereMaterial == null)
            sphereMaterial = CreateMaterial(Color.black, Color.black, 0f);

        cubeRenderer.sharedMaterial = cubeMaterial;
        sphereRenderer.sharedMaterial = sphereMaterial;
        SetTesseractEmission(TesseractBlue, 18f);

        switch (kind.value)
        {
            case BoundaryHazardKind.Meteor:
                SetEmission(new Color(1f, 0.18f, 0.04f), 4f);
                break;
            case BoundaryHazardKind.OrbitalDebris:
                SetEmission(new Color(0.2f, 0.7f, 1f), 3f);
                break;
            case BoundaryHazardKind.TornadoDebris:
                SetEmission(new Color(0.75f, 0.16f, 1f), 4f);
                break;
            case BoundaryHazardKind.FalseSingularity:
                SetEmission(new Color(0.35f, 0.08f, 0.9f), 4.5f);
                break;
            case BoundaryHazardKind.ArenaBlackHole:
                SetArenaBlackHolePalette();
                break;
        }

        ApplyArenaMassGlow();

        visualApplied = true;
    }

    private void ApplyArenaMassGlow()
    {
        if (!buildVisuals || !arenaMass.value || !ShouldGlowDuringHollow(variant.value))
            return;

        if (kind.value == BoundaryHazardKind.Cube)
        {
            Color glowColor = CubeGlowColor(hollowGlowActive, darknessGlowActive);
            float glowIntensity = CubeGlowIntensity(hollowGlowActive, darknessGlowActive);
            SetEmission(
                glowColor, glowIntensity);
            SetTesseractEmission(glowColor, glowIntensity);
        }
        else if (kind.value == BoundaryHazardKind.ArenaBlackHole)
        {
            if (!hollowGlowActive && !darknessGlowActive)
            {
                SetArenaBlackHolePalette();
                return;
            }

            SetMaterialColor(sphereMaterial, Color.black, Color.black);
            for (int index = 0; index < blackHoleMaterials.Count; index++)
            {
                Color glow = darknessGlowActive
                    ? index % 2 == 0 ? new Color(4f, 15f, 24f) : new Color(16f, 5f, 27f)
                    : index % 2 == 0 ? new Color(13.5f, 2.7f, 22.5f) : new Color(3.75f, 10.5f, 24f);
                SetMaterialColor(blackHoleMaterials[index], Color.black, glow);
            }
        }
    }

    private void SetArenaBlackHolePalette()
    {
        SetMaterialColor(sphereMaterial, Color.black, Color.black);
        foreach (Material material in blackHoleMaterials)
            SetMaterialColor(material, Color.black, Color.black);
    }

    private static void SetMaterialColor(Material material, Color baseColor, Color emission)
    {
        if (material == null)
            return;
        material.color = baseColor;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", emission);
    }

    private void SetEmission(Color color, float intensity)
    {
        Material target = IsSingularityVisual(kind.value) ? sphereMaterial : cubeMaterial;
        if (target == null)
            return;
        target.EnableKeyword("_EMISSION");
        target.SetColor("_EmissionColor", color * intensity);
    }

    private void SetTesseractEmission(Color color, float intensity)
    {
        tesseractGlowColor = color;
        tesseractGlowIntensity = intensity;
        ApplyTesseractEmission(1f);
    }

    private void ApplyTesseractEmission(float pulse)
    {
        if (tesseractMaterial != null)
        {
            tesseractMaterial.EnableKeyword("_EMISSION");
            tesseractMaterial.SetColor("_EmissionColor",
                tesseractGlowColor * tesseractGlowIntensity * pulse);
        }
        if (tesseractCoreMaterial != null)
        {
            tesseractCoreMaterial.EnableKeyword("_EMISSION");
            tesseractCoreMaterial.SetColor("_EmissionColor",
                tesseractGlowColor * tesseractGlowIntensity * pulse * 0.34f);
        }
    }

    public static Color CubeGlowColor(bool hollowActive, bool darknessActive)
    {
        if (darknessActive)
            return new Color(0.12f, 0.72f, 1f);
        return hollowActive ? new Color(0.035f, 0.55f, 1f) : TesseractBlue;
    }

    public static float CubeGlowIntensity(bool hollowActive, bool darknessActive)
    {
        return darknessActive ? 32f : hollowActive ? 30f : 18f;
    }

    private static Material CreateMaterial(Color baseColor, Color emission, float intensity)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null)
            return null;
        Material material = new Material(shader);
        material.color = baseColor;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission * intensity);
        return material;
    }

    private void SetCollisionEnabled(bool enabled)
    {
        if (boxCollider != null)
            boxCollider.enabled = enabled && !IsSingularityVisual(kind.value);
        if (sphereCollider != null)
            sphereCollider.enabled = enabled && IsSingularityVisual(kind.value);
    }

    private static Vector3 QuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }
}
