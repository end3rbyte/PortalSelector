using System.Collections.Generic;

namespace Landoria.PortalSelector
{
    internal static partial class PortalDirectory
    {
        // Runs once after startup validation, while keeping every linked portal.
        private static bool CleanupInactiveUnlinked()
        {
            if (_player == null || _player != Player.m_localPlayer ||
                ZNet.instance == null ||
                _catalogKey != CatalogPrefix + ZNet.instance.GetWorldUID())
            {
                return false;
            }
            var knownGuids = new HashSet<string>();
            foreach (PortalRecord record in Records.Values)
            {
                knownGuids.Add(record.Guid);
            }
            HashSet<string> linked = PlayerDestinations.LinkedGuids(knownGuids);
            var obsolete = new List<PortalRecord>();
            foreach (PortalRecord record in Records.Values)
            {
                if (!record.Active && !linked.Contains(record.Guid))
                {
                    obsolete.Add(record);
                }
            }
            foreach (PortalRecord record in obsolete)
            {
                Records.Remove(PortalIdentity.CatalogKey(record));
                PlayerPortalNames.Forget(record);
                PlayerDestinations.Forget(record.Guid);
                PortalVanillaImport.Forget(record.Guid);
                Plugin.DebugLog("Catalog: purged inactive unlinked guid=" +
                    record.Guid + " zdo=" + record.Id + ".");
            }
            if (obsolete.Count > 0)
            {
                PlayerPortalCatalog.Save(_player, _catalogKey, Records);
            }
            Plugin.DebugLog("Catalog: startup cleanup removed " +
                obsolete.Count + " inactive unlinked portals.");
            return true;
        }
    }
}
