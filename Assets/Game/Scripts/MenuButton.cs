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
        PracticeModePanel.Show();
    }

    public void PlayPractice()
    {
        PracticeModePanel.Show();
    }
}
