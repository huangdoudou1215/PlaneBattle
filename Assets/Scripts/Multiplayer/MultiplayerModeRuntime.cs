using Mirror;
using Mirror.Discovery;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 双人联机大厅入口：创建房间、发现房间、加入房间、返回主菜单。
/// </summary>
public class MultiplayerModeRuntime : MonoBehaviour
{
    private const long DiscoveryHandshake = 2026022201L;

    private static MultiplayerModeRuntime s_instance;

    private PlaneBattleNetworkManager m_networkManager;
    private PlaneBattleNetworkDiscovery m_networkDiscovery;
    private readonly Dictionary<long, PlaneBattleServerResponse> m_discoveredRooms = new Dictionary<long, PlaneBattleServerResponse>();
    private readonly List<string> m_chatLines = new List<string>();
    private Vector2 m_scrollPos;
    private Vector2 m_chatScrollPos;
    private string m_roomName = "My Room";
    private string m_chatInput = string.Empty;
    private int m_displayPlayers;
    private int m_displayMaxPlayers = 2;

    public static bool IsActive => s_instance != null;

    public static void Enter()
    {
        if (s_instance != null)
        {
            return;
        }

        GameObject go = new GameObject("MultiplayerModeRuntime");
        s_instance = go.AddComponent<MultiplayerModeRuntime>();
    }

    private void Awake()
    {
        TelepathyTransport transport = gameObject.AddComponent<TelepathyTransport>();

        m_networkManager = gameObject.AddComponent<PlaneBattleNetworkManager>();
        m_networkManager.transport = transport;
        m_networkManager.maxConnections = 2;

        m_networkDiscovery = gameObject.AddComponent<PlaneBattleNetworkDiscovery>();
        m_networkDiscovery.transport = transport;
        m_networkDiscovery.secretHandshake = DiscoveryHandshake;
        m_networkDiscovery.OnRoomFound += OnRoomFound;

        Debug.Log($"[Discovery] Handshake={m_networkDiscovery.secretHandshake}, Port=47777");

        NetworkClient.RegisterHandler<PlaneBattlePlayerCountMessage>(OnPlayerCountMessage, false);
        PlaneBattleNetworkManager.ChatMessageReceived += OnChatMessageReceived;
    }

    private void OnGUI()
    {
        if (!NetworkServer.active && !NetworkClient.active)
        {
            DrawLobbyGui();
        }
        else
        {
            DrawRuntimeGui();
        }
    }

    private void DrawLobbyGui()
    {
        GUI.Box(new Rect(10f, 230f, 420f, 360f), "联机大厅");

        GUI.Label(new Rect(20f, 260f, 80f, 25f), "房间名");
        m_roomName = GUI.TextField(new Rect(90f, 260f, 180f, 25f), m_roomName, 24);

        if (GUI.Button(new Rect(280f, 260f, 140f, 25f), "创建房间(主机)"))
        {
            StartHostAndAdvertise();
        }

        if (GUI.Button(new Rect(20f, 295f, 120f, 30f), "刷新房间列表"))
        {
            RefreshRoomList();
        }

        GUI.Label(new Rect(150f, 300f, 270f, 25f), $"已发现房间: {m_discoveredRooms.Count}");

        GUILayout.BeginArea(new Rect(20f, 330f, 400f, 210f));
        m_scrollPos = GUILayout.BeginScrollView(m_scrollPos, GUI.skin.box);

        foreach (PlaneBattleServerResponse room in m_discoveredRooms.Values)
        {
            GUILayout.BeginHorizontal("box");
            GUILayout.Label($"{room.roomName} [{room.currentPlayers}/{room.maxPlayers}]");
            GUILayout.FlexibleSpace();

            bool isFull = room.currentPlayers >= room.maxPlayers;
            GUI.enabled = !isFull;
            if (GUILayout.Button(isFull ? "已满" : "加入", GUILayout.Width(70f)))
            {
                JoinRoom(room);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        if (GUI.Button(new Rect(20f, 550f, 400f, 30f), "返回主菜单"))
        {
            Exit();
        }
    }

    private void DrawRuntimeGui()
    {
        string mode;
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            mode = "当前模式: 主机";
        }
        else if (NetworkClient.isConnected)
        {
            mode = "当前模式: 客户端";
        }
        else
        {
            mode = "当前模式: 服务器";
        }

        GUI.Box(new Rect(10f, 230f, 420f, 330f), "联机状态");
        GUI.Label(new Rect(20f, 260f, 300f, 25f), mode);

        int currentPlayers = NetworkServer.active ? m_networkManager.numPlayers : m_displayPlayers;
        int maxPlayers = NetworkServer.active ? m_networkManager.maxConnections : m_displayMaxPlayers;
        GUI.Label(new Rect(20f, 285f, 300f, 25f), $"玩家数: {currentPlayers}/{maxPlayers}");

        GUILayout.BeginArea(new Rect(20f, 315f, 390f, 180f));
        m_chatScrollPos = GUILayout.BeginScrollView(m_chatScrollPos, GUI.skin.box);
        foreach (string line in m_chatLines)
        {
            GUILayout.Label(line);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        m_chatInput = GUI.TextField(new Rect(20f, 505f, 300f, 25f), m_chatInput, 120);
        if (GUI.Button(new Rect(330f, 505f, 80f, 25f), "发送"))
        {
            SendChatMessage();
        }

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
        {
            SendChatMessage();
            Event.current.Use();
        }

        if (GUI.Button(new Rect(20f, 535f, 390f, 20f), "退出联机并返回主菜单"))
        {
            Exit();
        }
    }

    private void StartHostAndAdvertise()
    {
        if (NetworkServer.active || NetworkClient.active)
        {
            return;
        }

        m_discoveredRooms.Clear();
        m_networkManager.HostRoomName = string.IsNullOrWhiteSpace(m_roomName) ? "My Room" : m_roomName.Trim();
        m_networkManager.StartHost();
        m_networkDiscovery.AdvertiseServer();
    }

    private void RefreshRoomList()
    {
        if (NetworkClient.active || NetworkServer.active)
        {
            return;
        }

        m_discoveredRooms.Clear();
        m_networkDiscovery.StartDiscovery();
        m_networkDiscovery.BroadcastDiscoveryRequestAll();
    }

    private void JoinRoom(PlaneBattleServerResponse room)
    {
        if (NetworkClient.active || NetworkServer.active)
        {
            return;
        }

        m_displayPlayers = room.currentPlayers;
        m_displayMaxPlayers = room.maxPlayers;

        m_networkDiscovery.StopDiscovery();
        m_networkManager.StartClient(room.uri);
    }

    private void OnRoomFound(PlaneBattleServerResponse room)
    {
        m_discoveredRooms[room.serverId] = room;
    }

    private void OnPlayerCountMessage(PlaneBattlePlayerCountMessage msg)
    {
        m_displayPlayers = msg.currentPlayers;
        m_displayMaxPlayers = msg.maxPlayers;
    }

    private void OnChatMessageReceived(PlaneBattleChatBroadcastMessage msg)
    {
        m_chatLines.Add($"[{msg.senderName}] {msg.text}");
        if (m_chatLines.Count > 50)
        {
            m_chatLines.RemoveAt(0);
        }

        m_chatScrollPos.y = float.MaxValue;
    }

    private void SendChatMessage()
    {
        if (!NetworkClient.isConnected)
        {
            return;
        }

        string text = m_chatInput.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        NetworkClient.Send(new PlaneBattleChatSendMessage { text = text });
        m_chatInput = string.Empty;
    }

    private void Exit()
    {
        if (m_networkDiscovery != null)
        {
            m_networkDiscovery.StopDiscovery();
            m_networkDiscovery.OnRoomFound -= OnRoomFound;
        }

        NetworkClient.UnregisterHandler<PlaneBattlePlayerCountMessage>();
        PlaneBattleNetworkManager.ChatMessageReceived -= OnChatMessageReceived;

        if (NetworkServer.active && NetworkClient.isConnected)
        {
            m_networkManager.StopHost();
        }
        else if (NetworkClient.active)
        {
            m_networkManager.StopClient();
        }
        else if (NetworkServer.active)
        {
            m_networkManager.StopServer();
        }

        PanelMgr.instance.ShowPanel<StartGamePanel>();

        s_instance = null;
        Destroy(gameObject);
    }
}
