using System.Collections;

using System.Collections.Generic;

using UnityEngine;

using UnityEngine.Serialization;



public enum CloudType

{

    Platform,

    MoodDrop,

    Raindrop,

    Lightning,

    Happy,

    MediumHappy,

    SmallHappy

}



/// <summary>
/// One component per cloud object. Handles cloud type, hover, spawning (MoodDrop / rain / lightning),
/// Happy Star conversions, and swapping sprites or appearance prefabs when the type changes.
/// </summary>
public class Clouds : MonoBehaviour

{

    const int RainHitsToBecomeMoodDrop = 8;

    const float MoodDropRevertSeconds = 5f;

    const int GlobalMoodDropCap = 6;

    const int MoodDropsPerWave = 3;

    const float RainSpawnInterval = 4f;

    const float RainFirePointDelay = 0.5f;

    const float LightningActiveDuration = 4f;

    const float LightningCooldownDuration = 3f;



    [Header("Cloud type")]

    [SerializeField] CloudType initialCloudType = CloudType.Platform;



    [Header("Hover — platform & happy clouds")]

    [SerializeField] float hoverAmplitude = 0.5f;

    [SerializeField] float hoverSpeed = 1f;

    [Tooltip("Platform clouds only — lower = slower bob.")]

    [SerializeField] float platformHoverSpeed = 0.35f;



    [Header("Cloud appearance prefabs (visual theme to switch to)")]

    [Tooltip("Shown while this object is a Platform cloud.")]

    [SerializeField] GameObject platformCloudAppearance;

    [Tooltip("Shown while this object is a MoodDrop cloud.")]

    [SerializeField] GameObject moodDropCloudAppearance;

    [Tooltip("Shown while this object is a Raindrop cloud.")]

    [SerializeField] GameObject raindropCloudAppearance;

    [Tooltip("Shown while this object is a Lightning cloud.")]

    [SerializeField] GameObject lightningCloudAppearance;

    [Tooltip("Happy Star converts MoodDrop clouds to this look.")]

    [SerializeField] GameObject happyCloudAppearance;

    [Tooltip("Happy Star converts Raindrop clouds to this look.")]

    [SerializeField] GameObject mediumHappyCloudAppearance;

    [Tooltip("Happy Star converts Lightning clouds to this look.")]

    [SerializeField] GameObject smallHappyCloudAppearance;



    [Header("Cloud appearance sprites (fallback if prefab slot is empty)")]

    [SerializeField] Sprite platformCloudSprite;

    [SerializeField] Sprite moodDropCloudSprite;

    [SerializeField] Sprite raindropCloudSprite;

    [SerializeField] Sprite lightningCloudSprite;

    [SerializeField] Sprite happyCloudSprite;

    [SerializeField] Sprite mediumHappyCloudSprite;

    [SerializeField] Sprite smallHappyCloudSprite;



    [Header("Optional appearance root (child visuals spawn here)")]

    [SerializeField] Transform cloudAppearanceRoot;



    [Header("MoodDrop cloud — enemy spawn prefab")]

    [FormerlySerializedAs("moodDropPrefab")]

    [SerializeField] GameObject moodDropEnemyPrefab;

    [SerializeField] Transform[] moodDropFirePoints = new Transform[3];



    [Header("Raindrop cloud — projectile spawn prefab")]

    [FormerlySerializedAs("raindropPrefab")]

    [SerializeField] GameObject raindropProjectilePrefab;

    [SerializeField] Transform[] rainFirePoints = new Transform[2];



    [Header("Lightning cloud — hover")]

    [Tooltip("How far up and down the cloud bobs (world units).")]

    [SerializeField] float lightningHoverAmplitude = 0.4f;

    [Tooltip("Bob cycle speed — higher = faster up/down motion.")]

    [SerializeField] float lightningHoverSpeed = 1.5f;

    [Header("Lightning cloud — bolt spawn prefab")]

    [FormerlySerializedAs("lightningBoltPrefab")]

    [SerializeField] GameObject lightningBoltPrefab;

    [SerializeField] Transform lightningFirePoint;



    CloudType currentType;

    Vector3 restPosition;

    bool convertedFromRaindrop;

    int rainHitsReceived;

    float lastRainHitTime;



    readonly List<MoodDrop> activeMoodDropsFromThisCloud = new();

    GameObject attachedLightning;

    Coroutine lightningBehaviorCoroutine;

    GameObject activeAppearanceInstance;



    static Clouds activeHappyCloud;

    CloudType typeBeforeHappy;



    Collider2D cloudCollider;

    SpriteRenderer hostSpriteRenderer;



    public CloudType CurrentType => currentType;

    public bool IsPlatformCloud => currentType == CloudType.Platform;

    public Collider2D CloudCollider => cloudCollider;



    void Awake()

    {

        restPosition = transform.position;

        currentType = initialCloudType;

        cloudCollider = GetComponent<Collider2D>();

        hostSpriteRenderer = GetComponent<SpriteRenderer>();

        SanitizeAppearancePrefabReferences();
        FixWrongLightningCloudSprite();
        ConfigureFlightRigidbody();

    }

    void FixWrongLightningCloudSprite()
    {
        if (currentType != CloudType.Lightning)
            return;

        EnsureHostSpriteRenderer();
        if (hostSpriteRenderer == null)
            return;

        const string characterAttackSpriteName = "Smiley The Jester assests page 2_6";

        if (lightningCloudSprite != null &&
            lightningCloudSprite.name != characterAttackSpriteName)
        {
            if (hostSpriteRenderer.sprite == null ||
                hostSpriteRenderer.sprite.name == characterAttackSpriteName)
                hostSpriteRenderer.sprite = lightningCloudSprite;
        }
    }

    void ConfigureFlightRigidbody()
    {
        if (!TryGetComponent(out Rigidbody2D body))
            return;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }



    void Start()

    {

        ResolveSpawnPrefabs();
        ResolveLocalFirePoints();
        ConfigureFirePointColliders();

        // Keep the prefab SpriteRenderer art for types that are authored on the prefab (avoid wrong sheet slots).

        bool preservePrefabSprite = currentType == CloudType.Platform

            || currentType == CloudType.Lightning

            || currentType == CloudType.MoodDrop;

        ApplyCloudAppearance(currentType, allowOverwriteExistingSprite: !preservePrefabSprite);

        SyncRigidbodyToTransform();
        restPosition = transform.position;

        BeginBehaviorForCurrentType();

    }

    void ConfigureFirePointColliders()
    {
        CloudFirePoint.ConfigureFirePointArray(moodDropFirePoints);
        CloudFirePoint.ConfigureFirePointArray(rainFirePoints);
        CloudFirePoint.ConfigureTransform(lightningFirePoint);

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name.StartsWith("FirePoint"))
                CloudFirePoint.ConfigureTransform(child);
        }
    }

    void IgnoreSpawnCollisionsWithCloud(GameObject spawnedInstance)
    {
        if (spawnedInstance == null)
            return;

        Collider2D[] cloudColliders = GetComponents<Collider2D>();
        if (cloudColliders.Length == 0)
            return;

        Collider2D[] spawnedColliders = spawnedInstance.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D spawnedCollider in spawnedColliders)
        {
            if (spawnedCollider == null || spawnedCollider.isTrigger)
                continue;

            foreach (Collider2D cloudCollider in cloudColliders)
            {
                if (cloudCollider != null && !cloudCollider.isTrigger)
                    Physics2D.IgnoreCollision(spawnedCollider, cloudCollider, true);
            }
        }
    }



    void FixedUpdate()

    {

        if (currentType == CloudType.Platform)

            UpdatePlatformHover();

    }



    void ResolveSpawnPrefabs()

    {

        if (CloudSpawnDefaults.Instance != null)

        {

            if (moodDropEnemyPrefab == null)

                moodDropEnemyPrefab = CloudSpawnDefaults.Instance.MoodDropEnemyPrefab;

            if (raindropProjectilePrefab == null)

                raindropProjectilePrefab = CloudSpawnDefaults.Instance.RaindropProjectilePrefab;

            if (lightningBoltPrefab == null)

                lightningBoltPrefab = CloudSpawnDefaults.Instance.LightningBoltPrefab;

        }



        Clouds[] allClouds = FindObjectsByType<Clouds>(FindObjectsSortMode.None);

        foreach (Clouds other in allClouds)

        {

            if (other == null)

                continue;

            if (moodDropEnemyPrefab == null && other.moodDropEnemyPrefab != null)

                moodDropEnemyPrefab = other.moodDropEnemyPrefab;

            if (raindropProjectilePrefab == null && other.raindropProjectilePrefab != null)

                raindropProjectilePrefab = other.raindropProjectilePrefab;

            if (lightningBoltPrefab == null && other.lightningBoltPrefab != null)

                lightningBoltPrefab = other.lightningBoltPrefab;

        }



        if (currentType == CloudType.MoodDrop && moodDropEnemyPrefab == null)

            Debug.LogWarning($"{name}: MoodDrop cloud has no moodDropEnemyPrefab assigned.", this);

        if (currentType == CloudType.Raindrop && raindropProjectilePrefab == null)

            Debug.LogWarning($"{name}: Raindrop cloud has no raindropProjectilePrefab assigned.", this);

        if (currentType == CloudType.Lightning && lightningBoltPrefab == null)

            Debug.LogWarning($"{name}: Lightning cloud has no lightningBoltPrefab assigned.", this);

    }



    void Update()

    {

        switch (currentType)

        {

            case CloudType.Lightning:

                UpdateLightningHover();

                break;

            case CloudType.MoodDrop:

                UpdateMoodDropBehavior();

                break;

            case CloudType.Happy:

            case CloudType.MediumHappy:

            case CloudType.SmallHappy:

                UpdateHappyCloudHover();

                break;

        }



        UpdateRaindropConversionTimer();

    }



    void OnDestroy()

    {

        if (activeHappyCloud == this)

            activeHappyCloud = null;



        StopAllCoroutines();

        lightningBehaviorCoroutine = null;

        DestroyAttachedLightning();

        ClearMoodDropsFromThisCloud();

        ClearAppearanceInstance();

    }



#if UNITY_EDITOR

    void OnValidate()

    {

        SanitizeAppearancePrefabReferences();

        FillDefaultAppearanceSpritesIfEmpty();

    }



    void FillDefaultAppearanceSpritesIfEmpty()

    {

        const string page2 = "Assets/Art/Smiley The Jester assests page 2.png";

        const string page3 = "Assets/Art/Smiley The Jester assests page 3.png";



        if (platformCloudSprite == null)

            platformCloudSprite = LoadSpriteFromSheet(page2, "Smiley The Jester assests page 2_7");

        if (moodDropCloudSprite == null)

            moodDropCloudSprite = LoadSpriteFromSheet(page2, "Smiley The Jester assests page 2_7");

        if (raindropCloudSprite == null)

            raindropCloudSprite = LoadSpriteFromSheet(page2, "Smiley The Jester assests page 2_7");

        if (lightningCloudSprite == null)

            lightningCloudSprite = LoadSpriteFromSheet(page2, "Smiley The Jester assests page 2_8");

        if (happyCloudSprite == null)

            happyCloudSprite = LoadSpriteFromSheet(page3, "Smiley The Jester assests page 3_4");

        if (mediumHappyCloudSprite == null)

            mediumHappyCloudSprite = LoadSpriteFromSheet(page3, "Smiley The Jester assests page 3_3");

        if (smallHappyCloudSprite == null)

            smallHappyCloudSprite = LoadSpriteFromSheet(page3, "Smiley The Jester assests page 3_2");

    }



    static Sprite LoadSpriteFromSheet(string texturePath, string spriteName)

    {

        Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(texturePath);

        foreach (Object asset in assets)

        {

            if (asset is Sprite sprite && sprite.name == spriteName)

                return sprite;

        }



        return null;

    }

#endif



    void SanitizeAppearancePrefabReferences()

    {

        platformCloudAppearance = SanitizeAppearanceReference(platformCloudAppearance);

        moodDropCloudAppearance = SanitizeAppearanceReference(moodDropCloudAppearance);

        raindropCloudAppearance = SanitizeAppearanceReference(raindropCloudAppearance);

        lightningCloudAppearance = SanitizeAppearanceReference(lightningCloudAppearance);

        happyCloudAppearance = SanitizeAppearanceReference(happyCloudAppearance);

        mediumHappyCloudAppearance = SanitizeAppearanceReference(mediumHappyCloudAppearance);

        smallHappyCloudAppearance = SanitizeAppearanceReference(smallHappyCloudAppearance);

    }



    GameObject SanitizeAppearanceReference(GameObject reference)

    {

        return IsValidAppearancePrefab(reference, gameObject) ? reference : null;

    }



    // =========================================================================

    // CLOUD APPEARANCE — swap visual prefab / sprite when type changes

    // =========================================================================



    /// <param name="allowOverwriteExistingSprite">
    /// When false, keeps the SpriteRenderer's current sprite (used for Platform at level start).
    /// When true, assigns the sprite for the cloud type (used after Happy Star or rain conversion).
    /// </param>
    void ApplyCloudAppearance(CloudType type, bool allowOverwriteExistingSprite = true)

    {

        GameObject appearancePrefab = GetAppearancePrefab(type);

        Sprite appearanceSprite = GetAppearanceSprite(type);



        if (!IsValidAppearancePrefab(appearancePrefab))

        {

            if (appearancePrefab != null)

                GameDiagnostics.LogCloud(

                    $"{name}: ignored invalid appearance prefab (self-reference) for {type}",

                    this);

            appearancePrefab = null;

        }



        EnsureHostSpriteRenderer();



        if (appearancePrefab != null)

        {

            if (hostSpriteRenderer != null)

                hostSpriteRenderer.enabled = false;



            ClearAppearanceInstance();

            Transform root = cloudAppearanceRoot != null ? cloudAppearanceRoot : transform;

            activeAppearanceInstance = Instantiate(appearancePrefab, root);

            activeAppearanceInstance.transform.localPosition = Vector3.zero;

            activeAppearanceInstance.transform.localRotation = Quaternion.identity;

            activeAppearanceInstance.transform.localScale = Vector3.one;

            DisableNestedCloudBehaviours(activeAppearanceInstance);

            DisableAppearanceColliders();

            SyncHostColliderFromAppearanceInstance();

            GameDiagnostics.LogCloud($"{name}: instantiated appearance prefab for {type}", this);

            return;

        }



        ClearAppearanceInstance();



        if (hostSpriteRenderer != null)

        {

            hostSpriteRenderer.enabled = true;

            Sprite currentSprite = hostSpriteRenderer.sprite;

            if (appearanceSprite != null)

            {

                if (allowOverwriteExistingSprite || currentSprite == null)

                {

                    if (currentSprite != appearanceSprite)

                    {

                        GameDiagnostics.LogCloud(

                            $"{name}: sprite '{currentSprite?.name}' → '{appearanceSprite.name}' ({type})",

                            this);

                        ApplyHostSpritePreservingWorldBottom(appearanceSprite);

                    }

                }

                else

                {

                    GameDiagnostics.LogCloud(

                        $"{name}: kept prefab sprite '{currentSprite?.name}' (Platform start, type={type})",

                        this);

                }

            }

            else if (currentSprite == null)

            {

                GameDiagnostics.LogCloud(

                    $"{name}: WARNING — no sprite assigned for {type} on SpriteRenderer",

                    this);

            }

        }

    }



    void ApplyHostSpritePreservingWorldBottom(Sprite newSprite)

    {

        if (hostSpriteRenderer == null || newSprite == null)

            return;



        float worldBottomBefore = hostSpriteRenderer.sprite != null

            ? hostSpriteRenderer.bounds.min.y

            : transform.position.y;



        hostSpriteRenderer.sprite = newSprite;

        float worldBottomAfter = hostSpriteRenderer.bounds.min.y;

        float lift = worldBottomBefore - worldBottomAfter;

        if (Mathf.Abs(lift) > 0.001f)

            transform.position += new Vector3(0f, lift, 0f);

    }



    static bool IsValidAppearancePrefab(GameObject prefab, GameObject host)

    {

        if (prefab == null)

            return false;

        if (prefab == host)

            return false;

        // Scene clouds were mistakenly assigned here; instantiating them nests whole Clouds objects.

        // Scene cloud references nest a second Clouds object; project prefabs are allowed.
        if (prefab.scene.IsValid() && prefab.GetComponent<Clouds>() != null)

            return false;

        return true;

    }



    bool IsValidAppearancePrefab(GameObject prefab) => IsValidAppearancePrefab(prefab, gameObject);



    void EnsureHostSpriteRenderer()

    {

        if (hostSpriteRenderer == null)

            hostSpriteRenderer = GetComponent<SpriteRenderer>();

    }



    void DisableNestedCloudBehaviours(GameObject appearanceRoot)

    {

        foreach (Clouds nested in appearanceRoot.GetComponentsInChildren<Clouds>(true))

        {

            if (nested != this)

                nested.enabled = false;

        }

    }



    GameObject GetAppearancePrefab(CloudType type)

    {

        return type switch

        {

            CloudType.Platform => platformCloudAppearance,

            CloudType.MoodDrop => moodDropCloudAppearance,

            CloudType.Raindrop => raindropCloudAppearance,

            CloudType.Lightning => lightningCloudAppearance,

            CloudType.Happy => happyCloudAppearance,

            CloudType.MediumHappy => mediumHappyCloudAppearance,

            CloudType.SmallHappy => smallHappyCloudAppearance,

            _ => null

        };

    }



    Sprite GetAppearanceSprite(CloudType type)

    {

        return type switch

        {

            CloudType.Platform => platformCloudSprite,

            CloudType.MoodDrop => moodDropCloudSprite,

            CloudType.Raindrop => raindropCloudSprite,

            CloudType.Lightning => lightningCloudSprite,

            CloudType.Happy => happyCloudSprite,

            CloudType.MediumHappy => mediumHappyCloudSprite,

            CloudType.SmallHappy => smallHappyCloudSprite,

            _ => null

        };

    }



    void ClearAppearanceInstance()

    {

        if (activeAppearanceInstance == null)

            return;



        Destroy(activeAppearanceInstance);

        activeAppearanceInstance = null;

    }



    void SetCloudType(CloudType newType)

    {

        CloudType previousType = currentType;

        currentType = newType;

        if (IsHappyVariant(newType))
        {
            ApplyHappyCloudPrefab(newType);
        }
        else
        {
            ClearAppearanceInstance();
            EnsureHostSpriteRenderer();
            if (hostSpriteRenderer != null)
                hostSpriteRenderer.enabled = true;
            ApplyCloudAppearance(currentType, allowOverwriteExistingSprite: true);
        }

        GameDiagnostics.LogCloud(

            $"{name}: type {previousType} → {newType}",

            this);

    }



    static bool IsHappyVariant(CloudType type)

    {

        return type == CloudType.Happy || type == CloudType.MediumHappy || type == CloudType.SmallHappy;

    }



    // =========================================================================

    // TYPE 1 — PLATFORM CLOUD

    // =========================================================================



    void UpdatePlatformHover()

    {

        float offsetY = Mathf.Sin(Time.time * platformHoverSpeed) * hoverAmplitude;

        transform.position = restPosition + Vector3.up * offsetY;

        SyncRigidbodyToTransform();

    }

    void ApplyHappyCloudPrefab(CloudType happyType)

    {

        GameObject prefab = GetAppearancePrefab(happyType);

        EnsureHostSpriteRenderer();



        if (!IsValidAppearancePrefab(prefab))

        {

            ClearAppearanceInstance();

            if (hostSpriteRenderer != null)

                hostSpriteRenderer.enabled = true;

            ApplyCloudAppearance(happyType, allowOverwriteExistingSprite: true);

            return;

        }



        if (hostSpriteRenderer != null)

            hostSpriteRenderer.enabled = false;



        ClearAppearanceInstance();

        Transform root = cloudAppearanceRoot != null ? cloudAppearanceRoot : transform;

        activeAppearanceInstance = Instantiate(prefab, root);

        activeAppearanceInstance.transform.localPosition = Vector3.zero;

        activeAppearanceInstance.transform.localRotation = Quaternion.identity;

        activeAppearanceInstance.transform.localScale = Vector3.one;

        DisableNestedCloudBehaviours(activeAppearanceInstance);

        DisableAppearanceColliders();

        SyncHostColliderFromAppearanceInstance();

    }



    void DisableAppearanceColliders()

    {

        if (activeAppearanceInstance == null)

            return;



        foreach (Collider2D childCollider in activeAppearanceInstance.GetComponentsInChildren<Collider2D>(true))

        {

            if (childCollider != null)

                childCollider.enabled = false;

        }

    }



    void SyncHostColliderFromAppearanceInstance()

    {

        if (activeAppearanceInstance == null || cloudCollider == null)

            return;



        BoxCollider2D source = activeAppearanceInstance.GetComponent<BoxCollider2D>();

        if (source == null || cloudCollider is not BoxCollider2D hostBox)

            return;



        hostBox.size = source.size;

        hostBox.offset = source.offset;

    }

    void ResolveLocalFirePoints()

    {

        moodDropFirePoints = ResolveNamedChildPoints(moodDropFirePoints, "FirePoint", 3);

        rainFirePoints = ResolveNamedChildPoints(rainFirePoints, "FirePoint", 2);



        if (lightningFirePoint != null && !lightningFirePoint.IsChildOf(transform))

        {

            Transform localBoltPoint = transform.Find("FirePoint1");

            if (localBoltPoint != null)

                lightningFirePoint = localBoltPoint;

        }

    }



    Transform[] ResolveNamedChildPoints(Transform[] serialized, string prefix, int count)

    {

        Transform[] resolved = new Transform[count];

        for (int i = 0; i < count; i++)

        {

            string pointName = prefix + (i + 1);

            Transform localChild = transform.Find(pointName);

            if (localChild != null)

            {

                resolved[i] = localChild;

                continue;

            }



            if (serialized != null && i < serialized.Length && serialized[i] != null && serialized[i].IsChildOf(transform))

                resolved[i] = serialized[i];

            else if (serialized != null && i < serialized.Length && serialized[i] != null)

            {

                GameDiagnostics.LogCloud(

                    $"{name}: ignored external spawn point '{serialized[i].name}' — expected child '{pointName}'",

                    this);

            }

        }



        return resolved;

    }



    void SyncRigidbodyToTransform()

    {

        if (!TryGetComponent(out Rigidbody2D body))

            return;



        body.position = transform.position;

        body.linearVelocity = Vector2.zero;

        body.angularVelocity = 0f;

    }



    // =========================================================================

    // TYPE 2 — MOODDROP CLOUD (spawns enemies from moodDropEnemyPrefab)

    // =========================================================================



    void BeginMoodDropBehavior()

    {

        if (moodDropEnemyPrefab == null)

            return;



        PruneActiveMoodDropList();

        if (activeMoodDropsFromThisCloud.Count > 0)

            return;



        TryStartMoodDropWave();

    }



    void UpdateMoodDropBehavior()

    {

        if (moodDropEnemyPrefab == null)

            return;



        PruneActiveMoodDropList();

        if (activeMoodDropsFromThisCloud.Count > 0)

            return;



        TryStartMoodDropWave();

    }



    void PruneActiveMoodDropList()

    {

        for (int i = activeMoodDropsFromThisCloud.Count - 1; i >= 0; i--)

        {

            if (activeMoodDropsFromThisCloud[i] == null)

                activeMoodDropsFromThisCloud.RemoveAt(i);

        }

    }



    void TryStartMoodDropWave()

    {

        if (moodDropEnemyPrefab == null || moodDropFirePoints == null || moodDropFirePoints.Length == 0)

            return;



        if (MoodDrop.GlobalActiveCount >= GlobalMoodDropCap)

        {

            GameDiagnostics.LogMoodDrop(

                $"{name}: global cap reached ({MoodDrop.GlobalActiveCount}/{GlobalMoodDropCap}), waiting for defeats",

                this);

            return;

        }



        int spawnBudget = Mathf.Min(MoodDropsPerWave, GlobalMoodDropCap - MoodDrop.GlobalActiveCount);

        if (spawnBudget <= 0)

            return;



        GameDiagnostics.LogMoodDrop(

            $"{name}: spawning wave (budget={spawnBudget}, global={MoodDrop.GlobalActiveCount}/{GlobalMoodDropCap})",

            this);



        int spawned = 0;

        for (int i = 0; i < MoodDropsPerWave && spawned < spawnBudget; i++)

        {

            if (i >= moodDropFirePoints.Length)

                break;



            Transform firePoint = moodDropFirePoints[i];

            if (firePoint == null)

                continue;



            Vector3 spawnPosition = firePoint.position + Vector3.up * 0.35f;
            GameObject instance = Instantiate(moodDropEnemyPrefab, spawnPosition, firePoint.rotation);
            IgnoreSpawnCollisionsWithCloud(instance);

            MoodDrop enemy = instance.GetComponentInChildren<MoodDrop>();

            if (enemy == null)

            {

                GameDiagnostics.LogMoodDrop(

                    $"{name}: destroyed spawn — prefab has no MoodDrop component on root or children",

                    this);

                Destroy(instance);

                continue;

            }



            enemy.Initialize(this);

            activeMoodDropsFromThisCloud.Add(enemy);

            spawned++;

        }

    }



    public void NotifyMoodDropDestroyed(MoodDrop enemy)

    {

        activeMoodDropsFromThisCloud.Remove(enemy);

    }



    // =========================================================================

    // TYPE 3 — RAINDROP CLOUD (spawns raindropProjectilePrefab)

    // =========================================================================



    void BeginRaindropBehavior()

    {

        if (raindropProjectilePrefab == null || rainFirePoints == null || rainFirePoints.Length < 2)

            return;



        StartCoroutine(RaindropSpawnLoop());

    }



    IEnumerator RaindropSpawnLoop()

    {

        var waitInterval = new WaitForSeconds(RainSpawnInterval);

        var waitBetweenPoints = new WaitForSeconds(RainFirePointDelay);



        while (currentType == CloudType.Raindrop)

        {

            if (rainFirePoints[0] != null)

                SpawnRaindropAt(rainFirePoints[0]);



            yield return waitBetweenPoints;



            if (rainFirePoints[1] != null)

                SpawnRaindropAt(rainFirePoints[1]);



            yield return waitInterval;

        }

    }



    void SpawnRaindropAt(Transform firePoint)

    {

        Vector3 spawnPosition = firePoint.position + Vector3.down * 0.75f;

        GameObject instance = Instantiate(raindropProjectilePrefab, spawnPosition, firePoint.rotation);

        if (instance.TryGetComponent(out Projectiles projectile))
            projectile.InitializeRaindropFromCloud(this);
        else
            Debug.LogWarning($"{name}: Rain projectile prefab needs a Projectiles component set to Raindrop.", this);

    }



    /// <summary>

    /// Happy Star hit: MoodDrop → Happy, Raindrop → MediumHappy, Lightning → SmallHappy.

    /// </summary>

    public void RegisterHappyStarHit()

    {

        if (IsHappyVariant(currentType) && activeHappyCloud == this)

            return;



        if (activeHappyCloud != null && activeHappyCloud != this)

            activeHappyCloud.RevertFromHappy();



        typeBeforeHappy = currentType;

        activeHappyCloud = this;

        StopBehaviorForHappyTransition();



        CloudType happyTarget = GetHappyTargetForCurrentType();

        SetCloudType(happyTarget);

    }



    CloudType GetHappyTargetForCurrentType()

    {

        return currentType switch

        {

            CloudType.Lightning => CloudType.SmallHappy,

            CloudType.Raindrop => CloudType.MediumHappy,

            CloudType.MoodDrop => CloudType.Happy,

            _ => CloudType.Happy

        };

    }



    void RevertFromHappy()

    {

        if (!IsHappyVariant(currentType))

            return;



        SetCloudType(typeBeforeHappy);

        if (activeHappyCloud == this)

            activeHappyCloud = null;



        BeginBehaviorForCurrentType();

    }



    void StopBehaviorForHappyTransition()

    {

        StopAllCoroutines();

        lightningBehaviorCoroutine = null;

        DestroyAttachedLightning();

        ClearMoodDropsFromThisCloud();

    }



    // =========================================================================

    // HAPPY / MEDIUM HAPPY / SMALL HAPPY

    // =========================================================================



    void UpdateHappyCloudHover()

    {

        float offsetY = Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude;

        transform.position = restPosition + Vector3.up * offsetY;

    }



    public void RegisterRainHit()

    {

        if (IsHappyVariant(currentType))

            return;



        if (currentType == CloudType.Raindrop)

        {

            rainHitsReceived++;

            if (rainHitsReceived >= RainHitsToBecomeMoodDrop)

                ConvertToMoodDropType();

            return;

        }



        if (currentType == CloudType.MoodDrop && convertedFromRaindrop)

            lastRainHitTime = Time.time;

    }



    void UpdateRaindropConversionTimer()

    {

        if (!convertedFromRaindrop || currentType != CloudType.MoodDrop)

            return;



        if (Time.time - lastRainHitTime >= MoodDropRevertSeconds)

            ConvertToRaindropType();

    }



    void ConvertToMoodDropType()

    {

        StopAllCoroutines();

        convertedFromRaindrop = true;

        lastRainHitTime = Time.time;

        rainHitsReceived = 0;

        SetCloudType(CloudType.MoodDrop);

        BeginMoodDropBehavior();

    }



    void ConvertToRaindropType()

    {

        StopAllCoroutines();

        ClearMoodDropsFromThisCloud();

        convertedFromRaindrop = false;

        rainHitsReceived = 0;

        SetCloudType(CloudType.Raindrop);

        BeginRaindropBehavior();

    }



    void ClearMoodDropsFromThisCloud()

    {

        for (int i = activeMoodDropsFromThisCloud.Count - 1; i >= 0; i--)

        {

            if (activeMoodDropsFromThisCloud[i] != null)

                Destroy(activeMoodDropsFromThisCloud[i].gameObject);

        }



        activeMoodDropsFromThisCloud.Clear();

    }



    // =========================================================================

    // TYPE 4 — LIGHTNING CLOUD (spawns lightningBoltPrefab)

    // =========================================================================



    void BeginLightningBehavior()

    {

        if (lightningBoltPrefab == null || lightningFirePoint == null)

            return;



        StopLightningBehavior();

        lightningBehaviorCoroutine = StartCoroutine(LightningBehaviorLoop());

    }



    IEnumerator LightningBehaviorLoop()

    {

        var activeWait = new WaitForSeconds(LightningActiveDuration);

        var cooldownWait = new WaitForSeconds(LightningCooldownDuration);



        while (currentType == CloudType.Lightning)

        {

            SpawnAttachedLightning();

            yield return activeWait;

            DestroyAttachedLightning();

            yield return cooldownWait;

        }

    }



    void SpawnAttachedLightning()

    {

        if (lightningBoltPrefab == null || lightningFirePoint == null)

            return;



        DestroyAttachedLightning();

        attachedLightning = Instantiate(lightningBoltPrefab, lightningFirePoint.position, lightningFirePoint.rotation);

        if (attachedLightning.TryGetComponent(out Projectiles lightningProjectile))

            lightningProjectile.BindToLightningSpawner(lightningFirePoint);

        else

            attachedLightning.transform.SetParent(lightningFirePoint, false);

    }



    void StopLightningBehavior()

    {

        if (lightningBehaviorCoroutine != null)

        {

            StopCoroutine(lightningBehaviorCoroutine);

            lightningBehaviorCoroutine = null;

        }



        DestroyAttachedLightning();

    }



    void UpdateLightningHover()

    {

        float offsetY = Mathf.Sin(Time.time * lightningHoverSpeed) * lightningHoverAmplitude;

        transform.position = restPosition + Vector3.up * offsetY;

        SyncRigidbodyToTransform();

    }



    void DestroyAttachedLightning()

    {

        if (attachedLightning != null)

        {

            Destroy(attachedLightning);

            attachedLightning = null;

        }

    }



    void BeginBehaviorForCurrentType()

    {

        switch (currentType)

        {

            case CloudType.Platform:

                break;

            case CloudType.MoodDrop:

                BeginMoodDropBehavior();

                break;

            case CloudType.Raindrop:

                BeginRaindropBehavior();

                break;

            case CloudType.Lightning:

                BeginLightningBehavior();

                break;

            case CloudType.Happy:

            case CloudType.MediumHappy:

            case CloudType.SmallHappy:

                if (activeHappyCloud == null)

                    activeHappyCloud = this;

                break;

        }

    }

}


