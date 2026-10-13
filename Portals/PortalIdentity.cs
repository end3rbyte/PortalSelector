namespace Landoria.PortalSelector
{
    // Identifies a portal by the GUID stored in its world object.
    internal static class PortalIdentity
    {
        // Namespaces portal GUIDs inside the character catalog.
        internal static string CatalogKey(PortalRecord record)
        {
            return "g:" + record.Guid;
        }

        // Uses the stable portal GUID for names and validation.
        internal static string Key(PortalRecord record)
        {
            return record.Guid;
        }
    }
}
