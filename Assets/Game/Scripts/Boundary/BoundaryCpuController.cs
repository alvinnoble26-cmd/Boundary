using PurrNet;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A local-host-only brain on the existing player prefab. Decision ticks are
/// bounded to 10 Hz; normal PlayerMovement owns physics, jumping and forces.
/// No NetworkBehaviour, RPC, extra client, service or downloaded model is added.
/// </summary>
[DisallowMultipleComponent]
public sealed class BoundaryCpuController : MonoBehaviour
{
    private const float ThinkInterval = 0.1f;
    private static int previousLoadoutMask = -1;
    private struct Threat
    {
        public Vector3 position;
        public Vector3 velocity;
        public float radius;
    }

    private readonly Collider[] surroundings = new Collider[96];
    private readonly Threat[] threats = new Threat[96];
    private readonly RaycastHit[] castHits = new RaycastHit[32];
    private PlayerMovement movement;
    private BoundaryPlayerState state;
    private BoundaryPlayerState opponent;
    private PlayerMovement opponentMovement;
    private PlayerAbilities abilities;
    private PlayerAbilities opponentAbilities;
    private AbilityId[] loadout;
    private int nearbyCount;
    private float nextThinkAt;
    private float nextAbilityAt;
    private float nextJumpAt;
    private float stuckSince;
    private float lastUnstickAt;
    private Vector3 lastPosition;
    private Vector3 selectedDirection;
    private float strafeSign;
    private bool initialized;

    public static bool TrySpawn(PlayerMovement human)
    {
        GameManager game = GameManager.I;
        NetworkManager net = NetworkManager.main;
        if (game == null || !game.IsCpuPractice || net == null || !net.isServer || !net.isClient ||
            human == null || !human.isOwner || human.IsCpuControlled) return false;
        BoundaryCpuController existing = FindFirstObjectByType<BoundaryCpuController>();
        if (existing != null)
            return existing.initialized && existing.movement.isSpawned &&
                !existing.movement.hasOwner && existing.movement.HasSimulationAuthority;
        if (!net.prefabProvider.TryGetPrefabData(human.prefabId, out PrefabData data)) return false;

        Vector3 position = human.transform.position;
        Quaternion rotation = Quaternion.identity;
        float greatestDistance = 0f;
        foreach (GameObject spawn in GameObject.FindGameObjectsWithTag("SpawnPoint"))
        {
            if (spawn.scene != human.gameObject.scene) continue;
            float distance = (spawn.transform.position - human.transform.position).sqrMagnitude;
            if (distance <= greatestDistance) continue;
            greatestDistance = distance;
            position = spawn.transform.position;
            rotation = spawn.transform.rotation;
        }
        if (greatestDistance < 4f) return false;

        // Instantiate directly so configuration happens before network callbacks.
        // Retain the registered prefab's complete identity layout and references.
        GameObject cpu = UnityProxy.InstantiateDirectly(data.prefab, position, rotation);
        SceneManager.MoveGameObjectToScene(cpu, human.gameObject.scene);
        cpu.name = "CPU (Beard)";
        BoundaryCpuController controller = cpu.AddComponent<BoundaryCpuController>();
        if (!controller.Initialize(human))
        {
            UnityProxy.DestroyDirectly(cpu);
            return false;
        }
        NetworkIdentity.Spawn(cpu, data.prefab, net);
        // The project's host spawn rules initially assign the local player.
        // CPU-marked setup callbacks ignore that transient ownership.
        cpu.GetComponent<NetworkIdentity>().RemoveOwnership(propagateToChildren: true);
        Debug.Log("[CPU] Beard opponent spawned with " + string.Join(", ", controller.loadout));
        return true;
    }

    private bool Initialize(PlayerMovement human)
    {
        movement = GetComponent<PlayerMovement>();
        state = GetComponent<BoundaryPlayerState>();
        abilities = GetComponent<PlayerAbilities>();
        opponentMovement = human;
        opponent = human.GetComponent<BoundaryPlayerState>();
        opponentAbilities = human.GetComponent<PlayerAbilities>();
        if (movement == null || state == null || abilities == null || opponent == null) return false;
        movement.ConfigureCpuControl();
        for (int attempt = 0; attempt < 12; attempt++)
        {
            loadout = BoundaryCpuRules.RandomLoadout(Random.Range(0, int.MaxValue));
            if (BoundaryCpuRules.LoadoutMask(loadout) != previousLoadoutMask)
                break;
        }
        previousLoadoutMask = BoundaryCpuRules.LoadoutMask(loadout);
        if (!abilities.ConfigureCpuLoadout(loadout)) return false;
        foreach (Camera camera in GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = false;
            camera.tag = "Untagged";
        }
        foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        strafeSign = Random.value < 0.5f ? -1f : 1f;
        lastPosition = transform.position;
        stuckSince = Time.time;
        initialized = true;
        return true;
    }

    private void FixedUpdate()
    {
        if (!initialized || !movement.HasSimulationAuthority || Time.time < nextThinkAt) return;
        nextThinkAt = Time.time + ThinkInterval;
        BoundaryMatchController match = BoundaryMatchController.Instance;
        if (opponent == null || state.State == BoundaryKnockoutState.Consumed ||
            opponent.State == BoundaryKnockoutState.Consumed || match == null || match.Phase == BoundaryPhase.Waiting ||
            GameManager.I == null || GameManager.I.State != GameManager.GameState.Playing)
        {
            movement.SetCpuInput(Vector3.zero, false);
            return;
        }

        Vector3 position = transform.position;
        Vector3 enemyPosition = opponent.transform.position;
        Vector3 enemyVelocity = opponentMovement.rb != null ? opponentMovement.rb.linearVelocity : Vector3.zero;
        float distance = Vector3.Distance(position, enemyPosition);
        float safeRadius = GetSafeRadius(match);
        float preferredRange = HasAbility(AbilityId.Slice) ? 5f : HasAbility(AbilityId.Hollow) ? 18f : 13f;
        if (opponent.IsServerInvulnerable || state.CurrentHealth < 25f) preferredRange += 13f;
        if (opponentAbilities != null && opponentAbilities.HasEquippedAbility(AbilityId.Slice) && distance < 7f &&
            !HasAbility(AbilityId.Slice)) preferredRange = 15f;

        Vector3 toEnemy = Vector3.ProjectOnPlane(enemyPosition - position, Vector3.up).normalized;
        Vector3 tangent = Vector3.Cross(Vector3.up, toEnemy) * strafeSign;
        Vector3 desired = (toEnemy * Mathf.Clamp((distance - preferredRange) / 8f, -1f, 1f) + tangent * 0.65f).normalized;
        Vector3 inward = Vector3.ProjectOnPlane(match.ArenaCenter - position, Vector3.up).normalized;
        float radial = Vector3.ProjectOnPlane(position - match.ArenaCenter, Vector3.up).magnitude;
        float expectedFloorY = match.PlatformSurfaceYAtRadius(radial);
        bool belowStage = position.y < expectedFloorY - PlayerMovement.StandingCenterHeight * 0.65f;
        bool landingMissing = !TryFloor(position +
            Vector3.ProjectOnPlane(movement.rb != null ? movement.rb.linearVelocity : Vector3.zero,
                Vector3.up) * 0.55f, out _);
        if (belowStage || landingMissing || radial > safeRadius)
            desired = inward;
        if (Vector3.ProjectOnPlane(position - lastPosition, Vector3.up).sqrMagnitude > 0.6f)
        {
            stuckSince = Time.time;
            lastPosition = position;
        }
        bool stuck = Time.time - stuckSince > 1.1f;
        if (stuck)
        {
            if (Time.time - lastUnstickAt > 0.8f)
            {
                strafeSign *= -1f;
                lastUnstickAt = Time.time;
                tangent = -tangent;
            }
            Vector3 wallEscape = FindWallEscape(position);
            desired = (desired + tangent + movement.WallRunNormal * 1.4f +
                wallEscape * 1.8f).normalized;
        }

        SenseThreats(position);
        float bestScore = float.NegativeInfinity;
        Vector3 best = Vector3.zero;
        bool bestNeedsJump = false;
        // A stationary candidate lets the CPU brake rather than run into danger.
        for (int index = 0; index <= 16; index++)
        {
            float angle = index * Mathf.PI / 8f;
            Vector3 candidate = index == 16 ? Vector3.zero : new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            float score = ScoreMovement(candidate, desired, safeRadius, match, out bool needsJump);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
                bestNeedsJump = needsJump;
            }
        }
        if (movement.CanWallJump && (bestNeedsJump || stuck))
            best = (best + movement.WallRunNormal * 1.8f + inward * 0.4f).normalized;
        selectedDirection = best;
        Vector3 facing = toEnemy.sqrMagnitude > 0.01f ? toEnemy : best;
        if (facing.sqrMagnitude > 0.01f) Face(facing);
        bool jump = (movement.IsGrounded || movement.CanWallJump) &&
            !movement.MovementSuppressed && Time.time >= nextJumpAt &&
            (bestNeedsJump || stuck || (movement.IsWallRunning && landingMissing));
        if (jump) nextJumpAt = Time.time + 0.65f;
        movement.SetCpuInput(best, jump);
        if (Time.time >= nextAbilityAt && !movement.MovementSuppressed)
            ChooseAbility(enemyPosition, enemyVelocity, distance, match, safeRadius);
        // Ability aiming may rotate the movement basis; keep world steering stable.
        movement.SetCpuInput(best, false);
    }

    private bool HasAbility(AbilityId id)
    {
        for (int i = 0; i < loadout.Length; i++) if (loadout[i] == id) return true;
        return false;
    }

    private static float GetSafeRadius(BoundaryMatchController match)
    {
        float next = match.Phase == BoundaryPhase.OuterRing ? match.MiddleRadius : match.InnerRadius;
        if (match.Phase == BoundaryPhase.InnerRing) return match.RingRadius - 5f;
        if (match.Transition != BoundaryTransition.None)
            return Mathf.Max(5f, next - 7f);
        return BoundaryCpuRules.SafeRadius(match.RingRadius, next, match.PhaseTimeRemaining, 7f);
    }

    private float ScoreMovement(Vector3 direction, Vector3 desired, float safeRadius,
        BoundaryMatchController match, out bool needsJump)
    {
        const float horizon = 0.65f;
        Vector3 position = transform.position;
        Vector3 velocity = direction * movement.maxSpeed * movement.ExternalSpeedMultiplier;
        Vector3 future = position + velocity * horizon;
        // Include current momentum: a turn cannot instantly cancel an enemy shove.
        if (movement.rb != null) future += Vector3.ProjectOnPlane(movement.rb.linearVelocity, Vector3.up) * 0.16f;
        float radialDistance = Vector3.ProjectOnPlane(future - match.ArenaCenter, Vector3.up).magnitude;
        float score = Vector3.Dot(direction, desired) * 6f + Vector3.Dot(direction, selectedDirection) * 1.2f;
        score -= Mathf.Max(0f, radialDistance - safeRadius) * 15f;
        // Remain clear of the elevated event horizon's capture column.
        if (position.y > match.SingularityPosition.y - 10f)
            score -= Mathf.Max(0f, 13f - radialDistance) * 25f;
        needsJump = false;
        if (!TryFloor(future, out RaycastHit floor)) return score - 450f;
        float rise = floor.point.y - (position.y - PlayerMovement.StandingCenterHeight);
        if (rise > 2.5f) score -= 250f;
        else if (rise > 0.25f) needsJump = true;
        // A safe landing beyond a short gap still requires a jump at the lip.
        if (!TryFloor(Vector3.Lerp(position, future, 0.5f), out _)) needsJump = true;
        Vector3 longFuture = position + velocity * 1.25f;
        if (movement.rb != null)
            longFuture += Vector3.ProjectOnPlane(movement.rb.linearVelocity, Vector3.up) * 0.2f;
        if (!TryFloor(longFuture, out _))
            score -= 260f;
        BoundaryBreakawayPlatform platform = floor.collider.GetComponentInParent<BoundaryBreakawayPlatform>();
        if (platform != null)
            score -= Mathf.Max(0, match.PlatformContactCount(platform.PlatformIndex) - 2) * 18f;
        if (direction.sqrMagnitude > 0.01f)
        {
            int count = Physics.SphereCastNonAlloc(
                position + Vector3.up * PlayerMovement.ScaleDistance(0.25f),
                PlayerMovement.ScaleDistance(0.5f), direction, castHits,
                PlayerMovement.ScaleDistance(2f), ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = castHits[i].collider;
                if (obstacle.transform.root == transform || obstacle.GetComponentInParent<PlayerMovement>() != null ||
                    castHits[i].normal.y > 0.65f) continue;
                needsJump = true;
                score -= obstacle.bounds.max.y > position.y + 1.8f ? 80f : 7f;
            }
        }
        Vector3 center = position + Vector3.up *
            PlayerMovement.ScaleDistance(HollowAbility.TargetCenterHeight);
        for (int i = 0; i < nearbyCount; i++)
        {
            Threat threat = threats[i];
            score -= BoundaryCpuRules.MovingThreat(center, velocity, threat.position,
                threat.velocity, threat.radius, horizon) * 130f;
        }
        if (opponentAbilities != null) score -= opponentAbilities.CpuDangerAt(center, velocity, horizon);
        // Own Charge explosions also damage the caster.
        score -= abilities.CpuDangerAt(center, velocity, horizon, selfDamageOnly: true);
        return score;
    }

    private void SenseThreats(Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(position, 18f, surroundings, ~0, QueryTriggerInteraction.Collide);
        nearbyCount = 0;
        for (int i = 0; i < count; i++)
        {
            Collider hazard = surroundings[i];
            if (hazard == null || hazard.transform.root == transform ||
                hazard.GetComponentInParent<PlayerMovement>() != null) continue;
            bool dangerous = hazard.GetComponentInParent<BoundaryHazard>() != null ||
                hazard.GetComponentInParent<BlackHoleKill>() != null ||
                hazard.GetComponentInParent<BlackCubeKill>() != null || hazard.GetComponentInParent<BlackKill>() != null ||
                hazard.GetComponentInParent<ForceField>() != null;
            if (!dangerous) continue;
            Rigidbody body = hazard.attachedRigidbody;
            threats[nearbyCount++] = new Threat
            {
                position = hazard.bounds.center,
                velocity = body != null ? body.linearVelocity : Vector3.zero,
                radius = Mathf.Min(9f, hazard.bounds.extents.magnitude) + 2.2f
            };
        }
    }

    private bool TryFloor(Vector3 point, out RaycastHit best)
    {
        int count = Physics.RaycastNonAlloc(point + Vector3.up * 10f, Vector3.down, castHits,
            35f, movement.groundMask, QueryTriggerInteraction.Ignore);
        best = default;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = castHits[i];
            if (hit.normal.y < 0.65f || hit.collider.transform.root == transform ||
                hit.collider.GetComponentInParent<PlayerMovement>() != null ||
                hit.collider.GetComponentInParent<BoundaryHazard>() != null ||
                hit.collider.GetComponentInParent<NetworkProjectilePhysics>() != null || hit.distance >= nearest) continue;
            nearest = hit.distance;
            best = hit;
        }
        return nearest < float.PositiveInfinity;
    }

    private bool ClearShot(Vector3 origin, Vector3 direction, float distance)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, castHits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform root = castHits[i].collider.transform.root;
            if (root != transform && root != opponent.transform.root) return false;
        }
        return true;
    }

    private Vector3 FindWallEscape(Vector3 position)
    {
        Vector3 escape = Vector3.zero;
        for (int index = 0; index < 8; index++)
        {
            float angle = index * Mathf.PI * 0.25f;
            Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            if (!Physics.SphereCast(position + Vector3.up * PlayerMovement.ScaleDistance(0.45f),
                    PlayerMovement.ScaleDistance(0.42f), direction, out RaycastHit hit,
                    PlayerMovement.ScaleDistance(1.5f), movement.groundMask,
                    QueryTriggerInteraction.Ignore) || hit.collider.transform.root == transform ||
                hit.collider.GetComponentInParent<PlayerMovement>() != null || hit.normal.y > 0.55f)
                continue;
            escape += Vector3.ProjectOnPlane(hit.normal, Vector3.up) /
                Mathf.Max(0.15f, hit.distance);
        }
        return escape.sqrMagnitude > 0.001f ? escape.normalized : Vector3.zero;
    }

    private void Face(Vector3 direction)
    {
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        movement.SetViewYaw(yaw);
        if (movement.orientation != null) movement.orientation.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void ChooseAbility(Vector3 enemyPosition, Vector3 enemyVelocity, float distance,
        BoundaryMatchController match, float safeRadius)
    {
        float bestScore = 0f;
        int bestSlot = -1;
        Vector3 bestAim = Vector3.forward;
        Vector3 origin = transform.position + Vector3.up *
            PlayerMovement.ScaleDistance(HollowAbility.EyeHeight);
        Vector3 target = enemyPosition + Vector3.up *
            PlayerMovement.ScaleDistance(HollowAbility.TargetCenterHeight);
        bool emergency = state.IsOutOfBounds || (!movement.IsGrounded && !movement.IsWallRunning) ||
            Vector3.ProjectOnPlane(transform.position - match.ArenaCenter, Vector3.up).magnitude > safeRadius;
        for (int i = 0; i < loadout.Length; i++)
        {
            AbilityId id = loadout[i];
            if (!abilities.CpuAbilityReady(id)) continue;
            Vector3 aimPoint = target;
            float score = 0f;
            switch (id)
            {
                case AbilityId.Base:
                    bool hasFloorAhead = TryFloor(
                        transform.position + selectedDirection * 4f, out _);
                    score = BoundaryCpuRules.ShouldUseBase(
                        emergency, movement.IsGrounded, hasFloorAhead) ? 130f : 0f;
                    break;
                case AbilityId.Bullseye:
                    aimPoint = BoundaryCpuRules.LeadTarget(origin, target, enemyVelocity, BullseyeAbility.ProjectileSpeed);
                    score = distance < 85f ? 70f : 0f;
                    break;
                case AbilityId.Hollow:
                    aimPoint = target + enemyVelocity * HollowAbility.ChargeDuration;
                    score = distance < HollowAbility.MaximumRange - 5f ? 80f : 0f;
                    break;
                case AbilityId.Slice:
                    score = distance < SliceAbility.Radius - 0.6f ? 95f : 0f;
                    break;
                case AbilityId.Charge:
                    aimPoint = BoundaryCpuRules.LeadTarget(origin, target, enemyVelocity,
                        ChargeAbility.ProjectileSpeed, ChargeAbility.ChargeSeconds);
                    score = distance > ChargeAbility.ExplosionRadius + 4f && distance < 65f ? 60f : 0f;
                    break;
                case AbilityId.Void:
                    score = BoundaryCpuRules.ShouldUseVoid(state.CurrentHealth, opponent.CurrentHealth, true) ? 110f : 0f;
                    break;
                case AbilityId.BlackThrow:
                    aimPoint = BoundaryCpuRules.LeadTarget(origin, target, enemyVelocity,
                        BlackThrow.EffectiveThrowForce(BlackThrow.DefaultThrowForce));
                    score = distance > 7f && distance < 36f ? 55f : 0f;
                    break;
                case AbilityId.AttractThrow:
                    aimPoint = target + enemyVelocity * 0.35f;
                    score = distance > 9f && distance < 38f ? 45f : 0f;
                    break;
                case AbilityId.RepelThrow:
                    score = distance < 22f ? 65f : 0f;
                    break;
                case AbilityId.Dash:
                    aimPoint = origin + selectedDirection * 5f;
                    if (selectedDirection.sqrMagnitude > 0.1f && TryFloor(aimPoint, out _) &&
                        Vector3.ProjectOnPlane(aimPoint - match.ArenaCenter, Vector3.up).magnitude < safeRadius &&
                        ClearShot(origin, selectedDirection, 4f))
                        score = emergency ? 120f : distance > 20f ? 35f : 15f;
                    break;
                case AbilityId.Teleport:
                    Vector3 inward = Vector3.ProjectOnPlane(match.ArenaCenter - transform.position, Vector3.up).normalized;
                    Vector3 destination = transform.position + (emergency ? inward : selectedDirection) * 18f;
                    if (TryFloor(destination, out RaycastHit floor) &&
                        Vector3.ProjectOnPlane(floor.point - match.ArenaCenter, Vector3.up).magnitude < safeRadius)
                    {
                        aimPoint = floor.point;
                        score = emergency ? 125f : distance > 30f ? 30f : 0f;
                    }
                    break;
                case AbilityId.Grapple:
                    // Target an actual static floor/wall to traverse; pulling a
                    // lethal mass toward ourselves is deliberately low utility.
                    Vector3 ahead = transform.position + selectedDirection * 16f;
                    if (TryFloor(ahead, out RaycastHit anchor) &&
                        Vector3.ProjectOnPlane(anchor.point - match.ArenaCenter, Vector3.up).magnitude < safeRadius)
                    {
                        aimPoint = anchor.point;
                        score = emergency ? 85f : distance > 24f ? 32f : 0f;
                    }
                    break;
            }
            bool attack = id != AbilityId.Base && id != AbilityId.Dash && id != AbilityId.Teleport &&
                id != AbilityId.Grapple && id != AbilityId.Void;
            Vector3 direction = (aimPoint - origin).normalized;
            if (attack && (opponent.IsServerInvulnerable || !ClearShot(origin, direction, Vector3.Distance(origin, aimPoint)))) score = 0f;
            if (score <= bestScore) continue;
            bestScore = score;
            bestSlot = i;
            bestAim = direction;
        }
        if (bestSlot < 0) return;
        Face(bestAim);
        // Grapple facing is validated against the root, which normally turns in
        // PlayerMovement's physics step; explicitly face before this request.
        if (loadout[bestSlot] == AbilityId.Grapple)
            transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(bestAim.x, bestAim.z) * Mathf.Rad2Deg, 0f);
        abilities.CpuUseAbility(bestSlot, bestAim);
        nextAbilityAt = Time.time + 0.3f;
    }
}
