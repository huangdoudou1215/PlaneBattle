using Mirror;
using UnityEngine;

/// <summary>
/// 双人模式网络管理器。
/// </summary>
public class PlaneBattleNetworkManager : NetworkManager
{
    private const float SpawnYOffset = -3.5f;

    private void Awake()
    {
        autoCreatePlayer = false;
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
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Cube);
        player.name = $"NetPlane_{numPlayers + 1}";
        player.transform.position = new Vector3(numPlayers == 0 ? -2f : 2f, SpawnYOffset, 0f);
        player.transform.localScale = new Vector3(1f, 0.8f, 0.3f);

        PlaneBattleNetworkPlayer networkPlayer = player.AddComponent<PlaneBattleNetworkPlayer>();
        networkPlayer.playerIndex = numPlayers;

        NetworkServer.AddPlayerForConnection(conn, player);
    }
}
