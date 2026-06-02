using System.Collections.Generic;

public static class VillagerRegistry
{
    static readonly HashSet<Villagers> villagers = new();

    public static void Register(Villagers villager)
    {
        if (villager != null)
            villagers.Add(villager);
    }

    public static void Unregister(Villagers villager)
    {
        if (villager != null)
            villagers.Remove(villager);
    }

    public static int TotalCount => villagers.Count;

    public static IEnumerable<Villagers> All => villagers;
}
