namespace Landoria.PortalSelector
{
    // Uses Valheim's native text dialog for a personal portal name.
    internal sealed class PortalNameReceiver : TextReceiver
    {
        private readonly ZDOID _id;

        // Binds the text dialog to one portal.
        internal PortalNameReceiver(ZDOID id)
        {
            _id = id;
        }

        // Returns the current personal name for the native dialog.
        public string GetText()
        {
            return PlayerPortalNames.Get(_id);
        }

        // Saves the confirmed personal name in character memory.
        public void SetText(string text)
        {
            PlayerPortalNames.Set(_id, text);
        }
    }
}
