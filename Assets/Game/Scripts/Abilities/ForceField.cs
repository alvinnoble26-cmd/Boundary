using PurrNet;
using UnityEngine;
using UnityEngine.Serialization;

public class  ForceField : MonoBehaviour
{
    public enum Mode { Attract, Repel }

    public const float WindVisualRadius = 60f;
    public const float VisualBrightnessMultiplier = 9f;
    public const int MiniStarPointCount = 5;
    public const float AttractFlashRadius = 29.4f;
    public const float AttractFlashPeakIntensity = 5670f;
    public const float AttractFlashDuration = 2.1f;
    public const float RepelFlashRadius = 29.4f;
    public const float RepelFlashPeakIntensity = 5670f;
    public const float RepelFlashDuration = 2.1f;
    private const int WindParticleCapacity = 128;
    private const int WindRibbonCount = 8;

    [Header("Mode")]
    [SerializeField] private Mode mode = Mode.Repel;

    [Header("Timing")]
    [SerializeField] private float delayBeforePulse = 1.5f;
    [SerializeField] private float destroyAfterPulse = 0.2f;   

    [Header("Force")]
    [SerializeField] private float radius = 6f;
    [FormerlySerializedAs("strength")]
    [SerializeField, Min(0f), Tooltip("Raw attraction or repulsion force. Rigidbody mass reduces its effect.")]
    private float fieldForce = 220f;
    [FormerlySerializedAs("maxAccel")]
    [SerializeField, Min(0f), Tooltip("Maximum velocity response produced by one field pulse.")]
    private float fieldAcceleration = 88f;
    [Tooltip("Extra force applied only to player rigidbodies. Physics props remain unchanged.")]
    [SerializeField] private float playerForceMultiplier = 1.5f;
    [SerializeField] private AnimationCurve falloff = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [SerializeField] private bool affectOwner = true;

    private bool thrownByLocalPlayer;
    [SerializeField] private Rigidbody ownerRb;

    [Header("Filtering")]
    [SerializeField] private LayerMask affectMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("VFX")]
    [SerializeField] private ParticleSystem pulseVFXPrefab;   // burst prefab
    [SerializeField] private float vfxLifetime = 2f;

    [Header("Performance")]
    [SerializeField] private int maxHits = 64;

    private Collider[] hits;
    private float timer;
    private bool pulsed;
    private bool buildPresentation;
    private Material attractVisualMaterial;
    private LineRenderer[] attractSpirals;
    private ParticleSystem attractSpiralParticles;
    private float nextSpiralParticleTime;
    private Material repelVisualMaterial;
    private ParticleSystem repelOrbitSparks;
    private float nextRepelSparkTime;
    private ParticleSystem fieldWindParticles;
    private readonly ParticleSystem.Particle[] fieldWindParticleBuffer =
        new ParticleSystem.Particle[WindParticleCapacity];
    private LineRenderer[] fieldWindRibbons;
    private float nextFieldWindParticleTime;
    private int fieldWindSequence;

    // Repel is a caster-centered burst. The server follows the authoritative
    // caster; observers receive that position through NetworkTransform.
    private bool casterCentered;
    private Transform caster;
    private Vector3 casterOffset;

    private static readonly Color AttractBlue = Brighten(new Color(0.08f, 0.48f, 1f, 0.95f));
    private static readonly Color AttractPurple = Brighten(new Color(0.24f, 0.015f, 0.42f, 0.9f));
    private static readonly Color AttractLilac = Brighten(new Color(0.64f, 0.26f, 1f, 0.9f));
    private static readonly Color RepelRed = Brighten(new Color(1f, 0.06f, 0.05f, 0.96f));
    private static readonly Color RepelOrange = Brighten(new Color(1f, 0.38f, 0.04f, 0.92f));
    private static readonly Color RepelWhite = Brighten(new Color(1f, 0.92f, 0.7f, 1f));

    private static Color Brighten(Color color)
    {
        return new Color(color.r * VisualBrightnessMultiplier,
            color.g * VisualBrightnessMultiplier,
            color.b * VisualBrightnessMultiplier,
            color.a);
    }

    void Awake()
    {
        hits = new Collider[Mathf.Max(8, maxHits)];

        if (mode == Mode.Repel)
            PrepareCasterCenteredCarrier();

        buildPresentation = !Application.isBatchMode &&
            SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
        if (!buildPresentation)
            return;

        // This component is part of the networked projectile on every client.
        // Playing here gives both players throw feedback without adding another
        // RPC to PlayerAbilities (which would require a matching server build).
        if (mode == Mode.Repel)
        {
            SfxManager.PlayRepelThrow(transform.position);
            CreateRepelProjectileVisual();
        }
        else
        {
            SfxManager.PlayAttractThrow(transform.position);
            CreateAttractProjectileVisual();
        }

        thrownByLocalPlayer = AbilityFeedback.NoteWorldSource(FeedbackAbilityId, transform,
            radius, delayBeforePulse + destroyAfterPulse + 0.5f);
    }

    void Start()
    {
        if (casterCentered)
            PrepareCasterCenteredCarrier();
    }

    void LateUpdate()
    {
        FollowCaster();
    }

    /// <summary>
    /// Server-side: binds the field to the authoritative player who cast it.
    /// </summary>
    public void AttachToCaster(Transform casterRoot, Rigidbody casterBody)
    {
        caster = casterRoot;
        ownerRb = casterBody;
        affectOwner = false;
        if (caster != null)
            casterOffset = transform.position - caster.position;
    }

    private void PrepareCasterCenteredCarrier()
    {
        casterCentered = true;
        affectOwner = false;

        // Hide the old ball and remove its physical presence.
        foreach (Renderer ballRenderer in GetComponentsInChildren<Renderer>(true))
        {
            if (ballRenderer is MeshRenderer || ballRenderer is SkinnedMeshRenderer)
                ballRenderer.enabled = false;
        }
        foreach (Collider ballCollider in GetComponentsInChildren<Collider>(true))
            ballCollider.enabled = false;

        NetworkProjectilePhysics projectilePhysics = GetComponent<NetworkProjectilePhysics>();
        if (projectilePhysics != null)
            projectilePhysics.enabled = false;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.useGravity = false;
            body.isKinematic = true;
        }
    }

    private void FollowCaster()
    {
        if (!casterCentered || caster == null)
            return;

        transform.position = caster.position + casterOffset;
    }

    private bool IsCasterBody(Rigidbody rb)
    {
        if (caster == null || rb == null)
            return false;
        return rb.transform == caster || rb.transform.IsChildOf(caster);
    }

    void Update()
    {
        UpdateFieldWindVisual();
        if (mode == Mode.Attract)
            UpdateAttractSpirals();
        else if (!pulsed)
            UpdateRepelCoreVisual();

        if (pulsed) return;

        // Keep the pulse centered on the caster even if this runs before LateUpdate.
        FollowCaster();
        timer += Time.deltaTime;
        if (timer >= delayBeforePulse)
        {
            PulseOnce();
        }
    }

    private AbilityId FeedbackAbilityId =>
        mode == Mode.Repel ? AbilityId.RepelThrow : AbilityId.AttractThrow;

    /// <summary>
    /// The pushed player is told by the existing knockback TargetRpc, but that
    /// message never reaches the thrower. This pulse exists on every client at
    /// the same moment, so the thrower can confirm its own hit locally.
    /// </summary>
    private void ReportPulseHitToThrower()
    {
        if (!thrownByLocalPlayer)
            return;

        BoundaryPlayerState[] players =
            FindObjectsByType<BoundaryPlayerState>(FindObjectsSortMode.None);
        for (int index = 0; index < players.Length; index++)
        {
            BoundaryPlayerState player = players[index];
            // Catching yourself in your own field is not a hit worth confirming.
            if (player == null || player.isOwner)
                continue;
            if (Vector3.Distance(player.transform.position, transform.position) > radius)
                continue;

            AbilityFeedback.ReportDealtByWorldSource(FeedbackAbilityId, player.transform.position);
            return;
        }
    }

    private void PulseOnce()
    {
        pulsed = true;

        if (buildPresentation)
        {
            // The networked projectile exists on every client, so playing the pulse
            // here makes the explosion audible to both players at the correct time.
            if (mode == Mode.Repel)
                SfxManager.PlayRepelExplosion(transform.position);
            else
                SfxManager.PlayAttractExplosion(transform.position);

            if (pulseVFXPrefab != null)
            {
                ParticleSystem ps = Instantiate(pulseVFXPrefab, transform.position, Quaternion.identity);
                ps.Play();
                Destroy(ps.gameObject, vfxLifetime);
            }

            if (mode == Mode.Attract)
                SpawnAttractCollapsePulse(transform.position);
            else
                SpawnRepelShockwavePulse(transform.position);

            ReportPulseHitToThrower();
        }

        Vector3 center = transform.position;

        // The projectile and its VFX exist on every peer, but physics must
        // have one source of truth. Clients receive rigidbody movement through
        // each affected object's server-authoritative NetworkTransform.
        NetworkManager net = NetworkManager.main;
        if (net == null || !net.isServer)
        {
            Destroy(gameObject, destroyAfterPulse);
            return;
        }

        // Arena masses have a dedicated authoritative registry. A 220-unit
        // overlap contains hundreds of floor colliders and can saturate the
        // fixed query buffer before returning a single cube or black hole.
        // Applying this pass directly guarantees every mass in range is hit.
        BoundaryHazard.ServerApplyArenaMassField(
            center,
            radius,
            mode == Mode.Repel,
            fieldForce,
            fieldAcceleration,
            falloff);

        int count = Physics.OverlapSphereNonAlloc(center, radius, hits, affectMask, triggerInteraction);

        for (int i = 0; i < count; i++)
        {
            Collider c = hits[i];
            if (!c) continue;

            Rigidbody rb = c.attachedRigidbody;
            if (!rb) continue;

            BoundaryHazard registeredHazard = rb.GetComponent<BoundaryHazard>();
            if (registeredHazard != null && registeredHazard.IsArenaMass)
                continue;

            if (!affectOwner && ownerRb != null && rb == ownerRb)
                continue;

            // The caster is the source of Repel and is never pushed by it.
            if (casterCentered && IsCasterBody(rb))
                continue;

            if (rb.transform == transform || rb.transform.IsChildOf(transform))
                continue;

            Vector3 toBody = rb.position - center;
            float dist = toBody.magnitude;
            if (dist < 0.01f || dist > radius) continue;

            Vector3 dirOut = toBody / dist;        
            Vector3 dir = (mode == Mode.Repel) ? dirOut : -dirOut;

            float t = Mathf.Clamp01(dist / radius); 
            float f = Mathf.Clamp01(falloff.Evaluate(t));
            float velocityChange = BoundaryMath.FieldVelocityChange(
                fieldForce,
                fieldAcceleration,
                f,
                rb.mass);

            PlayerMovement player = rb.GetComponentInParent<PlayerMovement>();
            if (player != null)
            {
                velocityChange = Mathf.Min(
                    fieldAcceleration,
                    velocityChange * playerForceMultiplier);

                BoundaryPlayerState boundaryState = player.GetComponent<BoundaryPlayerState>();
                BoundaryMatchController match = BoundaryMatchController.Instance;
                if (boundaryState != null && boundaryState.State != BoundaryKnockoutState.Grounded &&
                    match != null && match.Phase == BoundaryPhase.InnerRing)
                {
                    // Airborne targets have less stability in the vortex, so
                    // Repel becomes the intended final-phase knockout tool.
                    velocityChange = Mathf.Min(
                        fieldAcceleration,
                        velocityChange * (mode == Mode.Repel ? 1.42f : 1.12f));
                }

                if (boundaryState != null)
                {
                    // Player rigidbodies are owner-authoritative and kinematic
                    // on the server. Send the velocity change to that owner;
                    // applying AddForce here would silently do nothing.
                    boundaryState.ServerPushOwner(dir * velocityChange);
                    continue;
                }
            }

            rb.AddForce(dir * velocityChange, ForceMode.VelocityChange);
        }

        Destroy(gameObject, destroyAfterPulse);
    }

    public void SetOwner(Rigidbody owner) => ownerRb = owner;

    public void ConfigureField(float force, float acceleration)
    {
        fieldForce = Mathf.Max(0f, force);
        fieldAcceleration = Mathf.Max(0f, acceleration);
    }

    public void ConfigureRepel(float force, float acceleration, float effectRadius,
        float buildUpSeconds)
    {
        ConfigureField(force, acceleration);
        radius = Mathf.Max(0f, effectRadius);
        delayBeforePulse = Mathf.Max(0f, buildUpSeconds);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = (mode == Mode.Attract) ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }

    private void OnDestroy()
    {
        if (attractVisualMaterial != null)
            Destroy(attractVisualMaterial);
        if (repelVisualMaterial != null)
            Destroy(repelVisualMaterial);
    }

    // The projectile is network-spawned to each client. Building this cosmetic
    // layer locally gives every observer the same feedback without a VFX RPC.
    private void CreateAttractProjectileVisual()
    {
        attractVisualMaterial = CreateMobileSafeVfxMaterial();
        if (attractVisualMaterial == null)
            return;
        CreateFieldWindVisual(attractVisualMaterial);

        GameObject coreObject = new GameObject("Attract Blue Core", typeof(ParticleSystem));
        coreObject.transform.SetParent(transform, false);
        ParticleSystem core = coreObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule coreMain = core.main;
        coreMain.loop = false;
        coreMain.startLifetime = 8f;
        coreMain.startSpeed = 0f;
        coreMain.startSize = 0.52f;
        coreMain.startColor = AttractBlue;
        coreMain.maxParticles = 2;
        coreMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule coreEmission = core.emission;
        coreEmission.rateOverTime = 0f;
        coreEmission.SetBurst(0, new ParticleSystem.Burst(0f, 1));
        core.GetComponent<ParticleSystemRenderer>().sharedMaterial = attractVisualMaterial;
        core.Play();

        attractSpirals = new LineRenderer[3];
        for (int index = 0; index < attractSpirals.Length; index++)
        {
            GameObject spiralObject = new GameObject("Attract Inward Spiral", typeof(LineRenderer));
            spiralObject.transform.SetParent(transform, false);
            LineRenderer spiral = spiralObject.GetComponent<LineRenderer>();
            spiral.useWorldSpace = false;
            spiral.positionCount = 22;
            spiral.widthMultiplier = 0.038f;
            spiral.numCornerVertices = 2;
            spiral.material = attractVisualMaterial;
            spiral.startColor = AttractLilac;
            spiral.endColor = new Color(AttractBlue.r, AttractBlue.g, AttractBlue.b, 0f);
            attractSpirals[index] = spiral;
        }

        GameObject particlesObject = new GameObject("Attract Spiral Particles", typeof(ParticleSystem));
        particlesObject.transform.SetParent(transform, false);
        attractSpiralParticles = particlesObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule particleMain = attractSpiralParticles.main;
        particleMain.loop = false;
        particleMain.startLifetime = 0.22f;
        particleMain.startSpeed = 0f;
        particleMain.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f);
        particleMain.maxParticles = 36;
        particleMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule particleEmission = attractSpiralParticles.emission;
        particleEmission.enabled = false;
        attractSpiralParticles.GetComponent<ParticleSystemRenderer>().sharedMaterial = attractVisualMaterial;
        nextSpiralParticleTime = Time.time;
        UpdateAttractSpirals();
    }

    private void CreateRepelProjectileVisual()
    {
        repelVisualMaterial = CreateMobileSafeVfxMaterial();
        if (repelVisualMaterial == null)
            return;
        CreateFieldWindVisual(repelVisualMaterial);

        // Red highlight that envelops the casting player (replaces the red ball).
        GameObject coreObject = new GameObject("Repel Player Red Aura", typeof(ParticleSystem));
        coreObject.transform.SetParent(transform, false);
        ParticleSystem core = coreObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = core.main;
        main.loop = false;
        main.startLifetime = 8f;
        main.startSpeed = 0f;
        main.startSize = RepelAuraSize;
        main.startColor = new Color(RepelRed.r, RepelRed.g, RepelRed.b, 0.42f);
        main.maxParticles = 2;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = core.emission;
        emission.rateOverTime = 0f;
        emission.SetBurst(0, new ParticleSystem.Burst(0f, 1));
        core.GetComponent<ParticleSystemRenderer>().sharedMaterial = repelVisualMaterial;
        core.Play();

        GameObject sparksObject = new GameObject("Repel Player Embers", typeof(ParticleSystem));
        sparksObject.transform.SetParent(transform, false);
        repelOrbitSparks = sparksObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule sparkMain = repelOrbitSparks.main;
        sparkMain.loop = false;
        sparkMain.startLifetime = 0.2f;
        sparkMain.startSpeed = 0f;
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        sparkMain.maxParticles = 96;
        // Local space so the embers stay wrapped around the caster while moving.
        sparkMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule sparkEmission = repelOrbitSparks.emission;
        sparkEmission.enabled = false;
        repelOrbitSparks.GetComponent<ParticleSystemRenderer>().sharedMaterial = repelVisualMaterial;
        nextRepelSparkTime = Time.time;
    }

    private void UpdateRepelCoreVisual()
    {
        if (repelOrbitSparks == null || Time.time < nextRepelSparkTime)
            return;

        nextRepelSparkTime = Time.time + 0.03f;
        // Embers spiral around the player's body and shoot outward off them.
        for (int index = 0; index < 3; index++)
        {
            float angle = Time.time * 11f + index * Mathf.PI * 2f / 3f;
            float height = Mathf.Sin(Time.time * 5f + index * 2.1f) * RepelAuraHalfHeight;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 offset = radial * RepelAuraRadius + Vector3.up * height;
            ParticleSystem.EmitParams spark = new ParticleSystem.EmitParams
            {
                position = offset,
                velocity = (radial + Vector3.up * height * 0.3f).normalized * 4.2f,
                startColor = index == 0 ? RepelWhite : (index == 1 ? RepelOrange : RepelRed),
                startSize = 0.07f,
                startLifetime = 0.26f
            };
            repelOrbitSparks.Emit(spark, 1);
        }
    }

    private const float RepelAuraSize = 2.6f;
    private const float RepelAuraRadius = 0.75f;
    private const float RepelAuraHalfHeight = 0.95f;

    private void UpdateAttractSpirals()
    {
        if (attractSpirals == null)
            return;

        for (int spiralIndex = 0; spiralIndex < attractSpirals.Length; spiralIndex++)
        {
            LineRenderer spiral = attractSpirals[spiralIndex];
            if (spiral == null)
                continue;

            for (int pointIndex = 0; pointIndex < spiral.positionCount; pointIndex++)
            {
                float t = pointIndex / (float)(spiral.positionCount - 1);
                float radiusAtPoint = Mathf.Lerp(0.9f, 0.08f, t);
                float angle = (t * Mathf.PI * 4f) + (Time.time * 12f) +
                    spiralIndex * Mathf.PI * 2f / attractSpirals.Length;
                spiral.SetPosition(pointIndex, new Vector3(Mathf.Cos(angle) * radiusAtPoint,
                    Mathf.Sin(angle * 1.4f) * 0.3f, Mathf.Sin(angle) * radiusAtPoint));
            }
        }

        if (attractSpiralParticles == null || Time.time < nextSpiralParticleTime)
            return;

        nextSpiralParticleTime = Time.time + 0.045f;
        for (int index = 0; index < 2; index++)
        {
            float angle = Time.time * 12f + index * Mathf.PI;
            Vector3 position = new Vector3(Mathf.Cos(angle) * 0.82f,
                Mathf.Sin(angle * 1.4f) * 0.28f, Mathf.Sin(angle) * 0.82f);
            ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = -position.normalized * 3.4f,
                startColor = AttractBlue,
                startSize = 0.045f,
                startLifetime = 0.22f
            };
            attractSpiralParticles.Emit(particle, 1);
        }
    }

    private void CreateFieldWindVisual(Material material)
    {
        GameObject particlesObject = new GameObject(
            mode == Mode.Attract ? "Attract 30m Inward Wind" : "Repel 30m Outward Wind",
            typeof(ParticleSystem));
        particlesObject.transform.SetParent(transform, false);
        fieldWindParticles = particlesObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = fieldWindParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.82f, 1.02f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
        main.maxParticles = WindParticleCapacity;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = fieldWindParticles.emission;
        emission.enabled = false;
        ParticleSystem.TrailModule trails = fieldWindParticles.trails;
        trails.enabled = true;
        trails.ratio = 1f;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
        trails.minVertexDistance = 0.18f;
        trails.dieWithParticles = true;
        trails.sizeAffectsWidth = true;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
        ParticleSystemRenderer particleRenderer =
            fieldWindParticles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.trailMaterial = material;
        fieldWindParticles.Play();

        fieldWindRibbons = new LineRenderer[WindRibbonCount];
        for (int index = 0; index < fieldWindRibbons.Length; index++)
        {
            GameObject ribbonObject = new GameObject(
                mode == Mode.Attract ? "Attract Inward Wind Tendril" : "Repel Outward Wind Tendril",
                typeof(LineRenderer));
            ribbonObject.transform.SetParent(transform, false);
            LineRenderer ribbon = ribbonObject.GetComponent<LineRenderer>();
            ribbon.useWorldSpace = false;
            ribbon.positionCount = 36;
            ribbon.widthMultiplier = mode == Mode.Attract ? 0.13f : 0.15f;
            ribbon.numCornerVertices = 3;
            ribbon.numCapVertices = 2;
            ribbon.sharedMaterial = material;
            if (mode == Mode.Attract)
            {
                ribbon.startColor = new Color(AttractBlue.r, AttractBlue.g, AttractBlue.b, 0f);
                ribbon.endColor = AttractLilac;
            }
            else
            {
                ribbon.startColor = RepelWhite;
                ribbon.endColor = new Color(RepelRed.r, RepelRed.g, RepelRed.b, 0f);
            }
            fieldWindRibbons[index] = ribbon;
        }
        nextFieldWindParticleTime = Time.time;
        UpdateFieldWindRibbons();
    }

    private void UpdateFieldWindVisual()
    {
        if (fieldWindParticles == null || pulsed)
            return;

        UpdateFieldWindRibbons();
        UpdateFieldWindParticleMotion();
        if (Time.time < nextFieldWindParticleTime)
            return;

        nextFieldWindParticleTime = Time.time + 0.025f;
        for (int index = 0; index < 3; index++)
            EmitFieldWindParticle(fieldWindSequence++);
    }

    private void UpdateFieldWindRibbons()
    {
        if (fieldWindRibbons == null)
            return;

        bool inward = mode == Mode.Attract;
        float direction = inward ? 1f : -1f;
        for (int ribbonIndex = 0; ribbonIndex < fieldWindRibbons.Length; ribbonIndex++)
        {
            LineRenderer ribbon = fieldWindRibbons[ribbonIndex];
            if (ribbon == null)
                continue;
            Vector3 axis = FieldWindDirection(ribbonIndex * 17 + 3);
            Vector3 tangent = TangentFor(axis);
            Vector3 bitangent = Vector3.Cross(axis, tangent).normalized;
            for (int pointIndex = 0; pointIndex < ribbon.positionCount; pointIndex++)
            {
                float t = pointIndex / (float)(ribbon.positionCount - 1);
                float distance = inward
                    ? Mathf.Lerp(WindVisualRadius, 0.3f, t)
                    : Mathf.Lerp(0.3f, WindVisualRadius, t);
                float phase = t * Mathf.PI * 5f + Time.time * 2.1f * direction +
                    ribbonIndex * 1.73f;
                float curl = Mathf.Sin(t * Mathf.PI) * 0.3f;
                Vector3 curvedDirection = (axis + tangent * Mathf.Cos(phase) * curl +
                    bitangent * Mathf.Sin(phase) * curl).normalized;
                ribbon.SetPosition(pointIndex, curvedDirection * distance);
            }
        }
    }

    private void EmitFieldWindParticle(int sequence)
    {
        Vector3 radial = FieldWindDirection(sequence);
        Vector3 tangent = TangentFor(radial);
        bool inward = mode == Mode.Attract;
        float sequenceVariation = Mathf.Repeat(sequence * 0.381966f, 1f);
        float startRadius = inward
            ? Mathf.Lerp(WindVisualRadius * 0.9f, WindVisualRadius, sequenceVariation)
            : Mathf.Lerp(0.45f, 1.4f, sequenceVariation);
        float radialSpeed = inward ? -38f : 34f;
        ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
        {
            position = radial * startRadius,
            velocity = radial * radialSpeed + tangent * (inward ? 8f : 6f),
            startColor = mode == Mode.Attract
                ? (sequence & 1) == 0 ? AttractBlue : AttractLilac
                : (sequence & 1) == 0 ? RepelRed : RepelOrange,
            startSize = Mathf.Lerp(0.075f, 0.16f, sequenceVariation),
            startLifetime = Mathf.Lerp(0.82f, 1.02f, sequenceVariation)
        };
        fieldWindParticles.Emit(particle, 1);
    }

    private void UpdateFieldWindParticleMotion()
    {
        int count = fieldWindParticles.GetParticles(fieldWindParticleBuffer);
        bool inward = mode == Mode.Attract;
        for (int index = 0; index < count; index++)
        {
            ParticleSystem.Particle particle = fieldWindParticleBuffer[index];
            float distance = particle.position.magnitude;
            if ((inward && distance <= 0.3f) || (!inward && distance >= WindVisualRadius))
            {
                particle.remainingLifetime = 0f;
                fieldWindParticleBuffer[index] = particle;
                continue;
            }

            Vector3 radial = distance > 0.001f ? particle.position / distance : Vector3.up;
            Vector3 tangent = TangentFor(radial);
            float normalizedDistance = Mathf.Clamp01(distance / WindVisualRadius);
            float speed = inward
                ? Mathf.Lerp(55f, 36f, normalizedDistance)
                : Mathf.Lerp(30f, 48f, normalizedDistance);
            Vector3 targetVelocity = radial * (inward ? -speed : speed) +
                tangent * Mathf.Lerp(9f, 4f, normalizedDistance);
            particle.velocity = Vector3.Lerp(particle.velocity, targetVelocity,
                Mathf.Clamp01(Time.deltaTime * 9f));
            fieldWindParticleBuffer[index] = particle;
        }
        fieldWindParticles.SetParticles(fieldWindParticleBuffer, count);
    }

    public static Vector3 FieldWindDirection(int sequence)
    {
        int positiveSequence = Mathf.Max(0, sequence);
        float y = 1f - 2f * Mathf.Repeat((positiveSequence + 0.5f) * 0.61803398875f, 1f);
        float radiusAtY = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
        float angle = positiveSequence * 2.39996323f;
        return new Vector3(Mathf.Cos(angle) * radiusAtY, y,
            Mathf.Sin(angle) * radiusAtY).normalized;
    }

    private static Vector3 TangentFor(Vector3 radial)
    {
        Vector3 reference = Mathf.Abs(Vector3.Dot(radial, Vector3.up)) > 0.9f
            ? Vector3.right
            : Vector3.up;
        return Vector3.Cross(reference, radial).normalized;
    }

    private void SpawnAttractCollapsePulse(Vector3 center)
    {
        Material pulseMaterial = CreateMobileSafeVfxMaterial();
        if (pulseMaterial == null)
            return;

        GameObject pulseRoot = new GameObject("Attract Collapse Pulse");
        pulseRoot.transform.position = center;
        CreateAttractLightFlash(pulseRoot);
        CreateMiniStarburst(pulseRoot.transform, pulseMaterial, "Attract Mini Star", AttractLilac, 1.35f);
        CreatePulseRing(pulseRoot.transform, pulseMaterial, 3.6f, 0.075f, AttractPurple);
        EmitRadialPulse(pulseRoot.transform, pulseMaterial, 36, 0.18f, 8f, AttractLilac);
        EmitRadialPulse(pulseRoot.transform, pulseMaterial, 44, 3.6f, -12f, AttractPurple);
        Destroy(pulseRoot, AttractFlashDuration + 0.1f);
        Destroy(pulseMaterial, AttractFlashDuration + 0.1f);
    }

    private static void CreateAttractLightFlash(GameObject pulseRoot)
    {
        Light flash = pulseRoot.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = Brighten(new Color(0.12f, 0.58f, 1f));
        flash.range = AttractFlashRadius;
        flash.intensity = AttractFlashPeakIntensity;
        flash.shadows = LightShadows.None;
        flash.renderMode = LightRenderMode.ForcePixel;
        pulseRoot.AddComponent<FieldLightFlash>().Configure(
            flash, AttractFlashPeakIntensity, AttractFlashDuration);
    }

    public const float RepelImplosionSeconds = 0.1f;
    public const float RepelBurstEffectMultiplier = 2f;

    private void SpawnRepelShockwavePulse(Vector3 center)
    {
        Material pulseMaterial = CreateMobileSafeVfxMaterial();
        if (pulseMaterial == null)
            return;

        GameObject pulseRoot = new GameObject("Repel Implosion + Shockwave");
        pulseRoot.transform.position = center;
        // Attach to the caster so every effect moves with them.
        if (casterCentered && caster != null)
            pulseRoot.transform.SetParent(caster, true);

        // 1) Implosion during the 0.1s active window: energy collapses into the player.
        EmitRadialPulse(pulseRoot.transform, pulseMaterial, 96, 3.4f, -3.4f / RepelImplosionSeconds,
            RepelWhite, RepelImplosionSeconds, true, 0.09f);
        EmitRadialPulse(pulseRoot.transform, pulseMaterial, 64, 2.2f, -2.2f / RepelImplosionSeconds,
            RepelRed, RepelImplosionSeconds, true, 0.12f);

        // 2) At the end of the implosion the blast fires with every effect doubled.
        float m = RepelBurstEffectMultiplier;
        pulseRoot.AddComponent<RepelDelayedAction>().Configure(RepelImplosionSeconds, () =>
        {
            CreateRepelLightFlash(pulseRoot, m);
            CreateMiniStarburst(pulseRoot.transform, pulseMaterial, "Repel Mini Star", RepelWhite, 1.5f * m);
            pulseRoot.AddComponent<RepelPulseVisual>().Configure(pulseMaterial, 0.58f, m);
            EmitRadialPulse(pulseRoot.transform, pulseMaterial, Mathf.RoundToInt(72 * m), 0.12f, 14f * m,
                RepelRed, 0.34f, true, 0.07f * m);
            EmitRadialPulse(pulseRoot.transform, pulseMaterial, Mathf.RoundToInt(42 * m), 0.05f, 18f * m,
                RepelWhite, 0.34f, true, 0.07f * m);
            EmitRepelDebris(pulseRoot.transform, pulseMaterial, m);
        });

        float life = RepelImplosionSeconds + RepelFlashDuration + 0.1f;
        Destroy(pulseRoot, life);
        Destroy(pulseMaterial, life);
    }

    private static void CreateRepelLightFlash(GameObject pulseRoot, float multiplier = 1f)
    {
        Light flash = pulseRoot.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = Brighten(new Color(1f, 0.025f, 0.015f));
        flash.range = RepelFlashRadius * multiplier;
        flash.intensity = RepelFlashPeakIntensity * multiplier;
        flash.shadows = LightShadows.None;
        flash.renderMode = LightRenderMode.ForcePixel;
        pulseRoot.AddComponent<FieldLightFlash>().Configure(
            flash, RepelFlashPeakIntensity * multiplier, RepelFlashDuration);
    }

    private static void EmitRepelDebris(Transform parent, Material material, float multiplier = 1f)
    {
        int count = Mathf.RoundToInt(32 * multiplier);
        GameObject debrisObject = new GameObject("Repel Debris", typeof(ParticleSystem));
        debrisObject.transform.SetParent(parent, false);
        ParticleSystem debris = debrisObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = debris.main;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.26f, 0.48f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.maxParticles = count;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = debris.emission;
        emission.enabled = false;
        debris.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;

        for (int index = 0; index < count; index++)
        {
            float angle = index / (float)count * Mathf.PI * 2f;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0.28f + (index % 3) * 0.13f,
                Mathf.Sin(angle)).normalized;
            ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
            {
                position = radial * 0.18f,
                velocity = radial * (7f + (index % 4) * 1.4f) * multiplier,
                startColor = index % 2 == 0 ? RepelOrange : RepelRed,
                startSize = 0.1f * multiplier,
                startLifetime = 0.42f
            };
            debris.Emit(particle, 1);
        }
    }

    private static void CreateMiniStarburst(Transform parent, Material material, string starName,
        Color color, float radius)
    {
        GameObject starObject = new GameObject(starName, typeof(LineRenderer), typeof(FieldStarburstVisual));
        starObject.transform.SetParent(parent, false);
        LineRenderer star = starObject.GetComponent<LineRenderer>();
        star.useWorldSpace = false;
        star.positionCount = MiniStarPointCount + 1;
        star.widthMultiplier = 0.105f;
        star.numCornerVertices = 2;
        star.numCapVertices = 3;
        star.sharedMaterial = material;
        star.startColor = star.endColor = color;
        for (int point = 0; point <= MiniStarPointCount; point++)
        {
            int starPoint = (point * 2) % MiniStarPointCount;
            float angle = Mathf.PI * 2f * starPoint / MiniStarPointCount + Mathf.PI * 0.5f;
            star.SetPosition(point, new Vector3(Mathf.Cos(angle) * radius, 0.16f,
                Mathf.Sin(angle) * radius));
        }
        starObject.GetComponent<FieldStarburstVisual>().Configure(star, color, 0.62f);
    }

    private static void CreatePulseRing(Transform parent, Material material, float radius,
        float width, Color color)
    {
        GameObject ringObject = new GameObject("Attract Purple Ring", typeof(LineRenderer));
        ringObject.transform.SetParent(parent, false);
        LineRenderer ring = ringObject.GetComponent<LineRenderer>();
        const int points = 48;
        ring.useWorldSpace = false;
        ring.positionCount = points + 1;
        ring.widthMultiplier = width;
        ring.numCornerVertices = 2;
        ring.material = material;
        ring.startColor = color;
        ring.endColor = color;
        for (int index = 0; index <= points; index++)
        {
            float angle = index / (float)points * Mathf.PI * 2f;
            ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, 0.04f,
                Mathf.Sin(angle) * radius));
        }
    }

    private static void EmitRadialPulse(Transform parent, Material material, int count,
        float startRadius, float signedSpeed, Color color, float lifetime = 0.34f,
        bool localSpace = false, float size = 0.07f)
    {
        GameObject particlesObject = new GameObject("Attract Pulse Particles", typeof(ParticleSystem));
        particlesObject.transform.SetParent(parent, false);
        ParticleSystem particles = particlesObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.startLifetime = 0.34f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
        main.maxParticles = count;
        main.simulationSpace = localSpace
            ? ParticleSystemSimulationSpace.Local
            : ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;

        for (int index = 0; index < count; index++)
        {
            float angle = index / (float)count * Mathf.PI * 2f;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0.08f, Mathf.Sin(angle)).normalized;
            ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
            {
                position = (localSpace ? Vector3.zero : parent.position) + radial * startRadius,
                velocity = radial * signedSpeed,
                startColor = color,
                startSize = size,
                startLifetime = lifetime
            };
            particles.Emit(particle, 1);
        }
    }

    private static Material CreateMobileSafeVfxMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        if (shader == null)
            return null;

        Material material = new Material(shader) { name = "AttractVfxMobileSafe" };
        material.SetColor("_Color", Color.white);
        material.SetColor("_BaseColor", Color.white);
        return material;
    }
}

// Detached from the projectile so the flash can complete after the networked
// field object is destroyed immediately following its pulse.
internal sealed class FieldLightFlash : MonoBehaviour
{
    private Light flash;
    private float peakIntensity;
    private float duration;
    private float startedAt;

    public void Configure(Light target, float peak, float seconds)
    {
        flash = target;
        peakIntensity = Mathf.Max(0f, peak);
        duration = Mathf.Max(0.05f, seconds);
        startedAt = Time.time;
    }

    private void Update()
    {
        if (flash == null)
            return;
        float progress = Mathf.Clamp01((Time.time - startedAt) / duration);
        float decay = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(0.08f, 1f, progress));
        flash.intensity = peakIntensity * decay;
    }
}

internal sealed class FieldStarburstVisual : MonoBehaviour
{
    private LineRenderer star;
    private Color color;
    private float duration;
    private float startedAt;

    public void Configure(LineRenderer target, Color starColor, float seconds)
    {
        star = target;
        color = starColor;
        duration = Mathf.Max(0.05f, seconds);
        startedAt = Time.time;
    }

    private void Update()
    {
        if (star == null)
            return;

        float progress = Mathf.Clamp01((Time.time - startedAt) / duration);
        transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.45f, progress);
        Color faded = color;
        faded.a *= 1f - Mathf.SmoothStep(0f, 1f, progress);
        star.startColor = star.endColor = faded;
    }
}

/// <summary>Runs one cosmetic callback after a short delay (used for the Repel implosion → blast).</summary>
public sealed class RepelDelayedAction : MonoBehaviour
{
    private float fireAt;
    private System.Action action;

    public void Configure(float delay, System.Action callback)
    {
        fireAt = Time.time + delay;
        action = callback;
    }

    private void Update()
    {
        if (action == null || Time.time < fireAt)
            return;
        System.Action callback = action;
        action = null;
        callback();
    }
}
