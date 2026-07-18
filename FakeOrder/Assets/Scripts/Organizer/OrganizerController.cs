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

    [Header("Trap Placement")]
    [SerializeField] private float trapPlacementDuration = 10f;

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

    private void Awake()
    {
        // 必要なコンポーネントが無ければ作成
        if (organizerCanvas == null)
            organizerCanvas = GetComponentInChildren<Canvas>();
    }

    private void Start()
    {
        InitializeUI();
        InitializeSurveillanceSystem();
    }

    private void Update()
    {
        if (!inputEnabled) return;
        HandleInput();
    }

    public void SetControlEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled)
            CancelTrapPlacement();
    }

    private void InitializeUI()
    {
        organizerUI = FindAnyObjectByType<OrganizerUI>();
        commandLog = FindAnyObjectByType<CommandLog>();
        spyUI = FindAnyObjectByType<SpyUI>();
        informationFreshness = FindAnyObjectByType<InformationFreshness>();

        if (organizerUI != null)
            organizerUI.Initialize(this);

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

        if (isPlacingTrap)
        {
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

        // ログUIの表示/非表示
        if (keyboard.lKey.wasPressedThisFrame)
            commandLog?.ToggleDisplay();

        // カメラグリッドの強調表示切替
        if (keyboard.cKey.wasPressedThisFrame)
            organizerUI?.CycleCameraView();

        // マップの拡大縮小
        // SpyControllerがWASD/矢印/Shift/E/Escapeを使うため、キーの奪い合いを避けてマウスホイールに統一する
        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.01f)
                ZoomMap(0.95f);
            else if (scroll < -0.01f)
                ZoomMap(1.05f);
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

    public void OnGameStart()
    {
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
}

public enum CommandType
{
    LeakInformation,
    SecurityMovement,
    InspectionOrder,
    EmergencyOrder
}
