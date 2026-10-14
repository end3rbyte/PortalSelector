using System;
using System.Collections.Generic;

namespace Landoria.PortalSelector
{
    // Resolves shared destination GUIDs stored on source portal ZDOs.
    internal static class PlayerDestinations
    {
        private static readonly Dictionary<ZDOID, string> LastResolution =
            new Dictionary<ZDOID, string>();
        private static readonly Dictionary<ZDOID, Guid> SuppressedRoutes =
            new Dictionary<ZDOID, Guid>();

        // Clears diagnostic state when the world or character changes.
        internal static void Reset()
        {
            LastResolution.Clear();
            SuppressedRoutes.Clear();
        }

        // Reads the source portal's shared destination, even if it is not known yet.
        internal static Guid Get(ZDOID origin)
        {
            if (PortalDirectory.Find(origin) == null ||
                !PlayerPortalNames.IsNamed(origin))
            {
                return Guid.Empty;
            }
            PortalZdoData data = PortalFields.Read(origin);
            Guid target = data?.DestinationGuid ?? Guid.Empty;
            if (target == Guid.Empty || target == data.Guid ||
                SuppressedRoutes.TryGetValue(origin, out Guid blocked) &&
                blocked == target)
            {
                Trace(origin, "no selection");
                return Guid.Empty;
            }
            Trace(origin, "target=" + target);
            return target;
        }

        // Saves an explicit choice on the source portal ZDO.
        internal static bool Set(ZDOID origin, Guid destination)
        {
            PortalRecord source = PortalDirectory.Find(origin);
            PortalRecord target = PortalDirectory.Find(destination);
            if (source == null || target == null ||
                source.Guid == destination ||
                !PlayerPortalNames.IsNamed(origin) ||
                string.IsNullOrWhiteSpace(target.LastKnownName) ||
                !PortalFields.Claim(origin) ||
                !PlayerPortalNames.EnsureStored(origin) ||
                !PortalFields.SetDestination(origin, destination,
                    target.Position))
            {
                return false;
            }
            Plugin.DebugLog("Route: saved on source zdo=" + origin +
                " targetGuid=" + destination + ".");
            SuppressedRoutes.Remove(origin);
            return true;
        }

        // Disconnects a nearby source whose target did not respond.
        internal static void ClearUnresponsive(ZDOID origin, Guid destination)
        {
            if (PortalFields.Read(origin)?.DestinationGuid != destination)
            {
                return;
            }
            SuppressedRoutes[origin] = destination;
            if (PortalFields.Claim(origin) &&
                PortalFields.SetDestination(origin, Guid.Empty,
                    UnityEngine.Vector3.zero))
            {
                SuppressedRoutes.Remove(origin);
            }
            LastResolution.Remove(origin);
        }

        // Clears locally owned routes to a portal Valheim destroyed.
        internal static void ForgetPortal(Guid destroyed)
        {
            int cleared = 0;
            if (ZDOMan.instance != null)
            {
                foreach (ZDO portal in ZDOMan.instance.GetPortalList())
                {
                    if (portal.IsOwner() &&
                        PortalFields.Read(portal).DestinationGuid == destroyed &&
                        PortalFields.SetDestination(portal.m_uid, Guid.Empty,
                            UnityEngine.Vector3.zero))
                    {
                        cleared++;
                    }
                }
            }
            LastResolution.Clear();
            if (cleared > 0)
            {
                Plugin.DebugLog("Route: cleared " + cleared +
                    " local routes to destroyed guid=" + destroyed + ".");
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
