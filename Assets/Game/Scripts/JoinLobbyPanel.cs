using UnityEngine;
using TMPro;

public class JoinLobbyPanel : MonoBehaviour
{
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private GameObject errorTextObject;

    private void Start()
    {
        if (errorTextObject != null)
            errorTextObject.SetActive(false);

        if (codeInput != null)
        {
            codeInput.characterLimit = 4;
            codeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        }
    }
}