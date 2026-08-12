using System.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PurrNet;
using Firebase.Firestore;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    public enum GameState
    {
        Boot,
        Menu,
        Connecting,
        Loading,
        Playing,
        GameOver
    }

    public GameState State { get; private set; }

    [Header("Scenes")]
    public string menuSceneName = "Menu";
    public string gameSceneName = "Game";

    public enum MatchResult
    {
        None,
        Win,
        Loss
    }

    public MatchResult lastMatchResult { get; private set; } = MatchResult.None;
    public string lastEndReason { get; private set; } = "";
    public string RematchStatus { get; private set; } = "";
    public event Action<string> RematchStatusChanged;

    [Header("Networking")]
    [SerializeField] private NetworkManager net;

    [Header("Transport")]
    [Tooltip("Optional. If empty, GameManager will auto-find the UDPTransport in the scene.")]
    [SerializeField] private MonoBehaviour udpTransport;

    [Header("End Game")]
    [SerializeField] private float returnToMenuDelay = 0.2f;

    private FirebaseFirestore db;
    private ListenerRegistration matchResultListener;
    private ListenerRegistration rematchListener;
    private string currentLobbyCode = "";
    private string localLobbyRole = "";
    private string lastLobbyCode = "";
    private string lastLobbyRole = "";
    private int rematchRound;
    private bool rematchResetInProgress;
    private bool rematchConnecting;
    private Coroutine rematchDeadlineRoutine;

    private bool hasLoadedGameScene;
    private bool receivedMatchResult;
    private bool isBusy;
    private bool isEndingGame;
    private Coroutine roundStartGateRoutine;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        ResolveNetworkManager();
        ResolveUdpTransport();

        SceneManager.sceneLoaded += OnSceneLoaded;

        isBusy = false;
        isEndingGame = false;
        hasLoadedGameScene = false;
    }

    private void Start()
    {
        SetState(GameState.Boot);

        if (IsServerBuild())
        {
            Debug.Log("[GameManager] Server build detected. Not loading Menu.");
            return;
        }

        if (!IsNetworkRunning())
        {
            if (SceneManager.GetActiveScene().name != menuSceneName)
                SceneManager.LoadScene(menuSceneName);
            else
                SetState(GameState.Menu);
        }
    }

    private void OnDestroy()
    {
        if (I == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopListeningForMatchResult();
            StopListeningForRematch();
        }
    }

    private void ResolveNetworkManager()
    {
        if (net != null)
            return;

        net = NetworkManager.main != null
            ? NetworkManager.main
            : FindObjectOfType<NetworkManager>(true);
    }

    private void ResolveUdpTransport()
    {
        if (udpTransport != null && udpTransport.GetType().Name.Contains("UDPTransport"))
        {
            Debug.Log("[GameManager] UDPTransport already assigned: " + udpTransport.GetType().FullName);
            return;
        }

        MonoBehaviour[] allBehaviours = FindObjectsOfType<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in allBehaviours)
        {
            if (behaviour == null)
                continue;

            string typeName = behaviour.GetType().Name;

            if (typeName.Contains("UDPTransport"))
            {
                udpTransport = behaviour;
                Debug.Log("[GameManager] Auto-found UDPTransport: " + behaviour.GetType().FullName);
                return;
            }
        }

        Debug.LogWarning("[GameManager] UDPTransport not found yet. Will try again before connecting.");
    }

    public void SetLobbyInfo(string lobbyCode, string role)
    {
        StopListeningForMatchResult();

        currentLobbyCode = lobbyCode;
        localLobbyRole = role;
        lastLobbyCode = lobbyCode;
        lastLobbyRole = role;
        receivedMatchResult = false;
        isEndingGame = false;
        hasLoadedGameScene = false;
        lastMatchResult = MatchResult.None;
        lastEndReason = "";
        SetRematchStatus("");

        Debug.Log("[GameManager] Lobby info set. Code=" + currentLobbyCode + " Role=" + localLobbyRole);

        StartListeningForMatchResult();
    }

    private void StartListeningForMatchResult()
    {
        if (string.IsNullOrEmpty(currentLobbyCode))
        {
            Debug.LogWarning("[GameManager] Cannot listen for match result. No lobby code.");
            return;
        }

        if (string.IsNullOrEmpty(localLobbyRole))
        {
            Debug.LogWarning("[GameManager] Cannot listen for match result. No local role.");
            return;
        }

        if (db == null)
            db = FirebaseFirestore.DefaultInstance;

        StopListeningForMatchResult();

        DocumentReference lobbyRef = db.Collection("lobbies").Document(currentLobbyCode);

        matchResultListener = lobbyRef.Listen(snapshot =>
        {
            if (receivedMatchResult)
                return;

            if (!snapshot.Exists)
                return;

            if (!snapshot.ContainsField("matchEnded"))
                return;

            bool matchEnded = snapshot.GetValue<bool>("matchEnded");

            if (!matchEnded)
                return;

            if (!snapshot.ContainsField("loserRole"))
                return;

            string loserRole = snapshot.GetValue<string>("loserRole");

            Debug.Log("[GameManager] Firebase match result received. LoserRole=" + loserRole + " MyRole=" + localLobbyRole);

            receivedMatchResult = true;

            if (loserRole == localLobbyRole)
                EndGameLoss("You were consumed by the black hole.");
            else
                EndGameWin("You are the last player alive.");
        });

        Debug.Log("[GameManager] Listening for Firebase match result.");
    }

    private void StopListeningForMatchResult()
    {
        if (matchResultListener != null)
        {
            matchResultListener.Stop();
            matchResultListener = null;
        }
    }

    public async void ReportLocalPlayerLost(string reason = "You were consumed by the black hole.")
    {
        if (receivedMatchResult)
            return;

        if (string.IsNullOrEmpty(currentLobbyCode) || string.IsNullOrEmpty(localLobbyRole))
        {
            Debug.LogWarning("[GameManager] No lobby info. Ending local loss only.");
            EndGameLoss(reason);
            return;
        }

        receivedMatchResult = true;

        if (db == null)
            db = FirebaseFirestore.DefaultInstance;

        try
        {
            DocumentReference lobbyRef = db.Collection("lobbies").Document(currentLobbyCode);

            await lobbyRef.UpdateAsync(new System.Collections.Generic.Dictionary<string, object>
            {
                { "matchEnded", true },
                { "loserRole", localLobbyRole },
                { "endReason", reason },
                { "endedAt", Timestamp.GetCurrentTimestamp() },
                { "hostRematchReady", false },
                { "joinerRematchReady", false }
            });

            Debug.Log("[GameManager] Reported local player lost. Role=" + localLobbyRole);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[GameManager] Failed to report loss to Firebase: " + e);
        }

        EndGameLoss(reason);
    }

    public void ConnectClientToServer(string address, int port)
    {
        if (string.IsNullOrEmpty(address))
        {
            Debug.LogError("[GameManager] Cannot connect. Address is empty.");
            return;
        }

        if (port <= 0)
        {
            Debug.LogError("[GameManager] Cannot connect. Port is invalid: " + port);
            return;
        }

        bool transportUpdated = SetTransportAddressAndPort(address, port);

        if (!transportUpdated)
        {
            Debug.LogError("[GameManager] Transport was NOT updated. Not connecting.");
            return;
        }

        Debug.Log("[GameManager] Connecting client to " + address + ":" + port);

        ConnectClient();
    }

    private bool SetTransportAddressAndPort(string address, int port)
    {
        ResolveUdpTransport();

        if (udpTransport == null)
        {
            Debug.LogError("[GameManager] UDPTransport reference is missing and could not be found.");
            return false;
        }

        Debug.Log("[GameManager] udpTransport reference type = " + udpTransport.GetType().FullName);

        System.Type transportType = udpTransport.GetType();

        bool addressSet = false;
        bool portSet = false;

        string[] addressNames =
        {
            "address",
            "Address",
            "_address",
            "clientAddress",
            "ClientAddress",
            "_clientAddress",
            "clientHost",
            "ClientHost",
            "_clientHost",
            "host",
            "Host",
            "_host",
            "ip",
            "IP",
            "_ip"
        };

        foreach (string name in addressNames)
        {
            var field = transportType.GetField(
                name,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic
            );

            if (field != null && field.FieldType == typeof(string))
            {
                field.SetValue(udpTransport, address);
                addressSet = true;
                Debug.Log("[GameManager] Set UDPTransport field " + name + " = " + address);
                break;
            }

            var prop = transportType.GetProperty(
                name,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic
            );

            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string))
            {
                prop.SetValue(udpTransport, address);
                addressSet = true;
                Debug.Log("[GameManager] Set UDPTransport property " + name + " = " + address);
                break;
            }
        }

        string[] portNames =
        {
            "serverPort",
            "ServerPort",
            "_serverPort",
            "port",
            "Port",
            "_port",
            "listenPort",
            "ListenPort",
            "_listenPort",
            "bindPort",
            "BindPort",
            "_bindPort",
            "clientPort",
            "ClientPort",
            "_clientPort"
        };

        foreach (string name in portNames)
        {
            var field = transportType.GetField(
                name,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic
            );

            if (field != null)
            {
                if (field.FieldType == typeof(int))
                {
                    field.SetValue(udpTransport, port);
                    portSet = true;
                    Debug.Log("[GameManager] Set UDPTransport field " + name + " = " + port);
                    break;
                }

                if (field.FieldType == typeof(ushort))
                {
                    field.SetValue(udpTransport, (ushort)port);
                    portSet = true;
                    Debug.Log("[GameManager] Set UDPTransport field " + name + " = " + port);
                    break;
                }

                if (field.FieldType == typeof(uint))
                {
                    field.SetValue(udpTransport, (uint)port);
                    portSet = true;
                    Debug.Log("[GameManager] Set UDPTransport field " + name + " = " + port);
                    break;
                }
            }

            var prop = transportType.GetProperty(
                name,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic
            );

            if (prop != null && prop.CanWrite)
            {
                if (prop.PropertyType == typeof(int))
                {
                    prop.SetValue(udpTransport, port);
                    portSet = true;
                    Debug.Log("[GameManager] Set UDPTransport property " + name + " = " + port);
                    break;
                }

                if (prop.PropertyType == typeof(ushort))
                {
                    prop.SetValue(udpTransport, (ushort)port);
                    portSet = true;
                    Debug.Log("[GameManager] Set UDPTransport property " + name + " = " + port);
                    break;
                }

                if (prop.PropertyType == typeof(uint))
                {
                    prop.SetValue(udpTransport, (uint)port);
                    portSet = true;
                    Debug.Log("[GameManager] Set UDPTransport property " + name + " = " + port);
                    break;
                }
            }
        }

        if (!addressSet)
            Debug.LogError("[GameManager] Could not find UDPTransport address field.");

        if (!portSet)
            Debug.LogError("[GameManager] Could not find UDPTransport port field.");

        if (!addressSet || !portSet)
            DumpTransportMembers(udpTransport);

        return addressSet && portSet;
    }

    private void DumpTransportMembers(MonoBehaviour target)
    {
        if (target == null)
            return;

        System.Type type = target.GetType();

        Debug.Log("[GameManager] Dumping UDPTransport fields/properties for: " + type.FullName);

        var fields = type.GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic
        );

        foreach (var field in fields)
            Debug.Log("[GameManager] FIELD: " + field.Name + " TYPE: " + field.FieldType.Name);

        var props = type.GetProperties(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic
        );

        foreach (var prop in props)
            Debug.Log("[GameManager] PROPERTY: " + prop.Name + " TYPE: " + prop.PropertyType.Name + " CAN_WRITE=" + prop.CanWrite);
    }

private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
{
    Debug.Log("[GameManager] sceneLoaded => " + scene.name);

    if (scene.name == gameSceneName)
    {
        Time.timeScale = 0f;
        hasLoadedGameScene = true;
        SetState(GameState.Playing);
        isBusy = false;

        if (roundStartGateRoutine != null)
            StopCoroutine(roundStartGateRoutine);
        roundStartGateRoutine = StartCoroutine(WaitForBothLoadedPlayers());

        // Keep the dedicated server in one stable Game scene between rounds.
        // Rematch clients reconnect through the same path as a code join. A
        // server-side scene reload while empty prevents PurrNet from assigning
        // that already-loaded scene to the later connections.
        return;
    }

    if (scene.name == menuSceneName)
    {
        Time.timeScale = 1f;
        if (IsConnectedToServer())
        {
            Debug.LogWarning("[GameManager] Menu loaded while connected. Ignoring menu state reset.");
            return;
        }

        SetState(GameState.Menu);
        isBusy = false;
        isEndingGame = false;

        ResolveNetworkManager();
        ResolveUdpTransport();
        return;
    }

    if (scene.name == "Boot")
    {
        if (IsConnectedToServer())
        {
            Debug.LogWarning("[GameManager] Boot loaded while connected. Ignoring boot/menu reset.");
            return;
        }

        SetState(GameState.Boot);
        return;
    }
}

private IEnumerator WaitForBothLoadedPlayers()
{
    Debug.Log("[GameManager] Round paused until both player objects are loaded.");

    while (SceneManager.GetActiveScene().name == gameSceneName)
    {
        ResolveNetworkManager();
        int connectedPlayers = net != null ? net.playerCount : 0;
        int loadedPlayers = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;

        if (connectedPlayers >= 2 && loadedPlayers >= 2)
            break;

        yield return new WaitForSecondsRealtime(0.1f);
    }

    yield return new WaitForSecondsRealtime(0.5f);

    if (SceneManager.GetActiveScene().name == gameSceneName)
    {
        Time.timeScale = 1f;
        Debug.Log("[GameManager] Both players loaded. Round started.");
    }

    roundStartGateRoutine = null;
}
private bool IsConnectedToServer()
{
    ResolveNetworkManager();

    if (net == null)
        return false;

    return net.isClient || net.clientState.ToString() == "Connected";
}

    public void ConnectClient()
    {
        if (isBusy)
            return;

        StartCoroutine(ConnectClientRoutine());
    }

    private IEnumerator ConnectClientRoutine()
    {
        isBusy = true;
        SetState(GameState.Connecting);

        ResolveNetworkManager();

        if (net == null)
        {
            Debug.LogError("[GameManager] NetworkManager not found.");
            SetState(GameState.Menu);
            isBusy = false;
            yield break;
        }

        if (net.isClient)
        {
            Debug.Log("[GameManager] Client already running.");
            isBusy = false;
            yield break;
        }

        Debug.Log("[GameManager] Starting client...");
        net.StartClient();

        float t = 0f;
        while (t < 8f)
        {
            if (net.clientState.ToString() == "Connected" || net.isClient)
                break;

            t += 0.25f;
            yield return new WaitForSeconds(0.25f);
        }

        if (!(net.clientState.ToString() == "Connected" || net.isClient))
        {
            Debug.LogError("[GameManager] Failed to connect to server.");
            SetState(GameState.Menu);
            isBusy = false;
            yield break;
        }

       Debug.Log("[GameManager] UDP transport connected. Waiting for PurrNet player registration and Game scene.");
SetState(GameState.Loading);

float sceneWait = 0f;
while (sceneWait < 20f)
{
    string activeScene = SceneManager.GetActiveScene().name;

    if (!net.isClient || net.clientState.ToString() == "Disconnected")
    {
        Debug.LogError("[GameManager] Server connection ended before the Game scene was assigned.");
        SetState(GameState.Menu);
        isBusy = false;
        yield break;
    }

    if (hasLoadedGameScene || activeScene == gameSceneName)
    {
        Debug.Log("[GameManager] Game scene confirmed loaded.");
        hasLoadedGameScene = true;
        SetState(GameState.Playing);
        isBusy = false;
        yield break;
    }

    Debug.Log("[GameManager] Waiting for Game scene. Active scene=" + activeScene +
              " ClientPlayers=" + net.playerCount +
              " ConnectionState=" + net.clientState);

    sceneWait += 0.5f;
    yield return new WaitForSeconds(0.5f);
}

Debug.LogWarning("[GameManager] Connected, but Game scene was not confirmed within timeout. Staying connected.");
isBusy = false;
    }

    public void PlayOffline()
    {
        if (isBusy)
            return;

        SceneManager.LoadScene(gameSceneName);
    }

    public void DisconnectToMenu()
    {
        if (isBusy)
            return;

        StartCoroutine(DisconnectRoutine());
    }

    private IEnumerator DisconnectRoutine()
    {
        isBusy = true;

        if (net)
        {
            try { net.StopClient(); } catch { }
            try { net.StopServer(); } catch { }
        }

        yield return null;

        SceneManager.LoadScene(menuSceneName);
    }

    public void EndGameWin(string reason = "You won!")
    {
        EndGameInternal(MatchResult.Win, reason);
    }

    public void EndGameLoss(string reason = "You lost!")
    {
        EndGameInternal(MatchResult.Loss, reason);
    }

    public void EndGame(string reason = "Game Over", bool wasLoss = true)
    {
        if (wasLoss)
            EndGameLoss(reason);
        else
            EndGameWin(reason);
    }

    private void EndGameInternal(MatchResult result, string reason)
    {
        if (isEndingGame)
            return;

        isEndingGame = true;

        if (result == MatchResult.Win)
            SfxManager.PlayWin();

        if (FirebaseManager.I != null && !string.IsNullOrEmpty(currentLobbyCode))
            FirebaseManager.I.RecordMatchResult(currentLobbyCode, rematchRound);

        lastMatchResult = result;
        lastEndReason = reason;

        SetState(GameState.GameOver);

        Debug.Log("[GameManager] Match ended. Result=" + result + " Reason=" + reason);

        if (!IsServerBuild())
            StartCoroutine(ReturnToMenuAfterDelay());
    }

    private IEnumerator ReturnToMenuAfterDelay()
    {
        yield return new WaitForSeconds(returnToMenuDelay);

        StopListeningForMatchResult();

        if (net != null && net.isClient)
        {
            Debug.Log("[GameManager] Match complete. Disconnecting client before returning to Menu.");
            net.StopClient();

            float disconnectWait = 0f;
            while (net.clientState.ToString() != "Disconnected" && disconnectWait < 5f)
            {
                disconnectWait += Time.unscaledDeltaTime;
                yield return null;
            }

            // Give PurrNet's scene cleanup a chance to unload the networked
            // Game scene and restore its original Boot scene first.
            yield return null;
            yield return null;
        }

        currentLobbyCode = "";
        localLobbyRole = "";
        receivedMatchResult = false;
        hasLoadedGameScene = false;
        isBusy = false;

        SceneManager.LoadScene(menuSceneName);
    }

    public async void RequestPlayAgain()
    {
        if (lastMatchResult == MatchResult.None ||
            string.IsNullOrEmpty(lastLobbyCode) ||
            string.IsNullOrEmpty(lastLobbyRole))
        {
            SetRematchStatus("No finished match is available.");
            return;
        }

        if (rematchConnecting)
            return;

        if (db == null)
            db = FirebaseFirestore.DefaultInstance;

        try
        {
            DocumentReference lobbyRef = db.Collection("lobbies").Document(lastLobbyCode);
            DocumentSnapshot snapshot = await lobbyRef.GetSnapshotAsync();

            if (!snapshot.Exists || !snapshot.ContainsField("matchEnded") ||
                !snapshot.GetValue<bool>("matchEnded"))
            {
                SetRematchStatus("This match is no longer waiting for a rematch.");
                return;
            }

            if (!IsInsideRematchWindow(snapshot))
            {
                SetRematchStatus("The 1 minute rematch window has expired.");
                return;
            }

            rematchRound = snapshot.ContainsField("rematchRound")
                ? snapshot.GetValue<int>("rematchRound")
                : 0;

            DateTime endedAtUtc = snapshot.GetValue<Timestamp>("endedAt").ToDateTime();

            string readyField = lastLobbyRole == "host"
                ? "hostRematchReady"
                : "joinerRematchReady";

            await lobbyRef.UpdateAsync(new Dictionary<string, object>
            {
                { readyField, true }
            });

            SetRematchStatus("Waiting for the other player... (1 minute limit)");
            StartListeningForRematch(lobbyRef);

            if (rematchDeadlineRoutine != null)
                StopCoroutine(rematchDeadlineRoutine);
            rematchDeadlineRoutine = StartCoroutine(ExpireRematchAt(endedAtUtc.AddMinutes(1)));
        }
        catch (Exception e)
        {
            Debug.LogError("[GameManager] Play Again request failed: " + e);
            SetRematchStatus("Could not request a rematch. Try again.");
        }
    }

    private void StartListeningForRematch(DocumentReference lobbyRef)
    {
        StopListeningForRematch();

        rematchListener = lobbyRef.Listen(snapshot =>
        {
            if (!snapshot.Exists || rematchConnecting)
                return;

            bool matchEnded = snapshot.ContainsField("matchEnded") &&
                              snapshot.GetValue<bool>("matchEnded");
            int round = snapshot.ContainsField("rematchRound")
                ? snapshot.GetValue<int>("rematchRound")
                : 0;

            if (!matchEnded && round > rematchRound)
            {
                BeginRematch(snapshot, round);
                return;
            }

            if (!matchEnded)
                return;

            if (!IsInsideRematchWindow(snapshot))
            {
                StopListeningForRematch();
                SetRematchStatus("The 1 minute rematch window has expired.");
                return;
            }

            bool hostReady = snapshot.ContainsField("hostRematchReady") &&
                             snapshot.GetValue<bool>("hostRematchReady");
            bool joinerReady = snapshot.ContainsField("joinerRematchReady") &&
                               snapshot.GetValue<bool>("joinerRematchReady");

            if (hostReady && joinerReady && lastLobbyRole == "host" && !rematchResetInProgress)
                _ = ResetLobbyForRematch(lobbyRef, round);
        });
    }

    private async Task ResetLobbyForRematch(DocumentReference lobbyRef, int round)
    {
        rematchResetInProgress = true;

        try
        {
            await lobbyRef.UpdateAsync(new Dictionary<string, object>
            {
                { "matchEnded", false },
                { "loserRole", "" },
                { "endReason", "" },
                { "hostRematchReady", false },
                { "joinerRematchReady", false },
                { "rematchRound", round + 1 },
                { "status", "full" }
            });
        }
        catch (Exception e)
        {
            rematchResetInProgress = false;
            Debug.LogError("[GameManager] Failed to reset lobby for rematch: " + e);
            SetRematchStatus("Could not start the rematch. Try again.");
        }
    }

    private void BeginRematch(DocumentSnapshot snapshot, int round)
    {
        if (!snapshot.ContainsField("serverHost") || !snapshot.ContainsField("serverPort"))
        {
            SetRematchStatus("The game server is unavailable.");
            return;
        }

        string serverHost = snapshot.GetValue<string>("serverHost");
        int serverPort = snapshot.GetValue<int>("serverPort");

        if (string.IsNullOrEmpty(serverHost) || serverPort <= 0)
        {
            SetRematchStatus("The game server is unavailable.");
            return;
        }

        rematchConnecting = true;
        rematchRound = round;
        StopListeningForRematch();
        if (rematchDeadlineRoutine != null)
        {
            StopCoroutine(rematchDeadlineRoutine);
            rematchDeadlineRoutine = null;
        }
        SetRematchStatus("Both players are ready. Starting rematch...");
        StartCoroutine(ConnectRematchAfterServerReset(serverHost, serverPort));
    }

    private IEnumerator ConnectRematchAfterServerReset(string serverHost, int serverPort)
    {
        // The dedicated server reloads the arena after both round-one clients
        // disconnect. Give that networked scene load time to finish before the
        // rematch clients enter it.
        yield return new WaitForSecondsRealtime(1.25f);

        string lobbyCode = lastLobbyCode;
        string lobbyRole = lastLobbyRole;
        ClearLastResult();
        SetLobbyInfo(lobbyCode, lobbyRole);
        ConnectClientToServer(serverHost, serverPort);
    }

    private bool IsInsideRematchWindow(DocumentSnapshot snapshot)
    {
        if (!snapshot.ContainsField("endedAt"))
            return false;

        DateTime endedAtUtc = snapshot.GetValue<Timestamp>("endedAt").ToDateTime();
        return DateTime.UtcNow <= endedAtUtc.AddMinutes(1);
    }

    private void StopListeningForRematch()
    {
        if (rematchListener == null)
            return;

        rematchListener.Stop();
        rematchListener = null;
    }

    private IEnumerator ExpireRematchAt(DateTime deadlineUtc)
    {
        while (DateTime.UtcNow < deadlineUtc && !rematchConnecting)
            yield return new WaitForSecondsRealtime(0.25f);

        rematchDeadlineRoutine = null;

        if (rematchConnecting)
            yield break;

        StopListeningForRematch();
        SetRematchStatus("The 1 minute rematch window has expired.");
    }

    private void SetRematchStatus(string message)
    {
        RematchStatus = message;
        RematchStatusChanged?.Invoke(message);
        if (!string.IsNullOrEmpty(message))
            Debug.Log("[GameManager] Rematch: " + message);
    }

    public void ClearLastResult()
    {
        lastMatchResult = MatchResult.None;
        lastEndReason = "";
        isEndingGame = false;
        receivedMatchResult = false;
        rematchResetInProgress = false;
        rematchConnecting = false;
    }

    private bool IsNetworkRunning()
    {
        ResolveNetworkManager();

        if (net == null)
            return false;

        bool notDisconnected = net.clientState.ToString() != "Disconnected";
        return net.isClient || net.isServer || notDisconnected;
    }

    private void SetState(GameState newState)
    {
        State = newState;
        Debug.Log("[GameManager] State => " + newState);
    }

    private static bool IsServerBuild()
    {
        return Application.isBatchMode ||
               SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
    }
}
