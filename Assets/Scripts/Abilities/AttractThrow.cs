using UnityEngine;
using PurrNet;

public class AttractThrow : MonoBehaviour, IAbility
{
    public AbilityId Id => AbilityId.AttractThrow;
    public float CooldownDuration => cooldown;

    [Header("Projectile Prefab")]
    [SerializeField] private GameObject objectToThrow;

    [Header("Throw Power")]
    [SerializeField] private float throwForce = 45f;
    [SerializeField] private float throwUpwardForce = 4f;
    [SerializeField] private float cooldown = 3f;
    [SerializeField, Min(0.5f)] private float launchHeightAbovePlayerCenter = 1.35f;

    [Header("Projectile Physics Boost")]
    [SerializeField] private float projectileMass = 3f;
    [SerializeField] private float projectileDrag = 0f;
    [SerializeField] private float projectileAngularDrag = 0.05f;
    [SerializeField] private bool useVelocityChange = true;

    private float nextReadyTime;

    private PlayerMovement pm;
    private Transform aimTransform;
    private ThrowPoint throwPoint;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Activate()
    {
        if (Time.time < nextReadyTime)
            return;

        ResolveReferences();

        if (aimTransform == null || throwPoint == null || objectToThrow == null)
        {
            Debug.LogError(
                $"[{name}] AttractThrow FAILED. " +
                $"aim={(aimTransform ? aimTransform.name : "NULL")} " +
                $"throwPoint={(throwPoint ? throwPoint.name : "NULL")} " +
                $"prefab={(objectToThrow ? objectToThrow.name : "NULL")}"
            );
            return;
        }

        nextReadyTime = Time.time + cooldown;
        ThrowOnce();
    }

    private void ResolveReferences()
    {
        if (pm == null)
            pm = GetComponentInParent<PlayerMovement>();

        if (aimTransform == null && pm != null)
            aimTransform = pm.orientation != null ? pm.orientation : pm.transform;

        if (throwPoint == null)
            throwPoint = transform.root.GetComponentInChildren<ThrowPoint>(true);
    }

    private void ThrowOnce()
    {
        Vector3 dir = GetAimDirection();
        Vector3 spawnPos = throwPoint.transform.position;
        ThrowOnce(spawnPos, dir);
    }

    public void ActivateFromNetwork(Vector3 spawnPos, Vector3 dir)
    {
        if (Time.time < nextReadyTime)
            return;

        nextReadyTime = Time.time + cooldown;
        ThrowOnce(spawnPos, dir);
    }

    private void ThrowOnce(Vector3 spawnPos, Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;
        dir.Normalize();
        PlayerMovement ownerPm = GetComponentInParent<PlayerMovement>();
        Transform owner = ownerPm != null ? ownerPm.transform : transform.root;
        GameObject projectile = ProjectileLaunchUtility.InstantiateSafely(
            objectToThrow, owner, spawnPos, dir, launchHeightAbovePlayerCenter, true);
        if (projectile == null)
            return;

        NetworkIdentity.Spawn(projectile, objectToThrow);

        Rigidbody rb = projectile.GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogWarning("[AttractThrow] Projectile has no Rigidbody.");
            return;
        }

        SetupProjectileRigidbody(rb);

        Vector3 launchVelocity = dir * throwForce + Vector3.up * throwUpwardForce;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (useVelocityChange)
            rb.AddForce(launchVelocity, ForceMode.VelocityChange);
        else
            rb.AddForce(launchVelocity, ForceMode.Impulse);

        Debug.Log("[AttractThrow] Fired attract projectile with force: " + launchVelocity);
    }

    private Vector3 GetAimDirection()
    {
        Vector3 dir = aimTransform != null ? aimTransform.forward : transform.forward;

        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        dir.Normalize();
        return dir;
    }

    private void SetupProjectileRigidbody(Rigidbody rb)
    {
        rb.mass = projectileMass;
        rb.linearDamping = projectileDrag;
        rb.angularDamping = projectileAngularDrag;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }
}
