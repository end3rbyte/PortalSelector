using BepInEx;
using BepInEx.Configuration;
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
        private static ManualLogSource _debugLogger;
        private static ConfigEntry<bool> _enableDebugLogs;
        private Harmony _harmony;

        // Writes diagnostics to the selected profile's BepInEx log.
        internal static void DebugLog(string message)
        {
            if (_enableDebugLogs?.Value == true)
            {
                _debugLogger?.LogInfo("[Debug] " + message);
            }
        }

        internal static void WarningLog(string message)
        {
            _debugLogger?.LogWarning(message);
        }

        // Installs the game hooks and logs one startup message.
        private void Awake()
        {
            _debugLogger = Logger;
            _enableDebugLogs = Config.Bind("Diagnostics", "EnableDebugLogs",
                true, "Write PortalSelector diagnostics to LogOutput.log.");
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            Game.m_playerInitialSpawn += PortalDirectory.BeginValidation;
        }

        // Scans loaded portals and checks destinations replaced by the player.
        private void Update()
        {
            PortalDirectory.Update();
            PortalPreviousDestinationCheck.Update();
        }

        // Removes this mod's hooks when BepInEx unloads it.
        private void OnDestroy()
        {
            Game.m_playerInitialSpawn -= PortalDirectory.BeginValidation;
            PortalDirectory.ResetValidation();
            PortalConnection.Reset();
            PlayerDestinations.Reset();
            PortalPreviousDestinationCheck.Reset();
            PortalNameConfirmPatch.Dispose();
            PortalDialog.Close();
            _harmony?.UnpatchSelf();
            _debugLogger = null;
            _enableDebugLogs = null;
        }
    }
}
