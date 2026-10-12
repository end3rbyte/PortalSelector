using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Keeps a per-character list of portals already observed by this client.
    internal static class PortalDirectory
    {
        private const string CatalogPrefix = "Landoria.PortalSelector.Catalog.";
        private static readonly Dictionary<string, PortalRecord> Records =
            new Dictionary<string, PortalRecord>();
        private static readonly HashSet<ZDOID> Confirmed = new HashSet<ZDOID>();
        private static readonly Queue<ZDOID> PendingValidation = new Queue<ZDOID>();
        private static Player _player;
        private static string _catalogKey;
        private static float _nextScan;
        private static float _nextValidationRequest;
        private static float _validationDeadline;
        private static float _nextConfirmationCheck;
        private static bool _beginRequested;
        private static bool _validating;

        // Schedules a native portal check after the character's first spawn.
        internal static void BeginValidation()
        {
            PortalLog.Info("validation", "First spawn; checking saved portals.");
            Confirmed.Clear();
            PendingValidation.Clear();
            _validating = false;
            _nextScan = 0f;
            _beginRequested = true;
        }

        // Scans portals currently known to the client every few seconds.
        internal static void Update()
        {
            if (Player.m_localPlayer == null || ZNet.instance == null ||
                ZDOMan.instance == null)
            {
                _validating = false;
                return;
            }
            if (Time.unscaledTime >= _nextScan)
            {
                Scan();
            }
            if (_beginRequested)
            {
                StartValidation();
            }
            DispatchValidation();
            RefreshConfirmed();
        }

        // Records portals received in the client's current world state.
        private static void Scan()
        {
            _nextScan = Time.unscaledTime + 5f;
            LoadSession();
            bool changed = false;
            List<ZDO> portals = new List<ZDO>(ZDOMan.instance.GetPortalList());
            PortalLog.Detail("scan", "Native portal list contains " +
                portals.Count + " objects; character catalog has " +
                Records.Count + ".");
            HashSet<ZDOID> observed = new HashSet<ZDOID>();
            for (int i = 0; i < portals.Count; i++)
            {
                ZDO portal = portals[i];
                if (!observed.Add(portal.m_uid)) { continue; }
                changed |= UpdateRecord(portal);
                ZDOID targetId = portal.GetConnectionZDOID(
                    ZDOExtraData.ConnectionType.Portal);
                if (targetId == ZDOID.None) { continue; }
                ZDO target = PortalConnection.GetTarget(targetId);
                if (target != null) { portals.Add(target); }
                else { QueueValidation(targetId); }
            }
            foreach (ZDO portal in portals)
            {
                PlayerDestinations.InitializeFromShared(portal);
                InitializeName(portal);
            }
            foreach (PortalRecord record in Records.Values)
            {
                Heightmap.Biome biome = ResolveBiome(record.Position);
                if (record.Biome == biome) { continue; }
                PortalLog.Detail("biome", "Corrected " +
                    PortalLog.Describe(record) + " from " + record.Biome +
                    " to " + biome + ".");
                record.Biome = biome;
                changed = true;
            }
            PlayerPortalNames.EnsureUniqueNames(Records.Values);
            if (changed) { PlayerPortalCatalog.Save(_player, _catalogKey, Records); }
        }

        // Copies a tag now and adds a pair suffix when its partner is known.
        private static void InitializeName(ZDO portal)
        {
            PortalRecord record = Find(portal.m_uid);
            if (record == null) { return; }
            ZDOID targetId = portal.GetConnectionZDOID(
                ZDOExtraData.ConnectionType.Portal);
            PortalRecord partner = targetId == ZDOID.None ? null : Find(targetId);
            PlayerPortalNames.InitializeFromShared(record, partner);
        }

        // Schedules linked portals through the existing 250 ms request queue.
        private static void QueueValidation(ZDOID id)
        {
            if (!PortalConnection.TimedOut(id) &&
                !PendingValidation.Contains(id))
            {
                PendingValidation.Enqueue(id);
                _validating = true;
                _validationDeadline = Time.unscaledTime + 10f;
                PortalLog.Detail("validation", "Queued connected/known ZDO " +
                    id + "; pending=" + PendingValidation.Count + ".");
            }
        }

        // Opens immediately with destinations confirmed in this session.
        internal static void Request(ZDOID origin)
        {
            _nextScan = 0f;
            Update();
            List<PortalRecord> available = new List<PortalRecord>();
            foreach (PortalRecord record in Records.Values)
            {
                if (record.Id == origin || Confirmed.Contains(record.Id))
                {
                    available.Add(record);
                }
            }
            if (available.Count == 1 && _validating)
            {
                PortalLog.Detail("dialog", "Waiting for destinations from " +
                    origin + "; pending=" + PendingValidation.Count + ".");
                Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                    "Checking destinations in the background. Try again shortly.");
                return;
            }
            PortalDialog.Open(origin, UniqueRecords(origin, available));
            PortalLog.Detail("dialog", "Requested destination dialog from " +
                origin + "; confirmed choices=" + available.Count + ".");
        }

        // Requests the saved portal IDs from the world host once per spawn.
        private static void StartValidation()
        {
            _beginRequested = false;
            _validationDeadline = Time.unscaledTime + 10f;
            _nextValidationRequest = Time.unscaledTime;
            _validating = true;
            foreach (PortalRecord record in Records.Values)
            {
                if (!Confirmed.Contains(record.Id))
                {
                    QueueValidation(record.Id);
                }
            }
            _nextConfirmationCheck = 0f;
            PortalLog.Detail("validation", "Started; catalog=" +
                Records.Count + ", pending=" + PendingValidation.Count + ".");
        }

        // Sends at most one catalog request every 250 milliseconds.
        private static void DispatchValidation()
        {
            if (!_validating || PendingValidation.Count == 0 ||
                Time.unscaledTime < _nextValidationRequest)
            {
                return;
            }
            ZDOID id = PendingValidation.Dequeue();
            _nextValidationRequest = Time.unscaledTime + 0.25f;
            if (!Confirmed.Contains(id) && PortalConnection.GetTarget(id) == null)
            {
                PortalLog.Detail("validation", "Dispatching ZDO " + id +
                    "; remaining=" + PendingValidation.Count + ".");
                PortalConnection.Request(id, FindById(id) != null);
                _validationDeadline = Time.unscaledTime + 10f;
            }
        }

        // Adds native portal objects received during the background check.
        private static void RefreshConfirmed()
        {
            if (Time.unscaledTime < _nextConfirmationCheck)
            {
                return;
            }
            _nextConfirmationCheck = Time.unscaledTime + (_validating ? 0.5f : 5f);
            List<ZDOID> expired = new List<ZDOID>();
            foreach (PortalRecord record in Records.Values)
            {
                if (PortalConnection.GetTarget(record.Id) != null)
                {
                    if (Confirmed.Add(record.Id))
                    {
                        PortalLog.Detail("validation", "Confirmed " +
                            PortalLog.Describe(record));
                    }
                }
                else if (PortalConnection.TimedOut(record.Id))
                {
                    expired.Add(record.Id);
                }
            }
            foreach (ZDOID id in expired)
            {
                Forget(id, false);
            }
            if (_validating && PendingValidation.Count == 0 &&
                Time.unscaledTime >= _validationDeadline)
            {
                _validating = false;
                PortalLog.Detail("validation", "Finished background checks; " +
                    Confirmed.Count + " portals confirmed.");
            }
        }

        // Collapses records with the same exact portal identity.
        private static List<PortalRecord> UniqueRecords(ZDOID origin,
            List<PortalRecord> candidates)
        {
            List<PortalRecord> result = new List<PortalRecord>();
            foreach (PortalRecord candidate in candidates)
            {
                int index = result.FindIndex(record => SamePlace(record, candidate));
                if (index < 0)
                {
                    result.Add(candidate);
                }
                else if (candidate.Id == origin ||
                         result[index].Id != origin && Prefer(candidate, result[index]))
                {
                    result[index] = candidate;
                }
            }
            return result;
        }

        // Keeps nearby portals distinct when any stored position bit differs.
        private static bool SamePlace(PortalRecord first, PortalRecord second)
        {
            return PortalIdentity.Key(first) == PortalIdentity.Key(second);
        }

        // Prefers a live object, then the later object ID from one creator.
        private static bool Prefer(PortalRecord candidate, PortalRecord current)
        {
            bool candidateLive = PortalConnection.GetTarget(candidate.Id) != null;
            bool currentLive = PortalConnection.GetTarget(current.Id) != null;
            if (candidateLive != currentLive)
            {
                return candidateLive;
            }
            return candidate.Id.UserID == current.Id.UserID &&
                candidate.Id.ID > current.Id.ID;
        }

        // Finds a saved destination outside the loaded area.
        internal static PortalRecord Find(ZDOID id)
        {
            LoadSession();
            return FindById(id);
        }

        // Resolves the temporary network ID within the saved identity catalog.
        private static PortalRecord FindById(ZDOID id)
        {
            foreach (PortalRecord record in Records.Values)
            {
                if (record.Id == id) { return record; }
            }
            return null;
        }

        // Resolves exact and older rounded identities to a current portal ID.
        internal static PortalRecord FindByIdentity(string identity)
        {
            LoadSession();
            PortalRecord result = null;
            foreach (PortalRecord record in Records.Values)
            {
                if ((PortalIdentity.Key(record) == identity ||
                     PortalIdentity.LegacyKey(record) == identity) &&
                    (result == null || Prefer(record, result)))
                {
                    result = record;
                }
            }
            return result;
        }

        // Protects older shared keys when another portal occupies the same 10 cm cell.
        internal static bool HasOtherLegacyIdentity(PortalRecord portal)
        {
            foreach (PortalRecord record in Records.Values)
            {
                if (PortalIdentity.LegacyKey(record) ==
                    PortalIdentity.LegacyKey(portal))
                {
                    return true;
                }
            }
            return false;
        }

        // Removes a portal when Valheim confirms its destruction.
        internal static void Forget(ZDOID id, bool destroyed = true)
        {
            LoadSession();
            PortalRecord removed = FindById(id);
            Confirmed.Remove(id);
            if (removed != null && Records.Remove(PortalIdentity.Key(removed)))
            {
                PortalLog.Detail("catalog", (destroyed ? "Destroyed " :
                    "Expired network lookup for ") + PortalLog.Describe(removed));
                PlayerPortalCatalog.Save(_player, _catalogKey, Records);
            }
            if (destroyed && removed != null)
            {
                PlayerDestinations.Forget(removed);
                PlayerPortalNames.Forget(removed);
            }
        }

        // Changes the active catalog when the world or character changes.
        private static void LoadSession()
        {
            Player player = Player.m_localPlayer;
            if (ZNet.instance == null || player == null)
            {
                return;
            }
            string key = CatalogPrefix + ZNet.instance.GetWorldUID();
            if (ReferenceEquals(_player, player) && _catalogKey == key)
            {
                return;
            }
            _player = player;
            _catalogKey = key;
            Records.Clear();
            Confirmed.Clear();
            PendingValidation.Clear();
            _validating = false;
            PlayerPortalCatalog.Load(_player, _catalogKey, Records);
            PortalLog.Info("session", "Character catalog ready for " + key +
                "; known portals=" + Records.Count + ".");
        }

        // Updates one known portal from current object data.
        private static bool UpdateRecord(ZDO portal)
        {
            TeleportWorld component = ZNetScene.instance?.GetPrefab(portal.GetPrefab())?
                .GetComponent<TeleportWorld>();
            if (component == null || WorldGenerator.instance == null)
            {
                return false;
            }
            PortalRecord record = new PortalRecord
            {
                Id = portal.m_uid, Tag = portal.GetString(ZDOVars.s_tag),
                Position = portal.GetPosition(), Rotation = portal.GetRotation(),
                Biome = ResolveBiome(portal.GetPosition()),
                AllowAllItems = component.m_allowAllItems,
                ExitDistance = component.m_exitDistance
            };
            string identity = PortalIdentity.Key(record);
            bool collided = RemoveIdCollision(record.Id, identity);
            bool existed = Records.TryGetValue(identity, out PortalRecord old);
            bool changed = collided || !existed ||
                old.Id != record.Id || old.Tag != record.Tag ||
                old.Position != record.Position || old.Rotation != record.Rotation ||
                old.Biome != record.Biome;
            Records[identity] = record;
            Confirmed.Add(record.Id);
            if (changed)
            {
                PortalLog.Detail("discovery", (old == null ? "Added " :
                    "Updated ") + PortalLog.Describe(record));
            }
            return changed;
        }

        // Uses the loaded terrain when possible and the world's biome map elsewhere.
        private static Heightmap.Biome ResolveBiome(Vector3 position)
        {
            Heightmap.Biome loaded = Heightmap.FindBiome(position);
            if (loaded != Heightmap.Biome.None) { return loaded; }
            return WorldGenerator.instance.GetBiomeSector(position).Biome;
        }

        // Drops a stale position if its old network ID now belongs elsewhere.
        private static bool RemoveIdCollision(ZDOID id, string identity)
        {
            string stale = null;
            foreach (KeyValuePair<string, PortalRecord> entry in Records)
            {
                if (entry.Value.Id == id && entry.Key != identity)
                {
                    stale = entry.Key;
                    break;
                }
            }
            if (stale == null) { return false; }
            PortalLog.Detail("catalog", "Removing stale identity " + stale +
                " because ZDO " + id + " now belongs to " + identity + ".");
            Records.Remove(stale);
            return true;
        }

    }
}
