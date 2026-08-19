using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Provides account, support, privacy, and restore actions from Options.</summary>
public sealed class OtherInformationUI : MonoBehaviour
{
    private const string PrivacyUrl = "https://entropy-7c113.web.app/privacy";
    private const string SupportUrl = "https://entropy-7c113.web.app/support";

    private static readonly Color PanelColor = new Color(0.018f, 0.045f, 0.12f, 0.98f);
    private static readonly Color AccentBlue = new Color(0.10f, 0.40f, 0.82f, 1f);
    private static readonly Color OptionsTextColor = new Color(0.90f, 0.20f, 0.02f, 1f);

    private GameObject optionsMenu;
    private GameObject informationPanel;
    private Button optionsButton;
    private Button deleteAccountButton;
    private Text deleteAccountText;
    private float deleteConfirmationExpiresAt;
    private Font font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded += (_, __) => EnsureInstalled();
        EnsureInstalled();
    }

    private static void EnsureInstalled()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Menu") return;

        foreach (OtherInformationUI existing in Resources.FindObjectsOfTypeAll<OtherInformationUI>())
            if (existing != null && existing.gameObject.scene == scene) return;

        Canvas canvas = null;
        GameObject options = null;
        foreach (Canvas candidate in Resources.FindObjectsOfTypeAll<Canvas>())
            if (candidate != null && candidate.gameObject.scene == scene && candidate.name == "Canvas")
                canvas = candidate;
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            if (candidate != null && candidate.gameObject.scene == scene && candidate.name == "OptionsMenu")
                options = candidate.gameObject;

        if (canvas == null || options == null) return;
        OtherInformationUI ui = canvas.gameObject.AddComponent<OtherInformationUI>();
        ui.Build(options);
    }

    private void Build(GameObject options)
    {
        optionsMenu = options;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        optionsButton = CreateOptionsTextButton();
        optionsButton.onClick.AddListener(Open);

        informationPanel = Ui("OtherInformationPanel", transform, PanelColor);
        RectTransform panelRect = informationPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        informationPanel.transform.SetAsLastSibling();
        informationPanel.SetActive(false);

        Label("OTHER INFORMATION", informationPanel.transform, 48, new Vector2(0, 410), new Vector2(700, 70));
        Label("ACCOUNT, PURCHASE, AND SUPPORT SETTINGS", informationPanel.transform, 19,
            new Vector2(0, 355), new Vector2(720, 35)).color = new Color(.68f, .84f, 1f, 1f);

        Button restore = MakeButton("RestorePurchases", informationPanel.transform, "RECOVER OWNED SKINS",
            new Vector2(0, 175), new Vector2(410, 62));
        restore.onClick.AddListener(() => SkinPurchaseManager.I?.RestorePurchases());
        MakeButton("PrivacyPolicy", informationPanel.transform, "PRIVACY POLICY",
            new Vector2(0, 82), new Vector2(410, 62)).onClick.AddListener(() => Application.OpenURL(PrivacyUrl));
        MakeButton("Support", informationPanel.transform, "SUPPORT",
            new Vector2(0, -11), new Vector2(410, 62)).onClick.AddListener(() => Application.OpenURL(SupportUrl));
        deleteAccountButton = MakeButton("DeleteAccount", informationPanel.transform, "DELETE ACCOUNT",
            new Vector2(0, -104), new Vector2(410, 62));
        deleteAccountText = deleteAccountButton.GetComponentInChildren<Text>();
        deleteAccountButton.onClick.AddListener(DeleteAccountClicked);
        MakeButton("Back", informationPanel.transform, "BACK", new Vector2(0, -230), new Vector2(250, 58))
            .onClick.AddListener(Close);
    }

    private void Update()
    {
        if (optionsButton == null || optionsMenu == null || informationPanel == null)
            return;

        bool shouldShow = optionsMenu.activeInHierarchy && !informationPanel.activeSelf;
        if (optionsButton.gameObject.activeSelf != shouldShow)
            optionsButton.gameObject.SetActive(shouldShow);
    }

    private Button CreateOptionsTextButton()
    {
        GameObject gameObject = new GameObject("OtherInformationButton", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button));
        gameObject.transform.SetParent(transform, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -124f);
        rect.sizeDelta = new Vector2(430f, 44f);

        Image hitArea = gameObject.GetComponent<Image>();
        hitArea.color = Color.clear;
        Button button = gameObject.GetComponent<Button>();
        button.targetGraphic = hitArea;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(gameObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text label = labelObject.GetComponent<Text>();
        label.text = "OTHER INFORMATION";
        label.font = font;
        label.fontSize = 32;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = OptionsTextColor;
        label.raycastTarget = false;
        gameObject.AddComponent<MenuTextButtonFeedback>().Initialize(label);

        return button;
    }

    private void Open()
    {
        optionsMenu.SetActive(false);
        informationPanel.SetActive(true);
    }

    private void Close()
    {
        informationPanel.SetActive(false);
        optionsMenu.SetActive(true);
    }

    private async void DeleteAccountClicked()
    {
        if (FirebaseManager.I == null || deleteAccountButton == null) return;
        if (Time.unscaledTime > deleteConfirmationExpiresAt)
        {
            deleteConfirmationExpiresAt = Time.unscaledTime + 6f;
            deleteAccountText.text = "TAP AGAIN TO DELETE";
            return;
        }

        deleteConfirmationExpiresAt = 0f;
        deleteAccountButton.interactable = false;
        deleteAccountText.text = "DELETING...";
        try
        {
            await FirebaseManager.I.DeletePlayerAccountAsync();
            deleteAccountText.text = "ACCOUNT DELETED";
        }
        catch (Exception exception)
        {
            Debug.LogError("[Account] Could not delete account: " + exception.Message);
            deleteAccountText.text = "DELETE FAILED — TRY AGAIN";
        }
        finally { deleteAccountButton.interactable = true; }
    }

    private GameObject Ui(string name, Transform parent, Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        gameObject.GetComponent<Image>().color = color;
        return gameObject;
    }

    private Text Label(string value, Transform parent, int size, Vector2 position, Vector2 dimensions)
    {
        GameObject gameObject = new GameObject(value, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = position;
        Text text = gameObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = value;
        return text;
    }

    private Button MakeButton(string name, Transform parent, string text, Vector2 position, Vector2 size)
    {
        GameObject gameObject = Ui(name, parent, AccentBlue);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Button button = gameObject.AddComponent<Button>();
        button.targetGraphic = gameObject.GetComponent<Image>();
        Label(text, gameObject.transform, 23, Vector2.zero, size).raycastTarget = false;
        return button;
    }
}
