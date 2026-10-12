using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Stores the known portal catalog inside the character's custom data.
    internal static class PlayerPortalCatalog
    {
        // Reads only this world's catalog from the current character.
        internal static void Load(Player player, string key,
            Dictionary<string, PortalRecord> records)
        {
            if (!player.m_customData.TryGetValue(key, out string value) ||
                string.IsNullOrEmpty(value))
            {
                PortalLog.Detail("catalog", "No saved catalog for " + key + ".");
                return;
            }
            try
            {
                ZPackage package = new ZPackage(Convert.FromBase64String(value));
                int count = Mathf.Clamp(package.ReadInt(), 0, 10000);
                for (int i = 0; i < count; i++)
                {
                    PortalRecord record = ReadRecord(package);
                    records[PortalIdentity.Key(record)] = record;
                    PortalLog.Detail("catalog", "Loaded " +
                        PortalLog.Describe(record));
                }
                PortalLog.Info("catalog", "Loaded " + records.Count +
                    " portals from character data for " + key + ".");
            }
            catch (Exception exception)
            {
                PortalLog.Warning("catalog", "Could not load catalog: " +
                    exception.Message);
                records.Clear();
            }
        }

        // Updates memory; Valheim writes it on its next normal character save.
        internal static void Save(Player player, string key,
            Dictionary<string, PortalRecord> records)
        {
            try
            {
                ZPackage package = new ZPackage();
                package.Write(records.Count);
                foreach (PortalRecord record in records.Values)
                {
                    WriteRecord(package, record);
                }
                player.m_customData[key] = Convert.ToBase64String(package.GetArray());
                PortalLog.Detail("catalog", "Staged " + records.Count +
                    " portals in character memory (" +
                    player.m_customData[key].Length +
                    " encoded characters); normal character save will persist them.");
            }
            catch (Exception exception)
            {
                PortalLog.Warning("catalog", "Could not update catalog: " +
                    exception.Message);
            }
        }

        // Decodes one saved portal.
        private static PortalRecord ReadRecord(ZPackage package)
        {
            return new PortalRecord
            {
                Id = package.ReadZDOID(), Tag = package.ReadString(),
                Position = package.ReadVector3(), Rotation = package.ReadQuaternion(),
                Biome = (Heightmap.Biome)package.ReadInt(),
                AllowAllItems = package.ReadBool(), ExitDistance = package.ReadSingle()
            };
        }

        // Encodes one portal using Valheim's binary package format.
        private static void WriteRecord(ZPackage package, PortalRecord record)
        {
            package.Write(record.Id);
            package.Write(record.Tag ?? "");
            package.Write(record.Position);
            package.Write(record.Rotation);
            package.Write((int)record.Biome);
            package.Write(record.AllowAllItems);
            package.Write(record.ExitDistance);
        }
    }
}
