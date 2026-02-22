using Mirror;
using System;
using System.Net;

public struct PlaneBattleServerResponse : NetworkMessage
{
    public IPEndPoint EndPoint { get; set; }

    public Uri uri;
    public long serverId;

    public string roomName;
    public int currentPlayers;
    public int maxPlayers;
}
