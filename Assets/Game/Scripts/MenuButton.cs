using UnityEngine;

public class MenuButtons : MonoBehaviour
{
    public void ConnectEdgegap()
    {
        GameManager.I.ConnectClient();
    }

    public void Disconnect()
    {
        GameManager.I.DisconnectToMenu();
    }

    public void PlayOffline()
    {
        GameManager.I.PlayPractice();
    }

    public void PlayPractice()
    {
        GameManager.I.PlayPractice();
    }
}
