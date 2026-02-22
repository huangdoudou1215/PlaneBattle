using Mirror;
using System;
using UnityEngine;

public struct PlaneBattlePlayerCountMessage : NetworkMessage
{
    public int currentPlayers;
    public int maxPlayers;
}

public struct PlaneBattleChatSendMessage : NetworkMessage
{
    public string text;
}

public struct PlaneBattleChatBroadcastMessage : NetworkMessage
{
    public int senderConnectionId;
    public string senderName;
    public string text;
}

/// <summary>
/// 双人模式网络管理器。
/// </summary>
public class PlaneBattleNetworkManager : NetworkManager
{
    private const float SpawnYOffset = -3.5f;
    private const string DefaultRoomName = "My Room";
    private const int MaxChatLength = 120;

    public static event Action<PlaneBattleChatBroadcastMessage> ChatMessageReceived;

    public string HostRoomName { get; set; } = DefaultRoomName;

    public override void Awake()
    {
        base.Awake();
        autoCreatePlayer = false;

        if (string.IsNullOrWhiteSpace(HostRoomName))
        {
            HostRoomName = DefaultRoomName;
        }

        if (playerPrefab == null)
        {
            playerPrefab = Resources.Load<GameObject>("Player/NetworkPlayer");
        }

        if (playerPrefab == null)
        {
            Debug.LogError("PlaneBattleNetworkManager: missing player prefab at Resources/Player/NetworkPlayer.prefab");
        }
        else if (!spawnPrefabs.Contains(playerPrefab))
        {
            spawnPrefabs.Add(playerPrefab);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<PlaneBattleChatSendMessage>(OnServerChatMessageReceived);
    }

    public override void OnStopServer()
    {
        NetworkServer.UnregisterHandler<PlaneBattleChatSendMessage>();
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        NetworkClient.RegisterHandler<PlaneBattleChatBroadcastMessage>(OnClientChatMessageReceived, false);
    }

    public override void OnStopClient()
    {
        NetworkClient.UnregisterHandler<PlaneBattleChatBroadcastMessage>();
        base.OnStopClient();
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();

        if (NetworkClient.connection != null && NetworkClient.localPlayer == null)
        {
            NetworkClient.AddPlayer();
        }
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("PlaneBattleNetworkManager: playerPrefab is null, cannot spawn player.");
            return;
        }

        GameObject player = Instantiate(playerPrefab);
        player.name = $"NetPlane_{numPlayers + 1}";
        player.transform.position = new Vector3(numPlayers == 0 ? -2f : 2f, SpawnYOffset, 0f);

        PlaneBattleNetworkPlayer networkPlayer = player.GetComponent<PlaneBattleNetworkPlayer>();
        if (networkPlayer != null)
        {
            networkPlayer.playerIndex = numPlayers;
        }

        NetworkServer.AddPlayerForConnection(conn, player);
        BroadcastPlayerCount();
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        BroadcastPlayerCount();
    }

    private void BroadcastPlayerCount()
    {
        PlaneBattlePlayerCountMessage msg = new PlaneBattlePlayerCountMessage
        {
            currentPlayers = numPlayers,
            maxPlayers = maxConnections
        };

        NetworkServer.SendToAll(msg);
    }

    private void OnServerChatMessageReceived(NetworkConnectionToClient conn, PlaneBattleChatSendMessage msg)
    {
        string text = NormalizeChatText(msg.text);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        PlaneBattleChatBroadcastMessage broadcast = new PlaneBattleChatBroadcastMessage
        {
            senderConnectionId = conn.connectionId,
            senderName = conn.connectionId == 0 ? "Host" : $"P{conn.connectionId}",
            text = text
        };

        NetworkServer.SendToAll(broadcast);
    }

    private void OnClientChatMessageReceived(PlaneBattleChatBroadcastMessage msg)
    {
        ChatMessageReceived?.Invoke(msg);
    }

    private string NormalizeChatText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string normalized = text.Trim();
        if (normalized.Length > MaxChatLength)
        {
            normalized = normalized.Substring(0, MaxChatLength);
        }

        return normalized;
    }
}
