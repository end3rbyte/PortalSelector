using System.Collections.Generic;
using System.Linq;
using GUIFramework;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landoria.PortalSelector
{
    // Copies Valheim's rename dialog and adds a scrolling destination picker.
    [HarmonyPatch]
    internal static partial class PortalDialog
    {
        private static TextInput _owner;
        private static GameObject _originalPanel;
        private static TMP_Text _originalTopic;
        private static GuiInputField _originalInput;
        private static GameObject _panel;
        private static TMP_Text _topic;
        private static GuiInputField _input;
        private static Button _choiceButton;
        private static GameObject _menu;
        private static ZDOID _origin;
        private static ZDOID _selected;
        private static ZDOID _committed;
        private static PortalConnection.State _lastState;
        private static List<PortalRecord> _options;

        // Opens a native-style dialog sorted by personal name.
        internal static void Open(ZDOID origin, List<PortalRecord> records)
        {
            if (TextInput.instance == null || !records.Any(r => r.Id == origin))
            {
                return;
            }
            _origin = origin;
            _options = records.Where(r => r.Id != origin)
                .OrderBy(r => PortalDisplay.PersonalName(r.Id),
                    System.StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(r => r.Biome.ToString()).ToList();
            _committed = PlayerDestinations.Get(origin);
            _selected = _committed;
            if (_options.Count == 0)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                    "No known destinations yet.");
                return;
            }
            TextInput.instance.RequestText(new PortalSelectionReceiver(),
                "Choose destination", 0);
            RefreshChoice();
        }

        // Stores the current choice after the native OK action.
        internal static void Confirm()
        {
            if (_selected != ZDOID.None && _options != null &&
                _options.Exists(record => record.Id == _selected))
            {
                PlayerDestinations.Set(_origin, _selected);
                _committed = _selected;
                PortalConnection.Request(_selected);
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft,
                    "Portal destination selected. Connecting...");
            }
        }

        // Restores the vanilla dialog references during plugin shutdown.
        internal static void Close()
        {
            if (_owner != null && _originalPanel != null)
            {
                _owner.m_panel = _originalPanel;
                _owner.m_topic = _originalTopic;
                _owner.m_inputField = _originalInput;
            }
            if (_panel != null)
            {
                Object.Destroy(_panel);
            }
            _panel = null;
            _owner = null;
        }

        // Refreshes connection text when a native destination request completes.
        internal static void Update()
        {
            if (_panel == null || !_panel.activeInHierarchy ||
                _committed == ZDOID.None)
            {
                return;
            }
            PortalConnection.State state = PortalConnection.GetState(_committed);
            if (state == _lastState)
            {
                return;
            }
            _lastState = state;
            RefreshChoice();
            if (_menu != null && _menu.activeSelf)
            {
                PopulateMenu();
            }
        }

        // Swaps only our request to a copy of Valheim's text dialog.
        [HarmonyPatch(typeof(TextInput), nameof(TextInput.RequestText))]
        [HarmonyPrefix]
        private static void BeforeRequest(TextInput __instance, TextReceiver sign)
        {
            if (_owner != __instance)
            {
                Reset(__instance);
            }
            bool ours = sign is PortalSelectionReceiver;
            if (ours && _panel == null)
            {
                Create();
            }
            if (_panel == null)
            {
                return;
            }
            if (__instance.m_panel.activeSelf)
            {
                __instance.Hide();
            }
            __instance.m_panel = ours ? _panel : _originalPanel;
            __instance.m_topic = ours ? _topic : _originalTopic;
            __instance.m_inputField = ours ? _input : _originalInput;
            if (ours)
            {
                _menu.SetActive(false);
                _lastState = PortalConnection.State.None;
            }
        }

        // Saves native controls from a newly created TextInput instance.
        private static void Reset(TextInput owner)
        {
            Close();
            _owner = owner;
            _originalPanel = owner.m_panel;
            _originalTopic = owner.m_topic;
            _originalInput = owner.m_inputField;
        }

        // Clones the native panel and replaces its text box with a picker.
        private static void Create()
        {
            if (_originalPanel == null || _originalTopic == null ||
                _originalInput == null)
            {
                return;
            }
            _panel = Object.Instantiate(_originalPanel, _originalPanel.transform.parent);
            _panel.name = "PortalSelectorDialog";
            _panel.SetActive(false);
            _topic = CopyControl(_originalTopic.transform)?.GetComponent<TMP_Text>();
            _input = CopyControl(_originalInput.transform)?.GetComponent<GuiInputField>();
            if (_topic == null || _input == null)
            {
                Object.Destroy(_panel);
                _panel = null;
                return;
            }
            CreatePicker();
        }

        // Locates a cloned control by its relative native-panel path.
        private static Transform CopyControl(Transform source)
        {
            string path = source.name;
            for (Transform parent = source.parent;
                 parent != _originalPanel.transform; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }
            return _panel.transform.Find(path);
        }

        // Places a native-styled selection button over the hidden text box.
        private static void CreatePicker()
        {
            RectTransform field = _input.transform as RectTransform;
            Button template = _panel.GetComponentInChildren<Button>(true);
            if (field == null || template == null)
            {
                return;
            }
            GameObject copy = Object.Instantiate(template.gameObject, field.parent, false);
            copy.name = "DestinationPicker";
            RectTransform rect = copy.transform as RectTransform;
            rect.anchorMin = field.anchorMin;
            rect.anchorMax = field.anchorMax;
            rect.offsetMin = field.offsetMin;
            rect.offsetMax = field.offsetMax;
            _choiceButton = copy.GetComponent<Button>();
            _choiceButton.onClick = new Button.ButtonClickedEvent();
            _choiceButton.onClick.AddListener(ToggleMenu);
            copy.SetActive(true);
            _input.gameObject.SetActive(false);
            CreateMenu(rect);
        }

        // Updates the button to show the current destination.
        private static void RefreshChoice()
        {
            if (_choiceButton == null)
            {
                return;
            }
            PortalRecord record = _options?.Find(item => item.Id == _selected);
            string label = record == null ? "Select a portal ▼" :
                ChoiceLabel(record) + " ▼";
            foreach (TMP_Text text in _choiceButton.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = label;
                text.enableAutoSizing = true;
                text.fontSizeMin = 12f;
            }
        }

        // Adds connection status only to the saved personal destination.
        private static string StatusSuffix(ZDOID id)
        {
            if (id != _committed)
            {
                return "";
            }
            PortalConnection.State state = PortalConnection.GetState(id);
            return state == PortalConnection.State.Connected ? " — " +
                Localization.instance.Localize("$piece_portal_connected") :
                state == PortalConnection.State.Connecting ? " — Connecting..." :
                " — Unavailable";
        }
    }
}
