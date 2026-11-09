using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// Full-screen key art with the title, a progress bar, a rotating tip and the Rushing Technologies badge while an
    /// island builds (at launch and when stepping ashore after a voyage). Lives on its own top-most canvas.
    /// The studio logo is Resources/UI/Brand/rushingtech (any image); until it exists a drawn text mark stands in.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        static readonly string[] Tips =
        {
            "Walk into a green ring and the work happens: chop, mine, gather.",
            "Night brings raiders. Light a fire and build walls before dark.",
            "Your backpack drops where you fall. Go back for it!",
            "Cook meat at the campfire: raw food barely fills you.",
            "Energy refills while you rest. Plan voyages to make it count.",
            "Supply crates wash up after storms. Check the beach.",
            "Upgrade the workbench to unlock stronger tools and weapons.",
            "Listen to the radio: someone out there needs help.",
        };

        public static LoadingScreen Current { get; private set; }
        /// <summary>True while a loading screen is up (stories and popups wait for it).</summary>
        public static bool Active { get { return Current != null; } }

        RectTransform _fill;
        Text _status;
        CanvasGroup _group;
        float _shown, _min;

        public static LoadingScreen Show(string status, float minSeconds = 2.4f)
        {
            if (Current != null) { Current.SetProgress(0, status); return Current; }
            var go = new GameObject("Loading Screen", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Hud.RefW, Hud.RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1;
            go.AddComponent<GraphicRaycaster>(); // swallow taps while loading
            var ls = go.AddComponent<LoadingScreen>();
            ls._min = minSeconds;
            ls.Build(status);
            Current = ls;
            return ls;
        }

        void Build(string status)
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            var root = (RectTransform)transform;
            var bg = UIKit.Img("Background", root, null, new Color(0.03f, 0.09f, 0.12f));
            bg.rectTransform.Fill();
            bg.raycastTarget = true;

            var art = Resources.Load<Texture2D>("UI/LoadingArt");
            if (art != null)
            {
                var raw = UIKit.Rect("Art", root).gameObject.AddComponent<RawImage>();
                raw.texture = art;
                raw.raycastTarget = false;
                raw.rectTransform.Fill();
                var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // cover the screen, crop the overflow
                fit.aspectRatio = art.width / (float)art.height;
                raw.gameObject.AddComponent<SlowZoom>();
            }

            // Darken the top for the title and the bottom for the bar, tip and badge.
            var top = UIKit.Img("TopShade", root, Fade(false), new Color(0, 0, 0, 0.7f));
            top.rectTransform.anchorMin = new Vector2(0, 0.62f); top.rectTransform.anchorMax = Vector2.one;
            top.rectTransform.offsetMin = top.rectTransform.offsetMax = Vector2.zero;
            var bottom = UIKit.Img("BottomShade", root, Fade(true), new Color(0, 0, 0, 0.85f));
            bottom.rectTransform.anchorMin = Vector2.zero; bottom.rectTransform.anchorMax = new Vector2(1, 0.4f);
            bottom.rectTransform.offsetMin = bottom.rectTransform.offsetMax = Vector2.zero;

            var title = UIKit.Outlined(UIKit.Label("Title", root, "LAST TIDE", 150, new Color(1f, 0.93f, 0.78f), TextAnchor.UpperCenter), 4f);
            title.rectTransform.Fill(); title.rectTransform.offsetMax = new Vector2(0, -60);
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -8);
            var sub = UIKit.Outlined(UIKit.Label("Sub", root, "SHIPWRECK  SURVIVAL", 40, UIKit.Amber, TextAnchor.UpperCenter), 2f);
            sub.rectTransform.Fill(); sub.rectTransform.offsetMax = new Vector2(0, -232);

            var track = UIKit.Img("Track", root, UIKit.Round, new Color(0.02f, 0.05f, 0.07f, 0.9f));
            track.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(1000, 30));
            var fill = UIKit.Img("Fill", track.transform, UIKit.Round, UIKit.Amber);
            _fill = fill.rectTransform;
            _fill.anchorMin = Vector2.zero; _fill.anchorMax = new Vector2(0, 1);
            _fill.offsetMin = new Vector2(4, 4); _fill.offsetMax = new Vector2(-4, -4);

            _status = UIKit.Outlined(UIKit.Label("Status", root, status, 32, new Color(1f, 0.95f, 0.85f), TextAnchor.MiddleCenter), 2f);
            _status.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 196), new Vector2(1200, 46));
            var tip = UIKit.Label("Tip", root, "TIP: " + Tips[Random.Range(0, Tips.Length)], 28, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter, false);
            tip.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 92), new Vector2(1300, 40));

            Brand(root);
            _shown = Time.realtimeSinceStartup;
        }

        /// <summary>The studio badge, bottom right: the logo if it's in the project, else the site's text mark.</summary>
        static void Brand(RectTransform root)
        {
            var logo = Resources.Load<Texture2D>("UI/Brand/rushingtech");
            if (logo != null)
            {
                // The neon logo (cut out of its black background; it carries the name) in the bottom-right corner.
                float h = 250, w = h * logo.width / (float)logo.height;
                var raw = UIKit.Rect("Brand", root).gameObject.AddComponent<RawImage>();
                raw.texture = logo;
                raw.raycastTarget = false;
                raw.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 26), new Vector2(w, h));
                var by = UIKit.Outlined(UIKit.Label("By", root, "A GAME BY", 22, new Color(1, 1, 1, 0.75f), TextAnchor.LowerCenter, false), 1.5f);
                by.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 26 + h), new Vector2(w, 30));
                return;
            }
            // Stand-in mark (the red status dot and name from rushingtech's site) if the logo file is missing.
            var box = UIKit.Rect("Brand", root);
            box.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-44, 28), new Vector2(460, 96));
            var dot = UIKit.Img("Dot", box, Dot(), new Color(0.89f, 0.16f, 0.22f));
            dot.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(4, 8), new Vector2(22, 22));
            var name = UIKit.Outlined(UIKit.Label("Name", box, "RUSHING TECHNOLOGIES", 30, Color.white, TextAnchor.UpperLeft), 1.5f);
            name.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(40, 10), new Vector2(420, 40));
        }

        public void SetProgress(float p, string status)
        {
            _fill.anchorMax = new Vector2(Mathf.Clamp01(p), 1);
            if (status != null) _status.text = status;
        }

        /// <summary>Fades out after the minimum display time (so the logo and tip can be read) and destroys itself.</summary>
        public IEnumerator Finish(string status = "Ashore!")
        {
            SetProgress(1, status);
            while (Time.realtimeSinceStartup - _shown < _min) yield return null;
            for (float t = 1; t > 0; t -= Time.unscaledDeltaTime * 2.2f)
            {
                _group.alpha = t;
                yield return null;
            }
            if (Current == this) Current = null;
            Destroy(gameObject);
        }

        void OnDestroy() { if (Current == this) Current = null; }

        static Sprite _fadeUp, _fadeDown, _dot;
        /// <summary>A vertical alpha ramp: opaque at the screen edge, clear toward the middle.</summary>
        static Sprite Fade(bool bottom)
        {
            var cached = bottom ? _fadeUp : _fadeDown;
            if (cached != null) return cached;
            var t = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                float k = bottom ? 1 - y / 63f : y / 63f;
                for (int x = 0; x < 4; x++) t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(k, 1.3f)));
            }
            t.Apply();
            var s = Sprite.Create(t, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f));
            if (bottom) _fadeUp = s; else _fadeDown = s;
            return s;
        }

        static Sprite Dot()
        {
            if (_dot != null) return _dot;
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - 1 - d)));
                }
            t.Apply();
            return _dot = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        sealed class SlowZoom : MonoBehaviour
        {
            float _t;
            void Update() { _t += Time.unscaledDeltaTime; transform.localScale = Vector3.one * (1.02f + Mathf.Min(_t, 15f) * 0.004f); }
        }
    }
}
