using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Win zone: when every registered villager touches this area (and is not carried), shows win UI.
/// Uses 2D colliders so Smiley and villagers can stand on the kingdom deck.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HappyKingdom : MonoBehaviour
{
    const string StartScreenSceneName = "Start Screen";

    [Header("Win screen")]
    [SerializeField] GameObject winScreenRoot;
    [SerializeField] Button returnToStartButton;

    [Header("2D physics — platform deck (matches scene BoxCollider placement)")]
    [SerializeField] Vector2 platformColliderSize = new Vector2(37.88f, 2f);
    [SerializeField] Vector2 platformColliderOffset = new Vector2(0f, -7f);

    [Header("2D win detection — trigger volume")]
    [SerializeField] Vector2 winZoneColliderSize = new Vector2(37.88f, 22.33f);
    [SerializeField] Vector2 winZoneColliderOffset = Vector2.zero;

    readonly HashSet<Villagers> villagersTouching = new();
    bool hasWon;

    void Awake()
    {
        Ensure2DPhysicsColliders();

        if (winScreenRoot != null)
            winScreenRoot.SetActive(false);

        if (returnToStartButton != null)
            returnToStartButton.onClick.AddListener(LoadStartScreen);
    }

    void Ensure2DPhysicsColliders()
    {
        // 3D BoxCollider on this object does not collide with Rigidbody2D — remove it.
        foreach (Collider legacyCollider in GetComponents<Collider>())
        {
            if (legacyCollider is Collider2D)
                continue;

            if (Application.isPlaying)
                Destroy(legacyCollider);
            else
                DestroyImmediate(legacyCollider);
        }

        BoxCollider2D platformCollider = null;
        BoxCollider2D winZoneCollider = null;

        foreach (BoxCollider2D box in GetComponents<BoxCollider2D>())
        {
            if (box.isTrigger)
                winZoneCollider = box;
            else
                platformCollider = box;
        }

        if (platformCollider == null)
            platformCollider = gameObject.AddComponent<BoxCollider2D>();

        platformCollider.isTrigger = false;
        platformCollider.size = platformColliderSize;
        platformCollider.offset = platformColliderOffset;

        if (winZoneCollider == null)
            winZoneCollider = gameObject.AddComponent<BoxCollider2D>();

        winZoneCollider.isTrigger = true;
        winZoneCollider.size = winZoneColliderSize;
        winZoneCollider.offset = winZoneColliderOffset;

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            gameObject.layer = groundLayer;
    }

    void Update()
    {
        if (hasWon)
            return;

        CheckForWin();
    }

    public void NotifyVillagerEntered(Villagers villager)
    {
        if (villager == null || villager.IsCarried)
            return;

        villager.SetTouchingHappyKingdom(true);
        villagersTouching.Add(villager);
    }

    public void NotifyVillagerExited(Villagers villager)
    {
        if (villager == null)
            return;

        villager.SetTouchingHappyKingdom(false);
        villagersTouching.Remove(villager);
    }

    void CheckForWin()
    {
        int totalVillagers = VillagerRegistry.TotalCount;
        if (totalVillagers <= 0)
        {
            Debug.LogWarning("[HappyKingdom] No villagers registered — assign Villagers in the scene.");
            return;
        }

        if (villagersTouching.Count < totalVillagers)
            return;

        foreach (Villagers villager in VillagerRegistry.All)
        {
            if (villager == null || villager.IsCarried || !villager.IsTouchingHappyKingdom)
                return;
        }

        TriggerWin();
    }

    void TriggerWin()
    {
        Debug.Log($"[HappyKingdom] All {VillagerRegistry.TotalCount} villagers reached the kingdom — win!");
        hasWon = true;
        Time.timeScale = 0f;

        if (winScreenRoot != null)
            winScreenRoot.SetActive(true);
    }

    void LoadStartScreen()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(StartScreenSceneName);
    }
}
