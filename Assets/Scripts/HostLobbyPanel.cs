using UnityEngine;
using TMPro;

public class HostLobbyPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text codeText;

    public void GenerateCode()
    {
        string code = Random.Range(1000, 10000).ToString();
        codeText.text = code;

        Debug.Log("[Lobby] Host code: " + code);
    }
}