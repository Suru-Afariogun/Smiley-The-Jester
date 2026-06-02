using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy spawned from MoodDrop clouds. Chases the player using recorded path waypoints.
/// Registered in a static set so Clouds can enforce the 6-enemy global cap.
/// </summary>
public class MoodDrop : MonoBehaviour
{
    static readonly HashSet<MoodDrop> ActiveInstances = new();

    public static int GlobalActiveCount => ActiveInstances.Count;

    const float DetectionRange = 6f;
    const float WaypointReachDistance = 0.08f;
    const float FallVelocityThreshold = 0.35f;
    const int GroundedFramesRequired = 3;
    const int AirborneFramesRequired = 2;

    static readonly int RunParam = Animator.StringToHash("Run");
    static readonly int JumpParam = Animator.StringToHash("Jump");
    static readonly int StartFallParam = Animator.StringToHash("StartFall");
    static readonly int FallParam = Animator.StringToHash("Fall");
    static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
    static readonly int IsInAirParam = Animator.StringToHash("IsInAir");

    [Header("Movement")]
    [SerializeField] float followSpeed = 6f;

    [Header("Ground check")]
    [SerializeField] Transform groundCheck;
    [SerializeField] float groundCheckRadius = 0.15f;
    [SerializeField] LayerMask groundLayers;

    [Header("Components")]
    [SerializeField] Animator animator;
    [SerializeField] SpriteRenderer spriteRenderer;

    Clouds spawner;
    Transform player;
    PlayerPathRecorder pathRecorder;
    Collider2D bodyCollider;
    Vector3 defaultScale;
    int pathIndex;
    bool isChasing;
    bool playerInScanArea;
    bool wasGrounded = true;
    bool isFallingAnim;
    bool stableGrounded;
    bool stableInAir;
    int groundedContactScore;
    int airborneContactFrames;
    float previousY;
    float verticalVelocity;

    public void Initialize(Clouds owner)
    {
        spawner = owner;
    }

    public void NotifyPlayerEnteredScan()
    {
        playerInScanArea = true;
    }

    public void NotifyPlayerExitedScan()
    {
        playerInScanArea = false;
    }

    void OnEnable()
    {
        ActiveInstances.Add(this);
    }

    void OnDisable()
    {
        ActiveInstances.Remove(this);
    }

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        bodyCollider = GetComponent<Collider2D>();
        defaultScale = transform.localScale;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;

        pathRecorder = PlayerPathRecorder.Instance;
        if (pathRecorder == null && player != null)
            pathRecorder = player.GetComponent<PlayerPathRecorder>();

        foreach (MoodDropScanArea scanArea in GetComponentsInChildren<MoodDropScanArea>(true))
        {
            Collider2D scanCollider = scanArea.GetComponent<Collider2D>();
            if (scanCollider != null)
                scanCollider.isTrigger = true;
        }
    }

    void Update()
    {
        if (player == null)
            return;

        float deltaTime = Time.deltaTime;
        float currentY = transform.position.y;
        verticalVelocity = deltaTime > 0f ? (currentY - previousY) / deltaTime : 0f;

        UpdateGroundedState();

        bool canSeePlayer = CanDetectPlayer();

        if (canSeePlayer)
        {
            if (!isChasing)
                BeginChase();

            FollowPlayerPath();
        }
        else
        {
            isChasing = false;
            UpdateAnimations(0f, false);
        }

        wasGrounded = stableGrounded;
        previousY = currentY;
    }

    bool CanDetectPlayer()
    {
        if (playerInScanArea)
            return true;

        return Vector2.Distance(transform.position, player.position) <= DetectionRange;
    }

    void BeginChase()
    {
        isChasing = true;
        if (pathRecorder != null && pathRecorder.Path.Count > 0)
            pathIndex = pathRecorder.FindClosestPathIndex(transform.position);
    }

    void FollowPlayerPath()
    {
        if (pathRecorder == null || pathRecorder.Path.Count == 0)
        {
            MoveToward(player.position);
            UpdateAnimations((player.position.x - transform.position.x), true);
            return;
        }

        if (pathIndex >= pathRecorder.Path.Count)
            pathIndex = pathRecorder.Path.Count - 1;

        Vector2 target = pathRecorder.Path[pathIndex];
        Vector2 previousPosition = transform.position;
        transform.position = Vector2.MoveTowards(transform.position, target, followSpeed * Time.deltaTime);

        float horizontalDelta = target.x - previousPosition.x;
        if (Mathf.Abs(horizontalDelta) > 0.001f)
            SetFacing(horizontalDelta);

        if (Vector2.Distance(transform.position, target) <= WaypointReachDistance)
            pathIndex = Mathf.Min(pathIndex + 1, pathRecorder.Path.Count - 1);

        bool movedUp = transform.position.y - previousY > 0.05f;
        if (movedUp && wasGrounded && animator != null)
            animator.SetTrigger(JumpParam);

        UpdateAnimations(horizontalDelta, true);
    }

    void MoveToward(Vector3 targetPosition)
    {
        Vector2 previousPosition = transform.position;
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, followSpeed * Time.deltaTime);
        float horizontalDelta = targetPosition.x - previousPosition.x;
        if (Mathf.Abs(horizontalDelta) > 0.001f)
            SetFacing(horizontalDelta);
    }

    void SetFacing(float horizontal)
    {
        CharacterFacing.ApplyMovementFacing(transform, defaultScale, horizontal);
    }

    void UpdateGroundedState()
    {
        bool touchingGround = ProbeGrounded();
        bool canCountAsLanded = touchingGround && verticalVelocity <= 0.2f;

        if (canCountAsLanded)
        {
            airborneContactFrames = 0;
            groundedContactScore = Mathf.Min(groundedContactScore + 1, GroundedFramesRequired + 2);
        }
        else
        {
            groundedContactScore = Mathf.Max(groundedContactScore - 1, 0);
            airborneContactFrames++;
        }

        stableGrounded = groundedContactScore >= GroundedFramesRequired;
        stableInAir = !stableGrounded && airborneContactFrames >= AirborneFramesRequired;
    }

    void UpdateAnimations(float horizontalDelta, bool isFollowingPath)
    {
        if (animator == null)
            return;

        bool isRunning = isFollowingPath && stableGrounded && Mathf.Abs(horizontalDelta) > 0.01f;

        if (stableGrounded)
            isFallingAnim = false;

        TryBeginFallAnimation();

        animator.SetBool(IsGroundedParam, stableGrounded);
        animator.SetBool(IsInAirParam, stableInAir);
        animator.SetBool(RunParam, isRunning);
        animator.SetBool(FallParam, false);
    }

    void TryBeginFallAnimation()
    {
        if (animator == null || isFallingAnim || stableGrounded)
            return;

        if (verticalVelocity > 0.08f)
            return;

        if (verticalVelocity >= -FallVelocityThreshold)
            return;

        BeginFallAnimation();
    }

    void BeginFallAnimation()
    {
        isFallingAnim = true;
        animator.ResetTrigger(StartFallParam);
        animator.SetTrigger(StartFallParam);
        animator.SetBool(FallParam, false);
    }

    bool ProbeGrounded()
    {
        Vector2 point = GetGroundProbePosition();
        int layerMask = groundLayers.value == 0 ? Physics2D.AllLayers : groundLayers.value;

        Collider2D[] hits = Physics2D.OverlapCircleAll(point, groundCheckRadius, layerMask);
        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.isTrigger)
                continue;
            if (bodyCollider != null && hit == bodyCollider)
                continue;
            if (hit.GetComponentInParent<MoodDropScanArea>() != null)
                continue;
            return true;
        }

        RaycastHit2D rayHit = Physics2D.Raycast(point, Vector2.down, groundCheckRadius + 0.35f, layerMask);
        if (rayHit.collider != null && !rayHit.collider.isTrigger && rayHit.collider != bodyCollider)
            return true;

        return HasSupportingContact(layerMask);
    }

    Vector2 GetGroundProbePosition()
    {
        if (groundCheck != null)
            return groundCheck.position;

        if (bodyCollider != null)
            return new Vector2(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y + 0.05f);

        return (Vector2)transform.position + Vector2.down * 0.5f;
    }

    bool HasSupportingContact(int layerMask)
    {
        if (bodyCollider == null)
            return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(layerMask);

        ContactPoint2D[] contacts = new ContactPoint2D[8];
        int count = bodyCollider.GetContacts(filter, contacts);
        for (int i = 0; i < count; i++)
        {
            if (contacts[i].normal.y >= 0.35f)
                return true;
        }

        return false;
    }

    public void KillFromHappyStar()
    {
        Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.TryGetComponent(out Clouds cloud) && cloud.IsPlatformCloud)
            KillFromHappyStar();

        TryHitPlayer(collision.collider);
    }

    void OnDestroy()
    {
        GameDiagnostics.LogMoodDrop(
            $"{name}: destroyed (global active={GlobalActiveCount})",
            this);

        if (spawner != null)
            spawner.NotifyMoodDropDestroyed(this);
        else
            GameDiagnostics.LogMoodDrop($"{name}: destroyed with no spawner reference", this);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Scan-area child triggers bubble to this rigidbody; they must not deal damage.
        if (bodyCollider == null || !bodyCollider.isTrigger)
            return;

        if (other.GetComponentInParent<MoodDropScanArea>() != null)
            return;

        if (other.TryGetComponent(out CarryCloud carryCloud))
        {
            carryCloud.ApplyRainOrMoodDropHit();
            return;
        }

        if (other.TryGetComponent(out Villagers villager))
        {
            villager.ApplyMoodDropOrLightningHit();
            return;
        }

        TryHitPlayer(other);
    }

    void TryHitPlayer(Collider2D other)
    {
        if (other == null || !IsPlayerCollider(other))
            return;

        PlayerController player = other.GetComponent<PlayerController>()
            ?? other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        player.ApplyMoodDropHit(transform.position);
    }

    static bool IsPlayerCollider(Collider2D other)
    {
        if (other.CompareTag("Player"))
            return true;

        return other.GetComponentInParent<PlayerControls>() != null;
    }
}
