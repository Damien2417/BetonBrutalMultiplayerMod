using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(PlayerController))]
[HarmonyPatch("Update")]
public static class PlayerControllerPatch
{
    public static void Postfix(PlayerController __instance)
    {
        P2PNetworkManager p2pNetworkManager = GameObject.FindObjectOfType<P2PNetworkManager>();
        if (p2pNetworkManager != null)
        {
            Vector3 playerPosition = __instance.transform.position;
            Quaternion playerRotation = __instance.cam.transform.rotation;

            p2pNetworkManager.SetPlayerPosition(playerPosition, playerRotation);
        }
    }
}
