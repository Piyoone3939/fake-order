using UnityEngine;

/// <summary>
/// 脱出ポイント。MonoBehaviourをシーン再読み込み後も復元できるよう、同名ファイルに分離する。
/// </summary>
public class ExitPoint : MonoBehaviour, IInteractable
{
    public void Interact(SpyController spy)
    {
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
