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
    sealed class Cloud
    {
        public RectTransform rt; public Image img;
        public Vector2 norm, home;
        public float ang, spin, phase, bob, alpha;
    }

    // Highlight rings riding on top of the baked disc. The disc itself cannot
    // rotate - it is a tilted shape, and spinning it would read as tumbling -
    // so these squashed rings carry the orbital motion instead.
    static readonly float[] RingSize = { 1150f, 1500f };
    static readonly float[] RingSpeed = { 16f, 10f };
    static readonly Color[] RingColor =
    {
        new Color(0.88f, 0.96f, 1.00f, 0.30f),
        new Color(0.72f, 0.88f, 1.00f, 0.20f)
    };

    // Smoke shells, largest first, counter-rotating against each other.
    static readonly float[] SmokeSize = { 1500f, 1050f, 780f };
    static readonly float[] SmokeSpeed = { -3.2f, 5.1f, -8.4f };
    // Much dimmer than before: a bright haze over the whole right-hand side is
    // what made the backdrop read as two different pictures stitched together.
    static readonly Color[] SmokeColor =
    {
        new Color(0.60f, 0.66f, 0.78f, 0.13f),
        new Color(0.80f, 0.84f, 0.92f, 0.11f),
        new Color(0.95f, 0.96f, 1.00f, 0.09f)
    };

    // Matches the tilt baked into EZSprites.DiscHalf so the highlight rings sit
    // in the disc's own plane.
    const float RingSquash = 0.32f;
    const float CoreDiameter = 240f;
    const float DiscSize = 1750f;

    RectTransform bg, nebA, nebB, sing, photonRt;
    Image nebAImg, nebBImg, haloImg, photonImg;
    CanvasGroup singGroup;
    readonly RectTransform[] rings = new RectTransform[2];
    readonly RectTransform[] smoke = new RectTransform[3];
    readonly RectTransform[] jets = new RectTransform[2];
    RectTransform rocket;
    Image rocketPlume;
    RectTransform flare;
    Image flareStar, flareCore;
    readonly List<Star> stars = new List<Star>();
    readonly List<Streak> streaks = new List<Streak>();
    readonly List<Cloud> clouds = new List<Cloud>();
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
        nebAImg = EZ.Img(bg, "EZ Nebula A", EZSprites.SoftGlow, new Color(0.26f, 0.34f, 0.56f, 0.20f));
        nebA = nebAImg.rectTransform;
        nebA.anchorMin = nebA.anchorMax = nebA.pivot = new Vector2(0.5f, 0.5f);
        nebBImg = EZ.Img(bg, "EZ Nebula B", EZSprites.SoftGlow, new Color(0.13f, 0.22f, 0.38f, 0.16f));
        nebB = nebBImg.rectTransform;
        nebB.anchorMin = nebB.anchorMax = nebB.pivot = new Vector2(0.5f, 0.5f);

        BuildStars();
        BuildSingularity();
        BuildClouds();
        BuildRocket();
        EZAbilityFlecks.Create(bg);

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
        float[,] layers = { { 90, 2.5f, 0.55f, 5f, 0.3f }, { 62, 3.5f, 0.80f, 11f, 0.6f }, { 34, 6f, 1.00f, 22f, 1.0f } };
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

        haloImg = EZ.Img(sing, "EZ Halo", EZSprites.SoftGlow, new Color(1f, 0.58f, 0.26f, 0.10f));
        EZ.Center(haloImg.rectTransform, new Vector2(1700f, 1700f), Vector2.zero);

        // Bipolar jets, kept narrow. Widened out they stop reading as plumes and
        // just fog over two quadrants of the screen.
        for (int i = 0; i < 2; i++)
        {
            bool up = i == 0;
            var img = EZ.Img(sing, "EZ Jet", EZSprites.Jet,
                up ? new Color(1f, 0.56f, 0.36f, 0.17f) : new Color(1f, 0.50f, 0.30f, 0.13f));
            EZ.Center(img.rectTransform, new Vector2(420f, 940f),
                new Vector2(up ? 140f : -140f, up ? 350f : -350f));
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, up ? 26f : 206f);
            jets[i] = img.rectTransform;
        }

        for (int i = 0; i < 3; i++)
        {
            var img = EZ.Img(sing, "EZ Smoke", EZSprites.Smoke(i), SmokeColor[i]);
            EZ.Center(img.rectTransform, new Vector2(SmokeSize[i], SmokeSize[i]), Vector2.zero);
            smoke[i] = img.rectTransform;
        }

        // Far half of the disc, then the hole, then the near half: that order is
        // what makes the disc pass behind the void at the top and across it at
        // the bottom.
        var discFar = EZ.Img(sing, "EZ Disc Far", EZSprites.DiscHalf(true), Color.white);
        EZ.Center(discFar.rectTransform, new Vector2(DiscSize, DiscSize), Vector2.zero);

        var core = EZ.Img(sing, "EZ Core", EZSprites.Disc, Color.black);
        EZ.Center(core.rectTransform, new Vector2(CoreDiameter, CoreDiameter), Vector2.zero);

        photonImg = EZ.Img(sing, "EZ Photon Ring", EZSprites.RingThick, new Color(0.95f, 0.98f, 1f, 0.95f));
        photonRt = photonImg.rectTransform;
        EZ.Center(photonRt, new Vector2(610f, 610f), Vector2.zero);

        var discNear = EZ.Img(sing, "EZ Disc Near", EZSprites.DiscHalf(false), Color.white);
        EZ.Center(discNear.rectTransform, new Vector2(DiscSize, DiscSize), Vector2.zero);

        // The white-hot flare where infalling matter meets the disc - the single
        // brightest thing in the reference art, and what stops the disc reading
        // as a flat painted ring.
        flare = NewRect("EZ Flare", sing);
        EZ.Center(flare, new Vector2(340f, 340f), new Vector2(-430f, 26f));
        var bloom = EZ.Img(flare, "EZ Flare Bloom", EZSprites.SoftGlow, new Color(1f, 0.80f, 0.45f, 0.55f));
        EZ.Stretch(bloom.rectTransform);
        flareStar = EZ.Img(flare, "EZ Flare Star", EZSprites.Flare, new Color(1f, 0.95f, 0.80f, 0.95f));
        EZ.Center(flareStar.rectTransform, new Vector2(300f, 300f), Vector2.zero);
        flareStar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
        flareCore = EZ.Img(flare, "EZ Flare Core", EZSprites.SoftGlow, new Color(1f, 1f, 0.95f, 0.85f));
        EZ.Center(flareCore.rectTransform, new Vector2(72f, 72f), Vector2.zero);

        for (int i = 0; i < rings.Length; i++)
        {
            var squish = NewRect("EZ Ring", sing);
            EZ.Center(squish, new Vector2(RingSize[i], RingSize[i]), Vector2.zero);
            squish.localScale = new Vector3(1f, RingSquash, 1f);
            var img = EZ.Img(squish, "EZ Arc", EZSprites.RingThin, RingColor[i]);
            EZ.Stretch(img.rectTransform);
            rings[i] = img.rectTransform;
        }

    }

    // A small ship drifting through the gap between the menu and the hole, which
    // is where the composition used to read as a seam between two pictures.
    void BuildRocket()
    {
        var plume = EZ.Img(bg, "EZ Rocket Plume", EZSprites.SoftGlow, new Color(0.40f, 0.82f, 1f, 0.5f));
        rocketPlume = plume;
        var hull = EZ.Img(bg, "EZ Rocket", EZSprites.Rocket, Color.white);
        rocket = hull.rectTransform;
        rocket.anchorMin = rocket.anchorMax = Vector2.zero;
        rocket.pivot = new Vector2(0.5f, 0.5f);
        rocket.sizeDelta = new Vector2(124f, 124f);
        plume.rectTransform.anchorMin = plume.rectTransform.anchorMax = Vector2.zero;
        plume.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        plume.rectTransform.sizeDelta = new Vector2(112f, 112f);
    }

    // The white billowing masses. They are scattered over the whole screen
    // rather than ringed around the hole, and they thin out over the title and
    // the buttons so the menu stays readable underneath them.
    void BuildClouds()
    {
        const int Count = 30;
        for (int i = 0; i < Count; i++)
        {
            Vector2 norm = new Vector2(Random.Range(-0.02f, 1.02f), Random.Range(-0.02f, 1.02f));
            float menu = Mathf.Clamp01((0.34f - norm.x) / 0.34f);
            float size = Random.Range(95f, 250f) * (1f - 0.45f * menu);
            float alpha = Random.Range(0.70f, 1f) * (1f - 0.62f * menu);

            var img = EZ.Img(bg, "EZ Cloud", EZSprites.InkCloud(i), new Color(0.96f, 0.98f, 1f, alpha));
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);

            clouds.Add(new Cloud
            {
                rt = rt, img = img, norm = norm,
                ang = Random.Range(0f, 360f),
                spin = (i % 2 == 0 ? 1f : -1f) * Random.Range(0.5f, 1.4f),
                phase = Random.Range(0f, 6.28f),
                bob = Random.Range(5f, 14f),
                alpha = alpha
            });
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
        for (int i = 0; i < clouds.Count; i++)
        {
            clouds[i].home = new Vector2(clouds[i].norm.x * w, clouds[i].norm.y * h);
            clouds[i].rt.anchoredPosition = clouds[i].home;
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
        sing.anchoredPosition = Vector2.Lerp(new Vector2(w * 0.34f, -h * 0.30f), new Vector2(w * 0.19f, h * 0.02f), e);
        float sc = Mathf.Lerp(0.5f, 1.02f, e) * k;
        sing.localScale = new Vector3(sc, sc, 1f);
        singGroup.alpha = Mathf.Lerp(0.38f, 1f, e);

        for (int i = 0; i < rings.Length; i++)
            rings[i].localRotation = Quaternion.Euler(0f, 0f, t * RingSpeed[i]);
        for (int i = 0; i < smoke.Length; i++)
            smoke[i].localRotation = Quaternion.Euler(0f, 0f, t * SmokeSpeed[i]);
        for (int i = 0; i < jets.Length; i++)
        {
            float flare = 1f + 0.05f * Mathf.Sin(t * 0.8f + i * 2.1f);
            jets[i].localScale = new Vector3(flare, 1f + 0.03f * Mathf.Sin(t * 0.55f + i), 1f);
        }
        photonRt.localRotation = Quaternion.Euler(0f, 0f, t * 24f);
        Color pc = photonImg.color; pc.a = 0.78f + 0.14f * Mathf.Sin(t * 1.3f); photonImg.color = pc;
        Color hc = haloImg.color; hc.a = 0.09f + 0.04f * Mathf.Sin(t * 0.55f); haloImg.color = hc;

        if (flare != null)
        {
            float beat = 0.86f + 0.14f * Mathf.Sin(t * 1.7f) + 0.06f * Mathf.Sin(t * 4.3f);
            flare.localScale = new Vector3(beat, beat, 1f);
            if (flareStar != null)
            {
                flareStar.color = new Color(1f, 0.95f, 0.80f, Mathf.Clamp01(0.72f + 0.26f * beat));
                flareStar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f + t * 6f);
            }
            if (flareCore != null)
                flareCore.color = new Color(1f, 1f, 0.95f, Mathf.Clamp01(0.62f + 0.32f * beat));
        }

        for (int i = 0; i < clouds.Count; i++)
        {
            Cloud cl = clouds[i];
            cl.ang += cl.spin * dt;
            cl.rt.localRotation = Quaternion.Euler(0f, 0f, cl.ang);
            cl.rt.anchoredPosition = cl.home + new Vector2(
                Mathf.Sin(t * 0.07f + cl.phase) * cl.bob,
                Mathf.Cos(t * 0.055f + cl.phase) * cl.bob * 0.7f);
            float breathe = 1f + 0.045f * Mathf.Sin(t * 0.21f + cl.phase);
            cl.rt.localScale = new Vector3(breathe, breathe, 1f);
            Color cc = cl.img.color;
            cc.a = cl.alpha * (0.86f + 0.14f * Mathf.Sin(t * 0.33f + cl.phase));
            cl.img.color = cc;
        }

        if (rocket != null)
        {
            float rt = t * 0.045f;
            Vector2 at = new Vector2(w * (0.38f + 0.07f * Mathf.Sin(rt)),
                                     h * (0.62f + 0.06f * Mathf.Cos(rt * 1.3f)));
            rocket.anchoredPosition = at;
            float lean = -24f + Mathf.Sin(rt * 1.7f) * 10f;
            rocket.localRotation = Quaternion.Euler(0f, 0f, lean);
            if (rocketPlume != null)
            {
                float back = (lean + 180f) * Mathf.Deg2Rad;
                rocketPlume.rectTransform.anchoredPosition =
                    at + new Vector2(Mathf.Sin(back) * 46f, -Mathf.Cos(back) * 46f);
                float flicker = 0.34f + 0.20f * Mathf.Sin(t * 11f) + 0.10f * Mathf.Sin(t * 27f);
                rocketPlume.color = new Color(0.40f, 0.82f, 1f, Mathf.Clamp01(flicker));
                float puff = 0.85f + 0.18f * Mathf.Sin(t * 13f);
                rocketPlume.rectTransform.localScale = new Vector3(puff, puff, 1f);
            }
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
