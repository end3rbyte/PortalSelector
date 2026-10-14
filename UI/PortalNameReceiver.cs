namespace Landoria.PortalSelector
{
    // Uses Valheim's native text dialog for a shared portal name.
    internal sealed class PortalNameReceiver : TextReceiver
    {
        private readonly ZDOID _id;
        internal ZDOID Id => _id;

        // Binds the text dialog to one portal.
        internal PortalNameReceiver(ZDOID id)
        {
            _id = id;
        }

        // Returns the current portal name for the native dialog.
        public string GetText()
        {
            return PlayerPortalNames.Get(_id);
        }

        // Saves the confirmed name on the portal ZDO.
        public void SetText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            PlayerPortalNames.Set(_id, text);
        }
    }
}
