using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ローカルプロトタイプ用のロール選択画面とフェーズ通知UI。
/// </summary>
public class RoleSelectionUI : MonoBehaviour
{
    [SerializeField] private Sprite titleLogoSprite;

    private Font defaultFont;
    private GameObject titlePanel;
    private GameObject selectionPanel;
    private GameObject statusPanel;
    private Text statusText;
    private bool isSelecting;
    private bool isShowingTitle;
    private bool isTitleTransitioning;
    private TitleLogoEffects titleLogoEffects;
    private RectTransform spyButtonRect;
    private RectTransform organizerButtonRect;

    private void Awake()
    {
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
    }

    private void Update()
    {
        if (isShowingTitle)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var titleKeyboard = Keyboard.current;
            var titleMouse = Mouse.current;
            bool enterPressed = titleKeyboard != null &&
                (titleKeyboard.enterKey.wasPressedThisFrame || titleKeyboard.numpadEnterKey.wasPressedThisFrame);
            bool clicked = titleMouse != null && titleMouse.leftButton.wasPressedThisFrame;
            if (!isTitleTransitioning && (enterPressed || clicked))
            {
                isTitleTransitioning = true;
                if (titleLogoEffects != null)
                    titleLogoEffects.PlayExit(() => GameManager.Instance?.OpenRoleSelection());
                else
                    GameManager.Instance?.OpenRoleSelection();
            }
            return;
        }

        if (!isSelecting) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            SelectRole(GameManager.LocalRole.Spy);
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            SelectRole(GameManager.LocalRole.Organizer);

        // InputSystemUIInputModuleの初期化状態に依存せず、クリックでも必ず選択できるようにする。
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 pointerPosition = mouse.position.ReadValue();
            if (spyButtonRect != null && RectTransformUtility.RectangleContainsScreenPoint(spyButtonRect, pointerPosition))
                SelectRole(GameManager.LocalRole.Spy);
            else if (organizerButtonRect != null && RectTransformUtility.RectangleContainsScreenPoint(organizerButtonRect, pointerPosition))
                SelectRole(GameManager.LocalRole.Organizer);
        }
    }

    public void ShowSelection()
    {
        isShowingTitle = false;
        isTitleTransitioning = false;
        titlePanel?.SetActive(false);
        isSelecting = true;
        selectionPanel?.SetActive(true);
        statusPanel?.SetActive(false);
    }

    public void ShowTitle()
    {
        isShowingTitle = true;
        isTitleTransitioning = false;
        isSelecting = false;
        titlePanel?.SetActive(true);
        selectionPanel?.SetActive(false);
        statusPanel?.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        titleLogoEffects?.ResetPresentation();
    }

    public void HideSelection()
    {
        isSelecting = false;
        selectionPanel?.SetActive(false);
    }

    public void ShowPreparation(GameManager.LocalRole role, float remainingSeconds)
    {
        HideSelection();
        statusPanel?.SetActive(true);
        if (statusText != null)
        {
            string roleName = role == GameManager.LocalRole.Spy ? "スパイ" : "オーガナイザー";
            bool waitingForHost = FakeOrderNetworkSession.Instance != null &&
                FakeOrderNetworkSession.Instance.HasAssignedRole && FakeOrderNetworkSession.Instance.IsRemoteClient;
            string startHint = waitingForHost ? "Hostの開始待ち" : "[Enter] 開始";
            statusText.text = $"{roleName} / 準備フェーズ  残り {Mathf.CeilToInt(remainingSeconds)}秒  /  {startHint}";
        }
    }

    public void HideStatus()
    {
        statusPanel?.SetActive(false);
    }

    public void ShowResult(GameResult result)
    {
        isShowingTitle = false;
        titlePanel?.SetActive(false);
        isSelecting = false;
        selectionPanel?.SetActive(false);
        statusPanel?.SetActive(true);
        if (statusText != null)
            statusText.text = $"ゲーム終了: {GetResultLabel(result)}  /  [F1] ロール選択へ戻る";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SelectRole(GameManager.LocalRole role)
    {
        if (!isSelecting) return;
        isSelecting = false;
        GameManager.Instance?.SelectLocalRole(role);
    }

    private void BuildUI()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
        }

        titlePanel = CreatePanel("TitlePanel", transform, new Color(0.957f, 0.953f, 0.933f, 1f));
        Image titlePanelImage = titlePanel.GetComponent<Image>();
        titlePanelImage.raycastTarget = false;

        GameObject logoRootObject = new GameObject("LogoRoot", typeof(RectTransform), typeof(CanvasGroup));
        logoRootObject.transform.SetParent(titlePanel.transform, false);
        RectTransform logoRoot = logoRootObject.GetComponent<RectTransform>();
        logoRoot.anchorMin = new Vector2(0.5f, 0.5f);
        logoRoot.anchorMax = new Vector2(0.5f, 0.5f);
        logoRoot.pivot = new Vector2(0.5f, 0.5f);
        logoRoot.anchoredPosition = new Vector2(0f, 45f);
        logoRoot.sizeDelta = new Vector2(1600f, 533f);

        GameObject logoObject = new GameObject("LogoImage", typeof(RectTransform), typeof(Image));
        logoObject.transform.SetParent(logoRoot, false);
        RectTransform logoRect = logoObject.GetComponent<RectTransform>();
        SetRect(logoRect, Vector2.zero, Vector2.one);
        Image logoImage = logoObject.GetComponent<Image>();
        logoImage.sprite = titleLogoSprite;
        logoImage.type = Image.Type.Simple;
        logoImage.preserveAspect = true;
        logoImage.raycastTarget = false;

        titleLogoEffects = logoRootObject.AddComponent<TitleLogoEffects>();
        titleLogoEffects.Configure(logoRoot, logoImage);

        Text tagline = CreateText("Tagline", titlePanel.transform,
            "その情報は、いつの真実だ。", 34, TextAnchor.MiddleCenter, new Color(0.15f, 0.2f, 0.23f));
        SetRect(tagline.rectTransform, new Vector2(0.24f, 0.14f), new Vector2(0.76f, 0.21f));

        Text startPrompt = CreateText("StartPrompt", titlePanel.transform,
            "[ ENTER ]  START", 30, TextAnchor.MiddleCenter, new Color(0.1f, 0.45f, 0.91f));
        SetRect(startPrompt.rectTransform, new Vector2(0.3f, 0.07f), new Vector2(0.7f, 0.14f));

        selectionPanel = CreatePanel("RoleSelectionPanel", transform, new Color(0.03f, 0.05f, 0.09f, 0.96f));

        Text title = CreateText("Title", selectionPanel.transform, "FAKE ORDER", 58, TextAnchor.MiddleCenter, Color.white);
        SetRect(title.rectTransform, new Vector2(0.2f, 0.68f), new Vector2(0.8f, 0.84f));

        Text description = CreateText("Description", selectionPanel.transform,
            "操作するロールを選択してください", 30, TextAnchor.MiddleCenter, new Color(0.75f, 0.85f, 1f));
        SetRect(description.rectTransform, new Vector2(0.2f, 0.57f), new Vector2(0.8f, 0.68f));

        Button spyButton = CreateButton("SpyButton", selectionPanel.transform,
            "[1] スパイ\n潜入・情報収集", new Color(0.1f, 0.42f, 0.7f));
        spyButtonRect = spyButton.GetComponent<RectTransform>();
        SetRect(spyButtonRect, new Vector2(0.2f, 0.31f), new Vector2(0.47f, 0.55f));
        spyButton.onClick.AddListener(() => SelectRole(GameManager.LocalRole.Spy));

        Button organizerButton = CreateButton("OrganizerButton", selectionPanel.transform,
            "[2] オーガナイザー\n監視・命令無効化", new Color(0.55f, 0.18f, 0.22f));
        organizerButtonRect = organizerButton.GetComponent<RectTransform>();
        SetRect(organizerButtonRect, new Vector2(0.53f, 0.31f), new Vector2(0.8f, 0.55f));
        organizerButton.onClick.AddListener(() => SelectRole(GameManager.LocalRole.Organizer));

        Text hint = CreateText("Hint", selectionPanel.transform,
            "ボタンをクリック、または数字キーで選択", 24, TextAnchor.MiddleCenter, Color.gray);
        SetRect(hint.rectTransform, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.29f));

        statusPanel = CreatePanel("PhaseStatusPanel", transform, new Color(0f, 0f, 0f, 0.72f));
        RectTransform statusRect = statusPanel.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.28f, 0.91f);
        statusRect.anchorMax = new Vector2(0.72f, 0.98f);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = Vector2.zero;

        statusText = CreateText("StatusText", statusPanel.transform, "", 26, TextAnchor.MiddleCenter, Color.white);
        SetRect(statusText.rectTransform, Vector2.zero, Vector2.one);
        titlePanel.SetActive(false);
        selectionPanel.SetActive(false);
        statusPanel.SetActive(false);
    }

    private GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private Text CreateText(string objectName, Transform parent, string content, int size,
        TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = defaultFont;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.text = content;
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, string label, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;

        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        button.colors = colors;

        Text text = CreateText("Label", go.transform, label, 30, TextAnchor.MiddleCenter, Color.white);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one);
        return button;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static string GetResultLabel(GameResult result)
    {
        switch (result)
        {
            case GameResult.SpyEscaped: return "スパイ脱出成功";
            case GameResult.SpyEliminated: return "スパイ排除";
            case GameResult.TimeUp: return "時間切れ";
            case GameResult.IncompleteEscape: return "部分成功";
            default: return result.ToString();
        }
    }
}
