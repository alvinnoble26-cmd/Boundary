using UnityEngine;
using UnityEngine.SceneManagement;

public class ServerEntry : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("Scene name for the actual match/gameplay.")]
    public string gameSceneName = "Game";

    [Header("PurrNet")]
    [Tooltip("Reference to your PurrNet Network Manager object/component.")]
    public MonoBehaviour purrNetNetworkManager; // drag your NetworkManager component here

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (IsDedicatedServer())
        {
            Debug.Log("[ServerEntry] Dedicated server detected. Starting server + loading game scene...");

            StartPurrNetServer();   // <-- you will wire this to the correct PurrNet call
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.Log("[ServerEntry] Not a dedicated server. Continue normal menu flow.");
        }
    }

    bool IsDedicatedServer()
    {
        // UNITY_SERVER is defined in Dedicated Server builds.
#if UNITY_SERVER
        return true;
#else
        // Optional: allow running server mode in editor/player with a flag:
        // e.g. -dedicatedServer
        return System.Array.Exists(System.Environment.GetCommandLineArgs(),
            arg => arg == "-dedicatedServer");
#endif
    }

    void StartPurrNetServer()
    {
        if (purrNetNetworkManager == null)
        {
            Debug.LogError("[ServerEntry] purrNetNetworkManager not assigned!");
            return;
        }

        // IMPORTANT:
        // I can’t safely guess the exact PurrNet API method name in your version.
        // Open the component type in the inspector and look for methods like:
        // StartServer(), StartHost(), StartAsServer(), Start(), etc.

        // OPTION A (best): call the real method directly once you know it:
        // ((YourPurrNetNetworkManagerType)purrNetNetworkManager).StartServer();

        // OPTION B (works even if you don’t know the exact method name): reflection fallback.
        var t = purrNetNetworkManager.GetType();

        var m =
            t.GetMethod("StartServer") ??
            t.GetMethod("StartHost") ??
            t.GetMethod("StartAsServer") ??
            t.GetMethod("Start");

        if (m == null)
        {
            Debug.LogError($"[ServerEntry] Could not find a server start method on {t.Name}. " +
                           "Look for StartServer/StartHost on your PurrNet NetworkManager.");
            return;
        }

        m.Invoke(purrNetNetworkManager, null);
        Debug.Log($"[ServerEntry] Invoked {t.Name}.{m.Name}()");
    }
}
