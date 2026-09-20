#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
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

    [MenuItem("Boundary/UI/Build Style Kit")]
    public static void BuildStyleKit()
    {
        EnsureFolders();
        Sprite fill = GenerateRoundedSprite("RoundedFill", SpriteKind.Fill);
        Sprite border = GenerateRoundedSprite("RoundedBorder", SpriteKind.Border);
        Sprite shadow = GenerateRoundedSprite("SoftShadow", SpriteKind.Shadow);
        Sprite glow = GenerateRoundedSprite("AccentGlow", SpriteKind.Glow);

        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UITheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        theme.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ExistingFontPath);
        theme.roundedFill = fill;
        theme.roundedBorder = border;
        theme.softShadow = shadow;
        theme.accentGlow = glow;
        EditorUtility.SetDirty(theme);
        CreatePrefabs(theme);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MenuUIStyler] Style kit built.");
    }

    [MenuItem("Boundary/UI/Apply First Batch to Menu")]
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
        Transform decoration = panel.Find("ContainedInstability");
        if (decoration == null)
        {
            GameObject root = NewUiObject("ContainedInstability", typeof(ContainedInstabilityBackground));
            root.transform.SetParent(panel, false);
            Stretch((RectTransform)root.transform, 0f);
            Image first = CreateDecorativeArc(root.transform, "OuterArc", new Vector2(880f, 880f), theme.accent);
            Image second = CreateDecorativeArc(root.transform, "InnerArc", new Vector2(620f, 620f), theme.secondary);
            first.rectTransform.anchoredPosition = new Vector2(510f, -220f);
            second.rectTransform.anchoredPosition = new Vector2(-610f, 300f);
            root.GetComponent<ContainedInstabilityBackground>().Configure(first.rectTransform, first, second.rectTransform, second);
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
        EnsureHeading(main, "BoundaryLogo", "BOUNDARY", "CONTAIN THE INSTABILITY", new Vector2(72f, -70f), theme);
        Button play = RequireButton(main, "PlayButton");
        StyleButton(play, true, theme);
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
        if (label != null)
        {
            label.font = theme.font;
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
        serialized.FindProperty("hostCodeText").objectReferenceValue = host.GetComponentInChildren<TMP_Text>(true);
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
