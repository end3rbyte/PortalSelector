using System;

namespace Landoria.PortalSelector
{
    // Formats shared portal names for read-only UI text.
    internal static class PortalDisplay
    {
        // Shows a usable label before one has been assigned.
        internal static string Name(Guid guid)
        {
            string name = PlayerPortalNames.Get(guid).RemoveRichTextTags();
            return string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name;
        }
    }
}
