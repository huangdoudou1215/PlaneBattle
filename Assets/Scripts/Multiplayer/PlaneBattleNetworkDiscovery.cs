using Mirror;
using Mirror.Discovery;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

[DisallowMultipleComponent]
public class PlaneBattleNetworkDiscovery : NetworkDiscoveryBase<PlaneBattleServerRequest, PlaneBattleServerResponse>
{
    public event Action<PlaneBattleServerResponse> OnRoomFound;

    public void BroadcastDiscoveryRequestAll()
    {
        BroadcastDiscoveryRequest();

        if (clientUdpClient == null)
        {
            return;
        }

        using (NetworkWriterPooled writer = NetworkWriterPool.Get())
        {
            writer.WriteLong(secretHandshake);
            writer.Write(new PlaneBattleServerRequest());
            ArraySegment<byte> data = writer.ToArraySegment();

            foreach (IPAddress address in GetCandidateBroadcastAddresses())
            {
                try
                {
                    IPEndPoint endPoint = new IPEndPoint(address, serverBroadcastListenPort);
                    clientUdpClient.Send(data.Array, data.Count, endPoint);
                    Debug.Log($"[Discovery] Send request -> {endPoint}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Discovery] Send request failed: {address}, {ex.Message}");
                }
            }
        }
    }

    protected override PlaneBattleServerResponse ProcessRequest(PlaneBattleServerRequest request, IPEndPoint endpoint)
    {
        PlaneBattleNetworkManager manager = NetworkManager.singleton as PlaneBattleNetworkManager;
        if (manager == null)
        {
#if UNITY_2023_1_OR_NEWER
            manager = FindFirstObjectByType<PlaneBattleNetworkManager>();
#else
            manager = FindObjectOfType<PlaneBattleNetworkManager>();
#endif
        }

        if (manager == null)
        {
            Debug.LogWarning("[Discovery] Ignore request: PlaneBattleNetworkManager not found.");
            return default;
        }

        try
        {
            PlaneBattleServerResponse response = new PlaneBattleServerResponse
            {
                serverId = ServerId,
                uri = transport.ServerUri(),
                roomName = string.IsNullOrWhiteSpace(manager.HostRoomName) ? "My Room" : manager.HostRoomName,
                currentPlayers = manager.numPlayers,
                maxPlayers = manager.maxConnections
            };

            Debug.Log($"[Discovery] Reply -> {endpoint.Address}:{endpoint.Port} | {response.roomName} [{response.currentPlayers}/{response.maxPlayers}]");
            return response;
        }
        catch (NotImplementedException)
        {
            Debug.LogError($"Transport {transport} does not support network discovery");
            throw;
        }
    }

    protected override PlaneBattleServerRequest GetRequest()
    {
        return new PlaneBattleServerRequest();
    }

    protected override void ProcessResponse(PlaneBattleServerResponse response, IPEndPoint endpoint)
    {
        if (response.uri == null)
        {
            Debug.LogWarning($"[Discovery] Ignore invalid response from {endpoint} (uri is null).");
            return;
        }

        response.EndPoint = endpoint;

        UriBuilder realUri = new UriBuilder(response.uri)
        {
            Host = endpoint.Address.ToString()
        };
        response.uri = realUri.Uri;

        Debug.Log($"[Discovery] Found room <- {response.roomName} [{response.currentPlayers}/{response.maxPlayers}] {response.uri}");
        OnRoomFound?.Invoke(response);
    }

    private IEnumerable<IPAddress> GetCandidateBroadcastAddresses()
    {
        HashSet<IPAddress> addresses = new HashSet<IPAddress>
        {
            IPAddress.Broadcast,
            IPAddress.Loopback
        };

        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            IPInterfaceProperties ipProps = ni.GetIPProperties();
            foreach (UnicastIPAddressInformation ua in ipProps.UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null)
                {
                    continue;
                }

                byte[] ipBytes = ua.Address.GetAddressBytes();
                byte[] maskBytes = ua.IPv4Mask.GetAddressBytes();
                byte[] broadcastBytes = new byte[4];

                for (int i = 0; i < 4; i++)
                {
                    broadcastBytes[i] = (byte)(ipBytes[i] | (~maskBytes[i]));
                }

                addresses.Add(new IPAddress(broadcastBytes));
            }
        }

        return addresses;
    }
}
