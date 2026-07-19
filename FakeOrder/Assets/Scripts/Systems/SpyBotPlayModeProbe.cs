#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>コマンドラインPlay検証専用。通常プレイでは生成されない。</summary>
public class SpyBotPlayModeProbe : MonoBehaviour
{
    private GameManager manager;
    private SpyBotController bot;
    private float deadline;

    private void Start()
    {
        manager = FindAnyObjectByType<GameManager>();
        bot = FindAnyObjectByType<SpyBotController>();
        if (manager == null || bot == null)
        {
            Finish("FAIL: Spy bot play-mode components are missing.", 1);
            return;
        }

        manager.StartImmediateEditorValidation(GameManager.LocalRole.Organizer);
        bot.ConfigureEditorValidationMode();
        deadline = Time.realtimeSinceStartup + 75f;
    }

    private void Update()
    {
        if (manager != null && manager.GetCurrentPhase() == GameManager.GamePhase.Finished)
        {
            if (manager.GetLastResult() == GameResult.SpyEscaped)
                Finish("PASS: Organizer test spy collected information and escaped.", 0);
            else
                Finish($"FAIL: Organizer test spy finished with {manager.GetLastResult()}.", 1);
        }
        else if (Time.realtimeSinceStartup >= deadline)
        {
            Finish($"FAIL: Organizer test spy timed out in state {bot?.GetBotStateLabel() ?? "missing"}.", 1);
        }
    }

    private static void Finish(string message, int exitCode)
    {
        Debug.Log(message);
        EditorApplication.Exit(exitCode);
    }
}
#endif
