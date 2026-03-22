using UnityEngine;

public class TitleEnterStarter : MonoBehaviour
{
    [SerializeField] private SceneLauncher launcher;

    private void Reset()
    {
        if (!launcher) launcher = FindObjectOfType<SceneLauncher>();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
        {
            launcher?.StartGame();
        }
#else
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            launcher?.StartGame();
        }
#endif
    }
}
