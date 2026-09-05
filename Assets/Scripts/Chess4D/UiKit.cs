using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chess4D.Unity
{
    /// <summary>Minimal helpers for building uGUI in code.</summary>
    public static class UiKit
    {
        private static Font font;
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.1f, 0.82f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.22f, 0.26f, 1f);
        public static readonly Color ActiveColor = new Color(0.95f, 0.6f, 0.15f, 1f);
        public static readonly Color ArmedColor = new Color(0.3f, 0.7f, 1f, 1f);
        public static readonly Color TextColor = new Color(0.92f, 0.92f, 0.92f, 1f);

        public static Canvas Canvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rt;
        }

        public static RectTransform VerticalGroup(Transform parent, string name, float spacing, RectOffset padding)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.padding = padding;
            v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            v.childControlHeight = true; v.childControlWidth = true;
            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        public static RectTransform HorizontalGroup(Transform parent, string name, float spacing, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            h.childControlWidth = true; h.childControlHeight = true;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            return rt;
        }

        public static Text Label(Transform parent, string text, int size = 14, TextAnchor align = TextAnchor.MiddleLeft, float height = 20)
        {
            var go = new GameObject("label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font; t.fontSize = size; t.color = TextColor; t.alignment = align; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            return t;
        }

        public static Button Button(Transform parent, string label, UnityAction onClick, float height = 26, int fontSize = 14)
        {
            var go = new GameObject("button " + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = ButtonColor;
            var b = go.GetComponent<Button>();
            b.onClick.AddListener(onClick);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var t = Label(go.transform, label, fontSize, TextAnchor.MiddleCenter, height);
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return b;
        }

        public static Slider Slider(Transform parent, float min, float max, UnityAction<float> onChanged, UnityAction onRelease, float height = 22)
        {
            var go = new GameObject("slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var bg = new GameObject("bg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.35f); bgRt.anchorMax = new Vector2(1, 0.65f); bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f);
            var fillArea = new GameObject("fill area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0, 0.35f); faRt.anchorMax = new Vector2(1, 0.65f); faRt.offsetMin = faRt.offsetMax = Vector2.zero;
            var fill = new GameObject("fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fRt = fill.GetComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one; fRt.offsetMin = fRt.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = ArmedColor;
            var handleArea = new GameObject("handle area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one; haRt.offsetMin = new Vector2(8, 0); haRt.offsetMax = new Vector2(-8, 0);
            var handle = new GameObject("handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var hRt = handle.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(16, 0);
            handle.GetComponent<Image>().color = TextColor;
            var s = go.GetComponent<Slider>();
            s.fillRect = fRt; s.handleRect = hRt; s.targetGraphic = handle.GetComponent<Image>();
            s.minValue = min; s.maxValue = max; s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.onValueChanged.AddListener(onChanged);
            var trigger = go.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            entry.callback.AddListener(_ => onRelease());
            trigger.triggers.Add(entry);
            return s;
        }

        public static InputField InputField(Transform parent, string placeholder, float height = 26)
        {
            var go = new GameObject("input", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var text = Label(go.transform, "", 14, TextAnchor.MiddleLeft, height);
            var trt = text.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(6, 0); trt.offsetMax = new Vector2(-6, 0);
            var ph = Label(go.transform, placeholder, 14, TextAnchor.MiddleLeft, height);
            ph.color = new Color(0.6f, 0.6f, 0.6f);
            ph.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var prt = ph.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = new Vector2(6, 0); prt.offsetMax = new Vector2(-6, 0);
            var f = go.GetComponent<InputField>();
            f.textComponent = text; f.placeholder = ph; f.targetGraphic = go.GetComponent<Image>();
            return f;
        }

        public static Image Box(Transform parent, Color color)
        {
            var go = new GameObject("box", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        public static void Stretch(RectTransform rt, float margin = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin); rt.offsetMax = new Vector2(-margin, -margin);
        }
    }
}
