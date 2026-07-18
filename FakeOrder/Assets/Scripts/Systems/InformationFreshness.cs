using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 情報鮮度システム
/// 情報の劣化管理と罠情報の配置
/// </summary>
public class InformationFreshness : MonoBehaviour
{
    [SerializeField] private float freshDuration = 60f;
    [SerializeField] private float degradedDuration = 180f;

    private Dictionary<int, GameState.InformationData> informationStates = 
        new Dictionary<int, GameState.InformationData>();

    public void Initialize()
    {
        Debug.Log("✓ InformationFreshness initialized");
    }

    public void RecordInformationCollection(int terminalId, bool isTrap = false)
    {
        var info = new GameState.InformationData
        {
            terminalId = terminalId,
            collectedTime = Time.time,
            isTrap = isTrap
        };

        informationStates[terminalId] = info;
        GameState.Instance.AddCollectedInformation(terminalId, isTrap);

        Debug.Log($"📊 Information collected: Terminal #{terminalId} {(isTrap ? "[TRAP]" : "")}");
    }

    public GameState.InformationData GetInformationState(int terminalId)
    {
        if (informationStates.ContainsKey(terminalId))
            return informationStates[terminalId];
        return null;
    }

    public float GetInformationFreshness(int terminalId)
    {
        var info = GetInformationState(terminalId);
        if (info == null) return 0f;

        float age = Time.time - info.collectedTime;
        
        if (age < freshDuration)
            return 1f; // 新鮮
        else if (age < degradedDuration)
            return 0.5f; // 劣化中
        else
            return 0f; // 腐敗
    }

    public string GetFreshnessLabel(int terminalId)
    {
        float freshness = GetInformationFreshness(terminalId);
        
        if (freshness >= 1f) return "🟦 新鮮";
        if (freshness >= 0.5f) return "🟨 劣化";
        return "🟥 腐敗";
    }

    public void PlaceTrapInformation(int terminalId, string trapContent)
    {
        var info = new GameState.InformationData
        {
            terminalId = terminalId,
            collectedTime = Time.time,
            isTrap = true
        };

        informationStates[terminalId] = info;

        Debug.Log($"🪤 Trap information placed at Terminal #{terminalId}");
    }

    public bool IsTrapInformation(int terminalId)
    {
        var info = GetInformationState(terminalId);
        return info != null && info.isTrap;
    }

    public void ResetInformationStates()
    {
        informationStates.Clear();
    }

    public void UpdateAllInformationFreshness()
    {
        // 全情報の鮮度を更新（毎フレーム呼び出し不要、UI表示時のみで十分）
        foreach (var kvp in informationStates)
        {
            // 鮮度は自動計算
        }
    }
}
