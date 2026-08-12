using UnityEngine;

public class MultiplayerMenu : MonoBehaviour
{
    public void ConnectEdgegap()
    {
        if (GameManager.I == null)
        {
            Debug.LogError("[MultiplayerMenu] GameManager.I is null. Start from Boot scene (or ensure GameManager exists).");
            return;
        }
        GameManager.I.ConnectClient();
    }

    public void Disconnect()
    {
        if (GameManager.I == null) return;
        GameManager.I.DisconnectToMenu();
    }

    public void PlayOffline()
    {
        if (GameManager.I == null) return;
        GameManager.I.PlayOffline();
    }
}
