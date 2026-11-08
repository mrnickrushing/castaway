using System.Collections.Generic;
using System.Linq;
using LastTide.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// Everything on screen: the floating joystick, survival meters, the guide card, ring labels over the world,
    /// pickups, the backpack, the crafting and chest panels, toasts and the pass-out screen.
    /// </summary>
    public sealed partial class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }
        public const float RefW = 1920, RefH = 1080;

        Canvas _canvas;
        RectTransform _root, _worldLayer;
        public Vector2 Stick { get; private set; }
        public bool Blocking { get { return _passOut > 0; } }

        public static Hud Create()
        {
            var go = new GameObject("HUD");
            var hud = go.AddComponent<Hud>();
            Instance = hud;
            hud.Build();
            return hud;
        }

        void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1;
            gameObject.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)transform;
            var safe = UIKit.Rect("Safe", _root);
            safe.Fill();
            safe.gameObject.AddComponent<SafeArea>();
            _worldLayer = UIKit.Rect("World", safe);
            _worldLayer.Fill();
            _islandUi = UIKit.Rect("Island UI", safe);
            _islandUi.Fill();
            BuildMeters(_islandUi);
            BuildLevel(_islandUi);
            BuildJoystick(safe);
            BuildButtons(_islandUi);
            BuildMap(safe);
            BuildQuest(safe);
            BuildPanels(safe);
            BuildRadio();
            BuildOffers();
            BuildOverlays();
            BuildSplash();

            var s = G.Session;
            s.Toast += Toast;
            s.Gained += (id, n) => { if (Player.Instance != null) Pop(id, n); };
            s.QuestDone += q => { _questFlash = 1.6f; Sfx.Play("quest"); };
            s.Changed += () => _dirty = true;
            s.Died += PassOut;
            s.LevelUp += l => { _levelFlash = 2f; Sfx.Play("levelup"); };
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ meters

        Image _healthFill, _foodFill, _waterFill;
        Text _healthText, _foodText, _waterText;

        void BuildMeters(RectTransform safe)
        {
            var box = UIKit.Rect("Meters", safe).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -24), new Vector2(380, 190));
            Meter(box, 0, "heart", UIKit.Red, out _healthFill, out _healthText);
            Meter(box, 1, "food", new Color(0.95f, 0.6f, 0.2f), out _foodFill, out _foodText);
            Meter(box, 2, "water", new Color(0.3f, 0.68f, 1f), out _waterFill, out _waterText);
        }

        void Meter(RectTransform box, int row, string icon, Color c, out Image fill, out Text text)
        {
            var r = UIKit.Rect("Meter " + icon, box).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -row * 60), new Vector2(380, 52));
            var bg = UIKit.Img("Back", r, UIKit.Round, UIKit.Ink);
            bg.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(320, 30));
            fill = UIKit.Img("Fill", bg.transform, UIKit.Round, c);
            fill.rectTransform.anchorMin = new Vector2(0, 0); fill.rectTransform.anchorMax = new Vector2(1, 1);
            fill.rectTransform.offsetMin = new Vector2(4, 4); fill.rectTransform.offsetMax = new Vector2(-4, -4);
            text = UIKit.Outlined(UIKit.Label("Value", bg.transform, "100", 24, Color.white, TextAnchor.MiddleRight), 1.5f);
            text.rectTransform.Fill(); text.rectTransform.offsetMax = new Vector2(-12, 0);
            var disc = UIKit.Img("Disc", r, UIKit.Circle, UIKit.Ink);
            disc.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(52, 52));
            var ic = UIKit.Img("Icon", disc.transform, UIKit.Symbol(icon), c);
            ic.rectTransform.Fill(9);
        }

        void SetMeter(Image fill, Text text, float v)
        {
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Max(0.04f, v / 100f), 1);
            text.text = Mathf.CeilToInt(v).ToString();
            var c = fill.color;
            fill.color = new Color(c.r, c.g, c.b, v < 25 ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6)) : 1);
        }

        // ------------------------------------------------------------------ guide card

        RectTransform _questCard;
        Text _questText, _questTitle;
        Image _questBack;
        float _questFlash;

        void BuildQuest(RectTransform safe)
        {
            _questCard = UIKit.Rect("Quest", safe).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(720, 96));
            _questBack = UIKit.Img("Back", _questCard, UIKit.Round, UIKit.Ink);
            _questBack.rectTransform.Fill();
            _questTitle = UIKit.Label("Title", _questCard, "SURVIVAL GUIDE", 22, UIKit.Amber, TextAnchor.UpperCenter);
            _questTitle.rectTransform.Fill(); _questTitle.rectTransform.offsetMax = new Vector2(0, -8);
            _questText = UIKit.Outlined(UIKit.Label("Text", _questCard, "", 34, Color.white, TextAnchor.LowerCenter), 1.5f);
            _questText.rectTransform.Fill(); _questText.rectTransform.offsetMin = new Vector2(16, 12); _questText.rectTransform.offsetMax = new Vector2(-16, 0);
        }

        void UpdateQuest()
        {
            var s = G.Session;
            var q = s.CurrentQuest;
            _questText.text = q.Label(s);
            if (_questFlash > 0)
            {
                _questFlash -= Time.deltaTime;
                float t = Mathf.Clamp01(_questFlash / 1.6f);
                _questBack.color = Color.Lerp(UIKit.Ink, new Color(0.2f, 0.55f, 0.18f, 0.95f), t);
                _questCard.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * 0.06f);
                _questTitle.text = t > 0.3f ? "DONE!" : "SURVIVAL GUIDE";
            }
            else { _questBack.color = UIKit.Ink; _questCard.localScale = Vector3.one; _questTitle.text = "SURVIVAL GUIDE"; }
        }

        // ------------------------------------------------------------------ level, XP and doubloons

        Text _levelText, _coinsIsland;
        Image _xpRing, _levelDisc;
        float _levelFlash;

        void BuildLevel(RectTransform parent)
        {
            // A round level badge with an XP ring, next to the meters; doubloons under it.
            var badge = UIKit.Img("Level", parent, UIKit.Circle, UIKit.Ink).rectTransform;
            badge.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(400, -26), new Vector2(120, 120));
            _levelDisc = badge.GetComponent<Image>();
            _xpRing = UIKit.Img("XP", badge, UIKit.RingSprite, UIKit.Green);
            _xpRing.rectTransform.Fill(2);
            _xpRing.type = Image.Type.Filled; _xpRing.fillMethod = Image.FillMethod.Radial360; _xpRing.fillOrigin = 2; _xpRing.fillClockwise = true;
            var lv = UIKit.Label("LV", badge, "LEVEL", 20, UIKit.Sand, TextAnchor.UpperCenter);
            lv.rectTransform.Fill(); lv.rectTransform.offsetMax = new Vector2(0, -22);
            _levelText = UIKit.Outlined(UIKit.Label("N", badge, "1", 52, Color.white, TextAnchor.MiddleCenter), 1.5f);
            _levelText.rectTransform.Fill(); _levelText.rectTransform.offsetMax = new Vector2(0, -14);
            var coin = UIKit.Img("Coin", parent, UIKit.Symbol("coin"), new Color(1f, 0.82f, 0.3f));
            coin.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(538, -40), new Vector2(46, 46));
            _coinsIsland = UIKit.Outlined(UIKit.Label("Coins", parent, "0", 34, Color.white), 1.5f);
            _coinsIsland.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(592, -38), new Vector2(200, 50));
            var b = UIKit.Button("Coins", parent, null, new Color(0, 0, 0, 0), OpenOffers);
            ((RectTransform)b.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(530, -30), new Vector2(200, 64));
        }

        void UpdateLevel()
        {
            var s = G.Session;
            _levelText.text = s.State.level.ToString();
            _xpRing.fillAmount = s.Xp01;
            _coinsIsland.text = s.State.coins.ToString();
            if (_levelFlash > 0)
            {
                _levelFlash -= Time.deltaTime;
                float t = Mathf.Clamp01(_levelFlash / 2f);
                _levelDisc.transform.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI * 4) * 0.12f * t);
                _levelDisc.color = Color.Lerp(UIKit.Ink, new Color(0.9f, 0.7f, 0.2f, 0.95f), t);
            }
        }

        // ------------------------------------------------------------------ joystick

        RectTransform _stickBase, _stickKnob;
        int _finger = -2;
        Vector2 _stickOrigin;
        const float StickRadius = 110;

        void BuildJoystick(RectTransform safe)
        {
            _stickBase = UIKit.Img("Stick", _root, UIKit.Circle, new Color(1, 1, 1, 0.16f)).rectTransform;
            _stickBase.Place(Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(260, 260), new Vector2(StickRadius * 2.2f, StickRadius * 2.2f));
            var ring = UIKit.Img("Ring", _stickBase, UIKit.RingSprite, new Color(1, 1, 1, 0.35f));
            ring.rectTransform.Fill();
            _stickKnob = UIKit.Img("Knob", _stickBase, UIKit.Circle, new Color(1, 1, 1, 0.55f)).rectTransform;
            _stickKnob.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 100));
        }

        float Scale { get { return _root.rect.height / Screen.height; } }

        void UpdateJoystick()
        {
            Vector2 pos = Vector2.zero;
            bool down = false, began = false;
            if (MapOpen || _radioOpen || _offersOpen) { _finger = -2; Stick = Vector2.zero; return; }
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (_finger == -2 && t.phase == TouchPhase.Began && t.position.x < Screen.width * 0.55f && !OverUI(t.fingerId))
                    { _finger = t.fingerId; began = true; }
                    if (t.fingerId == _finger)
                    {
                        pos = t.position;
                        down = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
                    }
                }
            }
            else
            {
                if (Input.GetMouseButtonDown(0) && Input.mousePosition.x < Screen.width * 0.55f && !OverUI(-1)) { _finger = -1; began = true; }
                if (_finger == -1) { pos = Input.mousePosition; down = Input.GetMouseButton(0); }
            }
            if (began) _stickOrigin = pos;
            if (_finger != -2 && down)
            {
                var d = (pos - _stickOrigin) * Scale;
                if (d.magnitude > StickRadius) { _stickOrigin += (d - d.normalized * StickRadius) / Scale; d = d.normalized * StickRadius; }
                Stick = d / StickRadius;
                _stickBase.anchoredPosition = _stickOrigin * Scale;
                _stickKnob.anchoredPosition = d;
                SetStickAlpha(1);
            }
            else
            {
                _finger = -2;
                Stick = Vector2.zero;
                _stickBase.anchoredPosition = Vector2.Lerp(_stickBase.anchoredPosition, new Vector2(260, 260), Time.deltaTime * 8);
                _stickKnob.anchoredPosition = Vector2.zero;
                SetStickAlpha(0.6f);
            }
            if (Stick.magnitude < 0.12f) Stick = Vector2.zero;
        }

        void SetStickAlpha(float a)
        {
            var img = _stickBase.GetComponent<Image>();
            img.color = new Color(1, 1, 1, 0.16f * a);
        }

        static bool OverUI(int finger)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return finger >= 0 ? es.IsPointerOverGameObject(finger) : es.IsPointerOverGameObject();
        }

        // ------------------------------------------------------------------ loop

        bool _dirty = true;
        float _refresh;

        void Update()
        {
            var s = G.Session;
            UpdateJoystick();
            SetMeter(_healthFill, _healthText, s.State.health);
            SetMeter(_foodFill, _foodText, s.State.hunger);
            SetMeter(_waterFill, _waterText, s.State.thirst);
            UpdateQuest();
            UpdateLevel();
            UpdateMap();
            UpdateRadio();
            UpdateSay();
            UpdateSplash();
            if (_offersOpen && Time.frameCount % 30 == 0) RefreshOffers();
            _worldLayer.gameObject.SetActive(!MapOpen);
            UpdateWorldLabels();
            UpdatePops();
            UpdateOverlays();
            // Rebuild the panels when something changed, and tick a craft's progress bar while it runs.
            _refresh -= Time.deltaTime;
            bool crafting = _station != null && s.Job(_station) != null;
            if (_dirty || (crafting && _refresh <= 0))
            {
                _dirty = false;
                _refresh = 0.2f;
                RefreshPanels();
                RefreshRadio();
                if (_offersOpen) RefreshOffers();
                if (MapOpen) RefreshLocationPanel();
            }
        }

        /// <summary>Canvas position (in the safe-area rect) of a world point; z &lt; 0 when behind the camera.</summary>
        public Vector3 ToCanvas(Vector3 world)
        {
            var cam = CameraRig.Instance != null ? CameraRig.Instance.Cam : Camera.main;
            var sp = cam.WorldToScreenPoint(world);
            var k = Scale;
            var safeRect = ((RectTransform)_worldLayer.parent);
            var off = safeRect.offsetMin;
            return new Vector3(sp.x * k - off.x, sp.y * k - off.y, sp.z);
        }

        // ------------------------------------------------------------------ labels over rings

        readonly List<Text> _labels = new List<Text>();
        readonly List<Image> _bars = new List<Image>();

        void UpdateWorldLabels()
        {
            int used = 0;
            var p = Player.Instance;
            if (p != null)
            {
                foreach (var sp in Spot.All)
                {
                    if (!sp.Active) continue;
                    bool site = sp is SiteSpot;
                    float d = Vector3.Distance(sp.Pos, p.transform.position);
                    bool show = sp == p.Current || (site && d < 16f && sp.Label != null && sp.Detail != null);
                    if (!show) continue;
                    string label = sp.Label, detail = sp.Detail;
                    if (label == null && detail == null) continue;
                    var c = ToCanvas(sp.Pos + Vector3.up * (site ? 3.4f : 2.6f));
                    if (c.z < 0) continue;
                    var t = LabelAt(used++);
                    t.text = detail == null ? label : "<size=34>" + label + "</size>\n<size=26>" + detail + "</size>";
                    t.color = sp.Blocked ? UIKit.Amber : Color.white;
                    t.rectTransform.anchoredPosition = c;
                }
            }
            for (int i = used; i < _labels.Count; i++) _labels[i].gameObject.SetActive(false);

            // Health bars over hurt boars.
            int bars = 0;
            foreach (var b in Enemy.All)
            {
                if (!b.Alive || !b.Hurt) continue;
                var c = ToCanvas(b.transform.position + Vector3.up * 1.5f);
                if (c.z < 0) continue;
                var bar = BarAt(bars++);
                bar.rectTransform.anchoredPosition = c;
                bar.transform.GetChild(0).GetComponent<RectTransform>().anchorMax = new Vector2(b.Health01, 1);
            }
            for (int i = bars; i < _bars.Count; i++) _bars[i].gameObject.SetActive(false);
        }

        Text LabelAt(int i)
        {
            while (_labels.Count <= i)
            {
                var t = UIKit.Outlined(UIKit.Label("Label", _worldLayer, "", 32, Color.white, TextAnchor.LowerCenter), 2f);
                t.supportRichText = true;
                t.rectTransform.Place(Vector2.zero, new Vector2(0.5f, 0), Vector2.zero, new Vector2(620, 120));
                _labels.Add(t);
            }
            _labels[i].gameObject.SetActive(true);
            return _labels[i];
        }

        Image BarAt(int i)
        {
            while (_bars.Count <= i)
            {
                var bg = UIKit.Img("Bar", _worldLayer, UIKit.Round, UIKit.Ink);
                bg.rectTransform.Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 16));
                var f = UIKit.Img("Fill", bg.transform, UIKit.Round, UIKit.Red);
                f.rectTransform.anchorMin = Vector2.zero; f.rectTransform.anchorMax = Vector2.one;
                f.rectTransform.offsetMin = new Vector2(3, 3); f.rectTransform.offsetMax = new Vector2(-3, -3);
                _bars.Add(bg);
            }
            _bars[i].gameObject.SetActive(true);
            return _bars[i];
        }

        // ------------------------------------------------------------------ pickups and numbers

        sealed class PopUp { public RectTransform Rt; public Vector3 World; public float Age, Life; public CanvasGroup Group; }
        readonly List<PopUp> _pops = new List<PopUp>();
        float _popStagger;

        /// <summary>"+3 Wood" with its icon, floating up from the castaway's head.</summary>
        public void Pop(string id, int n)
        {
            var rt = UIKit.Rect("Pop", _worldLayer).Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 64));
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            var icon = UIKit.Rect("Icon", rt).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(64, 64));
            UIKit.ItemIcon(icon, id, 64);
            string text = n > 0 ? "+" + n + " " + Content.NameOf(id) : Content.NameOf(id) == "Flask of Water" ? "Refreshing!" : Content.NameOf(id);
            if (id == "water" && n == 0) text = "+ Thirst";
            var t = UIKit.Outlined(UIKit.Label("Text", rt, text, 34, Color.white), 2f);
            t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(112, 0);
            var world = Player.Instance.transform.position + Vector3.up * 2.4f;
            _pops.Add(new PopUp { Rt = rt, World = world, Age = -_popStagger, Life = 1.4f, Group = g });
            _popStagger += 0.25f;
        }

        public void Damage(Vector3 world, int n)
        {
            var rt = UIKit.Rect("Damage", _worldLayer).Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 60));
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            var t = UIKit.Outlined(UIKit.Label("Text", rt, n.ToString(), 44, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter), 2.5f);
            t.rectTransform.Fill();
            _pops.Add(new PopUp { Rt = rt, World = world + Random.insideUnitSphere * 0.3f, Life = 0.8f, Group = g });
        }

        void UpdatePops()
        {
            _popStagger = Mathf.Max(0, _popStagger - Time.deltaTime);
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var p = _pops[i];
                p.Age += Time.deltaTime;
                if (p.Age < 0) { p.Group.alpha = 0; continue; }
                float t = p.Age / p.Life;
                if (t >= 1) { Destroy(p.Rt.gameObject); _pops.RemoveAt(i); continue; }
                var c = ToCanvas(p.World);
                p.Rt.anchoredPosition = (Vector2)c + Vector2.up * (t * 120);
                p.Group.alpha = t < 0.15f ? t / 0.15f : t > 0.7f ? (1 - t) / 0.3f : 1;
                p.Rt.localScale = Vector3.one * (t < 0.12f ? Mathf.Lerp(0.6f, 1.1f, t / 0.12f) : 1);
            }
        }

        // ------------------------------------------------------------------ toasts, flash, passing out

        Text _toast;
        float _toastT;
        Image _flash, _black;
        Text _passText;
        float _flashT, _passOut;

        void BuildOverlays()
        {
            _toast = UIKit.Outlined(UIKit.Label("Toast", _root, "", 40, Color.white, TextAnchor.MiddleCenter), 2.5f);
            _toast.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1000, 60));
            _flash = UIKit.Img("Hurt", _root, null, new Color(0.8f, 0, 0, 0));
            _flash.rectTransform.Fill();
            _black = UIKit.Img("PassOut", _root, null, new Color(0, 0, 0, 0));
            _black.rectTransform.Fill();
            _black.raycastTarget = false;
            _passText = UIKit.Label("Text", _black.transform, "", 56, Color.white, TextAnchor.MiddleCenter);
            _passText.rectTransform.Fill();
        }

        // ------------------------------------------------------------------ title splash

        Image _splash;
        float _splashT = 2.6f;

        /// <summary>The key art and the title for a moment at launch, fading into the island.</summary>
        void BuildSplash()
        {
            var art = UIKit.Place("keyart");
            _splash = UIKit.Img("Splash", _root, null, new Color(0.04f, 0.12f, 0.15f));
            _splash.rectTransform.Fill();
            _splash.raycastTarget = true;
            if (art != null)
            {
                var im = UIKit.Img("Art", _splash.transform, art, Color.white);
                var f = im.gameObject.AddComponent<AspectRatioFitter>();
                f.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                f.aspectRatio = art.rect.width / art.rect.height;
            }
            var t = UIKit.Outlined(UIKit.Label("Title", _splash.transform, "LAST TIDE", 150, new Color(1f, 0.93f, 0.78f), TextAnchor.UpperCenter), 4f);
            t.rectTransform.Fill(); t.rectTransform.offsetMax = new Vector2(0, -90);
            var sub = UIKit.Outlined(UIKit.Label("Sub", _splash.transform, "SHIPWRECK SURVIVAL", 44, new Color(1f, 0.8f, 0.45f), TextAnchor.UpperCenter), 2f);
            sub.rectTransform.Fill(); sub.rectTransform.offsetMax = new Vector2(0, -260);
        }

        void UpdateSplash()
        {
            if (_splash == null) return;
            _splashT -= Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(_splashT / 0.8f);
            foreach (var g in _splash.GetComponentsInChildren<Graphic>()) { var c = g.color; c.a = g == _splash ? a : a; g.color = c; }
            if (_splashT <= 0) { Destroy(_splash.gameObject); _splash = null; }
        }

        public void Toast(string msg)
        {
            _toast.text = msg;
            _toastT = 2.2f;
        }

        public void Flash() { _flashT = 0.35f; }

        void PassOut()
        {
            _passOut = 3.2f;
            _passText.text = "You passed out...\n<size=34>Your backpack lies where you fell.</size>";
            _passText.supportRichText = true;
            CloseAll();
        }

        void UpdateOverlays()
        {
            _toastT -= Time.deltaTime;
            _toast.color = new Color(1, 1, 1, Mathf.Clamp01(_toastT * 2));
            _flashT -= Time.deltaTime;
            float low = G.Session.State.health < 25 ? 0.12f + 0.08f * Mathf.Sin(Time.time * 5) : 0;
            _flash.color = MapOpen ? Color.clear : new Color(0.75f, 0, 0, Mathf.Max(Mathf.Clamp01(_flashT) * 0.9f, low));
            if (_passOut > 0)
            {
                _passOut -= Time.deltaTime;
                float a = _passOut > 2.4f ? (3.2f - _passOut) / 0.8f : _passOut < 0.8f ? _passOut / 0.8f : 1;
                _black.color = new Color(0, 0, 0, a);
                _black.raycastTarget = true;
                _passText.color = new Color(1, 1, 1, a);
                if (_passOut < 1.6f && _passOut + Time.deltaTime >= 1.6f) GameRoot.Instance.Respawn();
            }
            else { _black.color = new Color(0, 0, 0, 0); _black.raycastTarget = false; _passText.color = Color.clear; }
        }
    }

    /// <summary>Keeps the HUD inside the iPhone's safe area (clear of the notch and home bar).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect _last;
        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == _last) return;
            _last = sa;
            var rt = (RectTransform)transform;
            var canvas = rt.parent as RectTransform;
            float k = canvas.rect.height / Screen.height;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(sa.xMin * k, sa.yMin * k);
            rt.offsetMax = new Vector2(-(Screen.width - sa.xMax) * k, -(Screen.height - sa.yMax) * k);
        }
    }
}
