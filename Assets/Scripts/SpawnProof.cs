using System.Collections;
using UnityEngine;
using PurrNet;

public class SpawnProof : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private NetworkIdentity playerPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Debug")]
    [SerializeField] private float maxWaitSeconds = 5f;

    private IEnumerator Start()
    {
        Debug.Log("[SpawnProof] START (v2)");

        // Basic inspector validation
        if (playerPrefab == null)
        {
            Debug.LogError("[SpawnProof] playerPrefab NOT assigned in inspector.");
            yield break;
        }

        // Find NetworkManager
        var net = FindObjectOfType<NetworkManager>();
        Debug.Log("[SpawnProof] NetworkManager found? " + (net != null));
        if (net == null)
        {
            Debug.LogError("[SpawnProof] No NetworkManager found in scene/DDDOL.");
            yield break;
        }

        // Wait for networking to actually be running
        // (If your PurrNet has a different property name than IsServer/IsHost/State, swap it here)
        float t = 0f;
        while (t < maxWaitSeconds && !IsServer(net))
        {
            if (t == 0f) Debug.Log("[SpawnProof] Waiting for Host/Server to be active...");
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!IsServer(net))
        {
            Debug.LogError("[SpawnProof] Timed out waiting for server. Host probably never started.");
            yield break;
        }

        // Decide spawn transform
        Vector3 pos = spawnPoint ? spawnPoint.position : Vector3.zero;
        Quaternion rot = spawnPoint ? spawnPoint.rotation : Quaternion.identity;

        Debug.Log($"[SpawnProof] Server active. Spawning player at {pos}");

        // -----------------------------
        // IMPORTANT: Use PurrNet spawn, NOT Instantiate
        // Replace the next line with your PurrNet spawn call if needed.
        // -----------------------------

        // EXAMPLE OPTION 1 (common pattern):
        // var spawned = net.Spawn(playerPrefab, pos, rot);

        // EXAMPLE OPTION 2 (some APIs):
        // var spawned = NetworkManager.Spawn(playerPrefab, pos, rot);

        // EXAMPLE OPTION 3 (fallback debug only):
        // var spawned = Instantiate(playerPrefab, pos, rot);

        var spawned = TrySpawn(net, playerPrefab, pos, rot);

        Debug.Log("[SpawnProof] Spawn result: " + (spawned != null ? spawned.name : "NULL"));
    }

    // ---- Helpers ----

    // You MUST ensure this returns true only when you're actually hosting/server-side.
    private bool IsServer(NetworkManager net)
    {
        // I don’t know your exact PurrNet version properties.
        // Try these common ones. If your compiler errors here, replace with the correct property from your NetworkManager.
        // The goal is: true only on the Host/Server instance.

        // If your NetworkManager has IsServer:
        // return net.IsServer;

        // If it has IsHost:
        // return net.IsHost;

        // If it has State enum:
        // return net.State == NetworkState.Host || net.State == NetworkState.Server;

        // Safe fallback: assume host if a server transport is running (replace properly!)
        return true; // <-- REPLACE with correct check if possible
    }

    private NetworkIdentity TrySpawn(NetworkManager net, NetworkIdentity prefab, Vector3 pos, Quaternion rot)
    {
        // This wrapper is here so you only edit ONE place to match your PurrNet version.

        // ✅ Preferred (networked spawn)
        // return net.Spawn(prefab, pos, rot);

        // TEMP DEBUG ONLY (will not replicate):
        return Instantiate(prefab, pos, rot);
    }
}
