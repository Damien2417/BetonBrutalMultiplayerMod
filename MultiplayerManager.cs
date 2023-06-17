using BepInEx;
using System;
using System.IO;
using System.Threading;
using System.Xml.Linq;
using UnityEngine;

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance { get; private set; }
    public Client client { get; private set; }
    private Server server;
    private string _ipAddress;
    private int _port;
    private string _name;
    private bool _isHost;

    private Thread _serverThread;
    private Thread _clientThread;

    string instanceId = Guid.NewGuid().ToString();

    private AssetBundle assetBundle;
    private string bepinexModPath;
    private ClientThreadActionsManager mainThreadActionsManager;
    
    public void Initialize(string ipAddress, int port, bool isHost, string name)
    {
        _ipAddress = ipAddress;
        _isHost = isHost;
        _port = port;
        _name = name;
        go();
    }

    private void Awake()
    {
        Debug.Log("MultiplayerManager Awake");
        if (Instance != null)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    void go()
    {
        Debug.Log("MultiplayerManager Start");
        bepinexModPath = Path.Combine(Paths.PluginPath, "MultiMod");

        assetBundle = LoadAssetBundle(Path.Combine(bepinexModPath, "playerprefab"));

        mainThreadActionsManager = new ClientThreadActionsManager();

        if (_isHost)
        {
            server = new Server(_ipAddress, _port, Path.Combine(bepinexModPath, $"server_debug_{instanceId}.log"));
            _serverThread = new Thread(() => server.Start());
            _serverThread.Start();
        }

        client = new Client(_ipAddress, _port, _name, Path.Combine(bepinexModPath, $"client_debug_{instanceId}.log"), assetBundle, mainThreadActionsManager);
        _clientThread = new Thread(() => client.Start());
        _clientThread.Start();
    }

    void OnDestroy()
    {
        if (_serverThread != null)
        {
            _serverThread.Abort();
        }

        if (_clientThread != null)
        {
            _clientThread.Abort();
        }
    }


    void Update()
    {
        mainThreadActionsManager.ProcessActions();
    }

    AssetBundle LoadAssetBundle(string modelPath)
    {
        AssetBundle bundle = AssetBundle.LoadFromFile(modelPath);
        if (bundle == null)
        {
            Debug.LogError("Failed to load AssetBundle!");
        }
        return bundle;
    }

    public void Disconnect()
    {
        if (client != null)
        {
            client.Disconnect();
            client = null;
        }

        if (server != null)
        {
            server.Stop();
            server = null;
        }

        if (_serverThread != null && _serverThread.IsAlive)
        {
            _serverThread.Interrupt();
            _serverThread = null;
        }

        if (_clientThread != null && _clientThread.IsAlive)
        {
            _clientThread.Interrupt();
            _clientThread = null;
        }

        // Unload the asset bundle, including all its assets
        if (assetBundle != null)
        {
            assetBundle.Unload(true);
            assetBundle = null;
        }

        Debug.Log("Disconnected from the server.");
    }

}
