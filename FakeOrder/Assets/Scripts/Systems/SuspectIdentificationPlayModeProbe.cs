#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>監視映像の誤認と正しい照合を、同一Play Mode内で確認する。</summary>
public class SuspectIdentificationPlayModeProbe : MonoBehaviour
{
    private enum ProbeState
    {
        VerifyFalseIdentification,
        WaitForCooldown,
        VerifyCorrectIdentification
    }

    private GameManager manager;
    private OrganizerController organizer;
    private DelayedSurveillance surveillance;
    private SpyController spy;
    private SpyBotController bot;
    private ProbeState state;
    private float deadline;

    private void Start()
    {
        manager = FindAnyObjectByType<GameManager>();
        organizer = FindAnyObjectByType<OrganizerController>();
        spy = FindAnyObjectByType<SpyController>();
        bot = FindAnyObjectByType<SpyBotController>();
        surveillance = organizer != null ? organizer.GetSurveillanceSystem() : null;
        if (manager == null || organizer == null || surveillance == null || spy == null ||
            surveillance.GetCameraCount() < 1)
        {
            Finish("FAIL: Identification play-mode components are missing.", 1);
            return;
        }

        manager.StartImmediateEditorValidation(GameManager.LocalRole.Organizer);
        bot?.SetBotEnabled(false);
        organizer.ConfigureEditorIdentificationValidation();
        surveillance.ConfigureEditorIdentificationValidation();
        state = ProbeState.VerifyFalseIdentification;
        deadline = Time.realtimeSinceStartup + 20f;
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup >= deadline)
        {
            Finish($"FAIL: Identification validation timed out in {state}.", 1);
            return;
        }

        switch (state)
        {
            case ProbeState.VerifyFalseIdentification:
                PlaceSpy(new Vector3(0f, 1000f, 0f));
                surveillance.CaptureAllSnapshotsForEditor();
                if (surveillance.WasSpyPresentInDelayedFrame(0))
                {
                    Finish("FAIL: Empty surveillance frame incorrectly contains the spy.", 1);
                    return;
                }
                organizer.ResolveIdentificationForEditor(0);
                if (organizer.GetIdentificationAuthority() != 2 ||
                    organizer.GetIdentificationCooldownRemainingForEditor() <= 0f ||
                    manager.GetCurrentPhase() != GameManager.GamePhase.Playing)
                {
                    Finish("FAIL: False identification did not consume one authority and start cooldown.", 1);
                    return;
                }
                Debug.Log("PASS STEP: False identification consumed one authority and kept the game active.");
                state = ProbeState.WaitForCooldown;
                break;

            case ProbeState.WaitForCooldown:
                if (organizer.GetIdentificationCooldownRemainingForEditor() <= 0f)
                    state = ProbeState.VerifyCorrectIdentification;
                break;

            case ProbeState.VerifyCorrectIdentification:
                PlaceSpy(surveillance.GetAreaCenter(0));
                surveillance.CaptureAllSnapshotsForEditor();
                if (!surveillance.WasSpyPresentInDelayedFrame(0))
                {
                    Finish("FAIL: Spy surveillance frame was not recorded as evidence.", 1);
                    return;
                }
                organizer.ResolveIdentificationForEditor(0);
                if (manager.GetCurrentPhase() != GameManager.GamePhase.Finished ||
                    manager.GetLastResult() != GameResult.SpyEliminated)
                {
                    Finish("FAIL: Correct identification did not eliminate the spy.", 1);
                    return;
                }
                Finish("PASS: False and correct suspect identification paths completed.", 0);
                break;
        }
    }

    private void PlaceSpy(Vector3 position)
    {
        NavMeshAgent agent = spy.GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.enabled = false;
        CharacterController controller = spy.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;
        spy.transform.position = position;
    }

    private static void Finish(string message, int exitCode)
    {
        Debug.Log(message);
        EditorApplication.Exit(exitCode);
    }
}
#endif
