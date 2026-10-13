using HarmonyLib;

namespace Landoria.PortalSelector
{
    // Valheim's public destruction callback skips ZDOs absent from this client.
    [HarmonyPatch(typeof(ZDOMan), "HandleDestroyedZDO")]
    internal static class PortalDestroyedPatch
    {
        // Receives each destroyed ID before Valheim removes its local ZDO.
        [HarmonyPrefix]
        private static void BeforeDestroy(ZDOMan __instance, ZDOID uid)
        {
            PortalDirectory.OnDestroyedZDO(__instance, uid);
        }
    }
}
