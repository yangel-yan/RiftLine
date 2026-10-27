using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Riftline
{
    internal sealed class RiftlineUIElements
    {
        internal static readonly Color Navy = new Color32(31, 46, 68, 255);
        internal static readonly Color NavyRaised = new Color32(46, 64, 88, 255);
        internal static readonly Color Cyan = new Color32(94, 222, 241, 255);
        internal static readonly Color CyanDim = new Color32(47, 111, 140, 255);
        internal static readonly Color Amber = new Color32(255, 203, 112, 255);
        internal static readonly Color White = new Color32(245, 249, 255, 255);
        internal static readonly Color Muted = new Color32(178, 199, 221, 255);
        internal static readonly Color Red = new Color32(255, 91, 102, 255);
        internal static readonly Color Disabled = new Color32(61, 77, 85, 230);

        private readonly Font font;
        private readonly bool ownsFont;
        private readonly Func<float> fontScale;
        private readonly Action buttonCreated;
        private readonly Dictionary<Text, int> textSizes = new Dictionary<Text, int>();

        public Font Font { get { return font; } }

        public RiftlineUIElements(Func<float> fontScale, Action buttonCreated)
        {
            this.fontScale = fontScale;
            this.buttonCreated = buttonCreated;
            font = CreateSystemFont(out ownsFont);
        }

        public void Dispose()
        {
            if (ownsFont && font != null)
            {
                UnityEngine.Object.Destroy(font);
            }
        }

        public RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        public Image Panel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = Rect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = rect.gameObject.AddComponent<RiftlineUISurface>();
            image.color = color;
            image.Configure(14f, 1f, new Color(Cyan.r, Cyan.g, Cyan.b, .2f), true);
            return image;
        }

        public RiftlineUIGradient Gradient(string name, Transform parent, Color from, Color to,
            Vector2 anchorMin, Vector2 anchorMax, bool vertical = true)
        {
            RectTransform rect = Rect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var graphic = rect.gameObject.AddComponent<RiftlineUIGradient>();
            graphic.SetColors(from, to, vertical);
            graphic.raycastTarget = false;
            return graphic;
        }

        public Text Label(string name, Transform parent, string value, int size, TextAnchor alignment, Color color,
            FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = Mathf.Max(10, Mathf.RoundToInt(size * fontScale()));
            text.alignment = alignment;
            text.color = color;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.text = value;
            textSizes[text] = size;
            return text;
        }

        public Text FixedLabel(string name, Transform parent, string value, int size, TextAnchor alignment, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, FontStyle style = FontStyle.Normal)
        {
            Text text = Label(name, parent, value, size, alignment, color, style);
            RectTransform rect = (RectTransform)text.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return text;
        }

        public Button Button(string name, Transform parent, string caption, UnityAction clicked, bool interactable = true,
            Color? accent = null, float preferredHeight = 66f)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(RiftlineUISurface), typeof(Button), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            RiftlineUISurface image = root.GetComponent<RiftlineUISurface>();
            Color baseColor = accent ?? CyanDim;
            image.color = baseColor;
            image.Configure(14f, 1.5f, new Color(Cyan.r, Cyan.g, Cyan.b, .32f), true);

            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.interactable = interactable;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .96f, .86f, 1f);
            colors.pressedColor = new Color(.72f, .78f, .84f, 1f);
            colors.selectedColor = new Color(.82f, 1f, 1f, 1f);
            colors.disabledColor = new Color(.48f, .52f, .58f, .68f);
            colors.fadeDuration = .08f;
            button.colors = colors;
            if (clicked != null)
            {
                button.onClick.AddListener(clicked);
            }

            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            layout.minHeight = preferredHeight;

            Color textColor = interactable ? baseColor == Cyan || baseColor == Amber ? Navy : White : Muted;
            Text label = Label("Label", root.transform, caption, 30, TextAnchor.MiddleCenter, textColor, FontStyle.Bold);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.RoundToInt(24 * fontScale());
            label.resizeTextMaxSize = Mathf.RoundToInt(30 * fontScale());
            ((RectTransform)label.transform).offsetMin = new Vector2(18f, 8f);
            ((RectTransform)label.transform).offsetMax = new Vector2(-18f, -8f);
            buttonCreated();
            return button;
        }

        public RectTransform Vertical(string name, Transform parent, float spacing, RectOffset padding = null)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        public RectTransform Horizontal(string name, Transform parent, float spacing, float preferredHeight = -1f)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            root.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            if (preferredHeight > 0f)
            {
                LayoutElement element = root.AddComponent<LayoutElement>();
                element.preferredHeight = preferredHeight;
                element.minHeight = preferredHeight;
                element.flexibleHeight = 0f;
            }
            return root.GetComponent<RectTransform>();
        }

        public ScrollRect Scroll(string name, Transform parent, out RectTransform content)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, .12f);

            RectTransform viewport = Rect("Viewport", root.transform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(.5f, 1f);
            VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 8f;
            contentLayout.padding = new RectOffset(8, 8, 8, 8);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = root.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 100f;
            scroll.inertia = true;
            scroll.decelerationRate = .12f;
            return scroll;
        }

        public Image Bar(string name, Transform parent, Color background, Color fill, out Image fillImage)
        {
            Image root = Panel(name, parent, background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform inset = Rect("Inset", root.transform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            RectTransform fillRect = Rect("Fill", inset, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.color = fill;
            fillImage.raycastTarget = false;
            fillImage.type = Image.Type.Simple;
            return root;
        }

        public void SetBarValue(Image fill, float value)
        {
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }

        public RawImage Artwork(string name, Transform parent, Texture texture, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (texture == null)
            {
                return null;
            }
            RectTransform area = Rect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            RectTransform imageRect = Rect("Generated Artwork", area, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RawImage image = imageRect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            AspectRatioFitter fit = imageRect.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = texture.width / (float)Mathf.Max(1, texture.height);
            return image;
        }

        public void Segment(Transform parent, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < .01f)
            {
                return;
            }
            Image line = Panel("Recorded Path", parent, color, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            line.raycastTarget = false;
            line.rectTransform.anchoredPosition = (from + to) * .5f;
            line.rectTransform.sizeDelta = new Vector2(delta.magnitude, width);
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        public void RefreshTypography()
        {
            foreach (Text dead in textSizes.Keys.Where(label => label == null).ToArray())
            {
                textSizes.Remove(dead);
            }
            foreach (KeyValuePair<Text, int> entry in textSizes)
            {
                int size = Mathf.Max(10, Mathf.RoundToInt(entry.Value * fontScale()));
                entry.Key.fontSize = size;
                if (entry.Key.resizeTextForBestFit)
                {
                    entry.Key.resizeTextMinSize = Mathf.RoundToInt(20 * fontScale());
                    entry.Key.resizeTextMaxSize = size;
                }
            }
        }

        public LayoutElement SetLayout(Component component, float preferredWidth, float preferredHeight)
        {
            LayoutElement element = component.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = component.gameObject.AddComponent<LayoutElement>();
            }
            if (preferredWidth >= 0f)
            {
                element.preferredWidth = preferredWidth;
            }
            if (preferredHeight >= 0f)
            {
                preferredHeight *= Mathf.Max(1f, fontScale());
                element.preferredHeight = preferredHeight;
                element.minHeight = preferredHeight;
            }
            return element;
        }

        private static Font CreateSystemFont(out bool ownsFont)
        {
            string[] installed = Font.GetOSInstalledFontNames();
            string[] preferred =
            {
                "PingFang SC", "Hiragino Sans GB", "Noto Sans CJK SC", "Source Han Sans SC",
                "Microsoft YaHei", "Arial Unicode MS", "Arial"
            };
            string selected = preferred.FirstOrDefault(candidate =>
                installed.Any(value => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase)));
            if (string.IsNullOrEmpty(selected))
            {
                selected = installed.FirstOrDefault(value =>
                    value.IndexOf("CJK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (!string.IsNullOrEmpty(selected))
            {
                ownsFont = true;
                return Font.CreateDynamicFontFromOSFont(selected, 32);
            }
            ownsFont = false;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
