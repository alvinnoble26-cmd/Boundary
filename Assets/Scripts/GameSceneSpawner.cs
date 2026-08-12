using UnityEngine;
using PurrNet;

public class GameSceneSpawner : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab; // your Player prefab (must be in NetworkPrefabs)
    private NetworkManager nm;

    void Awake()
    {
        nm = FindObjectOfType<NetworkManager>();
    }

    void Start()
    {
        // If we aren't hosting, don't spawn here (clients will get their player from server)
        // If your PurrNet has a property like nm.IsServer / nm.IsHost, use it.
        // For now, we just attempt on host and you'll see logs.
        Debug.Log("GameSceneSpawner Start() - attempting spawn");

        var sp = GameObject.FindGameObjectWithTag("SpawnPoint");
        var pos = sp ? sp.transform.position : Vector3.zero;
        var rot = sp ? sp.transform.rotation : Quaternion.identity;

        // IMPORTANT: This line depends on PurrNet API.
        // Your version likely has something like nm.Spawn(playerPrefab, pos, rot) or NetworkPrefab.Instantiate(...)
        // If you tell me what methods show up on `nm.` autocomplete, I will give the exact correct spawn call.
    }
}
