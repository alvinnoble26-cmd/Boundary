using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SkinShopUI : MonoBehaviour
{
    private GameObject panel;
    private Text beardState;
    private Text sunState;
    private Button beardButton;
    private Button sunButton;
    private Button deleteAccountButton;
    private Text deleteAccountText;
    private float deleteConfirmationExpiresAt;
    private Font font;

    private const string PrivacyUrl = "https://entropy-7c113.web.app/privacy";
    private const string SupportUrl = "https://entropy-7c113.web.app/support";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded += (_, __) => EnsureInstalled();
        EnsureInstalled();
    }

    public static void EnsureInstalled()
    {
        if (SceneManager.GetActiveScene().name != "Menu" || FindFirstObjectByType<SkinShopUI>() != null)
            return;
        GameObject skins = null;
        foreach (Button candidate in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (candidate.name == "SkinButton" && candidate.gameObject.scene == SceneManager.GetActiveScene())
            {
                skins = candidate.gameObject;
                break;
            }
        }
        if (skins == null) { Debug.LogWarning("[SkinShop] Menu SkinButton was not found."); return; }
        SkinShopUI shop = skins.AddComponent<SkinShopUI>();
        Button openButton = skins.GetComponent<Button>();
        if (openButton != null)
        {
            // The scene had this button incorrectly connected to QuitGame.
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(shop.Open);
        }
    }

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildPanel();
    }

    private void OnEnable()
    {
        if (FirebaseManager.I != null) FirebaseManager.I.SkinDataChanged += Refresh;
        if (SkinPurchaseManager.I != null) SkinPurchaseManager.I.Changed += Refresh;
    }

    private void OnDisable()
    {
        if (FirebaseManager.I != null) FirebaseManager.I.SkinDataChanged -= Refresh;
        if (SkinPurchaseManager.I != null) SkinPurchaseManager.I.Changed -= Refresh;
    }

    public async void Open()
    {
        panel.SetActive(true);
        Refresh();
        if (FirebaseManager.I != null)
        {
            try { await FirebaseManager.I.RefreshSkinDataAsync(); }
            catch (Exception e) { Debug.LogError("[SkinShop] Could not load skins: " + e.Message); }
        }
        Refresh();
    }

    private void BuildPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        panel = Ui("SkinsPanel", canvas.transform, new Color(0.035f, 0.015f, 0.09f, 0.97f));
        panel.transform.SetAsLastSibling();
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

        Label("SKINS", panel.transform, 54, new Vector2(0, 360), new Vector2(600, 75));
        Button close = MakeButton("Close", panel.transform, "X", new Vector2(380, 365), new Vector2(75, 65));
        close.onClick.AddListener(() => panel.SetActive(false));

        beardButton = MakeSkinCard("BeardCard", "BEARD", "beard",
            new Vector2(-230, 20), out beardState);
        sunButton = MakeSkinCard("SunDuckerCard", "SUN DUCKER", "sun_ducker",
            new Vector2(230, 20), out sunState);
        beardButton.onClick.AddListener(() => Equip("beard"));
        sunButton.onClick.AddListener(SunClicked);

        Button restore = MakeButton("RestorePurchases", panel.transform, "Recover Owned Skins",
            new Vector2(0, -350), new Vector2(300, 58));
        restore.onClick.AddListener(() => SkinPurchaseManager.I?.RestorePurchases());

        Button privacy = MakeButton("PrivacyPolicy", panel.transform, "PRIVACY",
            new Vector2(-250, -420), new Vector2(190, 52));
        privacy.onClick.AddListener(() => Application.OpenURL(PrivacyUrl));
        Button support = MakeButton("Support", panel.transform, "SUPPORT",
            new Vector2(0, -420), new Vector2(190, 52));
        support.onClick.AddListener(() => Application.OpenURL(SupportUrl));
        deleteAccountButton = MakeButton("DeleteAccount", panel.transform, "DELETE ACCOUNT",
            new Vector2(250, -420), new Vector2(220, 52));
        deleteAccountText = deleteAccountButton.GetComponentInChildren<Text>();
        deleteAccountButton.onClick.AddListener(DeleteAccountClicked);
        panel.SetActive(false);
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
            Refresh();
        }
        catch (Exception e)
        {
            Debug.LogError("[Account] Could not delete account: " + e.Message);
            deleteAccountText.text = "DELETE FAILED — TRY AGAIN";
        }
        finally
        {
            deleteAccountButton.interactable = true;
        }
    }

    private Button MakeSkinCard(string objectName, string title, string skinId,
        Vector2 position, out Text state)
    {
        GameObject card = Ui(objectName, panel.transform, new Color(0.12f, 0.07f, 0.2f, 1));
        RectTransform r = card.GetComponent<RectTransform>(); r.sizeDelta = new Vector2(390, 610); r.anchoredPosition = position;
        Label(title, card.transform, 32, new Vector2(0, 245), new Vector2(360, 55));

        GameObject preview = Ui("Preview", card.transform, new Color(.025f, .012f, .06f, 1));
        RectTransform pv = preview.GetComponent<RectTransform>(); pv.sizeDelta = new Vector2(280, 360); pv.anchoredPosition = new Vector2(0, 15);
        GameObject livePreview = new GameObject("Live3DPreview", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(RawImage), typeof(SkinPreview3D));
        livePreview.transform.SetParent(preview.transform, false);
        RectTransform cr = livePreview.GetComponent<RectTransform>();
        cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
        cr.offsetMin = new Vector2(8, 8); cr.offsetMax = new Vector2(-8, -8);
        livePreview.GetComponent<SkinPreview3D>().Initialize(skinId);
        Text rotateHint = Label("DRAG TO ROTATE", preview.transform, 18,
            new Vector2(0, -155), new Vector2(250, 28));
        rotateHint.raycastTarget = false;
        Button button = MakeButton("StateButton", card.transform, "", new Vector2(0, -250), new Vector2(280, 70));
        state = button.GetComponentInChildren<Text>();
        return button;
    }

    private async void Equip(string id)
    {
        if (FirebaseManager.I == null) return;
        try { await FirebaseManager.I.EquipSkinAsync(id); }
        catch (Exception e) { Debug.LogError("[SkinShop] Could not equip skin: " + e.Message); }
        Refresh();
    }

    private void SunClicked()
    {
#if UNITY_EDITOR
        if (FirebaseManager.I != null && !FirebaseManager.I.OwnsSunDucker)
        {
            FirebaseManager.I.PreviewSunDuckerInEditor();
            Refresh();
            return;
        }
#endif
        if (FirebaseManager.I != null && FirebaseManager.I.OwnsSunDucker) Equip("sun_ducker");
        else SkinPurchaseManager.I?.PurchaseSunDucker();
    }

    private void Refresh()
    {
        if (beardState == null) return;
        FirebaseManager fm = FirebaseManager.I;
        bool sunOwned = fm != null && fm.OwnsSunDucker;
        beardState.text = fm != null && fm.SelectedSkin == "beard" ? "EQUIPPED" : "OWNED";
        sunState.text = sunOwned
            ? (fm.SelectedSkin == "sun_ducker" ? "EQUIPPED" : "OWNED")
            : (!string.IsNullOrEmpty(SkinPurchaseManager.I?.StatusMessage)
                ? SkinPurchaseManager.I.StatusMessage
                : (SkinPurchaseManager.I?.DisplayPrice ?? "$4.99"));
        beardButton.interactable = fm == null || fm.SelectedSkin != "beard";
        sunButton.interactable = !sunOwned || fm.SelectedSkin != "sun_ducker";
    }

    private GameObject Ui(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false); go.GetComponent<Image>().color = color; return go;
    }

    private Text Label(string value, Transform parent, int size, Vector2 pos, Vector2 dimensions)
    {
        GameObject go = new GameObject(value, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false); RectTransform r = go.GetComponent<RectTransform>();
        r.sizeDelta = dimensions; r.anchoredPosition = pos;
        Text t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white; t.text = value; return t;
    }

    private Button MakeButton(string name, Transform parent, string text, Vector2 pos, Vector2 size)
    {
        GameObject go = Ui(name, parent, new Color(.36f, .15f, .65f, 1));
        RectTransform r = go.GetComponent<RectTransform>(); r.sizeDelta = size; r.anchoredPosition = pos;
        Button b = go.AddComponent<Button>(); b.targetGraphic = go.GetComponent<Image>();
        Label(text, go.transform, 25, Vector2.zero, size);
        return b;
    }

}
