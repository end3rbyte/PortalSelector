using System.Collections.Generic;

namespace Landoria.PortalSelector
{
    // Imports each portal's vanilla name and linked partner once per character and world.
    internal static class PortalVanillaImport
    {
        private const string Prefix = "Landoria.PortalSelector.VanillaImportGuidV1.";

        // Deletes the one-time import marker with a retired catalog entry.
        internal static void Forget(string guid)
        {
            if (Player.m_localPlayer != null && ZNet.instance != null)
            {
                Player.m_localPlayer.m_customData.Remove(Key(guid));
            }
        }

        // Collects ready portals, imports their names, then saves their routes.
        internal static void Update(List<ZDO> portals)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null)
            {
                return;
            }
            var ready = new List<KeyValuePair<PortalRecord, PortalRecord>>();
            var seen = new HashSet<string>();
            foreach (ZDO portal in portals)
            {
                if (TryPrepare(portal, player, seen, out PortalRecord source,
                    out PortalRecord partner))
                {
                    ready.Add(new KeyValuePair<PortalRecord, PortalRecord>(
                        source, partner));
                }
            }
            foreach (var pair in ready)
            {
                PlayerPortalNames.ImportFromShared(pair.Key, pair.Value);
            }
            foreach (var pair in ready)
            {
                bool routeImported = pair.Value != null &&
                    PlayerDestinations.ImportIfAbsent(pair.Key, pair.Value);
                player.m_customData[Key(pair.Key.Guid)] = "1";
                Plugin.DebugLog("Vanilla import: guid=" + pair.Key.Guid +
                    " partner=" + (pair.Value?.Guid ?? "none") +
                    " routeImported=" + routeImported + ".");
            }
        }

        // Waits for the exact vanilla partner and its GUID before importing a link.
        private static bool TryPrepare(ZDO portal, Player player,
            HashSet<string> seen, out PortalRecord source, out PortalRecord partner)
        {
            source = PortalDirectory.Find(portal.m_uid);
            partner = null;
            if (source == null || !source.Active ||
                portal.GetString(PortalDirectory.PortalGuidKey) != source.Guid ||
                !seen.Add(source.Guid) || player.m_customData.ContainsKey(Key(source.Guid)))
            {
                return false;
            }
            ZDOID targetId = portal.GetConnectionZDOID(
                ZDOExtraData.ConnectionType.Portal);
            if (targetId == ZDOID.None)
            {
                return true;
            }
            ZDO target = PortalConnection.GetTarget(targetId);
            partner = PortalDirectory.Find(targetId);
            if (target != null && partner != null && partner.Active &&
                target.GetString(PortalDirectory.PortalGuidKey) == partner.Guid)
            {
                return true;
            }
            PortalConnection.Request(targetId);
            return false;
        }

        // Scopes the one-time import marker to one world and portal GUID.
        private static string Key(string guid)
        {
            return Prefix + ZNet.instance.GetWorldUID() + "." + guid;
        }
    }
}
