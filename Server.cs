using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using UnityEngine;

public class Server
{
    private readonly Logger _logger;
    private readonly UdpClient _udpServer;
    private List<IPEndPoint> _peers;
    private Dictionary<int, Vector3> _latestPlayerPositions;
    private readonly string _ipAddress;
    private readonly int _port;
    public Server(string ipAddress, int port, string logFilePath)
    {
        _peers = new List<IPEndPoint>();
        _latestPlayerPositions = new Dictionary<int, Vector3>();
        _udpServer = new UdpClient(port);
        _logger = new Logger("[Server]", logFilePath);
        _ipAddress = ipAddress;
        _port = port;

    }
    public void Start()
    {
        try
        {
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Parse(_ipAddress), _port);

            _logger.Log($"Server started on  {clientEndpoint.Address} {clientEndpoint.Port}");

            while (true)
            {
                try
                {
                    byte[] data = _udpServer.Receive(ref clientEndpoint);
                    _logger.Log($"Received {data.Length} bytes from {clientEndpoint.Address}:{clientEndpoint.Port}");

                    ProcessReceivedDataServer(clientEndpoint, data);
                }
                catch (Exception e)
                {
                    _logger.Log(e.ToString());
                }
            }
        }
        catch (Exception e)
        {
            _logger.Log(e.ToString());
        }
    }

    void ProcessReceivedDataServer(IPEndPoint sender, byte[] data)
    {
        string message = Encoding.UTF8.GetString(data);
        string[] parts = message.Split('|');

        // Add sender to the _peers list if not already there
        if (!_peers.Contains(sender))
        {
            _peers.Add(sender);
            _logger.Log($"Added {sender.Address}:{sender.Port} to the list of peers");

            // Send latest positions to the new player
            foreach (var player in _latestPlayerPositions)
            {
                if (player.Key != Int32.Parse(parts[1]))
                {
                    string positionString = $"ADD_PLAYER|{player.Key}|{player.Value.x}|{player.Value.y}|{player.Value.z}";
                    byte[] data2 = Encoding.UTF8.GetBytes(positionString);
                    _udpServer.Send(data2, data2.Length, sender);
                }
            }
        }

        switch (parts[0])
        {
            case "POSITION_UPDATE":
                int playerId = int.Parse(parts[1]);
                float x = float.Parse(parts[2]);
                float y = float.Parse(parts[3]);
                float z = float.Parse(parts[4]);
                Vector3 position = new Vector3(x, y, z);

                // Update the latest player position
                if (!_latestPlayerPositions.ContainsKey(playerId))
                {
                    _latestPlayerPositions.Add(playerId, position);
                }
                else
                {
                    _latestPlayerPositions[playerId] = position;
                }
                break;
        }

        // Process the data, handle different messages, and update the game state
        // Broadcast the data to all connected peers
        foreach (var peer in _peers)
        {
            if (!peer.Equals(sender))
            {
                _udpServer.Send(data, data.Length, peer);

                _logger.Log($"Sent {data.Length} bytes to {peer.Address}:{peer.Port}");
            }
        }
    }
}
