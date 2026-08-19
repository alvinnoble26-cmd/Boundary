using System.Collections;
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
    private Coroutine pendingTeleport;
    private PlayerMovement playerMovement;
    private Transform tiltVisual;
    private Quaternion windupBaseRotation;
    private bool windupPresentationActive;
    private float windupStartedAt;
    private ParticleSystem windupStartVFX;

    public const float WindupDuration = 0.5f;

    void Awake()
    {
        playerAbilities = GetComponentInParent<PlayerAbilities>();
        TryResolveRefs();
    }

    private void OnDisable()
    {
        // Unity stops this component's coroutines on disable. Clear the handle
        // so a respawn or scene re-entry cannot retain a stale pending state.
        pendingTeleport = null;
        ClearWindupPresentation();
    }

    private void TryResolveRefs()
    {
        if (orientation == null)
        {
            playerMovement = GetComponentInParent<PlayerMovement>();
            orientation = playerMovement?.orientation ?? transform;
        }
        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();
        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();
        if (capsule == null)
            capsule = GetComponentInParent<CapsuleCollider>();
        if (playerAbilities == null)
            playerAbilities = GetComponentInParent<PlayerAbilities>();
    }

    public bool TryPrepareWindup(out Vector3 start, out Vector3 destination, out Vector3 dir)
    {
        start = Vector3.zero;
        destination = Vector3.zero;
        dir = Vector3.forward;
        if (!CooldownReady()) return false;

        // Resolve refs if missing
        TryResolveRefs();

        if (orientation == null || rb == null || capsule == null)
        {
            Debug.LogWarning("[TeleportAbility] Missing refs, cannot activate.");
            return false;
        }

        start = rb.position;
        dir = orientation.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return false;
        dir.Normalize();

        destination = CalculateDestination(start, dir);

        // Start cooldown immediately regardless of success/fail
        StartCooldown();

        if (destination == start)
        {
            return false;
        }

        return true;
    }

    // Kept for the existing non-networked activation contract. Multiplayer
    // Teleport uses PlayerAbilities' server-owned wind-up sequence instead.
    public void Activate()
    {
        if (!TryPrepareWindup(out Vector3 start, out Vector3 destination, out Vector3 dir))
        {
            SpawnVFX(teleportFailVFX, rb != null ? rb.position : transform.position, dir);
            return;
        }

        BeginWindupPresentation(start, dir);
        pendingTeleport = StartCoroutine(CompleteTeleportAfterWindUp(start, destination, dir));
    }

    private IEnumerator CompleteTeleportAfterWindUp(Vector3 start, Vector3 destination, Vector3 dir)
    {
        yield return new WaitForSeconds(WindupDuration);

        CompleteServerTeleport(destination);

        CompleteWindupPresentation(destination, dir);
        pendingTeleport = null;
    }

    public void BeginWindupPresentation(Vector3 start, Vector3 dir)
    {
        if (windupPresentationActive)
            return;

        TryResolveRefs();
        Transform visual = playerMovement != null ? playerMovement.transform.Find("Visual") : null;
        tiltVisual = visual != null ? visual.Find("Tilt") : null;
        windupBaseRotation = tiltVisual != null ? tiltVisual.localRotation : Quaternion.identity;
        windupPresentationActive = true;
        windupStartedAt = Time.time;

        if (playerMovement != null && playerMovement.isOwner)
        {
            playerMovement.SetMovementSuppressed(true, -1f, false);
            if (rb != null)
                rb.linearVelocity = Vector3.zero;
        }

        Cam cameraController = playerAbilities != null
            ? playerAbilities.GetComponentInChildren<Cam>(true)
            : null;
        cameraController?.SetLookInputSuppressed(true);
        cameraController?.ShowTeleportArm();
        windupStartVFX = SpawnVFX(teleportStartVFX, start, dir);
    }

    public void CompleteServerTeleport(Vector3 destination)
    {
        if (rb == null)
            return;

        rb.position = destination;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public void CompleteWindupPresentation(Vector3 destination, Vector3 dir)
    {
        ClearWindupPresentation();
        SpawnVFX(teleportEndVFX, destination, dir);
        SfxManager.PlayTeleport();
    }

    public void PlayFailurePresentation(Vector3 start, Vector3 dir)
    {
        ClearWindupPresentation();
        SpawnVFX(teleportFailVFX, start, dir);
    }

    private void Update()
    {
        if (!windupPresentationActive || tiltVisual == null)
            return;

        float progress = Mathf.Clamp01((Time.time - windupStartedAt) / WindupDuration);
        tiltVisual.localRotation = windupBaseRotation * Quaternion.Euler(0f, 360f * progress, 0f);
    }

    private void ClearWindupPresentation()
    {
        if (windupStartVFX != null)
            Destroy(windupStartVFX.gameObject);
        windupStartVFX = null;
        if (tiltVisual != null)
            tiltVisual.localRotation = windupBaseRotation;
        if (playerMovement != null && playerMovement.isOwner)
            playerMovement.SetMovementSuppressed(false);
        if (playerAbilities != null)
            playerAbilities.GetComponentInChildren<Cam>(true)?.SetLookInputSuppressed(false);
        windupPresentationActive = false;
        tiltVisual = null;
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

    private ParticleSystem SpawnVFX(ParticleSystem prefab, Vector3 pos, Vector3 dir)
    {
        if (prefab == null) return null;
        Vector3 spawnPos = pos + Vector3.up * vfxYOffset;
        Quaternion rot = dir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(dir, Vector3.up)
            : Quaternion.identity;
        ParticleSystem ps = Instantiate(prefab, spawnPos, rot);
        ps.Play();
        Destroy(ps.gameObject, vfxLifetime);
        return ps;
    }

    protected bool CooldownReady() => Time.time >= nextReadyTime;
    protected void StartCooldown() => nextReadyTime = Time.time + cooldownSeconds;
}
