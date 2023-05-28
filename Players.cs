
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class Players
{
    private readonly Dictionary<int, GameObject> _players;
    private readonly Dictionary<int, Vector3> _latestPlayerPositionsClient = new Dictionary<int, Vector3>();
    private readonly Logger _logger;
    private readonly ClientThreadActionsManager _mainThreadActionsManager;
    private readonly GameObject _playerModel;
    public Players(Logger logger, ClientThreadActionsManager mainThreadActionsManager, GameObject model)
    {
        _logger = logger;
        _mainThreadActionsManager = mainThreadActionsManager;
        _players = new Dictionary<int, GameObject>();
        _playerModel = model;
    }
    public void AddPlayer(int playerId, Vector3 position)
    {
        Action action = () =>
        {
            if (!_players.ContainsKey(playerId))
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

                _players.Add(playerId, mymodel);
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

    public void UpdatePlayerPosition(int playerId, Vector3 position, Quaternion rotation, bool isSprinting, bool isSneaking)
    {
        Action action = () =>
        {
            if (_players.ContainsKey(playerId) && _players[playerId] != null)
            {
                if (!_latestPlayerPositionsClient.ContainsKey(playerId))
                {
                    _latestPlayerPositionsClient.Add(playerId, _players[playerId].transform.position);
                }
                else
                {
                    _latestPlayerPositionsClient[playerId] = _players[playerId].transform.position;
                }

                _players[playerId].transform.position = position;

                // Lock the prefab rotation on the up and down axis, and keep the rotation on the left and right axis.
                float yRotation = rotation.eulerAngles.y;
                _players[playerId].transform.rotation = Quaternion.Euler(0, yRotation, 0);

                // Find the head bone (B-head) and adjust its rotation.
                Transform headBone = _players[playerId].transform.Find("DummyRig/root/B-hips/B-spine/B-chest/B-upperChest/B-neck/B-head");
                if (headBone != null)
                {
                    float headVerticalRotation = rotation.eulerAngles.x;
                    Quaternion headRotation = Quaternion.Euler(headVerticalRotation, 0, 0);
                    headBone.localRotation = headRotation;
                }
                else
                {
                    _logger.Log($"Head bone not found for player {playerId}.");
                }

                UpdatePlayerAnimation(playerId, isSprinting, isSneaking);
            }
        };
        _mainThreadActionsManager.EnqueueAction(action);
    }

    void UpdatePlayerAnimation(int playerId, bool isSprinting, bool isSneaking)
    {
        if (_players.ContainsKey(playerId) && _players[playerId] != null)
        {
            Vector3 currentPosition = _players[playerId].transform.position;
            Vector3 previousPosition;

            if (_latestPlayerPositionsClient.ContainsKey(playerId))
            {
                previousPosition = _latestPlayerPositionsClient[playerId];
            }
            else
            {
                previousPosition = currentPosition;
            }

            RaycastHit hit;
            bool isGrounded = Physics.Raycast(currentPosition, Vector3.down, out hit, 0.1f);

            Animator animator = _players[playerId].GetComponent<Animator>();
            float speed = 0f;
            float direction = 0f; // 0 means forward, 180 means backward
            if (animator == null)
            {
                _logger.Log($"Animator not found for player {playerId}!");
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
                float angle = Vector3.Angle(_players[playerId].transform.forward, movementVector);

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

    public void DeletePlayer(int playerId)
    {
        Action action = () =>
        {
            if (_players.ContainsKey(playerId) && _players[playerId] != null)
            {
                // Destroy the GameObject associated with the player
                GameObject playerObject = _players[playerId];
                UnityEngine.Object.Destroy(playerObject);

                // Remove the player from the dictionary
                _players.Remove(playerId);

                _logger.Log($"Player {playerId} has been removed.");
            }
            else
            {
                _logger.Log($"Player {playerId} does not exist and cannot be removed.");
            }
        };
        _mainThreadActionsManager.EnqueueAction(action);
    }

}
