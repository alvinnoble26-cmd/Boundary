using UnityEngine;
using PurrNet;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Slope Movement")]
    public float maxSlopeAngle = 45f;
    private RaycastHit slopeHit;
    private bool exitingSlope;

    [Header("Movement")]
    public float acceleration = 35f;
    public float maxSpeed = 7f;
    public float deceleration = 20f;
    public float jumpForce = 7f;
    public float fallGravityMultiplier = 12f;

    [Header("Mobile Input")]
    public float deadzone = 0.15f;
    public float inputSmoothing = 12f;

    [Header("Stopping Control")]
    [SerializeField, Min(1f)] private float groundedStopDeceleration = 42f;
    [SerializeField, Min(0f)] private float airborneStopDeceleration = 6f;
    [SerializeField, Min(0.01f)] private float stopSpeedThreshold = 0.12f;

    [Header("Detection")]
    public LayerMask groundMask = ~0;
    public float groundCheckDistance = 0.25f;
    public float wallCheckDistance = 0.7f;
    public float wallSphereRadius = 0.35f;

    [Header("Wall Settings")]
    public float wallJumpUp = 7f;
    public float wallJumpSide = 7f;
    public float wallStickTime = 0.25f;
    public float wallSlideSpeed = 3.5f;
    public float wallAttachForce = 15f;
    [Min(1f)] public float wallRunSpeedMultiplier = 1.5f;

    [Header("Tilt Settings")]
    public float uprightStrength = 10f;
    public float wallTiltAngle = 25f;
    public float backTiltAngle = 10f;

    [Header("Refs")]
    public Transform orientation;
    public PlayerInputReader input;

    [Header("Boundary Physics")]
    [SerializeField, Min(5f)] private float maximumBoundaryVerticalSpeed = 26f;
    private float slideJumpVerticalSpeedAllowance = -1f;

    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public Vector2 moveInput;

    public bool IsGrounded => isGrounded;
    public bool IsWallRunning => !isGrounded && wallStickCounter > 0f;
    public Vector3 WallRunNormal => IsWallRunning ? wallNormal : Vector3.zero;
    public bool IsStableGrounded { get; private set; }
    public bool JumpPressedThisFrame { get; private set; }
    public bool MovementSuppressed { get; private set; }
    public float ExternalSpeedCap { get; private set; } = -1f;
    public float ExternalSpeedMultiplier { get; private set; } = 1f;
    private readonly System.Collections.Generic.Dictionary<int, float> externalSpeedModifiers =
        new System.Collections.Generic.Dictionary<int, float>();
    private float preservedMomentumSpeedCap = -1f;
    private float preservedMomentumStartedAt;
    private float preservedMomentumUntil;
    [SerializeField, Min(0f)] private float grappleMomentumCarryDuration = 0.9f;

    public Vector3 LastFlatMoveDir { get; private set; } = Vector3.forward;

    public Vector3 CurrentAimFlatDir
    {
        get
        {
            Vector3 fwd = orientation ? orientation.forward : transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f) return fwd.normalized;
            if (LastFlatMoveDir.sqrMagnitude > 0.0001f) return LastFlatMoveDir.normalized;
            return Vector3.forward;
        }
    }

    private bool isGrounded;
    private bool jumpRequested;
    private bool slideJumpExecutedThisStep;
    private bool heldJumpUsedOnCurrentGroundContact;
    private bool movementSuppressionAllowsEnvironmentalRelease = true;

    private Vector3 wallNormal;
    private float wallStickCounter;

    private Collider myCol;
    private Vector2 smoothedMoveInput;
    private BoundaryPlayerState boundaryState;
    private float viewYaw;
    private bool hasViewYaw;

    public float CurrentWallRunTilt
    {
        get
        {
            if (!IsWallRunning)
                return 0f;

            Transform basis = orientation ? orientation : transform;
            Vector3 localWallNormal = basis.InverseTransformDirection(wallNormal);
            return localWallNormal.x > 0f ? -wallTiltAngle : wallTiltAngle;
        }
    }

// Add this field near the top with other private fields
private SlideAbility slideAbility;

// Replace your existing Awake with this
void Awake()
{
    rb = GetComponent<Rigidbody>();
    myCol = GetComponent<Collider>();
    if (input == null) input = GetComponent<PlayerInputReader>();
    if (orientation == null) orientation = transform;
    slideAbility = GetComponentInChildren<SlideAbility>();
    boundaryState = GetComponent<BoundaryPlayerState>();
    viewYaw = transform.eulerAngles.y;
}
    protected override void OnSpawned()
{
    StartCoroutine(SetupPhysicsAuthority());
}

private System.Collections.IEnumerator SetupPhysicsAuthority()
{
    yield return new WaitUntil(() => rb != null);

    // Wait a moment for ownership to settle
    yield return null;

    Debug.Log($"[Move] OnSpawned isOwner={isOwner} kinematic(before)={rb.isKinematic}");

    if (isOwner)
    {
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }
    else
    {
        rb.isKinematic = true;
    }

    Debug.Log($"[Move] after: isOwner={isOwner} kinematic(after)={rb.isKinematic}");
}

    public void SetMovementSuppressed(bool on, float speedCap = -1f, bool allowEnvironmentalRelease = true)
    {
        MovementSuppressed = on;
        ExternalSpeedCap = speedCap;
        movementSuppressionAllowsEnvironmentalRelease = allowEnvironmentalRelease;
        if (on)
        {
            preservedMomentumSpeedCap = -1f;
            preservedMomentumUntil = 0f;
        }
    }

    public void SetExternalSpeedMultiplier(int sourceId, float multiplier)
    {
        if (sourceId == 0)
            return;
        externalSpeedModifiers[sourceId] = Mathf.Max(0.05f, multiplier);
        RecalculateExternalSpeedMultiplier();
    }

    public void RemoveExternalSpeedMultiplier(int sourceId)
    {
        if (externalSpeedModifiers.Remove(sourceId))
            RecalculateExternalSpeedMultiplier();
    }

    private void RecalculateExternalSpeedMultiplier()
    {
        float combined = 1f;
        foreach (float multiplier in externalSpeedModifiers.Values)
            combined *= multiplier;
        ExternalSpeedMultiplier = Mathf.Clamp(combined, 0.2f, 2.5f);
    }

    public void ReleaseMovementSuppressionPreservingMomentum(float carryDuration = -1f)
    {
        MovementSuppressed = false;
        ExternalSpeedCap = -1f;
        movementSuppressionAllowsEnvironmentalRelease = true;

        if (rb == null)
            return;

        Vector3 velocity = rb.linearVelocity;
        preservedMomentumSpeedCap = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        preservedMomentumStartedAt = Time.time;
        float duration = carryDuration >= 0f ? carryDuration : grappleMomentumCarryDuration;
        preservedMomentumUntil = Time.time + duration;
    }

    public void SetViewYaw(float yaw)
    {
        viewYaw = Mathf.Repeat(yaw, 360f);
        hasViewYaw = true;
    }

    void FixedUpdate()
    {
        
        if (!isOwner) return;
        slideJumpExecutedThisStep = false;
        if (slideJumpVerticalSpeedAllowance > 0f && rb != null && rb.linearVelocity.y <= 0f)
            slideJumpVerticalSpeedAllowance = -1f;
        Vector2 raw = input != null ? input.Move : Vector2.zero;
        raw = Vector2.ClampMagnitude(raw, 1f);
        if (raw.magnitude < deadzone)
            raw = Vector2.zero;

        if (raw == Vector2.zero)
        {
            // Releasing the stick/key must stop acceleration immediately.
            // Smoothing only the input ramp-up avoids the old movement tail.
            smoothedMoveInput = Vector2.zero;
        }
        else
        {
            float k = 1f - Mathf.Exp(-inputSmoothing * Time.fixedDeltaTime);
            smoothedMoveInput = Vector2.Lerp(smoothedMoveInput, raw, k);
        }

        moveInput = smoothedMoveInput;

        if (input != null && input.ConsumeJump())
        {
            RequestJump();
        }
        UpdateGrounded();

        // A held jump is queued once per landing. This makes the mobile jump
        // button continuously hop without turning a held press into repeated
        // air or wall jumps.
        if (!isGrounded)
        {
            heldJumpUsedOnCurrentGroundContact = false;
        }
        else if (input != null && input.IsJumpHeld && !heldJumpUsedOnCurrentGroundContact)
        {
            heldJumpUsedOnCurrentGroundContact = true;
            RequestJump();
        }

        UpdateBoundaryFooting();
        HandleWallDetection();

        // Consume an active slide jump before either suppression or normal
        // jump handling can downgrade it to a regular jump.
        if (slideJumpExecutedThisStep || TryExecuteRequestedSlideJump())
        {
            ApplyBoundaryForces();
            ApplyMasterRotation();
            JumpPressedThisFrame = false;
            jumpRequested = false;
            slideJumpExecutedThisStep = false;
            return;
        }

        if (MovementSuppressed)
        {
            ApplyBoundaryForces();
            ApplyMasterRotation();
            JumpPressedThisFrame = false;
            jumpRequested = false;
            return;
        }

        PerformNormalMovement();
        HandleGravityAndWallPhysics();
        HandleJump();
        ApplyBoundaryForces();
        ApplyMasterRotation();

        float cap = ((ExternalSpeedCap > 0f) ? ExternalSpeedCap : maxSpeed) * ExternalSpeedMultiplier;
        if (IsWallRunning)
            cap *= wallRunSpeedMultiplier;
        if (Time.time >= preservedMomentumUntil)
        {
            preservedMomentumSpeedCap = -1f;
        }
        else if (preservedMomentumSpeedCap > cap)
        {
            float carryProgress = Mathf.InverseLerp(
                preservedMomentumStartedAt,
                preservedMomentumUntil,
                Time.time);
            cap = Mathf.Lerp(preservedMomentumSpeedCap, cap, carryProgress);
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (horizontalVelocity.magnitude <= maxSpeed)
                preservedMomentumSpeedCap = -1f;
        }
        ClampHorizontalSpeed(cap);

        Vector3 v = rb.linearVelocity;
        if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || v.sqrMagnitude > 5000f)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        JumpPressedThisFrame = false;
        //Cursor.lockState = CursorLockMode.None;
        //Cursor.visible = true;
    }

    private bool TryExecuteRequestedSlideJump()
    {
        if (!jumpRequested)
            return false;

        if (slideAbility == null || !slideAbility.IsActive)
        {
            SlideAbility[] slideAbilities = GetComponentsInChildren<SlideAbility>(true);
            for (int index = 0; index < slideAbilities.Length; index++)
            {
                if (slideAbilities[index] != null && slideAbilities[index].IsActive)
                {
                    slideAbility = slideAbilities[index];
                    break;
                }
            }
        }

        return slideAbility != null && slideAbility.IsActive && slideAbility.TryManualSlideJump();
    }

    void UpdateGrounded()
    {
        if (myCol == null)
        {
            isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.2f, groundMask, QueryTriggerInteraction.Ignore);
            return;
        }

        Bounds b = myCol.bounds;

        float radius = Mathf.Min(b.extents.x, b.extents.z) * 0.45f;
        radius = Mathf.Max(radius, 0.05f);

        Vector3 start = new Vector3(b.center.x, b.min.y + radius + 0.02f, b.center.z);

        isGrounded = Physics.SphereCast(
            start,
            radius,
            Vector3.down,
            out _,
            groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (isGrounded)
            exitingSlope = false;
    }

    void PerformNormalMovement()
    {
        Vector3 fwd = orientation ? orientation.forward : transform.forward;
        Vector3 right = orientation ? orientation.right : transform.right;

        Vector3 inputDir = (fwd * moveInput.y + right * moveInput.x);
        inputDir.y = 0f;

        float inputMag = Mathf.Clamp01(inputDir.magnitude);
        Vector3 inputDirNorm = (inputMag > 0.0001f) ? (inputDir / inputMag) : Vector3.zero;

        if (inputMag > 0.001f)
            LastFlatMoveDir = inputDirNorm;

        Vector3 vel = rb.linearVelocity;
        Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);

        float movementMultiplier = 1f;
        if (boundaryState != null && (boundaryState.State == BoundaryKnockoutState.EventHorizon ||
                                      boundaryState.State == BoundaryKnockoutState.OutOfBounds))
            movementMultiplier *= 0.48f;

        if (inputMag > 0.001f)
        {
            if (OnSlope() && !exitingSlope)
            {
                Vector3 slopeDirNorm = GetSlopeMoveDirection(inputDirNorm);
                Vector3 slopeDir = slopeDirNorm * inputMag;

                rb.AddForce(slopeDir * (acceleration * movementMultiplier), ForceMode.Acceleration);

                if (rb.linearVelocity.y > 0f)
                    rb.AddForce(Vector3.down * 80f, ForceMode.Acceleration);

                rb.useGravity = true;
            }
            else
            {
                rb.useGravity = true;
                rb.AddForce(inputDirNorm * (acceleration * inputMag * movementMultiplier), ForceMode.Acceleration);
            }
        }
        else if (Time.time >= preservedMomentumUntil)
        {
            float stopRate = isGrounded
                ? Mathf.Max(deceleration, groundedStopDeceleration)
                : airborneStopDeceleration;
            Vector3 stoppedFlatVelocity = Vector3.MoveTowards(
                flatVel,
                Vector3.zero,
                stopRate * Time.fixedDeltaTime);
            if (stoppedFlatVelocity.magnitude <= stopSpeedThreshold)
                stoppedFlatVelocity = Vector3.zero;

            rb.linearVelocity = new Vector3(stoppedFlatVelocity.x, vel.y, stoppedFlatVelocity.z);
            rb.useGravity = true;
        }
        else
        {
            rb.useGravity = true;
        }
    }

    void ClampHorizontalSpeed(float speedCap)
    {
        Vector3 vel = rb.linearVelocity;
        Vector3 flat = new Vector3(vel.x, 0f, vel.z);

        float m = flat.magnitude;
        if (m > speedCap)
        {
            Vector3 capped = flat * (speedCap / m);
            rb.linearVelocity = new Vector3(capped.x, vel.y, capped.z);
        }
    }

void HandleWallDetection()
{
    if (isGrounded)
    {
        wallStickCounter = 0f;
        // If we land while suppressed (e.g. mid-slide), reset suppression
        if (MovementSuppressed && movementSuppressionAllowsEnvironmentalRelease)
            SetMovementSuppressed(false, -1f);
        return;
    }

    if (TryFindWall(out RaycastHit hit))
    {
        wallNormal = hit.normal;
        wallStickCounter = wallStickTime;
    }
    else
    {
        wallStickCounter = Mathf.Max(0f, wallStickCounter - Time.fixedDeltaTime);

        // Wall contact lost — if still suppressed, release it
        if (wallStickCounter <= 0f && MovementSuppressed && movementSuppressionAllowsEnvironmentalRelease)
            SetMovementSuppressed(false, -1f);
    }
}

    void HandleGravityAndWallPhysics()
    {
        if (IsWallRunning)
        {
            rb.AddForce(-wallNormal * wallAttachForce, ForceMode.Acceleration);

            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(v.x, 0f, v.z);
            rb.useGravity = false;
            return;
        }

        rb.useGravity = true;
        if (rb.linearVelocity.y < 0f)
        {
            BoundaryMatchController match = BoundaryMatchController.Instance;
            float activeFallMultiplier = match != null && match.Phase != BoundaryPhase.Waiting
                ? BoundaryMath.BoundaryFallGravityMultiplier(
                    BoundaryMath.SingularityProximity01(rb.position, match.SingularityPosition))
                : fallGravityMultiplier;
            rb.AddForce(Vector3.up * Physics.gravity.y * (activeFallMultiplier - 1f), ForceMode.Acceleration);
        }
    }

void HandleJump()
{
    if (!jumpRequested) return;

    // Always release suppression on jump regardless of source
    if (MovementSuppressed)
        SetMovementSuppressed(false, -1f);

    if (isGrounded)
    {
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.useGravity = true;
        rb.linearVelocity = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
        SfxManager.PlayJump();
    }
    else if (wallStickCounter > 0f)
    {
        Vector3 jumpDir = (wallNormal + Vector3.up).normalized;
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.useGravity = true;
        rb.linearVelocity = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
        rb.AddForce(jumpDir * (wallJumpUp + wallJumpSide), ForceMode.Impulse);
        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
        wallStickCounter = 0f;
        SfxManager.PlayJump();
    }

    jumpRequested = false;
    exitingSlope = true;
}

    private void RequestJump()
    {
        jumpRequested = true;
        JumpPressedThisFrame = true;
        GetComponent<PlayerAbilities>()?.CancelGrappleForJump();

        // Resolve slide jumps at the moment the input is queued. Waiting until
        // later in FixedUpdate allowed suppression and component execution
        // order to consume the request before SlideAbility saw it.
        if (TryExecuteRequestedSlideJump())
        {
            jumpRequested = false;
            heldJumpUsedOnCurrentGroundContact = true;
            slideJumpExecutedThisStep = true;
        }
    }

    public void ApplyBoundaryImpulse(Vector3 velocityChange)
    {
        if (!isOwner || rb == null)
            return;

        rb.AddForce(Vector3.ClampMagnitude(velocityChange, 18f), ForceMode.VelocityChange);
    }

    public void ApplyAbilityImpulse(Vector3 velocityChange)
    {
        if (!isOwner || rb == null)
            return;

        rb.AddForce(Vector3.ClampMagnitude(velocityChange, 60f), ForceMode.VelocityChange);
    }

    public void AllowSlideJumpVerticalSpeed(float launchSpeed)
    {
        slideJumpVerticalSpeedAllowance = Mathf.Max(0f, launchSpeed);
    }

    public void MarkExitingSlope()
    {
        exitingSlope = true;
    }

    public static float ResolveVerticalSpeedCap(float configuredCap, float slideJumpAllowance)
    {
        return Mathf.Max(configuredCap, slideJumpAllowance);
    }

    private void UpdateBoundaryFooting()
    {
        BoundaryMatchController match = BoundaryMatchController.Instance;
        Vector3 center = match != null ? match.ArenaCenter : transform.position;
        float radius = match != null ? match.RingRadius : float.MaxValue;
        Vector3 flatOffset = transform.position - center;
        flatOffset.y = 0f;
        IsStableGrounded = isGrounded && flatOffset.magnitude <= radius + 0.5f;
    }

    private void ApplyBoundaryForces()
    {
        BoundaryMatchController match = BoundaryMatchController.Instance;
        if (match == null || match.Phase == BoundaryPhase.Waiting || rb == null)
            return;

        Vector3 acceleration = BoundaryMath.PlayerPullAcceleration(
            rb.position,
            match.SingularityPosition,
            match.ArenaCenter,
            match.ArenaFloorY,
            match.RingRadius,
            match.EffectivePullStrength,
            IsStableGrounded);
        rb.AddForce(acceleration, ForceMode.Acceleration);
        // Slope movement may disable built-in gravity while grounded. Boundary
        // recovery at the arena edge must never leave the player gravity-free,
        // except while a wall run is actively holding the player in place.
        rb.useGravity = !IsWallRunning;

        Vector3 radial = rb.position - match.ArenaCenter;
        radial.y = 0f;
        if (radial.sqrMagnitude > 0.1f && match.Phase == BoundaryPhase.InnerRing)
        {
            Vector3 tangent = Vector3.Cross(Vector3.up, radial.normalized) * match.CurrentDirection;
            float currentStrength = match.Phase == BoundaryPhase.InnerRing ? 4.2f : 3.4f;
            rb.AddForce(tangent * currentStrength, ForceMode.Acceleration);
        }

        if (match.FracturePulse > 0f)
        {
            float angle = Mathf.Atan2(radial.z, radial.x);
            float stripe = Mathf.Abs(Mathf.Sin(angle * 4f + match.DisasterSeed * 0.001f));
            if (stripe < 0.24f)
            {
                float fracturePower = BoundaryMath.DisasterPower(BoundaryDisaster.FractureLines);
                Vector3 fracturePush = Vector3.up * (10f * fracturePower * match.FracturePulse) +
                                       radial.normalized * (3f * fracturePower * match.FracturePulse);
                rb.AddForce(fracturePush, ForceMode.Acceleration);
            }
        }

        BoundaryHazard.ApplyLocalFields(this);

        Vector3 velocity = rb.linearVelocity;
        float verticalSpeedCap = ResolveVerticalSpeedCap(
            maximumBoundaryVerticalSpeed, slideJumpVerticalSpeedAllowance);
        if (velocity.y > verticalSpeedCap)
            rb.linearVelocity = new Vector3(velocity.x, verticalSpeedCap, velocity.z);
    }

    void ApplyMasterRotation()
    {
        float zTilt = 0f;
        float xTilt = moveInput.y * backTiltAngle;

        Transform basis = orientation ? orientation : transform;
        float yaw = hasViewYaw ? viewYaw : basis.eulerAngles.y;

        zTilt = CurrentWallRunTilt;

        Vector3 currentEuler = rb.rotation.eulerAngles;
        float tiltBlend = 1f - Mathf.Exp(-uprightStrength * Time.fixedDeltaTime);
        float smoothedX = Mathf.LerpAngle(currentEuler.x, xTilt, tiltBlend);
        float smoothedZ = Mathf.LerpAngle(currentEuler.z, zTilt, tiltBlend);

        // Yaw is exact rather than damped: the networked skin's front must
        // agree with the first-person camera on every physics tick.
        rb.MoveRotation(Quaternion.Euler(smoothedX, yaw, smoothedZ));
    }

    bool TryFindWall(out RaycastHit bestHit)
    {
        Vector3[] dirs = { transform.forward, -transform.forward, transform.right, -transform.right };
        bestHit = default;

        float bestDist = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < dirs.Length; i++)
        {
            if (Physics.SphereCast(transform.position, wallSphereRadius, dirs[i], out RaycastHit hit,
                    wallCheckDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (!IsWallJumpSurface(hit.collider, hit.normal, transform))
                    continue;

                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    bestHit = hit;
                    found = true;
                }
            }
        }

        return found;
    }

    public bool TryFindSlideWall(Vector3 travelDirection, out RaycastHit bestHit)
    {
        bestHit = default;
        float bestDistance = float.PositiveInfinity;
        bool found = false;

        Vector3 flatTravel = Vector3.ProjectOnPlane(travelDirection, Vector3.up);
        if (flatTravel.sqrMagnitude < 0.0001f)
            flatTravel = transform.forward;
        flatTravel.Normalize();

        ConsiderSlideWall(flatTravel, ref bestHit, ref bestDistance, ref found);
        ConsiderSlideWall(-flatTravel, ref bestHit, ref bestDistance, ref found);
        ConsiderSlideWall(transform.right, ref bestHit, ref bestDistance, ref found);
        ConsiderSlideWall(-transform.right, ref bestHit, ref bestDistance, ref found);
        return found;
    }

    private void ConsiderSlideWall(Vector3 direction, ref RaycastHit bestHit, ref float bestDistance, ref bool found)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 castOrigin = myCol != null ? myCol.bounds.center : transform.position;
        if (!Physics.SphereCast(castOrigin, wallSphereRadius, direction.normalized,
                out RaycastHit hit, wallCheckDistance, groundMask, QueryTriggerInteraction.Ignore) ||
            !IsSlideWallSurface(hit.collider, hit.normal, transform) || hit.distance >= bestDistance)
            return;

        bestHit = hit;
        bestDistance = hit.distance;
        found = true;
    }

    public static bool IsSlideWallSurface(Collider candidate, Vector3 surfaceNormal, Transform playerRoot)
    {
        if (candidate == null || candidate.isTrigger || playerRoot == null ||
            candidate.transform.root == playerRoot.root || Mathf.Abs(surfaceNormal.y) >= 0.85f)
            return false;

        if (candidate.GetComponentInParent<PlayerMovement>() != null ||
            candidate.GetComponentInParent<NetworkProjectilePhysics>() != null ||
            candidate.GetComponentInParent<BoundaryHazard>() != null)
            return false;

        Rigidbody body = candidate.attachedRigidbody;
        if (body != null && !body.isKinematic)
            return false;

        if (candidate.CompareTag("Wall"))
            return true;

        Transform current = candidate.transform;
        while (current != null)
        {
            if (current.name == "Breakaway Platforms" || current.name == "Tier Transition Ramps")
                return true;
            current = current.parent;
        }

        return false;
    }

    public static bool IsWallJumpSurface(Collider candidate, Vector3 surfaceNormal, Transform playerRoot)
    {
        if (candidate == null || candidate.isTrigger || Mathf.Abs(surfaceNormal.y) > 0.25f)
            return false;
        if (playerRoot != null && candidate.transform.root == playerRoot.root)
            return false;

        if (candidate.CompareTag("Wall"))
            return true;

        // The Boundary floor is generated at runtime and its exposed sides are
        // legitimate wall-jump routes. Restrict the exception to the two floor
        // containers so hazards, projectiles, and other players cannot become
        // accidental wall-jump surfaces merely because they share a layer.
        Transform current = candidate.transform;
        while (current != null)
        {
            if (current.name == "Breakaway Platforms" || current.name == "Tier Transition Ramps")
                return true;
            current = current.parent;
        }

        return false;
    }

    private bool OnSlope()
    {
        float rayLen = 1.2f;
        if (myCol != null)
            rayLen = myCol.bounds.extents.y + 0.6f;

        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, rayLen, groundMask, QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0f;
        }
        return false;
    }

    private Vector3 GetSlopeMoveDirection(Vector3 directionNormalized)
    {
        Vector3 p = Vector3.ProjectOnPlane(directionNormalized, slopeHit.normal);
        if (p.sqrMagnitude < 0.0001f) return Vector3.zero;
        return p.normalized;
    }
    private void OnCollisionEnter(Collision collision)
{
    if (!isOwner) return;

    if (slideAbility != null && slideAbility.HandleObstacleCollision(collision))
        return;

    // If we hit a ceiling or steep wall, release suppression
    foreach (ContactPoint contact in collision.contacts)
    {
        float angle = Vector3.Angle(contact.normal, Vector3.down);
        // Normal pointing down = ceiling hit
        // Normal pointing mostly horizontal = wall hit  
        if (angle < 45f || (angle > 60f && angle < 120f))
        {
            if (MovementSuppressed && movementSuppressionAllowsEnvironmentalRelease)
            {
                bool isSliding = slideAbility != null && slideAbility.IsActive;
                if (!isSliding)
                {
                    SetMovementSuppressed(false, -1f);
                }
            }
        }
    }
}
}
