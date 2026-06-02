using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Pause : MonoBehaviour
{
    [SerializeField] GameObject pausePanel;
    [SerializeField] Text controlsText;

    InputAction pauseAction;
    bool isPaused;

    void Awake()
    {
        pauseAction = new InputAction("Pause", InputActionType.Button);
        pauseAction.AddBinding("<Keyboard>/escape");
        pauseAction.AddBinding("<Gamepad>/start");
        pauseAction.Enable();

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (controlsText != null)
            controlsText.text = GetControlsReferenceText();
    }

    void OnDestroy()
    {
        pauseAction?.Disable();
        pauseAction?.Dispose();

        if (isPaused)
            Time.timeScale = 1f;
    }

    void Update()
    {
        if (pauseAction.WasPressedThisFrame())
            TogglePause();
    }

    void TogglePause()
    {
        isPaused = !isPaused;

        if (pausePanel != null)
            pausePanel.SetActive(isPaused);

        Time.timeScale = isPaused ? 0f : 1f;
    }

    public static string GetControlsReferenceText()
    {
        return
            "KEYBOARD\n" +
            "Move (Smiley) ........ A / D or Arrow Keys\n" +
            "Move (Carry Cloud) ... W A S D or Arrow Keys\n" +
            "Jump ................. Space\n" +
            "Happy Star / Pickup .. Attack key (see Input Actions)\n" +
            "Switch Character ..... Y or M (Input Actions)\n" +
            "Pause ................ Escape\n\n" +
            "CONTROLLER\n" +
            "Move ................. Left Stick\n" +
            "Jump ................. A / South Button\n" +
            "Attack / Pickup ...... See Input Actions asset\n" +
            "Switch Character ..... See Input Actions asset\n" +
            "Pause ................ Start Button";
    }
}
