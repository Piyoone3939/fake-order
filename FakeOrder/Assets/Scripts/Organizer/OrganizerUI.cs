using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// オーガナイザー側のUI管理
/// 俯瞰マップ・カメラ映像グリッド・アラート表示
/// </summary>
public class OrganizerUI : MonoBehaviour
{
    private const int maxCameraDisplays = 6;

    private Font defaultFont;

    private RawImage mapDisplay;

    private GridLayoutGroup cameraGrid;
    private CameraFeedCellUI[] cameraCells;
    private int focusedCameraIndex = -1;

    private GameObject alertPanelRoot;
    private Text alertText;

    private GameObject trapPanelRoot;
    private Text trapInstructionText;
    private Image trapProgressFill;

    private OrganizerController organizerController;

    private void Awake()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.pixelPerfect = true;

        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildMapPanel();
        BuildCameraGridPanel();
        BuildAlertPanel();
        BuildTrapPlacementPanel();
    }

    private void Update()
    {
        RefreshCameraGrid();
    }

    public void Initialize(OrganizerController controller)
    {
        organizerController = controller;

        if (mapDisplay != null)
            mapDisplay.texture = controller.GetMapRenderTexture();

        Debug.Log("✓ OrganizerUI setup complete");
    }

    // ================= UI構築 =================

    private RectTransform CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private void SetStretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    private Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor anchor, Color color)
    {
        var rt = CreateUIObject(name, parent);
        var text = rt.gameObject.AddComponent<Text>();
        text.font = defaultFont;
        text.fontSize = Mathf.RoundToInt(fontSize * 1.18f);
        text.alignment = anchor;
        text.color = color;
        text.text = content;
        return text;
    }

    private void BuildMapPanel()
    {
        var background = CreateUIObject("OrganizerBackground", transform);
        SetStretch(background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        background.gameObject.AddComponent<Image>().color = new Color(0.015f, 0.025f, 0.04f, 1f);

        var panel = CreateUIObject("MapPanel", transform);
        SetStretch(panel, new Vector2(0.02f, 0.05f), new Vector2(0.7f, 0.9f), Vector2.zero, Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        var display = CreateUIObject("MapDisplay", panel);
        SetStretch(display, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f));
        var aspect = display.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 1f;

        mapDisplay = display.gameObject.AddComponent<RawImage>();
        mapDisplay.color = Color.white;

        var controlsPanel = CreateUIObject("ControlsPanel", transform);
        SetStretch(controlsPanel, new Vector2(0.72f, 0.58f), new Vector2(0.99f, 0.9f), Vector2.zero, Vector2.zero);
        controlsPanel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

        Text controls = CreateText("Controls", controlsPanel,
            "ORGANIZER\n\n[T] 罠情報配置\n[L] 命令ログ\n[C] カメラ切替\n[ホイール] マップ拡大縮小\n[F1] ロール選択へ戻る",
            19, TextAnchor.MiddleLeft, new Color(0.82f, 0.9f, 1f));
        SetStretch(controls.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 12f), new Vector2(-12f, -12f));
    }

    private void BuildCameraGridPanel()
    {
        var panel = CreateUIObject("CameraGridPanel", transform);
        SetStretch(panel, new Vector2(0.72f, 0.05f), new Vector2(0.99f, 0.55f), Vector2.zero, Vector2.zero);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.5f);

        cameraGrid = panel.gameObject.AddComponent<GridLayoutGroup>();
        cameraGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        cameraGrid.constraintCount = 2;
        cameraGrid.cellSize = new Vector2(240, 170);
        cameraGrid.spacing = new Vector2(8, 8);
        cameraGrid.padding = new RectOffset(8, 8, 8, 8);

        cameraCells = new CameraFeedCellUI[maxCameraDisplays];
        for (int i = 0; i < maxCameraDisplays; i++)
            cameraCells[i] = CameraFeedCellUI.Create(panel, defaultFont);
    }

    private void BuildAlertPanel()
    {
        var panel = CreateUIObject("AlertPanel", transform);
        SetStretch(panel, new Vector2(0.25f, 0.92f), new Vector2(0.75f, 1f), Vector2.zero, Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0.6f, 0f, 0f, 0.6f);

        alertText = CreateText("AlertText", panel, "", 20, TextAnchor.MiddleCenter, Color.white);
        SetStretch(alertText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        alertPanelRoot = panel.gameObject;
        alertPanelRoot.SetActive(false);
    }

    private void BuildTrapPlacementPanel()
    {
        var panel = CreateUIObject("TrapPlacementPanel", transform);
        SetStretch(panel, new Vector2(0.02f, 0.05f), new Vector2(0.38f, 0.3f), Vector2.zero, Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.02f, 0.08f, 0.92f);

        trapInstructionText = CreateText("Instruction", panel, "", 20, TextAnchor.MiddleCenter, Color.white);
        SetStretch(trapInstructionText.rectTransform, new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero);

        var progressBackground = CreateUIObject("ProgressBackground", panel);
        SetStretch(progressBackground, new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.24f), Vector2.zero, Vector2.zero);
        progressBackground.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        var progressFill = CreateUIObject("ProgressFill", progressBackground);
        SetStretch(progressFill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        trapProgressFill = progressFill.gameObject.AddComponent<Image>();
        trapProgressFill.color = new Color(0.75f, 0.2f, 0.85f);
        trapProgressFill.type = Image.Type.Filled;
        trapProgressFill.fillMethod = Image.FillMethod.Horizontal;
        trapProgressFill.fillAmount = 0f;

        trapPanelRoot = panel.gameObject;
        trapPanelRoot.SetActive(false);
    }

    // ================= カメラ映像グリッド =================

    private void RefreshCameraGrid()
    {
        var surveillance = organizerController != null ? organizerController.GetSurveillanceSystem() : null;
        if (surveillance == null || cameraCells == null) return;

        int activeCount = surveillance.GetCameraCount();
        for (int i = 0; i < cameraCells.Length; i++)
        {
            bool isHighlighted = (i == focusedCameraIndex);
            if (i < activeCount)
            {
                Texture frame = surveillance.GetDelayedFrame(i);
                float timestamp = surveillance.GetDelayedFrameTimestamp(i);
                cameraCells[i].SetFeed(frame, surveillance.GetAreaInfo(i), timestamp, isHighlighted);
            }
            else
            {
                cameraCells[i].SetUnused();
            }
        }
    }

    public void CycleCameraView()
    {
        var surveillance = organizerController != null ? organizerController.GetSurveillanceSystem() : null;
        int count = surveillance != null ? surveillance.GetCameraCount() : 0;
        if (count == 0) return;

        focusedCameraIndex = (focusedCameraIndex + 1) % count;
        Debug.Log($"Camera view focus: {focusedCameraIndex}");
    }

    // ================= アラート =================

    public void ShowAlert(string message, float duration = 3f)
    {
        if (alertText == null || alertPanelRoot == null) return;

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

    public void DisplaySuspicionAlert()
    {
        ShowAlert("⚠️ 疑惑レベル上昇: スパイが検出されました", 2f);
    }

    public void DisplayCommandAlert(string message)
    {
        ShowAlert($"📋 {message}", 3f);
    }

    public void ShowTrapPlacementMenu(bool terminal1Trapped, bool terminal2Trapped, bool terminal3Trapped)
    {
        if (trapPanelRoot == null || trapInstructionText == null) return;

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
        if (trapPanelRoot == null || trapInstructionText == null) return;

        float clampedProgress = Mathf.Clamp01(progress);
        trapInstructionText.text =
            $"端末 #{terminalId} に罠情報を配置中... {clampedProgress * 100f:F0}%\n[T / Esc] キャンセル";
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
/// カメラ映像グリッドの1セル分の表示（映像・エリア名/遅延ラベル・映像時刻）
/// </summary>
public class CameraFeedCellUI : MonoBehaviour
{
    private RawImage feedImage;
    private Text labelText;
    private Text timestampText;
    private Image borderHighlight;

    public static CameraFeedCellUI Create(Transform parent, Font font)
    {
        var go = new GameObject("CameraFeedCell", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.4f);

        var cell = go.AddComponent<CameraFeedCellUI>();

        var borderRt = new GameObject("Border", typeof(RectTransform)).GetComponent<RectTransform>();
        borderRt.SetParent(go.transform, false);
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = Vector2.zero;
        borderRt.offsetMax = Vector2.zero;
        cell.borderHighlight = borderRt.gameObject.AddComponent<Image>();
        cell.borderHighlight.color = new Color(1f, 0.9f, 0.2f, 0.5f);
        cell.borderHighlight.enabled = false;

        var feedRt = new GameObject("Feed", typeof(RectTransform)).GetComponent<RectTransform>();
        feedRt.SetParent(go.transform, false);
        feedRt.anchorMin = new Vector2(0.05f, 0.25f);
        feedRt.anchorMax = new Vector2(0.95f, 0.95f);
        feedRt.offsetMin = Vector2.zero;
        feedRt.offsetMax = Vector2.zero;
        cell.feedImage = feedRt.gameObject.AddComponent<RawImage>();
        cell.feedImage.color = Color.white;

        var labelRt = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
        labelRt.SetParent(go.transform, false);
        labelRt.anchorMin = new Vector2(0f, 0.12f);
        labelRt.anchorMax = new Vector2(1f, 0.25f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        var labelText = labelRt.gameObject.AddComponent<Text>();
        labelText.font = font;
        labelText.fontSize = 16;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        cell.labelText = labelText;

        var tsRt = new GameObject("Timestamp", typeof(RectTransform)).GetComponent<RectTransform>();
        tsRt.SetParent(go.transform, false);
        tsRt.anchorMin = new Vector2(0f, 0f);
        tsRt.anchorMax = new Vector2(1f, 0.12f);
        tsRt.offsetMin = Vector2.zero;
        tsRt.offsetMax = Vector2.zero;
        var timestampText = tsRt.gameObject.AddComponent<Text>();
        timestampText.font = font;
        timestampText.fontSize = 14;
        timestampText.alignment = TextAnchor.MiddleCenter;
        timestampText.color = new Color(0.8f, 0.8f, 0.8f);
        cell.timestampText = timestampText;

        return cell;
    }

    public void SetFeed(Texture frame, string areaInfoLabel, float capturedAt, bool isHighlighted)
    {
        if (frame != null)
            feedImage.texture = frame;
        labelText.text = areaInfoLabel;
        timestampText.text = $"映像時刻 {capturedAt:F1}s (現在 {Time.time:F1}s)";
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
