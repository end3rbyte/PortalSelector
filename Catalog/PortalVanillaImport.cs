namespace Landoria.PortalSelector
{
    // Imports a new portal before its GUID enters the character catalog.
    internal static class PortalVanillaImport
    {
        internal static bool TryImport(ZDO portal, PortalRecord source)
        {
            if (portal == null || source == null)
            {
                return false;
            }
            ZDOID targetId = portal.GetConnectionZDOID(
                ZDOExtraData.ConnectionType.Portal);
            PortalRecord partner = null;
            ZDO target = null;
            if (targetId != ZDOID.None &&
                PortalFields.Read(portal).DestinationGuid ==
                    System.Guid.Empty)
            {
                target = PortalConnection.GetTarget(targetId);
                if (target != null)
                {
                    PortalFields.EnsureIdentity(targetId);
                }
                partner = PortalDirectory.CreateRecord(target);
                if (partner == null)
                {
                    PortalConnection.Request(targetId);
                }
            }
            if (!PlayerPortalNames.ImportFromShared(portal, source, partner))
            {
                return false;
            }
            bool routeImported = false;
            if (partner != null && source.Guid != partner.Guid &&
                Named(portal) && Named(target))
            {
                if (!PortalFields.Claim(portal.m_uid) ||
                    !PortalFields.SetDestination(portal.m_uid, partner.Guid,
                        partner.Position))
                {
                    return false;
                }
                routeImported = true;
            }
            Plugin.DebugLog("Vanilla import: zdo=" + portal.m_uid +
                " partner=" + (target == null ? "none" :
                    target.m_uid.ToString()) +
                " routeImported=" + routeImported + ".");
            return true;
        }

        private static bool Named(ZDO portal)
        {
            return portal != null && (!string.IsNullOrWhiteSpace(
                PortalFields.Read(portal).Name) ||
                !string.IsNullOrWhiteSpace(portal.GetString(ZDOVars.s_tag)));
        }
    }
}
