using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoundaryArenaPresentation : MonoBehaviour
{
    private sealed class RingTile
    {
        public Transform transform;
        public float angle;
        public float radius;
        public Vector3 startScale;
    }

    [Header("Generated stadium")]
    [SerializeField, Range(24, 64)] private int boundarySegments = 40;
    [SerializeField, Range(12, 40)] private int debrisTilesPerRing = 24;
    [SerializeField] private float boundaryHeight = 1.25f;
    [SerializeField] private float boundaryThickness = 0.7f;

    private BoundaryMatchController match;
    private Transform generatedRoot;
    private readonly List<Transform> boundaryWalls = new List<Transform>();
    private readonly List<RingTile> outerTiles = new List<RingTile>();
    private readonly List<RingTile> middleTiles = new List<RingTile>();
    private readonly List<Renderer> fractureLines = new List<Renderer>();
    private readonly List<LineRenderer> vortexLines = new List<LineRenderer>();
    private readonly HashSet<PlayerMovement> trailedPlayers = new HashSet<PlayerMovement>();
    private Transform singularityCore;
    private LineRenderer horizonRing;
    private Material boundaryMaterial;
    private Material outerTileMaterial;
    private Material middleTileMaterial;
    private Material fractureMaterial;
    private Material vortexMaterial;
    private Material coreMaterial;
    private Color originalFogColor;
    private float originalFogDensity;
    private bool originalFogEnabled;
    private bool built;

    private void Start()
    {
        match = GetComponent<BoundaryMatchController>();
        if (match == null)
            match = BoundaryMatchController.Instance;

        originalFogEnabled = RenderSettings.fog;
        originalFogColor = RenderSettings.fogColor;
        originalFogDensity = RenderSettings.fogDensity;
        BuildArena();
    }

    private void OnDestroy()
    {
        RenderSettings.fog = originalFogEnabled;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogDensity = originalFogDensity;
        DestroyMaterial(boundaryMaterial);
        DestroyMaterial(outerTileMaterial);
        DestroyMaterial(middleTileMaterial);
        DestroyMaterial(fractureMaterial);
        DestroyMaterial(vortexMaterial);
        DestroyMaterial(coreMaterial);
    }

    private void Update()
    {
        if (!built || match == null)
            return;

        UpdateBoundary();
        UpdateCollapsingTiles();
        UpdateSingularity();
        UpdateFractures();
        UpdateFog();
        UpdatePlayerTrails();
    }

    private void BuildArena()
    {
        if (match == null || built)
            return;

        generatedRoot = new GameObject("Boundary Generated Stadium").transform;
        generatedRoot.SetParent(transform, false);
        generatedRoot.position = Vector3.zero;

        boundaryMaterial = CreateMaterial(
            new Color(0.08f, 0.018f, 0.14f),
            new Color(0.78f, 0.12f, 1f), 4.2f);
        outerTileMaterial = CreateMaterial(
            new Color(0.055f, 0.03f, 0.08f),
            new Color(0.35f, 0.08f, 0.52f), 1.4f);
        middleTileMaterial = CreateMaterial(
            new Color(0.07f, 0.025f, 0.10f),
            new Color(0.65f, 0.10f, 0.80f), 1.9f);
        fractureMaterial = CreateMaterial(
            new Color(0.04f, 0.01f, 0.06f),
            new Color(1f, 0.08f, 0.65f), 4.5f);
        vortexMaterial = CreateMaterial(
            new Color(0.05f, 0.01f, 0.08f),
            new Color(0.45f, 0.25f, 1f), 3.8f);
        coreMaterial = CreateMaterial(Color.black, new Color(0.36f, 0.03f, 0.85f), 6f);

        BuildBoundaryWalls();
        BuildTileRing(
            outerTiles,
            (match.OuterRadius + match.MiddleRadius) * 0.5f,
            (match.OuterRadius - match.MiddleRadius) * 0.48f,
            outerTileMaterial,
            "Outer Fragments");
        BuildTileRing(
            middleTiles,
            (match.MiddleRadius + match.InnerRadius) * 0.5f,
            (match.MiddleRadius - match.InnerRadius) * 0.48f,
            middleTileMaterial,
            "Middle Fragments");
        BuildSingularity();
        BuildFractureLines();
        BuildVortexLines();
        built = true;
    }

    private void BuildBoundaryWalls()
    {
        Transform parent = new GameObject("Moving Boundary").transform;
        parent.SetParent(generatedRoot, false);
        for (int i = 0; i < boundarySegments; i++)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = $"Boundary Segment {i:00}";
            segment.transform.SetParent(parent, false);
            segment.GetComponent<Renderer>().sharedMaterial = boundaryMaterial;
            Collider collider = segment.GetComponent<Collider>();
            collider.material = null;
            boundaryWalls.Add(segment.transform);
        }
    }

    private void BuildTileRing(List<RingTile> list, float radius, float radialSize, Material material, string name)
    {
        Transform parent = new GameObject(name).transform;
        parent.SetParent(generatedRoot, false);
        float circumferencePiece = Mathf.PI * 2f * radius / debrisTilesPerRing * 0.82f;
        for (int i = 0; i < debrisTilesPerRing; i++)
        {
            float angle = Mathf.PI * 2f * i / debrisTilesPerRing;
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"Arena Fragment {i:00}";
            tile.transform.SetParent(parent, false);
            tile.transform.localScale = new Vector3(radialSize, 0.14f, circumferencePiece);
            tile.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            tile.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(tile.GetComponent<Collider>());
            list.Add(new RingTile
            {
                transform = tile.transform,
                angle = angle,
                radius = radius,
                startScale = tile.transform.localScale
            });
        }
    }

    private void BuildSingularity()
    {
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Boundary Singularity Core";
        core.transform.SetParent(generatedRoot, false);
        core.transform.position = match.SingularityPosition;
        core.transform.localScale = Vector3.one * 3.4f;
        core.GetComponent<Renderer>().sharedMaterial = coreMaterial;
        Destroy(core.GetComponent<Collider>());
        singularityCore = core.transform;

        GameObject lightObject = new GameObject("Singularity Rim Light", typeof(Light));
        lightObject.transform.SetParent(core.transform, false);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.46f, 0.10f, 1f);
        light.intensity = 4.5f;
        light.range = 24f;

        horizonRing = CreateCircleLine("Event Horizon", 96, coreMaterial, 0.18f);
        horizonRing.transform.position = new Vector3(
            match.SingularityPosition.x,
            match.SingularityPosition.y - 5.5f,
            match.SingularityPosition.z);
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
                                      new Vector3(Mathf.Cos(angle) * 10f, 0.08f, Mathf.Sin(angle) * 10f);
            line.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            line.transform.localScale = new Vector3(20f, 0.05f, 0.16f);
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

    private void UpdateBoundary()
    {
        float radius = match.RingRadius;
        float segmentLength = Mathf.PI * 2f * radius / boundarySegments * 0.92f;
        float transitionRise = match.Transition == BoundaryTransition.None
            ? 1f
            : BoundaryMath.EaseInOut(match.TransitionElapsed / 1.4f);
        float floor = match.ArenaFloorY;

        for (int i = 0; i < boundaryWalls.Count; i++)
        {
            float angle = Mathf.PI * 2f * i / boundaryWalls.Count;
            Transform wall = boundaryWalls[i];
            wall.position = new Vector3(
                match.ArenaCenter.x + Mathf.Cos(angle) * radius,
                floor + boundaryHeight * 0.5f * transitionRise,
                match.ArenaCenter.z + Mathf.Sin(angle) * radius);
            wall.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            wall.localScale = new Vector3(boundaryThickness, boundaryHeight * transitionRise, segmentLength);
        }
    }

    private void UpdateCollapsingTiles()
    {
        bool outerRemoved = match.Phase == BoundaryPhase.MiddleRing || match.Phase == BoundaryPhase.InnerRing;
        bool middleRemoved = match.Phase == BoundaryPhase.InnerRing;
        float outerProgress = match.Transition == BoundaryTransition.ClosingOuterRing
            ? BoundaryMath.EaseInOut(match.TransitionElapsed / Mathf.Max(0.1f, match.TransitionRemaining + match.TransitionElapsed))
            : outerRemoved ? 1f : 0f;
        float middleProgress = match.Transition == BoundaryTransition.ClosingMiddleRing
            ? BoundaryMath.EaseInOut(match.TransitionElapsed / Mathf.Max(0.1f, match.TransitionRemaining + match.TransitionElapsed))
            : middleRemoved ? 1f : 0f;

        UpdateTileRing(outerTiles, outerProgress, outerRemoved);
        UpdateTileRing(middleTiles, middleProgress, middleRemoved);
    }

    private void UpdateTileRing(List<RingTile> tiles, float progress, bool removed)
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            RingTile tile = tiles[i];
            bool visible = !removed || progress < 0.995f;
            tile.transform.gameObject.SetActive(visible);
            if (!visible) continue;

            float stagger = Mathf.Clamp01(progress * 1.25f - (i % 4) * 0.055f);
            float radius = Mathf.Lerp(tile.radius, 2.5f, stagger);
            float y = Mathf.Lerp(match.ArenaFloorY + 0.04f, match.SingularityPosition.y - 1f, stagger);
            float angle = tile.angle + stagger * 0.65f * match.CurrentDirection;
            tile.transform.position = match.ArenaCenter +
                                      new Vector3(Mathf.Cos(angle) * radius, y - match.ArenaFloorY, Mathf.Sin(angle) * radius);
            tile.transform.rotation = Quaternion.Euler(stagger * 110f, -angle * Mathf.Rad2Deg, stagger * 160f);
            tile.transform.localScale = Vector3.Lerp(tile.startScale, tile.startScale * 0.18f, stagger);
        }
    }

    private void UpdateSingularity()
    {
        if (singularityCore == null)
            return;

        float intensity = match.Phase == BoundaryPhase.InnerRing
            ? 1.3f + Mathf.Clamp01(match.PhaseElapsed / 45f) * 0.7f
            : match.Phase == BoundaryPhase.MiddleRing ? 1.15f : 1f;
        float pulse = 1f + Mathf.Sin(Time.time * (2.2f + intensity)) * 0.045f;
        singularityCore.localScale = Vector3.one * (3.4f * intensity * pulse);
        singularityCore.Rotate(Vector3.up, (18f + intensity * 10f) * Time.deltaTime, Space.World);

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
            if (!vortexVisible) continue;

            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (float)(line.positionCount - 1);
                float turns = 3.2f + lane * 0.55f;
                float angle = t * turns * Mathf.PI * 2f * match.CurrentDirection + Time.time * (0.8f + lane * 0.2f);
                float radius = Mathf.Lerp(match.RingRadius * (0.88f - lane * 0.12f), 1.4f, t);
                float y = Mathf.Lerp(match.ArenaFloorY + 0.7f + lane * 2.6f, match.SingularityPosition.y, t);
                line.SetPosition(i, match.ArenaCenter + new Vector3(Mathf.Cos(angle) * radius, y - match.ArenaFloorY, Mathf.Sin(angle) * radius));
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
            if (!active) continue;
            float sequence = Mathf.Repeat(match.DisasterElapsed - i * 0.18f, 3.4f);
            float lift = sequence > 1.2f && sequence < 2.1f ? pulse : 0f;
            Vector3 position = renderer.transform.position;
            position.y = match.ArenaFloorY + 0.08f + lift * 0.55f;
            renderer.transform.position = position;
            renderer.transform.localScale = new Vector3(20f, 0.05f + lift * 0.22f, 0.16f + lift * 0.3f);
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
        RenderSettings.fogDensity = Mathf.Lerp(Mathf.Max(0.002f, originalFogDensity), 0.047f, fog);
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
            if (trail == null) trail = player.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.38f;
            trail.startWidth = 0.22f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.12f;
            trail.sharedMaterial = vortexMaterial;
            trail.startColor = new Color(0.75f, 0.24f, 1f, 0.8f);
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

    private static Material CreateMaterial(Color color, Color emission, float intensity)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader);
        material.color = color;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission * intensity);
        return material;
    }

    private static void DestroyMaterial(Material material)
    {
        if (material != null)
            Destroy(material);
    }
}
