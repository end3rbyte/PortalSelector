using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Verifies a personal destination through Valheim's native ZDO request.
    internal static class PortalConnection
    {
        internal enum State { None, Connecting, Connected, Unavailable }
        private static readonly Dictionary<ZDOID, float> Requests =
            new Dictionary<ZDOID, float>();

        // Clears request timers when Valheim starts a different world session.
        internal static void Reset()
        {
            Requests.Clear();
        }

        // Resolves the destination only after the client receives its live ZDO.
        internal static ZDO GetTarget(ZDOID id)
        {
            ZDO target = ZDOMan.instance?.GetZDO(id);
            if (target == null || Game.instance == null ||
                !Game.instance.PortalPrefabHash.Contains(target.GetPrefab()))
            {
                return null;
            }
            return target;
        }

        // Requests a remote portal at most once per retry interval.
        internal static void Request(ZDOID id, bool force = false)
        {
            if (id == ZDOID.None || ZDOMan.instance == null ||
                ZRoutedRpc.instance == null || GetTarget(id) != null)
            {
                return;
            }
            if (!force && Requests.TryGetValue(id, out float previous) &&
                Time.unscaledTime - previous < 30f)
            {
                return;
            }
            Requests[id] = Time.unscaledTime;
            ZDOMan.instance.RequestZDO(id);
        }

        // Reports whether the selected portal has been confirmed in this session.
        internal static State GetState(ZDOID id)
        {
            if (id == ZDOID.None)
            {
                return State.None;
            }
            if (GetTarget(id) != null)
            {
                return State.Connected;
            }
            Request(id);
            return Requests.TryGetValue(id, out float attempted) &&
                Time.unscaledTime - attempted < 8f ? State.Connecting : State.Unavailable;
        }

        // A requested portal that never arrived is no longer a usable destination.
        internal static bool TimedOut(ZDOID id)
        {
            return Requests.TryGetValue(id, out float attempted) &&
                Time.unscaledTime - attempted >= 10f && GetTarget(id) == null;
        }
    }
}
