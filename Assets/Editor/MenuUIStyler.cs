#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuUIStyler
{
    private const string ScenePath = "Assets/Game/Scenes/Menu.unity";
    private const string UiRoot = "Assets/Game/UI";
    private const string ResourcesRoot = UiRoot + "/Resources/UI";
    private const string GeneratedRoot = UiRoot + "/Generated";
    private const string PrefabRoot = UiRoot + "/Prefabs";
    private const string ThemePath = ResourcesRoot + "/UITheme.asset";
    private const string ExistingFontPath = "Assets/Items/Models & Prefabs/GameElements/Button/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Entropy Zero/UI/Build Style Kit")]
    public static void BuildStyleKit()
    {
        EnsureFolders();
        Sprite fill = GenerateRoundedSprite("RoundedFill", SpriteKind.Fill);
        Sprite border = GenerateRoundedSprite("RoundedBorder", SpriteKind.Border);
        Sprite shadow = GenerateRoundedSprite("SoftShadow", SpriteKind.Shadow);
        Sprite glow = GenerateRoundedSprite("AccentGlow", SpriteKind.Glow);
        Sprite horizon = GenerateHorizonSprite();

        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UITheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        theme.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ExistingFontPath);
        const string fontMaterialPath = GeneratedRoot + "/EntropyZeroFont.mat";
        Material fontMaterial = AssetDatabase.LoadAssetAtPath<Material>(fontMaterialPath);
        if (fontMaterial == null && theme.font != null)
        {
            fontMaterial = new Material(theme.font.material) { name = "EntropyZeroFont" };
            fontMaterial.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
            fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.clear);
            fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
            AssetDatabase.CreateAsset(fontMaterial, fontMaterialPath);
        }
        theme.fontMaterial = fontMaterial;
        theme.titleSize = 110f;
        theme.headerSize = 56f;
        theme.bodySize = 32f;
        theme.captionSize = 24f;
        theme.buttonSize = 40f;
        theme.titleCharacterSpacing = 9f;
        theme.roundedFill = fill;
        theme.roundedBorder = border;
        theme.softShadow = shadow;
        theme.accentGlow = glow;
        theme.spaceHorizon = horizon;
        EditorUtility.SetDirty(theme);
        CreatePrefabs(theme);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MenuUIStyler] Style kit built.");
    }

    [MenuItem("Entropy Zero/UI/Apply Shared Foundation")]
    public static void ApplySharedFoundation()
    {
        BuildStyleKit();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        Canvas canvas = FindSceneObject<Canvas>(scene, "Canvas");
        if (canvas == null) throw new InvalidOperationException("Menu Canvas was not found.");
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
        if (canvas.GetComponent<ResponsiveCanvasMatch>() == null)
            Undo.AddComponent<ResponsiveCanvasMatch>(canvas.gameObject);
        StyleGlobalPanel(canvas.transform.Find("Panel"), canvas.transform, theme);
        string[] roots = { "MainMenu", "StartMenu", "MuiltiplayerMenu", "JoinLobbyPanel", "HostLobbyPanel", "Lost", "Won", "OptionsMenu", "AbilitiesMenu", "Ability Information Panel", "ControlLayoutEditor" };
        foreach (string rootName in roots)
        {
            Transform root = canvas.transform.Find(rootName);
            if (root != null) EnsureSafeArea(root);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    public static void BatchApplyFoundation()
    {
        ApplySharedFoundation();
        EditorApplication.Exit(0);
    }

    [MenuItem("Entropy Zero/UI/Apply Core Menu Panels")]
    public static void ApplyCorePanels()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        Canvas canvas = FindSceneObject<Canvas>(scene, "Canvas");
        Transform main = canvas.transform.Find("MainMenu");
        Transform start = canvas.transform.Find("StartMenu");
        Transform multiplayer = canvas.transform.Find("MuiltiplayerMenu");
        Transform options = canvas.transform.Find("OptionsMenu");
        StyleMainMenu(main, start, theme);
        StyleStartMenu(start, theme);
        StyleMultiplayerMenu(multiplayer, theme);
        StyleOptionsMenu(options, theme);
        foreach (Transform root in new[] { main, start, multiplayer, options })
        {
            EnsureSafeArea(root);
            EnsurePanelAnimator(root);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    public static void BatchApplyCorePanels()
    {
        ApplyCorePanels();
        EditorApplication.Exit(0);
    }

    [MenuItem("Entropy Zero/UI/Apply Extended Menu Panels")]
    public static void ApplyExtendedPanels()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        Canvas canvas = FindSceneObject<Canvas>(scene, "Canvas");
        Transform abilities = canvas.transform.Find("AbilitiesMenu");
        if (abilities != null) StyleAbilitiesMenu(abilities, theme);
        AbilityInformationUI information = abilities != null ? abilities.GetComponent<AbilityInformationUI>() : null;
        if (information != null) information.EnsureBuilt();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    public static void BatchApplyExtendedPanels()
    {
        ApplyExtendedPanels();
        EditorApplication.Exit(0);
    }

    [MenuItem("Entropy Zero/UI/Apply Lobby and Result Panels")]
    public static void ApplyLobbyAndResults()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        Canvas canvas = FindSceneObject<Canvas>(scene, "Canvas");
        FirebaseLobbyManager firebase = canvas.GetComponent<FirebaseLobbyManager>();
        SerializedObject serializedFirebase = firebase != null ? new SerializedObject(firebase) : null;
        TMP_InputField codeInput = serializedFirebase?.FindProperty("codeInput").objectReferenceValue as TMP_InputField;
        TMP_Text hostCode = serializedFirebase?.FindProperty("hostCodeText").objectReferenceValue as TMP_Text;
        Transform joinPanel = canvas.transform.Find("JoinLobbyPanel");
        Transform hostPanel = canvas.transform.Find("HostLobbyPanel");
        if (codeInput == null && joinPanel != null) codeInput = joinPanel.GetComponentInChildren<TMP_InputField>(true);
        if (hostCode == null && hostPanel != null)
            hostCode = hostPanel.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.GetComponentInParent<Button>() == null);
        StyleJoinPanel(joinPanel, codeInput, theme);
        StyleHostPanel(hostPanel, hostCode, theme);

        MenuUIController controller = canvas.GetComponent<MenuUIController>();
        Transform lost = canvas.transform.Find("Lost");
        Transform won = canvas.transform.Find("Won");
        TMP_Text loseReason = StyleResultPanel(lost, false, controller, theme);
        TMP_Text winReason = StyleResultPanel(won, true, controller, theme);
        if (controller != null)
        {
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("loseReasonText").objectReferenceValue = loseReason;
            serializedController.FindProperty("winReasonText").objectReferenceValue = winReason;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }
        RepairMenuLobbyReferences(canvas, canvas.transform.Find("MainMenu"), canvas.transform.Find("HostLobbyPanel"));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    public static void BatchApplyLobbyAndResults()
    {
        ApplyLobbyAndResults();
        EditorApplication.Exit(0);
    }

    [MenuItem("Entropy Zero/UI/Apply First Batch to Menu")]
    public static void ApplyFirstBatch()
    {
        BuildStyleKit();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        Canvas canvas = FindSceneObject<Canvas>(scene, "Canvas");
        if (canvas == null) throw new InvalidOperationException("Menu Canvas was not found.");

        Transform panel = canvas.transform.Find("Panel");
        Transform main = canvas.transform.Find("MainMenu");
        Transform start = canvas.transform.Find("StartMenu");
        Transform multiplayer = canvas.transform.Find("MuiltiplayerMenu");
        Validate(main, "MainMenu");
        Validate(start, "StartMenu");
        Validate(multiplayer, "MuiltiplayerMenu");

        StyleGlobalPanel(panel, canvas.transform, theme);
        StyleMainMenu(main, start, theme);
        StyleStartMenu(start, theme);
        StyleMultiplayerMenu(multiplayer, theme);
        EnsureSafeArea(main);
        EnsureSafeArea(start);
        EnsureSafeArea(multiplayer);
        EnsurePanelAnimator(main);
        EnsurePanelAnimator(start);
        EnsurePanelAnimator(multiplayer);
        RepairMenuLobbyReferences(canvas, main, canvas.transform.Find("HostLobbyPanel"));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[MenuUIStyler] First batch applied to Menu scene without replacing serialized objects.");
    }

    public static void BatchApply()
    {
        ApplyFirstBatch();
        EditorApplication.Exit(0);
    }

    public static void BatchApplyGlobal()
    {
        BuildStyleKit();
        ApplySelected(true, false, false, false);
        EditorApplication.Exit(0);
    }

    public static void BatchApplyMain()
    {
        ApplySelected(false, true, false, false);
        EditorApplication.Exit(0);
    }

    public static void BatchApplyStart()
    {
        ApplySelected(false, false, true, false);
        EditorApplication.Exit(0);
    }

    public static void BatchApplyMultiplayer()
    {
        ApplySelected(false, false, false, true);
        EditorApplication.Exit(0);
    }

    private static void ApplySelected(bool global, bool mainMenu, bool startMenu, bool multiplayerMenu)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        Canvas canvas = FindSceneObject<Canvas>(scene, "Canvas");
        if (canvas == null || theme == null)
            throw new InvalidOperationException("Canvas or UITheme is missing.");
        Transform main = canvas.transform.Find("MainMenu");
        Transform start = canvas.transform.Find("StartMenu");
        Transform multiplayer = canvas.transform.Find("MuiltiplayerMenu");
        if (global)
        {
            StyleGlobalPanel(canvas.transform.Find("Panel"), canvas.transform, theme);
            RepairMenuLobbyReferences(canvas, main, canvas.transform.Find("HostLobbyPanel"));
        }
        if (mainMenu)
        {
            StyleMainMenu(main, start, theme);
            EnsureSafeArea(main);
            EnsurePanelAnimator(main);
        }
        if (startMenu)
        {
            StyleStartMenu(start, theme);
            EnsureSafeArea(start);
            EnsurePanelAnimator(start);
        }
        if (multiplayerMenu)
        {
            StyleMultiplayerMenu(multiplayer, theme);
            EnsureSafeArea(multiplayer);
            EnsurePanelAnimator(multiplayer);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Game", "UI");
        EnsureFolder(UiRoot, "Resources");
        EnsureFolder(UiRoot + "/Resources", "UI");
        EnsureFolder(UiRoot, "Generated");
        EnsureFolder(UiRoot, "Prefabs");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private enum SpriteKind { Fill, Border, Shadow, Glow }

    private static Sprite GenerateRoundedSprite(string name, SpriteKind kind)
    {
        const int size = 64;
        const float radius = 18f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = name;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
            float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float inside = 1f - Smooth01(radius - 1.5f, radius + 0.5f, distance);
            float inner = 1f - Smooth01(radius - 3.5f, radius - 1.5f, distance);
            float alpha = kind switch
            {
                SpriteKind.Fill => inside,
                SpriteKind.Border => Mathf.Clamp01(inside - inner),
                SpriteKind.Shadow => inside * 0.28f,
                SpriteKind.Glow => inside * 0.42f,
                _ => inside
            };
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();
        string path = $"{GeneratedRoot}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = new Vector4(20f, 20f, 20f, 20f);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static float Smooth01(float edge0, float edge1, float value)
    {
        float t = Mathf.InverseLerp(edge0, edge1, value);
        return t * t * (3f - 2f * t);
    }

    private static Sprite GenerateHorizonSprite()
    {
        const int width = 256;
        const int height = 128;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float nx = (x + 0.5f - width * 0.5f) / (width * 0.5f);
            float ny = y / (float)(height - 1);
            float alpha = Mathf.Exp(-nx * nx * 2.1f) * Mathf.Pow(1f - ny, 2.4f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();
        string path = GeneratedRoot + "/SpaceHorizon.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void CreatePrefabs(UITheme theme)
    {
        SaveButtonPrefab("PrimaryButton", "PRIMARY ACTION", theme, true);
        SaveButtonPrefab("SecondaryButton", "SECONDARY", theme, false);
        SaveImagePrefab("Card", theme.roundedFill, theme.raisedPanel, new Vector2(420f, 240f), theme);
        SaveImagePrefab("PanelBackground", theme.roundedFill, theme.panel, new Vector2(920f, 720f), theme);
        SaveCodeDigitPrefab(theme);
        SaveInputFieldPrefab(theme);
    }

    private static void SaveButtonPrefab(string name, string label, UITheme theme, bool primary)
    {
        GameObject root = NewUiObject(name, typeof(Image), typeof(Button), typeof(UIAnimator));
        RectTransform rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(primary ? 440f : 320f, primary ? 112f : 88f);
        Image image = root.GetComponent<Image>();
        image.sprite = theme.roundedFill;
        image.type = Image.Type.Sliced;
        image.color = primary ? theme.accent : theme.raisedPanel;
        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;
        SetButtonColors(button, primary ? theme.accent : theme.raisedPanel, theme);
        root.GetComponent<UIAnimator>().ConfigureButton(primary);
        CreateLabel(root.transform, "Label", label, theme.bodySize, theme, primary ? theme.background : theme.text);
        SavePrefab(root, name);
    }

    private static void SaveImagePrefab(string name, Sprite sprite, Color color, Vector2 size, UITheme theme)
    {
        GameObject root = NewUiObject(name, typeof(Image));
        ((RectTransform)root.transform).sizeDelta = size;
        Image image = root.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        AddBorder(root.transform, theme);
        SavePrefab(root, name);
    }

    private static void SaveCodeDigitPrefab(UITheme theme)
    {
        GameObject root = NewUiObject("CodeDigitBox", typeof(Image));
        ((RectTransform)root.transform).sizeDelta = new Vector2(112f, 128f);
        Image image = root.GetComponent<Image>();
        image.sprite = theme.roundedFill;
        image.type = Image.Type.Sliced;
        image.color = theme.raisedPanel;
        CreateLabel(root.transform, "Digit", "0", theme.headerSize, theme, theme.text);
        AddBorder(root.transform, theme);
        SavePrefab(root, "CodeDigitBox");
    }

    private static void SaveInputFieldPrefab(UITheme theme)
    {
        GameObject root = NewUiObject("InputField", typeof(Image), typeof(TMP_InputField));
        ((RectTransform)root.transform).sizeDelta = new Vector2(560f, 88f);
        Image image = root.GetComponent<Image>();
        image.sprite = theme.roundedFill;
        image.type = Image.Type.Sliced;
        image.color = theme.raisedPanel;
        GameObject area = NewUiObject("Text Area", typeof(RectMask2D));
        area.transform.SetParent(root.transform, false);
        Stretch((RectTransform)area.transform, 24f);
        TMP_Text text = CreateLabel(area.transform, "Text", "", theme.bodySize, theme, theme.text);
        TMP_Text placeholder = CreateLabel(area.transform, "Placeholder", "ENTER CODE", theme.bodySize, theme, theme.muted);
        placeholder.fontStyle = FontStyles.Italic;
        TMP_InputField input = root.GetComponent<TMP_InputField>();
        input.textViewport = (RectTransform)area.transform;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = image;
        SavePrefab(root, "InputField");
    }

    private static void SavePrefab(GameObject root, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{name}.prefab");
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static void StyleGlobalPanel(Transform panel, Transform canvas, UITheme theme)
    {
        if (panel == null) return;
        panel.SetAsFirstSibling();
        Image background = panel.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = theme.roundedFill;
            background.type = Image.Type.Sliced;
            background.color = theme.background;
            background.raycastTarget = false;
        }
        RectTransform rect = (RectTransform)panel;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Transform oldDecoration = panel.Find("ContainedInstability");
        if (oldDecoration != null)
            UnityEngine.Object.DestroyImmediate(oldDecoration.gameObject);
        Transform decoration = panel.Find("SpaceBackground");
        if (decoration != null)
        {
            UnityEngine.Object.DestroyImmediate(decoration.gameObject);
            decoration = null;
        }
        if (decoration == null)
        {
            GameObject root = NewUiObject("SpaceBackground", typeof(SpaceBackground));
            root.transform.SetParent(panel, false);
            Stretch((RectTransform)root.transform, 0f);
            GameObject stars = NewUiObject("StarField");
            stars.transform.SetParent(root.transform, false);
            Stretch((RectTransform)stars.transform, -40f);
            UnityEngine.Random.State previousState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(40719);
            for (int i = 0; i < 54; i++)
            {
                GameObject star = NewUiObject("Star", typeof(Image));
                star.transform.SetParent(stars.transform, false);
                RectTransform starRect = (RectTransform)star.transform;
                starRect.anchorMin = starRect.anchorMax = new Vector2(UnityEngine.Random.value, UnityEngine.Random.value);
                float size = UnityEngine.Random.Range(2f, 5f);
                starRect.sizeDelta = new Vector2(size, size);
                Image starImage = star.GetComponent<Image>();
                starImage.sprite = theme.roundedFill;
                starImage.color = new Color(theme.text.r, theme.text.g, theme.text.b, UnityEngine.Random.Range(0.12f, 0.48f));
                starImage.raycastTarget = false;
            }
            UnityEngine.Random.state = previousState;
            GameObject horizon = NewUiObject("HorizonGlow", typeof(Image));
            horizon.transform.SetParent(root.transform, false);
            RectTransform horizonRect = (RectTransform)horizon.transform;
            horizonRect.anchorMin = horizonRect.anchorMax = new Vector2(0.5f, 0f);
            horizonRect.pivot = new Vector2(0.5f, 0f);
            horizonRect.anchoredPosition = new Vector2(0f, -170f);
            horizonRect.sizeDelta = new Vector2(2500f, 520f);
            Image glow = horizon.GetComponent<Image>();
            glow.sprite = theme.spaceHorizon;
            glow.type = Image.Type.Simple;
            glow.color = new Color(theme.accent.r, theme.accent.g, theme.accent.b, 0.32f);
            glow.raycastTarget = false;
            root.GetComponent<SpaceBackground>().Configure((RectTransform)stars.transform, horizonRect, glow);
        }
    }

    private static Image CreateDecorativeArc(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject go = NewUiObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath).roundedBorder;
        image.type = Image.Type.Sliced;
        image.color = new Color(color.r, color.g, color.b, 0.05f);
        image.raycastTarget = false;
        go.GetComponent<RectTransform>().sizeDelta = size;
        return image;
    }

    private static void StyleMainMenu(Transform main, Transform start, UITheme theme)
    {
        SetFullScreen(main);
        EnsureHeading(main, "BoundaryLogo", "ENTROPY ZERO", "CONTAIN THE INSTABILITY", new Vector2(72f, -70f), theme);
        Button play = RequireButton(main, "PlayButton");
        StyleButton(play, true, theme);
        SetLabel(play, "PLAY", theme, theme.buttonSize);
        SetRect(play.transform as RectTransform, new Vector2(1f, 0f), new Vector2(-72f, 72f), new Vector2(520f, 128f), new Vector2(1f, 0f));

        Button options = RequireButton(main, "OptionsButton");
        StyleButton(options, false, theme);
        SetRect(options.transform as RectTransform, new Vector2(1f, 0f), new Vector2(-72f, 224f), new Vector2(248f, 88f), new Vector2(1f, 0f));
        SetLabel(options, "SETTINGS", theme);

        Transform skin = start.Find("SkinButton");
        if (skin != null && skin.parent != main)
            Undo.SetTransformParent(skin, main, "Move existing SkinButton into main menu secondary row");
        if (skin != null)
        {
            Button skinButton = skin.GetComponent<Button>();
            StyleButton(skinButton, false, theme);
            SetRect((RectTransform)skin, new Vector2(1f, 0f), new Vector2(-344f, 224f), new Vector2(248f, 88f), new Vector2(1f, 0f));
            SetLabel(skinButton, "SKINS", theme);
        }
    }

    private static void StyleStartMenu(Transform start, UITheme theme)
    {
        SetFullScreen(start);
        EnsureHeading(start, "StartHeading", "CHOOSE LOADOUT", "CONFIGURE, THEN ENTER THE ARENA", new Vector2(72f, -70f), theme);
        SetSubscreenHeading(start.Find("StartHeading"), theme);
        Button server = RequireButton(start, "ServerSelectorButton");
        Button abilities = RequireButton(start, "AbilitiesButton");
        Button back = RequireButton(start, "BackButton (1)");
        StyleButton(server, true, theme);
        StyleButton(abilities, false, theme);
        StyleButton(back, false, theme);
        SetLabel(server, "MULTIPLAYER", theme);
        SetLabel(abilities, "ABILITIES", theme);
        SetLabel(back, "BACK", theme);
        SetRect(server.transform as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(640f, 120f));
        SetRect(abilities.transform as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(640f, 96f));
        SetRect(back.transform as RectTransform, new Vector2(0f, 0f), new Vector2(48f, 48f), new Vector2(200f, 88f), Vector2.zero);
    }

    private static void StyleMultiplayerMenu(Transform menu, UITheme theme)
    {
        SetFullScreen(menu);
        EnsureHeading(menu, "MultiplayerHeading", "MULTIPLAYER", "CREATE A SPACE OR ENTER A FOUR-DIGIT CODE", new Vector2(72f, -70f), theme);
        SetSubscreenHeading(menu.Find("MultiplayerHeading"), theme);
        Button host = RequireButton(menu, "HOST");
        Button join = RequireButton(menu, "JOIN");
        Button practice = RequireButton(menu, "PracticeButton");
        Button back = RequireButton(menu, "BackButton (4)");
        StyleCardAction(host, "CREATE LOBBY", "OPEN A PRIVATE TWO-PLAYER ARENA", theme, true);
        StyleCardAction(join, "JOIN WITH CODE", "ENTER AN EXISTING FOUR-DIGIT LOBBY", theme, false);
        SetRect(host.transform as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(-350f, 10f), new Vector2(600f, 300f));
        SetRect(join.transform as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(350f, 10f), new Vector2(600f, 300f));
        StyleButton(practice, false, theme);
        StyleButton(back, false, theme);
        SetLabel(practice, "PRACTICE", theme);
        SetLabel(back, "BACK", theme);
        SetRect(practice.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(120f, 54f), new Vector2(280f, 88f));
        SetRect(back.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(-180f, 54f), new Vector2(240f, 88f));
    }

    private static void StyleOptionsMenu(Transform menu, UITheme theme)
    {
        if (menu == null) return;
        SetFullScreen(menu);
        EnsureHeading(menu, "OptionsHeading", "OPTIONS", "AUDIO, CONTROLS, AND ACCESSIBILITY", new Vector2(72f, -70f), theme);
        SetSubscreenHeading(menu.Find("OptionsHeading"), theme);
        Transform card = menu.Find("StyleOptionsCard");
        if (card == null)
        {
            GameObject cardObject = NewUiObject("StyleOptionsCard", typeof(Image));
            cardObject.transform.SetParent(menu, false);
            card = cardObject.transform;
            card.SetAsFirstSibling();
        }
        Image cardImage = card.GetComponent<Image>();
        cardImage.sprite = theme.roundedFill;
        cardImage.type = Image.Type.Sliced;
        cardImage.color = theme.panel;
        cardImage.raycastTarget = false;
        SetRect((RectTransform)card, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(760f, 720f));
        AddBorder(card, theme);

        EnsureOptionsLabel(menu, "VolumeLabel", "MASTER VOLUME", new Vector2(0f, 270f), theme);
        EnsureOptionsLabel(menu, "AccessibilityLabel", "ACCESSIBILITY", new Vector2(0f, 135f), theme);

        Button[] buttons = menu.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            StyleButton(button, false, theme);
            SetRect((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -290f), new Vector2(240f, 88f));
            SetLabel(button, "BACK", theme, theme.buttonSize);
        }
        Slider slider = menu.GetComponentInChildren<Slider>(true);
        if (slider != null)
            SetRect((RectTransform)slider.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 205f), new Vector2(560f, 56f));
        Toggle toggle = menu.GetComponentInChildren<Toggle>(true);
        if (toggle != null)
            SetRect((RectTransform)toggle.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 82f), new Vector2(560f, 88f));
        foreach (TMP_Text text in menu.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.transform.IsChildOf(menu.Find("OptionsHeading"))) continue;
            text.font = theme.font;
            text.color = theme.text;
            text.fontSize = text.GetComponentInParent<Button>() != null ? theme.buttonSize : theme.bodySize;
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = false;
        }
    }

    private static void StyleAbilitiesMenu(Transform menu, UITheme theme)
    {
        SetFullScreen(menu);
        EnsureHeading(menu, "AbilitiesHeading", "ABILITIES", "SELECT THREE TO BUILD YOUR LOADOUT", new Vector2(72f, -70f), theme);
        SetSubscreenHeading(menu.Find("AbilitiesHeading"), theme);
        Button[] buttons = menu.GetComponentsInChildren<Button>(true);
        int cardIndex = 0;
        foreach (Button button in buttons)
        {
            bool navigation = button.name.Contains("Back", StringComparison.OrdinalIgnoreCase) ||
                              button.name.Contains("Info", StringComparison.OrdinalIgnoreCase);
            StyleButton(button, false, theme);
            if (navigation)
            {
                float x = button.name.Contains("Info", StringComparison.OrdinalIgnoreCase) ? 300f : 72f;
                SetRect((RectTransform)button.transform, Vector2.zero, new Vector2(x, 48f), new Vector2(210f, 88f), Vector2.zero);
                continue;
            }
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = label.text.ToUpperInvariant();
            int column = cardIndex % 4;
            int row = cardIndex / 4;
            SetRect((RectTransform)button.transform, new Vector2(0.5f, 0.5f),
                new Vector2((column - 1.5f) * 330f, 165f - row * 150f), new Vector2(300f, 120f));
            cardIndex++;
        }
        EnsureSafeArea(menu);
        EnsurePanelAnimator(menu);
    }

    private static void StyleJoinPanel(Transform panel, TMP_InputField input, UITheme theme)
    {
        if (panel == null || input == null) return;
        SetFullScreen(panel);
        EnsureHeading(panel, "JoinHeading", "JOIN WITH CODE", "ENTER THE FOUR-DIGIT INVITATION", new Vector2(72f, -70f), theme);
        SetSubscreenHeading(panel.Find("JoinHeading"), theme);
        Transform row = panel.Find("CodeDigitRow");
        if (row == null)
        {
            GameObject rowObject = NewUiObject("CodeDigitRow", typeof(Image), typeof(JoinCodePresentation));
            rowObject.transform.SetParent(panel, false);
            row = rowObject.transform;
            Image hit = rowObject.GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            TMP_Text[] digits = new TMP_Text[4];
            Image[] borders = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject box = NewUiObject("Digit " + (i + 1), typeof(Image));
                box.transform.SetParent(row, false);
                SetRect((RectTransform)box.transform, new Vector2(0.5f, 0.5f), new Vector2((i - 1.5f) * 144f, 0f), new Vector2(112f, 128f));
                Image image = box.GetComponent<Image>();
                image.sprite = theme.roundedBorder;
                image.type = Image.Type.Sliced;
                image.color = theme.border;
                image.raycastTarget = false;
                digits[i] = CreateLabel(box.transform, "Digit", "—", theme.headerSize, theme, theme.text);
                borders[i] = image;
            }
            rowObject.GetComponent<JoinCodePresentation>().Configure(input, digits, borders);
        }
        SetRect((RectTransform)row, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(600f, 150f));
        input.characterLimit = 4;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        RectTransform inputRect = input.transform as RectTransform;
        SetRect(inputRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(600f, 150f));
        Image inputImage = input.GetComponent<Image>();
        if (inputImage != null) inputImage.color = new Color(1f, 1f, 1f, 0.001f);
        if (input.textComponent != null) input.textComponent.color = Color.clear;
        if (input.placeholder is TMP_Text placeholder) placeholder.color = Color.clear;
        input.transform.SetAsLastSibling();
        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
            if (text.name.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0 || text.text.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                UIStyle.ApplyText(text, theme.captionSize, theme.danger, FontStyles.Bold);
        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            StyleButton(button, false, theme);
            SetRect((RectTransform)button.transform, Vector2.zero, new Vector2(72f, 48f), new Vector2(220f, 88f), Vector2.zero);
        }
        EnsureSafeArea(panel);
        EnsurePanelAnimator(panel);
    }

    private static void StyleHostPanel(Transform panel, TMP_Text code, UITheme theme)
    {
        if (panel == null || code == null) return;
        SetFullScreen(panel);
        EnsureHeading(panel, "HostHeading", "LOBBY", "SHARE THE CODE WITH YOUR OPPONENT", new Vector2(72f, -70f), theme);
        SetSubscreenHeading(panel.Find("HostHeading"), theme);
        UIStyle.ApplyText(code, 104f, theme.accent, FontStyles.Bold);
        code.characterSpacing = 18f;
        SetRect(code.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 135f), new Vector2(720f, 140f));
        LobbyPresentation presentation = panel.GetComponent<LobbyPresentation>();
        if (presentation == null) presentation = panel.gameObject.AddComponent<LobbyPresentation>();
        Transform content = panel.Find("LobbyPresentationContent");
        if (content == null)
        {
            content = NewUiObject("LobbyPresentationContent").transform;
            content.SetParent(panel, false);
            SetFullScreen(content);
        }
        TMP_Text status = GetOrCreateLabel(content, "Status", "WAITING FOR OPPONENT…", theme.bodySize, theme, theme.muted);
        SetRect(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(720f, 70f));
        Transform waiting = content.Find("WaitingRadar");
        if (waiting == null)
        {
            GameObject radar = NewUiObject("WaitingRadar", typeof(Image));
            radar.transform.SetParent(content, false);
            waiting = radar.transform;
            Image radarImage = radar.GetComponent<Image>();
            radarImage.sprite = theme.roundedBorder;
            radarImage.type = Image.Type.Sliced;
            radarImage.color = new Color(theme.accent.r, theme.accent.g, theme.accent.b, 0.48f);
            radarImage.raycastTarget = false;
        }
        SetRect((RectTransform)waiting, new Vector2(0.5f, 0.5f), new Vector2(0f, -145f), new Vector2(180f, 180f));
        Transform opponent = content.Find("OpponentJoined");
        if (opponent == null)
        {
            opponent = NewUiObject("OpponentJoined").transform;
            opponent.SetParent(content, false);
            CreateLabel(opponent, "You", "YOU", theme.headerSize, theme, theme.text);
            CreateLabel(opponent, "VS", "VS", theme.bodySize, theme, theme.accent);
            CreateLabel(opponent, "Opponent", "OPPONENT", theme.headerSize, theme, theme.text);
        }
        SetRect((RectTransform)opponent, new Vector2(0.5f, 0.5f), new Vector2(0f, -145f), new Vector2(900f, 160f));
        TMP_Text[] versus = opponent.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < versus.Length; i++)
            SetRect(versus[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 300f, 0f), new Vector2(260f, 100f));
        GameObject progressObject = content.Find("Progress")?.gameObject;
        if (progressObject == null)
        {
            progressObject = NewUiObject("Progress", typeof(Image));
            progressObject.transform.SetParent(content, false);
        }
        Image progress = progressObject.GetComponent<Image>();
        progress.sprite = theme.roundedFill;
        progress.color = theme.accent;
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        progress.fillAmount = 0f;
        SetRect((RectTransform)progressObject.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -275f), new Vector2(640f, 8f));
        TMP_Text opponentName = opponent.Find("Opponent").GetComponent<TMP_Text>();
        presentation.Configure(code, status, waiting.gameObject, opponent.gameObject, opponentName, progress);
        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            StyleButton(button, false, theme);
            SetRect((RectTransform)button.transform, Vector2.zero, new Vector2(72f, 48f), new Vector2(220f, 88f), Vector2.zero);
        }
        Transform copy = panel.Find("CopyCodeButton");
        if (copy == null)
        {
            GameObject copyObject = NewUiObject("CopyCodeButton", typeof(Image), typeof(Button), typeof(UIAnimator));
            copyObject.transform.SetParent(panel, false);
            Button copyButton = copyObject.GetComponent<Button>();
            StyleButton(copyButton, false, theme);
            CreateLabel(copyObject.transform, "Label", "COPY CODE", theme.buttonSize, theme, theme.text);
            UnityEventTools.AddPersistentListener(copyButton.onClick, presentation.CopyCode);
            SetRect((RectTransform)copyObject.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 55f), new Vector2(280f, 88f));
        }
        EnsureSafeArea(panel);
        EnsurePanelAnimator(panel);
    }

    private static TMP_Text StyleResultPanel(Transform panel, bool won, MenuUIController controller, UITheme theme)
    {
        if (panel == null) return null;
        SetFullScreen(panel);
        Image overlay = panel.GetComponent<Image>();
        if (overlay != null) overlay.color = new Color(0.01f, 0.015f, 0.035f, 0.82f);
        Transform card = panel.Find("ResultCard");
        if (card == null)
        {
            GameObject cardObject = NewUiObject("ResultCard", typeof(Image));
            cardObject.transform.SetParent(panel, false);
            card = cardObject.transform;
            card.SetAsFirstSibling();
        }
        Image cardImage = card.GetComponent<Image>();
        cardImage.sprite = theme.roundedFill;
        cardImage.type = Image.Type.Sliced;
        cardImage.color = theme.panel;
        cardImage.raycastTarget = false;
        SetRect((RectTransform)card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 570f));
        AddBorder(card, theme);
        TMP_Text reason = null;
        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            Button owner = text.GetComponentInParent<Button>();
            if (owner != null)
            {
                text.text = text.text.ToUpperInvariant();
                continue;
            }
            bool title = text.text.IndexOf("WON", StringComparison.OrdinalIgnoreCase) >= 0 || text.text.IndexOf("LOST", StringComparison.OrdinalIgnoreCase) >= 0;
            UIStyle.ApplyText(text, title ? theme.titleSize : theme.bodySize, title ? (won ? theme.success : theme.danger) : theme.text, title ? FontStyles.Bold : FontStyles.Normal);
            if (title)
                SetRect(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 105f), new Vector2(800f, 150f));
            else
            {
                reason = text;
                SetRect(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(760f, 80f));
            }
        }
        if (reason == null)
        {
            reason = CreateLabel(panel, "ResultReason", string.Empty, theme.bodySize, theme, theme.text);
            SetRect(reason.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(760f, 80f));
        }
        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        int action = 0;
        foreach (Button button in buttons)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            bool back = button.name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0 || label != null && label.text.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0;
            StyleButton(button, !back, theme);
            if (label != null) label.text = back ? "BACK" : "PLAY AGAIN";
            SetRect((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(back ? -170f : 170f, -175f), new Vector2(300f, 96f));
            if (back && controller != null)
            {
                for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
                UnityEventTools.AddPersistentListener(button.onClick, controller.ContinueToMainMenu);
            }
            action++;
        }
        EnsureSafeArea(panel);
        EnsurePanelAnimator(panel);
        return reason;
    }

    private static TMP_Text GetOrCreateLabel(Transform parent, string name, string value, float size, UITheme theme, Color color)
    {
        Transform existing = parent.Find(name);
        TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : CreateLabel(parent, name, value, size, theme, color);
        text.text = value;
        UIStyle.ApplyText(text, size, color);
        return text;
    }

    private static void EnsureOptionsLabel(Transform parent, string name, string value, Vector2 position, UITheme theme)
    {
        Transform existing = parent.Find(name);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : CreateLabel(parent, name, value, theme.captionSize, theme, theme.muted);
        label.text = value;
        label.alignment = TextAlignmentOptions.Left;
        label.fontStyle = FontStyles.Bold;
        SetRect(label.rectTransform, new Vector2(0.5f, 0.5f), position, new Vector2(560f, 40f));
    }

    private static void StyleCardAction(Button button, string title, string caption, UITheme theme, bool primary)
    {
        StyleButton(button, primary, theme);
        SetLabel(button, title, theme, theme.headerSize);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.44f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(36f, 0f);
            rect.offsetMax = new Vector2(-36f, -28f);
        }
        Transform existing = button.transform.Find("StyleCaption");
        TMP_Text captionText = existing != null ? existing.GetComponent<TMP_Text>() : CreateLabel(button.transform, "StyleCaption", caption, theme.captionSize, theme, primary ? theme.background : theme.muted);
        captionText.text = caption;
        captionText.alignment = TextAlignmentOptions.Top;
        RectTransform captionRect = captionText.rectTransform;
        captionRect.anchorMin = new Vector2(0f, 0f);
        captionRect.anchorMax = new Vector2(1f, 0.5f);
        captionRect.offsetMin = new Vector2(44f, 36f);
        captionRect.offsetMax = new Vector2(-44f, -10f);
    }

    private static void EnsureHeading(Transform parent, string name, string title, string subtitle, Vector2 position, UITheme theme)
    {
        Transform root = parent.Find(name);
        if (root == null)
        {
            GameObject go = NewUiObject(name);
            go.transform.SetParent(parent, false);
            root = go.transform;
            CreateLabel(root, "Title", title, theme.titleSize, theme, theme.text);
            CreateLabel(root, "Subtitle", subtitle, theme.captionSize, theme, theme.muted);
        }
        RectTransform rect = (RectTransform)root;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(900f, 150f);
        TMP_Text titleText = root.Find("Title").GetComponent<TMP_Text>();
        titleText.text = title;
        titleText.alignment = TextAlignmentOptions.TopLeft;
        titleText.characterSpacing = theme.titleCharacterSpacing;
        titleText.fontStyle = FontStyles.Bold;
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.35f);
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
        TMP_Text subtitleText = root.Find("Subtitle").GetComponent<TMP_Text>();
        subtitleText.text = subtitle;
        subtitleText.alignment = TextAlignmentOptions.BottomLeft;
        RectTransform subtitleRect = subtitleText.rectTransform;
        subtitleRect.anchorMin = Vector2.zero;
        subtitleRect.anchorMax = new Vector2(1f, 0.35f);
        subtitleRect.offsetMin = subtitleRect.offsetMax = Vector2.zero;
    }

    private static void StyleButton(Button button, bool primary, UITheme theme)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = theme.roundedFill;
            image.type = Image.Type.Sliced;
            image.color = primary ? theme.accent : theme.raisedPanel;
        }
        button.targetGraphic = image;
        SetButtonColors(button, primary ? theme.accent : theme.raisedPanel, theme);
        UIAnimator animator = button.GetComponent<UIAnimator>();
        if (animator == null)
            animator = Undo.AddComponent<UIAnimator>(button.gameObject);
        animator.ConfigureButton(primary);
        AddBorder(button.transform, theme);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            Text legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null)
            {
                legacy.enabled = false;
                label = CreateLabel(button.transform, "StyleLabel", legacy.text.ToUpperInvariant(), theme.buttonSize, theme, primary ? theme.background : theme.text);
            }
        }
        if (label != null)
        {
            label.font = theme.font;
            label.fontSharedMaterial = theme.fontMaterial;
            label.fontSize = theme.bodySize;
            label.fontStyle = FontStyles.Bold;
            label.color = primary ? theme.background : theme.text;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            Stretch(label.rectTransform, 16f);
        }
    }

    private static void SetSubscreenHeading(Transform heading, UITheme theme)
    {
        if (heading == null) return;
        RectTransform root = (RectTransform)heading;
        root.sizeDelta = new Vector2(1200f, 118f);
        TMP_Text title = heading.Find("Title")?.GetComponent<TMP_Text>();
        if (title != null) title.fontSize = theme.headerSize;
    }

    private static void SetButtonColors(Button button, Color normal, UITheme theme)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = Color.Lerp(normal, Color.white, 0.08f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Color.Lerp(normal, Color.black, 0.12f);
        colors.disabledColor = new Color(theme.muted.r, theme.muted.g, theme.muted.b, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;
    }

    private static void AddBorder(Transform parent, UITheme theme)
    {
        Transform existing = parent.Find("StyleBorder");
        Image border;
        if (existing == null)
        {
            GameObject go = NewUiObject("StyleBorder", typeof(Image));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            border = go.GetComponent<Image>();
        }
        else border = existing.GetComponent<Image>();
        border.sprite = theme.roundedBorder;
        border.type = Image.Type.Sliced;
        border.color = theme.border;
        border.raycastTarget = false;
        Stretch(border.rectTransform, 0f);
    }

    private static TMP_Text CreateLabel(Transform parent, string name, string value, float size, UITheme theme, Color color)
    {
        GameObject go = NewUiObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = value;
        text.font = theme.font;
        text.fontSharedMaterial = theme.fontMaterial;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        Stretch(text.rectTransform, 0f);
        return text;
    }

    private static void SetLabel(Button button, string value, UITheme theme, float size = -1f)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        label.text = value;
        label.font = theme.font;
        label.fontSharedMaterial = theme.fontMaterial;
        label.fontSize = size > 0f ? size : theme.bodySize;
    }

    private static GameObject NewUiObject(string name, params Type[] components)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        foreach (Type component in components)
            if (go.GetComponent(component) == null) go.AddComponent(component);
        return go;
    }

    private static void SetFullScreen(Transform transform)
    {
        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.localScale = Vector3.one;
    }

    private static void EnsureSafeArea(Transform panel)
    {
        if (panel.GetComponent<SafeAreaFitter>() == null)
            Undo.AddComponent<SafeAreaFitter>(panel.gameObject);
    }

    private static void EnsurePanelAnimator(Transform panel)
    {
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null)
            group = Undo.AddComponent<CanvasGroup>(panel.gameObject);
        group.alpha = panel.gameObject.activeSelf ? 1f : 0f;
        group.interactable = panel.gameObject.activeSelf;
        group.blocksRaycasts = panel.gameObject.activeSelf;
        UIAnimator animator = panel.GetComponent<UIAnimator>();
        if (animator == null)
            animator = Undo.AddComponent<UIAnimator>(panel.gameObject);
        animator.ConfigurePanel();
    }

    private static void RepairMenuLobbyReferences(Canvas canvas, Transform main, Transform host)
    {
        MenuLobbyUI lobby = canvas.GetComponent<MenuLobbyUI>();
        if (lobby == null || host == null) return;
        SerializedObject serialized = new SerializedObject(lobby);
        serialized.FindProperty("mainMenuPanel").objectReferenceValue = main.gameObject;
        serialized.FindProperty("hostLobbyPanel").objectReferenceValue = host.gameObject;
        FirebaseLobbyManager firebase = canvas.GetComponent<FirebaseLobbyManager>();
        TMP_Text hostCode = null;
        if (firebase != null)
        {
            SerializedObject firebaseSerialized = new SerializedObject(firebase);
            hostCode = firebaseSerialized.FindProperty("hostCodeText").objectReferenceValue as TMP_Text;
        }
        if (hostCode == null)
            hostCode = host.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.text.Length <= 4);
        serialized.FindProperty("hostCodeText").objectReferenceValue = hostCode;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Button RequireButton(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null || child.GetComponent<Button>() == null)
            throw new InvalidOperationException($"Required button '{parent.name}/{name}' was not found.");
        return child.GetComponent<Button>();
    }

    private static void Validate(Transform value, string name)
    {
        if (value == null) throw new InvalidOperationException($"Required Menu object '{name}' was not found.");
    }

    private static T FindSceneObject<T>(Scene scene, string name) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T component in root.GetComponentsInChildren<T>(true))
                if (component.name == name) return component;
        return null;
    }
}
#endif
