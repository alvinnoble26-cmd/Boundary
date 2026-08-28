using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Runs only in a dedicated server build. Edgegap injects a one-time self-stop
/// URL and token into the container. Firebase handles normal post-match and
/// abandoned-lobby cleanup; this component is only the server's hard lifetime
/// guardrail.
/// </summary>
public sealed class EdgegapServerLifecycle : MonoBehaviour
{
    private const float MaximumDeploymentLifetimeSeconds = 10f * 60f;

    private string deleteUrl;
    private string deleteToken;
    private bool stopRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForDedicatedServer()
    {
        if (!IsServerBuild())
            return;

        if (FindFirstObjectByType<EdgegapServerLifecycle>() != null)
            return;

        GameObject lifecycleObject = new GameObject("EdgegapServerLifecycle");
        DontDestroyOnLoad(lifecycleObject);
        lifecycleObject.AddComponent<EdgegapServerLifecycle>();
    }

    private IEnumerator Start()
    {
        deleteUrl = Environment.GetEnvironmentVariable("ARBITRIUM_DELETE_URL");
        deleteToken = Environment.GetEnvironmentVariable("ARBITRIUM_DELETE_TOKEN");

        if (string.IsNullOrWhiteSpace(deleteUrl) || string.IsNullOrWhiteSpace(deleteToken))
        {
            Debug.Log("[EdgegapLifecycle] Edgegap self-stop variables not present. Lifecycle disabled.");
            yield break;
        }

        Debug.Log("[EdgegapLifecycle] Ten-minute deployment lifetime armed. " +
                  "Firebase controls normal post-match cleanup.");
        yield return new WaitForSecondsRealtime(MaximumDeploymentLifetimeSeconds);
        yield return StopDeployment("10-minute maximum lifetime reached");
    }

    private IEnumerator StopDeployment(string reason)
    {
        if (stopRequested)
            yield break;

        stopRequested = true;
        Debug.Log("[EdgegapLifecycle] Stopping Edgegap deployment: " + reason);

        using (UnityWebRequest request = UnityWebRequest.Delete(deleteUrl))
        {
            request.SetRequestHeader("Authorization", deleteToken);
            request.timeout = 20;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success ||
                request.responseCode == 200 || request.responseCode == 202)
            {
                Debug.Log("[EdgegapLifecycle] Edgegap accepted deployment shutdown.");
            }
            else
            {
                Debug.LogError("[EdgegapLifecycle] Deployment shutdown failed. HTTP=" +
                               request.responseCode + " Error=" + request.error);
                stopRequested = false;
            }
        }
    }

    private static bool IsServerBuild()
    {
        return Application.isBatchMode ||
               SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
    }
}
