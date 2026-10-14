using System.Collections.Generic;

namespace Landoria.PortalSelector
{
    // Resolves shared destination ZDOIDs stored on source portal ZDOs.
    internal static class PlayerDestinations
    {
        private static readonly Dictionary<ZDOID, string> LastResolution =
            new Dictionary<ZDOID, string>();

        // Clears diagnostic state when the world or character changes.
        internal static void Reset()
        {
            LastResolution.Clear();
        }

        // Reads the source portal's shared destination, even if it is not known yet.
        internal static ZDOID Get(ZDOID origin)
        {
            if (PortalDirectory.Find(origin) == null ||
                !PlayerPortalNames.IsNamed(origin))
            {
                return ZDOID.None;
            }
            ZDOID targetId = PortalFields.Read(origin)?.Destination ??
                ZDOID.None;
            if (targetId == ZDOID.None || targetId == origin)
            {
                Trace(origin, "no selection");
                return ZDOID.None;
            }
            Trace(origin, "target=" + targetId);
            return targetId;
        }

        // Saves an explicit choice on the source portal ZDO.
        internal static bool Set(ZDOID origin, ZDOID destination)
        {
            if (origin == destination || PortalDirectory.Find(origin) == null ||
                PortalDirectory.Find(destination) == null ||
                !PlayerPortalNames.IsNamed(origin) ||
                !PlayerPortalNames.IsNamed(destination) ||
                !PortalFields.Claim(origin) ||
                !PlayerPortalNames.EnsureStored(origin) ||
                !PortalFields.SetDestination(origin, destination))
            {
                return false;
            }
            Plugin.DebugLog("Route: saved on source zdo=" + origin +
                " target=" + destination + ".");
            return true;
        }

        // Clears locally owned routes to a portal Valheim destroyed.
        internal static void ForgetPortal(ZDOID destroyed)
        {
            int cleared = 0;
            if (ZDOMan.instance != null)
            {
                foreach (ZDO portal in ZDOMan.instance.GetPortalList())
                {
                    if (portal.IsOwner() &&
                        PortalFields.Read(portal).Destination == destroyed &&
                        PortalFields.SetDestination(portal.m_uid, ZDOID.None))
                    {
                        cleared++;
                    }
                }
            }
            LastResolution.Clear();
            if (cleared > 0)
            {
                Plugin.DebugLog("Route: cleared " + cleared +
                    " local routes to destroyed zdo=" + destroyed + ".");
            }
        }

        // Reports a route only when its resolution changes.
        private static void Trace(ZDOID source, string state)
        {
            if (LastResolution.TryGetValue(source, out string previous) &&
                previous == state)
            {
                return;
            }
            LastResolution[source] = state;
            Plugin.DebugLog("Route: source=" + source + " " + state + ".");
        }
    }
}
