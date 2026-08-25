using UnityEngine;

public class SlideAbility : MonoBehaviour, IAbility
{
    public const float SpeedMultiplier = 1f;
    public const float SlideJumpImpulseMultiplier = 2f;
    public const float SlideJumpHeightMultiplier =
        SlideJumpImpulseMultiplier * SlideJumpImpulseMultiplier;

    public AbilityId Id => AbilityId.Slide;
    public float CooldownDuration => cooldownSeconds;
    public bool IsActive => active;

    [Header("Cooldown")]
    [SerializeField] private float cooldownSeconds = 2.5f;
    private float nextReadyTime;

    [Header("Refs")]
    [SerializeField] private PlayerMovement pm;
    [SerializeField] private Transform visuals;
    [SerializeField] private Transform tiltVisual;

    [Header("Slide Settings")]
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float slideSpeedCap = 12f;
    [SerializeField] private float slideStartSpeed = 10f;
    [SerializeField] private float maintainAccel = 60f;
    [SerializeField] private float crouchYScale = 0.7f;

    [Header("Slide Look")]
    [SerializeField] private float backLeanAngle = 12f;
    [SerializeField] private float leanLerp = 18f;

    [Header("Trail")]
    [SerializeField] private GameObject trailPrefab;
    [SerializeField] private GameObject holyHitPrefab;
    [SerializeField] private Transform trailSpawnPoint;
    [SerializeField] private float trailLifetime = 0.6f;

    private Rigidbody rb;
    private PlayerAbilities playerAbilities;
    private Vector3 originalVisualScale;
    private Quaternion originalTiltLocalRot;
    private bool active;
    private bool observerPresentationActive;
    private float endTime;
    private float presentationEndsAt;
    private Vector3 dir;
    private bool wallSliding;
    private bool slideJumpExecuted;
    private Vector3 lastWallNormal;
    private GameObject slideFlashRoot;
    private ParticleSystem groundSparks;
    private Material slideFlashMaterial;

    private static readonly Color SlideOrange = new Color(1f, 0.31f, 0.04f, 0.9f);
    private static readonly Color SlideTeal = new Color(0.03f, 0.92f, 0.72f, 0.82f);

    void Awake()
    {
        if (pm == null) pm = GetComponentInParent<PlayerMovement>();
        rb = pm ? pm.rb : null;
        playerAbilities = GetComponentInParent<PlayerAbilities>();

        if (visuals == null) visuals = transform;
        if (tiltVisual == null) tiltVisual = visuals;

        originalVisualScale = visuals.localScale;
        originalTiltLocalRot = tiltVisual.localRotation;

        if (trailSpawnPoint == null) trailSpawnPoint = transform;
    }

    public void Activate()
    {
        if (!CooldownReady()) return;

        if (!TryGetActivationSupport(out Vector3 activationDirection, out RaycastHit wallHit,
                out bool hasWallSupport))
            return;

        dir = activationDirection;

        active = true;
        observerPresentationActive = false;
        wallSliding = hasWallSupport;
        slideJumpExecuted = false;
        if (hasWallSupport)
        {
            lastWallNormal = wallHit.normal;
            dir = SelectHorizontalWallTangent(dir, wallHit.normal);
        }
        endTime = Time.time + duration;
        presentationEndsAt = endTime;

        pm.SetMovementSuppressed(true, IncreasedSlideSpeed(slideSpeedCap), false);

        if (visuals != null)
            visuals.localScale = new Vector3(originalVisualScale.x, crouchYScale, originalVisualScale.z);

        SpawnTrail();
        SpawnHolyHit();
        BeginFlashPresentation();
        ApplyInstantSlideKick();
        StartCooldown();
        SfxManager.PlaySlide();
    }

    // Called before the UI cooldown starts so an unsupported airborne press is free.
    public bool CanActivate()
    {
        return !active && CooldownReady() &&
               TryGetActivationSupport(out _, out _, out _);
    }

    public bool TryGetActivationDirection(out Vector3 activationDirection)
    {
        if (!TryGetActivationSupport(out activationDirection, out RaycastHit wallHit,
                out bool hasWallSupport))
            return false;
        if (hasWallSupport)
            activationDirection = SelectHorizontalWallTangent(activationDirection, wallHit.normal);
        return activationDirection.sqrMagnitude > 0.0001f;
    }

    public static bool HasValidActivationSupport(bool hasFloorSupport, bool hasWallSupport)
    {
        return hasFloorSupport || hasWallSupport;
    }

    private bool TryGetActivationSupport(out Vector3 activationDirection, out RaycastHit wallHit,
        out bool hasWallSupport)
    {
        if (pm == null) pm = GetComponentInParent<PlayerMovement>();
        rb = pm ? pm.rb : null;

        activationDirection = Vector3.forward;
        wallHit = default;
        hasWallSupport = false;
        if (pm == null || rb == null)
            return false;

        Transform basis = pm.orientation ? pm.orientation : pm.transform;
        Vector2 input = pm.moveInput;
        activationDirection = basis.forward * input.y + basis.right * input.x;
        activationDirection.y = 0f;
        if (activationDirection.sqrMagnitude > 0.001f)
            activationDirection.Normalize();
        else
        {
            activationDirection = pm.transform.forward;
            activationDirection.y = 0f;
            if (activationDirection.sqrMagnitude < 0.0001f)
                activationDirection = Vector3.forward;
            activationDirection.Normalize();
        }

        bool hasFloorSupport = pm.IsGrounded;
        hasWallSupport = !hasFloorSupport && pm.TryFindSlideWall(activationDirection, out wallHit);
        return HasValidActivationSupport(hasFloorSupport, hasWallSupport);
    }

    void FixedUpdate()
    {
        if (!active || pm == null || rb == null) return;

        if (Time.time >= endTime) { Stop(); return; }

        if (pm.IsGrounded)
        {
            wallSliding = false;
        }
        else if (pm.TryFindSlideWall(dir, out RaycastHit wallHit))
        {
            wallSliding = true;
            lastWallNormal = wallHit.normal;
            dir = SelectHorizontalWallTangent(dir, wallHit.normal);
        }
        else
        {
            // A one-tick ground miss is common while crossing inclines and
            // bumps. Keep the slide alive and let gravity restore contact.
            wallSliding = false;
        }

        MaintainSlideSpeed();
        if (!wallSliding && pm.IsGrounded)
            rb.AddForce(Vector3.down * 10f, ForceMode.Acceleration);
    }

    public bool TryManualSlideJump()
    {
        if (!active || slideJumpExecuted || rb == null || pm == null)
            return false;

        ExecuteSlideJump(false);
        return true;
    }

    public static Vector3 SelectHorizontalWallTangent(Vector3 incomingDirection, Vector3 wallNormal)
    {
        Vector3 flatNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        Vector3 flatIncoming = Vector3.ProjectOnPlane(incomingDirection, Vector3.up);
        if (flatNormal.sqrMagnitude < 0.0001f || flatIncoming.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        Vector3 tangent = Vector3.ProjectOnPlane(flatIncoming, flatNormal.normalized);
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.Cross(Vector3.up, flatNormal);
        tangent.Normalize();
        return Vector3.Dot(tangent, flatIncoming) < 0f ? -tangent : tangent;
    }

    public static float SlideJumpUpwardImpulse(float normalJumpForce)
    {
        return Mathf.Max(0f, normalJumpForce) * SlideJumpImpulseMultiplier;
    }

    public static Vector3 CalculateSlideJumpVelocity(Vector3 incomingVelocity,
        float normalJumpForce, Vector3 outwardVelocity)
    {
        Vector3 horizontal = Vector3.ProjectOnPlane(incomingVelocity, Vector3.up) +
            Vector3.ProjectOnPlane(outwardVelocity, Vector3.up);
        return horizontal + Vector3.up * SlideJumpUpwardImpulse(normalJumpForce);
    }

    public static float IncreasedSlideSpeed(float configuredSpeed)
    {
        return Mathf.Max(0f, configuredSpeed) * SpeedMultiplier;
    }

    public static bool ShouldEndSlideAfterJump(bool automatic)
    {
        return true;
    }

    public static bool ShouldEndSlideAfterSupportLoss()
    {
        return false;
    }

    void Update()
    {
        if (tiltVisual != null)
        {
            Quaternion target = originalTiltLocalRot;

            if (active)
            {
                Quaternion face = Quaternion.LookRotation(dir, Vector3.up);
                Quaternion leanBack = Quaternion.Euler(-backLeanAngle, 0f, 0f);
                target = face * leanBack;

                if (tiltVisual.parent != null)
                    target = Quaternion.Inverse(tiltVisual.parent.rotation) * target;
            }

            tiltVisual.localRotation = Quaternion.Slerp(
                tiltVisual.localRotation, target, leanLerp * Time.deltaTime);
        }

        if (!active && observerPresentationActive && Time.time >= presentationEndsAt)
        {
            observerPresentationActive = false;
            EndFlashPresentation();
        }
        UpdateFlashPresentation();
    }

    // Remote peers render this server-relayed presentation only. They do not
    // simulate the owning player's slide forces or collision checks.
    public void PlayObserverPresentation(Vector3 direction)
    {
        if (active)
            return;

        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;
        dir = direction.normalized;
        observerPresentationActive = true;
        presentationEndsAt = Time.time + duration;
        SpawnTrail();
        SpawnHolyHit();
        BeginFlashPresentation();
        SfxManager.PlaySlide();
    }

    public void PlayObserverJumpBurst(Vector3 position)
    {
        SpawnSlideJumpBurst(position);
    }

    private void ApplyInstantSlideKick()
    {
        Vector3 v = rb.linearVelocity;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        float speedCap = IncreasedSlideSpeed(slideSpeedCap);
        float target = Mathf.Clamp(IncreasedSlideSpeed(slideStartSpeed), 0f, speedCap);
        Vector3 desiredFlat = dir * target;
        float along = Vector3.Dot(flat, dir);
        if (along > target) desiredFlat = dir * Mathf.Min(along, speedCap);
        rb.linearVelocity = new Vector3(desiredFlat.x, v.y, desiredFlat.z);
    }

    private void MaintainSlideSpeed()
    {
        Vector3 v = rb.linearVelocity;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        float along = Vector3.Dot(flat, dir);
        float target = IncreasedSlideSpeed(slideSpeedCap);
        float delta = target - along;

        if (delta <= 0f) { ClampPlanarSpeed(target); return; }

        rb.AddForce(dir * (delta * maintainAccel), ForceMode.Acceleration);
        ClampPlanarSpeed(target);
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

    public static Vector3 ResolveObstacleCollisionVelocity(Vector3 currentVelocity,
        Vector3 slideDirection, Vector3 collisionNormal, float speedCap)
    {
        Vector3 flatNormal = Vector3.ProjectOnPlane(collisionNormal, Vector3.up);
        Vector3 flatDirection = Vector3.ProjectOnPlane(slideDirection, Vector3.up);
        if (flatNormal.sqrMagnitude < 0.0001f || flatDirection.sqrMagnitude < 0.0001f)
            return currentVelocity;

        flatNormal.Normalize();
        flatDirection.Normalize();
        if (Vector3.Dot(flatDirection, flatNormal) >= -0.05f)
            return currentVelocity;

        Vector3 tangent = Vector3.ProjectOnPlane(flatDirection, flatNormal);
        float speed = Mathf.Min(Vector3.ProjectOnPlane(currentVelocity, Vector3.up).magnitude,
            Mathf.Max(0f, speedCap));
        Vector3 stableHorizontal = tangent.sqrMagnitude > 0.0001f
            ? tangent.normalized * speed
            : Vector3.zero;
        return stableHorizontal + Vector3.up * currentVelocity.y;
    }

    public bool HandleObstacleCollision(Collision collision)
    {
        if (!active || rb == null || collision == null)
            return false;

        foreach (ContactPoint contact in collision.contacts)
        {
            Vector3 flatNormal = Vector3.ProjectOnPlane(contact.normal, Vector3.up);
            if (flatNormal.sqrMagnitude < 0.0001f || Vector3.Dot(dir, flatNormal.normalized) >= -0.05f)
                continue;

            rb.linearVelocity = ResolveObstacleCollisionVelocity(
                rb.linearVelocity, dir, contact.normal, IncreasedSlideSpeed(slideSpeedCap));
            Vector3 tangent = Vector3.ProjectOnPlane(dir, flatNormal.normalized);
            if (tangent.sqrMagnitude > 0.0001f)
                dir = tangent.normalized;
            return true;
        }

        return false;
    }

    private void ExecuteSlideJump(bool automatic)
    {
        if (slideJumpExecuted || rb == null || pm == null)
            return;

        slideJumpExecuted = true;
        Vector3 outwardImpulse = Vector3.zero;
        if (wallSliding)
        {
            Vector3 flatNormal = Vector3.ProjectOnPlane(lastWallNormal, Vector3.up);
            if (flatNormal.sqrMagnitude > 0.0001f)
                outwardImpulse = flatNormal.normalized * pm.jumpForce;
        }

        Vector3 launchVelocity = CalculateSlideJumpVelocity(
            rb.linearVelocity, pm.jumpForce, outwardImpulse);
        Vector3 burstPosition = rb.position + Vector3.up * 0.14f;
        SpawnSlideJumpBurst(burstPosition);
        playerAbilities?.NotifySlideJumpPresentation(burstPosition);
        Stop(true);
        rb.useGravity = true;
        pm.AllowSlideJumpVerticalSpeed(launchVelocity.y);
        pm.MarkExitingSlope();
        rb.linearVelocity = launchVelocity;
        SfxManager.PlayJump();
    }

    private void SpawnTrail()
    {
        if (trailPrefab == null) return;
        // Keep the emitter on the sliding player. Particle systems using world
        // simulation leave their particles behind and create a visible trail.
        GameObject go = Instantiate(trailPrefab, trailSpawnPoint);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        Destroy(go, trailLifetime);
    }

    private void SpawnHolyHit()
    {
        if (holyHitPrefab == null)
            return;

        GameObject effect = Instantiate(holyHitPrefab,
            trailSpawnPoint.position + Vector3.up * 0.45f, Quaternion.identity, trailSpawnPoint);
        effect.name = "Slide Holy Hit";
        effect.transform.localScale = Vector3.one * 0.65f;
        Destroy(effect, Mathf.Max(duration, trailLifetime));
    }

    private void Stop(bool preserveMomentum = false)
    {
        active = false;
        observerPresentationActive = false;
        EndFlashPresentation();
        wallSliding = false;
        lastWallNormal = Vector3.zero;
        if (pm != null)
        {
            if (preserveMomentum)
                pm.ReleaseMovementSuppressionPreservingMomentum();
            else
                pm.SetMovementSuppressed(false, -1f);
        }
        if (visuals != null) visuals.localScale = originalVisualScale;
    }

    private void OnDisable()
    {
        observerPresentationActive = false;
        if (active)
            Stop();
        else
            EndFlashPresentation();
    }

    // Use a verified URP particle material for every generated renderer rather
    // than inheriting an imported legacy material that could turn magenta.
    private void BeginFlashPresentation()
    {
        EndFlashPresentation();
        slideFlashMaterial = CreateSlideFlashMaterial();
        if (slideFlashMaterial == null)
            return;

        slideFlashRoot = new GameObject("Slide Ground Flash");
        slideFlashRoot.transform.SetParent(trailSpawnPoint, false);
        slideFlashRoot.transform.localPosition = Vector3.zero;
        slideFlashRoot.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        CreateDustRibbon(slideFlashRoot);
        groundSparks = CreateGroundSparks(slideFlashRoot.transform);
    }

    private void UpdateFlashPresentation()
    {
        if ((!active && !observerPresentationActive) || slideFlashRoot == null)
            return;

        slideFlashRoot.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        if (groundSparks != null)
        {
            ParticleSystem.EmissionModule emission = groundSparks.emission;
            bool showSparks = observerPresentationActive ||
                (!wallSliding && pm != null && pm.IsGrounded);
            emission.rateOverTime = showSparks ? 58f : 0f;
        }
    }

    private void CreateDustRibbon(GameObject parent)
    {
        TrailRenderer ribbon = parent.AddComponent<TrailRenderer>();
        ribbon.time = 0.28f;
        ribbon.minVertexDistance = 0.05f;
        ribbon.widthMultiplier = 0.48f;
        ribbon.material = slideFlashMaterial;
        ribbon.startColor = new Color(SlideTeal.r, SlideTeal.g, SlideTeal.b, 0.48f);
        ribbon.endColor = new Color(SlideOrange.r, SlideOrange.g, SlideOrange.b, 0f);
    }

    private ParticleSystem CreateGroundSparks(Transform parent)
    {
        GameObject sparksObject = new GameObject("Slide Sparks", typeof(ParticleSystem));
        sparksObject.transform.SetParent(parent, false);
        sparksObject.transform.localPosition = new Vector3(0f, 0.05f, -0.38f);
        ParticleSystem sparks = sparksObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = sparks.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(SlideOrange, SlideTeal);
        main.maxParticles = 48;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = sparks.emission;
        emission.rateOverTime = 58f;
        ParticleSystem.ShapeModule shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 19f;
        ParticleSystemRenderer renderer = sparksObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = slideFlashMaterial;
        sparks.Play();
        return sparks;
    }

    private void SpawnSlideJumpBurst()
    {
        SpawnSlideJumpBurst(rb != null ? rb.position + Vector3.up * 0.14f : transform.position);
    }

    private void SpawnSlideJumpBurst(Vector3 position)
    {
        Material material = CreateSlideFlashMaterial();
        if (material == null)
            return;

        GameObject burstObject = new GameObject("Slide Jump Burst", typeof(ParticleSystem));
        burstObject.transform.position = position;
        ParticleSystem particles = burstObject.GetComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.38f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
        main.startColor = new ParticleSystem.MinMaxGradient(SlideOrange, SlideTeal);
        main.maxParticles = 56;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 42) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.18f;
        ParticleSystemRenderer renderer = burstObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        particles.Play();
        Destroy(burstObject, 0.9f);
        Destroy(material, 0.9f);
    }

    private void EndFlashPresentation()
    {
        if (slideFlashRoot != null)
            Destroy(slideFlashRoot, 0.3f);
        slideFlashRoot = null;
        groundSparks = null;
        if (slideFlashMaterial != null)
            Destroy(slideFlashMaterial, 0.3f);
        slideFlashMaterial = null;
    }

    private static Material CreateSlideFlashMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            return null;

        Material material = new Material(shader) { name = "SlideFlashURP" };
        material.SetColor("_BaseColor", Color.white);
        return material;
    }

    protected bool CooldownReady() => Time.time >= nextReadyTime;
    protected void StartCooldown() =>
        nextReadyTime = Time.time + PlayerAbilities.GetPhaseAdjustedCooldown(cooldownSeconds);
}
