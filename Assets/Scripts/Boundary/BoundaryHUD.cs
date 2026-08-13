using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
public sealed class BoundaryHUD : MonoBehaviour
{
    public const float EventBannerWidth = 650f;
    public const float EventBannerHeight = 142f;
    public const int EventTitleFontSize = 29;
    public const int EventCountdownFontSize = 23;
    public const int EventHintFontSize = 15;

    private static readonly Color Deep = new Color(0.025f, 0.008f, 0.05f, 0.94f);
    private static readonly Color Purple = new Color(0.52f, 0.10f, 0.88f, 1f);
    private static readonly Color Cyan = new Color(0.18f, 0.78f, 1f, 1f);
    private static readonly Color Danger = new Color(1f, 0.13f, 0.48f, 1f);

    private Canvas canvas;
    private Text phaseText;
    private Text timerText;
    private Text bannerTitle;
    private Text bannerCountdown;
    private Text bannerHint;
    private Text horizonText;
    private Image phaseFill;
    private Image bannerPanel;
    private Image horizonOverlay;
    private AudioSource audioSource;
    private AudioClip phaseCue;
    private AudioClip disasterCue;
    private AudioClip horizonCue;
    private BoundaryMatchController match;
    private BoundaryPlayerState localState;
    private BoundaryPhase previousPhase = BoundaryPhase.Waiting;
    private BoundaryTransition previousTransition = BoundaryTransition.None;
    private BoundaryDisaster previousDisaster = BoundaryDisaster.None;
    private BoundaryKnockoutState previousKnockout = BoundaryKnockoutState.Grounded;

    private void Awake()
    {
        Build();
    }

    private void OnDestroy()
    {
        if (phaseCue != null) Destroy(phaseCue);
        if (disasterCue != null) Destroy(disasterCue);
        if (horizonCue != null) Destroy(horizonCue);
    }

    private void Update()
    {
        if (match == null)
            match = BoundaryMatchController.Instance;
        if (localState == null)
            localState = FindLocalState();

        bool ready = match != null;
        canvas.enabled = ready;
        if (!ready)
            return;

        UpdatePhaseHeader();
        UpdateBanner();
        UpdateHorizon();
        PlayStateCues();
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform safeArea = CreateRect(transform, "Safe Area");
        Stretch(safeArea);
        safeArea.gameObject.AddComponent<SafeAreaFitter>();

        Image header = CreateImage(safeArea, "Phase Header", Deep);
        SetRect(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(520f, 78f));
        phaseText = CreateText(header.transform, "OUTER RING", 26, TextAnchor.MiddleLeft, Color.white);
        RectTransform phaseRect = phaseText.rectTransform;
        phaseRect.anchorMin = new Vector2(0f, 0f);
        phaseRect.anchorMax = new Vector2(0.72f, 1f);
        phaseRect.offsetMin = new Vector2(28f, 8f);
        phaseRect.offsetMax = new Vector2(-5f, -8f);
        timerText = CreateText(header.transform, "01:00", 29, TextAnchor.MiddleCenter, Cyan);
        RectTransform timerRect = timerText.rectTransform;
        timerRect.anchorMin = new Vector2(0.72f, 0f);
        timerRect.anchorMax = Vector2.one;
        timerRect.offsetMin = new Vector2(0f, 8f);
        timerRect.offsetMax = new Vector2(-18f, -8f);

        Image progressBackground = CreateImage(header.transform, "Progress Background", new Color(0.12f, 0.04f, 0.18f, 1f));
        progressBackground.rectTransform.anchorMin = new Vector2(0f, 0f);
        progressBackground.rectTransform.anchorMax = new Vector2(1f, 0f);
        progressBackground.rectTransform.pivot = new Vector2(0f, 0f);
        progressBackground.rectTransform.anchoredPosition = Vector2.zero;
        progressBackground.rectTransform.sizeDelta = new Vector2(0f, 6f);
        phaseFill = CreateImage(progressBackground.transform, "Progress", Purple);
        phaseFill.type = Image.Type.Filled;
        phaseFill.fillMethod = Image.FillMethod.Horizontal;
        phaseFill.fillOrigin = 0;
        Stretch(phaseFill.rectTransform);

        bannerPanel = CreateImage(safeArea, "Event Banner", new Color(0.06f, 0.01f, 0.09f, 0.96f));
        SetRect(bannerPanel.rectTransform, new Vector2(0.5f, 0.78f), Vector2.zero,
            new Vector2(EventBannerWidth, EventBannerHeight));
        bannerTitle = CreateText(bannerPanel.transform, string.Empty, EventTitleFontSize,
            TextAnchor.MiddleCenter, Color.white);
        SetRect(bannerTitle.rectTransform, new Vector2(0.5f, 0.73f), Vector2.zero, new Vector2(606f, 46f));
        bannerCountdown = CreateText(bannerPanel.transform, string.Empty, EventCountdownFontSize,
            TextAnchor.MiddleCenter, Danger);
        SetRect(bannerCountdown.rectTransform, new Vector2(0.5f, 0.44f), Vector2.zero, new Vector2(606f, 39f));
        bannerHint = CreateText(bannerPanel.transform, string.Empty, EventHintFontSize,
            TextAnchor.MiddleCenter, new Color(0.82f, 0.72f, 1f));
        SetRect(bannerHint.rectTransform, new Vector2(0.5f, 0.15f), Vector2.zero, new Vector2(600f, 44f));

        horizonOverlay = CreateImage(safeArea, "Event Horizon Distortion", new Color(0.42f, 0.01f, 0.40f, 0f));
        Stretch(horizonOverlay.rectTransform);
        horizonOverlay.raycastTarget = false;
        horizonText = CreateText(horizonOverlay.transform, "EVENT HORIZON\nESCAPE NOW", 54, TextAnchor.MiddleCenter, Color.white);
        SetRect(horizonText.rectTransform, new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(900f, 180f));

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.32f;
        phaseCue = CreateTone("Boundary Phase Pulse", 76f, 0.7f, 0.34f);
        disasterCue = CreateTone("Boundary Event Alert", 142f, 0.46f, 0.25f);
        horizonCue = CreateTone("Event Horizon Warning", 220f, 0.30f, 0.18f);
        bannerPanel.gameObject.SetActive(false);
        horizonOverlay.gameObject.SetActive(false);
    }

    private void UpdatePhaseHeader()
    {
        string phaseName;
        switch (match.Phase)
        {
            case BoundaryPhase.OuterRing: phaseName = "PHASE I  •  OUTER RING"; break;
            case BoundaryPhase.MiddleRing: phaseName = "PHASE II  •  MIDDLE RING"; break;
            case BoundaryPhase.InnerRing: phaseName = "PHASE III  •  THE VORTEX"; break;
            default: phaseName = "WAITING FOR FIGHTERS"; break;
        }

        phaseText.text = phaseName;
        float remaining = match.PhaseTimeRemaining;
        timerText.text = match.Phase == BoundaryPhase.InnerRing
            ? "SUDDEN DEATH"
            : FormatTime(remaining);
        timerText.fontSize = match.Phase == BoundaryPhase.InnerRing ? 20 : 29;

        float fill = 1f;
        if (match.Phase == BoundaryPhase.OuterRing && match.Transition == BoundaryTransition.None)
            fill = Mathf.Clamp01(remaining / 60f);
        else if (match.Phase == BoundaryPhase.MiddleRing && match.Transition == BoundaryTransition.None)
            fill = Mathf.Clamp01(remaining / 50f);
        else if (match.Transition != BoundaryTransition.None)
            fill = Mathf.Clamp01(match.TransitionRemaining / 7f);
        phaseFill.fillAmount = fill;
        phaseFill.color = match.Phase == BoundaryPhase.InnerRing ? Danger :
            match.Phase == BoundaryPhase.MiddleRing ? Purple : Cyan;
    }

    private void UpdateBanner()
    {
        bool show = false;
        if (match.Transition != BoundaryTransition.None)
        {
            show = true;
            bannerTitle.text = "BOUNDARY COLLAPSE";
            bannerCountdown.text = $"RING CLOSES IN {Mathf.CeilToInt(match.TransitionRemaining)}";
            bannerHint.text = "Move inward. The highlighted sections are being torn into the vortex.";
        }
        else if (match.DisasterStage == BoundaryDisasterStage.Warning)
        {
            show = true;
            bannerTitle.text = "BOUNDARY EVENT: " + BoundaryMath.DisasterName(match.Disaster);
            bannerCountdown.text = $"IMPACT IN {Mathf.Max(1, Mathf.CeilToInt(match.DisasterTimeRemaining))}";
            bannerHint.text = BoundaryMath.DisasterHint(match.Disaster);
        }
        else if (match.DisasterStage == BoundaryDisasterStage.Active)
        {
            show = true;
            bannerTitle.text = BoundaryMath.DisasterName(match.Disaster);
            bannerCountdown.text = match.Disaster == BoundaryDisaster.GravitySurge && match.GravitySurgePulse > 0.12f
                ? "KEEP YOUR FOOTING"
                : $"{Mathf.CeilToInt(match.DisasterTimeRemaining)} SECONDS";
            bannerHint.text = BoundaryMath.DisasterHint(match.Disaster);
        }
        else if (match.Phase == BoundaryPhase.InnerRing && match.PhaseElapsed < 4.5f)
        {
            show = true;
            bannerTitle.text = "THE VORTEX";
            bannerCountdown.text = "SUDDEN DEATH";
            bannerHint.text = "Stay grounded. Airborne targets are easier to launch into the event horizon.";
        }

        bannerPanel.gameObject.SetActive(show);
        if (show)
        {
            float pulse = 0.90f + Mathf.Sin(Time.unscaledTime * 5f) * 0.04f;
            bannerPanel.rectTransform.localScale = Vector3.one * pulse;
        }
    }

    private void UpdateHorizon()
    {
        bool inHorizon = localState != null && localState.State == BoundaryKnockoutState.EventHorizon;
        horizonOverlay.gameObject.SetActive(inHorizon);
        if (!inHorizon)
            return;

        float progress = localState.EscapeProgress;
        float pulse = 0.18f + Mathf.Sin(Time.unscaledTime * 14f) * 0.07f + progress * 0.34f;
        horizonOverlay.color = new Color(0.42f, 0.01f, 0.40f, Mathf.Clamp01(pulse));
        horizonText.text = $"EVENT HORIZON\nESCAPE NOW  •  {Mathf.CeilToInt((1f - progress) * 16f) / 10f:0.0}s";
        horizonText.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.035f);
    }

    private void PlayStateCues()
    {
        if (match.Phase != previousPhase || match.Transition != previousTransition)
        {
            if (match.Phase != BoundaryPhase.Waiting || match.Transition != BoundaryTransition.None)
                Play(phaseCue, 0.55f);
            previousPhase = match.Phase;
            previousTransition = match.Transition;
        }

        if (match.Disaster != previousDisaster)
        {
            if (match.Disaster != BoundaryDisaster.None)
                Play(disasterCue, 0.42f);
            previousDisaster = match.Disaster;
        }

        BoundaryKnockoutState knockout = localState != null ? localState.State : BoundaryKnockoutState.Grounded;
        if (knockout == BoundaryKnockoutState.EventHorizon && previousKnockout != knockout)
            Play(horizonCue, 0.52f);
        previousKnockout = knockout;
    }

    private void Play(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip, volume);
    }

    private static BoundaryPlayerState FindLocalState()
    {
        BoundaryPlayerState[] states = FindObjectsByType<BoundaryPlayerState>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (BoundaryPlayerState state in states)
        {
            if (state != null && state.isOwner)
                return state;
        }
        return null;
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }

    private static AudioClip CreateTone(string name, float frequency, float duration, float decay)
    {
        const int sampleRate = 44100;
        int samples = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t / Mathf.Max(0.05f, decay));
            data[i] = (Mathf.Sin(Mathf.PI * 2f * frequency * t) * 0.72f +
                       Mathf.Sin(Mathf.PI * 2f * frequency * 0.5f * t) * 0.28f) * envelope;
        }
        AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        return (RectTransform)obj.transform;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(Transform parent, string value, int size, TextAnchor alignment, Color color)
    {
        GameObject obj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        Text text = obj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }
}
