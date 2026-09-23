// ============================================================================
// ENTROPY ZERO UI KIT  (5 of 5)  EZLayouts.cs
//   EZLayouts       - re-positions the known panels (by their existing object names) so
//                     nothing overlaps: same Back button + title position on every screen.
//                     Objects are MOVED, never destroyed or re-parented, so every
//                     serialized reference and OnClick stays intact.
//   EZSkinCarousel  - brings back the "one skin in front at a time" carousel:
//                     swipe / arrows / tap a neighbour; focused card is 100% interactive.
//   EZCarouselHandle- small drag/click forwarder used by the carousel.
// Layout runs every time a panel is enabled (after other scripts have done their own layout).
// ============================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class EZLayouts
{
    static readonly HashSet<string> Flow = new HashSet<string>
    {
        "MainMenu", "StartMenu", "MuiltiplayerMenu", "JoinLobbyPanel", "HostLobbyPanel", "OptionsMenu", "AbilitiesMenu"
    };

    public static bool IsFlow(string n) { return Flow.Contains(n); }
    public static bool IsResult(string n) { return n == "Lost" || n == "Won"; }

    public static void Apply(RectTransform panel)
    {
        if (panel == null || panel.rect.width < 10f || panel.rect.height < 10f) return;
        try
        {
            switch (panel.name)
            {
                case "MainMenu": MainMenu(panel); break;
                case "StartMenu": StartMenu(panel); break;
                case "MuiltiplayerMenu": Multiplayer(panel); break;
                case "JoinLobbyPanel":
                case "HostLobbyPanel": Header(panel); break;
                case "OptionsMenu": Options(panel); break;
                case "AbilitiesMenu": Abilities(panel); break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[EZ] Layout of '" + panel.name + "' skipped: " + e.Message);
        }
    }

    // ------------------------------------------------------------------ helpers
    static Button Find(RectTransform p, params string[] keys)
    {
        foreach (var b in p.GetComponentsInChildren<Button>(false))
        {
            if (EZ.IsEZ(b.transform)) continue;
            string k = (b.name + " " + EZ.LabelOf(b.transform)).ToUpperInvariant();
            foreach (var key in keys)
                if (k.Contains(key)) return b;
        }
        return null;
    }

    static TMP_Text FindText(RectTransform p, params string[] keys)
    {
        foreach (var t in p.GetComponentsInChildren<TMP_Text>(false))
        {
            if (EZ.IsEZ(t.transform) || t.GetComponentInParent<Selectable>() != null) continue;
            string u = t.text.ToUpperInvariant();
            foreach (var k in keys)
                if (u.Contains(k)) return t;
        }
        return null;
    }

    static void Own(RectTransform rt)
    {
        Transform parent = rt.parent;
        if (parent != null)
        {
            foreach (var lg in parent.GetComponents<LayoutGroup>()) lg.enabled = false;
            var csfp = parent.GetComponent<ContentSizeFitter>();
            if (csfp != null) csfp.enabled = false;
        }
        var csf = rt.GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;
    }

    /// <summary>Puts a UI element at (cx, cy) from the panel centre with the given size (works even if it lives in another parent).</summary>
    static void Place(RectTransform panel, Component c, float cx, float cy, float w, float h)
    {
        if (c == null) return;
        var rt = c.transform as RectTransform;
        if (rt == null) return;
        Own(rt);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        Vector3 wp = panel.TransformPoint(new Vector3(panel.rect.center.x + cx, panel.rect.center.y + cy, 0f));
        wp.z = rt.position.z;
        rt.position = wp;
    }

    static void FitLabel(TMP_Text t, float min, float max, TextAlignmentOptions align)
    {
        if (t == null) return;
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
        t.alignment = align;
        t.overflowMode = TextOverflowModes.Ellipsis;
    }

    /// <summary>Title (bigger) + caption (smaller) inside a button get their own non-overlapping bands.</summary>
    static void LayoutButtonTexts(Button b)
    {
        if (b == null) return;
        var texts = new List<TMP_Text>();
        foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
            if (!EZ.IsEZ(t.transform)) texts.Add(t);
        if (texts.Count == 0) return;
        texts.Sort((x, y) => y.fontSize.CompareTo(x.fontSize));

        if (texts.Count == 1)
        {
            EZ.Stretch(texts[0].rectTransform, 20f, 8f, 20f, 8f);
            FitLabel(texts[0], 22f, 46f, TextAlignmentOptions.Center);
            return;
        }
        RectTransform tr = texts[0].rectTransform;
        tr.anchorMin = new Vector2(0f, 0.42f); tr.anchorMax = new Vector2(1f, 0.90f);
        tr.offsetMin = new Vector2(24f, 0f); tr.offsetMax = new Vector2(-24f, 0f);
        FitLabel(texts[0], 24f, 56f, TextAlignmentOptions.Center);

        RectTransform cr = texts[1].rectTransform;
        cr.anchorMin = new Vector2(0f, 0.08f); cr.anchorMax = new Vector2(1f, 0.38f);
        cr.offsetMin = new Vector2(24f, 0f); cr.offsetMax = new Vector2(-24f, 0f);
        FitLabel(texts[1], 16f, 28f, TextAlignmentOptions.Center);
    }

    /// <summary>Same header on every sub-screen: BACK top-left, title + subtitle to its right.</summary>
    static void Header(RectTransform p)
    {
        float w = p.rect.width, h = p.rect.height;
        float left = -w * 0.5f + 56f, top = h * 0.5f - 48f;
        Button back = Find(p, "BACK", "CLOSE");
        if (back != null)
        {
            Place(p, back, left + 125f, top - 42f, 250f, 84f);
            LayoutButtonTexts(back);
        }

        float limitY = p.rect.center.y + h * 0.18f;
        TMP_Text title = null;
        foreach (var t in p.GetComponentsInChildren<TMP_Text>(false))
        {
            if (EZ.IsEZ(t.transform) || t.GetComponentInParent<Selectable>() != null) continue;
            if (p.InverseTransformPoint(t.transform.position).y < limitY) continue;
            if (title == null || t.fontSize > title.fontSize) title = t;
        }
        if (title == null || title.fontSize < 36f) return;

        TMP_Text sub = null;
        // This project names every panel's caption text "Subtitle" (a child of a "*Heading"
        // container that also holds the title). That exact name is a far more reliable signal
        // than the position guess below, which can miss it when the heading container's own
        // anchoring puts the caption outside the expected band (seen on OptionsMenu, where the
        // untouched "Subtitle" ended up overlapping "ACCESSIBILITY").
        foreach (var t in p.GetComponentsInChildren<TMP_Text>(false))
        {
            if (t == title || EZ.IsEZ(t.transform)) continue;
            if (t.gameObject.name == "Subtitle") { sub = t; break; }
        }
        if (sub == null)
        {
            float titleY = p.InverseTransformPoint(title.transform.position).y;
            float bestGap = float.MaxValue;
            foreach (var t in p.GetComponentsInChildren<TMP_Text>(false))
            {
                if (t == title || EZ.IsEZ(t.transform) || t.GetComponentInParent<Selectable>() != null) continue;
                if (t.fontSize > title.fontSize * 0.75f) continue;
                float y = p.InverseTransformPoint(t.transform.position).y;
                if (y < limitY - h * 0.1f) continue;
                float gap = titleY - y;
                if (gap > 0f && gap < bestGap) { bestGap = gap; sub = t; }
            }
        }

        float tx = (back != null ? left + 250f + 40f : left) + 480f;
        Place(p, title, tx, top - 34f, 960f, 84f);
        FitLabel(title, 28f, 64f, TextAlignmentOptions.Left);
        if (sub != null)
        {
            Place(p, sub, tx, top - 100f, 960f, 40f);
            FitLabel(sub, 18f, 28f, TextAlignmentOptions.Left);
        }
    }

    // ------------------------------------------------------------------ panels
    static EZLogoFX EnsureLogo(RectTransform p)
    {
        Transform existing = p.Find("EZ Logo");
        if (existing != null) return existing.GetComponent<EZLogoFX>();

        var go = new GameObject("EZ Logo", typeof(RectTransform));
        go.transform.SetParent(p, false);
        var fx = go.AddComponent<EZLogoFX>();
        var a = EZ.Txt(go.transform, "EZ Entropy", "ENTROPY", 120f, EZTheme.Ink, TextAlignmentOptions.Left, true);
        var b = EZ.Txt(go.transform, "EZ Zero", "ZERO", 120f, EZTheme.Accent, TextAlignmentOptions.Left, true);
        var c = EZ.Txt(go.transform, "EZ Tagline", "CONTAIN THE INSTABILITY", 26f, EZTheme.Muted, TextAlignmentOptions.Left, false);
        c.characterSpacing = 10f;
        EZ.Center(a.rectTransform, new Vector2(1200f, 150f), new Vector2(0f, 150f));
        EZ.Center(b.rectTransform, new Vector2(1200f, 150f), new Vector2(0f, 22f));
        EZ.Center(c.rectTransform, new Vector2(1200f, 44f), new Vector2(0f, -108f));
        fx.entropy = a;
        fx.zero = b;
        fx.tagline = c;
        return fx;
    }

    static void MainMenu(RectTransform p)
    {
        float w = p.rect.width;
        float leftX = -w * 0.5f + Mathf.Max(96f, w * 0.05f);

        // hide the previous attempt's title / tagline (we draw our own animated logo)
        foreach (var t in p.GetComponentsInChildren<TMP_Text>(true))
        {
            if (EZ.IsEZ(t.transform) || t.GetComponentInParent<Button>(true) != null) continue;
            string u = t.text.ToUpperInvariant();
            if (u.Contains("ENTROPY") || u.Contains("BOUNDARY") || u.Contains("CONTAIN THE INSTABILITY"))
                t.gameObject.SetActive(false);
        }

        var logo = EnsureLogo(p);
        Place(p, logo, leftX + 600f, 130f, 1200f, 480f);

        Button play = null;
        foreach (var b in p.GetComponentsInChildren<Button>(false))
        {
            if (EZ.IsEZ(b.transform)) continue;
            string k = (b.name + " " + EZ.LabelOf(b.transform)).ToUpperInvariant();
            if (k.Contains("PLAY") && !k.Contains("AGAIN")) { play = b; break; }
        }
        Button skins = Find(p, "SKIN");
        Button options = Find(p, "OPTION", "SETTING");

        Place(p, play, leftX + 260f, -110f, 520f, 120f);
        Place(p, skins, leftX + 125f, -235f, 250f, 88f);
        Place(p, options, leftX + 395f, -235f, 250f, 88f);
        LayoutButtonTexts(play);
        LayoutButtonTexts(skins);
        LayoutButtonTexts(options);
    }

    static void StartMenu(RectTransform p)
    {
        Header(p);
        float cw = Mathf.Min(960f, p.rect.width - 160f);
        Button a = Find(p, "SERVER", "MULTI");
        Button b = Find(p, "ABILIT");
        Button c = Find(p, "SKIN");
        Place(p, a, 0f, 185f, cw, 150f);
        Place(p, b, 0f, 0f, cw, 150f);
        Place(p, c, 0f, -185f, cw, 150f);
        LayoutButtonTexts(a);
        LayoutButtonTexts(b);
        LayoutButtonTexts(c);
    }

    static void Multiplayer(RectTransform p)
    {
        Header(p);
        Button host = Find(p, "HOST", "CREATE");
        Button join = Find(p, "JOIN");
        Button prac = Find(p, "PRACTICE");
        float cw = Mathf.Min(640f, (p.rect.width - 240f) * 0.5f);
        Place(p, host, -(cw * 0.5f + 24f), 40f, cw, 320f);
        Place(p, join, cw * 0.5f + 24f, 40f, cw, 320f);
        Place(p, prac, 0f, -205f, cw * 2f + 48f, 96f);
        LayoutButtonTexts(host);
        LayoutButtonTexts(join);
        LayoutButtonTexts(prac);
    }

    static void Options(RectTransform p)
    {
        Header(p);
        float h = p.rect.height;
        float col = Mathf.Min(760f, p.rect.width - 200f);
        TMP_Text optionsTitle = p.Find("OptionsHeading/Title")?.GetComponent<TMP_Text>();
        if (optionsTitle != null)
        {
            float titleY = p.InverseTransformPoint(optionsTitle.transform.position).y;
            Place(p, optionsTitle, 0f, titleY, col + 100f, 84f);
            FitLabel(optionsTitle, 28f, 64f, TextAlignmentOptions.Center);
        }

        // background card first, so the controls placed afterwards sit on top of it
        Image card = null;
        float bestArea = 0f;
        foreach (var img in p.GetComponentsInChildren<Image>(false))
        {
            if (EZ.IsEZ(img.transform) || img.GetComponentInParent<Selectable>() != null) continue;
            Vector2 s = img.rectTransform.rect.size;
            if (s.x >= p.rect.width * 0.9f && s.y >= p.rect.height * 0.9f) continue;   // full-screen backdrop
            float area = s.x * s.y;
            if (area > bestArea) { bestArea = area; card = img; }
        }

        TMP_Text volHead = p.Find("VolumeLabel")?.GetComponent<TMP_Text>();
        TMP_Text subtitle = p.Find("OptionsHeading/Subtitle")?.GetComponent<TMP_Text>();
        TMP_Text accHead = p.Find("AccessibilityLabel")?.GetComponent<TMP_Text>();
        Slider slider = p.GetComponentInChildren<Slider>(false);
        Button shake = Find(p, "SHAKE", "DAMAGE");
        Canvas ownerCanvas = p.GetComponentInParent<Canvas>();
        Button edit = ownerCanvas?.transform.Find("Edit ControlsButton")?.GetComponent<Button>();
        Button other = ownerCanvas?.transform.Find("OtherInformationButton")?.GetComponent<Button>();

        // Lay out every row from one cursor. The subtitle includes the word
        // "ACCESSIBILITY", so broad text searches must not select it as the heading.
        float[] heights = { 44f, 48f, 32f, 40f, 88f, 96f, 96f };
        float[] gaps = { 24f, 34f, 22f, 32f, 80f, 80f };
        float totalHeight = 0f;
        foreach (float rowHeight in heights) totalHeight += rowHeight;
        foreach (float rowGap in gaps) totalHeight += rowGap;
        float scale = Mathf.Min(1f, (h - 230f) / totalHeight);
        float y = h * 0.5f - 180f * scale;
        float[] centers = new float[heights.Length];
        for (int i = 0; i < heights.Length; i++)
        {
            heights[i] *= scale;
            y -= heights[i] * 0.5f;
            centers[i] = y;
            y -= heights[i] * 0.5f;
            if (i < gaps.Length) y -= gaps[i] * scale;
        }

        Place(p, volHead, 0f, centers[0], col, heights[0]);
        FitLabel(volHead, 20f, 30f, TextAlignmentOptions.Left);
        Place(p, slider, 0f, centers[1], col, heights[1]);
        Place(p, subtitle, 0f, centers[2], col, heights[2]);
        FitLabel(subtitle, 18f, 28f, TextAlignmentOptions.Left);
        Place(p, accHead, 0f, centers[3], col, heights[3]);
        FitLabel(accHead, 18f, 26f, TextAlignmentOptions.Left);
        Place(p, shake, 0f, centers[4], col, heights[4]);
        Place(p, edit, 0f, centers[5], col, heights[5]);
        Place(p, other, 0f, centers[6], col, heights[6]);
        float cardTop = centers[0] + heights[0] * 0.5f + 48f * scale;
        float cardBottom = centers[6] - heights[6] * 0.5f - 48f * scale;
        Place(p, card, 0f, (cardTop + cardBottom) * 0.5f, col + 100f, cardTop - cardBottom);
        LayoutButtonTexts(edit);
        LayoutButtonTexts(other);

        if (shake != null)
        {
            var pill = shake.GetComponent<EZTogglePill>();
            if (pill == null)
            {
                pill = shake.gameObject.AddComponent<EZTogglePill>();
                pill.Build(shake);
            }
            // The scene uses a legacy Text caption; its leftover TMP labels are stray.
            foreach (var t in shake.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!EZ.IsEZ(t.transform)) t.gameObject.SetActive(false);
            }
            // Size the legacy caption to match the other Options labels.
            foreach (var l in shake.GetComponentsInChildren<Text>(true))
            {
                if (EZ.IsEZ(l.transform) || (l.text ?? "").Trim() != "DAMAGE SCREEN SHAKE") continue;
                EZ.Stretch(l.rectTransform, 32f, 8f, 150f, 8f);
                l.alignment = TextAnchor.MiddleLeft;
                l.resizeTextForBestFit = false;
                l.fontSize = 32;
                break;
            }
        }
    }

    static void Abilities(RectTransform p)
    {
        Header(p);
        float w = p.rect.width, h = p.rect.height;
        Button back = Find(p, "BACK", "CLOSE");
        Button info = Find(p, "INFO");

        var list = new List<Button>();
        Button slice = null;
        foreach (var b in p.GetComponentsInChildren<Button>(false))
        {
            if (EZ.IsEZ(b.transform) || b == back || b == info) continue;
            string k = (b.name + " " + EZ.LabelOf(b.transform)).ToUpperInvariant();
            if (k.Contains("BACK") || k.Contains("CLOSE")) continue;
            if (k.Contains("SLICE") || k.Contains("SLIDE")) { slice = b; continue; }
            list.Add(b);
        }
        list.Sort((x, y) => x.transform.GetSiblingIndex().CompareTo(y.transform.GetSiblingIndex()));
        if (slice != null) list.Add(slice);

        int n = list.Count;
        if (n > 0)
        {
            float gridW = w - 200f;
            int cols = gridW >= 1500f ? 4 : 3;
            float gap = 24f, chh = 116f;
            float cw = Mathf.Min(400f, (gridW - gap * (cols - 1)) / cols);
            int rows = Mathf.CeilToInt(n / (float)cols);
            float totalH = rows * chh + (rows - 1) * gap;
            float y0 = -10f;
            for (int i = 0; i < n; i++)
            {
                int r = i / cols, c = i % cols;
                int inRow = Mathf.Min(cols, n - r * cols);
                float x = (c - (inRow - 1) * 0.5f) * (cw + gap);
                float y = y0 + totalH * 0.5f - chh * 0.5f - r * (chh + gap);
                Place(p, list[i], x, y, cw, chh);
                LayoutButtonTexts(list[i]);
            }
        }

        Place(p, info, 0f, -h * 0.5f + 100f, 300f, 88f);
        LayoutButtonTexts(info);
        TMP_Text tip = FindText(p, "TIP");
        Place(p, tip, 0f, -h * 0.5f + 190f, Mathf.Min(1200f, w - 200f), 40f);
        FitLabel(tip, 16f, 26f, TextAlignmentOptions.Center);
    }
}

// ----------------------------------------------------------------------------
// Skin carousel
// ----------------------------------------------------------------------------
public sealed class EZCarouselHandle : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public EZSkinCarousel owner;
    public int index = -1;

    public void OnPointerClick(PointerEventData e) { if (owner != null && index >= 0) owner.Focus(index); }
    public void OnBeginDrag(PointerEventData e) { if (owner != null) owner.BeginDrag(); }
    public void OnDrag(PointerEventData e) { if (owner != null) owner.Drag(e.delta.x); }
    public void OnEndDrag(PointerEventData e) { if (owner != null) owner.EndDrag(); }
}

public sealed class EZSkinCarousel : MonoBehaviour
{
    static readonly string[] Names = { "BEARD", "TURTLE", "SUN DUCKER" };
    static EZSkinCarousel instance;

    readonly List<RectTransform> cards = new List<RectTransform>();
    readonly List<CanvasGroup> groups = new List<CanvasGroup>();
    readonly List<Image> catchers = new List<Image>();
    readonly List<Vector3> baseScales = new List<Vector3>();
    readonly List<Image> dots = new List<Image>();
    // Buttons that already lived on the panel before the carousel attached (e.g. a
    // Close/Back button). The full-panel "EZ Swipe" catcher added below needs to sit
    // above the plain background to catch drags, but that put it - and, after
    // ApplyOrder() reshuffles cards/prev/next/dots on top of it, everything the
    // carousel doesn't own - permanently underneath it, so its Close button stopped
    // receiving clicks. Re-elevating these every time ApplyOrder() runs keeps them
    // clickable regardless of how the carousel's own pieces get reordered.
    readonly List<Transform> chromeButtons = new List<Transform>();
    RectTransform box, swipe;
    Button prev, next;
    Canvas rootCanvas;
    float current, vel, spacing, lastVel, cardH;
    int target = -1;
    bool dragging;

    public static bool Owns(RectTransform rt)
    {
        return instance != null && instance.cards.Contains(rt);
    }

    // ---------------------------------------------------------------- discovery
    public static void EnsureAttached()
    {
        if (instance != null) return;
        var labels = new Transform[3];
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas) continue;
            labels[0] = labels[1] = labels[2] = null;
            foreach (var t in canvas.GetComponentsInChildren<TMP_Text>(true)) Match(t.text, t.transform, labels);
            foreach (var t in canvas.GetComponentsInChildren<Text>(true)) Match(t.text, t.transform, labels);
            if (labels[0] == null || labels[1] == null || labels[2] == null) continue;

            Transform lca = Lca(Lca(labels[0], labels[1]), labels[2]);
            if (lca == null || !(lca is RectTransform)) continue;

            var found = new List<RectTransform>();
            bool ok = true;
            for (int i = 0; i < 3; i++)
            {
                Transform c = labels[i];
                while (c != null && c.parent != lca) c = c.parent;
                var rt = c as RectTransform;
                if (rt == null || found.Contains(rt)) { ok = false; break; }
                found.Add(rt);
            }
            if (!ok) continue;

            var comp = lca.gameObject.AddComponent<EZSkinCarousel>();
            comp.Init(found);
            instance = comp;
            Debug.Log("[EZ] Skin carousel attached to '" + EZ.PathOf(lca) + "'.");
            return;
        }
    }

    static void Match(string text, Transform t, Transform[] labels)
    {
        if (string.IsNullOrEmpty(text)) return;
        string u = text.Trim().ToUpperInvariant();
        for (int i = 0; i < 3; i++)
            if (labels[i] == null && u == Names[i]) labels[i] = t;
    }

    static Transform Lca(Transform a, Transform b)
    {
        if (a == null || b == null) return null;
        var set = new HashSet<Transform>();
        for (Transform t = a; t != null; t = t.parent) set.Add(t);
        for (Transform t = b; t != null; t = t.parent) if (set.Contains(t)) return t;
        return null;
    }

    // ---------------------------------------------------------------- setup
    void Init(List<RectTransform> list)
    {
        box = (RectTransform)transform;
        rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
        foreach (var lg in GetComponents<LayoutGroup>()) lg.enabled = false;
        var csf = GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;

        // Snapshot every button already on the panel (Close/Back, page arrows, etc.)
        // before the carousel adds its own swipe catcher/dots/arrows below.
        foreach (var btn in box.GetComponentsInChildren<Button>(true))
            chromeButtons.Add(btn.transform);

        float maxW = 0f;
        int start = 0;
        for (int i = 0; i < list.Count; i++)
        {
            RectTransform rt = list[i];
            Vector2 size = rt.rect.size;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            maxW = Mathf.Max(maxW, size.x);
            cardH = Mathf.Max(cardH, size.y);
            cards.Add(rt);
            baseScales.Add(rt.localScale);

            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            groups.Add(cg);

            var catcher = EZ.Img(rt, "EZ Catcher", null, new Color(0f, 0f, 0f, 0f));
            catcher.canvasRenderer.cullTransparentMesh = false;
            catcher.raycastTarget = true;
            EZ.Stretch(catcher.rectTransform);
            EZ.Ignore(catcher.gameObject);
            var handle = catcher.gameObject.AddComponent<EZCarouselHandle>();
            handle.owner = this;
            handle.index = i;
            catchers.Add(catcher);

            if (EZ.LabelOf(rt).Contains("EQUIPPED")) start = i;
        }
        spacing = maxW * 1.06f;

        swipe = EZ.Img(box, "EZ Swipe", null, new Color(0f, 0f, 0f, 0f)).rectTransform;
        var swImg = swipe.GetComponent<Image>();
        swImg.canvasRenderer.cullTransparentMesh = false;
        swImg.raycastTarget = true;
        EZ.Stretch(swipe);
        EZ.Ignore(swipe.gameObject);
        var sh = swipe.gameObject.AddComponent<EZCarouselHandle>();
        sh.owner = this;
        sh.index = -1;

        prev = EZRestyle.MakeButton(box, "SkinPrev", "<", new Vector2(96f, 96f), EZRestyle.Variant.Secondary, () => Focus(target - 1));
        next = EZRestyle.MakeButton(box, "SkinNext", ">", new Vector2(96f, 96f), EZRestyle.Variant.Secondary, () => Focus(target + 1));
        float ax = spacing * 1.62f;
        EZ.Center((RectTransform)prev.transform, new Vector2(96f, 96f), new Vector2(-ax, 0f));
        EZ.Center((RectTransform)next.transform, new Vector2(96f, 96f), new Vector2(ax, 0f));

        for (int i = 0; i < cards.Count; i++)
        {
            var d = EZ.Img(box, "EZ Dot", EZSprites.Disc, EZTheme.Muted);
            EZ.Center(d.rectTransform, new Vector2(16f, 16f),
                new Vector2((i - (cards.Count - 1) * 0.5f) * 32f, -(cardH * 0.62f + 44f)));
            dots.Add(d);
        }

        target = start;
        current = start;
        ApplyOrder();
    }

    void ApplyOrder()
    {
        if (cards.Count == 0) return;
        swipe.SetAsLastSibling();
        var order = new List<int>();
        for (int i = 0; i < cards.Count; i++) order.Add(i);
        order.Sort((a, b) => Mathf.Abs(b - target).CompareTo(Mathf.Abs(a - target)));   // farthest first, focused card last = on top
        foreach (int i in order) cards[i].SetAsLastSibling();
        prev.transform.SetAsLastSibling();
        next.transform.SetAsLastSibling();
        foreach (var d in dots) d.transform.SetAsLastSibling();
        // Always finish on top so a pre-existing Close/Back button never ends up
        // stuck behind the swipe catcher (see the chromeButtons field comment).
        foreach (var t in chromeButtons) if (t != null) t.SetAsLastSibling();
    }

    // ---------------------------------------------------------------- input
    public void Focus(int i)
    {
        int t = Mathf.Clamp(i, 0, cards.Count - 1);
        if (t == target) return;
        target = t;
        ApplyOrder();
    }

    public void BeginDrag() { dragging = true; vel = 0f; lastVel = 0f; }

    public void Drag(float deltaX)
    {
        float d = deltaX / Mathf.Max(0.01f, rootCanvas.scaleFactor);
        current = Mathf.Clamp(current - d / spacing, -0.35f, cards.Count - 0.65f);
        lastVel = Mathf.Lerp(lastVel, d / Mathf.Max(Time.unscaledDeltaTime, 0.001f), 0.5f);
    }

    public void EndDrag()
    {
        dragging = false;
        int t = Mathf.Clamp(Mathf.RoundToInt(current - lastVel / spacing * 0.12f), 0, cards.Count - 1);
        lastVel = 0f;
        if (t != target) { target = t; ApplyOrder(); }
    }

    // ---------------------------------------------------------------- animation
    void Update()
    {
        if (cards.Count == 0) return;
        float dt = Time.unscaledDeltaTime;
        if (!dragging) current = Mathf.SmoothDamp(current, target, ref vel, 0.18f, Mathf.Infinity, dt);

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null) continue;
            float o = i - current, ao = Mathf.Abs(o);
            float near = Mathf.Clamp01(ao);
            float x = Mathf.Sign(o) * (ao <= 1f ? ao * spacing : spacing + (ao - 1f) * spacing * 0.6f);
            cards[i].anchoredPosition = new Vector2(x, 0f);
            cards[i].localScale = baseScales[i] * Mathf.Lerp(1.10f, 0.78f, near);
            bool focus = ao < 0.5f;
            groups[i].alpha = Mathf.Lerp(1f, 0.42f, near);
            groups[i].interactable = focus;
            groups[i].blocksRaycasts = true;
            if (catchers[i].raycastTarget == focus) catchers[i].raycastTarget = !focus;   // neighbours: tap to focus
        }

        prev.interactable = target > 0;
        next.interactable = target < cards.Count - 1;
        for (int i = 0; i < dots.Count; i++)
        {
            float f = 1f - Mathf.Clamp01(Mathf.Abs(i - current));
            dots[i].color = Color.Lerp(EZTheme.WithAlpha(EZTheme.Muted, 0.5f), EZTheme.Accent, f);
            dots[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.5f, f);
        }
    }
}
