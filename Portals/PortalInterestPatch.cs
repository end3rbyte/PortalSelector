using HarmonyLib;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Uses Valheim's ordinary area sync to receive a distant portal ZDO.
    [HarmonyPatch(typeof(ZNet), "SendServerSyncPlayerData")]
    internal static class PortalInterestPatch
    {
        // Gives travel priority, then checks one saved portal's area.
        [HarmonyPrefix]
        private static void BeforeSync(ref Vector3 ___m_referencePosition,
            out Vector3 __state)
        {
            __state = ___m_referencePosition;
            if (PortalConnection.TryGetInterest(out Vector3 position))
            {
                PortalConnection.MarkInterestSent();
                ___m_referencePosition = position;
            }
            else if (PortalDirectory.TryGetStartupInterest(out position))
            {
                PortalDirectory.MarkStartupInterestSent();
                ___m_referencePosition = position;
            }
        }

        // Restores the player's real reference position after sending.
        [HarmonyPostfix]
        private static void AfterSync(ref Vector3 ___m_referencePosition,
            Vector3 __state)
        {
            ___m_referencePosition = __state;
        }
    }
}
