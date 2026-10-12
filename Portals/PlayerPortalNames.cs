using System;
using System.Collections.Generic;
using System.Linq;

namespace Landoria.PortalSelector
{
    // Stores personal labels under a position-based portal identity.
    internal static class PlayerPortalNames
    {
        internal const int MaxLength = 50;
        private const string Prefix = "Landoria.PortalSelector.NameStable.";
        private const string AutoPrefix = "Landoria.PortalSelector.AutoName.";

        // Reads the label for the current portal object.
        internal static string Get(ZDOID id)
        {
            Player player = Player.m_localPlayer;
            PortalRecord record = PortalDirectory.Find(id);
            if (player == null || record == null || ZNet.instance == null)
            {
                return "";
            }
            if (player.m_customData.TryGetValue(Key(record), out string value))
            {
                return value;
            }
            return player.m_customData.TryGetValue(LegacyKey(record), out value) ?
                value : "";
        }

        // Changes the personal label in memory for Valheim's next save.
        internal static bool Set(ZDOID id, string name)
        {
            Player player = Player.m_localPlayer;
            PortalRecord record = PortalDirectory.Find(id);
            if (player != null && record != null && ZNet.instance != null)
            {
                string personalName = name?.Trim() ?? "";
                if (personalName.Length > MaxLength)
                {
                    personalName = personalName.Substring(0, MaxLength);
                }
                if (InUse(player, record, personalName))
                {
                    PortalLog.Detail("name", "Rejected duplicate personal name \"" +
                        PortalLog.Clean(personalName) + "\" for " +
                        PortalLog.Describe(record));
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                        "Ce nom est déjà utilisé par un autre portail.");
                    return false;
                }
                player.m_customData[Key(record)] = personalName;
                player.m_customData.Remove(AutoKey(record));
                PortalLog.Detail("name", "Set personal name \"" +
                    PortalLog.Clean(name?.Trim()) + "\" for " +
                    PortalLog.Describe(record));
                return true;
            }
            return false;
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
            if (HasPersonalName(player, record)) { return; }
            string name = UniqueName(player, record, InitialName(record, partner));
            player.m_customData[key] = name;
            player.m_customData[autoKey] = name;
            PortalLog.Detail("name", "Copied shared tag as personal name \"" +
                PortalLog.Clean(name) + "\" for " + PortalLog.Describe(record));
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
                PortalLog.Detail("name", "Stopped automatic naming for " +
                    PortalLog.Describe(record) + "; current name changed.");
                return;
            }
            if (partner == null) { return; }
            string name = UniqueName(player, record, InitialName(record, partner));
            if (current == name) { return; }
            player.m_customData[key] = name;
            player.m_customData[autoKey] = name;
            PortalLog.Detail("name", "Completed paired name \"" +
                PortalLog.Clean(name) + "\" for " +
                PortalLog.Describe(record) + " with " +
                PortalLog.Describe(partner));
        }

        // Keeps the shared tag intact and adds the pair suffix when applicable.
        private static string InitialName(PortalRecord record, PortalRecord partner)
        {
            string suffix = partner == null ? "" : IsFirst(record, partner) ?
                "_1" : "_2";
            string tag = record.Tag.Trim();
            int available = MaxLength - suffix.Length;
            if (tag.Length > available) { tag = tag.Substring(0, available); }
            return tag + suffix;
        }

        // Repairs older duplicate labels without changing unique labels.
        internal static void EnsureUniqueNames(IEnumerable<PortalRecord> records)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null) { return; }
            var ordered = records.Where(r => !string.IsNullOrWhiteSpace(Get(r.Id)))
                .OrderBy(r => player.m_customData.ContainsKey(AutoKey(r)) ? 1 : 0)
                .ThenBy(r => PortalIdentity.Key(r), StringComparer.Ordinal);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PortalRecord record in ordered)
            {
                string current = Get(record.Id);
                if (used.Add(current)) { continue; }
                string replacement = UniqueName(current, used);
                player.m_customData[Key(record)] = replacement;
                if (player.m_customData.ContainsKey(AutoKey(record)))
                {
                    player.m_customData[AutoKey(record)] = replacement;
                }
                used.Add(replacement);
                PortalLog.Detail("name", "Resolved duplicate \"" +
                    PortalLog.Clean(current) + "\" as \"" +
                    PortalLog.Clean(replacement) + "\" for " +
                    PortalLog.Describe(record));
            }
        }

        private static bool InUse(Player player, PortalRecord record, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) { return false; }
            string prefix = Prefix + ZNet.instance.GetWorldUID() + ".";
            string ownKey = Key(record);
            string ownLegacy = LegacyKey(record);
            return player.m_customData.Any(entry =>
                entry.Key.StartsWith(prefix, StringComparison.Ordinal) &&
                entry.Key != ownKey && entry.Key != ownLegacy &&
                string.Equals(entry.Value, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string UniqueName(Player player, PortalRecord record,
            string proposed)
        {
            if (!InUse(player, record, proposed)) { return proposed; }
            string prefix = Prefix + ZNet.instance.GetWorldUID() + ".";
            var used = new HashSet<string>(player.m_customData.Where(entry =>
                entry.Key.StartsWith(prefix, StringComparison.Ordinal) &&
                entry.Key != Key(record) && entry.Key != LegacyKey(record))
                .Select(entry => entry.Value), StringComparer.OrdinalIgnoreCase);
            return UniqueName(proposed, used);
        }

        private static string UniqueName(string proposed, HashSet<string> used)
        {
            for (int number = 2; ; number++)
            {
                string suffix = " (" + number + ")";
                string head = proposed.Substring(0,
                    Math.Min(proposed.Length, MaxLength - suffix.Length)).TrimEnd();
                string candidate = head + suffix;
                if (!used.Contains(candidate)) { return candidate; }
            }
        }

        // Checks both precise and earlier rounded saved labels.
        private static bool HasPersonalName(Player player, PortalRecord record)
        {
            if (player.m_customData.TryGetValue(Key(record), out string name))
            {
                return !string.IsNullOrWhiteSpace(name);
            }
            return player.m_customData.TryGetValue(LegacyKey(record), out name) &&
                !string.IsNullOrWhiteSpace(name);
        }

        // Uses the stable position first and the live ID only for exact ties.
        private static bool IsFirst(PortalRecord record, PortalRecord partner)
        {
            int order = string.CompareOrdinal(PortalIdentity.Key(record),
                PortalIdentity.Key(partner));
            if (order != 0) { return order < 0; }
            if (record.Id.UserID != partner.Id.UserID)
            {
                return record.Id.UserID < partner.Id.UserID;
            }
            return record.Id.ID < partner.Id.ID;
        }

        // Clears a destroyed portal's personal label.
        internal static void Forget(PortalRecord record)
        {
            if (Player.m_localPlayer != null && ZNet.instance != null)
            {
                Player.m_localPlayer.m_customData.Remove(Key(record));
                Player.m_localPlayer.m_customData.Remove(AutoKey(record));
                PortalLog.Detail("name", "Removed personal name for " +
                    PortalLog.Describe(record));
                if (!PortalDirectory.HasOtherLegacyIdentity(record))
                {
                    Player.m_localPlayer.m_customData.Remove(LegacyKey(record));
                }
            }
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

        // Finds a label saved under the earlier rounded position key.
        private static string LegacyKey(PortalRecord record)
        {
            return Prefix + ZNet.instance.GetWorldUID() + "." +
                PortalIdentity.LegacyKey(record);
        }
    }
}
