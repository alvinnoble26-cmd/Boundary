using UnityEngine;
using PurrNet;

public class TeleportAbility : MonoBehaviour, IAbility
{
    public AbilityId Id => AbilityId.Teleport;
    public float CooldownDuration => cooldownSeconds;

    [Header("Cooldown")]
    [SerializeField] private float cooldownSeconds = 4f;
    private float nextReadyTime;

    [Header("Refs")]
    [SerializeField] private Transform orientation;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private CapsuleCollider capsule;

    [Header("VFX")]
    [SerializeField] private ParticleSystem teleportStartVFX;
    [SerializeField] private ParticleSystem teleportEndVFX;
    [SerializeField] private ParticleSystem teleportFailVFX;
    [SerializeField] private float vfxLifetime = 2f;
    [SerializeField] private float vfxYOffset = 0.0f;

    [Header("Teleport")]
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float skin = 0.05f;
    [SerializeField] private int backoffSteps = 10;

    [Header("Ground Snap")]
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private float groundRayUp = 1.0f;
    [SerializeField] private float groundRayDown = 3.0f;

    private PlayerAbilities playerAbilities;

    void Awake()
    {
        playerAbilities = GetComponentInParent<PlayerAbilities>();
        TryResolveRefs();
    }

    private void TryResolveRefs()
    {
        if (orientation == null)
        {
            var pm = GetComponentInParent<PlayerMovement>();
            orientation = pm?.orientation ?? transform;
        }
        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();
        if (capsule == null)
            capsule = GetComponentInParent<CapsuleCollider>();
        if (playerAbilities == null)
            playerAbilities = GetComponentInParent<PlayerAbilities>();
    }

    public void Activate()
    {
        // Always check cooldown FIRST before anything else
        if (!CooldownReady()) return;

        // Resolve refs if missing
        TryResolveRefs();

        if (orientation == null || rb == null || capsule == null)
        {
            Debug.LogWarning("[TeleportAbility] Missing refs, cannot activate.");
            return;
        }

        Vector3 start = rb.position;
        Vector3 dir = orientation.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        Vector3 destination = CalculateDestination(start, dir);

        // Start cooldown immediately regardless of success/fail
        StartCooldown();

        if (destination == start)
        {
            SpawnVFX(teleportFailVFX, start, dir);
            return;
        }

        SpawnVFX(teleportStartVFX, start, dir);

        rb.position = destination;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        SpawnVFX(teleportEndVFX, destination, dir);
        SfxManager.PlayTeleport();
    }

    private Vector3 CalculateDestination(Vector3 start, Vector3 dir)
    {
        Vector3 intended = start + dir * maxDistance;

        // Interior walls, movable cubes, players, and black holes are meant to
        // be phased through. Only an explicitly marked perimeter wall can
        // shorten the horizontal teleport.
        if (TryFindBoundaryHit(start, dir, maxDistance,
                TeleportArenaBoundary.SurfaceType.OuterWall, WorldCapsuleRadius(),
                out RaycastHit wallHit))
            intended = start + dir * Mathf.Max(0f, wallHit.distance - skin);

        if (snapToGround)
            intended = SnapToGround(intended);

        if (CanStandAt(intended))
            return intended;

        float step = maxDistance / Mathf.Max(1, backoffSteps);
        Vector3 p = intended;

        for (int i = 0; i < backoffSteps; i++)
        {
            p -= dir * step;
            if (snapToGround) p = SnapToGround(p);
            if (CanStandAt(p)) return p;
        }

        return start;
    }

    private bool CanStandAt(Vector3 pos)
    {
        float radius = Mathf.Max(0.01f, capsule.radius - skin);
        float height = Mathf.Max(capsule.height, radius * 2f);
        Vector3 center = pos + capsule.center;
        float half = (height * 0.5f) - radius;
        Vector3 p1 = center + Vector3.up * half;
        Vector3 p2 = center - Vector3.up * half;

        Collider[] hits = Physics.OverlapCapsule(p1, p2, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (c == null) continue;
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (FindBoundary(c) != null) return false;
        }
        return true;
    }

    private Vector3 SnapToGround(Vector3 pos)
    {
        Vector3 origin = pos + Vector3.up * groundRayUp;
        if (TryFindBoundaryHit(origin, Vector3.down, groundRayUp + groundRayDown,
                TeleportArenaBoundary.SurfaceType.Floor, 0f, out RaycastHit hit))
        {
            // rb.position is at the capsule transform, not at its feet.
            pos.y = hit.point.y - CapsuleBottomOffset() + skin;
        }
        return pos;
    }

    private bool TryFindBoundaryHit(Vector3 origin, Vector3 direction, float distance,
        TeleportArenaBoundary.SurfaceType requiredSurface, float castRadius,
        out RaycastHit closestHit)
    {
        RaycastHit[] hits = castRadius > 0f
            ? Physics.SphereCastAll(origin, castRadius, direction, distance, ~0,
                QueryTriggerInteraction.Ignore)
            : Physics.RaycastAll(origin, direction, distance, ~0,
                QueryTriggerInteraction.Ignore);
        float closestDistance = float.PositiveInfinity;
        closestHit = default;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit candidate = hits[i];
            TeleportArenaBoundary boundary = FindBoundary(candidate.collider);
            if (boundary == null || boundary.Surface != requiredSurface ||
                candidate.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = candidate.distance;
            closestHit = candidate;
            found = true;
        }

        return found;
    }

    private static TeleportArenaBoundary FindBoundary(Collider collider)
    {
        return collider != null
            ? collider.GetComponentInParent<TeleportArenaBoundary>()
            : null;
    }

    private float WorldCapsuleRadius()
    {
        Vector3 scale = capsule.transform.lossyScale;
        return capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
    }

    private float CapsuleBottomOffset()
    {
        Vector3 scale = capsule.transform.lossyScale;
        float radius = WorldCapsuleRadius();
        float height = Mathf.Max(capsule.height * Mathf.Abs(scale.y), radius * 2f);
        float centerY = capsule.center.y * scale.y;
        return centerY - height * 0.5f;
    }

    private void SpawnVFX(ParticleSystem prefab, Vector3 pos, Vector3 dir)
    {
        if (prefab == null) return;
        Vector3 spawnPos = pos + Vector3.up * vfxYOffset;
        Quaternion rot = dir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(dir, Vector3.up)
            : Quaternion.identity;
        ParticleSystem ps = Instantiate(prefab, spawnPos, rot);
        ps.Play();
        Destroy(ps.gameObject, vfxLifetime);
    }

    protected bool CooldownReady() => Time.time >= nextReadyTime;
    protected void StartCooldown() => nextReadyTime = Time.time + cooldownSeconds;
}
