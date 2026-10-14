using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landoria.PortalSelector
{
    // Builds the scrollable portal picker inside the copied native dialog.
    internal static partial class PortalDialog
    {
        private const float MenuHeight = 260f;
        private const float RowHeight = 36f;
        private const float ScrollbarWidth = 12f;

        // Shows or hides the destination list.
        private static void ToggleMenu()
        {
            if (_menu == null)
            {
                return;
            }
            bool show = !_menu.activeSelf;
            if (show)
            {
                _options.RemoveAll(record => !Available(record));
                if (_options.Count == 0)
                {
                    Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                        "No known destinations available.");
                    return;
                }
                PopulateMenu();
                _menu.transform.SetAsLastSibling();
            }
            _menuBlocker.SetActive(show);
            _menu.SetActive(show);
        }

        // Closes the list without changing the selected destination.
        private static void HideMenu()
        {
            if (_menu != null)
            {
                _menu.SetActive(false);
            }
            if (_menuBlocker != null)
            {
                _menuBlocker.SetActive(false);
            }
        }

        // Catches clicks outside the list before they reach other controls.
        private static void CreateBlocker(RectTransform panel)
        {
            RectTransform blocker = Rect("Destination click blocker", panel,
                new Vector2(10000f, 10000f));
            blocker.anchorMin = blocker.anchorMax = new Vector2(0.5f, 0.5f);
            blocker.anchoredPosition = Vector2.zero;
            Image image = blocker.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            Button button = blocker.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(HideMenu);
            _menuBlocker = blocker.gameObject;
            _menuBlocker.SetActive(false);
        }

        // Positions a scrolling menu under the selection button.
        private static void CreateMenu(RectTransform button)
        {
            RectTransform panel = _panel.transform as RectTransform;
            CreateBlocker(panel);
            Vector3[] corners = new Vector3[4];
            button.GetWorldCorners(corners);
            Vector3 corner = panel.InverseTransformPoint(corners[0]);
            Vector3 right = panel.InverseTransformPoint(corners[3]);
            RectTransform root = Rect("Portal destinations", panel,
                new Vector2(right.x - corner.x, MenuHeight));
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(corner.x, corner.y);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.98f);
            RectTransform viewport = Rect("Viewport", root, Vector2.zero);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-ScrollbarWidth, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Content", viewport, Vector2.zero);
            content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 800f;
            CreateScrollbar(root, scroll);
            _menu = root.gameObject;
            _menu.SetActive(false);
        }

        // Adds a visible draggable scrollbar to the picker.
        private static void CreateScrollbar(RectTransform root, ScrollRect scroll)
        {
            RectTransform track = Rect("Scrollbar", root, Vector2.zero);
            track.anchorMin = new Vector2(1f, 0f);
            track.anchorMax = Vector2.one;
            track.offsetMin = new Vector2(-ScrollbarWidth, 0f);
            track.offsetMax = Vector2.zero;
            RectTransform handle = Rect("Handle", track, Vector2.zero);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            Image image = handle.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 0.68f, 0.3f, 0.85f);
            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = image;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
        }

        // Rebuilds biome headers and portal rows ordered from the map center.
        private static void PopulateMenu()
        {
            RectTransform content = _menu.transform.Find("Viewport/Content") as
                RectTransform;
            float contentWidth = (_menu.transform as RectTransform).rect.width -
                ScrollbarWidth;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(content.GetChild(i).gameObject);
            }
            int position = 0;
            Heightmap.Biome? previousBiome = null;
            foreach (PortalRecord record in _options)
            {
                if (!Available(record))
                {
                    continue;
                }
                if (previousBiome != record.Biome)
                {
                    AddBiomeRow(content, position++, record.Biome, contentWidth);
                    previousBiome = record.Biome;
                }
                AddRow(content, position++, record, contentWidth);
            }
            content.sizeDelta = new Vector2(contentWidth, position * RowHeight);
            content.anchoredPosition = Vector2.zero;
        }

        // Adds a non-clickable heading for one biome.
        private static void AddBiomeRow(RectTransform content, int index,
            Heightmap.Biome biome, float width)
        {
            RectTransform row = Rect("Biome " + biome, content,
                new Vector2(width, RowHeight));
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(0f, -index * RowHeight);
            TextMeshProUGUI text = Rect("Label", row, Vector2.zero)
                .gameObject.AddComponent<TextMeshProUGUI>();
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(8f, 0f);
            text.rectTransform.offsetMax = new Vector2(-8f, 0f);
            text.font = _topic.font;
            text.fontSize = 18f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(1f, 0.68f, 0.3f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.text = BiomeLabel(biome);
            text.raycastTarget = false;
        }

        // Adds one clickable destination row.
        private static void AddRow(RectTransform content, int index, PortalRecord record,
            float width)
        {
            RectTransform row = Rect("Portal " + index, content,
                new Vector2(width, RowHeight));
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(0f, -index * RowHeight);
            Image image = row.gameObject.AddComponent<Image>();
            image.color = Color.white;
            Button button = row.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.clear;
            colors.highlightedColor = new Color(1f, 0.62f, 0.25f, 0.32f);
            colors.pressedColor = new Color(1f, 0.62f, 0.25f, 0.48f);
            colors.selectedColor = Color.clear;
            button.colors = colors;
            button.onClick.AddListener(() => Select(record.Id));
            TextMeshProUGUI text = Rect("Label", row, Vector2.zero)
                .gameObject.AddComponent<TextMeshProUGUI>();
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(24f, 0f);
            text.rectTransform.offsetMax = new Vector2(-8f, 0f);
            text.font = _topic.font;
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.text = PortalDisplay.Name(record.Id);
            text.raycastTarget = false;
        }

        // Records the highlighted choice without saving until OK is pressed.
        private static void Select(ZDOID id)
        {
            if (_options == null || !_options.Exists(record =>
                    record.Id == id && Available(record)))
            {
                return;
            }
            _selected = id;
            HideMenu();
            RefreshChoice();
        }

        // Localizes the biome shown in the menu and selected button.
        private static string BiomeLabel(Heightmap.Biome biome)
        {
            return Localization.instance.Localize(
                "$biome_" + biome.ToString().ToLowerInvariant());
        }

        // Keeps the selected name and biome visible on the closed button.
        private static string ChoiceLabel(PortalRecord record)
        {
            return PortalDisplay.Name(record.Id) + " - " +
                BiomeLabel(record.Biome);
        }

        // Creates a UI rectangle with the requested parent and size.
        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }
    }
}
