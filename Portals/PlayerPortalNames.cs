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

        // Copies a shared label only when this portal has no personal name.
        internal static void InitializeFromShared(PortalRecord record,
            PortalRecord partner)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null ||
                string.IsNullOrWhiteSpace(record.Tag))
            {
                return;
            }
            string key = Key(record);
            string autoKey = AutoKey(record);
            if (player.m_customData.TryGetValue(autoKey, out string automatic))
            {
                CompleteAutomaticName(player, key, autoKey, automatic,
                    record, partner);
                return;
            }
            if (HasPersonalName(player, record))
            {
                return;
            }
            string name = InitialName(record, partner);
            player.m_customData[key] = name;
            player.m_customData[autoKey] = name;
        }

        // Adds a suffix later only if the player kept the generated name.
        private static void CompleteAutomaticName(Player player, string key,
            string autoKey, string automatic, PortalRecord record,
            PortalRecord partner)
        {
            if (!player.m_customData.TryGetValue(key, out string current) ||
                current != automatic)
            {
                player.m_customData.Remove(autoKey);
                return;
            }
            string name = InitialName(record, partner);
            if (current == name)
            {
                return;
            }
            player.m_customData[key] = name;
            player.m_customData[autoKey] = name;
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

        // Repairs older duplicate labels without changing unique labels.
        internal static void EnsureUniqueNames(IEnumerable<PortalRecord> records)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null)
            {
                return;
            }
            var ordered = records.Where(r => r.Active &&
                    !string.IsNullOrWhiteSpace(Get(r.Id)))
                .OrderBy(r => player.m_customData.ContainsKey(AutoKey(r)) ? 1 : 0)
                .ThenBy(r => PortalIdentity.Key(r), StringComparer.Ordinal);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PortalRecord record in ordered)
            {
                string current = Get(record.Id);
                if (used.Add(current))
                {
                    continue;
                }
                string replacement = UniqueName(current, used);
                player.m_customData[Key(record)] = replacement;
                if (player.m_customData.ContainsKey(AutoKey(record)))
                {
                    player.m_customData[AutoKey(record)] = replacement;
                }
                used.Add(replacement);
            }
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

        // Adds a suffix when a generated name is already in use.
        private static string UniqueName(Player player, PortalRecord record,
            string proposed)
        {
            if (!InUse(record, proposed))
            {
                return proposed;
            }
            string prefix = Prefix + ZNet.instance.GetWorldUID() + ".";
            var used = new HashSet<string>(player.m_customData.Where(entry =>
                entry.Key.StartsWith(prefix, StringComparison.Ordinal) &&
                entry.Key != Key(record))
                .Select(entry => entry.Value), StringComparer.OrdinalIgnoreCase);
            return UniqueName(proposed, used);
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

        // Keeps the previously named endpoint plain, then uses portal GUIDs.
        private static bool IsFirst(PortalRecord record, PortalRecord partner)
        {
            Player player = Player.m_localPlayer;
            bool recordNamed = HasPersonalName(player, record);
            bool partnerNamed = HasPersonalName(player, partner);
            if (recordNamed != partnerNamed)
            {
                return recordNamed;
            }
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
