using System;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Identifies a portal by the exact stored position bits and its type.
    internal static class PortalIdentity
    {
        // Preserves every bit of Valheim's three saved float coordinates.
        internal static string Key(PortalRecord record)
        {
            Vector3 position = record.Position;
            return FloatBits(position.x) + "." +
                FloatBits(position.y) + "." +
                FloatBits(position.z) + "." +
                (record.AllowAllItems ? "1" : "0");
        }

        // Reads keys written by versions that rounded positions to 10 cm.
        internal static string LegacyKey(PortalRecord record)
        {
            Vector3 position = record.Position;
            return Mathf.RoundToInt(position.x * 10f) + "." +
                Mathf.RoundToInt(position.y * 10f) + "." +
                Mathf.RoundToInt(position.z * 10f) + "." +
                (record.AllowAllItems ? "1" : "0");
        }

        // Formats a float's bit pattern independently of machine byte order.
        private static string FloatBits(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0).ToString("X8");
        }
    }
}
