using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ゲーム全体で共有されるゲーム状態
/// </summary>
public class GameState : MonoBehaviour
{
    [System.Serializable]
    public class PlayerInfo
    {
        public enum Role { Spy, Organizer }
        public Role role;
        public bool isReady;
    }

    [System.Serializable]
    public class InformationData
    {
        public int terminalId;
        public float collectedTime;
        public bool isFresh => Time.time - collectedTime < 60f;
        public bool isDegraded => Time.time - collectedTime >= 60f && Time.time - collectedTime < 180f;
        public bool isCorrupted => Time.time - collectedTime >= 180f;
        public bool isTrap;
        public Vector3 terminalPosition;
    }

    public static GameState Instance { get; private set; }

    [SerializeField] private PlayerInfo spyPlayer = new PlayerInfo();
    [SerializeField] private PlayerInfo organizerPlayer = new PlayerInfo();

    public List<InformationData> CollectedInformation { get; private set; } = new List<InformationData>();
    public float GameStartTime { get; private set; }
    public float GameMaxDuration = 900f; // 15分
    public bool IsGameActive { get; set; } = false;

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

    public void InitializeGame(PlayerInfo.Role spyRole, PlayerInfo.Role organizerRole)
    {
        spyPlayer.role = spyRole;
        organizerPlayer.role = organizerRole;
        GameStartTime = Time.time;
        IsGameActive = true;
        CollectedInformation.Clear();
    }

    public float GetElapsedTime()
    {
        return Time.time - GameStartTime;
    }

    public bool IsTimeUp()
    {
        return GetElapsedTime() >= GameMaxDuration;
    }

    public void AddCollectedInformation(int terminalId, bool isTrap = false, Vector3 position = default)
    {
        InformationData existing = CollectedInformation.Find(info => info.terminalId == terminalId);
        if (existing != null)
        {
            // 同じ端末の再収集は件数を増やさず、鮮度と内容だけを更新する。
            existing.collectedTime = Time.time;
            existing.isTrap = isTrap;
            existing.terminalPosition = position;
            return;
        }

        var info = new InformationData
        {
            terminalId = terminalId,
            collectedTime = Time.time,
            isTrap = isTrap,
            terminalPosition = position
        };
        CollectedInformation.Add(info);
    }

    public int GetValidInformationCount()
    {
        int count = 0;
        foreach (InformationData info in CollectedInformation)
        {
            if (!info.isTrap && info.isFresh)
                count++;
        }
        return count;
    }
}
