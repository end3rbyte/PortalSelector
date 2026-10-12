using BepInEx;
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
        private Harmony _harmony;

        // Installs the game hooks and logs one startup message.
        private void Awake()
        {
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            Game.m_playerInitialSpawn += PortalDirectory.BeginValidation;
        }

        // Processes pending portal directory requests and dialog changes.
        private void Update()
        {
            PortalDirectory.Update();
            PortalDialog.Update();
        }

        // Removes this mod's hooks when BepInEx unloads it.
        private void OnDestroy()
        {
            Game.m_playerInitialSpawn -= PortalDirectory.BeginValidation;
            PortalDialog.Close();
            _harmony?.UnpatchSelf();
        }
    }
}
