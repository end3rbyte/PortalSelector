using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Verifies a portal destination through Valheim's native ZDO request.
    internal static class PortalConnection
    {
        private static readonly Dictionary<ZDOID, float> Requests =
            new Dictionary<ZDOID, float>();
        private static readonly HashSet<ZDOID> TrackedRequests =
            new HashSet<ZDOID>();

        // Clears request timers when Valheim starts a different world session.
        internal static void Reset()
        {
            Requests.Clear();
            TrackedRequests.Clear();
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

        // Confirms that the destination portal ZDO has reached this client.
        internal static ZDO GetConfirmedTarget(ZDOID id)
        {
            ZDO target = GetTarget(id);
            if (target == null)
            {
                return null;
            }
            if (TrackedRequests.Remove(id))
            {
                Plugin.DebugLog("ZDO: received portal zdo=" + id + ".");
            }
            return target;
        }

        // Uses the same request-and-wait rule as vanilla TargetFound.
        internal static bool TargetFound(ZDOID id)
        {
            if (id == ZDOID.None)
            {
                return false;
            }
            if (GetConfirmedTarget(id) != null)
            {
                return true;
            }
            Request(id);
            return false;
        }

        // Requests a missing portal while the player is near its source.
        internal static void Request(ZDOID id)
        {
            if (id == ZDOID.None || ZDOMan.instance == null ||
                ZRoutedRpc.instance == null || GetConfirmedTarget(id) != null)
            {
                return;
            }
            if (Requests.TryGetValue(id, out float previous) &&
                Time.unscaledTime - previous < 0.5f)
            {
                return;
            }
            Requests[id] = Time.unscaledTime;
            if (TrackedRequests.Add(id))
            {
                ZDO actual = GetTarget(id);
                Plugin.DebugLog("ZDO: request zdo=" + id + " local=" +
                    (actual == null ? "missing" : "available") + ".");
            }
            ZDOMan.instance.RequestZDO(id);
        }

    }
}
