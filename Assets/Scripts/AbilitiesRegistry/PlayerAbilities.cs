using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using PurrNet; 
using PurrNet.Modules;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class PlayerAbilities : NetworkBehaviour 
{
    private const float GrappleEyeHeight = 1.1f;
    private const float MaximumSubmittedEyeOffset = 3.5f;
    private const float MaximumSubmittedAbilityOriginOffset = 4f;
    private const float GrappleHitValidationTolerance = 0.75f;

    [Header("Game UI Names")]
    [SerializeField] private string btn1Name = "AbilityButton1";
    [SerializeField] private string btn2Name = "AbilityButton2";
    [SerializeField] private string btn3Name = "AbilityButton3";

    [Header("Player References")]
    [SerializeField] private AbilityRegistry registry;

    private Button abilityButton1;
    private Button abilityButton2;
    private Button abilityButton3;

    private AbilityId?[] slots = new AbilityId?[3];
    private readonly float[] slotCooldownEnds = new float[3];
    private readonly Dictionary<AbilityId, float> serverAbilityCooldownEnds = new Dictionary<AbilityId, float>();
    private readonly AbilityCooldownButton[] cooldownVisuals = new AbilityCooldownButton[3];
    private bool hasStartedSkinLoad;
    private Cam localCameraController;
    private Coroutine serverTeleportWindup;
    private GrappleAbility grappleAbility;
    private HollowAbility hollowAbility;
    private VoidAbility voidAbility;
    private Coroutine serverGrappleRoutine;
    private Coroutine serverHollowRoutine;
    private Coroutine serverVoidRoutine;
    private float serverGrappleCooldownUntil;
    private float serverHollowCooldownUntil;
    private float serverVoidCooldownUntil;
    private readonly Collider[] voidOverlapBuffer = new Collider[160];
    private readonly HashSet<Rigidbody> voidPulledBodies = new HashSet<Rigidbody>();
    private GameObject grappleTargetReticle;
    private int hollowHeldSlot = -1;
    private int grappleHeldSlot = -1;
    private Coroutine localHollowChargeRoutine;


    protected override void OnSpawned()
{
    Debug.Log($"[PlayerAbilities] Spawned! Owner: {isOwner}");
    
    if (isOwner)
    {
        SetupLocalPlayer();
    }
}

protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
{
    // This fires when ownership is assigned AFTER spawn
    if (!asServer && isOwner)
    {
        Debug.Log("[PlayerAbilities] Ownership received, setting up local player.");
        SetupLocalPlayer();
    }
}

private void SetupLocalPlayer()
{
    EnsureGrappleAbility();
    EnsureHollowAbility();
    EnsureVoidAbility();
    localCameraController = GetComponentInChildren<Cam>(true);

    // Cam owns the Camera and AudioListener lifecycle. Enabling either one
    // here races its first-person setup and can expose the prefab's stale
    // third-person transform for a frame during spawn or respawn.
    Cam cameraController = localCameraController;
    if (cameraController == null)
    {
        Camera legacyCamera = GetComponentInChildren<Camera>(true);
        if (legacyCamera != null)
        {
            legacyCamera.enabled = true;
            legacyCamera.gameObject.tag = "MainCamera";
        }

        AudioListener legacyListener = GetComponentInChildren<AudioListener>(true);
        if (legacyListener != null)
            legacyListener.enabled = true;
    }

    try
    {
        FindUIButtons();
        ApplyLocalLoadout();
        WireButtons();
        RequestSyncLoadout(GetSelectedIdsFromManager());
        LoadAndSyncSelectedSkin();
    }
    catch (System.Exception e)
    {
        Debug.LogError("[PlayerAbilities] UI Setup failed: " + e.Message);
    }
}

private void EnsureGrappleAbility()
{
    if (grappleAbility == null)
        grappleAbility = GetComponent<GrappleAbility>();
    if (grappleAbility == null)
        grappleAbility = gameObject.AddComponent<GrappleAbility>();
    registry?.Register(grappleAbility);
}

private void EnsureHollowAbility()
{
    if (hollowAbility == null)
        hollowAbility = GetComponent<HollowAbility>();
    if (hollowAbility == null)
        hollowAbility = gameObject.AddComponent<HollowAbility>();
    registry?.Register(hollowAbility);
}

private void EnsureVoidAbility()
{
    if (voidAbility == null)
        voidAbility = GetComponent<VoidAbility>();
    if (voidAbility == null)
        voidAbility = gameObject.AddComponent<VoidAbility>();
    registry?.Register(voidAbility);
}

private async void LoadAndSyncSelectedSkin()
{
    if (hasStartedSkinLoad)
        return;

    hasStartedSkinLoad = true;
    string selectedSkin = FirebaseManager.I != null
        ? FirebaseManager.I.SelectedSkin
        : "beard";
    string locallyAppliedSkin = NormalizeSkinId(selectedSkin);

    // Practice can begin while the cloud refresh is still in flight. Apply
    // the locally persisted choice immediately, then reconcile it once the
    // profile refresh completes.
    RequestSelectedSkin(locallyAppliedSkin);

    try
    {
        if (FirebaseManager.I != null)
        {
            await FirebaseManager.I.RefreshSkinDataAsync();
            selectedSkin = FirebaseManager.I.SelectedSkin;
        }
    }
    catch (System.Exception e)
    {
        // FirebaseManager starts with the locally confirmed selection. Keep it
        // if a profile refresh fails instead of broadcasting the beard skin.
        Debug.LogWarning("[PlayerAbilities] Could not refresh equipped skin; using local selection '" +
                         selectedSkin + "': " + e.Message);
    }

    if (NormalizeSkinId(selectedSkin) != locallyAppliedSkin)
        RequestSelectedSkin(selectedSkin);
}

[ServerRpc]
private void RequestSelectedSkin(string skinId)
{
    SyncSelectedSkin(NormalizeSkinId(skinId));
}

[ObserversRpc(bufferLast: true, runLocally: true)]
private void SyncSelectedSkin(string skinId)
{
    ApplySkinVisual(skinId);
}

private void ApplySkinVisual(string skinId)
{
    Transform visual = transform.Find("Visual");
    Transform tilt = visual != null ? visual.Find("Tilt") : null;
    Transform beardBody = tilt != null ? tilt.Find("Capsule") : null;
    Transform beardEye = transform.Find("eye");
    RemovePreviouslyEquippedSkins(tilt);

    bool useSun = skinId == "sun_ducker";
    bool useTurtle = skinId == "turtle";
    bool useCustomSkin = useSun || useTurtle;
    if (beardBody != null) beardBody.gameObject.SetActive(!useCustomSkin);
    if (beardEye != null) beardEye.gameObject.SetActive(!useCustomSkin);
    if (!useCustomSkin || tilt == null)
    {
        RefreshLocalFirstPersonVisuals(skinId);
        return;
    }

    string templateName = useTurtle ? "Turtle" : "Sun Ducker";
    Transform source = FindSkinTemplateInScene(gameObject.scene, templateName);
    if (source == null)
    {
        Debug.LogError($"[PlayerAbilities] Game scene skin template 'skins/{templateName}' was not found.");
        if (beardBody != null) beardBody.gameObject.SetActive(true);
        if (beardEye != null) beardEye.gameObject.SetActive(true);
        RefreshLocalFirstPersonVisuals(skinId);
        return;
    }

    GameObject clone = Instantiate(source.gameObject, tilt);
    clone.name = "EquippedSkin";
    clone.SetActive(true);
    clone.transform.localPosition = Vector3.zero;
    clone.transform.localRotation = Quaternion.identity;
    clone.transform.localScale = Vector3.one;
    if (useSun)
        SunDuckerDemonVisual.Build(clone.transform, clone.layer);
    foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true))
        Destroy(collider);
    foreach (Rigidbody body in clone.GetComponentsInChildren<Rigidbody>(true))
        Destroy(body);
    RefreshLocalFirstPersonVisuals(skinId);
}

private void RefreshLocalFirstPersonVisuals(string skinId)
{
    Cam cameraController = GetLocalCameraController();
    if (cameraController != null)
        cameraController.RefreshLocalFirstPersonVisuals(skinId);
}

private Cam GetLocalCameraController()
{
    if (localCameraController == null)
        localCameraController = GetComponentInChildren<Cam>(true);
    return localCameraController;
}

public static Transform FindSkinTemplateInScene(Scene scene, string templateName)
{
    if (!scene.IsValid() || string.IsNullOrWhiteSpace(templateName))
        return null;

    // Scene.GetRootGameObjects includes inactive roots. GameObject.Find does
    // not, which caused Practice mode to fall back to Beard whenever the
    // editor-only template container was hidden in the Game scene.
    foreach (GameObject root in scene.GetRootGameObjects())
    {
        Transform skinsRoot = FindNamedTransform(root.transform, "skins");
        if (skinsRoot == null)
            continue;

        Transform template = skinsRoot.Find(templateName);
        if (template != null)
            return template;
    }

    return null;
}

private static Transform FindNamedTransform(Transform root, string targetName)
{
    if (root == null)
        return null;
    if (root.name == targetName)
        return root;

    for (int i = 0; i < root.childCount; i++)
    {
        Transform found = FindNamedTransform(root.GetChild(i), targetName);
        if (found != null)
            return found;
    }

    return null;
}

private static string NormalizeSkinId(string skinId)
{
    return skinId == "sun_ducker" || skinId == "turtle" ? skinId : "beard";
}

private static void RemovePreviouslyEquippedSkins(Transform tilt)
{
    if (tilt == null) return;

    // More than one buffered/network update can arrive in one frame. Unity's
    // Destroy is deferred, so hide every old clone immediately and then remove
    // it. Iterating backwards also handles legacy clone names safely.
    for (int i = tilt.childCount - 1; i >= 0; i--)
    {
        Transform child = tilt.GetChild(i);
        if (child.name != "EquippedSkin" && child.name != "EquippedSunDucker" &&
            !child.name.StartsWith("EquippedTurtle"))
            continue;

        child.gameObject.SetActive(false);
        Destroy(child.gameObject);
    }
}

  private void FindUIButtons()
{
    // Using FindObjectOfType is often safer than Find if the UI is in the scene
    // Or, better yet, ensure these buttons are actually in the active scene
    abilityButton1 = GameObject.Find(btn1Name)?.GetComponent<Button>();
    abilityButton2 = GameObject.Find(btn2Name)?.GetComponent<Button>();
    abilityButton3 = GameObject.Find(btn3Name)?.GetComponent<Button>();

    if (abilityButton1 == null) Debug.LogWarning($"[PlayerAbilities] Could not find {btn1Name}");
}
    private AbilityId[] GetSelectedIdsFromManager()
    {
        if (LoadoutManager.I == null) return new AbilityId[0];
        return LoadoutManager.I.selectedAbilities.ToArray();
    }

    [ServerRpc]
    private void RequestSyncLoadout(AbilityId[] selectedIds)
    {
        EnsureGrappleAbility();
        EnsureHollowAbility();
        EnsureVoidAbility();
        if (!TryApplyAuthoritativeLoadout(selectedIds))
        {
            Debug.LogWarning("[PlayerAbilities] Rejected an invalid ability loadout.");
            return;
        }

        SyncLoadoutToObservers(selectedIds);
    }

    private bool TryApplyAuthoritativeLoadout(AbilityId[] selectedIds)
    {
        if (selectedIds == null || selectedIds.Length > slots.Length)
            return false;

        for (int index = 0; index < selectedIds.Length; index++)
        {
            AbilityId id = selectedIds[index];
            if (!System.Enum.IsDefined(typeof(AbilityId), id) || registry == null ||
                !registry.TryGet(id, out _))
                return false;

            for (int earlierIndex = 0; earlierIndex < index; earlierIndex++)
            {
                if (selectedIds[earlierIndex] == id)
                    return false;
            }
        }

        for (int index = 0; index < slots.Length; index++)
            slots[index] = index < selectedIds.Length ? selectedIds[index] : null;
        return true;
    }

[ObserversRpc]
private void SyncLoadoutToObservers(AbilityId[] selectedIds)
{
    if (isOwner) return;

    for (int i = 0; i < slots.Length; i++)
        slots[i] = selectedIds != null && i < selectedIds.Length ? selectedIds[i] : null;
    
    // Fixed line
    Debug.Log($"Synced {selectedIds.Length} abilities for player {owner?.id.ToString() ?? "Unknown"}");
}

    [ServerRpc]
    private void RequestActivateAbility(AbilityId id, Vector3 spawnPosition, Vector3 aimDirection)
    {
        if (!IsFiniteVector(spawnPosition) || !IsFiniteVector(aimDirection) ||
            aimDirection.sqrMagnitude < 0.0001f ||
            Vector3.Distance(spawnPosition, transform.position) > MaximumSubmittedAbilityOriginOffset ||
            !IsAbilityEquipped(id))
            return;

        if ((id == AbilityId.Dash || id == AbilityId.Slide || id == AbilityId.Teleport) &&
            !TryConsumeServerAbilityCooldown(id))
            return;

        if (id == AbilityId.BlackThrow || id == AbilityId.AttractThrow || id == AbilityId.RepelThrow)
        {
            // Physical projectiles are instantiated exactly once on the server.
            // Their NetworkTransform then creates and moves the same projectile
            // for every observer.
            ActivateNetworkThrow(id, spawnPosition, aimDirection);
            return;
        }

        if (id == AbilityId.Teleport)
        {
            BeginServerTeleportWindup(spawnPosition, aimDirection);
            return;
        }

        ObserversActivateAbility(id, aimDirection);
    }

    private bool IsAbilityEquipped(AbilityId id)
    {
        for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
        {
            if (slots[slotIndex] == id)
                return true;
        }
        return false;
    }

    private bool TryConsumeServerAbilityCooldown(AbilityId id)
    {
        if (serverAbilityCooldownEnds.TryGetValue(id, out float cooldownEnd) && Time.time < cooldownEnd)
            return false;
        if (registry == null || !registry.TryGet(id, out IAbility ability))
            return false;

        serverAbilityCooldownEnds[id] = Time.time + GetPhaseAdjustedCooldown(ability.CooldownDuration);
        return true;
    }

    [ServerRpc]
    private void RequestHollow(Vector3 submittedEyePosition, Vector3 aimDirection, int slotIndex)
    {
        EnsureHollowAbility();
        if (serverHollowRoutine != null || Time.time < serverHollowCooldownUntil ||
            slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] != AbilityId.Hollow ||
            !IsFiniteVector(submittedEyePosition) || !IsFiniteVector(aimDirection) ||
            aimDirection.sqrMagnitude < 0.0001f)
            return;

        Vector3 expectedEye = transform.position + Vector3.up * HollowAbility.EyeHeight;
        if (Vector3.Distance(submittedEyePosition, expectedEye) > MaximumSubmittedEyeOffset)
            return;

        Vector3 direction = aimDirection.normalized;
        serverHollowCooldownUntil = Time.time + HollowAbility.CooldownSeconds;
        ObserversBeginHollow(direction);
        serverHollowRoutine = StartCoroutine(ServerRunHollow(direction));
    }

    private IEnumerator ServerRunHollow(Vector3 direction)
    {
        yield return new WaitForSeconds(HollowAbility.ChargeDuration);

        Vector3 origin = HollowAbility.GetBlastOrigin(transform.position, direction);
        BoundaryPlayerState[] targets = FindObjectsByType<BoundaryPlayerState>(FindObjectsSortMode.None);
        float startedAt = Time.time;
        float lastTickAt = startedAt;
        while (Time.time - startedAt < HollowAbility.BlastDuration)
        {
            yield return new WaitForFixedUpdate();
            float cappedNow = Mathf.Min(Time.time, startedAt + HollowAbility.BlastDuration);
            float elapsed = Mathf.Max(0f, cappedNow - lastTickAt);
            lastTickAt = cappedNow;
            if (elapsed <= 0f)
                continue;

            foreach (BoundaryPlayerState target in targets)
            {
                if (target == null || target.transform.root == transform.root)
                    continue;
                Vector3 targetCenter = target.transform.position + Vector3.up * HollowAbility.TargetCenterHeight;
                if (HollowAbility.IsPointInsideBlast(targetCenter, origin, direction))
                    target.ServerApplyAbilityDamage(HollowAbility.DamagePerSecond * elapsed);
            }
        }

        serverHollowRoutine = null;
    }

    [ServerRpc]
    private void RequestVoid(Vector3 submittedAimDirection, int slotIndex)
    {
        EnsureVoidAbility();
        if (serverVoidRoutine != null || Time.time < serverVoidCooldownUntil ||
            slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] != AbilityId.Void ||
            !IsFiniteVector(submittedAimDirection) || submittedAimDirection.sqrMagnitude < 0.0001f)
            return;

        BoundaryPlayerState caster = GetComponent<BoundaryPlayerState>();
        bool practiceMode = GameManager.I != null && GameManager.I.IsPracticeMode;
        BoundaryPlayerState opponent = null;
        bool hasOpponent = caster != null &&
            BoundaryPlayerState.TryGetOpponent(caster, out opponent);
        float opponentHealth = hasOpponent && opponent != null ? opponent.CurrentHealth : 0f;
        if (caster == null || !VoidAbility.CanActivateForMode(
                practiceMode, hasOpponent, caster.CurrentHealth, opponentHealth))
            return;

        Vector3 groundPosition = VoidAbility.GetBlackHoleGroundPosition(transform.position,
            submittedAimDirection);
        int seed = unchecked(gameObject.GetInstanceID() * 397 ^ Mathf.RoundToInt(Time.time * 1000f));
        serverVoidCooldownUntil = Time.time + VoidAbility.CooldownSeconds;
        caster.ServerGrantInvulnerability(VoidAbility.ImmunitySeconds);
        if (owner.HasValue)
            ConfirmVoidCooldown(owner.Value, slotIndex);
        ObserversBeginVoid(groundPosition, seed);
        serverVoidRoutine = StartCoroutine(ServerRunVoid(groundPosition, opponent));
    }

    private IEnumerator ServerRunVoid(Vector3 groundPosition, BoundaryPlayerState opponent)
    {
        Vector3 blackHolePosition = groundPosition + Vector3.up * VoidAbility.BlackHoleHeight;
        float endsAt = Time.time + VoidAbility.DurationSeconds;
        float nextOpponentPullAt = Time.time;
        while (Time.time < endsAt)
        {
            yield return new WaitForFixedUpdate();
            if (Time.time >= nextOpponentPullAt)
            {
                PullVoidOpponent(blackHolePosition, opponent, 0.1f);
                nextOpponentPullAt = Time.time + 0.1f;
            }
            PullVoidHazards(blackHolePosition);
        }

        serverVoidRoutine = null;
    }

    private static void PullVoidOpponent(Vector3 center, BoundaryPlayerState opponent, float elapsed)
    {
        if (opponent == null)
            return;
        Vector3 delta = center - (opponent.transform.position + Vector3.up * 0.8f);
        float distance = delta.magnitude;
        if (distance <= 0.05f || distance >= VoidAbility.GravityRadius)
            return;
        opponent.ServerPushOwner(delta.normalized * VoidAbility.GravityAcceleration *
            VoidAbility.GravityFalloff(distance) * elapsed);
    }

    private void PullVoidHazards(Vector3 center)
    {
        BoundaryHazard.ServerApplyArenaMassGravity(
            center, VoidAbility.GravityRadius, VoidAbility.GravityAcceleration);
        NetworkProjectilePhysics.ServerApplyBlackHoleGravity(
            center, VoidAbility.GravityRadius, VoidAbility.GravityAcceleration);

        int hitCount = Physics.OverlapSphereNonAlloc(center, VoidAbility.GravityRadius,
            voidOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        voidPulledBodies.Clear();
        for (int index = 0; index < hitCount; index++)
        {
            Collider hit = voidOverlapBuffer[index];
            Rigidbody body = hit != null ? hit.attachedRigidbody : null;
            if (body == null || body.isKinematic || !voidPulledBodies.Add(body) ||
                body.GetComponentInParent<PlayerMovement>() != null)
                continue;

            BoundaryHazard hazard = body.GetComponentInParent<BoundaryHazard>();
            NetworkArenaCubePhysics arenaCube = body.GetComponentInParent<NetworkArenaCubePhysics>();
            if (hazard != null || arenaCube == null)
                continue;

            Vector3 delta = center - body.worldCenterOfMass;
            float distance = delta.magnitude;
            if (distance <= 0.05f || distance >= VoidAbility.GravityRadius)
                continue;
            body.AddForce(delta.normalized * VoidAbility.GravityAcceleration *
                VoidAbility.GravityFalloff(distance), ForceMode.Acceleration);
        }
    }

    [TargetRpc]
    private void ConfirmVoidCooldown(PlayerID target, int slotIndex)
    {
        if (!isOwner || slotIndex < 0 || slotIndex >= slotCooldownEnds.Length)
            return;
        slotCooldownEnds[slotIndex] = Time.time + VoidAbility.CooldownSeconds;
        cooldownVisuals[slotIndex]?.BeginCooldown(VoidAbility.CooldownSeconds);
    }

    [ObserversRpc]
    private void ObserversBeginVoid(Vector3 groundPosition, int seed)
    {
        EnsureVoidAbility();
        BoundaryPlayerState caster = GetComponent<BoundaryPlayerState>();
        bool hasOpponent = caster != null && BoundaryPlayerState.TryGetOpponent(caster, out _);
        voidAbility.BeginPresentation(groundPosition, seed,
            VoidAbility.ShouldShowEnemyHighlight(isOwner, hasOpponent));
    }

    [ObserversRpc]
    private void ObserversBeginHollow(Vector3 direction)
    {
        EnsureHollowAbility();
        hollowAbility.BeginPresentation(direction, true);
        if (isOwner)
            GetLocalCameraController()?.RequestHollowShake();
    }

    [ServerRpc]
    private void RequestGrapple(Vector3 aimOrigin, Vector3 requestedPoint,
        NetworkIdentity requestedTarget, int slotIndex)
    {
        EnsureGrappleAbility();
        if (serverGrappleRoutine != null || Time.time < serverGrappleCooldownUntil ||
            slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] != AbilityId.Grapple ||
            !IsFiniteVector(aimOrigin) || !IsFiniteVector(requestedPoint))
            return;

        Vector3 expectedEye = transform.position + Vector3.up * GrappleEyeHeight;
        if (Vector3.Distance(aimOrigin, expectedEye) > MaximumSubmittedEyeOffset)
            return;

        Vector3 validatedPoint = requestedTarget != null
            ? requestedTarget.transform.TransformPoint(requestedPoint)
            : requestedPoint;
        Vector3 rayDelta = validatedPoint - aimOrigin;
        float requestedDistance = rayDelta.magnitude;
        if (requestedDistance <= 0.001f || requestedDistance > GrappleAbility.MaximumRange ||
            !Physics.Raycast(aimOrigin, rayDelta / requestedDistance, out RaycastHit hit,
                requestedDistance + GrappleHitValidationTolerance, ~0, QueryTriggerInteraction.Ignore) ||
            !TryResolveGrappleTarget(hit, out bool movable, out Rigidbody targetBody,
                out NetworkIdentity targetIdentity))
            return;

        if (movable
            ? requestedTarget == null || targetIdentity != requestedTarget
            : requestedTarget != null || Vector3.Distance(hit.point, requestedPoint) > GrappleHitValidationTolerance)
            return;

        serverGrappleCooldownUntil = Time.time + GrappleAbility.CooldownSeconds;
        ObserversBeginGrapple(hit.point, targetIdentity, movable);
        if (owner.HasValue)
            ConfirmGrappleCooldown(owner.Value, slotIndex);
        serverGrappleRoutine = StartCoroutine(CompleteServerGrapple(
            targetBody, hit.point, movable, Time.time));
    }

    private IEnumerator CompleteServerGrapple(Rigidbody targetBody, Vector3 staticAnchor, bool movable,
        float activatedAt)
    {
        Rigidbody playerBody = GetComponentInChildren<Rigidbody>();
        float cableTravelTime = GrappleAbility.GetCableTravelDuration(
            Vector3.Distance(transform.position, staticAnchor));
        yield return new WaitForSeconds(cableTravelTime);

        float visibleUntil = Time.time + 0.14f;
        while (!GrappleAbility.HasTimedOut(activatedAt, Time.time) && (movable
            ? targetBody != null && (Time.time < visibleUntil || Vector3.Distance(targetBody.position, transform.position) > 1.5f)
            : playerBody != null && (Time.time < visibleUntil || Vector3.Distance(playerBody.worldCenterOfMass, staticAnchor) > GrappleAbility.ReleaseDistance)))
        {
            if (movable)
                targetBody.AddForce((transform.position - targetBody.position).normalized * 36f, ForceMode.Acceleration);
            yield return new WaitForFixedUpdate();
        }
        ObserversEndGrapple();
        serverGrappleRoutine = null;
    }

    [TargetRpc]
    private void ConfirmGrappleCooldown(PlayerID target, int slotIndex)
    {
        if (!isOwner || slotIndex < 0 || slotIndex >= slotCooldownEnds.Length)
            return;
        slotCooldownEnds[slotIndex] = Time.time + GrappleAbility.CooldownSeconds;
        cooldownVisuals[slotIndex]?.BeginCooldown(GrappleAbility.CooldownSeconds);
    }

    [ObserversRpc]
    private void ObserversBeginGrapple(Vector3 hitPoint, NetworkIdentity targetIdentity, bool movable)
    {
        EnsureGrappleAbility();
        grappleAbility.BeginPresentation(hitPoint,
            movable && targetIdentity != null ? targetIdentity.transform : null, movable);
        if (isOwner)
            GetLocalCameraController()?.ShowThrowArm((hitPoint - transform.position).normalized);
    }

    [ObserversRpc]
    private void ObserversEndGrapple()
    {
        grappleAbility?.EndPresentation();
    }

    public void CancelGrappleForJump()
    {
        if (!isOwner || grappleAbility == null)
            return;
        grappleAbility.CancelForJump();
        RequestCancelGrapple();
    }

    [ServerRpc]
    private void RequestCancelGrapple()
    {
        if (serverGrappleRoutine != null)
        {
            StopCoroutine(serverGrappleRoutine);
            serverGrappleRoutine = null;
        }
        ObserversEndGrapple();
    }

    private void BeginServerTeleportWindup(Vector3 aimOrigin, Vector3 aimDirection)
    {
        if (serverTeleportWindup != null || registry == null ||
            !registry.TryGet(AbilityId.Teleport, out var ability) ||
            !(ability is TeleportAbility teleport))
            return;

        if (!teleport.TryPrepareWindup(aimOrigin, aimDirection, out Vector3 start, out Vector3 destination,
                out Vector3 direction))
        {
            ObserversTeleportFailed(start, direction);
            return;
        }

        ObserversBeginTeleportWindup(start, direction);
        serverTeleportWindup = StartCoroutine(CompleteServerTeleportWindup(teleport, destination, direction));
    }

    private IEnumerator CompleteServerTeleportWindup(TeleportAbility teleport, Vector3 destination, Vector3 direction)
    {
        yield return new WaitForSeconds(TeleportAbility.WindupDuration);
        if (teleport != null && teleport.TryCompleteServerTeleport(ref destination))
            ObserversCompleteTeleportWindup(destination, direction);
        else
            ObserversTeleportFailed(transform.position, direction);
        serverTeleportWindup = null;
    }

    [ObserversRpc]
    private void ObserversBeginTeleportWindup(Vector3 start, Vector3 direction)
    {
        if (registry != null && registry.TryGet(AbilityId.Teleport, out var ability) &&
            ability is TeleportAbility teleport)
            teleport.BeginWindupPresentation(start, direction);
    }

    [ObserversRpc]
    private void ObserversCompleteTeleportWindup(Vector3 destination, Vector3 direction)
    {
        if (registry != null && registry.TryGet(AbilityId.Teleport, out var ability) &&
            ability is TeleportAbility teleport)
            teleport.CompleteWindupPresentation(destination, direction);
    }

    [ObserversRpc]
    private void ObserversTeleportFailed(Vector3 start, Vector3 direction)
    {
        if (registry != null && registry.TryGet(AbilityId.Teleport, out var ability) &&
            ability is TeleportAbility teleport)
            teleport.PlayFailurePresentation(start, direction);
    }

private void ActivateNetworkThrow(AbilityId id, Vector3 spawnPosition, Vector3 aimDirection)
{
    if (registry == null || !registry.TryGet(id, out var ability))
    {
        Debug.LogError($"[PlayerAbilities] Could not find network throw ability {id}");
        return;
    }

    if (ability is BlackThrow blackThrow)
        blackThrow.ActivateFromNetwork(spawnPosition, aimDirection);
    else if (ability is AttractThrow attractThrow)
        attractThrow.ActivateFromNetwork(spawnPosition, aimDirection);
    else if (ability is RepelThrow repelThrow)
        repelThrow.ActivateFromNetwork(spawnPosition, aimDirection);
}

[ObserversRpc]
private void ObserversActivateAbility(AbilityId id, Vector3 aimDirection)
{
    // Dash and Slide movement remains owner-simulated. The server relays a
    // direction-only presentation to every observer so remote clients do not
    // need to run movement/collision logic merely to see the effects.
    if (id == AbilityId.Dash && registry != null && registry.TryGet(id, out var dashAbility) &&
        dashAbility is DashAbility dash)
    {
        if (isOwner)
            dash.Activate();
        else
            dash.PlayObserverPresentation(aimDirection);
        return;
    }

    if (id == AbilityId.Slide && registry != null && registry.TryGet(id, out var slideAbility) &&
        slideAbility is SlideAbility slide)
    {
        if (isOwner)
            slide.Activate();
        else
            slide.PlayObserverPresentation(aimDirection);
        return;
    }

    ActivateAbility(id);
}

// Slide jumps are resolved by the owning player, so relay the short radial
// burst separately once that move has actually happened.
public void NotifySlideJumpPresentation(Vector3 position)
{
    if (isOwner)
        RequestSlideJumpPresentation(position);
}

[ServerRpc]
private void RequestSlideJumpPresentation(Vector3 position)
{
    ObserversPlaySlideJumpPresentation(position);
}

[ObserversRpc(excludeOwner: true)]
private void ObserversPlaySlideJumpPresentation(Vector3 position)
{
    if (registry != null && registry.TryGet(AbilityId.Slide, out var ability) &&
        ability is SlideAbility slide)
        slide.PlayObserverJumpBurst(position);
}

private void ActivateAbility(AbilityId id)
{
    if (registry != null && registry.TryGet(id, out var ability))
    {
        Debug.Log($"[PlayerAbilities] Found ability, calling Activate()");
        ability.Activate();
    }
    else
    {
        Debug.LogError($"[PlayerAbilities] Could not find ability {id} in registry");
    }
}
    void ApplyLocalLoadout()
    {
        if (LoadoutManager.I == null) return;

        var list = LoadoutManager.I.selectedAbilities;
        for (int i = 0; i < slots.Length && i < list.Count; i++)
        {
            slots[i] = list[i];
        }
    }

    void WireButtons()
    {
        SetupButton(abilityButton1, 0);
        SetupButton(abilityButton2, 1);
        SetupButton(abilityButton3, 2);
    }

    void SetupButton(Button btn, int slotIndex)
    {
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();

        AbilityReleaseButton releaseButton = btn.GetComponent<AbilityReleaseButton>();
        if (releaseButton != null)
            releaseButton.Configure(null);

        AbilityTouchTransferTarget transferTarget = btn.GetComponent<AbilityTouchTransferTarget>();
        if (transferTarget == null)
            transferTarget = btn.gameObject.AddComponent<AbilityTouchTransferTarget>();
        transferTarget.Configure(null);

        AbilityCooldownButton cooldownVisual = btn.GetComponent<AbilityCooldownButton>();
        if (cooldownVisual == null)
            cooldownVisual = btn.gameObject.AddComponent<AbilityCooldownButton>();

        cooldownVisual.Initialize(btn);
        cooldownVisuals[slotIndex] = cooldownVisual;

        var id = slots[slotIndex];
        if (id == null)
        {
            btn.interactable = false;
            return;
        }

        btn.interactable = true;
        System.Action pressAction =
            id.Value == AbilityId.Hollow ? () => BeginHollowHold(slotIndex) :
            id.Value == AbilityId.Grapple ? () => BeginGrappleHold(slotIndex) : null;
        System.Action cancelAction =
            id.Value == AbilityId.Hollow ? CancelHollowHold :
            id.Value == AbilityId.Grapple ? CancelGrappleHold : null;
        transferTarget.Configure(() => UseSlot(slotIndex), pressAction, cancelAction);
        if (RequiresReleaseActivation(id.Value))
        {
            if (releaseButton == null)
                releaseButton = btn.gameObject.AddComponent<AbilityReleaseButton>();

            // The player can hold this touch while using a second touch to
            // look around. Aim is sampled only when this touch is released.
            releaseButton.Configure(
                () => UseSlot(slotIndex),
                pressAction,
                cancelAction);
        }
        else
        {
            btn.onClick.AddListener(() => UseSlot(slotIndex));
        }
    }

    private static bool RequiresReleaseActivation(AbilityId id)
    {
        return id == AbilityId.BlackThrow ||
               id == AbilityId.AttractThrow ||
               id == AbilityId.RepelThrow ||
               id == AbilityId.Dash ||
               id == AbilityId.Slide ||
               id == AbilityId.Grapple ||
               id == AbilityId.Hollow ||
               id == AbilityId.Void ||
               id == AbilityId.Teleport;
    }

private void BeginHollowHold(int slotIndex)
{
    if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] != AbilityId.Hollow ||
        Time.time < slotCooldownEnds[slotIndex])
        return;

    hollowHeldSlot = slotIndex;
    UpdateHollowArms();
}

private void CancelHollowHold()
{
    hollowHeldSlot = -1;
    if (localHollowChargeRoutine == null)
        GetLocalCameraController()?.SetHollowArmsActive(false, transform.position);
}

private void BeginGrappleHold(int slotIndex)
{
    if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] != AbilityId.Grapple ||
        Time.time < slotCooldownEnds[slotIndex])
        return;

    grappleHeldSlot = slotIndex;
}

private void CancelGrappleHold()
{
    grappleHeldSlot = -1;
    SetGrappleTargetReticleVisible(false);
}

void UseSlot(int slotIndex)
{
    PlayerMovement movement = GetComponent<PlayerMovement>();
    var id = slots[slotIndex];
    if (id == null)
    {
        Debug.LogError($"[PlayerAbilities] Slot {slotIndex} is null!");
        return;
    }

    if (Time.time < slotCooldownEnds[slotIndex])
        return;

    // Slide support is owner-local. Validate it before starting the UI cooldown
    // or relaying presentation to observers.
    if (id == AbilityId.Slide &&
        (registry == null || !registry.TryGet(AbilityId.Slide, out var slideAbility) ||
         !(slideAbility is SlideAbility slide) || !slide.CanActivate()))
        return;

    Debug.Log($"[PlayerAbilities] UseSlot {slotIndex} id={id}");

    float cooldownDuration = 0f;
    if (id != AbilityId.Grapple && id != AbilityId.Void)
    {
        cooldownDuration = GetCooldownDuration(id.Value);
        BoundaryMatchController match = BoundaryMatchController.Instance;
        if (match != null && id != AbilityId.Hollow)
        {
            if (match.Phase == BoundaryPhase.OuterRing)
                cooldownDuration *= 0.94f;
            else if (match.Phase == BoundaryPhase.MiddleRing)
                cooldownDuration *= 0.88f;
            else if (match.Phase == BoundaryPhase.InnerRing)
                cooldownDuration *= 0.76f;
        }
        slotCooldownEnds[slotIndex] = Time.time + cooldownDuration;
        cooldownVisuals[slotIndex]?.BeginCooldown(cooldownDuration);
    }

    Transform aim = movement != null && movement.orientation != null
        ? movement.orientation
        : transform;
    Camera ownerCamera = GetComponentInChildren<Camera>(true);
    ThrowPoint point = transform.root.GetComponentInChildren<ThrowPoint>(true);
    Vector3 cameraDirection = ownerCamera != null ? ownerCamera.transform.forward : aim.forward;
    Vector3 spawnPosition = id == AbilityId.Teleport && ownerCamera != null
        ? ownerCamera.transform.position
        : point != null ? point.transform.position : transform.position + aim.forward;
    Vector3 aimDirection = cameraDirection.sqrMagnitude > 0.0001f
        ? cameraDirection.normalized
        : transform.forward;

    if (id == AbilityId.Dash && registry != null &&
        registry.TryGet(AbilityId.Dash, out IAbility dashNetworkAbility) &&
        dashNetworkAbility is DashAbility dashForDirection)
    {
        aimDirection = dashForDirection.GetActivationDirection();
    }
    else if (id == AbilityId.Slide && registry != null &&
             registry.TryGet(AbilityId.Slide, out IAbility slideNetworkAbility) &&
             slideNetworkAbility is SlideAbility slideForDirection &&
             slideForDirection.TryGetActivationDirection(out Vector3 slideDirection))
    {
        aimDirection = slideDirection;
    }

    if (id == AbilityId.Grapple)
    {
        grappleHeldSlot = -1;
        SetGrappleTargetReticleVisible(false);
        if (TryCaptureGrappleRequest(out Vector3 grappleOrigin, out Vector3 requestedPoint,
                out NetworkIdentity requestedTarget))
            RequestGrapple(grappleOrigin, requestedPoint, requestedTarget, slotIndex);
        return;
    }

    if (id == AbilityId.Hollow)
    {
        hollowHeldSlot = -1;
        BeginLocalHollowChargePresentation(aimDirection);
        RequestHollow(ownerCamera != null ? ownerCamera.transform.position : transform.position,
            aimDirection, slotIndex);
        return;
    }

    if (id == AbilityId.Void)
    {
        RequestVoid(aimDirection, slotIndex);
        return;
    }

    RequestActivateAbility(id.Value, spawnPosition, aimDirection);

    if (id == AbilityId.BlackThrow || id == AbilityId.AttractThrow || id == AbilityId.RepelThrow)
    {
        GetLocalCameraController()?.ShowThrowArm(aimDirection);
    }
}

private void Update()
{
    if (!isOwner || registry == null)
        return;

    bool movementAbilityActive =
        (registry.TryGet(AbilityId.Slide, out var slide) && slide is SlideAbility slideAbility && slideAbility.IsActive) ||
        (registry.TryGet(AbilityId.Dash, out var dash) && dash is DashAbility dashAbility && dashAbility.IsActive);

    localCameraController?.SetMovementArmActive(movementAbilityActive);
    UpdateHollowArms();
    UpdateGrappleButtonAvailability();
    UpdateVoidButtonAvailability();
}

private void UpdateVoidButtonAvailability()
{
    BoundaryPlayerState caster = GetComponent<BoundaryPlayerState>();
    bool practiceMode = GameManager.I != null && GameManager.I.IsPracticeMode;
    BoundaryPlayerState opponent = null;
    bool hasOpponent = caster != null &&
        BoundaryPlayerState.TryGetOpponent(caster, out opponent);
    float opponentHealth = hasOpponent && opponent != null ? opponent.CurrentHealth : 0f;
    bool eligible = caster != null && VoidAbility.CanActivateForMode(
        practiceMode, hasOpponent, caster.CurrentHealth, opponentHealth);
    for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
    {
        if (slots[slotIndex] != AbilityId.Void)
            continue;
        Button button = GetAbilityButton(slotIndex);
        if (button != null)
            button.interactable = Time.time >= slotCooldownEnds[slotIndex] && eligible;
    }
}

private void UpdateHollowArms()
{
    if (hollowHeldSlot < 0 || hollowHeldSlot >= slots.Length ||
        slots[hollowHeldSlot] != AbilityId.Hollow)
        return;

    Camera ownerCamera = GetComponentInChildren<Camera>(true);
    Vector3 direction = ownerCamera != null ? ownerCamera.transform.forward : transform.forward;
    Vector3 target = HollowAbility.GetChargePresentationPosition(transform.position, direction);
    GetLocalCameraController()?.SetHollowArmsActive(true, target);
}

private void BeginLocalHollowChargePresentation(Vector3 direction)
{
    if (localHollowChargeRoutine != null)
        StopCoroutine(localHollowChargeRoutine);
    localHollowChargeRoutine = StartCoroutine(ShowLocalHollowArmsDuringCharge(direction));
}

private IEnumerator ShowLocalHollowArmsDuringCharge(Vector3 direction)
{
    if (direction.sqrMagnitude < 0.0001f)
        direction = transform.forward;
    direction.Normalize();

    float endsAt = Time.time + HollowAbility.ChargeDuration;
    while (Time.time < endsAt)
    {
        Vector3 target = HollowAbility.GetChargePresentationPosition(transform.position, direction);
        GetLocalCameraController()?.SetHollowArmsActive(true, target);
        yield return null;
    }

    GetLocalCameraController()?.SetHollowArmsActive(false, transform.position);
    localHollowChargeRoutine = null;
}

public static float GetPhaseAdjustedCooldown(float baseCooldown)
{
    BoundaryMatchController match = BoundaryMatchController.Instance;
    if (match == null)
        return baseCooldown;
    if (match.Phase == BoundaryPhase.OuterRing)
        return baseCooldown * 0.94f;
    if (match.Phase == BoundaryPhase.MiddleRing)
        return baseCooldown * 0.88f;
    if (match.Phase == BoundaryPhase.InnerRing)
        return baseCooldown * 0.76f;
    return baseCooldown;
}

private void UpdateGrappleButtonAvailability()
{
    bool grappleEquipped = false;
    for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
    {
        if (slots[slotIndex] == AbilityId.Grapple)
        {
            grappleEquipped = true;
            break;
        }
    }

    if (!grappleEquipped)
    {
        grappleHeldSlot = -1;
        SetGrappleTargetReticleVisible(false);
        return;
    }

    bool hasValidTarget = TryCaptureGrappleRequest(out _, out _, out _);
    bool grappleHeld = grappleHeldSlot >= 0 && grappleHeldSlot < slots.Length &&
        slots[grappleHeldSlot] == AbilityId.Grapple;
    SetGrappleTargetReticleVisible(grappleHeld && hasValidTarget);

    for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
    {
        if (slots[slotIndex] != AbilityId.Grapple)
            continue;

        Button button = GetAbilityButton(slotIndex);
        if (button != null)
            button.interactable = Time.time >= slotCooldownEnds[slotIndex];
    }
}

private void SetGrappleTargetReticleVisible(bool visible)
{
    if (visible && grappleTargetReticle == null)
        grappleTargetReticle = CreateGrappleTargetReticle();

    if (grappleTargetReticle != null && grappleTargetReticle.activeSelf != visible)
        grappleTargetReticle.SetActive(visible);
}

private static GameObject CreateGrappleTargetReticle()
{
    GameObject crosshair = GameObject.Find("Aim Crosshair");
    if (crosshair == null)
        return null;

    GameObject reticle = new GameObject("Grapple Target Reticle", typeof(RectTransform), typeof(Image));
    reticle.layer = crosshair.layer;
    reticle.transform.SetParent(crosshair.transform, false);
    RectTransform rect = reticle.GetComponent<RectTransform>();
    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
    rect.sizeDelta = new Vector2(56f, 56f);
    Image image = reticle.GetComponent<Image>();
    image.sprite = CreateGrappleReticleSprite();
    image.color = Color.white;
    image.raycastTarget = false;
    reticle.transform.SetAsFirstSibling();
    return reticle;
}

private static Sprite CreateGrappleReticleSprite()
{
    const int size = 64;
    const float outerRadius = 29f;
    const float innerRadius = 25f;
    Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
    texture.filterMode = FilterMode.Bilinear;
    texture.wrapMode = TextureWrapMode.Clamp;
    Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
    for (int y = 0; y < size; y++)
    {
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), center);
            float alpha = distance >= innerRadius && distance <= outerRadius ? 1f : 0f;
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
    }
    texture.Apply(false, true);
    return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
}

private Button GetAbilityButton(int slotIndex)
{
    switch (slotIndex)
    {
        case 0: return abilityButton1;
        case 1: return abilityButton2;
        case 2: return abilityButton3;
        default: return null;
    }
}

private bool TryCaptureGrappleRequest(out Vector3 aimOrigin, out Vector3 requestedPoint,
    out NetworkIdentity requestedTarget)
{
    aimOrigin = Vector3.zero;
    requestedPoint = Vector3.zero;
    requestedTarget = null;

    Camera ownerCamera = GetComponentInChildren<Camera>(true);
    if (ownerCamera == null)
        return false;

    aimOrigin = ownerCamera.transform.position;
    if (!Physics.Raycast(aimOrigin, ownerCamera.transform.forward, out RaycastHit hit,
            GrappleAbility.MaximumRange, ~0, QueryTriggerInteraction.Ignore) ||
        !TryResolveGrappleTarget(hit, out bool movable, out _, out NetworkIdentity targetIdentity))
        return false;

    requestedTarget = movable ? targetIdentity : null;
    requestedPoint = movable && targetIdentity != null
        ? targetIdentity.transform.InverseTransformPoint(hit.point)
        : hit.point;
    return true;
}

private static bool IsFiniteVector(Vector3 value)
{
    return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
           !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
           !float.IsNaN(value.z) && !float.IsInfinity(value.z);
}

private bool TryResolveGrappleTarget(RaycastHit hit, out bool movable,
    out Rigidbody targetBody, out NetworkIdentity targetIdentity)
{
    movable = false;
    targetBody = null;
    targetIdentity = null;

    Collider hitCollider = hit.collider;
    if (hitCollider == null || hitCollider.isTrigger ||
        hitCollider.transform.root == transform.root ||
        hitCollider.GetComponentInParent<PlayerMovement>() != null)
        return false;

    BoundaryHazard hazard = hitCollider.GetComponentInParent<BoundaryHazard>();
    NetworkProjectilePhysics projectile = hitCollider.GetComponentInParent<NetworkProjectilePhysics>();
    movable = (hazard != null && hazard.IsArenaMass &&
               (hazard.Kind == BoundaryHazardKind.Cube || hazard.Kind == BoundaryHazardKind.ArenaBlackHole)) ||
              (projectile != null && projectile.GetComponentInChildren<BlackHoleKill>() != null);

    if ((hazard != null || projectile != null) && !movable)
        return false;

    targetBody = movable ? hit.rigidbody : null;
    if ((movable && targetBody == null) || (!movable && hit.rigidbody != null))
        return false;

    targetIdentity = hitCollider.GetComponentInParent<NetworkIdentity>();
    return !movable || targetIdentity != null;
}

private float GetCooldownDuration(AbilityId id)
{
    if (registry != null && registry.TryGet(id, out var ability))
        return Mathf.Max(0f, ability.CooldownDuration);

    Debug.LogWarning($"[PlayerAbilities] No cooldown source found for {id}.");
    return 0f;
}
    // =========================================================
// Networked Throw
// =========================================================
public void RequestThrow(Vector3 spawnPos, Vector3 dir, float force, float upForce, GameObject prefab)
{
    // Find the prefab index so we can send it over the network
    // We use the prefab name to look it up on the server
    ServerRequestThrow(spawnPos, dir, force, upForce, prefab.name);
}

[ServerRpc]
private void ServerRequestThrow(Vector3 spawnPos, Vector3 dir, 
    float force, float upForce, string prefabName)
{
    // Find the prefab from the registry or Resources folder
    GameObject prefab = Resources.Load<GameObject>(prefabName);
    if (prefab == null)
    {
        Debug.LogError($"[PlayerAbilities] Prefab '{prefabName}' not found in Resources.");
        return;
    }

    Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
    GameObject projectile = Instantiate(prefab, spawnPos, rot);

    Rigidbody rb = projectile.GetComponent<Rigidbody>();
    if (rb != null)
    {
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.AddForce(dir * force + Vector3.up * upForce, ForceMode.Impulse);
    }

    // PurrNet will auto-sync this to all clients since it has NetworkIdentity
}

// =========================================================
// Networked Teleport
// =========================================================
public void RequestTeleport(Vector3 destination)
{
    ServerRequestTeleport(destination);
}

[ServerRpc]
private void ServerRequestTeleport(Vector3 destination)
{
    // Move the rigidbody on the server — NetworkTransform syncs it to all clients
    Rigidbody rb = GetComponentInChildren<Rigidbody>();
    if (rb != null)
    {
        rb.MovePosition(destination);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
}

/// <summary>
/// Invokes the assigned action only when the pointer that pressed the button
/// is released. This keeps the button held without consuming a second touch
/// used by the look area.
/// </summary>
[DisallowMultipleComponent]
public sealed class AbilityReleaseButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICancelHandler
{
    private System.Action onRelease;
    private System.Action onPress;
    private System.Action onCancel;
    private int holdingPointerId = int.MinValue;
    private TouchLookHandler touchLook;
    public bool IsHolding => holdingPointerId != int.MinValue;

    public void Configure(System.Action releaseAction, System.Action pressAction = null,
        System.Action cancelAction = null)
    {
        if (holdingPointerId != int.MinValue)
            onCancel?.Invoke();
        onRelease = releaseAction;
        onPress = pressAction;
        onCancel = cancelAction;
        holdingPointerId = int.MinValue;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        AbilityTouchTransferTarget transferTarget = GetComponent<AbilityTouchTransferTarget>();
        if (onRelease == null || holdingPointerId != int.MinValue ||
            (transferTarget != null && transferTarget.IsHolding))
            return;

        holdingPointerId = eventData.pointerId;
        if (touchLook == null)
            touchLook = FindFirstObjectByType<TouchLookHandler>();
        onPress?.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId == holdingPointerId)
            touchLook?.SubmitLookDelta(eventData.delta);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != holdingPointerId)
            return;

        holdingPointerId = int.MinValue;
        onRelease?.Invoke();
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (holdingPointerId != int.MinValue)
            onCancel?.Invoke();
        holdingPointerId = int.MinValue;
    }
}

/// <summary>
/// Receives a touch that began on an explicitly allowed source control. It is
/// intentionally passive, so a touch beginning on one ability cannot transfer
/// to another ability.
/// </summary>
[DisallowMultipleComponent]
public sealed class AbilityTouchTransferTarget : MonoBehaviour
{
    private System.Action onRelease;
    private System.Action onPress;
    private System.Action onCancel;
    private int holdingPointerId = int.MinValue;
    private Button button;

    public bool IsHolding => holdingPointerId != int.MinValue;

    public void Configure(System.Action releaseAction, System.Action pressAction = null,
        System.Action cancelAction = null)
    {
        CancelTransferredTouch();
        onRelease = releaseAction;
        onPress = pressAction;
        onCancel = cancelAction;
        if (button == null)
            button = GetComponent<Button>();
    }

    public bool BeginTransferredTouch(int pointerId)
    {
        AbilityReleaseButton directRelease = GetComponent<AbilityReleaseButton>();
        if (onRelease == null || holdingPointerId != int.MinValue ||
            !isActiveAndEnabled || button == null || !button.interactable ||
            (directRelease != null && directRelease.IsHolding))
            return false;

        holdingPointerId = pointerId;
        onPress?.Invoke();
        return true;
    }

    public void ReleaseTransferredTouch(int pointerId)
    {
        if (pointerId != holdingPointerId)
            return;

        holdingPointerId = int.MinValue;
        onRelease?.Invoke();
    }

    public void CancelTransferredTouch(int pointerId = int.MinValue)
    {
        if (holdingPointerId == int.MinValue ||
            (pointerId != int.MinValue && pointerId != holdingPointerId))
            return;

        holdingPointerId = int.MinValue;
        onCancel?.Invoke();
    }

    private void OnDisable()
    {
        CancelTransferredTouch();
    }
}

/// <summary>
/// Tracks one touch that began on Move or Jump and transfers its release to the
/// ability currently under that finger.
/// </summary>
[DisallowMultipleComponent]
public sealed class AbilityTouchTransferSource : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler, ICancelHandler
{
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private int pointerId = int.MinValue;
    private AbilityTouchTransferTarget currentTarget;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pointerId != int.MinValue)
            return;
        pointerId = eventData.pointerId;
        UpdateTarget(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId == pointerId)
            UpdateTarget(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId)
            return;

        UpdateTarget(eventData);
        AbilityTouchTransferTarget releasedTarget = currentTarget;
        currentTarget = null;
        pointerId = int.MinValue;
        releasedTarget?.ReleaseTransferredTouch(eventData.pointerId);
    }

    public void OnCancel(BaseEventData eventData)
    {
        CancelTransfer();
    }

    private void OnDisable()
    {
        CancelTransfer();
    }

    private void UpdateTarget(PointerEventData eventData)
    {
        AbilityTouchTransferTarget nextTarget = FindTarget(eventData);
        if (nextTarget == currentTarget)
            return;

        currentTarget?.CancelTransferredTouch(pointerId);
        currentTarget = nextTarget != null && nextTarget.BeginTransferredTouch(pointerId)
            ? nextTarget
            : null;
    }

    private AbilityTouchTransferTarget FindTarget(PointerEventData eventData)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return null;

        raycastResults.Clear();
        eventSystem.RaycastAll(eventData, raycastResults);
        for (int i = 0; i < raycastResults.Count; i++)
        {
            AbilityTouchTransferTarget target =
                raycastResults[i].gameObject.GetComponentInParent<AbilityTouchTransferTarget>();
            if (target != null)
                return target;
        }
        return null;
    }

    private void CancelTransfer()
    {
        currentTarget?.CancelTransferredTouch(pointerId);
        currentTarget = null;
        pointerId = int.MinValue;
    }
}
