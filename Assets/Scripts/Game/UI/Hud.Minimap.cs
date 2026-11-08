using System.Collections.Generic;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// The radar at the top right: what's around the castaway within 45 m, north up. Green for things to gather,
    /// brown for camp, gold for the guide's goal (clamped to the rim when it's further), red for enemies, Biscuit in
    /// amber, the castaway's arrow in the middle.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _mini, _miniDots, _miniMe;
        readonly List<Image> _dots2 = new List<Image>();
        const float MiniR = 130, MiniMetres = 45;

        void BuildMinimap(RectTransform safe)
        {
            _mini = UIKit.Img("Minimap", safe, UIKit.Circle, new Color(0.12f, 0.16f, 0.14f, 0.7f)).rectTransform;
            _mini.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -20), new Vector2(MiniR * 2, MiniR * 2));
            var rim = UIKit.Img("Rim", _mini, UIKit.RingSprite, new Color(0.85f, 0.88f, 0.85f, 0.85f)); rim.rectTransform.Fill(-4);
            var inner = UIKit.Img("Inner", _mini, UIKit.RingSprite, new Color(1, 1, 1, 0.25f)); inner.rectTransform.Fill(MiniR * 0.45f);
            foreach (var a in new[] { 0f, 90f })
            {
                var l = UIKit.Img("Line", _mini, null, new Color(1, 1, 1, 0.2f));
                l.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MiniR * 2, 2));
                l.rectTransform.localRotation = Quaternion.Euler(0, 0, a + 45);
            }
            _miniDots = UIKit.Rect("Dots", _mini); _miniDots.Fill();
            _miniMe = UIKit.Img("Me", _mini, UIKit.Symbol("arrow"), Color.white).rectTransform;
            _miniMe.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
        }

        void UpdateMinimap()
        {
            var p = Player.Instance;
            if (p == null || _mini == null) return;
            var me = p.transform.position;
            _miniMe.localRotation = Quaternion.Euler(0, 0, -p.transform.eulerAngles.y);
            int n = 0;
            Image Dot(Vector2 at, Color c, float size, Sprite sprite = null)
            {
                while (_dots2.Count <= n) { var d = UIKit.Img("Dot", _miniDots, UIKit.Circle, Color.white); _dots2.Add(d); }
                var img = _dots2[n++];
                img.gameObject.SetActive(true);
                img.sprite = sprite ?? UIKit.Circle;
                img.color = c;
                img.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, new Vector2(size, size));
                img.rectTransform.localRotation = Quaternion.identity;
                return img;
            }
            Vector2 To(Vector3 w, out bool inside)
            {
                var d = new Vector2(w.x - me.x, w.z - me.z) / MiniMetres * (MiniR - 10);
                inside = d.magnitude <= MiniR - 10;
                if (!inside) d = d.normalized * (MiniR - 12);
                return d;
            }
            bool inside2;
            foreach (var sp in Spot.All)
            {
                if (!sp.Active) continue;
                var at = To(sp.Pos, out inside2);
                if (!inside2) continue;
                bool camp = sp is SiteSpot || sp is PieceView;
                Dot(at, camp ? new Color(0.85f, 0.6f, 0.3f) : sp is LootSpot ? new Color(0.9f, 0.85f, 0.55f) : sp.Blocked ? new Color(0.6f, 0.6f, 0.4f, 0.7f) : new Color(0.45f, 0.9f, 0.4f), camp ? 14 : 9);
            }
            foreach (var e in Enemy.All)
            {
                if (!e.Alive) continue;
                var at = To(e.transform.position, out inside2);
                if (inside2) Dot(at, new Color(1f, 0.25f, 0.2f), 14);
            }
            if (Pet.Instance != null) { var at = To(Pet.Instance.transform.position, out inside2); if (inside2) Dot(at, new Color(1f, 0.75f, 0.3f), 12); }
            var goal = GuideArrow.GoalPosition(me);
            if (goal.HasValue)
            {
                var at = To(goal.Value, out inside2);
                var g = Dot(at, new Color(1f, 0.82f, 0.25f), 26, UIKit.Symbol("pin"));
            }
            for (int i = n; i < _dots2.Count; i++) _dots2[i].gameObject.SetActive(false);
            _miniMe.SetAsLastSibling();
        }
    }
}
