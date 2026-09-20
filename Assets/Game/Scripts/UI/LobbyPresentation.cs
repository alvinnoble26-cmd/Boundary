using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LobbyPresentation : MonoBehaviour
{
    [SerializeField] private TMP_Text codeText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject waitingVisual;
    [SerializeField] private GameObject opponentVisual;
    [SerializeField] private TMP_Text opponentNameText;
    [SerializeField] private Image progressLine;

    public void Configure(TMP_Text code, TMP_Text status, GameObject waiting, GameObject opponent, TMP_Text opponentName, Image progress)
    {
        codeText = code;
        statusText = status;
        waitingVisual = waiting;
        opponentVisual = opponent;
        opponentNameText = opponentName;
        progressLine = progress;
        SetWaiting();
    }

    public void SetWaiting()
    {
        if (waitingVisual != null) waitingVisual.SetActive(true);
        if (opponentVisual != null) opponentVisual.SetActive(false);
        SetStatus("WAITING FOR OPPONENT…");
    }

    public void SetOpponentJoined(string playerName)
    {
        if (waitingVisual != null) waitingVisual.SetActive(false);
        if (opponentVisual != null) opponentVisual.SetActive(true);
        if (opponentNameText != null) opponentNameText.text = string.IsNullOrWhiteSpace(playerName) ? "OPPONENT" : playerName.ToUpperInvariant();
        SetStatus("OPPONENT CONNECTED");
    }

    public void SetStatus(string value) { if (statusText != null) statusText.text = value ?? string.Empty; }
    public void SetProgress(float value) { if (progressLine != null) progressLine.fillAmount = Mathf.Clamp01(value); }
    public void CopyCode() { if (codeText != null) GUIUtility.systemCopyBuffer = codeText.text; }

    private void Update()
    {
        if (statusText == null || waitingVisual == null || !waitingVisual.activeSelf) return;
        Color color = statusText.color;
        color.a = Mathf.Lerp(0.55f, 1f, (Mathf.Sin(Time.unscaledTime * 2.4f) + 1f) * 0.5f);
        statusText.color = color;
    }
}
