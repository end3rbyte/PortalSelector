using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Stores the known portal catalog inside the character's custom data.
    internal static class PlayerPortalCatalog
    {
        private const int ActiveFormat = -3;
        private const int PreviousGuidFormat = -2;

        // Reads only this world's catalog from the current character.
        internal static bool Load(Player player, string key,
            Dictionary<string, PortalRecord> records)
        {
            if (!player.m_customData.TryGetValue(key, out string value) ||
                string.IsNullOrEmpty(value))
            {
                Plugin.DebugLog("Catalog: no saved data for this world.");
                return false;
            }
            try
            {
                return ReadPackage(value, records);
            }
            catch (Exception exception)
            {
                records.Clear();
                Plugin.WarningLog("Catalog: load failed (" +
                    exception.GetType().Name + ").");
                return false;
            }
        }

        // Reads GUID catalogs and rewrites the previous layout without unused fields.
        private static bool ReadPackage(string value,
            Dictionary<string, PortalRecord> records)
        {
            ZPackage package = new ZPackage(Convert.FromBase64String(value));
            int format = package.ReadInt();
            if (format != ActiveFormat && format != PreviousGuidFormat)
            {
                Plugin.WarningLog("Catalog: unsupported format; ignoring saved data.");
                return false;
            }
            int count = Mathf.Clamp(package.ReadInt(), 0, 10000);
            bool rewrite = format != ActiveFormat;
            for (int i = 0; i < count; i++)
            {
                PortalRecord record = ReadRecord(package, format);
                if (string.IsNullOrWhiteSpace(record.Guid))
                {
                    rewrite = true;
                    continue;
                }
                records[PortalIdentity.CatalogKey(record)] = record;
            }
            Plugin.DebugLog("Catalog: loaded " + records.Count +
                " portals; rewrite=" + rewrite + ".");
            return rewrite;
        }

        // Updates memory; Valheim writes it on its next normal character save.
        internal static void Save(Player player, string key,
            Dictionary<string, PortalRecord> records)
        {
            try
            {
                ZPackage package = new ZPackage();
                package.Write(ActiveFormat);
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

        // Skips obsolete fields when reading the previous GUID catalog layout.
        private static PortalRecord ReadRecord(ZPackage package, int format)
        {
            PortalRecord record = new PortalRecord();
            record.Id = package.ReadZDOID();
            record.Tag = package.ReadString();
            record.Metadata.Position = package.ReadVector3();
            if (format == PreviousGuidFormat)
            {
                package.ReadQuaternion();
            }
            record.Metadata.Biome = (Heightmap.Biome)package.ReadInt();
            if (format == PreviousGuidFormat)
            {
                package.ReadBool();
            }
            record.ExitDistance = package.ReadSingle();
            record.Guid = package.ReadString();
            record.Active = package.ReadBool();
            return record;
        }

        // Encodes one portal using Valheim's binary package format.
        private static void WriteRecord(ZPackage package, PortalRecord record)
        {
            package.Write(record.Id);
            package.Write(record.Tag ?? "");
            package.Write(record.Metadata.Position);
            package.Write((int)record.Metadata.Biome);
            package.Write(record.ExitDistance);
            package.Write(record.Guid ?? "");
            package.Write(record.Active);
        }
    }
}
