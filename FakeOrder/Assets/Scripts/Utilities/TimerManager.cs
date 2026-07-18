using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 時間管理システム
/// ゲーム全体の時間を統一管理（同期用）
/// </summary>
public class TimerManager : MonoBehaviour
{
    public static TimerManager Instance { get; private set; }

    [SerializeField] private float timeScale = 1f;
    private float gameStartTime;
    private List<IGameTimer> registeredTimers = new List<IGameTimer>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        gameStartTime = Time.time;
    }

    private void Update()
    {
        // タイムスケール変更時の処理
        Time.timeScale = timeScale;
    }

    public float GetGameTime()
    {
        return (Time.time - gameStartTime) * timeScale;
    }

    public void PauseGame()
    {
        timeScale = 0f;
    }

    public void ResumeGame()
    {
        timeScale = 1f;
    }

    public void SetTimeScale(float scale)
    {
        timeScale = Mathf.Max(0f, scale);
    }

    public void RegisterTimer(IGameTimer timer)
    {
        registeredTimers.Add(timer);
    }

    public void UnregisterTimer(IGameTimer timer)
    {
        registeredTimers.Remove(timer);
    }
}

public interface IGameTimer
{
    void OnTimerTick(float deltaTime);
}

/// <summary>
/// イベント通知システム
/// </summary>
public class GameEventSystem : MonoBehaviour
{
    public static GameEventSystem Instance { get; private set; }

    public delegate void GameEventHandler(string message);

    public event GameEventHandler OnSpyEventTriggered;
    public event GameEventHandler OnOrganizerEventTriggered;
    public event GameEventHandler OnCommandExecuted;
    public event GameEventHandler OnSuspicionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void TriggerSpyEvent(string message)
    {
        OnSpyEventTriggered?.Invoke(message);
    }

    public void TriggerOrganizerEvent(string message)
    {
        OnOrganizerEventTriggered?.Invoke(message);
    }

    public void TriggerCommandExecuted(string message)
    {
        OnCommandExecuted?.Invoke(message);
    }

    public void TriggerSuspicionChanged(string message)
    {
        OnSuspicionChanged?.Invoke(message);
    }
}
