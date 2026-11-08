using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>uGUI helpers: rounded panels, labels, buttons and painted icons, all made from code.</summary>
    public static class UIKit
    {
        static Font _bold, _body;
        static Sprite _round, _circle, _ring;
        static readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();

        public static Font Bold { get { return _bold ?? (_bold = Resources.Load<Font>("Fonts/BarlowCondensed-Bold")); } }
        public static Font Body { get { return _body ?? (_body = Resources.Load<Font>("Fonts/BarlowSemiCondensed-SemiBold")); } }

        public static readonly Color Ink = new Color(0.08f, 0.1f, 0.1f, 0.82f);
        public static readonly Color Panel = new Color(0.1f, 0.14f, 0.15f, 0.92f);
        public static readonly Color Cream = new Color(0.98f, 0.95f, 0.86f);
        public static readonly Color Green = new Color(0.36f, 0.86f, 0.32f);
        public static readonly Color Amber = new Color(1f, 0.74f, 0.25f);
        public static readonly Color Red = new Color(0.9f, 0.25f, 0.22f);
        public static readonly Color Sand = new Color(0.93f, 0.82f, 0.6f);

        /// <summary>White rounded rectangle, 9-sliced.</summary>
        public static Sprite Round
        {
            get
            {
                if (_round != null) return _round;
                const int n = 64, r = 20;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r))), dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r)));
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(r - d + 0.5f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px); tex.Apply();
                _round = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
                return _round;
            }
        }

        public static Sprite Circle { get { return _circle ?? (_circle = Paint("circle", 128, (u, v) => Disc(u, v, 0.5f, 0.5f, 0.48f))); } }
        public static Sprite RingSprite { get { return _ring ?? (_ring = Paint("ring", 128, (u, v) => Mathf.Min(Disc(u, v, 0.5f, 0.5f, 0.48f), 1 - Disc(u, v, 0.5f, 0.5f, 0.4f)))); } }

        static float Disc(float u, float v, float cx, float cy, float r)
        {
            float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
            return Mathf.Clamp01((r - d) * 128 + 0.5f);
        }

        /// <summary>A white icon painted from a coverage function over the unit square.</summary>
        static Sprite Paint(string name, int n, System.Func<float, float, float> cover)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(cover((x + 0.5f) / n, (y + 0.5f) / n)) * 255));
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
        }

        /// <summary>HUD symbols: heart, drumstick, drop, backpack, hammer.</summary>
        public static Sprite Symbol(string id)
        {
            Sprite s;
            if (_icons.TryGetValue("sym_" + id, out s)) return s;
            var art = Resources.Load<Sprite>("UI/Icons/" + id);
            if (art != null) return _icons["sym_" + id] = art;
            System.Func<float, float, float> f;
            switch (id)
            {
                case "heart":
                    f = (u, v) =>
                    {
                        float x = (u - 0.5f) * 2.4f, y = (v - 0.45f) * 2.4f;
                        float a = x * x + y * y - 1;
                        return Mathf.Clamp01(-(a * a * a - x * x * y * y * y) * 40);
                    };
                    break;
                case "food":
                    f = (u, v) => Mathf.Max(Disc(u, v, 0.6f, 0.6f, 0.3f), Mathf.Max(Capsule(u, v, 0.45f, 0.45f, 0.2f, 0.2f, 0.07f), Mathf.Max(Disc(u, v, 0.17f, 0.24f, 0.08f), Disc(u, v, 0.24f, 0.17f, 0.08f))));
                    break;
                case "water":
                    f = (u, v) =>
                    {
                        float d = Disc(u, v, 0.5f, 0.38f, 0.28f);
                        float tri = v > 0.38f && Mathf.Abs(u - 0.5f) < (0.92f - v) * 0.52f ? 1 : 0;
                        return Mathf.Max(d, tri);
                    };
                    break;
                case "pack":
                    f = (u, v) => Mathf.Max(Box(u, v, 0.2f, 0.12f, 0.8f, 0.78f, 0.12f), 1 - Disc(u, v, 0.5f, 0.82f, 0.13f) < 0.5f && v > 0.7f ? Box(u, v, 0.35f, 0.7f, 0.65f, 0.95f, 0.08f) : 0);
                    break;
                default:
                    f = (u, v) => Disc(u, v, 0.5f, 0.5f, 0.45f);
                    break;
            }
            return _icons["sym_" + id] = Paint(id, 128, f);
        }

        static float Capsule(float u, float v, float ax, float ay, float bx, float by, float r)
        {
            var p = new Vector2(u, v); var a = new Vector2(ax, ay); var b = new Vector2(bx, by);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
            float d = Vector2.Distance(p, a + (b - a) * t);
            return Mathf.Clamp01((r - d) * 128 + 0.5f);
        }

        static float Box(float u, float v, float x0, float y0, float x1, float y1, float r)
        {
            float dx = Mathf.Max(Mathf.Max(x0 + r - u, u - (x1 - r)), 0), dy = Mathf.Max(Mathf.Max(y0 + r - v, v - (y1 - r)), 0);
            return Mathf.Clamp01((r - Mathf.Sqrt(dx * dx + dy * dy)) * 128 + 0.5f);
        }

        /// <summary>Item art from Resources/UI/Items/{id}, or null.</summary>
        public static Sprite ItemArt(string id)
        {
            Sprite s;
            if (_icons.TryGetValue(id, out s)) return s;
            s = Resources.Load<Sprite>("UI/Items/" + id);
            if (s == null)
            {
                var tex = Resources.Load<Texture2D>("UI/Items/" + id);
                if (tex != null) s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
            }
            _icons[id] = s;
            return s;
        }

        /// <summary>Fallback colour for an item without art.</summary>
        public static Color ItemColor(string id)
        {
            switch (id)
            {
                case "wood": return Palette.Wood;
                case "stone": return Tropic.Rock;
                case "fiber": return Tropic.Fern;
                case "scrap": return Palette.Steel;
                case "cloth": return Tropic.Sail;
                case "hide": return Tropic.Boar;
                case "rope": return Tropic.Rope;
                case "coconut": return Tropic.Coconut;
                case "berries": return Tropic.Berry;
                case "raw_meat": return new Color(0.8f, 0.3f, 0.3f);
                case "meat": return new Color(0.6f, 0.32f, 0.16f);
                case "crab": return new Color(0.85f, 0.35f, 0.2f);
                case "crab_cooked": return new Color(0.9f, 0.5f, 0.25f);
                case "water": return new Color(0.35f, 0.7f, 0.95f);
                case "bandage": return Color.white;
                default: return new Color(0.6f, 0.6f, 0.62f);
            }
        }

        // ------------------------------------------------------------------ widgets

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            if (sprite == Round) img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool bold = true)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = bold ? Bold : Body;
            t.fontStyle = FontStyle.Normal;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Text with a dark outline so it reads over the bright beach.</summary>
        public static Text Outlined(Text t, float px = 2.5f)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.75f);
            o.effectDistance = new Vector2(px, -px);
            return t;
        }

        public static Button Button(string name, Transform parent, string text, Color color, System.Action onClick, int size = 34)
        {
            var img = Img(name, parent, Round, color);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            b.colors = colors;
            b.onClick.AddListener(() => { Sfx.Play("click"); onClick(); });
            if (!string.IsNullOrEmpty(text))
            {
                var t = Label("Text", img.transform, text, size, Color.white, TextAnchor.MiddleCenter);
                t.rectTransform.Fill(6);
                Outlined(t, 1.5f);
            }
            return b;
        }

        /// <summary>An item picture: the art if there is some, else a coloured disc with the initial.</summary>
        public static void ItemIcon(Transform parent, string id, float size)
        {
            var art = ItemArt(id);
            if (art != null)
            {
                var img = Img("Icon", parent, art, Color.white);
                img.preserveAspect = true;
                img.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
                return;
            }
            var disc = Img("Icon", parent, Circle, ItemColor(id));
            disc.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.8f, size * 0.8f));
            var name = Content(id);
            var t = Label("Initial", disc.transform, name.Substring(0, 1), Mathf.RoundToInt(size * 0.42f), Color.white, TextAnchor.MiddleCenter);
            t.rectTransform.Fill();
            Outlined(t, 1.5f);
        }

        static string Content(string id) { return LastTide.Core.Content.NameOf(id); }
    }
}
