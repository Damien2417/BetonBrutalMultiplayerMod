using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

public class Client
{
    private readonly Logger _logger;
    private readonly UdpClient _udpClient;
    private readonly string _ipAddress;
    private readonly int _port;
    private Vector3 _playerPosition;
    private Quaternion _playerRotation;
    private readonly Players _players;
    private bool _isSprinting = false;
    private bool _isSneaking = false;
    public Client(string ipAddress, int port, string logFilePath, AssetBundle assetBundle, ClientThreadActionsManager mainThreadActionsManager)
    {
        _udpClient = new UdpClient();

        _logger = new Logger("[Client]", logFilePath);

        _ipAddress = ipAddress;
        _port = port;

        _players = new Players(_logger, mainThreadActionsManager, assetBundle.LoadAsset<GameObject>("playerprefab"));

    }
    public void Start()
    {
        IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse(_ipAddress), _port);
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

        IPAddress serverAddress = serverEndpoint.Address;
        int serverPort = serverEndpoint.Port;

        _logger.Log($"Client started, connecting to server at {serverEndpoint.Address}:{serverEndpoint.Port}");

        //Receive thread
        Thread receiveThread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    // Receive data from the server
                    byte[] receivedData = _udpClient.Receive(ref serverEndpoint);
                    ProcessReceivedClientData(serverEndpoint, receivedData);
                }
                catch (SocketException e)
                {
                    _logger.Log($"Receive thread SocketException: {e.Message}");
                }
                catch (Exception e)
                {
                    _logger.Log($"Receive thread Exception: {e.ToString()}");
                }
            }
        });
        receiveThread.Start();

        int playerId = UnityEngine.Random.Range(0, 1000000);
        Vector3 previousPosition = _playerPosition;
        Quaternion previousRotation = _playerRotation;
        bool check = false;

        //Send thread
        while (true)
        {
            try
            {
                if (_playerPosition != previousPosition || _playerRotation != previousRotation)
                {
                    string method = "POSITION_UPDATE";
                    if (!check)
                    {
                        method = "ADD_PLAYER";
                        check = true;
                    }

                    // Send player position and other updates to the server
                    string positionString = $"{method}|{playerId}|{_playerPosition.x}|{_playerPosition.y - 0.8f}|{_playerPosition.z}|{_playerRotation.x}|{_playerRotation.y}|{_playerRotation.z}|{_playerRotation.w}|{_isSprinting}|{_isSneaking}";
                    byte[] data = Encoding.UTF8.GetBytes(positionString);
                    _udpClient.Send(data, data.Length, new IPEndPoint(serverAddress, serverPort));


                    _logger.Log($"Sent {data.Length} bytes to {serverEndpoint.Address}:{serverEndpoint.Port}");

                    previousPosition = _playerPosition;
                    previousRotation = _playerRotation;
                }
                Thread.Sleep(20); // Adjust the sending interval as needed
            }
            catch (SocketException e)
            {
                _logger.Log($"Send thread SocketException: {e.Message}");
            }
            catch (Exception e)
            {
                _logger.Log($"Send thread Exception: {e.ToString()}");
            }
        }
    }

    void ProcessReceivedClientData(IPEndPoint sender, byte[] data)
    {
        string message = Encoding.UTF8.GetString(data);
        string[] parts = message.Split('|');
        string logMessage = $"Received {parts[0]} for player {parts[1]} at ";

        for (int i = 2; i < parts.Length; i++)
        {
            logMessage += $"{parts[i]}";
            if (i < parts.Length - 1)
            {
                logMessage += ",";
            }
        }

        _logger.Log(logMessage);

        switch (parts[0])
        {
            case "POSITION_UPDATE":
                int playerId = int.Parse(parts[1]);
                float x = float.Parse(parts[2]);
                float y = float.Parse(parts[3]);
                float z = float.Parse(parts[4]);
                Vector3 position = new Vector3(x, y, z);
                Quaternion rotation = new Quaternion(float.Parse(parts[5]), float.Parse(parts[6]), float.Parse(parts[7]), float.Parse(parts[8]));

                bool isSprinting = Convert.ToBoolean(parts[10]);
                bool isSneaking = Convert.ToBoolean(parts[9]);

                _players.UpdatePlayerPosition(playerId, position, rotation, isSprinting, isSneaking);
                break;
            case "ADD_PLAYER":
                int newPlayerId = int.Parse(parts[1]);
                float newX = float.Parse(parts[2]);
                float newY = float.Parse(parts[3]);
                float newZ = float.Parse(parts[4]);
                Vector3 newPosition = new Vector3(newX, newY, newZ);

                _players.AddPlayer(newPlayerId, newPosition);
                break;
                // Handle other messages
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
}