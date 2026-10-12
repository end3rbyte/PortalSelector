using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace Landoria.PortalSelector
{
    // Starts the portal selector and owns its logging configuration.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "Landoria.PortalSelector";
        public const string PluginName = "PortalSelector";
        public const string PluginVersion = "0.1.0";
        internal static Plugin Instance { get; private set; }
        internal static ConfigEntry<bool> DetailedLogging { get; private set; }
        private Harmony _harmony;

        // Binds settings and installs the game hooks.
        private void Awake()
        {
            Instance = this;
            DetailedLogging = Config.Bind("Diagnostics", "DetailedLogging", true,
                "Log portal discovery, linking, catalog, names, choices, and travel.");
            PortalLog.Initialize(Logger);
            PortalLog.Info("lifecycle", "Started; detailed logging=" +
                DetailedLogging.Value + ", cost multiplier=" +
                PortalCost.CostMultiplier);
            ConfigWatcher.Initialize(Config, Logger, PluginName, RestoreDefaults,
                ApplyConfiguration);
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            Game.m_playerInitialSpawn += PortalDirectory.BeginValidation;
        }

        // Processes file changes and pending portal directory requests.
        private void Update()
        {
            ConfigWatcher.Update();
            PortalDirectory.Update();
            PortalDialog.Update();
        }

        // Restores the single setting when the config file is deleted.
        private void RestoreDefaults()
        {
            DetailedLogging.Value = true;
            Config.Save();
            ConfigWatcher.IgnoreCurrentFileVersion();
        }

        // Applies the live logging setting after a configuration reload.
        private void ApplyConfiguration()
        {
            PortalLog.Info("config", "Reloaded; detailed logging=" +
                DetailedLogging.Value + ".");
        }

        // Removes only this mod's hooks and watcher.
        private void OnDestroy()
        {
            PortalLog.Info("lifecycle", "Stopping PortalSelector.");
            Game.m_playerInitialSpawn -= PortalDirectory.BeginValidation;
            PortalDialog.Close();
            ConfigWatcher.Dispose();
            _harmony?.UnpatchSelf();
            PortalLog.Dispose();
            Instance = null;
        }
    }
}
