using UnityEngine;

/// <summary>
/// Optional helper: wires Carry Cloud reference onto Smiley's PlayerControls.
/// Character switching is handled by PlayerControls (Y / M / gamepad West from Input Actions).
/// </summary>
public class CharacterSwitchController : MonoBehaviour
{
    [SerializeField] PlayerControls smiley;
    [SerializeField] CarryCloud carryCloud;

    void Awake()
    {
        if (smiley == null)
            smiley = FindFirstObjectByType<PlayerControls>();

        if (carryCloud == null)
            carryCloud = FindFirstObjectByType<CarryCloud>();

        if (smiley != null && carryCloud != null)
            smiley.SetCarryCloud(carryCloud);
    }
}
