using UnityEngine;

/// <summary>
/// Fire points and scan volumes are triggers only — they must not block movement or trap spawns.
/// </summary>
public static class CloudFirePoint
{
    public static void ConfigureTransform(Transform firePoint)
    {
        if (firePoint == null)
            return;

        foreach (Collider2D collider in firePoint.GetComponents<Collider2D>())
            ConfigureColliderPhasing(collider);
    }

    public static void ConfigureColliderPhasing(Collider2D collider)
    {
        if (collider == null)
            return;

        collider.isTrigger = true;
    }

    public static void ConfigureFirePointArray(Transform[] firePoints)
    {
        if (firePoints == null)
            return;

        foreach (Transform firePoint in firePoints)
            ConfigureTransform(firePoint);
    }
}
