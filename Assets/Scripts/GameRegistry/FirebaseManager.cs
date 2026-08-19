using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine.Networking;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager I { get; private set; }

    public FirebaseFirestore Db { get; private set; }
    public FirebaseAuth Auth { get; private set; }
    public FirebaseUser CurrentUser => Auth != null ? Auth.CurrentUser : null;
    public bool IsReady => CurrentUser != null && Db != null;

    private Task<FirebaseUser> signInTask;
    private const string RecordResultUrl =
        "https://us-central1-entropy-7c113.cloudfunctions.net/recordMatchResult";
    private const string VerifyPurchaseUrl =
        "https://us-central1-entropy-7c113.cloudfunctions.net/verifyAppleSkinPurchase";
    private const string DeleteAccountUrl =
        "https://us-central1-entropy-7c113.cloudfunctions.net/deletePlayerAccount";
    private const string SelectedSkinPreferenceKey = "entropy.selectedSkin";
    private const string PendingSkinSyncPreferenceKey = "entropy.selectedSkin.pendingSync";

    public string SelectedSkin { get; private set; } = "beard";
    public bool OwnsSunDucker { get; private set; }
    public bool OwnsTurtle { get; private set; }
    public event Action SkinDataChanged;
#if UNITY_EDITOR
    private bool editorSunDuckerPreview;
    private bool editorTurtlePreview;
    private bool editorSkinSelectionActive;
#endif
    private bool hasPendingSkinSync;

    [Serializable]
    private class MatchResultRequest
    {
        public string lobbyCode;
        public int round;
    }

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        // The last successfully chosen skin is available immediately when the
        // game scene opens, even while Firebase is still loading.
        SelectedSkin = NormalizeSkinId(PlayerPrefs.GetString(
            SelectedSkinPreferenceKey, "beard"));
        hasPendingSkinSync = PlayerPrefs.GetInt(PendingSkinSyncPreferenceKey, 0) == 1;
    }

    private async void Start()
    {
        if (IsServerBuild())
        {
            Debug.Log("[FirebaseManager] Server build detected. Skipping Firebase initialization.");
            return;
        }

        DependencyStatus status = await FirebaseApp.CheckAndFixDependenciesAsync();

        if (status != DependencyStatus.Available)
        {
            Debug.LogError("[FirebaseManager] Firebase dependencies not available: " + status);
            return;
        }

        Db = FirebaseFirestore.DefaultInstance;
        Auth = FirebaseAuth.DefaultInstance;

        try
        {
            Db.Settings.PersistenceEnabled = false;
            Debug.Log("[FirebaseManager] Firebase ready. Firestore persistence disabled.");
        }
        catch (System.InvalidOperationException)
        {
            Debug.LogWarning("[FirebaseManager] Firestore settings were already locked. Continuing.");
        }

        try
        {
            FirebaseUser user = await EnsureSignedInAsync();
            await EnsurePlayerProfileAsync(user);
            await RefreshSkinDataAsync();
            Debug.Log("[FirebaseManager] Player account ready. Guest=" + user.IsAnonymous);
        }
        catch (Exception e)
        {
            Debug.LogError("[FirebaseManager] Could not initialize player account: " + e);
        }
    }

    public Task<FirebaseUser> EnsureSignedInAsync()
    {
        if (IsServerBuild())
            return Task.FromResult<FirebaseUser>(null);

        if (Auth == null)
            Auth = FirebaseAuth.DefaultInstance;

        if (Auth.CurrentUser != null)
            return Task.FromResult(Auth.CurrentUser);

        if (signInTask == null || signInTask.IsFaulted || signInTask.IsCanceled)
            signInTask = SignInGuestAsync();

        return signInTask;
    }

    private async Task<FirebaseUser> SignInGuestAsync()
    {
        AuthResult result = await Auth.SignInAnonymouslyAsync();
        if (result == null || result.User == null)
            throw new InvalidOperationException("Firebase anonymous sign-in returned no user.");
        return result.User;
    }

    public async Task<string> GetIdTokenAsync()
    {
        FirebaseUser user = await EnsureSignedInAsync();
        return user == null ? "" : await user.TokenAsync(false);
    }

    private async Task EnsurePlayerProfileAsync(FirebaseUser user)
    {
        if (user == null || Db == null)
            return;

        DocumentReference profile = Db.Collection("players").Document(user.UserId);
        DocumentSnapshot snapshot = await profile.GetSnapshotAsync();

        if (!snapshot.Exists)
        {
            await profile.SetAsync(new Dictionary<string, object>
            {
                { "uid", user.UserId },
                { "accountType", user.IsAnonymous ? "guest" : "apple" },
                { "wins", 0 },
                { "losses", 0 },
                { "matchesPlayed", 0 },
                { "selectedSkin", "beard" },
                { "createdAt", FieldValue.ServerTimestamp },
                { "lastSeenAt", FieldValue.ServerTimestamp }
            });

            await profile.Collection("skins").Document("beard").SetAsync(
                new Dictionary<string, object>
                {
                    { "owned", true },
                    { "acquisitionType", "default" },
                    { "acquiredAt", FieldValue.ServerTimestamp }
                });
        }
        else
        {
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                { "lastSeenAt", FieldValue.ServerTimestamp },
                { "accountType", user.IsAnonymous ? "guest" : "apple" }
            };

            if (!snapshot.ContainsField("uid"))
                updates["uid"] = user.UserId;
            if (!snapshot.ContainsField("selectedSkin"))
                updates["selectedSkin"] = "beard";
            if (!snapshot.ContainsField("createdAt"))
                updates["createdAt"] = FieldValue.ServerTimestamp;

            await profile.UpdateAsync(updates);

            DocumentReference defaultSkin = profile.Collection("skins").Document("beard");
            DocumentSnapshot skinSnapshot = await defaultSkin.GetSnapshotAsync();
            if (!skinSnapshot.Exists)
            {
                await defaultSkin.SetAsync(new Dictionary<string, object>
                {
                    { "owned", true },
                    { "acquisitionType", "default" },
                    { "acquiredAt", FieldValue.ServerTimestamp }
                });
            }
        }
    }

    public async Task RefreshSkinDataAsync()
    {
        FirebaseUser user = await EnsureSignedInAsync();
        if (user == null || Db == null) return;

        DocumentReference profile = Db.Collection("players").Document(user.UserId);
        DocumentSnapshot profileSnapshot = await profile.GetSnapshotAsync();
        string remoteSelected = profileSnapshot.Exists && profileSnapshot.ContainsField("selectedSkin")
            ? profileSnapshot.GetValue<string>("selectedSkin") : "beard";
        remoteSelected = NormalizeSkinId(remoteSelected);

        DocumentSnapshot paid = await profile.Collection("skins").Document("sun_ducker")
            .GetSnapshotAsync();
        OwnsSunDucker = paid.Exists && paid.ContainsField("owned") &&
                         paid.GetValue<bool>("owned");
        DocumentSnapshot turtle = await profile.Collection("skins").Document("turtle")
            .GetSnapshotAsync();
        OwnsTurtle = turtle.Exists && turtle.ContainsField("owned") &&
                     turtle.GetValue<bool>("owned");
#if UNITY_EDITOR
        if (editorSunDuckerPreview)
        {
            OwnsSunDucker = true;
        }
        if (editorTurtlePreview)
        {
            OwnsTurtle = true;
        }
#endif
        string selected = hasPendingSkinSync ? SelectedSkin : remoteSelected;
#if UNITY_EDITOR
        if (editorSkinSelectionActive)
            selected = SelectedSkin;
#endif
        if (!IsOwnedSkin(selected))
        {
            selected = "beard";
            hasPendingSkinSync = false;
        }

        // If a previous equip worked locally while the network save failed,
        // retry it before accepting an older remote selection.
        if (hasPendingSkinSync)
        {
            try
            {
                await profile.UpdateAsync(new Dictionary<string, object>
                    { { "selectedSkin", selected } });
                hasPendingSkinSync = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FirebaseManager] Equipped skin is active locally but still " +
                                 "waiting to sync: " + e.Message);
            }
        }

        SaveSelectedSkinLocally(selected, hasPendingSkinSync, true);
    }

#if UNITY_EDITOR
    public void PreviewSunDuckerInEditor()
    {
        editorSunDuckerPreview = true;
        editorSkinSelectionActive = true;
        OwnsSunDucker = true;
        SelectedSkin = "sun_ducker";
        Debug.Log("[SkinShop] Sun Ducker preview enabled for this Editor run. " +
                  "No purchase or Firestore entitlement was created.");
        SkinDataChanged?.Invoke();
    }

    public void PreviewTurtleInEditor()
    {
        editorTurtlePreview = true;
        editorSkinSelectionActive = true;
        OwnsTurtle = true;
        SelectedSkin = "turtle";
        Debug.Log("[SkinShop] Turtle preview enabled for this Editor run. " +
                  "No purchase or Firestore entitlement was created.");
        SkinDataChanged?.Invoke();
    }
#endif

    public async Task<bool> EquipSkinAsync(string skinId)
    {
        skinId = NormalizeSkinId(skinId);
#if UNITY_EDITOR
        if (skinId == "sun_ducker" && editorSunDuckerPreview)
        {
            editorSkinSelectionActive = true;
            SelectedSkin = "sun_ducker";
            SkinDataChanged?.Invoke();
            return true;
        }
        if (skinId == "turtle" && editorTurtlePreview)
        {
            editorSkinSelectionActive = true;
            SelectedSkin = "turtle";
            SkinDataChanged?.Invoke();
            return true;
        }
#endif
        if (!IsOwnedSkin(skinId)) return false;

        // Equip immediately. A temporary Firestore problem must not silently
        // cancel the player's choice and put the beard skin back on.
        SaveSelectedSkinLocally(skinId, true, true);

        try
        {
            FirebaseUser user = await EnsureSignedInAsync();
            if (user == null || Db == null)
            {
                Debug.LogWarning("[FirebaseManager] Skin equipped locally; cloud sync will retry later.");
                return true;
            }

            await Db.Collection("players").Document(user.UserId).UpdateAsync(
                new Dictionary<string, object> { { "selectedSkin", skinId } });
            SaveSelectedSkinLocally(skinId, false, false);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FirebaseManager] Skin equipped locally; cloud sync will retry later: " +
                             e.Message);
        }

        return true;
    }

    public async Task<bool> VerifyAppleSkinPurchaseAsync(string receipt, string productId, string appleJws = null)
    {
        if ((string.IsNullOrWhiteSpace(receipt) && string.IsNullOrWhiteSpace(appleJws)) || string.IsNullOrWhiteSpace(productId)) return false;
        string token = await GetIdTokenAsync();
        string escaped = (receipt ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\r", "\\r");
        string escapedJws = (appleJws ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\r", "\\r");
        string escapedProductId = productId.Replace("\\", "\\\\").Replace("\"", "\\\"");
        using (UnityWebRequest request = new UnityWebRequest(
                   VerifyPurchaseUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                "{\"receipt\":\"" + escaped + "\",\"jws\":\"" + escapedJws +
                "\",\"productId\":\"" + escapedProductId + "\"}"));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 45;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[FirebaseManager] Purchase verification failed: " +
                               request.responseCode + " " + request.downloadHandler.text);
                return false;
            }
        }
        await RefreshSkinDataAsync();
        return productId == SkinPurchaseManager.TurtleProductId ? OwnsTurtle : OwnsSunDucker;
    }

    public async Task DeletePlayerAccountAsync()
    {
        string token = await GetIdTokenAsync();
        using (UnityWebRequest request = new UnityWebRequest(
                   DeleteAccountUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 60;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException("Account deletion failed: " +
                    request.responseCode + " " + request.downloadHandler.text);
        }

        OwnsSunDucker = false;
        OwnsTurtle = false;
        SaveSelectedSkinLocally("beard", false, true);
        Auth?.SignOut();
        signInTask = null;
        FirebaseUser replacement = await EnsureSignedInAsync();
        await EnsurePlayerProfileAsync(replacement);
        await RefreshSkinDataAsync();
    }

    private static string NormalizeSkinId(string skinId)
    {
        return skinId == "sun_ducker" || skinId == "turtle" ? skinId : "beard";
    }

    private bool IsOwnedSkin(string skinId)
    {
        return skinId == "beard" ||
               (skinId == "sun_ducker" && OwnsSunDucker) ||
               (skinId == "turtle" && OwnsTurtle);
    }

    private void SaveSelectedSkinLocally(string skinId, bool pendingSync, bool notify)
    {
        SelectedSkin = NormalizeSkinId(skinId);
        hasPendingSkinSync = pendingSync;
        PlayerPrefs.SetString(SelectedSkinPreferenceKey, SelectedSkin);
        PlayerPrefs.SetInt(PendingSkinSyncPreferenceKey, pendingSync ? 1 : 0);
        PlayerPrefs.Save();
        if (notify)
            SkinDataChanged?.Invoke();
    }

    public async void RecordMatchResult(string lobbyCode, int round)
    {
        if (string.IsNullOrWhiteSpace(lobbyCode) || IsServerBuild())
            return;

        try
        {
            string idToken = await GetIdTokenAsync();
            string json = JsonUtility.ToJson(new MatchResultRequest
            {
                lobbyCode = lobbyCode,
                round = Mathf.Max(0, round)
            });

            using (UnityWebRequest request = new UnityWebRequest(
                       RecordResultUrl, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + idToken);
                request.timeout = 30;

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                    Debug.LogError("[FirebaseManager] Result recording failed: " +
                                   request.responseCode + " " + request.downloadHandler.text);
                else
                    Debug.Log("[FirebaseManager] Match result recorded for account.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[FirebaseManager] Could not record match result: " + e);
        }
    }

    private static bool IsServerBuild()
    {
        return Application.isBatchMode ||
               SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
    }
}
