using UnityEngine;
using TMPro;

public class LocalLobbyCodeTester : MonoBehaviour
{
}
  /*  [Header("Host")]
    [SerializeField] private TMP_Text hostCodeText;

    [Header("Join")]
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private GameObject errorTextObject;

    private string currentCode;
    private float codeCreatedTime;
    private readonly float codeLifetime = 600f;

    private void Awake()
    {
        if (errorTextObject != null)
            errorTextObject.SetActive(false);

        if (codeInput != null)
            codeInput.onValueChanged.AddListener(HideError);
    }

    public void GenerateHostCode()
    {
        currentCode = Random.Range(1000, 10000).ToString();
        codeCreatedTime = Time.time;

        if (hostCodeText != null)
            hostCodeText.text = currentCode;

        Debug.Log("[Lobby] Generated host code: " + currentCode);
    }

    public void TryJoinWithCode()
    {
        string typedCode = codeInput.text.Trim();

        Debug.Log("[Lobby] Typed: " + typedCode);
        Debug.Log("[Lobby] Real host code: " + currentCode);

        if (typedCode.Length != 4 || !int.TryParse(typedCode, out _))
        {
            ShowError();
            Debug.Log("[Lobby] Invalid: not 4 digits");
            return;
        }

        if (string.IsNullOrEmpty(currentCode))
        {
            ShowError();
            Debug.Log("[Lobby] Invalid: no active host code");
            return;
        }

        if (Time.time - codeCreatedTime > codeLifetime)
        {
            ShowError();
            Debug.Log("[Lobby] Invalid: code expired");
            return;
        }

        if (typedCode != currentCode)
        {
            ShowError();
            Debug.Log("[Lobby] Invalid: code does not match");
            return;
        }

        if (errorTextObject != null)
            errorTextObject.SetActive(false);

        Debug.Log("[Lobby] VALID CODE. Join allowed.");
    }

    private void ShowError()
    {
        if (errorTextObject != null)
            errorTextObject.SetActive(true);
        else
            Debug.LogError("[Lobby] errorTextObject is not assigned.");
    }

    private void HideError(string value)
    {
        if (errorTextObject != null)
            errorTextObject.SetActive(false);
    }
}
*/