using System;

namespace Landoria.PortalSelector
{
    // Holds PortalSelector values stored on one portal's world ZDO.
    internal sealed class PortalZdoData
    {
        internal string Name;
        internal ZDOID Destination;
        internal DateTime? CreatedAtUtc;
    }
}
