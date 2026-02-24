using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public sealed class ProgressDataManager : MonoBehaviour
{
    public static ProgressDataManager Instance;
    private const string LifeKey = "Life";
    private const string TrophiesKey = "Trophies";

    [Serializable]
    public class ProgressSnapshot
    {
        public int level = 1;
        public int coins = 0;
        public int winStreak = 0;

        public int oven = 0;
        public int pan = 0;
        public int blender = 0;
        public int horizontalKnife = 0;
        public int verticalKnife = 0;
        public int flies = 0;
    }

    private enum SyncEventType
    {
        LevelComplete = 1,
        InventoryOnly = 2,
        LevelFailed = 3
    }

    [Serializable]
    private class PendingSyncEvent
    {
        public string id;
        public int type;
        public int level;
        public int coinsDelta;
        public int trophiesDelta;
        public ProgressSnapshot snapshot;
        public long createdUtc;
        public int attempts;
        public long nextAttemptUtc;
        public string lastError;
    }

    [Serializable]
    private class Bundle
    {
        public ProgressSnapshot snapshot;
        public ProgressSnapshot serverSnapshot;
        public bool hasServerSnapshot;
        public List<PendingSyncEvent> queue;
        public long lastSavedUtc;
        public string schema = "progress_bundle_v2";
    }

    [Serializable]
    private class LevelCompleteRequest
    {
        public int level;
        public int coins;
        public int trophies;
        public int oven;
        public int pan;
        public int blender;
        public int horizontalKnife;
        public int verticalKnife;
        public int flies;
    }

    [Serializable]
    private class LevelCompleteResponse
    {
        public bool success;
        public LevelCompleteData data;
    }

    [Serializable]
    private class LevelCompleteData
    {
        public int level;
        public Wallet wallet;
        public Inventory inventory;
    }

    [Serializable]
    private class Wallet
    {
        public int coins = -1;
        public int diamonds = -1;
        public int trophies = -1;
    }

    [Serializable]
    private class Inventory
    {
        public int life = -1;
        public int oven = -1;
        public int pan = -1;
        public int blender = -1;
        public int hat = -1;
        public int horizontalKnife = -1;
        public int verticalKnife = -1;
        public int flies = -1;
    }

    public string ApiBaseUrl = "https://apigame.blazemobilestudio.com/api";
    public bool Log = true;
    public bool SyncOnLevelComplete = true;
    public bool SyncOnAppClose = true;
    public bool SyncOnLaunch = true;

    public event Action<ProgressSnapshot> OnSnapshotChanged;
    public event Action<int> OnPendingSyncCountChanged;

    private readonly object gate = new object();
    private Bundle bundle;
    private bool syncInProgress;
    private Coroutine syncRoutine;
    private float lastSyncStartRealtime;
    private float nextAutoSyncCheckRealtime;

    public bool IsSyncInProgress => syncInProgress;

    public int PendingSyncCount
    {
        get
        {
            lock (gate)
            {
                return bundle != null && bundle.queue != null ? bundle.queue.Count : 0;
            }
        }
    }

    public static ProgressDataManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("ProgressDataManager");
        Instance = go.AddComponent<ProgressDataManager>();
        DontDestroyOnLoad(go);
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        LoadOrCreate();
        PublishAll();
    }

    private void Start()
    {
        if (SyncOnLaunch) TrySyncNow();
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup < nextAutoSyncCheckRealtime) return;
        nextAutoSyncCheckRealtime = Time.realtimeSinceStartup + 2.0f;

        if (syncInProgress) return;
        if (Application.internetReachability == NetworkReachability.NotReachable) return;
        if (PendingSyncCount == 0) return;
        if (!HasServerBaseline()) return;

        TrySyncNow();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus) return;
        SaveBundle();
        if (SyncOnAppClose) TrySyncNow();
    }

    private void OnApplicationQuit()
    {
        SaveBundle();
        if (SyncOnAppClose) TrySyncNow();
    }

    public ProgressSnapshot GetSnapshotCopy()
    {
        lock (gate)
        {
            return CloneSnapshot(bundle.snapshot);
        }
    }

    public int GetPowerupCount(string key)
    {
        lock (gate)
        {
            switch (key)
            {
                case "Powerup_Oven": return bundle.snapshot.oven;
                case "Powerup_Pan": return bundle.snapshot.pan;
                case "Powerup_Blender": return bundle.snapshot.blender;
                case "Powerup_HorizontalKnife": return bundle.snapshot.horizontalKnife;
                case "Powerup_VerticalKnife": return bundle.snapshot.verticalKnife;
                case "Powerup_Flies": return bundle.snapshot.flies;
                default: return 0;
            }
        }
    }

    public void ApplyLevelCompleted(int completedLevel, int coinsEarned, int newWinStreak, int trophiesEarned)
    {
        if (completedLevel <= 0) completedLevel = 1;
        if (coinsEarned < 0) coinsEarned = 0;
        if (newWinStreak < 0) newWinStreak = 0;

        lock (gate)
        {
            bundle.snapshot.level = Math.Max(bundle.snapshot.level, completedLevel + 1);
            bundle.snapshot.coins = SafeAdd(bundle.snapshot.coins, coinsEarned);
            bundle.snapshot.winStreak = newWinStreak;
            
            // Trophies are managed locally and synced via delta
            int currentTrophies = PlayerPrefs.GetInt(TrophiesKey, 0);
            PlayerPrefs.SetInt(TrophiesKey, Math.Max(0, currentTrophies + trophiesEarned));

            EnqueueSyncEventLocked(SyncEventType.LevelComplete, completedLevel, coinsEarned, trophiesEarned);
            SaveBundleLocked();
        }

        PublishAll();
        if (Log) Debug.Log($"[Progress] LevelComplete local. completedLevel={completedLevel} coinsEarned={coinsEarned} trophiesDelta={trophiesEarned} levelNow={PlayerPrefs.GetInt("CurrentLevel", 1)} coinsNow={PlayerPrefs.GetInt("TotalCoins", 0)} pending={PendingSyncCount}");
        if (SyncOnLevelComplete) TrySyncNow();
    }

    public void ApplyLevelFailed(int level, int trophiesDelta)
    {
        lock (gate)
        {
            // For a failed level, we don't increase level count or coins (usually 0)
            // But we do record the trophy deduction
            int currentTrophies = PlayerPrefs.GetInt(TrophiesKey, 0);
            PlayerPrefs.SetInt(TrophiesKey, Math.Max(0, currentTrophies + trophiesDelta));

            // Enqueue a LevelFailed event, backend should not increment level
            EnqueueSyncEventLocked(SyncEventType.LevelFailed, level, 0, trophiesDelta);
            SaveBundleLocked();
        }

        PublishAll();
        if (Log) Debug.Log($"[Progress] LevelFailed local. level={level} trophiesDelta={trophiesDelta} pending={PendingSyncCount}");
        if (SyncOnLevelComplete) TrySyncNow();
    }

    public void ConsumePowerup(string key, int amount)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (amount <= 0) return;

        lock (gate)
        {
            int current = GetPowerupCount(key);
            int next = current - amount;
            if (next < 0) next = 0;

            switch (key)
            {
                case "Powerup_Oven": bundle.snapshot.oven = next; break;
                case "Powerup_Pan": bundle.snapshot.pan = next; break;
                case "Powerup_Blender": bundle.snapshot.blender = next; break;
                case "Powerup_HorizontalKnife": bundle.snapshot.horizontalKnife = next; break;
                case "Powerup_VerticalKnife": bundle.snapshot.verticalKnife = next; break;
                case "Powerup_Flies": bundle.snapshot.flies = next; break;
                default: return;
            }

            EnqueueSyncEventLocked(SyncEventType.InventoryOnly, 0, 0, 0);
            SaveBundleLocked();
        }

        PublishAll();
        if (Log) Debug.Log($"[Progress] Inventory local. key={key} amount={amount} pending={PendingSyncCount}");
    }

    public bool TrySpendCoins(int amount, out string error)
    {
        error = null;
        if (amount <= 0)
        {
            error = "invalid amount";
            return false;
        }

        lock (gate)
        {
            if (bundle == null || bundle.snapshot == null)
            {
                error = "progress not ready";
                return false;
            }

            int current = bundle.snapshot.coins;
            if (current < amount)
            {
                error = "insufficient coins";
                return false;
            }

            bundle.snapshot.coins = current - amount;
            EnqueueSyncEventLocked(SyncEventType.InventoryOnly, 0, -amount, 0);
            SaveBundleLocked();
        }

        PublishAll();
        return true;
    }

    public void OverwriteFromServer(int level, int coins, int oven, int pan, int blender, int horizontalKnife, int verticalKnife, int flies)
    {
        lock (gate)
        {
            if (bundle.serverSnapshot == null) bundle.serverSnapshot = CloneSnapshot(bundle.snapshot);

            if (level > 0) bundle.serverSnapshot.level = level;
            if (coins >= 0) bundle.serverSnapshot.coins = coins;

            if (oven >= 0) bundle.serverSnapshot.oven = oven;
            if (pan >= 0) bundle.serverSnapshot.pan = pan;
            if (blender >= 0) bundle.serverSnapshot.blender = blender;
            if (horizontalKnife >= 0) bundle.serverSnapshot.horizontalKnife = horizontalKnife;
            if (verticalKnife >= 0) bundle.serverSnapshot.verticalKnife = verticalKnife;
            if (flies >= 0) bundle.serverSnapshot.flies = flies;

            bundle.hasServerSnapshot = true;

            if (bundle.snapshot != null && bundle.serverSnapshot != null)
            {
                bundle.snapshot.oven = Math.Min(bundle.snapshot.oven, bundle.serverSnapshot.oven);
                bundle.snapshot.pan = Math.Min(bundle.snapshot.pan, bundle.serverSnapshot.pan);
                bundle.snapshot.blender = Math.Min(bundle.snapshot.blender, bundle.serverSnapshot.blender);
                bundle.snapshot.horizontalKnife = Math.Min(bundle.snapshot.horizontalKnife, bundle.serverSnapshot.horizontalKnife);
                bundle.snapshot.verticalKnife = Math.Min(bundle.snapshot.verticalKnife, bundle.serverSnapshot.verticalKnife);
                bundle.snapshot.flies = Math.Min(bundle.snapshot.flies, bundle.serverSnapshot.flies);
            }

            if (bundle.queue == null || bundle.queue.Count == 0)
            {
                bundle.snapshot = CloneSnapshot(bundle.serverSnapshot);
            }

            SaveBundleLocked();
        }

        PublishAll();
        if (Log) Debug.Log($"[Progress] Server overwrite. level={level} coins={coins} oven={oven} pan={pan} blender={blender} hKnife={horizontalKnife} vKnife={verticalKnife} flies={flies}");
    }

    public void TrySyncNow()
    {
        if (syncInProgress) return;
        if (Time.realtimeSinceStartup - lastSyncStartRealtime < 1.0f) return;
        if (!HasServerBaseline()) return;

        if (syncRoutine != null) StopCoroutine(syncRoutine);
        syncRoutine = StartCoroutine(SyncRoutine());
    }

    private IEnumerator SyncRoutine()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable) yield break;

        string accessToken = PlayerPrefs.GetString("AccessToken", "");
        if (string.IsNullOrEmpty(accessToken)) yield break;
        if (!HasServerBaseline()) yield break;

        syncInProgress = true;
        lastSyncStartRealtime = Time.realtimeSinceStartup;

        while (true)
        {
            PendingSyncEvent next;
            lock (gate)
            {
                next = PeekNextReadyEventLocked();
                if (next == null)
                {
                    syncInProgress = false;
                    yield break;
                }
            }

            bool ok = false;
            string err = null;
            yield return SendEventOnce(accessToken, next, v => ok = v, e => err = e);

            lock (gate)
            {
                if (ok)
                {
                    RemoveEventByIdLocked(next.id);
                    // Only adopt full server snapshot after successful LevelComplete (win) events.
                    if (next.type == (int)SyncEventType.LevelComplete)
                    {
                        if (bundle.queue != null && bundle.queue.Count == 0 && bundle.serverSnapshot != null)
                        {
                            bundle.snapshot = CloneSnapshot(bundle.serverSnapshot);
                        }
                    }
                    SaveBundleLocked();
                }
                else
                {
                    MarkEventFailedLocked(next.id, err);
                    SaveBundleLocked();
                    syncInProgress = false;
                    PublishAll();
                    yield break;
                }
            }

            PublishAll();
            yield return null;
        }
    }

    private IEnumerator SendEventOnce(string accessToken, PendingSyncEvent e, Action<bool> onDone, Action<string> onError)
    {
        if (e == null || e.snapshot == null)
        {
            onError?.Invoke("null event");
            onDone?.Invoke(false);
            yield break;
        }

        string url = CombineUrl(ApiBaseUrl, "player/level/complete");

        int ovenDelta = 0;
        int panDelta = 0;
        int blenderDelta = 0;
        int horizontalKnifeDelta = 0;
        int verticalKnifeDelta = 0;
        int fliesDelta = 0;

        lock (gate)
        {
            if (bundle != null && bundle.hasServerSnapshot && bundle.serverSnapshot != null)
            {
                ovenDelta = e.snapshot.oven - bundle.serverSnapshot.oven;
                panDelta = e.snapshot.pan - bundle.serverSnapshot.pan;
                blenderDelta = e.snapshot.blender - bundle.serverSnapshot.blender;
                horizontalKnifeDelta = e.snapshot.horizontalKnife - bundle.serverSnapshot.horizontalKnife;
                verticalKnifeDelta = e.snapshot.verticalKnife - bundle.serverSnapshot.verticalKnife;
                fliesDelta = e.snapshot.flies - bundle.serverSnapshot.flies;
            }
        }

        int levelForRequest = 0;
        lock (gate)
        {
            if (e.type == (int)SyncEventType.LevelComplete)
            {
                levelForRequest = e.level;
            }
            else if (e.type == (int)SyncEventType.LevelFailed)
            {
                levelForRequest = 0;
            }
            else
            {
                if (bundle != null && bundle.hasServerSnapshot && bundle.serverSnapshot != null)
                {
                    levelForRequest = Math.Max(0, bundle.serverSnapshot.level - 1);
                }
                else
                {
                    levelForRequest = 0;
                }
            }
        }

        var payload = new LevelCompleteRequest
        {
            level = levelForRequest,
            coins = e.coinsDelta,
            trophies = e.trophiesDelta,
            oven = ovenDelta,
            pan = panDelta,
            blender = blenderDelta,
            horizontalKnife = horizontalKnifeDelta,
            verticalKnife = verticalKnifeDelta,
            flies = fliesDelta
        };

        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + accessToken);

            UnityWebRequestAsyncOperation op;
            try
            {
                op = req.SendWebRequest();
            }
            catch (InvalidOperationException)
            {
                onError?.Invoke("insecure connection not allowed");
                onDone?.Invoke(false);
                yield break;
            }

            yield return op;

            string body = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"http {(int)req.responseCode} {req.error} {body}");
                onDone?.Invoke(false);
                yield break;
            }

            if (Log) Debug.Log($"[Progress] Sync ok. eventId={e.id} type={e.type} body={body}");

            LevelCompleteResponse resp;
            try
            {
                resp = JsonUtility.FromJson<LevelCompleteResponse>(body);
            }
            catch
            {
                onError?.Invoke("invalid json");
                onDone?.Invoke(false);
                yield break;
            }

            if (resp == null || !resp.success || resp.data == null)
            {
                onError?.Invoke("unsuccessful");
                onDone?.Invoke(false);
                yield break;
            }

            ApplyServerResponse(resp, e.type);
            onDone?.Invoke(true);
        }
    }

    private void ApplyServerResponse(LevelCompleteResponse resp, int triggeringEventType)
    {
        if (resp == null || resp.data == null) return;

        lock (gate)
        {
            if (bundle.serverSnapshot == null) bundle.serverSnapshot = CloneSnapshot(bundle.snapshot);
            bundle.hasServerSnapshot = true;

            if (triggeringEventType == (int)SyncEventType.LevelComplete && resp.data.level > 0)
            {
                bundle.serverSnapshot.level = resp.data.level;
            }
            if (resp.data.wallet != null && resp.data.wallet.coins >= 0) bundle.serverSnapshot.coins = resp.data.wallet.coins;
            if (resp.data.wallet != null && resp.data.wallet.trophies >= 0) PlayerPrefs.SetInt(TrophiesKey, resp.data.wallet.trophies);

            if (resp.data.inventory != null)
            {
                if (!PlayerPrefs.HasKey(LifeKey))
                {
                    if (resp.data.inventory.life >= 0) PlayerPrefs.SetInt(LifeKey, resp.data.inventory.life);
                }
                if (resp.data.inventory.oven >= 0) bundle.serverSnapshot.oven = resp.data.inventory.oven;
                if (resp.data.inventory.pan >= 0) bundle.serverSnapshot.pan = resp.data.inventory.pan;
                if (resp.data.inventory.blender >= 0) bundle.serverSnapshot.blender = resp.data.inventory.blender;
                if (resp.data.inventory.horizontalKnife >= 0) bundle.serverSnapshot.horizontalKnife = resp.data.inventory.horizontalKnife;
                if (resp.data.inventory.verticalKnife >= 0) bundle.serverSnapshot.verticalKnife = resp.data.inventory.verticalKnife;
                if (resp.data.inventory.flies >= 0) bundle.serverSnapshot.flies = resp.data.inventory.flies;
            }

            SaveBundleLocked();
        }
    }

    private void EnqueueSyncEventLocked(SyncEventType type, int level, int coinsDelta, int trophiesDelta)
    {
        if (bundle.queue == null) bundle.queue = new List<PendingSyncEvent>();

        if (type == SyncEventType.InventoryOnly && bundle.queue.Count > 0)
        {
            var last = bundle.queue[bundle.queue.Count - 1];
            if (last != null && last.type == (int)SyncEventType.InventoryOnly)
            {
                last.snapshot = CloneSnapshot(bundle.snapshot);
                last.level = level;
                last.coinsDelta += coinsDelta;
                last.trophiesDelta += trophiesDelta;
                last.lastError = null;
                last.nextAttemptUtc = 0;
                return;
            }
        }

        var now = DateTimeOffset.UtcNow;
        var evt = new PendingSyncEvent
        {
            id = Guid.NewGuid().ToString("N"),
            type = (int)type,
            level = level,
            coinsDelta = coinsDelta,
            trophiesDelta = trophiesDelta,
            snapshot = CloneSnapshot(bundle.snapshot),
            createdUtc = now.ToUnixTimeSeconds(),
            attempts = 0,
            nextAttemptUtc = 0,
            lastError = null
        };

        bundle.queue.Add(evt);
    }

    private PendingSyncEvent PeekNextReadyEventLocked()
    {
        if (bundle.queue == null || bundle.queue.Count == 0) return null;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        PendingSyncEvent best = null;
        foreach (var e in bundle.queue)
        {
            if (e == null) continue;
            if (e.nextAttemptUtc > now) continue;
            best = e;
            break;
        }
        return best;
    }

    private void RemoveEventByIdLocked(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (bundle.queue == null) return;
        bundle.queue.RemoveAll(e => e != null && e.id == id);
    }

    private void MarkEventFailedLocked(string id, string err)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (bundle.queue == null) return;

        var e = bundle.queue.Find(x => x != null && x.id == id);
        if (e == null) return;

        e.attempts = Math.Max(0, e.attempts) + 1;
        e.lastError = err ?? "unknown";

        int delay = 5;
        if (e.attempts >= 2) delay = 10;
        if (e.attempts >= 3) delay = 20;
        if (e.attempts >= 4) delay = 40;
        if (e.attempts >= 5) delay = 80;
        if (e.attempts >= 6) delay = 160;
        if (e.attempts >= 7) delay = 300;

        e.nextAttemptUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + delay;

        if (Log) Debug.LogWarning($"[Progress] Sync failed. eventId={e.id} attempt={e.attempts} nextInSec={delay} err={e.lastError}");
    }

    private void PublishAll()
    {
        ProgressSnapshot snap;
        int pending;
        lock (gate)
        {
            snap = CloneSnapshot(bundle.snapshot);
            pending = bundle.queue != null ? bundle.queue.Count : 0;
            WriteSnapshotToPlayerPrefsLocked(bundle.snapshot);
        }

        OnSnapshotChanged?.Invoke(snap);
        OnPendingSyncCountChanged?.Invoke(pending);
    }

    private void LoadOrCreate()
    {
        lock (gate)
        {
            bundle = LoadBundleLocked();
            if (bundle == null)
            {
                bundle = new Bundle
                {
                    snapshot = ReadSnapshotFromPlayerPrefsLocked(),
                    serverSnapshot = null,
                    hasServerSnapshot = false,
                    queue = new List<PendingSyncEvent>(),
                    lastSavedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                SaveBundleLocked();
            }
            else
            {
                if (bundle.serverSnapshot == null) bundle.serverSnapshot = CloneSnapshot(bundle.snapshot);
            }
        }
    }

    private void SaveBundle()
    {
        lock (gate)
        {
            SaveBundleLocked();
        }
    }

    private void SaveBundleLocked()
    {
        if (bundle == null) return;
        if (bundle.snapshot == null) bundle.snapshot = new ProgressSnapshot();
        if (bundle.serverSnapshot == null) bundle.serverSnapshot = CloneSnapshot(bundle.snapshot);
        if (bundle.queue == null) bundle.queue = new List<PendingSyncEvent>();

        bundle.lastSavedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        string json = JsonUtility.ToJson(bundle);
        string path = GetBundlePath();
        string tmp = path + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
        catch (Exception ex)
        {
            if (Log) Debug.LogError($"[Progress] Save failed. path={path} err={ex.GetType().Name}:{ex.Message}");
        }

        WriteSnapshotToPlayerPrefsLocked(bundle.snapshot);
        PlayerPrefs.Save();
    }

    private bool HasServerBaseline()
    {
        lock (gate)
        {
            return bundle != null && bundle.hasServerSnapshot && bundle.serverSnapshot != null;
        }
    }

    private Bundle LoadBundleLocked()
    {
        string path = GetBundlePath();
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrEmpty(json)) return null;
            var loaded = JsonUtility.FromJson<Bundle>(json);
            if (loaded == null || loaded.snapshot == null) return null;
            if (loaded.queue == null) loaded.queue = new List<PendingSyncEvent>();
            return loaded;
        }
        catch (Exception ex)
        {
            if (Log) Debug.LogError($"[Progress] Load failed. path={path} err={ex.GetType().Name}:{ex.Message}");
            return null;
        }
    }

    private static ProgressSnapshot CloneSnapshot(ProgressSnapshot s)
    {
        if (s == null) return new ProgressSnapshot();
        return new ProgressSnapshot
        {
            level = s.level,
            coins = s.coins,
            winStreak = s.winStreak,
            oven = s.oven,
            pan = s.pan,
            blender = s.blender,
            horizontalKnife = s.horizontalKnife,
            verticalKnife = s.verticalKnife,
            flies = s.flies
        };
    }

    private static int SafeAdd(int a, int b)
    {
        long v = (long)a + b;
        if (v > int.MaxValue) return int.MaxValue;
        if (v < int.MinValue) return int.MinValue;
        return (int)v;
    }

    private static string GetBundlePath()
    {
        return Path.Combine(Application.persistentDataPath, "progress_bundle.json");
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

    private static ProgressSnapshot ReadSnapshotFromPlayerPrefsLocked()
    {
        return new ProgressSnapshot
        {
            level = PlayerPrefs.GetInt("CurrentLevel", 1),
            coins = PlayerPrefs.GetInt("TotalCoins", 0),
            winStreak = PlayerPrefs.GetInt("WinStreak", 0),
            oven = PlayerPrefs.GetInt("Powerup_Oven", 0),
            pan = PlayerPrefs.GetInt("Powerup_Pan", 0),
            blender = PlayerPrefs.GetInt("Powerup_Blender", 0),
            horizontalKnife = PlayerPrefs.GetInt("Powerup_HorizontalKnife", 0),
            verticalKnife = PlayerPrefs.GetInt("Powerup_VerticalKnife", 0),
            flies = PlayerPrefs.GetInt("Powerup_Flies", 0)
        };
    }

    private static void WriteSnapshotToPlayerPrefsLocked(ProgressSnapshot s)
    {
        if (s == null) return;
        PlayerPrefs.SetInt("CurrentLevel", Math.Max(1, s.level));
        PlayerPrefs.SetInt("TotalCoins", Math.Max(0, s.coins));
        PlayerPrefs.SetInt("WinStreak", Math.Max(0, s.winStreak));
        PlayerPrefs.SetInt("Powerup_Oven", Math.Max(0, s.oven));
        PlayerPrefs.SetInt("Powerup_Pan", Math.Max(0, s.pan));
        PlayerPrefs.SetInt("Powerup_Blender", Math.Max(0, s.blender));
        PlayerPrefs.SetInt("Powerup_HorizontalKnife", Math.Max(0, s.horizontalKnife));
        PlayerPrefs.SetInt("Powerup_VerticalKnife", Math.Max(0, s.verticalKnife));
        PlayerPrefs.SetInt("Powerup_Flies", Math.Max(0, s.flies));
    }
}
