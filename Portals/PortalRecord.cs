namespace Landoria.PortalSelector
{
    // Holds a portal remembered by the player's catalog.
    internal sealed class PortalRecord
    {
        internal ZDOID Id;
        internal string Guid;
        internal bool Active = true;
        internal string Tag;
        internal PortalMetadata Metadata = new PortalMetadata();
        internal float ExitDistance;
    }
}
