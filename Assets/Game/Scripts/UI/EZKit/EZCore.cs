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
    static readonly Sprite[] smoke = new Sprite[3];
    static readonly Sprite[] inkCloud = new Sprite[3];
    static readonly Sprite[] discHalf = new Sprite[2];
    static Sprite jet, rocket, burst, slash, beam, flare;

    public static Sprite Rounded { get { return Get(ref rounded, () => MakeRounded(64, 20f, 0f)); } }
    public static Sprite RoundedRing { get { return Get(ref roundedRing, () => MakeRounded(64, 20f, 2f)); } }
    public static Sprite OuterGlow { get { return Get(ref outerGlow, MakeOuterGlow); } }
    public static Sprite Dot { get { return Get(ref dot, MakeDot); } }
    public static Sprite Disc { get { return Get(ref disc, MakeDisc); } }
    public static Sprite SoftGlow { get { return Get(ref softGlow, MakeSoftGlow); } }
    public static Sprite RingThick { get { return Get(ref ringThick, () => MakeRing(0.035f)); } }
    public static Sprite RingThin { get { return Get(ref ringThin, () => MakeRing(0.010f)); } }
    public static Sprite Streak { get { return Get(ref streak, MakeStreak); } }

    /// <summary>Wispy volumetric smoke wound into a vortex. Rotate it and it churns.</summary>
    public static Sprite Smoke(int variant)
    {
        int i = Mathf.Abs(variant) % smoke.Length;
        if (smoke[i] == null) smoke[i] = MakeSmoke(i);
        return smoke[i];
    }

    /// <summary>A billowing cloud mass with a ragged, islanded silhouette.</summary>
    public static Sprite InkCloud(int variant)
    {
        int i = Mathf.Abs(variant) % inkCloud.Length;
        if (inkCloud[i] == null) inkCloud[i] = MakeInkCloud(i);
        return inkCloud[i];
    }

    /// <summary>
    /// Half of a tilted accretion disc. Draw the far half, then the event
    /// horizon, then the near half, and the disc passes behind the hole at the
    /// top and in front of it at the bottom.
    /// </summary>
    public static Sprite DiscHalf(bool far)
    {
        if (discHalf[0] == null) MakeDiscPair();
        return discHalf[far ? 0 : 1];
    }

    public static Sprite Jet { get { return Get(ref jet, MakeJet); } }
    public static Sprite Rocket { get { return Get(ref rocket, MakeRocket); } }
    public static Sprite Burst { get { return Get(ref burst, () => MakeBurst(6)); } }
    public static Sprite Slash { get { return Get(ref slash, () => MakeBurst(2)); } }
    public static Sprite Beam { get { return Get(ref beam, MakeBeam); } }
    public static Sprite Flare { get { return Get(ref flare, () => MakeBurst(4)); } }

    /// <summary>GLSL-style smoothstep. Mathf.SmoothStep is a smoothed lerp, not this.</summary>
    static float SmoothEdge(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

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

    const float DiscTilt = 0.32f;
    const float DiscInner = 0.30f;
    const float DiscOuter = 0.72f;

    // The accretion disc: a temperature ramp from a white-hot inner edge out to
    // deep blue, dust lanes cut through it, turbulence along the flow, and one
    // side brightened the way relativistic beaming brightens a real disc. Both
    // halves come out of a single pass because they only differ by their mask -
    // computing the noise twice would double the cost for nothing.
    static void MakeDiscPair()
    {
        const int S = 448;
        var far = NewTex(S, S);
        var near = NewTex(S, S);
        var pxFar = new Color32[S * S];
        var pxNear = new Color32[S * S];
        float half = S * 0.5f;
        Color hot = new Color(1.00f, 0.98f, 0.90f);
        Color mid = new Color(1.00f, 0.72f, 0.30f);
        Color cold = new Color(0.44f, 0.13f, 0.04f);
        Color under = new Color(1.00f, 0.52f, 0.22f);

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float ey = dy / DiscTilt;
                float e = Mathf.Sqrt(dx * dx + ey * ey);
                float t = Mathf.Clamp01((e - DiscInner) / (DiscOuter - DiscInner));
                float profile = SmoothEdge(0f, 0.10f, t) * (1f - SmoothEdge(0.16f, 1f, t));

                float a = 0f;
                Color c = cold;
                if (profile > 0.0005f)
                {
                    float theta = Mathf.Atan2(ey, dx);
                    float swirl = theta + t * 2.4f;
                    float turb = 0.62f + 0.92f * Fbm(40f + Mathf.Cos(swirl) * 2.2f + t * 3f,
                        40f + Mathf.Sin(swirl) * 2.2f, 3);
                    float lane = Mathf.Lerp(0.22f, 1.12f,
                        SmoothEdge(0.34f, 0.66f, Fbm(9f + t * 8f, 9f + theta * 0.9f, 2)));
                    float beaming = Mathf.Lerp(0.35f, 1.45f, (Mathf.Cos(theta - Mathf.PI) + 1f) * 0.5f);
                    a = Mathf.Clamp01(profile * turb * lane * beaming);
                    c = t < 0.30f
                        ? Color.Lerp(hot, mid, t / 0.30f)
                        : Color.Lerp(mid, cold, (t - 0.30f) / 0.70f);
                    // The near side runs a little warmer, as it does in the
                    // reference imagery. Blended by height rather than per half,
                    // so the two halves match exactly where they meet.
                    c = Color.Lerp(c, under, 0.30f * Mathf.Clamp01(-dy / 0.26f));
                }

                // Feathered split: the masks sum to one, so the seam cannot show.
                float upper = SmoothEdge(-0.010f, 0.010f, dy);
                int index = y * S + x;
                pxFar[index] = new Color32(A(c.r), A(c.g), A(c.b), A(a * upper));
                pxNear[index] = new Color32(A(c.r), A(c.g), A(c.b), A(a * (1f - upper)));
            }

        far.SetPixels32(pxFar);
        near.SetPixels32(pxNear);
        discHalf[0] = Build(far, 0f);
        discHalf[1] = Build(near, 0f);
    }

    // A relativistic jet: a filamented cone that fades to nothing well inside
    // every edge of its own texture, so the quad it lives on can never show.
    static Sprite MakeJet()
    {
        const int S = 256;
        var tex = NewTex(S, S);
        var px = new Color32[S * S];
        float half = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float up = (dy + 1f) * 0.5f;
                float width = 0.05f + up * 0.42f;
                float core = Mathf.Exp(-(dx / width) * (dx / width) * 2.2f);
                float fade = Mathf.Pow(Mathf.Clamp01(1f - up), 0.9f) * SmoothEdge(0f, 0.14f, up);
                float fil = 0.55f + 0.9f * Fbm(5f + dx * 5f, 5f + up * 2.2f, 3);
                float border = SmoothEdge(0.98f, 0.45f, Mathf.Abs(dx)) *
                               SmoothEdge(0.99f, 0.55f, Mathf.Abs(dy));
                px[y * S + x] = new Color32(255, 255, 255, A(core * fade * fil * border * 0.85f));
            }
        tex.SetPixels32(px);
        return Build(tex, 0f);
    }

    // Nose-up capsule with two swept fins and a lit porthole.
    static Sprite MakeRocket()
    {
        const int S = 128;
        var tex = NewTex(S, S);
        var px = new Color32[S * S];
        float half = S * 0.5f;
        const float aa = 0.028f;
        Color shell = new Color(0.90f, 0.93f, 0.97f);
        Color stripe = new Color(0.55f, 0.64f, 0.78f);
        Color glass = new Color(0.30f, 0.82f, 1.00f);

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float px0 = (x + 0.5f - half) / half, py0 = (y + 0.5f - half) / half;
                float taper = Mathf.Clamp01(1f - Mathf.Pow(Mathf.Clamp01((py0 - 0.18f) / 0.52f), 2f));
                float hullW = py0 <= 0.18f ? 0.19f : 0.19f * Mathf.Sqrt(taper);
                float hull = SmoothEdge(aa, -aa, Mathf.Abs(px0) - hullW) *
                             SmoothEdge(-aa, aa, py0 + 0.42f) *
                             SmoothEdge(aa, -aa, py0 - 0.70f);

                float fin = 0f;
                for (int side = 0; side < 2; side++)
                {
                    float sx = side == 0 ? -px0 : px0;
                    float edge = Mathf.Min(sx - 0.10f, (py0 + 0.42f) * 0.85f - (sx - 0.10f) * 0.95f);
                    fin = Mathf.Max(fin, SmoothEdge(-aa, aa, edge) *
                                         SmoothEdge(-aa, aa, py0 + 0.42f) *
                                         SmoothEdge(aa, -aa, py0 - 0.02f) *
                                         SmoothEdge(aa, -aa, sx - 0.42f));
                }

                float a = Mathf.Clamp01(Mathf.Max(hull, fin));
                float port = SmoothEdge(aa, -aa,
                    Mathf.Sqrt(px0 * px0 + (py0 - 0.26f) * (py0 - 0.26f)) - 0.072f);
                float band = SmoothEdge(aa, -aa, Mathf.Abs(py0 + 0.16f) - 0.035f) * hull;

                Color c = shell;
                if (band > 0.5f) c = stripe;
                if (port > 0.5f) c = glass;
                px[y * S + x] = new Color32(A(c.r), A(c.g), A(c.b), A(a));
            }
        tex.SetPixels32(px);
        return Build(tex, 0f);
    }

    // Spiky star burst. Two spikes reads as a slash, six as a detonation.
    static Sprite MakeBurst(int spikes)
    {
        const int S = 128;
        var tex = NewTex(S, S);
        var px = new Color32[S * S];
        float half = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float spike = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * spikes * 0.5f)), 8f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f) * (0.25f + 0.95f * spike);
                a += Mathf.Pow(Mathf.Clamp01(1f - r * 5f), 2f) * 0.9f;
                px[y * S + x] = new Color32(255, 255, 255, A(a));
            }
        tex.SetPixels32(px);
        return Build(tex, 0f);
    }

    // Tapered energy beam, thin at the left and flaring to the right.
    static Sprite MakeBeam()
    {
        const int S = 128;
        var tex = NewTex(S, S);
        var px = new Color32[S * S];
        float half = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float u = (dx + 1f) * 0.5f;
                float halfW = 0.05f + 0.30f * u;
                float a = Mathf.Exp(-(dy / halfW) * (dy / halfW) * 2.5f) *
                          Mathf.Pow(u, 1.6f) * (1f - SmoothEdge(0.85f, 1f, u));
                px[y * S + x] = new Color32(255, 255, 255, A(a));
            }
        tex.SetPixels32(px);
        return Build(tex, 0f);
    }

    static float Fbm(float x, float y, int octaves)
    {
        float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2.07f;
        }
        return norm > 0f ? sum / norm : 0f;
    }

    // Smoke: noise sampled in coordinates that are sheared further the closer
    // they sit to the middle, so the filaments wind inward instead of lying
    // flat, and taking the ridge of the noise rather than the noise itself,
    // which is what turns soft cloud into visible strands.
    static Sprite MakeSmoke(int variant)
    {
        const int S = 192;
        var tex = NewTex(S, S);
        var px = new Color32[S * S];
        float half = S * 0.5f;
        float seed = 13.7f + variant * 41.3f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 0f;
                if (r < 1f)
                {
                    float ang = Mathf.Atan2(dy, dx) + (1f - r) * 3f;
                    float sx = Mathf.Cos(ang) * r, sy = Mathf.Sin(ang) * r;
                    // Warping the sample point by a second, coarser noise is
                    // what breaks the swirl up into turbulence; without it the
                    // filaments come out as tidy concentric ripples.
                    float wx = Fbm(seed + sx * 1.3f, seed + sy * 1.3f, 2) - 0.5f;
                    float wy = Fbm(seed + 19f + sx * 1.3f, seed + 7f + sy * 1.3f, 2) - 0.5f;
                    float n = Fbm(seed + sx * 3f + wx * 1.7f, seed + sy * 3f + wy * 1.7f, 3);
                    float ridge = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(n * 2f - 1f)), 4.5f);
                    a = ridge * Mathf.Clamp01((r - 0.16f) / 0.20f) * Mathf.Clamp01((1f - r) / 0.34f);
                }
                px[y * S + x] = new Color32(255, 255, 255, A(a));
            }
        tex.SetPixels32(px);
        return Build(tex, 0f);
    }

    // Cloud mass: a noise field pushed through a hard threshold. The threshold
    // is what produces the billowing, partly-islanded silhouette in the
    // reference art; a plain falloff would only ever give a soft blob.
    static Sprite MakeInkCloud(int variant)
    {
        const int S = 256;
        var tex = NewTex(S, S);
        var px = new Color32[S * S];
        float half = S * 0.5f;
        float seed = 71.9f + variant * 57.1f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float n = Fbm(seed + dx * 1.9f, seed + dy * 1.9f, 4);
                float v = n + (1f - r) * 0.62f - 0.72f;
                float a = Mathf.Clamp01(v * 8f) * Mathf.Clamp01((1f - r) * 2.6f);
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

        // The pre-EZ backdrop is still in the Menu scene, nested inside "Panel"
        // rather than sitting directly under the canvas, so this top-level Find
        // never matched it: it kept drawing a second star field and its own
        // horizon sprite on top of this one. Search the whole tree instead.
        foreach (var legacy in canvas.GetComponentsInChildren<RectTransform>(true))
            if (legacy.name == "SpaceBackground") legacy.gameObject.SetActive(false);

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
