using UnityEngine;

public class SlideAbility : MonoBehaviour, IAbility
{
    public AbilityId Id => AbilityId.Slide;
    public bool IsActive => active;

    [Header("Cooldown")]
    [SerializeField] private float cooldownSeconds = 1f;
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
        if (!pm.IsGrounded) return;
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

        active = true;
        endTime = Time.time + duration;

        pm.SetMovementSuppressed(true, slideSpeedCap);

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
        if (!pm.IsGrounded) { Stop(); return; }
        if (pm.JumpPressedThisFrame) { Stop(); return; }

        MaintainSlideSpeed();
        rb.AddForce(Vector3.down * 10f, ForceMode.Acceleration);
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
        if (pm != null) pm.SetMovementSuppressed(false, -1f);
        if (visuals != null) visuals.localScale = originalVisualScale;
    }

    protected bool CooldownReady() => Time.time >= nextReadyTime;
    protected void StartCooldown() => nextReadyTime = Time.time + cooldownSeconds;
}
