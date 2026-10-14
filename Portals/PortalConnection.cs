using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Finds distant portals by GUID after Valheim syncs their map area.
    internal static class PortalConnection
    {
        private static readonly Dictionary<ZDOID, float> Requests =
            new Dictionary<ZDOID, float>();
        private static readonly HashSet<ZDOID> TrackedRequests =
            new HashSet<ZDOID>();
        private static Guid _interestGuid;
        private static Vector3 _interestPosition;
        private static float _interestStarted;
        private static float _interestUntil;
        private static int _interestSyncs;
        private const float ResponseTimeout = 5f;

        // Clears request timers when Valheim starts a different world session.
        internal static void Reset()
        {
            Requests.Clear();
            TrackedRequests.Clear();
            _interestGuid = Guid.Empty;
            _interestSyncs = 0;
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

        // Finds a portal by GUID among ZDOs already received by this client.
        internal static ZDO FindLoaded(Guid guid)
        {
            if (guid == Guid.Empty || ZDOMan.instance == null)
            {
                return null;
            }
            foreach (ZDO portal in ZDOMan.instance.GetPortalList())
            {
                if (PortalFields.Read(portal).Guid == guid)
                {
                    return portal;
                }
            }
            return null;
        }

        // Finds a portal after Valheim syncs the target map area.
        internal static ZDO GetConfirmedTarget(Guid guid, Vector3 position,
            bool focus = true)
        {
            if (guid == Guid.Empty)
            {
                return null;
            }
            ZDO portal = FindLoaded(guid);
            if (portal != null)
            {
                if (_interestGuid == guid)
                {
                    _interestGuid = Guid.Empty;
                    Plugin.DebugLog("ZDO: resolved guid=" + guid +
                        " zdo=" + portal.m_uid + ".");
                }
                return portal;
            }
            if (focus)
            {
                Focus(guid, position);
            }
            return null;
        }

        // Waits for the destination area's ZDOs before enabling passage.
        internal static bool TargetFound(Guid guid, Vector3 position, bool focus)
        {
            return GetConfirmedTarget(guid, position, focus) != null;
        }

        // Supplies a destination position to Valheim's next area sync.
        internal static bool TryGetInterest(out Vector3 position)
        {
            position = _interestPosition;
            return _interestGuid != Guid.Empty &&
                Time.unscaledTime <= _interestUntil;
        }

        // Counts area syncs sent while waiting for one destination.
        internal static void MarkInterestSent()
        {
            _interestSyncs++;
        }

        // Reports when the nearby source has waited too long for its target.
        internal static bool TimedOut(Guid guid, Vector3 position)
        {
            return guid != Guid.Empty && _interestGuid == guid &&
                _interestPosition == position &&
                (ZNet.instance != null && ZNet.instance.IsServer() ||
                    _interestSyncs >= 2) &&
                Time.unscaledTime - _interestStarted >= ResponseTimeout;
        }

        // Stops requesting an area after its destination is removed.
        internal static void StopWaiting(Guid guid)
        {
            if (_interestGuid == guid)
            {
                _interestGuid = Guid.Empty;
                _interestSyncs = 0;
            }
        }

        // Requests the target area without trusting a saved ZDOID.
        private static void Focus(Guid guid, Vector3 position)
        {
            if (_interestGuid != guid || _interestPosition != position ||
                Time.unscaledTime > _interestUntil)
            {
                _interestStarted = Time.unscaledTime;
                _interestSyncs = 0;
                Plugin.DebugLog("ZDO: awaiting guid=" + guid +
                    " near=" + position + ".");
            }
            _interestGuid = guid;
            _interestPosition = position;
            _interestUntil = Time.unscaledTime + 2f;
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
