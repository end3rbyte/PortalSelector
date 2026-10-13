using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Checks a former destination only after its personal route changes.
    internal static class PortalPreviousDestinationCheck
    {
        private const float Timeout = 3f;
        private static readonly Dictionary<ZDOID, float> Pending =
            new Dictionary<ZDOID, float>();
        private static Player _player;
        private static long _worldUid;

        // Starts a short native object request for the replaced destination.
        internal static void Begin(ZDOID id)
        {
            if (id == ZDOID.None || Player.m_localPlayer == null ||
                ZNet.instance == null || ZDOMan.instance == null ||
                ZRoutedRpc.instance == null ||
                PortalConnection.GetConfirmedTarget(id) != null)
            {
                return;
            }
            if (_player != Player.m_localPlayer ||
                _worldUid != ZNet.instance.GetWorldUID())
            {
                Pending.Clear();
            }
            _player = Player.m_localPlayer;
            _worldUid = ZNet.instance.GetWorldUID();
            Pending[id] = Time.unscaledTime + Timeout;
            Plugin.DebugLog("Previous destination: requesting zdo=" + id +
                " timeout=3s.");
            PortalConnection.Request(id);
        }

        // Keeps a destination that the player selected again.
        internal static void Cancel(ZDOID id)
        {
            Pending.Remove(id);
        }

        // Keeps requesting while waiting, then forgets only an unanswered portal.
        internal static void Update()
        {
            if (Pending.Count == 0)
            {
                return;
            }
            if (_player != Player.m_localPlayer || ZNet.instance == null ||
                ZDOMan.instance == null || ZRoutedRpc.instance == null ||
                ZNet.instance.GetWorldUID() != _worldUid)
            {
                Reset();
                return;
            }
            foreach (ZDOID id in new List<ZDOID>(Pending.Keys))
            {
                Check(id);
            }
        }

        // Ends a request on confirmation or after the response deadline.
        private static void Check(ZDOID id)
        {
            if (PortalDirectory.Find(id) == null ||
                PortalConnection.GetConfirmedTarget(id) != null)
            {
                Pending.Remove(id);
                Plugin.DebugLog("Previous destination: resolved zdo=" + id + ".");
                return;
            }
            if (Time.unscaledTime < Pending[id])
            {
                PortalConnection.Request(id);
                return;
            }
            Pending.Remove(id);
            Plugin.DebugLog("Previous destination: timed out zdo=" + id + ".");
            if (PortalDirectory.MarkInactive(id))
            {
                _player.Message(MessageHud.MessageType.TopLeft,
                    "Previous destination did not respond. Marked inactive.");
            }
        }

        // Drops pending checks when leaving the current world or unloading.
        internal static void Reset()
        {
            Pending.Clear();
            _player = null;
            _worldUid = 0;
        }
    }
}
