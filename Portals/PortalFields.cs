using System;
using System.Globalization;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Reads and writes PortalSelector fields on vanilla portal ZDOs.
    internal static class PortalFields
    {
        internal const string NameKey = "Landoria.PortalSelector.Name";
        internal const string GuidKey = "Landoria.PortalSelector.Guid";
        internal const string DestinationGuidKey =
            "Landoria.PortalSelector.DestinationGuid";
        internal const string DestinationPositionKey =
            "Landoria.PortalSelector.DestinationPosition";
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
            if (portal == null)
            {
                return null;
            }
            Guid.TryParse(portal.GetString(GuidKey), out Guid guid);
            Guid.TryParse(portal.GetString(DestinationGuidKey),
                out Guid destination);
            return new PortalZdoData
            {
                Name = portal.GetString(NameKey),
                Guid = guid,
                Position = portal.GetPosition(),
                DestinationGuid = destination,
                DestinationPosition = portal.GetVec3(DestinationPositionKey,
                    Vector3.zero),
                CreatedAtUtc = createdAt
            };
        }

        // Gives a nearby portal a stable identity when first discovered.
        internal static bool EnsureIdentity(ZDOID id)
        {
            ZDO portal = PortalConnection.GetTarget(id);
            if (portal == null)
            {
                return false;
            }
            if (Read(portal).Guid != Guid.Empty)
            {
                return true;
            }
            if (!Claim(id))
            {
                return false;
            }
            portal.Set(GuidKey, Guid.NewGuid().ToString("D"));
            SetCreatedAt(portal);
            ZDOMan.instance.SetDirtyPortals();
            return true;
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

        // Stores the destination identity and its last known map position.
        internal static bool SetDestination(ZDOID id, Guid destination,
            Vector3 position)
        {
            ZDO portal = PortalConnection.GetTarget(id);
            if (portal == null || !portal.IsOwner() || !SetCreatedAt(portal))
            {
                return false;
            }
            portal.Set(DestinationGuidKey, destination == Guid.Empty ? "" :
                destination.ToString("D"));
            portal.Set(DestinationPositionKey, position);
            ZDOMan.instance.SetDirtyPortals();
            return true;
        }

    }
}
