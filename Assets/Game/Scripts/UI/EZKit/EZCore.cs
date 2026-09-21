// ============================================================================
// ENTROPY ZERO UI KIT  (1 of 5)  EZCore.cs
// Theme, procedural sprites, helpers, self-installing bootstrap, cleanup, diagnostics.
//
// INSTALL: copy all five EZ*.cs files into Assets/Game/Scripts/UI/EZKit/ (any folder
// under Assets works). No scene edits are needed: the kit installs itself at runtime
// in any scene that has a root Canvas containing a child called "MainMenu".
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class EZTheme
{
    /// <summary>Set to false to disable all decorative motion (state changes still work).</summary>
    public static bool MotionEnabled = true;

    public static readonly Color Accent = new Color32(61, 224, 255, 255);
    public static readonly Color AccentHover = new Color32(140, 240, 255, 255);
    public static readonly Color Violet = new Color32(139, 92, 255, 255);
    public static readonly Color Ink = new Color32(232, 236, 245, 255);       // main text
    public static readonly Color Muted = new Color32(169, 179, 199, 255);     // captions
    public static readonly Color Danger = new Color32(255, 77, 94, 255);
    public static readonly Color Success = new Color32(77, 255, 176, 255);
    public static readonly Color Amber = new Color32(255, 180, 84, 255);
    public static readonly Color Glass = new Color32(14, 21, 42, 232);
    public static readonly Color GlassHover = new Color32(28, 42, 78, 245);
    public static readonly Color DarkOnAccent = new Color32(3, 16, 24, 255);

    public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}

// ----------------------------------------------------------------------------
// Procedural sprites (no art assets needed)
// ----------------------------------------------------------------------------
public static class EZSprites
{
    static Sprite rounded, roundedRing, outerGlow, dot, disc, softGlow, ringThick, ringThin, streak;

    public static Sprite Rounded { get { return Get(ref rounded, () => MakeRounded(64, 20f, 0f)); } }
    public static Sprite RoundedRing { get { return Get(ref roundedRing, () => MakeRounded(64, 20f, 2f)); } }
    public static Sprite OuterGlow { get { return Get(ref outerGlow, MakeOuterGlow); } }
    public static Sprite Dot { get { return Get(ref dot, MakeDot); } }
    public static Sprite Disc { get { return Get(ref disc, MakeDisc); } }
    public static Sprite SoftGlow { get { return Get(ref softGlow, MakeSoftGlow); } }
    public static Sprite RingThick { get { return Get(ref ringThick, () => MakeRing(0.035f)); } }
    public static Sprite RingThin { get { return Get(ref ringThin, () => MakeRing(0.010f)); } }
    public static Sprite Streak { get { return Get(ref streak, MakeStreak); } }

    static Sprite Get(ref Sprite slot, System.Func<Sprite> make)
    {
        if (slot == null) slot = make();
        return slot;
    }

    static Texture2D NewTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    static Sprite Build(Texture2D tex, float border)
    {
        tex.Apply(false, true);
        tex.hideFlags = HideFlags.HideAndDontSave;
        Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        s.hideFlags = HideFlags.HideAndDontSave;
        return s;
    }

    static float RoundedSdf(float px, float py, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(px) - (hx - r);
        float qy = Mathf.Abs(py) - (hy - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static byte A(float a) { return (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f); }

    static Sprite MakeRounded(int size, float radius, float ringWidth)
    {
        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedSdf(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                float a = Mathf.Clamp01(0.5f - d);
                if (ringWidth > 0f) a *= Mathf.Clamp01(d + ringWidth + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, A(a));
            }
        tex.SetPixels32(px);
        return Build(tex, radius + 2f);
    }

    // Soft glow that only exists OUTSIDE a rounded rect (interior transparent). 9-sliced.
    static Sprite MakeOuterGlow()
    {
        const int S = 128;
        var tex = NewTex(S, S);
        var px = new Color32[S * S];
        float half = S * 0.5f;
        float inset = 32f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = RoundedSdf(x + 0.5f - half, y + 0.5f - half, half - inset, half - inset, 22f);
                float a = d <= 0f ? 0f : Mathf.Exp(-d / 9f) * Mathf.Clamp01((inset - d) / 12f) * 0.9f;
                px[y * S + x] = new Color32(255, 255, 255, A(a));
            }
        tex.SetPixels32(px);
        return Build(tex, inset + 22f);
    }

    static Sprite MakeDot()
    {
        const int S = 32;
        var tex = NewTex(S, S);
        var px = new Color32[S * S];
        float half = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / half;
                px[y * S + x] = new Color32(255, 255, 255, A(Mathf.Pow(Mathf.Clamp01(1.25f - r * 1.25f), 1.5f)));
            }
        tex.SetPixels32(px);
        return Build(tex, 0f);
    }

    static Sprite MakeDisc()
    {
        const int S = 128;
        var tex = NewTex(S, S);
        var px = new Color32[S * S];
        float half = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * S + x] = new Color32(255, 255, 255, A(half - 1f - dist));
            }
        tex.SetPixels32(px);
        return Build(tex, 0f);
    }

    static Sprite MakeSoftGlow()
    {
        const int S = 128;
        var tex = NewTex(S, S);
        var px = new Color32[S * S];
        float half = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / half;
                px[y * S + x] = new Color32(255, 255, 255, A(Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f)));
            }
        tex.SetPixels32(px);
        return Build(tex, 0f);
    }

    // Ring with travelling highlights baked in, so rotating the Image visibly "swirls".
    static Sprite MakeRing(float widthN)
    {
        const int S = 256;
        var tex = NewTex(S, S);
        var px = new Color32[S * S];
        float half = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / half;
                float g = Mathf.Exp(-Mathf.Pow((r - 0.44f) / widthN, 2f));
                float ang = Mathf.Atan2(dy, dx);
                float h1 = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(ang - 0.6f)), 3f);
                float h2 = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(ang + 2.5f)), 5f);
                float a = g * Mathf.Clamp01(0.30f + 0.70f * h1 + 0.45f * h2);
                px[y * S + x] = new Color32(255, 255, 255, A(a));
            }
        tex.SetPixels32(px);
        return Build(tex, 0f);
    }

    // Shooting-star streak: head is on the right (+x).
    static Sprite MakeStreak()
    {
        const int W = 128, H = 8;
        var tex = NewTex(W, H);
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)(W - 1);
                float v = Mathf.Abs((y + 0.5f - H * 0.5f) / (H * 0.5f));
                px[y * W + x] = new Color32(255, 255, 255, A(Mathf.Pow(u, 3f) * (1f - v * v)));
            }
        tex.SetPixels32(px);
        return Build(tex, 0f);
    }
}

// ----------------------------------------------------------------------------
// Small helpers
// ----------------------------------------------------------------------------
public static class EZ
{
    public static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        return 1f - u * u * u;
    }

    /// <summary>True if this object or any ancestor is one of our own generated objects (name starts with "EZ").</summary>
    public static bool IsEZ(Transform t)
    {
        for (; t != null; t = t.parent)
            if (t.name.StartsWith("EZ", System.StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>Name of the top-level child of the canvas that contains t.</summary>
    public static string PanelNameOf(Transform t)
    {
        Transform cur = t;
        while (cur != null && cur.parent != null)
        {
            if (cur.parent.GetComponent<Canvas>() != null) return cur.name;
            cur = cur.parent;
        }
        return t != null ? t.name : "";
    }

    public static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    /// <summary>All visible label text under t, upper-cased (TMP + legacy Text).</summary>
    public static string LabelOf(Transform t)
    {
        var sb = new StringBuilder();
        foreach (var tmp in t.GetComponentsInChildren<TMP_Text>(true))
        {
            if (IsEZ(tmp.transform)) continue;
            sb.Append(tmp.text).Append(' ');
        }
        foreach (var l in t.GetComponentsInChildren<Text>(true))
        {
            if (IsEZ(l.transform)) continue;
            sb.Append(l.text).Append(' ');
        }
        return sb.ToString().ToUpperInvariant();
    }

    public static void Stretch(RectTransform rt, float l = 0f, float b = 0f, float r = 0f, float t = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    public static void Center(RectTransform rt, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    public static void Ignore(GameObject go)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
    }

    public static Image Img(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sliced && sprite != null) img.type = Image.Type.Sliced;
        return img;
    }

    public static TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color color,
        TextAlignmentOptions align, bool title)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.raycastTarget = false;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.overflowMode = TextOverflowModes.Overflow;
        EZRestyle.SetupText(t, title);
        return t;
    }
}

// ----------------------------------------------------------------------------
// Bootstrap: finds the Menu canvas and installs everything, then keeps it maintained.
// ----------------------------------------------------------------------------
public sealed class EZRunner : MonoBehaviour
{
    public static Canvas Main;
    Canvas canvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryStart();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { TryStart(); }

    static void TryStart()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EZRunner>() != null) return;
        Canvas found = null;
        foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.isRootCanvas && c.transform.Find("MainMenu") != null) { found = c; break; }
        }
        if (found == null) return;
        var go = new GameObject("EZ Kit");
        var runner = go.AddComponent<EZRunner>();
        runner.canvas = found;
        Main = found;
    }

    IEnumerator Start()
    {
        yield return null;
        yield return null;   // let SkinShopUI / AbilityInformationUI / MenuLobbyUI finish their own Start()
        if (canvas == null) yield break;

        EZPanelFX.OverlayCount = 0;
        EZRestyle.Reset();
        EZCleanup.Run(canvas);
        EZBackdrop.Create(canvas);
        Maintain();
        yield return null;
        EZCleanup.Dump(canvas);

        var wait = new WaitForSecondsRealtime(0.35f);
        while (canvas != null)
        {
            yield return wait;
            Maintain();
        }
    }

    void Maintain()
    {
        try
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || c.gameObject.scene != gameObject.scene) continue;
                EZRestyle.All(c);
                AttachFX(c);
            }
            EZSkinCarousel.EnsureAttached();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    static void AttachFX(Canvas c)
    {
        // PracticeModePanel is built and styled synchronously before its
        // source panel is hidden. Applying the generic per-child entrance FX
        // afterward makes its buttons reappear one at a time as blank cards.
        if (c.GetComponent<PracticeModePanel>() != null) return;

        foreach (Transform ch in c.transform)
        {
            if (ch.name == "Panel" || ch.name.StartsWith("EZ", System.StringComparison.Ordinal)) continue;
            if (!(ch is RectTransform)) continue;
            if (ch.GetComponent<EZPanelFX>() == null) ch.gameObject.AddComponent<EZPanelFX>();
        }
    }
}

// ----------------------------------------------------------------------------
// Removes leftovers of the previous redesign attempt and forces a black backdrop.
// ----------------------------------------------------------------------------
public static class EZCleanup
{
    public static void Run(Canvas canvas)
    {
        // 1) Destroy the previous attempt's animators / backgrounds (matched by type name so this
        //    file compiles whether or not those classes still exist).
        foreach (var mb in canvas.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            if (n == "UIAnimator" || n == "ContainedInstabilityBackground")
                UnityEngine.Object.DestroyImmediate(mb);
        }
        // UIAnimator.OnDisable leaves panels at alpha 0 / raycasts off: undo that.
        foreach (var cg in canvas.GetComponentsInChildren<CanvasGroup>(true))
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        var old = canvas.transform.Find("SpaceBackground");
        if (old != null) old.gameObject.SetActive(false);

        // 2) The original "Global Panel" image (grid / gradient look).
        var panel = canvas.transform.Find("Panel");
        if (panel != null)
        {
            var img = panel.GetComponent<Image>();
            if (img != null) img.enabled = false;
            var raw = panel.GetComponent<RawImage>();
            if (raw != null) raw.enabled = false;
        }

        // 3) Whatever the 3D camera draws behind the UI: make it solid black.
        foreach (var cam in Camera.allCameras)
        {
            if (cam.targetTexture != null) continue;   // leave skin-preview cameras alone
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }
        if (RenderSettings.skybox != null) RenderSettings.skybox = null;
    }

    /// <summary>Prints everything that could still be drawing the old grid, so it can be found fast.</summary>
    public static void Dump(Canvas main)
    {
        var sb = new StringBuilder("[EZ] ---- Diagnostics (paste this if anything still looks wrong) ----\n");
        foreach (var cam in Camera.allCameras)
            sb.AppendLine("Camera '" + cam.name + "' clear=" + cam.clearFlags + " bg=" + cam.backgroundColor +
                          " mask=" + cam.cullingMask + " targetTexture=" + (cam.targetTexture != null) + " depth=" + cam.depth);
        sb.AppendLine("Skybox: " + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "none"));
        foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.isRootCanvas)
                sb.AppendLine("Canvas '" + c.name + "' mode=" + c.renderMode + " order=" + c.sortingOrder + " active=" + c.gameObject.activeInHierarchy);

        var rootRect = (RectTransform)main.transform;
        foreach (var g in main.GetComponentsInChildren<Graphic>(true))
        {
            if (EZ.IsEZ(g.transform)) continue;
            Rect rc = g.rectTransform.rect;
            if (rc.width >= rootRect.rect.width * 0.85f && rc.height >= rootRect.rect.height * 0.85f)
                sb.AppendLine("Full-screen graphic: " + EZ.PathOf(g.transform) + " (" + g.GetType().Name +
                              ", enabled=" + g.enabled + ", color=" + g.color + ")");
        }
        int count = 0;
        foreach (var rd in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (count++ >= 25) break;
            sb.AppendLine("3D renderer: " + EZ.PathOf(rd.transform) + " (" + rd.GetType().Name + ")");
        }
        Debug.Log(sb.ToString());
    }
}
