using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Client
{
    private readonly Logger _logger;
    private readonly UdpClient _udpClient;
    private readonly string _ipAddress;
    private readonly int _port;
    private readonly string _playerName;
    private Vector3 _playerPosition;
    private Quaternion _playerRotation;
    private readonly Players _players;
    private bool _isSprinting = false;
    private bool _isSneaking = false;
    private Thread receiveThread;
    private bool _isRunning = true;
    public Client(string ipAddress, int port, string name, string logFilePath, AssetBundle assetBundle, ClientThreadActionsManager mainThreadActionsManager)
    {
        _udpClient = new UdpClient();

        _logger = new Logger("[Client]", logFilePath);

        _ipAddress = ipAddress;
        _port = port;
        _playerName =  name;

        _players = new Players(_logger, mainThreadActionsManager, assetBundle.LoadAsset<GameObject>("playerprefab"));
    }
    public void Start()
    {
        _isRunning = true;

        IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse(_ipAddress), _port);
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

        IPAddress serverAddress = serverEndpoint.Address;
        int serverPort = serverEndpoint.Port;
        _logger.Log($"Client started, connecting to server at {serverEndpoint.Address}:{serverEndpoint.Port}");

        //Receive thread
        receiveThread = new Thread(() =>
        {
            while (_isRunning)
            {
                try
                {
                    // Receive data from the server
                    byte[] receivedData = _udpClient.Receive(ref serverEndpoint);
                    ProcessReceivedClientData(serverEndpoint, receivedData);
                }
                catch (SocketException e)
                {
                    _logger.Log($"Receiver thread SocketException: {e.Message}");
                }
                catch (Exception e)
                {
                    _logger.Log($"Receiver thread Exception: {e.ToString()}");
                }
            }
        });
        receiveThread.Start();

        Vector3 previousPosition = _playerPosition;
        Quaternion previousRotation = _playerRotation;
        bool check = false;

        // Send thread
        Thread sendThread = new Thread(() =>
        {
            while (_isRunning)
            {
                try
                {
                    if (_playerPosition != previousPosition || _playerRotation != previousRotation)
                    {
                        string positionString = $"POSITION_UPDATE|{_playerName}|{_playerPosition.x}|{_playerPosition.y - 0.8f}|{_playerPosition.z}|{_playerRotation.x}|{_playerRotation.y}|{_playerRotation.z}|{_playerRotation.w}|{_isSprinting}|{_isSneaking}";

                        if (!check)
                        {
                            positionString = $"ADD_PLAYER|{_playerName}|{_playerPosition.x}|{_playerPosition.y - 0.8f}|{_playerPosition.z}";
                            check = true;
                        }

                        byte[] data = Encoding.UTF8.GetBytes(positionString);
                        _udpClient.Send(data, data.Length, new IPEndPoint(serverAddress, serverPort));

                        previousPosition = _playerPosition;
                        previousRotation = _playerRotation;
                    }
                    Thread.Sleep(20);
                }
                catch (SocketException e)
                {
                    _logger.Log($"Sender thread SocketException: {e.Message}");
                }
                catch (Exception e)
                {
                    _logger.Log($"Sender thread Exception: {e.ToString()}");
                }
            }
        });
        sendThread.Start();

        // Wait for the threads to finish before closing resources
        receiveThread.Join();
        sendThread.Join();
        _udpClient?.Close();
        _logger.Log("Client stopped.");
    }

    void ProcessReceivedClientData(IPEndPoint sender, byte[] data)
    {
        string message = Encoding.UTF8.GetString(data);
        string[] parts = message.Split('|');

        if (parts.Length < 2) // We expect at least a method and playerId
        {
            _logger.Log($"Invalid message received from server: {message}");
            return;
        }

        try
        {
            switch (parts[0])
            {
                case "POSITION_UPDATE":
                    if (parts.Length != 11) // We expect exactly 11 parts for a "POSITION_UPDATE" message
                    {
                        _logger.Log($"Invalid POSITION_UPDATE message received from server: {message}");
                        return;
                    }

                    string playerName = parts[1];
                    float x = float.Parse(parts[2]);
                    float y = float.Parse(parts[3]);
                    float z = float.Parse(parts[4]);
                    Vector3 position = new Vector3(x, y, z);
                    Quaternion rotation = new Quaternion(float.Parse(parts[5]), float.Parse(parts[6]), float.Parse(parts[7]), float.Parse(parts[8]));

                    bool isSprinting = Convert.ToBoolean(parts[9]);
                    bool isSneaking = Convert.ToBoolean(parts[10]);

                    _players.UpdatePlayerPosition(playerName, position, rotation, isSprinting, isSneaking);
                    break;

                case "ADD_PLAYER":
                    if (parts.Length != 5) // We expect exactly 5 parts for a "ADD_PLAYER" message
                    {
                        _logger.Log($"Invalid ADD_PLAYER message received from server: {message}");
                        return;
                    }
                    _logger.Log($"Adding player");

                    string newPlayerName = parts[1];
                    float newX = float.Parse(parts[2]);
                    float newY = float.Parse(parts[3]);
                    float newZ = float.Parse(parts[4]);
                    Vector3 newPosition = new Vector3(newX, newY, newZ);

                    _players.AddPlayer(newPlayerName, newPosition);
                    break;
                case "DELETE_PLAYER":
                    if (parts.Length != 2) // We expect exactly 2 parts for a "DELETE_PLAYER" message
                    {
                        _logger.Log($"Invalid DELETE_PLAYER message received from server: {message}");
                        return;
                    }

                    string deletePlayerName = parts[1];
                    _players.DeletePlayer(deletePlayerName);
                    break;
            }
        }
        catch (FormatException fe)
        {
            _logger.Log($"Invalid message format from server: {fe.Message}");
        }
    }

    public void SetPlayerPosition(Vector3 position, Quaternion playerRotation)
    {
        _playerPosition = position;
        _playerRotation = playerRotation;
    }

    public void SetPlayerSprinting(bool isSprinting)
    {
        _isSprinting= isSprinting;
    }

    public void SetPlayerSneaking(bool isSneaking)
    {
        _isSneaking = isSneaking;
    }

    public void Disconnect()
    {
        string disconnectMessage = $"DELETE_PLAYER|{_playerName}";
        byte[] data = Encoding.UTF8.GetBytes(disconnectMessage);
        _udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(_ipAddress), _port));
        _logger.Log("Delete player message sent to the server.");

        _isRunning = false;
        _logger.Log("Client disconnecting...");
        // Find the object by its script type
        Canvas canvas = GameObject.FindObjectOfType<Canvas>();

        // Check if the object was found
        if (canvas != null)
        {
            Transform leaderboard = canvas.transform.Find("Leaderboard");

            // Check if the Leaderboard child object was found
            if (leaderboard != null)
            {
                // Destroy the Leaderboard object
                GameObject.Destroy(leaderboard.gameObject);
            }
        }


    }

    public List<KeyValuePair<string, Vector3>>  getTop5Players()
    {
        List<KeyValuePair<string, Vector3>> players = _players.GetPlayers();
        players.Add(new KeyValuePair<string, Vector3>(_playerName, _playerPosition));
        players.Sort((a, b) => b.Value.y.CompareTo(a.Value.y)); // sort descending by Y position
        if (players.Count > 5)
        {
            players.RemoveRange(5, players.Count - 5); // keep only the top 5
        }
        return players;
    }
}