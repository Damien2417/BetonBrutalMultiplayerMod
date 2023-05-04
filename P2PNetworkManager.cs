using BepInEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
public class P2PNetworkManager : MonoBehaviour
{
    private Thread _serverThread;
    private Thread _clientThread;

    private UdpClient _udpClient;
    private Vector3 _playerPosition;
    private Quaternion _playerRotation;
    private readonly object _logFileLock = new object();

    private List<IPEndPoint> _peers;
    private Dictionary<int, GameObject> _players;
    private Dictionary<int, Vector3> _latestPlayerPositions;
    private Dictionary<int, Vector3> _latestPlayerPositionsClient;

    private ConcurrentQueue<Action> _mainThreadActions;
    
    private string bepinexModPath;
    private string _logFilePath;

    private AssetBundle assetBundle;

    void Start()
    {
        _peers = new List<IPEndPoint>();
        _players = new Dictionary<int, GameObject>();
        _udpClient = new UdpClient();
        _latestPlayerPositions = new Dictionary<int, Vector3>();
        _latestPlayerPositionsClient = new Dictionary<int, Vector3>();
        _mainThreadActions = new ConcurrentQueue<Action>();
        bepinexModPath = Paths.PluginPath;

        string json = "";
        
        if (File.Exists(Path.Combine(bepinexModPath, "MultiMod", "network_config.json")))
        {
            json = File.ReadAllText(Path.Combine(bepinexModPath, "MultiMod", "network_config.json"));
        }
        else
        {
            Debug.LogError("Network configuration file not found: " + Path.Combine(Application.dataPath, "network_config.json"));
            return;
        }

        NetworkConfig networkConfig = JsonUtility.FromJson<NetworkConfig>(json);

        string modelPath = Path.Combine(bepinexModPath, "MultiMod", "playerprefab");
        assetBundle = AssetBundle.LoadFromFile(modelPath);
        if (assetBundle == null)
        {
            Log("Failed to load AssetBundle!");
        }


        string ipAddress = networkConfig.ipAddress;
        int port = networkConfig.port;
        bool isHost = networkConfig.isHost;

        // Generate a unique log file name for this instance of the game
        string instanceId = System.Guid.NewGuid().ToString();
        _logFilePath = Path.Combine(bepinexModPath, "MultiMod", $"debug_{instanceId}.log");
        Debug.LogError(_logFilePath);

        if (isHost)
        {
            _serverThread = new Thread(() => StartServer(ipAddress, port));
            _serverThread.Start();
        }

        _clientThread = new Thread(() => StartClient(ipAddress, port));
        _clientThread.Start();
    }

    void OnDestroy()
    {
        _serverThread.Abort();
        _clientThread.Abort();
        _udpClient.Close();
    }
    void Update()
    {
        while (_mainThreadActions.TryDequeue(out Action action))
        {
            action.Invoke();
        }
    }

    void Log(string message)
    {
        lock (_logFileLock)
        {
            using (StreamWriter streamWriter = new StreamWriter(_logFilePath, true))
            {
                streamWriter.WriteLine(message);
            }
        }
    }

    // Server
    public void StartServer(string ipAddress, int port)
    {
        try
        {
            UdpClient server = new UdpClient(port);
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);

            Log($"[Server] Server started on  {clientEndpoint.Address} {clientEndpoint.Port}");

            while (true)
            {
                try
                {
                    byte[] data = server.Receive(ref clientEndpoint);
                    Log($"[Server] Received {data.Length} bytes from {clientEndpoint.Address}:{clientEndpoint.Port}");

                    ProcessReceivedDataServer(clientEndpoint, data);
                }
                catch (Exception e)
                {
                    Log(e.ToString());
                }
            }
        }
        catch (Exception e)
        {
            Log(e.ToString());
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
            Log($"[Server] Added {sender.Address}:{sender.Port} to the list of peers");

            // Send latest positions to the new player
            foreach (var player in _latestPlayerPositions)
            {
                if (player.Key != Int32.Parse(parts[1])) {
                    string positionString = $"ADD_PLAYER|{player.Key}|{player.Value.x}|{player.Value.y}|{player.Value.z}";
                    byte[] data2 = Encoding.UTF8.GetBytes(positionString);
                    _udpClient.Send(data2, data2.Length, sender);
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
                _udpClient.Send(data, data.Length, peer);

                Log($"[Server] Sent {data.Length} bytes to {peer.Address}:{peer.Port}");
            }
        }
    }


    // Client
    public void StartClient(string ipAddress, int port)
    {
        UdpClient client = new UdpClient();
        client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);

        IPAddress serverAddress = serverEndpoint.Address;
        int serverPort = serverEndpoint.Port;

        Log($"[Client] Client started, connecting to server at {serverEndpoint.Address}:{serverEndpoint.Port}");

        //Receive thread
        Thread receiveThread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    // Receive data from the server
                    byte[] receivedData = client.Receive(ref serverEndpoint);
                    ProcessReceivedClientData(serverEndpoint, receivedData);
                }
                catch (Exception e)
                {
                    Log(e.ToString());
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
                    string positionString = $"{method}|{playerId}|{_playerPosition.x}|{_playerPosition.y- 0.8f}|{_playerPosition.z}|{_playerRotation.x}|{_playerRotation.y}|{_playerRotation.z}|{_playerRotation.w}";
                    byte[] data = Encoding.UTF8.GetBytes(positionString);
                    client.Send(data, data.Length, new IPEndPoint(serverAddress, serverPort));


                    Log($"[Client] Sent {data.Length} bytes to {serverEndpoint.Address}:{serverEndpoint.Port}");

                    previousPosition = _playerPosition;
                    previousRotation = _playerRotation;
                }
                Thread.Sleep(20); // Adjust the sending interval as needed
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
        }
    }


    void ProcessReceivedClientData(IPEndPoint sender, byte[] data)
    {
        string message = Encoding.UTF8.GetString(data);
        string[] parts = message.Split('|');
        string logMessage = $"[Client] Received {parts[0]} for player {parts[1]} at ";

        for (int i = 2; i < parts.Length; i++)
        {
            logMessage += $"{parts[i]}";
            if (i < parts.Length - 1)
            {
                logMessage += ",";
            }
        }

        Log(logMessage);

        switch (parts[0])
        {
            case "POSITION_UPDATE":
                int playerId = int.Parse(parts[1]);
                float x = float.Parse(parts[2]);
                float y = float.Parse(parts[3]);
                float z = float.Parse(parts[4]);
                Vector3 position = new Vector3(x, y, z);
                Quaternion rotation = new Quaternion(float.Parse(parts[5]), float.Parse(parts[6]), float.Parse(parts[7]), float.Parse(parts[8]));

                UpdatePlayerPosition(playerId, position, rotation);
                break;
            case "ADD_PLAYER":
                int newPlayerId = int.Parse(parts[1]);
                float newX = float.Parse(parts[2]);
                float newY = float.Parse(parts[3]);
                float newZ = float.Parse(parts[4]);
                Vector3 newPosition = new Vector3(newX, newY, newZ);

                AddPlayer(newPlayerId, newPosition);
                break;
                // Handle other messages
        }
    }

    void AddPlayer(int playerId, Vector3 position)
    {
        Action action = () =>
        {
            if (!_players.ContainsKey(playerId))
            {
                GameObject model = assetBundle.LoadAsset<GameObject>("playerprefab");
                if (model == null)
                {
                    Log("Failed to load custom model!");
                }

                GameObject mymodel = Instantiate(model, position, Quaternion.identity) as GameObject;

                Renderer renderer = mymodel.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material material = renderer.material;
                    if (material != null)
                    {
                        material.color = Color.red;
                    }
                }
                // Access the Animator component
                Animator animator = mymodel.GetComponent<Animator>();
                AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);

                if (animator != null && !animator.enabled)
                {
                    animator.enabled = true;
                }
                if (animator != null)
                {
                    animator.Play("BasicMotions@Idle01");
                }


                // Disable the cube collisions
                /*var modelCollider = mymodel.GetComponent<Collider>();
                if (modelCollider != null)
                {
                    modelCollider.enabled = false;
                }*/

                _players.Add(playerId, mymodel);
                Log($"[Client] Add player at position {position}");

                // Spawn a clone of the FlashlightAnchor and anchor it to the top of the cube
                GameObject flashlightAnchorPrefab = GameObject.Find("Flashlight");
                GameObject newFlashlightAnchor = Instantiate(flashlightAnchorPrefab, mymodel.transform.position + Vector3.up, Quaternion.identity);

                newFlashlightAnchor.transform.parent = mymodel.transform;
                newFlashlightAnchor.transform.localPosition = new Vector3(0, 0.5f, 0);
            }
        };
        _mainThreadActions.Enqueue(action);
    }

    void UpdatePlayerPosition(int playerId, Vector3 position, Quaternion rotation)
    {
        Action action = () =>
        {
            if (_players.ContainsKey(playerId))
            {
                if (!_latestPlayerPositionsClient.ContainsKey(playerId))
                {
                    _latestPlayerPositionsClient.Add(playerId, _players[playerId].transform.position);
                }
                else
                {
                    _latestPlayerPositionsClient[playerId] = _players[playerId].transform.position;
                }
                    
                _players[playerId].transform.position = position;

                // Lock the prefab rotation on the up and down axis, and keep the rotation on the left and right axis.
                float yRotation = rotation.eulerAngles.y;
                _players[playerId].transform.rotation = Quaternion.Euler(0, yRotation, 0);

                // Find the head bone (B-head) and adjust its rotation.
                Transform headBone = _players[playerId].transform.Find("B-head");
                if (headBone != null)
                {
                    float xRotation = rotation.eulerAngles.x;
                    float zRotation = rotation.eulerAngles.z;

                    // Apply a slight rotation to the head bone.
                    float headRotationFactor = 0.2f;
                    Log(Quaternion.Euler(xRotation * headRotationFactor, 0, zRotation * headRotationFactor).ToString());
                    headBone.localRotation = Quaternion.Euler(xRotation * headRotationFactor, 0, zRotation * headRotationFactor);
                }
                UpdatePlayerAnimation(playerId);
            }
        };
        _mainThreadActions.Enqueue(action);
    }

    public void SetPlayerPosition(Vector3 position, Quaternion playerRotation)
    {
        _playerPosition = position;
        _playerRotation = playerRotation;
    }
    void UpdatePlayerAnimation(int playerId)
    {
        if (_players.ContainsKey(playerId))
        {
            Vector3 currentPosition = _players[playerId].transform.position;
            Vector3 previousPosition;

            if (_latestPlayerPositionsClient.ContainsKey(playerId))
            {
                previousPosition = _latestPlayerPositionsClient[playerId];
            }
            else
            {
                previousPosition = currentPosition;
            }
            float distanceMoved = Vector3.Distance(currentPosition, previousPosition);

            float movementThreshold = 0.01f;
            float movementUpThreshold = 0.01f;

            RaycastHit hit;
            bool isGrounded = Physics.Raycast(currentPosition, Vector3.down, out hit, 0.1f);

            if(!isGrounded && currentPosition.y > previousPosition.y)
            {
                PlayAnimation(playerId, "Jump", true);
            }
            else if (!isGrounded && currentPosition.y < previousPosition.y)
            {
                PlayAnimation(playerId, "Fall", true);
            }
            else if (isGrounded && distanceMoved > movementThreshold)
            {
                PlayAnimation(playerId, "Run", true);
            }
            else
            {
                PlayAnimation(playerId, "Idle", true);
            }
        }
    }


    private void PlayAnimation(int playerId, string animationParameter, bool value)
    {
        if (_players.ContainsKey(playerId))
        {
            Animator animator = _players[playerId].GetComponent<Animator>();

            // Check if the requested animation is already playing
            if (animator.GetBool(animationParameter) == value)
            {
                return; // The animation is already playing, do nothing
            }


            // Reset all the animation parameters to false
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(param.name, false);
                }
            }

            // Set the desired animation parameter to the given value
            animator.SetBool(animationParameter, value);
        }
    }
}
