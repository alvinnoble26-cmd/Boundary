using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Firebase;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseLobbyManager : MonoBehaviour
{
    private const int LobbyMinutesUntilExpire = 10;

    public static FirebaseLobbyManager I { get; private set; }

    [Header("Edgegap Automation")]
    [SerializeField] private string createDeploymentUrl =
        "https://us-central1-entropy-7c113.cloudfunctions.net/createEdgegapDeployment";
    [SerializeField] private float serverReadyTimeoutSeconds = 180f;

    [Header("UI")]
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private TMP_Text hostCodeText;
    [SerializeField] private TMP_Text errorText;

    [Header("Lobby Settings")]
    [SerializeField] private int codeLength = 4;

    private FirebaseFirestore db;
    private ListenerRegistration lobbyListener;

    private string currentLobbyCode = "";
    private bool matchStarted;
    private bool joinInProgress;
    private bool createInProgress;
    private Coroutine hostPollRoutine;
    private Coroutine deploymentRequestRoutine;

    [Serializable]
    private class DeploymentRequest
    {
        public string lobbyCode;
    }

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;

        if (errorText != null)
            errorText.gameObject.SetActive(false);

        if (codeInput != null)
        {
            codeInput.characterLimit = codeLength;
            codeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            codeInput.onValueChanged.AddListener(OnCodeTyped);
        }
    }

    private void Start()
    {
 db = FirebaseFirestore.DefaultInstance;

try
{
    db.Settings.PersistenceEnabled = false;
    Debug.Log("[FirebaseLobby] Firestore ready. Persistence disabled.");
}
catch (System.InvalidOperationException)
{
    Debug.LogWarning("[FirebaseLobby] Firestore settings already locked. Continuing.");
}
    }

    private void OnDestroy()
    {
        StopLobbyListener();

        if (hostPollRoutine != null)
        {
            StopCoroutine(hostPollRoutine);
            hostPollRoutine = null;
        }

        if (deploymentRequestRoutine != null)
        {
            StopCoroutine(deploymentRequestRoutine);
            deploymentRequestRoutine = null;
        }

        if (codeInput != null)
            codeInput.onValueChanged.RemoveListener(OnCodeTyped);
    }

    // Hook this to your Host button.
    public async void CreateLobby()
    {
        if (createInProgress)
            return;

        HideError();
        createInProgress = true;

        try
        {
            Firebase.Auth.FirebaseUser account = FirebaseManager.I != null
                ? await FirebaseManager.I.EnsureSignedInAsync()
                : null;
            if (account == null)
                throw new InvalidOperationException("Player account is not ready.");

            if (db == null)
                db = FirebaseFirestore.DefaultInstance;

            matchStarted = false;
            joinInProgress = false;

            string code = await GenerateUnusedLobbyCode();

            if (string.IsNullOrEmpty(code))
            {
                ShowError("Could not create lobby. Try again.");
                return;
            }

            currentLobbyCode = code;
            if (GameManager.I != null)
                GameManager.I.SetLobbyInfo(code, "host");

            DocumentReference lobbyRef = db.Collection("lobbies").Document(code);
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(LobbyMinutesUntilExpire);

            Dictionary<string, object> lobbyData = new Dictionary<string, object>
            {
                { "code", code },
                { "hostUid", account.UserId },
                { "joinerUid", "" },
                { "status", "waiting" },
                { "players", 1 },
                { "serverStatus", "deploying" },
                { "serverHost", "" },
                { "serverPort", 0 },
                { "serverError", "" },
                { "deploymentRequestId", "" },
                { "deploymentClaimed", false },
                { "createdAt", Timestamp.GetCurrentTimestamp() },
                { "expiresAt", Timestamp.FromDateTime(expiresAtUtc) },
                { "matchEnded", false },
                { "loserRole", "" },
                { "endReason", "" },
                { "hostRematchReady", false },
                { "joinerRematchReady", false },
                { "rematchRound", 0 }
            };

            await lobbyRef.SetAsync(lobbyData);

            Debug.Log("[FirebaseLobby] Created lobby: " + code);

            if (hostCodeText != null)
                hostCodeText.text = code;

            deploymentRequestRoutine = StartCoroutine(RequestEdgegapDeployment(code));
            StartListeningForLobbyFull(code);
            StartHostPolling(code);
        }
        catch (Exception e)
        {
            Debug.LogError("[FirebaseLobby] Failed to create lobby: " + e);

            bool permissionDenied = e.Message.IndexOf(
                "permission",
                StringComparison.OrdinalIgnoreCase) >= 0;

            ShowError(permissionDenied
                ? "Lobby access denied. Check Firestore rules."
                : "Failed to create lobby. Check the Console.");
        }
        finally
        {
            createInProgress = false;
        }
    }

    private async Task<string> GenerateUnusedLobbyCode()
    {
        for (int i = 0; i < 20; i++)
        {
            string code = UnityEngine.Random.Range(0, 10000).ToString("0000");

            DocumentSnapshot snapshot = await db.Collection("lobbies").Document(code).GetSnapshotAsync();

            if (!snapshot.Exists)
                return code;
        }

        return "";
    }

    private void OnCodeTyped(string value)
    {
        HideError();

        if (string.IsNullOrEmpty(value))
            return;

        if (value.Length < codeLength)
            return;

        if (joinInProgress)
            return;

        TryJoinLobby(value);
    }

    private async void TryJoinLobby(string code)
    {
        if (db == null)
            db = FirebaseFirestore.DefaultInstance;

        joinInProgress = true;
        HideError();

        Debug.Log("[FirebaseLobby] Trying to join lobby: " + code);

        DocumentReference lobbyRef = db.Collection("lobbies").Document(code);

        try
        {
            Firebase.Auth.FirebaseUser account = FirebaseManager.I != null
                ? await FirebaseManager.I.EnsureSignedInAsync()
                : null;
            if (account == null)
                throw new InvalidOperationException("Player account is not ready.");

            DocumentSnapshot snapshot = await lobbyRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                Debug.Log("[FirebaseLobby] Lobby does not exist.");
                ShowError("Invalid code.");
                joinInProgress = false;
                return;
            }

            string status = snapshot.ContainsField("status")
                ? snapshot.GetValue<string>("status")
                : "";

            int players = snapshot.ContainsField("players")
                ? snapshot.GetValue<int>("players")
                : 0;

            if (snapshot.ContainsField("expiresAt"))
            {
                Timestamp expiresAt = snapshot.GetValue<Timestamp>("expiresAt");
                DateTime expiresAtUtc = expiresAt.ToDateTime();

                if (DateTime.UtcNow > expiresAtUtc)
                {
                    Debug.Log("[FirebaseLobby] Lobby expired.");
                    ShowError("Lobby expired.");
                    joinInProgress = false;
                    return;
                }
            }

            if (status != "waiting")
            {
                Debug.Log("[FirebaseLobby] Lobby is not waiting. Status=" + status);
                ShowError("Lobby is full.");
                joinInProgress = false;
                return;
            }

            if (players >= 2)
            {
                Debug.Log("[FirebaseLobby] Lobby already has 2 players.");
                ShowError("Lobby is full.");
                joinInProgress = false;
                return;
            }

            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                { "players", 2 },
                { "status", "full" },
                { "joinerUid", account.UserId }
            };

            await lobbyRef.UpdateAsync(updates);

            Debug.Log("[FirebaseLobby] Joined lobby successfully: " + code);

            currentLobbyCode = code;
            if (GameManager.I != null)
                GameManager.I.SetLobbyInfo(code, "joiner");

            StartMatch();
        }
        catch (Exception e)
        {
            Debug.LogError("[FirebaseLobby] Failed to join lobby: " + e);
            ShowError("Failed to join lobby.");
            joinInProgress = false;
        }
    }

    private void StartListeningForLobbyFull(string code)
    {
        StopLobbyListener();

        Debug.Log("[FirebaseLobby] Host listening for lobby full: " + code);

        DocumentReference lobbyRef = db.Collection("lobbies").Document(code);

        lobbyListener = lobbyRef.Listen(snapshot =>
        {
            if (matchStarted)
                return;

            if (!snapshot.Exists)
                return;

            if (!snapshot.ContainsField("status"))
                return;

            string status = snapshot.GetValue<string>("status");

            Debug.Log("[FirebaseLobby] Listener status=" + status);

            if (status == "full")
            {
                Debug.Log("[FirebaseLobby] Listener detected full lobby.");
                StartMatch();
            }
        });
    }

    private void StartHostPolling(string code)
    {
        if (hostPollRoutine != null)
        {
            StopCoroutine(hostPollRoutine);
            hostPollRoutine = null;
        }

        hostPollRoutine = StartCoroutine(PollLobbyUntilFull(code));
    }

    private IEnumerator PollLobbyUntilFull(string code)
    {
        Debug.Log("[FirebaseLobby] Host polling for lobby full: " + code);

        while (!matchStarted)
        {
            if (db == null)
                db = FirebaseFirestore.DefaultInstance;

            Task<DocumentSnapshot> task = db.Collection("lobbies").Document(code).GetSnapshotAsync();

            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("[FirebaseLobby] Poll failed: " + task.Exception);
            }
            else
            {
                DocumentSnapshot snapshot = task.Result;

                if (snapshot.Exists && snapshot.ContainsField("status"))
                {
                    string status = snapshot.GetValue<string>("status");

                    Debug.Log("[FirebaseLobby] Poll status=" + status);

                    if (status == "full")
                    {
                        Debug.Log("[FirebaseLobby] Poll detected full lobby.");
                        StartMatch();
                        yield break;
                    }
                }
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private void StartMatch()
    {
        if (matchStarted)
            return;

        matchStarted = true;
        joinInProgress = false;

        Debug.Log("[FirebaseLobby] StartMatch CALLED. Code=" + currentLobbyCode + " Platform=" + Application.platform);

        StopLobbyListener();

        if (hostPollRoutine != null)
        {
            StopCoroutine(hostPollRoutine);
            hostPollRoutine = null;
        }

        if (GameManager.I == null)
        {
            Debug.LogError("[FirebaseLobby] GameManager.I is NULL. Cannot connect client.");
            return;
        }

        Debug.Log("[FirebaseLobby] Waiting for server info before connecting...");
        StartCoroutine(WaitForServerInfoThenConnect(currentLobbyCode));
       
    }
  private IEnumerator WaitForServerInfoThenConnect(string code)
{
    Debug.Log("[FirebaseLobby] WaitForServerInfoThenConnect START. Code=" + code);

    if (string.IsNullOrEmpty(code))
    {
        Debug.LogError("[FirebaseLobby] Cannot wait for server info. Lobby code is empty.");
        yield break;
    }

    float timeout = Mathf.Max(60f, serverReadyTimeoutSeconds);
    float timer = 0f;

    while (timer < timeout)
    {
        Debug.Log("[FirebaseLobby] Reading lobby server info... Code=" + code);

        var task = db.Collection("lobbies").Document(code).GetSnapshotAsync();

        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled)
        {
            Debug.LogError("[FirebaseLobby] Failed to read server info: " + task.Exception);
        }
        else
        {
            DocumentSnapshot snapshot = task.Result;

            if (!snapshot.Exists)
            {
                Debug.LogError("[FirebaseLobby] Lobby snapshot does not exist.");
            }
            else
            {
                bool hasServerHost = snapshot.ContainsField("serverHost");
                bool hasServerPort = snapshot.ContainsField("serverPort");
                bool hasServerStatus = snapshot.ContainsField("serverStatus");

                Debug.Log("[FirebaseLobby] Snapshot exists. " +
                          "hasServerHost=" + hasServerHost +
                          " hasServerPort=" + hasServerPort +
                          " hasServerStatus=" + hasServerStatus);

                string serverStatus = hasServerStatus ? snapshot.GetValue<string>("serverStatus") : "";
                string serverHost = hasServerHost ? snapshot.GetValue<string>("serverHost") : "";
                int serverPort = hasServerPort ? snapshot.GetValue<int>("serverPort") : 0;

                Debug.Log("[FirebaseLobby] serverStatus=" + serverStatus +
                          " serverHost=" + serverHost +
                          " serverPort=" + serverPort);

                if (string.Equals(serverStatus, "error", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(serverStatus, "terminated", StringComparison.OrdinalIgnoreCase))
                {
                    string serverError = snapshot.ContainsField("serverError")
                        ? snapshot.GetValue<string>("serverError")
                        : "";
                    Debug.LogError("[FirebaseLobby] Edgegap server unavailable. Status=" +
                                   serverStatus + " Error=" + serverError);
                    ShowError(string.IsNullOrEmpty(serverError)
                        ? "Server failed to start."
                        : "Server failed to start: " + serverError);
                    yield break;
                }

                if (string.Equals(serverStatus, "ready", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(serverHost) && serverPort > 0)
                {
                    Debug.Log("[FirebaseLobby] Server info valid. Connecting to " + serverHost + ":" + serverPort);

                    if (GameManager.I == null)
                    {
                        Debug.LogError("[FirebaseLobby] GameManager.I is NULL.");
                        yield break;
                    }

                    GameManager.I.ConnectClientToServer(serverHost, serverPort);
                    yield break;
                }
            }
        }

        timer += 1f;
        yield return new WaitForSeconds(1f);
    }

    Debug.LogError("[FirebaseLobby] Timed out waiting for Edgegap server info.");
    ShowError("Server failed to start.");
} 

    private IEnumerator RequestEdgegapDeployment(string code)
    {
        if (string.IsNullOrWhiteSpace(createDeploymentUrl))
        {
            Debug.LogError("[FirebaseLobby] Edgegap deployment function URL is empty.");
            ShowError("Server automation is not configured.");
            deploymentRequestRoutine = null;
            yield break;
        }

        Task<string> tokenTask = FirebaseManager.I != null
            ? FirebaseManager.I.GetIdTokenAsync()
            : Task.FromResult("");
        yield return new WaitUntil(() => tokenTask.IsCompleted);

        string idToken = tokenTask.Status == TaskStatus.RanToCompletion
            ? tokenTask.Result
            : "";

        if (string.IsNullOrWhiteSpace(idToken))
        {
            Debug.LogError("[FirebaseLobby] Firebase sign-in returned no ID token.");
            ShowError("Could not authenticate for server creation.");
            deploymentRequestRoutine = null;
            yield break;
        }

        string json = JsonUtility.ToJson(new DeploymentRequest { lobbyCode = code });
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(createDeploymentUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + idToken);
            request.timeout = Mathf.CeilToInt(Mathf.Max(60f, serverReadyTimeoutSeconds));

            Debug.Log("[FirebaseLobby] Requesting automatic Edgegap deployment for lobby " + code);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[FirebaseLobby] Deployment function failed. HTTP=" +
                               request.responseCode + " Error=" + request.error +
                               " Body=" + request.downloadHandler.text);

                // The backend may still have accepted the deployment before the
                // request timed out, so Firestore remains the authoritative state.
                if (request.responseCode > 0 && request.responseCode != 500)
                    ShowError("Could not request a game server.");
            }
            else
            {
                Debug.Log("[FirebaseLobby] Edgegap deployment function completed: " +
                          request.downloadHandler.text);
            }
        }

        deploymentRequestRoutine = null;
    }

    private void StopLobbyListener()
    {
        if (lobbyListener != null)
        {
            lobbyListener.Stop();
            lobbyListener = null;
        }
    }

    private void ShowError(string message)
    {
        if (errorText == null)
            return;

        errorText.text = message;
        errorText.gameObject.SetActive(true);
    }

    private void HideError()
    {
        if (errorText != null)
            errorText.gameObject.SetActive(false);
    }
}
