using UnityEngine;

/// <summary>
/// Player-only detection volume on MoodDrop enemies. Ignored by projectiles and other gameplay colliders.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MoodDropScanArea : MonoBehaviour
{
    [SerializeField] MoodDrop owner;

    Collider2D areaCollider;
    int playerInsideCount;

    public static bool IsScanCollider(Collider2D collider)
    {
        return collider != null && collider.GetComponent<MoodDropScanArea>() != null;
    }

    void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<MoodDrop>();

        areaCollider = GetComponent<Collider2D>();
        if (areaCollider != null)
            areaCollider.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        playerInsideCount++;
        if (playerInsideCount == 1)
            owner?.NotifyPlayerEnteredScan();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        playerInsideCount = Mathf.Max(0, playerInsideCount - 1);
        if (playerInsideCount == 0)
            owner?.NotifyPlayerExitedScan();
    }

    static bool IsPlayer(Collider2D other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        return other.GetComponentInParent<PlayerControls>() != null;
    }
}
