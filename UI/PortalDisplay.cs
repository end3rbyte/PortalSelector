namespace Landoria.PortalSelector
{
    // Formats personal portal names for read-only UI text.
    internal static class PortalDisplay
    {
        // Shows a usable personal label before one has been assigned.
        internal static string PersonalName(ZDOID id)
        {
            string name = PlayerPortalNames.Get(id).RemoveRichTextTags();
            return string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name;
        }
    }
}
