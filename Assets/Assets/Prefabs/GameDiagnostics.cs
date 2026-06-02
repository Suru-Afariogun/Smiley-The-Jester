using UnityEngine;

/// <summary>
/// Toggle these flags in the Inspector at edit time, or change defaults here,
/// to print Debug.Log messages that explain what each system is doing at runtime.
/// </summary>
public static class GameDiagnostics
{
    public const bool CloudAppearance = true;
    public const bool MoodDropSpawning = true;
    public const bool PlayerFacing = false;
    public const bool HappyStarCombat = false;
    public const bool VillagerPhysics = false;
    public const bool CarryCloud = false;

    public static void LogCloud(string message, Object context = null)
    {
        if (CloudAppearance)
            Debug.Log($"[Clouds] {message}", context);
    }

    public static void LogMoodDrop(string message, Object context = null)
    {
        if (MoodDropSpawning)
            Debug.Log($"[MoodDrop] {message}", context);
    }

    public static void LogPlayer(string message, Object context = null)
    {
        if (PlayerFacing)
            Debug.Log($"[Player] {message}", context);
    }

    public static void LogCombat(string message, Object context = null)
    {
        if (HappyStarCombat)
            Debug.Log($"[Combat] {message}", context);
    }

    public static void LogVillager(string message, Object context = null)
    {
        if (VillagerPhysics)
            Debug.Log($"[Villager] {message}", context);
    }

    public static void LogCarryCloud(string message, Object context = null)
    {
        if (CarryCloud)
            Debug.Log($"[CarryCloud] {message}", context);
    }
}
