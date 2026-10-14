using System;

namespace Landoria.PortalSelector
{
    // Removes destinations that fail while the player waits at a portal.
    internal static partial class PortalDirectory
    {
        // Removes one unanswered destination from this character's catalog.
        internal static void OnUnresponsive(Guid destination, ZDOID origin)
        {
            LoadSession();
            if (_player == null || destination == Guid.Empty ||
                PortalFields.Read(origin)?.DestinationGuid != destination)
            {
                return;
            }
            PlayerDestinations.ClearUnresponsive(origin, destination);
            bool removed = Records.Remove(destination);
            if (removed)
            {
                PlayerPortalCatalog.Save(_player, _catalogKey, Records);
                PortalDialog.OnPortalRemoved(destination);
            }
            PortalConnection.StopWaiting(destination);
            _player.Message(MessageHud.MessageType.Center, removed ?
                "Destination portal did not respond and was removed from your list." :
                "Destination portal did not respond.");
            Plugin.DebugLog("Catalog: unresponsive guid=" + destination +
                " removed=" + removed + ".");
        }
    }
}
