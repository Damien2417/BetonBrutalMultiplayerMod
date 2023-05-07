using BepInEx;
using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance { get; private set; }
    public Client client { get; private set; }
    private Server server;

    private Thread _serverThread;
    private Thread _clientThread;

    string instanceId = Guid.NewGuid().ToString();

    private AssetBundle assetBundle;
    private string bepinexModPath;
    private ClientThreadActionsManager mainThreadActionsManager;
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



    void Start()
    {
        Debug.Log("MultiplayerManager Start");
        bepinexModPath = Path.Combine(Paths.PluginPath, "MultiMod");

        NetworkConfig networkConfig = NetworkConfig.Load(Path.Combine(bepinexModPath, "network_config.json"));
        assetBundle = LoadAssetBundle(Path.Combine(bepinexModPath, "playerprefab"));

        mainThreadActionsManager = new ClientThreadActionsManager();

        if (networkConfig.isHost)
        {
            server = new Server(networkConfig.serverIpAddress, networkConfig.serverPort, Path.Combine(bepinexModPath, $"server_debug_{instanceId}.log"));
            _serverThread = new Thread(() => server.Start());
            _serverThread.Start();
        }

        client = new Client(networkConfig.serverIpAddress, networkConfig.serverPort, Path.Combine(bepinexModPath, $"client_debug_{instanceId}.log"), assetBundle, mainThreadActionsManager);
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
}
