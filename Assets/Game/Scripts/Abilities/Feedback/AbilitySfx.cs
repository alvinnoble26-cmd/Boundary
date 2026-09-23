using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Synthesises one short impact sound per ability at runtime, following the
/// same generated-clip approach <see cref="BoundaryHUD"/> already uses for its
/// phase cues. No audio assets and no Boot-scene wiring are required.
///
/// Each ability sounds like the thing that hit you because the profile picks a
/// <see cref="AbilityHitVoice"/> - a whole synthesis family - rather than just
/// a pitch. An energy blast is swept resonant noise over a detuned core; a
/// thrown knife is a scrape transient over inharmonic steel partials; an arc
/// is gated crackle. Sharing one oscillator between them, as this used to,
/// made every ability land as the same beep at a different pitch.
/// </summary>
public static class AbilitySfx
{
    public enum Role
    {
        /// <summary>Crisp, quieter confirmation for the player who landed the hit.</summary>
        Dealt,
        /// <summary>Heavier, lower impact for the player who was hit.</summary>
        Taken
    }

    private const int SampleRate = 44100;
    private const float Tau = 6.2831853f;
    private const float DealtVolume = 0.45f;
    private const float TakenVolume = 0.72f;

    // Every clip is normalised to the same peak so a voice built from
    // resonant filters cannot arrive twice as loud as a plain oscillator.
    private const float NormalisedPeak = 0.90f;

    private static readonly Dictionary<int, AudioClip> Clips = new Dictionary<int, AudioClip>();
    private static AudioSource localSource;
    private static AudioClip dragonRoarDealtClip;
    private static AudioClip dragonRoarTakenClip;

    public static void PlayDealt(AbilityId id) => PlayLocal(id, Role.Dealt, DealtVolume);

    public static void PlayTaken(AbilityId id) => PlayLocal(id, Role.Taken, TakenVolume);

    /// <summary>
    /// A landed Bullseye knife on the inner circle - the opponent's own
    /// revealed outline, not just the outer ring - is the ability's best
    /// possible outcome, and it is answered by a dragon roar rather than
    /// another knife sound: a growling formant voice that swells, rasps and
    /// falls away. Played in addition to the normal dealt cue, once the real
    /// damage amount confirms which zone was hit.
    /// </summary>
    public static void PlayBullseyeCenterDealt()
    {
        if (IsSilentBuild())
            return;

        if (dragonRoarDealtClip == null)
            dragonRoarDealtClip = SynthesiseDragonRoar(false);

        EnsureLocalSource();
        if (localSource != null)
            localSource.PlayOneShot(dragonRoarDealtClip, 0.55f);
    }

    /// <summary>The victim's half of <see cref="PlayBullseyeCenterDealt"/> - lower and closer.</summary>
    public static void PlayBullseyeCenterTaken()
    {
        if (IsSilentBuild())
            return;

        if (dragonRoarTakenClip == null)
            dragonRoarTakenClip = SynthesiseDragonRoar(true);

        EnsureLocalSource();
        if (localSource != null)
            localSource.PlayOneShot(dragonRoarTakenClip, 0.70f);
    }

    /// <summary>Unattributed damage still deserves a thud, just a neutral one.</summary>
    public static void PlayUnattributedTaken() =>
        PlayLocalProfile(AbilityFeedbackCatalog.Unattributed, -1, Role.Taken, TakenVolume);

    private static void PlayLocal(AbilityId id, Role role, float volume) =>
        PlayLocalProfile(AbilityFeedbackCatalog.Get(id), (int)id, role, volume);

    private static void PlayLocalProfile(AbilityFeedbackProfile profile, int key, Role role, float volume)
    {
        if (IsSilentBuild())
            return;

        AudioClip clip = GetClip(profile, key, role);
        if (clip == null)
            return;

        EnsureLocalSource();
        if (localSource != null)
            localSource.PlayOneShot(clip, volume);
    }

    private static void EnsureLocalSource()
    {
        if (localSource != null)
            return;

        GameObject host = new GameObject("Ability Hit SFX");
        Object.DontDestroyOnLoad(host);
        localSource = host.AddComponent<AudioSource>();
        localSource.playOnAwake = false;
        // A hit landing on you is a UI event, not a world event: keep it 2D so
        // it reads identically wherever the camera happens to be looking.
        localSource.spatialBlend = 0f;
    }

    private static AudioClip GetClip(AbilityFeedbackProfile profile, int key, Role role)
    {
        int cacheKey = key * 8 + (int)role;
        if (Clips.TryGetValue(cacheKey, out AudioClip cached) && cached != null)
            return cached;

        AudioClip clip = Synthesise(profile, role);
        Clips[cacheKey] = clip;
        return clip;
    }

    private static AudioClip Synthesise(AbilityFeedbackProfile profile, Role role)
    {
        bool taken = role == Role.Taken;
        float pitchScale = taken ? 0.84f : 1.22f;
        float lengthScale = taken ? 1f : 0.70f;

        float duration = Mathf.Clamp(profile.Duration * lengthScale, 0.05f, 1.2f);
        int sampleCount = Mathf.Max(64, Mathf.RoundToInt(duration * SampleRate));
        float[] data = new float[sampleCount];

        float startHz = Mathf.Max(20f, profile.ToneHz * pitchScale);
        float endHz = Mathf.Max(20f, startHz * Mathf.Max(0.05f, profile.Sweep));

        // A fixed seed keeps the clip identical every run, so an ability never
        // sounds subtly different between sessions.
        System.Random noise = new System.Random(
            unchecked(Mathf.RoundToInt(profile.ToneHz) * 73 + (int)profile.Voice * 17 + (int)role));

        switch (profile.Voice)
        {
            case AbilityHitVoice.EnergyBlast:
                RenderEnergyBlast(data, profile, startHz, endHz, noise);
                break;
            case AbilityHitVoice.Blade:
                RenderBlade(data, profile, startHz, endHz, noise);
                break;
            case AbilityHitVoice.Electric:
                RenderElectric(data, profile, startHz, endHz, noise);
                break;
            case AbilityHitVoice.Gravity:
                RenderGravity(data, profile, startHz, endHz, noise);
                break;
            case AbilityHitVoice.Whoosh:
                RenderWhoosh(data, profile, startHz, endHz, noise);
                break;
            case AbilityHitVoice.Shimmer:
                RenderShimmer(data, profile, startHz, endHz, noise);
                break;
            case AbilityHitVoice.Ice:
                RenderIce(data, profile, startHz, noise);
                break;
            default:
                RenderThud(data, profile, startHz, endHz, noise);
                break;
        }

        Finish(data);
        return ToClip(data, "Ability " + profile.DisplayName + " " + role);
    }

    /// <summary>
    /// Plasma discharge. Broadband noise driven through a steeply falling
    /// resonant peak gives the beam its "fwoom"; a pair of detuned saws under
    /// it keeps a pitch you can hear, and a short sub marks the moment of
    /// contact.
    /// </summary>
    private static void RenderEnergyBlast(float[] data, AbilityFeedbackProfile profile,
        float startHz, float endHz, System.Random noise)
    {
        Resonator beam = new Resonator();
        double corePhase = 0d;
        double detunePhase = 0d;
        double subPhase = 0d;
        int count = data.Length;

        for (int index = 0; index < count; index++)
        {
            float progress = (float)index / count;
            float frequency = Mathf.Lerp(startHz, endHz, progress * progress);
            corePhase += frequency / SampleRate;
            detunePhase += frequency * 1.0075f / SampleRate;
            subPhase += frequency * 0.25f / SampleRate;

            float cutoff = Mathf.Lerp(startHz * 3.2f, endHz * 0.9f, Mathf.Sqrt(progress));
            float beamed = beam.Process(Noise(noise), cutoff, 7f) * 2.2f;
            float core = (Saw(corePhase) + Saw(detunePhase)) * 0.5f;
            float sub = Mathf.Sin((float)subPhase * Tau) * Mathf.Pow(1f - progress, 5f);

            float body = Mathf.Lerp(core, beamed, profile.NoiseMix);
            float envelope = Attack(index, 0.004f) * (0.18f + 0.82f * Mathf.Pow(1f - progress, 1.7f));
            data[index] = (body + sub * 0.55f) * envelope;
        }
    }

    /// <summary>
    /// Struck steel. Four inharmonic partials ring and die quickly over a
    /// very short high scrape, which is what separates a blade from a tone:
    /// the ear reads the inharmonicity as metal.
    /// </summary>
    private static void RenderBlade(float[] data, AbilityFeedbackProfile profile,
        float startHz, float endHz, System.Random noise)
    {
        float ratioOne = Mathf.Max(1.2f, profile.Partial);
        float ratioTwo = ratioOne * 1.63f;
        float ratioThree = ratioOne * 2.31f;
        double first = 0d;
        double second = 0d;
        double third = 0d;
        double fourth = 0d;
        double bodyPhase = 0d;
        int count = data.Length;
        int transient = Mathf.Max(1, Mathf.RoundToInt(0.006f * SampleRate));
        Resonator scrape = new Resonator();

        for (int index = 0; index < count; index++)
        {
            float progress = (float)index / count;
            float frequency = Mathf.Lerp(startHz, endHz, progress);
            first += frequency / SampleRate;
            second += frequency * ratioOne / SampleRate;
            third += frequency * ratioTwo / SampleRate;
            fourth += frequency * ratioThree / SampleRate;

            float ring = Mathf.Sin((float)first * Tau) * 0.52f +
                         Mathf.Sin((float)second * Tau) * 0.28f +
                         Mathf.Sin((float)third * Tau) * 0.14f +
                         Mathf.Sin((float)fourth * Tau) * 0.08f;
            ring *= Mathf.Pow(1f - progress, 3.6f);

            float gate = index < transient
                ? 1f
                : Mathf.Pow(Mathf.Clamp01(1f - (index - transient) / (count * 0.08f)), 5f);
            float edge = scrape.Process(Noise(noise), Mathf.Lerp(4200f, 2200f, progress), 1.6f) * 2f * gate;

            // A little mass behind the strike so it lands rather than tinkles.
            bodyPhase += startHz * 0.11f / SampleRate;
            float weight = Mathf.Sin((float)bodyPhase * Tau) * Mathf.Pow(1f - progress, 8f) * 0.4f;

            data[index] = ring * (1f - profile.NoiseMix) * 1.4f +
                          edge * profile.NoiseMix * 1.6f + weight;
        }
    }

    /// <summary>
    /// High-voltage arc. Randomly gating the source at roughly 1.7 kHz is what
    /// turns a hiss into a crackle, and jittering the ring's pitch with the
    /// same gate keeps it from settling into a note.
    /// </summary>
    private static void RenderElectric(float[] data, AbilityFeedbackProfile profile,
        float startHz, float endHz, System.Random noise)
    {
        int gatePeriod = Mathf.Max(1, SampleRate / 1700);
        float gate = 1f;
        float jitter = 1f;
        double ringPhase = 0d;
        Resonator arc = new Resonator();
        int count = data.Length;

        for (int index = 0; index < count; index++)
        {
            if (index % gatePeriod == 0)
            {
                gate = noise.NextDouble() < 0.55d ? 1f : 0.10f;
                jitter = 0.65f + (float)noise.NextDouble() * 0.9f;
            }

            float progress = (float)index / count;
            float frequency = Mathf.Lerp(startHz, endHz, progress) * jitter;
            ringPhase += frequency / SampleRate;

            float ring = Mathf.Sin((float)ringPhase * Tau) * 0.6f +
                         Mathf.Sin((float)ringPhase * Tau * profile.Partial) * 0.4f;
            float crackle = arc.Process(Noise(noise), Mathf.Lerp(3800f, 1500f, progress), 2.4f) * 2.4f;

            float envelope = Attack(index, 0.002f) * Mathf.Pow(1f - progress, 2.4f);
            data[index] = Mathf.Lerp(ring, crackle, profile.NoiseMix) * gate * envelope;
        }
    }

    /// <summary>
    /// Collapsing mass. A sub sweeping under low rumble, with a slow tremolo
    /// so it churns instead of humming.
    /// </summary>
    private static void RenderGravity(float[] data, AbilityFeedbackProfile profile,
        float startHz, float endHz, System.Random noise)
    {
        double subPhase = 0d;
        float rumbleState = 0f;
        int count = data.Length;

        for (int index = 0; index < count; index++)
        {
            float progress = (float)index / count;
            float seconds = index / (float)SampleRate;
            float frequency = Mathf.Lerp(startHz, endHz, Mathf.Sqrt(progress));
            subPhase += frequency / SampleRate;

            float sub = Mathf.Sin((float)subPhase * Tau) +
                        Mathf.Sin((float)subPhase * Tau * profile.Partial) * 0.32f;
            float rumble = OnePole(ref rumbleState, Noise(noise), 150f) * 3.2f;
            float tremolo = 1f + 0.32f * Mathf.Sin(seconds * 7f * Tau);

            float envelope = Mathf.Min(1f, progress / 0.12f) * Mathf.Pow(1f - progress, 1.6f);
            data[index] = (sub * 0.85f + rumble * profile.NoiseMix) * tremolo * envelope;
        }
    }

    /// <summary>
    /// Displaced air. The pass band opens and closes across the clip, which is
    /// what makes a whoosh travel rather than just hiss.
    /// </summary>
    private static void RenderWhoosh(float[] data, AbilityFeedbackProfile profile,
        float startHz, float endHz, System.Random noise)
    {
        Resonator air = new Resonator();
        double bodyPhase = 0d;
        int count = data.Length;

        for (int index = 0; index < count; index++)
        {
            float progress = (float)index / count;
            float arc = Mathf.Sin(progress * Mathf.PI);
            float cutoff = Mathf.Lerp(startHz * 0.8f, startHz * 4.5f, arc);
            float air1 = air.Process(Noise(noise), cutoff, 2.2f) * 2.4f;

            float frequency = Mathf.Lerp(startHz, endHz, progress);
            bodyPhase += frequency * 0.5f / SampleRate;
            float body = Mathf.Sin((float)bodyPhase * Tau) * 0.22f * Mathf.Pow(1f - progress, 2f);

            float envelope = Attack(index, 0.005f) * Mathf.Pow(arc, 0.7f) *
                             Mathf.Pow(1f - progress, 0.9f);
            data[index] = (air1 * profile.NoiseMix + body) * envelope;
        }
    }

    /// <summary>
    /// Rising warp. Partials enter one after another as the pitch climbs, so
    /// the sound assembles itself upward the way the teleport visual does.
    /// </summary>
    private static void RenderShimmer(float[] data, AbilityFeedbackProfile profile,
        float startHz, float endHz, System.Random noise)
    {
        float[] ratios = { 1f, 1.5f, 2f, 3f };
        double[] phases = new double[ratios.Length];
        int count = data.Length;

        for (int index = 0; index < count; index++)
        {
            float progress = (float)index / count;
            float frequency = Mathf.Lerp(startHz, endHz, Mathf.Pow(progress, 0.7f));

            float sum = 0f;
            for (int partial = 0; partial < ratios.Length; partial++)
            {
                phases[partial] += frequency * ratios[partial] / SampleRate;
                float onset = Mathf.Clamp01((progress - partial * 0.10f) / 0.12f);
                sum += Mathf.Sin((float)phases[partial] * Tau) * onset / (1f + partial * 1.1f);
            }

            float sparkle = Noise(noise) * profile.NoiseMix * Mathf.Pow(1f - progress, 4f);
            float envelope = Attack(index, 0.003f) * Mathf.Pow(1f - progress, 1.3f);
            data[index] = (sum * 0.55f + sparkle) * envelope;
        }
    }

    /// <summary>
    /// Struck glass. Inharmonic partials with independent decay rates - the
    /// high ones die first - which is what a bell does and a sine cannot.
    /// </summary>
    private static void RenderIce(float[] data, AbilityFeedbackProfile profile,
        float startHz, System.Random noise)
    {
        float partialRatio = Mathf.Max(1.2f, profile.Partial);
        float[] ratios = { 1f, partialRatio, partialRatio * 1.96f, partialRatio * 3.9f };
        double[] phases = new double[ratios.Length];
        int count = data.Length;

        for (int index = 0; index < count; index++)
        {
            float progress = (float)index / count;

            float sum = 0f;
            for (int partial = 0; partial < ratios.Length; partial++)
            {
                phases[partial] += startHz * ratios[partial] / SampleRate;
                sum += Mathf.Sin((float)phases[partial] * Tau) *
                       Mathf.Exp(-progress * (2.6f + ratios[partial] * 1.5f)) / (1f + partial);
            }

            float chiff = Noise(noise) * profile.NoiseMix * Mathf.Pow(1f - progress, 12f);
            data[index] = (sum * 0.8f + chiff) * Attack(index, 0.002f);
        }
    }

    /// <summary>Blunt, tuneless weight for damage with nothing to blame.</summary>
    private static void RenderThud(float[] data, AbilityFeedbackProfile profile,
        float startHz, float endHz, System.Random noise)
    {
        double phase = 0d;
        float lowState = 0f;
        int count = data.Length;

        for (int index = 0; index < count; index++)
        {
            float progress = (float)index / count;
            float frequency = Mathf.Lerp(startHz, endHz, Mathf.Sqrt(progress));
            phase += frequency / SampleRate;

            float tone = Mathf.Sin((float)phase * Tau);
            float dull = OnePole(ref lowState, Noise(noise), 900f) * 2.2f;
            float envelope = Attack(index, 0.003f) * Mathf.Pow(1f - progress, 2.6f);
            data[index] = Mathf.Lerp(tone, dull, profile.NoiseMix) * envelope;
        }
    }

    /// <summary>
    /// A dragon roar for the Bullseye centre hit. A roar is not a low tone:
    /// it is a buzzing glottal source shaped by fixed resonances. So a
    /// sawtooth at chest pitch is amplitude-modulated at ~35 Hz to make it
    /// rasp, then pushed through three formant resonators that stay put while
    /// the pitch bends down. Breath noise through the top formant sits behind
    /// it, and the whole thing swells before it falls away.
    /// </summary>
    private static AudioClip SynthesiseDragonRoar(bool taken)
    {
        float duration = taken ? 1.30f : 1.05f;
        int sampleCount = Mathf.Max(64, Mathf.RoundToInt(duration * SampleRate));
        float[] data = new float[sampleCount];
        System.Random noise = new System.Random(taken ? 911771 : 911772);

        float startHz = taken ? 82f : 104f;
        float endHz = taken ? 54f : 68f;
        float growlHz = taken ? 33f : 39f;
        float formantScale = taken ? 0.88f : 1f;

        Resonator formantOne = new Resonator();
        Resonator formantTwo = new Resonator();
        Resonator formantThree = new Resonator();
        Resonator breathBand = new Resonator();
        float breathState = 0f;
        double sourcePhase = 0d;

        for (int index = 0; index < sampleCount; index++)
        {
            float progress = (float)index / sampleCount;
            float seconds = index / (float)SampleRate;

            // Pitch holds, then bends down as the roar runs out of breath,
            // with a slow vibrato so it never sits perfectly still.
            float bend = Mathf.Pow(Mathf.Clamp01((progress - 0.35f) / 0.65f), 1.4f);
            float frequency = Mathf.Lerp(startHz, endHz, bend) *
                              (1f + 0.035f * Mathf.Sin(seconds * 5.5f * Tau));
            sourcePhase += frequency / SampleRate;

            // Buzzing source: a saw rasped by a deep, slightly drifting
            // amplitude modulation - the growl.
            float rasp = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(seconds * growlHz * Tau));
            float source = Saw(sourcePhase) * rasp;

            float voiced = formantOne.Process(source, 520f * formantScale, 9f) +
                           formantTwo.Process(source, 1180f * formantScale, 7f) * 0.55f +
                           formantThree.Process(source, 2450f * formantScale, 5f) * 0.30f;

            float breath = OnePole(ref breathState, Noise(noise), 3200f);
            float air = breathBand.Process(breath, 2100f * formantScale, 2.2f) * 1.6f;

            // Swell in, hold, then fall away.
            float swell = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.14f));
            float fall = progress < 0.52f
                ? 1f
                : Mathf.Pow(Mathf.Clamp01((1f - progress) / 0.48f), 1.5f);
            float envelope = swell * fall;

            data[index] = (voiced * 0.85f + air * 0.28f) * envelope;
        }

        Finish(data);
        return ToClip(data, "Bullseye Dragon Roar " + (taken ? "Taken" : "Dealt"));
    }

    /// <summary>
    /// Levels a rendered clip and fades its ends.
    ///
    /// Peak normalisation alone is not enough: a knife strike is nearly all
    /// transient and a collapsing sub is nearly all body, so matching their
    /// peaks leaves the knife sounding four times quieter. Each clip is
    /// therefore saturated in proportion to its own crest factor - spiky
    /// sources get driven, dense ones are left alone - which brings their
    /// perceived loudness within about a factor of two of each other before
    /// the final peak normalise.
    /// </summary>
    private static void Finish(float[] data)
    {
        float peak = 0f;
        for (int index = 0; index < data.Length; index++)
            peak = Mathf.Max(peak, Mathf.Abs(data[index]));

        if (peak < 0.0001f)
            return;

        double sumOfSquares = 0d;
        for (int index = 0; index < data.Length; index++)
        {
            data[index] /= peak;
            sumOfSquares += data[index] * (double)data[index];
        }

        float rms = Mathf.Max(0.0001f, Mathf.Sqrt((float)(sumOfSquares / data.Length)));
        float drive = Mathf.Clamp(1f / (rms * 4f), 1f, 3.5f);
        float driveNormaliser = (float)System.Math.Tanh(drive);

        float saturatedPeak = 0f;
        for (int index = 0; index < data.Length; index++)
        {
            data[index] = (float)System.Math.Tanh(data[index] * drive) / driveNormaliser;
            saturatedPeak = Mathf.Max(saturatedPeak, Mathf.Abs(data[index]));
        }

        float gain = saturatedPeak > 0.0001f ? NormalisedPeak / saturatedPeak : 0f;
        int fadeIn = Mathf.Min(data.Length / 4, Mathf.RoundToInt(0.0015f * SampleRate));
        int fadeOut = Mathf.Min(data.Length / 4, Mathf.RoundToInt(0.006f * SampleRate));

        for (int index = 0; index < data.Length; index++)
        {
            float edge = 1f;
            if (fadeIn > 0 && index < fadeIn)
                edge = index / (float)fadeIn;
            int fromEnd = data.Length - 1 - index;
            if (fadeOut > 0 && fromEnd < fadeOut)
                edge = Mathf.Min(edge, fromEnd / (float)fadeOut);

            data[index] = Mathf.Clamp(data[index] * gain * edge, -1f, 1f);
        }
    }

    private static AudioClip ToClip(float[] data, string clipName)
    {
        AudioClip clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Noise(System.Random source) => (float)(source.NextDouble() * 2d - 1d);

    /// <summary>Phase is counted in cycles, so a saw is just the wrapped remainder.</summary>
    private static float Saw(double phase) =>
        (float)(2d * (phase - System.Math.Floor(phase + 0.5d)));

    private static float Attack(int index, float seconds) =>
        Mathf.Min(1f, index / Mathf.Max(1f, seconds * SampleRate));

    private static float OnePole(ref float state, float input, float cutoffHz)
    {
        float coefficient = Mathf.Clamp01(1f - Mathf.Exp(-Tau * cutoffHz / SampleRate));
        state += coefficient * (input - state);
        return state;
    }

    /// <summary>
    /// A two-pole state-variable band-pass. Cheap, stable at the cutoffs used
    /// here, and the one building block every voice above needs to shape noise
    /// into something with a body.
    /// </summary>
    private sealed class Resonator
    {
        private float low;
        private float band;

        public float Process(float input, float cutoffHz, float q)
        {
            // Capped well below Nyquist: the simple SVF loses stability as the
            // cutoff approaches it.
            float clamped = Mathf.Clamp(cutoffHz, 20f, SampleRate * 0.11f);
            float f = 2f * Mathf.Sin(Mathf.PI * clamped / SampleRate);
            float damp = Mathf.Clamp(1f / Mathf.Max(0.5f, q), 0.02f, 2f);

            float high = input - low - damp * band;
            band += f * high;
            low += f * band;

            if (float.IsNaN(band) || float.IsInfinity(band))
            {
                low = 0f;
                band = 0f;
            }

            return band;
        }
    }

    private static bool IsSilentBuild()
    {
        return Application.isBatchMode ||
               SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
    }
}
