namespace Landoria.PortalSelector
{
    // Formats shared portal names for read-only UI text.
    internal static class PortalDisplay
    {
        // Shows a usable label before one has been assigned.
        internal static string Name(ZDOID id)
        {
            string name = PlayerPortalNames.Get(id).RemoveRichTextTags();
            return string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name;
        }
    }
}
