using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landoria.PortalSelector
{
    // Builds the scrollable portal picker inside the copied native dialog.
    internal static partial class PortalDialog
    {
        private const float MenuWidth = 400f;
        private const float MenuHeight = 260f;
        private const float RowHeight = 36f;

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
                PopulateMenu();
                _menu.transform.SetAsLastSibling();
            }
            _menu.SetActive(show);
        }

        // Positions a scrolling menu under the selection button.
        private static void CreateMenu(RectTransform button)
        {
            RectTransform panel = _panel.transform as RectTransform;
            Vector3[] corners = new Vector3[4];
            button.GetWorldCorners(corners);
            Vector3 corner = panel.InverseTransformPoint(corners[1]);
            RectTransform root = Rect("Portal destinations", panel,
                new Vector2(MenuWidth, MenuHeight));
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(corner.x, corner.y - 3f);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.95f);
            RectTransform viewport = Rect("Viewport", root, Vector2.zero);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-12f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Content", viewport, Vector2.zero);
            content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 50f;
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
            track.offsetMin = new Vector2(-12f, 0f);
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

        // Rebuilds rows in alphabetical order.
        private static void PopulateMenu()
        {
            RectTransform content = _menu.transform.Find("Viewport/Content") as
                RectTransform;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(content.GetChild(i).gameObject);
            }
            for (int i = 0; i < _options.Count; i++)
            {
                AddRow(content, i, _options[i]);
            }
            content.sizeDelta = new Vector2(MenuWidth - 12f,
                _options.Count * RowHeight);
            content.anchoredPosition = Vector2.zero;
        }

        // Adds one clickable destination row.
        private static void AddRow(RectTransform content, int index, PortalRecord record)
        {
            RectTransform row = Rect("Portal " + index, content,
                new Vector2(MenuWidth - 12f, RowHeight));
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
            text.rectTransform.offsetMin = new Vector2(8f, 0f);
            text.rectTransform.offsetMax = new Vector2(-8f, 0f);
            text.font = _topic.font;
            text.fontSize = 15f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.text = RowLabel(record);
            text.raycastTarget = false;
        }

        // Records the highlighted choice without saving until OK is pressed.
        private static void Select(ZDOID id)
        {
            _selected = id;
            PortalLog.Detail("dialog", "Highlighted destination " +
                PortalLog.Describe(PortalDirectory.Find(id)));
            _menu.SetActive(false);
            RefreshChoice();
        }

        // Shows only the personal name and biome.
        private static string RowLabel(PortalRecord record)
        {
            string name = PortalDisplay.PersonalName(record.Id);
            string biome = Localization.instance.Localize(
                "$biome_" + record.Biome.ToString().ToLowerInvariant());
            return name + " - " + biome;
        }

        // Keeps the selected name and biome visible on the closed button.
        private static string ChoiceLabel(PortalRecord record)
        {
            return RowLabel(record);
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
