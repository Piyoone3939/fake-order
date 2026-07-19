using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// オーガナイザー側のUI管理。
/// 管理ルームでの認証後、監視カメラ一覧と選択映像を表示する。
/// </summary>
public class OrganizerUI : MonoBehaviour
{
    private const int maxCameraDisplays = 6;
    private const float authenticationDuration = 1.5f;

    private Font defaultFont;
    private RawImage focusedDisplay;
    private Text focusedCameraText;
    private GameObject organizerBackgroundRoot;
    private GameObject monitorPanelRoot;
    private GameObject controlsPanelRoot;
    private GameObject cameraGridPanelRoot;

    private CameraFeedCellUI[] cameraCells;
    private int focusedCameraIndex = -1;

    private GameObject authenticationPanelRoot;
    private GameObject roomHudRoot;
    private Text roomPromptText;
    private Text authenticationStatusText;
    private Text networkStatusText;
    private Image authenticationProgressFill;
    private Button authenticateButton;
    private bool interactionEnabled;
    private bool isAuthenticating;
    private float authenticationProgress;

    private GameObject alertPanelRoot;
    private Text alertText;

    private GameObject trapPanelRoot;
    private Text trapInstructionText;
    private Image trapProgressFill;

    private Text identificationStatusText;
    private Image identificationProgressFill;

    private OrganizerController organizerController;

    public bool IsSurveillanceUnlocked { get; private set; }
    public bool IsComputerOpen { get; private set; }

    private void Awake()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.pixelPerfect = true;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildMonitorPanel();
        BuildIdentificationPanel();
        BuildCameraGridPanel();
        BuildTrapPlacementPanel();
        BuildRoomHud();
        BuildAuthenticationPanel();
        BuildAlertPanel();
        ShowRoomView();
    }

    private void Update()
    {
        if (!interactionEnabled)
            return;

        if (!IsComputerOpen)
            return;

        if (!IsSurveillanceUnlocked)
        {
            if (!isAuthenticating && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                BeginAuthentication();

            if (isAuthenticating)
                UpdateAuthentication();
            return;
        }

        RefreshCameraGrid();
        RefreshFocusedDisplay();
    }

    public void Initialize(OrganizerController controller)
    {
        organizerController = controller;
        Debug.Log("✓ OrganizerUI setup complete");
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        if (authenticateButton != null)
            authenticateButton.interactable = enabled && !isAuthenticating;
        if (!enabled)
            CloseComputer();
    }

    public void PrepareForSession()
    {
        IsSurveillanceUnlocked = false;
        isAuthenticating = false;
        authenticationProgress = 0f;
        focusedCameraIndex = -1;
        IsComputerOpen = false;

        if (authenticationProgressFill != null)
            authenticationProgressFill.fillAmount = 0f;
        if (authenticationStatusText != null)
            authenticationStatusText.text = "ID TOKEN DETECTED\nENTER またはボタンで認証";
        if (networkStatusText != null)
            networkStatusText.text = "CAMERA NETWORK\n\nLOCKED\n\n監視回線への接続には\n管理者認証が必要です";
        if (authenticateButton != null)
            authenticateButton.interactable = interactionEnabled;
        SetIdentificationStatus(3, 3, 0f);

        ShowRoomView();
    }

    public void OpenComputer()
    {
        if (!interactionEnabled)
            return;

        IsComputerOpen = true;
        if (IsSurveillanceUnlocked)
        {
            ShowMonitoringView();
            if (focusedCameraIndex < 0)
                SelectCamera(0);
        }
        else
        {
            isAuthenticating = false;
            authenticationProgress = 0f;
            authenticationProgressFill.fillAmount = 0f;
            authenticationStatusText.text = "ID TOKEN DETECTED\nENTER またはボタンで認証";
            networkStatusText.text = "CAMERA NETWORK\n\nLOCKED\n\n監視回線への接続には\n管理者認証が必要です";
            authenticateButton.interactable = interactionEnabled;
            ShowAuthenticationView();
        }
    }

    public void CloseComputer()
    {
        IsComputerOpen = false;
        isAuthenticating = false;
        HideTrapPlacement();
        ShowRoomView();
    }

    public void SetRoomInteractionPrompt(string message)
    {
        if (roomPromptText != null)
            roomPromptText.text = message ?? string.Empty;
    }

    private RectTransform CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void SetStretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    private Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor anchor, Color color)
    {
        RectTransform rt = CreateUIObject(name, parent);
        Text text = rt.gameObject.AddComponent<Text>();
        text.font = defaultFont;
        text.fontSize = Mathf.RoundToInt(fontSize * 1.18f);
        text.alignment = anchor;
        text.color = color;
        text.text = content;
        text.raycastTarget = false;
        return text;
    }

    private void BuildMonitorPanel()
    {
        RectTransform background = CreateUIObject("OrganizerBackground", transform);
        SetStretch(background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        background.gameObject.AddComponent<Image>().color = new Color(0.012f, 0.025f, 0.045f, 1f);
        organizerBackgroundRoot = background.gameObject;

        RectTransform panel = CreateUIObject("FocusedCameraPanel", transform);
        SetStretch(panel, new Vector2(0.02f, 0.06f), new Vector2(0.70f, 0.92f), Vector2.zero, Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.055f, 0.10f, 0.96f);
        monitorPanelRoot = panel.gameObject;

        Text title = CreateText("FocusedTitle", panel, "SURVEILLANCE / SELECTED CAMERA", 19,
            TextAnchor.MiddleLeft, new Color(0.25f, 0.72f, 1f));
        SetStretch(title.rectTransform, new Vector2(0.025f, 0.925f), new Vector2(0.975f, 0.99f), Vector2.zero, Vector2.zero);

        RectTransform display = CreateUIObject("FocusedCameraDisplay", panel);
        SetStretch(display, new Vector2(0.025f, 0.12f), new Vector2(0.975f, 0.92f), Vector2.zero, Vector2.zero);
        AspectRatioFitter aspect = display.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 16f / 9f;
        focusedDisplay = display.gameObject.AddComponent<RawImage>();
        focusedDisplay.color = Color.white;
        focusedDisplay.raycastTarget = false;

        focusedCameraText = CreateText("FocusedCameraInfo", panel, "カメラを選択してください", 18,
            TextAnchor.MiddleLeft, new Color(0.84f, 0.93f, 1f));
        SetStretch(focusedCameraText.rectTransform, new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.11f), Vector2.zero, Vector2.zero);

        RectTransform controls = CreateUIObject("ControlsPanel", transform);
        SetStretch(controls, new Vector2(0.72f, 0.62f), new Vector2(0.99f, 0.92f), Vector2.zero, Vector2.zero);
        controls.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.05f, 0.09f, 0.96f);
        controlsPanelRoot = controls.gameObject;

        Text controlsText = CreateText("Controls", controls,
            "ORGANIZER / CAMERA SYSTEM\n\n[クリック] カメラ選択\n[C] 次のカメラ\n[R長押し] 容疑者照合・排除\n[T] 罠情報配置\n[L] 命令ログ\n[E / Esc] 端末から離れる\n[F1] ロール選択へ戻る",
            16, TextAnchor.MiddleLeft, new Color(0.82f, 0.92f, 1f));
        SetStretch(controlsText.rectTransform, new Vector2(0f, 0.28f), Vector2.one,
            new Vector2(24f, 4f), new Vector2(-12f, -8f));
    }

    private void BuildIdentificationPanel()
    {
        if (controlsPanelRoot == null)
            return;

        RectTransform panel = CreateUIObject("IdentificationPanel", controlsPanelRoot.transform);
        SetStretch(panel, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.27f), Vector2.zero, Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.12f, 0.19f, 0.96f);

        identificationStatusText = CreateText("IdentificationStatus", panel,
            "IDENTIFICATION AUTHORITY  3 / 3\n[R長押し] 選択映像を照合", 13,
            TextAnchor.MiddleCenter, new Color(0.58f, 0.86f, 1f));
        SetStretch(identificationStatusText.rectTransform, new Vector2(0.03f, 0.28f),
            new Vector2(0.97f, 0.98f), Vector2.zero, Vector2.zero);

        RectTransform progressBackground = CreateUIObject("IdentificationProgressBackground", panel);
        SetStretch(progressBackground, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.24f), Vector2.zero, Vector2.zero);
        progressBackground.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.14f);
        RectTransform progressFill = CreateUIObject("IdentificationProgressFill", progressBackground);
        SetStretch(progressFill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        identificationProgressFill = progressFill.gameObject.AddComponent<Image>();
        identificationProgressFill.color = new Color(0.04f, 0.62f, 1f);
        identificationProgressFill.type = Image.Type.Filled;
        identificationProgressFill.fillMethod = Image.FillMethod.Horizontal;
        identificationProgressFill.fillAmount = 0f;
    }

    private void BuildRoomHud()
    {
        RectTransform hud = CreateUIObject("ManagementRoomHUD", transform);
        SetStretch(hud, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        roomHudRoot = hud.gameObject;

        Text crosshair = CreateText("Crosshair", hud, "+", 22, TextAnchor.MiddleCenter, new Color(0.55f, 0.84f, 1f, 0.9f));
        SetStretch(crosshair.rectTransform, new Vector2(0.48f, 0.46f), new Vector2(0.52f, 0.54f), Vector2.zero, Vector2.zero);

        roomPromptText = CreateText("RoomInteractionPrompt", hud, string.Empty, 20,
            TextAnchor.MiddleCenter, Color.white);
        SetStretch(roomPromptText.rectTransform, new Vector2(0.25f, 0.08f), new Vector2(0.75f, 0.17f), Vector2.zero, Vector2.zero);

        Text help = CreateText("RoomControls", hud, "WASD: 移動   マウス: 視点   E: 操作   F1: ロール選択", 15,
            TextAnchor.MiddleRight, new Color(0.75f, 0.86f, 0.95f, 0.8f));
        SetStretch(help.rectTransform, new Vector2(0.52f, 0.01f), new Vector2(0.98f, 0.07f), Vector2.zero, Vector2.zero);
    }

    private void BuildCameraGridPanel()
    {
        RectTransform panel = CreateUIObject("CameraGridPanel", transform);
        SetStretch(panel, new Vector2(0.72f, 0.06f), new Vector2(0.99f, 0.59f), Vector2.zero, Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0.025f, 0.055f, 0.95f);
        cameraGridPanelRoot = panel.gameObject;

        GridLayoutGroup grid = panel.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.cellSize = new Vector2(240, 170);
        grid.spacing = new Vector2(8, 8);
        grid.padding = new RectOffset(8, 8, 8, 8);

        cameraCells = new CameraFeedCellUI[maxCameraDisplays];
        for (int i = 0; i < maxCameraDisplays; i++)
            cameraCells[i] = CameraFeedCellUI.Create(panel, defaultFont, i, SelectCamera);
    }

    private void BuildAuthenticationPanel()
    {
        RectTransform panel = CreateUIObject("ManagementRoomAuthentication", transform);
        SetStretch(panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        authenticationPanelRoot = panel.gameObject;

        panel.gameObject.AddComponent<Image>().color = new Color(0.005f, 0.025f, 0.055f, 1f);

        RectTransform tint = CreateUIObject("RoomTint", panel);
        SetStretch(tint, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        tint.gameObject.AddComponent<Image>().color = new Color(0.005f, 0.025f, 0.07f, 0.28f);

        RectTransform header = CreateUIObject("Header", panel);
        SetStretch(header, new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero);
        header.gameObject.AddComponent<Image>().color = new Color(0.015f, 0.07f, 0.13f, 0.92f);
        Text headerText = CreateText("HeaderText", header,
            "FAKE ORDER  /  SURVEILLANCE OPERATIONS ROOM", 27, TextAnchor.MiddleLeft, Color.white);
        SetStretch(headerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(30f, 0f), new Vector2(-20f, 0f));

        RectTransform authCard = CreateUIObject("AuthenticationTerminal", panel);
        SetStretch(authCard, new Vector2(0.18f, 0.16f), new Vector2(0.62f, 0.70f), Vector2.zero, Vector2.zero);
        authCard.gameObject.AddComponent<Image>().color = new Color(0.008f, 0.045f, 0.09f, 0.96f);

        Text authTitle = CreateText("AuthTitle", authCard, "SECURE AUTHENTICATION", 25,
            TextAnchor.MiddleCenter, new Color(0.28f, 0.75f, 1f));
        SetStretch(authTitle.rectTransform, new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero);

        authenticationStatusText = CreateText("AuthStatus", authCard,
            "ID TOKEN DETECTED\nENTER またはボタンで認証", 20, TextAnchor.MiddleCenter, Color.white);
        SetStretch(authenticationStatusText.rectTransform, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.76f), Vector2.zero, Vector2.zero);

        RectTransform progressBackground = CreateUIObject("AuthProgressBackground", authCard);
        SetStretch(progressBackground, new Vector2(0.12f, 0.31f), new Vector2(0.88f, 0.37f), Vector2.zero, Vector2.zero);
        progressBackground.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.14f);
        RectTransform progressFill = CreateUIObject("AuthProgressFill", progressBackground);
        SetStretch(progressFill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        authenticationProgressFill = progressFill.gameObject.AddComponent<Image>();
        authenticationProgressFill.color = new Color(0.02f, 0.48f, 1f);
        authenticationProgressFill.type = Image.Type.Filled;
        authenticationProgressFill.fillMethod = Image.FillMethod.Horizontal;
        authenticationProgressFill.fillAmount = 0f;

        RectTransform buttonRt = CreateUIObject("AuthenticateButton", authCard);
        SetStretch(buttonRt, new Vector2(0.20f, 0.10f), new Vector2(0.80f, 0.25f), Vector2.zero, Vector2.zero);
        Image buttonImage = buttonRt.gameObject.AddComponent<Image>();
        buttonImage.color = new Color(0.02f, 0.37f, 0.92f, 1f);
        authenticateButton = buttonRt.gameObject.AddComponent<Button>();
        authenticateButton.targetGraphic = buttonImage;
        authenticateButton.onClick.AddListener(BeginAuthentication);
        Text buttonText = CreateText("Label", buttonRt, "AUTHENTICATE", 20, TextAnchor.MiddleCenter, Color.white);
        SetStretch(buttonText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform statusCard = CreateUIObject("NetworkStatus", panel);
        SetStretch(statusCard, new Vector2(0.67f, 0.22f), new Vector2(0.92f, 0.70f), Vector2.zero, Vector2.zero);
        statusCard.gameObject.AddComponent<Image>().color = new Color(0.008f, 0.045f, 0.09f, 0.94f);
        networkStatusText = CreateText("NetworkStatusText", statusCard,
            "CAMERA NETWORK\n\nLOCKED\n\n監視回線への接続には\n管理者認証が必要です", 20,
            TextAnchor.MiddleCenter, new Color(0.55f, 0.78f, 1f));
        SetStretch(networkStatusText.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);
    }

    private void BeginAuthentication()
    {
        if (!interactionEnabled || !IsComputerOpen || IsSurveillanceUnlocked || isAuthenticating)
            return;

        isAuthenticating = true;
        authenticationProgress = 0f;
        authenticationStatusText.text = "AUTH-SCAN IN PROGRESS...\n認証情報を照合しています";
        networkStatusText.text = "CAMERA NETWORK\n\nVERIFYING\n\nSECURE CHANNEL\nINITIALIZING";
        authenticateButton.interactable = false;
    }

    private void UpdateAuthentication()
    {
        authenticationProgress += Time.unscaledDeltaTime / authenticationDuration;
        authenticationProgressFill.fillAmount = Mathf.Clamp01(authenticationProgress);
        if (authenticationProgress < 1f)
            return;

        isAuthenticating = false;
        IsSurveillanceUnlocked = true;
        authenticationStatusText.text = "AUTHORIZED";
        networkStatusText.text = "CAMERA NETWORK\n\nONLINE\n\nACCESS GRANTED";
        ShowMonitoringView();
        SelectCamera(0);
        ShowAlert("監視カメラシステムのロックを解除しました", 2.5f);
    }

    private void ShowAuthenticationView()
    {
        if (organizerBackgroundRoot != null) organizerBackgroundRoot.SetActive(false);
        if (authenticationPanelRoot != null) authenticationPanelRoot.SetActive(true);
        if (roomHudRoot != null) roomHudRoot.SetActive(false);
        if (monitorPanelRoot != null) monitorPanelRoot.SetActive(false);
        if (controlsPanelRoot != null) controlsPanelRoot.SetActive(false);
        if (cameraGridPanelRoot != null) cameraGridPanelRoot.SetActive(false);
        HideTrapPlacement();
    }

    private void ShowMonitoringView()
    {
        if (organizerBackgroundRoot != null) organizerBackgroundRoot.SetActive(true);
        if (authenticationPanelRoot != null) authenticationPanelRoot.SetActive(false);
        if (roomHudRoot != null) roomHudRoot.SetActive(false);
        if (monitorPanelRoot != null) monitorPanelRoot.SetActive(true);
        if (controlsPanelRoot != null) controlsPanelRoot.SetActive(true);
        if (cameraGridPanelRoot != null) cameraGridPanelRoot.SetActive(true);
        RefreshCameraGrid();
    }

    private void ShowRoomView()
    {
        if (organizerBackgroundRoot != null) organizerBackgroundRoot.SetActive(false);
        if (authenticationPanelRoot != null) authenticationPanelRoot.SetActive(false);
        if (monitorPanelRoot != null) monitorPanelRoot.SetActive(false);
        if (controlsPanelRoot != null) controlsPanelRoot.SetActive(false);
        if (cameraGridPanelRoot != null) cameraGridPanelRoot.SetActive(false);
        if (roomHudRoot != null) roomHudRoot.SetActive(true);
        if (alertPanelRoot != null) alertPanelRoot.SetActive(false);
    }

    private void RefreshCameraGrid()
    {
        DelayedSurveillance surveillance = organizerController != null ? organizerController.GetSurveillanceSystem() : null;
        if (surveillance == null || cameraCells == null)
            return;

        int activeCount = surveillance.GetCameraCount();
        for (int i = 0; i < cameraCells.Length; i++)
        {
            if (i < activeCount)
            {
                cameraCells[i].SetFeed(surveillance.GetDelayedFrame(i), surveillance.GetAreaInfo(i),
                    surveillance.GetDelayedFrameTimestamp(i), i == focusedCameraIndex);
            }
            else
            {
                cameraCells[i].SetUnused();
            }
        }
    }

    private void RefreshFocusedDisplay()
    {
        DelayedSurveillance surveillance = organizerController != null ? organizerController.GetSurveillanceSystem() : null;
        if (surveillance == null || focusedCameraIndex < 0 || focusedCameraIndex >= surveillance.GetCameraCount())
            return;

        focusedDisplay.texture = surveillance.GetDelayedFrame(focusedCameraIndex);
        float capturedAt = surveillance.GetDelayedFrameTimestamp(focusedCameraIndex);
        focusedCameraText.text = $"CAM {focusedCameraIndex + 1:00}  /  {surveillance.GetAreaInfo(focusedCameraIndex)}     映像時刻 {capturedAt:F1}s";
    }

    public void SelectCamera(int cameraIndex)
    {
        if (!IsSurveillanceUnlocked)
            return;

        DelayedSurveillance surveillance = organizerController != null ? organizerController.GetSurveillanceSystem() : null;
        int count = surveillance != null ? surveillance.GetCameraCount() : 0;
        if (cameraIndex < 0 || cameraIndex >= count)
            return;

        focusedCameraIndex = cameraIndex;
        RefreshCameraGrid();
        RefreshFocusedDisplay();
        Debug.Log($"Camera view focus: {focusedCameraIndex}");
    }

    public int GetFocusedCameraIndex() => focusedCameraIndex;

    public void ShowIdentificationProgress(int cameraIndex, float progress, int authority, int maximumAuthority)
    {
        if (identificationStatusText == null || identificationProgressFill == null)
            return;
        float clamped = Mathf.Clamp01(progress);
        identificationStatusText.text =
            $"CAM {cameraIndex + 1:00} IDENTITY SCAN  {clamped * 100f:F0}%\nAUTHORITY  {authority} / {maximumAuthority}  /  Rを離すと中止";
        identificationProgressFill.fillAmount = clamped;
    }

    public void SetIdentificationStatus(int authority, int maximumAuthority, float cooldownRemaining)
    {
        if (identificationStatusText == null || identificationProgressFill == null)
            return;
        identificationProgressFill.fillAmount = 0f;
        if (authority <= 0)
            identificationStatusText.text = "IDENTIFICATION AUTHORITY  0 / 3\n照合権限を停止しました";
        else if (cooldownRemaining > 0f)
            identificationStatusText.text =
                $"IDENTIFICATION AUTHORITY  {authority} / {maximumAuthority}\n再照合まで {cooldownRemaining:F1} 秒";
        else
            identificationStatusText.text =
                $"IDENTIFICATION AUTHORITY  {authority} / {maximumAuthority}\n[R長押し] 選択映像を照合";
    }

    public void CycleCameraView()
    {
        if (!IsSurveillanceUnlocked)
            return;

        DelayedSurveillance surveillance = organizerController != null ? organizerController.GetSurveillanceSystem() : null;
        int count = surveillance != null ? surveillance.GetCameraCount() : 0;
        if (count == 0)
            return;

        SelectCamera((focusedCameraIndex + 1) % count);
    }

    private void BuildAlertPanel()
    {
        RectTransform panel = CreateUIObject("AlertPanel", transform);
        SetStretch(panel, new Vector2(0.25f, 0.92f), new Vector2(0.75f, 1f), Vector2.zero, Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.30f, 0.66f, 0.92f);

        alertText = CreateText("AlertText", panel, "", 20, TextAnchor.MiddleCenter, Color.white);
        SetStretch(alertText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        alertPanelRoot = panel.gameObject;
        alertPanelRoot.SetActive(false);
    }

    private void BuildTrapPlacementPanel()
    {
        RectTransform panel = CreateUIObject("TrapPlacementPanel", transform);
        SetStretch(panel, new Vector2(0.02f, 0.05f), new Vector2(0.38f, 0.3f), Vector2.zero, Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.02f, 0.08f, 0.92f);

        trapInstructionText = CreateText("Instruction", panel, "", 20, TextAnchor.MiddleCenter, Color.white);
        SetStretch(trapInstructionText.rectTransform, new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero);

        RectTransform progressBackground = CreateUIObject("ProgressBackground", panel);
        SetStretch(progressBackground, new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.24f), Vector2.zero, Vector2.zero);
        progressBackground.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        RectTransform progressFill = CreateUIObject("ProgressFill", progressBackground);
        SetStretch(progressFill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        trapProgressFill = progressFill.gameObject.AddComponent<Image>();
        trapProgressFill.color = new Color(0.75f, 0.2f, 0.85f);
        trapProgressFill.type = Image.Type.Filled;
        trapProgressFill.fillMethod = Image.FillMethod.Horizontal;
        trapProgressFill.fillAmount = 0f;

        trapPanelRoot = panel.gameObject;
        trapPanelRoot.SetActive(false);
    }

    public void ShowAlert(string message, float duration = 3f)
    {
        if (alertText == null || alertPanelRoot == null)
            return;

        alertText.text = message;
        alertPanelRoot.SetActive(true);
        CancelInvoke(nameof(HideAlert));
        Invoke(nameof(HideAlert), duration);
    }

    private void HideAlert()
    {
        if (alertPanelRoot != null)
            alertPanelRoot.SetActive(false);
    }

    public void DisplaySuspicionAlert() => ShowAlert("⚠️ 疑惑レベル上昇: スパイが検出されました", 2f);

    public void DisplayCommandAlert(string message) => ShowAlert($"📋 {message}", 3f);

    public void ShowTrapPlacementMenu(bool terminal1Trapped, bool terminal2Trapped, bool terminal3Trapped)
    {
        if (trapPanelRoot == null || trapInstructionText == null)
            return;

        string Mark(bool trapped) => trapped ? "（配置済み）" : "";
        trapInstructionText.text =
            "罠情報を配置する端末を選択\n" +
            $"[1] 端末 #1 {Mark(terminal1Trapped)}   " +
            $"[2] 端末 #2 {Mark(terminal2Trapped)}   " +
            $"[3] 端末 #3 {Mark(terminal3Trapped)}\n" +
            "[T / Esc] 閉じる";
        trapProgressFill.fillAmount = 0f;
        trapPanelRoot.SetActive(true);
    }

    public void ShowTrapPlacementProgress(int terminalId, float progress)
    {
        if (trapPanelRoot == null || trapInstructionText == null)
            return;

        float clampedProgress = Mathf.Clamp01(progress);
        trapInstructionText.text = $"端末 #{terminalId} に罠情報を配置中... {clampedProgress * 100f:F0}%\n[T / Esc] キャンセル";
        trapProgressFill.fillAmount = clampedProgress;
        trapPanelRoot.SetActive(true);
    }

    public void HideTrapPlacement()
    {
        if (trapPanelRoot != null)
            trapPanelRoot.SetActive(false);
        if (trapProgressFill != null)
            trapProgressFill.fillAmount = 0f;
    }
}

/// <summary>
/// カメラ映像グリッドの1セル分。セル全体をクリックしてメイン映像を選択できる。
/// </summary>
public class CameraFeedCellUI : MonoBehaviour
{
    private RawImage feedImage;
    private Text labelText;
    private Text timestampText;
    private Image borderHighlight;

    public static CameraFeedCellUI Create(Transform parent, Font font, int cameraIndex, Action<int> onSelected)
    {
        var go = new GameObject($"CameraFeedCell_{cameraIndex + 1}", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image background = go.AddComponent<Image>();
        background.color = new Color(0f, 0.02f, 0.04f, 0.92f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() => onSelected?.Invoke(cameraIndex));

        var cell = go.AddComponent<CameraFeedCellUI>();

        RectTransform borderRt = new GameObject("Border", typeof(RectTransform)).GetComponent<RectTransform>();
        borderRt.SetParent(go.transform, false);
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = Vector2.zero;
        borderRt.offsetMax = Vector2.zero;
        cell.borderHighlight = borderRt.gameObject.AddComponent<Image>();
        cell.borderHighlight.color = new Color(0.05f, 0.58f, 1f, 0.55f);
        cell.borderHighlight.enabled = false;
        cell.borderHighlight.raycastTarget = false;

        RectTransform feedRt = new GameObject("Feed", typeof(RectTransform)).GetComponent<RectTransform>();
        feedRt.SetParent(go.transform, false);
        feedRt.anchorMin = new Vector2(0.05f, 0.25f);
        feedRt.anchorMax = new Vector2(0.95f, 0.95f);
        feedRt.offsetMin = Vector2.zero;
        feedRt.offsetMax = Vector2.zero;
        cell.feedImage = feedRt.gameObject.AddComponent<RawImage>();
        cell.feedImage.color = Color.white;
        cell.feedImage.raycastTarget = false;

        RectTransform labelRt = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
        labelRt.SetParent(go.transform, false);
        labelRt.anchorMin = new Vector2(0f, 0.12f);
        labelRt.anchorMax = new Vector2(1f, 0.25f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        cell.labelText = labelRt.gameObject.AddComponent<Text>();
        cell.labelText.font = font;
        cell.labelText.fontSize = 16;
        cell.labelText.alignment = TextAnchor.MiddleCenter;
        cell.labelText.color = Color.white;
        cell.labelText.raycastTarget = false;

        RectTransform tsRt = new GameObject("Timestamp", typeof(RectTransform)).GetComponent<RectTransform>();
        tsRt.SetParent(go.transform, false);
        tsRt.anchorMin = new Vector2(0f, 0f);
        tsRt.anchorMax = new Vector2(1f, 0.12f);
        tsRt.offsetMin = Vector2.zero;
        tsRt.offsetMax = Vector2.zero;
        cell.timestampText = tsRt.gameObject.AddComponent<Text>();
        cell.timestampText.font = font;
        cell.timestampText.fontSize = 14;
        cell.timestampText.alignment = TextAnchor.MiddleCenter;
        cell.timestampText.color = new Color(0.7f, 0.82f, 0.92f);
        cell.timestampText.raycastTarget = false;

        return cell;
    }

    public void SetFeed(Texture frame, string areaInfoLabel, float capturedAt, bool isHighlighted)
    {
        feedImage.texture = frame;
        labelText.text = areaInfoLabel;
        timestampText.text = $"映像時刻 {capturedAt:F1}s";
        borderHighlight.enabled = isHighlighted;
    }

    public void SetUnused()
    {
        feedImage.texture = null;
        labelText.text = "（未使用）";
        timestampText.text = "";
        borderHighlight.enabled = false;
    }
}
