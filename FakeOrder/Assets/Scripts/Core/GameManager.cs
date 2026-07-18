using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゲーム全体の進行管理
/// スパイとオーガナイザーの画面分割を管理し、ゲームフローを制御
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private float preparationDuration = 60f;
    private float preparationTimer;
    private GamePhase currentPhase = GamePhase.Title;
    private LocalRole selectedRole = LocalRole.None;

    private SpyController spyController;
    private OrganizerController organizerController;
    private SpyUI spyUI;
    private OrganizerUI organizerUI;
    private RoleSelectionUI roleSelectionUI;
    private SuspicionGauge suspicionGauge;
    private InformationFreshness informationFreshness;
    private GameState gameState;

    public enum GamePhase
    {
        Title,
        RoleSelection,
        Preparation,
        Playing,
        Finished
    }

    public enum LocalRole
    {
        None,
        Spy,
        Organizer
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        gameState = GameState.Instance;
        InitializeFlow();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (currentPhase != GamePhase.Title && currentPhase != GamePhase.RoleSelection &&
            keyboard != null && keyboard.f1Key.wasPressedThisFrame)
        {
            EnterRoleSelection();
            return;
        }

        switch (currentPhase)
        {
            case GamePhase.Title:
                break;
            case GamePhase.RoleSelection:
                break;
            case GamePhase.Preparation:
                UpdatePreparation();
                break;
            case GamePhase.Playing:
                UpdatePlaying();
                break;
            case GamePhase.Finished:
                // ゲーム終了
                break;
        }
    }

    private void InitializeFlow()
    {
        spyController = FindAnyObjectByType<SpyController>();
        organizerController = FindAnyObjectByType<OrganizerController>();
        spyUI = FindAnyObjectByType<SpyUI>();
        organizerUI = FindAnyObjectByType<OrganizerUI>();
        roleSelectionUI = FindAnyObjectByType<RoleSelectionUI>();
        suspicionGauge = FindAnyObjectByType<SuspicionGauge>();
        informationFreshness = FindAnyObjectByType<InformationFreshness>();

        EnterTitle();
    }

    private void EnterTitle()
    {
        currentPhase = GamePhase.Title;
        selectedRole = LocalRole.None;
        if (gameState != null)
            gameState.IsGameActive = false;

        SetRoleControls(false);
        SetCanvasVisible(spyUI, false);
        SetCanvasVisible(organizerUI, false);

        if (roleSelectionUI != null)
            roleSelectionUI.ShowTitle();
        else
            EnterRoleSelection();
    }

    public void OpenRoleSelection()
    {
        if (currentPhase == GamePhase.Title)
            EnterRoleSelection();
    }

    private void EnterRoleSelection()
    {
        currentPhase = GamePhase.RoleSelection;
        selectedRole = LocalRole.None;
        preparationTimer = preparationDuration;
        if (gameState != null)
            gameState.IsGameActive = false;

        SetRoleControls(false);
        SetCanvasVisible(spyUI, false);
        SetCanvasVisible(organizerUI, false);

        if (roleSelectionUI != null)
            roleSelectionUI.ShowSelection();
        else
        {
            Debug.LogWarning("RoleSelectionUI が見つからないため、スパイとして開始します。");
            SelectLocalRole(LocalRole.Spy);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void SelectLocalRole(LocalRole role)
    {
        if (role == LocalRole.None) return;

        selectedRole = role;
        currentPhase = GamePhase.Preparation;
        preparationTimer = preparationDuration;
        informationFreshness?.ResetInformationStates();
        spyUI?.ResetForNewGame();
        suspicionGauge?.ResetGauge();

        SetCanvasVisible(spyUI, role == LocalRole.Spy);
        SetCanvasVisible(organizerUI, role == LocalRole.Organizer);
        SetRoleControls(false);
        if (role == LocalRole.Organizer)
            organizerController?.SetControlEnabled(true);
        roleSelectionUI?.ShowPreparation(role, preparationTimer);
        Debug.Log($"Local role selected: {role}");
    }

    private void UpdatePreparation()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            preparationTimer = 0f;

        preparationTimer -= Time.deltaTime;
        roleSelectionUI?.ShowPreparation(selectedRole, Mathf.Max(0f, preparationTimer));

        if (preparationTimer <= 0)
        {
            StartMainGame();
        }
    }

    private void UpdatePlaying()
    {
        if (gameState.IsTimeUp())
        {
            EndGame(GameResult.TimeUp);
        }
    }

    private void StartMainGame()
    {
        currentPhase = GamePhase.Playing;
        gameState.InitializeGame(GameState.PlayerInfo.Role.Spy, GameState.PlayerInfo.Role.Organizer);
        spyUI?.ResetForNewGame();
        suspicionGauge?.ResetGauge();
        roleSelectionUI?.HideStatus();
        SetRoleControls(true);

        if (spyController != null)
            spyController.OnGameStart();

        if (organizerController != null)
            organizerController.OnGameStart();

        Debug.Log("🎮 Main Game Started!");
    }

    public void EndGame(GameResult result)
    {
        currentPhase = GamePhase.Finished;
        gameState.IsGameActive = false;
        SetRoleControls(false);
        roleSelectionUI?.ShowResult(result);

        Debug.Log($"Game Finished: {result}");
        // リザルト画面へ遷移
        // SceneManager.LoadScene("ResultScene");
    }

    public float GetPreparationProgress()
    {
        return 1f - (preparationTimer / preparationDuration);
    }

    public GamePhase GetCurrentPhase()
    {
        return currentPhase;
    }

    public LocalRole GetSelectedRole()
    {
        return selectedRole;
    }

    private void SetRoleControls(bool phaseAllowsInput)
    {
        spyController?.SetControlEnabled(phaseAllowsInput && selectedRole == LocalRole.Spy);
        organizerController?.SetControlEnabled(phaseAllowsInput && selectedRole == LocalRole.Organizer);
    }

    private static void SetCanvasVisible(MonoBehaviour ui, bool visible)
    {
        if (ui == null) return;
        Canvas canvas = ui.GetComponent<Canvas>();
        if (canvas != null)
            canvas.enabled = visible;
    }
}

public enum GameResult
{
    SpyEscaped,
    SpyEliminated,
    TimeUp,
    IncompleteEscape
}
