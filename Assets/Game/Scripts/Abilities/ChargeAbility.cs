using System.Collections;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;
using UnityEngine.VFX;

public sealed class ChargeAbility : MonoBehaviour, IAbility
{
    public const float CooldownSeconds = 7f;
    public const float ChargeSeconds = 0.5f;
    public const float FirstTickDelay = 2f;
    public const float SecondTickDelay = 2f;
    public const float ExplosionRadius = 10f;
    public const float FirstTickDamage = 5f;
    public const float SecondTickDamage = 7f;
    public const float ProjectileSpeed = 36f;
    public const float ProjectileRadius = 0.38f;
    public const float TargetCenterHeight = 0.8f;
    public const float HitEffectDuration = 2f;

    public AbilityId Id => AbilityId.Charge;
    public float CooldownDuration => CooldownSeconds;
    public void Activate() { }

    public static bool IsInsideExplosion(Vector3 playerPosition, Vector3 center)
    {
        return Vector3.Distance(playerPosition, center) <= ExplosionRadius;
    }

    public static Vector3 GetContactCenter(Vector3 position, Vector3 direction,
        float hitDistance)
    {
        return position + direction.normalized * Mathf.Max(0f, hitDistance);
    }

    public static Vector3 GetStaffChargeOrigin(Bounds swordBounds, Vector3 screenUp,
        Vector3 screenForward)
    {
        return swordBounds.center + screenUp.normalized * (swordBounds.extents.y * 0.72f) +
            screenForward.normalized * 0.045f;
    }

    public static Vector3 GetAnchoredChargeOrigin(Transform playerAnchor,
        Vector3 initialWorldOrigin)
    {
        return playerAnchor != null
            ? playerAnchor.InverseTransformPoint(initialWorldOrigin)
            : initialWorldOrigin;
    }
}

public sealed class ChargeBallPresentation : MonoBehaviour
{
    private const float MissingRpcCleanupGraceSeconds = 2f;

    private sealed class BallState
    {
        public GameObject root;
        public Vector3 direction;
        public Vector3 localChargeOrigin;
        public bool released;
        public bool stopped;
        public float elapsedAtBegin;
    }

    private readonly Dictionary<int, BallState> balls = new Dictionary<int, BallState>();
    private VisualEffectAsset magicBallAsset;
    private AudioClip releaseClip;
    private AudioClip firstTickClip;
    private AudioClip secondTickClip;
    private GameObject plexusAuraPrefab;

    public void Configure(VisualEffectAsset ballAsset, AudioClip release,
        AudioClip firstTick, AudioClip secondTick, GameObject plexusPrefab)
    {
        magicBallAsset = ballAsset;
        releaseClip = release;
        firstTickClip = firstTick;
        secondTickClip = secondTick;
        plexusAuraPrefab = plexusPrefab;
    }

    public void Begin(int id, Vector3 origin, Vector3 direction, float elapsed = 0f)
    {
        RemoveBall(id);
        GameObject root = new GameObject("Charge Magic Ball " + id);
        root.transform.position = origin;
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Charge Ball Blue Core";
        core.transform.SetParent(root.transform, false);
        Object.Destroy(core.GetComponent<Collider>());
        core.GetComponent<Renderer>().sharedMaterial =
            AbilityRuntimeMaterialOwner.Track(root, CreateBlueMaterial());
        root.transform.localScale = Vector3.one * 0.12f;
        if (elapsed >= ChargeAbility.ChargeSeconds)
            root.transform.localScale = Vector3.one * 1.15f;

        if (magicBallAsset != null)
        {
            VisualEffect effect = root.AddComponent<VisualEffect>();
            effect.visualEffectAsset = magicBallAsset;
            effect.Play();
        }
        CreateElectricArcs(root.transform);
        Light light = root.AddComponent<Light>();
        light.color = new Color(0.08f, 0.65f, 1f);
        light.intensity = 9f;
        light.range = 6f;
        light.shadows = LightShadows.None;

        BallState state = new BallState
        {
            root = root,
            direction = direction.normalized,
            localChargeOrigin = ChargeAbility.GetAnchoredChargeOrigin(transform, origin),
            elapsedAtBegin = Mathf.Max(0f, elapsed)
        };
        balls[id] = state;
        StartCoroutine(AnimateBall(state));
        StartCoroutine(RemoveIfSequenceStalls(id, state));
    }

    private void OnDisable()
    {
        ClearBalls();
    }

    private void OnDestroy()
    {
        ClearBalls();
    }

    private void ClearBalls()
    {
        foreach (BallState state in balls.Values)
        {
            if (state.root != null)
                Object.Destroy(state.root);
        }
        balls.Clear();
    }

    private void RemoveBall(int id)
    {
        if (!balls.TryGetValue(id, out BallState state))
            return;

        balls.Remove(id);
        if (state.root != null)
            Object.Destroy(state.root);
    }

    public void FirstTick(int id, Vector3 position, float elapsed = 0f)
    {
        if (!balls.TryGetValue(id, out BallState state) || state.root == null)
            return;
        state.stopped = true;
        state.root.transform.position = position;
        PlayClip(firstTickClip, position, 0.9f);
        StartCoroutine(ExplosionPulse(position, false));
        SpawnExpandingPlexus(position, false);
        StartCoroutine(AnimateUnstable(state, Mathf.Max(0f, elapsed)));
    }

    public void SecondTick(int id, Vector3 position)
    {
        if (!balls.TryGetValue(id, out BallState state) || state.root == null)
            return;
        state.stopped = true;
        state.root.transform.position = position;
        state.root.transform.localScale = Vector3.one * 3.4f;
        Light light = state.root.GetComponent<Light>();
        if (light != null)
            light.intensity = 38f;
        PlayClip(secondTickClip, position, 1f);
        StartCoroutine(ExplosionPulse(position, true));
        SpawnExpandingPlexus(position, true);
        balls.Remove(id);
        Object.Destroy(state.root, 0.28f);
    }

    private IEnumerator AnimateBall(BallState state)
    {
        float startedAt = Time.time - Mathf.Min(state.elapsedAtBegin, ChargeAbility.ChargeSeconds);
        while (state.root != null && Time.time - startedAt < ChargeAbility.ChargeSeconds)
        {
            // The projectile has not launched yet. Keep the buildup attached to
            // the casting player instead of leaving it at the release-frame
            // world position while the player moves.
            state.root.transform.position = transform.TransformPoint(state.localChargeOrigin);
            float progress = (Time.time - startedAt) / ChargeAbility.ChargeSeconds;
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float buildupPulse = 1f + Mathf.Sin(progress * Mathf.PI * 8f) * 0.055f * progress;
            state.root.transform.localScale = Vector3.one *
                Mathf.Lerp(0.12f, 1.15f, eased) * buildupPulse;
            Light chargeLight = state.root.GetComponent<Light>();
            if (chargeLight != null)
                chargeLight.intensity = Mathf.Lerp(2f, 14f, eased) * buildupPulse;
            state.root.transform.Rotate(0f, 220f * Time.deltaTime, 90f * Time.deltaTime);
            yield return null;
        }
        if (state.root == null)
            yield break;
        state.released = true;
        PlayClip(releaseClip, state.root.transform.position, 0.9f);
        while (state.root != null && !state.stopped)
        {
            state.root.transform.position += state.direction * ChargeAbility.ProjectileSpeed * Time.deltaTime;
            state.root.transform.Rotate(100f * Time.deltaTime, 260f * Time.deltaTime, 0f);
            yield return null;
        }
    }

    private IEnumerator AnimateUnstable(BallState state, float elapsedAtStart)
    {
        float startedAt = Time.time - Mathf.Min(elapsedAtStart, ChargeAbility.SecondTickDelay);
        Vector3 baseScale = state.root.transform.localScale;
        while (state.root != null && Time.time - startedAt < ChargeAbility.SecondTickDelay)
        {
            float progress = (Time.time - startedAt) / ChargeAbility.SecondTickDelay;
            float pulse = 1f + Mathf.Sin(Time.time * Mathf.Lerp(12f, 34f, progress)) *
                Mathf.Lerp(0.06f, 0.22f, progress);
            state.root.transform.localScale = baseScale * Mathf.Lerp(1f, 2.25f, progress) * pulse;
            Light light = state.root.GetComponent<Light>();
            if (light != null)
                light.intensity = Mathf.Lerp(10f, 31f, progress) * pulse;
            yield return null;
        }
    }

    private IEnumerator RemoveIfSequenceStalls(int id, BallState state)
    {
        float maximumSequenceDuration = ChargeAbility.ChargeSeconds +
            ChargeAbility.FirstTickDelay + ChargeAbility.SecondTickDelay +
            MissingRpcCleanupGraceSeconds;
        yield return new WaitForSeconds(maximumSequenceDuration);
        if (balls.TryGetValue(id, out BallState current) && current == state)
            RemoveBall(id);
    }

    private IEnumerator ExplosionPulse(Vector3 position, bool finalTick)
    {
        GameObject pulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pulse.name = finalTick ? "Charge Second Damage Pulse" : "Charge First Damage Pulse";
        Object.Destroy(pulse.GetComponent<Collider>());
        pulse.transform.position = position;
        Shader pulseShader = Shader.Find("Sprites/Default");
        if (pulseShader == null) pulseShader = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = AbilityRuntimeMaterialOwner.Track(pulse, new Material(pulseShader));
        pulse.GetComponent<Renderer>().sharedMaterial = material;
        float startedAt = Time.time;
        float duration = finalTick ? 0.42f : 0.34f;
        while (pulse != null && Time.time - startedAt < duration)
        {
            float progress = (Time.time - startedAt) / duration;
            float diameter = ChargeAbility.ExplosionRadius * 2f *
                Mathf.SmoothStep(0f, 1f, progress);
            pulse.transform.localScale = Vector3.one * diameter;
            Color color = new Color(0.08f, 0.62f, 1f,
                (1f - progress) * (finalTick ? 0.42f : 0.28f));
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            yield return null;
        }
        if (pulse != null)
            Object.Destroy(pulse);
    }

    private void SpawnExpandingPlexus(Vector3 position, bool finalTick)
    {
        if (plexusAuraPrefab == null)
            return;

        Vector3[] directions =
        {
            Vector3.up, Vector3.down, Vector3.left, Vector3.right,
            Vector3.forward, Vector3.back,
            new Vector3(1f, 1f, 1f).normalized,
            new Vector3(-1f, 1f, -1f).normalized
        };
        GameObject group = new GameObject(finalTick
            ? "Charge Final Expanding Plexus" : "Charge Expanding Plexus");
        group.transform.position = position;
        foreach (Vector3 direction in directions)
        {
            GameObject plexus = UnityProxy.InstantiateDirectly(plexusAuraPrefab, group.transform);
            plexus.name = "Hovl Plexus Outward " + direction;
            plexus.transform.localPosition = Vector3.zero;
            plexus.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            plexus.transform.localScale = Vector3.one * (finalTick ? 0.42f : 0.3f);
            foreach (ParticleSystem particles in plexus.GetComponentsInChildren<ParticleSystem>(true))
                particles.Play(true);
        }
        StartCoroutine(AnimatePlexusExpansion(group, directions, finalTick));
    }

    private static IEnumerator AnimatePlexusExpansion(GameObject group, Vector3[] directions,
        bool finalTick)
    {
        float duration = finalTick ? 0.7f : 0.58f;
        float distance = finalTick ? ChargeAbility.ExplosionRadius : ChargeAbility.ExplosionRadius * 0.82f;
        float startedAt = Time.time;
        while (group != null && Time.time - startedAt < duration)
        {
            float progress = Mathf.Clamp01((Time.time - startedAt) / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            for (int index = 0; index < group.transform.childCount && index < directions.Length; index++)
            {
                Transform plexus = group.transform.GetChild(index);
                plexus.localPosition = directions[index] * distance * eased;
                plexus.localScale = Vector3.one * Mathf.Lerp(0.3f, finalTick ? 1.3f : 0.95f, eased);
            }
            yield return null;
        }
        if (group != null)
            Object.Destroy(group);
    }

    private static Material CreateBlueMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        Color blue = new Color(0.05f, 0.72f, 1f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", blue);
        if (material.HasProperty("_Color")) material.SetColor("_Color", blue);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", blue * 9f);
        }
        return material;
    }

    private static void CreateElectricArcs(Transform parent)
    {
        Material material = AbilityRuntimeMaterialOwner.Track(parent.gameObject,
            new Material(Shader.Find("Sprites/Default")));
        for (int arcIndex = 0; arcIndex < 5; arcIndex++)
        {
            GameObject arcObject = new GameObject("Charge Swirling Electricity " + arcIndex);
            arcObject.transform.SetParent(parent, false);
            LineRenderer line = arcObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 20;
            line.startWidth = line.endWidth = 0.035f;
            line.sharedMaterial = material;
            line.startColor = Color.white;
            line.endColor = new Color(0.05f, 0.5f, 1f);
            float radius = 0.62f + arcIndex * 0.09f;
            for (int point = 0; point < line.positionCount; point++)
            {
                float angle = point / (float)line.positionCount * Mathf.PI * 2f;
                line.SetPosition(point, new Vector3(Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle * 2f + arcIndex) * 0.18f, Mathf.Sin(angle) * radius));
            }
            arcObject.transform.localRotation = Quaternion.Euler(arcIndex * 31f, arcIndex * 47f, 0f);
        }
    }

    private static void PlayClip(AudioClip clip, Vector3 position, float volume)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, position, volume);
    }
}

public sealed class ChargeSwordElectricity : MonoBehaviour
{
    private Transform[] arcs;

    public static void Attach(Transform sword)
    {
        if (sword == null || sword.GetComponent<ChargeSwordElectricity>() != null)
            return;
        sword.gameObject.AddComponent<ChargeSwordElectricity>().Build();
    }

    private void Build()
    {
        arcs = new Transform[6];
        Material material = AbilityRuntimeMaterialOwner.Track(gameObject,
            new Material(Shader.Find("Sprites/Default")));
        Vector3 axis = BullseyeAbility.GetVisualLongAxisLocal(gameObject).normalized;
        Vector3 side = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f
            ? Vector3.right : Vector3.up).normalized;
        Vector3 otherSide = Vector3.Cross(axis, side).normalized;
        for (int index = 0; index < arcs.Length; index++)
        {
            GameObject arc = new GameObject("Blue Sword Electricity Arc " + index);
            arc.transform.SetParent(transform, false);
            LineRenderer line = arc.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 24;
            line.startWidth = line.endWidth = 0.018f;
            line.sharedMaterial = material;
            line.startColor = index % 2 == 0 ? Color.white : new Color(0.05f, 0.45f, 1f);
            line.endColor = new Color(0.15f, 0.8f, 1f);
            float radius = 0.13f + index * 0.014f;
            for (int point = 0; point < line.positionCount; point++)
            {
                float t = point / (float)line.positionCount;
                float angle = t * Mathf.PI * 2f;
                line.SetPosition(point,
                    axis * Mathf.Lerp(-0.46f, 0.46f, t) +
                    side * (Mathf.Cos(angle * 3f + index) * radius) +
                    otherSide * (Mathf.Sin(angle * 3f + index) * radius));
            }
            arcs[index] = arc.transform;
        }
        Light glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.05f, 0.55f, 1f);
        glow.intensity = 7f;
        glow.range = 3.2f;
        glow.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (arcs == null)
            return;
        for (int index = 0; index < arcs.Length; index++)
            arcs[index].Rotate(70f * Time.deltaTime, 0f,
                (180f + index * 31f) * Time.deltaTime, Space.Self);
    }
}
