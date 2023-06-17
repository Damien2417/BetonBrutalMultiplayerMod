using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public static class Patcher
{
    private static GameObject leaderboardGO;
    private static Text[] playerTexts;
    public class PlayerData
    {
        public string playerName;
        public float altitude;
    }

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

                UpdateLeaderboard();
            }
        }
    }

    private static void CreateLeaderboardUI()
    {
        GameObject canvasGO = GameObject.Find("Canvas"); // Assuming the canvas is already present
        if (canvasGO == null)
        {
            Debug.LogError("Canvas GameObject not found!");
            return;
        }

        leaderboardGO = new GameObject("Leaderboard");
        leaderboardGO.transform.parent = canvasGO.transform;
        RectTransform leaderboardRect = leaderboardGO.AddComponent<RectTransform>();
        leaderboardRect.anchorMin = new Vector2(1, 1);
        leaderboardRect.anchorMax = new Vector2(1, 1);
        leaderboardRect.pivot = new Vector2(1, 1);
        leaderboardRect.anchoredPosition = new Vector2(-20, -200);
        leaderboardRect.sizeDelta = new Vector2(200, 200);

        playerTexts = new Text[5];

        for (int i = 0; i < 5; i++)
        {
            GameObject playerTextGO = new GameObject("Player" + (i + 1));
            playerTextGO.transform.parent = leaderboardGO.transform;
            RectTransform playerTextRect = playerTextGO.AddComponent<RectTransform>();
            playerTextRect.anchorMin = new Vector2(0, 1);
            playerTextRect.anchorMax = new Vector2(1, 1);
            playerTextRect.pivot = new Vector2(0.5f, 1);
            playerTextRect.anchoredPosition = new Vector2(0, -40 * i);
            playerTextRect.sizeDelta = new Vector2(0, 40);

            Text playerTextComponent = playerTextGO.AddComponent<Text>();
            playerTextComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            playerTextComponent.color = Color.white;
            playerTextComponent.alignment = TextAnchor.MiddleRight;

            // Add index to player text
            GameObject indexGO = new GameObject("Index");
            indexGO.transform.parent = playerTextGO.transform;
            RectTransform indexRect = indexGO.AddComponent<RectTransform>();
            indexRect.anchorMin = new Vector2(0, 0);
            indexRect.anchorMax = new Vector2(0, 1);
            indexRect.pivot = new Vector2(0, 0.5f);
            indexRect.anchoredPosition = new Vector2(5, 0);
            indexRect.sizeDelta = new Vector2(20, 0);

            Text indexText = indexGO.AddComponent<Text>();
            indexText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            indexText.color = Color.white;
            indexText.alignment = TextAnchor.MiddleLeft;

            playerTexts[i] = playerTextComponent;
        }

    }

    private static List<PlayerData> playerDataList = new List<PlayerData>();

    private static void UpdateLeaderboard()
    {
        if (leaderboardGO == null || playerTexts == null)
            return;

        Client client = MultiplayerManager.Instance?.client;
        if (client == null)
            return;

        playerDataList.Clear();

        List<KeyValuePair<string, Vector3>> topPlayers = client.getTop5Players();

        for (int i = 0; i < topPlayers.Count; i++)
        {
            string playerName = topPlayers[i].Key;
            float altitude = Mathf.Round(topPlayers[i].Value.y);

            playerDataList.Add(new PlayerData { playerName = playerName, altitude = altitude });
        }

        playerDataList.Sort((a, b) => b.altitude.CompareTo(a.altitude)); // Sort the players based on altitude

        for (int i = 0; i < playerTexts.Length; i++)
        {
            Text playerText = playerTexts[i];
            if (i < playerDataList.Count)
            {
                PlayerData playerData = playerDataList[i];
                playerText.text = playerData.playerName + ": " + playerData.altitude + "m";
            }
            else
            {
                playerText.text = "";
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
            if (screenName == "PauseMenu")
            {
                if (GameUI_Start_Patch.connectionMenu != null)
                {
                    GameUI_Start_Patch.ManageConnectionUI();
                }
            }
            else // When we switch away from PauseMenu, we disable our menu
            {
                if (GameUI_Start_Patch.connectionMenu != null)
                    GameUI_Start_Patch.connectionMenu.SetActive(false);
                if (GameUI_Start_Patch.disconnectionMenu != null)
                    GameUI_Start_Patch.disconnectionMenu.SetActive(false);
            }

            if (screenName == "HUD")
            {
                if (leaderboardGO != null)
                {
                    leaderboardGO.SetActive(true);
                }
                else
                {
                    CreateLeaderboardUI();
                    UpdateLeaderboard();
                }
            }
            else
            {
                if (leaderboardGO != null)
                {
                    leaderboardGO.SetActive(false);
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameUI))]
    [HarmonyPatch("Start")] // Patch the Start method of GameUI
    public static class GameUI_Start_Patch
    {
        public static GameObject canvasGO;
        public static GameObject connectionMenu;
        public static GameObject disconnectionMenu;
        public static Button disconnectButton;
        public static GameObject leaderboardGO;

        [HarmonyPostfix]
        public static void Postfix(GameUI __instance)
        {
            // Create a new Canvas
            canvasGO = new GameObject("Canvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            canvas.sortingOrder = 1000;

            // Create a new Panel for Connection Menu
            connectionMenu = new GameObject("ConnectionMenu");
            connectionMenu.transform.parent = canvasGO.transform;
            RectTransform connectionMenuRect = connectionMenu.AddComponent<RectTransform>();
            connectionMenuRect.anchorMin = new Vector2(1, 1);
            connectionMenuRect.anchorMax = new Vector2(1, 1);
            connectionMenuRect.pivot = new Vector2(1, 1);
            connectionMenuRect.anchoredPosition = Vector2.zero;
            connectionMenuRect.sizeDelta = new Vector2(200, 200);

            // Create an Input Field under the Panel for IP address input
            InputField ipInputField = AddInputFieldToUI(connectionMenu, "IPInputField", new Vector2(-10, -10), "Enter IP Address...");

            // Create an Input Field under the Panel for Port input
            InputField portInputField = AddInputFieldToUI(connectionMenu, "PortInputField", new Vector2(-10, -50), "Enter Port...");

            // Create an Input Field under the Panel for Port input
            InputField nameInputField = AddInputFieldToUI(connectionMenu, "NameInputField", new Vector2(-10, -90), "Enter name...");

            // Add Connect Button
            Button connectButton = AddButtonToUI(connectionMenu, "ConnectButton", new Vector2(-10, -170), "Connect");

            // Add IsHost CheckBox
            Toggle isHostToggle = AddToggleToUI(connectionMenu, "IsHostToggle", new Vector2(-10, -130), "Host");
            RectTransform toggleRect = isHostToggle.GetComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(70, 30);
            toggleRect.anchorMin = new Vector2(0.5f, 1);
            toggleRect.anchorMax = new Vector2(0.5f, 1);
            toggleRect.pivot = new Vector2(0.5f, 1);
            ColorBlock colors = isHostToggle.colors;
            colors.normalColor = Color.red;
            colors.highlightedColor = Color.red;
            colors.pressedColor = Color.red;
            colors.selectedColor = Color.red;
            isHostToggle.colors = colors;

            connectButton.onClick.AddListener(() =>
            {
                Debug.Log("Connect Button clicked!");
                Debug.Log("Entered IP: " + ipInputField.text);
                Debug.Log("Entered Port: " + portInputField.text);
                Debug.Log("Entered Name: " + nameInputField.text);
                Debug.Log("Is host: " + isHostToggle.isOn);

                if (GameObject.FindObjectOfType<MultiplayerManager>() == null)
                {
                    GameObject multiplayerManager = new GameObject("MultiplayerManager");
                    MultiplayerManager managerComponent = multiplayerManager.AddComponent<MultiplayerManager>();
                    managerComponent.Initialize(ipInputField.text, int.Parse(portInputField.text), isHostToggle.isOn, nameInputField.text);
                    ManageConnectionUI();
                    Canvas.ForceUpdateCanvases();
                }
            });

            // Create a new Panel for Disconnection
            disconnectionMenu = new GameObject("DisconnectionMenu");
            disconnectionMenu.transform.parent = canvasGO.transform;
            RectTransform disconnectionMenuRect = disconnectionMenu.AddComponent<RectTransform>();
            disconnectionMenuRect.anchorMin = new Vector2(1, 1);
            disconnectionMenuRect.anchorMax = new Vector2(1, 1);
            disconnectionMenuRect.pivot = new Vector2(1, 1);
            disconnectionMenuRect.anchoredPosition = Vector2.zero;
            disconnectionMenuRect.sizeDelta = new Vector2(200, 200);

            // Add Disconnect Button
            disconnectButton = AddButtonToUI(disconnectionMenu, "DisconnectButton", new Vector2(-10, -10), "Disconnect");

            disconnectButton.onClick.AddListener(() =>
            {
                Debug.Log("Disconnect Button clicked!");

                MultiplayerManager manager = GameObject.FindObjectOfType<MultiplayerManager>();
                if (manager != null)
                {
                    manager.Disconnect();
                    GameObject.Destroy(manager.gameObject);

                    ManageConnectionUI();
                    Canvas.ForceUpdateCanvases();
                }
            });

        }

        public static void ManageConnectionUI()
        {
            bool isConnected = GameObject.FindObjectOfType<MultiplayerManager>() != null;
            connectionMenu.SetActive(!isConnected);
            disconnectionMenu.SetActive(isConnected);
        }

        public static InputField AddInputFieldToUI(GameObject parentObject, string inputFieldName, Vector2 anchoredPosition, string placeholderText)
        {
            GameObject inputFieldGO = new GameObject(inputFieldName);
            inputFieldGO.transform.parent = parentObject.transform;
            InputField inputField = inputFieldGO.AddComponent<InputField>();
            RectTransform inputFieldRect = inputFieldGO.AddComponent<RectTransform>();
            inputFieldRect.anchorMin = new Vector2(1, 1);
            inputFieldRect.anchorMax = new Vector2(1, 1);
            inputFieldRect.pivot = new Vector2(1, 1);
            inputFieldRect.anchoredPosition = anchoredPosition;
            inputFieldRect.sizeDelta = new Vector2(180, 30);
            Image inputFieldImage = inputFieldGO.AddComponent<Image>();
            inputFieldImage.color = new Color(1, 1, 1, 0.2f); // Make the input field's background white

            // Assign the Text component to the InputField
            inputField.textComponent = AddTextToUI(inputFieldGO, "");

            // Assign the Placeholder Text component to the InputField
            inputField.placeholder = AddTextToUI(inputFieldGO, placeholderText);

            return inputField;
        }

        public static Text AddTextToUI(GameObject parentObject, string text)
        {
            GameObject textGO = new GameObject("Text");
            textGO.transform.parent = parentObject.transform;
            RectTransform parentRect = parentObject.GetComponent<RectTransform>();
            RectTransform textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.anchoredPosition = new Vector2(parentRect.sizeDelta.x / 2, parentRect.sizeDelta.y / 2 - 15);
            textRect.sizeDelta = new Vector2(parentRect.sizeDelta.x - 10, parentRect.sizeDelta.y - 10);

            Text textComponent = textGO.AddComponent<Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.color = Color.white;
            textComponent.alignment = TextAnchor.MiddleLeft;

            return textComponent;
        }

        public static Button AddButtonToUI(GameObject parentObject, string buttonName, Vector2 anchoredPosition, string buttonText)
        {
            GameObject buttonGO = new GameObject(buttonName);
            buttonGO.transform.parent = parentObject.transform;

            Button button = buttonGO.AddComponent<Button>();
            RectTransform buttonRect = buttonGO.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1, 1);
            buttonRect.anchorMax = new Vector2(1, 1);
            buttonRect.pivot = new Vector2(1, 1);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = new Vector2(180, 30);
            Image buttonImage = buttonGO.AddComponent<Image>();
            buttonImage.color = new Color(1, 1, 1, 0.2f); // Make the button's background white

            Text buttonTextComponent = AddTextToUI(buttonGO, buttonText);
            return button;
        }

        public static Toggle AddToggleToUI(GameObject parentObject, string toggleName, Vector2 anchoredPosition, string toggleText)
        {
            GameObject toggleGO = new GameObject(toggleName);
            toggleGO.transform.parent = parentObject.transform;

            Toggle toggle = toggleGO.AddComponent<Toggle>();
            RectTransform toggleRect = toggleGO.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(1, 1);
            toggleRect.anchorMax = new Vector2(1, 1);
            toggleRect.pivot = new Vector2(1, 1);
            toggleRect.anchoredPosition = anchoredPosition;
            toggleRect.sizeDelta = new Vector2(180, 30);
            Image toggleBackgroundImage = toggleGO.AddComponent<Image>();
            toggleBackgroundImage.color = new Color(1, 1, 1, 1); // Make the checkbox's background white

            // Add a child GameObject for the checkmark
            GameObject checkmarkGO = new GameObject("Checkmark");
            checkmarkGO.transform.parent = toggleGO.transform;

            toggle.targetGraphic = toggleBackgroundImage;

            Text toggleTextComponent = AddTextToUI(toggleGO, toggleText);
            toggleTextComponent.alignment = TextAnchor.MiddleLeft;

            // Add a ColorBlock to handle the checked and unchecked colors
            ColorBlock colors = toggle.colors;

            bool isHost = false;

            toggle.onValueChanged.AddListener((value) =>
            {
                isHost = value;

                // Update the color based on the state
                if (isHost)
                {
                    colors.normalColor = Color.green; // Set the unchecked color to green
                    colors.highlightedColor = Color.green; // Set the highlighted color to green
                    colors.pressedColor = Color.green;
                    colors.selectedColor = Color.green;
                    toggle.colors = colors;
                }
                else
                {
                    colors.normalColor = Color.red; // Set the unchecked color to red
                    colors.highlightedColor = Color.red; // Set the highlighted color to red
                    colors.pressedColor = Color.red;
                    colors.selectedColor = Color.red;
                    toggle.colors = colors;
                }
            });
            return toggle;
        }
    }
}
