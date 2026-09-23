using UnityEngine;

/// <summary>
/// The family of sound an ability's impact belongs to. The voice - not a
/// single tone - is what makes a hit recognisable: an energy blast and a
/// thrown knife need fundamentally different synthesis, not the same sine
/// wave at two pitches. <see cref="AbilitySfx"/> renders one of these per
/// profile.
/// </summary>
public enum AbilityHitVoice
{
    /// <summary>Plasma discharge: swept resonant noise over a detuned core.</summary>
    EnergyBlast,

    /// <summary>Struck steel: a bright scrape transient over inharmonic ring partials.</summary>
    Blade,

    /// <summary>High-voltage arc: gated noise bursts with a jittering ring.</summary>
    Electric,

    /// <summary>Collapsing mass: a descending sub with low rumble and tremolo.</summary>
    Gravity,

    /// <summary>Displaced air: band-passed noise sweeping through the pass.</summary>
    Whoosh,

    /// <summary>Rising warp: staggered partials climbing in pitch.</summary>
    Shimmer,

    /// <summary>Struck glass: inharmonic bell partials with staggered decays.</summary>
    Ice,

    /// <summary>Blunt, tuneless weight - the fallback for unattributed damage.</summary>
    Thud
}

/// <summary>
/// The look and sound of one ability's impact. One profile drives both sides of
/// a hit: the victim's screen treatment and the attacker's confirmation, so a
/// player learns to recognise an ability by colour and tone alone.
/// </summary>
public readonly struct AbilityFeedbackProfile
{
    /// <summary>Short name shown on the hit tag, already upper-case.</summary>
    public readonly string DisplayName;

    /// <summary>
    /// Accent colour shared by the vignette, hit marker and text. This is the
    /// ability's own colour as it appears in the arena - taken from the
    /// ability's visual script, not chosen independently - so the colour that
    /// flashes on your screen is the colour of the thing that hit you.
    /// </summary>
    public readonly Color Accent;

    /// <summary>Which synthesis family renders this ability's impact.</summary>
    public readonly AbilityHitVoice Voice;

    /// <summary>Fundamental of the synthesised impact, in hertz.</summary>
    public readonly float ToneHz;

    /// <summary>
    /// Second partial as a multiple of the fundamental. Voices that build their
    /// own partial series (Blade, Ice) use it as the first inharmonic ratio.
    /// </summary>
    public readonly float Partial;

    /// <summary>Pitch multiplier reached at the end of the sound.</summary>
    public readonly float Sweep;

    /// <summary>0 is a pure tone, 1 is pure noise.</summary>
    public readonly float NoiseMix;

    /// <summary>Length of the synthesised impact, in seconds.</summary>
    public readonly float Duration;

    /// <summary>
    /// How long after a cast this ability may still be blamed for a health
    /// drop. Instant abilities use a short window; sustained ones such as
    /// Hollow or a thrown black hole stay accountable for their whole life.
    /// </summary>
    public readonly float AttributionWindow;

    /// <summary>
    /// How loudly this ability's hit is allowed to dress the screen, from 0
    /// (barely there) to 1 (the full treatment). Sustained abilities keep
    /// refreshing the same overlay for as long as they are damaging you, so
    /// they are deliberately quieter on screen than an instant hit - a
    /// full-strength frame that never fades stops being feedback and starts
    /// being an obstruction.
    /// </summary>
    public readonly float Prominence;

    public AbilityFeedbackProfile(string displayName, Color accent, AbilityHitVoice voice,
        float toneHz, float partial, float sweep, float noiseMix, float duration,
        float attributionWindow, float prominence = 1f)
    {
        DisplayName = displayName;
        Accent = accent;
        Voice = voice;
        ToneHz = toneHz;
        Partial = partial;
        Sweep = sweep;
        NoiseMix = noiseMix;
        Duration = duration;
        AttributionWindow = attributionWindow;
        Prominence = prominence;
    }
}

/// <summary>
/// Single source of truth for per-ability hit feedback. Adding an ability to
/// <see cref="AbilityId"/> and giving it an entry here is all that is needed
/// for it to be fully represented on both players' screens.
///
/// Every accent below is lifted from the ability's own visual script so the
/// two can never drift apart; the source colour is named in each comment.
/// </summary>
public static class AbilityFeedbackCatalog
{
    /// <summary>
    /// Used when damage cannot be traced to an ability - an arena hazard, the
    /// closing ring, or a fall. Deliberately colourless so it never imitates an
    /// ability hit.
    /// </summary>
    public static readonly AbilityFeedbackProfile Unattributed = new AbilityFeedbackProfile(
        "IMPACT", new Color(0.86f, 0.88f, 0.95f, 1f), AbilityHitVoice.Thud,
        130f, 1.5f, 0.55f, 0.80f, 0.26f, 0f, 0.8f);

    public static AbilityFeedbackProfile Get(AbilityId id)
    {
        switch (id)
        {
            // Rising warp shimmer. TeleportAbility.TeleportCyan.
            case AbilityId.Teleport:
                return new AbilityFeedbackProfile("TELEPORT",
                    new Color(0.10f, 0.88f, 1f, 1f), AbilityHitVoice.Shimmer,
                    520f, 1.5f, 2.2f, 0.10f, 0.30f, 0.45f);

            // Gritty ground scrape. SlideAbility.SlideOrange.
            case AbilityId.Slide:
                return new AbilityFeedbackProfile("SLIDE",
                    new Color(1f, 0.38f, 0.10f, 1f), AbilityHitVoice.Whoosh,
                    240f, 1.5f, 0.55f, 0.85f, 0.26f, 0.4f);

            // Short compressed air burst. DashAbility.DashBlue.
            case AbilityId.Dash:
                return new AbilityFeedbackProfile("DASH",
                    new Color(0.18f, 0.66f, 1f, 1f), AbilityHitVoice.Whoosh,
                    420f, 1.5f, 0.45f, 0.90f, 0.20f, 0.4f);

            // Deep collapse. BlackHoleKill.AccretionGlow. Stays blameable while
            // the hole lives, so its overlay is held back a little.
            case AbilityId.BlackThrow:
                return new AbilityFeedbackProfile("BLACK HOLE",
                    new Color(0.62f, 0.10f, 1f, 1f), AbilityHitVoice.Gravity,
                    78f, 2.0f, 0.55f, 0.30f, 0.55f, 6f, 0.75f);

            // Inward pull - the sweep rises rather than falls. ForceField.AttractBlue.
            case AbilityId.AttractThrow:
                return new AbilityFeedbackProfile("ATTRACT",
                    new Color(0.16f, 0.52f, 1f, 1f), AbilityHitVoice.Gravity,
                    150f, 1.5f, 1.70f, 0.28f, 0.34f, 2.5f);

            // Outward shove of air. ForceField.RepelRed into RepelOrange.
            case AbilityId.RepelThrow:
                return new AbilityFeedbackProfile("REPEL",
                    new Color(1f, 0.22f, 0.06f, 1f), AbilityHitVoice.Whoosh,
                    260f, 1.25f, 0.40f, 0.75f, 0.30f, 2.5f);

            // Taut steel cable snapping home. GrappleAbility.CableCyan.
            case AbilityId.Grapple:
                return new AbilityFeedbackProfile("GRAPPLE",
                    new Color(0.20f, 0.84f, 1f, 1f), AbilityHitVoice.Blade,
                    330f, 2.40f, 0.75f, 0.35f, 0.22f, 1.2f);

            // An energy blast, and it should sound like one: a swept plasma
            // roar over a detuned core, not a tone. HollowAbility.BrightPurple,
            // normalised out of its HDR range.
            case AbilityId.Hollow:
                return new AbilityFeedbackProfile("HOLLOW",
                    new Color(0.72f, 0.30f, 1f, 1f), AbilityHitVoice.EnergyBlast,
                    520f, 1.5f, 0.42f, 0.55f, 0.40f, 3.2f, 0.8f);

            // Cold collapsing singularity. VoidAbility.VoidBlue lifted towards
            // VoidViolet, normalised out of its HDR range. Void damages for a
            // full 15 seconds, so its window covers the whole duration and its
            // on-screen treatment is held right down - see Prominence.
            case AbilityId.Void:
                return new AbilityFeedbackProfile("VOID",
                    new Color(0.38f, 0.42f, 1f, 1f), AbilityHitVoice.Gravity,
                    62f, 2.0f, 0.70f, 0.35f, 0.60f, 16f, 0.25f);

            // A thrown knife: struck steel, bright and short.
            // BullseyePresentation.FlamePink.
            case AbilityId.Bullseye:
                return new AbilityFeedbackProfile("BULLSEYE",
                    new Color(1f, 0.10f, 0.52f, 1f), AbilityHitVoice.Blade,
                    1400f, 2.41f, 0.55f, 0.22f, 0.30f, 1.6f);

            // High-voltage arc. ChargeAbility's lightning blue.
            case AbilityId.Charge:
                return new AbilityFeedbackProfile("CHARGE",
                    new Color(0.10f, 0.68f, 1f, 1f), AbilityHitVoice.Electric,
                    900f, 3.0f, 0.65f, 0.70f, 0.28f, 2.2f);

            // Bladed cut, one octave under Bullseye so the two never trade
            // places. SliceAbility.SlicePurple.
            case AbilityId.Slice:
                return new AbilityFeedbackProfile("SLICE",
                    new Color(0.95f, 0.12f, 0.68f, 1f), AbilityHitVoice.Blade,
                    1150f, 2.76f, 0.42f, 0.30f, 0.24f, 0.9f);

            // Struck ice. BaseAbilityVisual.IceBlue.
            case AbilityId.Base:
                return new AbilityFeedbackProfile("BASE",
                    new Color(0.42f, 0.78f, 1f, 1f), AbilityHitVoice.Ice,
                    660f, 2.76f, 0.90f, 0.12f, 0.42f, 1.5f);
        }

        return Unattributed;
    }
}
