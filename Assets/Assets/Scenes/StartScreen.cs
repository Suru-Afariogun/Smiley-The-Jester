using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartScreen : MonoBehaviour
{
    const string LevelOneSceneName = "Level one";

    [SerializeField] Button startButton;
    [Tooltip("Optional backup: on the Button's On Click (), drag this object and pick StartScreen → LoadLevelOne.")]
    [SerializeField] bool wireStartButtonInCode = true;

    bool isLoadingLevel;
    InputActionMap quitInputMap;
    InputAction cancelAction;
    InputAction pauseAction;

    void Awake()
    {
        if (startButton == null)
            startButton = GetComponentInChildren<Button>(true);

        if (wireStartButtonInCode && startButton != null)
            startButton.onClick.AddListener(LoadLevelOne);

        quitInputMap = new InputActionMap("StartScreenQuit");
        cancelAction = quitInputMap.AddAction("Cancel", InputActionType.Button, "*/{Cancel}");
        pauseAction = quitInputMap.AddAction("Pause", InputActionType.Button);
        pauseAction.AddBinding("<Keyboard>/escape");
        pauseAction.AddBinding("<Gamepad>/start");
        quitInputMap.Enable();
    }

    void OnDestroy()
    {
        if (wireStartButtonInCode && startButton != null)
            startButton.onClick.RemoveListener(LoadLevelOne);

        quitInputMap?.Disable();
        quitInputMap?.Dispose();
    }

    void Update()
    {
        if (cancelAction.WasPressedThisFrame() || pauseAction.WasPressedThisFrame())
            QuitGame();
    }

    public void LoadLevelOne()
    {
        if (isLoadingLevel)
            return;

        isLoadingLevel = true;
        SceneManager.LoadScene(LevelOneSceneName);
    }

    static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE
        Application.Quit();
#endif
    }
}
