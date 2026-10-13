namespace Landoria.PortalSelector
{
    internal static partial class PortalDirectory
    {
        // Retires a known portal as soon as Valheim reports its destruction.
        internal static void OnDestroyedZDO(ZDOMan manager, ZDOID id)
        {
            Player player = Player.m_localPlayer;
            ZNet network = ZNet.instance;
            if (player == null || network == null || manager != ZDOMan.instance ||
                _player != player ||
                _catalogKey != CatalogPrefix + network.GetWorldUID())
            {
                return;
            }
            PortalRecord record = FindById(id);
            if (record == null || !record.Active)
            {
                return;
            }
            ZDO portal = manager.GetZDO(id);
            if (portal != null && (Game.instance == null ||
                !Game.instance.PortalPrefabHash.Contains(portal.GetPrefab()) ||
                portal.GetString(PortalGuidKey) != record.Guid))
            {
                return;
            }
            if (MarkInactive(id))
            {
                Plugin.DebugLog("Portal: destruction received guid=" +
                    record.Guid + " zdo=" + id + ".");
            }
        }
    }
}
