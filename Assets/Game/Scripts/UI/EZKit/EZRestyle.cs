// ============================================================================
// ENTROPY ZERO UI KIT  (3 of 5)  EZRestyle.cs
// One consistent look for every panel, including panels that other scripts generate
// at runtime (Skins, Practice, Other Information...). Re-run every 0.35 s; it only
// touches each object once, so scripts that later change colours are respected.
//
// Fixes seen in the screenshots:
//  * orange gradient / black-faced TMP text  -> clean white-face material, light colours
//  * flat black buttons                      -> glass buttons with border, glow, hover/press
//  * old grid full-screen images             -> removed / made transparent
//
// OPTIONAL FONTS: put Orbitron.ttf (titles) and Exo2.ttf (body) in Assets/Resources/EZFonts/
// (exact names). They are turned into TMP font assets at runtime; nothing else to do.
// ============================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class EZRestyle
{
    public enum Variant { Primary, Secondary, Danger }

    static readonly HashSet<int> styled = new HashSet<int>();
    static readonly Dictionary<int, Material> mats = new Dictionary<int, Material>();
    public static TMP_FontAsset TitleFont, BodyFont;
    static bool fontsLoaded;

    public static void Reset()
    {
        styled.Clear();
        mats.Clear();
    }

    // ------------------------------------------------------------------ fonts
    static void LoadFonts()
    {
        if (fontsLoaded) return;
        fontsLoaded = true;
        TitleFont = TryFont("EZFonts/Orbitron");
        BodyFont = TryFont("EZFonts/Exo2");
    }

    static TMP_FontAsset TryFont(string path)
    {
        try
        {
            var asset = Resources.Load<TMP_FontAsset>(path + " SDF");
            if (asset != null) return asset;
            var font = Resources.Load<Font>(path);
            if (font == null) return null;
            var fa = TMP_FontAsset.CreateFontAsset(font);   // dynamic SDF atlas, filled on demand
            if (fa != null) fa.hideFlags = HideFlags.HideAndDontSave;
            return fa;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[EZ] Could not build font '" + path + "': " + e.Message);
            return null;
        }
    }

    // ------------------------------------------------------------------ text
    public static void SetupText(TMP_Text t, bool title)
    {
        LoadFonts();
        TMP_FontAsset f = (title && TitleFont != null) ? TitleFont : BodyFont;
        if (f != null && t.font != f) t.font = f;
        CleanMaterial(t);
    }

    /// <summary>The project's LiberationSans material has a black face colour baked in; use a clean white-face copy.</summary>
    public static void CleanMaterial(TMP_Text t)
    {
        TMP_FontAsset font = t.font;
        if (font == null || font.material == null) return;
        int id = font.GetInstanceID();
        Material m;
        if (!mats.TryGetValue(id, out m) || m == null)
        {
            m = new Material(font.material);
            m.name = font.name + " (EZ)";
            m.hideFlags = HideFlags.HideAndDontSave;
            if (m.HasProperty("_FaceColor")) m.SetColor("_FaceColor", Color.white);
            if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", 0f);
            if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 0f));
            m.DisableKeyword("UNDERLAY_ON");
            m.DisableKeyword("GLOW_ON");
            m.DisableKeyword("BEVEL_ON");
            mats[id] = m;
        }
        if (t.fontSharedMaterial != m) t.fontSharedMaterial = m;
    }

    static bool IsDark(Color c) { return c.a > 0.05f && Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < 0.62f; }

    static bool IsOrangeish(Color c)
    {
        float h, s, v;
        Color.RGBToHSV(c, out h, out s, out v);
        return s > 0.35f && v > 0.35f && h > 0.015f && h < 0.115f;
    }

    static void StyleText(TMP_Text t)
    {
        if (t == null || EZ.IsEZ(t.transform)) return;
        if (!styled.Add(t.GetInstanceID())) return;
        bool gradient = t.enableVertexGradient;
        if (gradient) t.enableVertexGradient = false;
        Color c = t.color;
        if (gradient || IsDark(c) || IsOrangeish(c))
            t.color = t.fontSize <= 27f ? EZTheme.Muted : EZTheme.Ink;
        SetupText(t, t.fontSize >= 56f);
    }

    static void StyleLegacyText(Text l)
    {
        if (l == null || EZ.IsEZ(l.transform)) return;
        if (!styled.Add(l.GetInstanceID())) return;
        foreach (var s in l.GetComponents<Shadow>()) s.enabled = false;   // includes Outline
        Color c = l.color;
        if (IsDark(c) || IsOrangeish(c)) l.color = l.fontSize <= 24 ? EZTheme.Muted : EZTheme.Ink;
    }

    // ------------------------------------------------------------------ buttons
    public static Variant Classify(string key)
    {
        if (key.Contains("DELETE")) return Variant.Danger;
        if (key.Contains("PLAY") || key.Contains("HOST") || key.Contains("CREATE")) return Variant.Primary;
        return Variant.Secondary;
    }

    static void StyleButton(Button b)
    {
        if (b == null || EZ.IsEZ(b.transform)) return;
        if (!styled.Add(b.GetInstanceID())) return;
        string key = (b.name + " " + EZ.LabelOf(b.transform)).ToUpperInvariant();
        ApplyButton(b, Classify(key));
    }

    public static void ApplyButton(Button b, Variant v)
    {
        Color fill, hover, borderCol, glowCol, textCol, capCol;
        switch (v)
        {
            case Variant.Primary:
                fill = EZTheme.Accent; hover = EZTheme.AccentHover; borderCol = EZTheme.WithAlpha(Color.white, 0.55f);
                glowCol = EZTheme.Accent; textCol = EZTheme.DarkOnAccent; capCol = EZTheme.WithAlpha(EZTheme.DarkOnAccent, 0.75f);
                break;
            case Variant.Danger:
                fill = EZTheme.Glass; hover = new Color32(60, 26, 40, 250); borderCol = EZTheme.WithAlpha(EZTheme.Danger, 0.7f);
                glowCol = EZTheme.Danger; textCol = new Color32(255, 130, 142, 255); capCol = EZTheme.Muted;
                break;
            default:
                fill = EZTheme.Glass; hover = EZTheme.GlassHover; borderCol = EZTheme.WithAlpha(EZTheme.Accent, 0.38f);
                glowCol = EZTheme.Accent; textCol = EZTheme.Ink; capCol = EZTheme.Muted;
                break;
        }

        var img = b.targetGraphic as Image;
        if (img == null) img = b.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = EZSprites.Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.material = null;
            img.color = fill;
            img.raycastTarget = true;
        }
        b.transition = Selectable.Transition.None;

        // border ring + outer glow (both non-interactive, ignored by layout groups)
        Image border = null, glow = null;
        Transform bt = b.transform.Find("EZ Border");
        if (bt != null) border = bt.GetComponent<Image>();
        else
        {
            border = EZ.Img(b.transform, "EZ Border", EZSprites.RoundedRing, borderCol, true);
            EZ.Stretch(border.rectTransform);
            EZ.Ignore(border.gameObject);
            border.transform.SetAsFirstSibling();
        }
        Transform gt = b.transform.Find("EZ Glow");
        if (gt != null) glow = gt.GetComponent<Image>();
        else
        {
            glow = EZ.Img(b.transform, "EZ Glow", EZSprites.OuterGlow, EZTheme.WithAlpha(glowCol, 0f), true);
            RectTransform gr = glow.rectTransform;
            gr.anchorMin = Vector2.zero;
            gr.anchorMax = Vector2.one;
            gr.offsetMin = new Vector2(-32f, -32f);
            gr.offsetMax = new Vector2(32f, 32f);
            EZ.Ignore(glow.gameObject);
            glow.transform.SetAsFirstSibling();
        }
        border.color = borderCol;
        glow.color = EZTheme.WithAlpha(glowCol, 0f);

        // labels: biggest text = title, next = caption
        var texts = new List<TMP_Text>();
        foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
            if (!EZ.IsEZ(t.transform)) texts.Add(t);
        texts.Sort((x, y) => y.fontSize.CompareTo(x.fontSize));
        for (int i = 0; i < texts.Count; i++)
        {
            TMP_Text t = texts[i];
            styled.Add(t.GetInstanceID());
            t.enableVertexGradient = false;
            t.color = i == 0 ? textCol : capCol;
            if (i == 0) t.characterSpacing = 3f;
            SetupText(t, false);
        }
        foreach (var l in b.GetComponentsInChildren<Text>(true))
        {
            if (EZ.IsEZ(l.transform)) continue;
            styled.Add(l.GetInstanceID());
            foreach (var s in l.GetComponents<Shadow>()) s.enabled = false;
            l.color = textCol;
        }

        var fx = b.GetComponent<EZButtonFX>();
        if (fx == null) fx = b.gameObject.AddComponent<EZButtonFX>();
        fx.Configure(v == Variant.Primary, img, border, glow, fill, hover, borderCol);
    }

    /// <summary>Gives a button a clear, persistent "selected" look (bright border + glow +
    /// tinted fill) without touching its label — call whenever a selection state changes
    /// (e.g. from AbilitiesSelectUI when an ability is toggled on/off). Safe to call every
    /// frame; only reacts to changes.</summary>
    public static void SetSelected(Button b, bool selected)
    {
        if (b == null) return;
        var fx = b.GetComponent<EZButtonFX>();
        if (fx == null) return;   // button hasn't been styled by EZ yet; nothing to highlight
        fx.SetSelected(selected);
    }

    /// <summary>Creates a fully styled button (used for the skin carousel arrows).</summary>
    public static Button MakeButton(Transform parent, string name, string label, Vector2 size, Variant v,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        var b = go.GetComponent<Button>();
        b.targetGraphic = go.GetComponent<Image>();
        var t = EZ.Txt(go.transform, "Label", label, 48f, EZTheme.Ink, TextAlignmentOptions.Center, false);
        t.name = "Label";
        EZ.Stretch(t.rectTransform);
        styled.Add(b.GetInstanceID());
        ApplyButton(b, v);
        if (onClick != null) b.onClick.AddListener(onClick);
        return b;
    }

    // ------------------------------------------------------------------ sliders
    static void StyleSlider(Slider s)
    {
        if (s == null || EZ.IsEZ(s.transform)) return;
        if (!styled.Add(s.GetInstanceID())) return;
        try
        {
            Transform bgT = s.transform.Find("Background");
            if (bgT != null)
            {
                var i = bgT.GetComponent<Image>();
                if (i != null)
                {
                    i.sprite = EZSprites.Rounded; i.type = Image.Type.Sliced; i.material = null;
                    i.color = new Color(1f, 1f, 1f, 0.12f);
                    RectTransform r = i.rectTransform;
                    r.anchorMin = new Vector2(0f, 0.5f);
                    r.anchorMax = new Vector2(1f, 0.5f);
                    r.offsetMin = new Vector2(r.offsetMin.x, -7f);
                    r.offsetMax = new Vector2(r.offsetMax.x, 7f);
                }
            }
            if (s.fillRect != null)
            {
                var i = s.fillRect.GetComponent<Image>();
                if (i != null)
                {
                    i.sprite = EZSprites.Rounded; i.type = Image.Type.Sliced; i.material = null; i.color = EZTheme.Accent;
                }
                var area = s.fillRect.parent as RectTransform;
                if (area != null && area != s.transform)
                {
                    area.anchorMin = new Vector2(0f, 0.5f);
                    area.anchorMax = new Vector2(1f, 0.5f);
                    area.offsetMin = new Vector2(area.offsetMin.x, -7f);
                    area.offsetMax = new Vector2(area.offsetMax.x, 7f);
                }
            }
            if (s.handleRect != null)
            {
                var i = s.handleRect.GetComponent<Image>();
                if (i != null)
                {
                    i.sprite = EZSprites.Disc; i.type = Image.Type.Simple; i.material = null; i.color = EZTheme.Ink;
                }
                s.handleRect.sizeDelta = new Vector2(44f, 44f);
            }
            s.transition = Selectable.Transition.None;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[EZ] Slider styling skipped: " + e.Message);
        }
    }

    // ------------------------------------------------------------------ images / cards
    static bool IsFlowName(string n) { return EZLayouts.IsFlow(n); }

    static void StyleImage(Image img, RectTransform rootRect)
    {
        if (img == null || EZ.IsEZ(img.transform)) return;
        if (img.GetComponentInParent<Selectable>(true) != null) return;   // buttons / sliders handled above
        int id = img.GetInstanceID();
        if (styled.Contains(id)) return;
        Vector2 size = img.rectTransform.rect.size;
        if (size.x <= 1f || size.y <= 1f) return;                          // layout not ready: retry next pass
        styled.Add(id);

        bool full = size.x >= rootRect.rect.width * 0.9f && size.y >= rootRect.rect.height * 0.9f;
        if (full)
        {
            string panel = EZ.PanelNameOf(img.transform);
            float a = IsFlowName(panel) ? 0f : (EZLayouts.IsResult(panel) ? 0.70f : 0.30f);
            img.sprite = null;
            img.material = null;
            img.color = new Color(0f, 0f, 0f, a);
            return;
        }
        if (size.x >= 240f && size.y >= 140f)
        {
            img.sprite = EZSprites.Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.material = null;
            img.color = EZTheme.Glass;
            if (img.transform.Find("EZ Border") == null)
            {
                var ring = EZ.Img(img.transform, "EZ Border", EZSprites.RoundedRing, EZTheme.WithAlpha(EZTheme.Accent, 0.35f), true);
                EZ.Stretch(ring.rectTransform);
                EZ.Ignore(ring.gameObject);
                ring.transform.SetAsFirstSibling();
            }
        }
    }

    // ------------------------------------------------------------------ entry point
    public static void All(Canvas canvas)
    {
        if (canvas == null) return;
        LoadFonts();
        var rootRect = (RectTransform)canvas.rootCanvas.transform;
        foreach (var b in canvas.GetComponentsInChildren<Button>(true)) StyleButton(b);
        foreach (var s in canvas.GetComponentsInChildren<Slider>(true)) StyleSlider(s);
        foreach (var i in canvas.GetComponentsInChildren<Image>(true)) StyleImage(i, rootRect);
        foreach (var t in canvas.GetComponentsInChildren<TMP_Text>(true)) StyleText(t);
        foreach (var l in canvas.GetComponentsInChildren<Text>(true)) StyleLegacyText(l);
    }
}
