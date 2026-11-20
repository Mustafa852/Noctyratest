using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Noctyra
{
    /// <summary>Sahne düzenlemeden, tamamen kodla uGUI üretmek için küçük yardımcılar.</summary>
    public static class UIKit
    {
        public static readonly Color Ink = new Color(0.91f, 0.89f, 0.94f);
        public static readonly Color Muted = new Color(0.66f, 0.63f, 0.75f);
        public static readonly Color Crimson = new Color(0.76f, 0.10f, 0.22f);
        public static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.14f, 0.88f);
        public static readonly Color ButtonColor = new Color(0.24f, 0.16f, 0.40f, 1f);
        public static readonly Color GoodColor = new Color(0.25f, 0.55f, 0.40f, 1f);

        static Font font;

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                return font;
            }
        }

        public static string Hex(Color c)
        {
            return ColorUtility.ToHtmlStringRGB(c);
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Stretch(RectTransform rt)
        {
            Anchor(rt, 0f, 0f, 1f, 1f);
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = NewRect("Label", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button MakeButton(Transform parent, string label, Color color, UnityAction onClick, int fontSize = 28)
        {
            var rt = NewRect("Button_" + label, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var txt = Label(rt, label, fontSize, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(txt.rectTransform);
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 14;
            txt.resizeTextMaxSize = fontSize;

            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                Object.Destroy(c);
            }
        }

        // ---------------------------------------------------------- Prosedürel sprite'lar

        public static Sprite Disc(int size, bool crescent)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var c = new Vector2(size / 2f, size / 2f);
            float r = size * 0.42f;
            var c2 = c + new Vector2(size * 0.14f, size * 0.06f);
            float r2 = r * 0.92f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x, y);
                    float a = Mathf.Clamp01((r - Vector2.Distance(p, c)) / 3f + 0.5f);
                    if (crescent)
                    {
                        float cut = Mathf.Clamp01((r2 - Vector2.Distance(p, c2)) / 3f + 0.5f);
                        a = Mathf.Clamp01(a - cut);
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Sprite Glow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var c = new Vector2(size / 2f, size / 2f);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / r;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Altta opak, üstte saydam dikey gradyan (ufuk parıltısı).</summary>
        public static Sprite VerticalGradient()
        {
            var tex = new Texture2D(2, 128, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 128; y++)
            {
                float a = Mathf.Pow(1f - y / 127f, 2f);
                for (int x = 0; x < 2; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 128), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
