using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class BoundaryArenaPresentation : MonoBehaviour
{
    private const string RuntimeAuthoredArenaName = "Boundary Authored Stadium";
    public const float GeneratedWallSizeMultiplier = 1.12f;
    public const float GeneratedWallExtraHeightMultiplier = 1.35f;
    public const float GeneratedWallElevationExponent = 1.05f;
    public const float ArenaAmbientLightMultiplier = 0.5f;
    public const float VoidWallGlowIntensity = 6f;

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
        public bool authoredActive = true;
    }

    private sealed class AccretionRing
    {
        public Transform transform;
        public Vector3 axis;
        public float speed;
    }

    private sealed class VoidWallGlowSnapshot
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] glowMaterials;
        public GameObject lightObject;
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
    [SerializeField, Range(4, 12)] private int wallCountPerTier = 7;
    [SerializeField, Range(6f, 14f)] private float wallLength = 10.5f;
    [SerializeField, Range(5f, 12f)] private float wallHeight = 7.5f;
    [SerializeField, Range(0.7f, 2f)] private float wallThickness = 1.15f;
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
    private readonly List<VoidWallGlowSnapshot> voidWallGlowSnapshots = new List<VoidWallGlowSnapshot>();
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
    private Color originalAmbientLight;
    private Color originalAmbientSkyColor;
    private Color originalAmbientEquatorColor;
    private Color originalAmbientGroundColor;
    private bool built;
    private bool buildVisuals;
    private bool renderSettingsCaptured;
    private bool editorPreview;
    private int transitionRampCount;
    private ArenaPreviewSnapshot pendingEditorPreviewSnapshot;

    [Serializable]
    private sealed class ArenaPreviewSnapshot
    {
        public string presentationSettings;
        public List<ArenaPreviewTransform> transforms = new List<ArenaPreviewTransform>();
    }

    [Serializable]
    private sealed class ArenaPreviewTransform
    {
        public string siblingPath;
        public string namePath;
        public bool activeSelf;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    public int GeneratedPlatformCount => platforms.Count;
    public float AuthoredPlayableRadius { get; private set; }
    public bool LegacyArenaHidden => disabledLegacyArena.Count > 0;
    public bool HasSideWalls => false;
    public int GeneratedTransitionRampCount => transitionRampCount;
    public int GeneratedPlatformColliderCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < platforms.Count; i++)
            {
                if (platforms[i].collider != null)
                    count++;
            }
            return count;
        }
    }

    private void Awake()
    {
        LoadAuthoredArenaSnapshot();
        Instance = this;
    }

    private void LoadAuthoredArenaSnapshot()
    {
        if (!Application.isPlaying && !Application.isBatchMode)
            return;

        string settings = string.Empty;
        TextAsset authoredSnapshot = Resources.Load<TextAsset>("Boundary/BoundaryArenaAuthoring");
        if (authoredSnapshot != null)
            settings = authoredSnapshot.text;

#if UNITY_EDITOR
        const string sessionKey = "Boundary.ArenaPreviewSettings";
        string snapshotPath = Path.GetFullPath(Path.Combine(
            Application.dataPath, "../Library", "BoundaryArenaPreview.PlayMode.json"));
        if (File.Exists(snapshotPath))
            settings = File.ReadAllText(snapshotPath);
        else if (string.IsNullOrEmpty(settings))
            settings = SessionState.GetString(sessionKey, string.Empty);
#endif
        if (string.IsNullOrEmpty(settings))
        {
            Debug.LogWarning("[BoundaryArenaPresentation] No saved arena preview overrides were available for Play Mode.");
            return;
        }

        pendingEditorPreviewSnapshot = JsonUtility.FromJson<ArenaPreviewSnapshot>(settings);
        if (pendingEditorPreviewSnapshot != null &&
            !string.IsNullOrEmpty(pendingEditorPreviewSnapshot.presentationSettings))
        {
#if UNITY_EDITOR
            EditorJsonUtility.FromJsonOverwrite(pendingEditorPreviewSnapshot.presentationSettings, this);
#endif
        }
#if UNITY_EDITOR
        else
        {
            // Accept preview data captured by the earlier settings-only handoff.
            EditorJsonUtility.FromJsonOverwrite(settings, this);
        }
        if (File.Exists(snapshotPath))
            File.Delete(snapshotPath);
        SessionState.EraseString(sessionKey);
#endif
    }

#if UNITY_EDITOR
    public string CaptureEditorPreviewForPlayMode()
    {
        ArenaPreviewSnapshot snapshot = new ArenaPreviewSnapshot
        {
            presentationSettings = EditorJsonUtility.ToJson(this)
        };
        CapturePreviewTransforms(transform, transform, string.Empty, snapshot.transforms);
        return JsonUtility.ToJson(snapshot);
    }

    private static void CapturePreviewTransforms(
        Transform root, Transform parent, string parentPath, List<ArenaPreviewTransform> results)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            string path = string.IsNullOrEmpty(parentPath) ? i.ToString() : parentPath + "/" + i;
            string namePath = BuildNamePath(child, root);
            results.Add(new ArenaPreviewTransform
            {
                siblingPath = path,
                namePath = namePath,
                activeSelf = child.gameObject.activeSelf,
                localPosition = child.localPosition,
                localRotation = child.localRotation,
                localScale = child.localScale
            });
            CapturePreviewTransforms(root, child, path, results);
        }
    }

    private static string BuildNamePath(Transform target, Transform root)
    {
        var names = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            names.Add(current.name);
            current = current.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }
#endif

    private void Start()
    {
        match = GetComponent<BoundaryMatchController>();
        if (match == null)
            match = BoundaryMatchController.Instance;

        buildVisuals = ShouldBuildVisuals();
        if (buildVisuals)
        {
            originalFogEnabled = RenderSettings.fog;
            originalFogColor = RenderSettings.fogColor;
            originalFogDensity = RenderSettings.fogDensity;
            renderSettingsCaptured = true;
            platformProperties = new MaterialPropertyBlock();
            originalAmbientLight = RenderSettings.ambientLight;
            originalAmbientSkyColor = RenderSettings.ambientSkyColor;
            originalAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            originalAmbientGroundColor = RenderSettings.ambientGroundColor;
            RenderSettings.ambientLight = originalAmbientLight * ArenaAmbientLightMultiplier;
            RenderSettings.ambientSkyColor = originalAmbientSkyColor * ArenaAmbientLightMultiplier;
            RenderSettings.ambientEquatorColor = originalAmbientEquatorColor * ArenaAmbientLightMultiplier;
            RenderSettings.ambientGroundColor = originalAmbientGroundColor * ArenaAmbientLightMultiplier;
        }

        BuildArena(buildVisuals);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (renderSettingsCaptured)
        {
            RenderSettings.fog = originalFogEnabled;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogDensity = originalFogDensity;
            RenderSettings.ambientLight = originalAmbientLight;
            RenderSettings.ambientSkyColor = originalAmbientSkyColor;
            RenderSettings.ambientEquatorColor = originalAmbientEquatorColor;
            RenderSettings.ambientGroundColor = originalAmbientGroundColor;
        }

        SetVoidWallGlow(false);

        foreach (GameObject legacyRoot in disabledLegacyArena)
        {
            if (legacyRoot != null)
                legacyRoot.SetActive(true);
        }

        foreach (Material material in generatedMaterials)
        {
            if (material != null)
            {
                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
            }
        }
    }

    private void Update()
    {
        if (!built || match == null)
            return;

        UpdatePlatformCollapses();
        if (buildVisuals)
        {
            UpdateSingularity();
            UpdateFractures();
            UpdateFog();
        }
    }

    private void BuildArena(bool includeVisuals)
    {
        if (match == null || built)
            return;

        if (TryAdoptAuthoredArena(includeVisuals))
            return;

        if (!editorPreview)
        {
            Debug.LogError("[BoundaryArenaPresentation] Boundary Authored Stadium is missing. Runtime generation is disabled so the saved scene cannot be replaced.");
            return;
        }

        generatedRoot = new GameObject("Boundary Generated Stadium").transform;
        generatedRoot.SetParent(transform, false);
        generatedRoot.position = Vector3.zero;

        // Physics is authoritative on the dedicated server, while shaders and
        // render components may be stripped from its build. Construct every
        // collider before touching an optional presentation resource.
        BuildPlatformFloor();
        BuildTierTransitionRamps();
        BuildWallJumpStructures();

        if (GeneratedPlatformColliderCount == 0)
        {
            Debug.LogError("[BoundaryArenaPresentation] Generated arena has no colliders. Keeping the legacy arena enabled.");
            return;
        }

        // Only retire the fallback floor after its collision replacement is
        // complete. A rendering failure can therefore never remove physics.
        DisableLegacyArena();
        if (!editorPreview)
            AlignSpawnsAndExistingPlayers();

        if (includeVisuals)
        {
            platformMaterial = CreateMaterial(
                new Color(0.09f, 0.095f, 0.105f),
                new Color(0.01375f, 0.01625f, 0.0225f), 0.55f);
            fractureMaterial = CreateMaterial(
                new Color(0.04f, 0.01f, 0.06f),
                new Color(1f, 0.08f, 0.65f), 4.5f);
            vortexMaterial = CreateMaterial(
                new Color(0.05f, 0.01f, 0.06f),
                new Color(0.45f, 0.25f, 1f), 3.8f);
            coreMaterial = CreateMaterial(Color.black, new Color(0.32f, 0.02f, 0.72f), 6f);
            ApplyPlatformMaterial();
            BuildSingularity();
            BuildFractureLines();
            BuildVortexLines();
        }

        built = true;
        ApplyPendingEditorPreviewOverrides();
        string buildKind = editorPreview ? "editor-preview" : "authoritative";
        Debug.Log($"[BoundaryArenaPresentation] Built {GeneratedPlatformColliderCount} {buildKind} arena colliders (visuals={includeVisuals}).");
    }

    private bool TryAdoptAuthoredArena(bool includeVisuals)
    {
        Transform authoredRoot = null;
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.name == RuntimeAuthoredArenaName && candidate.gameObject.scene == gameObject.scene)
            {
                authoredRoot = candidate;
                break;
            }
        }
        if (authoredRoot == null)
            return false;

        generatedRoot = authoredRoot;
        RegisterAuthoredPlatforms(authoredRoot);
        if (GeneratedPlatformColliderCount == 0)
        {
            Debug.LogError("[BoundaryArenaPresentation] The authored arena has no platform colliders.");
            return false;
        }

        if (includeVisuals)
        {
            // The saved scene is authoritative. Do not replace authored wall
            // or platform materials with the procedural arena's gray default.
            singularityCore = FindDescendant(authoredRoot, "Boundary Singularity Core");
            horizonRing = FindDescendant(authoredRoot, "Event Horizon")?.GetComponent<LineRenderer>();
            RegisterAuthoredLines(authoredRoot, "Fracture Lines", fractureLines);
            RegisterAuthoredLines(authoredRoot, "Vortex Currents", vortexLines);
        }

        DisableLegacyArena();
        AlignSpawnsAndExistingPlayers();
        built = true;
        Debug.Log($"[BoundaryArenaPresentation] Adopted {GeneratedPlatformColliderCount} saved arena colliders without regenerating geometry.");
        return true;
    }

    private void RegisterAuthoredPlatforms(Transform authoredRoot)
    {
        AuthoredPlayableRadius = 0f;
        Collider[] colliders = authoredRoot.GetComponentsInChildren<Collider>(true);
        int fallbackIndex = 10000;
        foreach (Collider collider in colliders)
        {
            Transform tileTransform = collider.transform;
            string groupName = FindArenaGroupName(tileTransform, authoredRoot);
            if (groupName == null)
                continue;

            Vector3 boundsCenter = collider.bounds.center;
            Vector3 boundsExtents = collider.bounds.extents;
            float boundsRadius = Vector2.Distance(
                new Vector2(boundsCenter.x, boundsCenter.z),
                new Vector2(match.ArenaCenter.x, match.ArenaCenter.z)) +
                new Vector2(boundsExtents.x, boundsExtents.z).magnitude;
            AuthoredPlayableRadius = Mathf.Max(AuthoredPlayableRadius, boundsRadius);

            BoundaryBreakawayPlatform contact = tileTransform.GetComponent<BoundaryBreakawayPlatform>();
            bool isWall = groupName == "Wall Jump Structures";
            int stableIndex = ResolveAuthoredStableIndex(groupName, tileTransform.name, fallbackIndex++);
            if (contact != null)
                contact.PlatformIndex = stableIndex;
            int band = isWall ? ParseBand(tileTransform.name) : CollapseBandForGroup(groupName,
                Vector2.Distance(
                    new Vector2(tileTransform.position.x, tileTransform.position.z),
                    new Vector2(match.ArenaCenter.x, match.ArenaCenter.z)));
            int hash = BoundaryMath.StableHash(74191 + band * 193, stableIndex);
            var platform = new PlatformTile
            {
                stableIndex = stableIndex,
                transform = tileTransform,
                renderer = tileTransform.GetComponent<Renderer>(),
                collider = collider,
                startPosition = tileTransform.position,
                startRotation = tileTransform.rotation,
                startScale = tileTransform.localScale,
                collapseBand = band,
                stagger = (hash % 1000) / 999f,
                canCorrupt = !isWall,
                authoredActive = tileTransform.gameObject.activeInHierarchy
            };
            platforms.Add(platform);
            if (!platform.authoredActive)
            {
                tileTransform.gameObject.SetActive(false);
                collider.enabled = false;
            }
            if (contact != null)
                breakawayPlatforms[stableIndex] = platform;
            if (groupName == "Tier Transition Ramps")
                transitionRampCount++;
        }
    }

    private static string FindArenaGroupName(Transform target, Transform root)
    {
        Transform current = target.parent;
        string fallbackGroup = null;
        while (current != null && current != root)
        {
            if (current.name == "Tier0" || current.name == "Tier1" || current.name == "Tier2")
                return current.name;
            if (fallbackGroup == null &&
                (current.name == "Breakaway Platforms" || current.name == "Tier Transition Ramps" ||
                 current.name == "Wall Jump Structures"))
                fallbackGroup = current.name;
            current = current.parent;
        }
        return fallbackGroup;
    }

    private int CollapseBandForGroup(string groupName, float radius)
    {
        if (groupName == "Tier0")
            return 0;
        if (groupName == "Tier1")
            return 1;
        if (groupName == "Tier2")
            return 2;
        return CollapseBand(radius);
    }

    private static int ParseBand(string objectName)
    {
        string[] parts = objectName.Split(' ');
        if (parts.Length > 1 && parts[1].Length > 0 && int.TryParse(parts[1].Split('-')[0], out int band))
            return band;
        return 0;
    }

    private static int ResolveAuthoredStableIndex(string groupName, string objectName, int fallback)
    {
        string[] parts = objectName.Split(' ');
        if (groupName == "Breakaway Platforms" && parts.Length > 0 &&
            int.TryParse(parts[parts.Length - 1], out int floorIndex))
            return floorIndex;

        if ((groupName == "Tier Transition Ramps" || groupName == "Wall Jump Structures") &&
            parts.Length > 1)
        {
            string[] bandAndIndex = parts[parts.Length - 1].Split('-');
            if (bandAndIndex.Length == 2 && int.TryParse(bandAndIndex[0], out int band) &&
                int.TryParse(bandAndIndex[1], out int index))
            {
                if (groupName == "Tier Transition Ramps")
                    return (band == 2 ? 4000 : 5000) + index;
                return (band == 2 ? 1000 : band == 1 ? 2000 : 3000) + index;
            }
        }
        return fallback;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == objectName)
                return child;
        return null;
    }

    private static void RegisterAuthoredLines<T>(Transform root, string groupName, List<T> results) where T : Component
    {
        Transform group = FindDescendant(root, groupName);
        if (group == null)
            return;
        results.AddRange(group.GetComponentsInChildren<T>(true));
    }

    private void ApplyPendingEditorPreviewOverrides()
    {
        if (pendingEditorPreviewSnapshot == null)
            return;

        foreach (ArenaPreviewTransform previewTransform in pendingEditorPreviewSnapshot.transforms)
        {
            Transform target = ResolveNamePath(transform, previewTransform.namePath);
            if (target == null)
                target = ResolveSiblingPath(transform, previewTransform.siblingPath);
            if (target == null)
                continue;

            target.localPosition = previewTransform.localPosition;
            target.localRotation = previewTransform.localRotation;
            target.localScale = previewTransform.localScale;
            target.gameObject.SetActive(previewTransform.activeSelf);
        }

        foreach (PlatformTile platform in platforms)
        {
            if (platform.transform == null)
                continue;
            platform.startPosition = platform.transform.position;
            platform.startRotation = platform.transform.rotation;
            platform.startScale = platform.transform.localScale;
            // A disabled authoring group (for example Wall Jump Structures)
            // disables every platform beneath it even though each child may
            // still have activeSelf=true. Preserve the effective hierarchy
            // state so gameplay cannot resurrect those children later.
            platform.authoredActive = platform.transform.gameObject.activeInHierarchy;
            if (!platform.authoredActive)
            {
                platform.transform.gameObject.SetActive(false);
                if (platform.collider != null)
                    platform.collider.enabled = false;
            }
        }

        pendingEditorPreviewSnapshot = null;
        Debug.Log("[BoundaryArenaPresentation] Applied the Game scene arena preview overrides for Play Mode.");
    }

    private static Transform ResolveNamePath(Transform root, string namePath)
    {
        if (string.IsNullOrEmpty(namePath))
            return null;

        Transform current = root;
        string[] names = namePath.Split('/');
        foreach (string childName in names)
        {
            Transform next = null;
            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                if (child.name == childName)
                {
                    next = child;
                    break;
                }
            }
            if (next == null)
                return null;
            current = next;
        }
        return current;
    }

    private static Transform ResolveSiblingPath(Transform root, string siblingPath)
    {
        Transform current = root;
        string[] indices = siblingPath.Split('/');
        foreach (string indexText in indices)
        {
            if (!int.TryParse(indexText, out int index) || index < 0 || index >= current.childCount)
                return null;
            current = current.GetChild(index);
        }
        return current;
    }

#if UNITY_EDITOR
    public void BuildEditorPreview()
    {
        match = GetComponent<BoundaryMatchController>();
        if (match == null)
            return;

        editorPreview = true;
        buildVisuals = true;
        platformProperties = new MaterialPropertyBlock();
        BuildArena(true);
    }
#endif

    private void ApplyPlatformMaterial()
    {
        for (int i = 0; i < platforms.Count; i++)
        {
            if (platforms[i].renderer != null)
                platforms[i].renderer.sharedMaterial = platformMaterial;
        }
    }

    public void SetVoidWallGlow(bool enabled)
    {
        if (!buildVisuals)
            return;

        if (!enabled)
        {
            foreach (VoidWallGlowSnapshot snapshot in voidWallGlowSnapshots)
            {
                if (snapshot.renderer != null)
                    snapshot.renderer.sharedMaterials = snapshot.originalMaterials;
                foreach (Material material in snapshot.glowMaterials)
                    if (material != null)
                        Destroy(material);
                if (snapshot.lightObject != null)
                    Destroy(snapshot.lightObject);
            }
            voidWallGlowSnapshots.Clear();
            return;
        }

        if (voidWallGlowSnapshots.Count > 0)
            return;

        foreach (PlatformTile platform in platforms)
        {
            if (platform.renderer == null || platform.transform == null || !platform.transform.CompareTag("Wall"))
                continue;

            Material[] originals = platform.renderer.sharedMaterials;
            Material[] glowing = new Material[originals.Length];
            for (int index = 0; index < originals.Length; index++)
            {
                Material source = originals[index];
                if (source == null)
                    continue;
                Material copy = new Material(source) { name = source.name + " (Void Wall Glow)" };
                copy.EnableKeyword("_EMISSION");
                if (copy.HasProperty("_EmissionColor"))
                    copy.SetColor("_EmissionColor", new Color(0.12f, 0.72f, 1f) * VoidWallGlowIntensity);
                glowing[index] = copy;
            }
            platform.renderer.sharedMaterials = glowing;

            GameObject lightObject = new GameObject("Void Wall Light", typeof(Light));
            lightObject.transform.SetParent(platform.transform, false);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.12f, 0.72f, 1f);
            light.range = 14f;
            light.intensity = VoidWallGlowIntensity;
            light.shadows = LightShadows.None;
            voidWallGlowSnapshots.Add(new VoidWallGlowSnapshot
            {
                renderer = platform.renderer,
                originalMaterials = originals,
                glowMaterials = glowing,
                lightObject = lightObject
            });
        }
    }

    private static bool ShouldBuildVisuals()
    {
        return !Application.isBatchMode && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
    }

    private void DisableLegacyArena()
    {
        DisableLegacyObject("Wall");
        DisableLegacyObject("Plane");

        BlackKill[] legacySingularities = FindObjectsByType<BlackKill>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (BlackKill singularity in legacySingularities)
        {
            if (singularity == null || singularity.gameObject.scene != gameObject.scene)
                continue;
            if (singularity.transform.IsChildOf(transform))
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

        int wallCount = WallCountForBand(wallCountPerTier, collapseBand);
        for (int wallIndex = 0; wallIndex < wallCount; wallIndex++)
        {
            int seed = 81017 + collapseBand * 7919;
            float sectorJitter = Mathf.Lerp(-0.38f, 0.38f, BoundaryMath.StableUnit(seed, wallIndex));
            float angle = Mathf.PI * 2f * (wallIndex + 0.5f) / wallCount +
                          collapseBand * 0.19f + sectorJitter;
            float radius01 = Mathf.Lerp(0.12f, 0.88f, BoundaryMath.StableUnit(seed + 1, wallIndex));
            float radius = Mathf.Lerp(innerDistance, outerDistance, radius01);
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 position = match.ArenaCenter + radial * radius;

            float length = ScaleGeneratedWallDimension(wallLength * Mathf.Lerp(0.92f, 1.32f,
                BoundaryMath.StableUnit(seed + 3, wallIndex)));
            float height = ScaleGeneratedWallDimension(wallHeight * Mathf.Lerp(0.92f, 1.38f,
                BoundaryMath.StableUnit(seed + 4, wallIndex)));
            float thickness = ScaleGeneratedWallDimension(wallThickness * Mathf.Lerp(0.92f, 1.28f,
                BoundaryMath.StableUnit(seed + 5, wallIndex)));
            float extraHeight = GeneratedWallExtraHeight(
                wallMaximumExtraHeight,
                BoundaryMath.StableUnit(seed + 6, wallIndex));
            position.y = baseSurfaceY + wallGroundClearance + extraHeight + height * 0.5f;

            // The wall's complete forward axis, including pitch, faces the
            // arena center. This eliminates tangential and yaw-jittered walls.
            Vector3 towardCenter = match.ArenaCenter - position;
            Quaternion rotation = Quaternion.LookRotation(towardCenter.normalized, Vector3.up);
            CreatePlatform(
                parent,
                $"Wall {collapseBand}-{wallIndex:00}",
                position,
                new Vector3(length, height, thickness),
                rotation,
                collapseBand,
                indexOffset + wallIndex,
                true);
        }
    }

    public static int WallCountForBand(int standardCount, int collapseBand)
    {
        int count = Mathf.Max(0, standardCount);
        if (collapseBand == 0)
            return 0;
        if (collapseBand == 2)
            return Mathf.RoundToInt(count * 0.75f);
        return count;
    }

    public static float ScaleGeneratedWallDimension(float value)
    {
        return Mathf.Max(0f, value) * GeneratedWallSizeMultiplier;
    }

    public static float GeneratedWallExtraHeight(float maximumExtraHeight, float stableUnit)
    {
        return Mathf.Max(0f, maximumExtraHeight) * GeneratedWallExtraHeightMultiplier *
               Mathf.Pow(Mathf.Clamp01(stableUnit), GeneratedWallElevationExponent);
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
        GameObject tile;
        Renderer renderer = null;
        Collider collider;
        if (buildVisuals)
        {
            tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            renderer = tile.GetComponent<Renderer>();
            collider = tile.GetComponent<Collider>();
        }
        else
        {
            // A plain BoxCollider survives Dedicated Server asset stripping and
            // avoids all dependencies on meshes, renderers, materials, and shaders.
            tile = new GameObject();
            collider = tile.AddComponent<BoxCollider>();
        }

        tile.name = platformName;
        tile.layer = 3;
        tile.transform.SetParent(parent, false);
        tile.transform.position = position;
        tile.transform.rotation = rotation;
        tile.transform.localScale = scale;
        if (isWall)
            tile.tag = "Wall";

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
            if (!tile.authoredActive)
            {
                if (tile.transform.gameObject.activeSelf)
                    tile.transform.gameObject.SetActive(false);
                continue;
            }
            bool removed = tile.collapseBand == 2 ? outerRemoved : tile.collapseBand == 1 && middleRemoved;
            bool transitioning = (tile.collapseBand == 2 && match.Transition == BoundaryTransition.ClosingOuterRing) ||
                                 (tile.collapseBand == 1 && match.Transition == BoundaryTransition.ClosingMiddleRing);

            if (removed)
            {
                if (tile.transform.gameObject.activeSelf)
                    tile.transform.gameObject.SetActive(false);
                continue;
            }

            if (tile.authoredActive && !tile.transform.gameObject.activeSelf)
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

    public void ResetForNewRound()
    {
        for (int i = 0; i < platforms.Count; i++)
        {
            PlatformTile tile = platforms[i];
            tile.forcedCollapseAt = -1f;
            tile.corruptionHits = 0;
            if (tile.authoredActive && !tile.transform.gameObject.activeSelf)
                tile.transform.gameObject.SetActive(true);
            ResetPlatform(tile);
        }
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

        tile.corruptionHits = Mathf.Clamp(hitCount, 0, BoundaryMatchController.PlatformHitsToCollapse);
        SetPlatformColor(tile.renderer, StablePlatformColor(tile));
        if (tile.corruptionHits >= BoundaryMatchController.PlatformHitsToCollapse)
            tile.forcedCollapseAt = Time.time;
    }

    private static Color StablePlatformColor(PlatformTile tile)
    {
        switch (tile.corruptionHits)
        {
            case 1: return new Color(0.245f, 0.255f, 0.275f);
            case 2: return new Color(0.115f, 0.122f, 0.138f);
            case 3: return new Color(0.075f, 0.080f, 0.092f);
            case 4: return new Color(0.052f, 0.056f, 0.066f);
            case 5: return new Color(0.042f, 0.045f, 0.055f);
            default: return new Color(0.36f, 0.38f, 0.42f);
        }
    }

    private void SetPlatformColor(Renderer renderer, Color color)
    {
        if (renderer == null || platformProperties == null)
            return;

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

        ApplyBlackHoleContact(tile.stableIndex, BoundaryMatchController.PlatformHitsToCollapse);
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
        DestroyGeneratedObject(core.GetComponent<Collider>());
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
            DestroyGeneratedObject(line.GetComponent<Collider>());
        }
    }

    private static void DestroyGeneratedObject(UnityEngine.Object generatedObject)
    {
        if (generatedObject == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(generatedObject);
            return;
        }
#endif
        Destroy(generatedObject);
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
        RenderSettings.fogDensity = Mathf.Lerp(
            Mathf.Max(0.002f, originalFogDensity),
            0.039f * BoundaryMath.DisasterPower(BoundaryDisaster.DarkMatterFog),
            fog);
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
        if (shader == null)
        {
            Debug.LogWarning("[BoundaryArenaPresentation] No runtime shader is available; continuing with collision geometry only.");
            return null;
        }
        Material material = new Material(shader);
        material.color = color;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission * intensity);
        generatedMaterials.Add(material);
        return material;
    }

#if UNITY_EDITOR
    public void BuildPhysicsOnlyArenaForValidation(BoundaryMatchController controller)
    {
        match = controller;
        // The validation root intentionally has no saved scene arena. Permit
        // the procedural path so this continues to verify collider-only builds.
        editorPreview = true;
        buildVisuals = false;
        BuildArena(false);
    }
#endif
}

public sealed class BoundaryBreakawayPlatform : MonoBehaviour
{
    public int PlatformIndex { get; set; }
}
