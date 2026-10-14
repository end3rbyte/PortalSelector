using Guid = System.Guid;
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
        private static GameObject _menuBlocker;
        private static ZDOID _origin;
        private static Guid _originGuid;
        private static Guid _selected;
        private static List<PortalRecord> _options;
        private const float PickerHeightIncrease = 16f;
        private const float ExtraDialogHeight = 32f;
        private const float TitleSpacing = 2f;
        private const float ActionSpacing = 12f;
        internal static bool IsOpen => _owner != null && _panel != null &&
            _owner.m_panel == _panel && _panel.activeInHierarchy;

        // Opens a native-style dialog grouped by biome and map-center distance.
        internal static void Open(ZDOID origin, List<PortalRecord> records)
        {
            PortalRecord source = PortalDirectory.Find(origin);
            if (TextInput.instance == null || source == null ||
                !records.Any(r => r.Guid == source.Guid && Available(r)))
            {
                return;
            }
            _origin = origin;
            _originGuid = source.Guid;
            _options = records.Where(r => r.Guid != source.Guid && Available(r))
                .OrderBy(r => BiomeOrder(r.Biome))
                .ThenBy(r => r.Position.x * r.Position.x +
                    r.Position.z * r.Position.z)
                .ThenBy(r => PortalDisplay.Name(r.Guid),
                    System.StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            _selected = PlayerDestinations.Get(origin);
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

        // Sorts biomes in the usual exploration order.
        private static int BiomeOrder(Heightmap.Biome biome)
        {
            switch (biome.ToString())
            {
                case "Meadows": return 0;
                case "BlackForest": return 1;
                case "Swamp": return 2;
                case "Mountain": return 3;
                case "Plains": return 4;
                case "Mistlands": return 5;
                case "AshLands": return 6;
                case "DeepNorth": return 7;
                case "Ocean": return 8;
                default: return 9;
            }
        }

        // Stores the current choice after the native OK action.
        internal static void Confirm()
        {
            if (_selected != Guid.Empty && _options != null &&
                _options.Exists(record => record.Guid == _selected &&
                    Available(record)))
            {
                if (PlayerDestinations.Set(_origin, _selected))
                {
                    Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft,
                        "Portal destination selected.");
                }
                else
                {
                    Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                        "Could not save the portal destination.");
                }
            }
        }

        // Removes a destroyed portal from an open destination dialog.
        internal static void OnPortalRemoved(Guid guid)
        {
            if (_originGuid == guid && IsOpen)
            {
                HideMenu();
                _owner.Hide();
                return;
            }
            if (_options == null || _options.RemoveAll(record => record.Guid == guid) == 0)
            {
                return;
            }
            if (_selected == guid)
            {
                _selected = Guid.Empty;
            }
            RefreshChoice();
            if (IsOpen && _menu != null && _menu.activeSelf)
            {
                if (_options.Count == 0)
                {
                    HideMenu();
                }
                else
                {
                    PopulateMenu();
                }
            }
        }

        // Refreshes an open picker when a peer announces a portal or new name.
        internal static void OnPortalUpdated(PortalRecord record)
        {
            if (!IsOpen || _options == null || record.Guid == _originGuid)
            {
                return;
            }
            _options.RemoveAll(option => option.Guid == record.Guid);
            if (!string.IsNullOrWhiteSpace(record.LastKnownName))
            {
                _options.Add(record);
            }
            _options = _options.OrderBy(option => BiomeOrder(option.Biome))
                .ThenBy(option => option.Position.x * option.Position.x +
                    option.Position.z * option.Position.z)
                .ThenBy(option => PortalDisplay.Name(option.Guid),
                    System.StringComparer.CurrentCultureIgnoreCase).ToList();
            RefreshChoice();
            if (_menu != null && _menu.activeSelf)
            {
                if (_options.Count == 0)
                {
                    HideMenu();
                }
                else
                {
                    PopulateMenu();
                }
            }
        }

        // Confirms that a saved option still belongs to the GUID catalog.
        private static bool Available(PortalRecord option)
        {
            return PortalDirectory.Find(option.Guid) != null;
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
            _menu = null;
            _menuBlocker = null;
            _owner = null;
        }

        // Swaps only our request to a copy of Valheim's text dialog.
        [HarmonyPatch(typeof(TextInput), nameof(TextInput.RequestText))]
        [HarmonyPrefix]
        [HarmonyAfter("Landoria.NearbyStorage")]
        private static void BeforeRequest(TextInput __instance, TextReceiver sign)
        {
            bool ours = sign is PortalSelectionReceiver;
            if (!ours && (_owner != __instance || __instance.m_panel != _panel))
            {
                return;
            }
            if (ours && _owner != __instance)
            {
                Reset(__instance);
            }
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
                HideMenu();
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
            SpaceDialogVertically();
            CreatePicker();
        }

        // Adds space between the destination picker, title, and action buttons.
        private static void SpaceDialogVertically()
        {
            RectTransform panel = _panel.transform as RectTransform;
            if (panel == null)
            {
                return;
            }
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                panel.rect.height + ExtraDialogHeight);
            _topic.transform.position += panel.TransformVector(new Vector3(0f, TitleSpacing, 0f));
            foreach (Button action in panel.GetComponentsInChildren<Button>(true))
            {
                action.transform.position += panel.TransformVector(
                    new Vector3(0f, -ActionSpacing, 0f));
            }
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
            rect.offsetMin = field.offsetMin - new Vector2(0f, PickerHeightIncrease / 2f);
            rect.offsetMax = field.offsetMax + new Vector2(0f, PickerHeightIncrease / 2f);
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
            PortalRecord record = _options?.Find(item =>
                item.Guid == _selected && Available(item));
            string label = record == null ? "Select a portal" : ChoiceLabel(record);
            foreach (TMP_Text text in _choiceButton.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = label;
                text.enableAutoSizing = true;
                text.fontSize = 20f;
                text.fontSizeMin = 16f;
                text.fontSizeMax = 20f;
            }
        }
    }
}
