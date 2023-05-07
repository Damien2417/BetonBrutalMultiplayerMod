using HarmonyLib;
using UnityEngine;
public static class Patcher
{
    [HarmonyPatch(typeof(PlayerController))]
    [HarmonyPatch("Update")]
    public static class PlayerController_Update_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerController __instance)
        {
            Client client = MultiplayerManager.Instance?.client;
            if (client != null)
            {
                Vector3 playerPosition = __instance.transform.position;
                Quaternion playerRotation = __instance.cam.transform.rotation;

                client.SetPlayerPosition(playerPosition, playerRotation);

                client.SetPlayerSprinting(__instance.isSprinting);
                client.SetPlayerSneaking(__instance.isSneaking);
            }
        }
    }

    [HarmonyPatch(typeof(GameUI))]
    [HarmonyPatch("SwitchToScreen")]
    public static class GameUI_SwitchToScreen_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameUI __instance, string screenName)
        {
            if (screenName == "HUD")
            {
                if (GameObject.FindObjectOfType<MultiplayerManager>() == null)
                {
                    GameObject multiplayerManager = new GameObject("MultiplayerManager");
                    multiplayerManager.AddComponent<MultiplayerManager>();
                }
            }
        }
    }
}
