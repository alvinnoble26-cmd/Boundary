using UnityEngine;

public class DashAbility : MonoBehaviour, IAbility
{
    public AbilityId Id => AbilityId.Dash;
    public float CooldownDuration => cooldownSeconds;

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
    [SerializeField] private float trailLifetime = 0.4f;

    private bool active;
    private float endTime;
    private Vector3 dashDir;

    private Quaternion originalTiltLocalRot;

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
        nextReadyTime = Time.time + cooldownSeconds;

        if (active) return;

        dashDir = orientation.forward;
        if (flattenY) dashDir.y = 0f;
        if (dashDir.sqrMagnitude < 0.0001f) return;
        dashDir.Normalize();

        active = true;
        endTime = Time.time + duration;
        SpawnTrail();
        SfxManager.PlayDash();

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
        if (tiltVisual == null) return;

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

        tiltVisual.localRotation = Quaternion.Slerp(tiltVisual.localRotation, target, leanLerp * Time.deltaTime);
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

    private void Stop()
    {
        active = false;
        if (pm != null) pm.SetMovementSuppressed(false, -1f);
    }
}
