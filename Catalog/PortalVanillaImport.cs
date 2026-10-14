namespace Landoria.PortalSelector
{
    // Imports a new portal before its ZDOID enters the character catalog.
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
            if (targetId != ZDOID.None &&
                (PortalFields.Read(source.Id)?.Destination ?? ZDOID.None) ==
                    ZDOID.None)
            {
                ZDO target = PortalConnection.GetTarget(targetId);
                partner = PortalDirectory.CreateRecord(target);
                if (partner == null)
                {
                    PortalConnection.Request(targetId);
                    return false;
                }
            }
            if (!PlayerPortalNames.ImportFromShared(source, partner))
            {
                return false;
            }
            bool routeImported = false;
            if (partner != null && source.Id != partner.Id &&
                Named(portal) &&
                Named(PortalConnection.GetTarget(partner.Id)))
            {
                if (!PortalFields.Claim(source.Id) ||
                    !PortalFields.SetDestination(source.Id, partner.Id))
                {
                    return false;
                }
                routeImported = true;
            }
            Plugin.DebugLog("Vanilla import: zdo=" + source.Id +
                " partner=" + (partner?.Id.ToString() ?? "none") +
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
