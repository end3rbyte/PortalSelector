using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Landoria.PortalSelector
{
    // Reports which mod data Valheim serializes with the local character.
    [HarmonyPatch(typeof(Player), nameof(Player.Save), typeof(ZPackage))]
    internal static class CharacterSaveLog
    {
        // Runs after Valheim writes the player's custom data to its package.
        [HarmonyPostfix]
        private static void AfterSave(Player __instance)
        {
            if (__instance != Player.m_localPlayer) { return; }
            int names = 0;
            int destinations = 0;
            int catalogs = 0;
            foreach (KeyValuePair<string, string> entry in __instance.m_customData)
            {
                if (!entry.Key.StartsWith("Landoria.PortalSelector.",
                    StringComparison.Ordinal)) { continue; }
                if (entry.Key.Contains(".NameStable.")) { names++; }
                else if (entry.Key.Contains(".DestinationStable."))
                {
                    destinations++;
                }
                else if (entry.Key.Contains(".Catalog.")) { catalogs++; }
            }
            PortalLog.Info("character-save", "Player.Save serialized " +
                catalogs + " world catalogs, " + names + " personal names, " +
                destinations + " personal destinations.");
        }
    }
}
