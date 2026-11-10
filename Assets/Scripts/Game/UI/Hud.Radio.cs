using System.Linq;
using System.Collections.Generic;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// The ship's radio: an amber dial with long, medium and short wave scales and a red needle, the signals it can
    /// pick up, and the conversation once tuned in. Untuned, the scope shows white noise.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _radio, _needle, _signalList, _chat, _scope;
        readonly List<RectTransform> _scopeBars = new List<RectTransform>();
        bool _radioOpen;
        SignalDef _tuning;
        float _needleAt = 0.05f, _tuneT;
        int _linesShown;
        float _lineT;

        void BuildRadio()
        {
            _radio = UIKit.Img("Radio", _root, null, new Color(0.18f, 0.2f, 0.19f, 1f)).rectTransform;
            _radio.Fill();
            _radio.GetComponent<Image>().raycastTarget = true;
            var left = UIKit.Img("Left", _radio, null, new Color(0.24f, 0.26f, 0.24f, 1)).rectTransform;
            left.Place(new Vector2(0.5f, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(720, 900));
            var right = UIKit.Img("Right", _radio, null, new Color(0.21f, 0.24f, 0.23f, 1)).rectTransform;
            right.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(-30, 0), new Vector2(700, 900));

            // The set: title and dial.
            var set = UIKit.Img("Set", left, null, new Color(0.11f, 0.12f, 0.12f)).rectTransform;
            set.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(660, 330));
            var title = UIKit.Label("Title", set, "RADIO", 56, new Color(0.92f, 0.9f, 0.85f), TextAnchor.UpperCenter);
            title.font = Resources.Load<Font>("Fonts/BarlowCondensed-Bold"); title.rectTransform.Fill(); title.rectTransform.offsetMax = new Vector2(0, -14);
            var dial = UIKit.Img("Dial", set, DialSprite(), Color.white).rectTransform;
            dial.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(610, 190));
            string[] rows = { "LW   160   180   200   220   250   280  kHz", "MW   500   700   1000   1300   1600  kHz", "SW     3     4     7     9    11    12    14  MHz" };
            for (int i = 0; i < 3; i++)
            {
                var t = UIKit.Label("Scale", dial, rows[i], 26, new Color(0.35f, 0.18f, 0.08f, 0.9f), TextAnchor.UpperLeft, false);
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(14, 0); t.rectTransform.offsetMax = new Vector2(0, -14 - i * 38);
            }
            for (int i = 0; i < 70; i++)
            {
                var tick = UIKit.Img("Tick", dial, null, new Color(0.2f, 0.1f, 0.05f, 0.85f));
                tick.rectTransform.Place(new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(40 + i * 7.7f, 18), new Vector2(3, i % 5 == 0 ? 34 : 20));
            }
            _needle = UIKit.Img("Needle", dial, null, new Color(0.75f, 0.05f, 0.05f)).rectTransform;
            _needle.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30, 0), new Vector2(8, 190));
            var tri = UIKit.Label("Pointer", set, "▲", 30, new Color(0.9f, 0.9f, 0.9f), TextAnchor.LowerCenter, false);
            tri.rectTransform.Fill(); tri.rectTransform.offsetMin = new Vector2(0, 8);

            _signalList = UIKit.Rect("Signals", left).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -390), new Vector2(660, 480));

            // The scope and the conversation.
            _scope = UIKit.Img("Scope", right, null, new Color(0.12f, 0.13f, 0.13f)).rectTransform;
            _scope.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(620, 200));
            var mid = UIKit.Img("Mid", _scope, null, new Color(1, 1, 1, 0.25f));
            mid.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(620, 2));
            for (int i = 0; i < 96; i++)
            {
                var b = UIKit.Img("Bar", _scope, null, new Color(0.75f, 0.78f, 0.75f, 0.85f)).rectTransform;
                b.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(12 + i * 6.3f, 0), new Vector2(2.5f, 4));
                _scopeBars.Add(b);
            }
            _chat = UIKit.Rect("Chat", right).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -270), new Vector2(620, 600));

            var close = UIKit.Button("Close", _radio, "X", new Color(0, 0, 0, 0), () => { _radioOpen = false; _dirty = true; }, 72);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -30), new Vector2(110, 110));
            var cl = UIKit.Label("CLOSE", _radio, "CLOSE", 40, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, false);
            cl.rectTransform.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-115, -230), new Vector2(200, 50));
            cl.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            _radio.gameObject.SetActive(false);
        }

        /// <summary>The amber backlit dial face.</summary>
        static Sprite DialSprite()
        {
            const int w = 256, h = 80;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    float glow = 1 - Mathf.Pow(Mathf.Abs(u - 0.45f) * 1.6f, 2) * 0.5f - Mathf.Abs(v - 0.5f) * 0.4f;
                    var c = Color.Lerp(new Color(0.7f, 0.35f, 0.1f), new Color(0.98f, 0.68f, 0.3f), glow);
                    if ((y == 26 || y == 52) && x > 20) c *= 0.85f;
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        public void OpenRadio()
        {
            if (!G.Session.Built("radio")) { Toast("Rebuild the ship's radio at camp first"); return; }
            _radioOpen = true;
            _tuning = null;
            _linesShown = 0;
            Sfx.Play("static");
            RefreshRadio();
        }

        void RefreshRadio()
        {
            _radio.gameObject.SetActive(_radioOpen);
            if (!_radioOpen) return;
            Clear(_signalList);
            var signals = G.Session.Signals;
            for (int i = 0; i < signals.Length; i++)
            {
                var sig = signals[i];
                bool on = _tuning == sig;
                var row = UIKit.Button("Signal", _signalList, null, new Color(0.11f, 0.12f, 0.12f), () => Tune(sig));
                ((RectTransform)row.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -i * 104), new Vector2(660, 92));
                if (on || !G.Session.Heard(sig))
                {
                    var glow = UIKit.Img("Glow", row.transform, UIKit.Round, new Color(0.2f, 0.75f, 0.75f, on ? 0.9f : 0.5f + 0.4f * Mathf.Sin(UnityEngine.Time.time * 4)));
                    glow.rectTransform.Fill(-4); glow.type = Image.Type.Sliced; glow.fillCenter = false;
                }
                var t = UIKit.Label("Name", row.transform, sig.Title + "   <size=24><color=#9AA>" + sig.Freq + "</color></size>", 34, G.Session.Heard(sig) ? Color.white : UIKit.Amber, TextAnchor.MiddleLeft, false);
                t.supportRichText = true;
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(26, 0);
                var arrow = UIKit.Label("Arrow", row.transform, ">", 40, Color.white, TextAnchor.MiddleRight, false);
                arrow.rectTransform.Fill(); arrow.rectTransform.offsetMax = new Vector2(-26, 0);
            }
            // The calls already answered, below the live ones: who was saved and where.
            int k = signals.Length;
            foreach (var done in World.Signals.Where(x => G.Session.Rescued(x)))
            {
                var row = UIKit.Img("Rescued", _signalList, null, new Color(0.11f, 0.12f, 0.12f, 0.5f)).rectTransform;
                row.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -k * 104), new Vector2(660, 92));
                var at = World.Location(done.Location);
                var t = UIKit.Label("Name", row, done.Npc + "   <size=24><color=#9AA>" + (at != null ? at.Name : "") + "</color></size>", 32, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleLeft, false);
                t.supportRichText = true;
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(26, 0);
                var ok = UIKit.Label("Done", row, "RESCUED", 26, new Color(0.45f, 0.85f, 0.5f), TextAnchor.MiddleRight, false);
                ok.rectTransform.Fill(); ok.rectTransform.offsetMax = new Vector2(-26, 0);
                k++;
            }
            RefreshChat();
        }

        void Tune(SignalDef s)
        {
            _tuning = s;
            _tuneT = 0;
            _linesShown = G.Session.Heard(s) ? s.Chat.Length : 0;
            _lineT = 0;
            Sfx.Play("static");
            RefreshRadio();
        }

        void RefreshChat()
        {
            Clear(_chat);
            if (_tuning == null)
            {
                var ss = G.Session;
                var next = World.Signals.Where(x => !ss.Rescued(x) && x.Level > ss.State.level).OrderBy(x => x.Level).FirstOrDefault();
                if (ss.Signals.Length > 0) Line(0, "YOU CAN HEAR ONLY WHITE NOISE", "Tap a signal on the left to tune to its frequency");
                else if (next != null) Line(0, "YOU CAN HEAR ONLY WHITE NOISE", "No one is calling... yet. The next call comes through at level " + next.Level + ".");
                else Line(0, "THE AIRWAVES ARE QUIET", "Everyone who called for help has been rescued. Well done, sailor.");
                return;
            }
            var s = _tuning;
            float y = 0;
            for (int i = 0; i < _linesShown && i < s.Chat.Length; i++)
                y = Line(y, i % 2 == 0 ? (G.Session.Heard(s) || i > 0 ? s.Npc.ToUpper() : "SURVIVOR") : "YOU", s.Chat[i]);
            if (_linesShown >= s.Chat.Length)
            {
                string need = string.Join(", ", System.Array.ConvertAll(s.Need, n => n.n + " " + Content.NameOf(n.id)));
                y = Line(y + 10, "NEW RESCUE", "Bring " + need + " to " + s.Npc + " at " + World.Location(s.Location).Name + ".", UIKit.Amber);
                var b = UIKit.Button("Show", _chat, "SHOW ON MAP", new Color(0.15f, 0.55f, 0.55f), () =>
                {
                    _radioOpen = false; _dirty = true;
                    if (!MapOpen) OpenMap();
                    Select(World.Location(s.Location));
                }, 32);
                ((RectTransform)b.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y - 10), new Vector2(330, 76));
            }
        }

        float Line(float y, string who, string text, Color? whoColor = null)
        {
            var a = UIKit.Label("Who", _chat, who, 34, whoColor ?? Color.white, TextAnchor.UpperLeft);
            a.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y), new Vector2(620, 44));
            var b = UIKit.Label("Text", _chat, text, 30, new Color(1, 1, 1, 0.75f), TextAnchor.UpperLeft, false);
            b.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y - 40), new Vector2(620, 90));
            int lines = Mathf.CeilToInt(text.Length / 40f);
            return y + 54 + lines * 36;
        }

        void UpdateRadio()
        {
            if (!_radioOpen) return;
            float goal = _tuning != null ? _tuning.Dial : 0.05f;
            _needleAt = Mathf.MoveTowards(_needleAt, goal, UnityEngine.Time.unscaledDeltaTime * 0.6f);
            _needle.anchoredPosition = new Vector2(30 + _needleAt * 560 + (_tuning == null ? Mathf.PerlinNoise(UnityEngine.Time.time, 0) * 6 : 0), 0);
            bool locked = _tuning != null && Mathf.Abs(_needleAt - goal) < 0.002f;
            // Scope: hiss until locked on, then a voice's bursts.
            float t = UnityEngine.Time.unscaledTime;
            for (int i = 0; i < _scopeBars.Count; i++)
            {
                float u = i / (float)_scopeBars.Count;
                float h = locked
                    ? Mathf.Abs(Mathf.Sin(u * 40 + t * 12)) * (0.2f + Mathf.PerlinNoise(u * 4, t * 2.5f) * Mathf.Exp(-Mathf.Pow((u - 0.5f) * 3.5f, 2)) * 1.6f) * 150
                    : Mathf.Abs(Mathf.Sin(u * 18 + t * 6)) * (0.2f + Random.value * 0.35f) * 90;
                _scopeBars[i].sizeDelta = new Vector2(2.5f, Mathf.Max(3, h));
            }
            if (locked && _linesShown < _tuning.Chat.Length)
            {
                _lineT -= UnityEngine.Time.unscaledDeltaTime;
                if (_lineT <= 0)
                {
                    _lineT = 1.6f;
                    _linesShown++;
                    Sfx.Play("beep");
                    if (_linesShown >= _tuning.Chat.Length) G.Session.Tune(_tuning);
                    RefreshChat();
                }
            }
        }

        // ------------------------------------------------------------------ survivors speak

        Text _say;
        float _sayT;

        /// <summary>A line of dialogue across the bottom of the screen.</summary>
        public void Say(string who, string line)
        {
            if (_say == null)
            {
                var bg = UIKit.Img("Say", _root, UIKit.Round, UIKit.Ink);
                bg.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(1300, 150));
                _say = UIKit.Label("Text", bg.transform, "", 34, Color.white, TextAnchor.MiddleCenter, false);
                _say.supportRichText = true;
                _say.rectTransform.Fill(24);
            }
            _say.text = "<color=#FFC24A>" + who.ToUpper() + "</color>\n" + line;
            _say.transform.parent.gameObject.SetActive(true);
            _sayT = 6;
        }

        void UpdateSay()
        {
            if (_say == null || _sayT <= 0) return;
            _sayT -= UnityEngine.Time.unscaledDeltaTime;
            if (_sayT <= 0) _say.transform.parent.gameObject.SetActive(false);
        }
    }
}
