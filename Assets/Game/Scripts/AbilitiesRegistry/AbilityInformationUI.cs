using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class AbilityInformationUI : MonoBehaviour
{
    public const string InformationButtonName = "Ability Information Button";
    public const string InformationPanelName = "Ability Information Panel";

    private static readonly string[] AbilityDescriptions =
    {
        "<b>TELEPORT</b>  •  4s cooldown\nAim where you want to go, then activate. After a 0.5s wind-up, you teleport to a valid open destination. A failed destination still uses the cooldown.",
        // Slide is currently disabled and intentionally omitted from the guide.
        "<b>DASH</b>  •  2.5s cooldown\nUse on the ground or in the air. Dash immediately in your movement/aim direction for a short burst while preserving useful sideways momentum.",
        "<b>BLACK HOLE</b>  •  3.5s cooldown\nThrow a five-second black hole along your aim. It damages players after brief owner immunity and can consume movable arena cubes. Each loadout spawn carries five throws.",
        "<b>ATTRACT</b>  •  3s cooldown\nThrow a gravity field along your aim. It pulls players, hazards, and movable objects toward its center; lighter and closer objects move the most.",
        "<b>REPEL</b>  •  3s cooldown\nThrow a force field along your aim. It pushes players, hazards, and movable objects away from its center; lighter and closer objects move the most.",
        "<b>GRAPPLE</b>  •  3s cooldown\nAim at a valid surface or movable target within 50m. Surfaces pull you in; movable targets are pulled toward you. Jump to release while keeping your momentum.",
        "<b>HOLLOW</b>  •  5s cooldown\nAim and fire a widening 70m void blast after a 0.75s charge. The blast lasts 2s and deals continuous damage to opponents caught inside it.",
        "<b>VOID</b>  •  45s cooldown\nAvailable when your health is higher than your opponent's (always available in Practice). For 15s you become immune and faster while the opponent slows and is pulled strongly toward the domain black hole. Only you see the enemy highlighted with a bright cyan glow.",
        "<b>BULLSEYE</b>  •  2s cooldown\nHold to mark your opponent, aim with the crosshair, then release to throw a very fast knife with unlimited reach. Hit the center for 12 damage or the surrounding ring for 7 damage; hits outside the ring deal no damage.",
        "<b>CHARGE</b>  •  7s cooldown\nHold the electrified Frost Sword, aim, then release. A blue magic ball charges for 1s and travels for 2s before exploding in a 10m radius for 5 damage. Two seconds later it destabilizes for a second 7-damage tick. Both players, including the caster, can be damaged.",
        "<b>SLICE</b>  •  1s cooldown\nHold the black Electricity Sword at a 30-degree angle, aim, then release to sweep a purple-blue 120° slash across a recommended 4m radius. Enemies caught in the forward arc take 7 damage."
    };

    private Button informationButton;
    private GameObject informationPanel;

    private void OnEnable()
    {
        EnsureBuilt();
        WireButtons();
    }

    private void Update()
    {
        if (!Application.isPlaying || informationButton == null || informationPanel == null)
            return;
        informationButton.gameObject.SetActive(gameObject.activeInHierarchy && !informationPanel.activeSelf);
    }

    [ContextMenu("Rebuild Ability Information UI")]
    public void Rebuild()
    {
        Transform canvas = GetComponentInParent<Canvas>()?.transform;
        if (canvas == null)
            return;
        Transform oldButton = transform.Find(InformationButtonName);
        if (oldButton != null)
            DestroySafe(oldButton.gameObject);
        Transform oldPanel = canvas.Find(InformationPanelName);
        if (oldPanel != null)
            DestroySafe(oldPanel.gameObject);
        EnsureBuilt();
        WireButtons();
    }

    public void EnsureBuilt()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        informationButton = transform.Find(InformationButtonName)?.GetComponent<Button>();
        if (informationButton == null)
            informationButton = CreateInformationButton(transform);
        PositionInformationButton((RectTransform)informationButton.transform, transform);
        Transform existingPanel = canvas.transform.Find(InformationPanelName);
        if (existingPanel != null && !HasCompleteInformation(existingPanel))
        {
            DestroySafe(existingPanel.gameObject);
            existingPanel = null;
        }
        informationPanel = existingPanel != null ? existingPanel.gameObject : CreateInformationPanel(canvas.transform);
        ApplyInformationStyling();
    }

    public void ShowInformation()
    {
        if (informationPanel == null)
            EnsureBuilt();
        if (informationPanel != null)
        {
            informationPanel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            RectTransform content = informationPanel.transform.Find("Ability Guide Viewport/Content") as RectTransform;
            if (content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
        if (informationButton != null)
            informationButton.gameObject.SetActive(false);
    }

    public void HideInformation()
    {
        if (informationPanel != null)
            informationPanel.SetActive(false);
        if (informationButton != null)
            informationButton.gameObject.SetActive(gameObject.activeInHierarchy);
    }

    private void WireButtons()
    {
        if (!Application.isPlaying || informationButton == null || informationPanel == null)
            return;
        informationButton.onClick.RemoveListener(ShowInformation);
        informationButton.onClick.AddListener(ShowInformation);
        Button back = informationPanel.transform.Find("Header/Back Button")?.GetComponent<Button>();
        if (back != null)
        {
            back.onClick.RemoveListener(HideInformation);
            back.onClick.AddListener(HideInformation);
        }
    }

    private Button CreateInformationButton(Transform parent)
    {
        Button button = CreateButton(parent, InformationButtonName, "INFO", new Color(0.132f, 0.132f, 0.132f, 1f));
        PositionInformationButton((RectTransform)button.transform, parent);
        return button;
    }

    private void ApplyInformationStyling()
    {
        if (informationButton != null)
        {
            Image informationImage = informationButton.GetComponent<Image>();
            RectTransform backRect = FindAbilitiesBackButton(transform);
            Button backButton = backRect != null ? backRect.GetComponent<Button>() : null;
            TMP_Text backText = backButton != null
                ? backButton.GetComponentInChildren<TMP_Text>(true)
                : null;
            TMP_Text informationText = informationButton.GetComponentInChildren<TMP_Text>(true);

            if (informationImage != null)
            {
                informationImage.enabled = true;
                informationImage.color = Color.clear;
                informationImage.raycastTarget = true;
            }

            if (informationText != null)
            {
                CopyTextAppearance(backText, informationText);
                informationButton.targetGraphic = informationText;
            }

            ColorBlock colors = informationButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.9f, 0.65f, 1f);
            colors.pressedColor = new Color(1f, 0.72f, 0.32f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            informationButton.transition = Selectable.Transition.ColorTint;
            informationButton.colors = colors;
        }

        if (informationPanel != null)
        {
            foreach (TMP_Text text in informationPanel.GetComponentsInChildren<TMP_Text>(true))
            {
                text.color = Color.white;
                text.alpha = 1f;
                text.enableVertexGradient = false;
            }
        }

        if (informationButton != null)
        {
            foreach (TMP_Text text in informationButton.GetComponentsInChildren<TMP_Text>(true))
            {
                text.alpha = 1f;
            }
        }
    }

    private static void CopyTextAppearance(TMP_Text source, TMP_Text destination)
    {
        if (source == null)
            return;

        destination.font = source.font;
        destination.fontSharedMaterial = source.fontSharedMaterial;
        destination.fontSize = source.fontSize;
        destination.fontStyle = source.fontStyle;
        destination.color = source.color;
        destination.enableVertexGradient = source.enableVertexGradient;
        destination.colorGradient = source.colorGradient;
        destination.colorGradientPreset = source.colorGradientPreset;
        destination.alignment = source.alignment;
        destination.characterSpacing = source.characterSpacing;
        destination.wordSpacing = source.wordSpacing;
        destination.lineSpacing = source.lineSpacing;

        RectTransform sourceRect = source.rectTransform;
        RectTransform destinationRect = destination.rectTransform;
        destinationRect.anchorMin = sourceRect.anchorMin;
        destinationRect.anchorMax = sourceRect.anchorMax;
        destinationRect.pivot = sourceRect.pivot;
        destinationRect.anchoredPosition = sourceRect.anchoredPosition;
        destinationRect.sizeDelta = sourceRect.sizeDelta;
        destinationRect.localScale = sourceRect.localScale;
        destinationRect.localRotation = sourceRect.localRotation;
    }

    private static void PositionInformationButton(RectTransform rect, Transform parent)
    {
        RectTransform back = FindAbilitiesBackButton(parent);
        if (back != null)
        {
            rect.anchorMin = back.anchorMin;
            rect.anchorMax = back.anchorMax;
            rect.pivot = back.pivot;
            rect.sizeDelta = back.sizeDelta;
            rect.localScale = back.localScale;
            rect.localRotation = back.localRotation;
            float spacing = back.sizeDelta.x * Mathf.Abs(back.localScale.x) + 1f;
            rect.anchoredPosition = back.anchoredPosition + Vector2.right * spacing;
        }
        else
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(72f, 18f);
            rect.sizeDelta = new Vector2(132f, 48f);
        }
    }

    private GameObject CreateInformationPanel(Transform canvas)
    {
        GameObject panel = UiObject(InformationPanelName, canvas, typeof(Image));
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = Color.black;
        panel.transform.SetAsLastSibling();

        GameObject header = UiObject("Header", panel.transform, typeof(Image));
        RectTransform headerRect = (RectTransform)header.transform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = Vector2.one;
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 82f);
        headerRect.anchoredPosition = Vector2.zero;
        header.GetComponent<Image>().color = new Color(0.025f, 0f, 0.04f, 1f);

        TMP_Text title = CreateText(header.transform, "Title", "ABILITY GUIDE", 32, FontStyles.Bold);
        SetStretch(title.rectTransform, 170f, 170f, 10f, 10f);
        title.alignment = TextAlignmentOptions.Center;

        Button back = CreateButton(header.transform, "Back Button", "BACK", new Color(0.22f, 0.12f, 0.36f, 1f));
        RectTransform backRect = (RectTransform)back.transform;
        backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 0.5f);
        backRect.pivot = new Vector2(0f, 0.5f);
        backRect.anchoredPosition = new Vector2(22f, 0f);
        backRect.sizeDelta = new Vector2(132f, 48f);

        GameObject viewport = UiObject("Ability Guide Viewport", panel.transform,
            typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        RectTransform viewportRect = (RectTransform)viewport.transform;
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(24f, 24f);
        viewportRect.offsetMax = new Vector2(-24f, -96f);
        viewport.GetComponent<Image>().color = Color.clear;

        GameObject content = UiObject("Content", viewport.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 12f;
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (string description in AbilityDescriptions)
            CreateAbilityCard(content.transform, description);

        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        scroll.content = contentRect;
        scroll.viewport = viewportRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 34f;
        panel.SetActive(false);
        return panel;
    }

    private void CreateAbilityCard(Transform parent, string description)
    {
        int titleEnd = description.IndexOf("</b>", System.StringComparison.Ordinal);
        GameObject card = UiObject(description.Substring(3, titleEnd - 3) + " Card", parent,
            typeof(Image), typeof(LayoutElement));
        card.GetComponent<Image>().color = new Color(0.035f, 0.01f, 0.055f, 1f);
        LayoutElement element = card.GetComponent<LayoutElement>();
        element.minHeight = 112f;
        element.preferredHeight = 124f;
        TMP_Text text = CreateText(card.transform, "Description", description, 20, FontStyles.Normal);
        SetStretch(text.rectTransform, 22f, 22f, 14f, 14f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.richText = true;
        text.color = new Color(0.93f, 0.95f, 1f, 1f);
    }

    private TMP_Text CreateText(Transform parent, string objectName, string value, float size, FontStyles style)
    {
        GameObject textObject = UiObject(objectName, parent, typeof(TextMeshProUGUI));
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        TMP_Text template = GetComponentInChildren<TMP_Text>(true);
        text.font = template != null && template.font != null ? template.font : TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(Transform parent, string objectName, string label, Color color)
    {
        GameObject buttonObject = UiObject(objectName, parent, typeof(Image), typeof(Button));
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText(buttonObject.transform, "Label", label, 22, FontStyles.Bold);
        SetStretch(text.rectTransform, 4f, 4f, 4f, 4f);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static GameObject UiObject(string objectName, Transform parent, params System.Type[] components)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.layer = 5;
        result.transform.SetParent(parent, false);
        foreach (System.Type component in components)
            result.AddComponent(component);
        return result;
    }

    private static void SetStretch(RectTransform rect, float left, float right, float bottom, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static RectTransform FindAbilitiesBackButton(Transform parent)
    {
        foreach (Button candidate in parent.GetComponentsInChildren<Button>(true))
        {
            if (candidate.transform.parent == parent && candidate.name.StartsWith("BackButton"))
                return candidate.transform as RectTransform;
        }
        return null;
    }

    private static bool HasCompleteInformation(Transform panel)
    {
        int descriptions = 0;
        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
            if (text.name == "Description" && !string.IsNullOrWhiteSpace(text.text))
                descriptions++;
        Transform viewport = panel.Find("Ability Guide Viewport");
        return descriptions == AbilityDescriptions.Length && viewport != null &&
               viewport.GetComponent<RectMask2D>() != null && viewport.GetComponent<Mask>() == null;
    }

    private static void DestroySafe(GameObject target)
    {
        if (Application.isPlaying)
            Object.Destroy(target);
        else
            Object.DestroyImmediate(target);
    }
}
