using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player-controlled flying cloud: 2D movement, villager pickup/drop, knockback from rain/MoodDrop hits.
/// </summary>
public class CarryCloud : MonoBehaviour
{
    const float KnockbackDistance = 6f;

    [Header("Flight")]
    [SerializeField] float flySpeed = 5f;

    [Header("Villager pickup")]
    [SerializeField] Transform villagerContainer;
    [SerializeField] float pickupRadius = 1.25f;
    [SerializeField] LayerMask villagerLayers;

    [Header("Smiley rider")]
    [SerializeField] Transform riderAnchor;

    [Header("Input")]
    [SerializeField] PlayerControls playerControls;

    Rigidbody2D body;
    Collider2D rideCollider;
    Collider2D containerCollider;
    Vector2 lastMoveDirection = Vector2.up;
    Villagers carriedVillager;
    PlayerControls attachedRider;
    bool isControlled;

    public bool IsControlled => isControlled;
    public Villagers CarriedVillager => carriedVillager;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        ConfigureFlightRigidbody();

        if (playerControls == null)
            playerControls = FindFirstObjectByType<PlayerControls>();

        if (playerControls != null)
            playerControls.SetCarryCloud(this);

        rideCollider = GetComponent<Collider2D>();
        EnsureContainerPickupTrigger();
        EnsureRiderAnchor();
        SetupCollisionPhasing();
    }

    void Start()
    {
        EnsureContainerPickupTrigger();
        SetupCollisionPhasing();
    }

    /// <summary>
    /// Keeps the Container Point pivot for carried villagers; pickup trigger lives on a child.
    /// </summary>
    void EnsureContainerPickupTrigger()
    {
        if (villagerContainer == null)
            return;

        Transform existingTrigger = villagerContainer.Find("Pickup Trigger");
        if (existingTrigger != null)
        {
            containerCollider = existingTrigger.GetComponent<Collider2D>();
            return;
        }

        if (!villagerContainer.TryGetComponent(out BoxCollider2D boxOnContainer))
        {
            containerCollider = villagerContainer.GetComponentInChildren<Collider2D>();
            return;
        }

        var triggerObject = new GameObject("Pickup Trigger");
        triggerObject.transform.SetParent(villagerContainer, false);
        triggerObject.transform.localPosition = new Vector3(boxOnContainer.offset.x, boxOnContainer.offset.y, 0f);
        triggerObject.transform.localRotation = Quaternion.identity;
        triggerObject.transform.localScale = Vector3.one;

        var triggerBox = triggerObject.AddComponent<BoxCollider2D>();
        triggerBox.isTrigger = true;
        triggerBox.offset = Vector2.zero;
        triggerBox.size = boxOnContainer.size;

        if (Application.isPlaying)
            Destroy(boxOnContainer);
        else
            DestroyImmediate(boxOnContainer);

        containerCollider = triggerBox;
    }

    public Vector3 GetVillagerCarryLocalPosition(Villagers villager)
    {
        float feetBelowPivot = villager.GetFeetOffsetAbovePivot();

        // Sit on the Container Point pivot (inside the cloud), not on the pickup trigger gizmo.
        const float interiorStandHeight = 0.15f;
        return new Vector3(0f, interiorStandHeight + feetBelowPivot, 0f);
    }

    void SetupCollisionPhasing()
    {
        if (containerCollider != null)
            containerCollider.isTrigger = true;

        foreach (Villagers villager in VillagerRegistry.All)
            IgnoreCollisionWithVillager(villager);

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null && player.TryGetComponent(out Collider2D playerCollider))
        {
            if (containerCollider != null)
                Physics2D.IgnoreCollision(containerCollider, playerCollider, true);
        }
    }

    void IgnoreCollisionWithVillager(Villagers villager)
    {
        if (villager == null || !villager.TryGetComponent(out Collider2D villagerCollider))
            return;

        if (rideCollider != null)
            Physics2D.IgnoreCollision(rideCollider, villagerCollider, true);

        if (containerCollider != null)
            Physics2D.IgnoreCollision(containerCollider, villagerCollider, true);
    }

    void ConfigureFlightRigidbody()
    {
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    public void SetControlled(bool controlled)
    {
        isControlled = controlled;
        ConfigureFlightRigidbody();

        if (!controlled && attachedRider != null)
            DetachRider();
    }

    void EnsureRiderAnchor()
    {
        if (riderAnchor != null)
            return;

        Transform existing = transform.Find("Rider Point");
        if (existing != null)
        {
            riderAnchor = existing;
            return;
        }

        var anchorObject = new GameObject("Rider Point");
        anchorObject.transform.SetParent(transform, false);

        if (rideCollider is BoxCollider2D box)
            anchorObject.transform.localPosition = new Vector3(box.offset.x, box.offset.y + box.size.y * 0.5f, 0f);
        else if (rideCollider != null)
            anchorObject.transform.localPosition = new Vector3(0f, rideCollider.bounds.max.y - transform.position.y, 0f);
        else
            anchorObject.transform.localPosition = new Vector3(0f, 1f, 0f);

        riderAnchor = anchorObject.transform;
    }

    public bool IsPlayerOnRideSurface(PlayerControls rider)
    {
        if (rider == null || rideCollider == null)
            return false;

        Collider2D riderCollider = rider.GetComponent<Collider2D>();
        if (riderCollider == null)
            return false;

        if (rider.IsRidingCarryCloud)
            return true;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.useLayerMask = false;

        Collider2D[] overlaps = new Collider2D[8];
        int count = riderCollider.Overlap(filter, overlaps);
        for (int i = 0; i < count; i++)
        {
            if (overlaps[i] == null)
                continue;

            if (overlaps[i] == rideCollider || overlaps[i].transform.IsChildOf(transform))
                return true;
        }

        Bounds playerBounds = riderCollider.bounds;
        Bounds cloudBounds = rideCollider.bounds;
        bool horizontalOverlap = playerBounds.max.x > cloudBounds.min.x && playerBounds.min.x < cloudBounds.max.x;
        bool feetOnDeck = playerBounds.min.y >= cloudBounds.min.y - 0.2f
            && playerBounds.min.y <= cloudBounds.max.y + 0.4f;

        return horizontalOverlap && feetOnDeck;
    }

    public void AttachRider(PlayerControls rider)
    {
        if (rider == null || !IsPlayerOnRideSurface(rider))
            return;

        EnsureRiderAnchor();
        attachedRider = rider;
        rider.BeginRidingCloud(riderAnchor, rideCollider);
    }

    public void DetachRider()
    {
        if (attachedRider == null)
            return;

        attachedRider.EndRidingCloud(rideCollider);
        attachedRider = null;
    }

    void FixedUpdate()
    {
        if (!isControlled || playerControls == null || playerControls.GameInput == null)
            return;

        var player = playerControls.GameInput.PlayerControls;
        Vector2 moveInput = ReadFlightMoveInput(player);
        if (moveInput.sqrMagnitude < 0.01f)
            return;

        lastMoveDirection = moveInput.normalized;
        Vector2 nextPosition = body.position + moveInput * flySpeed * Time.fixedDeltaTime;

        if (LevelBackgroundBounds.Instance != null)
            nextPosition = LevelBackgroundBounds.Instance.ClampPosition(nextPosition, GetComponent<Collider2D>());

        body.MovePosition(nextPosition);
    }

    void Update()
    {
        if (!isControlled || playerControls == null)
            return;

        if (playerControls.AttackAction != null && playerControls.AttackAction.WasPressedThisFrame())
            TryPickupOrSwapVillager();
    }

    static Vector2 ReadFlightMoveInput(InputActions.PlayerControlsActions player)
    {
        Vector2 move = Vector2.zero;
        if (player.Up.IsPressed())
            move.y += 1f;
        if (player.Down.IsPressed())
            move.y -= 1f;
        if (player.Left.IsPressed())
            move.x -= 1f;
        if (player.Right.IsPressed())
            move.x += 1f;

        if (move.sqrMagnitude < 0.01f)
            move = player.Movement.ReadValue<Vector2>();

        return move.sqrMagnitude > 1f ? move.normalized : move;
    }

    void TryPickupOrSwapVillager()
    {
        if (villagerContainer == null)
            return;

        Villagers target = FindVillagerInPickupRange();

        if (carriedVillager != null)
        {
            if (target != null && target != carriedVillager)
            {
                Vector3 swapWaitPosition = target.transform.position;
                carriedVillager.ReleaseToWorld(swapWaitPosition, droppedFromCloud: false);
                carriedVillager = target;
                target.AttachToContainer(villagerContainer, this);
                return;
            }

            DropCarriedVillager();
            return;
        }

        if (target == null)
            return;

        carriedVillager = target;
        target.AttachToContainer(villagerContainer, this);
    }

    Villagers FindVillagerInPickupRange()
    {
        Collider2D[] hits = villagerLayers.value == 0
            ? Physics2D.OverlapCircleAll(transform.position, pickupRadius)
            : Physics2D.OverlapCircleAll(transform.position, pickupRadius, villagerLayers);
        Villagers closest = null;
        float closestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            if (!hit.TryGetComponent(out Villagers villager))
                continue;

            if (villager.IsCarried)
                continue;

            float distance = Vector2.Distance(transform.position, villager.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = villager;
            }
        }

        return closest;
    }

    /// <summary>Knockback opposite last flight direction (rain / MoodDrop enemy hit).</summary>
    public void ApplyRainOrMoodDropHit()
    {
        if (lastMoveDirection.sqrMagnitude < 0.01f)
            lastMoveDirection = Vector2.up;

        transform.position -= (Vector3)(lastMoveDirection.normalized * KnockbackDistance);
        GameDiagnostics.LogCarryCloud($"{name}: knockback from rain/MoodDrop", this);
    }

    public void ApplyLightningHit()
    {
        DropCarriedVillager();
    }

    public void DropCarriedVillager()
    {
        if (carriedVillager == null)
            return;

        carriedVillager.ReleaseToWorld(transform.position, droppedFromCloud: true);
        carriedVillager = null;
    }

    public void RegisterVillagerForPhasing(Villagers villager) => IgnoreCollisionWithVillager(villager);
}
