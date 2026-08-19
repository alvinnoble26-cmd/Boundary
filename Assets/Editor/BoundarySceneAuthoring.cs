using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BoundarySceneAuthoring
{
    internal const string ArenaPreviewSettingsSessionKey = "Boundary.ArenaPreviewSettings";
    private const string ArenaPreviewSnapshotFileName = "BoundaryArenaPreview.PlayMode.json";
    private const string ArenaAuthoringAssetPath = "Assets/Resources/Boundary/BoundaryArenaAuthoring.json";
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string MenuScenePath = "Assets/Scenes/Menu.unity";
    private const string PreviewName = "Boundary Arena Preview (Editor Only)";
    private static readonly string[] MenuPreviewRoots =
    {
        "SkinsPanel", "ControlLayoutEditor", "Edit ControlsButton"
    };
    private static bool refreshing;

    static BoundarySceneAuthoring()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneOpened += (_, __) => QueuePreviewRefresh();
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorApplication.hierarchyChanged += QueuePreviewRefresh;
        QueuePreviewRefresh();
    }

    [MenuItem("Boundary/Authoring/Refresh Edit Mode Arena Preview")]
    public static void RefreshPreviewMenu()
    {
        RefreshPreview();
    }

    [MenuItem("Boundary/Authoring/Rebuild Permanent Game HUD")]
    public static void RebuildPermanentHudMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Boundary Authoring] Exit Play Mode before rebuilding the permanent HUD.");
            return;
        }

        Scene gameScene = SceneManager.GetSceneByPath(GameScenePath);
        if (!gameScene.IsValid() || !gameScene.isLoaded)
            gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        EnsurePermanentHud(gameScene);
        EditorSceneManager.SaveScene(gameScene);
        Debug.Log("[Boundary Authoring] Permanent Game HUD rebuilt and saved.");
    }

    [MenuItem("Boundary/Authoring/Apply Play Layout To Saved Scenes")]
    public static void ApplyPlayLayoutToSavedScenesMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Boundary Authoring] Exit Play Mode before applying the Play layout.");
            return;
        }

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene menuScene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            MenuButtonTextAlignment alignment = FindInScene<MenuButtonTextAlignment>(menuScene);
            if (alignment != null)
            {
                alignment.ApplyAuthoringLayout();
                EditorUtility.SetDirty(alignment.gameObject);
            }

            AbilitiesSelectUI grapple = LoadoutManager.EnsureGrappleSelector();
            if (grapple != null)
                EditorUtility.SetDirty(grapple.gameObject);
            EditorSceneManager.MarkSceneDirty(menuScene);
            EditorSceneManager.SaveScene(menuScene);

            Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            EnsurePermanentHud(gameScene);
            Canvas gameCanvas = FindNamedInScene(gameScene, "Canvas")?.GetComponent<Canvas>();
            if (gameCanvas != null)
            {
                ControlLayoutSettings.ApplyToGameCanvas(gameCanvas);
                EditorUtility.SetDirty(gameCanvas.gameObject);
                EditorSceneManager.MarkSceneDirty(gameScene);
            }
            EditorSceneManager.SaveScene(gameScene);
            Debug.Log("[Boundary Authoring] Applied the Play layout to the saved Menu and Game scenes.");
        }
        finally
        {
            if (setup != null && setup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            QueuePreviewRefresh();
        }
    }

    private static bool EnsurePermanentHud(Scene gameScene)
    {
        if (!gameScene.IsValid() || !gameScene.isLoaded)
            return false;

        BoundaryHUD existing = FindInScene<BoundaryHUD>(gameScene);
        if (existing != null)
        {
            existing.BuildForEditorAuthoring();
            EditorUtility.SetDirty(existing.gameObject);
            return true;
        }

        GameObject hudObject = new GameObject("Boundary HUD");
        SceneManager.MoveGameObjectToScene(hudObject, gameScene);
        BoundaryHUD hud = hudObject.AddComponent<BoundaryHUD>();
        hud.BuildForEditorAuthoring();
        EditorUtility.SetDirty(hudObject);
        EditorSceneManager.MarkSceneDirty(gameScene);
        return true;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }
        return null;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            CaptureArenaPreviewSettings();
            DestroyPreviews();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
            QueuePreviewRefresh();
    }

    private static void CaptureArenaPreviewSettings()
    {
        SessionState.EraseString(ArenaPreviewSettingsSessionKey);

        string snapshotPath = GetArenaPreviewSnapshotPath();
        if (File.Exists(snapshotPath))
            File.Delete(snapshotPath);

        GameObject preview = GameObject.Find(PreviewName);
        BoundaryArenaPresentation presentation =
            preview != null ? preview.GetComponent<BoundaryArenaPresentation>() : null;
        if (presentation == null)
            return;

        string settings = presentation.CaptureEditorPreviewForPlayMode();
        File.WriteAllText(snapshotPath, settings);
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ArenaAuthoringAssetPath)), settings);
        AssetDatabase.ImportAsset(ArenaAuthoringAssetPath, ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"[Boundary Authoring] Captured {settings.Length} characters of saved arena overrides for Play Mode.");
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (scene.name != "Game")
            return;

        GameObject preview = FindNamedInScene(scene, PreviewName);
        BoundaryArenaPresentation presentation =
            preview != null ? preview.GetComponent<BoundaryArenaPresentation>() : null;
        if (presentation == null)
            return;

        string settings = presentation.CaptureEditorPreviewForPlayMode();
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ArenaAuthoringAssetPath)), settings);
        AssetDatabase.ImportAsset(ArenaAuthoringAssetPath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static string GetArenaPreviewSnapshotPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../Library", ArenaPreviewSnapshotFileName));
    }

    private static void QueuePreviewRefresh()
    {
        if (refreshing || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;
        refreshing = true;
        EditorApplication.delayCall += RefreshPreview;
    }

    private static void RefreshPreview()
    {
        refreshing = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            DestroyPreviews();
            return;
        }

        if (activeScene.name == "Menu")
        {
            DestroyArenaPreview();
            BuildMenuPreview(activeScene);
            return;
        }

        DestroyMenuPreview(activeScene);
        if (activeScene.name != "Game")
        {
            DestroyArenaPreview();
            return;
        }

        GameObject existingPreview = GameObject.Find(PreviewName);
        if (existingPreview != null)
        {
            if (MakeArenaPreviewSaveable(existingPreview.transform))
                EditorSceneManager.MarkSceneDirty(activeScene);
            return;
        }

        GameObject directorPrefab = Resources.Load<GameObject>("Boundary/BoundaryMatchDirector");
        if (directorPrefab == null)
            return;

        Vector3 center = ResolvePreviewCenter(activeScene);
        GameObject preview = PrefabUtility.InstantiatePrefab(directorPrefab, activeScene) as GameObject;
        if (preview == null)
            return;

        preview.name = PreviewName;
        preview.tag = "EditorOnly";
        preview.transform.position = center;
        // Keep the authoring hierarchy in the scene so designers can save and
        // continue editing it. The EditorOnly tag and DontSaveInBuild still
        // prevent this non-networked preview director from entering a build.
        preview.hideFlags = HideFlags.DontSaveInBuild;

        BoundaryArenaPresentation presentation = preview.GetComponent<BoundaryArenaPresentation>();
        if (presentation != null)
            presentation.BuildEditorPreview();

        ApplyPreviewFlags(preview.transform);
        SceneView.RepaintAll();
    }

    private static void BuildMenuPreview(Scene scene)
    {
        if (FindNamedInScene(scene, "SkinsPanel") != null)
            return;

        SkinShopUI.EnsureInstalled();

        Canvas canvas = FindInScene<Canvas>(scene);
        GameObject options = FindNamedInScene(scene, "OptionsMenu");
        if (canvas != null && options != null)
        {
            ControlLayoutEditorUI controls = canvas.GetComponent<ControlLayoutEditorUI>();
            if (controls == null)
                controls = canvas.gameObject.AddComponent<ControlLayoutEditorUI>();
            controls.Build(options);
        }

        MarkPreviewComponent(FindInScene<SkinShopUI>(scene));
        MarkPreviewComponent(FindInScene<ControlLayoutEditorUI>(scene));
        foreach (string rootName in MenuPreviewRoots)
        {
            GameObject root = FindNamedInScene(scene, rootName);
            if (root != null)
                ApplyPreviewFlags(root.transform);
        }
        Debug.Log("[Boundary Authoring] Built editor-only Menu UI preview.");
        SceneView.RepaintAll();
    }

    private static void MarkPreviewComponent(Component component)
    {
        if (component != null)
            component.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
    }

    private static GameObject FindNamedInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == objectName)
                    return candidate.gameObject;
        }
        return null;
    }

    private static Vector3 ResolvePreviewCenter(Scene scene)
    {
        BlackKill[] cores = Resources.FindObjectsOfTypeAll<BlackKill>();
        foreach (BlackKill core in cores)
        {
            if (core != null && core.gameObject.scene == scene)
                return core.transform.position;
        }
        return Vector3.zero;
    }

    private static void ApplyPreviewFlags(Transform root)
    {
        root.gameObject.hideFlags = HideFlags.DontSaveInBuild;
        for (int i = 0; i < root.childCount; i++)
            ApplyPreviewFlags(root.GetChild(i));
    }

    private static bool MakeArenaPreviewSaveable(Transform root)
    {
        bool changed = root.gameObject.hideFlags != HideFlags.DontSaveInBuild;
        root.gameObject.hideFlags = HideFlags.DontSaveInBuild;
        for (int i = 0; i < root.childCount; i++)
            changed |= MakeArenaPreviewSaveable(root.GetChild(i));
        return changed;
    }

    private static void DestroyPreviews()
    {
        DestroyArenaPreview();
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
            DestroyMenuPreview(activeScene);
    }

    private static void DestroyArenaPreview()
    {
        GameObject preview = GameObject.Find(PreviewName);
        if (preview != null)
            UnityEngine.Object.DestroyImmediate(preview);
    }

    private static void DestroyMenuPreview(Scene scene)
    {
        foreach (string rootName in MenuPreviewRoots)
        {
            foreach (GameObject root in FindAllNamedInScene(scene, rootName))
            {
                if ((root.hideFlags & HideFlags.DontSaveInBuild) != 0)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        DestroyPreviewComponent(FindInScene<SkinShopUI>(scene));
        DestroyPreviewComponent(FindInScene<ControlLayoutEditorUI>(scene));
    }

    private static List<GameObject> FindAllNamedInScene(Scene scene, string objectName)
    {
        var matches = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == objectName)
                    matches.Add(candidate.gameObject);
            }
        }
        return matches;
    }

    private static void DestroyPreviewComponent(Component component)
    {
        if (component != null && (component.hideFlags & HideFlags.DontSaveInBuild) != 0)
            UnityEngine.Object.DestroyImmediate(component);
    }
}
