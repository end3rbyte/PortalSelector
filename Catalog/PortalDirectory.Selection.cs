using System.Linq;

namespace Landoria.PortalSelector
{
    // Opens the destination picker from the saved portal catalog.
    internal static partial class PortalDirectory
    {
        // Includes remembered portals without checking them in the background.
        internal static void Request(ZDOID origin)
        {
            ScanNow();
            if (Find(origin) == null || !PlayerPortalNames.IsNamed(origin))
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                    "Name this portal before choosing a destination.");
                return;
            }
            PortalDialog.Open(origin, Records.Values.Where(record =>
                PlayerPortalNames.IsNamed(record.Id)).ToList());
        }
    }
}
