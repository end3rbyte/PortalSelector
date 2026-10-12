using UnityEngine;

namespace Landoria.PortalSelector
{
    // Holds one portal returned by the world host.
    internal sealed class PortalRecord
    {
        internal ZDOID Id;
        internal string Tag;
        internal Vector3 Position;
        internal Quaternion Rotation;
        internal Heightmap.Biome Biome;
        internal bool AllowAllItems;
        internal float ExitDistance;
    }
}
