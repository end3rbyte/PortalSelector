using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Rechecks saved portals once after the local player's first spawn.
    internal static partial class PortalDirectory
    {
        private const float StartupTimeout = 10f;
        private const float StartupSpacing = 0.25f;
        private const int MaxStartupCandidates = 2048;
        private static readonly Queue<StartupEntry> StartupQueue =
            new Queue<StartupEntry>();
        private static readonly HashSet<Guid> StartupScheduled =
            new HashSet<Guid>();
        private static ZNet _startupSession;
        private static StartupEntry _startupCurrent;
        private static bool _startupRequested;
        private static bool _startupStarted;
        private static bool _startupCompleted;
        private static float _nextStartupCheck;
        private static int _startupChecked;
        private static int _startupRemoved;

        // Starts one catalog pass for the first spawn of this world session.
        internal static void BeginStartupCleanup()
        {
            if (_startupSession != ZNet.instance)
            {
                ResetStartupCleanup();
                _startupSession = ZNet.instance;
            }
            if (!_startupStarted && !_startupCompleted)
            {
                _startupRequested = true;
                Plugin.DebugLog("Startup: first spawn requested catalog check.");
            }
        }

        // Clears pending requests when the plugin or world session ends.
        internal static void ResetStartupCleanup()
        {
            StartupQueue.Clear();
            StartupScheduled.Clear();
            _startupSession = null;
            _startupCurrent = null;
            _startupRequested = false;
            _startupStarted = false;
            _startupCompleted = false;
            _startupChecked = 0;
            _startupRemoved = 0;
        }

        // Advances a single staggered check after the catalog has loaded.
        private static void UpdateStartupCleanup()
        {
            if (!_startupRequested && !_startupStarted)
            {
                return;
            }
            if (_startupSession != ZNet.instance || _player != Player.m_localPlayer)
            {
                ResetStartupCleanup();
                return;
            }
            if (_startupRequested && WorldGenerator.instance != null)
            {
                StartStartupCleanup();
            }
            if (!_startupStarted)
            {
                return;
            }
            if (_startupCurrent != null)
            {
                CheckStartupCurrent();
            }
            else if (Time.unscaledTime >= _nextStartupCheck)
            {
                DispatchStartupCheck();
            }
            if (_startupCurrent == null && StartupQueue.Count == 0)
            {
                FinishStartupCleanup();
            }
        }

        // Snapshots the saved GUIDs before checking live world objects.
        private static void StartStartupCleanup()
        {
            _startupRequested = false;
            _startupStarted = true;
            _nextStartupCheck = Time.unscaledTime;
            foreach (PortalRecord record in Records.Values)
            {
                ScheduleStartupCheck(record.Guid, record.Position, true);
            }
            Plugin.DebugLog("Startup: checking " + StartupQueue.Count +
                " saved portals.");
            PortalPeerSync.RequestSnapshot();
        }

        // Checks a known local ZDO immediately or requests its saved area.
        private static void DispatchStartupCheck()
        {
            if (StartupQueue.Count == 0)
            {
                return;
            }
            _startupCurrent = StartupQueue.Dequeue();
            Plugin.DebugLog("Startup: checking guid=" +
                _startupCurrent.Guid + " known=" + _startupCurrent.Known +
                " near=" + _startupCurrent.Position + ".");
            if (_startupCurrent.Known && !Records.ContainsKey(_startupCurrent.Guid))
            {
                Plugin.DebugLog("Startup: skipped removed guid=" +
                    _startupCurrent.Guid + ".");
                ReleaseStartupCheck();
                return;
            }
            ZDO portal = PortalConnection.FindLoaded(_startupCurrent.Guid);
            if (portal != null)
            {
                ConfirmStartupPortal(portal);
                ReleaseStartupCheck();
            }
            else if (ZNet.instance.IsServer())
            {
                Plugin.DebugLog("Startup: host has no matching portal guid=" +
                    _startupCurrent.Guid + ".");
                ExpireStartupCheck();
                ReleaseStartupCheck();
            }
        }

        // Accepts a matching GUID or expires after two area syncs and ten seconds.
        private static void CheckStartupCurrent()
        {
            if (_startupCurrent.Known && !Records.ContainsKey(_startupCurrent.Guid))
            {
                ReleaseStartupCheck();
                return;
            }
            ZDO portal = PortalConnection.FindLoaded(_startupCurrent.Guid);
            if (portal != null)
            {
                ConfirmStartupPortal(portal);
                ReleaseStartupCheck();
                return;
            }
            if (PortalConnection.TryGetInterest(out _))
            {
                if (_startupCurrent.FirstSync >= 0f)
                {
                    Plugin.DebugLog("Startup: travel paused check guid=" +
                        _startupCurrent.Guid + ".");
                }
                _startupCurrent.FirstSync = -1f;
                _startupCurrent.Syncs = 0;
                return;
            }
            if (_startupCurrent.FirstSync >= 0f &&
                _startupCurrent.Syncs >= 2 &&
                Time.unscaledTime - _startupCurrent.FirstSync >= StartupTimeout)
            {
                ExpireStartupCheck();
                ReleaseStartupCheck();
            }
        }

        // Gives Valheim the saved position when no active travel needs a sync.
        internal static bool TryGetStartupInterest(out Vector3 position)
        {
            position = Vector3.zero;
            if (_startupCurrent == null || _startupSession != ZNet.instance ||
                Player.m_localPlayer == null || ZNet.instance == null ||
                ZNet.instance.IsServer())
            {
                return false;
            }
            position = Records.TryGetValue(_startupCurrent.Guid,
                out PortalRecord record) ? record.Position :
                _startupCurrent.Position;
            return true;
        }

        // Starts the deadline only after Valheim sends the first area interest.
        internal static void MarkStartupInterestSent()
        {
            if (_startupCurrent == null)
            {
                return;
            }
            if (_startupCurrent.FirstSync < 0f)
            {
                _startupCurrent.FirstSync = Time.unscaledTime;
                Plugin.DebugLog("Startup: requesting guid=" +
                    _startupCurrent.Guid + " near=" + _startupCurrent.Position +
                    ".");
            }
            _startupCurrent.Syncs++;
            Plugin.DebugLog("Startup: area sync guid=" +
                _startupCurrent.Guid + " count=" +
                _startupCurrent.Syncs + ".");
        }

        // Refreshes the name and follows any changed shared destination.
        private static void ConfirmStartupPortal(ZDO portal)
        {
            PortalZdoData data = PortalFields.Read(portal);
            string oldName = Records.TryGetValue(data.Guid,
                out PortalRecord old) ? old.LastKnownName : null;
            if (!Remember(portal))
            {
                return;
            }
            PortalRecord current = Find(data.Guid);
            Plugin.DebugLog("Startup: live portal guid=" + data.Guid +
                " zdo=" + portal.m_uid + " name='" +
                current?.LastKnownName + "' position=" +
                current?.Position + ".");
            if (current != null && oldName != current.LastKnownName)
            {
                PortalDialog.OnPortalUpdated(current);
                Plugin.DebugLog("Startup: refreshed name guid=" + data.Guid +
                    ".");
                if (old != null)
                {
                    PortalPeerSync.AnnouncePortal(portal.m_uid, current);
                }
            }
            Guid route = PlayerDestinations.Get(portal.m_uid);
            if (route != Guid.Empty && route != data.Guid)
            {
                ScheduleStartupCheck(route, data.DestinationPosition,
                    Records.ContainsKey(route));
            }
            Plugin.DebugLog("Startup: confirmed guid=" + data.Guid +
                " route=" + route + ".");
        }

        // Queues each known portal or newly linked destination at most once.
        private static void ScheduleStartupCheck(Guid guid, Vector3 position,
            bool known)
        {
            if (guid != Guid.Empty && StartupScheduled.Add(guid))
            {
                StartupQueue.Enqueue(new StartupEntry(guid, position, known));
                Plugin.DebugLog("Startup: queued guid=" + guid +
                    " known=" + known + " near=" + position +
                    " pending=" + StartupQueue.Count + ".");
            }
        }

        // Verifies portals learned from peers before adding them to this catalog.
        internal static void QueueStartupCandidate(Guid guid, Vector3 position)
        {
            if (_startupSession != ZNet.instance || _player == null ||
                (!_startupStarted && !_startupCompleted))
            {
                Plugin.DebugLog("Startup: ignored peer guid=" + guid +
                    " because catalog check is not ready.");
                return;
            }
            if (Records.ContainsKey(guid) || RemovedIds.Contains(guid) ||
                StartupScheduled.Contains(guid) ||
                StartupScheduled.Count >= MaxStartupCandidates)
            {
                Plugin.DebugLog("Startup: ignored peer guid=" + guid +
                    " known=" + Records.ContainsKey(guid) +
                    " removed=" + RemovedIds.Contains(guid) +
                    " queued=" + StartupScheduled.Contains(guid) +
                    " limit=" + (StartupScheduled.Count >= MaxStartupCandidates) +
                    ".");
                return;
            }
            ScheduleStartupCheck(guid, position, false);
            if (_startupCompleted && StartupQueue.Count > 0)
            {
                Plugin.DebugLog("Startup: resumed check for peer portals.");
                _startupCompleted = false;
                _startupStarted = true;
                _nextStartupCheck = Time.unscaledTime;
            }
        }

        // Removes an unanswered saved portal only from this player's catalog.
        private static void ExpireStartupCheck()
        {
            Guid guid = _startupCurrent.Guid;
            bool removed = _startupCurrent.Known && Records.Remove(guid);
            if (removed)
            {
                PlayerPortalCatalog.Save(_player, _catalogKey, Records);
                PortalDialog.OnPortalRemoved(guid);
                _startupRemoved++;
            }
            Plugin.DebugLog("Startup: unanswered guid=" + guid +
                " known=" + _startupCurrent.Known +
                " syncs=" + _startupCurrent.Syncs +
                " removed=" + removed + ".");
        }

        // Separates checks so one spawn does not request every area at once.
        private static void ReleaseStartupCheck()
        {
            _startupCurrent = null;
            _startupChecked++;
            _nextStartupCheck = Time.unscaledTime + StartupSpacing;
        }

        // Reports the finished one-time pass without repeating it on respawn.
        private static void FinishStartupCleanup()
        {
            _startupStarted = false;
            _startupCompleted = true;
            Plugin.DebugLog("Startup: complete checked=" + _startupChecked +
                " removed=" + _startupRemoved + ".");
        }

        // Holds one saved identity and its validation progress.
        private sealed class StartupEntry
        {
            internal readonly Guid Guid;
            internal readonly Vector3 Position;
            internal readonly bool Known;
            internal float FirstSync = -1f;
            internal int Syncs;

            // Remembers the last known area without persisting a ZDOID.
            internal StartupEntry(Guid guid, Vector3 position, bool known)
            {
                Guid = guid;
                Position = position;
                Known = known;
            }
        }
    }
}
