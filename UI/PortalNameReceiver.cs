namespace Landoria.PortalSelector
{
    // Uses Valheim's native text dialog for a personal portal name.
    internal sealed class PortalNameReceiver : TextReceiver
    {
        private readonly ZDOID _id;

        internal PortalNameReceiver(ZDOID id)
        {
            _id = id;
        }

        public string GetText()
        {
            return PlayerPortalNames.Get(_id);
        }

        public void SetText(string text)
        {
            PlayerPortalNames.Set(_id, text);
        }
    }
}
