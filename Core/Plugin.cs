using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace Landoria.PortalSelector
{
    // Starts the portal selector and owns its configuration.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "Landoria.PortalSelector";
        public const string PluginName = "PortalSelector";
        public const string PluginVersion = "0.1.0";
        internal static Plugin Instance { get; private set; }
        internal static ConfigEntry<float> CostMultiplier { get; private set; }
        internal static ConfigEntry<bool> DetailedLogging { get; private set; }
        private Harmony _harmony;

        // Binds settings and installs the game hooks.
        private void Awake()
        {
            Instance = this;
            CostMultiplier = Config.Bind("Construction", "PortalCostMultiplier", 1.5f,
                new ConfigDescription("Classic portal material cost: 1, 1.5, or 2.",
                    new AcceptableValueList<float>(1f, 1.5f, 2f)));
            DetailedLogging = Config.Bind("Diagnostics", "DetailedLogging", true,
                "Log portal discovery, linking, catalog, names, choices, and travel.");
            PortalLog.Initialize(Logger);
            PortalLog.Info("lifecycle", "Started; detailed logging=" +
                DetailedLogging.Value + ", cost multiplier=" + CostMultiplier.Value);
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
            CostMultiplier.Value = 1.5f;
            DetailedLogging.Value = true;
            Config.Save();
            ConfigWatcher.IgnoreCurrentFileVersion();
        }

        // Applies both live settings after a configuration reload.
        private void ApplyConfiguration()
        {
            PortalCost.Apply();
            PortalLog.Info("config", "Reloaded; detailed logging=" +
                DetailedLogging.Value + ", cost multiplier=" +
                CostMultiplier.Value + ".");
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
