using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ControlLayoutEditorUI : MonoBehaviour
{
    private static readonly Color Navy = new Color(0.025f, 0.075f, 0.17f, 1f);
    private static readonly Color DeepNavy = new Color(0.012f, 0.035f, 0.09f, 0.98f);
    private static readonly Color AccentBlue = new Color(0.12f, 0.48f, 0.90f, 1f);

    public static Color PanelColor => Navy;

    private GameObject optionsMenu;
    private GameObject editButton;
    private GameObject editorPanel;
    private RectTransform workspace;
    private Slider sensitivitySlider;
    private Text sensitivityValue;
    private Slider fieldOfViewSlider;
    private Text fieldOfViewValue;
    private readonly Dictionary<string, EditableControlWidget> widgets = new Dictionary<string, EditableControlWidget>();

    public void Build(GameObject optionsPanel)
    {
        if (editorPanel != null)
            return;

        optionsMenu = optionsPanel;
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            return;

        RemoveStaleGeneratedUi(canvas.transform);
        EnsureCanvasInput(canvas);
        // Match the existing menu labels rather than covering Options with a
        // blue card. Other Information is placed below this text control.
        editButton = CreateMenuTextButton(canvas.transform, "Edit Controls",
            new Vector2(0.5f, 0.5f), new Vector2(0f, -58f), new Vector2(360f, 42f), OpenEditor);

        editorPanel = CreateImage(canvas.transform, "ControlLayoutEditor", Navy).gameObject;
        StretchToParent((RectTransform)editorPanel.transform);
        editorPanel.transform.SetAsLastSibling();

        workspace = (RectTransform)CreateImage(editorPanel.transform, "ControlWorkspace", new Color(0.018f, 0.055f, 0.13f, 1f)).transform;
        StretchToParent(workspace);

        RectTransform topBar = (RectTransform)CreateImage(editorPanel.transform, "TopBar", DeepNavy).transform;
        topBar.anchorMin = new Vector2(0f, 1f);
        topBar.anchorMax = new Vector2(1f, 1f);
        topBar.pivot = new Vector2(0.5f, 1f);
        topBar.anchoredPosition = Vector2.zero;
        topBar.sizeDelta = new Vector2(0f, 242f);

        CreateButton(topBar, "Cancel", new Vector2(0f, 1f), new Vector2(90f, -52f), new Vector2(150f, 58f), new Color(0.08f, 0.16f, 0.28f, 1f), Cancel);
        CreateButton(topBar, "Save", new Vector2(1f, 1f), new Vector2(-90f, -52f), new Vector2(150f, 58f), AccentBlue, Save);
        CreateButton(topBar, "Reset", new Vector2(1f, 1f), new Vector2(-90f, -116f), new Vector2(150f, 45f), new Color(0.08f, 0.16f, 0.28f, 1f), ResetToDefaults);

        CreateText(topBar, "CAMERA SENSITIVITY", 20, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(420f, 28f));

        sensitivitySlider = CreateSlider(topBar, "SensitivitySlider", new Vector2(0f, -61f));
        sensitivitySlider.minValue = ControlLayoutSettings.MinimumCameraSensitivity;
        sensitivitySlider.maxValue = ControlLayoutSettings.MaximumCameraSensitivity;
        sensitivitySlider.wholeNumbers = false;
        sensitivitySlider.onValueChanged.AddListener(UpdateSensitivityLabel);
        sensitivityValue = CreateText(topBar, string.Empty, 22, TextAnchor.MiddleLeft, Color.white,
            new Vector2(0.5f, 1f), new Vector2(282f, -61f), new Vector2(90f, 34f));

        CreateText(topBar, "FIELD OF VIEW", 20, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 1f), new Vector2(0f, -105f), new Vector2(420f, 28f));

        fieldOfViewSlider = CreateSlider(topBar, "FieldOfViewSlider", new Vector2(0f, -146f));
        fieldOfViewSlider.minValue = ControlLayoutSettings.MinimumCameraFieldOfView;
        fieldOfViewSlider.maxValue = ControlLayoutSettings.MaximumCameraFieldOfView;
        fieldOfViewSlider.wholeNumbers = true;
        fieldOfViewSlider.onValueChanged.AddListener(UpdateFieldOfViewLabel);
        fieldOfViewValue = CreateText(topBar, string.Empty, 22, TextAnchor.MiddleLeft, Color.white,
            new Vector2(0.5f, 1f), new Vector2(282f, -146f), new Vector2(90f, 34f));

        CreateText(topBar, "Drag controls to move them. The centered crosshair is size-only.", 18,
            TextAnchor.MiddleCenter, new Color(0.68f, 0.84f, 1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(620f, 28f));

        CreateControlWidget("Move", 175f, new Color(0.20f, 0.55f, 0.86f, 0.88f));
        CreateControlWidget("Jump", 250f, new Color(0.92f, 0.45f, 0.20f, 0.88f));
        CreateControlWidget("A1", 150f, new Color(0.12f, 0.42f, 0.82f, 0.90f));
        CreateControlWidget("A2", 150f, new Color(0.12f, 0.42f, 0.82f, 0.90f));
        CreateControlWidget("A3", 150f, new Color(0.12f, 0.42f, 0.82f, 0.90f));
        CreateCrosshairWidget();

        editorPanel.SetActive(false);
        UpdateEditButtonVisibility();
    }

    private static void RemoveStaleGeneratedUi(Transform canvas)
    {
        var staleObjects = new List<GameObject>();
        for (int i = 0; i < canvas.childCount; i++)
        {
            GameObject child = canvas.GetChild(i).gameObject;
            if (child.name == "Edit ControlsButton" || child.name == "ControlLayoutEditor")
                staleObjects.Add(child);
        }

        foreach (GameObject staleObject in staleObjects)
        {
            staleObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(staleObject);
            else
                DestroyImmediate(staleObject);
        }
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
            cameraFieldOfView = fieldOfViewSlider.value,
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
        fieldOfViewSlider.SetValueWithoutNotify(data.cameraFieldOfView);
        UpdateFieldOfViewLabel(data.cameraFieldOfView);

        foreach (KeyValuePair<string, EditableControlWidget> pair in widgets)
            pair.Value.Apply(data.Find(pair.Key));
    }

    private void UpdateSensitivityLabel(float value)
    {
        if (sensitivityValue != null)
            sensitivityValue.text = value.ToString("0.0");
    }

    private void UpdateFieldOfViewLabel(float value)
    {
        if (fieldOfViewValue != null)
            fieldOfViewValue.text = Mathf.RoundToInt(value) + "°";
    }

    private void CreateControlWidget(string id, float baseSize, Color color)
    {
        CircleGraphic circle = CreateCircle(workspace, id, color);
        RectTransform rect = circle.rectTransform;
        var widget = circle.gameObject.AddComponent<EditableControlWidget>();
        widget.Initialize(id, baseSize, workspace);

        CreateText(rect, id, id == "Jump" ? 32 : 38, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);

        CircleGraphic handleGraphic = CreateCircle(rect, "ResizeHandle", new Color(0.78f, 0.90f, 1f, 1f));
        RectTransform handle = handleGraphic.rectTransform;
        handle.anchorMin = new Vector2(1f, 0f);
        handle.anchorMax = new Vector2(1f, 0f);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.anchoredPosition = Vector2.zero;
        handle.sizeDelta = new Vector2(46f, 46f);
        CreateText(handle, "+", 30, TextAnchor.MiddleCenter, DeepNavy,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
        handleGraphic.gameObject.AddComponent<ControlResizeHandle>().Initialize(widget);

        widgets[id] = widget;
    }

    private void CreateCrosshairWidget()
    {
        GameObject root = new GameObject(ControlLayoutSettings.CrosshairControlId, typeof(RectTransform));
        root.layer = 5;
        root.transform.SetParent(workspace, false);
        RectTransform rect = (RectTransform)root.transform;
        EditableControlWidget widget = root.AddComponent<EditableControlWidget>();
        widget.Initialize(ControlLayoutSettings.CrosshairControlId,
            ControlLayoutSettings.CrosshairBaseSize, workspace, false);

        CreateCrosshairBar(rect, "Horizontal", true);
        CreateCrosshairBar(rect, "Vertical", false);

        Image handleImage = CreateImage(rect, "ResizeHandle", new Color(0.78f, 0.90f, 1f, 1f));
        RectTransform handle = handleImage.rectTransform;
        handle.anchorMin = new Vector2(1f, 0f);
        handle.anchorMax = new Vector2(1f, 0f);
        handle.pivot = Vector2.one * 0.5f;
        handle.anchoredPosition = Vector2.zero;
        handle.sizeDelta = new Vector2(30f, 30f);
        CreateText(handle, "+", 22, TextAnchor.MiddleCenter, DeepNavy,
            Vector2.one * 0.5f, Vector2.zero, Vector2.zero, true);
        handleImage.gameObject.AddComponent<ControlResizeHandle>().Initialize(widget);

        CreateText(rect, "SIZE ONLY", 12, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 0f), new Vector2(0f, -18f), new Vector2(100f, 24f));
        widgets[ControlLayoutSettings.CrosshairControlId] = widget;
    }

    private static void CreateCrosshairBar(Transform parent, string name, bool horizontal)
    {
        Image image = CreateImage(parent, name, Color.white);
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = horizontal ? new Vector2(0.14f, 0.44f) : new Vector2(0.44f, 0.14f);
        rect.anchorMax = horizontal ? new Vector2(0.86f, 0.56f) : new Vector2(0.56f, 0.86f);
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
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

    private static GameObject CreateMenuTextButton(Transform parent, string label, Vector2 anchor,
        Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        Image hitArea = CreateImage(parent, label + "Button", Color.clear);
        RectTransform rect = hitArea.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Button button = hitArea.gameObject.AddComponent<Button>();
        button.targetGraphic = hitArea;
        button.onClick.AddListener(action);
        Text text = CreateText(rect, label.ToUpperInvariant(), 32, TextAnchor.MiddleCenter,
            new Color(0.90f, 0.20f, 0.02f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, true);
        hitArea.gameObject.AddComponent<MenuTextButtonFeedback>().Initialize(text);
        return hitArea.gameObject;
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

    private static Slider CreateSlider(Transform parent, string name, Vector2 position)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Slider));
        root.layer = 5;
        root.transform.SetParent(parent, false);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = position;
        rootRect.sizeDelta = new Vector2(470f, 26f);

        Image background = CreateImage(root.transform, "Background", new Color(0.012f, 0.03f, 0.075f, 1f));
        StretchToParent(background.rectTransform);

        Image fill = CreateImage(root.transform, "Fill", AccentBlue);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(7f, 7f);
        fillRect.offsetMax = new Vector2(-7f, -7f);

        Image handleImage = CreateImage(root.transform, "Handle", Color.white);
        RectTransform handleRect = handleImage.rectTransform;
        handleRect.anchorMin = Vector2.one * 0.5f;
        handleRect.anchorMax = Vector2.one * 0.5f;
        handleRect.pivot = Vector2.one * 0.5f;
        handleRect.sizeDelta = new Vector2(24f, 30f);

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

    private static void EnsureCanvasInput(Canvas canvas)
    {
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;

        if (!Application.isPlaying)
            return;

        EventSystem selected = null;
        foreach (EventSystem candidate in Resources.FindObjectsOfTypeAll<EventSystem>())
        {
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;
            if (candidate.GetComponent<InputSystemUIInputModule>() != null)
            {
                selected = candidate;
                break;
            }
        }

        if (selected == null)
        {
            var eventSystemObject = new GameObject(
                "Control Layout EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            selected = eventSystemObject.GetComponent<EventSystem>();
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            Object.DontDestroyOnLoad(eventSystemObject);
        }

        selected.gameObject.SetActive(true);
        selected.enabled = true;
        InputSystemUIInputModule selectedInput = selected.GetComponent<InputSystemUIInputModule>();
        if (selectedInput != null)
            selectedInput.enabled = true;

        foreach (EventSystem candidate in Resources.FindObjectsOfTypeAll<EventSystem>())
        {
            if (candidate == null || candidate == selected || !candidate.gameObject.scene.IsValid())
                continue;

            candidate.enabled = false;
            foreach (BaseInputModule inputModule in candidate.GetComponents<BaseInputModule>())
                inputModule.enabled = false;
        }

        EventSystem.current = selected;
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
            if (!ControlLayoutSettings.HasSavedLayout)
                yield break;

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
