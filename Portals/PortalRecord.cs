using UnityEngine;

namespace Landoria.PortalSelector
{
    // Holds one portal returned by the world host.
    internal sealed class PortalRecord
    {
        internal ZDOID Id;
        internal string Guid;
        internal bool Active = true;
        internal string Tag;
        internal PortalMetadata Metadata = new PortalMetadata();
        internal Quaternion Rotation;
        internal bool AllowAllItems;
        internal float ExitDistance;
    }
}
