using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SkinShopUI : MonoBehaviour
{
    private readonly List<GameObject> cards = new List<GameObject>();
    private readonly string[] skinIds = { "beard", "turtle", "sun_ducker" };
    private GameObject panel;
    private Text beardState;
    private Text turtleState;
    private Text sunState;
    private Text pageLabel;
    private Button beardButton;
    private Button turtleButton;
    private Button sunButton;
    private Button deleteAccountButton;
    private Text deleteAccountText;
    private float deleteConfirmationExpiresAt;
    private int currentPage;
    private Coroutine pageAnimation;
    private Font font;

    private const float SideCardOffset = 455f;
    private const float SideCardScale = .68f;
    private const float SideCardAlpha = .58f;

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
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "Menu")
            return;

        // FindObjectsByType excludes inactive objects. StartMenu begins inactive,
        // so use Resources to prevent a second shop from being installed later.
        foreach (SkinShopUI existing in Resources.FindObjectsOfTypeAll<SkinShopUI>())
        {
            if (existing != null && existing.gameObject.scene == activeScene)
                return;
        }

        GameObject skins = null;
        foreach (Button candidate in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (candidate.name == "SkinButton" && candidate.gameObject.scene == activeScene)
            {
                skins = candidate.gameObject;
                break;
            }
        }

        if (skins == null)
        {
            Debug.LogWarning("[SkinShop] Menu SkinButton was not found.");
            return;
        }

        SkinShopUI shop = skins.AddComponent<SkinShopUI>();
        Button openButton = skins.GetComponent<Button>();
        if (openButton != null)
        {
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
        SelectEquippedPage();
        Refresh();
        if (FirebaseManager.I != null)
        {
            try { await FirebaseManager.I.RefreshSkinDataAsync(); }
            catch (Exception e) { Debug.LogError("[SkinShop] Could not load skins: " + e.Message); }
        }
        SelectEquippedPage();
        Refresh();
    }

    private void BuildPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        panel = Ui("SkinsPanel", canvas.transform, new Color(0.035f, 0.015f, 0.09f, 0.97f));
        panel.transform.SetAsLastSibling();
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        // Hide before constructing previews. If any individual preview fails,
        // the shop must never cover the main menu automatically.
        panel.SetActive(false);
        SkinCarouselSwipe swipe = panel.AddComponent<SkinCarouselSwipe>();
        swipe.Initialize(PreviousPage, NextPage);

        Label("SKINS", panel.transform, 54, new Vector2(0, 440), new Vector2(600, 70));
        pageLabel = Label("", panel.transform, 22, new Vector2(0, 390), new Vector2(500, 35));
        pageLabel.color = new Color(.8f, .68f, 1f, 1f);
        Button close = MakeButton("Close", panel.transform, "X", new Vector2(430, 430), new Vector2(70, 62));
        close.onClick.AddListener(() => panel.SetActive(false));

        beardButton = MakeSkinCard("BeardCard", "BEARD", "beard", out beardState);
        turtleButton = MakeSkinCard("TurtleCard", "TURTLE", "turtle", out turtleState);
        sunButton = MakeSkinCard("SunDuckerCard", "SUN DUCKER", "sun_ducker", out sunState);
        beardButton.onClick.AddListener(() => Equip("beard"));
        turtleButton.onClick.AddListener(TurtleClicked);
        sunButton.onClick.AddListener(SunClicked);

        Button previous = MakeButton("PreviousSkin", panel.transform, "‹", new Vector2(-720, 25), new Vector2(90, 110));
        previous.GetComponentInChildren<Text>().fontSize = 58;
        previous.onClick.AddListener(PreviousPage);
        Button next = MakeButton("NextSkin", panel.transform, "›", new Vector2(720, 25), new Vector2(90, 110));
        next.GetComponentInChildren<Text>().fontSize = 58;
        next.onClick.AddListener(NextPage);

        Button restore = MakeButton("RestorePurchases", panel.transform, "RECOVER OWNED SKINS",
            new Vector2(0, -348), new Vector2(330, 54));
        restore.onClick.AddListener(() => SkinPurchaseManager.I?.RestorePurchases());

        Button privacy = MakeButton("PrivacyPolicy", panel.transform, "PRIVACY",
            new Vector2(-250, -425), new Vector2(190, 50));
        privacy.onClick.AddListener(() => Application.OpenURL(PrivacyUrl));
        Button support = MakeButton("Support", panel.transform, "SUPPORT",
            new Vector2(0, -425), new Vector2(190, 50));
        support.onClick.AddListener(() => Application.OpenURL(SupportUrl));
        deleteAccountButton = MakeButton("DeleteAccount", panel.transform, "DELETE ACCOUNT",
            new Vector2(250, -425), new Vector2(220, 50));
        deleteAccountText = deleteAccountButton.GetComponentInChildren<Text>();
        deleteAccountButton.onClick.AddListener(DeleteAccountClicked);

        ShowPage(0, false);
    }

    private Button MakeSkinCard(string objectName, string title, string skinId, out Text state)
    {
        GameObject card = Ui(objectName, panel.transform, new Color(0.13f, 0.065f, 0.23f, 1f));
        card.AddComponent<CanvasGroup>();
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(430, 650);
        cardRect.anchoredPosition = new Vector2(0, 20);
        Label(title, card.transform, 38, new Vector2(0, 275), new Vector2(400, 58));

        GameObject preview = Ui("Preview", card.transform, new Color(.025f, .012f, .06f, 1f));
        RectTransform previewRect = preview.GetComponent<RectTransform>();
        previewRect.sizeDelta = new Vector2(320, 395);
        previewRect.anchoredPosition = new Vector2(0, 30);
        GameObject livePreview = new GameObject("Live3DPreview", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(RawImage), typeof(SkinPreview3D));
        livePreview.transform.SetParent(preview.transform, false);
        RectTransform liveRect = livePreview.GetComponent<RectTransform>();
        liveRect.anchorMin = Vector2.zero;
        liveRect.anchorMax = Vector2.one;
        liveRect.offsetMin = new Vector2(8, 8);
        liveRect.offsetMax = new Vector2(-8, -8);
        livePreview.GetComponent<SkinPreview3D>().Initialize(skinId);
        Text hint = Label("DRAG HERE TO ROTATE", preview.transform, 15,
            new Vector2(0, -175), new Vector2(300, 25));
        hint.raycastTarget = false;

        Button button = MakeButton("StateButton", card.transform, "", new Vector2(0, -270), new Vector2(300, 70));
        state = button.GetComponentInChildren<Text>();
        cards.Add(card);
        return button;
    }

    private void PreviousPage() => ShowPage(currentPage - 1, true);
    private void NextPage() => ShowPage(currentPage + 1, true);

    private void ShowPage(int page, bool animate)
    {
        if (cards.Count == 0) return;
        currentPage = (page % cards.Count + cards.Count) % cards.Count;
        pageLabel.text = $"{currentPage + 1} / {cards.Count}   •   SWIPE LEFT OR RIGHT";
        if (pageAnimation != null)
        {
            StopCoroutine(pageAnimation);
            pageAnimation = null;
        }
        if (animate)
            pageAnimation = StartCoroutine(AnimateCardsToPage());
        else
            ApplyCardLayoutImmediately();
    }

    private IEnumerator AnimateCardsToPage()
    {
        int count = cards.Count;
        Vector2[] startPositions = new Vector2[count];
        Vector3[] startScales = new Vector3[count];
        float[] startAlphas = new float[count];
        Vector2[] targetPositions = new Vector2[count];
        Vector3[] targetScales = new Vector3[count];
        float[] targetAlphas = new float[count];

        for (int i = 0; i < count; i++)
        {
            RectTransform rect = (RectTransform)cards[i].transform;
            CanvasGroup group = cards[i].GetComponent<CanvasGroup>();
            startPositions[i] = rect.anchoredPosition;
            startScales[i] = rect.localScale;
            startAlphas[i] = group.alpha;
            GetCardLayout(i, out targetPositions[i], out targetScales[i], out targetAlphas[i]);
            cards[i].SetActive(true);
            group.blocksRaycasts = i == currentPage;
            group.interactable = i == currentPage;
        }

        cards[currentPage].transform.SetAsLastSibling();
        float elapsed = 0f;
        const float duration = .28f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float amount = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
            for (int i = 0; i < count; i++)
            {
                RectTransform rect = (RectTransform)cards[i].transform;
                CanvasGroup group = cards[i].GetComponent<CanvasGroup>();
                rect.anchoredPosition = Vector2.Lerp(startPositions[i], targetPositions[i], amount);
                rect.localScale = Vector3.Lerp(startScales[i], targetScales[i], amount);
                group.alpha = Mathf.Lerp(startAlphas[i], targetAlphas[i], amount);
            }
            yield return null;
        }
        ApplyCardLayoutImmediately();
        pageAnimation = null;
    }

    private void ApplyCardLayoutImmediately()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            GetCardLayout(i, out Vector2 position, out Vector3 scale, out float alpha);
            GameObject card = cards[i];
            card.SetActive(true);
            RectTransform rect = (RectTransform)card.transform;
            rect.anchoredPosition = position;
            rect.localScale = scale;
            CanvasGroup group = card.GetComponent<CanvasGroup>();
            group.alpha = alpha;
            group.blocksRaycasts = i == currentPage;
            group.interactable = i == currentPage;
        }
        cards[currentPage].transform.SetAsLastSibling();
    }

    private void GetCardLayout(int cardIndex, out Vector2 position, out Vector3 scale, out float alpha)
    {
        if (cardIndex == currentPage)
        {
            position = new Vector2(0f, 20f);
            scale = Vector3.one;
            alpha = 1f;
            return;
        }

        int previousIndex = (currentPage - 1 + cards.Count) % cards.Count;
        bool isPrevious = cardIndex == previousIndex;
        position = new Vector2(isPrevious ? -SideCardOffset : SideCardOffset, 20f);
        scale = Vector3.one * SideCardScale;
        alpha = SideCardAlpha;
    }

    private void SelectEquippedPage()
    {
        string selected = FirebaseManager.I?.SelectedSkin ?? "beard";
        int page = Array.IndexOf(skinIds, selected);
        ShowPage(page < 0 ? 0 : page, false);
    }

    private async void Equip(string id)
    {
        if (FirebaseManager.I == null) return;
        try
        {
            bool equipped = await FirebaseManager.I.EquipSkinAsync(id);
            if (!equipped)
                Debug.LogWarning("[SkinShop] Cannot equip unowned skin '" + id + "'.");
        }
        catch (Exception e) { Debug.LogError("[SkinShop] Could not equip skin: " + e.Message); }
        Refresh();
    }

    private void TurtleClicked()
    {
#if UNITY_EDITOR
        if (FirebaseManager.I != null && !FirebaseManager.I.OwnsTurtle)
        {
            FirebaseManager.I.PreviewTurtleInEditor();
            Refresh();
            return;
        }
#endif
        if (FirebaseManager.I != null && FirebaseManager.I.OwnsTurtle) Equip("turtle");
        else SkinPurchaseManager.I?.PurchaseTurtle();
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
        FirebaseManager firebase = FirebaseManager.I;
        SkinPurchaseManager purchases = SkinPurchaseManager.I;
        string selected = firebase?.SelectedSkin ?? "beard";
        bool turtleOwned = firebase != null && firebase.OwnsTurtle;
        bool sunOwned = firebase != null && firebase.OwnsSunDucker;
        string purchaseStatus = purchases?.StatusMessage;

        beardState.text = selected == "beard" ? "EQUIPPED" : "OWNED";
        turtleState.text = turtleOwned
            ? (selected == "turtle" ? "EQUIPPED" : "OWNED")
            : (!string.IsNullOrEmpty(purchaseStatus) ? purchaseStatus : purchases?.TurtleDisplayPrice ?? "$0.29");
        sunState.text = sunOwned
            ? (selected == "sun_ducker" ? "EQUIPPED" : "OWNED")
            : (!string.IsNullOrEmpty(purchaseStatus) ? purchaseStatus : purchases?.SunDuckerDisplayPrice ?? "$4.99");
        beardButton.interactable = selected != "beard";
        turtleButton.interactable = !turtleOwned || selected != "turtle";
        sunButton.interactable = !sunOwned || selected != "sun_ducker";
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
        GameObject gameObject = Ui(name, parent, new Color(.36f, .15f, .65f, 1f));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Button button = gameObject.AddComponent<Button>();
        button.targetGraphic = gameObject.GetComponent<Image>();
        Label(text, gameObject.transform, 23, Vector2.zero, size);
        return button;
    }
}

public sealed class SkinCarouselSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const float SwipeThreshold = 70f;
    private Action previous;
    private Action next;
    private Vector2 dragTotal;

    public void Initialize(Action previousPage, Action nextPage)
    {
        previous = previousPage;
        next = nextPage;
    }

    public void OnBeginDrag(PointerEventData eventData) => dragTotal = Vector2.zero;

    public void OnDrag(PointerEventData eventData) => dragTotal += eventData.delta;

    public void OnEndDrag(PointerEventData eventData)
    {
        if (Mathf.Abs(dragTotal.x) < SwipeThreshold || Mathf.Abs(dragTotal.x) < Mathf.Abs(dragTotal.y))
            return;
        if (dragTotal.x < 0f) next?.Invoke();
        else previous?.Invoke();
    }

    public void SwipeFromPreview(float horizontalDelta)
    {
        if (horizontalDelta < 0f) next?.Invoke();
        else previous?.Invoke();
    }
}
