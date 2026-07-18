using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// 命令ログの管理と表示
/// オーガナイザーが命令を監視・無効化できるUI
/// 操作: L=開閉, I/K=選択エントリ移動, Enter長押し5秒=無効化
/// </summary>
public class CommandLog : MonoBehaviour
{
    [System.Serializable]
    public class CommandEntry
    {
        public int id;
        public float timestamp;
        public CommandType commandType;
        public Vector3 location;
        public string locationName;
        public bool isInvalidated;
    }

    [SerializeField] private int maxLogEntries = 20;
    [SerializeField] private float invalidateHoldDuration = 5f;
    [SerializeField] private int suspicionThreshold = 3;
    [SerializeField] private float areaLockDuration = 300f; // 5分

    private Font defaultFont;
    private List<CommandEntry> entries = new List<CommandEntry>(); // 先頭(0)が最新
    private int nextEntryId = 0;
    private int selectedEntryId = -1;
    private bool isDisplaying = false;
    private float holdProgress = 0f;

    private Dictionary<string, int> areaForgeCounts = new Dictionary<string, int>();
    private Dictionary<string, float> areaLockedUntil = new Dictionary<string, float>();

    private GameObject logRootPanel;
    private CommandLogRowUI[] rows;
    private Image holdProgressFill;

    private OrganizerController organizerController;

    private void Awake()
    {
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
    }

    public void Initialize(OrganizerController controller)
    {
        organizerController = controller;
        isDisplaying = false;
        logRootPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isDisplaying) return;

        HandleSelectionInput();
        HandleInvalidateHold();
    }

    /// <summary>
    /// スパイが端末から偽造命令を発行する唯一のエントリポイント。
    /// 対象エリアが無効化ロック中の場合は発行できず、failureReasonに理由を返す。
    /// </summary>
    public bool TryIssueForgedCommand(CommandType commandType, Vector3 location, out string failureReason)
    {
        string areaName = GetLocationName(location);

        if (areaLockedUntil.TryGetValue(areaName, out float lockedUntil) && Time.time < lockedUntil)
        {
            failureReason = $"{areaName}は命令受付停止中（残り{(lockedUntil - Time.time):F0}秒）";
            return false;
        }

        AddCommandEntry(commandType, location);

        areaForgeCounts.TryGetValue(areaName, out int count);
        count++;
        areaForgeCounts[areaName] = count;
        if (count % suspicionThreshold == 0)
            organizerController?.OnAreaSuspicionThreshold(areaName);

        failureReason = null;
        return true;
    }

    public void AddCommandEntry(CommandType commandType, Vector3 location)
    {
        var entry = new CommandEntry
        {
            id = nextEntryId++,
            timestamp = Time.time,
            commandType = commandType,
            location = location,
            locationName = GetLocationName(location),
            isInvalidated = false
        };

        entries.Insert(0, entry);

        while (entries.Count > maxLogEntries)
            entries.RemoveAt(entries.Count - 1);

        if (selectedEntryId == -1)
            selectedEntryId = entry.id;

        UpdateLogDisplay();
    }

    /// <summary>
    /// 即時無効化の簡易API。UI上の主経路はEnter長押し(5秒)によるホールド式無効化(HandleInvalidateHold)。
    /// こちらは将来の自動テスト/デバッグ用途として維持する。
    /// </summary>
    public void InvalidateCommand(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= entries.Count) return;

        entries[entryIndex].isInvalidated = true;
        areaLockedUntil[entries[entryIndex].locationName] = Time.time + areaLockDuration;
        UpdateLogDisplay();
        Debug.Log($"✓ Command invalidated: {entries[entryIndex].commandType}");
    }

    public void ToggleDisplay()
    {
        isDisplaying = !isDisplaying;
        logRootPanel.SetActive(isDisplaying);

        if (isDisplaying)
        {
            if (selectedEntryId == -1 && entries.Count > 0)
                selectedEntryId = entries[0].id;
            UpdateLogDisplay();
        }
        else
        {
            CancelHold();
        }

        organizerController?.NotifySpyLogAccess(isDisplaying);
    }

    // ================= 選択 / 長押し無効化 =================

    private void HandleSelectionInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || entries.Count == 0) return;

        if (keyboard.iKey.wasPressedThisFrame) MoveSelection(-1);
        if (keyboard.kKey.wasPressedThisFrame) MoveSelection(1);
    }

    private void MoveSelection(int delta)
    {
        CancelHold();

        int current = entries.FindIndex(e => e.id == selectedEntryId);
        if (current < 0) current = 0;

        int next = Mathf.Clamp(current + delta, 0, entries.Count - 1);
        selectedEntryId = entries[next].id;
        UpdateLogDisplay();
    }

    private void HandleInvalidateHold()
    {
        var keyboard = Keyboard.current;
        int index = entries.FindIndex(e => e.id == selectedEntryId);
        bool canInvalidate = index >= 0 && !entries[index].isInvalidated;
        bool holdKeyDown = keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed);

        if (holdKeyDown && canInvalidate)
        {
            holdProgress += Time.deltaTime / invalidateHoldDuration;
            if (holdProgressFill != null)
                holdProgressFill.fillAmount = Mathf.Clamp01(holdProgress);

            if (holdProgress >= 1f)
            {
                CommitInvalidate(index);
                CancelHold();
            }
        }
        else
        {
            CancelHold();
        }
    }

    private void CommitInvalidate(int index)
    {
        var entry = entries[index];
        entry.isInvalidated = true;
        areaLockedUntil[entry.locationName] = Time.time + areaLockDuration;
        UpdateLogDisplay();
        organizerController?.OnCommandInvalidated(entry);
    }

    private void CancelHold()
    {
        holdProgress = 0f;
        if (holdProgressFill != null)
            holdProgressFill.fillAmount = 0f;
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
        text.fontSize = fontSize;
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

    private void BuildUI()
    {
        var panel = CreateUIObject("CommandLogPanel", transform);
        // 固定サイズ+中央ピボットだと、画面アスペクト比が基準解像度(1920x1080)からズレた際に
        // キャンバスの実効高さを超えて上下が見切れうる。画面比率に対する相対サイズで確保する。
        SetStretch(panel, new Vector2(0.15f, 0.05f), new Vector2(0.85f, 0.95f), Vector2.zero, Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
        logRootPanel = panel.gameObject;

        var title = CreateText("Title", panel,
            "命令ログ（直近20件）　[I/K] 選択　[Enter長押し5秒] 無効化　[L] 閉じる",
            18, TextAnchor.UpperLeft, Color.white);
        SetStretch(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -40), new Vector2(-15, -10));

        var listArea = CreateUIObject("ListArea", panel);
        SetStretch(listArea, new Vector2(0, 0), new Vector2(1, 1), new Vector2(15, 60), new Vector2(-15, -45));
        var vlg = listArea.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childAlignment = TextAnchor.UpperLeft;

        rows = new CommandLogRowUI[maxLogEntries];
        for (int i = 0; i < maxLogEntries; i++)
        {
            rows[i] = CommandLogRowUI.Create(listArea, defaultFont);
            rows[i].Hide();
        }

        // 無効化進捗バー(下部)
        var progressBg = CreateImage("HoldProgressBackground", panel, new Color(0.15f, 0.15f, 0.15f, 1f));
        SetStretch(progressBg.rectTransform, new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), new Vector2(0, 10), new Vector2(0, 30));

        holdProgressFill = CreateImage("Fill", progressBg.rectTransform, new Color(0.8f, 0.2f, 0.2f, 1f));
        holdProgressFill.type = Image.Type.Filled;
        holdProgressFill.fillMethod = Image.FillMethod.Horizontal;
        holdProgressFill.fillAmount = 0f;
        SetStretch(holdProgressFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        logRootPanel.SetActive(false);
    }

    private void UpdateLogDisplay()
    {
        for (int i = 0; i < rows.Length; i++)
        {
            if (i < entries.Count)
                rows[i].SetData(entries[i], entries[i].id == selectedEntryId);
            else
                rows[i].Hide();
        }
    }

    private string GetLocationName(Vector3 location)
    {
        string name = organizerController != null
            ? organizerController.GetSurveillanceSystem()?.GetAreaNameForPosition(location)
            : null;
        return string.IsNullOrEmpty(name) ? "盲点エリア（カメラなし）" : name;
    }
}

/// <summary>
/// 命令ログ1行分の表示（プールして使い回す）
/// </summary>
public class CommandLogRowUI : MonoBehaviour
{
    private Image background;
    private Text lineText;

    public static CommandLogRowUI Create(Transform parent, Font font)
    {
        var go = new GameObject("LogRow", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 24;

        var row = go.AddComponent<CommandLogRowUI>();
        row.background = go.AddComponent<Image>();
        row.background.color = new Color(1f, 1f, 1f, 0.05f);

        var textRt = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        textRt.SetParent(go.transform, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10, 0);
        textRt.offsetMax = new Vector2(-10, 0);
        var text = textRt.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        row.lineText = text;

        return row;
    }

    public void SetData(CommandLog.CommandEntry entry, bool isSelected)
    {
        gameObject.SetActive(true);

        string invalidMarker = entry.isInvalidated ? " [無効化済み]" : "";
        lineText.text = $"[{entry.timestamp:F1}s] {entry.commandType} @ {entry.locationName}{invalidMarker}";
        lineText.color = entry.isInvalidated ? Color.red : Color.white;
        background.color = isSelected ? new Color(1f, 0.9f, 0.2f, 0.35f) : new Color(1f, 1f, 1f, 0.05f);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
