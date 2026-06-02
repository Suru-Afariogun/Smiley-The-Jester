using UnityEngine;

/// <summary>
/// Shared projectile / enemy prefabs for clouds. Place once in the level (or rely on per-prefab wiring).
/// </summary>
public class CloudSpawnDefaults : MonoBehaviour
{
    public static CloudSpawnDefaults Instance { get; private set; }

    [SerializeField] GameObject moodDropEnemyPrefab;
    [SerializeField] GameObject raindropProjectilePrefab;
    [SerializeField] GameObject lightningBoltPrefab;

    public GameObject MoodDropEnemyPrefab => moodDropEnemyPrefab;
    public GameObject RaindropProjectilePrefab => raindropProjectilePrefab;
    public GameObject LightningBoltPrefab => lightningBoltPrefab;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CloudSpawnDefaults] Duplicate in scene — keeping the first instance.");
            return;
        }

        Instance = this;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInScene()
    {
        if (Instance != null)
            return;

        CloudSpawnDefaults existing = FindFirstObjectByType<CloudSpawnDefaults>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

#if UNITY_EDITOR
        const string prefabPath = "Assets/Prefabs/CloudSpawnDefaults.prefab";
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
            Instantiate(prefab);
#endif
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
