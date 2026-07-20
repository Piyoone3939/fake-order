using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// オーガナイザー側のメインコントローラー
/// 俯瞰マップとカメラ監視管理
/// </summary>
public class OrganizerController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas organizerCanvas;

    [Header("Camera Setup")]
    [SerializeField] private Camera mapCamera;
    [SerializeField] private RenderTexture mapRenderTexture;
    [SerializeField] private Camera managementRoomCamera;
    [SerializeField] private RenderTexture managementRoomRenderTexture;

    [Header("Management Room Movement")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float moveSpeed = 3.8f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float interactionRange = 3.2f;
    [SerializeField] private float terminalDisconnectDistance = 4.5f;
    [SerializeField] private Vector3 roomStartPosition = new Vector3(56f, 0.9f, -5.2f);

    [Header("Trap Placement")]
    [SerializeField] private float trapPlacementDuration = 10f;

    [Header("Suspect Identification")]
    [SerializeField] private float identificationDuration = 3f;
    [SerializeField] private float falseIdentificationCooldown = 10f;
    [SerializeField] private int maximumIdentificationAuthority = 3;

    private OrganizerUI organizerUI;
    private CommandLog commandLog;
    private DelayedSurveillance delayedSurveillance;
    private InformationFreshness informationFreshness;
    private SpyUI spyUI;
    private bool inputEnabled;
    private bool trapMenuOpen;
    private bool isPlacingTrap;
    private int pendingTrapTerminalId;
    private float trapPlacementProgress;
    private Vector3 movementVelocity;
    private float cameraPitch;
    private Transform lookedAtTerminal;
    private Transform activeTerminal;
    private bool isIdentifyingSuspect;
    private float identificationProgress;
    private float nextIdentificationAllowedAt;
    private int identificationAuthority;
    private int identificationCameraIndex = -1;

    private void Awake()
    {
        // 必要なコンポーネントが無ければ作成
        if (organizerCanvas == null)
            organizerCanvas = GetComponentInChildren<Canvas>();
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        if (playerCamera == null)
            playerCamera = GameObject.Find("ManagementRoomCamera")?.GetComponent<Camera>();
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        InitializeSurveillanceSystem();
        InitializeUI();
        ApplyControlState();
    }

    private void Update()
    {
        if (!inputEnabled) return;

        UpdateMovement();

        if (organizerUI != null && organizerUI.IsComputerOpen)
        {
            if (activeTerminal == null || Vector3.Distance(transform.position, activeTerminal.transform.position) > terminalDisconnectDistance)
            {
                CloseComputer();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                CloseComputer();
                return;
            }

            HandleInput();
            return;
        }

        UpdateCameraLook();
        UpdateTerminalTarget();
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && lookedAtTerminal != null)
            OpenComputer(lookedAtTerminal);
    }

    public void SetControlEnabled(bool enabled)
    {
        bool isNewSession = enabled && !inputEnabled;
        inputEnabled = enabled;
        organizerUI?.SetInteractionEnabled(enabled);
        if (isNewSession)
        {
            TeleportToRoomStart();
            organizerUI?.PrepareForSession();
            ResetIdentificationSystem();
        }
        if (!enabled)
        {
            CancelTrapPlacement();
            CancelIdentification();
            CloseComputer();
            lookedAtTerminal = null;
            organizerUI?.SetRoomInteractionPrompt(string.Empty);
        }

        ApplyControlState();
    }

    private void InitializeUI()
    {
        organizerUI = FindAnyObjectByType<OrganizerUI>();
        commandLog = FindAnyObjectByType<CommandLog>();
        spyUI = FindAnyObjectByType<SpyUI>();
        informationFreshness = FindAnyObjectByType<InformationFreshness>();

        if (organizerUI != null)
        {
            organizerUI.Initialize(this);
            organizerUI.SetInteractionEnabled(inputEnabled);
            if (inputEnabled)
                organizerUI.PrepareForSession();
        }

        if (commandLog != null)
            commandLog.Initialize(this);

        Debug.Log("✓ OrganizerUI initialized");
    }

    private void InitializeSurveillanceSystem()
    {
        delayedSurveillance = GetComponent<DelayedSurveillance>();
        if (delayedSurveillance == null)
            delayedSurveillance = gameObject.AddComponent<DelayedSurveillance>();

        delayedSurveillance.Initialize();
        Debug.Log("✓ DelayedSurveillance initialized");
    }

    private void HandleInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 端末を開き、認証を完了している間だけ監視関連操作を受け付ける。
        if (organizerUI == null || !organizerUI.IsComputerOpen || !organizerUI.IsSurveillanceUnlocked)
            return;

        if (isPlacingTrap)
        {
            CancelIdentification();
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.tKey.wasPressedThisFrame)
            {
                CancelTrapPlacement();
                return;
            }

            trapPlacementProgress += Time.deltaTime / Mathf.Max(0.1f, trapPlacementDuration);
            organizerUI?.ShowTrapPlacementProgress(pendingTrapTerminalId, trapPlacementProgress);

            if (trapPlacementProgress >= 1f)
                CompleteTrapPlacement();
            return;
        }

        if (trapMenuOpen)
        {
            CancelIdentification();
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                BeginTrapPlacement(1);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                BeginTrapPlacement(2);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                BeginTrapPlacement(3);
            else if (keyboard.escapeKey.wasPressedThisFrame || keyboard.tKey.wasPressedThisFrame)
                CloseTrapMenu();
            return;
        }

        if (keyboard.tKey.wasPressedThisFrame)
        {
            OpenTrapMenu();
            return;
        }

        UpdateSuspectIdentification(keyboard);

        // ログUIの表示/非表示
        if (keyboard.lKey.wasPressedThisFrame)
            commandLog?.ToggleDisplay();

        // カメラグリッドの強調表示切替
        if (keyboard.cKey.wasPressedThisFrame)
            organizerUI?.CycleCameraView();

    }

    private void UpdateMovement()
    {
        if (characterController == null)
            return;

        var keyboard = Keyboard.current;
        float horizontal = 0f;
        float vertical = 0f;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;
        }

        Vector3 movement = (transform.right * horizontal + transform.forward * vertical).normalized * moveSpeed;
        movementVelocity.x = movement.x;
        movementVelocity.z = movement.z;
        movementVelocity.y = characterController.isGrounded ? -0.5f : movementVelocity.y - 9.81f * Time.deltaTime;
        characterController.Move(movementVelocity * Time.deltaTime);
    }

    private void UpdateCameraLook()
    {
        if (playerCamera == null || Mouse.current == null || Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity * 0.02f;
        cameraPitch = Mathf.Clamp(cameraPitch - delta.y, -85f, 85f);
        playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        transform.Rotate(Vector3.up * delta.x);
    }

    private void UpdateTerminalTarget()
    {
        lookedAtTerminal = null;
        if (playerCamera != null && Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward,
            out RaycastHit hit, interactionRange))
        {
            if (hit.collider.name == "DeskMonitorScreen")
                lookedAtTerminal = hit.collider.transform;
        }

        organizerUI?.SetRoomInteractionPrompt(lookedAtTerminal != null
            ? "[E] 監視端末を操作"
            : string.Empty);
    }

    private void OpenComputer(Transform terminal)
    {
        activeTerminal = terminal;
        organizerUI?.SetRoomInteractionPrompt(string.Empty);
        organizerUI?.OpenComputer();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseComputer()
    {
        activeTerminal = null;
        CancelTrapPlacement();
        CancelIdentification();
        commandLog?.CloseDisplay();
        organizerUI?.CloseComputer();
        if (inputEnabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void TeleportToRoomStart()
    {
        bool wasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
            characterController.enabled = false;
        transform.position = roomStartPosition;
        transform.rotation = Quaternion.identity;
        cameraPitch = 0f;
        movementVelocity = Vector3.zero;
        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.identity;
        if (characterController != null)
            characterController.enabled = wasEnabled;
    }

    private void ApplyControlState()
    {
        if (playerCamera != null)
        {
            playerCamera.enabled = inputEnabled;
            AudioListener listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = inputEnabled;
        }

        if (inputEnabled && (organizerUI == null || !organizerUI.IsComputerOpen))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OpenTrapMenu()
    {
        trapMenuOpen = true;
        organizerUI?.ShowTrapPlacementMenu(
            informationFreshness != null && informationFreshness.IsTrapInformation(1),
            informationFreshness != null && informationFreshness.IsTrapInformation(2),
            informationFreshness != null && informationFreshness.IsTrapInformation(3));
    }

    private void CloseTrapMenu()
    {
        trapMenuOpen = false;
        organizerUI?.HideTrapPlacement();
    }

    private void BeginTrapPlacement(int terminalId)
    {
        if (informationFreshness == null)
        {
            organizerUI?.ShowAlert("情報鮮度システムが見つかりません");
            CloseTrapMenu();
            return;
        }

        trapMenuOpen = false;
        isPlacingTrap = true;
        pendingTrapTerminalId = terminalId;
        trapPlacementProgress = 0f;
        organizerUI?.ShowTrapPlacementProgress(terminalId, 0f);
    }

    private void CompleteTrapPlacement()
    {
        informationFreshness.PlaceTrapInformation(
            pendingTrapTerminalId,
            $"Terminal {pendingTrapTerminalId} forged security data");

        int completedTerminalId = pendingTrapTerminalId;
        isPlacingTrap = false;
        pendingTrapTerminalId = 0;
        trapPlacementProgress = 0f;
        organizerUI?.HideTrapPlacement();
        organizerUI?.ShowAlert($"端末 #{completedTerminalId} に罠情報を配置しました", 3f);
    }

    private void CancelTrapPlacement()
    {
        bool wasOperating = trapMenuOpen || isPlacingTrap;
        trapMenuOpen = false;
        isPlacingTrap = false;
        pendingTrapTerminalId = 0;
        trapPlacementProgress = 0f;
        organizerUI?.HideTrapPlacement();

        if (wasOperating && inputEnabled)
            organizerUI?.ShowAlert("罠情報の配置をキャンセルしました", 2f);
    }

    private void ResetIdentificationSystem()
    {
        identificationAuthority = Mathf.Max(1, maximumIdentificationAuthority);
        nextIdentificationAllowedAt = 0f;
        CancelIdentification();
        organizerUI?.SetIdentificationStatus(identificationAuthority, maximumIdentificationAuthority, 0f);
    }

    private void UpdateSuspectIdentification(Keyboard keyboard)
    {
        float cooldownRemaining = Mathf.Max(0f, nextIdentificationAllowedAt - Time.time);
        bool canIdentify = GameManager.Instance != null &&
            GameManager.Instance.GetCurrentPhase() == GameManager.GamePhase.Playing &&
            identificationAuthority > 0 && cooldownRemaining <= 0f;

        if (!canIdentify || organizerUI == null || organizerUI.GetFocusedCameraIndex() < 0)
        {
            CancelIdentification();
            organizerUI?.SetIdentificationStatus(identificationAuthority, maximumIdentificationAuthority, cooldownRemaining);
            return;
        }

        if (!keyboard.rKey.isPressed)
        {
            CancelIdentification();
            organizerUI.SetIdentificationStatus(identificationAuthority, maximumIdentificationAuthority, 0f);
            return;
        }

        if (!isIdentifyingSuspect)
        {
            isIdentifyingSuspect = true;
            identificationProgress = 0f;
            identificationCameraIndex = organizerUI.GetFocusedCameraIndex();
        }

        identificationProgress += Time.deltaTime / Mathf.Max(0.1f, identificationDuration);
        organizerUI.ShowIdentificationProgress(identificationCameraIndex, identificationProgress,
            identificationAuthority, maximumIdentificationAuthority);
        if (identificationProgress >= 1f)
            CompleteIdentification();
    }

    private void CompleteIdentification()
    {
        bool spyConfirmed = delayedSurveillance != null &&
            delayedSurveillance.WasSpyPresentInDelayedFrame(identificationCameraIndex);
        float evidenceTimestamp = delayedSurveillance != null
            ? delayedSurveillance.GetDelayedFrameTimestamp(identificationCameraIndex)
            : Time.time;
        int cameraNumber = identificationCameraIndex + 1;
        CancelIdentification();

        if (spyConfirmed)
        {
            organizerUI?.ShowAlert($"身元照合成功: CAM {cameraNumber:00} / 映像時刻 {evidenceTimestamp:F1}s", 4f);
            GameManager.Instance?.EndGame(GameResult.SpyEliminated);
            return;
        }

        identificationAuthority = Mathf.Max(0, identificationAuthority - 1);
        nextIdentificationAllowedAt = Time.time + falseIdentificationCooldown;
        organizerUI?.SetIdentificationStatus(identificationAuthority, maximumIdentificationAuthority,
            falseIdentificationCooldown);
        organizerUI?.ShowAlert(
            $"誤認照合: CAM {cameraNumber:00}に対象なし / AUTHORITY -1 ({identificationAuthority}/{maximumIdentificationAuthority})",
            4f);
    }

    private void CancelIdentification()
    {
        isIdentifyingSuspect = false;
        identificationProgress = 0f;
        identificationCameraIndex = -1;
    }

    public void OnGameStart()
    {
        if (GameManager.Instance != null && GameManager.Instance.GetSelectedRole() == GameManager.LocalRole.Organizer)
            organizerUI?.ShowAlert("侵入シミュレーション開始：社員に擬態したスパイAIを追跡してください", 5f);
        Debug.Log("🔐 Organizer: Game started!");
    }

    public void ConfigureMapCamera(Camera camera, RenderTexture renderTexture)
    {
        mapCamera = camera;
        mapRenderTexture = renderTexture;

        if (mapCamera != null)
            mapCamera.targetTexture = mapRenderTexture;
    }

    public RenderTexture GetMapRenderTexture()
    {
        return mapRenderTexture;
    }

    public void ConfigureManagementRoomCamera(Camera camera, RenderTexture renderTexture)
    {
        managementRoomCamera = camera;
        managementRoomRenderTexture = renderTexture;
        playerCamera = camera;

        if (managementRoomCamera != null)
            managementRoomCamera.targetTexture = null;
    }

    public RenderTexture GetManagementRoomRenderTexture()
    {
        return managementRoomRenderTexture;
    }

    public void ZoomMap(float zoomFactor)
    {
        if (mapCamera != null)
        {
            mapCamera.orthographicSize *= zoomFactor;
            mapCamera.orthographicSize = Mathf.Clamp(mapCamera.orthographicSize, 5f, 50f);
        }
    }

    public void OnCommandInvalidated(CommandLog.CommandEntry entry)
    {
        organizerUI?.DisplayCommandAlert($"命令を無効化しました: {entry.commandType} @ {entry.locationName}");
    }

    public void OnAreaSuspicionThreshold(string areaName)
    {
        organizerUI?.ShowAlert($"⚠️ {areaName}で偽造命令が複数回検知されました");
    }

    public void NotifySpyLogAccess(bool isOpen)
    {
        spyUI?.SetOrganizerLogAccessNotice(isOpen);
    }

    public DelayedSurveillance GetSurveillanceSystem()
    {
        return delayedSurveillance;
    }

    public int GetIdentificationAuthority() => identificationAuthority;
    public bool IsIdentifyingSuspect() => isIdentifyingSuspect;

#if UNITY_EDITOR
    public void ConfigureEditorIdentificationValidation()
    {
        identificationDuration = 0.05f;
        falseIdentificationCooldown = 0.1f;
        ResetIdentificationSystem();
    }

    public float GetIdentificationCooldownRemainingForEditor()
    {
        return Mathf.Max(0f, nextIdentificationAllowedAt - Time.time);
    }

    public void ResolveIdentificationForEditor(int cameraIndex)
    {
        identificationCameraIndex = cameraIndex;
        CompleteIdentification();
    }
#endif
}

public enum CommandType
{
    LeakInformation,
    SecurityMovement,
    InspectionOrder,
    EmergencyOrder
}
