using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 1Fオフィスの社員・警備員NPC。
/// NavMesh巡回と、壁で遮蔽される視界判定を担当する。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class OfficeNpcController : MonoBehaviour
{
    public enum NpcRole
    {
        Employee,
        SecurityGuard
    }

    [SerializeField] private NpcRole role;
    [SerializeField] private string npcLabel;
    [SerializeField] private int floorNumber = 1;
    [SerializeField] private Vector3[] patrolRoute;
    [SerializeField] private float visionDistance = 9f;
    [SerializeField] private float visionAngle = 75f;
    [SerializeField] private float suspicionPerSecond = 2.5f;
    [SerializeField] private float waypointWaitMin = 1.5f;
    [SerializeField] private float waypointWaitMax = 4f;

    private NavMeshAgent agent;
    private SpyController spy;
    private SuspicionGauge suspicionGauge;
    private Renderer[] visualRenderers;
    private int waypointIndex;
    private float waitUntil;
    private float awareness;
    private float pendingSuspicion;
    private float nextSuspicionReportAt;
    private bool initializedOnNavMesh;
    private bool followingForgedCommand;
    private CommandType activeCommandType;
    private Vector3 commandDestination;
    private float commandExpiresAt;

    public void Configure(NpcRole npcRole, string label, int assignedFloor, Vector3[] route)
    {
        role = npcRole;
        npcLabel = label;
        floorNumber = assignedFloor;
        patrolRoute = route;

        bool isGuard = role == NpcRole.SecurityGuard;
        visionDistance = isGuard ? 13f : 9f;
        visionAngle = isGuard ? 90f : 70f;
        suspicionPerSecond = isGuard ? 8f : 2.5f;
        waypointWaitMin = isGuard ? 0.8f : 2f;
        waypointWaitMax = isGuard ? 2f : 5f;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        CommandLog.ForgedCommandIssued += OnForgedCommandIssued;
        CommandLog.ForgedCommandInvalidated += OnForgedCommandInvalidated;
    }

    private void OnDisable()
    {
        CommandLog.ForgedCommandIssued -= OnForgedCommandIssued;
        CommandLog.ForgedCommandInvalidated -= OnForgedCommandInvalidated;
    }

    private void Start()
    {
        spy = FindAnyObjectByType<SpyController>(FindObjectsInactive.Include);
        suspicionGauge = spy != null ? spy.GetComponent<SuspicionGauge>() : null;
        visualRenderers = GetComponentsInChildren<Renderer>(true);
        InitializeOnNavMesh();
    }

    private void Update()
    {
        if (!initializedOnNavMesh)
            InitializeOnNavMesh();

        GameManager manager = GameManager.Instance;
        GameManager.GamePhase phase = manager != null ? manager.GetCurrentPhase() : GameManager.GamePhase.Playing;
        bool simulationActive = phase == GameManager.GamePhase.Preparation || phase == GameManager.GamePhase.Playing;

        if (initializedOnNavMesh)
        {
            if (!simulationActive)
                agent.isStopped = true;
            else
                UpdatePatrol();
        }

        bool canDetectSpy = phase == GameManager.GamePhase.Playing && manager != null &&
            manager.GetSelectedRole() == GameManager.LocalRole.Spy;
        UpdateSpyDetection(canDetectSpy);
    }

    private void InitializeOnNavMesh()
    {
        if (agent == null || patrolRoute == null || patrolRoute.Length == 0)
            return;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, agent.areaMask))
            return;

        agent.Warp(hit.position);
        agent.speed = role == NpcRole.SecurityGuard ? 2.8f : 2.1f;
        agent.angularSpeed = 360f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 0.25f;
        agent.autoBraking = true;
        initializedOnNavMesh = true;
        waypointIndex = 0;
        waitUntil = Time.time + Random.Range(0f, 1.5f);
    }

    private void UpdatePatrol()
    {
        if (!agent.isOnNavMesh || patrolRoute == null || patrolRoute.Length == 0)
            return;

        float stopAwareness = role == NpcRole.SecurityGuard ? 0.3f : 0.7f;
        if (awareness >= stopAwareness)
        {
            agent.isStopped = true;
            FaceSpy();
            return;
        }

        if (followingForgedCommand)
        {
            if (Time.time >= commandExpiresAt)
            {
                followingForgedCommand = false;
                agent.ResetPath();
            }
            else
            {
                agent.isStopped = false;
                if (!agent.hasPath || Vector3.Distance(agent.destination, commandDestination) > 0.2f)
                    agent.SetDestination(commandDestination);
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.25f)
                    agent.isStopped = true;
                return;
            }
        }

        if (Time.time < waitUntil)
        {
            agent.isStopped = true;
            return;
        }

        agent.isStopped = false;
        if (!agent.hasPath || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f))
        {
            if (TrySetDestination(patrolRoute[waypointIndex]))
            {
                waypointIndex = (waypointIndex + 1) % patrolRoute.Length;
                waitUntil = Time.time + Random.Range(waypointWaitMin, waypointWaitMax);
            }
            else
            {
                waypointIndex = (waypointIndex + 1) % patrolRoute.Length;
                waitUntil = Time.time + 0.5f;
            }
        }
    }

    private bool TrySetDestination(Vector3 destination)
    {
        if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 3f, agent.areaMask))
            return false;
        return agent.SetDestination(hit.position);
    }

    private void OnForgedCommandIssued(CommandType commandType, Vector3 location)
    {
        if (GetFloorNumber(location) != floorNumber || !ShouldReactTo(commandType) || !initializedOnNavMesh)
            return;
        if (!NavMesh.SamplePosition(location, out NavMeshHit hit, 4f, agent.areaMask))
            return;

        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(transform.position, hit.position, agent.areaMask, path) ||
            path.status != NavMeshPathStatus.PathComplete)
            return;

        activeCommandType = commandType;
        commandDestination = hit.position;
        commandExpiresAt = Time.time + 35f;
        followingForgedCommand = true;
        agent.ResetPath();
    }

    private void OnForgedCommandInvalidated(CommandType commandType, Vector3 location)
    {
        if (!followingForgedCommand || activeCommandType != commandType || GetFloorNumber(location) != floorNumber)
            return;
        followingForgedCommand = false;
        if (initializedOnNavMesh && agent.isOnNavMesh)
            agent.ResetPath();
    }

    private bool ShouldReactTo(CommandType commandType)
    {
        return commandType == CommandType.EmergencyOrder ||
            (commandType == CommandType.SecurityMovement && role == NpcRole.SecurityGuard) ||
            (commandType == CommandType.InspectionOrder && role == NpcRole.Employee);
    }

    private static int GetFloorNumber(Vector3 location)
    {
        return Mathf.Clamp(Mathf.RoundToInt(location.y / 5f) + 1, 1, 3);
    }

    private void UpdateSpyDetection(bool detectionEnabled)
    {
        bool visible = detectionEnabled && CanSeeSpy();
        float buildRate = role == NpcRole.SecurityGuard ? 1.7f : 0.85f;
        awareness = Mathf.MoveTowards(awareness, visible ? 1f : 0f,
            (visible ? buildRate : 1.2f) * Time.deltaTime);

        if (visible && suspicionGauge != null)
        {
            float behaviorMultiplier = 1f;
            if (spy.IsSprinting())
                behaviorMultiplier *= 1.8f;
            if (spy.IsPerformingSuspiciousAction())
                behaviorMultiplier *= 2.2f;

            pendingSuspicion += suspicionPerSecond * behaviorMultiplier * Mathf.Max(0.2f, awareness) * Time.deltaTime;
            if (Time.time >= nextSuspicionReportAt && pendingSuspicion >= 0.1f)
            {
                suspicionGauge.IncreaseSuspicion(pendingSuspicion);
                pendingSuspicion = 0f;
                nextSuspicionReportAt = Time.time + 0.5f;
            }
        }
        else if (pendingSuspicion > 0f && Time.time >= nextSuspicionReportAt)
        {
            suspicionGauge?.IncreaseSuspicion(pendingSuspicion);
            pendingSuspicion = 0f;
        }

        UpdateAlertVisual();
    }

    private bool CanSeeSpy()
    {
        if (spy == null)
            return false;

        Vector3 eye = transform.position + Vector3.up * 1.55f;
        Vector3 target = spy.transform.position + Vector3.up * 0.9f;
        Vector3 toSpy = target - eye;
        float distance = toSpy.magnitude;
        if (distance > visionDistance || distance < 0.01f)
            return false;

        Vector3 horizontalDirection = Vector3.ProjectOnPlane(toSpy, Vector3.up).normalized;
        if (Vector3.Angle(transform.forward, horizontalDirection) > visionAngle * 0.5f)
            return false;

        if (!Physics.Raycast(eye, toSpy.normalized, out RaycastHit hit, distance + 0.25f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;

        return hit.transform == spy.transform || hit.transform.IsChildOf(spy.transform);
    }

    private void FaceSpy()
    {
        if (spy == null)
            return;
        Vector3 direction = Vector3.ProjectOnPlane(spy.transform.position - transform.position, Vector3.up);
        if (direction.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 360f * Time.deltaTime);
    }

    private void UpdateAlertVisual()
    {
        if (visualRenderers == null)
            return;

        Color alertColor = role == NpcRole.SecurityGuard
            ? Color.Lerp(new Color(0.08f, 0.24f, 0.42f), new Color(1f, 0.08f, 0.04f), awareness)
            : Color.Lerp(new Color(0.68f, 0.73f, 0.78f), new Color(1f, 0.42f, 0.08f), awareness);

        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", alertColor);
        block.SetColor("_Color", alertColor);
        foreach (Renderer renderer in visualRenderers)
            renderer.SetPropertyBlock(block);
    }

    public NpcRole GetNpcRole() => role;
    public float GetAwareness() => awareness;
    public string GetNpcLabel() => npcLabel;
    public Vector3[] GetPatrolRoute() => patrolRoute;
    public int GetFloorNumber() => floorNumber;
    public bool IsFollowingForgedCommand() => followingForgedCommand;
    public bool CanReactToCommand(CommandType commandType, Vector3 location)
    {
        return GetFloorNumber(location) == floorNumber && ShouldReactTo(commandType);
    }
}
