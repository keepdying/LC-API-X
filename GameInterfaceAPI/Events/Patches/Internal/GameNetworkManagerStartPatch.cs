﻿using BepInEx;
using HarmonyLib;
using LC_API.BundleAPI;
using System;
using System.IO;
using Unity.Netcode;
using UnityEngine;

namespace LC_API.GameInterfaceAPI.Events.Patches.Internal
{
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))]
    class GameNetworkManagerStartPatch
    {
        private static readonly string BUNDLE_PATH = Path.Combine(Paths.PluginPath, "2018-LC_API", "Bundles", "networking");

        private const string PLAYER_NETWORKING_ASSET_LOCATION = "assets/lc_api/playernetworkingprefab.prefab";

        //private const string ITEM_NETWORKING_ASSET_LOCATION = "assets/lc_api/itemnetworkingprefab.prefab";

        private static void Postfix(GameNetworkManager __instance)
        {
            if (!File.Exists(BUNDLE_PATH))
            {
                // People like to manually install and then not put stuff in the right place, guess we'll handle that...
                string p = Path.Combine(Paths.PluginPath, "Bundles", "networking");
                if (File.Exists(p))
                {
                    File.Move(p, BUNDLE_PATH);
                }
                else
                {
                    throw new Exception("Networking bundle not found at expected path");
                }
            }

            LoadedAssetBundle assets = BundleLoader.LoadAssetBundle(BUNDLE_PATH, false);

            GameObject playerObj = assets.GetAsset<GameObject>(PLAYER_NETWORKING_ASSET_LOCATION);
            playerObj.AddComponent<Features.Player>();
            playerObj.AddComponent<Features.Player.PlayerInventory>();
            __instance.GetComponent<NetworkManager>().AddNetworkPrefab(playerObj);
            Features.Player.PlayerNetworkPrefab = playerObj;

            foreach (NetworkPrefab prefab in __instance.GetComponent<NetworkManager>().NetworkConfig.Prefabs.Prefabs)
            {
                if (prefab.Prefab.GetComponent<GrabbableObject>() != null)
                {
                    prefab.Prefab.AddComponent<Features.Item>();
                }
            }

            //GameObject itemObj = assets.GetAsset<GameObject>(ITEM_NETWORKING_ASSET_LOCATION);
            //itemObj.AddComponent<Features.Item>();
            //__instance.GetComponent<NetworkManager>().AddNetworkPrefab(itemObj);
            //Features.Item.ItemNetworkPrefab = itemObj;
        }
    }
}
