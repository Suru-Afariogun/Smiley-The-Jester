using UnityEngine;

/// <summary>
/// World limits from the level background sprite. Used to clamp the player, carry cloud, and camera.
/// </summary>
[DisallowMultipleComponent]
public class LevelBackgroundBounds : MonoBehaviour
{
    public static LevelBackgroundBounds Instance { get; private set; }

    [SerializeField] SpriteRenderer backgroundRenderer;
    [SerializeField] float edgePadding = 0.35f;

    Bounds worldBounds;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[LevelBackgroundBounds] Multiple instances — using the first.");
            return;
        }

        Instance = this;

        if (backgroundRenderer == null)
            backgroundRenderer = GetComponent<SpriteRenderer>();

        RefreshBounds();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RefreshBounds()
    {
        if (backgroundRenderer == null)
            return;

        worldBounds = backgroundRenderer.bounds;
    }

    public Vector2 ClampPosition(Vector2 position, Collider2D collider)
    {
        RefreshBounds();

        Vector2 halfExtents = collider != null
            ? (Vector2)collider.bounds.extents
            : Vector2.zero;

        halfExtents += Vector2.one * edgePadding;
        return ClampPoint(position, halfExtents);
    }

    public Vector2 ClampCameraCenter(Vector2 center, float halfWidth, float halfHeight)
    {
        RefreshBounds();

        float minX = worldBounds.min.x + halfWidth;
        float maxX = worldBounds.max.x - halfWidth;
        float minY = worldBounds.min.y + halfHeight;
        float maxY = worldBounds.max.y - halfHeight;

        if (minX > maxX)
            center.x = worldBounds.center.x;
        else
            center.x = Mathf.Clamp(center.x, minX, maxX);

        if (minY > maxY)
            center.y = worldBounds.center.y;
        else
            center.y = Mathf.Clamp(center.y, minY, maxY);

        return center;
    }

    Vector2 ClampPoint(Vector2 point, Vector2 halfExtents)
    {
        point.x = Mathf.Clamp(point.x, worldBounds.min.x + halfExtents.x, worldBounds.max.x - halfExtents.x);
        point.y = Mathf.Clamp(point.y, worldBounds.min.y + halfExtents.y, worldBounds.max.y - halfExtents.y);
        return point;
    }

    void OnDrawGizmosSelected()
    {
        if (backgroundRenderer == null)
            backgroundRenderer = GetComponent<SpriteRenderer>();

        if (backgroundRenderer == null)
            return;

        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.35f);
        Gizmos.DrawCube(backgroundRenderer.bounds.center, backgroundRenderer.bounds.size);
    }
}
