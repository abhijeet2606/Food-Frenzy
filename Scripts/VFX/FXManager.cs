using System.Collections.Generic;
using UnityEngine;

public class FXManager : MonoBehaviour
{
    public static FXManager Instance;

    [System.Serializable]
    public class FXEntry
    {
        public string key;
        public GameObject prefab;
        public int preload = 4;
        public float defaultLifetime = 1.2f;
    }

    public List<FXEntry> entries = new List<FXEntry>();

    private readonly Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
    private readonly Dictionary<string, FXEntry> entryMap = new Dictionary<string, FXEntry>();

    public static FXManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("FXManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<FXManager>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        // If no entries configured via editor tool, try to build a minimal runtime set
        if (entries == null || entries.Count == 0)
        {
            TryAddRuntimeDefaults();
        }

        entryMap.Clear();
        pools.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.prefab == null || string.IsNullOrEmpty(e.key)) continue;
            entryMap[e.key] = e;
            var q = new Queue<GameObject>();
            int count = Mathf.Max(1, e.preload);
            for (int j = 0; j < count; j++)
            {
                var go = Instantiate(e.prefab, transform);
                go.SetActive(false);
                q.Enqueue(go);
            }
            pools[e.key] = q;
        }
    }

    private void TryAddRuntimeDefaults()
    {
        entries = new List<FXEntry>();

        // These prefabs exist under Assets/Resources/Prefabs in your project
        TryAdd("fx_pop_small",   "Prefabs/explosionblue", 8, 0.8f);
        TryAdd("fx_row_sweep",   "Prefabs/swirl_blue",    4, 1.0f);
        TryAdd("fx_bomb_big",    "Prefabs/explosionred",  4, 1.2f);
        TryAdd("fx_goal_sparkle","Prefabs/swirl_pink",    4, 1.0f);
        TryAdd("fx_confetti",    "Prefabs/swirl_orange",  3, 1.5f);
        TryAdd("fx_lose_puff",   "Prefabs/explosiongreen",3, 1.0f);
    }

    private void TryAdd(string key, string resourcesPath, int preload, float lifetime)
    {
        var prefab = Resources.Load<GameObject>(resourcesPath);
        if (prefab == null) return;
        entries.Add(new FXEntry
        {
            key = key,
            prefab = prefab,
            preload = preload,
            defaultLifetime = lifetime
        });
    }

    public void Play(string key, Vector3 position, Transform parent = null, float lifetime = -1f)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (Instance == null) EnsureInstance();
        if (!entryMap.ContainsKey(key)) return;
        if (!pools.ContainsKey(key)) pools[key] = new Queue<GameObject>();

        var e = entryMap[key];
        var pool = pools[key];
        GameObject go = pool.Count > 0 ? pool.Dequeue() : Instantiate(e.prefab, transform);
        if (parent != null) go.transform.SetParent(parent, false);
        else go.transform.SetParent(transform, false);
        go.transform.position = position;
        go.SetActive(true);

        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null) ps.Play(true);

        float lt = lifetime > 0 ? lifetime : Mathf.Max(0.2f, e.defaultLifetime);
        StartCoroutine(ReturnToPoolAfter(go, key, lt));
    }

    private System.Collections.IEnumerator ReturnToPoolAfter(GameObject go, string key, float t)
    {
        yield return new WaitForSeconds(t);
        if (go == null) yield break;
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        if (!pools.ContainsKey(key)) pools[key] = new Queue<GameObject>();
        pools[key].Enqueue(go);
    }
}
