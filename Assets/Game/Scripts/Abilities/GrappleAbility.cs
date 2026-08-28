using UnityEngine;

public sealed class GrappleAbility : MonoBehaviour, IAbility
{
    public AbilityId Id => AbilityId.Grapple;
    public float CooldownDuration => 3f;
    public bool IsActive => active;
    public const float CooldownSeconds = 3f;
    public const float MaximumRange = 50f;
    public const float MaximumActiveDuration = 4f;
    public const float MaximumServerFacingAngle = 60f;
    // Grappling steers the current velocity instead of simply dragging the
    // player to the anchor. This keeps traversal momentum meaningful and lets
    // a release carry the player past the ledge.
    public const float MaximumTraversalSpeed = 96f;
    public const float MinimumTraversalSpeed = 24f;
    public const float TraversalAcceleration = 95f;
    public const float DirectionRedirectRadiansPerSecond = 8f;
    // Release before reaching the anchor so the grapple becomes a traversal
    // launch instead of pulling the player all the way into the surface.
    public const float ReleaseDistance = 4f;
    private const float CableSpeed = 115f;
    private const float MinimumCableTravelTime = 0.08f;
    private const float MaximumCableTravelTime = 0.38f;

    private PlayerMovement movement;
    private PlayerWindPresentation windPresentation;
    private LineRenderer rope;
    private bool active;
    private bool movable;
    private Vector3 anchor;
    private Transform target;
    private Vector3 targetLocalAnchor;
    private float ropeStartTime;
    private float cableTravelDuration;
    private bool attached;
    private bool yankApplied;
    private Cam localCamera;
    private Material electricMaterial;
    private ParticleSystem anchorSparks;
    private ParticleSystem cableParticles;
    private float nextCableParticleTime;

    private static readonly Color CableCyan = new Color(0.15f, 0.82f, 1f, 0.96f);
    private static readonly Color CableWhite = new Color(0.95f, 1f, 1f, 1f);

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        windPresentation = GetComponent<PlayerWindPresentation>();
    }

    private void FixedUpdate()
    {
        if (!active || !attached || movable || movement == null || !movement.isOwner || movement.rb == null)
            return;

        Vector3 delta = anchor - movement.rb.worldCenterOfMass;
        if (delta.magnitude <= ReleaseDistance)
            return;

        movement.SetMovementSuppressed(true, MaximumTraversalSpeed, false);
        windPresentation?.TriggerAbilityWind(0.88f, 0.16f);
        if (!yankApplied)
        {
            yankApplied = true;
            localCamera?.PlayGrappleYank();
        }
        movement.rb.linearVelocity = CalculateTraversalVelocity(
            movement.rb.linearVelocity,
            delta.normalized,
            Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        if (!active || rope == null)
            return;
        Vector3 start = localCamera != null && movement != null && movement.isOwner
            ? localCamera.GetGrappleArmOrigin()
            : transform.position + Vector3.up * 1.1f;
        Vector3 anchorPosition = target != null ? target.TransformPoint(targetLocalAnchor) : anchor;
        float travelProgress = Mathf.Clamp01((Time.time - ropeStartTime) / cableTravelDuration);
        Vector3 end = Vector3.Lerp(start, anchorPosition, Mathf.SmoothStep(0f, 1f, travelProgress));
        if (!attached && travelProgress >= 1f)
            Attach(anchorPosition);
        Vector3 delta = end - start;
        Vector3 sideways = Vector3.Cross(delta.normalized, Vector3.up);
        if (sideways.sqrMagnitude < 0.001f)
            sideways = Vector3.right;
        sideways.Normalize();
        for (int index = 0; index < rope.positionCount; index++)
        {
            float t = index / (float)(rope.positionCount - 1);
            float wave = Mathf.Sin((Time.time - ropeStartTime) * 34f + index * 2.1f) *
                Mathf.Sin(t * Mathf.PI) * 0.075f;
            rope.SetPosition(index, Vector3.Lerp(start, end, t) + sideways * wave);
        }
        UpdateElectricEffects(start, end);
    }

    public void Activate() { }

    public void BeginPresentation(Vector3 hitPoint, Transform liveTarget, bool pullsTarget,
        float elapsed = 0f)
    {
        anchor = hitPoint;
        target = liveTarget;
        targetLocalAnchor = target != null ? target.InverseTransformPoint(hitPoint) : Vector3.zero;
        movable = pullsTarget;
        active = true;
        attached = false;
        yankApplied = false;
        ropeStartTime = Time.time - Mathf.Max(0f, elapsed);
        Vector3 presentationStart = movement != null && movement.isOwner
            ? GetComponentInChildren<Cam>(true)?.GetGrappleArmOrigin() ?? transform.position
            : transform.position + Vector3.up * 1.1f;
        cableTravelDuration = GetCableTravelDuration(Vector3.Distance(presentationStart, hitPoint));
        if (movement != null && movement.isOwner)
        {
            localCamera = GetComponentInChildren<Cam>(true);
            localCamera?.SetGrappleArmActive(true, hitPoint - transform.position);
        }
        if (electricMaterial == null)
            electricMaterial = CreateMobileSafeRopeMaterial();
        if (rope == null)
        {
            rope = gameObject.AddComponent<LineRenderer>();
            rope.positionCount = 9;
            rope.startWidth = rope.endWidth = 0.115f;
            rope.numCornerVertices = 3;
            rope.numCapVertices = 3;
        }
        rope.material = electricMaterial;
        rope.startColor = CableWhite;
        rope.endColor = CableCyan;
        rope.enabled = true;
        SfxManager.PlayGrappleActivation();
        CreateLaunchEffects();
    }

    public void EndPresentation()
    {
        active = false;
        attached = false;
        if (movement != null && movement.isOwner)
        {
            movement.ReleaseMovementSuppressionPreservingMomentum();
            localCamera?.SetGrappleArmActive(false, Vector3.forward);
        }
        if (rope != null)
            rope.enabled = false;
        ClearElectricEffects();
    }

    public void CancelForJump()
    {
        if (active && attached && !movable && movement != null && movement.isOwner && movement.rb != null)
        {
            Vector3 delta = anchor - movement.rb.worldCenterOfMass;
            if (delta.sqrMagnitude > 1.5f * 1.5f)
            {
                // Preserve the final redirected pull before releasing it.
                movement.rb.linearVelocity = CalculateTraversalVelocity(
                    movement.rb.linearVelocity,
                    delta.normalized,
                    Time.fixedDeltaTime);
            }
        }

        EndPresentation();
    }

    public static float GetCableTravelDuration(float distance)
    {
        return Mathf.Clamp(distance / CableSpeed, MinimumCableTravelTime, MaximumCableTravelTime);
    }

    public static Vector3 CalculateTraversalVelocity(Vector3 currentVelocity, Vector3 anchorDirection,
        float deltaTime)
    {
        if (anchorDirection.sqrMagnitude < 0.0001f || deltaTime <= 0f)
            return currentVelocity;

        float currentSpeed = currentVelocity.magnitude;
        float nextSpeed = Mathf.Min(
            MaximumTraversalSpeed,
            Mathf.Max(currentSpeed, MinimumTraversalSpeed) + TraversalAcceleration * deltaTime);
        Vector3 currentDirection = currentSpeed > 0.0001f
            ? currentVelocity / currentSpeed
            : anchorDirection.normalized;
        Vector3 redirectedDirection = Vector3.RotateTowards(
            currentDirection,
            anchorDirection.normalized,
            DirectionRedirectRadiansPerSecond * deltaTime,
            0f);
        return redirectedDirection * nextSpeed;
    }

    public static bool HasTimedOut(float activatedAt, float currentTime)
    {
        return currentTime - activatedAt >= MaximumActiveDuration;
    }

    public static bool IsAimWithinServerFacing(Vector3 serverForward, Vector3 aimDirection)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(serverForward, Vector3.up);
        Vector3 flatAim = Vector3.ProjectOnPlane(aimDirection, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
            return false;
        if (flatAim.sqrMagnitude < 0.0001f)
            return aimDirection.sqrMagnitude >= 0.0001f;

        float minimumDot = Mathf.Cos(MaximumServerFacingAngle * Mathf.Deg2Rad);
        return Vector3.Dot(flatForward.normalized, flatAim.normalized) >= minimumDot;
    }

    private void Attach(Vector3 anchorPosition)
    {
        if (attached)
            return;

        attached = true;
        anchor = anchorPosition;
        CreateAnchorEffects();
    }

    private static Material CreateMobileSafeRopeMaterial()
    {
        // Use a shader already retained by the UI build on iOS. The previous
        // runtime Shader.Find-only URP material could be stripped and render
        // as pink on device.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = shader != null ? new Material(shader) : null;
        if (material != null)
        {
            material.SetColor("_Color", Color.white);
            material.SetColor("_BaseColor", Color.white);
        }
        return material;
    }

    private void CreateLaunchEffects()
    {
        if (anchorSparks != null)
            Destroy(anchorSparks.gameObject);
        if (cableParticles != null)
            Destroy(cableParticles.gameObject);
        anchorSparks = null;
        cableParticles = null;
        if (electricMaterial == null)
            return;

        Vector3 start = localCamera != null && movement != null && movement.isOwner
            ? localCamera.GetGrappleArmOrigin()
            : transform.position + Vector3.up * 1.1f;
        SpawnMuzzleFlash(start);

        GameObject cableObject = new GameObject("Grapple Traveling Particles", typeof(ParticleSystem));
        cableParticles = cableObject.GetComponent<ParticleSystem>();
        ConfigureSparks(cableParticles, 0f, 28);
        nextCableParticleTime = Time.time;
    }

    private void CreateAnchorEffects()
    {
        if (anchorSparks != null)
            Destroy(anchorSparks.gameObject);
        GameObject anchorObject = new GameObject("Grapple Anchor Sparks", typeof(ParticleSystem));
        anchorObject.transform.position = anchor;
        anchorSparks = anchorObject.GetComponent<ParticleSystem>();
        ConfigureSparks(anchorSparks, 28f, 36);
    }

    private void UpdateElectricEffects(Vector3 start, Vector3 end)
    {
        if (anchorSparks != null)
            anchorSparks.transform.position = end;
        if (cableParticles == null || Time.time < nextCableParticleTime)
            return;

        nextCableParticleTime = Time.time + 0.025f;
        Vector3 velocity = (end - start).normalized * 24f;
        for (int index = 0; index < 3; index++)
        {
            float t = Mathf.Repeat((Time.time - ropeStartTime) * 1.8f + index / 3f, 1f);
            ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
            {
                position = Vector3.Lerp(start, end, t),
                velocity = velocity,
                startColor = CableWhite,
                startSize = 0.07f,
                startLifetime = 0.12f
            };
            cableParticles.Emit(particle, 1);
        }
    }

    private void SpawnMuzzleFlash(Vector3 position)
    {
        GameObject flashObject = new GameObject("Grapple Muzzle Flash", typeof(ParticleSystem));
        flashObject.transform.position = position;
        ParticleSystem flash = flashObject.GetComponent<ParticleSystem>();
        ConfigureSparks(flash, 0f, 32);
        ParticleSystem.EmissionModule emission = flash.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
        flash.Play();
        Destroy(flashObject, 0.45f);
    }

    private void ConfigureSparks(ParticleSystem particles, float rate, int maxParticles)
    {
        // A newly added ParticleSystem can already be playing before its first
        // frame. Duration cannot be changed until it has been fully cleared.
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = rate > 0f;
        main.duration = 0.35f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
        main.startColor = new ParticleSystem.MinMaxGradient(CableWhite, CableCyan);
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = rate;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.06f;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = electricMaterial;
    }

    private void ClearElectricEffects()
    {
        if (anchorSparks != null)
            Destroy(anchorSparks.gameObject);
        anchorSparks = null;
        if (cableParticles != null)
            Destroy(cableParticles.gameObject);
        cableParticles = null;
        if (electricMaterial != null)
            Destroy(electricMaterial);
        electricMaterial = null;
    }

    private void OnDestroy()
    {
        ClearElectricEffects();
    }

    private void OnDisable()
    {
        EndPresentation();
    }
}
