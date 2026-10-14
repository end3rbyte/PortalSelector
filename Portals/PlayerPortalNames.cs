using System;
using System.Collections.Generic;
using System.Linq;

namespace Landoria.PortalSelector
{
    // Reads shared PortalSelector names from portal ZDOs and catalog metadata.
    internal static class PlayerPortalNames
    {
        internal const int MaxLength = 50;
        internal const string DuplicateNameMessage =
            "This name is already used by another portal.";
        // Reads the portal's shared name or its original vanilla label.
        internal static string Get(ZDOID id)
        {
            PortalRecord record = PortalDirectory.Find(id);
            ZDO portal = PortalConnection.GetTarget(id);
            string live = PortalFields.Read(portal)?.Name;
            if (!string.IsNullOrWhiteSpace(live))
            {
                if (record != null && record.LastKnownName != live)
                {
                    PortalDirectory.CacheName(id, live);
                }
                return live;
            }
            if (!string.IsNullOrWhiteSpace(record?.LastKnownName))
            {
                return record.LastKnownName;
            }
            return portal?.GetString(ZDOVars.s_tag) ?? "";
        }

        // Checks whether a portal has a nonblank name.
        internal static bool IsNamed(ZDOID id)
        {
            return !string.IsNullOrWhiteSpace(Get(id));
        }

        // Writes a shared name without changing the vanilla portal tag.
        internal static bool Set(ZDOID id, string name)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null ||
                PortalConnection.GetTarget(id) == null)
            {
                return false;
            }
            string personalName = name?.Trim() ?? "";
            if (personalName.Length > MaxLength)
            {
                personalName = personalName.Substring(0, MaxLength);
            }
            if (string.IsNullOrWhiteSpace(personalName))
            {
                return false;
            }
            if (InUse(id, personalName))
            {
                player.Message(MessageHud.MessageType.Center,
                    DuplicateNameMessage);
                return false;
            }
            if (!PortalFields.Claim(id) ||
                !PortalFields.SetName(id, personalName))
            {
                player.Message(MessageHud.MessageType.Center,
                    "Could not save the portal name.");
                return false;
            }
            PortalDirectory.CacheName(id, personalName);
            Plugin.DebugLog("Name: saved on zdo=" + id + ".");
            return true;
        }

        // Reports whether another cataloged portal has this name.
        internal static bool NameExists(ZDOID id, string name)
        {
            return Player.m_localPlayer != null && ZNet.instance != null &&
                PortalConnection.GetTarget(id) != null &&
                InUse(id, name?.Trim() ?? "");
        }

        // Writes a displayed fallback name before a route is chosen.
        internal static bool EnsureStored(ZDOID id)
        {
            if (!string.IsNullOrWhiteSpace(PortalFields.Read(id)?.Name))
            {
                return true;
            }
            string name = Get(id).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }
            if (InUse(id, name))
            {
                var used = new HashSet<string>(PortalDirectory.KnownRecords()
                    .Where(other => other.Id != id)
                    .Select(other => Get(other.Id)),
                    StringComparer.OrdinalIgnoreCase);
                name = UniqueName(name, used);
            }
            if (!PortalFields.SetName(id, name))
            {
                return false;
            }
            PortalDirectory.CacheName(id, name);
            return true;
        }

        // Imports a vanilla tag into an owned portal ZDO once.
        internal static bool ImportFromShared(PortalRecord record,
            PortalRecord partner)
        {
            ZDO portal = PortalConnection.GetTarget(record.Id);
            if (portal == null)
            {
                return false;
            }
            string tag = portal.GetString(ZDOVars.s_tag);
            if (string.IsNullOrWhiteSpace(tag) ||
                !string.IsNullOrWhiteSpace(PortalFields.Read(portal).Name))
            {
                return true;
            }
            if (!PortalFields.Claim(record.Id))
            {
                return false;
            }
            string name = InitialName(record, partner, tag);
            if (InUse(record.Id, name))
            {
                var used = new HashSet<string>(PortalDirectory.KnownRecords()
                    .Where(other => other.Id != record.Id)
                    .Select(other => Get(other.Id)),
                    StringComparer.OrdinalIgnoreCase);
                name = UniqueName(name, used);
            }
            if (!PortalFields.SetName(record.Id, name))
            {
                return false;
            }
            PortalDirectory.CacheName(record.Id, name);
            return true;
        }

        // Marks one endpoint of a vanilla connected pair with a suffix.
        private static string InitialName(PortalRecord record,
            PortalRecord partner, string tag)
        {
            string suffix = "";
            if (partner != null)
            {
                float distance = PortalDirectory.NearestOtherPortalDistanceSquared(
                    record, partner);
                float partnerDistance =
                    PortalDirectory.NearestOtherPortalDistanceSquared(partner, record);
                bool nearby = distance < 100f * 100f;
                bool partnerNearby = partnerDistance < 100f * 100f;
                bool second = nearby != partnerNearby ? nearby :
                    nearby && distance != partnerDistance ?
                        distance < partnerDistance : !IsFirst(record, partner);
                suffix = second ? "_2" : "";
            }
            tag = tag.Trim();
            int available = MaxLength - suffix.Length;
            return tag.Length > available ? tag.Substring(0, available) + suffix :
                tag + suffix;
        }

        // Checks only other ZDOIDs in the current catalog.
        private static bool InUse(ZDOID id, string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                PortalDirectory.KnownRecords().Any(other => other.Id != id &&
                    string.Equals(Get(other.Id), name,
                        StringComparison.OrdinalIgnoreCase));
        }

        // Finds the first unused numbered form of an imported name.
        private static string UniqueName(string proposed, HashSet<string> used)
        {
            for (int number = 2; ; number++)
            {
                string suffix = " (" + number + ")";
                string head = proposed.Substring(0,
                    Math.Min(proposed.Length, MaxLength - suffix.Length)).TrimEnd();
                string candidate = head + suffix;
                if (!used.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        // Uses both ZDOID components to break equal-distance ties.
        private static bool IsFirst(PortalRecord record, PortalRecord partner)
        {
            return record.Id.UserID != partner.Id.UserID ?
                record.Id.UserID < partner.Id.UserID :
                record.Id.ID < partner.Id.ID;
        }

    }
}
