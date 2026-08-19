using UnityEngine;

public class SlideAbility : MonoBehaviour, IAbility
{
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
    [SerializeField] private Transform trailSpawnPoint;
    [SerializeField] private float trailLifetime = 0.6f;

    private Rigidbody rb;
    private Vector3 originalVisualScale;
    private Quaternion originalTiltLocalRot;
    private bool active;
    private float endTime;
    private Vector3 dir;
    private bool wallSliding;
    private bool slideJumpExecuted;
    private Vector3 lastWallNormal;

    void Awake()
    {
        if (pm == null) pm = GetComponentInParent<PlayerMovement>();
        rb = pm ? pm.rb : null;

        if (visuals == null) visuals = transform;
        if (tiltVisual == null) tiltVisual = visuals;

        originalVisualScale = visuals.localScale;
        originalTiltLocalRot = tiltVisual.localRotation;

        if (trailSpawnPoint == null) trailSpawnPoint = transform;
    }

    public void Activate()
    {
        if (!CooldownReady()) return;

        if (pm == null) pm = GetComponentInParent<PlayerMovement>();
        rb = pm ? pm.rb : null;

        if (pm == null || rb == null) return;
        if (active) return;

        Transform basis = pm.orientation ? pm.orientation : pm.transform;
        Vector2 inp = pm.moveInput;

        Vector3 inputDir = (basis.forward * inp.y + basis.right * inp.x);
        inputDir.y = 0f;

        if (inputDir.sqrMagnitude > 0.001f)
            dir = inputDir.normalized;
        else
        {
            dir = pm.transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
        }

        bool hasFloorSupport = pm.IsGrounded;
        RaycastHit wallHit = default;
        bool hasWallSupport = !hasFloorSupport && pm.TryFindSlideWall(dir, out wallHit);
        if (!hasFloorSupport && !hasWallSupport)
            return;

        active = true;
        wallSliding = hasWallSupport;
        slideJumpExecuted = false;
        if (hasWallSupport)
        {
            lastWallNormal = wallHit.normal;
            dir = SelectHorizontalWallTangent(dir, wallHit.normal);
        }
        endTime = Time.time + duration;

        pm.SetMovementSuppressed(true, slideSpeedCap, false);

        if (visuals != null)
            visuals.localScale = new Vector3(originalVisualScale.x, crouchYScale, originalVisualScale.z);

        SpawnTrail();
        ApplyInstantSlideKick();
        StartCooldown();
        SfxManager.PlaySlide();
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
            ExecuteSlideJump(true);
            return;
        }

        MaintainSlideSpeed();
        if (!wallSliding)
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
        return normalJumpForce * 1.5f;
    }

    void Update()
    {
        if (tiltVisual == null) return;

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

    private void ApplyInstantSlideKick()
    {
        Vector3 v = rb.linearVelocity;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        float target = Mathf.Clamp(slideStartSpeed, 0f, slideSpeedCap);
        Vector3 desiredFlat = dir * target;
        float along = Vector3.Dot(flat, dir);
        if (along > target) desiredFlat = dir * Mathf.Min(along, slideSpeedCap);
        rb.linearVelocity = new Vector3(desiredFlat.x, v.y, desiredFlat.z);
    }

    private void MaintainSlideSpeed()
    {
        Vector3 v = rb.linearVelocity;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        float along = Vector3.Dot(flat, dir);
        float target = slideSpeedCap;
        float delta = target - along;

        if (delta <= 0f) { ClampPlanarSpeed(slideSpeedCap); return; }

        rb.AddForce(dir * (delta * maintainAccel), ForceMode.Acceleration);
        ClampPlanarSpeed(slideSpeedCap);
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

    private void ExecuteSlideJump(bool automatic)
    {
        if (slideJumpExecuted || rb == null || pm == null)
            return;

        slideJumpExecuted = true;
        Vector3 outwardImpulse = Vector3.zero;
        if (automatic && wallSliding)
        {
            Vector3 flatNormal = Vector3.ProjectOnPlane(lastWallNormal, Vector3.up);
            if (flatNormal.sqrMagnitude > 0.0001f)
                outwardImpulse = flatNormal.normalized * (slideStartSpeed * 0.5f);
        }

        Stop();
        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(velocity.x, 0f, velocity.z);
        rb.AddForce(Vector3.up * SlideJumpUpwardImpulse(pm.jumpForce) + outwardImpulse, ForceMode.Impulse);
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

    private void Stop()
    {
        active = false;
        wallSliding = false;
        lastWallNormal = Vector3.zero;
        if (pm != null) pm.SetMovementSuppressed(false, -1f);
        if (visuals != null) visuals.localScale = originalVisualScale;
    }

    private void OnDisable()
    {
        if (active)
            Stop();
    }

    protected bool CooldownReady() => Time.time >= nextReadyTime;
    protected void StartCooldown() => nextReadyTime = Time.time + cooldownSeconds;
}
