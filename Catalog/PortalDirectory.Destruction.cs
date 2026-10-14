using System;
using System.Collections.Generic;

namespace Landoria.PortalSelector
{
    internal static partial class PortalDirectory
    {
        // Removes a cataloged portal as soon as Valheim reports its destruction.
        internal static void OnDestroyedZDO(ZDOMan manager, ZDOID id)
        {
            Player player = Player.m_localPlayer;
            ZNet network = ZNet.instance;
            if (player == null || network == null || manager != ZDOMan.instance)
            {
                return;
            }
            LoadSession();
            PortalRecord record = Find(id);
            Guid guid = record?.Guid ?? PortalFields.Read(id)?.Guid ?? Guid.Empty;
            if (guid == Guid.Empty && SeenIds.TryGetValue(id, out Guid seen))
            {
                guid = seen;
            }
            SeenIds.Remove(id);
            if (guid == Guid.Empty || !RemovePortal(guid))
            {
                return;
            }
            PortalPeerSync.AnnounceRemoval(guid);
            Plugin.DebugLog("Catalog: destroyed portal removed zdo=" + id + ".");
        }

        // Applies a deletion reported by another modded player without echoing it.
        internal static void OnPeerRemoval(Guid guid)
        {
            LoadSession();
            if (_player != null && RemovePortal(guid))
            {
                Plugin.DebugLog("Catalog: peer removed guid=" + guid + ".");
            }
        }

        // Marks a destroyed identity and clears local routes and display state.
        private static bool RemovePortal(Guid guid)
        {
            if (guid == Guid.Empty || !RemovedIds.Add(guid))
            {
                return false;
            }
            PlayerDestinations.ForgetPortal(guid);
            var forgottenIds = new List<ZDOID>();
            foreach (KeyValuePair<ZDOID, Guid> seen in SeenIds)
            {
                if (seen.Value == guid)
                {
                    forgottenIds.Add(seen.Key);
                }
            }
            foreach (ZDOID id in forgottenIds)
            {
                SeenIds.Remove(id);
            }
            if (Records.Remove(guid))
            {
                PlayerPortalCatalog.Save(_player, _catalogKey, Records);
            }
            PortalDialog.OnPortalRemoved(guid);
            return true;
        }
    }
}
