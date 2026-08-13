#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PurrNet;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BoundaryFeatureBuilder
{
    private const string DirectorPath = "Assets/Resources/Boundary/BoundaryMatchDirector.prefab";
    private const string HazardPath = "Assets/Resources/Boundary/BoundaryHazard.prefab";
    private const string PlayerPath = "Assets/Player.prefab";
    private const string RegistryPath = "Assets/NetworkPrefabs.asset";

    [MenuItem("Boundary/Build Feature Assets")]
    public static void Build()
    {
        EnsureFolders();
        GameObject hazard = BuildHazardPrefab();
        GameObject director = BuildDirectorPrefab();
        AddPlayerState();
        AddToNetworkRegistry(hazard, director);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BoundaryFeatureBuilder] Boundary prefabs, player state, and network registry are ready.");
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Boundary"))
            AssetDatabase.CreateFolder("Assets/Resources", "Boundary");
    }

    private static GameObject BuildDirectorPrefab()
    {
        GameObject root = new GameObject("BoundaryMatchDirector");
        try
        {
            root.AddComponent<NetworkIdentity>();
            root.AddComponent<BoundaryMatchController>();
            root.AddComponent<BoundaryArenaPresentation>();
            return PrefabUtility.SaveAsPrefabAsset(root, DirectorPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject BuildHazardPrefab()
    {
        GameObject root = new GameObject("BoundaryHazard");
        try
        {
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 2.5f;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.08f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = Vector3.one;
            SphereCollider sphere = root.AddComponent<SphereCollider>();
            sphere.radius = 1.65f;
            sphere.isTrigger = true;
            sphere.enabled = false;

            root.AddComponent<NetworkIdentity>();
            NetworkTransform networkTransform = root.AddComponent<NetworkTransform>();
            SerializedObject serializedTransform = new SerializedObject(networkTransform);
            serializedTransform.FindProperty("_ownerAuth").boolValue = false;
            serializedTransform.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<BoundaryHazard>();

            CreateVisual(root.transform, PrimitiveType.Cube, "CubeVisual", Vector3.one);
            CreateVisual(root.transform, PrimitiveType.Sphere, "SphereVisual", Vector3.one * 1.65f);
            root.transform.Find("SphereVisual").gameObject.SetActive(false);

            return PrefabUtility.SaveAsPrefabAsset(root, HazardPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateVisual(Transform parent, PrimitiveType primitive, string name, Vector3 scale)
    {
        GameObject visual = GameObject.CreatePrimitive(primitive);
        visual.name = name;
        visual.transform.SetParent(parent, false);
        visual.transform.localScale = scale;
        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);
    }

    private static void AddPlayerState()
    {
        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            if (player.GetComponent<BoundaryPlayerState>() == null)
                player.AddComponent<BoundaryPlayerState>();
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }
    }

    private static void AddToNetworkRegistry(params GameObject[] prefabs)
    {
        NetworkPrefabs registry = AssetDatabase.LoadAssetAtPath<NetworkPrefabs>(RegistryPath);
        if (registry == null)
            throw new System.InvalidOperationException("NetworkPrefabs.asset was not found.");

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null)
                continue;

            string path = AssetDatabase.GetAssetPath(prefab);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (registry.prefabs.Any(entry => entry.prefab == prefab || entry.guid == guid))
                continue;

            registry.prefabs.Add(new NetworkPrefabs.UserPrefabData
            {
                guid = guid,
                prefab = prefab,
                pooled = false,
                warmupCount = 0
            });
        }

        EditorUtility.SetDirty(registry);
        registry.Refresh();
    }
}

public static class BoundaryFeatureValidator
{
    [MenuItem("Boundary/Validate Feature")]
    public static void Validate()
    {
        GameObject director = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Boundary/BoundaryMatchDirector.prefab");
        GameObject hazard = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Boundary/BoundaryHazard.prefab");
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player.prefab");
        NetworkPrefabs registry = AssetDatabase.LoadAssetAtPath<NetworkPrefabs>("Assets/NetworkPrefabs.asset");

        Require(director != null, "Match director prefab is missing.");
        Require(director.GetComponent<NetworkIdentity>() != null, "Match director has no NetworkIdentity.");
        BoundaryMatchController controller = director.GetComponent<BoundaryMatchController>();
        Require(controller != null, "Match director has no BoundaryMatchController.");
        Require(director.GetComponent<BoundaryArenaPresentation>() != null, "Match director has no arena presentation.");
        Require(controller.OuterRadius >= 100f, "Outer ring is not meaningfully larger than the original arena.");
        Require(Mathf.Abs(controller.MiddleRadius - 68f) < 0.1f,
            "Middle ring must preserve the former outer-ring footprint.");
        Require(controller.InnerRadius >= 36f && controller.InnerRadius < controller.MiddleRadius,
            "Inner ring must remain large enough for combat.");
        Require(controller.OuterPlatformSurfaceY < controller.MiddlePlatformSurfaceY &&
                controller.MiddlePlatformSurfaceY < controller.InnerPlatformSurfaceY,
            "Platform tiers must rise toward the singularity.");
        Require(Mathf.Abs(controller.PlatformSurfaceYAtRadius(68f) - controller.MiddlePlatformSurfaceY) < 0.01f,
            "The former outer-ring footprint must use the raised middle platform tier.");

        Require(hazard != null, "Boundary hazard prefab is missing.");
        Require(hazard.GetComponent<BoundaryHazard>() != null, "Boundary hazard behavior is missing.");
        Require(hazard.GetComponent<Rigidbody>() != null, "Boundary hazard has no Rigidbody.");
        NetworkTransform hazardTransform = hazard.GetComponent<NetworkTransform>();
        Require(hazardTransform != null && !hazardTransform.ownerAuth,
            "Boundary hazard transform must be server-authoritative.");

        Require(player != null && player.GetComponent<BoundaryPlayerState>() != null,
            "Player prefab is missing its owner-authoritative Boundary state.");
        Require(player.GetComponent<PlayerMovement>() != null, "Player prefab has no movement component.");
        Require(registry != null, "Network prefab registry is missing.");
        Require(registry.prefabs.Any(entry => entry.prefab == director), "Director is not in NetworkPrefabs.");
        Require(registry.prefabs.Any(entry => entry.prefab == hazard), "Hazard is not in NetworkPrefabs.");

        float midpoint = BoundaryMath.TransitionRadius(106f, 68f, 3.5f, 7f);
        Require(Mathf.Abs(midpoint - 87f) < 0.001f, "Ring interpolation is not deterministic.");
        Vector3 airborne = BoundaryMath.PlayerPullAcceleration(
            Vector3.zero, new Vector3(0f, 32f, 0f), Vector3.zero, -1f, 106f, 5.5f, false);
        Vector3 grounded = BoundaryMath.PlayerPullAcceleration(
            Vector3.zero, new Vector3(0f, 32f, 0f), Vector3.zero, -1f, 106f, 5.5f, true);
        Require(grounded.magnitude < airborne.magnitude * 0.2f,
            "Stable footing must meaningfully reduce singularity pull.");
        Require(BoundaryMatchController.ArenaMassPopulation == 20,
            "The arena must begin with twenty interactive masses.");
        Require(BoundaryMatchController.ArenaMassInnerSurvivors * 4 ==
                BoundaryMatchController.ArenaMassPopulation,
            "Exactly one quarter of arena masses must reach the inner ring.");
        Require(System.Enum.IsDefined(typeof(BoundaryHazardKind), BoundaryHazardKind.ArenaBlackHole),
            "The arena black-hole sphere kind is missing.");

        int disasterCount = 0;
        foreach (BoundaryDisaster value in System.Enum.GetValues(typeof(BoundaryDisaster)))
        {
            if (value == BoundaryDisaster.None) continue;
            disasterCount++;
            Require(!string.IsNullOrEmpty(BoundaryMath.DisasterName(value)), value + " has no presentation name.");
            Require(!string.IsNullOrEmpty(BoundaryMath.DisasterHint(value)), value + " has no tactical hint.");
        }
        Require(disasterCount == 9, "Reverse Current must be removed and exactly nine disasters must remain.");

        Debug.Log("[BoundaryFeatureValidator] PASS — wall-jump arena, 20 masses, quarter survival, platform corruption, authority, and all 9 events validated.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new System.InvalidOperationException("[BoundaryFeatureValidator] " + message);
    }

    public static void BuildMacValidation()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "/tmp/boundary-mac-validation/Boundary.app",
            target = BuildTarget.StandaloneOSX,
            subtarget = (int)StandaloneBuildSubtarget.Player,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.InvalidOperationException(
                $"[BoundaryFeatureValidator] Mac build failed: {report.summary.result}, " +
                $"errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}");

        Debug.Log($"[BoundaryFeatureValidator] BUILD PASS — {report.summary.totalSize} bytes, " +
                  $"warnings={report.summary.totalWarnings}.");
    }
}

[InitializeOnLoad]
public static class BoundaryRuntimeSmokeRunner
{
    private const string SessionKey = "Boundary.RuntimeSmoke.Active";
    private const string ResultPath = "/tmp/boundary-runtime-smoke.txt";
    private static readonly List<string> RuntimeErrors = new List<string>();
    private static double enteredPlayAt;
    private static int stage;

    static BoundaryRuntimeSmokeRunner()
    {
        if (SessionState.GetBool(SessionKey, false))
            Hook();
    }

    public static void Run()
    {
        if (File.Exists(ResultPath))
            File.Delete(ResultPath);
        SessionState.SetBool(SessionKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
        Hook();
        EditorApplication.EnterPlaymode();
    }

    private static void Hook()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange stateChange)
    {
        if (!SessionState.GetBool(SessionKey, false))
            return;

        if (stateChange == PlayModeStateChange.EnteredPlayMode)
        {
            RuntimeErrors.Clear();
            Application.logMessageReceived += CaptureLog;
            enteredPlayAt = EditorApplication.timeSinceStartup;
            stage = 0;
            EditorApplication.update += TickPlayMode;
        }
        else if (stateChange == PlayModeStateChange.EnteredEditMode && File.Exists(ResultPath))
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.Exit(0);
        }
    }

    private static void TickPlayMode()
    {
        try
        {
            if (stage == 0)
            {
                GameObject directorPrefab = Resources.Load<GameObject>("Boundary/BoundaryMatchDirector");
                GameObject hazardPrefab = Resources.Load<GameObject>("Boundary/BoundaryHazard");
                RequireSmoke(directorPrefab != null && hazardPrefab != null, "Resources prefabs failed to load.");
                Object.Instantiate(directorPrefab, new Vector3(0f, 32f, 0f), Quaternion.identity);
                Object.Instantiate(hazardPrefab, new Vector3(0f, 8f, 0f), Quaternion.identity);
                if (Object.FindFirstObjectByType<BoundaryHUD>() == null)
                    new GameObject("Boundary HUD Smoke Instance").AddComponent<BoundaryHUD>();
                stage = 1;
                enteredPlayAt = EditorApplication.timeSinceStartup;
                return;
            }

            if (EditorApplication.timeSinceStartup - enteredPlayAt < 2.0d)
                return;

            BoundaryMatchController controller = Object.FindFirstObjectByType<BoundaryMatchController>();
            BoundaryHazard hazard = Object.FindFirstObjectByType<BoundaryHazard>();
            BoundaryHUD hud = Object.FindFirstObjectByType<BoundaryHUD>();
            GameObject generated = GameObject.Find("Boundary Generated Stadium");
            GameObject platformRoot = GameObject.Find("Breakaway Platforms");
            GameObject wallRoot = GameObject.Find("Wall Jump Structures");
            BoundaryArenaPresentation presentation = Object.FindFirstObjectByType<BoundaryArenaPresentation>();

            RequireSmoke(controller != null && BoundaryMatchController.Instance == controller,
                "Runtime director did not initialize.");
            RequireSmoke(hazard != null && hazard.transform.Find("CubeVisual") != null &&
                         hazard.transform.Find("SphereVisual") != null &&
                         hazard.transform.Find("BlackHoleAccretion") != null,
                "Runtime hazard visuals were not generated.");
            RequireSmoke(hud != null, "Boundary HUD was not installed in the Game scene.");
            RequireSmoke(generated != null && platformRoot != null && platformRoot.transform.childCount >= 300,
                "Generated breakaway platform floor is incomplete.");
            RequireSmoke(presentation != null && presentation.GeneratedPlatformCount >= 340 &&
                         presentation.LegacyArenaHidden && !presentation.HasSideWalls,
                "Open platform arena did not replace the legacy floor and side walls.");
            RequireSmoke(wallRoot != null && wallRoot.transform.childCount >= 30,
                "Purpose-built wall-jump structures were not generated.");
            foreach (Transform wall in wallRoot.transform)
                RequireSmoke(wall.CompareTag("Wall"), wall.name + " is not tagged for wall jumping.");
            RequireSmoke(GameObject.Find("Vertical Combat Routes") == null,
                "Random elevated stepping routes must not remain in the arena.");
            RequireSmoke(GameObject.Find("Brace") == null,
                "The removed Anchor control was still generated.");
            RequireSmoke(presentation.PreviewCollapseWarningForValidation() >= 20,
                "Breakaway platforms did not pulse dark while remaining collidable.");
            RequireSmoke(presentation.PreviewCollapseFlightForValidation() >= 80,
                "Breakaway platforms did not release and fly toward the singularity.");
            RequireSmoke(presentation.PreviewPlatformCorruptionForValidation() == 3,
                "Three black-hole contacts did not progressively darken and absorb a platform.");
            RequireSmoke(GameObject.Find("Event Horizon") != null, "Visible event horizon was not generated.");
            RequireSmoke(GameObject.Find("Hot Accretion Band") != null,
                "The upgraded central black-hole accretion disc was not generated.");
            RequireSmoke(GameObject.Find("White Photon Crown") != null &&
                         GameObject.Find("North Relativistic Jet") != null &&
                         GameObject.Find("Accretion Sparks") != null,
                "The upgraded photon crown, relativistic jets, or accretion particles were not generated.");
            RequireSmoke(RuntimeErrors.Count == 0, "Runtime errors: " + string.Join(" | ", RuntimeErrors));

            File.WriteAllText(ResultPath,
                "PASS\nOpen breakaway arena, wall-jump cover, upgraded black holes, event horizon, and Anchor-free HUD initialized without runtime errors.\n");
        }
        catch (System.Exception exception)
        {
            File.WriteAllText(ResultPath, "FAIL\n" + exception + "\n");
        }
        finally
        {
            if (File.Exists(ResultPath))
            {
                Application.logMessageReceived -= CaptureLog;
                EditorApplication.update -= TickPlayMode;
                EditorApplication.ExitPlaymode();
            }
        }
    }

    private static void CaptureLog(string condition, string stackTrace, LogType type)
    {
        bool featureLog = condition.Contains("[Boundary") ||
                          stackTrace.Contains("Assets/Scripts/Boundary") ||
                          stackTrace.Contains("BoundaryHUD") ||
                          stackTrace.Contains("BoundaryArenaPresentation");
        if (featureLog && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            RuntimeErrors.Add(condition);
    }

    private static void RequireSmoke(bool condition, string message)
    {
        if (!condition)
            throw new System.InvalidOperationException("[BoundaryRuntimeSmoke] " + message);
    }
}
#endif
