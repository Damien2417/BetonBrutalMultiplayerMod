using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

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
            if (screenName == "PauseMenu")
            {
                if (GameUI_Start_Patch.canvasGO != null)
                {
                    GameUI_Start_Patch.canvasGO.SetActive(true);
                    GameUI_Start_Patch.ManageConnectionUI();
                }
            }
            else // When we switch away from PauseMenu, we disable our menu
            {
                if (GameUI_Start_Patch.canvasGO != null)
                    GameUI_Start_Patch.canvasGO.SetActive(false);
            }
        }
    }

    [HarmonyPatch(typeof(GameUI))]
    [HarmonyPatch("Start")] // Patch the Start method of GameUI
    public static class GameUI_Start_Patch
    {
        public static GameObject canvasGO;
        public static GameObject connectionMenu;
        public static Button disconnectButton;

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

            // Add Connect Button
            Button connectButton = AddButtonToUI(connectionMenu, "ConnectButton", new Vector2(-10, -130), "Connect");

            // Add IsHost CheckBox
            Toggle isHostToggle = AddToggleToUI(connectionMenu, "IsHostToggle", new Vector2(-10, -90), "Host");
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
                Debug.Log("Is host: " + isHostToggle.isOn);

                if (GameObject.FindObjectOfType<MultiplayerManager>() == null)
                {
                    GameObject multiplayerManager = new GameObject("MultiplayerManager");
                    MultiplayerManager managerComponent = multiplayerManager.AddComponent<MultiplayerManager>();
                    managerComponent.Initialize(ipInputField.text, int.Parse(portInputField.text), isHostToggle.isOn);
                    ManageConnectionUI();
                    Canvas.ForceUpdateCanvases();
                }
            });

            // Create a new Panel for Disconnection
            GameObject disconnectionMenu = new GameObject("DisconnectionMenu");
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
            disconnectButton.gameObject.SetActive(isConnected);
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
            Image checkmarkImage = checkmarkGO.AddComponent<Image>();
            checkmarkImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Checkmark");

            toggle.targetGraphic = toggleBackgroundImage;
            toggle.graphic = checkmarkImage;

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
