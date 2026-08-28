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
    [SerializeField] private GameObject teleportMagicCirclePrefab;
    [SerializeField] private float vfxLifetime = 2f;
    [SerializeField] private float vfxYOffset = 0.0f;

    [Header("Teleport")]
    [SerializeField] private float maxDistance = 50f;
    [SerializeField] private float skin = 0.05f;
    [SerializeField] private int backoffSteps = 10;

    [Header("Legacy Ground Snap")]
    [SerializeField] private bool snapToGround = false;
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
    private GameObject windupFlashRoot;
    private GameObject teleportMagicCircleInstance;
    private LineRenderer[] windupRibbons;
    private Material teleportFlashMaterial;

    private static readonly Color TeleportCyan = new Color(0.05f, 0.9f, 1f, 0.92f);
    private static readonly Color TeleportMagenta = new Color(1f, 0.08f, 0.72f, 0.88f);
    private const float AfterimageLifetime = 0.55f;

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
        TryResolveRefs();
        Vector3 fallbackDirection = orientation != null ? orientation.forward : transform.forward;
        return TryPrepareWindup(rb != null ? rb.position : transform.position, fallbackDirection,
            out start, out destination, out dir);
    }

    public bool TryPrepareWindup(Vector3 aimOrigin, Vector3 aimDirection, out Vector3 start,
        out Vector3 destination, out Vector3 dir)
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
        // Preserve the complete crosshair direction so Teleport can travel up
        // or down as well as across the arena.
        dir = aimDirection;
        if (dir.sqrMagnitude < 0.0001f) return false;
        dir.Normalize();

        // Reject a fabricated camera origin far from this player, while still
        // allowing the real local camera height and shoulder offset.
        if ((aimOrigin - start).sqrMagnitude > 25f)
            aimOrigin = start;
        destination = CalculateDestination(start, aimOrigin, dir);

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

            Cam cameraController = playerAbilities != null
                ? playerAbilities.GetComponentInChildren<Cam>(true)
                : null;
            cameraController?.SetLookInputSuppressed(true);
            cameraController?.ShowTeleportArm();
        }
        SfxManager.PlayTeleportWindup();
        windupStartVFX = SpawnVFX(teleportStartVFX, start, dir);
        SpawnTeleportMagicCircle(start);
        BeginFlashPresentation(start);
    }

    public bool TryCompleteServerTeleport(ref Vector3 destination)
    {
        if (rb == null)
            return false;

        // Retain compatibility with older prefabs that enabled this serialized
        // option, without restoring forced floor-only teleporting.
        if (snapToGround)
            destination = ClampAboveFloor(destination, destination);
        rb.position = destination;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        return true;
    }

    public void CompleteServerTeleport(Vector3 destination)
    {
        TryCompleteServerTeleport(ref destination);
    }

    public void CompleteWindupPresentation(Vector3 destination, Vector3 dir)
    {
        // PlayerMovement keeps the owning client's Rigidbody simulation active.
        // Apply the server-approved observer destination locally as well, so
        // that simulation cannot overwrite the NetworkTransform correction
        // before its next replication tick arrives.
        if (playerMovement != null && playerMovement.isOwner && rb != null)
        {
            rb.position = destination;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        SpawnStartAfterimage();
        ClearWindupPresentation();
        SpawnVFX(teleportEndVFX, destination, dir);
        SpawnDestinationBurst(destination);
        SfxManager.PlayTeleport();
    }

    public void PlayFailurePresentation(Vector3 start, Vector3 dir)
    {
        ClearWindupPresentation();
        SpawnVFX(teleportFailVFX, start, dir);
        SfxManager.PlayTeleportFail();
    }

    private void Update()
    {
        if (!windupPresentationActive)
            return;

        float progress = Mathf.Clamp01((Time.time - windupStartedAt) / WindupDuration);
        if (tiltVisual != null)
            tiltVisual.localRotation = windupBaseRotation * Quaternion.Euler(0f, 360f * progress, 0f);
        UpdateFlashPresentation(progress);
    }

    private void ClearWindupPresentation()
    {
        if (windupStartVFX != null)
            Destroy(windupStartVFX.gameObject);
        windupStartVFX = null;
        if (windupFlashRoot != null)
            Destroy(windupFlashRoot);
        windupFlashRoot = null;
        windupRibbons = null;
        if (teleportMagicCircleInstance != null)
            Destroy(teleportMagicCircleInstance);
        teleportMagicCircleInstance = null;
        DestroyFlashMaterial();
        if (tiltVisual != null)
            tiltVisual.localRotation = windupBaseRotation;
        if (playerMovement != null && playerMovement.isOwner)
            playerMovement.SetMovementSuppressed(false);
        if (playerMovement != null && playerMovement.isOwner && playerAbilities != null)
            playerAbilities.GetComponentInChildren<Cam>(true)?.SetLookInputSuppressed(false);
        windupPresentationActive = false;
        tiltVisual = null;
    }

    private void SpawnTeleportMagicCircle(Vector3 position)
    {
        if (teleportMagicCirclePrefab == null)
            return;

        teleportMagicCircleInstance = Instantiate(teleportMagicCirclePrefab,
            position + Vector3.up * 0.035f, Quaternion.identity);
        teleportMagicCircleInstance.name = "Teleport Magic Circle";
        teleportMagicCircleInstance.transform.localScale = Vector3.one * 0.75f;
    }

    // The flash uses explicitly-created URP particle materials rather than an
    // imported prefab material. This avoids the magenta fallback caused by
    // Built-In-pipeline shaders in third-party VFX packages.
    private void BeginFlashPresentation(Vector3 start)
    {
        DestroyFlashMaterial();
        teleportFlashMaterial = CreateTeleportFlashMaterial();
        if (teleportFlashMaterial == null)
            return;

        windupFlashRoot = new GameObject("Teleport Windup Flash");
        windupFlashRoot.transform.position = start;
        CreateGroundRune(windupFlashRoot.transform);
        windupRibbons = new LineRenderer[3];
        for (int index = 0; index < windupRibbons.Length; index++)
            windupRibbons[index] = CreateRibbon(windupFlashRoot.transform, index);
    }

    private void UpdateFlashPresentation(float progress)
    {
        if (windupFlashRoot == null || windupRibbons == null)
            return;

        if (rb != null)
            windupFlashRoot.transform.position = rb.position;

        float radius = Mathf.Lerp(0.45f, 1.25f, progress);
        for (int ribbonIndex = 0; ribbonIndex < windupRibbons.Length; ribbonIndex++)
        {
            LineRenderer ribbon = windupRibbons[ribbonIndex];
            if (ribbon == null)
                continue;

            for (int pointIndex = 0; pointIndex < ribbon.positionCount; pointIndex++)
            {
                float t = pointIndex / (float)(ribbon.positionCount - 1);
                float angle = (t * Mathf.PI * 2.1f) +
                    (progress * Mathf.PI * 6f) + (ribbonIndex * Mathf.PI * 2f / 3f);
                Vector3 point = new Vector3(Mathf.Cos(angle) * radius,
                    Mathf.Lerp(0.08f, 2.25f, t), Mathf.Sin(angle) * radius);
                ribbon.SetPosition(pointIndex, point);
            }
        }
    }

    private void CreateGroundRune(Transform parent)
    {
        CreateRing(parent, "Outer Rune", 1.4f, 0.055f, TeleportMagenta, 48, 0f);
        CreateRing(parent, "Inner Rune", 0.82f, 0.038f, TeleportCyan, 32, 0.18f);

        for (int index = 0; index < 8; index++)
        {
            float angle = index * Mathf.PI * 2f / 8f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            LineRenderer glyph = CreateLine(parent, "Rune Glyph", 2, 0.05f,
                index % 2 == 0 ? TeleportCyan : TeleportMagenta);
            glyph.SetPosition(0, direction * 0.95f + Vector3.up * 0.025f);
            glyph.SetPosition(1, direction * 1.22f + Vector3.up * 0.025f);
        }
    }

    private void CreateRing(Transform parent, string effectName, float radius, float width,
        Color color, int points, float offset)
    {
        LineRenderer ring = CreateLine(parent, effectName, points + 1, width, color);
        for (int index = 0; index <= points; index++)
        {
            float angle = (index / (float)points * Mathf.PI * 2f) + offset;
            ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, 0.025f,
                Mathf.Sin(angle) * radius));
        }
    }

    private LineRenderer CreateRibbon(Transform parent, int index)
    {
        return CreateLine(parent, "Energy Ribbon " + index, 24, 0.09f,
            index % 2 == 0 ? TeleportCyan : TeleportMagenta);
    }

    private LineRenderer CreateLine(Transform parent, string effectName, int count, float width,
        Color color)
    {
        GameObject lineObject = new GameObject(effectName, typeof(LineRenderer));
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = count;
        line.widthMultiplier = width;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        line.material = teleportFlashMaterial;
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0f);
        return line;
    }

    private void SpawnDestinationBurst(Vector3 destination)
    {
        Material material = CreateTeleportFlashMaterial();
        if (material == null)
            return;

        GameObject burstObject = new GameObject("Teleport Destination Burst", typeof(ParticleSystem));
        burstObject.transform.position = destination + Vector3.up * 0.8f;
        ParticleSystem particles = burstObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(TeleportCyan, TeleportMagenta);
        main.maxParticles = 96;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 72) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;
        ParticleSystemRenderer renderer = burstObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        particles.Play();
        Destroy(burstObject, 1.2f);
        Destroy(material, 1.2f);
    }

    private void SpawnStartAfterimage()
    {
        if (windupFlashRoot == null || playerMovement == null)
            return;

        Transform visual = playerMovement.transform.Find("Visual");
        if (visual == null)
            return;

        Material material = CreateTeleportFlashMaterial();
        if (material == null)
            return;

        GameObject afterimage = new GameObject("Teleport Afterimage");
        afterimage.transform.SetPositionAndRotation(windupFlashRoot.transform.position,
            visual.rotation);
        afterimage.transform.localScale = visual.lossyScale;
        foreach (SkinnedMeshRenderer source in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Mesh bakedMesh = new Mesh { name = "Teleport Afterimage Mesh" };
            source.BakeMesh(bakedMesh);
            GameObject ghost = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
            ghost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            ghost.transform.SetParent(afterimage.transform, true);
            ghost.transform.localScale = source.transform.lossyScale;
            ghost.GetComponent<MeshFilter>().sharedMesh = bakedMesh;
            ghost.GetComponent<MeshRenderer>().sharedMaterial = material;
            Destroy(bakedMesh, AfterimageLifetime);
        }

        foreach (MeshRenderer source in visual.GetComponentsInChildren<MeshRenderer>())
        {
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
                continue;

            GameObject ghost = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
            ghost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            ghost.transform.SetParent(afterimage.transform, true);
            ghost.transform.localScale = source.transform.lossyScale;
            ghost.GetComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            ghost.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        Destroy(afterimage, AfterimageLifetime);
        Destroy(material, AfterimageLifetime);
    }

    private static Material CreateTeleportFlashMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            return null;

        Material material = new Material(shader) { name = "TeleportFlashURP" };
        material.SetColor("_BaseColor", Color.white);
        return material;
    }

    private void DestroyFlashMaterial()
    {
        if (teleportFlashMaterial != null)
            Destroy(teleportFlashMaterial);
        teleportFlashMaterial = null;
    }

    private Vector3 CalculateDestination(Vector3 start, Vector3 aimOrigin, Vector3 dir)
    {
        Vector3 intended = aimOrigin + dir * maxDistance;

        // The first valid floor or wall under the crosshair is the target. A
        // floor becomes a safe capsule position; a wall stays just in front of
        // its hit point so it cannot be crossed.
        if (TryFindAimSurface(aimOrigin, dir, out RaycastHit surfaceHit))
        {
            if (IsTeleportFloor(surfaceHit.collider))
            {
                intended = surfaceHit.point;
                intended.y = surfaceHit.point.y - CapsuleBottomOffset() + skin;
            }
            else
            {
                intended = surfaceHit.point - dir * (WorldCapsuleRadius() + skin);
            }
        }

        intended = ClampAboveFloor(start, intended);

        if (CanStandAt(intended))
            return intended;

        float step = maxDistance / Mathf.Max(1, backoffSteps);
        Vector3 p = intended;
        for (int i = 0; i < backoffSteps; i++)
        {
            p -= dir * step;
            if (CanStandAt(p)) return p;
        }

        return start;
    }

    private Vector3 ClampAboveFloor(Vector3 start, Vector3 destination)
    {
        // Test beneath the intended X/Z location, not only along the aim ray.
        // This catches shallow downward aims and nearby floor targets reliably.
        Vector3 origin = destination;
        origin.y = Mathf.Max(start.y, destination.y) + 64f;
        if (!TryFindArenaFloor(origin, 128f, out RaycastHit floorHit))
            return destination;

        float minimumCenterY = floorHit.point.y - CapsuleBottomOffset() + skin;
        if (destination.y < minimumCenterY)
            destination.y = minimumCenterY;
        return destination;
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
            TeleportArenaBoundary boundary = FindBoundary(c);
            // A floor at the capsule's feet is valid. Other marked boundary
            // surfaces remain invalid landing locations.
            if (boundary != null && boundary.Surface != TeleportArenaBoundary.SurfaceType.Floor)
                return false;
        }
        return true;
    }

    private Vector3 SnapToGround(Vector3 pos)
    {
        // Start above every tier so a player coming from the lower outer ring
        // can still find an elevated inner platform or its transition ramp.
        BoundaryMatchController match = BoundaryMatchController.Instance;
        float highestSurface = match != null
            ? Mathf.Max(match.OuterPlatformSurfaceY, match.MiddlePlatformSurfaceY,
                match.InnerPlatformSurfaceY)
            : pos.y;
        Vector3 origin = pos;
        origin.y = Mathf.Max(pos.y, highestSurface) + Mathf.Max(groundRayUp, 1f) + 0.5f;

        if (TryFindArenaFloor(origin, Mathf.Max(groundRayUp + groundRayDown, 32f), out RaycastHit floorHit))
        {
            pos.y = floorHit.point.y - CapsuleBottomOffset() + skin;
            return pos;
        }

        // The playable arena is tiered at runtime. Keep this deterministic
        // fallback for server/headless cases where a platform collider has not
        // been constructed yet.
        if (match != null)
        {
            Vector3 flatOffset = pos - match.ArenaCenter;
            flatOffset.y = 0f;
            pos.y = match.PlatformSurfaceYAtRadius(flatOffset.magnitude) -
                    CapsuleBottomOffset() + skin;
            return pos;
        }

        Vector3 fallbackOrigin = pos + Vector3.up * groundRayUp;
        if (TryFindBoundaryHit(fallbackOrigin, Vector3.down, groundRayUp + groundRayDown,
                TeleportArenaBoundary.SurfaceType.Floor, 0f, out RaycastHit hit))
        {
            // rb.position is at the capsule transform, not at its feet.
            pos.y = hit.point.y - CapsuleBottomOffset() + skin;
        }

        return pos;
    }

    private static bool TryFindArenaFloor(Vector3 origin, float distance, out RaycastHit closestHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0,
            QueryTriggerInteraction.Ignore);
        float closestDistance = float.PositiveInfinity;
        closestHit = default;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit candidate = hits[i];
            if (candidate.collider == null || candidate.normal.y < 0.5f ||
                candidate.distance >= closestDistance)
            {
                continue;
            }

            if (candidate.collider.GetComponentInParent<BoundaryBreakawayPlatform>() == null &&
                FindBoundary(candidate.collider)?.Surface != TeleportArenaBoundary.SurfaceType.Floor)
            {
                continue;
            }

            closestDistance = candidate.distance;
            closestHit = candidate;
            found = true;
        }

        return found;
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

    private bool TryFindAimSurface(Vector3 origin, Vector3 direction, out RaycastHit closestHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxDistance, ~0,
            QueryTriggerInteraction.Ignore);
        float closestDistance = float.PositiveInfinity;
        closestHit = default;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit candidate = hits[i];
            if (candidate.collider == null || candidate.distance >= closestDistance ||
                candidate.collider.transform.root == transform.root)
                continue;

            if (!IsTeleportFloor(candidate.collider) && !IsTeleportWall(candidate.collider))
                continue;

            closestDistance = candidate.distance;
            closestHit = candidate;
            found = true;
        }

        return found;
    }

    private static bool IsTeleportFloor(Collider collider)
    {
        return collider != null &&
               (collider.GetComponentInParent<BoundaryBreakawayPlatform>() != null ||
                FindBoundary(collider)?.Surface == TeleportArenaBoundary.SurfaceType.Floor);
    }

    private static bool IsTeleportWall(Collider collider)
    {
        TeleportArenaBoundary boundary = FindBoundary(collider);
        return collider != null &&
               ((boundary != null && boundary.Surface == TeleportArenaBoundary.SurfaceType.OuterWall) ||
                collider.CompareTag("Wall") || collider.transform.root.CompareTag("Wall"));
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
    protected void StartCooldown() =>
        nextReadyTime = Time.time + PlayerAbilities.GetPhaseAdjustedCooldown(cooldownSeconds);
}
