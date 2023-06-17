
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Players
{
    private readonly Dictionary<string, GameObject> _players;
    private readonly Dictionary<string, Vector3> _latestPlayerPositionsClient = new Dictionary<string, Vector3>();
    private readonly Logger _logger;
    private readonly ClientThreadActionsManager _mainThreadActionsManager;
    private readonly GameObject _playerModel;

    public Players(Logger logger, ClientThreadActionsManager mainThreadActionsManager, GameObject model)
    {
        _logger = logger;
        _mainThreadActionsManager = mainThreadActionsManager;
        _players = new Dictionary<string, GameObject>();
        _playerModel = model;
    }
    public void AddPlayer(string playerName, Vector3 position)
    {
        Action action = () =>
        {
            if (!_players.ContainsKey(playerName))
            {
                if (_playerModel == null)
                {
                    _logger.Log("Failed to load custom model!");
                }

                GameObject mymodel = UnityEngine.Object.Instantiate(_playerModel, position, Quaternion.identity) as GameObject;

                Renderer renderer = mymodel.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material material = renderer.material;
                    if (material != null)
                    {
                        material.color = Color.red;
                    }
                }

                _players.Add(playerName, mymodel);
                _logger.Log($"Add player at position {position}");

                // Spawn a clone of the FlashlightAnchor and anchor it to the top of the model
                GameObject flashlightAnchorPrefab = GameObject.Find("Flashlight");
                GameObject newFlashlightAnchor = UnityEngine.Object.Instantiate(flashlightAnchorPrefab, mymodel.transform.position + Vector3.up, Quaternion.identity);

                newFlashlightAnchor.transform.parent = mymodel.transform;
                newFlashlightAnchor.transform.localPosition = new Vector3(0, 0.5f, 0);
            }
        };
        _mainThreadActionsManager.EnqueueAction(action);
    }

    public void UpdatePlayerPosition(string playerName, Vector3 position, Quaternion rotation, bool isSprinting, bool isSneaking)
    {
        Action action = () =>
        {
            if (_players.ContainsKey(playerName) && _players[playerName] != null)
            {
                if (!_latestPlayerPositionsClient.ContainsKey(playerName))
                {
                    _latestPlayerPositionsClient.Add(playerName, _players[playerName].transform.position);
                }
                else
                {
                    _latestPlayerPositionsClient[playerName] = _players[playerName].transform.position;
                }

                _players[playerName].transform.position = position;

                // Lock the prefab rotation on the up and down axis, and keep the rotation on the left and right axis.
                float yRotation = rotation.eulerAngles.y;
                _players[playerName].transform.rotation = Quaternion.Euler(0, yRotation, 0);

                // Find the head bone (B-head) and adjust its rotation.
                Transform headBone = _players[playerName].transform.Find("DummyRig/root/B-hips/B-spine/B-chest/B-upperChest/B-neck/B-head");
                if (headBone != null)
                {
                    float headVerticalRotation = rotation.eulerAngles.x;
                    Quaternion headRotation = Quaternion.Euler(headVerticalRotation, 0, 0);
                    headBone.localRotation = headRotation;
                }
                else
                {
                    _logger.Log($"Head bone not found for player {playerName}.");
                }

                
                // Raycast from camera to player
                RaycastHit hit;
                GameObject selfCam = GameObject.Find("PlayerController/Main Camera");
                GameObject canvasBeacon = _players[playerName].transform.Find("Canvas").gameObject;
                Transform canvas = canvasBeacon.transform.Find("playerName");
                Text playerNameText = canvas.GetComponentInChildren<Text>();
                GameObject rawImage = canvasBeacon.transform.Find("RawImage").gameObject;

                
                if (selfCam == null)
                {
                    _logger.Log($"Self cam not found");
                }
                else if (canvasBeacon == null)
                {
                    _logger.Log($"canvasBeacon not found");
                }
                else if (rawImage == null)
                {
                    _logger.Log($"rawImage not found");
                }
                else
                {
                    if (Physics.Raycast(selfCam.transform.position, (_players[playerName].transform.position - selfCam.transform.position).normalized, out hit))
                    {
                        if (hit.transform == _players[playerName].transform)
                        {
                            // If the player was hit by the raycast, disable the marker
                            rawImage.SetActive(false);
                        }
                        else
                        {
                            // If something else was hit, enable the marker
                            rawImage.SetActive(true);
                        }
                    }
                    playerNameText.text = playerName;

                }
                


                UpdatePlayerAnimation(playerName, isSprinting, isSneaking);
            }
        };
        _mainThreadActionsManager.EnqueueAction(action);
    }

    void UpdatePlayerAnimation(string playerName, bool isSprinting, bool isSneaking)
    {
        if (_players.ContainsKey(playerName) && _players[playerName] != null)
        {
            Vector3 currentPosition = _players[playerName].transform.position;
            Vector3 previousPosition;

            if (_latestPlayerPositionsClient.ContainsKey(playerName))
            {
                previousPosition = _latestPlayerPositionsClient[playerName];
            }
            else
            {
                previousPosition = currentPosition;
            }

            RaycastHit hit;
            bool isGrounded = Physics.Raycast(currentPosition, Vector3.down, out hit, 0.1f);

            Animator animator = _players[playerName].GetComponent<Animator>();
            float speed = 0f;
            float direction = 0f; // 0 means forward, 180 means backward
            if (animator == null)
            {
                _logger.Log($"Animator not found for player {playerName}!");
                return;
            }

            if (!isGrounded && currentPosition.y > previousPosition.y)
            {
                animator.SetBool("Jump", true);
            }
            else if (!isGrounded && currentPosition.y < previousPosition.y)
            {
                animator.SetBool("Fall", true);
            }
            else if (isGrounded)
            {
                // Calculate the speed and direction based on the distance moved
                Vector3 movementVector = (currentPosition - previousPosition).normalized;
                float angle = Vector3.Angle(_players[playerName].transform.forward, movementVector);

                float distanceMoved = Vector3.Distance(currentPosition, previousPosition);
                if (distanceMoved > 0.001f) // Use Mathf.Epsilon to account for floating-point inaccuracies
                {
                    // The angle will be in the range [0, 180], where 0 is forward and 180 is backward.
                    direction = angle;
                    if (isSprinting)
                    {
                        speed = 2.1f;
                    }
                    else if (isSneaking)
                    {
                        speed = 0.6f;
                    }
                    else
                    {
                        speed = 1.1f;
                    }
                }
                animator.SetBool("Jump", false);
                animator.SetBool("Fall", false);
            }
            animator.SetFloat("Speed", speed);
            animator.SetFloat("Direction", direction);
        }
    }

    public void DeletePlayer(string playerName)
    {
        Action action = () =>
        {
            if (_players.ContainsKey(playerName) && _players[playerName] != null)
            {
                // Destroy the GameObject associated with the player
                GameObject playerObject = _players[playerName];
                UnityEngine.Object.Destroy(playerObject);

                // Remove the player from the dictionary
                _players.Remove(playerName);
                _latestPlayerPositionsClient.Remove(playerName);

                _logger.Log($"Player {playerName} has been removed.");
            }
            else
            {
                _logger.Log($"Player {playerName} does not exist and cannot be removed.");
            }
        };
        _mainThreadActionsManager.EnqueueAction(action);
    }
    public List<KeyValuePair<string, Vector3>> GetPlayers()
    {
        return new List<KeyValuePair<string, Vector3>>(_latestPlayerPositionsClient);
    }
}
