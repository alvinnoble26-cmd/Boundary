using System.Collections;
using PurrNet;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Installs the Boundary event without relying on fragile scene object IDs.
/// The server spawns exactly one registered match director; clients receive it
/// through PurrNet and create only their local HUD.
/// </summary>
public sealed class BoundaryRuntimeBootstrap : MonoBehaviour
{
    private Coroutine spawnRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<BoundaryRuntimeBootstrap>() != null)
            return;

        GameObject host = new GameObject("Boundary Runtime");
        DontDestroyOnLoad(host);
        host.AddComponent<BoundaryRuntimeBootstrap>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        ConfigureScene(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureScene(scene);
    }

    private void ConfigureScene(Scene scene)
    {
        if (scene.name != "Game")
            return;

        if (!Application.isBatchMode && FindFirstObjectByType<BoundaryHUD>() == null)
        {
            GameObject hud = new GameObject("Boundary HUD");
            SceneManager.MoveGameObjectToScene(hud, scene);
            hud.AddComponent<BoundaryHUD>();
        }

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnDirectorWhenReady(scene));
    }

    private IEnumerator SpawnDirectorWhenReady(Scene gameScene)
    {
        yield return new WaitForSecondsRealtime(0.5f);

        float timeout = 20f;
        while (timeout > 0f)
        {
            if (!gameScene.IsValid() || !gameScene.isLoaded)
                yield break;

            if (BoundaryMatchController.Instance != null ||
                FindFirstObjectByType<BoundaryMatchController>() != null)
                yield break;

            NetworkManager manager = NetworkManager.main;
            if (manager != null && manager.isServer)
                break;

            timeout -= 0.1f;
            yield return new WaitForSecondsRealtime(0.1f);
        }

        NetworkManager network = NetworkManager.main;
        if (network == null || !network.isServer)
            yield break;

        GameObject prefab = Resources.Load<GameObject>("Boundary/BoundaryMatchDirector");
        if (prefab == null)
        {
            Debug.LogError("[Boundary] BoundaryMatchDirector prefab is missing from Resources.");
            yield break;
        }

        Vector3 singularityPosition = ResolveSingularityPosition(gameScene);
        GameObject director = Instantiate(prefab, singularityPosition, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(director, gameScene);
        NetworkIdentity.Spawn(director, prefab, network);
        Debug.Log("[Boundary] Match director spawned at " + singularityPosition);
        spawnRoutine = null;
    }

    private static Vector3 ResolveSingularityPosition(Scene gameScene)
    {
        BlackKill[] cores = FindObjectsByType<BlackKill>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        BlackKill best = null;
        foreach (BlackKill core in cores)
        {
            if (core == null || core.gameObject.scene != gameScene)
                continue;
            Vector2 coreFlat = new Vector2(core.transform.position.x, core.transform.position.z);
            Vector2 bestFlat = best != null
                ? new Vector2(best.transform.position.x, best.transform.position.z)
                : Vector2.one * float.MaxValue;
            if (best == null || coreFlat.sqrMagnitude < bestFlat.sqrMagnitude)
                best = core;
        }

        if (best != null)
            return new Vector3(best.transform.position.x, 32f, best.transform.position.z);

        return new Vector3(0f, 32f, 0f);
    }
}
