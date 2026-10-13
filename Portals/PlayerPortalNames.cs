using System;
using System.Collections.Generic;
using System.Linq;

namespace Landoria.PortalSelector
{
    // Stores personal labels under each portal's GUID.
    internal static class PlayerPortalNames
    {
        internal const int MaxLength = 50;
        internal const string DuplicateNameMessage =
            "This name is already used by another portal.";
        private const string Prefix = "Landoria.PortalSelector.NameGuidV1.";
        private const string AutoPrefix = "Landoria.PortalSelector.AutoNameGuidV1.";

        // Reads the label for the current portal object.
        internal static string Get(ZDOID id)
        {
            Player player = Player.m_localPlayer;
            PortalRecord record = PortalDirectory.Find(id);
            if (player == null || record == null || ZNet.instance == null)
            {
                return "";
            }
            return player.m_customData.TryGetValue(Key(record), out string value) ?
                value : "";
        }

        // Accepts a personal name or a name copied from a shared portal tag.
        internal static bool IsNamed(ZDOID id)
        {
            return !string.IsNullOrWhiteSpace(Get(id));
        }

        // Finds an active portal by its personal name for name checks.
        internal static PortalRecord FindByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            return PortalDirectory.KnownRecords().FirstOrDefault(record =>
                record.Active && string.Equals(Get(record.Id), name,
                    StringComparison.OrdinalIgnoreCase));
        }

        // Changes the personal label in memory for Valheim's next save.
        internal static bool Set(ZDOID id, string name)
        {
            Player player = Player.m_localPlayer;
            PortalRecord record = PortalDirectory.Find(id);
            if (player != null && record != null && ZNet.instance != null)
            {
                string currentKey = Key(record);
                string currentAutoKey = AutoKey(record);
                string personalName = name?.Trim() ?? "";
                if (personalName.Length > MaxLength)
                {
                    personalName = personalName.Substring(0, MaxLength);
                }
                if (InUse(record, personalName))
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                        DuplicateNameMessage);
                    return false;
                }
                PortalRecord inactive = FindInactiveNameOwner(id, personalName);
                if (inactive != null &&
                    PortalDirectory.RebindInactive(inactive.Id, id))
                {
                    player.m_customData.Remove(currentKey);
                    player.m_customData.Remove(currentAutoKey);
                }
                player.m_customData[Key(record)] = personalName;
                player.m_customData.Remove(AutoKey(record));
                Plugin.DebugLog("Name: saved guid=" + record.Guid + ".");
                return true;
            }
            return false;
        }

        // Reports whether another portal already uses this personal name.
        internal static bool NameExists(ZDOID id, string name)
        {
            Player player = Player.m_localPlayer;
            PortalRecord record = PortalDirectory.Find(id);
            return player != null && record != null && ZNet.instance != null &&
                InUse(record, name?.Trim() ?? "");
        }

        // Finds the saved portal that currently owns a proposed name.
        internal static PortalRecord FindNameOwner(ZDOID id, string name)
        {
            foreach (PortalRecord candidate in PortalDirectory.KnownRecords())
            {
                if (candidate.Active && candidate.Id != id &&
                    string.Equals(Get(candidate.Id),
                    name?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            return null;
        }

        // Finds a retired entry that the player is rebuilding under its old name.
        private static PortalRecord FindInactiveNameOwner(ZDOID id, string name)
        {
            return PortalDirectory.KnownRecords().FirstOrDefault(candidate =>
                !candidate.Active && candidate.Id != id &&
                string.Equals(Get(candidate.Id), name,
                    StringComparison.OrdinalIgnoreCase));
        }

        // Imports a shared tag only once and preserves an existing personal name.
        internal static void ImportFromShared(PortalRecord record,
            PortalRecord partner)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null)
            {
                return;
            }
            player.m_customData.Remove(AutoKey(record));
            if (HasPersonalName(player, record) ||
                string.IsNullOrWhiteSpace(record.Tag))
            {
                return;
            }
            string name = InitialName(record, partner);
            if (InUse(record, name))
            {
                var used = new HashSet<string>(PortalDirectory.KnownRecords()
                    .Where(other => other.Active && other.Guid != record.Guid)
                    .Select(other => Get(other.Id)),
                    StringComparer.OrdinalIgnoreCase);
                name = UniqueName(name, used);
            }
            player.m_customData[Key(record)] = name;
        }

        // Marks one endpoint of a connected pair and leaves lone portals plain.
        private static string InitialName(PortalRecord record, PortalRecord partner)
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
            string tag = record.Tag.Trim();
            int available = MaxLength - suffix.Length;
            if (tag.Length > available)
            {
                tag = tag.Substring(0, available);
            }
            return tag + suffix;
        }

        // Checks whether another portal already owns this personal name.
        private static bool InUse(PortalRecord record, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }
            string ownIdentity = PortalIdentity.Key(record);
            return PortalDirectory.KnownRecords().Any(other =>
                other.Active && PortalIdentity.Key(other) != ownIdentity &&
                string.Equals(Get(other.Id), name,
                    StringComparison.OrdinalIgnoreCase));
        }

        // Finds the first free numbered form of a name.
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

        // Checks the current personal label.
        private static bool HasPersonalName(Player player, PortalRecord record)
        {
            return player.m_customData.TryGetValue(Key(record), out string name) &&
                !string.IsNullOrWhiteSpace(name);
        }

        // Deletes the personal name and legacy automatic-name marker.
        internal static void Forget(PortalRecord record)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null)
            {
                return;
            }
            player.m_customData.Remove(Key(record));
            player.m_customData.Remove(AutoKey(record));
        }

        // Uses stable GUIDs to break equal-distance ties for a vanilla pair.
        private static bool IsFirst(PortalRecord record, PortalRecord partner)
        {
            int order = string.CompareOrdinal(PortalIdentity.Key(record),
                PortalIdentity.Key(partner));
            if (order != 0)
            {
                return order < 0;
            }
            if (record.Id.UserID != partner.Id.UserID)
            {
                return record.Id.UserID < partner.Id.UserID;
            }
            return record.Id.ID < partner.Id.ID;
        }

        // Scopes the stable identity to the active world.
        private static string Key(PortalRecord record)
        {
            return Prefix + ZNet.instance.GetWorldUID() + "." +
                PortalIdentity.Key(record);
        }

        // Marks names generated by this mod so later pairing stays safe.
        private static string AutoKey(PortalRecord record)
        {
            return AutoPrefix + ZNet.instance.GetWorldUID() + "." +
                PortalIdentity.Key(record);
        }

    }
}
