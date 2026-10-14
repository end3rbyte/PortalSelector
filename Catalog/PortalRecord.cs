using UnityEngine;

namespace Landoria.PortalSelector
{
    // Holds a portal remembered by the player's catalog.
    internal sealed class PortalRecord
    {
        internal ZDOID Id;
        internal string LastKnownName;
        internal Vector3 Position;
        internal Heightmap.Biome Biome;
    }
}
