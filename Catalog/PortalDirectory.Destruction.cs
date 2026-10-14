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
            PlayerDestinations.ForgetPortal(id);
            if (!Records.ContainsKey(id))
            {
                return;
            }
            Records.Remove(id);
            PlayerPortalCatalog.Save(player, _catalogKey, Records);
            PortalDialog.OnPortalRemoved(id);
            Plugin.DebugLog("Catalog: destroyed portal removed zdo=" + id + ".");
        }
    }
}
