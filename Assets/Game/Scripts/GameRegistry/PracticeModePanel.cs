using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Runtime menu extension, preserving existing scene button bindings.</summary>
public sealed class PracticeModePanel : MonoBehaviour
{
    private bool selectionInProgress;
    private GameObject sourcePanel;

    public static void Show(GameObject sourcePanel = null)
    {
        bool verification = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--ez-ui-capture") >= 0;
        if (!verification && (GameManager.I == null || GameManager.I.State != GameManager.GameState.Menu)) return;
        PracticeModePanel existing = FindFirstObjectByType<PracticeModePanel>();
        if (existing != null) return;

        GameObject root = new GameObject("Practice Mode", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PracticeModePanel));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 1f;
        root.AddComponent<SafeAreaFitter>();
        PracticeModePanel panel = root.GetComponent<PracticeModePanel>();
        panel.sourcePanel = sourcePanel;
        panel.Build();
        Canvas.ForceUpdateCanvases();
        EZRestyle.All(canvas);

        // The Practice screen owns the menu while it is open. Hiding the
        // source after the overlay is fully built prevents the Multiplayer
        // selector from rendering through the first transition frames.
        if (sourcePanel != null)
            sourcePanel.SetActive(false);
    }

    private void Build()
    {
        Image backdrop = new GameObject("Background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        backdrop.transform.SetParent(transform, false);
        backdrop.rectTransform.anchorMin = Vector2.zero;
        backdrop.rectTransform.anchorMax = Vector2.one;
        backdrop.rectTransform.offsetMin = backdrop.rectTransform.offsetMax = Vector2.zero;
        UITheme theme = UITheme.Current;
        backdrop.color = theme != null ? new Color(theme.background.r, theme.background.g, theme.background.b, 0.94f) : new Color(0.005f, 0.012f, 0.035f, 0.99f);
        AddText(transform, "PRACTICE", theme != null ? Mathf.RoundToInt(theme.headerSize) : 42, new Vector2(0f, 230f));
        AddButton("Playground", "Free play in the arena", 65f, false);
        AddButton("CPU", "A full match against a strong opponent", -45f, true);
        Button back = MakeButton("Back", -190f);
        back.onClick.AddListener(CloseAndRestoreSource);
    }

    private void AddButton(string title, string subtitle, float y, bool cpu)
    {
        Button button = MakeButton(title, y);
        AddText(button.transform, subtitle, 17, new Vector2(0f, -22f));
        button.onClick.AddListener(() =>
        {
            if (selectionInProgress || GameManager.I == null)
                return;

            selectionInProgress = true;
            SetButtonsInteractable(false);

            if (cpu)
                GameManager.I.PlayCpuPractice();
            else
                GameManager.I.PlayPractice();

            // Keep this full-screen panel visible while the local host starts.
            // Loading Game destroys it with the Menu scene; a failed startup
            // returns to Menu and closes it from this coroutine.
            StartCoroutine(CloseIfPracticeStartupFails());
        });
    }

    private System.Collections.IEnumerator CloseIfPracticeStartupFails()
    {
        yield return null;

        while (GameManager.I != null && GameManager.I.State != GameManager.GameState.Menu)
            yield return null;

        if (this != null)
            CloseAndRestoreSource();
    }

    private void CloseAndRestoreSource()
    {
        if (sourcePanel != null)
            sourcePanel.SetActive(true);

        Destroy(gameObject);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
            button.interactable = interactable;
    }

    private Button MakeButton(string label, float y)
    {
        GameObject obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)obj.transform;
        rect.sizeDelta = new Vector2(460f, 88f);
        rect.anchoredPosition = new Vector2(0f, y);
        UITheme theme = UITheme.Current;
        Image image = obj.GetComponent<Image>();
        image.color = label == "Back" && theme != null ? theme.raisedPanel : theme != null ? theme.accent : new Color(0.02f, 0.16f, 0.46f, 1f);
        if (theme != null) { image.sprite = theme.roundedFill; image.type = Image.Type.Sliced; }
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = obj.GetComponent<Image>();
        AddText(obj.transform, label.ToUpperInvariant(), theme != null ? Mathf.RoundToInt(theme.buttonSize) : 28, new Vector2(0f, label == "Back" ? 0f : 12f));
        obj.AddComponent<UIAnimator>().ConfigureButton(label != "Back");
        return button;
    }

    private static void AddText(Transform parent, string value, int size, Vector2 position)
    {
        TMP_Text text = new GameObject(value, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        text.transform.SetParent(parent, false);
        text.rectTransform.sizeDelta = new Vector2(600f, 55f);
        text.rectTransform.anchoredPosition = position;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        UITheme theme = UITheme.Current;
        if (theme != null && theme.font != null) text.font = theme.font;
        if (theme != null && theme.fontMaterial != null) text.fontSharedMaterial = theme.fontMaterial;
        text.color = theme != null ? theme.text : Color.white;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        text.text = value;
    }
}
