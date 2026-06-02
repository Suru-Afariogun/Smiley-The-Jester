using System.Collections;
using UnityEngine;

public enum ProjectileType
{
    LightningBolt,
    HappyStar,
    Raindrop
}

public class Projectiles : MonoBehaviour
{
    const float RainFallDelay = 0.2f;
    const float LightningFlickerInterval = 0.05f;
    const float LightningPlayerStunCooldown = 0.55f;
    const float ProjectileLifespan = 6f;
    const int RaindropSortingOrder = 12;
    const int HappyStarSortingOrder = 15;
    const float HappyStarSpawnGrace = 0.1f;

    [Header("Projectile type")]
    [SerializeField] ProjectileType projectileType = ProjectileType.Raindrop;

    public ProjectileType Type => projectileType;

    [Header("Happy Star")]
    [SerializeField] float happyStarSpeed = 12f;
    [SerializeField] float happyStarSpinSpeed = 720f;

    [Header("Raindrop")]
    [SerializeField] float rainFallSpeed = 5f;

    [Header("Lightning Bolt")]
    [SerializeField] float lightningFlickerInterval = LightningFlickerInterval;

    Vector2 travelDirection = Vector2.right;
    float happyStarSpawnTime = -1f;
    bool rainIsFalling;
    Clouds rainSpawnOwner;
    Transform lightningSpawner;
    float lightningFlickerTimer;
    float nextLightningPlayerStunTime;
    SpriteRenderer lightningSprite;
    Vector3 lightningDefaultScale;

    void Awake()
    {
        if (projectileType == ProjectileType.LightningBolt)
            ConfigureLightningBolt();
        else
            ConfigureMovingProjectileBody();
    }

    void ConfigureMovingProjectileBody()
    {
        if (TryGetComponent(out Rigidbody2D body))
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        foreach (Collider2D collider in GetComponents<Collider2D>())
            collider.isTrigger = true;

        if (projectileType == ProjectileType.Raindrop && TryGetComponent(out SpriteRenderer rainSprite))
            rainSprite.sortingOrder = RaindropSortingOrder;

        if (projectileType == ProjectileType.HappyStar)
            ConfigureHappyStarVisual();
    }

    public void InitializeRaindropFromCloud(Clouds owner)
    {
        projectileType = ProjectileType.Raindrop;
        rainSpawnOwner = owner;
        ConfigureMovingProjectileBody();

        if (owner == null)
            return;

        Collider2D[] rainColliders = GetComponents<Collider2D>();
        foreach (Collider2D cloudCollider in owner.GetComponentsInChildren<Collider2D>(true))
        {
            if (cloudCollider == null)
                continue;

            foreach (Collider2D rainCollider in rainColliders)
            {
                if (rainCollider != null)
                    Physics2D.IgnoreCollision(rainCollider, cloudCollider, true);
            }
        }
    }

    public void BindToLightningSpawner(Transform spawner)
    {
        lightningSpawner = spawner;
        transform.SetParent(spawner, false);
        transform.localPosition = Vector3.zero;
        ConfigureLightningBolt();
    }

    void ConfigureLightningBolt()
    {
        lightningSprite = GetComponent<SpriteRenderer>();
        if (lightningSprite != null)
            lightningSprite.flipX = false;

        lightningDefaultScale = transform.localScale;
        if (Mathf.Abs(lightningDefaultScale.x) < 0.001f)
            lightningDefaultScale.x = 1f;

        transform.rotation = Quaternion.identity;

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            Destroy(colliders[i]);

        BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        if (lightningSprite != null && lightningSprite.sprite != null)
        {
            Vector2 size = lightningSprite.sprite.bounds.size;
            box.size = new Vector2(Mathf.Max(size.x, 0.5f), Mathf.Max(size.y, 0.5f));
        }
        else
        {
            box.size = new Vector2(1.2f, 3f);
        }

        if (TryGetComponent(out Rigidbody2D body))
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    void UpdateLightningBolt()
    {
        if (lightningSpawner != null)
            transform.position = lightningSpawner.position;

        transform.rotation = Quaternion.identity;

        lightningFlickerTimer -= Time.deltaTime;
        if (lightningFlickerTimer > 0f)
            return;

        lightningFlickerTimer = lightningFlickerInterval;

        if (lightningSprite != null)
        {
            lightningSprite.flipX = !lightningSprite.flipX;
            return;
        }

        float sign = transform.localScale.x >= 0f ? -1f : 1f;
        transform.localScale = new Vector3(
            Mathf.Abs(lightningDefaultScale.x) * sign,
            lightningDefaultScale.y,
            lightningDefaultScale.z);
    }

    public void LaunchHappyStar(Vector2 direction)
    {
        projectileType = ProjectileType.HappyStar;
        travelDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        happyStarSpawnTime = Time.time;

        float angle = Mathf.Atan2(travelDirection.y, travelDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        transform.localScale = Vector3.one;

        ConfigureHappyStarVisual();
        IgnoreHappyStarCollisionWithPlayer();
        VillagerPhasing.RegisterHappyStar(gameObject);
    }

    void ConfigureHappyStarVisual()
    {
        if (!TryGetComponent(out SpriteRenderer spriteRenderer))
            return;

        spriteRenderer.enabled = true;
        spriteRenderer.sortingOrder = HappyStarSortingOrder;
    }

    void IgnoreHappyStarCollisionWithPlayer()
    {
        PlayerControls player = FindFirstObjectByType<PlayerControls>();
        if (player == null || !player.TryGetComponent(out Collider2D playerCollider))
            return;

        foreach (Collider2D starCollider in GetComponents<Collider2D>())
            Physics2D.IgnoreCollision(starCollider, playerCollider, true);
    }

    void UpdateHappyStar()
    {
        transform.position += (Vector3)(travelDirection * happyStarSpeed * Time.deltaTime);
        transform.Rotate(0f, 0f, happyStarSpinSpeed * Time.deltaTime);
    }

    void StartRaindrop()
    {
        StartCoroutine(BeginRainFallAfterDelay());
    }

    IEnumerator BeginRainFallAfterDelay()
    {
        yield return new WaitForSeconds(RainFallDelay);
        rainIsFalling = true;
    }

    void UpdateRaindrop()
    {
        if (!rainIsFalling)
            return;

        transform.position += Vector3.down * rainFallSpeed * Time.deltaTime;
    }

    void Start()
    {
        if (projectileType == ProjectileType.Raindrop)
            StartRaindrop();

        if (projectileType != ProjectileType.LightningBolt)
            Destroy(gameObject, ProjectileLifespan);
    }

    void Update()
    {
        switch (projectileType)
        {
            case ProjectileType.LightningBolt:
                UpdateLightningBolt();
                break;
            case ProjectileType.HappyStar:
                UpdateHappyStar();
                break;
            case ProjectileType.Raindrop:
                UpdateRaindrop();
                break;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        DispatchHit(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (projectileType == ProjectileType.LightningBolt)
            DispatchHit(other);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        DispatchHit(collision.collider);
    }

    void DispatchHit(Collider2D other)
    {
        if (other == null)
            return;

        if (MoodDropScanArea.IsScanCollider(other))
            return;

        switch (projectileType)
        {
            case ProjectileType.LightningBolt:
                HandleLightningCollision(other);
                break;
            case ProjectileType.HappyStar:
                HandleHappyStarCollision(other);
                break;
            case ProjectileType.Raindrop:
                HandleRaindropCollision(other);
                break;
        }
    }

    void HandleLightningCollision(Collider2D other)
    {
        if (other.GetComponentInParent<CarryCloud>() is CarryCloud carryCloud)
        {
            carryCloud.ApplyLightningHit();
            return;
        }

        if (other.GetComponentInParent<Villagers>() is Villagers villager)
        {
            villager.ApplyMoodDropOrLightningHit();
            return;
        }

        if (!TryGetPlayerFromCollider(other, out PlayerController player))
            return;

        if (Time.time < nextLightningPlayerStunTime)
            return;

        nextLightningPlayerStunTime = Time.time + LightningPlayerStunCooldown;
        player.ApplyLightningHit();
    }

    void HandleHappyStarCollision(Collider2D other)
    {
        if (Time.time < happyStarSpawnTime + HappyStarSpawnGrace)
            return;

        Clouds cloud = other.GetComponent<Clouds>() ?? other.GetComponentInParent<Clouds>();
        if (cloud != null)
        {
            GameDiagnostics.LogCombat(
                $"Happy Star hit cloud '{cloud.name}' (type={cloud.CurrentType})",
                this);

            cloud.RegisterHappyStarHit();

            Destroy(gameObject);
            return;
        }

        MoodDrop moodDrop = other.GetComponent<MoodDrop>() ?? other.GetComponentInParent<MoodDrop>();
        if (moodDrop != null && moodDrop.GetComponent<Collider2D>() == other)
        {
            moodDrop.KillFromHappyStar();
            Destroy(gameObject);
        }
    }

    void HandleRaindropCollision(Collider2D other)
    {
        Clouds cloud = other.GetComponent<Clouds>() ?? other.GetComponentInParent<Clouds>();
        if (cloud != null)
        {
            if (cloud == rainSpawnOwner)
                return;

            cloud.RegisterRainHit();
            Destroy(gameObject);
            return;
        }

        if (other.GetComponentInParent<CarryCloud>() is CarryCloud carryCloud)
        {
            carryCloud.ApplyRainOrMoodDropHit();
            Destroy(gameObject);
            return;
        }

        if (TryGetPlayerFromCollider(other, out PlayerController player))
        {
            player.RegisterRainHit();
            Destroy(gameObject);
            return;
        }

        if (other.TryGetComponent(out Projectiles projectile) && projectile.Type == ProjectileType.HappyStar)
        {
            Destroy(projectile.gameObject);
            Destroy(gameObject);
        }
    }

    static bool TryGetPlayerFromCollider(Collider2D other, out PlayerController player)
    {
        player = other.GetComponent<PlayerController>();
        if (player != null)
            return true;

        player = other.GetComponentInParent<PlayerController>();
        return player != null;
    }
}
