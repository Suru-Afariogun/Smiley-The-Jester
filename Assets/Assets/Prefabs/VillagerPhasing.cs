using UnityEngine;

/// <summary>
/// Smiley and Happy Stars pass through villagers without blocking or accidental hits.
/// </summary>
public static class VillagerPhasing
{
    public static void RegisterPlayer(Collider2D playerCollider)
    {
        if (playerCollider == null)
            return;

        foreach (Villagers villager in VillagerRegistry.All)
            IgnoreWithVillager(playerCollider, villager);
    }

    public static void RegisterVillager(Villagers villager)
    {
        if (villager == null || !villager.TryGetComponent(out Collider2D villagerCollider))
            return;

        PlayerControls player = Object.FindFirstObjectByType<PlayerControls>();
        if (player != null && player.TryGetComponent(out Collider2D playerCollider))
            Physics2D.IgnoreCollision(playerCollider, villagerCollider, true);

        foreach (Projectiles projectile in Object.FindObjectsByType<Projectiles>(FindObjectsSortMode.None))
        {
            if (projectile.Type != ProjectileType.HappyStar)
                continue;

            foreach (Collider2D projectileCollider in projectile.GetComponentsInChildren<Collider2D>())
                Physics2D.IgnoreCollision(projectileCollider, villagerCollider, true);
        }
    }

    public static void RegisterHappyStar(GameObject happyStarInstance)
    {
        if (happyStarInstance == null)
            return;

        Collider2D[] starColliders = happyStarInstance.GetComponentsInChildren<Collider2D>();
        if (starColliders.Length == 0)
            return;

        foreach (Villagers villager in VillagerRegistry.All)
        {
            if (villager == null || !villager.TryGetComponent(out Collider2D villagerCollider))
                continue;

            foreach (Collider2D starCollider in starColliders)
                Physics2D.IgnoreCollision(starCollider, villagerCollider, true);
        }
    }

    static void IgnoreWithVillager(Collider2D otherCollider, Villagers villager)
    {
        if (otherCollider == null || villager == null || !villager.TryGetComponent(out Collider2D villagerCollider))
            return;

        Physics2D.IgnoreCollision(otherCollider, villagerCollider, true);
    }
}
