using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Stores the portals known to one character in one world.
    internal static class PlayerPortalCatalog
    {
        private const int Format = 1;

        // Reads only the ZDOID catalog format for the current world.
        internal static void Load(Player player, string key,
            Dictionary<ZDOID, PortalRecord> records)
        {
            if (!player.m_customData.TryGetValue(key, out string value) ||
                string.IsNullOrEmpty(value))
            {
                Plugin.DebugLog("Catalog: no saved data for this world.");
                return;
            }
            try
            {
                ZPackage package = new ZPackage(Convert.FromBase64String(value));
                int format = package.ReadInt();
                if (format != Format)
                {
                    Plugin.WarningLog("Catalog: unsupported format; ignoring saved data.");
                    return;
                }
                int count = Mathf.Clamp(package.ReadInt(), 0, 10000);
                for (int i = 0; i < count; i++)
                {
                    PortalRecord record = ReadRecord(package);
                    if (record.Id != ZDOID.None)
                    {
                        records[record.Id] = record;
                    }
                }
                Plugin.DebugLog("Catalog: loaded " + records.Count + " portals.");
                return;
            }
            catch (Exception exception)
            {
                records.Clear();
                Plugin.WarningLog("Catalog: load failed (" +
                    exception.GetType().Name + ").");
                return;
            }
        }

        // Updates memory; Valheim writes it on its next normal character save.
        internal static void Save(Player player, string key,
            Dictionary<ZDOID, PortalRecord> records)
        {
            try
            {
                ZPackage package = new ZPackage();
                package.Write(Format);
                package.Write(records.Count);
                foreach (PortalRecord record in records.Values)
                {
                    WriteRecord(package, record);
                }
                player.m_customData[key] = Convert.ToBase64String(package.GetArray());
            }
            catch (Exception exception)
            {
                Plugin.WarningLog("Catalog: save failed (" +
                    exception.GetType().Name + ").");
            }
        }

        // Decodes one portal and its display metadata.
        private static PortalRecord ReadRecord(ZPackage package)
        {
            var record = new PortalRecord();
            record.Id = package.ReadZDOID();
            record.LastKnownName = package.ReadString();
            record.Position = package.ReadVector3();
            record.Biome = (Heightmap.Biome)package.ReadInt();
            return record;
        }

        // Encodes one portal without a GUID or availability flag.
        private static void WriteRecord(ZPackage package, PortalRecord record)
        {
            package.Write(record.Id);
            package.Write(record.LastKnownName ?? "");
            package.Write(record.Position);
            package.Write((int)record.Biome);
        }
    }
}
