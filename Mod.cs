using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

[BepInPlugin("dam.betonbrutal.multimod", "Multiplayer Mod", "1.0.0")]
public class Mod : BaseUnityPlugin
{
    private Harmony _harmony;
    public MultiplayerManager p2pNetworkManager;

    private void Awake()
    {
        _harmony = new Harmony("dam.betonbrutal.multimod");
        _harmony.PatchAll(Assembly.GetExecutingAssembly());
        
        // Create a new GameObject and add the MultiplayerManager component
        //GameObject multiplayerManagerObj = new GameObject("MultiplayerManager");
        //p2pNetworkManager = multiplayerManagerObj.AddComponent<MultiplayerManager>();

    }
}

