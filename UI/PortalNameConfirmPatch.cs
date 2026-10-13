using System.Collections;
using GUIFramework;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Landoria.PortalSelector
{
    // Disables the native OK button while a portal name is blank.
    [HarmonyPatch(typeof(TextInput), nameof(TextInput.RequestText))]
    internal static class PortalNameConfirmPatch
    {
        private static GuiInputField _field;
        private static Button _okButton;
        private static bool _editing;
        private static TextInput _owner;
        private static PortalNameReceiver _receiver;
        private static bool _checking;
        private static string _pendingName;
        private static int _checkVersion;
        private static Player _pendingPlayer;
        private static long _pendingWorldUid;

        // Connects the portal name field to the native OK button.
        [HarmonyPostfix]
        private static void AfterRequest(TextInput __instance, TextReceiver sign)
        {
            _editing = false;
            _receiver = null;
            _checking = false;
            _checkVersion++;
            if (_okButton != null)
            {
                _okButton.interactable = true;
            }
            if (!(sign is PortalNameReceiver))
            {
                return;
            }
            _owner = __instance;
            _receiver = (PortalNameReceiver)sign;
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
            if (_checking && value.Trim() != _pendingName)
            {
                _checking = false;
                _checkVersion++;
            }
            if (_editing && _okButton != null)
            {
                _okButton.interactable = !string.IsNullOrWhiteSpace(value);
            }
        }

        // Rejects blank or duplicate names before Valheim closes the dialog.
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
            PortalRecord old = PlayerPortalNames.FindNameOwner(_receiver.Id, name);
            if (old != null &&
                PortalConnection.GetConfirmedTarget(old.Id) == null)
            {
                if (!_checking)
                {
                    _checking = true;
                    _pendingName = name.Trim();
                    _pendingPlayer = Player.m_localPlayer;
                    _pendingWorldUid = ZNet.instance.GetWorldUID();
                    owner.StartCoroutine(CheckReplacement(owner, _field,
                        old, _pendingName, ++_checkVersion));
                }
                return false;
            }
            if (_checking)
            {
                _checking = false;
                _checkVersion++;
            }
            Reject(owner);
            return false;
        }

        // Checks that the old object stays absent before reusing its name.
        private static IEnumerator CheckReplacement(TextInput owner, GuiInputField field,
            PortalRecord old, string name, int version)
        {
            float deadline = Time.unscaledTime + 3f;
            while (Time.unscaledTime < deadline)
            {
                if (!IsCurrent(owner, field, name, version))
                {
                    yield break;
                }
                if (PortalConnection.GetConfirmedTarget(old.Id) != null)
                {
                    _checking = false;
                    Reject(owner);
                    yield break;
                }
                PortalConnection.Request(old.Id);
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (!IsCurrent(owner, field, name, version))
            {
                yield break;
            }
            if (PortalConnection.GetConfirmedTarget(old.Id) != null)
            {
                _checking = false;
                Reject(owner);
                yield break;
            }
            _checking = false;
            PortalDirectory.MarkInactive(old.Id);
            owner.OnInput();
        }

        // Keeps a delayed confirmation tied to the original open dialog.
        private static bool IsCurrent(TextInput owner, GuiInputField field,
            string name, int version)
        {
            return _editing && _checking && owner == _owner && field == _field &&
                owner.m_panel.activeInHierarchy && field.text.Trim() == name &&
                _checkVersion == version && Player.m_localPlayer == _pendingPlayer &&
                ZNet.instance != null &&
                ZNet.instance.GetWorldUID() == _pendingWorldUid;
        }

        // Keeps the dialog open and returns keyboard focus on a live duplicate.
        private static void Reject(TextInput owner)
        {
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                PlayerPortalNames.DuplicateNameMessage);
            owner.StartCoroutine(RestoreFocus(owner, _field));
        }

        // Returns keyboard focus after the rejected submit event finishes.
        private static IEnumerator RestoreFocus(TextInput owner, GuiInputField field)
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
            _checking = false;
            _checkVersion++;
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
