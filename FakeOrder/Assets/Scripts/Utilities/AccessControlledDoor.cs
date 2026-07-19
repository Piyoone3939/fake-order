using UnityEngine;

/// <summary>
/// 認証パスの代わりにハッキングで解錠するセキュリティドア。
/// </summary>
public class AccessControlledDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private string accessAreaName = "RESTRICTED AREA";
    [SerializeField] private float hackingDuration = 5f;
    [SerializeField] private float maxHackingDistance = 3.5f;
    [SerializeField] private float openHeight = 3.2f;
    [SerializeField] private float openDuration = 0.65f;

    private SpyController currentSpy;
    private SpyUI spyUI;
    private Vector3 closedPosition;
    private float hackingProgress;
    private float openingProgress;
    private bool isHacking;
    private bool isUnlocked;

    private void Awake()
    {
        closedPosition = transform.position;
    }

    private void Start()
    {
        spyUI = FindAnyObjectByType<SpyUI>();
    }

    private void Update()
    {
        if (isHacking)
            UpdateHacking();

        if (isUnlocked && openingProgress < 1f)
        {
            openingProgress = Mathf.Clamp01(openingProgress + Time.deltaTime / openDuration);
            float eased = Mathf.SmoothStep(0f, 1f, openingProgress);
            transform.position = closedPosition + Vector3.up * (openHeight * eased);
        }
    }

    public void Configure(string areaName, float duration = 5f)
    {
        accessAreaName = areaName;
        hackingDuration = duration;
    }

    public void Interact(SpyController spy)
    {
        if (isUnlocked || isHacking || spy == null)
            return;

        currentSpy = spy;
        isHacking = true;
        hackingProgress = 0f;
        spyUI?.ShowHackingProgress(true, $"{accessAreaName} 認証解析中...");
    }

    public string GetInteractionPrompt()
    {
        if (isUnlocked || isHacking)
            return null;
        return $"[E] {accessAreaName} の認証をハッキング";
    }

    private void UpdateHacking()
    {
        if (currentSpy == null || Vector3.Distance(currentSpy.GetPosition(), closedPosition) > maxHackingDistance)
        {
            CancelHacking();
            return;
        }

        hackingProgress += Time.deltaTime / hackingDuration;
        spyUI?.UpdateHackingProgress(Mathf.Clamp01(hackingProgress));
        if (hackingProgress < 1f)
            return;

        isHacking = false;
        isUnlocked = true;
        openingProgress = 0f;
        currentSpy = null;
        spyUI?.ShowHackingProgress(false);
        spyUI?.ShowTransientMessage($"{accessAreaName}: 認証突破");
        Debug.Log($"🔓 Access door unlocked: {accessAreaName}");
    }

    private void CancelHacking()
    {
        isHacking = false;
        hackingProgress = 0f;
        currentSpy = null;
        spyUI?.ShowHackingProgress(false);
    }
}
