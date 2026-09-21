// ============================================================================
// ENTROPY ZERO UI KIT  (2 of 5)  EZBackdrop.cs
// One persistent, always-animated space scene behind every menu panel:
//   solid black base, drifting nebulae, 3-layer parallax star field with twinkle,
//   shooting stars, and a rotating accretion-disk singularity that slides to a
//   corner (and dims) whenever you leave the main menu.
// Built from pooled UI Images under sibling index 0 of the Menu canvas.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class EZBackdrop : MonoBehaviour
{
    sealed class Star { public RectTransform rt; public Image img; public float x, y, vx, vy, phase, freq, a, depth; }
    sealed class Streak { public RectTransform rt; public Image img; public float t = -1f, dur = 1f, x0, y0, x1, y1; }
    sealed class Orb { public RectTransform rt; public Image img; public float ang, speed, rx, ry, size; public Color col; }

    static readonly float[] BandSize = { 1000f, 800f, 620f };
    static readonly float[] BandSpeed = { 8f, -13f, 24f };
    static readonly Color[] BandColor =
    {
        new Color(0.55f, 0.38f, 1.00f, 0.75f),
        new Color(1.00f, 0.72f, 0.35f, 0.95f),
        new Color(0.25f, 0.90f, 1.00f, 1.00f)
    };

    RectTransform bg, nebA, nebB, sing, lensRt, photonRt;
    Image nebAImg, nebBImg, haloImg;
    CanvasGroup singGroup;
    readonly RectTransform[] bandBack = new RectTransform[3];
    readonly RectTransform[] bandFront = new RectTransform[3];
    readonly List<Star> stars = new List<Star>();
    readonly List<Streak> streaks = new List<Streak>();
    readonly List<Orb> orbs = new List<Orb>();
    Transform mainMenu;
    float blend = -1f, nextStreak, lastW, lastH;

    public static EZBackdrop Create(Canvas canvas)
    {
        Transform existing = canvas.transform.Find("EZ Space");
        if (existing != null) return existing.GetComponent<EZBackdrop>();

        var go = new GameObject("EZ Space", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsFirstSibling();
        var b = go.AddComponent<EZBackdrop>();
        b.Build(canvas);
        return b;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    void Build(Canvas main)
    {
        bg = (RectTransform)transform;
        EZ.Stretch(bg);
        mainMenu = main.transform.Find("MainMenu");

        // Solid black base: guarantees black no matter what the camera / scene draws behind.
        var black = EZ.Img(bg, "EZ Black", null, Color.black);
        EZ.Stretch(black.rectTransform);

        // Nebulae
        nebAImg = EZ.Img(bg, "EZ Nebula A", EZSprites.SoftGlow, new Color(0.30f, 0.15f, 0.75f, 0.20f));
        nebA = nebAImg.rectTransform;
        nebA.anchorMin = nebA.anchorMax = nebA.pivot = new Vector2(0.5f, 0.5f);
        nebBImg = EZ.Img(bg, "EZ Nebula B", EZSprites.SoftGlow, new Color(0.05f, 0.45f, 0.60f, 0.16f));
        nebB = nebBImg.rectTransform;
        nebB.anchorMin = nebB.anchorMax = nebB.pivot = new Vector2(0.5f, 0.5f);

        BuildStars();
        BuildSingularity();

        for (int i = 0; i < 3; i++)
        {
            var img = EZ.Img(bg, "EZ Streak", EZSprites.Streak, new Color(1f, 1f, 1f, 0f));
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(1f, 0.5f);          // head on the right edge
            rt.sizeDelta = new Vector2(300f, 5f);
            streaks.Add(new Streak { rt = rt, img = img });
        }
        nextStreak = Time.unscaledTime + Random.Range(2f, 5f);
    }

    void BuildStars()
    {
        int divisor = SystemInfo.systemMemorySize < 3000 ? 2 : 1;
        // count, radius(px), alpha, speed(px/s), parallax depth
        float[,] layers = { { 46, 2.5f, 0.55f, 5f, 0.3f }, { 34, 3.5f, 0.80f, 11f, 0.6f }, { 18, 6f, 1.00f, 22f, 1.0f } };
        Color[] tints =
        {
            Color.white, new Color(0.80f, 0.92f, 1f), new Color(0.85f, 0.80f, 1f), new Color(1f, 0.90f, 0.75f)
        };
        for (int L = 0; L < 3; L++)
        {
            int n = Mathf.RoundToInt(layers[L, 0]) / divisor;
            for (int i = 0; i < n; i++)
            {
                Color c = tints[Random.value < 0.6f ? 0 : Random.Range(1, tints.Length)];
                var img = EZ.Img(bg, "EZ Star", EZSprites.Dot, c);
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                float sz = layers[L, 1] * 2f;
                rt.sizeDelta = new Vector2(sz, sz);
                float speed = layers[L, 3] * Random.Range(0.8f, 1.25f);
                stars.Add(new Star
                {
                    rt = rt, img = img,
                    vx = -speed * 0.95f, vy = -speed * 0.30f,
                    phase = Random.Range(0f, 6.28f), freq = Random.Range(0.6f, 2.4f),
                    a = layers[L, 2], depth = layers[L, 4]
                });
            }
        }
    }

    void BuildSingularity()
    {
        sing = NewRect("EZ Singularity", bg);
        EZ.Center(sing, new Vector2(1000f, 1000f), Vector2.zero);
        singGroup = sing.gameObject.AddComponent<CanvasGroup>();

        var halo = EZ.Img(sing, "EZ Halo", EZSprites.SoftGlow, new Color(0.42f, 0.25f, 0.95f, 0.30f));
        haloImg = halo;
        EZ.Center(halo.rectTransform, new Vector2(1500f, 1500f), Vector2.zero);

        var lens = EZ.Img(sing, "EZ Lens", EZSprites.RingThin, new Color(1f, 0.78f, 0.45f, 0.35f));
        lensRt = lens.rectTransform;
        EZ.Center(lensRt, new Vector2(560f, 560f), Vector2.zero);

        BuildBands(sing, bandBack, true);    // far half of the disk, behind the hole

        var core = EZ.Img(sing, "EZ Core", EZSprites.Disc, Color.black);
        EZ.Center(core.rectTransform, new Vector2(230f, 230f), Vector2.zero);

        var photon = EZ.Img(sing, "EZ Photon Ring", EZSprites.RingThin, new Color(1f, 1f, 1f, 0.9f));
        photonRt = photon.rectTransform;
        EZ.Center(photonRt, new Vector2(300f, 300f), Vector2.zero);

        BuildBands(sing, bandFront, false);  // near half of the disk, in front of the hole

        Color[] orbColors = { EZTheme.Accent, EZTheme.Amber, Color.white, EZTheme.Violet };
        for (int i = 0; i < 14; i++)
        {
            Color c = orbColors[Random.Range(0, orbColors.Length)];
            c.a = Random.Range(0.6f, 1f);
            float sz = Random.Range(9f, 20f);
            var img = EZ.Img(sing, "EZ Orb", EZSprites.Dot, c);
            EZ.Center(img.rectTransform, new Vector2(sz, sz), Vector2.zero);
            float rx = Random.Range(280f, 470f);
            orbs.Add(new Orb
            {
                rt = img.rectTransform, img = img, col = c, size = sz,
                ang = Random.Range(0f, 6.28f), speed = Random.Range(0.35f, 1.0f), rx = rx, ry = rx * 0.26f
            });
        }
    }

    // Each band is a full ring squashed vertically (0.26) and clipped to the upper or lower half.
    void BuildBands(RectTransform parent, RectTransform[] store, bool upper)
    {
        var clip = NewRect(upper ? "EZ Back Clip" : "EZ Front Clip", parent);
        clip.gameObject.AddComponent<RectMask2D>();
        clip.anchorMin = clip.anchorMax = new Vector2(0.5f, 0.5f);
        clip.pivot = new Vector2(0.5f, upper ? 0f : 1f);
        clip.sizeDelta = new Vector2(1700f, 700f);
        clip.anchoredPosition = Vector2.zero;

        for (int i = 0; i < 3; i++)
        {
            var sq = NewRect("EZ Squish", clip);
            sq.anchorMin = sq.anchorMax = new Vector2(0.5f, upper ? 0f : 1f);
            sq.pivot = new Vector2(0.5f, 0.5f);
            sq.anchoredPosition = Vector2.zero;
            sq.sizeDelta = new Vector2(BandSize[i], BandSize[i]);
            sq.localScale = new Vector3(1f, 0.26f, 1f);
            var img = EZ.Img(sq, "EZ Band", i < 2 ? EZSprites.RingThick : EZSprites.RingThin, BandColor[i]);
            EZ.Stretch(img.rectTransform);
            store[i] = img.rectTransform;
        }
    }

    void Scatter(float w, float h)
    {
        float nk = Mathf.Max(w, h) * 1.15f;
        nebA.sizeDelta = new Vector2(nk, nk);
        nebB.sizeDelta = new Vector2(nk * 0.9f, nk * 0.9f);
        for (int i = 0; i < stars.Count; i++)
        {
            stars[i].x = Random.Range(0f, w);
            stars[i].y = Random.Range(0f, h);
        }
    }

    void Update()
    {
        if (bg == null) return;
        float w = bg.rect.width, h = bg.rect.height;
        if (w < 2f || h < 2f) return;

        bool motion = EZTheme.MotionEnabled;
        float dt = motion ? Mathf.Min(Time.unscaledDeltaTime, 0.05f) : 0f;
        float t = Time.unscaledTime;

        if (Mathf.Abs(w - lastW) > 0.5f || Mathf.Abs(h - lastH) > 0.5f)
        {
            Scatter(w, h);
            lastW = w;
            lastH = h;
        }

        // ---- nebulae: slow Lissajous drift + breathing ----
        nebA.anchoredPosition = new Vector2(-w * 0.22f + Mathf.Sin(t * 0.05f) * w * 0.05f,
                                             h * 0.18f + Mathf.Cos(t * 0.043f) * h * 0.04f);
        nebB.anchoredPosition = new Vector2(w * 0.26f + Mathf.Cos(t * 0.041f) * w * 0.05f,
                                            -h * 0.20f + Mathf.Sin(t * 0.060f) * h * 0.05f);
        Color ca = nebAImg.color; ca.a = 0.17f + 0.07f * Mathf.Sin(t * 0.35f); nebAImg.color = ca;
        Color cb = nebBImg.color; cb.a = 0.14f + 0.06f * Mathf.Sin(t * 0.29f + 1.7f); nebBImg.color = cb;

        // ---- stars: 3 parallax layers, wrap, twinkle, gentle sway ----
        float swayX = Mathf.Sin(t * 0.13f) * 16f, swayY = Mathf.Cos(t * 0.09f) * 10f;
        for (int i = 0; i < stars.Count; i++)
        {
            Star s = stars[i];
            s.x += s.vx * dt;
            s.y += s.vy * dt;
            if (s.x < -12f) s.x += w + 24f; else if (s.x > w + 12f) s.x -= w + 24f;
            if (s.y < -12f) s.y += h + 24f; else if (s.y > h + 12f) s.y -= h + 24f;
            s.rt.anchoredPosition = new Vector2(s.x + swayX * s.depth, s.y + swayY * s.depth);
            float tw = 0.55f + 0.45f * Mathf.Sin(t * s.freq + s.phase);
            Color c = s.img.color; c.a = s.a * tw; s.img.color = c;
        }

        // ---- singularity: hero on main menu, dimmed corner accent elsewhere ----
        bool wantMain = mainMenu != null && mainMenu.gameObject.activeInHierarchy;
        float target = wantMain ? 1f : 0f;
        if (blend < 0f || !motion) blend = target;
        blend = Mathf.MoveTowards(blend, target, Time.unscaledDeltaTime * 1.6f);
        float e = blend * blend * (3f - 2f * blend);
        float k = Mathf.Min(w / 1920f, h / 1080f);
        sing.anchoredPosition = Vector2.Lerp(new Vector2(w * 0.34f, -h * 0.30f), new Vector2(w * 0.24f, h * 0.02f), e);
        float sc = Mathf.Lerp(0.5f, 1.02f, e) * k;
        sing.localScale = new Vector3(sc, sc, 1f);
        singGroup.alpha = Mathf.Lerp(0.38f, 1f, e);

        for (int i = 0; i < 3; i++)
        {
            Quaternion q = Quaternion.Euler(0f, 0f, t * BandSpeed[i]);
            bandBack[i].localRotation = q;
            bandFront[i].localRotation = q;
        }
        lensRt.localRotation = Quaternion.Euler(0f, 0f, -t * 5f);
        photonRt.localRotation = Quaternion.Euler(0f, 0f, t * 22f);
        Color hc = haloImg.color; hc.a = 0.26f + 0.10f * Mathf.Sin(t * 0.9f); haloImg.color = hc;

        for (int i = 0; i < orbs.Count; i++)
        {
            Orb o = orbs[i];
            o.ang += o.speed * dt;
            float sn = Mathf.Sin(o.ang);
            o.rt.anchoredPosition = new Vector2(Mathf.Cos(o.ang) * o.rx, sn * o.ry);
            float behind = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.2f, 0.2f, sn));   // upper half = behind the hole
            Color oc = o.col;
            oc.a = o.col.a * Mathf.Lerp(1f, 0.22f, behind) * (0.75f + 0.25f * Mathf.Sin(t * 3f + o.size));
            o.img.color = oc;
            o.rt.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.65f, behind);
        }

        // ---- shooting stars ----
        if (motion && t >= nextStreak)
        {
            for (int i = 0; i < streaks.Count; i++)
            {
                Streak s = streaks[i];
                if (s.t >= 0f) continue;
                float a = Random.Range(0.35f, 0.6f);
                Vector2 dir = new Vector2(-Mathf.Cos(a), -Mathf.Sin(a));
                float len = Random.Range(650f, 950f);
                s.x0 = Random.Range(0.35f, 1.0f) * w;
                s.y0 = Random.Range(0.55f, 1.0f) * h;
                s.x1 = s.x0 + dir.x * len;
                s.y1 = s.y0 + dir.y * len;
                s.dur = Random.Range(0.7f, 1.1f);
                s.t = 0f;
                s.rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                break;
            }
            nextStreak = t + Random.Range(4f, 9f);
        }
        for (int i = 0; i < streaks.Count; i++)
        {
            Streak s = streaks[i];
            if (s.t < 0f) continue;
            s.t += dt / s.dur;
            if (s.t >= 1f)
            {
                s.t = -1f;
                s.img.color = new Color(1f, 1f, 1f, 0f);
                continue;
            }
            s.rt.anchoredPosition = new Vector2(Mathf.Lerp(s.x0, s.x1, s.t), Mathf.Lerp(s.y0, s.y1, s.t));
            s.img.color = new Color(0.85f, 0.95f, 1f, Mathf.Sin(s.t * Mathf.PI) * 0.9f);
        }
    }
}
