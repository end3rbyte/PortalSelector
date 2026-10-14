using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Landoria.PortalSelector
{
    // Starts the portal selector and installs its game hooks.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "Landoria.PortalSelector";
        public const string PluginName = "PortalSelector";
        public const string PluginVersion = "1.0.0";
        private static ManualLogSource _logger;
        private Harmony _harmony;

        // Writes diagnostics to the selected profile's BepInEx log.
        internal static void DebugLog(string message)
        {
            _logger?.LogDebug(message);
        }

        internal static void WarningLog(string message)
        {
            _logger?.LogWarning(message);
        }

        // Installs the game hooks and logs one startup message.
        private void Awake()
        {
            _logger = Logger;
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
        }

        // Scans portals known to the local player.
        private void Update()
        {
            PortalDirectory.Update();
        }

        // Removes this mod's hooks when BepInEx unloads it.
        private void OnDestroy()
        {
            PortalConnection.Reset();
            PlayerDestinations.Reset();
            PortalNameConfirmPatch.Dispose();
            PortalDialog.Close();
            _harmony?.UnpatchSelf();
            _logger = null;
        }
    }
}
