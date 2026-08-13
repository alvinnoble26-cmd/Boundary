using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ControlLayoutEditorUI : MonoBehaviour
{
    private static readonly Color Purple = new Color(0.24f, 0.08f, 0.40f, 1f);
    private static readonly Color DeepPurple = new Color(0.12f, 0.035f, 0.22f, 0.96f);
    private static readonly Color AccentPurple = new Color(0.63f, 0.30f, 0.91f, 1f);

    private GameObject optionsMenu;
    private GameObject editButton;
    private GameObject editorPanel;
    private RectTransform workspace;
    private Slider sensitivitySlider;
    private Text sensitivityValue;
    private readonly Dictionary<string, EditableControlWidget> widgets = new Dictionary<string, EditableControlWidget>();

    public void Build(GameObject optionsPanel)
    {
        if (editorPanel != null)
            return;

        optionsMenu = optionsPanel;
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            return;

        EnsureEventSystem();
        // This sits between the volume slider and the existing Back button.
        editButton = CreateButton(canvas.transform, "Edit Controls", new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(300f, 58f), AccentPurple, OpenEditor);

        editorPanel = CreateImage(canvas.transform, "ControlLayoutEditor", Purple).gameObject;
        StretchToParent((RectTransform)editorPanel.transform);
        editorPanel.transform.SetAsLastSibling();

        workspace = (RectTransform)CreateImage(editorPanel.transform, "ControlWorkspace", new Color(0.18f, 0.055f, 0.31f, 1f)).transform;
        StretchToParent(workspace);

        RectTransform topBar = (RectTransform)CreateImage(editorPanel.transform, "TopBar", DeepPurple).transform;
        topBar.anchorMin = new Vector2(0f, 1f);
        topBar.anchorMax = new Vector2(1f, 1f);
        topBar.pivot = new Vector2(0.5f, 1f);
        topBar.anchoredPosition = Vector2.zero;
        topBar.sizeDelta = new Vector2(0f, 155f);

        CreateButton(topBar, "Cancel", new Vector2(0f, 1f), new Vector2(90f, -52f), new Vector2(150f, 58f), new Color(0.38f, 0.20f, 0.48f, 1f), Cancel);
        CreateButton(topBar, "Save", new Vector2(1f, 1f), new Vector2(-90f, -52f), new Vector2(150f, 58f), AccentPurple, Save);
        CreateButton(topBar, "Reset", new Vector2(1f, 1f), new Vector2(-90f, -116f), new Vector2(150f, 45f), new Color(0.38f, 0.20f, 0.48f, 1f), ResetToDefaults);

        CreateText(topBar, "CAMERA SENSITIVITY", 24, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 1f), new Vector2(0f, -31f), new Vector2(420f, 38f));

        sensitivitySlider = CreateSlider(topBar);
        sensitivitySlider.minValue = ControlLayoutSettings.MinimumCameraSensitivity;
        sensitivitySlider.maxValue = ControlLayoutSettings.MaximumCameraSensitivity;
        sensitivitySlider.wholeNumbers = false;
        sensitivitySlider.onValueChanged.AddListener(UpdateSensitivityLabel);
        sensitivityValue = CreateText(topBar, string.Empty, 22, TextAnchor.MiddleLeft, Color.white,
            new Vector2(0.5f, 1f), new Vector2(292f, -81f), new Vector2(100f, 40f));

        CreateText(topBar, "Drag a control to move it. Drag its + corner to resize it.", 18,
            TextAnchor.MiddleCenter, new Color(0.88f, 0.78f, 1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(620f, 32f));

        CreateControlWidget("Move", 175f, new Color(0.20f, 0.55f, 0.86f, 0.88f));
        CreateControlWidget("Jump", 250f, new Color(0.92f, 0.45f, 0.20f, 0.88f));
        CreateControlWidget("A1", 150f, new Color(0.62f, 0.25f, 0.82f, 0.90f));
        CreateControlWidget("A2", 150f, new Color(0.62f, 0.25f, 0.82f, 0.90f));
        CreateControlWidget("A3", 150f, new Color(0.62f, 0.25f, 0.82f, 0.90f));

        editorPanel.SetActive(false);
        UpdateEditButtonVisibility();
    }

    private void Update()
    {
        UpdateEditButtonVisibility();
    }

    private void UpdateEditButtonVisibility()
    {
        if (editButton == null || optionsMenu == null)
            return;

        bool shouldShow = optionsMenu.activeInHierarchy && (editorPanel == null || !editorPanel.activeSelf);
        if (editButton.activeSelf != shouldShow)
            editButton.SetActive(shouldShow);
    }

    private void OpenEditor()
    {
        ApplyWorkingLayout(ControlLayoutSettings.Load());
        optionsMenu.SetActive(false);
        editorPanel.SetActive(true);
        editorPanel.transform.SetAsLastSibling();
    }

    private void Save()
    {
        var data = new ControlLayoutSettings.LayoutData
        {
            cameraSensitivity = sensitivitySlider.value,
            controls = new List<ControlLayoutSettings.ControlEntry>()
        };

        foreach (EditableControlWidget widget in widgets.Values)
            data.controls.Add(widget.Capture());

        ControlLayoutSettings.Save(data);
        CloseEditor();
    }

    private void Cancel()
    {
        ApplyWorkingLayout(ControlLayoutSettings.Load());
        CloseEditor();
    }

    private void ResetToDefaults()
    {
        ApplyWorkingLayout(ControlLayoutSettings.CreateDefault());
    }

    private void CloseEditor()
    {
        editorPanel.SetActive(false);
        optionsMenu.SetActive(true);
    }

    private void ApplyWorkingLayout(ControlLayoutSettings.LayoutData data)
    {
        sensitivitySlider.SetValueWithoutNotify(data.cameraSensitivity);
        UpdateSensitivityLabel(data.cameraSensitivity);

        foreach (KeyValuePair<string, EditableControlWidget> pair in widgets)
            pair.Value.Apply(data.Find(pair.Key));
    }

    private void UpdateSensitivityLabel(float value)
    {
        if (sensitivityValue != null)
            sensitivityValue.text = value.ToString("0.0");
    }

    private void CreateControlWidget(string id, float baseSize, Color color)
    {
        CircleGraphic circle = CreateCircle(workspace, id, color);
        RectTransform rect = circle.rectTransform;
        var widget = circle.gameObject.AddComponent<EditableControlWidget>();
        widget.Initialize(id, baseSize, workspace);

        CreateText(rect, id, id == "Jump" ? 32 : 38, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);

        CircleGraphic handleGraphic = CreateCircle(rect, "ResizeHandle", new Color(0.95f, 0.85f, 1f, 1f));
        RectTransform handle = handleGraphic.rectTransform;
        handle.anchorMin = new Vector2(1f, 0f);
        handle.anchorMax = new Vector2(1f, 0f);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.anchoredPosition = Vector2.zero;
        handle.sizeDelta = new Vector2(46f, 46f);
        CreateText(handle, "+", 30, TextAnchor.MiddleCenter, DeepPurple,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
        handleGraphic.gameObject.AddComponent<ControlResizeHandle>().Initialize(widget);

        widgets[id] = widget;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static CircleGraphic CreateCircle(Transform parent, string name, Color color)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(CircleGraphic));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        CircleGraphic circle = gameObject.GetComponent<CircleGraphic>();
        circle.color = color;
        return circle;
    }

    private static GameObject CreateButton(Transform parent, string label, Vector2 anchor, Vector2 position,
        Vector2 size, Color color, UnityEngine.Events.UnityAction action)
    {
        Image image = CreateImage(parent, label + "Button", color);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        CreateText(rect, label.ToUpperInvariant(), 24, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
        return image.gameObject;
    }

    private static Text CreateText(Transform parent, string text, int fontSize, TextAnchor alignment, Color color,
        Vector2 anchor, Vector2 position, Vector2 size, bool stretch = false)
    {
        var gameObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)gameObject.transform;
        if (stretch)
        {
            StretchToParent(rect);
        }
        else
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        Text uiText = gameObject.GetComponent<Text>();
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiText.text = text;
        uiText.fontSize = fontSize;
        uiText.fontStyle = FontStyle.Bold;
        uiText.alignment = alignment;
        uiText.color = color;
        uiText.raycastTarget = false;
        return uiText;
    }

    private static Slider CreateSlider(Transform parent)
    {
        var root = new GameObject("SensitivitySlider", typeof(RectTransform), typeof(Slider));
        root.layer = 5;
        root.transform.SetParent(parent, false);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, -81f);
        rootRect.sizeDelta = new Vector2(500f, 34f);

        Image background = CreateImage(root.transform, "Background", new Color(0.12f, 0.06f, 0.18f, 1f));
        StretchToParent(background.rectTransform);

        Image fill = CreateImage(root.transform, "Fill", AccentPurple);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(8f, 8f);
        fillRect.offsetMax = new Vector2(-8f, -8f);

        Image handleImage = CreateImage(root.transform, "Handle", Color.white);
        RectTransform handleRect = handleImage.rectTransform;
        handleRect.sizeDelta = new Vector2(38f, 48f);

        Slider slider = root.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Object.DontDestroyOnLoad(eventSystem);
    }
}

public class ControlLayoutRuntime : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (Object.FindFirstObjectByType<ControlLayoutRuntime>() != null)
            return;

        var runtimeObject = new GameObject("ControlLayoutRuntime");
        Object.DontDestroyOnLoad(runtimeObject);
        runtimeObject.AddComponent<ControlLayoutRuntime>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        StartCoroutine(ConfigureSceneNextFrame(SceneManager.GetActiveScene()));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ConfigureSceneNextFrame(scene));
    }

    private IEnumerator ConfigureSceneNextFrame(Scene scene)
    {
        yield return null;

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        if (scene.name == "Game")
        {
            foreach (Canvas canvas in canvases)
            {
                if (canvas.gameObject.scene == scene && canvas.name == "Canvas")
                {
                    ControlLayoutSettings.ApplyToGameCanvas(canvas);
                    break;
                }
            }
        }
        else if (scene.name == "Menu")
        {
            GameObject optionsMenu = FindSceneObject(scene, "OptionsMenu");
            foreach (Canvas canvas in canvases)
            {
                if (canvas.gameObject.scene != scene || canvas.name != "Canvas")
                    continue;

                ControlLayoutEditorUI editor = canvas.GetComponent<ControlLayoutEditorUI>();
                if (editor == null)
                    editor = canvas.gameObject.AddComponent<ControlLayoutEditorUI>();
                editor.Build(optionsMenu);
                break;
            }
        }
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == objectName)
                    return candidate.gameObject;
            }
        }

        return null;
    }
}
