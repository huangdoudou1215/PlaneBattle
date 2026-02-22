using Mirror;
using UnityEngine;

/// <summary>
/// 双人模式网络管理器。
/// </summary>
public class PlaneBattleNetworkManager : NetworkManager
{
    private const float SpawnYOffset = -3.5f;

    public override void Awake()
    {
        base.Awake();
        autoCreatePlayer = false;

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
    }
}
