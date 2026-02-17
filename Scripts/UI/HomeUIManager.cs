using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Networking;

public class HomeUIManager : MonoBehaviour
{
    private static bool didBackendBootstrapThisSession = false;
    private const string BlockedKey = "AccountBlocked";
    private const int OfflineLevelLimit = 10;

    public Text LevelText, LevelText2;
    public Text CoinsText;
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
    public GameObject BlockedPanel;
    public GameObject InternetConnectivityPanel;

    // Track selected boosters
    private System.Collections.Generic.List<string> selectedBoosters = new System.Collections.Generic.List<string>();
    private bool isBlocked;

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
