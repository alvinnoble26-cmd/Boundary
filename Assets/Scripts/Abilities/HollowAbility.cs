using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class HollowAbility : MonoBehaviour, IAbility
{
    public const float CooldownSeconds = 5f;
    public const float ChargeDuration = 0.75f;
    public const float BlastDuration = 2f;
    public const float DamagePerSecond = 20f;
    public const float MaximumRange = 60f;
    public const float InitialBlastRadius = 2.4f;
    public const float BlastRadius = 7.2f;
    public const float EyeHeight = 1.1f;
    public const float TargetCenterHeight = 0.8f;
    public const float ChargePresentationVerticalOffset = -0.35f;

    private const float VisualPortalRadius = 2.35f;
    private const float VisualPortalSpacing = 12f;
    private const float VisualBeamWidth = 1.35f;

    private static readonly Color Purple = new Color(0.7f, 0.08f, 1.8f, 0.9f);
    private static readonly Color BrightPurple = new Color(1.6f, 0.55f, 3f, 1f);
    private static readonly Color DarkPurple = new Color(0.16f, 0.015f, 0.32f, 0.92f);
    private static readonly Color PortalPurple = new Color(0.34f, 0.035f, 0.62f, 0.82f);
    private static readonly Color VoidBlack = new Color(0.005f, 0f, 0.012f, 0.96f);

    [Header("Presentation Assets")]
    [SerializeField] private GameObject magicCircleTwoPrefab;
    [SerializeField] private Material magicEnergyMaterial;
    [SerializeField] private Texture2D localBlastTexture;
    [SerializeField] private AudioClip chargeClip;
    [SerializeField] private AudioClip blastClip;

    private Coroutine presentation;
    private GameObject presentationRoot;
    private bool ownsWorldGlow;

    private static int worldGlowUsers;
    private static readonly List<FireflyRendererSnapshot> FireflyRenderers =
        new List<FireflyRendererSnapshot>();

    private sealed class FireflyRendererSnapshot
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] boostedMaterials;
    }

    public AbilityId Id => AbilityId.Hollow;
    public float CooldownDuration => CooldownSeconds;
    public void Activate() { }

    public static Vector3 GetBlastOrigin(Vector3 playerPosition, Vector3 direction)
    {
        Vector3 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        return playerPosition + Vector3.up * EyeHeight + normalized * 1.15f;
    }

    public static Vector3 GetChargePresentationPosition(Vector3 playerPosition, Vector3 direction)
    {
        return GetBlastOrigin(playerPosition, direction) + Vector3.up * ChargePresentationVerticalOffset;
    }

    public static bool IsPointInsideBlast(Vector3 point, Vector3 origin, Vector3 direction)
    {
        Vector3 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        float distanceAlong = Vector3.Dot(point - origin, normalized);
        if (distanceAlong < 0f || distanceAlong > MaximumRange)
            return false;
        Vector3 closest = origin + normalized * distanceAlong;
        float radius = GetBlastRadius(distanceAlong);
        return (point - closest).sqrMagnitude <= radius * radius;
    }

    public static float GetBlastRadius(float distanceAlong)
    {
        float distance01 = Mathf.Clamp01(distanceAlong / MaximumRange);
        return Mathf.Lerp(InitialBlastRadius, BlastRadius, distance01);
    }

    public void BeginPresentation(Vector3 direction, bool showCrispBlastEffect)
    {
        if (Application.isBatchMode)
            return;
        if (presentation != null)
        {
            StopCoroutine(presentation);
            ReleaseWorldGlow();
        }
        if (presentationRoot != null)
            Destroy(presentationRoot);
        presentation = StartCoroutine(PlayPresentation(direction.normalized, showCrispBlastEffect));
    }

    private void OnDestroy()
    {
        ReleaseWorldGlow();
        if (presentationRoot != null)
            Destroy(presentationRoot);
    }

    private IEnumerator PlayPresentation(Vector3 direction, bool showCrispBlastEffect)
    {
        AcquireWorldGlow();
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;

        GameObject root = new GameObject("Hollow Presentation");
        presentationRoot = root;
        Transform charge = new GameObject("Hollow Charge").transform;
        charge.SetParent(root.transform, false);

        Material purpleMaterial = CreateMaterial(Purple);
        Material energyMaterial = magicEnergyMaterial != null ? new Material(magicEnergyMaterial) : null;
        Material brightMaterial = CreateMaterial(BrightPurple);
        Material blackMaterial = CreateMaterial(VoidBlack);
        Material hotWhiteMaterial = CreateMaterial(new Color(5f, 4.2f, 5f, 1f));
        Texture2D softTexture = CreateSoftParticleTexture();
        Material softPurpleMaterial = CreateParticleMaterial(softTexture);
        Material softWhiteMaterial = CreateParticleMaterial(softTexture);
        GameObject core = CreateSphere(charge, "Void Core", blackMaterial, 0.2f);
        GameObject energyShell = energyMaterial != null
            ? CreateSphere(charge, "G-Spot Magic Energy Shell", energyMaterial, 0.28f)
            : null;
        GameObject brightCore = CreateSphere(charge, "White Hot Core", hotWhiteMaterial, 0.16f);
        ParticleSystem coreGlow = CreateCoreGlow(charge, softWhiteMaterial);
        LineRenderer[] chargeLightning = CreateChargeLightning(charge, brightMaterial);
        ParticleSystem particles = CreateChargeParticles(charge, softPurpleMaterial);
        ParticleSystem sparks = CreateSparkParticles(charge, softWhiteMaterial, "Hollow Charge Sparks", 1.15f, true);
        Light chargeLight = CreateLight(charge, new Color(1f, 0.72f, 1f), 10f, 10f);
        SpawnCasterMagicCircle(root.transform);
        PlayClip(chargeClip, transform.position, 0.7f);

        float chargeStartedAt = Time.time;
        while (Time.time - chargeStartedAt < ChargeDuration)
        {
            float t = Mathf.Clamp01((Time.time - chargeStartedAt) / ChargeDuration);
            charge.position = GetChargePresentationPosition(transform.position, direction);
            float releaseRamp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, t));
            float pulse = 0.32f + Mathf.SmoothStep(0f, 0.18f, t) + Mathf.Sin(Time.time * 18f) * 0.02f;
            charge.localScale = Vector3.one * pulse;
            core.transform.Rotate(17f * Time.deltaTime, 31f * Time.deltaTime, 9f * Time.deltaTime, Space.Self);
            if (energyShell != null)
                energyShell.transform.Rotate(-28f * Time.deltaTime, 46f * Time.deltaTime,
                    15f * Time.deltaTime, Space.Self);
            brightCore.transform.localScale = Vector3.one *
                (0.16f + t * 0.13f + releaseRamp * 0.22f + Mathf.Sin(Time.time * 25f) * 0.022f);
            coreGlow.transform.localScale = Vector3.one * (0.9f + t * 0.25f + releaseRamp * 1.05f);
            chargeLight.intensity = Mathf.Lerp(10f, 20f, t) + releaseRamp * 14f +
                Mathf.Sin(Time.time * 22f) * 0.8f;
            UpdateChargeLightning(chargeLightning, t);
            yield return null;
        }

        Vector3 origin = GetBlastOrigin(transform.position, direction);
        PlayClip(blastClip, origin, 0.85f);
        if (showCrispBlastEffect)
            SpawnCrispBurst(root.transform, origin, direction);
        charge.gameObject.SetActive(false);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        Transform[] shockwaves = CreateShockwaves(root.transform, origin, direction, brightMaterial, blackMaterial);
        LineRenderer[] radialStreaks = CreateRadialStreaks(root.transform, origin, direction, brightMaterial);
        LineRenderer[] lightningArcs = CreateLightningArcs(root.transform, brightMaterial);
        ParticleSystem blastSparks = CreateSparkParticles(root.transform, brightMaterial,
            "Hollow Blast Sparks", VisualPortalRadius, false);
        blastSparks.transform.position = origin;
        ParticleSystem endpointGlow = CreateCoreGlow(root.transform, softWhiteMaterial);
        endpointGlow.name = "Hollow Moving Endpoint Glow";
        endpointGlow.transform.localScale = Vector3.one * 2.2f;
        Light blastLight = CreateLight(root.transform, BrightPurple, 15f, BlastRadius * 3.5f);
        blastLight.transform.position = origin;

        LineRenderer outerBeam = CreateBeam(root.transform, "Purple Blast Aura", purpleMaterial, VisualBeamWidth);
        LineRenderer innerBeam = CreateBeam(root.transform, "Bright Purple Blast", brightMaterial, VisualBeamWidth * 0.52f);
        LineRenderer coreBeam = CreateBeam(root.transform, "White Hot Hollow Core", hotWhiteMaterial, VisualBeamWidth * 0.14f);
        LineRenderer[] beams = { outerBeam, innerBeam, coreBeam };
        Transform[] circles = CreateBlastCircles(root.transform, origin, direction, blackMaterial, brightMaterial);
        Transform[] groundRipples = CreateGroundRipples(root.transform, transform.position, brightMaterial);

        float blastStartedAt = Time.time;
        while (Time.time - blastStartedAt < BlastDuration)
        {
            float t = Mathf.Clamp01((Time.time - blastStartedAt) / BlastDuration);
            float visibleLength = Mathf.Lerp(MaximumRange * 0.12f, MaximumRange,
                Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 7f)));
            UpdateBeamPositions(beams, origin, direction, visibleLength, t);
            float fade = 1f - t;
            outerBeam.startColor = outerBeam.endColor = WithAlpha(Purple, fade * 0.32f);
            innerBeam.startColor = innerBeam.endColor = WithAlpha(BrightPurple, fade * 0.82f);
            coreBeam.startColor = coreBeam.endColor = WithAlpha(Color.white, fade);
            Vector3 endpoint = origin + direction * visibleLength;
            endpointGlow.transform.position = endpoint;
            endpointGlow.transform.localScale = Vector3.one * (1.4f + Mathf.Sin(Time.time * 24f) * 0.2f);
            blastLight.transform.position = endpoint;
            blastLight.intensity = Mathf.Lerp(18f, 0f, t) + Mathf.Sin(Time.time * 28f) * fade * 2f;
            UpdateLightningArcs(lightningArcs, origin, direction, visibleLength, t, fade);
            for (int index = 0; index < shockwaves.Length; index++)
            {
                float waveT = Mathf.Repeat(t * 2.4f + index * 0.34f, 1f);
                shockwaves[index].localScale = Vector3.one * Mathf.Lerp(0.2f, 2.3f, waveT);
                LineRenderer wave = shockwaves[index].GetComponent<LineRenderer>();
                wave.startColor = wave.endColor = WithAlpha(BrightPurple, (1f - waveT) * fade * 0.75f);
            }
            for (int index = 0; index < radialStreaks.Length; index++)
            {
                float flicker = 0.45f + Mathf.Sin(Time.time * 31f + index * 1.7f) * 0.35f;
                radialStreaks[index].startColor = radialStreaks[index].endColor =
                    WithAlpha(BrightPurple, Mathf.Clamp01(flicker) * fade);
            }
            foreach (Transform circle in circles)
            {
                circle.Rotate(0f, 0f, 110f * Time.deltaTime, Space.Self);
                float portalDistance = Vector3.Dot(circle.position - origin, direction);
                float arrival = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(portalDistance - 3f, portalDistance, visibleLength));
                float distanceScale = Mathf.Lerp(1f, 3f, Mathf.Clamp01(portalDistance / MaximumRange));
                circle.localScale = Vector3.one * arrival * distanceScale *
                    (0.9f + Mathf.Sin(Time.time * 10f + circle.GetSiblingIndex()) * 0.1f);
                foreach (LineRenderer line in circle.GetComponentsInChildren<LineRenderer>())
                    line.startColor = line.endColor = WithAlpha(BrightPurple, fade * arrival);
            }
            for (int index = 0; index < groundRipples.Length; index++)
            {
                float rippleT = Mathf.Repeat(t * 1.8f + index * 0.3f, 1f);
                groundRipples[index].localScale = Vector3.one * Mathf.Lerp(0.25f, 2.2f, rippleT);
                LineRenderer ripple = groundRipples[index].GetComponent<LineRenderer>();
                ripple.startColor = ripple.endColor = WithAlpha(BrightPurple, (1f - rippleT) * fade * 0.7f);
            }
            yield return null;
        }

        Destroy(root);
        Destroy(purpleMaterial);
        if (energyMaterial != null)
            Destroy(energyMaterial);
        Destroy(brightMaterial);
        Destroy(blackMaterial);
        Destroy(hotWhiteMaterial);
        Destroy(softPurpleMaterial);
        Destroy(softWhiteMaterial);
        Destroy(softTexture);
        ReleaseWorldGlow();
        presentationRoot = null;
        presentation = null;
    }

    private void AcquireWorldGlow()
    {
        if (ownsWorldGlow)
            return;
        ownsWorldGlow = true;
        worldGlowUsers++;
        if (worldGlowUsers > 1)
            return;

        BoundaryHazard.SetHollowGlowForAll(true);
        ParticleSystemRenderer[] renderers = FindObjectsByType<ParticleSystemRenderer>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (ParticleSystemRenderer renderer in renderers)
        {
            if (renderer == null || !HasFireflyAncestor(renderer.transform))
                continue;

            Material[] originals = renderer.sharedMaterials;
            Material[] boosted = new Material[originals.Length];
            bool changed = false;
            for (int index = 0; index < originals.Length; index++)
            {
                Material source = originals[index];
                if (source == null || !source.HasProperty("_EmissiveAmount"))
                {
                    boosted[index] = source;
                    continue;
                }

                Material copy = new Material(source)
                {
                    name = source.name + " (Hollow 10x Glow)"
                };
                copy.SetFloat("_EmissiveAmount", source.GetFloat("_EmissiveAmount") * 10f);
                boosted[index] = copy;
                changed = true;
            }

            if (!changed)
                continue;
            renderer.sharedMaterials = boosted;
            FireflyRenderers.Add(new FireflyRendererSnapshot
            {
                renderer = renderer,
                originalMaterials = originals,
                boostedMaterials = boosted
            });
        }
    }

    private void ReleaseWorldGlow()
    {
        if (!ownsWorldGlow)
            return;
        ownsWorldGlow = false;
        worldGlowUsers = Mathf.Max(0, worldGlowUsers - 1);
        if (worldGlowUsers > 0)
            return;

        BoundaryHazard.SetHollowGlowForAll(false);
        foreach (FireflyRendererSnapshot snapshot in FireflyRenderers)
        {
            if (snapshot.renderer != null)
                snapshot.renderer.sharedMaterials = snapshot.originalMaterials;
            foreach (Material material in snapshot.boostedMaterials)
            {
                if (material != null && System.Array.IndexOf(snapshot.originalMaterials, material) < 0)
                    Destroy(material);
            }
        }
        FireflyRenderers.Clear();
    }

    private static bool HasFireflyAncestor(Transform current)
    {
        while (current != null)
        {
            if (current.name.IndexOf("FireFl", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            current = current.parent;
        }
        return false;
    }

    private GameObject SpawnCasterMagicCircle(Transform parent)
    {
        if (magicCircleTwoPrefab == null)
            return null;

        GameObject circle = Instantiate(magicCircleTwoPrefab,
            transform.position + Vector3.up * 0.035f, Quaternion.identity, parent);
        circle.name = "Hollow Magic Circle 2";
        circle.transform.localScale = Vector3.one * 0.7f;
        return circle;
    }

    private void SpawnCrispBurst(Transform parent, Vector3 origin, Vector3 direction)
    {
        if (localBlastTexture == null)
            return;

        Material material = CreateParticleMaterial(localBlastTexture);
        GameObject burstObject = new GameObject("Hollow Crisp Burst", typeof(ParticleSystem));
        burstObject.transform.SetParent(parent, false);
        burstObject.transform.position = origin + direction * 0.45f;
        ParticleSystem particles = burstObject.GetComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 0.12f;
        main.startLifetime = 0.3f;
        main.startSpeed = 0f;
        main.startSize = 1.25f;
        main.startColor = new Color(1f, 0.72f, 1f, 0.95f);
        main.maxParticles = 1;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        ParticleSystemRenderer renderer = burstObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        particles.Play();
        Destroy(burstObject, 0.5f);
        Destroy(material, 0.5f);
    }

    private static void PlayClip(AudioClip clip, Vector3 position, float volume)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    private static Transform[] CreateShockwaves(Transform parent, Vector3 origin, Vector3 direction,
        Material brightMaterial, Material blackMaterial)
    {
        Transform[] waves = new Transform[3];
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
        for (int index = 0; index < waves.Length; index++)
        {
            LineRenderer wave = CreateCircle(parent, "Hollow Shockwave " + index,
                brightMaterial, VisualPortalRadius * 1.15f, 72, 0.045f);
            wave.transform.SetPositionAndRotation(origin + direction * 0.2f, rotation);
            waves[index] = wave.transform;
        }
        return waves;
    }

    private static LineRenderer[] CreateRadialStreaks(Transform parent, Vector3 origin, Vector3 direction,
        Material material)
    {
        const int streakCount = 18;
        LineRenderer[] streaks = new LineRenderer[streakCount];
        Vector3 side = Vector3.Cross(direction, Vector3.up).normalized;
        if (side.sqrMagnitude < 0.001f)
            side = Vector3.right;
        Vector3 up = Vector3.Cross(side, direction).normalized;
        for (int index = 0; index < streakCount; index++)
        {
            float angle = index * Mathf.PI * 2f / streakCount;
            Vector3 radial = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
            GameObject streakObject = new GameObject("Hollow Radial Streak " + index);
            streakObject.transform.SetParent(parent, false);
            LineRenderer streak = streakObject.AddComponent<LineRenderer>();
            streak.sharedMaterial = material;
            streak.useWorldSpace = true;
            streak.positionCount = 2;
            streak.startWidth = 0.08f;
            streak.endWidth = 0f;
            streak.SetPosition(0, origin + radial * VisualPortalRadius * 0.35f);
            streak.SetPosition(1, origin + radial * VisualPortalRadius * 1.7f + direction * 2.5f);
            streaks[index] = streak;
        }
        return streaks;
    }

    private static LineRenderer[] CreateLightningArcs(Transform parent, Material material)
    {
        const int arcCount = 7;
        LineRenderer[] arcs = new LineRenderer[arcCount];
        for (int index = 0; index < arcCount; index++)
        {
            GameObject arcObject = new GameObject("Hollow Lightning Arc " + index);
            arcObject.transform.SetParent(parent, false);
            LineRenderer arc = arcObject.AddComponent<LineRenderer>();
            arc.sharedMaterial = material;
            arc.useWorldSpace = true;
            arc.positionCount = 24;
            arc.startWidth = index == 0 ? 0.16f : 0.065f;
            arc.endWidth = arc.startWidth * 0.45f;
            arc.numCornerVertices = 2;
            arcs[index] = arc;
        }
        return arcs;
    }

    private static LineRenderer[] CreateChargeLightning(Transform parent, Material material)
    {
        const int tendrilCount = 8;
        LineRenderer[] tendrils = new LineRenderer[tendrilCount];
        for (int index = 0; index < tendrilCount; index++)
        {
            GameObject tendrilObject = new GameObject("Charge Lightning Tendril " + index);
            tendrilObject.transform.SetParent(parent, false);
            LineRenderer tendril = tendrilObject.AddComponent<LineRenderer>();
            tendril.sharedMaterial = material;
            tendril.useWorldSpace = false;
            tendril.positionCount = 10;
            tendril.startWidth = 0.018f;
            tendril.endWidth = 0.004f;
            tendril.numCornerVertices = 2;
            tendrils[index] = tendril;
        }
        return tendrils;
    }

    private static void UpdateChargeLightning(LineRenderer[] tendrils, float charge01)
    {
        for (int tendrilIndex = 0; tendrilIndex < tendrils.Length; tendrilIndex++)
        {
            LineRenderer tendril = tendrils[tendrilIndex];
            float angle = tendrilIndex * Mathf.PI * 2f / tendrils.Length + Time.time * 0.7f;
            Vector3 start = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle),
                Mathf.Sin(angle * 2.3f)) * Mathf.Lerp(1.05f, 0.72f, charge01);
            Vector3 tangent = Vector3.Cross(start.normalized, Vector3.forward);
            if (tangent.sqrMagnitude < 0.001f)
                tangent = Vector3.up;
            for (int pointIndex = 0; pointIndex < tendril.positionCount; pointIndex++)
            {
                float pointT = pointIndex / (float)(tendril.positionCount - 1);
                float envelope = Mathf.Sin(pointT * Mathf.PI);
                float jagged = Mathf.Sin(pointIndex * 9.7f + Time.time * 48f + tendrilIndex * 13f) *
                    0.09f * envelope;
                tendril.SetPosition(pointIndex, Vector3.Lerp(start, Vector3.zero, pointT) + tangent * jagged);
            }
            float flicker = 0.45f + Mathf.Sin(Time.time * 67f + tendrilIndex * 7f) * 0.42f;
            tendril.startColor = tendril.endColor =
                WithAlpha(BrightPurple, Mathf.Clamp01(flicker) * Mathf.Lerp(0.35f, 1f, charge01));
        }
    }

    private static void UpdateLightningArcs(LineRenderer[] arcs, Vector3 origin, Vector3 direction,
        float length, float elapsed01, float fade)
    {
        Vector3 side = Vector3.Cross(direction, Vector3.up);
        if (side.sqrMagnitude < 0.001f)
            side = Vector3.right;
        side.Normalize();
        Vector3 up = Vector3.Cross(side, direction).normalized;
        for (int arcIndex = 0; arcIndex < arcs.Length; arcIndex++)
        {
            LineRenderer arc = arcs[arcIndex];
            float angle = arcIndex * Mathf.PI * 2f / arcs.Length + elapsed01 * (arcIndex % 2 == 0 ? 3f : -2f);
            Vector3 radial = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
            float radialDistance = arcIndex == 0 ? 0f : VisualPortalRadius * (0.08f + arcIndex * 0.045f);
            for (int pointIndex = 0; pointIndex < arc.positionCount; pointIndex++)
            {
                float pointT = pointIndex / (float)(arc.positionCount - 1);
                float envelope = Mathf.Sin(pointT * Mathf.PI);
                float visualRadius = Mathf.Lerp(VisualPortalRadius, VisualPortalRadius * 3f,
                    Mathf.Clamp01(length * pointT / MaximumRange));
                float jitter = Mathf.Sin(pointIndex * 8.13f + elapsed01 * 130f + arcIndex * 17f) *
                    visualRadius * 0.085f * envelope;
                Vector3 crossJitter = radial * jitter + Vector3.Cross(direction, radial) *
                    Mathf.Sin(pointIndex * 5.71f - elapsed01 * 97f) * visualRadius * 0.045f * envelope;
                arc.SetPosition(pointIndex,
                    origin + direction * (length * pointT) +
                    radial * radialDistance * (visualRadius / VisualPortalRadius) * envelope + crossJitter);
            }
            float flicker = 0.55f + Mathf.Sin(elapsed01 * 190f + arcIndex * 9f) * 0.4f;
            arc.startColor = arc.endColor = WithAlpha(BrightPurple, Mathf.Clamp01(flicker) * fade);
        }
    }

    private static void UpdateBeamPositions(LineRenderer[] beams, Vector3 origin, Vector3 direction,
        float length, float elapsed01)
    {
        Vector3 side = Vector3.Cross(direction, Vector3.up);
        if (side.sqrMagnitude < 0.001f)
            side = Vector3.right;
        side.Normalize();
        Vector3 up = Vector3.Cross(side, direction).normalized;
        for (int pointIndex = 0; pointIndex < 16; pointIndex++)
        {
            float t = pointIndex / 15f;
            float wave = Mathf.Sin(pointIndex * 2.35f + elapsed01 * 42f) *
                Mathf.Sin(t * Mathf.PI) * 0.16f;
            Vector3 point = origin + direction * (length * t) + side * wave + up * wave * 0.45f;
            foreach (LineRenderer beam in beams)
                beam.SetPosition(pointIndex, point);
        }
    }

    private static Transform[] CreateBlastCircles(Transform parent, Vector3 origin, Vector3 direction,
        Material blackMaterial, Material purpleMaterial)
    {
        List<Transform> circles = new List<Transform>();
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
        GameObject iceArrowCirclePrefab = Resources.Load<GameObject>("Hollow/HollowMagicCircle");
        for (float distance = 7.5f; distance < MaximumRange; distance += VisualPortalSpacing)
        {
            Transform gate = new GameObject("Hollow Portal Gate " + circles.Count).transform;
            gate.SetParent(parent, false);
            gate.SetPositionAndRotation(origin + direction * distance, rotation);
            if (iceArrowCirclePrefab != null)
            {
                GameObject magicCircle = Instantiate(iceArrowCirclePrefab, gate);
                magicCircle.name = "Dark Purple Ice Arrow Magic Circle";
                magicCircle.transform.localPosition = Vector3.zero;
                magicCircle.transform.localRotation = Quaternion.identity;
                magicCircle.transform.localScale = Vector3.one * 1.6f;
                foreach (ParticleSystem particleSystem in magicCircle.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(DarkPurple, PortalPurple);
                    particleSystem.Play(true);
                }
            }
            for (int ringIndex = 0; ringIndex < 4; ringIndex++)
            {
                float radius = VisualPortalRadius * (0.72f + ringIndex * 0.11f);
                LineRenderer ring = CreateCircle(gate, "Portal Energy Ring " + ringIndex,
                    purpleMaterial, radius, 72, ringIndex == 1 ? 0.065f : 0.035f);
                ring.transform.localPosition = Vector3.back * (0.06f + ringIndex * 0.015f);
                ring.transform.localRotation = Quaternion.Euler(0f, 0f, ringIndex * 17f);
            }
            circles.Add(gate);
        }
        return circles.ToArray();
    }

    private static Transform[] CreateGroundRipples(Transform parent, Vector3 playerPosition, Material material)
    {
        Transform[] ripples = new Transform[3];
        Vector3 position = playerPosition + Vector3.up * 0.05f;
        for (int index = 0; index < ripples.Length; index++)
        {
            LineRenderer ripple = CreateCircle(parent, "Hollow Ground Ripple " + index,
                material, VisualPortalRadius * 1.2f, 72, 0.035f);
            ripple.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
            ripples[index] = ripple.transform;
        }
        return ripples;
    }

    private static LineRenderer CreateBeam(Transform parent, string name, Material material, float width)
    {
        GameObject beamObject = new GameObject(name);
        beamObject.transform.SetParent(parent, false);
        LineRenderer beam = beamObject.AddComponent<LineRenderer>();
        beam.sharedMaterial = material;
        beam.positionCount = 16;
        beam.useWorldSpace = true;
        beam.startWidth = width;
        beam.endWidth = width * 3f;
        beam.numCornerVertices = 6;
        beam.numCapVertices = 6;
        return beam;
    }

    private static LineRenderer CreateCircle(Transform parent, string name, Material material,
        float radius, int segments, float width)
    {
        GameObject circleObject = new GameObject(name);
        circleObject.transform.SetParent(parent, false);
        LineRenderer circle = circleObject.AddComponent<LineRenderer>();
        circle.sharedMaterial = material;
        circle.useWorldSpace = false;
        circle.loop = true;
        circle.positionCount = segments;
        circle.startWidth = circle.endWidth = width;
        circle.numCornerVertices = 3;
        for (int index = 0; index < segments; index++)
        {
            float angle = index * Mathf.PI * 2f / segments;
            circle.SetPosition(index,
                new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
        return circle;
    }

    private static GameObject CreateSphere(Transform parent, string name, Material material, float scale)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(parent, false);
        sphere.transform.localScale = Vector3.one * scale;
        Destroy(sphere.GetComponent<Collider>());
        sphere.GetComponent<Renderer>().sharedMaterial = material;
        return sphere;
    }

    private static ParticleSystem CreateChargeParticles(Transform parent, Material material)
    {
        GameObject particleObject = new GameObject("Hollow Charge Particles");
        particleObject.transform.SetParent(parent, false);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(-1.8f, -0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(Purple, VoidBlack);
        main.maxParticles = 180;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 95f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.25f;
        shape.radiusThickness = 0.18f;
        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.28f;
        noise.frequency = 0.7f;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        particles.Play();
        return particles;
    }

    private static ParticleSystem CreateCoreGlow(Transform parent, Material material)
    {
        GameObject glowObject = new GameObject("Soft White Core Glow");
        glowObject.transform.SetParent(parent, false);
        ParticleSystem glow = glowObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = glow.main;
        main.loop = true;
        main.startLifetime = 0.4f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.65f, 0.82f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white,
            new Color(1f, 0.55f, 1f, 0.82f));
        main.maxParticles = 4;
        ParticleSystem.EmissionModule emission = glow.emission;
        emission.rateOverTime = 4f;
        ParticleSystem.ShapeModule shape = glow.shape;
        shape.enabled = false;
        ParticleSystemRenderer renderer = glow.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        glow.Play();
        return glow;
    }

    private static ParticleSystem CreateSparkParticles(Transform parent, Material material, string name,
        float radius, bool inward)
    {
        GameObject particleObject = new GameObject(name);
        particleObject.transform.SetParent(parent, false);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
        main.startSpeed = inward
            ? new ParticleSystem.MinMaxCurve(-4.5f, -1.8f)
            : new ParticleSystem.MinMaxCurve(8f, 18f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(BrightPurple, Purple);
        main.maxParticles = 320;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = inward ? 150f : 30f;
        if (!inward)
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 180) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        shape.radiusThickness = inward ? 0.15f : 0.45f;
        ParticleSystem.TrailModule trails = particles.trails;
        trails.enabled = true;
        trails.lifetime = 0.16f;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.trailMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.4f;
        particles.Play();
        return particles;
    }

    private static Light CreateLight(Transform parent, Color color, float intensity, float range)
    {
        GameObject lightObject = new GameObject("Hollow Energy Light");
        lightObject.transform.SetParent(parent, false);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        Material material = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        return material;
    }

    private static Material CreateParticleMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        Material material = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));
        material.mainTexture = texture;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        return material;
    }

    private static Texture2D CreateSoftParticleTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Runtime Hollow Soft Particle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedX = (x + 0.5f) / size * 2f - 1f;
                float normalizedY = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.4f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
