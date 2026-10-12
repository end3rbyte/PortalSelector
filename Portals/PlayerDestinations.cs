using System;
using System.Collections.Generic;

namespace Landoria.PortalSelector
{
    // Stores personal routes with stable source and destination identities.
    internal static class PlayerDestinations
    {
        private const string Prefix = "Landoria.PortalSelector.DestinationStable.";

        // Resolves a saved destination to its current Valheim object ID.
        internal static ZDOID Get(ZDOID origin)
        {
            Player player = Player.m_localPlayer;
            PortalRecord source = PortalDirectory.Find(origin);
            if (player == null || source == null || ZNet.instance == null)
            {
                return ZDOID.None;
            }
            if (!player.m_customData.TryGetValue(Key(source), out string identity) &&
                !player.m_customData.TryGetValue(LegacyKey(source), out identity))
            {
                return ZDOID.None;
            }
            return PortalDirectory.FindByIdentity(identity)?.Id ?? ZDOID.None;
        }

        // Changes a route in memory for Valheim's next normal character save.
        internal static void Set(ZDOID origin, ZDOID destination)
        {
            Player player = Player.m_localPlayer;
            PortalRecord source = PortalDirectory.Find(origin);
            PortalRecord target = PortalDirectory.Find(destination);
            if (player != null && source != null && target != null &&
                ZNet.instance != null)
            {
                player.m_customData[Key(source)] = PortalIdentity.Key(target);
            }
        }

        // Copies an existing vanilla link only for a new personal source.
        internal static void InitializeFromShared(ZDO portal)
        {
            Player player = Player.m_localPlayer;
            PortalRecord source = PortalDirectory.Find(portal.m_uid);
            if (player == null || source == null || ZNet.instance == null ||
                (player.m_customData.ContainsKey(Key(source)) ||
                 player.m_customData.ContainsKey(LegacyKey(source))))
            {
                return;
            }
            ZDOID targetId = portal.GetConnectionZDOID(
                ZDOExtraData.ConnectionType.Portal);
            PortalRecord target = PortalDirectory.Find(targetId);
            if (target != null)
            {
                player.m_customData[Key(source)] = PortalIdentity.Key(target);
            }
        }

        // Clears routes involving a confirmed destroyed portal.
        internal static void Forget(PortalRecord portal)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null)
            {
                return;
            }
            string identity = PortalIdentity.Key(portal);
            string legacyIdentity = PortalIdentity.LegacyKey(portal);
            bool clearLegacy = !PortalDirectory.HasOtherLegacyIdentity(portal);
            string prefix = Prefix + ZNet.instance.GetWorldUID() + ".";
            List<string> affected = new List<string>();
            foreach (KeyValuePair<string, string> choice in player.m_customData)
            {
                if (choice.Key.StartsWith(prefix, StringComparison.Ordinal) &&
                    (choice.Key == Key(portal) ||
                     choice.Value == identity ||
                     clearLegacy && (choice.Key == LegacyKey(portal) ||
                                     choice.Value == legacyIdentity)))
                {
                    affected.Add(choice.Key);
                }
            }
            foreach (string key in affected)
            {
                player.m_customData[key] = "";
            }
        }

        // Namespaces the physical portal identity to the active world.
        private static string Key(PortalRecord record)
        {
            return Prefix + ZNet.instance.GetWorldUID() + "." +
                PortalIdentity.Key(record);
        }

        // Finds a route saved under the earlier rounded position key.
        private static string LegacyKey(PortalRecord record)
        {
            return Prefix + ZNet.instance.GetWorldUID() + "." +
                PortalIdentity.LegacyKey(record);
        }
    }
}
