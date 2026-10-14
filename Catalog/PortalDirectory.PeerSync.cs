using System;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Applies live portal announcements to this character's current catalog.
    internal static partial class PortalDirectory
    {
        // Saves a portal discovered or renamed by another connected player.
        internal static void OnPeerPortal(ZDOID id, Guid guid, string name,
            Vector3 position)
        {
            LoadSession();
            if (_player == null || WorldGenerator.instance == null ||
                RemovedIds.Contains(guid))
            {
                return;
            }
            if (id != ZDOID.None)
            {
                SeenIds[id] = guid;
            }
            var record = new PortalRecord
            {
                Guid = guid,
                LastKnownName = name,
                Position = position,
                Biome = ResolveBiome(position)
            };
            if (!StoreRecord(record))
            {
                return;
            }
            PlayerPortalCatalog.Save(_player, _catalogKey, Records);
            PortalDialog.OnPortalUpdated(Records[guid]);
            Plugin.DebugLog("Catalog: peer portal guid=" + guid + ".");
        }
    }
}
