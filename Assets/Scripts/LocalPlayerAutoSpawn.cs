using System.Collections;
using UnityEngine;
using PurrNet;

public class LocalPlayerAutoSpawn : MonoBehaviour
{
    [Header("Drag your PLAYER prefab that has NetworkIdentity/PlayerIdentity")]
    [SerializeField] private NetworkIdentity playerPrefab;

    [Header("Optional spawn points (assign SpawnA/SpawnB...)")]
    [SerializeField] private Transform[] spawnPoints;

    private static bool spawnedThisRun;

    private IEnumerator Start()
    {
        if (spawnedThisRun) yield break;

        // Wait until a NetworkManager exists and is connected/running
        var nm = FindObjectOfType<NetworkManager>();
        float timeout = 5f;
        while (nm == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            nm = FindObjectOfType<NetworkManager>();
            yield return null;
        }

        // Give PurrNet a couple frames to finish connecting
        yield return null;
        yield return null;

        if (playerPrefab == null)
        {
            Debug.LogError("[LocalPlayerAutoSpawn] playerPrefab not assigned.");
            yield break;
        }

        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;

        if (spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[0] != null)
        {
            // Simple: pick a random spawn (or you can do round-robin later)
            var sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
            pos = sp.position;
            rot = sp.rotation;
        }

        // PurrNet: Instantiating a NetworkIdentity prefab triggers a network spawn (auto spawn)
        Instantiate(playerPrefab, pos, rot);

        spawnedThisRun = true;
        Debug.Log("[LocalPlayerAutoSpawn] Spawned local player.");
    }
}
