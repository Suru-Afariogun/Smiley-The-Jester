using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Smiley movement, jump, animator parameters, hit reactions, and sprite facing.
/// Requires Rigidbody2D on the same GameObject (Unity applies velocity in FixedUpdate).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerControls : MonoBehaviour
{
    const float HitStunDuration = 0.2f;
    const float InvincibilityDuration = 0.5f;
    const float FlickerStep = 0.05f;
    const float FlickerAlphaVisible = 0.5f;
    const float FlickerAlphaHidden = 0f;
    const int GroundedFramesRequired = 3;
    const int AirborneFramesRequired = 2;
    const float FallVelocityThreshold = 0.35f;

    static readonly int RunParam = Animator.StringToHash("Run");
    static readonly int JumpParam = Animator.StringToHash("Jump");
    static readonly int StartFallParam = Animator.StringToHash("StartFall");
    static readonly int FallParam = Animator.StringToHash("Fall");
    static readonly int StarShotParam = Animator.StringToHash("StarShot");
    static readonly int StunnedParam = Animator.StringToHash("Stunned");
    static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
    static readonly int IsInAirParam = Animator.StringToHash("IsInAir");
    static readonly int IsAttackingParam = Animator.StringToHash("IsAttacking");

    [Header("Movement")]
    [SerializeField] float moveSpeed = 6f;

    [Header("Jump")]
    [SerializeField] float jumpForce = 12f;
    [SerializeField] float jumpMinForce = 4f;
    [SerializeField] float jumpMaxHoldTime = 0.32f;
    [SerializeField] float jumpCutMultiplier = 0.5f;

    [Header("Fall")]
    [SerializeField] float fallGravityScale = 2.3f;

    [Header("Ground check")]
    [SerializeField] Transform groundCheck;
    [SerializeField] float groundCheckRadius = 0.15f;
    [SerializeField] LayerMask groundLayers;

    [Header("Attack")]
    [SerializeField] Transform happyStarFirePoint;
    [SerializeField] float starShotMovementLockTime = 0.35f;

    [Header("Character switch")]
    [SerializeField] CarryCloud carryCloud;

    [Header("Components")]
    [SerializeField] Animator animator;
    [SerializeField] SpriteRenderer spriteRenderer;

    Rigidbody2D body;
    Collider2D bodyCollider;
    PlayerController combat;
    InputActions inputActions;
    InputAction movementAction;
    InputAction leftAction;
    InputAction rightAction;
    InputAction jumpAction;
    InputAction attackAction;
    InputAction characterSwitchAction;

    Vector3 defaultScale;
    Vector2 lastMoveDirection = Vector2.right;
    bool isStunned;
    bool isAttacking;
    bool isInvincible;
    float invincibleUntilTime;
    Coroutine hitReactionRoutine;
    bool isActiveCharacter = true;
    bool controllingCarryCloud;
    bool isRidingCarryCloud;
    Transform parentBeforeRide;
    RigidbodyType2D bodyTypeBeforeRide;
    float gravityScaleBeforeRide;
    bool stableGrounded;
    bool stableInAir;
    bool isFallingAnim;
    bool leftGroundLatch;
    int groundedContactScore;
    int airborneContactFrames;
    float jumpAnimatorLockUntil;
    bool jumpBoostActive;
    float jumpHoldTimer;
    SpriteRenderer[] allSpriteRenderers;
    Color[] defaultSpriteColors;

    public Vector2 LastMoveDirection => lastMoveDirection;
    public bool IsMovementBlocked => isStunned || isAttacking;
    public bool IsInvincible => isInvincible && Time.time < invincibleUntilTime;
    public bool IsActiveCharacter => isActiveCharacter;
    public bool IsControllingCarryCloud => controllingCarryCloud;
    public bool IsRidingCarryCloud => isRidingCarryCloud;
    public CarryCloud ActiveCarryCloud => carryCloud;
    public Transform HappyStarFirePoint => happyStarFirePoint;

    /// <summary>Project Input Actions asset (Assets/InputActions.inputactions).</summary>
    public InputActions GameInput => inputActions;

    public InputAction AttackAction => attackAction;

    public InputAction CharacterSwitchAction => characterSwitchAction;

    public void SetCarryCloud(CarryCloud cloud) => carryCloud = cloud;

    void Awake()
    {
        // GetComponent<T>() reads a component on this same GameObject.
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        body.gravityScale = fallGravityScale;
        combat = GetComponent<PlayerController>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Store starting scale so facing flips can return to the artist's default (left-facing).
        defaultScale = transform.localScale;
        CacheSpriteColors();
        SetupInput();

        if (GetComponent<PlayerPathRecorder>() == null)
            gameObject.AddComponent<PlayerPathRecorder>();

        AlignGroundCheckToFeet();

        VillagerPhasing.RegisterPlayer(bodyCollider);

        if (carryCloud == null)
            carryCloud = FindFirstObjectByType<CarryCloud>();
    }

    public void ToggleCarryCloudControl()
    {
        controllingCarryCloud = !controllingCarryCloud;

        if (controllingCarryCloud)
        {
            // Only parent to the cloud if Smiley is already standing on it.
            carryCloud?.AttachRider(this);
            SetCharacterActive(false);
            SetBackgroundAppearance(true);
            carryCloud?.SetControlled(true);
        }
        else
        {
            carryCloud?.DetachRider();
            SetCharacterActive(true);
            SetBackgroundAppearance(false);
            carryCloud?.SetControlled(false);
        }
    }

    public void BeginRidingCloud(Transform anchor, Collider2D cloudCollider)
    {
        if (anchor == null)
            return;

        isRidingCarryCloud = true;
        parentBeforeRide = transform.parent;
        bodyTypeBeforeRide = body.bodyType;
        gravityScaleBeforeRide = body.gravityScale;

        float feetOffset = GetFeetOffsetAbovePivot();
        transform.SetParent(anchor, false);
        transform.localPosition = new Vector3(0f, feetOffset, 0f);
        transform.localRotation = Quaternion.identity;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;

        if (bodyCollider != null && cloudCollider != null)
            Physics2D.IgnoreCollision(bodyCollider, cloudCollider, true);

        if (animator != null)
        {
            animator.SetBool(RunParam, false);
            animator.SetBool(FallParam, false);
            animator.SetBool(IsInAirParam, false);
            animator.SetBool(IsGroundedParam, true);
        }
    }

    public void EndRidingCloud(Collider2D cloudCollider)
    {
        if (!isRidingCarryCloud)
            return;

        isRidingCarryCloud = false;

        Vector3 worldPosition = transform.position;
        transform.SetParent(parentBeforeRide);
        transform.position = worldPosition;

        body.bodyType = bodyTypeBeforeRide;
        body.gravityScale = gravityScaleBeforeRide;
        body.linearVelocity = Vector2.zero;

        if (bodyCollider != null && cloudCollider != null)
            Physics2D.IgnoreCollision(bodyCollider, cloudCollider, false);
    }

    float GetFeetOffsetAbovePivot()
    {
        if (bodyCollider != null)
            return transform.position.y - bodyCollider.bounds.min.y;

        if (spriteRenderer != null)
            return transform.position.y - spriteRenderer.bounds.min.y;

        return 0.5f;
    }

    /// <summary>
    /// Moves GroundCheck to the feet if it was left at the character center (common prefab mistake).
    /// </summary>
    void AlignGroundCheckToFeet()
    {
        if (groundCheck == null)
            return;

        if (groundCheck.localPosition.sqrMagnitude > 0.02f)
            return;

        if (bodyCollider != null)
        {
            Vector3 feetWorld = new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y, transform.position.z);
            groundCheck.position = feetWorld + Vector3.up * 0.08f;
            return;
        }

        if (spriteRenderer != null)
        {
            float feetLocalY = spriteRenderer.bounds.min.y - transform.position.y + 0.08f;
            groundCheck.localPosition = new Vector3(0f, feetLocalY, 0f);
        }
    }

    void Start()
    {
        if (combat != null && happyStarFirePoint != null)
            combat.SetFirePoint(happyStarFirePoint);

        controllingCarryCloud = false;
        carryCloud?.SetControlled(false);
    }

    void CacheSpriteColors()
    {
        allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        defaultSpriteColors = new Color[allSpriteRenderers.Length];
        for (int i = 0; i < allSpriteRenderers.Length; i++)
            defaultSpriteColors[i] = allSpriteRenderers[i].color;
    }

    public void SetCharacterActive(bool active)
    {
        isActiveCharacter = active;
    }

    public void SetBackgroundAppearance(bool dimmed)
    {
        if (allSpriteRenderers == null || defaultSpriteColors == null)
            return;

        for (int i = 0; i < allSpriteRenderers.Length; i++)
        {
            if (allSpriteRenderers[i] == null)
                continue;

            allSpriteRenderers[i].color = dimmed
                ? new Color(0.4f, 0.4f, 0.45f, 0.2f)
                : defaultSpriteColors[i];
        }
    }

    void OnDestroy()
    {
        inputActions?.Dispose();
    }

    void SetupInput()
    {
        inputActions = new InputActions();
        var player = inputActions.PlayerControls;
        movementAction = player.Movement;
        leftAction = player.Left;
        rightAction = player.Right;
        jumpAction = player.Jump;
        attackAction = player.Attack;
        characterSwitchAction = player.CharacterSwitch;
        player.Enable();
    }

    float ReadHorizontalMoveInput()
    {
        float horizontal = movementAction.ReadValue<Vector2>().x;
        if (leftAction.IsPressed())
            horizontal -= 1f;
        if (rightAction.IsPressed())
            horizontal += 1f;
        return Mathf.Clamp(horizontal, -1f, 1f);
    }

    void Update()
    {
        if (characterSwitchAction != null && characterSwitchAction.WasPressedThisFrame())
            ToggleCarryCloudControl();

        if (!isActiveCharacter)
            return;

        float horizontalMove = ReadHorizontalMoveInput();

        if (!IsMovementBlocked)
        {
            if (Mathf.Abs(horizontalMove) < 0.01f)
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            else
                HandleHorizontalMovement(horizontalMove);
        }

        UpdateGroundedState();

        if (!IsMovementBlocked && jumpAction.WasPressedThisFrame() && ProbeGrounded())
            Jump();

        TryBeginFallAnimation();
        UpdateAnimatorParameters(horizontalMove);

        if (attackAction.WasPressedThisFrame() && !isStunned && !isAttacking)
            BeginStarShot(new Vector2(horizontalMove, 0f));
    }

    void FixedUpdate()
    {
        if (isRidingCarryCloud)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            return;
        }

        if (!isActiveCharacter)
            return;

        if (IsMovementBlocked)
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

        HandleVariableJump();
        ClampPositionToLevelBounds();
    }

    void ClampPositionToLevelBounds()
    {
        if (LevelBackgroundBounds.Instance == null)
            return;

        Vector2 current = body.position;
        Vector2 clamped = LevelBackgroundBounds.Instance.ClampPosition(current, bodyCollider);
        if ((clamped - current).sqrMagnitude < 0.0001f)
            return;

        body.position = clamped;

        Vector2 velocity = body.linearVelocity;
        if (Mathf.Abs(current.x - clamped.x) > 0.001f)
            velocity.x = 0f;
        if (Mathf.Abs(current.y - clamped.y) > 0.001f)
            velocity.y = 0f;
        body.linearVelocity = velocity;
    }

    // --- Movement: left / right only, run anim, sprite flip ---

    void HandleHorizontalMovement(float horizontal)
    {
        if (Mathf.Abs(horizontal) < 0.01f)
            return;

        // Vector2.right / left are unit directions used by knockback and AI systems.
        lastMoveDirection = horizontal > 0f ? Vector2.right : Vector2.left;
        SetFacing(horizontal);
        // linearVelocity: (x, y) — we only change X so gravity/jump Y is preserved.
        body.linearVelocity = new Vector2(horizontal * moveSpeed, body.linearVelocity.y);
    }

    /// <summary>
    /// Mirrors the sprite on X. Art faces left by default; see CharacterFacing for the sign rules.
    /// </summary>
    void SetFacing(float horizontal)
    {
        CharacterFacing.ApplyMovementFacing(transform, defaultScale, horizontal);
        GameDiagnostics.LogPlayer(
            $"{name} facing: scale.x={transform.localScale.x:F2}, moveX={horizontal:F2}",
            this);
    }

    /// <summary>
    /// Ground contact buffer for gameplay. Animator uses the same flags; Fall exits via Fall→Idle in the controller.
    /// </summary>
    void UpdateGroundedState()
    {
        bool touchingGround = ProbeGrounded();
        bool canCountAsLanded = touchingGround && body.linearVelocity.y <= 0.2f;

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

        if (!stableGrounded)
            leftGroundLatch = true;
        else
            leftGroundLatch = false;
    }

    void TryBeginFallAnimation()
    {
        if (animator == null || isFallingAnim)
            return;

        if (Time.time < jumpAnimatorLockUntil)
            return;

        if (stableGrounded)
            return;

        if (body.linearVelocity.y > 0.08f)
            return;

        if (body.linearVelocity.y >= -FallVelocityThreshold)
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

    void UpdateAnimatorParameters(float horizontal)
    {
        if (animator == null)
            return;

        bool jumpLock = Time.time < jumpAnimatorLockUntil;
        bool animatorGrounded = stableGrounded && !jumpLock;
        bool animatorInAir = !animatorGrounded || isFallingAnim;

        if (stableGrounded)
            isFallingAnim = false;

        bool movingHorizontally = Mathf.Abs(horizontal) > 0.01f;
        bool isRunning = !IsMovementBlocked && animatorGrounded && movingHorizontally;

        animator.SetBool(IsGroundedParam, animatorGrounded);
        animator.SetBool(IsInAirParam, animatorInAir);
        animator.SetBool(IsAttackingParam, isAttacking);
        animator.SetBool(StunnedParam, isStunned);
        animator.SetBool(RunParam, isRunning);
        animator.SetBool(FallParam, false);
    }

    public bool IsGrounded() => stableGrounded;

    // --- Jump ---

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
            return true;
        }

        // Thin / moving platform clouds: short ray below the feet probe
        RaycastHit2D rayHit = Physics2D.Raycast(point, Vector2.down, groundCheckRadius + 0.2f, layerMask);
        if (rayHit.collider != null && !rayHit.collider.isTrigger && rayHit.collider != bodyCollider)
            return true;

        if (IsOnPlatformCloud(point, layerMask))
            return true;

        if (IsOnHappyKingdomDeck(point))
            return true;

        return HasSupportingContact();
    }

    bool IsOnHappyKingdomDeck(Vector2 feetPoint)
    {
        float rayDistance = groundCheckRadius + 0.45f;
        RaycastHit2D hit = Physics2D.Raycast(feetPoint, Vector2.down, rayDistance, Physics2D.AllLayers);
        if (hit.collider == null || hit.collider.isTrigger || hit.collider == bodyCollider)
            return false;

        return hit.collider.GetComponentInParent<HappyKingdom>() != null;
    }

    bool IsOnPlatformCloud(Vector2 feetPoint, int layerMask)
    {
        float rayDistance = groundCheckRadius + 0.45f;
        RaycastHit2D hit = Physics2D.Raycast(feetPoint, Vector2.down, rayDistance, layerMask);
        if (hit.collider == null || hit.collider.isTrigger || hit.collider == bodyCollider)
            return false;

        return hit.collider.TryGetComponent(out Clouds cloud) && cloud.IsPlatformCloud;
    }

    Vector2 GetGroundProbePosition()
    {
        if (groundCheck != null)
            return groundCheck.position;

        if (bodyCollider != null)
            return new Vector2(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y + 0.05f);

        return (Vector2)transform.position + Vector2.down * 2f;
    }

    bool HasSupportingContact()
    {
        if (bodyCollider == null)
            return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(groundLayers.value == 0 ? Physics2D.AllLayers : groundLayers);

        ContactPoint2D[] contacts = new ContactPoint2D[8];
        int count = bodyCollider.GetContacts(filter, contacts);
        for (int i = 0; i < count; i++)
        {
            if (contacts[i].normal.y >= 0.45f)
                return true;
        }

        return false;
    }

    void Jump()
    {
        jumpBoostActive = true;
        jumpHoldTimer = 0f;
        body.linearVelocity = new Vector2(body.linearVelocity.x, jumpMinForce);
        groundedContactScore = 0;
        airborneContactFrames = AirborneFramesRequired;
        stableGrounded = false;
        stableInAir = true;
        leftGroundLatch = true;
        isFallingAnim = false;
        jumpAnimatorLockUntil = Time.time + 0.3f;

        if (animator == null)
            return;

        // Jump Any State transition needs Jump trigger + IsGrounded false (see animator).
        animator.SetBool(IsGroundedParam, false);
        animator.SetBool(IsInAirParam, true);
        animator.SetBool(RunParam, false);
        animator.SetBool(FallParam, false);
        animator.ResetTrigger(StartFallParam);
        animator.ResetTrigger(JumpParam);
        animator.SetTrigger(JumpParam);
    }

    void HandleVariableJump()
    {
        if (!jumpBoostActive || jumpAction == null)
            return;

        bool held = jumpAction.IsPressed();
        float verticalVelocity = body.linearVelocity.y;

        if (held && verticalVelocity > 0.01f && jumpHoldTimer < jumpMaxHoldTime)
        {
            jumpHoldTimer += Time.fixedDeltaTime;
            float holdProgress = Mathf.Clamp01(jumpHoldTimer / jumpMaxHoldTime);
            float targetVelocity = Mathf.Lerp(jumpMinForce, jumpForce, holdProgress);

            if (verticalVelocity < targetVelocity)
                body.linearVelocity = new Vector2(body.linearVelocity.x, targetVelocity);

            if (jumpHoldTimer >= jumpMaxHoldTime)
                jumpBoostActive = false;

            return;
        }

        if (!held && verticalVelocity > 0.01f && jumpHoldTimer < jumpMaxHoldTime)
        {
            body.linearVelocity = new Vector2(
                body.linearVelocity.x,
                verticalVelocity * jumpCutMultiplier);
        }

        if (!held || verticalVelocity <= 0.01f || jumpHoldTimer >= jumpMaxHoldTime)
            jumpBoostActive = false;
    }

    // --- Star shot: stops movement, plays attack anim, fires projectile ---

    void BeginStarShot(Vector2 moveInput)
    {
        isAttacking = true;
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

        if (animator != null)
        {
            animator.SetBool(RunParam, false);
            animator.ResetTrigger(StarShotParam);
            animator.SetTrigger(StarShotParam);
            animator.SetBool(IsAttackingParam, true);
        }

        combat?.TryFireHappyStar(moveInput);
        StartCoroutine(StarShotMovementLockRoutine());
    }

    IEnumerator StarShotMovementLockRoutine()
    {
        float timeout = Mathf.Max(starShotMovementLockTime, 0.25f);
        float elapsed = 0f;
        bool attackFinished = false;

        while (elapsed < timeout)
        {
            if (animator != null)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.IsName("Attack") && state.normalizedTime >= 0.99f && !animator.IsInTransition(0))
                    attackFinished = true;
            }

            if (attackFinished)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        isAttacking = false;
        if (animator != null)
        {
            animator.SetBool(IsAttackingParam, false);
            animator.ResetTrigger(StarShotParam);
        }
    }

    // --- Hit reaction: stun, stunned anim, alpha flicker, then invincibility ---

    public void TakeHit()
    {
        if (IsInvincible || isStunned)
            return;

        if (hitReactionRoutine != null)
            StopCoroutine(hitReactionRoutine);

        hitReactionRoutine = StartCoroutine(HitReactionRoutine());
    }

    IEnumerator HitReactionRoutine()
    {
        isStunned = true;
        isAttacking = false;
        jumpBoostActive = false;
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

        if (animator != null)
        {
            animator.SetBool(IsAttackingParam, false);
            animator.SetBool(RunParam, false);
            animator.SetBool(StunnedParam, true);
        }

        float stunEndTime = Time.time + HitStunDuration;
        bool showVisible = true;

        while (Time.time < stunEndTime)
        {
            SetSpriteAlpha(showVisible ? FlickerAlphaVisible : FlickerAlphaHidden);
            showVisible = !showVisible;
            yield return new WaitForSeconds(FlickerStep);
        }

        SetSpriteAlpha(1f);
        if (animator != null)
            animator.SetBool(StunnedParam, false);

        isStunned = false;
        isInvincible = true;
        invincibleUntilTime = Time.time + InvincibilityDuration;

        yield return new WaitForSeconds(InvincibilityDuration);
        isInvincible = false;
        hitReactionRoutine = null;
    }

    void SetSpriteAlpha(float alpha)
    {
        if (spriteRenderer == null)
            return;

        Color c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;
    }
}
