using UnityEngine;
using System.Collections;

public class MainMenu : MonoBehaviour
{
    private IEnumerator Start()
    {
        // Scene-loaded callbacks can occur before inactive menu children are
        // registered. Waiting one frame guarantees the Skins button is available.
        yield return null;
        SkinShopUI.EnsureInstalled();
    }

    public void PlayGame()
    {
        Debug.Log("[MainMenu] PlayGame clicked.");

        // Host lobby is now handled by the Host Button OnClick:
        // 1. MultiplayerPanel.SetActive(false)
        // 2. HostLobbyPanel.SetActive(true)
        // 3. FirebaseLobbyManager.CreateLobby()
    }

    public void Disconnect()
    {
        if (GameManager.I == null)
        {
            Debug.LogWarning("[MainMenu] GameManager.I is null. Cannot disconnect.");
            return;
        }

        GameManager.I.DisconnectToMenu();
    }

    public void Offline()
    {
        if (GameManager.I == null)
        {
            Debug.LogWarning("[MainMenu] GameManager.I is null. Cannot play offline.");
            return;
        }

        GameManager.I.PlayPractice();
    }
}
