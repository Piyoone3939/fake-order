using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 疑惑ゲージシステム
/// スパイの怪しい行動を監視して疑惑を蓄積
/// </summary>
public class SuspicionGauge : MonoBehaviour
{
    [Header("Suspicion Parameters")]
    [SerializeField] private float runningIncrease = 15f;
    [SerializeField] private float restrictedAreaIncrease = 20f;
    [SerializeField] private float prolongedStayIncrease = 5f;
    [SerializeField] private float routineBreakIncrease = 10f;
    [SerializeField] private float cameraGlazeIncrease = 3f;

    [SerializeField] private float npcGroupDecrease = 5f;
    [SerializeField] private float terminalOperationDecrease = 8f;
    [SerializeField] private float restAreaStayDecrease = 3f;

    [SerializeField] private float decayRate = 0.5f; // 秒ごとの疑惑減衰

    private float currentSuspicion = 0f;
    private float lastSuspicionDecayTime;
    private SpyController spyController;
    private SpyUI spyUI;
    private bool wasRunning = false;
    private bool alertTriggered = false;

    private void Start()
    {
        spyController = GetComponent<SpyController>();
        spyUI = FindAnyObjectByType<SpyUI>();
        lastSuspicionDecayTime = Time.time;
    }

    private void Update()
    {
        // 疑惑の自然減衰
        float timeSinceLastDecay = Time.time - lastSuspicionDecayTime;
        if (timeSinceLastDecay > 1f) // 1秒ごとに減衰判定
        {
            ApplySuspicionDecay();
            lastSuspicionDecayTime = Time.time;
        }

        // 行動の監視
        MonitorSpyBehavior();
    }

    private void MonitorSpyBehavior()
    {
        if (spyController == null) return;

        bool isCurrentlyRunning = spyController.IsSprinting();
        if (isCurrentlyRunning && !wasRunning)
        {
            IncreaseSuspicion(runningIncrease);
            wasRunning = true;
        }
        else if (!isCurrentlyRunning)
        {
            wasRunning = false;
        }

        // 制限エリア判定（後で距離判定に変更可能）
        // CheckRestrictedAreaAccess();
    }

    public void IncreaseSuspicion(float amount)
    {
        if (amount <= 0f) return;

        float previousSuspicion = currentSuspicion;
        currentSuspicion = Mathf.Min(currentSuspicion + amount, 100f);
        float actualIncrease = currentSuspicion - previousSuspicion;
        if (actualIncrease <= 0f) return;

        if (spyUI != null)
            spyUI.AddSuspicion(actualIncrease);

        Debug.Log($"📈 Suspicion increased by {actualIncrease}. Total: {currentSuspicion}");

        // 100%に達したら警告
        if (currentSuspicion >= 100f && !alertTriggered)
        {
            alertTriggered = true;
            TriggerSuspicionAlert();
        }
    }

    public void DecreaseSuspicion(float amount)
    {
        if (amount <= 0f || currentSuspicion <= 0f) return;

        float previousSuspicion = currentSuspicion;
        currentSuspicion = Mathf.Max(currentSuspicion - amount, 0f);
        float actualDecrease = previousSuspicion - currentSuspicion;

        if (spyUI != null)
            spyUI.ReduceSuspicion(actualDecrease);

        Debug.Log($"📉 Suspicion decreased by {actualDecrease}. Total: {currentSuspicion}");

        if (currentSuspicion < 100f)
            alertTriggered = false;
    }

    private void ApplySuspicionDecay()
    {
        float decay = decayRate;
        
        // NPCに近い場合や安全な場所にいる場合は減衰を加速
        // (実装は別途)

        DecreaseSuspicion(decay);
    }

    private void TriggerSuspicionAlert()
    {
        Debug.Log("⚠️ SUSPICION ALERT: 100%に達しました！");
        // オーガナイザー側に通知を送る
    }

    public float GetCurrentSuspicion()
    {
        return currentSuspicion;
    }

    public void ResetGauge()
    {
        currentSuspicion = 0f;
        wasRunning = false;
        alertTriggered = false;
        lastSuspicionDecayTime = Time.time;
    }

    public void ReportProblematicBehavior(string behaviorType)
    {
        switch (behaviorType)
        {
            case "running":
                IncreaseSuspicion(runningIncrease);
                break;
            case "restrictedArea":
                IncreaseSuspicion(restrictedAreaIncrease);
                break;
            case "prolongedStay":
                IncreaseSuspicion(prolongedStayIncrease);
                break;
            case "routineBreak":
                IncreaseSuspicion(routineBreakIncrease);
                break;
            case "cameraGaze":
                IncreaseSuspicion(cameraGlazeIncrease);
                break;
        }
    }

    public void ReportSafeActivity(string activityType)
    {
        switch (activityType)
        {
            case "withNPC":
                DecreaseSuspicion(npcGroupDecrease);
                break;
            case "terminalOperation":
                DecreaseSuspicion(terminalOperationDecrease);
                break;
            case "restArea":
                DecreaseSuspicion(restAreaStayDecrease);
                break;
        }
    }
}
