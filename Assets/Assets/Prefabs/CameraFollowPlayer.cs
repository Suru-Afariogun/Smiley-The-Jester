using UnityEngine;

/// <summary>
/// Orthographic camera that follows the active player (or carry cloud when switched).
/// Stays inside the level background bounds.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraFollowPlayer : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 0f, -10f);
    [SerializeField] float smoothTime = 0.15f;

    Camera cam;
    PlayerControls playerControls;
    Vector3 smoothVelocity;

    void Awake()
    {
        cam = GetComponent<Camera>();

        if (!cam.orthographic)
            Debug.LogWarning("[CameraFollowPlayer] Camera is not orthographic — bounds clamping assumes orthographic.");

        if (target == null)
            playerControls = FindFirstObjectByType<PlayerControls>();
    }

    void LateUpdate()
    {
        Transform followTarget = ResolveTarget();
        if (followTarget == null)
            return;

        Vector3 desired = followTarget.position + offset;
        desired.z = offset.z;

        if (LevelBackgroundBounds.Instance != null && cam != null)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector2 clamped = LevelBackgroundBounds.Instance.ClampCameraCenter(
                new Vector2(desired.x, desired.y),
                halfWidth,
                halfHeight);
            desired.x = clamped.x;
            desired.y = clamped.y;
        }

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref smoothVelocity, smoothTime);
    }

    Transform ResolveTarget()
    {
        if (target != null)
            return target;

        if (playerControls == null)
            playerControls = FindFirstObjectByType<PlayerControls>();

        if (playerControls == null)
            return null;

        if (playerControls.IsControllingCarryCloud && playerControls.ActiveCarryCloud != null)
            return playerControls.ActiveCarryCloud.transform;

        return playerControls.transform;
    }
}
