using Mirror;
using UnityEngine;

/// <summary>
/// 双人模式入口与返回控制（与原单人逻辑解耦）。
/// </summary>
public class MultiplayerModeRuntime : MonoBehaviour
{
    private static MultiplayerModeRuntime s_instance;

    private PlaneBattleNetworkManager m_networkManager;
    private NetworkManagerHUD m_networkHud;

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

        m_networkHud = gameObject.AddComponent<NetworkManagerHUD>();
        m_networkHud.showGUI = true;
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(10f, 230f, 260f, 70f), "双人模式");
        if (GUI.Button(new Rect(20f, 260f, 240f, 30f), "返回主菜单"))
        {
            Exit();
        }
    }

    private void Exit()
    {
        if (NetworkServer.active || NetworkClient.isConnected)
        {
            m_networkManager.StopHost();
        }

        PanelMgr.instance.ShowPanel<StartGamePanel>();

        s_instance = null;
        Destroy(gameObject);
    }
}
