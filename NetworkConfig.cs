using System.IO;
using UnityEngine;

public class NetworkConfig
{
    public string serverIpAddress;
    public int serverPort;
    public bool isHost;
    public string clientipAddress;
    public int clientport;

    public static NetworkConfig Load(string bepinexModPath)
    {
        string json = "";
        string configPath = bepinexModPath;

        if (File.Exists(configPath))
        {
            json = File.ReadAllText(configPath);
        }
        else
        {
            Debug.LogError("Network configuration file not found: " + configPath);
            return null;
        }

        return JsonUtility.FromJson<NetworkConfig>(json);
    }
}