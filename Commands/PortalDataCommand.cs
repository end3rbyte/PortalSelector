using System;
using System.Collections.Generic;

namespace Landoria.PortalSelector
{
    // Clears this character's PortalSelector data through an explicit command.
    internal static class PortalDataCommand
    {
        private const string Prefix = "Landoria.PortalSelector.";

        // Registers the character data command in Valheim's console.
        internal static void Register()
        {
            new Terminal.ConsoleCommand("portalselector_clear",
                "confirm clears your personal portal data in this world.", Clear);
        }

        // Removes only this mod's keys from the active character.
        private static object Clear(Terminal.ConsoleEventArgs args)
        {
            if (args.Length != 2 ||
                !string.Equals(args[1], "confirm", StringComparison.OrdinalIgnoreCase))
            {
                args.Context.AddString("Usage: portalselector_clear confirm " +
                    "(current world only).");
                return true;
            }
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null)
            {
                args.Context.AddString("Load a character in a world first.");
                return true;
            }
            int count = RemoveKeys(player);
            PortalDirectory.PauseAfterClear(player);
            PortalDialog.Close();
            args.Context.AddString("Removed " + count + " PortalSelector entries " +
                "from this character. Rejoin to rediscover portals; Valheim " +
                "writes the change on its next normal character save.");
            return true;
        }

        // Selects only this world's PortalSelector keys.
        private static int RemoveKeys(Player player)
        {
            string world = ZNet.instance.GetWorldUID().ToString();
            List<string> keys = new List<string>();
            foreach (string key in player.m_customData.Keys)
            {
                if (key.StartsWith(Prefix, StringComparison.Ordinal) &&
                    (key == Prefix + "Catalog." + world ||
                     key.StartsWith(Prefix + "NameStable." + world + ".",
                         StringComparison.Ordinal) ||
                     key.StartsWith(Prefix + "AutoName." + world + ".",
                         StringComparison.Ordinal) ||
                     key.StartsWith(Prefix + "DestinationStable." + world + ".",
                         StringComparison.Ordinal)))
                {
                    keys.Add(key);
                }
            }
            foreach (string key in keys)
            {
                player.m_customData.Remove(key);
            }
            return keys.Count;
        }
    }
}
