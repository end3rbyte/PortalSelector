using HarmonyLib;

namespace Landoria.PortalSelector
{
    // Removes known portals when Valheim receives a destruction notification.
    [HarmonyPatch(typeof(ZDOMan), "HandleDestroyedZDO", typeof(ZDOID))]
    internal static class PortalDestroyedPatch
    {
        // Forgets a destroyed ID even when its object is no longer loaded nearby.
        [HarmonyPrefix]
        private static void BeforeDestroy(ZDOID uid)
        {
            PortalDirectory.Forget(uid);
        }
    }
}
