// ============================================================================
// ENTROPY ZERO UI KIT  (6 of 6)  EZAbilityFlecks.cs
// Small, short-lived flashes of the game's own abilities going off across the
// menu backdrop at random intervals. Every colour comes straight out of
// AbilityFeedbackCatalog, so a Hollow blast on the menu is the same blue-white
// it is in a match and the two never drift apart.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class EZAbilityFlecks : MonoBehaviour
{
    enum Shape { Beam, Burst, Ring, Slash }

    sealed class Fleck
    {
        public RectTransform rt;
        public Image shape;
        public Image core;
        public Shape kind;
        public Color tint;
        public float t = -1f, life = 1f, spin, ang, size;
    }

    static readonly AbilityId[] Abilities =
    {
        AbilityId.Teleport, AbilityId.Slide, AbilityId.Dash, AbilityId.BlackThrow,
        AbilityId.AttractThrow, AbilityId.RepelThrow, AbilityId.Grapple, AbilityId.Hollow,
        AbilityId.Void, AbilityId.Bullseye, AbilityId.Charge, AbilityId.Slice, AbilityId.Base
    };

    const int PoolSize = 4;
    // Everything left of this is the title and the buttons; nothing fires there.
    const float MenuColumn = 0.36f;

    readonly List<Fleck> pool = new List<Fleck>();
    RectTransform bg;
    float nextAt;

    public static EZAbilityFlecks Create(RectTransform parent)
    {
        Transform existing = parent.Find("EZ Ability Flecks");
        if (existing != null) return existing.GetComponent<EZAbilityFlecks>();

        var go = new GameObject("EZ Ability Flecks", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var flecks = go.AddComponent<EZAbilityFlecks>();
        flecks.Build(parent);
        return flecks;
    }

    void Build(RectTransform parent)
    {
        bg = parent;
        EZ.Stretch((RectTransform)transform);

        for (int i = 0; i < PoolSize; i++)
        {
            var host = new GameObject("EZ Fleck", typeof(RectTransform));
            host.transform.SetParent(transform, false);
            var rt = (RectTransform)host.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            var shape = EZ.Img(rt, "EZ Fleck Shape", EZSprites.Burst, Color.clear);
            EZ.Stretch(shape.rectTransform);
            var core = EZ.Img(rt, "EZ Fleck Core", EZSprites.SoftGlow, Color.clear);
            EZ.Center(core.rectTransform, new Vector2(42f, 42f), Vector2.zero);

            host.SetActive(false);
            pool.Add(new Fleck { rt = rt, shape = shape, core = core });
        }

        nextAt = Time.unscaledTime + Random.Range(0.8f, 2f);
    }

    /// <summary>
    /// Which silhouette stands in for each ability: a lance of energy for the
    /// beams and dashes, an expanding hoop for the thrown fields, a two-pointed
    /// flash for Slice, a detonation for the rest.
    /// </summary>
    static Shape ShapeFor(AbilityId id)
    {
        switch (id)
        {
            case AbilityId.Hollow:
            case AbilityId.Void:
            case AbilityId.Grapple:
            case AbilityId.Dash:
            case AbilityId.Slide:
                return Shape.Beam;
            case AbilityId.Slice:
                return Shape.Slash;
            case AbilityId.Bullseye:
            case AbilityId.AttractThrow:
            case AbilityId.RepelThrow:
            case AbilityId.BlackThrow:
            case AbilityId.Teleport:
                return Shape.Ring;
            default:
                return Shape.Burst;
        }
    }

    void Spawn()
    {
        Fleck fleck = null;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].t < 0f) { fleck = pool[i]; break; }
        }
        if (fleck == null || bg == null)
            return;

        float w = bg.rect.width, h = bg.rect.height;
        if (w < 2f || h < 2f)
            return;

        AbilityId id = Abilities[Random.Range(0, Abilities.Length)];
        fleck.kind = ShapeFor(id);
        fleck.tint = AbilityFeedbackCatalog.Get(id).Accent;
        fleck.life = fleck.kind == Shape.Beam ? Random.Range(0.75f, 1.15f) : Random.Range(0.55f, 0.95f);
        fleck.size = fleck.kind == Shape.Beam ? Random.Range(110f, 180f) : Random.Range(58f, 104f);
        fleck.spin = Random.Range(-70f, 70f);
        fleck.ang = Random.Range(0f, 360f);
        fleck.t = 0f;

        switch (fleck.kind)
        {
            case Shape.Beam: fleck.shape.sprite = EZSprites.Beam; break;
            case Shape.Slash: fleck.shape.sprite = EZSprites.Slash; break;
            case Shape.Ring: fleck.shape.sprite = EZSprites.RingThin; break;
            default: fleck.shape.sprite = EZSprites.Burst; break;
        }

        fleck.rt.sizeDelta = fleck.kind == Shape.Beam
            ? new Vector2(fleck.size * 1.7f, fleck.size * 0.85f)
            : new Vector2(fleck.size, fleck.size);
        fleck.rt.anchoredPosition = new Vector2(
            Random.Range(w * MenuColumn, w * 0.97f), Random.Range(h * 0.06f, h * 0.94f));
        fleck.rt.localRotation = Quaternion.Euler(0f, 0f, fleck.ang);
        fleck.rt.gameObject.SetActive(true);
    }

    void Update()
    {
        float t = Time.unscaledTime;
        bool motion = EZTheme.MotionEnabled;
        float dt = motion ? Mathf.Min(Time.unscaledDeltaTime, 0.05f) : 0f;

        if (motion && t >= nextAt)
        {
            Spawn();
            nextAt = t + Random.Range(1.3f, 3.1f);
        }

        for (int i = 0; i < pool.Count; i++)
        {
            Fleck fleck = pool[i];
            if (fleck.t < 0f)
                continue;

            fleck.t += dt / Mathf.Max(0.05f, fleck.life);
            if (fleck.t >= 1f)
            {
                fleck.t = -1f;
                fleck.rt.gameObject.SetActive(false);
                continue;
            }

            // Snap in, ease out - a hit should arrive, not swell.
            float envelope = fleck.t < 0.22f
                ? fleck.t / 0.22f
                : 1f - (fleck.t - 0.22f) / 0.78f;
            envelope = Mathf.Clamp01(envelope);

            float grow = fleck.kind == Shape.Ring
                ? Mathf.Lerp(0.35f, 1.40f, fleck.t)
                : Mathf.Lerp(0.74f, 1.14f, EZ.EaseOutCubic(fleck.t));
            fleck.rt.localScale = new Vector3(grow, grow, 1f);

            fleck.ang += fleck.spin * dt;
            fleck.rt.localRotation = Quaternion.Euler(0f, 0f, fleck.ang);

            Color body = fleck.tint;
            body.a = envelope * 0.9f;
            fleck.shape.color = body;

            Color heart = Color.Lerp(fleck.tint, Color.white, 0.55f);
            heart.a = envelope * (fleck.kind == Shape.Ring ? 0.35f : 0.7f);
            fleck.core.color = heart;
        }
    }
}
