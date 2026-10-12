using HarmonyLib;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Applies the configured material cost to the classic portal prefab.
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class PortalCost
    {
        // Updates the prefab when the game registers its pieces.
        [HarmonyPostfix]
        private static void OnSceneReady()
        {
            PortalConnection.Reset();
            Apply();
        }

        // Updates the material amounts after startup or a config reload.
        internal static void Apply()
        {
            GameObject prefab = ZNetScene.instance?.GetPrefab("portal_wood");
            Piece piece = prefab?.GetComponent<Piece>();
            if (piece == null || Plugin.CostMultiplier == null)
            {
                return;
            }
            foreach (Piece.Requirement resource in piece.m_resources)
            {
                string name = resource.m_resItem?.gameObject.name;
                int amount = name == "FineWood" ? 20 :
                    name == "GreydwarfEye" ? 10 : name == "SurtlingCore" ? 2 : 0;
                if (amount > 0)
                {
                    resource.m_amount = Mathf.RoundToInt(amount * Plugin.CostMultiplier.Value);
                    PortalLog.Detail("cost", name + " = " + resource.m_amount +
                        " (multiplier " + Plugin.CostMultiplier.Value + ").");
                }
            }
        }
    }
}
