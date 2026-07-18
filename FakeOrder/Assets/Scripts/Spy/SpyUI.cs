using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// スパイ側のHUDを管理
/// 情報ステータス・疑惑ゲージ・ミニマップ・インタラクションプロンプトを構築して表示
/// </summary>
public class SpyUI : MonoBehaviour
{
    // GamePrototypeSetup.CreateFacilityLayout() の壁配置（X/Z: -10〜10）と対応させている。
    // 施設レイアウトを変更した場合はここも合わせて更新すること。
    private static readonly Vector2 FacilityWorldMin = new Vector2(-10f, -10f);
    private static readonly Vector2 FacilityWorldMax = new Vector2(10f, 10f);

    private Font defaultFont;

    private Text informationStatusText;
    private Transform informationContainer;
    private List<InformationUIItem> informationItems = new List<InformationUIItem>();

    private Image suspicionGaugeImage;
    private Text suspicionPercentText;

    private RectTransform minimapContent;
    private Image playerMarker;
    private Dictionary<int, Image> terminalMarkers = new Dictionary<int, Image>();

    private GameObject interactionPromptPanel;
    private Text interactionPromptText;
    private GameObject hackingProgressPanel;
    private Image hackingProgressFill;
    private Text hackingProgressLabel;

    private GameObject logAccessNoticePanel;
    private Text logAccessNoticeText;

    private GameObject terminalMenuPanel;
    private GameObject transientMessagePanel;
    private Text transientMessageText;

    private SpyController spyController;
    private float currentSuspicion = 0f;
    private const float maxSuspicion = 100f;
    private float freshnessRefreshTimer = 0f;

    private void Awake()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.pixelPerfect = true;

        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildInformationPanel();
        BuildSuspicionPanel();
        BuildMinimap();
        BuildInteractionPrompt();
        BuildLogAccessNotice();
        BuildTerminalActionMenu();
        BuildTransientMessage();
    }

    private void Start()
    {
        spyController = FindAnyObjectByType<SpyController>();

        UpdateInformationDisplay();
        UpdateSuspicionDisplay();
        ClearInteractionPrompt();
        ShowHackingProgress(false);
    }

    private void Update()
    {
        UpdateSuspicionDisplay();
        UpdateMinimapPlayerMarker();

        freshnessRefreshTimer += Time.deltaTime;
        if (freshnessRefreshTimer >= 0.5f)
        {
            freshnessRefreshTimer = 0f;
            RefreshFreshnessColors();
        }
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

    private Image CreateImage(string name, Transform parent, Color color)
    {
        var rt = CreateUIObject(name, parent);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private void BuildInformationPanel()
    {
        var panel = CreateUIObject("InformationPanel", transform);
        panel.anchorMin = new Vector2(0, 1);
        panel.anchorMax = new Vector2(1, 1);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0, -10);
        panel.sizeDelta = new Vector2(-40, 60);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.5f);

        informationStatusText = CreateText("StatusText", panel, "情報収集: 0/3", 22, TextAnchor.MiddleLeft, Color.white);
        SetStretch(informationStatusText.rectTransform, new Vector2(0, 0), new Vector2(0.3f, 1), new Vector2(15, 0), new Vector2(0, 0));

        var containerRt = CreateUIObject("InformationContainer", panel);
        SetStretch(containerRt, new Vector2(0.3f, 0), new Vector2(1, 1), new Vector2(0, 6), new Vector2(-15, -6));
        var hlg = containerRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        informationContainer = containerRt;
    }

    private void BuildSuspicionPanel()
    {
        var panel = CreateUIObject("SuspicionPanel", transform);
        panel.anchorMin = new Vector2(1, 1);
        panel.anchorMax = new Vector2(1, 1);
        panel.pivot = new Vector2(1, 1);
        panel.anchoredPosition = new Vector2(-10, -80);
        panel.sizeDelta = new Vector2(220, 50);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.5f);

        var label = CreateText("Label", panel, "疑惑ゲージ", 14, TextAnchor.UpperCenter, Color.white);
        SetStretch(label.rectTransform, new Vector2(0, 0.6f), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, -2));

        var barBg = CreateImage("BarBackground", panel, new Color(0.15f, 0.15f, 0.15f, 1f));
        SetStretch(barBg.rectTransform, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.55f), Vector2.zero, Vector2.zero);

        suspicionGaugeImage = CreateImage("Fill", barBg.rectTransform, Color.yellow);
        suspicionGaugeImage.type = Image.Type.Filled;
        suspicionGaugeImage.fillMethod = Image.FillMethod.Horizontal;
        suspicionGaugeImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        suspicionGaugeImage.fillAmount = 0f;
        SetStretch(suspicionGaugeImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        suspicionPercentText = CreateText("PercentText", barBg.rectTransform, "0%", 14, TextAnchor.MiddleCenter, Color.white);
        SetStretch(suspicionPercentText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private void BuildMinimap()
    {
        var panel = CreateUIObject("MinimapPanel", transform);
        panel.anchorMin = new Vector2(0, 0);
        panel.anchorMax = new Vector2(0, 0);
        panel.pivot = new Vector2(0, 0);
        panel.anchoredPosition = new Vector2(10, 10);
        panel.sizeDelta = new Vector2(200, 200);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        minimapContent = CreateUIObject("MinimapContent", panel);
        SetStretch(minimapContent, new Vector2(0, 0), new Vector2(1, 1), new Vector2(8, 8), new Vector2(-8, -8));

        playerMarker = CreateImage("PlayerMarker", minimapContent, Color.green);
        playerMarker.rectTransform.sizeDelta = new Vector2(10, 10);
        playerMarker.rectTransform.anchorMin = playerMarker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        playerMarker.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void BuildInteractionPrompt()
    {
        var anchor = CreateUIObject("InteractionAnchor", transform);
        anchor.anchorMin = new Vector2(0.5f, 0);
        anchor.anchorMax = new Vector2(0.5f, 0);
        anchor.pivot = new Vector2(0.5f, 0);
        anchor.anchoredPosition = new Vector2(0, 120);
        anchor.sizeDelta = new Vector2(500, 70);

        // プロンプト
        interactionPromptPanel = CreateUIObject("InteractionPrompt", anchor).gameObject;
        var promptRt = interactionPromptPanel.GetComponent<RectTransform>();
        SetStretch(promptRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var promptBg = interactionPromptPanel.AddComponent<Image>();
        promptBg.color = new Color(0f, 0f, 0f, 0.6f);
        interactionPromptText = CreateText("PromptText", promptRt, "", 22, TextAnchor.MiddleCenter, Color.white);
        SetStretch(interactionPromptText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ハッキング進捗バー（同じ枠を共有。Terminalがハッキング中はプロンプトを返さないため同時表示はされない）
        hackingProgressPanel = CreateUIObject("HackingProgress", anchor).gameObject;
        var progressRt = hackingProgressPanel.GetComponent<RectTransform>();
        SetStretch(progressRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var progressBg = hackingProgressPanel.AddComponent<Image>();
        progressBg.color = new Color(0f, 0f, 0f, 0.6f);

        hackingProgressLabel = CreateText("Label", progressRt, "", 18, TextAnchor.UpperCenter, Color.white);
        SetStretch(hackingProgressLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, -4));

        var barBg = CreateImage("BarBackground", progressRt, new Color(0.15f, 0.15f, 0.15f, 1f));
        SetStretch(barBg.rectTransform, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.45f), Vector2.zero, Vector2.zero);

        hackingProgressFill = CreateImage("Fill", barBg.rectTransform, new Color(0.2f, 0.6f, 1f, 1f));
        hackingProgressFill.type = Image.Type.Filled;
        hackingProgressFill.fillMethod = Image.FillMethod.Horizontal;
        hackingProgressFill.fillAmount = 0f;
        SetStretch(hackingProgressFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        interactionPromptPanel.SetActive(false);
        hackingProgressPanel.SetActive(false);
    }

    private void BuildLogAccessNotice()
    {
        var panel = CreateUIObject("LogAccessNotice", transform);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0, -80);
        panel.sizeDelta = new Vector2(420, 40);
        panel.gameObject.AddComponent<Image>().color = new Color(0.6f, 0f, 0f, 0.7f);

        logAccessNoticeText = CreateText("Text", panel, "⚠ オーガナイザーが命令ログを確認中", 18, TextAnchor.MiddleCenter, Color.white);
        SetStretch(logAccessNoticeText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        logAccessNoticePanel = panel.gameObject;
        logAccessNoticePanel.SetActive(false);
    }

    public void SetOrganizerLogAccessNotice(bool isActive)
    {
        if (logAccessNoticePanel != null)
            logAccessNoticePanel.SetActive(isActive);
    }

    private void BuildTerminalActionMenu()
    {
        // 前回CommandLogパネルが固定sizeDelta+中央ピボットで画面アスペクト比により上下が
        // 見切れるバグを起こしたため、ここでも必ず%アンカー(SetStretch)で組む。
        var panel = CreateUIObject("TerminalActionMenu", transform);
        SetStretch(panel, new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f), Vector2.zero, Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

        var text = CreateText("MenuText", panel,
            "端末操作を選択\n[1] 情報収集\n[2] 命令書偽造：情報漏洩\n[3] 命令書偽造：警備移動命令\n[4] 命令書偽造：点検命令\n[5] 命令書偽造：緊急命令\n[E] キャンセル",
            20, TextAnchor.MiddleCenter, Color.white);
        SetStretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        terminalMenuPanel = panel.gameObject;
        terminalMenuPanel.SetActive(false);
    }

    public void ShowTerminalActionMenu()
    {
        if (terminalMenuPanel != null)
            terminalMenuPanel.SetActive(true);
    }

    public void HideTerminalActionMenu()
    {
        if (terminalMenuPanel != null)
            terminalMenuPanel.SetActive(false);
    }

    private void BuildTransientMessage()
    {
        var panel = CreateUIObject("TransientMessage", transform);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0, 200);
        panel.sizeDelta = new Vector2(500, 50);
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        transientMessageText = CreateText("Text", panel, "", 18, TextAnchor.MiddleCenter, Color.white);
        SetStretch(transientMessageText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        transientMessagePanel = panel.gameObject;
        transientMessagePanel.SetActive(false);
    }

    public void ShowTransientMessage(string message, float duration = 3f)
    {
        if (transientMessagePanel == null) return;

        transientMessageText.text = message;
        transientMessagePanel.SetActive(true);
        CancelInvoke(nameof(HideTransientMessage));
        Invoke(nameof(HideTransientMessage), duration);
    }

    private void HideTransientMessage()
    {
        if (transientMessagePanel != null)
            transientMessagePanel.SetActive(false);
    }

    // ================= 情報ステータス =================

    public void UpdateInformationDisplay()
    {
        var gameState = GameState.Instance;
        if (gameState == null) return;

        foreach (var item in informationItems)
            Destroy(item.gameObject);
        informationItems.Clear();

        foreach (var info in gameState.CollectedInformation)
        {
            var item = InformationUIItem.Create(informationContainer);
            item.SetInformation(info);
            informationItems.Add(item);
        }

        if (informationStatusText != null)
        {
            int collected = gameState.CollectedInformation.Count;
            informationStatusText.text = string.Format("情報収集: {0}/3", collected);
        }

        UpdateMinimapMarkers();
    }

    private void RefreshFreshnessColors()
    {
        var gameState = GameState.Instance;
        if (gameState == null) return;

        var infos = gameState.CollectedInformation;
        for (int i = 0; i < informationItems.Count && i < infos.Count; i++)
        {
            informationItems[i].SetInformation(infos[i]);
        }
    }

    // ================= 疑惑ゲージ =================

    public void UpdateSuspicionDisplay()
    {
        if (suspicionGaugeImage != null)
            suspicionGaugeImage.fillAmount = currentSuspicion / maxSuspicion;

        if (suspicionPercentText != null)
            suspicionPercentText.text = string.Format("{0:F0}%", currentSuspicion);

        if (suspicionGaugeImage != null)
        {
            if (currentSuspicion >= 100f)
            {
                suspicionGaugeImage.color = Color.red;
            }
            else if (currentSuspicion >= 70f)
            {
                suspicionGaugeImage.color = new Color(1f, 0.5f, 0f); // 橙
            }
            else
            {
                suspicionGaugeImage.color = Color.yellow;
            }
        }
    }

    public void AddSuspicion(float amount)
    {
        currentSuspicion = Mathf.Min(currentSuspicion + amount, maxSuspicion);
    }

    public void ReduceSuspicion(float amount)
    {
        currentSuspicion = Mathf.Max(currentSuspicion - amount, 0f);
    }

    public float GetCurrentSuspicion()
    {
        return currentSuspicion;
    }

    public void ResetForNewGame()
    {
        currentSuspicion = 0f;

        foreach (var item in informationItems)
            if (item != null) Destroy(item.gameObject);
        informationItems.Clear();

        foreach (var marker in terminalMarkers.Values)
            if (marker != null) Destroy(marker.gameObject);
        terminalMarkers.Clear();

        if (informationStatusText != null)
            informationStatusText.text = "情報収集: 0/3";

        UpdateSuspicionDisplay();
    }

    // ================= ミニマップ =================

    private void UpdateMinimapMarkers()
    {
        var gameState = GameState.Instance;
        if (gameState == null || minimapContent == null) return;

        foreach (var info in gameState.CollectedInformation)
        {
            if (terminalMarkers.ContainsKey(info.terminalId)) continue;

            // 罠情報も取得時点では本物と同じ見た目にし、正体をUIから漏らさない。
            var marker = CreateImage($"TerminalMarker_{info.terminalId}", minimapContent,
                new Color(0.3f, 0.7f, 1f));
            marker.rectTransform.sizeDelta = new Vector2(12, 12);
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            marker.rectTransform.anchoredPosition = WorldToMinimap(info.terminalPosition);

            terminalMarkers[info.terminalId] = marker;
        }
    }

    private void UpdateMinimapPlayerMarker()
    {
        if (playerMarker == null) return;

        if (spyController == null)
        {
            spyController = FindAnyObjectByType<SpyController>();
            if (spyController == null) return;
        }

        playerMarker.rectTransform.anchoredPosition = WorldToMinimap(spyController.GetPosition());
    }

    private Vector2 WorldToMinimap(Vector3 worldPosition)
    {
        float normalizedX = Mathf.InverseLerp(FacilityWorldMin.x, FacilityWorldMax.x, worldPosition.x);
        float normalizedZ = Mathf.InverseLerp(FacilityWorldMin.y, FacilityWorldMax.y, worldPosition.z);

        Vector2 size = minimapContent.rect.size;
        return new Vector2(
            (normalizedX - 0.5f) * size.x,
            (normalizedZ - 0.5f) * size.y
        );
    }

    // ================= インタラクションプロンプト / ハッキング進捗 =================

    public void SetInteractionPrompt(string text)
    {
        if (interactionPromptPanel == null) return;
        interactionPromptText.text = text;
        interactionPromptPanel.SetActive(true);
    }

    public void ClearInteractionPrompt()
    {
        if (interactionPromptPanel == null) return;
        interactionPromptPanel.SetActive(false);
    }

    public void ShowHackingProgress(bool show, string label = null)
    {
        if (hackingProgressPanel == null) return;

        hackingProgressPanel.SetActive(show);
        if (show)
        {
            hackingProgressLabel.text = label ?? "";
            hackingProgressFill.fillAmount = 0f;
        }
    }

    public void UpdateHackingProgress(float progress01)
    {
        if (hackingProgressFill == null) return;
        hackingProgressFill.fillAmount = progress01;
    }
}

/// <summary>
/// 収集済み情報1件分を表示するUIチップ
/// </summary>
public class InformationUIItem : MonoBehaviour
{
    private Text infoText;
    private Image freshnessIndicator;

    public static InformationUIItem Create(Transform parent)
    {
        var go = new GameObject("InformationChip", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.15f);

        var layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 150;

        var item = go.AddComponent<InformationUIItem>();

        var indicatorRt = new GameObject("FreshnessIndicator", typeof(RectTransform)).GetComponent<RectTransform>();
        indicatorRt.SetParent(go.transform, false);
        indicatorRt.anchorMin = new Vector2(0f, 0.2f);
        indicatorRt.anchorMax = new Vector2(0.2f, 0.8f);
        indicatorRt.offsetMin = new Vector2(6, 0);
        indicatorRt.offsetMax = Vector2.zero;
        item.freshnessIndicator = indicatorRt.gameObject.AddComponent<Image>();

        var textRt = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        textRt.SetParent(go.transform, false);
        textRt.anchorMin = new Vector2(0.25f, 0f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = new Vector2(-6, 0);
        var text = textRt.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        item.infoText = text;

        return item;
    }

    public void SetInformation(GameState.InformationData data)
    {
        if (infoText != null)
            infoText.text = string.Format("端末 #{0}", data.terminalId);

        if (freshnessIndicator != null)
        {
            if (data.isFresh)
            {
                freshnessIndicator.color = Color.cyan;
            }
            else if (data.isDegraded)
            {
                freshnessIndicator.color = Color.yellow;
            }
            else if (data.isCorrupted)
            {
                freshnessIndicator.color = Color.red;
            }
        }
    }
}
