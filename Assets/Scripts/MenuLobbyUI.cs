using UnityEngine;
using TMPro;

public class MenuLobbyUI : MonoBehaviour
{
    public static MenuLobbyUI I { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject hostLobbyPanel;

    [Header("Text")]
    [SerializeField] private TMP_Text hostCodeText;

    private void Awake()
    {
        I = this;

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (hostLobbyPanel != null)
            hostLobbyPanel.SetActive(false);
    }

    public void ShowHostLobby(string code)
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (hostLobbyPanel != null)
            hostLobbyPanel.SetActive(true);

        if (hostCodeText != null)
            hostCodeText.text = code;
    }
}