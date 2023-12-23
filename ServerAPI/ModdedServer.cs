using System;

#pragma warning disable CS0618 // Member is obsolete
namespace LC_API.ServerAPI
{
    /// <summary>
    /// You're probably here for <see cref="SetServerModdedOnly"/>
    /// </summary>
    public static class ModdedServer
    {
        private static bool moddedOnly;
        [Obsolete("Use SetServerModdedOnly() instead. This will be removed/private in a future update.")]
        public static bool setModdedOnly; // obsolete for the purposes of getting peoples' IDE's to bitch at them.

        public static int GameVersion { get; internal set; }

        /// <summary>
        /// Has the user been placed in modded only servers?
        /// </summary>
        public static bool ModdedOnly
        {
            get { return moddedOnly; }
        }

        /// <summary>
        /// Call this method to make your plugin place the user in modded only servers.
        /// </summary>
        public static void SetServerModdedOnly()
        {
            Plugin.Log.LogMessage("A plugin has tried to set your game to only modded lobbies");
        }

        /// <summary>
        /// For internal use. Do not call this method.
        /// </summary>
        public static void OnSceneLoaded()
        {
            Plugin.Log.LogMessage("OnSceneLoaded() called");
        }
    }
}
#pragma warning restore CS0618 // Member is obsolete
