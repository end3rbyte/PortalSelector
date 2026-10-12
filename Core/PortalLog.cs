using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Logging;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Writes readable, event-based diagnostics to the normal BepInEx log.
    internal static class PortalLog
    {
        private static readonly Dictionary<string, float> LastMessage =
            new Dictionary<string, float>();
        private static ManualLogSource _source;

        // Attaches the plugin logger when BepInEx starts the mod.
        internal static void Initialize(ManualLogSource source)
        {
            _source = source;
            LastMessage.Clear();
        }

        // Releases the logger when BepInEx unloads the mod.
        internal static void Dispose()
        {
            LastMessage.Clear();
            _source = null;
        }

        // Records a major lifecycle event regardless of detailed logging.
        internal static void Info(string category, string message)
        {
            _source?.LogInfo("[" + category + "] " + message);
        }

        // Records failures even if detailed logging is disabled.
        internal static void Warning(string category, string message)
        {
            _source?.LogWarning("[" + category + "] " + message);
        }

        // Records a detailed event when diagnostics are enabled.
        internal static void Detail(string category, string message)
        {
            if (Plugin.DetailedLogging?.Value == true)
            {
                Info(category, message);
            }
        }

        // Records repeated gameplay attempts at most once per interval.
        internal static void Throttled(string key, string category,
            string message, float interval = 3f)
        {
            if (Plugin.DetailedLogging?.Value != true) { return; }
            float now = Time.unscaledTime;
            if (LastMessage.TryGetValue(key, out float previous) &&
                now - previous < interval) { return; }
            LastMessage[key] = now;
            Info(category, message);
        }

        // Formats both the persistent identity and temporary network ID.
        internal static string Describe(PortalRecord record)
        {
            if (record == null) { return "unknown portal"; }
            return string.Format(CultureInfo.InvariantCulture,
                "stable={0} zdo={1} pos=({2:R},{3:R},{4:R}) shared=\"{5}\"",
                PortalIdentity.Key(record), record.Id, record.Position.x,
                record.Position.y, record.Position.z, Clean(record.Tag));
        }

        // Keeps player-entered labels on one bounded log line.
        internal static string Clean(string value)
        {
            string oneLine = (value ?? "").Replace('\r', ' ')
                .Replace('\n', ' ').Replace('"', '\'');
            return oneLine.Length <= 80 ? oneLine : oneLine.Substring(0, 80);
        }
    }
}
