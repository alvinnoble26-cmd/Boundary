using UnityEngine;
using UnityEngine.UI;

public class MenuUIController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject serverSelectorPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private GameObject winPanel;

    [Header("Lose Screen UI")]
    [SerializeField] private Text loseReasonText;

    [Header("Win Screen UI")]
    [SerializeField] private Text winReasonText;

    private bool leavingResultScreen;

    private void Start()
    {
        if (GameManager.I == null)
        {
            ShowMainMenu();
            return;
        }

        if (GameManager.I.ConsumeReturnToServerSelector())
        {
            ShowServerSelector();
        }
        else if (GameManager.I.lastMatchResult == GameManager.MatchResult.Loss)
        {
            ShowLose(GameManager.I.lastEndReason);
        }
        else if (GameManager.I.lastMatchResult == GameManager.MatchResult.Win)
        {
            ShowWin(GameManager.I.lastEndReason);
        }
        else
        {
            ShowMainMenu();
        }
    }

    private void OnEnable()
    {
        if (GameManager.I != null)
            GameManager.I.RematchStatusChanged += ShowRematchStatus;
    }

    private void OnDisable()
    {
        if (GameManager.I != null)
            GameManager.I.RematchStatusChanged -= ShowRematchStatus;
    }

    private void ShowMainMenu()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (serverSelectorPanel != null)
            serverSelectorPanel.SetActive(false);

        if (losePanel != null)
            losePanel.SetActive(false);

        if (winPanel != null)
            winPanel.SetActive(false);
    }

    private void ShowServerSelector()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (serverSelectorPanel != null)
            serverSelectorPanel.SetActive(true);

        if (losePanel != null)
            losePanel.SetActive(false);

        if (winPanel != null)
            winPanel.SetActive(false);
    }

    private void ShowWin(string reason)
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (serverSelectorPanel != null)
            serverSelectorPanel.SetActive(false);

        if (losePanel != null)
            losePanel.SetActive(false);

        if (winPanel != null)
            winPanel.SetActive(true);

        if (winReasonText != null)
            winReasonText.text = string.IsNullOrEmpty(reason)
                ? "You won!"
                : reason;
    }

    private void ShowLose(string reason)
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        // The server/multiplayer selector is a sibling of the main menu in
        // Menu.unity. Explicitly close it when a match ends so returning from
        // the loss screen cannot reveal two menu stacks at once.
        if (serverSelectorPanel != null)
            serverSelectorPanel.SetActive(false);

        if (winPanel != null)
            winPanel.SetActive(false);

        if (losePanel != null)
            losePanel.SetActive(true);

        if (loseReasonText != null)
            loseReasonText.text = string.IsNullOrEmpty(reason)
                ? "You lost!"
                : reason;
    }

    public void ContinueToMainMenu()
    {
        if (leavingResultScreen) return;
        leavingResultScreen = true;
        if (losePanel != null) losePanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (GameManager.I != null)
            GameManager.I.ClearLastResult();

        // A completed multiplayer match returns to its multiplayer selector,
        // not the play/start panel. This keeps the two menu stacks mutually
        // exclusive after pressing Back on the loss or win screen.
        ShowServerSelector();
    }

    public void PlayAgain()
    {
        if (GameManager.I != null)
            GameManager.I.RequestPlayAgain();
    }

    private void ShowRematchStatus(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        if (winPanel != null && winPanel.activeSelf && winReasonText != null)
            winReasonText.text = message;

        if (losePanel != null && losePanel.activeSelf && loseReasonText != null)
            loseReasonText.text = message;
    }
}
