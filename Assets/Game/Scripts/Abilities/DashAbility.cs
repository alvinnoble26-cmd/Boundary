using UnityEngine;

public class DashAbility : MonoBehaviour, IAbility
{
    public AbilityId Id => AbilityId.Dash;
    public float CooldownDuration => cooldownSeconds;
    public bool IsActive => active;

    [Header("Cooldown")]
    [SerializeField] private float cooldownSeconds = 2.5f;
    private float nextReadyTime;

    [Header("Refs")]
    [SerializeField] private PlayerMovement pm;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform trailSpawnPoint;
    [SerializeField] private Transform visuals;
    [SerializeField] private Transform tiltVisual;

    [Header("Dash")]
    [SerializeField] private float duration = 0.18f;
    [SerializeField] private float dashSpeed = 16f;
    [SerializeField] private float maintainAccel = 70f;
    [SerializeField] private float dashSpeedCap = 18f;
    [SerializeField] private bool flattenY = true;

    [Header("Dash Look")]
    [SerializeField] private float leanForwardAngle = 10f;
    [SerializeField] private float leanLerp = 20f;

    [Header("Trail")]
    [SerializeField] private GameObject trailPrefab;
    [SerializeField] private GameObject greenHitPrefab;
    [SerializeField] private float trailLifetime = 0.4f;

    private bool active;
    private bool observerPresentationActive;
    private float endTime;
    private float presentationStartedAt;
    private float presentationEndsAt;
    private Vector3 dashDir;

    private Quaternion originalTiltLocalRot;
    private GameObject dashFlashRoot;
    private Transform activationRing;
    private Material dashFlashMaterial;

    private static readonly Color DashBlue = new Color(0.12f, 0.62f, 1f, 0.9f);
    private static readonly Color DashWhite = new Color(0.92f, 0.98f, 1f, 0.95f);

    void Awake()
    {
        if (pm == null) pm = GetComponentInParent<PlayerMovement>();

        if (rb == null)
        {
            if (pm != null && pm.rb != null) rb = pm.rb;
            else rb = GetComponentInParent<Rigidbody>();
        }

        if (orientation == null)
        {
            if (pm != null && pm.orientation != null) orientation = pm.orientation;
            else orientation = transform;
        }

        if (trailSpawnPoint == null) trailSpawnPoint = transform;

        if (visuals == null) visuals = transform;
        if (tiltVisual == null) tiltVisual = visuals;

        originalTiltLocalRot = tiltVisual.localRotation;
    }

    public void Activate()
    {
        if (pm == null) pm = GetComponentInParent<PlayerMovement>();
    if (rb == null && pm != null) rb = pm.rb;

    if (Time.time < nextReadyTime) return;
        nextReadyTime = Time.time + PlayerAbilities.GetPhaseAdjustedCooldown(cooldownSeconds);

        if (active) return;

        dashDir = GetActivationDirection();
        if (dashDir.sqrMagnitude < 0.0001f) return;

        active = true;
        observerPresentationActive = false;
        endTime = Time.time + duration;
        presentationStartedAt = Time.time;
        presentationEndsAt = endTime;
        SpawnTrail();
        SpawnGreenHit();
        BeginFlashPresentation();
        SfxManager.PlayDash();
        pm?.GetComponent<PlayerWindPresentation>()?.TriggerAbilityWind(0.92f, duration + 0.18f);

        Vector3 v = rb.linearVelocity;
        Vector3 dashVel = dashDir * dashSpeed;

        if (flattenY)
            rb.linearVelocity = new Vector3(dashVel.x, v.y, dashVel.z);
        else
            rb.linearVelocity = dashVel + Vector3.ProjectOnPlane(v, dashDir) * 0.15f;
    }

    void FixedUpdate()
    {
        if (!active || rb == null) return;

        if (Time.time >= endTime)
        {
            Stop();
            return;
        }

        MaintainDashVelocity();
        ClampPlanarSpeed(dashSpeedCap);
    }

    void Update()
    {
        if (tiltVisual != null)
        {
            Quaternion target = originalTiltLocalRot;

            if (active)
            {
                Quaternion face = Quaternion.LookRotation(dashDir, Vector3.up);
                Quaternion lean = Quaternion.Euler(leanForwardAngle, 0f, 0f);
                Quaternion worldRot = face * lean;

                if (tiltVisual.parent != null)
                    target = Quaternion.Inverse(tiltVisual.parent.rotation) * worldRot;
                else
                    target = worldRot;
            }

            tiltVisual.localRotation = Quaternion.Slerp(tiltVisual.localRotation, target,
                leanLerp * Time.deltaTime);
        }
        if (!active && observerPresentationActive && Time.time >= presentationEndsAt)
        {
            observerPresentationActive = false;
            SpawnEndBurst();
            EndFlashPresentation();
        }

        UpdateFlashPresentation();
    }

    // The owner simulates movement. Other players receive this through the
    // server observer RPC and render the same flash without touching physics.
    public void PlayObserverPresentation(Vector3 direction)
    {
        if (active)
            return;

        direction = flattenY ? Vector3.ProjectOnPlane(direction, Vector3.up) : direction;
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;
        dashDir = direction.normalized;
        observerPresentationActive = true;
        presentationStartedAt = Time.time;
        presentationEndsAt = presentationStartedAt + duration;
        SpawnTrail();
        SpawnGreenHit();
        BeginFlashPresentation();
        SfxManager.PlayDash();
    }

    public Vector3 GetActivationDirection()
    {
        Vector3 direction = orientation != null ? orientation.forward : transform.forward;
        if (flattenY)
            direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    private void MaintainDashVelocity()
    {
        Vector3 v = rb.linearVelocity;

        if (flattenY)
        {
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float along = Vector3.Dot(flat, dashDir);
            float delta = dashSpeed - along;
            if (delta <= 0f) return;

            rb.AddForce(dashDir * (delta * maintainAccel), ForceMode.Acceleration);
        }
        else
        {
            float along = Vector3.Dot(v, dashDir);
            float delta = dashSpeed - along;
            if (delta <= 0f) return;

            rb.AddForce(dashDir * (delta * maintainAccel), ForceMode.Acceleration);
        }
    }

    private void ClampPlanarSpeed(float cap)
    {
        Vector3 v = rb.linearVelocity;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        float m = flat.magnitude;

        if (m > cap)
        {
            Vector3 capped = flat * (cap / m);
            rb.linearVelocity = new Vector3(capped.x, v.y, capped.z);
        }
    }

    private void SpawnTrail()
    {
        if (trailPrefab == null) return;

        GameObject go = Instantiate(trailPrefab, trailSpawnPoint.position, trailSpawnPoint.rotation);
        go.transform.SetParent(trailSpawnPoint, true);
        Destroy(go, trailLifetime);
    }

    private void SpawnGreenHit()
    {
        if (greenHitPrefab == null)
            return;

        GameObject effect = Instantiate(greenHitPrefab,
            trailSpawnPoint.position + Vector3.up * 0.8f, Quaternion.identity, trailSpawnPoint);
        effect.name = "Dash Green Hit";
        effect.transform.localScale = Vector3.one * 0.65f;
        Destroy(effect, Mathf.Max(duration, trailLifetime));
    }

    private void Stop()
    {
        active = false;
        observerPresentationActive = false;
        SpawnEndBurst();
        EndFlashPresentation();
        if (pm != null) pm.SetMovementSuppressed(false, -1f);
    }

    private void OnDisable()
    {
        observerPresentationActive = false;
        EndFlashPresentation();
    }

    // Explicit URP material setup prevents imported legacy VFX shaders from
    // falling back to Unity's magenta error material on mobile builds.
    private void BeginFlashPresentation()
    {
        EndFlashPresentation();
        dashFlashMaterial = CreateDashFlashMaterial();
        if (dashFlashMaterial == null)
            return;

        GameObject ringRoot = new GameObject("Dash Activation Ring");
        ringRoot.transform.position = rb != null ? rb.position + Vector3.up * 0.04f : transform.position;
        activationRing = ringRoot.transform;
        CreateRing(activationRing, 0.55f, 0.055f, DashBlue, 32);
        CreateRing(activationRing, 0.31f, 0.035f, DashWhite, 24);

        dashFlashRoot = new GameObject("Dash Speed Trail");
        dashFlashRoot.transform.SetParent(trailSpawnPoint, false);
        dashFlashRoot.transform.localPosition = Vector3.zero;
        dashFlashRoot.transform.rotation = Quaternion.LookRotation(dashDir, Vector3.up);
        CreateSpeedTrail(dashFlashRoot);
        CreateDirectionalStreaks(dashFlashRoot.transform);
    }

    private void UpdateFlashPresentation()
    {
        if (!active && !observerPresentationActive)
            return;

        float progress = Mathf.InverseLerp(presentationStartedAt, presentationEndsAt, Time.time);
        if (activationRing != null)
        {
            float scale = Mathf.Lerp(1f, 3.1f, progress);
            activationRing.localScale = new Vector3(scale, 1f, scale);
        }

        if (dashFlashRoot != null)
            dashFlashRoot.transform.rotation = Quaternion.LookRotation(dashDir, Vector3.up);
    }

    private void CreateSpeedTrail(GameObject parent)
    {
        TrailRenderer trail = parent.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.minVertexDistance = 0.06f;
        trail.widthMultiplier = 0.32f;
        trail.material = dashFlashMaterial;
        trail.startColor = DashWhite;
        trail.endColor = new Color(DashBlue.r, DashBlue.g, DashBlue.b, 0f);
    }

    private void CreateDirectionalStreaks(Transform parent)
    {
        for (int index = 0; index < 7; index++)
        {
            float lateral = (index - 3) * 0.14f;
            float height = 0.3f + (index % 3) * 0.25f;
            LineRenderer streak = CreateLine(parent, "Dash Streak", 2, 0.035f,
                index % 2 == 0 ? DashWhite : DashBlue);
            streak.SetPosition(0, new Vector3(lateral, height, 0.45f));
            streak.SetPosition(1, new Vector3(lateral * 1.8f, height, -1.65f - index * 0.1f));
        }
    }

    private void CreateRing(Transform parent, float radius, float width, Color color, int points)
    {
        LineRenderer ring = CreateLine(parent, "Dash Ring", points + 1, width, color);
        for (int index = 0; index <= points; index++)
        {
            float angle = index / (float)points * Mathf.PI * 2f;
            ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, 0f,
                Mathf.Sin(angle) * radius));
        }
    }

    private LineRenderer CreateLine(Transform parent, string effectName, int count, float width,
        Color color)
    {
        GameObject lineObject = new GameObject(effectName, typeof(LineRenderer));
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = count;
        line.widthMultiplier = width;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.material = dashFlashMaterial;
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0f);
        return line;
    }

    private void SpawnEndBurst()
    {
        Material material = CreateDashFlashMaterial();
        if (material == null)
            return;

        GameObject burstObject = new GameObject("Dash End Burst", typeof(ParticleSystem));
        burstObject.transform.position = rb != null ? rb.position + Vector3.up * 0.75f : transform.position;
        ParticleSystem particles = burstObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 0.32f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(DashWhite, DashBlue);
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 34f;
        shape.rotation = new Vector3(0f, 180f, 0f);
        ParticleSystemRenderer renderer = burstObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        particles.Play();
        Destroy(burstObject, 0.8f);
        Destroy(material, 0.8f);
    }

    private void EndFlashPresentation()
    {
        if (activationRing != null)
            Destroy(activationRing.gameObject, 0.12f);
        activationRing = null;
        if (dashFlashRoot != null)
            Destroy(dashFlashRoot, trailLifetime);
        dashFlashRoot = null;
        if (dashFlashMaterial != null)
            Destroy(dashFlashMaterial, trailLifetime);
        dashFlashMaterial = null;
    }

    private static Material CreateDashFlashMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            return null;

        Material material = new Material(shader) { name = "DashFlashURP" };
        material.SetColor("_BaseColor", Color.white);
        return material;
    }
}
