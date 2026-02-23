using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public class HomeUIManager : MonoBehaviour
{
    private static bool didBackendBootstrapThisSession = false;
    private static bool didLeaderboardBootstrapThisSession = false;
    private const string BlockedKey = "AccountBlocked";
    private const int OfflineLevelLimit = 10;
    private const string LifeKey = "Life";
    private const string TrophiesKey = "Trophies";
    private const string LifeNextReadyKey = "LifeNextReadyUtc";
    private const int MaxLife = 5;
    private const int LifeRegenSeconds = 15 * 60;

    public Text LevelText, LevelText2;
    public Text CoinsText;
    [FormerlySerializedAs("LivesText")]
    public Text LifeText;
    public Text TrophiesText;
    public Text LifeTimerText;
    public string ApiBaseUrl = "https://apigame.blazemobilestudio.com/api";
    public Text OvenCountText;
    public Text PanCountText;
    public Text KnifeCountText;
    public Button OvenSelectButton;
    public Button OvenSelectedButton;
    public Button PanSelectButton;
    public Button PanSelectedButton;
    public Button KnifeSelectButton;
    public Button KnifeSelectedButton;
    public bool LogContinueWithDeviceResponse = true;
    public bool RedactTokensInLogs = true;
    public bool DebugLeaderboardHighlight = false;
    public GameObject BlockedPanel;
    public GameObject InternetConnectivityPanel;
    public GameObject LeaderboardPanel;
    public Transform LeaderboardContent;
    public LeaderboardRowUI LeaderboardRowPrefab;
    public Text LeaderboardTierText;
    public Text LeaderboardUserRankText;
    public Text LeaderboardUserNameText;
    public Text LeaderboardUserTrophiesText;
    public Image LeaderboardUserAvatarImage;

    // Track selected boosters
    private System.Collections.Generic.List<string> selectedBoosters = new System.Collections.Generic.List<string>();
    private bool isBlocked;
    private Coroutine leaderboardRoutine;
    private LeaderboardTierResponse cachedLeaderboard;

    private void Start()
    {
        isBlocked = PlayerPrefs.GetInt(BlockedKey, 0) == 1;
        SetBlockedPanelVisible(isBlocked);
        SetInternetConnectivityPanelVisible(false);

        UpdateLevelUI();
        UpdateBoosterCountUI();
        // Reset booster selection on start
        selectedBoosters.Clear();
        PlayerPrefs.DeleteKey("SelectedBoosters");

        if (!didBackendBootstrapThisSession && !string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            StartCoroutine(StartupSyncRoutine());
            didBackendBootstrapThisSession = true;
        }

        SetLeaderboardVisible(false);
        BootstrapLeaderboardOnce();
    }

    private void Update()
    {
        if (InternetConnectivityPanel != null && InternetConnectivityPanel.activeSelf)
        {
            if (Application.internetReachability != NetworkReachability.NotReachable)
            {
                SetInternetConnectivityPanelVisible(false);
            }
        }

        UpdateLifeTimerUI();
    }

    public void OnLeaderboardButton()
    {
        if (isBlocked) return;
        SetLeaderboardVisible(true);
        RefreshLeaderboardUIFromLocalFallback();
        if (cachedLeaderboard != null) ApplyLeaderboardResponse(cachedLeaderboard);
    }

    public void OnLeaderboardCloseButton()
    {
        SetLeaderboardVisible(false);
    }

    private void SetLeaderboardVisible(bool visible)
    {
        if (LeaderboardPanel != null) LeaderboardPanel.SetActive(visible);
    }

    private void TryFetchLeaderboard()
    {
        if (leaderboardRoutine != null) StopCoroutine(leaderboardRoutine);
        leaderboardRoutine = StartCoroutine(FetchLeaderboardRoutine());
    }

    private IEnumerator FetchLeaderboardRoutine()
    {
        if (string.IsNullOrWhiteSpace(ApiBaseUrl)) yield break;
        if (Application.internetReachability == NetworkReachability.NotReachable) yield break;

        string accessToken = PlayerPrefs.GetString("AccessToken", "");
        if (string.IsNullOrEmpty(accessToken)) yield break;

        string url = CombineUrl(ApiBaseUrl, "player/leaderboard/tier");
        if (DebugLeaderboardHighlight) Debug.Log($"Leaderboard fetch start. Url={url}");
        using (var req = UnityWebRequest.Get(url))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + accessToken);

            yield return req.SendWebRequest();

            string body = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (req.result != UnityWebRequest.Result.Success)
            {
                if (LogContinueWithDeviceResponse)
                {
                    Debug.LogWarning($"Leaderboard fetch failed. Url={url} Code={(int)req.responseCode} Error={req.error} Body={PrepareApiLogBody(body)}");
                }
                yield break;
            }

            LeaderboardTierResponse resp;
            try
            {
                resp = JsonUtility.FromJson<LeaderboardTierResponse>(body);
            }
            catch
            {
                Debug.LogWarning($"Leaderboard invalid JSON. Url={url} Body={PrepareApiLogBody(body)}");
                yield break;
            }

            if (resp == null || !resp.success) yield break;
            cachedLeaderboard = resp;
            if (DebugLeaderboardHighlight) Debug.Log($"Leaderboard fetch success. Tier='{resp.tier}' userRank={(resp.userRank != null ? resp.userRank.rank.ToString() : "null")} listCount={(resp.leaderboard != null ? resp.leaderboard.Length : 0)}");
            if (LeaderboardPanel != null && LeaderboardPanel.activeInHierarchy) ApplyLeaderboardResponse(resp);
        }
    }

    private void ApplyLeaderboardResponse(LeaderboardTierResponse resp)
    {
        if (resp == null) return;

        if (LeaderboardTierText != null) LeaderboardTierText.text = string.IsNullOrEmpty(resp.tier) ? "" : resp.tier;

        string currentUserId = PlayerPrefs.GetString("UserId", "");
        string currentFullName = PlayerPrefs.GetString("FullName", "");
        string highlightId = (resp.userRank != null && !string.IsNullOrEmpty(resp.userRank._id)) ? resp.userRank._id : currentUserId;
        int highlightRank = resp.userRank != null ? resp.userRank.rank : -1;
        string highlightFullName = (resp.userRank != null && !string.IsNullOrEmpty(resp.userRank.fullName)) ? resp.userRank.fullName : currentFullName;

        if (DebugLeaderboardHighlight)
        {
            Debug.Log($"Leaderboard highlight base: userId='{currentUserId}' fullName='{currentFullName}' highlightId='{highlightId}' highlightFullName='{highlightFullName}' highlightRank={highlightRank} tier='{resp.tier}' listCount={(resp.leaderboard != null ? resp.leaderboard.Length : 0)}");
        }

        if (resp.userRank != null)
        {
            if (LeaderboardUserRankText != null) LeaderboardUserRankText.text = resp.userRank.rank > 0 ? resp.userRank.rank.ToString() : "";
            if (LeaderboardUserNameText != null) LeaderboardUserNameText.text = string.IsNullOrEmpty(resp.userRank.fullName) ? "-" : resp.userRank.fullName;
            if (LeaderboardUserTrophiesText != null) LeaderboardUserTrophiesText.text = resp.userRank.trophies >= 0 ? resp.userRank.trophies.ToString() : "0";
        }

        if (LeaderboardContent != null)
        {
            for (int i = LeaderboardContent.childCount - 1; i >= 0; i--)
            {
                Destroy(LeaderboardContent.GetChild(i).gameObject);
            }
        }

        if (LeaderboardRowPrefab == null || LeaderboardContent == null || resp.leaderboard == null) return;

        bool anyHighlighted = false;
        for (int i = 0; i < resp.leaderboard.Length; i++)
        {
            var e = resp.leaderboard[i];
            if (e == null) continue;

            var row = Instantiate(LeaderboardRowPrefab, LeaderboardContent);
            row.gameObject.SetActive(true);
            row.SetData(e.rank, e.fullName, e.trophies);
            bool isHighlighted =
                (!string.IsNullOrEmpty(highlightId) && !string.IsNullOrEmpty(e._id) && string.Equals(e._id, highlightId, StringComparison.OrdinalIgnoreCase)) ||
                (highlightRank > 0 && e.rank == highlightRank) ||
                (!string.IsNullOrEmpty(highlightFullName) && !string.IsNullOrEmpty(e.fullName) && string.Equals(e.fullName, highlightFullName, StringComparison.OrdinalIgnoreCase));
            row.SetHighlighted(isHighlighted);
            if (isHighlighted) anyHighlighted = true;

            if (DebugLeaderboardHighlight && (isHighlighted || i < 3))
            {
                Debug.Log($"Leaderboard row[{i}]: rank={e.rank} id='{e._id}' name='{e.fullName}' trophies={e.trophies} highlighted={isHighlighted}");
            }

            if (row.AvatarImage != null && !string.IsNullOrEmpty(e.profileImageUrl))
            {
                StartCoroutine(LoadAvatarSpriteRoutine(e.profileImageUrl, row.AvatarImage));
            }
        }

        if (DebugLeaderboardHighlight && !anyHighlighted)
        {
            Debug.LogWarning("Leaderboard: no highlighted row found. Check userRank and row data ids/names/ranks, and ensure HighlightBackgroundSprite is assigned on the row prefab.");
        }
    }

    private IEnumerator LoadAvatarSpriteRoutine(string url, Image target)
    {
        if (string.IsNullOrEmpty(url) || target == null) yield break;

        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;

            var tex = DownloadHandlerTexture.GetContent(req);
            if (tex == null) yield break;

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            target.sprite = sprite;
            target.enabled = true;
        }
    }

    private void RefreshLeaderboardUIFromLocalFallback()
    {
        if (LeaderboardTierText != null && string.IsNullOrEmpty(LeaderboardTierText.text)) LeaderboardTierText.text = "";
    }

    private void UpdateLevelUI()
    {
        int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        if (LevelText != null)
        {
            LevelText.text = "Level " + currentLevel;
        }
        if (LevelText2 != null)
        {
            LevelText2.text = "Level " + currentLevel;
        }

        int totalCoins = PlayerPrefs.GetInt("TotalCoins", 0);
        if (CoinsText != null)
        {
            CoinsText.text = totalCoins.ToString();
        }

        if (LifeText != null)
        {
            int lives = GetCurrentLife();
            LifeText.text = lives.ToString();
        }

        if (TrophiesText != null)
        {
            int trophies = PlayerPrefs.GetInt(TrophiesKey, 0);
            TrophiesText.text = trophies.ToString();
        }
    }

    private void UpdateBoosterCountUI()
    {
        if (OvenCountText != null) OvenCountText.text = GetPowerupCountForBoosterName("Oven").ToString();
        if (PanCountText != null) PanCountText.text = GetPowerupCountForBoosterName("Pan").ToString();
        if (KnifeCountText != null) KnifeCountText.text = GetPowerupCountForBoosterName("Knife").ToString();

        UpdateBoosterButtonState("Oven", OvenSelectButton, OvenSelectedButton);
        UpdateBoosterButtonState("Pan", PanSelectButton, PanSelectedButton);
        UpdateBoosterButtonState("Knife", KnifeSelectButton, KnifeSelectedButton);
    }

    private void UpdateBoosterButtonState(string boosterName, Button selectButton, Button selectedButton)
    {
        int count = GetPowerupCountForBoosterName(boosterName);
        bool hasAny = count > 0;
        bool canInteract = hasAny && !isBlocked;

        if (selectButton != null) selectButton.interactable = canInteract;
        if (selectedButton != null) selectedButton.interactable = canInteract;

        if (!hasAny)
        {
            selectedBoosters.Remove(boosterName);
            if (selectedButton != null) selectedButton.gameObject.SetActive(false);
            if (selectButton != null) selectButton.gameObject.SetActive(true);
        }
    }

    private IEnumerator ContinueWithDeviceRoutine()
    {
        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            UpdateLevelUI();
            yield break;
        }

        string deviceId = GetOrCreateDeviceId();
        string os = GetOsString();

        var payload = new ContinueWithDeviceRequest
        {
            deviceId = deviceId,
            os = os
        };

        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        string url = CombineUrl(ApiBaseUrl, "auth/continue/device");

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            UnityWebRequestAsyncOperation op;
            try
            {
                op = req.SendWebRequest();
            }
            catch (InvalidOperationException)
            {
                Debug.LogError("Insecure connection not allowed. For HTTP backend URLs, enable: Project Settings > Player > Other Settings > Allow downloads over HTTP (Always allowed / Only in Development Builds), or switch backend to HTTPS.");
                UpdateLevelUI();
                yield break;
            }

            yield return op;

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"ContinueWithDevice failed. Url={url} Code={(int)req.responseCode} Error={req.error} Body={PrepareApiLogBody(req.downloadHandler?.text)}");
                if (req.responseCode == 403)
                {
                    SetBlockedState(true);
                    ClearAuthTokens();
                }
                UpdateLevelUI();
                yield break;
            }

            ContinueWithDeviceResponse resp;
            string rawBody = req.downloadHandler.text;
            if (LogContinueWithDeviceResponse)
            {
                Debug.Log($"ContinueWithDevice response: {PrepareApiLogBody(rawBody)}");
            }
            try
            {
                resp = JsonUtility.FromJson<ContinueWithDeviceResponse>(rawBody);
            }
            catch
            {
                Debug.LogError($"ContinueWithDevice invalid JSON. Url={url} Body={PrepareApiLogBody(req.downloadHandler?.text)}");
                UpdateLevelUI();
                yield break;
            }

            if (resp == null || !resp.success)
            {
                string msg = resp != null ? resp.message : "null response";
                Debug.LogError($"ContinueWithDevice unsuccessful. Url={url} Message={msg}");
                if (req.responseCode == 403 || IsBlockedMessage(msg))
                {
                    SetBlockedState(true);
                    ClearAuthTokens();
                }
                UpdateLevelUI();
                yield break;
            }

            if (!string.IsNullOrEmpty(resp.accessToken)) PlayerPrefs.SetString("AccessToken", resp.accessToken);
            if (!string.IsNullOrEmpty(resp.refreshToken)) PlayerPrefs.SetString("RefreshToken", resp.refreshToken);
            SetBlockedState(false);

            if (resp.user != null)
            {
                if (!string.IsNullOrEmpty(resp.user.id)) PlayerPrefs.SetString("UserId", resp.user.id);
                if (!string.IsNullOrEmpty(resp.user.fullName)) PlayerPrefs.SetString("FullName", resp.user.fullName);

                int serverLevel = resp.user.level;
                int coins = GetCoinsFromUser(resp.user);
                PowerupCounts powerups = GetPowerups(resp.user, rawBody);

                if (!PlayerPrefs.HasKey(LifeKey))
                {
                    if (resp.user.life >= 0) PlayerPrefs.SetInt(LifeKey, resp.user.life);
                }

                if (!PlayerPrefs.HasKey(TrophiesKey))
                {
                    if (resp.user.trophies >= 0) PlayerPrefs.SetInt(TrophiesKey, resp.user.trophies);
                }
                ProgressDataManager.EnsureInstance().OverwriteFromServer(
                    serverLevel,
                    coins,
                    powerups.oven,
                    powerups.pan,
                    powerups.blender,
                    powerups.horizontalKnife,
                    powerups.verticalKnife,
                    powerups.flies
                );
            }

            PlayerPrefs.Save();
            UpdateLevelUI();
            UpdateBoosterCountUI();
            Debug.Log($"ContinueWithDevice success. isNewUser={resp.isNewUser} deviceId={deviceId} userId={PlayerPrefs.GetString("UserId","")} level={PlayerPrefs.GetInt("CurrentLevel", 1)} coins={PlayerPrefs.GetInt("TotalCoins", 0)} powerups(oven={PlayerPrefs.GetInt("Powerup_Oven", 0)} pan={PlayerPrefs.GetInt("Powerup_Pan", 0)} knifeV={PlayerPrefs.GetInt("Powerup_VerticalKnife", 0)} blender={PlayerPrefs.GetInt("Powerup_Blender", 0)})");
        }
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        if (string.IsNullOrEmpty(baseUrl)) return path ?? "";
        baseUrl = baseUrl.Trim();
        path = (path ?? "").Trim();
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        if (path.StartsWith("/")) path = path.Substring(1);
        return baseUrl + path;
    }

    private static string GetOrCreateDeviceId()
    {
        const string key = "DeviceId";
        string existing = PlayerPrefs.GetString(key, "");
        if (!string.IsNullOrEmpty(existing)) return existing;

        string created = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(key, created);
        PlayerPrefs.Save();
        return created;
    }

    private static string GetOsString()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.Android:
                return "android";
            case RuntimePlatform.IPhonePlayer:
                return "ios";
            default:
                return "android";
        }
    }

    private static int GetCoinsFromUser(UserDto user)
    {
        if (user == null) return -1;
        if (user.coins >= 0) return user.coins;
        if (user.coinBalance >= 0) return user.coinBalance;
        if (user.wallet >= 0) return user.wallet;
        return -1;
    }

    private struct PowerupCounts
    {
        public int oven;
        public int pan;
        public int blender;
        public int horizontalKnife;
        public int verticalKnife;
        public int flies;
    }

    private static PowerupCounts GetPowerups(UserDto user, string rawJson)
    {
        var r = new PowerupCounts
        {
            oven = -1,
            pan = -1,
            blender = -1,
            horizontalKnife = -1,
            verticalKnife = -1,
            flies = -1
        };

        if (user != null)
        {
            var p = user.powerups != null ? user.powerups : user.powerUps;
            if (p != null)
            {
                r.horizontalKnife = p.HorizontalKnife;
                r.verticalKnife = p.VerticalKnife;
                r.pan = p.Pan;
                r.oven = p.Oven;
                r.flies = p.Flies;
                r.blender = p.Blender;
            }
            else
            {
                r.pan = user.pan;
                r.oven = user.oven;
                r.blender = user.blender;
                r.verticalKnife = user.verticalKnife;
            }
        }

        if (!string.IsNullOrEmpty(rawJson))
        {
            int pan = TryGetJsonInt(rawJson, "pan");
            int oven = TryGetJsonInt(rawJson, "oven");
            int blender = TryGetJsonInt(rawJson, "blender");
            int verticalKnife = TryGetJsonInt(rawJson, "verticalKnife");

            if (pan >= 0) r.pan = pan;
            if (blender >= 0) r.blender = blender;
            if (verticalKnife >= 0) r.verticalKnife = verticalKnife;
            if (oven >= 0) r.oven = oven;
        }

        return r;
    }

    private static int TryGetJsonInt(string json, string fieldName)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName)) return -1;

        string needle = "\"" + fieldName + "\"";
        int idx = json.IndexOf(needle, StringComparison.Ordinal);
        if (idx < 0) return -1;

        int colon = json.IndexOf(':', idx + needle.Length);
        if (colon < 0) return -1;

        int i = colon + 1;
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
        if (i >= json.Length) return -1;

        int sign = 1;
        if (json[i] == '-')
        {
            sign = -1;
            i++;
        }

        long value = 0;
        bool any = false;
        while (i < json.Length && char.IsDigit(json[i]))
        {
            any = true;
            value = (value * 10) + (json[i] - '0');
            if (value > int.MaxValue) return -1;
            i++;
        }

        if (!any) return -1;
        long signed = value * sign;
        if (signed < int.MinValue || signed > int.MaxValue) return -1;
        return (int)signed;
    }

    private static void SetPowerup(string key, int value, bool overwrite)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (value < 0) return;
        if (!overwrite && PlayerPrefs.HasKey(key)) return;
        PlayerPrefs.SetInt(key, value);
    }

    private IEnumerator StartupSyncRoutine()
    {
        var progress = ProgressDataManager.EnsureInstance();
        int pendingBefore = progress.PendingSyncCount;

        yield return ContinueWithDeviceRoutine();
        if (isBlocked) yield break;

        if (pendingBefore > 0)
        {
            progress.TrySyncNow();

            float end = Time.realtimeSinceStartup + 3.0f;
            while (Time.realtimeSinceStartup < end)
            {
                if (!progress.IsSyncInProgress && progress.PendingSyncCount == 0) break;
                yield return null;
            }

            yield return ContinueWithDeviceRoutine();
        }

        BootstrapLeaderboardOnce();
    }

    private void BootstrapLeaderboardOnce()
    {
        if (didLeaderboardBootstrapThisSession) return;
        if (isBlocked) return;
        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            if (DebugLeaderboardHighlight) Debug.Log("Leaderboard bootstrap skipped: ApiBaseUrl empty.");
            return;
        }
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            if (DebugLeaderboardHighlight) Debug.Log("Leaderboard bootstrap skipped: no internet.");
            return;
        }
        string accessToken = PlayerPrefs.GetString("AccessToken", "");
        if (string.IsNullOrEmpty(accessToken))
        {
            if (DebugLeaderboardHighlight) Debug.Log("Leaderboard bootstrap skipped: AccessToken empty.");
            return;
        }

        didLeaderboardBootstrapThisSession = true;
        TryFetchLeaderboard();
    }

    private static int GetCurrentLife()
    {
        int lives = PlayerPrefs.GetInt(LifeKey, MaxLife);
        if (!PlayerPrefs.HasKey(LifeKey))
        {
            lives = MaxLife;
            PlayerPrefs.SetInt(LifeKey, lives);
            PlayerPrefs.Save();
        }
        return Mathf.Clamp(lives, 0, MaxLife);
    }

    public static void AddTrophies(int amount)
    {
        if (amount <= 0) return;
        int trophies = PlayerPrefs.GetInt(TrophiesKey, 0);
        trophies = Mathf.Max(0, trophies + amount);
        PlayerPrefs.SetInt(TrophiesKey, trophies);
        PlayerPrefs.Save();
    }

    public static void RemoveTrophies(int amount)
    {
        if (amount <= 0) return;
        int trophies = PlayerPrefs.GetInt(TrophiesKey, 0);
        trophies = Mathf.Max(0, trophies - amount);
        PlayerPrefs.SetInt(TrophiesKey, trophies);
        PlayerPrefs.Save();
    }

    public static void ConsumeLifeForLose()
    {
        int lives = GetCurrentLife();
        if (lives <= 0) return;

        lives = Mathf.Max(0, lives - 1);
        PlayerPrefs.SetInt(LifeKey, lives);

        if (lives < MaxLife)
        {
            int now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int existing = PlayerPrefs.GetInt(LifeNextReadyKey, 0);
            if (existing <= now)
            {
                PlayerPrefs.SetInt(LifeNextReadyKey, now + LifeRegenSeconds);
            }
        }

        PlayerPrefs.Save();
    }

    private void UpdateLifeTimerUI()
    {
        if (LifeTimerText == null) return;

        int lives = GetCurrentLife();
        if (lives >= MaxLife)
        {
            LifeTimerText.text = "Full";
            PlayerPrefs.DeleteKey(LifeNextReadyKey);
            return;
        }

        int nextReady = PlayerPrefs.GetInt(LifeNextReadyKey, 0);
        int now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (nextReady <= 0)
        {
            LifeTimerText.text = "00:00";
            return;
        }

        if (now >= nextReady)
        {
            int missing = MaxLife - lives;
            if (missing <= 0)
            {
                PlayerPrefs.DeleteKey(LifeNextReadyKey);
                LifeTimerText.text = "Full";
                return;
            }

            int totalIntervals = 1 + Math.Max(0, (now - nextReady) / LifeRegenSeconds);
            int livesToAdd = Mathf.Min(missing, totalIntervals);

            lives += livesToAdd;
            PlayerPrefs.SetInt(LifeKey, lives);

            if (lives >= MaxLife)
            {
                PlayerPrefs.DeleteKey(LifeNextReadyKey);
                LifeTimerText.text = "Full";
                PlayerPrefs.Save();
                UpdateLevelUI();
                return;
            }
            else
            {
                int newNext = nextReady + livesToAdd * LifeRegenSeconds;
                if (newNext <= now) newNext = now + LifeRegenSeconds;

                PlayerPrefs.SetInt(LifeNextReadyKey, newNext);
                PlayerPrefs.Save();
                LifeTimerText.text = FormatSecondsAsTimer(newNext - now);
                UpdateLevelUI();
                return;
            }
        }

        int remaining = nextReady - now;
        LifeTimerText.text = FormatSecondsAsTimer(remaining);
    }

    private static string FormatSecondsAsTimer(int seconds)
    {
        if (seconds < 0) seconds = 0;
        int minutes = seconds / 60;
        int secs = seconds % 60;
        return minutes.ToString("00") + ":" + secs.ToString("00");
    }

    private string PrepareApiLogBody(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;
        if (!RedactTokensInLogs) return body;
        return RedactTokenValue(body, "accessToken", "***");
    }

    private static string RedactTokenValue(string json, string fieldName, string replacement)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName)) return json;

        string a = ReplaceJsonStringField(json, fieldName, replacement);
        string b = ReplaceJsonStringField(a, "refreshToken", replacement);
        return b;
    }

    private static string ReplaceJsonStringField(string json, string fieldName, string replacement)
    {
        string needle = "\"" + fieldName + "\"";
        int idx = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return json;

        int colon = json.IndexOf(':', idx + needle.Length);
        if (colon < 0) return json;

        int firstQuote = json.IndexOf('"', colon + 1);
        if (firstQuote < 0) return json;

        int secondQuote = json.IndexOf('"', firstQuote + 1);
        if (secondQuote < 0) return json;

        return json.Substring(0, firstQuote + 1) + replacement + json.Substring(secondQuote);
    }

    [Serializable]
    private class ContinueWithDeviceRequest
    {
        public string deviceId;
        public string os;
    }

    [Serializable]
    private class ContinueWithDeviceResponse
    {
        public bool success;
        public string message;
        public bool isNewUser;
        public string accessToken;
        public string refreshToken;
        public UserDto user;
    }

    [Serializable]
    private class UserDto
    {
        public string id;
        public string fullName;
        public int level;
        public int trophies = -1;

        public int coins = -1;
        public int coinBalance = -1;
        public int wallet = -1;

        public int life = -1;
        public int pan = -1;
        public int oven = -1;
        public int blender = -1;
        public int verticalKnife = -1;
        public int hat = -1;
        public int KetchUp = -1;

        public PowerupsDto powerups;
        public PowerupsDto powerUps;
    }

    [Serializable]
    private class PowerupsDto
    {
        public int HorizontalKnife = -1;
        public int VerticalKnife = -1;
        public int Pan = -1;
        public int Oven = -1;
        public int Flies = -1;
        public int Blender = -1;
    }

    [Serializable]
    private class LeaderboardTierResponse
    {
        public bool success;
        public string tier;
        public LeaderboardUserRank userRank;
        public LeaderboardEntry[] leaderboard;
    }

    [Serializable]
    private class LeaderboardUserRank
    {
        public int rank;
        public string _id;
        public string fullName;
        public int level;
        public int trophies;
    }

    [Serializable]
    private class LeaderboardEntry
    {
        public int rank;
        public string _id;
        public string fullName;
        public int level;
        public int trophies;
        public string profileImageUrl;
        public bool isDummy;
    }

    // Called by UI Buttons (Oven, Hat, Knife)
    public void SelectBooster(string boosterName)
    {
        if (isBlocked) return;
        if (selectedBoosters.Contains(boosterName))
        {
            // Deselect if already selected
            selectedBoosters.Remove(boosterName);
            Debug.Log("Booster Deselected: " + boosterName);
        }
        else
        {
            int count = GetPowerupCountForBoosterName(boosterName);
            if (count <= 0)
            {
                Debug.LogWarning("Booster not available (count is 0): " + boosterName);
                return;
            }
            selectedBoosters.Add(boosterName);
            Debug.Log("Booster Selected: " + boosterName);
        }
    }

    public void OnPlayButton()
    {
        if (isBlocked) return;
        int lives = GetCurrentLife();
        if (lives <= 0) return;
        if (ShouldBlockPlayForOfflineLevel())
        {
            SetInternetConnectivityPanelVisible(true);
            return;
        }
        SetInternetConnectivityPanelVisible(false);

        ConsumeSelectedBoosters();
        UpdateBoosterCountUI();

        // Save selected boosters for the Gameplay scene
        if (selectedBoosters.Count > 0)
        {
            string joinedBoosters = string.Join(",", selectedBoosters);
            PlayerPrefs.SetString("SelectedBoosters", joinedBoosters);
        }
        else
        {
            PlayerPrefs.DeleteKey("SelectedBoosters");
        }
        ProgressDataManager.EnsureInstance();
        SceneManager.LoadScene("Gameplay");
    }

    private void ConsumeSelectedBoosters()
    {
        if (selectedBoosters == null || selectedBoosters.Count == 0) return;

        foreach (var boosterName in selectedBoosters)
        {
            string key = GetPowerupKeyForBoosterName(boosterName);
            if (string.IsNullOrEmpty(key)) continue;
            ProgressDataManager.EnsureInstance().ConsumePowerup(key, 1);
        }
    }

    private static int GetPowerupCountForBoosterName(string boosterName)
    {
        string key = GetPowerupKeyForBoosterName(boosterName);
        if (string.IsNullOrEmpty(key)) return 0;
        return ProgressDataManager.EnsureInstance().GetPowerupCount(key);
    }

    private static string GetPowerupKeyForBoosterName(string boosterName)
    {
        if (string.IsNullOrEmpty(boosterName)) return null;

        string b = boosterName.Replace(" ", "").Trim();

        if (b.Equals("Oven", StringComparison.OrdinalIgnoreCase) || b.Equals("Hat", StringComparison.OrdinalIgnoreCase))
            return "Powerup_Oven";

        if (b.Equals("Pan", StringComparison.OrdinalIgnoreCase))
            return "Powerup_Pan";

        if (b.Equals("HorizontalKnife", StringComparison.OrdinalIgnoreCase))
            return "Powerup_HorizontalKnife";

        if (b.Equals("VerticalKnife", StringComparison.OrdinalIgnoreCase))
            return "Powerup_VerticalKnife";

        if (b.Equals("Knife", StringComparison.OrdinalIgnoreCase))
            return "Powerup_VerticalKnife";

        if (b.Equals("Flies", StringComparison.OrdinalIgnoreCase))
            return "Powerup_Flies";

        if (b.Equals("Blender", StringComparison.OrdinalIgnoreCase))
            return "Powerup_Blender";

        return null;
    }

    public void OnResetButton()
    {
        if (isBlocked) return;
        ProgressDataManager.EnsureInstance().OverwriteFromServer(1, -1, -1, -1, -1, -1, -1, -1);
        UpdateLevelUI();
        Debug.Log("Progress reset to Level 1");
    }

    private void SetBlockedState(bool blocked)
    {
        isBlocked = blocked;
        PlayerPrefs.SetInt(BlockedKey, blocked ? 1 : 0);
        PlayerPrefs.Save();
        SetBlockedPanelVisible(blocked);
    }

    private void SetBlockedPanelVisible(bool visible)
    {
        if (BlockedPanel != null) BlockedPanel.SetActive(visible);
    }

    private void SetInternetConnectivityPanelVisible(bool visible)
    {
        if (InternetConnectivityPanel != null) InternetConnectivityPanel.SetActive(visible);
    }

    private static bool ShouldBlockPlayForOfflineLevel()
    {
        int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        if (currentLevel <= OfflineLevelLimit) return false;
        return Application.internetReachability == NetworkReachability.NotReachable;
    }

    private static void ClearAuthTokens()
    {
        PlayerPrefs.DeleteKey("AccessToken");
        PlayerPrefs.DeleteKey("RefreshToken");
        PlayerPrefs.DeleteKey("UserId");
        PlayerPrefs.DeleteKey("FullName");
        PlayerPrefs.Save();
    }

    private static bool IsBlockedMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        return message.IndexOf("blocked", StringComparison.OrdinalIgnoreCase) >= 0 ||
               message.IndexOf("ban", StringComparison.OrdinalIgnoreCase) >= 0 ||
               message.IndexOf("banned", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
