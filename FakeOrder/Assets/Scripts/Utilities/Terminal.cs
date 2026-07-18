using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 情報端末のプリハブ用スクリプト
/// スパイがハッキングして「情報収集」または「命令書偽造」のいずれかを行う
/// </summary>
public class Terminal : MonoBehaviour, IInteractable
{
    private enum PendingAction { None, CollectInfo, Forge }

    [SerializeField] private int terminalId;
    [SerializeField] private float hackingDuration = 5f;
    [SerializeField] private float maxHackingDistance = 3.5f;

    private bool awaitingSelection = false;
    private bool isBeingHacked = false;
    private float hackingProgress = 0f;
    private PendingAction pendingAction = PendingAction.None;
    private CommandType pendingCommandType;

    private SpyController currentSpyController;
    private InformationFreshness informationFreshness;
    private SpyUI spyUI;
    private CommandLog commandLog;

    private void Start()
    {
        informationFreshness = FindAnyObjectByType<InformationFreshness>();
        spyUI = FindAnyObjectByType<SpyUI>();
        commandLog = FindAnyObjectByType<CommandLog>();
    }

    public void Interact(SpyController spy)
    {
        if (isBeingHacked) return;

        if (awaitingSelection)
        {
            // メニュー表示中にもう一度Eを押す＝キャンセル
            // (Escapeはカーソルロック解除キーと衝突するため使わない)
            CancelSelection();
            return;
        }

        currentSpyController = spy;
        awaitingSelection = true;
        spyUI?.ShowTerminalActionMenu();
    }

    public string GetInteractionPrompt()
    {
        if (isBeingHacked || awaitingSelection) return null;
        return $"[E] 端末 #{terminalId} を操作";
    }

    private void Update()
    {
        if (awaitingSelection)
        {
            HandleSelectionInput();
            return;
        }

        if (!isBeingHacked) return;

        if (currentSpyController != null &&
            Vector3.Distance(currentSpyController.GetPosition(), transform.position) > maxHackingDistance)
        {
            CancelHacking();
            return;
        }

        hackingProgress += Time.deltaTime / hackingDuration;
        spyUI?.UpdateHackingProgress(Mathf.Clamp01(hackingProgress));

        if (hackingProgress >= 1f)
        {
            CompleteHacking();
        }
    }

    private void HandleSelectionInput()
    {
        // メニュー表示中も対象から離れたら自動キャンセル(ハッキング中の距離キャンセルと同じ考え方)
        if (currentSpyController != null &&
            Vector3.Distance(currentSpyController.GetPosition(), transform.position) > maxHackingDistance)
        {
            CancelSelection();
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.digit1Key.wasPressedThisFrame) BeginHacking(PendingAction.CollectInfo);
        else if (keyboard.digit2Key.wasPressedThisFrame) BeginHacking(PendingAction.Forge, CommandType.LeakInformation);
        else if (keyboard.digit3Key.wasPressedThisFrame) BeginHacking(PendingAction.Forge, CommandType.SecurityMovement);
        else if (keyboard.digit4Key.wasPressedThisFrame) BeginHacking(PendingAction.Forge, CommandType.InspectionOrder);
        else if (keyboard.digit5Key.wasPressedThisFrame) BeginHacking(PendingAction.Forge, CommandType.EmergencyOrder);
    }

    private void BeginHacking(PendingAction action, CommandType commandType = default)
    {
        awaitingSelection = false;
        pendingAction = action;
        pendingCommandType = commandType;
        spyUI?.HideTerminalActionMenu();
        StartHacking();
    }

    private void CancelSelection()
    {
        awaitingSelection = false;
        currentSpyController = null;
        spyUI?.HideTerminalActionMenu();
    }

    private void StartHacking()
    {
        isBeingHacked = true;
        hackingProgress = 0f;

        string label = pendingAction == PendingAction.Forge
            ? $"端末 #{terminalId} 命令書偽造中..."
            : $"端末 #{terminalId} ハッキング中...";
        spyUI?.ShowHackingProgress(true, label);

        Debug.Log($"🔓 Accessing terminal #{terminalId} ({pendingAction})...");
    }

    private void CancelHacking()
    {
        isBeingHacked = false;
        hackingProgress = 0f;
        currentSpyController = null;
        spyUI?.ShowHackingProgress(false);

        Debug.Log($"✗ Hacking cancelled: Terminal #{terminalId}");
    }

    private void CompleteHacking()
    {
        isBeingHacked = false;
        hackingProgress = 0f;
        spyUI?.ShowHackingProgress(false);

        if (pendingAction == PendingAction.Forge)
        {
            string failureReason = "命令ログが見つかりません";
            bool success = commandLog != null &&
                commandLog.TryIssueForgedCommand(pendingCommandType, transform.position, out failureReason);

            spyUI?.ShowTransientMessage(success
                ? $"命令書偽造: {pendingCommandType} を発行しました"
                : $"発行失敗: {failureReason}");

            Debug.Log(success
                ? $"📝 Forged command issued: {pendingCommandType} @ Terminal #{terminalId}"
                : $"🚫 Forged command rejected @ Terminal #{terminalId}");
        }
        else
        {
            bool isTrap = informationFreshness != null && informationFreshness.IsTrapInformation(terminalId);

            if (currentSpyController != null)
                currentSpyController.AddInformation(terminalId, transform.position, isTrap);

            Debug.Log($"✓ Terminal #{terminalId} hacking complete! {(isTrap ? "[TRAP DETECTED]" : "")}");
        }

        pendingAction = PendingAction.None;
        currentSpyController = null;
    }

    public int GetTerminalId()
    {
        return terminalId;
    }

    public void SetTerminalId(int id)
    {
        terminalId = id;
    }

    public float GetHackingProgress()
    {
        return Mathf.Clamp01(hackingProgress);
    }
}

/// <summary>
/// 脱出ポイント
/// </summary>
public class ExitPoint : MonoBehaviour, IInteractable
{
    public void Interact(SpyController spy)
    {
        // 脱出判定
        int collectedCount = GameState.Instance != null ? GameState.Instance.GetValidInformationCount() : 0;

        if (collectedCount >= 3)
        {
            Debug.Log("✓ Spy escaped with 3+ information! VICTORY!");
            GameManager.Instance.EndGame(GameResult.SpyEscaped);
        }
        else if (collectedCount >= 1)
        {
            Debug.Log("⚠️ Spy escaped with incomplete information. PARTIAL SUCCESS!");
            GameManager.Instance.EndGame(GameResult.IncompleteEscape);
        }
        else
        {
            Debug.Log("✗ Spy escaped with no information. FAILURE!");
            GameManager.Instance.EndGame(GameResult.SpyEliminated);
        }
    }

    public string GetInteractionPrompt()
    {
        int collectedCount = GameState.Instance != null ? GameState.Instance.GetValidInformationCount() : 0;

        if (collectedCount >= 3)
            return "[E] 脱出する（情報収集済み）";
        if (collectedCount >= 1)
            return $"[E] 脱出する（部分的な情報収集: {collectedCount}/3）";
        return "[E] 脱出する（情報未収集）";
    }
}
