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
    try
    {
        if (FirebaseManager.I == null) return;
        await FirebaseManager.I.RefreshSkinDataAsync();
        RequestSelectedSkin(FirebaseManager.I.SelectedSkin);
    }
    catch (System.Exception e)
    {
        Debug.LogError("[PlayerAbilities] Could not load equipped skin: " + e.Message);
        RequestSelectedSkin("beard");
    }
}

[ServerRpc]
private void RequestSelectedSkin(string skinId)
{
    SyncSelectedSkin(skinId == "sun_ducker" ? "sun_ducker" : "beard");
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
    Transform oldSun = tilt != null ? tilt.Find("EquippedSunDucker") : null;
    if (oldSun != null) Destroy(oldSun.gameObject);

    bool useSun = skinId == "sun_ducker";
    if (beardBody != null) beardBody.gameObject.SetActive(!useSun);
    if (beardEye != null) beardEye.gameObject.SetActive(!useSun);
    if (!useSun || tilt == null) return;

    GameObject templates = GameObject.Find("skins");
    Transform source = templates != null ? templates.transform.Find("Sun Ducker") : null;
    if (source == null)
    {
        Debug.LogError("[PlayerAbilities] Game scene skin template 'skins/Sun Ducker' was not found.");
        if (beardBody != null) beardBody.gameObject.SetActive(true);
        if (beardEye != null) beardEye.gameObject.SetActive(true);
        return;
    }

    GameObject clone = Instantiate(source.gameObject, tilt);
    clone.name = "EquippedSunDucker";
    clone.SetActive(true);
    clone.transform.localPosition = Vector3.zero;
    clone.transform.localRotation = Quaternion.identity;
    clone.transform.localScale = Vector3.one;
    SunDuckerDemonVisual.Build(clone.transform, clone.layer);
    foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true))
        Destroy(collider);
    foreach (Rigidbody body in clone.GetComponentsInChildren<Rigidbody>(true))
        Destroy(body);
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

    Debug.Log($"[PlayerAbilities] UseSlot {slotIndex} id={id}");

    PlayerMovement movement = GetComponent<PlayerMovement>();
    Transform aim = movement != null && movement.orientation != null
        ? movement.orientation
        : transform;
    ThrowPoint point = transform.root.GetComponentInChildren<ThrowPoint>(true);
    Vector3 spawnPosition = point != null ? point.transform.position : transform.position + aim.forward;
    Vector3 aimDirection = aim.forward.sqrMagnitude > 0.0001f ? aim.forward.normalized : transform.forward;

    RequestActivateAbility(id.Value, spawnPosition, aimDirection);
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
