using System;
using System.Globalization;

namespace Landoria.PortalSelector
{
    // Reads and writes PortalSelector fields on vanilla portal ZDOs.
    internal static class PortalFields
    {
        internal const string NameKey = "Landoria.PortalSelector.Name";
        internal const string DestinationKey = "Landoria.PortalSelector.Destination";
        internal const string CreatedAtKey = "Landoria.PortalSelector.CreatedAtUtc";

        // Reads the mod's shared fields from a portal ZDO.
        internal static PortalZdoData Read(ZDOID id)
        {
            return Read(PortalConnection.GetTarget(id));
        }

        internal static PortalZdoData Read(ZDO portal)
        {
            DateTime? createdAt = null;
            if (portal != null && DateTime.TryParse(
                portal.GetString(CreatedAtKey), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                createdAt = parsed.ToUniversalTime();
            }
            return portal == null ? null : new PortalZdoData
            {
                Name = portal.GetString(NameKey),
                Destination = portal.GetZDOID(DestinationKey),
                CreatedAtUtc = createdAt
            };
        }

        // Sets creation time once when the portal's shared data is first written.
        internal static void EnsureCreated(ZDOID id)
        {
            ZDO portal = PortalConnection.GetTarget(id);
            if (portal == null || Read(portal).CreatedAtUtc.HasValue)
            {
                return;
            }
            if (Claim(id))
            {
                SetCreatedAt(portal);
            }
        }

        private static bool SetCreatedAt(ZDO portal)
        {
            if (portal == null || !portal.IsOwner())
            {
                return false;
            }
            if (!Read(portal).CreatedAtUtc.HasValue)
            {
                portal.Set(CreatedAtKey, DateTime.UtcNow.ToString("O",
                    CultureInfo.InvariantCulture));
                ZDOMan.instance.SetDirtyPortals();
            }
            return true;
        }

        // Claims a nearby portal before an explicit player edit.
        internal static bool Claim(ZDOID id)
        {
            ZDO portal = PortalConnection.GetTarget(id);
            if (portal == null)
            {
                return false;
            }
            if (!portal.IsOwner())
            {
                ZNetScene.instance?.FindInstance(id)?.GetComponent<ZNetView>()
                    ?.ClaimOwnership();
            }
            return portal.IsOwner();
        }

        // Writes a name through the owner of this portal ZDO.
        internal static bool SetName(ZDOID id, string name)
        {
            ZDO portal = PortalConnection.GetTarget(id);
            if (portal == null || !portal.IsOwner() || !SetCreatedAt(portal))
            {
                return false;
            }
            portal.Set(NameKey, name);
            ZDOMan.instance.SetDirtyPortals();
            return true;
        }

        // Writes a destination using Valheim's native ZDOID fields.
        internal static bool SetDestination(ZDOID id, ZDOID destination)
        {
            ZDO portal = PortalConnection.GetTarget(id);
            if (portal == null || !portal.IsOwner() || !SetCreatedAt(portal))
            {
                return false;
            }
            portal.Set(DestinationKey, destination);
            ZDOMan.instance.SetDirtyPortals();
            return true;
        }

    }
}
