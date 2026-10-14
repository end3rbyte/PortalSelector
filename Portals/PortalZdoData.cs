using System;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Holds PortalSelector values stored on one portal's world ZDO.
    internal sealed class PortalZdoData
    {
        internal string Name;
        internal Guid Guid;
        internal Vector3 Position;
        internal Guid DestinationGuid;
        internal Vector3 DestinationPosition;
        internal DateTime? CreatedAtUtc;
    }
}
