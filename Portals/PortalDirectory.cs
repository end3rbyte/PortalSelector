using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Keeps a per-character list of portals already observed by this client.
    internal static partial class PortalDirectory
    {
        private const string CatalogPrefix = "Landoria.PortalSelector.Catalog.";
        private static readonly Dictionary<string, PortalRecord> Records =
            new Dictionary<string, PortalRecord>();
        private static readonly HashSet<ZDOID> Confirmed = new HashSet<ZDOID>();
        private static readonly Queue<ZDOID> PendingValidation = new Queue<ZDOID>();
        private static Player _player;
        private static Player _clearedPlayer;
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
            Confirmed.Clear();
            PendingValidation.Clear();
            _validating = false;
            _nextScan = 0f;
            _beginRequested = true;
        }

        // Scans portals currently known to the client every few seconds.
        internal static void Update()
        {
            if (_clearedPlayer != null)
            {
                if (Player.m_localPlayer == null ||
                    ReferenceEquals(Player.m_localPlayer, _clearedPlayer))
                {
                    return;
                }
                _clearedPlayer = null;
            }
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

        // Pauses rediscovery until this character leaves the current session.
        internal static void PauseAfterClear(Player player)
        {
            LoadSession();
            _clearedPlayer = player;
            Records.Clear();
            Confirmed.Clear();
            PendingValidation.Clear();
            _validating = false;
            _beginRequested = false;
            PortalConnection.Reset();
        }

        // Reports whether this character has cleared portal data this session.
        internal static bool IsPaused => _clearedPlayer != null &&
            ReferenceEquals(Player.m_localPlayer, _clearedPlayer);

        // Records portals received in the client's current world state.
        private static void Scan()
        {
            _nextScan = Time.unscaledTime + 5f;
            LoadSession();
            bool changed = false;
            List<ZDO> portals = new List<ZDO>(ZDOMan.instance.GetPortalList());
            changed |= ScanObjects(portals);
            foreach (ZDO portal in portals)
            {
                PlayerDestinations.InitializeFromShared(portal);
                InitializeName(portal);
            }
            changed |= RefreshBiomes();
            PlayerPortalNames.EnsureUniqueNames(Records.Values);
            if (changed)
            {
                PlayerPortalCatalog.Save(_player, _catalogKey, Records);
            }
        }

        // Adds native portal objects and requests linked objects not yet present.
        private static bool ScanObjects(List<ZDO> portals)
        {
            bool changed = false;
            HashSet<ZDOID> observed = new HashSet<ZDOID>();
            for (int i = 0; i < portals.Count; i++)
            {
                ZDO portal = portals[i];
                if (!observed.Add(portal.m_uid))
                {
                    continue;
                }
                changed |= UpdateRecord(portal);
                ZDOID targetId = portal.GetConnectionZDOID(
                    ZDOExtraData.ConnectionType.Portal);
                if (targetId == ZDOID.None)
                {
                    continue;
                }
                ZDO target = PortalConnection.GetTarget(targetId);
                if (target != null)
                {
                    portals.Add(target);
                }
                else
                {
                    QueueValidation(targetId);
                }
            }
            return changed;
        }

        // Corrects saved biomes with the current world biome map.
        private static bool RefreshBiomes()
        {
            bool changed = false;
            foreach (PortalRecord record in Records.Values)
            {
                Heightmap.Biome biome = ResolveBiome(record.Position);
                if (record.Biome == biome)
                {
                    continue;
                }
                record.Biome = biome;
                changed = true;
            }
            return changed;
        }

        // Copies a tag now and adds a pair suffix when its partner is known.
        private static void InitializeName(ZDO portal)
        {
            PortalRecord record = Find(portal.m_uid);
            if (record == null)
            {
                return;
            }
            ZDOID targetId = portal.GetConnectionZDOID(
                ZDOExtraData.ConnectionType.Portal);
            PortalRecord partner = targetId == ZDOID.None ? null : Find(targetId);
            PlayerPortalNames.InitializeFromShared(record, partner);
        }

        // Measures the nearest portal other than this pair's other endpoint.
        internal static float NearestOtherPortalDistanceSquared(PortalRecord record,
            PortalRecord partner)
        {
            float nearest = float.PositiveInfinity;
            foreach (PortalRecord other in Records.Values)
            {
                if (PortalIdentity.Key(other) == PortalIdentity.Key(record) ||
                    partner != null &&
                    PortalIdentity.Key(other) == PortalIdentity.Key(partner))
                {
                    continue;
                }
                float dx = record.Position.x - other.Position.x;
                float dz = record.Position.z - other.Position.z;
                nearest = Math.Min(nearest, dx * dx + dz * dz);
            }
            return nearest;
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
                if (record.Id == id)
                {
                    return record;
                }
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
            return changed;
        }

        // Uses the loaded terrain when possible and the world's biome map elsewhere.
        private static Heightmap.Biome ResolveBiome(Vector3 position)
        {
            Heightmap.Biome loaded = Heightmap.FindBiome(position);
            if (loaded != Heightmap.Biome.None)
            {
                return loaded;
            }
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
            if (stale == null)
            {
                return false;
            }
            Records.Remove(stale);
            return true;
        }

    }
}
