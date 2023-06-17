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
    private Dictionary<string, Vector3> _latestPlayerPositions;
    private readonly string _ipAddress;
    private readonly int _port;
    private const int MAX_CONNECTIONS = 100;
    private bool _isRunning = true;

    public Server(string ipAddress, int port, string logFilePath)
    {
        _peers = new List<IPEndPoint>();
        _latestPlayerPositions = new Dictionary<string, Vector3>();
        _udpServer = new UdpClient(port);
        _logger = new Logger("[Server]", logFilePath);
        _ipAddress = ipAddress;
        _port = port;

    }

    public void Start()
    {
        try
        {
            _isRunning = true;
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Parse(_ipAddress), _port);
            _logger.Log($"Server started on  {clientEndpoint.Address} {clientEndpoint.Port}");

            while (_isRunning)
            {
                try
                {
                    byte[] data = _udpServer.Receive(ref clientEndpoint);
                    ProcessReceivedDataServer(clientEndpoint, data);
                }
                catch (SocketException e)
                {
                    _logger.Log($"Client disconnected from {clientEndpoint.Address}:{clientEndpoint.Port}. Message: {e.Message}");

                    int disconnectedPlayerId = _peers.IndexOf(clientEndpoint);
                    _peers.Remove(clientEndpoint);

                    string deleteMessage = $"DELETE_PLAYER|{disconnectedPlayerId}";
                    byte[] deleteData = Encoding.UTF8.GetBytes(deleteMessage);
                    foreach (var peer in _peers)
                    {
                        _udpServer.Send(deleteData, deleteData.Length, peer);
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.Log(e.ToString());
        }
        finally
        {
            _udpServer.Close();
            _logger.Log("Server closed.");
        }
    }


    void ProcessReceivedDataServer(IPEndPoint sender, byte[] data)
    {
        string message = Encoding.UTF8.GetString(data);
        string[] parts = message.Split('|');

        // Add sender to the _peers list if not already there
        if (!_peers.Contains(sender) && _peers.Count < MAX_CONNECTIONS)
        {
            _peers.Add(sender);
            _logger.Log($"Added {sender.Address}:{sender.Port} to the list of peers");

            // Send latest positions to the new player
            foreach (var player in _latestPlayerPositions)
            {
                if (player.Key != parts[1])
                {
                    string positionString = $"ADD_PLAYER|{player.Key}|{player.Value.x}|{player.Value.y}|{player.Value.z}";
                    byte[] data2 = Encoding.UTF8.GetBytes(positionString);
                    _udpServer.Send(data2, data2.Length, sender);
                }
            }
        }

        try
        {
            switch (parts[0])
            {
                case "POSITION_UPDATE":
                    if (parts.Length != 11)
                    {
                        _logger.Log($"Invalid message received from {sender.Address}:{sender.Port}, message: {String.Join(", ", parts)}");
                        return;
                    }
                    string playerName = parts[1];
                    float x = float.Parse(parts[2]);
                    float y = float.Parse(parts[3]);
                    float z = float.Parse(parts[4]);
                    Vector3 position = new Vector3(x, y, z);

                    if (!_latestPlayerPositions.ContainsKey(playerName))
                    {
                        _latestPlayerPositions.Add(playerName, position);
                    }
                    else
                    {
                        _latestPlayerPositions[playerName] = position;
                    }
                    break;
                case "ADD_PLAYER":
                    _logger.Log($"{message}");
                    break;
                case "DELETE_PLAYER":
                    if (parts.Length != 2)
                    {
                        _logger.Log($"Invalid DELETE_PLAYER message received from {sender.Address}:{sender.Port}. Message: {String.Join(", ", parts)}");
                        return;
                    }
                    string playerNameToDelete = parts[1];
                    if (_latestPlayerPositions.ContainsKey(playerNameToDelete))
                    {
                        _latestPlayerPositions.Remove(playerNameToDelete);
                        _logger.Log($"Player {playerNameToDelete} removed from the game");
                    }
                    else
                    {
                        _logger.Log($"DELETE_PLAYER received for a non-existent player id: {playerNameToDelete}");
                    }
                    break;
            }
        }
        catch (FormatException fe)
        {
            _logger.Log($"Invalid message format from {sender.Address}:{sender.Port}. Message: {fe.Message}");
        }


        // Process the data, handle different messages, and update the game state
        // Broadcast the data to all connected peers
        foreach (var peer in _peers)
        {
            if (!peer.Equals(sender))
            {
                _udpServer.Send(data, data.Length, peer);
            }
        }
    }

    // New method to get the top 5 players by Y position
    public List<KeyValuePair<string, Vector3>> GetTop5PlayersByY()
    {
        List<KeyValuePair<string, Vector3>> top5Players = new List<KeyValuePair<string, Vector3>>(_latestPlayerPositions);
        top5Players.Sort((a, b) => b.Value.y.CompareTo(a.Value.y)); // sort descending by Y position
        if (top5Players.Count > 5)
        {
            top5Players.RemoveRange(5, top5Players.Count - 5); // keep only the top 5
        }
        return top5Players;
    }


    public void Stop()
    {
        _isRunning = false;
        _logger.Log("Server stopping.");
    }
}
