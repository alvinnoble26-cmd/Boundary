// ============================================================================
// ENTROPY ZERO UI KIT  (4 of 5)  EZMotion.cs
//   EZPanelFX     - every panel: fade + rise, children cascade in 50 ms apart,
//                   title letter-spacing tightens; hides menu pages that sit under
//                   full-screen overlays (Skins, Practice, Ability Info...).
//   EZButtonFX    - hover 1.04x, press 0.95x, glow, breathing pulse on primary buttons.
//   EZLogoFX      - ENTROPY / ZERO intro (tracking + fade), idle float, rare glitch.
//   EZTogglePill  - replaces the tiny checkmark with an animated pill switch.
// All motion uses unscaled time and respects EZTheme.MotionEnabled.
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class EZPanelFX : MonoBehaviour
{
    struct Item
    {
        public RectTransform rt;
        public CanvasGroup cg;
        public Vector2 rest;
        public bool move;
    }

    public static int OverlayCount;

    CanvasGroup group;
    bool isFlow, isOverlay, counted;
    float enterAlpha = 1f, sup = 1f;
    readonly List<Item> items = new List<Item>();
    TMP_Text title;
    float titleSpacing;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    void Classify()
    {
        isFlow = EZLayouts.IsFlow(name);
        Canvas main = EZRunner.Main;
        Canvas own = GetComponentInParent<Canvas>();
        bool secondary = main != null && own != null && own.rootCanvas != main;
        string u = name.ToUpperInvariant();
        bool keyword = u.Contains("SKIN") || u.Contains("OTHERINFO") || u.Contains("OTHER INFO") ||
                       u.Contains("ABILITY INFORMATION") || u.Contains("CONTROLLAYOUT") || u.Contains("PRACTICE");
        isOverlay = !isFlow && transform.childCount >= 2 && GetComponent<Button>() == null && (secondary || keyword);
    }

    void OnEnable()
    {
        Classify();
        if (isOverlay && !counted) { OverlayCount++; counted = true; }
        StartCoroutine(Enter());
    }

    void OnDisable()
    {
        if (counted) { OverlayCount = Mathf.Max(0, OverlayCount - 1); counted = false; }
        Restore();
    }

    void LateUpdate()
    {
        float targetSup = (isFlow && OverlayCount > 0) ? 0f : 1f;
        sup = Mathf.MoveTowards(sup, targetSup, Time.unscaledDeltaTime * 6f);
        group.alpha = enterAlpha * sup;
        group.blocksRaycasts = sup > 0.5f;
    }

    void Restore()
    {
        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            if (it.rt == null) continue;
            if (it.cg != null) it.cg.alpha = 1f;
            if (it.move) it.rt.anchoredPosition = it.rest;
        }
        items.Clear();
        if (title != null) title.characterSpacing = titleSpacing;
        title = null;
        enterAlpha = 1f;
    }

    IEnumerator Enter()
    {
        enterAlpha = 0f;
        yield return null;                                   // let the owner script finish its own OnEnable/Start work
        var rt = transform as RectTransform;
        Canvas.ForceUpdateCanvases();
        if (rt != null) EZLayouts.Apply(rt);
        Canvas c = GetComponentInParent<Canvas>();
        if (c != null) EZRestyle.All(c.rootCanvas);
        yield return null;
        Canvas.ForceUpdateCanvases();
        Collect();

        if (!EZTheme.MotionEnabled || items.Count == 0)
        {
            enterAlpha = 1f;
            yield break;
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            if (it.cg != null) it.cg.alpha = 0f;
            if (it.move) it.rt.anchoredPosition = it.rest + new Vector2(0f, -32f);
        }
        if (title != null) title.characterSpacing = titleSpacing + 26f;

        float t0 = Time.unscaledTime;
        while (true)
        {
            float e = Time.unscaledTime - t0;
            enterAlpha = Mathf.Clamp01(e / 0.16f);
            bool done = true;
            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                if (it.rt == null) continue;
                if (EZSkinCarousel.Owns(it.rt)) continue;    // the carousel positions those itself
                float local = (e - Mathf.Min(i * 0.05f, 0.45f)) / 0.38f;
                float k = EZ.EaseOutCubic(local);
                if (it.cg != null) it.cg.alpha = k;
                if (it.move) it.rt.anchoredPosition = it.rest + new Vector2(0f, -32f * (1f - k));
                if (local < 1f) done = false;
            }
            if (title != null)
                title.characterSpacing = Mathf.Lerp(titleSpacing + 26f, titleSpacing, EZ.EaseOutCubic(e / 0.8f));
            if (done && e > 0.3f) break;
            yield return null;
        }
        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            if (it.rt == null) continue;
            if (it.cg != null) it.cg.alpha = 1f;
            if (it.move) it.rt.anchoredPosition = it.rest;
        }
        if (title != null) title.characterSpacing = titleSpacing;
        enterAlpha = 1f;
    }

    void Collect()
    {
        items.Clear();
        var panel = transform as RectTransform;
        if (panel == null) return;
        bool hasLayout = panel.GetComponent<LayoutGroup>() != null;
        foreach (Transform ch in transform)
        {
            var rt = ch as RectTransform;
            if (rt == null || !rt.gameObject.activeSelf) continue;
            if (rt.GetComponentInChildren<EZCarouselHandle>(true) != null) continue;   // skin cards
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            bool pointAnchored = rt.anchorMin == rt.anchorMax;
            items.Add(new Item { rt = rt, cg = cg, rest = rt.anchoredPosition, move = pointAnchored && !hasLayout });
        }

        // title = largest non-button text near the top
        title = null;
        float best = 0f;
        foreach (var t in GetComponentsInChildren<TMP_Text>(false))
        {
            if (EZ.IsEZ(t.transform) || t.GetComponentInParent<Selectable>() != null) continue;
            if (t.fontSize > best) { best = t.fontSize; title = t; }
        }
        if (title != null && best < 40f) title = null;
        if (title != null) titleSpacing = title.characterSpacing;
    }
}

public sealed class EZButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    bool primary, hover, down, wasInteractable = true, selected;
    Image fill, border, glow;
    Color fillCol, hoverCol, borderCol;
    Button btn;
    Vector3 baseScale = Vector3.one;
    float k = 1f, phase;

    public void Configure(bool isPrimary, Image fillImg, Image borderImg, Image glowImg, Color fillC, Color hoverC, Color borderC)
    {
        primary = isPrimary;
        fill = fillImg;
        border = borderImg;
        glow = glowImg;
        fillCol = fillC;
        hoverCol = hoverC;
        borderCol = borderC;
    }

    /// <summary>Gives the button a persistent "selected" look (bright border + glow + tinted
    /// fill) that survives this component's own per-frame hover/press animation. Safe to call
    /// every frame; only reacts to changes.</summary>
    public void SetSelected(bool value) { selected = value; }

    void Awake()
    {
        btn = GetComponent<Button>();
        baseScale = transform.localScale;
        phase = Random.value * 6.28f;
    }

    void OnEnable()
    {
        hover = down = false;
        k = 1f;
        transform.localScale = baseScale;
    }

    void OnDisable() { transform.localScale = baseScale; }

    public void OnPointerEnter(PointerEventData e) { hover = true; }
    public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
    public void OnPointerDown(PointerEventData e) { down = true; }
    public void OnPointerUp(PointerEventData e) { down = false; }

    void SetVisualAlpha(float a)
    {
        foreach (var g in GetComponentsInChildren<Graphic>(true)) g.canvasRenderer.SetAlpha(a);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;
        bool interact = btn == null || btn.IsInteractable();
        if (interact != wasInteractable)
        {
            wasInteractable = interact;
            SetVisualAlpha(interact ? 1f : 0.4f);
        }

        float target = !interact ? 1f : down ? 0.95f : hover ? 1.04f : 1f;
        k = Mathf.Lerp(k, target, 1f - Mathf.Exp(-18f * dt));
        float breathe = (primary && interact && EZTheme.MotionEnabled) ? 1f + 0.012f * Mathf.Sin(t * 2.4f + phase) : 1f;
        transform.localScale = baseScale * (k * breathe);

        if (glow != null)
        {
            float ga = !interact ? 0f : selected ? 0.55f : primary ? 0.42f + 0.22f * Mathf.Sin(t * 2.4f + phase) : (hover || down ? 0.5f : 0f);
            Color gc = glow.color;
            gc.a = Mathf.Lerp(gc.a, ga, 1f - Mathf.Exp(-10f * dt));
            glow.color = gc;
        }
        if (border != null)
        {
            Color targetBorder = selected ? EZTheme.Accent : borderCol;
            float targetA = selected ? 1f : (hover || down ? 1f : borderCol.a);
            Color bc = border.color;
            bc = Color.Lerp(bc, targetBorder, 1f - Mathf.Exp(-12f * dt));
            bc.a = Mathf.Lerp(border.color.a, targetA, 1f - Mathf.Exp(-12f * dt));
            border.color = bc;
        }
        if (fill != null)
        {
            Color targetFill = selected ? EZTheme.WithAlpha(EZTheme.Accent, 0.30f) : (hover || down ? hoverCol : fillCol);
            fill.color = Color.Lerp(fill.color, targetFill, 1f - Mathf.Exp(-14f * dt));
        }
    }
}

public sealed class EZLogoFX : MonoBehaviour
{
    public TMP_Text entropy, zero, tagline;
    RectTransform rt;
    float t0, nextGlitch, glitchUntil, lastBob;
    bool introDone;

    void Awake() { rt = transform as RectTransform; }

    void OnEnable()
    {
        t0 = Time.unscaledTime;
        nextGlitch = t0 + Random.Range(6f, 10f);
        glitchUntil = 0f;
        lastBob = 0f;
        introDone = !EZTheme.MotionEnabled;
        if (introDone) ApplyFinal();
    }

    void OnDisable() { lastBob = 0f; }

    static void SetAlpha(TMP_Text t, float a)
    {
        if (t == null) return;
        Color c = t.color;
        c.a = Mathf.Clamp01(a);
        t.color = c;
    }

    void ApplyFinal()
    {
        if (entropy == null) return;
        entropy.characterSpacing = 16f; SetAlpha(entropy, 1f);
        zero.characterSpacing = 46f; SetAlpha(zero, 1f);
        SetAlpha(tagline, 1f);
    }

    void Update()
    {
        if (entropy == null || zero == null) return;
        float now = Time.unscaledTime;
        float e = now - t0;

        if (!introDone)
        {
            float k1 = EZ.EaseOutCubic(e / 1.1f);
            float k2 = EZ.EaseOutCubic((e - 0.2f) / 1.1f);
            float k3 = EZ.EaseOutCubic((e - 0.5f) / 0.8f);
            entropy.characterSpacing = Mathf.Lerp(40f, 16f, k1); SetAlpha(entropy, k1);
            zero.characterSpacing = Mathf.Lerp(80f, 46f, k2); SetAlpha(zero, k2);
            SetAlpha(tagline, k3);
            if (e > 1.6f) { introDone = true; ApplyFinal(); }
        }

        if (!EZTheme.MotionEnabled || e < 1.2f || rt == null) return;

        float bob = Mathf.Sin(now * 0.9f) * 5f;                      // idle float (delta-applied so it never fights layout)
        rt.anchoredPosition += new Vector2(0f, bob - lastBob);
        lastBob = bob;

        if (now >= nextGlitch)
        {
            glitchUntil = now + 0.12f;
            nextGlitch = now + Random.Range(8f, 15f);
        }
        bool glitch = now < glitchUntil;
        var zr = zero.rectTransform;
        zr.anchoredPosition = new Vector2(glitch ? Random.Range(-9f, 9f) : 0f, zr.anchoredPosition.y);
        SetAlpha(entropy, glitch ? 0.55f : 1f);
    }
}

public sealed class EZTogglePill : MonoBehaviour
{
    Graphic check;
    Image track, knob;
    float pos;
    bool built;

    public void Build(Button button)
    {
        if (built) return;
        built = true;

        // the legacy checkmark: hide it, but keep reading it (SettingsMenu keeps updating it)
        foreach (var g in button.GetComponentsInChildren<Graphic>(true))
        {
            if (EZ.IsEZ(g.transform) || g.gameObject == button.gameObject) continue;
            if (g is Text) { check = g; break; }
            if (g is Image && g.name.ToUpperInvariant().Contains("CHECK")) { check = g; break; }
        }
        if (check != null) check.transform.localScale = Vector3.zero;

        track = EZ.Img(button.transform, "EZ Pill", EZSprites.Rounded, new Color(1f, 1f, 1f, 0.16f), true);
        var tr = track.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(1f, 0.5f);
        tr.pivot = new Vector2(1f, 0.5f);
        tr.sizeDelta = new Vector2(96f, 48f);
        tr.anchoredPosition = new Vector2(-28f, 0f);
        EZ.Ignore(track.gameObject);

        knob = EZ.Img(track.transform, "EZ Knob", EZSprites.Disc, EZTheme.Ink);
        EZ.Center(knob.rectTransform, new Vector2(36f, 36f), new Vector2(-24f, 0f));
        pos = ReadState() ? 1f : 0f;
    }

    bool ReadState()
    {
        if (check == null) return true;
        var tx = check as Text;
        if (tx != null)
            return tx.enabled && tx.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(tx.text) && tx.color.a > 0.1f;
        return check.enabled && check.gameObject.activeInHierarchy && check.color.a > 0.1f;
    }

    void Update()
    {
        if (track == null || knob == null) return;
        bool on = ReadState();
        pos = Mathf.Lerp(pos, on ? 1f : 0f, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        knob.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-24f, 24f, pos), 0f);
        track.color = Color.Lerp(new Color(1f, 1f, 1f, 0.16f), EZTheme.WithAlpha(EZTheme.Accent, 0.85f), pos);
    }
}
