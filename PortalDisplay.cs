namespace Landoria.PortalSelector
{
    // Formats personal and vanilla shared labels for read-only UI text.
    internal static class PortalDisplay
    {
        // Shows a usable personal label before one has been assigned.
        internal static string PersonalName(ZDOID id)
        {
            string name = PlayerPortalNames.Get(id).RemoveRichTextTags();
            return string.IsNullOrWhiteSpace(name) ? "(sans nom)" : name;
        }

        // Reads a vanilla tag without offering any edit operation for it.
        internal static string SharedTag(string raw)
        {
            string tag = (raw ?? "").RemoveRichTextTags();
            return string.IsNullOrWhiteSpace(tag) ? "(aucun)" : tag;
        }
    }
}
