using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using PurrNet; // adjust if your namespace differs

public class StartHostAndLoadGame : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";

    private NetworkManager nm;

    void Awake()
    {
        nm = FindObjectOfType<NetworkManager>();
    }

    public void HostAndStart()
    {
        StartCoroutine(HostThenLoad());
    }

    private IEnumerator HostThenLoad()
    {
        nm.StartHost();

        // wait a couple frames so PurrNet initializes
        yield return null;
        yield return null;

        SceneManager.LoadScene(gameSceneName);
    }
}
