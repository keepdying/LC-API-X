using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using LC_API.GameInterfaceAPI;
using LC_API.ServerAPI;
using System.Collections.Generic;

namespace LC_API
{
    internal static class CheatDatabase
    {
        const string DAT_CD_BROADCAST = "LC_API_CD_Broadcast";
        const string SIG_REQ_GUID = "LC_API_ReqGUID";
        const string SIG_SEND_MODS = "LC_APISendMods";

        internal static ConfigEntry<bool> hideModList;
        internal static ConfigEntry<List<string>> customModList;
        private static Dictionary<string, PluginInfo> PluginsLoaded = new Dictionary<string, PluginInfo>();

        public static void RunLocalCheatDetector()
        {
            Plugin.Log.LogInfo("Some plugin is called RunLocalCheatDetector() method, Ignoring...");

        }

        public static void OtherPlayerCheatDetector()
        {
            Plugin.Log.LogWarning("Asking all other players for their mod list..");
            GameTips.ShowTip("Mod List:", "Asking all other players for installed mods..");
            GameTips.ShowTip("Mod List:", "Check the logs for more detailed results.\n<size=13>(Note that if someone doesnt show up on the list, they may not have LC_API installed)</size>");
            Networking.Broadcast(DAT_CD_BROADCAST, SIG_REQ_GUID);
        }

        internal static void CDNetGetString(string data, string signature)
        {
            if (data == DAT_CD_BROADCAST && signature == SIG_REQ_GUID)
            {
                if (!hideModList.Value)
               {
                    string mods = "";
                    Plugin.Log.LogWarning("Someone asked for my mod list, so I'm sending it.");
                    if (customModList.Value.Count == 0)
                    {   
                        Plugin.Log.LogWarning("Sending real mod list.");
                        foreach (PluginInfo info in PluginsLoaded.Values)
                        {
                            mods += "\n" + info.Metadata.Name + " " + info.Metadata.Version;
                        }
                    }
                    else {
                        Plugin.Log.LogWarning("Sending custom mod list.");
                        foreach (string mod in customModList.Value)
                        {
                            mods += "\n" + mod;
                        }
                    }
                    Networking.Broadcast(GameNetworkManager.Instance.localPlayerController.playerUsername + " responded with these mods:" + mods, SIG_SEND_MODS);
                } else {
                    Plugin.Log.LogWarning("Someone asked for my mod list, but I'm hiding it.");
                }
            }

            if (signature == SIG_SEND_MODS)
            {
                GameTips.ShowTip("Mod List:", data);
                Plugin.Log.LogWarning(data);
            }
        }
    }
}
