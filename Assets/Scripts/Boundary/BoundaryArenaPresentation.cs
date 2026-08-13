using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoundaryArenaPresentation : MonoBehaviour
{
    public static BoundaryArenaPresentation Instance { get; private set; }

    private sealed class PlatformTile
    {
        public int stableIndex;
        public Transform transform;
        public Renderer renderer;
        public Collider collider;
        public Vector3 startPosition;
        public Quaternion startRotation;
        public Vector3 startScale;
        public int collapseBand;
        public float stagger;
        public bool animated;
        public bool canCorrupt;
        public int corruptionHits;
        public float forcedCollapseAt = -1f;
    }

    private sealed class AccretionRing
    {
        public Transform transform;
        public Vector3 axis;
        public float speed;
    }

    [Header("Breakaway platform arena")]
    [SerializeField, Range(7f, 14f)] private float platformSize = 9.2f;
    [SerializeField, Range(0.1f, 1f)] private float platformSeamOverlap = 0.4f;
    [SerializeField, Range(0.35f, 1.5f)] private float platformThickness = 0.85f;
    [SerializeField, Range(0.5f, 3f)] private float collapseWarningSeconds = 1.35f;

    [Header("Tier transitions")]
    [SerializeField, Range(9f, 18f)] private float tierRampLength = 14f;
    [SerializeField, Range(0.1f, 1f)] private float tierRampArcOverlap = 0.45f;

    [Header("Wall-jump cover")]
    [SerializeField, Range(4, 12)] private int wallPairsPerTier = 7;
    [SerializeField, Range(6f, 14f)] private float wallLength = 10.5f;
    [SerializeField, Range(5f, 12f)] private float wallHeight = 7.5f;
    [SerializeField, Range(0.7f, 2f)] private float wallThickness = 1.15f;
    [SerializeField, Range(3f, 10f)] private float wallPairGap = 6.4f;
    [SerializeField, Range(0.5f, 3f)] private float wallGroundClearance = 1.15f;
    [SerializeField, Range(0f, 6f)] private float wallMaximumExtraHeight = 4.5f;

    private BoundaryMatchController match;
    private Transform generatedRoot;
    private readonly List<PlatformTile> platforms = new List<PlatformTile>();
    private readonly Dictionary<int, PlatformTile> breakawayPlatforms = new Dictionary<int, PlatformTile>();
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
    private int transitionRampCount;

    public int GeneratedPlatformCount => platforms.Count;
    public bool LegacyArenaHidden => disabledLegacyArena.Count > 0;
    public bool HasSideWalls => false;
    public int GeneratedTransitionRampCount => transitionRampCount;

    private void Awake()
    {
        Instance = this;
    }

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
        if (Instance == this)
            Instance = null;

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
        BuildTierTransitionRamps();
        BuildWallJumpStructures();
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

        // An aligned, slightly overlapping grid removes both the visible seams
        // and the collider-sized catches that previously stopped a slide.
        float spacing = BoundaryMath.DensePlatformSpacing(platformSize, platformSeamOverlap);
        int extent = Mathf.CeilToInt(match.OuterRadius / spacing);
        int index = 0;
        for (int z = -extent; z <= extent; z++)
        {
            for (int x = -extent; x <= extent; x++)
            {
                Vector2 flat = new Vector2(x * spacing, z * spacing);
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

    private void BuildTierTransitionRamps()
    {
        Transform parent = new GameObject("Tier Transition Ramps").transform;
        parent.SetParent(generatedRoot, false);
        transitionRampCount = 0;

        BuildTransitionRampRing(
            parent,
            match.MiddleRadius,
            match.MiddlePlatformSurfaceY,
            match.OuterPlatformSurfaceY,
            2,
            4000);
        BuildTransitionRampRing(
            parent,
            match.InnerRadius,
            match.InnerPlatformSurfaceY,
            match.MiddlePlatformSurfaceY,
            1,
            5000);
    }

    private void BuildTransitionRampRing(
        Transform parent,
        float boundaryRadius,
        float innerSurfaceY,
        float outerSurfaceY,
        int collapseBand,
        int indexOffset)
    {
        float outerEdgeRadius = boundaryRadius + tierRampLength * 0.5f;
        float targetArcWidth = BoundaryMath.DensePlatformSpacing(platformSize, platformSeamOverlap);
        int segmentCount = Mathf.Max(16, Mathf.CeilToInt(Mathf.PI * 2f * outerEdgeRadius / targetArcWidth));
        float arcWidth = Mathf.PI * 2f * outerEdgeRadius / segmentCount + tierRampArcOverlap;
        float slope = BoundaryMath.TierRampSlopeDegrees(innerSurfaceY - outerSurfaceY, tierRampLength);
        float centerY = (innerSurfaceY + outerSurfaceY) * 0.5f -
                        platformThickness * 0.5f * Mathf.Cos(slope * Mathf.Deg2Rad);

        for (int segment = 0; segment < segmentCount; segment++)
        {
            float angle = Mathf.PI * 2f * segment / segmentCount;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 position = match.ArenaCenter + radial * boundaryRadius;
            position.y = centerY;
            Quaternion rotation = Quaternion.LookRotation(radial, Vector3.up) *
                                  Quaternion.Euler(slope, 0f, 0f);
            CreatePlatform(
                parent,
                $"Tier Ramp {collapseBand}-{segment:00}",
                position,
                new Vector3(arcWidth, platformThickness, tierRampLength),
                rotation,
                collapseBand,
                indexOffset + segment);
            transitionRampCount++;
        }
    }

    private void BuildWallJumpStructures()
    {
        Transform parent = new GameObject("Wall Jump Structures").transform;
        parent.SetParent(generatedRoot, false);

        BuildWallBand(parent, 2, match.MiddleRadius + 12f, match.OuterRadius - 15f,
            match.OuterPlatformSurfaceY, 1000);
        BuildWallBand(parent, 1, match.InnerRadius + 10f, match.MiddleRadius - 11f,
            match.MiddlePlatformSurfaceY, 2000);
        BuildWallBand(parent, 0, 11f, match.InnerRadius - 9f,
            match.InnerPlatformSurfaceY, 3000);
    }

    private void BuildWallBand(
        Transform parent,
        int collapseBand,
        float innerDistance,
        float outerDistance,
        float baseSurfaceY,
        int indexOffset)
    {
        if (outerDistance <= innerDistance)
            return;

        for (int pair = 0; pair < wallPairsPerTier; pair++)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                int wallIndex = pair * 2 + (side > 0 ? 1 : 0);
                int seed = 81017 + collapseBand * 7919;
                float sectorJitter = Mathf.Lerp(-0.38f, 0.38f, BoundaryMath.StableUnit(seed, wallIndex));
                float angle = Mathf.PI * 2f * (pair + 0.5f) / wallPairsPerTier +
                              collapseBand * 0.19f + sectorJitter;
                float radius01 = Mathf.Lerp(0.12f, 0.88f, BoundaryMath.StableUnit(seed + 1, wallIndex));
                float radius = Mathf.Lerp(innerDistance, outerDistance, radius01);
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
                float pairOffset = wallPairGap * Mathf.Lerp(0.35f, 0.72f,
                    BoundaryMath.StableUnit(seed + 2, wallIndex));
                Vector3 position = match.ArenaCenter + radial * radius + tangent * pairOffset * side;

                float length = wallLength * Mathf.Lerp(0.92f, 1.32f,
                    BoundaryMath.StableUnit(seed + 3, wallIndex));
                float height = wallHeight * Mathf.Lerp(0.92f, 1.38f,
                    BoundaryMath.StableUnit(seed + 4, wallIndex));
                float thickness = wallThickness * Mathf.Lerp(0.92f, 1.28f,
                    BoundaryMath.StableUnit(seed + 5, wallIndex));
                float extraHeight = wallMaximumExtraHeight *
                                    Mathf.Pow(BoundaryMath.StableUnit(seed + 6, wallIndex), 1.45f);
                position.y = baseSurfaceY + wallGroundClearance + extraHeight + height * 0.5f;
                float yawJitter = Mathf.Lerp(-42f, 42f, BoundaryMath.StableUnit(seed + 7, wallIndex));
                Quaternion rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + yawJitter, 0f);
                CreatePlatform(
                    parent,
                    $"Wall Pair {collapseBand}-{pair:00}-{(side < 0 ? "L" : "R")}",
                    position,
                    new Vector3(length, height, thickness),
                    rotation,
                    collapseBand,
                    indexOffset + wallIndex,
                    true);
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
        int stableIndex,
        bool isWall = false)
    {
        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.name = platformName;
        tile.layer = 3;
        tile.transform.SetParent(parent, false);
        tile.transform.position = position;
        tile.transform.rotation = rotation;
        tile.transform.localScale = scale;
        if (isWall)
            tile.tag = "Wall";

        Renderer renderer = tile.GetComponent<Renderer>();
        renderer.sharedMaterial = platformMaterial;
        Collider collider = tile.GetComponent<Collider>();
        collider.material = null;

        int hash = BoundaryMath.StableHash(74191 + band * 193, stableIndex);
        PlatformTile platform = new PlatformTile
        {
            stableIndex = stableIndex,
            transform = tile.transform,
            renderer = renderer,
            collider = collider,
            startPosition = position,
            startRotation = rotation,
            startScale = scale,
            collapseBand = band,
            stagger = (hash % 1000) / 999f,
            canCorrupt = !isWall
        };
        platforms.Add(platform);

        if (!isWall)
        {
            BoundaryBreakawayPlatform contact = tile.AddComponent<BoundaryBreakawayPlatform>();
            contact.PlatformIndex = stableIndex;
            breakawayPlatforms[stableIndex] = platform;
        }
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
            if (tile.forcedCollapseAt >= 0f)
            {
                UpdateForcedCollapse(tile, i);
                continue;
            }
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
        SetPlatformColor(tile.renderer, StablePlatformColor(tile));
        tile.animated = false;
    }

    private void UpdateCollapsingPlatform(PlatformTile tile, float progress, int index)
    {
        float warningFraction = Mathf.Clamp01(collapseWarningSeconds /
            Mathf.Max(0.1f, match.TransitionElapsed + match.TransitionRemaining));
        bool earlyCohort = tile.stagger < 0.28f;
        float warningStart = earlyCohort ? 0f : 0.10f + tile.stagger * 0.32f;
        float flightStart = earlyCohort
            ? 0.16f + tile.stagger * 0.18f
            : Mathf.Min(0.68f, warningStart + warningFraction);

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

    private void UpdateForcedCollapse(PlatformTile tile, int index)
    {
        float elapsed = Time.time - tile.forcedCollapseAt;
        if (elapsed >= 2.15f)
        {
            tile.transform.gameObject.SetActive(false);
            return;
        }

        tile.animated = true;
        if (elapsed < 0.52f)
        {
            tile.collider.enabled = true;
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 28f);
            SetPlatformColor(tile.renderer, Color.Lerp(
                new Color(0.045f, 0.048f, 0.058f),
                new Color(0.16f, 0.17f, 0.19f), pulse));
            tile.transform.localScale = new Vector3(
                tile.startScale.x,
                tile.startScale.y * Mathf.Lerp(1f, 0.68f, pulse * 0.3f),
                tile.startScale.z);
            return;
        }

        tile.collider.enabled = false;
        SetPlatformColor(tile.renderer, new Color(0.018f, 0.020f, 0.026f));
        float flight = BoundaryMath.EaseInOut(Mathf.InverseLerp(0.52f, 2.15f, elapsed));
        Vector3 target = match.SingularityPosition;
        Vector3 control = Vector3.Lerp(tile.startPosition, target, 0.44f) + Vector3.up * (9f + tile.stagger * 7f);
        control += Vector3.Cross(Vector3.up, target - tile.startPosition).normalized *
                   Mathf.Lerp(-6f, 6f, tile.stagger);
        tile.transform.position = QuadraticBezier(tile.startPosition, control, target, flight);
        tile.transform.rotation = tile.startRotation * Quaternion.Euler(
            flight * (270f + index % 5 * 29f),
            flight * (410f + index % 7 * 21f),
            flight * 190f);
        tile.transform.localScale = Vector3.Lerp(tile.startScale, tile.startScale * 0.04f, flight);
    }

    public void ApplyBlackHoleContact(int platformIndex, int hitCount)
    {
        if (!breakawayPlatforms.TryGetValue(platformIndex, out PlatformTile tile) ||
            tile == null || !tile.canCorrupt || tile.forcedCollapseAt >= 0f)
        {
            return;
        }

        tile.corruptionHits = Mathf.Clamp(hitCount, 0, 3);
        SetPlatformColor(tile.renderer, StablePlatformColor(tile));
        if (tile.corruptionHits >= 3)
            tile.forcedCollapseAt = Time.time;
    }

    private static Color StablePlatformColor(PlatformTile tile)
    {
        switch (tile.corruptionHits)
        {
            case 1: return new Color(0.245f, 0.255f, 0.275f);
            case 2: return new Color(0.115f, 0.122f, 0.138f);
            case 3: return new Color(0.042f, 0.045f, 0.055f);
            default: return new Color(0.36f, 0.38f, 0.42f);
        }
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
            UpdateCollapsingPlatform(tile, 0.32f, i);
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

    public int PreviewPlatformCorruptionForValidation()
    {
        PlatformTile tile = platforms.Find(candidate => candidate.canCorrupt);
        if (tile == null)
            return 0;

        int score = 0;
        ApplyBlackHoleContact(tile.stableIndex, 1);
        tile.renderer.GetPropertyBlock(platformProperties);
        float firstDarkness = platformProperties.GetColor("_BaseColor").r;
        if (firstDarkness < 0.30f)
            score++;

        ApplyBlackHoleContact(tile.stableIndex, 2);
        tile.renderer.GetPropertyBlock(platformProperties);
        if (platformProperties.GetColor("_BaseColor").r < firstDarkness)
            score++;

        ApplyBlackHoleContact(tile.stableIndex, 3);
        tile.forcedCollapseAt = Time.time - 1.1f;
        UpdateForcedCollapse(tile, 0);
        if (!tile.collider.enabled && Vector3.Distance(tile.transform.position, tile.startPosition) > 1f)
            score++;

        tile.transform.gameObject.SetActive(true);
        tile.corruptionHits = 0;
        tile.forcedCollapseAt = -1f;
        ResetPlatform(tile);
        return score;
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
        CreateAccretionRing(core.transform, "White Photon Crown", 1.92f, 0.12f,
            new Color(1f, 0.88f, 0.68f), new Vector3(0.04f, 1f, -0.03f), 86f);
        CreateAccretionRing(core.transform, "Violet Lensing Band", 2.8f, 0.13f,
            new Color(0.72f, 0.10f, 1f), new Vector3(-0.08f, 1f, 0.18f), -33f);
        CreateAccretionRing(core.transform, "Blue Photon Band", 3.2f, 0.075f,
            new Color(0.12f, 0.72f, 1f), new Vector3(0.24f, 1f, -0.12f), 25f);
        CreateAccretionRing(core.transform, "Crimson Outer Disc", 3.65f, 0.06f,
            new Color(1f, 0.055f, 0.13f), new Vector3(-0.16f, 1f, -0.08f), -19f);
        CreateAccretionRing(core.transform, "Polar Lensing Arc", 2.72f, 0.055f,
            new Color(0.34f, 0.9f, 1f), new Vector3(1f, 0.08f, 0.06f), 37f);

        CreatePolarJet(core.transform, "North Relativistic Jet", 1f);
        CreatePolarJet(core.transform, "South Relativistic Jet", -1f);
        CreateSingularityParticles(core.transform);

        GameObject lightObject = new GameObject("Singularity Rim Light", typeof(Light));
        lightObject.transform.SetParent(core.transform, false);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.62f, 0.12f, 1f);
        light.intensity = 6.5f;
        light.range = 34f;

        GameObject blueLightObject = new GameObject("Photon Rim Light", typeof(Light));
        blueLightObject.transform.SetParent(core.transform, false);
        Light blueLight = blueLightObject.GetComponent<Light>();
        blueLight.type = LightType.Point;
        blueLight.color = new Color(0.08f, 0.68f, 1f);
        blueLight.intensity = 3.2f;
        blueLight.range = 22f;

        horizonRing = CreateCircleLine("Event Horizon", 96, coreMaterial, 0.18f);
        horizonRing.transform.position = new Vector3(
            match.SingularityPosition.x,
            match.SingularityPosition.y - 5.5f,
            match.SingularityPosition.z);
    }

    private void CreatePolarJet(Transform parent, string name, float direction)
    {
        Material material = CreateMaterial(Color.black, new Color(0.30f, 0.82f, 1f), 9f);
        LineRenderer jet = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
        jet.transform.SetParent(parent, false);
        jet.sharedMaterial = material;
        jet.useWorldSpace = false;
        jet.positionCount = 7;
        jet.startWidth = 0.34f;
        jet.endWidth = 0.015f;
        for (int i = 0; i < jet.positionCount; i++)
        {
            float t = i / (float)(jet.positionCount - 1);
            float bend = Mathf.Sin(t * Mathf.PI) * 0.24f;
            jet.SetPosition(i, new Vector3(bend, direction * Mathf.Lerp(1.4f, 8.5f, t), -bend * 0.45f));
        }
    }

    private void CreateSingularityParticles(Transform parent)
    {
        GameObject particleObject = new GameObject("Accretion Sparks", typeof(ParticleSystem));
        particleObject.transform.SetParent(parent, false);
        ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.22f, 0.04f, 0.92f),
            new Color(0.18f, 0.72f, 1f, 0.78f));
        main.maxParticles = 220;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 74f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 3.35f;
        shape.radiusThickness = 0.3f;
        shape.rotation = new Vector3(78f, 0f, 0f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.32f;
        noise.frequency = 0.75f;
        noise.scrollSpeed = 0.45f;

        ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial = vortexMaterial;
        particles.Play();
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

public sealed class BoundaryBreakawayPlatform : MonoBehaviour
{
    public int PlatformIndex { get; set; }
}
