using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// オーガナイザーを一人で検証するための侵入スパイAI。
/// 端末1→2→3を巡り、各階を移動してから1Fの脱出地点へ向かう。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SpyBotController : MonoBehaviour
{
    private enum BotState
    {
        Inactive,
        InitialDelay,
        MovingToTerminal,
        Hacking,
        AccessBypass,
        MovingToElevator,
        MovingToExit
    }

    [SerializeField] private float employeeMoveSpeed = 2.1f;
    [SerializeField] private float hackingDuration = 5f;
    [SerializeField] private float initialDelay = 3f;
    [SerializeField] private float accessBypassDuration = 3f;
    [SerializeField] private Vector3 startPosition = new Vector3(0f, 1.05f, -11.5f);

    private NavMeshAgent agent;
    private CharacterController characterController;
    private SpyController spyController;
    private InformationFreshness informationFreshness;
    private Terminal[] terminals = Array.Empty<Terminal>();
    private ExitPoint exitPoint;
    private BotState state;
    private BotState delayedNextState;
    private int terminalIndex;
    private float stateEndsAt;
    private Vector3 sampledDestination;
    private int destinationFloor;
    private bool bypassIntoTerminal;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        characterController = GetComponent<CharacterController>();
        spyController = GetComponent<SpyController>();
        ConfigureAgent();
        agent.enabled = false;
        state = BotState.Inactive;
    }

    private void Update()
    {
        if (state == BotState.Inactive || agent == null || !agent.enabled)
            return;

        switch (state)
        {
            case BotState.InitialDelay:
                if (Time.time >= stateEndsAt)
                    ResumeDelayedState();
                break;
            case BotState.MovingToTerminal:
                if (HasArrived())
                    BeginHacking();
                break;
            case BotState.Hacking:
                if (Time.time >= stateEndsAt)
                    CompleteHacking();
                break;
            case BotState.AccessBypass:
                if (Time.time >= stateEndsAt)
                    CompleteAccessBypass();
                break;
            case BotState.MovingToElevator:
                if (HasArrived())
                    TransitToDestinationFloor();
                break;
            case BotState.MovingToExit:
                if (HasArrived())
                    CompleteEscape();
                break;
        }
    }

    public void SetBotEnabled(bool enabled)
    {
        if (enabled)
            BeginBotRun();
        else
            StopBot();
    }

    private void BeginBotRun()
    {
        if (state != BotState.Inactive)
            return;

        ResolveObjectives();
        if (terminals.Length != 3 || exitPoint == null || spyController == null)
        {
            Debug.LogError($"Spy bot objectives are incomplete: terminals={terminals.Length}, " +
                $"exit={(exitPoint != null)}, spy={(spyController != null)}.");
            return;
        }

        if (characterController != null)
            characterController.enabled = false;
        transform.position = startPosition;
        agent.enabled = true;
        ConfigureAgent();
        if (!WarpToNearestNavMesh(startPosition))
        {
            Debug.LogError("Spy bot could not find the first-floor NavMesh.");
            StopBot();
            return;
        }

        terminalIndex = 0;
        state = BotState.InitialDelay;
        delayedNextState = BotState.MovingToTerminal;
        stateEndsAt = Time.time + initialDelay;
        Debug.Log("🤖 Organizer test spy started.");
    }

    private void StopBot()
    {
        spyController?.SetSuspiciousAction(false);
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
                agent.ResetPath();
            agent.enabled = false;
        }
        if (characterController != null)
            characterController.enabled = true;
        state = BotState.Inactive;
        terminalIndex = 0;
    }

    private void ConfigureAgent()
    {
        if (agent == null)
            return;
        agent.speed = employeeMoveSpeed;
        agent.acceleration = 7f;
        agent.angularSpeed = 360f;
        agent.radius = 0.3f;
        agent.height = 1.8f;
        agent.baseOffset = 0f;
        agent.stoppingDistance = 1.4f;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    }

    private void ResolveObjectives()
    {
        terminals = FindObjectsByType<Terminal>(FindObjectsInactive.Include);
        Array.Sort(terminals, (left, right) => left.GetTerminalId().CompareTo(right.GetTerminalId()));
        exitPoint = FindAnyObjectByType<ExitPoint>(FindObjectsInactive.Include);
        informationFreshness = FindAnyObjectByType<InformationFreshness>(FindObjectsInactive.Include);
    }

    private void BeginMoveToCurrentTerminal()
    {
        if (terminalIndex < 0 || terminalIndex >= terminals.Length)
            return;
        state = BotState.MovingToTerminal;
        if (!TrySetDestination(terminals[terminalIndex].transform.position, 4f))
            BeginAccessBypass(true);
    }

    private void BeginHacking()
    {
        state = BotState.Hacking;
        stateEndsAt = Time.time + hackingDuration;
        agent.isStopped = true;
        spyController?.SetSuspiciousAction(true);
        Debug.Log($"🤖 Spy bot hacking Terminal #{terminals[terminalIndex].GetTerminalId()}.");
    }

    private void CompleteHacking()
    {
        Terminal terminal = terminals[terminalIndex];
        int terminalId = terminal.GetTerminalId();
        bool isTrap = informationFreshness != null && informationFreshness.IsTrapInformation(terminalId);
        spyController?.SetSuspiciousAction(false);
        spyController?.AddInformation(terminalId, terminal.transform.position, isTrap);
        terminalIndex++;

        destinationFloor = terminalIndex < terminals.Length
            ? GetFloorNumber(terminals[terminalIndex].transform.position)
            : 1;
        BeginMoveToElevator();
    }

    private void BeginMoveToElevator()
    {
        state = BotState.MovingToElevator;
        agent.isStopped = false;
        int currentFloor = GetFloorNumber(transform.position);
        Vector3 elevatorLobby = GetElevatorLobbyPosition(currentFloor);
        if (!TrySetDestination(elevatorLobby, 4f))
            BeginAccessBypass(false);
    }

    private void TransitToDestinationFloor()
    {
        Vector3 arrival = GetElevatorLobbyPosition(destinationFloor) + Vector3.up * 0.9f;
        agent.enabled = false;
        transform.position = arrival;
        agent.enabled = true;
        ConfigureAgent();
        if (!WarpToNearestNavMesh(arrival))
        {
            Debug.LogError($"Spy bot could not enter floor {destinationFloor} NavMesh.");
            StopBot();
            return;
        }

        if (terminalIndex < terminals.Length)
            BeginMoveToCurrentTerminal();
        else
            BeginMoveToExit();
    }

    private void BeginMoveToExit()
    {
        state = BotState.MovingToExit;
        if (!TrySetDestination(exitPoint.transform.position, 4f))
            RetryStateAfterDelay();
    }

    private void CompleteEscape()
    {
        state = BotState.Inactive;
        exitPoint.Interact(spyController);
    }

    private bool TrySetDestination(Vector3 destination, float sampleRadius)
    {
        if (!agent.enabled || !agent.isOnNavMesh ||
            !NavMesh.SamplePosition(destination, out NavMeshHit hit, sampleRadius, agent.areaMask))
            return false;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(agent.nextPosition, hit.position, agent.areaMask, path) ||
            path.status != NavMeshPathStatus.PathComplete)
            return false;
        sampledDestination = hit.position;
        agent.isStopped = false;
        return agent.SetPath(path);
    }

    private void BeginAccessBypass(bool enteringTerminal)
    {
        bypassIntoTerminal = enteringTerminal;
        state = BotState.AccessBypass;
        stateEndsAt = Time.time + accessBypassDuration;
        agent.isStopped = true;
        spyController?.SetSuspiciousAction(true);
        Debug.Log(enteringTerminal
            ? "🤖 Spy bot bypassing a restricted-room credential."
            : "🤖 Spy bot leaving a restricted NavMesh area.");
    }

    private void CompleteAccessBypass()
    {
        spyController?.SetSuspiciousAction(false);
        Vector3 target = bypassIntoTerminal
            ? terminals[terminalIndex].transform.position
            : GetElevatorLobbyPosition(GetFloorNumber(transform.position));
        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas) || !agent.Warp(hit.position))
        {
            Debug.LogError("Spy bot access bypass could not find its destination NavMesh.");
            StopBot();
            return;
        }
        sampledDestination = hit.position;
        agent.isStopped = false;
        state = bypassIntoTerminal ? BotState.MovingToTerminal : BotState.MovingToElevator;
    }

    private bool WarpToNearestNavMesh(Vector3 position)
    {
        return NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas) && agent.Warp(hit.position);
    }

    private bool HasArrived()
    {
        return !agent.pathPending &&
            (agent.remainingDistance <= agent.stoppingDistance + 0.25f ||
             Vector3.Distance(transform.position, sampledDestination) <= agent.stoppingDistance + 0.25f);
    }

    private void RetryStateAfterDelay()
    {
        delayedNextState = state;
        state = BotState.InitialDelay;
        stateEndsAt = Time.time + 1f;
    }

    private void ResumeDelayedState()
    {
        switch (delayedNextState)
        {
            case BotState.MovingToElevator:
                BeginMoveToElevator();
                break;
            case BotState.MovingToExit:
                BeginMoveToExit();
                break;
            default:
                BeginMoveToCurrentTerminal();
                break;
        }
    }

    private static int GetFloorNumber(Vector3 position)
    {
        return Mathf.Clamp(Mathf.RoundToInt(position.y / 5f) + 1, 1, 3);
    }

    private static Vector3 GetElevatorLobbyPosition(int floor)
    {
        return new Vector3(0f, (floor - 1) * 5f, -10f);
    }

    public bool ValidateSceneObjectives()
    {
        ResolveObjectives();
        return terminals.Length == 3 && terminals[0].GetTerminalId() == 1 &&
            terminals[1].GetTerminalId() == 2 && terminals[2].GetTerminalId() == 3 && exitPoint != null;
    }

    public bool IsBotActive() => state != BotState.Inactive;
    public string GetBotStateLabel() => state.ToString();

#if UNITY_EDITOR
    public void ConfigureEditorValidationMode()
    {
        employeeMoveSpeed = 18f;
        hackingDuration = 0.15f;
        initialDelay = 0.1f;
        accessBypassDuration = 0.15f;
        ConfigureAgent();
        if (state == BotState.InitialDelay)
            stateEndsAt = Time.time + initialDelay;
    }
#endif
}
