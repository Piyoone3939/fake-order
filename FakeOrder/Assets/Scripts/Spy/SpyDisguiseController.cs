using UnityEngine;

/// <summary>
/// 監視カメラ上でスパイを一般社員と同じ外見に見せる。
/// 一人称カメラからは専用レイヤーを除外し、監視カメラには描画する。
/// </summary>
public class SpyDisguiseController : MonoBehaviour
{
    public const int DisguiseVisualLayer = 29;

    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Renderer headRenderer;
    [SerializeField] private int appearanceVariant;
    [SerializeField] private string employeeId = "FO-000";

    public void Configure(Renderer body, Renderer head)
    {
        bodyRenderer = body;
        headRenderer = head;
        ApplyAppearance();
    }

    private void Awake()
    {
        ResolveRenderers();
        ApplyAppearance();
    }

    public void ApplyNewIdentity()
    {
        appearanceVariant = Random.Range(0, OfficeNpcController.EmployeeAppearanceVariantCount);
        employeeId = $"FO-{Random.Range(100, 1000)}";
        ApplyAppearance();
    }

    private void ResolveRenderers()
    {
        Transform visual = transform.Find("SpyDisguiseVisual");
        if (visual == null)
            return;
        bodyRenderer ??= visual.Find("EmployeeBody")?.GetComponent<Renderer>();
        headRenderer ??= visual.Find("EmployeeHead")?.GetComponent<Renderer>();
    }

    private void ApplyAppearance()
    {
        ResolveRenderers();
        ApplyColor(bodyRenderer, OfficeNpcController.GetEmployeeUniformColor(appearanceVariant));
        ApplyColor(headRenderer, OfficeNpcController.GetEmployeeSkinColor(appearanceVariant));
    }

    private static void ApplyColor(Renderer target, Color color)
    {
        if (target == null)
            return;
        var block = new MaterialPropertyBlock();
        target.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        target.SetPropertyBlock(block);
    }

    public string GetEmployeeId() => employeeId;
    public int GetAppearanceVariant() => appearanceVariant;
    public bool HasVisibleDisguise() => bodyRenderer != null && headRenderer != null;
}
