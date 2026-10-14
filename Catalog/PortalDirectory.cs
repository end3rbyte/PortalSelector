using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Keeps a per-character catalog indexed by Valheim's portal ZDOIDs.
    internal static partial class PortalDirectory
    {
        private const string CatalogPrefix = "Landoria.PortalSelector.CatalogZdoV4.";
        private static readonly Dictionary<ZDOID, PortalRecord> Records =
            new Dictionary<ZDOID, PortalRecord>();
        private static Player _player;
        private static string _catalogKey;
        private static float _nextScan;

        // Scans portals currently known to this client every few seconds.
        internal static void Update()
        {
            if (Player.m_localPlayer == null || ZNet.instance == null ||
                ZDOMan.instance == null)
            {
                return;
            }
            LoadSession();
            if (Time.unscaledTime >= _nextScan)
            {
                Scan();
            }
        }

        // Scans immediately before a player interacts with a portal.
        internal static void ScanNow()
        {
            _nextScan = 0f;
            Update();
        }

        // Imports newly seen portals before adding them to the catalog.
        private static void Scan()
        {
            _nextScan = Time.unscaledTime + 5f;
            bool changed = false;
            List<ZDO> portals = new List<ZDO>(ZDOMan.instance.GetPortalList());
            changed |= ScanObjects(portals);
            changed |= RefreshBiomes();
            if (changed)
            {
                PlayerPortalCatalog.Save(_player, _catalogKey, Records);
            }
        }

        // Adds loaded portals and their already loaded vanilla partners.
        private static bool ScanObjects(List<ZDO> portals)
        {
            bool changed = false;
            var observed = new HashSet<ZDOID>();
            for (int i = 0; i < portals.Count; i++)
            {
                ZDO portal = portals[i];
                if (!observed.Add(portal.m_uid))
                {
                    continue;
                }
                PortalFields.EnsureCreated(portal.m_uid);
                PortalRecord record = CreateRecord(portal);
                if (record != null && (Records.ContainsKey(portal.m_uid) ||
                    PortalVanillaImport.TryImport(portal, record)))
                {
                    changed |= UpdateRecord(portal);
                }
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

        // Corrects saved biomes with the current world's biome map.
        private static bool RefreshBiomes()
        {
            if (WorldGenerator.instance == null)
            {
                return false;
            }
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

        // Measures the nearest portal other than this pair's endpoints.
        internal static float NearestOtherPortalDistanceSquared(PortalRecord record,
            PortalRecord partner)
        {
            float nearest = float.PositiveInfinity;
            foreach (PortalRecord other in Records.Values)
            {
                if (other.Id == record.Id || partner != null &&
                    other.Id == partner.Id)
                {
                    continue;
                }
                float dx = record.Position.x - other.Position.x;
                float dz = record.Position.z - other.Position.z;
                nearest = Math.Min(nearest, dx * dx + dz * dz);
            }
            return nearest;
        }

        // Finds a remembered portal by its world object ID.
        internal static PortalRecord Find(ZDOID id)
        {
            LoadSession();
            Records.TryGetValue(id, out PortalRecord record);
            return record;
        }

        // Uses a received destination ZDO and catalogs it after import.
        internal static bool Remember(ZDO portal)
        {
            LoadSession();
            if (_player == null || portal == null)
            {
                return false;
            }
            PortalFields.EnsureCreated(portal.m_uid);
            PortalRecord details = CreateRecord(portal);
            if (details == null)
            {
                return false;
            }
            if ((Records.ContainsKey(portal.m_uid) ||
                PortalVanillaImport.TryImport(portal, details)) &&
                UpdateRecord(portal))
            {
                PlayerPortalCatalog.Save(_player, _catalogKey, Records);
            }
            return true;
        }

        // Exposes saved portals for name conflict checks.
        internal static List<PortalRecord> KnownRecords()
        {
            LoadSession();
            return new List<PortalRecord>(Records.Values);
        }

        // Caches the latest name read from or written to the portal ZDO.
        internal static void CacheName(ZDOID id, string name)
        {
            LoadSession();
            if (_player == null || string.IsNullOrWhiteSpace(name) ||
                !Records.TryGetValue(id, out PortalRecord record) ||
                record.LastKnownName == name)
            {
                return;
            }
            record.LastKnownName = name;
            PlayerPortalCatalog.Save(_player, _catalogKey, Records);
        }

        // Loads this character's catalog for the current world.
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
            _nextScan = 0f;
            Records.Clear();
            PlayerDestinations.Reset();
            PortalConnection.Reset();
            PlayerPortalCatalog.Load(player, key, Records);
            Plugin.DebugLog("Catalog: world session ready, records=" +
                Records.Count + ".");
        }

        // Updates one portal from its current world object.
        private static bool UpdateRecord(ZDO portal)
        {
            PortalRecord record = CreateRecord(portal);
            return record != null && StoreRecord(record);
        }

        // Creates display metadata without cataloging the portal.
        internal static PortalRecord CreateRecord(ZDO portal)
        {
            if (portal == null)
            {
                return null;
            }
            if (WorldGenerator.instance == null)
            {
                return null;
            }
            Vector3 position = portal.GetPosition();
            return new PortalRecord
            {
                Id = portal.m_uid,
                LastKnownName = PortalFields.Read(portal).Name,
                Position = position,
                Biome = ResolveBiome(position)
            };
        }

        // Refreshes an existing ZDOID record or adds a newly seen portal.
        private static bool StoreRecord(PortalRecord record)
        {
            bool existed = Records.TryGetValue(record.Id, out PortalRecord old);
            if (existed && string.IsNullOrWhiteSpace(record.LastKnownName))
            {
                record.LastKnownName = old.LastKnownName;
            }
            bool changed = !existed || old.LastKnownName != record.LastKnownName ||
                old.Position != record.Position ||
                old.Biome != record.Biome;
            Records[record.Id] = record;
            if (!existed)
            {
                Plugin.DebugLog("Catalog: discovered zdo=" + record.Id + ".");
            }
            return changed;
        }

        // Uses loaded terrain when possible and the world map elsewhere.
        private static Heightmap.Biome ResolveBiome(Vector3 position)
        {
            Heightmap.Biome loaded = Heightmap.FindBiome(position);
            return loaded != Heightmap.Biome.None ? loaded :
                WorldGenerator.instance.GetBiomeSector(position).Biome;
        }
    }
}
