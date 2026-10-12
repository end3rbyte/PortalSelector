namespace Landoria.PortalSelector
{
    // Commits the selected portal when the native dialog's OK button is used.
    internal sealed class PortalSelectionReceiver : TextReceiver
    {
        // Supplies an empty value for the hidden native text input.
        public string GetText()
        {
            return "";
        }

        // Saves the choice only after OK is pressed.
        public void SetText(string ignored)
        {
            PortalDialog.Confirm();
        }
    }
}
