using UnityEngine;

/// <summary>
/// Shared left/right facing for 2D characters whose art is drawn facing LEFT at default scale.
/// We flip by negating localScale.x (mirror on X) instead of using SpriteRenderer.flipX,
/// so child objects and fire points flip with the character.
/// </summary>
public static class CharacterFacing
{
    /// <summary>
    /// Turns the transform to face movement direction.
    /// horizontal: positive = moving right, negative = moving left (world X axis).
    /// defaultScale: scale captured in Awake before any flipping — defines "face left" pose.
    /// </summary>
    public static void ApplyMovementFacing(Transform target, Vector3 defaultScale, float horizontal)
    {
        float absX = Mathf.Abs(defaultScale.x);
        if (absX < 0.0001f)
            absX = 1f;

        // Art faces left at +absX. Moving right requires a negative X scale (mirror).
        if (horizontal > 0.01f)
        {
            target.localScale = new Vector3(-absX, defaultScale.y, defaultScale.z);
        }
        else if (horizontal < -0.01f)
        {
            target.localScale = new Vector3(absX, defaultScale.y, defaultScale.z);
        }
    }

    /// <summary>
    /// Returns +1 when the character visually faces right, -1 when facing left.
    /// Used for projectile direction and knockback along the look direction.
    /// </summary>
    public static float GetFacingSign(Transform target)
    {
        // Negative localScale.x means we mirrored from left-default to face right.
        return target.localScale.x < 0f ? 1f : -1f;
    }
}
