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
            Trace(source.Guid, target != null && target.Active ?
                "active target=" + targetGuid + " zdo=" + target.Id :
                "unavailable target=" + targetGuid);
            return target != null && target.Active ? target.Id : ZDOID.None;
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
