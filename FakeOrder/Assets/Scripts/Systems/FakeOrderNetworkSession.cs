using System.Collections.Generic;
using System.Linq;
using System;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Phase 3.1のLAN対戦基盤。プレイヤーオブジェクトを生成せず、
/// Custom Messageでロールと試合進行だけをサーバー権限で同期する。
/// </summary>
public class FakeOrderNetworkSession : MonoBehaviour
{
    private const string ReadyMessage = "FakeOrder.ClientReady.v1";
    private const string RoleMessage = "FakeOrder.Role.v1";
    private const string StateMessage = "FakeOrder.State.v1";
    private const string ResultRequestMessage = "FakeOrder.ResultRequest.v1";
    private const ushort DefaultPort = 7777;
    private const float SnapshotInterval = 0.1f;

    public static FakeOrderNetworkSession Instance { get; private set; }

    private readonly HashSet<ulong> readyClients = new HashSet<ulong>();
    private NetworkManager networkManager;
    private UnityTransport transport;
    private string serverAddress = "127.0.0.1";
    private string status = "ONLINE (LAN) / 未接続";
    private bool handlersRegistered;
    private bool rolesAssigned;
    private bool offlineMode;
    private ulong spyClientId = ulong.MaxValue;
    private float nextSnapshotAt;
    private float smokeTestExitAt = -1f;
    private bool smokeTestMode;
    private GameManager.LocalRole assignedRole = GameManager.LocalRole.None;

    public bool IsConnected => networkManager != null && networkManager.IsListening;
    public bool IsServer => IsConnected && networkManager.IsServer;
    public bool IsRemoteClient => IsConnected && networkManager.IsClient && !networkManager.IsServer;
    public bool HasAssignedRole => rolesAssigned && assignedRole != GameManager.LocalRole.None;
    public float SynchronizedRemainingSeconds { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureCreated()
    {
        if (Instance != null)
            return;
        new GameObject("FakeOrderNetworkSession").AddComponent<FakeOrderNetworkSession>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        transport = gameObject.AddComponent<UnityTransport>();
        networkManager = gameObject.AddComponent<NetworkManager>();
        networkManager.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = false,
            ConnectionApproval = true,
            TickRate = 30
        };
        networkManager.ConnectionApprovalCallback = ApproveConnection;
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void Start()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        bool runAsHost = arguments.Contains("-fakeOrderHost");
        bool runAsClient = arguments.Contains("-fakeOrderClient");
        if (!runAsHost && !runAsClient)
            return;

        smokeTestMode = true;
        serverAddress = ReadCommandLineValue(arguments, "-fakeOrderAddress", "127.0.0.1");
        if (runAsHost)
            StartHostSession();
        else
            StartClientSession(serverAddress);
    }

    private void Update()
    {
        if (smokeTestExitAt > 0f && Time.unscaledTime >= smokeTestExitAt)
        {
            Application.Quit(0);
            return;
        }

        if (!IsConnected)
            return;

        RegisterMessageHandlers();
        if (!IsServer)
            return;

        TryAssignRoles();
        if (!rolesAssigned || Time.unscaledTime < nextSnapshotAt)
            return;

        nextSnapshotAt = Time.unscaledTime + SnapshotInterval;
        BroadcastState();
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        bool hasRoom = networkManager.ConnectedClientsIds.Count < 2 ||
            request.ClientNetworkId == NetworkManager.ServerClientId;
        response.Approved = hasRoom;
        response.CreatePlayerObject = false;
        response.Pending = false;
        response.Reason = hasRoom ? string.Empty : "Fake Order session is full (2/2).";
    }

    public bool StartHostSession()
    {
        if (IsConnected)
            return false;
        offlineMode = false;
        ResetSessionState();
        transport.SetConnectionData("127.0.0.1", DefaultPort, "0.0.0.0");
        bool started = networkManager.StartHost();
        if (!started)
        {
            status = "HOST起動に失敗しました";
            return false;
        }

        RegisterMessageHandlers();
        readyClients.Add(networkManager.LocalClientId);
        status = "HOST / Clientの接続待ち (PORT 7777)";
        return true;
    }

    public bool StartClientSession(string address)
    {
        if (IsConnected)
            return false;
        offlineMode = false;
        ResetSessionState();
        serverAddress = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
        transport.SetConnectionData(serverAddress, DefaultPort);
        bool started = networkManager.StartClient();
        status = started ? $"CLIENT / {serverAddress}:7777 へ接続中..." : "CLIENT起動に失敗しました";
        if (started)
            RegisterMessageHandlers();
        return started;
    }

    public void UseOfflineMode()
    {
        ShutdownSession();
        offlineMode = true;
        status = "OFFLINE / ローカル検証";
        GameManager.Instance?.OpenRoleSelection();
    }

    public void ShutdownSession()
    {
        if (networkManager != null && networkManager.IsListening)
            networkManager.Shutdown();
        ResetSessionState();
        status = "ONLINE (LAN) / 未接続";
    }

    private void ResetSessionState()
    {
        handlersRegistered = false;
        readyClients.Clear();
        rolesAssigned = false;
        assignedRole = GameManager.LocalRole.None;
        spyClientId = ulong.MaxValue;
        SynchronizedRemainingSeconds = 0f;
        nextSnapshotAt = 0f;
    }

    private void RegisterMessageHandlers()
    {
        if (handlersRegistered || networkManager.CustomMessagingManager == null)
            return;
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ReadyMessage, OnReadyMessage);
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(RoleMessage, OnRoleMessage);
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage, OnStateMessage);
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ResultRequestMessage, OnResultRequestMessage);
        handlersRegistered = true;
    }

    private void OnClientConnected(ulong clientId)
    {
        RegisterMessageHandlers();
        if (networkManager.IsServer)
        {
            if (clientId == networkManager.LocalClientId)
                readyClients.Add(clientId);
            status = networkManager.ConnectedClientsIds.Count < 2
                ? "HOST / Clientの接続待ち (PORT 7777)"
                : "HOST / Client初期化待ち";
        }

        if (networkManager.IsClient && clientId == networkManager.LocalClientId && !networkManager.IsServer)
        {
            status = "CLIENT / 接続済み・ロール割り当て待ち";
            using var writer = new FastBufferWriter(sizeof(byte), Allocator.Temp);
            writer.WriteValueSafe((byte)1);
            networkManager.CustomMessagingManager.SendNamedMessage(
                ReadyMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        readyClients.Remove(clientId);
        if (networkManager != null && networkManager.IsServer)
        {
            if (rolesAssigned && clientId == spyClientId)
                GameManager.Instance?.CancelNetworkMatch();
            rolesAssigned = false;
            spyClientId = ulong.MaxValue;
            assignedRole = GameManager.LocalRole.None;
            status = "HOST / Clientが切断しました・再接続待ち";
            return;
        }

        if (clientId == networkManager?.LocalClientId)
        {
            string reason = string.IsNullOrEmpty(networkManager.DisconnectReason)
                ? "Hostから切断されました"
                : networkManager.DisconnectReason;
            ResetSessionState();
            status = $"CLIENT / {reason}";
            GameManager.Instance?.CancelNetworkMatch();
        }
    }

    private void OnReadyMessage(ulong senderId, FastBufferReader reader)
    {
        if (!networkManager.IsServer || !networkManager.ConnectedClientsIds.Contains(senderId))
            return;
        reader.ReadValueSafe(out byte ready);
        if (ready == 1)
            readyClients.Add(senderId);
    }

    private void TryAssignRoles()
    {
        if (rolesAssigned || networkManager.ConnectedClientsIds.Count != 2)
            return;
        foreach (ulong clientId in networkManager.ConnectedClientsIds)
        {
            if (!readyClients.Contains(clientId))
                return;
        }

        foreach (ulong clientId in networkManager.ConnectedClientsIds)
        {
            GameManager.LocalRole role = clientId == networkManager.LocalClientId
                ? GameManager.LocalRole.Organizer
                : GameManager.LocalRole.Spy;
            if (role == GameManager.LocalRole.Spy)
                spyClientId = clientId;

            if (clientId == networkManager.LocalClientId)
                ApplyAssignedRole(role);
            else
                SendRole(clientId, role);
        }

        rolesAssigned = true;
        status = "HOST / 2人接続済み / あなたはオーガナイザー";
    }

    private void SendRole(ulong clientId, GameManager.LocalRole role)
    {
        using var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
        writer.WriteValueSafe((int)role);
        networkManager.CustomMessagingManager.SendNamedMessage(
            RoleMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
    }

    private void OnRoleMessage(ulong senderId, FastBufferReader reader)
    {
        if (senderId != NetworkManager.ServerClientId || networkManager.IsServer)
            return;
        reader.ReadValueSafe(out int roleValue);
        GameManager.LocalRole role = (GameManager.LocalRole)roleValue;
        if (role != GameManager.LocalRole.Spy && role != GameManager.LocalRole.Organizer)
            return;
        rolesAssigned = true;
        ApplyAssignedRole(role);
        status = role == GameManager.LocalRole.Spy
            ? "CLIENT / 2人接続済み / あなたはスパイ"
            : "CLIENT / 2人接続済み / あなたはオーガナイザー";
    }

    private void ApplyAssignedRole(GameManager.LocalRole role)
    {
        assignedRole = role;
        GameManager.Instance?.ApplyNetworkRole(role);
        if (smokeTestMode)
        {
            Debug.Log($"NETWORK_SMOKE_PASS role={role} server={IsServer} clients={networkManager.ConnectedClientsIds.Count}");
            smokeTestExitAt = Time.unscaledTime + 1.5f;
        }
    }

    private static string ReadCommandLineValue(string[] arguments, string key, string fallback)
    {
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (string.Equals(arguments[i], key, StringComparison.OrdinalIgnoreCase))
                return arguments[i + 1];
        }
        return fallback;
    }

    private void BroadcastState()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || spyClientId == ulong.MaxValue)
            return;
        using var writer = new FastBufferWriter(sizeof(int) * 2 + sizeof(float), Allocator.Temp);
        writer.WriteValueSafe((int)manager.GetCurrentPhase());
        writer.WriteValueSafe(manager.GetNetworkRemainingSeconds());
        writer.WriteValueSafe((int)manager.GetLastResult());
        networkManager.CustomMessagingManager.SendNamedMessage(
            StateMessage, spyClientId, writer, NetworkDelivery.UnreliableSequenced);
    }

    private void OnStateMessage(ulong senderId, FastBufferReader reader)
    {
        if (senderId != NetworkManager.ServerClientId || networkManager.IsServer)
            return;
        reader.ReadValueSafe(out int phaseValue);
        reader.ReadValueSafe(out float remainingSeconds);
        reader.ReadValueSafe(out int resultValue);
        SynchronizedRemainingSeconds = Mathf.Max(0f, remainingSeconds);
        GameManager.Instance?.ApplyNetworkSnapshot(
            (GameManager.GamePhase)phaseValue, SynchronizedRemainingSeconds, (GameResult)resultValue);
    }

    public void RequestGameResult(GameResult result)
    {
        if (!IsRemoteClient || !rolesAssigned)
            return;
        using var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
        writer.WriteValueSafe((int)result);
        networkManager.CustomMessagingManager.SendNamedMessage(
            ResultRequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
    }

    private void OnResultRequestMessage(ulong senderId, FastBufferReader reader)
    {
        if (!networkManager.IsServer || senderId != spyClientId)
            return;
        reader.ReadValueSafe(out int resultValue);
        GameResult result = (GameResult)resultValue;
        if (result == GameResult.SpyEscaped || result == GameResult.IncompleteEscape)
            GameManager.Instance?.ApplyAuthoritativeNetworkResult(result);
    }

    private void OnGUI()
    {
        if (offlineMode || smokeTestMode)
            return;
        const float width = 420f;
        float height = IsConnected ? 104f : 190f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, 18f, width, height);
        GUI.Box(panel, string.Empty);
        GUI.Label(new Rect(panel.x + 16f, panel.y + 10f, width - 32f, 26f), status);

        if (IsConnected)
        {
            if (GUI.Button(new Rect(panel.x + 145f, panel.y + 52f, 130f, 34f), "DISCONNECT"))
                ShutdownSession();
            return;
        }

        GUI.Label(new Rect(panel.x + 16f, panel.y + 43f, 72f, 28f), "HOST IP");
        serverAddress = GUI.TextField(new Rect(panel.x + 90f, panel.y + 42f, 314f, 30f), serverAddress, 64);
        if (GUI.Button(new Rect(panel.x + 16f, panel.y + 84f, 120f, 40f), "HOST"))
            StartHostSession();
        if (GUI.Button(new Rect(panel.x + 150f, panel.y + 84f, 120f, 40f), "JOIN"))
            StartClientSession(serverAddress);
        if (GUI.Button(new Rect(panel.x + 284f, panel.y + 84f, 120f, 40f), "OFFLINE"))
            UseOfflineMode();
        GUI.Label(new Rect(panel.x + 16f, panel.y + 137f, width - 32f, 38f),
            "LAN内のHost IPを入力 / PORT 7777 / 先着2人");
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    public NetworkManager GetNetworkManagerForEditor() => networkManager;
    public UnityTransport GetTransportForEditor() => transport;
    public ushort GetPortForEditor() => DefaultPort;
#endif
}
