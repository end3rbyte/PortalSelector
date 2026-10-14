using System.Collections;
using GUIFramework;
using HarmonyLib;
using UnityEngine.UI;

namespace Landoria.PortalSelector
{
    // Keeps the native rename dialog open for blank or duplicate names.
    [HarmonyPatch(typeof(TextInput), nameof(TextInput.RequestText))]
    internal static class PortalNameConfirmPatch
    {
        private static GuiInputField _field;
        private static Button _okButton;
        private static bool _editing;
        private static TextInput _owner;
        private static PortalNameReceiver _receiver;

        // Connects the portal name field to the native OK button.
        [HarmonyPostfix]
        private static void AfterRequest(TextInput __instance, TextReceiver sign)
        {
            _editing = false;
            _receiver = null;
            if (_okButton != null)
            {
                _okButton.interactable = true;
            }
            if (!(sign is PortalNameReceiver receiver))
            {
                return;
            }
            _owner = __instance;
            _receiver = receiver;
            GuiInputField field = __instance.m_inputField;
            if (_field != field)
            {
                if (_field != null)
                {
                    _field.onValueChanged.RemoveListener(UpdateButton);
                }
                _field = field;
                _field.onValueChanged.AddListener(UpdateButton);
            }
            Button[] buttons = __instance.m_panel.GetComponentsInChildren<Button>(true);
            if (buttons.Length != 2)
            {
                return;
            }
            _okButton = buttons[0].transform.position.x > buttons[1].transform.position.x
                ? buttons[0] : buttons[1];
            _editing = true;
            UpdateButton(field.text);
        }

        // Grays out OK until the player enters a nonblank name.
        private static void UpdateButton(string value)
        {
            if (_editing && _okButton != null)
            {
                _okButton.interactable = !string.IsNullOrWhiteSpace(value);
            }
        }

        // Rejects a name owned by another ZDOID before the dialog closes.
        internal static bool CanSubmit(TextInput owner)
        {
            if (!_editing || _owner != owner || _receiver == null)
            {
                return true;
            }
            string name = _field.text;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }
            if (!PlayerPortalNames.NameExists(_receiver.Id, name))
            {
                return true;
            }
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                PlayerPortalNames.DuplicateNameMessage);
            owner.StartCoroutine(RestoreFocus(owner, _field));
            return false;
        }

        // Returns keyboard focus after a rejected submit event finishes.
        private static IEnumerator RestoreFocus(TextInput owner,
            GuiInputField field)
        {
            yield return null;
            if (!_editing || owner != _owner || field == null ||
                !owner.m_panel.activeInHierarchy)
            {
                yield break;
            }
            field.ActivateInputField();
            field.MoveTextEnd(false);
        }

        // Restores the native button and removes the input listener on unload.
        internal static void Dispose()
        {
            _editing = false;
            if (_okButton != null)
            {
                _okButton.interactable = true;
            }
            if (_field != null)
            {
                _field.onValueChanged.RemoveListener(UpdateButton);
            }
            _field = null;
            _okButton = null;
            _owner = null;
            _receiver = null;
        }
    }
}
