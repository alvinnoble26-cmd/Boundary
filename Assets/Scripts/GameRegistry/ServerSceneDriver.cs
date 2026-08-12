using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using PurrNet;

public class ServerSceneDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkManager net;

    [Header("Scene Loading")]
    [SerializeField] private string gameSceneName = "Game";
    private bool loaded;

    private void Awake()
    {
        if (!IsServerBuild())
        {
            Destroy(gameObject);
            return;
        }

        Debug.Log("[ServerSceneDriver] VERSION_10_PRELOAD_GAME_SINGLE");
        Debug.Log("[ServerSceneDriver] Awake() in scene: " + SceneManager.GetActiveScene().name);

        ResolveNetworkManager();
    }

    private IEnumerator Start()
    {
        if (!IsServerBuild())
            yield break;

        Debug.Log("[ServerSceneDriver] Server build confirmed. Waiting for NetworkManager...");

        yield return null;

        ResolveNetworkManager();

        if (net == null)
        {
            Debug.LogError("[ServerSceneDriver] No NetworkManager found.");
            yield break;
        }

        if (net.sceneModule == null)
        {
            Debug.LogError("[ServerSceneDriver] sceneModule is NULL.");
            yield break;
        }

        float serverWait = 0f;

        while (!net.isServer && serverWait < 10f)
        {
            serverWait += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("[ServerSceneDriver] After wait: isServer=" + net.isServer + " serverState=" + net.serverState);

        if (!net.isServer)
        {
            Debug.LogError("[ServerSceneDriver] Timed out waiting for server.");
            yield break;
        }

        // Register the authoritative Game scene before clients arrive. Waiting
        // in Boot for playerCount creates a deadlock: clients wait for the
        // server's scene assignment while the server waits for fully registered
        // players. GameManager pauses the arena separately until both spawned
        // PlayerMovement objects exist, so preloading does not start the round.
        Debug.Log("[ServerSceneDriver] Server ready. Preloading network Game scene.");
        LoadGameScene();
    }

    private void OnDestroy()
    {
    }

    private void ResolveNetworkManager()
    {
        if (net != null)
            return;

        net = NetworkManager.main != null
            ? NetworkManager.main
            : FindObjectOfType<NetworkManager>(true);
    }

    private void LoadGameScene()
    {
        if (loaded)
            return;

        loaded = true;

        Debug.Log("[ServerSceneDriver] Loading authoritative scene SINGLE: " + gameSceneName);

        net.sceneModule.LoadSceneAsync(gameSceneName, LoadSceneMode.Single);
    }

    private static bool IsServerBuild()
    {
        return Application.isBatchMode ||
               SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
    }
}
