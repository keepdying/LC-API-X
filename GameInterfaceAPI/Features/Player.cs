﻿using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace LC_API.GameInterfaceAPI.Features
{
    /// <summary>
    /// Encapsulates a <see cref="PlayerControllerB"/> for earier interacting.
    /// </summary>
    public class Player : NetworkBehaviour
    {
        internal static GameObject PlayerNetworkPrefab { get; set; }

        /// <summary>
        /// Gets a dictionary containing all <see cref="Player"/>'s. Even inactive ones.
        /// </summary>
        public static Dictionary<PlayerControllerB, Player> Dictionary { get; } = new Dictionary<PlayerControllerB, Player>();

        /// <summary>
        /// Gets a list containing all <see cref="Player"/>'s. Even inactive ones.
        /// </summary>
        public static IReadOnlyCollection<Player> List => Dictionary.Values;

        /// <summary>
        /// Gets a list containing only the currently active <see cref="Player"/>'s, dead or alive.
        /// </summary>
        /// TODO: `.Where` is bad. Potentially add and remove from this list as needed with a patch.
        public static IReadOnlyCollection<Player> ActiveList => Dictionary.Values.Where(p => p.IsActive).ToList();

        /// <summary>
        /// Gets the local <see cref="Player"/>.
        /// </summary>
        public static Player LocalPlayer { get; internal set; }

        /// <summary>
        /// Gets the host <see cref="Player"/>.
        /// </summary>
        public static Player HostPlayer { get; internal set; }

        /// <summary>
        /// Gets the encapsulated <see cref="PlayerControllerB"/>.
        /// </summary>
        public PlayerControllerB PlayerController { get; internal set; }

        internal NetworkVariable<ulong> NetworkClientId { get; } = new NetworkVariable<ulong>(ulong.MaxValue);

        /// <summary>
        /// Gets the <see cref="Player"/>'s client id.
        /// </summary>
        public ulong ClientId => PlayerController.actualClientId;

        /// <summary>
        /// Gets the <see cref="Player"/>'s steam id.
        /// </summary>
        public ulong SteamId => PlayerController.playerSteamId;

        /// <summary>
        /// Gets whether or not the <see cref="Player"/> is the host.
        /// </summary>
        public new bool IsHost => PlayerController.gameObject == PlayerController.playersManager.allPlayerObjects[0];

        /// <summary>
        /// Gets whether or not the <see cref="Player"/> is the current local player.
        /// </summary>
        public new bool IsLocalPlayer => PlayerController == StartOfRound.Instance.localPlayerController;

        /// <summary>
        /// Gets whether or not the <see cref="Player"/> has a connected user.
        /// </summary>
        public bool IsActive => IsControlled || IsDead;

        /// <summary>
        /// Gets whether or not the <see cref="Player"/> is currently being controlled.
        /// Lethal Company creates PlayerControllers ahead of time, so all of them always exist.
        /// </summary>
        public bool IsControlled => PlayerController.isPlayerControlled;

        /// <summary>
        /// Gets whether or not the <see cref="Player"/> is currently dead.
        /// Due to the way the PlayerController works, this is false if there is not an active user connected to the controller.
        /// </summary>
        public bool IsDead => PlayerController.isPlayerDead;

        /// <summary>
        /// Gets or sets the <see cref="Player"/>'s username.
        /// </summary>
        public string Username
        {
            get
            {
                return PlayerController.playerUsername;
            }
            set
            {
                PlayerController.playerUsername = value;

                if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
                {
                    SetPlayerUsernameClientRpc(value);
                }
                else
                {
                    SetPlayerUsernameServerRpc(value);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetPlayerUsernameServerRpc(string name, ServerRpcParams @params = default)
        {
            if (@params.Receive.SenderClientId == ClientId)
            {
                SetPlayerUsernameClientRpc(name);
            }
        }

        [ClientRpc]
        private void SetPlayerUsernameClientRpc(string name)
        {
            PlayerController.playerUsername = name;
            PlayerController.usernameBillboardText.text = name;
        }

        /// <summary>
        /// Gets or sets the <see cref="Player"/>'s sprint meter.
        /// </summary>
        /// <exception cref="Exception">Thrown when attempting to set position from the client.</exception>
        public float SprintMeter
        {
            get
            {
                return PlayerController.sprintMeter;
            }
            set
            {
                if (!(NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost))
                {
                    throw new Exception("Tried to sprint meter on client.");
                }

                PlayerController.sprintMeter = value;
                SetSprintMeterClientRpc(value);
            }
        }

        [ClientRpc]
        private void SetSprintMeterClientRpc(float value)
        {
            if (!NetworkManager.Singleton.IsClient) return;

            PlayerController.sprintMeter = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="Player"/>'s position. 
        /// If you set a <see cref="Features.Player"/>'s position out of bounds, they will be teleported back to a safe location next to the ship or entrance/exit to a dungeon.
        /// </summary>
        /// <exception cref="Exception">Thrown when attempting to set position from the client.</exception>
        public Vector3 Position
        {
            get
            {
                return PlayerController.transform.position;
            }
            set
            {
                if (!(NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost))
                {
                    throw new Exception("Tried to set position on client.");
                }

                PlayerController.transform.position = value;
                PlayerController.serverPlayerPosition = value;

                TeleportPlayerClientRpc(value);
            }
        }

        // UpdatePlayerPositionClientRpc doesn't actually set the player's position, so we need a custom rpc to do so.
        [ClientRpc]
        private void TeleportPlayerClientRpc(Vector3 position)
        {
            if (!NetworkManager.Singleton.IsClient) return;

            PlayerController.TeleportPlayer(position);

            if (IsLocalPlayer) PlayerController.UpdatePlayerPositionServerRpc(position, PlayerController.isInElevator, PlayerController.isExhausted, PlayerController.thisController.isGrounded);
        }

        /// <summary>
        /// Gets or sets the <see cref="Player"/>'s euler angles.
        /// </summary>
        /// <exception cref="Exception">Thrown when attempting to update euler angles from a client that isn't the local client, or the host.</exception>
        public Vector3 EulerAngles
        {
            get
            {
                return PlayerController.transform.eulerAngles;
            }
            set
            {
                if (!(IsLocalPlayer || NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
                {
                    throw new Exception("Tried to update euler angles from other client.");
                }

                PlayerController.transform.eulerAngles = value;

                // Only the local client or the host can set rotation, but if we are the host, we need to sync that to everyone.
                if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
                {
                    PlayerController.UpdatePlayerRotationFullClientRpc(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="Player"/>'s rotation. Quaternions can't gimbal lock, but they are harder to understand.
        /// Use <see cref="Player.EulerAngles"/> if you don't know what you're doing.
        /// </summary>
        /// <exception cref="Exception">Thrown when attempting to update rotation from a client that isn't the local client, or the host.</exception>
        public Quaternion Rotation
        {
            get
            {
                return PlayerController.transform.rotation;
            }
            set
            {
                if (!(IsLocalPlayer || NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
                {
                    throw new Exception("Tried to update rotation from other client.");
                }

                PlayerController.transform.rotation = value;
                PlayerController.transform.eulerAngles = value.eulerAngles;

                // Only the local client or the host can set rotation, but if we are the host, we need to sync that to everyone.
                if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
                {
                    PlayerController.UpdatePlayerRotationFullClientRpc(value.eulerAngles);
                }
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="Player"/>'s health.
        /// </summary>
        /// <exception cref="Exception">Thrown when attempting to set health from the client.</exception>
        public int Health
        {
            get
            {
                return PlayerController.health;
            }
            set
            {
                if (!(NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost))
                {
                    throw new Exception("Tried to set health on client.");
                }

                PlayerController.health = value;

                SetHealthClientRpc(value);
            }
        }

        [ClientRpc]
        private void SetHealthClientRpc(int health)
        {
            if (!NetworkManager.Singleton.IsClient) return;

            int oldHealth = PlayerController.health;

            PlayerController.health = health;

            if (PlayerController.IsOwner) HUDManager.Instance.UpdateHealthUI(health, health < oldHealth);

            if (health <= 0 && !PlayerController.isPlayerDead && PlayerController.AllowPlayerDeath())
            {
                PlayerController.KillPlayer(default, true, CauseOfDeath.Unknown, 0);
            }
        }

        private void Start()
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                Dictionary.Add(PlayerController, this);
            }
            else
            {
                PlayerController = StartOfRound.Instance.allPlayerScripts.FirstOrDefault(c => c.actualClientId == NetworkClientId.Value);
            }

            if (PlayerController != null)
            {
                if (IsLocalPlayer) LocalPlayer = this;
                if (IsHost) HostPlayer = this;
            }

            NetworkClientId.OnValueChanged += clientIdChanged;
        }

        /// <summary>
        /// Hurts the <see cref="Player"/>.
        /// </summary>
        /// <param name="damage">The amount of health to take from the <see cref="Player"/>.</param>
        /// <param name="causeOfDeath">The cause of death to show on the end screen.</param>
        /// <param name="bodyVelocity">he velocity to launch the ragdoll at, if killed.</param>
        /// <param name="overrideOneShotProtection">Whether or not to override one shot protection.</param>
        /// <param name="deathAnimation">Which death animation to use.</param>
        /// <param name="fallDamage">Whether or not this should be considered fall damage.</param>
        /// <param name="hasSFX">Whether or not this damage has sfx.</param>
        /// <exception cref="Exception">Thrown when attempting to hurt a <see cref="Player"/> that isn't the local <see cref="Player"/>'s, if not the host.</exception>
        public void Hurt(int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown, Vector3 bodyVelocity = default, bool overrideOneShotProtection = false, int deathAnimation = 0, bool fallDamage = false, bool hasSFX = true)
        {
            if (overrideOneShotProtection && Health - damage <= 0)
            {
                Kill(bodyVelocity, true, causeOfDeath, deathAnimation);
                return;
            }

            if (IsLocalPlayer)
            {
                PlayerController.DamagePlayer(damage, hasSFX, true, causeOfDeath, deathAnimation, fallDamage, bodyVelocity);
            }
            else
            {
                if (!(NetworkManager.IsServer || NetworkManager.IsHost))
                {
                    throw new Exception("Tried to kill player from other client.");
                }

                PlayerController.DamagePlayerClientRpc(damage, Health - damage);
            }
        }

        /// <summary>
        /// Kills the <see cref="Player"/>.
        /// </summary>
        /// <param name="bodyVelocity">The velocity to launch the ragdoll at, if spawned.</param>
        /// <param name="spawnBody">Whether or not to spawn a ragdoll.</param>
        /// <param name="causeOfDeath">The cause of death to show on the end screen.</param>
        /// <param name="deathAnimation">Which death animation to use.</param>
        /// <exception cref="Exception">Thrown when attempting to kill a <see cref="Player"/> that isn't the local <see cref="Player"/>'s, if not the host.</exception>
        public void Kill(Vector3 bodyVelocity = default, bool spawnBody = true, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown, int deathAnimation = 0)
        {
            if (IsLocalPlayer)
            {
                PlayerController.KillPlayer(bodyVelocity, spawnBody, causeOfDeath, deathAnimation);
            }
            else
            {
                if (!(NetworkManager.IsServer || NetworkManager.IsHost))
                {
                    throw new Exception("Tried to kill player from other client.");
                }

                PlayerController.KillPlayerClientRpc((int)ClientId, spawnBody, bodyVelocity, (int)causeOfDeath, deathAnimation);
            }
        }

        /// <summary>
        /// For internal use only. Do not use.
        /// </summary>
        public override void OnDestroy()
        {
            NetworkClientId.OnValueChanged -= clientIdChanged;
            base.OnDestroy();
        }

        #region Network variable handlers
        private void clientIdChanged(ulong oldId, ulong newId)
        {
            PlayerController = StartOfRound.Instance.allPlayerScripts.FirstOrDefault(c => c.actualClientId == newId);

            if (PlayerController != null)
            {
                if (IsLocalPlayer) LocalPlayer = this;
                if (IsHost) HostPlayer = this;
            }
        }
        #endregion

        #region Player getters
        /// <summary>
        /// Gets or adds a <see cref="Features.Player"/> from a <see cref="PlayerControllerB"/>.
        /// </summary>
        /// <param name="playerController">The player's <see cref="PlayerControllerB"/>.</param>
        /// <returns>A <see cref="Player"/>.</returns>
        public static Player GetOrAdd(PlayerControllerB playerController)
        {
            if (playerController == null) return null;

            if (Dictionary.TryGetValue(playerController, out Player player)) return player;

            foreach (Player p in FindObjectsOfType<Player>())
            {
                if (p.NetworkClientId.Value == playerController.actualClientId)
                {
                    Dictionary.Add(playerController, p);
                    return p;
                }
            }

            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                GameObject go = Instantiate(PlayerNetworkPrefab, Vector3.zero, default);
                go.SetActive(true);
                Player p = go.GetComponent<Player>();
                p.PlayerController = playerController;
                go.GetComponent<NetworkObject>().Spawn(false);

                return p;
            }

            return null;
        }

        /// <summary>
        /// Gets a <see cref="Features.Player"/> from a <see cref="PlayerControllerB"/>.
        /// </summary>
        /// <param name="playerController">The player's <see cref="PlayerControllerB"/>.</param>
        /// <returns>A <see cref="Player"/> or <see langword="null"/> if not found.</returns>
        public static Player Get(PlayerControllerB playerController)
        {
            if (Dictionary.TryGetValue(playerController, out Player player)) return player;

            return null;
        }

        /// <summary>
        /// Tries to get a <see cref="Features.Player"/> from a <see cref="PlayerControllerB"/>.
        /// </summary>
        /// <param name="playerController">The player's <see cref="PlayerControllerB"/>.</param>
        /// <param name="player">Outputs a <see cref="Player"/>, or <see langword="null"/> if not found.</param>
        /// <returns><see langword="true"/> if a <see cref="Features.Player"/> is found, <see langword="false"/> otherwise.</returns>
        public static bool TryGet(PlayerControllerB playerController, out Player player)
        {
            return Dictionary.TryGetValue(playerController, out player);
        }

        /// <summary>
        /// Gets a <see cref="Features.Player"/> from a <see cref="Features.Player"/>'s client id.
        /// </summary>
        /// <param name="clientId">The player's client id.</param>
        /// <returns>A <see cref="Player"/> or <see langword="null"/> if not found.</returns>
        public static Player Get(ulong clientId)
        {
            foreach (Player player in List)
            {
                if (player.ClientId == clientId) return player;
            }

            return null;
        }

        /// <summary>
        /// Tries to get a <see cref="Features.Player"/> from a <see cref="Features.Player"/>'s client id.
        /// </summary>
        /// <param name="clientId">The player's client id.</param>
        /// <param name="player">Outputs a <see cref="Player"/>, or <see langword="null"/> if not found.</param>
        /// <returns><see langword="true"/> if a <see cref="Features.Player"/> is found, <see langword="false"/> otherwise.</returns>
        public static bool TryGet(ulong clientId, out Player player)
        {
            return (player = Get(clientId)) != null;
        }
        #endregion
    }
}
