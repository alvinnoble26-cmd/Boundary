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
    private const string DirectorPath = "Assets/Game/Resources/Boundary/BoundaryMatchDirector.prefab";
    private const string HazardPath = "Assets/Game/Resources/Boundary/BoundaryHazard.prefab";
    private const string PlayerPath = "Assets/Items/Models & Prefabs/Player.prefab";
    private const string RegistryPath = "Assets/Game/NetworkPrefabs.asset";

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
        if (!AssetDatabase.IsValidFolder("Assets/Game/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Game/Resources/Boundary"))
            AssetDatabase.CreateFolder("Assets/Game/Resources", "Boundary");
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
        GameObject director = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Resources/Boundary/BoundaryMatchDirector.prefab");
        GameObject hazard = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Resources/Boundary/BoundaryHazard.prefab");
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Items/Models & Prefabs/Player.prefab");
        GameObject attractPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Items/Models & Prefabs/GameElements/Attract.prefab");
        GameObject repelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Items/Models & Prefabs/GameElements/Repel.prefab");
        NetworkPrefabs registry = AssetDatabase.LoadAssetAtPath<NetworkPrefabs>("Assets/Game/NetworkPrefabs.asset");

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
        Cam playerCameraController = player.GetComponentInChildren<Cam>(true);
        Camera playerCamera = player.GetComponentInChildren<Camera>(true);
        Require(playerCameraController != null && playerCamera != null,
            "Player prefab is missing its owner-controlled first-person camera.");
        SerializedObject firstPersonCamera = new SerializedObject(playerCameraController);
        Require(firstPersonCamera.FindProperty("firstPersonEyeOffset") != null &&
                firstPersonCamera.FindProperty("minimumFirstPersonEyeHeight") != null &&
                firstPersonCamera.FindProperty("minimumFirstPersonEyeHeight").floatValue >= 0.7f &&
                firstPersonCamera.FindProperty("firstPersonMinPitch").floatValue <= -80f &&
                firstPersonCamera.FindProperty("firstPersonMaxPitch").floatValue >= 80f &&
                firstPersonCamera.FindProperty("firstPersonNearClip").floatValue <= 0.05f,
            "First-person eye pose, look range, or near clipping protection is not configured.");
        SerializedObject attractThrow = new SerializedObject(player.GetComponent<AttractThrow>());
        SerializedObject repelThrow = new SerializedObject(player.GetComponent<RepelThrow>());
        Require(attractThrow.FindProperty("launchHeightAbovePlayerCenter").floatValue >= 1.3f &&
                repelThrow.FindProperty("launchHeightAbovePlayerCenter").floatValue >= 1.3f,
            "Attract and Repel must launch above the player's center.");
        Require(attractThrow.FindProperty("attractionForce").floatValue > 0f &&
                attractThrow.FindProperty("fieldAcceleration").floatValue > 0f &&
                repelThrow.FindProperty("repulsionForce").floatValue > 0f &&
                repelThrow.FindProperty("fieldAcceleration").floatValue > 0f,
            "Attract and Repel must expose force and acceleration tuning fields.");
        Require(attractPrefab != null && repelPrefab != null,
            "Attract or Repel projectile prefab is missing.");
        SerializedObject attractField = new SerializedObject(attractPrefab.GetComponent<ForceField>());
        SerializedObject repelField = new SerializedObject(repelPrefab.GetComponent<ForceField>());
        Require(attractField.FindProperty("radius").floatValue >= 220f &&
                repelField.FindProperty("radius").floatValue >= 220f &&
                attractField.FindProperty("fieldForce").floatValue >= 220f &&
                repelField.FindProperty("fieldForce").floatValue >= 220f &&
                attractField.FindProperty("fieldAcceleration").floatValue >= 88f &&
                repelField.FindProperty("fieldAcceleration").floatValue >= 88f,
            "Attract and Repel must retain their expanded radius and tunable response.");
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
        Require(BoundaryMatchController.ArenaMassPopulation == 80 &&
                BoundaryMatchController.GroundArenaMassesPerKind == 22 &&
                BoundaryMatchController.FloatingArenaMassesPerKind == 18,
            "The arena must begin with 22 floor and 18 floating masses of each kind.");
        Require(BoundaryMatchController.PlatformHitsToCollapse == 5,
            "Arena platforms must withstand five cube or black-hole hits.");
        Require(Mathf.Approximately(BoundaryMatchController.HazardSizeMultiplier, 1.6f) &&
                Mathf.Approximately(BoundaryMatchController.ArenaMassCubeScale, 4.48f) &&
                Mathf.Approximately(BoundaryMatchController.ArenaMassBlackHoleScale, 2.8f) &&
                Mathf.Approximately(BoundaryMatchController.ScaleBoundaryHazard(2f), 3.2f) &&
                Mathf.Approximately(BoundaryMatchController.EventHazardSizeMultiplier, 1.5f) &&
                Mathf.Approximately(BoundaryMatchController.ScaleEventBoundaryHazard(2f), 4.8f),
            "Arena hazards must remain 1.6x enlarged and event hazards need the additional 1.5x scale.");
        Require(BoundaryMath.IsBelowVoidKillPlane(-5f, -0.9f, 4f),
            "The platform void must have a lethal fall plane.");
        Require(BoundaryMath.ArenaMassAbilityVelocityChange(1f) >= 88f,
            "Attract and Repel must decisively move arena masses.");
        Require(typeof(BoundaryHazard).GetMethod("ServerApplyArenaMassField") != null,
            "Arena masses need a direct field path that cannot be crowded out by platform colliders.");
        Require(BoundaryMath.BoundaryFallGravityMultiplier(1f) < 1f &&
                BoundaryMath.BoundaryFallGravityMultiplier(0f) <= 2.2f,
            "Downward gravity must ease smoothly near the singularity.");
        SerializedObject controllerSettings = new SerializedObject(controller);
        Require(controllerSettings.FindProperty("outerPull").floatValue <= 0.325f &&
                controllerSettings.FindProperty("middlePull").floatValue <= 1.05f &&
                controllerSettings.FindProperty("innerPull").floatValue <= 2.75f,
            "Boundary singularity gravity must remain at the reduced level.");
        Require(!BoundaryMath.IsLethalContactHazard(BoundaryHazardKind.Cube, true) &&
                !BoundaryMath.IsLethalContactHazard(BoundaryHazardKind.ArenaBlackHole, true),
            "Arena black cubes and black holes must use server-authoritative health damage.");
        Require(BoundaryMath.DensePlatformSpacing(9.2f, 0.4f) < 9.2f,
            "Platform colliders must overlap instead of leaving slide-breaking seams.");
        Require(BoundaryMath.TierRampSlopeDegrees(2.25f, 14f) < 10f,
            "Raised platform tiers must use a slideable transition slope.");
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
        Require(disasterCount == 8,
            "Reverse Current and False Singularities must be removed; exactly eight disasters must remain.");

        Debug.Log("[BoundaryFeatureValidator] PASS — first-person ownership/camera safety, seamless sliding floor, continuous tier ramps, lethal masses/void, floating walls, ability physics, corruption, authority, and all 8 events validated.");
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
        EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity", OpenSceneMode.Single);
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
            GameObject rampRoot = GameObject.Find("Tier Transition Ramps");
            BoundaryArenaPresentation presentation = Object.FindFirstObjectByType<BoundaryArenaPresentation>();

            RequireSmoke(controller != null && BoundaryMatchController.Instance == controller,
                "Runtime director did not initialize.");
            RequireSmoke(hazard != null && hazard.transform.Find("CubeVisual") != null &&
                         hazard.transform.Find("SphereVisual") != null &&
                         hazard.transform.Find("BlackHoleAccretion") != null,
                "Runtime hazard visuals were not generated.");
            RequireSmoke(hud != null, "Boundary HUD was not installed in the Game scene.");
            RectTransform crosshair = GameObject.Find("Aim Crosshair")?.transform as RectTransform;
            RequireSmoke(crosshair != null &&
                         crosshair.anchorMin == Vector2.one * 0.5f &&
                         crosshair.anchorMax == Vector2.one * 0.5f &&
                         crosshair.anchoredPosition == Vector2.zero &&
                         crosshair.Find("Horizontal") != null &&
                         crosshair.Find("Vertical") != null,
                "The white plus crosshair was not locked to the exact screen center.");
            RectTransform eventBanner = hud.transform.Find("Safe Area/Event Banner") as RectTransform;
            RequireSmoke(eventBanner != null &&
                         Mathf.Approximately(eventBanner.sizeDelta.x, BoundaryHUD.EventBannerWidth) &&
                         Mathf.Approximately(eventBanner.sizeDelta.y, BoundaryHUD.EventBannerHeight),
                "Boundary event UI did not use the compact banner layout.");
            RequireSmoke(generated != null && platformRoot != null && platformRoot.transform.childCount >= 300,
                "Generated breakaway platform floor is incomplete.");
            RequireSmoke(presentation != null && presentation.GeneratedPlatformCount >= 340 &&
                         presentation.LegacyArenaHidden && !presentation.HasSideWalls,
                "Open platform arena did not replace the legacy floor and side walls.");
            RequireSmoke(rampRoot != null && presentation.GeneratedTransitionRampCount >= 70 &&
                         rampRoot.transform.childCount == presentation.GeneratedTransitionRampCount,
                "Continuous slide ramps were not generated across both raised tier seams.");
            RequireSmoke(wallRoot != null && wallRoot.transform.childCount == 12,
                "The arena must generate five outer walls, seven middle walls, and no inner walls.");
            int substantiallyRaisedWalls = 0;
            int outerWalls = 0;
            int middleWalls = 0;
            foreach (Transform wall in wallRoot.transform)
            {
                RequireSmoke(wall.CompareTag("Wall"), wall.name + " is not tagged for wall jumping.");
                RequireSmoke(wall.localScale.x >= 10.8f && wall.localScale.y >= 7.7f &&
                             wall.localScale.z >= 1.18f,
                    wall.name + " is not enlarged across length, height, and thickness.");
                float tierSurface = wall.name.StartsWith("Wall 2")
                    ? controller.OuterPlatformSurfaceY
                    : controller.MiddlePlatformSurfaceY;
                RequireSmoke(!wall.name.StartsWith("Wall 0"),
                    "Inner-circle wall-jump cover must be completely removed.");
                if (wall.name.StartsWith("Wall 2")) outerWalls++;
                if (wall.name.StartsWith("Wall 1")) middleWalls++;
                float wallBottom = wall.position.y - wall.localScale.y * 0.5f;
                RequireSmoke(wallBottom >= tierSurface + 1.1f,
                    wall.name + " is not visibly suspended above its platform tier.");
                if (wallBottom >= tierSurface + 2.75f)
                    substantiallyRaisedWalls++;
                Vector3 towardCenter = (controller.ArenaCenter - wall.position).normalized;
                RequireSmoke(Vector3.Dot(wall.forward, towardCenter) >= 0.999f,
                    wall.name + " does not point toward the arena center on all axes.");
            }
            RequireSmoke(outerWalls == 5 && middleWalls == 7,
                "Outer wall cover was not reduced by approximately 25 percent.");
            RequireSmoke(substantiallyRaisedWalls >= 4,
                "The randomized wall field did not create enough elevated wall-jump routes.");
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
                "PASS\nDense overlapping floor, continuous tier ramps, wall-jump cover, upgraded black holes, and event horizon initialized without Boundary runtime errors.\n");
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
                          stackTrace.Contains("Assets/Game/Scripts/Boundary") ||
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
