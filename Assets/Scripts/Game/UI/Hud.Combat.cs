using System.Collections.Generic;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// What a fight tells the player: a yellow "!" when something notices the castaway, a red "!" while it gathers
    /// itself to strike (its ring fills on the ground), a grey "?" when sneaking is all that kept it from noticing, a
    /// red arc pointing at whatever just landed a blow, a pulsing red edge and DANGER when more than one is after
    /// them, and the held weapon's condition under its button (with a warning before it breaks).
    /// </summary>
    public sealed partial class Hud
    {
        readonly List<Text> _marks = new List<Text>();
        Image _hurtArc, _danger, _condBack, _condFill;
        Text _dangerText;
        float _hurtT;
        Vector3 _hurtFrom;
        int _condSlot = -1;
        float _condLast = 1;

        /// <summary>A word or a number floating up from a world point ("DODGED", "-9").</summary>
        public void Pop(Vector3 world, string text, Color color)
        {
            var rt = UIKit.Rect("Pop", _worldLayer).Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 60));
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            var t = UIKit.Outlined(UIKit.Label("Text", rt, text, 42, color, TextAnchor.MiddleCenter), 2.5f);
            t.rectTransform.Fill();
            _pops.Add(new PopUp { Rt = rt, World = world, Life = 0.9f, Group = g });
        }

        /// <summary>The castaway was struck by something at <paramref name="from"/>.</summary>
        public void HurtFrom(Vector3 from) { _hurtFrom = from; _hurtT = 0.7f; }

        void BuildCombat()
        {
            _danger = UIKit.Img("Danger", _islandUi, Vignette(), new Color(0.85f, 0.05f, 0.03f, 0));
            _danger.rectTransform.Fill(); _danger.raycastTarget = false;
            _danger.transform.SetAsFirstSibling(); // under the buttons and meters
            _dangerText = UIKit.Outlined(UIKit.Label("DangerText", _islandUi, "", 36, new Color(1f, 0.4f, 0.32f), TextAnchor.MiddleCenter), 2.5f);
            _dangerText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -230), new Vector2(700, 50));

            _hurtArc = UIKit.Img("HurtArc", _worldLayer, UIKit.RingSprite, new Color(1f, 0.15f, 0.1f, 0));
            _hurtArc.raycastTarget = false;
            _hurtArc.rectTransform.Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330, 330));
            _hurtArc.type = Image.Type.Filled; _hurtArc.fillMethod = Image.FillMethod.Radial360;
            _hurtArc.fillOrigin = 2; _hurtArc.fillAmount = 0.16f; // a short arc, turned to face the blow

            // The held weapon's condition, a thin bar under its button.
            _condBack = UIKit.Img("Condition", _weaponSlot, UIKit.Round, new Color(0, 0, 0, 0.6f));
            _condBack.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(120, 14));
            _condBack.raycastTarget = false;
            _condFill = UIKit.Img("Fill", _condBack.transform, UIKit.Round, UIKit.Green);
            _condFill.rectTransform.anchorMin = Vector2.zero; _condFill.rectTransform.anchorMax = Vector2.one;
            _condFill.rectTransform.offsetMin = new Vector2(2, 2); _condFill.rectTransform.offsetMax = new Vector2(-2, -2);
            _condFill.raycastTarget = false;
        }

        void UpdateCombat()
        {
            var p = Player.Instance;
            var s = G.Session;
            int used = 0, after = 0;
            if (p != null && !MapOpen)
            {
                var me = p.transform.position;
                foreach (var e in Enemy.All)
                {
                    if (!e.Alive) continue;
                    if (e.Angry && Vector3.Distance(e.transform.position, me) < 25f) after++;
                    string mark = null; Color col = Color.white; float size = 1;
                    if (e.Windup01 >= 0) { mark = "!"; col = new Color(1f, 0.22f, 0.15f); size = 1.15f + 0.25f * e.Windup01; }
                    else if (e.Angry && e.SinceAlert < 0.9f) { mark = "!"; col = UIKit.Amber; size = e.SinceAlert < 0.12f ? Mathf.Lerp(0.5f, 1.3f, e.SinceAlert / 0.12f) : 1.3f - 0.3f * Mathf.Clamp01((e.SinceAlert - 0.12f) / 0.3f); }
                    else if (e.Suspicious) { mark = "?"; col = new Color(0.82f, 0.85f, 0.85f, 0.85f); }
                    if (mark == null) continue;
                    var c = ToCanvas(e.transform.position + Vector3.up * 2.15f);
                    if (c.z < 0) continue;
                    var t = MarkAt(used++);
                    t.text = mark; t.color = col;
                    t.rectTransform.anchoredPosition = c;
                    t.rectTransform.localScale = Vector3.one * size;
                }

                // The arc: centred on the castaway, its middle pointing at the attacker on screen.
                if (_hurtT > 0)
                {
                    _hurtT -= Time.deltaTime;
                    var at = ToCanvas(me + Vector3.up * 1f);
                    var from = ToCanvas(_hurtFrom + Vector3.up * 1f);
                    var dir = (Vector2)(from - at);
                    float ang = dir.sqrMagnitude > 1 ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : 90;
                    _hurtArc.rectTransform.anchoredPosition = at;
                    // Radial fill starts at the top and runs clockwise: turn it so the arc's middle sits on 'ang'.
                    _hurtArc.rectTransform.localRotation = Quaternion.Euler(0, 0, ang - 90 + _hurtArc.fillAmount * 180);
                    _hurtArc.color = new Color(1f, 0.15f, 0.1f, Mathf.Clamp01(_hurtT / 0.35f) * 0.9f);
                }
                else if (_hurtArc.color.a > 0) _hurtArc.color = Color.clear;
            }
            for (int i = used; i < _marks.Count; i++) _marks[i].gameObject.SetActive(false);

            // DANGER: two or more after the castaway at once.
            bool danger = after >= 2 && s.State.health > 0;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6);
            _danger.color = new Color(0.85f, 0.05f, 0.03f, danger ? 0.22f + 0.2f * pulse : Mathf.MoveTowards(_danger.color.a, 0, Time.deltaTime));
            _dangerText.text = danger ? "DANGER: " + after + " ENEMIES" : "";
            _dangerText.color = new Color(1f, 0.4f, 0.32f, 0.7f + 0.3f * pulse);

            // Condition of what's held (fists and unbreakables: no bar).
            int slot = s.WeaponSlot();
            var d = slot >= 0 ? Content.Item(s.State.backpack[slot].id) : null;
            bool show = d != null && d.Durability > 0;
            _condBack.gameObject.SetActive(show);
            if (show)
            {
                float f = Mathf.Clamp01(1 - s.State.backpack[slot].wear / (float)d.Durability);
                _condFill.rectTransform.anchorMax = new Vector2(f, 1);
                _condFill.color = f > 0.5f ? UIKit.Green : f > 0.2f ? UIKit.Amber : UIKit.Red;
                if (slot == _condSlot && _condLast > 0.15f && f <= 0.15f) Toast(d.Name + " is about to break");
                _condSlot = slot; _condLast = f;
            }
            else _condSlot = -1;
        }

        Text MarkAt(int i)
        {
            while (_marks.Count <= i)
            {
                var t = UIKit.Outlined(UIKit.Label("Mark", _worldLayer, "", 80, Color.white, TextAnchor.LowerCenter), 3f);
                t.rectTransform.Place(Vector2.zero, new Vector2(0.5f, 0), Vector2.zero, new Vector2(90, 90));
                _marks.Add(t);
            }
            _marks[i].gameObject.SetActive(true);
            return _marks[i];
        }

        static Sprite _vignette;

        /// <summary>Clear in the middle, solid at the edges (stretched over the screen).</summary>
        static Sprite Vignette()
        {
            if (_vignette != null) return _vignette;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1;
                    float d = Mathf.Sqrt(u * u * 0.8f + v * v * 1.1f);
                    float a = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.72f, 1.25f, d));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            return _vignette = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
