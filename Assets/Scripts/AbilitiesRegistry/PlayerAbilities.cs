using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using PurrNet; 
using PurrNet.Modules;

public class PlayerAbilities : NetworkBehaviour 
{
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
    private readonly AbilityCooldownButton[] cooldownVisuals = new AbilityCooldownButton[3];
    private bool hasStartedSkinLoad;


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
    // Enable Camera
    Camera cam = GetComponentInChildren<Camera>(true);
    if (cam != null)
    {
        cam.enabled = true;
        cam.gameObject.tag = "MainCamera";
        Debug.Log("[PlayerAbilities] Camera Enabled.");
    }

    AudioListener listener = GetComponentInChildren<AudioListener>(true);
    if (listener != null) listener.enabled = true;

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

private async void LoadAndSyncSelectedSkin()
{
    if (hasStartedSkinLoad)
        return;

    hasStartedSkinLoad = true;
    string selectedSkin = FirebaseManager.I != null
        ? FirebaseManager.I.SelectedSkin
        : "beard";

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
    if (!useCustomSkin || tilt == null) return;

    GameObject templates = GameObject.Find("skins");
    string templateName = useTurtle ? "Turtle" : "Sun Ducker";
    Transform source = templates != null ? templates.transform.Find(templateName) : null;
    if (source == null)
    {
        Debug.LogError($"[PlayerAbilities] Game scene skin template 'skins/{templateName}' was not found.");
        if (beardBody != null) beardBody.gameObject.SetActive(true);
        if (beardEye != null) beardEye.gameObject.SetActive(true);
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
        SyncLoadoutToObservers(selectedIds);
    }

[ObserversRpc]
private void SyncLoadoutToObservers(AbilityId[] selectedIds)
{
    if (isOwner) return;

    for (int i = 0; i < slots.Length && i < selectedIds.Length; i++)
    {
        slots[i] = selectedIds[i];
    }
    
    // Fixed line
    Debug.Log($"Synced {selectedIds.Length} abilities for player {owner?.id.ToString() ?? "Unknown"}");
}

    [ServerRpc]
    private void RequestActivateAbility(AbilityId id, Vector3 spawnPosition, Vector3 aimDirection)
    {
        if (id == AbilityId.BlackThrow || id == AbilityId.AttractThrow || id == AbilityId.RepelThrow)
        {
            // Physical projectiles are instantiated exactly once on the server.
            // Their NetworkTransform then creates and moves the same projectile
            // for every observer.
            ActivateNetworkThrow(id, spawnPosition, aimDirection);
            return;
        }

        ObserversActivateAbility(id);
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
private void ObserversActivateAbility(AbilityId id)
{
    Debug.Log($"[PlayerAbilities] ObserversActivateAbility id={id} isOwner={isOwner} registry={registry != null}");
    
    ActivateAbility(id);
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
        btn.onClick.AddListener(() => UseSlot(slotIndex));
    }

void UseSlot(int slotIndex)
{
    var id = slots[slotIndex];
    if (id == null)
    {
        Debug.LogError($"[PlayerAbilities] Slot {slotIndex} is null!");
        return;
    }

    if (Time.time < slotCooldownEnds[slotIndex])
        return;

    Debug.Log($"[PlayerAbilities] UseSlot {slotIndex} id={id}");

    float cooldownDuration = GetCooldownDuration(id.Value);
    slotCooldownEnds[slotIndex] = Time.time + cooldownDuration;
    cooldownVisuals[slotIndex]?.BeginCooldown(cooldownDuration);

    PlayerMovement movement = GetComponent<PlayerMovement>();
    Transform aim = movement != null && movement.orientation != null
        ? movement.orientation
        : transform;
    Camera ownerCamera = GetComponentInChildren<Camera>(true);
    ThrowPoint point = transform.root.GetComponentInChildren<ThrowPoint>(true);
    Vector3 spawnPosition = point != null ? point.transform.position : transform.position + aim.forward;
    Vector3 cameraDirection = ownerCamera != null ? ownerCamera.transform.forward : aim.forward;
    Vector3 aimDirection = cameraDirection.sqrMagnitude > 0.0001f
        ? cameraDirection.normalized
        : transform.forward;

    RequestActivateAbility(id.Value, spawnPosition, aimDirection);
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
