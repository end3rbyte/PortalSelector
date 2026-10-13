using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Keeps a per-character list of portals already observed by this client.
    internal static partial class PortalDirectory
    {
        private const string CatalogPrefix = "Landoria.PortalSelector.CatalogGuidV1.";
        internal const string PortalGuidKey = "Landoria.PortalSelector.Guid";
        private static readonly Dictionary<string, PortalRecord> Records =
            new Dictionary<string, PortalRecord>();
        private static Player _player;
        private static string _catalogKey;
        private static float _nextScan;

        // Scans portals currently known to the client every few seconds.
        internal static void Update()
        {
            if (Player.m_localPlayer == null || ZNet.instance == null ||
                ZDOMan.instance == null)
            {
                return;
            }
            if (Time.unscaledTime >= _nextScan)
            {
                Scan();
            }
            UpdateValidation();
        }

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
                InitializeName(portal);
            }
            changed |= RefreshBiomes();
            PlayerPortalNames.EnsureUniqueNames(Records.Values);
            if (changed)
            {
                PlayerPortalCatalog.Save(_player, _catalogKey, Records);
            }
        }

        // Adds loaded portal objects and their loaded vanilla link partners.
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
            }
            return changed;
        }

        // Corrects saved biomes with the current world biome map.
        private static bool RefreshBiomes()
        {
            bool changed = false;
            foreach (PortalRecord record in Records.Values)
            {
                Heightmap.Biome biome = ResolveBiome(record.Metadata.Position);
                if (record.Metadata.Biome == biome)
                {
                    continue;
                }
                record.Metadata.Biome = biome;
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
                float dx = record.Metadata.Position.x - other.Metadata.Position.x;
                float dz = record.Metadata.Position.z - other.Metadata.Position.z;
                nearest = Math.Min(nearest, dx * dx + dz * dz);
            }
            return nearest;
        }

        // Collapses records with the same portal GUID.
        private static List<PortalRecord> UniqueRecords(ZDOID origin,
            List<PortalRecord> candidates)
        {
            List<PortalRecord> result = new List<PortalRecord>();
            foreach (PortalRecord candidate in candidates)
            {
                int index = result.FindIndex(record => SamePortal(record, candidate));
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

        // Compares persistent portal identities.
        private static bool SamePortal(PortalRecord first, PortalRecord second)
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

        // Resolves a world object ID within the saved identity catalog.
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

        // Resolves a portal GUID to its current world object ID.
        internal static PortalRecord FindByGuid(string guid)
        {
            LoadSession();
            Records.TryGetValue("g:" + guid, out PortalRecord record);
            return record;
        }

        // Exposes saved identities for personal-name conflict checks.
        internal static List<PortalRecord> KnownRecords()
        {
            LoadSession();
            return new List<PortalRecord>(Records.Values);
        }

        // Retains an unanswered portal while hiding it from active destinations.
        internal static bool MarkInactive(ZDOID id)
        {
            LoadSession();
            PortalRecord record = FindById(id);
            if (record == null || !record.Active)
            {
                return false;
            }
            record.Active = false;
            PlayerPortalCatalog.Save(_player, _catalogKey, Records);
            PortalDialog.OnPortalInactive(id);
            Plugin.DebugLog("Catalog: inactive guid=" + record.Guid +
                " zdo=" + record.Id + ".");
            return true;
        }

        // Reuses an inactive identity when a rebuilt portal takes its name.
        internal static bool RebindInactive(ZDOID inactiveId, ZDOID currentId)
        {
            LoadSession();
            PortalRecord inactive = FindById(inactiveId);
            PortalRecord current = FindById(currentId);
            ZDO portal = PortalConnection.GetTarget(currentId);
            if (inactive == null || inactive.Active || current == null ||
                !current.Active || portal == null ||
                portal.GetString(PortalGuidKey) != current.Guid)
            {
                return false;
            }
            string guid = string.IsNullOrEmpty(inactive.Guid) ?
                current.Guid : inactive.Guid;
            if (!string.IsNullOrEmpty(guid))
            {
                portal.Set(PortalGuidKey, guid);
            }
            Records.Remove(PortalIdentity.CatalogKey(inactive));
            Records.Remove(PortalIdentity.CatalogKey(current));
            current.Guid = guid;
            Records[PortalIdentity.CatalogKey(current)] = current;
            PlayerPortalCatalog.Save(_player, _catalogKey, Records);
            Plugin.DebugLog("Catalog: rebound guid=" + guid +
                " oldZdo=" + inactiveId + " newZdo=" + currentId + ".");
            return true;
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
            PlayerDestinations.Reset();
            PortalConnection.Reset();
            if (PlayerPortalCatalog.Load(_player, _catalogKey, Records))
            {
                PlayerPortalCatalog.Save(_player, _catalogKey, Records);
            }
            Plugin.DebugLog("Catalog: world session ready, records=" +
                Records.Count + ".");
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
            string guid = portal.GetString(PortalGuidKey);
            if (string.IsNullOrWhiteSpace(guid) && portal.IsOwner())
            {
                guid = Guid.NewGuid().ToString("N");
                portal.Set(PortalGuidKey, guid);
                Plugin.DebugLog("Portal: assigned guid=" + guid +
                    " zdo=" + portal.m_uid + ".");
            }
            if (string.IsNullOrWhiteSpace(guid))
            {
                return false;
            }
            PortalRecord record = new PortalRecord
            {
                Id = portal.m_uid, Guid = guid,
                Tag = portal.GetString(ZDOVars.s_tag),
                Metadata = new PortalMetadata
                {
                    Position = portal.GetPosition(),
                    Biome = ResolveBiome(portal.GetPosition())
                },
                Rotation = portal.GetRotation(),
                AllowAllItems = component.m_allowAllItems,
                ExitDistance = component.m_exitDistance
            };
            return StoreRecord(record);
        }

        // Refreshes an existing GUID record or adds a newly received portal.
        private static bool StoreRecord(PortalRecord record)
        {
            string key = PortalIdentity.CatalogKey(record);
            bool collided = PreserveIdCollision(record.Id, PortalIdentity.Key(record));
            PortalRecord prior = FindById(record.Id);
            bool replaced = prior != null &&
                PortalIdentity.CatalogKey(prior) != key;
            if (replaced)
            {
                Records.Remove(PortalIdentity.CatalogKey(prior));
            }
            bool existed = Records.TryGetValue(key, out PortalRecord old);
            bool changed = collided || replaced || !existed ||
                old.Id != record.Id || old.Guid != record.Guid ||
                !old.Active ||
                old.Tag != record.Tag ||
                old.Metadata.Position != record.Metadata.Position ||
                old.Rotation != record.Rotation ||
                old.Metadata.Biome != record.Metadata.Biome;
            Records[key] = record;
            LogRecordChange(record, old, existed);
            return changed;
        }

        // Reports only discoveries, reactivations, and changed world object IDs.
        private static void LogRecordChange(PortalRecord record,
            PortalRecord old, bool existed)
        {
            if (!existed)
            {
                Plugin.DebugLog("Catalog: discovered guid=" + record.Guid +
                    " zdo=" + record.Id + ".");
            }
            else if (!old.Active)
            {
                Plugin.DebugLog("Catalog: reactivated guid=" + record.Guid +
                    " zdo=" + record.Id + ".");
            }
            else if (old.Id != record.Id)
            {
                Plugin.DebugLog("Catalog: zdo changed guid=" + record.Guid +
                    " oldZdo=" + old.Id + " newZdo=" + record.Id + ".");
            }
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

        // Gives a remembered portal a spare ID when Valheim reuses its old ID.
        private static bool PreserveIdCollision(ZDOID id, string guid)
        {
            PortalRecord stale = null;
            foreach (KeyValuePair<string, PortalRecord> entry in Records)
            {
                if (entry.Value.Id == id &&
                    PortalIdentity.Key(entry.Value) != guid)
                {
                    stale = entry.Value;
                    break;
                }
            }
            if (stale == null)
            {
                return false;
            }
            uint spare = uint.MaxValue;
            while (FindById(new ZDOID(id.UserID, spare)) != null)
            {
                spare--;
            }
            stale.Id = new ZDOID(id.UserID, spare);
            return true;
        }

    }
}
