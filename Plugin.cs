using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LC_API.ClientAPI;
using LC_API.Comp;
using LC_API.GameInterfaceAPI.Events;
using LC_API.ManualPatches;
using LC_API.ServerAPI;
using System.Reflection;
using UnityEngine;

namespace LC_API
{
    // .____    _________           _____  __________ .___  
    // |    |   \_   ___ \         /  _  \ \______   \|   | 
    // |    |   /    \  \/        /  /_\  \ |     ___/|   | 
    // |    |___\     \____      /    |    \|    |    |   | 
    // |_______ \\______  /______\____|__  /|____|    |___| 
    //         \/       \//_____/        \/                 
    /// <summary>
    /// The Lethal Company modding API plugin!
    /// </summary>
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public sealed class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// Runs after the LC API plugin's "Awake" method is finished.
        /// </summary>
        public static bool Initialized { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        internal static ManualLogSource Log;

        private ConfigEntry<bool> configLegacyAssetLoading;
        private ConfigEntry<bool> configDisableBundleLoader;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        private void Awake()
        {
            configLegacyAssetLoading = Config.Bind("General", "Legacy asset bundle loading", false, "Should the BundleLoader use legacy asset loading? Turning this on may help with loading assets from older plugins.");
            configDisableBundleLoader = Config.Bind("General", "Disable BundleLoader", false, "Should the BundleLoader be turned off? Enable this if you are having problems with mods that load assets using a different method from LC_API's BundleLoader.");
            CheatDatabase.hideModList = Config.Bind("LC_API_X", "Hide mod list", true, "Disables sending mod list to other players via LC_API_X");
            CheatDatabase.customModList = Config.Bind("LC_API_X", "Custom mod list", "", "A list of mods to pretend you have installed. This is useful for faking mod list for paranoid server owners. Default sends original mod list.");
            ServerPatch.enableModdedLobbyName = Config.Bind("LC_API_X", "Enable modded lobby name", false, "Should the modded lobby name be enabled? This will add [MODDED] to the start of the lobby name.");
            CommandHandler.commandPrefix = Config.Bind("General", "Prefix", "/", "Command prefix");

            Log = Logger;
            // Plugin startup logic
            Logger.LogWarning("\n.____    _________           _____  __________ .___  \r\n|    |   \\_   ___ \\         /  _  \\ \\______   \\|   | \r\n|    |   /    \\  \\/        /  /_\\  \\ |     ___/|   | \r\n|    |___\\     \\____      /    |    \\|    |    |   | \r\n|_______ \\\\______  /______\\____|__  /|____|    |___| \r\n        \\/       \\//_____/        \\/                 \r\n                                                     ");
            Logger.LogInfo($"LC_API_X Starting up..");

            Harmony harmony = new Harmony("ModAPI");
            MethodInfo originalLobbyCreated = AccessTools.Method(typeof(GameNetworkManager), "SteamMatchmaking_OnLobbyCreated");
            MethodInfo originalLobbyJoinable = AccessTools.Method(typeof(GameNetworkManager), "LobbyDataIsJoinable");

            MethodInfo patchLobbyCreate = AccessTools.Method(typeof(ServerPatch), nameof(ServerPatch.OnLobbyCreate));

            MethodInfo originalMenuAwake = AccessTools.Method(typeof(MenuManager), "Awake");

            MethodInfo patchCacheMenuMgr = AccessTools.Method(typeof(ServerPatch), nameof(ServerPatch.CacheMenuManager));

            MethodInfo originalAddChatMsg = AccessTools.Method(typeof(HUDManager), "AddChatMessage");

            MethodInfo patchChatInterpreter = AccessTools.Method(typeof(ServerPatch), nameof(ServerPatch.ChatInterpreter));

            MethodInfo originalSubmitChat = AccessTools.Method(typeof(HUDManager), "SubmitChat_performed");

            MethodInfo patchSubmitChat = AccessTools.Method(typeof(CommandHandler.SubmitChatPatch), nameof(CommandHandler.SubmitChatPatch.Transpiler));

            MethodInfo originalGameManagerAwake = AccessTools.Method(typeof(GameNetworkManager), nameof(GameNetworkManager.Awake));

            MethodInfo patchGameManagerAwake = AccessTools.Method(typeof(ServerPatch), nameof(ServerPatch.GameNetworkManagerAwake));

            harmony.Patch(originalMenuAwake, new HarmonyMethod(patchCacheMenuMgr));
            harmony.Patch(originalAddChatMsg, new HarmonyMethod(patchChatInterpreter));
            harmony.Patch(originalLobbyCreated, new HarmonyMethod(patchLobbyCreate));
            harmony.Patch(originalSubmitChat, null, null, new HarmonyMethod(patchSubmitChat));
            harmony.Patch(originalGameManagerAwake, new HarmonyMethod(patchGameManagerAwake));

            Networking.GetString += CheatDatabase.CDNetGetString;
            Networking.GetListString += Networking.LCAPI_NET_SYNCVAR_SET;

            Networking.SetupNetworking();
            Events.Patch(harmony);
        }

        internal void Start()
        {
            Initialize();
        }

        internal void OnDestroy()
        {
            Initialize();
        }

        internal void Initialize()
        {
            if (!Initialized)
            {
                Initialized = true;
                if (!configDisableBundleLoader.Value)
                {
                    BundleAPI.BundleLoader.Load(configLegacyAssetLoading.Value);
                }
                GameObject gameObject = new GameObject("API");
                DontDestroyOnLoad(gameObject);
                gameObject.AddComponent<LC_APIManager>();
                Logger.LogInfo($"LC_API_X Started!");
            }
        }

        internal static void PatchMethodManual(MethodInfo method, MethodInfo patch, Harmony harmony)
        {
            harmony.Patch(method, new HarmonyMethod(patch));
        }
    }
}