using UnityEngine;

public enum RoutineActivityType
{
    Workstation,
    Break
}

/// <summary>
/// ブラフPCや休憩設備を、スパイが社員らしく振る舞うための操作地点にする。
/// </summary>
public class RoutineActivityPoint : MonoBehaviour, IInteractable
{
    [SerializeField] private RoutineActivityType activityType;
    [SerializeField] private float duration = 4f;
    [SerializeField] private string activityLabel = "通常業務";

    public void Configure(RoutineActivityType type, float activityDuration, string label)
    {
        activityType = type;
        duration = Mathf.Max(1f, activityDuration);
        activityLabel = label;
    }

    public void Interact(SpyController spy)
    {
        spy?.BeginRoutineActivity(activityType, duration, activityLabel);
    }

    public string GetInteractionPrompt()
    {
        return $"[E] {activityLabel}を行う";
    }

    public RoutineActivityType GetActivityType() => activityType;
    public float GetDuration() => duration;
}
