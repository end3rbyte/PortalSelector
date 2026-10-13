using System.Collections.Generic;

namespace Landoria.PortalSelector
{
    // Stores each personal route by the GUIDs of its two portals.
    internal static class PlayerDestinations
    {
        private const string Prefix =
            "Landoria.PortalSelector.DestinationByGuidV1.";
        private static readonly Dictionary<string, string> LastResolution =
            new Dictionary<string, string>();

        internal static void Reset()
        {
            LastResolution.Clear();
        }

        // Finds both endpoints of routes between portals still in the catalog.
        internal static HashSet<string> LinkedGuids(HashSet<string> knownGuids)
        {
            var linked = new HashSet<string>();
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null)
            {
                return linked;
            }
            foreach (string source in knownGuids)
            {
                if (player.m_customData.TryGetValue(Key(source),
                        out string target) && source != target &&
                    knownGuids.Contains(target))
                {
                    linked.Add(source);
                    linked.Add(target);
                }
            }
            return linked;
        }

        // Removes the saved route of a portal deleted from the catalog.
        internal static void Forget(string guid)
        {
            if (Player.m_localPlayer == null || ZNet.instance == null)
            {
                return;
            }
            Player.m_localPlayer.m_customData.Remove(Key(guid));
            LastResolution.Remove(guid);
        }

        // Resolves a saved destination GUID to its current world object ID.
        internal static ZDOID Get(ZDOID origin)
        {
            Player player = Player.m_localPlayer;
            PortalRecord source = PortalDirectory.Find(origin);
            if (player == null || source == null || !source.Active ||
                ZNet.instance == null || !PlayerPortalNames.IsNamed(origin))
            {
                return ZDOID.None;
            }
            if (!player.m_customData.TryGetValue(Key(source.Guid),
                    out string targetGuid))
            {
                Trace(source.Guid, "no selection");
                return ZDOID.None;
            }
            PortalRecord target = PortalDirectory.FindByGuid(targetGuid);
            Trace(source.Guid, target != null ?
                (target.Active ? "active" : "inactive") + " target=" +
                    targetGuid + " zdo=" + target.Id :
                "unknown target=" + targetGuid);
            return target != null ? target.Id : ZDOID.None;
        }

        // Saves an explicit choice using both portals' GUIDs.
        internal static void Set(ZDOID origin, ZDOID destination)
        {
            Player player = Player.m_localPlayer;
            PortalRecord source = PortalDirectory.Find(origin);
            PortalRecord target = PortalDirectory.Find(destination);
            if (player != null && ZNet.instance != null && source != null &&
                target != null && source.Active && target.Active &&
                PlayerPortalNames.IsNamed(origin) &&
                PlayerPortalNames.IsNamed(destination))
            {
                player.m_customData[Key(source.Guid)] = target.Guid;
                Plugin.DebugLog("Route: selected source=" + source.Guid +
                    " target=" + target.Guid + " targetZdo=" + target.Id + ".");
            }
        }

        // Imports a vanilla partner only when no personal route was saved.
        internal static bool ImportIfAbsent(PortalRecord source, PortalRecord target)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null || source == null ||
                target == null || !source.Active || !target.Active ||
                source.Guid == target.Guid ||
                !PlayerPortalNames.IsNamed(source.Id) ||
                !PlayerPortalNames.IsNamed(target.Id))
            {
                return false;
            }
            string key = Key(source.Guid);
            if (player.m_customData.ContainsKey(key))
            {
                return false;
            }
            player.m_customData[key] = target.Guid;
            Plugin.DebugLog("Route: imported vanilla source=" + source.Guid +
                " target=" + target.Guid + " targetZdo=" + target.Id + ".");
            return true;
        }

        private static string Key(string guid)
        {
            return Prefix + ZNet.instance.GetWorldUID() + "." + guid;
        }

        // Reports a route only when its resolution changes.
        private static void Trace(string sourceGuid, string state)
        {
            if (LastResolution.TryGetValue(sourceGuid, out string previous) &&
                previous == state)
            {
                return;
            }
            LastResolution[sourceGuid] = state;
            Plugin.DebugLog("Route: source=" + sourceGuid + " " + state + ".");
        }
    }
}
