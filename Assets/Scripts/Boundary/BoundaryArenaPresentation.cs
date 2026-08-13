using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoundaryArenaPresentation : MonoBehaviour
{
    private sealed class PlatformTile
    {
        public Transform transform;
        public Renderer renderer;
        public Collider collider;
        public Vector3 startPosition;
        public Quaternion startRotation;
        public Vector3 startScale;
        public int collapseBand;
        public float stagger;
        public bool animated;
    }

    private sealed class AccretionRing
    {
        public Transform transform;
        public Vector3 axis;
        public float speed;
    }

    [Header("Breakaway platform arena")]
    [SerializeField, Range(7f, 14f)] private float platformSize = 9.2f;
    [SerializeField, Range(0.1f, 1.5f)] private float platformGap = 0.45f;
    [SerializeField, Range(0.35f, 1.5f)] private float platformThickness = 0.85f;
    [SerializeField, Range(0.5f, 3f)] private float collapseWarningSeconds = 1.35f;

    [Header("Vertical combat routes")]
    [SerializeField, Range(4, 12)] private int routeCountPerTier = 8;
    [SerializeField, Range(3f, 8f)] private float routePlatformSize = 5.2f;

    private BoundaryMatchController match;
    private Transform generatedRoot;
    private readonly List<PlatformTile> platforms = new List<PlatformTile>();
    private readonly List<Renderer> fractureLines = new List<Renderer>();
    private readonly List<LineRenderer> vortexLines = new List<LineRenderer>();
    private readonly List<AccretionRing> accretionRings = new List<AccretionRing>();
    private readonly List<Material> generatedMaterials = new List<Material>();
    private readonly List<GameObject> disabledLegacyArena = new List<GameObject>();
    private readonly HashSet<PlayerMovement> trailedPlayers = new HashSet<PlayerMovement>();
    private MaterialPropertyBlock platformProperties;
    private Transform singularityCore;
    private LineRenderer horizonRing;
    private Material platformMaterial;
    private Material fractureMaterial;
    private Material vortexMaterial;
    private Material coreMaterial;
    private Color originalFogColor;
    private float originalFogDensity;
    private bool originalFogEnabled;
    private bool built;

    public int GeneratedPlatformCount => platforms.Count;
    public bool LegacyArenaHidden => disabledLegacyArena.Count > 0;
    public bool HasSideWalls => false;

    private void Start()
    {
        match = GetComponent<BoundaryMatchController>();
        if (match == null)
            match = BoundaryMatchController.Instance;

        originalFogEnabled = RenderSettings.fog;
        originalFogColor = RenderSettings.fogColor;
        originalFogDensity = RenderSettings.fogDensity;
        platformProperties = new MaterialPropertyBlock();
        BuildArena();
    }

    private void OnDestroy()
    {
        RenderSettings.fog = originalFogEnabled;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogDensity = originalFogDensity;

        foreach (GameObject legacyRoot in disabledLegacyArena)
        {
            if (legacyRoot != null)
                legacyRoot.SetActive(true);
        }

        foreach (Material material in generatedMaterials)
        {
            if (material != null)
                Destroy(material);
        }
    }

    private void Update()
    {
        if (!built || match == null)
            return;

        UpdatePlatformCollapses();
        UpdateSingularity();
        UpdateFractures();
        UpdateFog();
        UpdatePlayerTrails();
    }

    private void BuildArena()
    {
        if (match == null || built)
            return;

        DisableLegacyArena();

        generatedRoot = new GameObject("Boundary Generated Stadium").transform;
        generatedRoot.SetParent(transform, false);
        generatedRoot.position = Vector3.zero;

        platformMaterial = CreateMaterial(
            new Color(0.36f, 0.38f, 0.42f),
            new Color(0.055f, 0.065f, 0.09f), 0.55f);
        fractureMaterial = CreateMaterial(
            new Color(0.04f, 0.01f, 0.06f),
            new Color(1f, 0.08f, 0.65f), 4.5f);
        vortexMaterial = CreateMaterial(
            new Color(0.05f, 0.01f, 0.08f),
            new Color(0.45f, 0.25f, 1f), 3.8f);
        coreMaterial = CreateMaterial(Color.black, new Color(0.32f, 0.02f, 0.72f), 6f);

        BuildPlatformFloor();
        BuildVerticalRoutes();
        AlignSpawnsAndExistingPlayers();
        BuildSingularity();
        BuildFractureLines();
        BuildVortexLines();
        built = true;
    }

    private void DisableLegacyArena()
    {
        DisableLegacyObject("Wall");
        DisableLegacyObject("Plane");

        BlackKill[] legacySingularities = FindObjectsByType<BlackKill>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (BlackKill singularity in legacySingularities)
        {
            if (singularity == null || singularity.gameObject.scene != gameObject.scene)
                continue;
            disabledLegacyArena.Add(singularity.gameObject);
            singularity.gameObject.SetActive(false);
        }
    }

    private void DisableLegacyObject(string objectName)
    {
        GameObject legacy = GameObject.Find(objectName);
        if (legacy == null || legacy.scene != gameObject.scene)
            return;

        disabledLegacyArena.Add(legacy);
        legacy.SetActive(false);
    }

    private void BuildPlatformFloor()
    {
        Transform parent = new GameObject("Breakaway Platforms").transform;
        parent.SetParent(generatedRoot, false);

        float spacing = platformSize + platformGap;
        int extent = Mathf.CeilToInt(match.OuterRadius / spacing);
        int index = 0;
        for (int z = -extent; z <= extent; z++)
        {
            float rowOffset = (z & 1) == 0 ? 0f : spacing * 0.5f;
            for (int x = -extent; x <= extent; x++)
            {
                Vector2 flat = new Vector2(x * spacing + rowOffset, z * spacing);
                float distance = flat.magnitude;
                if (distance > match.OuterRadius - platformSize * 0.32f)
                    continue;

                int band = CollapseBand(distance);
                float surfaceY = SurfaceYForBand(band);
                Vector3 position = new Vector3(
                    match.ArenaCenter.x + flat.x,
                    surfaceY - platformThickness * 0.5f,
                    match.ArenaCenter.z + flat.y);
                CreatePlatform(
                    parent,
                    $"Floor Platform {index:000}",
                    position,
                    new Vector3(platformSize, platformThickness, platformSize),
                    Quaternion.identity,
                    band,
                    index++);
            }
        }
    }

    private void BuildVerticalRoutes()
    {
        Transform parent = new GameObject("Vertical Combat Routes").transform;
        parent.SetParent(generatedRoot, false);

        BuildRouteBand(parent, 2, match.MiddleRadius + 9f, match.OuterRadius - 12f,
            match.OuterPlatformSurfaceY, 1000);
        BuildRouteBand(parent, 1, match.InnerRadius + 7f, match.MiddleRadius - 9f,
            match.MiddlePlatformSurfaceY, 2000);
        BuildRouteBand(parent, 0, 10f, match.InnerRadius - 7f,
            match.InnerPlatformSurfaceY, 3000);
    }

    private void BuildRouteBand(
        Transform parent,
        int collapseBand,
        float innerDistance,
        float outerDistance,
        float baseSurfaceY,
        int indexOffset)
    {
        if (outerDistance <= innerDistance)
            return;

        for (int route = 0; route < routeCountPerTier; route++)
        {
            float angle = Mathf.PI * 2f * route / routeCountPerTier + collapseBand * 0.17f;
            for (int step = 0; step < 2; step++)
            {
                float lane = (route + step) % 3 / 2f;
                float radius = Mathf.Lerp(innerDistance, outerDistance, 0.28f + lane * 0.44f);
                float stepAngle = angle + (step == 0 ? -0.035f : 0.055f);
                float surfaceY = baseSurfaceY + 1.75f + step * 1.65f;
                Vector3 position = new Vector3(
                    match.ArenaCenter.x + Mathf.Cos(stepAngle) * radius,
                    surfaceY - platformThickness * 0.5f,
                    match.ArenaCenter.z + Mathf.Sin(stepAngle) * radius);
                Quaternion rotation = Quaternion.Euler(0f, -stepAngle * Mathf.Rad2Deg, 0f);
                CreatePlatform(
                    parent,
                    $"Jump Route {collapseBand}-{route:00}-{step}",
                    position,
                    new Vector3(routePlatformSize, platformThickness, routePlatformSize * 1.35f),
                    rotation,
                    collapseBand,
                    indexOffset + route * 2 + step);
            }
        }
    }

    private void CreatePlatform(
        Transform parent,
        string platformName,
        Vector3 position,
        Vector3 scale,
        Quaternion rotation,
        int band,
        int stableIndex)
    {
        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.name = platformName;
        tile.layer = 3;
        tile.transform.SetParent(parent, false);
        tile.transform.position = position;
        tile.transform.rotation = rotation;
        tile.transform.localScale = scale;

        Renderer renderer = tile.GetComponent<Renderer>();
        renderer.sharedMaterial = platformMaterial;
        Collider collider = tile.GetComponent<Collider>();
        collider.material = null;

        int hash = BoundaryMath.StableHash(74191 + band * 193, stableIndex);
        platforms.Add(new PlatformTile
        {
            transform = tile.transform,
            renderer = renderer,
            collider = collider,
            startPosition = position,
            startRotation = rotation,
            startScale = scale,
            collapseBand = band,
            stagger = (hash % 1000) / 999f
        });
    }

    private int CollapseBand(float radius)
    {
        if (radius > match.MiddleRadius)
            return 2;
        if (radius > match.InnerRadius)
            return 1;
        return 0;
    }

    private float SurfaceYForBand(int band)
    {
        if (band == 2)
            return match.OuterPlatformSurfaceY;
        if (band == 1)
            return match.MiddlePlatformSurfaceY;
        return match.InnerPlatformSurfaceY;
    }

    private float SurfaceYAtRadius(float radius)
    {
        return SurfaceYForBand(CollapseBand(radius));
    }

    private void AlignSpawnsAndExistingPlayers()
    {
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        foreach (GameObject spawnPoint in spawnPoints)
        {
            Vector3 flat = spawnPoint.transform.position - match.ArenaCenter;
            flat.y = 0f;
            Vector3 position = spawnPoint.transform.position;
            position.y = SurfaceYAtRadius(flat.magnitude) + 1.15f;
            spawnPoint.transform.position = position;
        }

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (PlayerMovement player in players)
        {
            if (player == null || !player.isOwner || player.rb == null)
                continue;

            Vector3 flat = player.transform.position - match.ArenaCenter;
            flat.y = 0f;
            float safeY = SurfaceYAtRadius(flat.magnitude) + 1.15f;
            if (player.rb.position.y >= safeY)
                continue;

            Vector3 safePosition = player.rb.position;
            safePosition.y = safeY;
            player.rb.position = safePosition;
            Vector3 velocity = player.rb.linearVelocity;
            player.rb.linearVelocity = new Vector3(velocity.x, Mathf.Max(0f, velocity.y), velocity.z);
        }
    }

    private void UpdatePlatformCollapses()
    {
        bool outerRemoved = match.Phase == BoundaryPhase.MiddleRing || match.Phase == BoundaryPhase.InnerRing;
        bool middleRemoved = match.Phase == BoundaryPhase.InnerRing;
        float totalTransition = match.TransitionElapsed + match.TransitionRemaining;
        float transitionProgress = totalTransition > 0.01f
            ? Mathf.Clamp01(match.TransitionElapsed / totalTransition)
            : 0f;

        for (int i = 0; i < platforms.Count; i++)
        {
            PlatformTile tile = platforms[i];
            bool removed = tile.collapseBand == 2 ? outerRemoved : tile.collapseBand == 1 && middleRemoved;
            bool transitioning = (tile.collapseBand == 2 && match.Transition == BoundaryTransition.ClosingOuterRing) ||
                                 (tile.collapseBand == 1 && match.Transition == BoundaryTransition.ClosingMiddleRing);

            if (removed)
            {
                if (tile.transform.gameObject.activeSelf)
                    tile.transform.gameObject.SetActive(false);
                continue;
            }

            if (!tile.transform.gameObject.activeSelf)
                tile.transform.gameObject.SetActive(true);
            if (!transitioning)
            {
                ResetPlatform(tile);
                continue;
            }

            UpdateCollapsingPlatform(tile, transitionProgress, i);
        }
    }

    private void ResetPlatform(PlatformTile tile)
    {
        if (!tile.animated)
            return;

        tile.transform.position = tile.startPosition;
        tile.transform.rotation = tile.startRotation;
        tile.transform.localScale = tile.startScale;
        tile.collider.enabled = true;
        SetPlatformColor(tile.renderer, new Color(0.36f, 0.38f, 0.42f));
        tile.animated = false;
    }

    private void UpdateCollapsingPlatform(PlatformTile tile, float progress, int index)
    {
        float warningFraction = Mathf.Clamp01(collapseWarningSeconds /
            Mathf.Max(0.1f, match.TransitionElapsed + match.TransitionRemaining));
        float warningStart = tile.stagger * 0.28f;
        float flightStart = Mathf.Min(0.58f, warningStart + warningFraction);

        if (progress < warningStart)
        {
            ResetPlatform(tile);
            return;
        }

        if (progress < flightStart)
        {
            tile.animated = true;
            tile.collider.enabled = true;
            float warning01 = Mathf.InverseLerp(warningStart, flightStart, progress);
            float pulse = 0.5f + 0.5f * Mathf.Sin(warning01 * Mathf.PI * 8f + tile.stagger * 5f);
            Color color = Color.Lerp(new Color(0.35f, 0.37f, 0.40f), new Color(0.065f, 0.07f, 0.085f), pulse);
            SetPlatformColor(tile.renderer, color);
            tile.transform.localScale = new Vector3(
                tile.startScale.x,
                tile.startScale.y * Mathf.Lerp(1f, 0.72f, pulse * 0.25f),
                tile.startScale.z);
            return;
        }

        tile.animated = true;
        tile.collider.enabled = false;
        SetPlatformColor(tile.renderer, new Color(0.035f, 0.038f, 0.05f));
        float flight = BoundaryMath.EaseInOut(Mathf.InverseLerp(flightStart, 1f, progress));
        Vector3 target = match.SingularityPosition;
        Vector3 control = Vector3.Lerp(tile.startPosition, target, 0.48f);
        control += Vector3.up * (7f + tile.stagger * 9f);
        control += Vector3.Cross(Vector3.up, target - tile.startPosition).normalized *
                   Mathf.Lerp(-8f, 8f, tile.stagger);
        tile.transform.position = QuadraticBezier(tile.startPosition, control, target, flight);
        tile.transform.rotation = tile.startRotation * Quaternion.Euler(
            flight * (210f + index % 5 * 31f),
            flight * (330f + index % 7 * 23f),
            flight * 170f);
        tile.transform.localScale = Vector3.Lerp(tile.startScale, tile.startScale * 0.08f, flight);
    }

    private void SetPlatformColor(Renderer renderer, Color color)
    {
        renderer.GetPropertyBlock(platformProperties);
        platformProperties.SetColor("_BaseColor", color);
        platformProperties.SetColor("_Color", color);
        renderer.SetPropertyBlock(platformProperties);
    }

    private static Vector3 QuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }

#if UNITY_EDITOR
    public int PreviewCollapseWarningForValidation()
    {
        int pulsing = 0;
        for (int i = 0; i < platforms.Count; i++)
        {
            PlatformTile tile = platforms[i];
            if (tile.collapseBand != 2)
                continue;
            UpdateCollapsingPlatform(tile, 0.20f, i);
            tile.renderer.GetPropertyBlock(platformProperties);
            if (tile.collider.enabled && platformProperties.GetColor("_BaseColor").r < 0.30f)
                pulsing++;
        }
        foreach (PlatformTile tile in platforms)
            ResetPlatform(tile);
        return pulsing;
    }

    public int PreviewCollapseFlightForValidation()
    {
        int flying = 0;
        for (int i = 0; i < platforms.Count; i++)
        {
            PlatformTile tile = platforms[i];
            if (tile.collapseBand != 2)
                continue;
            UpdateCollapsingPlatform(tile, 0.90f, i);
            if (!tile.collider.enabled && Vector3.Distance(tile.transform.position, tile.startPosition) > 3f)
                flying++;
        }
        foreach (PlatformTile tile in platforms)
            ResetPlatform(tile);
        return flying;
    }
#endif

    private void BuildSingularity()
    {
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Boundary Singularity Core";
        core.transform.SetParent(generatedRoot, false);
        core.transform.position = match.SingularityPosition;
        core.transform.localScale = Vector3.one * 3.7f;
        core.GetComponent<Renderer>().sharedMaterial = coreMaterial;
        Destroy(core.GetComponent<Collider>());
        singularityCore = core.transform;

        CreateAccretionRing(core.transform, "Hot Accretion Band", 2.35f, 0.24f,
            new Color(1f, 0.24f, 0.045f), new Vector3(0.16f, 1f, 0.06f), 46f);
        CreateAccretionRing(core.transform, "Violet Lensing Band", 2.8f, 0.13f,
            new Color(0.72f, 0.10f, 1f), new Vector3(-0.08f, 1f, 0.18f), -33f);
        CreateAccretionRing(core.transform, "Blue Photon Band", 3.2f, 0.075f,
            new Color(0.12f, 0.72f, 1f), new Vector3(0.24f, 1f, -0.12f), 25f);

        GameObject lightObject = new GameObject("Singularity Rim Light", typeof(Light));
        lightObject.transform.SetParent(core.transform, false);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.62f, 0.12f, 1f);
        light.intensity = 6.5f;
        light.range = 34f;

        horizonRing = CreateCircleLine("Event Horizon", 96, coreMaterial, 0.18f);
        horizonRing.transform.position = new Vector3(
            match.SingularityPosition.x,
            match.SingularityPosition.y - 5.5f,
            match.SingularityPosition.z);
    }

    private void CreateAccretionRing(
        Transform parent,
        string name,
        float radius,
        float width,
        Color color,
        Vector3 axis,
        float speed)
    {
        Material material = CreateMaterial(Color.black, color, 7f);
        LineRenderer line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
        line.transform.SetParent(parent, false);
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 96;
        line.startWidth = width;
        line.endWidth = width * 0.55f;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = Mathf.PI * 2f * i / line.positionCount;
            float turbulence = 1f + Mathf.Sin(angle * 5f) * 0.055f;
            line.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius * turbulence,
                Mathf.Sin(angle * 3f) * 0.075f,
                Mathf.Sin(angle) * radius * turbulence));
        }

        line.transform.localRotation = Quaternion.Euler(18f + accretionRings.Count * 8f, 0f, accretionRings.Count * 14f);
        accretionRings.Add(new AccretionRing
        {
            transform = line.transform,
            axis = axis.normalized,
            speed = speed
        });
    }

    private void BuildFractureLines()
    {
        Transform parent = new GameObject("Fracture Lines").transform;
        parent.SetParent(generatedRoot, false);
        for (int i = 0; i < 8; i++)
        {
            float angle = Mathf.PI * 2f * i / 8f;
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = $"Fracture {i + 1}";
            line.transform.SetParent(parent, false);
            line.transform.position = match.ArenaCenter +
                                      new Vector3(Mathf.Cos(angle) * 18f, match.InnerPlatformSurfaceY + 0.08f - match.ArenaFloorY,
                                          Mathf.Sin(angle) * 18f);
            line.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            line.transform.localScale = new Vector3(34f, 0.05f, 0.18f);
            Renderer renderer = line.GetComponent<Renderer>();
            renderer.sharedMaterial = fractureMaterial;
            renderer.enabled = false;
            fractureLines.Add(renderer);
            Destroy(line.GetComponent<Collider>());
        }
    }

    private void BuildVortexLines()
    {
        Transform parent = new GameObject("Vortex Currents").transform;
        parent.SetParent(generatedRoot, false);
        for (int lane = 0; lane < 3; lane++)
        {
            LineRenderer line = new GameObject($"Vortex Lane {lane + 1}", typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.sharedMaterial = vortexMaterial;
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 80;
            line.startWidth = 0.08f + lane * 0.025f;
            line.endWidth = 0.015f;
            line.enabled = false;
            vortexLines.Add(line);
        }
    }

    private void UpdateSingularity()
    {
        if (singularityCore == null)
            return;

        float intensity = match.Phase == BoundaryPhase.InnerRing
            ? 1.18f + Mathf.Clamp01(match.PhaseElapsed / 55f) * 0.25f
            : match.Phase == BoundaryPhase.MiddleRing ? 1.10f : 1f;
        float pulse = 1f + Mathf.Sin(Time.time * (2.2f + intensity)) * 0.035f;
        singularityCore.localScale = Vector3.one * (3.7f * intensity * pulse);

        foreach (AccretionRing ring in accretionRings)
            ring.transform.Rotate(ring.axis, ring.speed * Time.deltaTime, Space.Self);

        if (horizonRing != null)
        {
            float horizonRadius = 9.5f + Mathf.Sin(Time.time * 2.7f) * 0.25f;
            SetCirclePoints(horizonRing, horizonRadius, match.SingularityPosition.y - 5.5f);
        }

        bool vortexVisible = match.Phase == BoundaryPhase.InnerRing;
        for (int lane = 0; lane < vortexLines.Count; lane++)
        {
            LineRenderer line = vortexLines[lane];
            line.enabled = vortexVisible;
            if (!vortexVisible)
                continue;

            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (float)(line.positionCount - 1);
                float turns = 3.2f + lane * 0.55f;
                float angle = t * turns * Mathf.PI * 2f + Time.time * (0.8f + lane * 0.2f);
                float radius = Mathf.Lerp(match.RingRadius * (0.88f - lane * 0.12f), 1.4f, t);
                float y = Mathf.Lerp(match.InnerPlatformSurfaceY + 0.7f + lane * 2.6f,
                    match.SingularityPosition.y, t);
                line.SetPosition(i, new Vector3(
                    match.ArenaCenter.x + Mathf.Cos(angle) * radius,
                    y,
                    match.ArenaCenter.z + Mathf.Sin(angle) * radius));
            }
        }
    }

    private void UpdateFractures()
    {
        bool active = match.IsDisasterActive && match.Disaster == BoundaryDisaster.FractureLines;
        float pulse = match.FracturePulse;
        for (int i = 0; i < fractureLines.Count; i++)
        {
            Renderer renderer = fractureLines[i];
            renderer.enabled = active;
            if (!active)
                continue;
            float sequence = Mathf.Repeat(match.DisasterElapsed - i * 0.18f, 3.4f);
            float lift = sequence > 1.2f && sequence < 2.1f ? pulse : 0f;
            Vector3 position = renderer.transform.position;
            position.y = match.InnerPlatformSurfaceY + 0.08f + lift * 0.55f;
            renderer.transform.position = position;
            renderer.transform.localScale = new Vector3(34f, 0.05f + lift * 0.22f, 0.18f + lift * 0.3f);
        }
    }

    private void UpdateFog()
    {
        float fog = match.FogAmount;
        if (fog <= 0.001f)
        {
            RenderSettings.fog = originalFogEnabled;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogDensity = originalFogDensity;
            return;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.Lerp(originalFogColor, new Color(0.015f, 0.006f, 0.025f), fog);
        RenderSettings.fogDensity = Mathf.Lerp(Mathf.Max(0.002f, originalFogDensity), 0.039f, fog);
    }

    private void UpdatePlayerTrails()
    {
        if (match.Phase != BoundaryPhase.InnerRing)
            return;

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (PlayerMovement player in players)
        {
            if (player == null || !trailedPlayers.Add(player))
                continue;

            TrailRenderer trail = player.GetComponent<TrailRenderer>();
            if (trail == null)
                trail = player.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.34f;
            trail.startWidth = 0.20f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.12f;
            trail.sharedMaterial = vortexMaterial;
            trail.startColor = new Color(0.75f, 0.24f, 1f, 0.72f);
            trail.endColor = new Color(0.18f, 0.55f, 1f, 0f);
        }
    }

    private LineRenderer CreateCircleLine(string name, int points, Material material, float width)
    {
        LineRenderer line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
        line.transform.SetParent(generatedRoot, false);
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = points;
        line.startWidth = width;
        line.endWidth = width;
        SetCirclePoints(line, 9.5f, match.SingularityPosition.y - 5.5f);
        return line;
    }

    private void SetCirclePoints(LineRenderer line, float radius, float y)
    {
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = Mathf.PI * 2f * i / line.positionCount;
            line.SetPosition(i, new Vector3(
                match.ArenaCenter.x + Mathf.Cos(angle) * radius,
                y,
                match.ArenaCenter.z + Mathf.Sin(angle) * radius));
        }
    }

    private Material CreateMaterial(Color color, Color emission, float intensity)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        Material material = new Material(shader);
        material.color = color;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission * intensity);
        generatedMaterials.Add(material);
        return material;
    }
}
