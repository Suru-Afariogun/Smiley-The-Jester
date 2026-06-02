using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Villager physics: fall with gravity, parent to platform clouds, fall through MoodDrop/Lightning clouds.
/// Picked up by CarryCloud; registers with VillagerRegistry for win checks.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Villagers : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField] float gravityScale = 2f;
    [SerializeField] float subtleBounceVelocity = 2.5f;
    [SerializeField] float dropPushDownVelocity = 1.5f;

    [Header("Ground check")]
    [SerializeField] Transform groundCheck;

    [Header("Happy Kingdom")]
    [SerializeField] bool isTouchingHappyKingdom;

    Collider2D bodyCollider;
    Rigidbody2D body;
    CarryCloud carryingCloud;
    Clouds cloudStandingOn;
    HappyKingdom kingdomStandingOn;
    Clouds ignoredFallThroughCloud;
    bool isCarried;
    bool isFallingThrough;
    public bool IsCarried => isCarried;
    public bool IsTouchingHappyKingdom => isTouchingHappyKingdom;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        body.gravityScale = gravityScale;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;

        EnsureGroundCheck();
        VillagerRegistry.Register(this);
        VillagerPhasing.RegisterVillager(this);
        NotifyCarryCloudOfNewVillager();
    }

    void NotifyCarryCloudOfNewVillager()
    {
        CarryCloud carryCloud = FindFirstObjectByType<CarryCloud>();
        carryCloud?.RegisterVillagerForPhasing(this);
    }

    void OnDestroy()
    {
        VillagerRegistry.Unregister(this);
        RestoreIgnoredCloudCollision();
    }

    void FixedUpdate()
    {
        if (isCarried)
            return;

        UpdateCloudParenting();
    }

    void UpdateCloudParenting()
    {
        if (isFallingThrough)
            return;

        if (kingdomStandingOn != null)
        {
            if (transform.parent != kingdomStandingOn.transform)
                transform.SetParent(kingdomStandingOn.transform, true);
            SnapFeetToKingdomDeck(kingdomStandingOn);
            return;
        }

        if (cloudStandingOn == null)
            return;

        if (transform.parent != cloudStandingOn.transform)
            transform.SetParent(cloudStandingOn.transform, true);

        if (cloudStandingOn.IsPlatformCloud)
            SnapFeetToCloudDeck(cloudStandingOn.CloudCollider);
    }

    void SnapFeetToKingdomDeck(HappyKingdom kingdom)
    {
        if (kingdom == null)
            return;

        foreach (Collider2D collider in kingdom.GetComponents<BoxCollider2D>())
        {
            if (collider != null && !collider.isTrigger)
            {
                SnapFeetToCloudDeck(collider);
                return;
            }
        }
    }

    public void AttachToContainer(Transform container, CarryCloud cloud)
    {
        carryingCloud = cloud;
        isCarried = true;
        isFallingThrough = false;
        cloudStandingOn = null;
        kingdomStandingOn = null;
        RestoreIgnoredCloudCollision();

        Vector3 localPosition = cloud != null
            ? cloud.GetVillagerCarryLocalPosition(this)
            : GetFallbackContainerLocalPosition(container);

        transform.SetParent(container, false);
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.identity;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;

        if (bodyCollider != null)
            bodyCollider.enabled = false;

        EnsureGroundCheck();
        groundCheck.localPosition = new Vector3(0f, GetFeetLocalOffset() + 0.08f, 0f);
    }

    public float GetFeetOffsetAbovePivot()
    {
        return GetFeetBelowPivotWorld();
    }

    Vector3 GetFallbackContainerLocalPosition(Transform container)
    {
        float feetBelowPivot = GetFeetBelowPivotWorld();
        float floorY = GetContainerFloorLocalY(container);
        return new Vector3(0f, floorY + feetBelowPivot, 0f);
    }

    public void ReleaseToWorld(Vector3 worldPosition, bool droppedFromCloud = false)
    {
        isCarried = false;
        carryingCloud = null;

        transform.SetParent(null);
        transform.position = worldPosition;
        RestoreGroundCheckToFeet();

        if (bodyCollider != null)
            bodyCollider.enabled = true;

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = gravityScale;

        if (droppedFromCloud)
            body.linearVelocity = new Vector2(0f, -dropPushDownVelocity);
    }

    public void ApplyMoodDropOrLightningHit()
    {
        if (isCarried)
            return;

        if (cloudStandingOn != null && cloudStandingOn.IsPlatformCloud)
            return;

        BeginFallThroughCurrentCloud();
    }

    void BeginFallThroughCurrentCloud()
    {
        if (cloudStandingOn == null)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            return;
        }

        if (cloudStandingOn.IsPlatformCloud)
            return;

        ignoredFallThroughCloud = cloudStandingOn;
        Collider2D cloudCollider = cloudStandingOn.CloudCollider;
        if (cloudCollider != null && bodyCollider != null)
            Physics2D.IgnoreCollision(bodyCollider, cloudCollider, true);

        transform.SetParent(null);
        cloudStandingOn = null;
        kingdomStandingOn = null;
        isFallingThrough = true;
        body.bodyType = RigidbodyType2D.Dynamic;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isCarried)
            return;

        if (collision.collider.TryGetComponent(out HappyKingdom kingdom))
        {
            LandOnHappyKingdom(kingdom, collision.collider);
            return;
        }

        if (!collision.collider.TryGetComponent(out Clouds cloud))
            return;

        if (isFallingThrough && cloud == ignoredFallThroughCloud)
            return;

        LandOnCloud(cloud);
    }

    void LandOnHappyKingdom(HappyKingdom kingdom, Collider2D kingdomCollider)
    {
        isFallingThrough = false;
        cloudStandingOn = null;
        kingdomStandingOn = kingdom;
        transform.SetParent(kingdom.transform, true);
        SnapFeetToCloudDeck(kingdomCollider);

        if (body.linearVelocity.y < 0f)
            body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
    }

    void LandOnCloud(Clouds cloud)
    {
        RestoreIgnoredCloudCollision();
        isFallingThrough = false;
        kingdomStandingOn = null;
        cloudStandingOn = cloud;

        GameDiagnostics.LogVillager(
            $"{name}: landed on {cloud.name} (platform={cloud.IsPlatformCloud})",
            this);

        if (cloud.IsPlatformCloud)
        {
            if (transform.parent != cloud.transform)
                transform.SetParent(cloud.transform, true);
            SnapFeetToCloudDeck(cloud.CloudCollider);
        }
        else if (body.linearVelocity.y <= 0.5f)
            body.linearVelocity = new Vector2(body.linearVelocity.x * 0.4f, subtleBounceVelocity);
        else if (body.linearVelocity.y < 0f)
            body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
    }

    void SnapFeetToCloudDeck(Collider2D cloudCollider)
    {
        if (cloudCollider == null || bodyCollider == null)
            return;

        float feetOffset = transform.position.y - bodyCollider.bounds.min.y;
        float targetY = cloudCollider.bounds.max.y + feetOffset;
        if (transform.position.y < targetY - 0.02f)
        {
            transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
            if (body.linearVelocity.y < 0f)
                body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
        }
    }

    void RestoreIgnoredCloudCollision()
    {
        if (ignoredFallThroughCloud == null)
            return;

        Collider2D cloudCollider = ignoredFallThroughCloud.CloudCollider;
        if (cloudCollider != null && bodyCollider != null)
            Physics2D.IgnoreCollision(bodyCollider, cloudCollider, false);

        ignoredFallThroughCloud = null;
    }

    public void SetTouchingHappyKingdom(bool touching)
    {
        isTouchingHappyKingdom = touching;
    }

    void EnsureGroundCheck()
    {
        if (groundCheck != null)
            return;

        var groundCheckObject = new GameObject("GroundCheck");
        groundCheckObject.transform.SetParent(transform, false);
        groundCheck = groundCheckObject.transform;
        RestoreGroundCheckToFeet();
    }

    void RestoreGroundCheckToFeet()
    {
        EnsureGroundCheck();

        float feetLocalY = GetFeetLocalOffset();
        groundCheck.localPosition = new Vector3(0f, feetLocalY + 0.08f, 0f);
    }

    static float GetContainerFloorLocalY(Transform container)
    {
        if (container.TryGetComponent(out BoxCollider2D box))
            return box.offset.y - box.size.y * 0.5f;

        return 0f;
    }

    float GetFeetBelowPivotWorld()
    {
        if (bodyCollider != null && bodyCollider.enabled)
            return transform.position.y - bodyCollider.bounds.min.y;

        if (TryGetComponent(out SpriteRenderer spriteRenderer))
            return transform.position.y - spriteRenderer.bounds.min.y;

        return 0.5f;
    }

    float GetFeetLocalOffset()
    {
        if (bodyCollider != null)
            return bodyCollider.bounds.min.y - transform.position.y;

        if (TryGetComponent(out SpriteRenderer spriteRenderer))
            return spriteRenderer.bounds.min.y - transform.position.y;

        return -0.5f;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out HappyKingdom kingdom))
            kingdom.NotifyVillagerEntered(this);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out HappyKingdom kingdom))
            kingdom.NotifyVillagerExited(this);
    }
}
