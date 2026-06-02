using UnityEngine;

/// <summary>
/// Happy Star shooting and special hit responses. Movement and animations live on PlayerControls.
/// Uses CharacterFacing.GetFacingSign so shots match mirrored left-facing sprite art.
/// </summary>
[RequireComponent(typeof(PlayerControls))]
public class PlayerController : MonoBehaviour
{
    const float LightningBounceDistance = 1f;

    [Header("Happy Star")]
    [SerializeField] GameObject happyStarPrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] int maxBurstShots = 6;
    [SerializeField] float burstCooldown = 0.5f;

    [Header("Hit reactions")]
    [SerializeField] float moodDropKnockbackDistance = 3f;

    PlayerControls controls;
    int shotsInBurst;
    float burstCooldownEndTime;

    void Awake()
    {
        controls = GetComponent<PlayerControls>();
        if (firePoint == null && controls != null)
            firePoint = controls.HappyStarFirePoint;
    }

    public void SetFirePoint(Transform point)
    {
        firePoint = point;
    }

    public void TryFireHappyStar(Vector2 moveInput)
    {
        if (happyStarPrefab == null)
        {
            GameDiagnostics.LogCombat($"{name}: happyStarPrefab is not assigned.", this);
            return;
        }

        if (shotsInBurst >= maxBurstShots)
        {
            if (Time.time < burstCooldownEndTime)
                return;

            shotsInBurst = 0;
        }

        // facingSign matches visual aim: +1 = shooting to the right (negative localScale.x after flip).
        float facingSign = CharacterFacing.GetFacingSign(transform);
        Vector2 direction = new Vector2(facingSign, 0f);

        if (moveInput.y > 0.25f)
            direction = new Vector2(facingSign, 1f).normalized;
        else if (moveInput.y < -0.25f)
            direction = new Vector2(facingSign, -1f).normalized;

        if (firePoint == null && controls != null)
            firePoint = controls.HappyStarFirePoint;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject instance = Instantiate(happyStarPrefab, spawnPos, Quaternion.identity);

        if (instance.TryGetComponent(out Projectiles projectile))
            projectile.LaunchHappyStar(direction);

        shotsInBurst++;
        GameDiagnostics.LogCombat(
            $"{name}: Happy Star shot {shotsInBurst}/{maxBurstShots}, direction={direction}",
            this);

        if (shotsInBurst >= maxBurstShots)
            burstCooldownEndTime = Time.time + burstCooldown;
    }

    public void ApplyLightningHit()
    {
        if (controls == null)
            return;

        transform.position -= (Vector3)(controls.LastMoveDirection * LightningBounceDistance);
        controls.TakeHit();
    }

    public void ApplyMoodDropHit(Vector2 attackerWorldPosition)
    {
        if (controls == null)
            return;

        Vector2 knockback = (Vector2)transform.position - attackerWorldPosition;
        knockback.y = 0f;
        if (knockback.sqrMagnitude < 0.01f)
            knockback = -controls.LastMoveDirection;
        knockback.Normalize();

        transform.position += (Vector3)(knockback * moodDropKnockbackDistance);
        controls.TakeHit();
    }

    public void RegisterRainHit()
    {
        controls?.TakeHit();
    }
}
