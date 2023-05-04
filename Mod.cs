using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

[BepInPlugin("dam.betonbrutal.multimod", "Multiplayer Mod", "1.0.0")]
public class Mod : BaseUnityPlugin
{
    private Harmony _harmony;
    private ManualLogSource _logger;
    public P2PNetworkManager p2pNetworkManager;

    private void Awake()
    {
        _logger = Logger;
        _harmony = new Harmony("dam.betonbrutal.multimod");
        _harmony.PatchAll(Assembly.GetExecutingAssembly());
        p2pNetworkManager = new P2PNetworkManager();
    }
}

[HarmonyPatch(typeof(GameUI))]
[HarmonyPatch("SwitchToScreen")]
public static class MultiPatch
{
    public static void Postfix(GameUI __instance, string screenName)
    {
        if (screenName == "HUD")
        {
            if (GameObject.FindObjectOfType<P2PNetworkManager>() == null)
            {
                GameObject p2pNetworkManagerObject = new GameObject("P2PNetworkManager");
                p2pNetworkManagerObject.AddComponent<P2PNetworkManager>();
            }
        }
    }
}
