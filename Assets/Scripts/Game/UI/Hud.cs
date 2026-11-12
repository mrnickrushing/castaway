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
        /// <summary>
        /// The world waits: story, a movie, passing out, loading, and the full-screen cards you read and tap away (back
        /// home, a reward, a plan found). Meters don't drain and enemies hold still meanwhile.
        /// </summary>
        public bool Blocking { get { return _passOut > 0 || StoryPlaying || Cinema.Playing || LoadingScreen.Active || _trip != null || _plan != null || (_rewardCard != null && _rewardCard.gameObject.activeSelf); } }

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
            // Never narrower or shorter than 1920x1080: phones (wider than 16:9) keep 1080 high, an iPad (4:3) gets
            // 1920x1440, so the inventory and station panels side by side still fit across it.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
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
            GrowHudOnPhones();
            BuildCombat();      // after growing: the red edge covers the screen at its own size
            BuildBlueprints();
            BuildBuildMode();
            BuildPath();
            BuildSeason();
            BuildRadio();
            BuildOffers();
            BuildShop();
            BuildHelp();
            BuildOverlays();
            BuildStory();

            var s = G.Session;
            s.Toast += Toast;
            s.Gained += (id, n) =>
            {
                if (Player.Instance != null) Pop(id, n);
                // A camp improvement is for good: mark the moment (away from home, where its prop can't appear and sound).
                if (Content.IsPerk(id) && !s.AtHome) Sfx.Play("built");
            };
            s.QuestDone += q => { _questFlash = 1.6f; Sfx.Play("quest"); };
            s.Changed += () => _dirty = true;
            s.Died += PassOut;
            s.PlanFound += ShowPlan;
            s.LevelUp += l => { _levelFlash = 2f; Sfx.Play("levelup"); };
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ meters

        Image _healthFill, _foodFill, _waterFill;
        Text _healthText, _foodText, _waterText;

        /// <summary>Top left: the level badge, the castaway's name and health (number box and bar), food and water below.</summary>
        void BuildMeters(RectTransform safe)
        {
            // The level in a round badge, left of the name; tapping it (or the name) opens Settings to rename.
            var badge = UIKit.Button("Level", safe, null, new Color(0.1f, 0.12f, 0.12f, 0.85f), () => OpenHelp(2));
            badge.GetComponent<Image>().sprite = UIKit.Circle; badge.GetComponent<Image>().type = Image.Type.Simple;
            ((RectTransform)badge.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -22), new Vector2(96, 96));
            var bring = UIKit.Img("Ring", badge.transform, UIKit.RingSprite, new Color(1f, 0.8f, 0.3f, 0.95f)); bring.rectTransform.Fill(2);
            _levelBadge = UIKit.Outlined(UIKit.Label("N", badge.transform, "1", 40, Color.white, TextAnchor.MiddleCenter), 1.5f);
            _levelBadge.rectTransform.Fill(); _levelBadge.rectTransform.offsetMin = new Vector2(8, 34); _levelBadge.rectTransform.offsetMax = new Vector2(-8, -10);
            var lv = UIKit.Label("Lv", badge.transform, "LVL", 18, UIKit.Amber, TextAnchor.LowerCenter);
            lv.rectTransform.anchorMin = new Vector2(0, 0); lv.rectTransform.anchorMax = new Vector2(1, 0); lv.rectTransform.pivot = new Vector2(0.5f, 0);
            lv.rectTransform.anchoredPosition = new Vector2(0, 12); lv.rectTransform.sizeDelta = new Vector2(-30, 22);
            var box = UIKit.Rect("Meters", safe).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -20), new Vector2(420, 140));
            var name = UIKit.Outlined(UIKit.Label("Name", box, "Castaway", 36, Color.white, TextAnchor.MiddleLeft, false), 1.5f);
            name.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 52));
            _playerName = name;
            var tapName = name.gameObject.AddComponent<Button>(); name.raycastTarget = true;
            tapName.onClick.AddListener(() => { Sfx.Play("click"); OpenHelp(2); });
            var hb = UIKit.Img("HP", box, null, new Color(0.2f, 0.55f, 0.2f, 0.95f));
            hb.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, -4), new Vector2(100, 46));
            _healthText = UIKit.Label("N", hb.transform, "100", 34, Color.white, TextAnchor.MiddleRight, false);
            _healthText.rectTransform.Fill(); _healthText.rectTransform.offsetMax = new Vector2(-10, 0);
            var bar = UIKit.Img("Bar", box, null, new Color(0, 0, 0, 0.45f));
            bar.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -56), new Vector2(400, 12));
            _healthFill = UIKit.Img("Fill", bar.transform, null, new Color(0.35f, 0.85f, 0.3f));
            _healthFill.rectTransform.anchorMin = Vector2.zero; _healthFill.rectTransform.anchorMax = Vector2.one;
            _healthFill.rectTransform.offsetMin = _healthFill.rectTransform.offsetMax = Vector2.zero;
            Small(box, 0, "food", new Color(0.95f, 0.6f, 0.2f), out _foodFill, out _foodText);
            Small(box, 1, "water", new Color(0.3f, 0.68f, 1f), out _waterFill, out _waterText);
        }

        Text _playerName, _levelBadge;

        /// <summary>1.2 on phones, else 1: how much bigger the field HUD is drawn.</summary>
        float _hudK = 1;

        /// <summary>
        /// A phone shows the 1080-unit-tall canvas on a screen ~7 cm tall: the field HUD (meters, guide card, buttons,
        /// minimap, coins) comes up a fifth bigger there, each piece about its own corner, which the 2340-wide canvas has
        /// room for. Panels keep their layout and grow their type instead (UIKit.Label).
        /// </summary>
        void GrowHudOnPhones()
        {
            if (!UIKit.Phone) return;
            _hudK = 1.2f;
            foreach (RectTransform c in _islandUi) if (c.name != "XP") Grow(c);
            Grow(_questCard); Grow(_levelUp);
            // Grown, the MAP button would sit on the weapon slot: it moves up beside the coins, under the round buttons.
            var map = _islandUi.Find("MAP") as RectTransform;
            if (map != null) map.anchoredPosition = new Vector2(-330, -300) * _hudK;
        }

        void Grow(RectTransform c) { c.localScale = Vector3.one * _hudK; c.anchoredPosition *= _hudK; }

        void Small(RectTransform box, int col, string icon, Color c, out Image fill, out Text text)
        {
            var r = UIKit.Rect("Small " + icon, box).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(col * 205, -82), new Vector2(195, 44));
            var ic = UIKit.Img("Icon", r, UIKit.Symbol(icon), c);
            ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(40, 40));
            var bg = UIKit.Img("Back", r, null, new Color(0, 0, 0, 0.45f));
            bg.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(48, 0), new Vector2(140, 12));
            fill = UIKit.Img("Fill", bg.transform, null, c);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            text = UIKit.Outlined(UIKit.Label("V", r, "", 22, Color.white, TextAnchor.UpperRight, false), 1.2f);
            text.rectTransform.Fill(); text.rectTransform.offsetMax = new Vector2(-6, 14);
        }

        void SetMeter(Image fill, Text text, float v)
        {
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Max(0.02f, v / 100f), 1);
            text.text = Mathf.CeilToInt(v).ToString();
            var c = fill.color;
            fill.color = new Color(c.r, c.g, c.b, v < 25 ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6)) : 1);
        }

        // ------------------------------------------------------------------ guide card

        RectTransform _questCard, _levelUp;
        Text _questText, _questTitle;
        Image _questBack;
        float _questFlash;
        bool _questFolded;

        /// <summary>The guide on the left: the chapter in amber, the step under it; folds away with the arrows.</summary>
        void BuildQuest(RectTransform safe)
        {
            _questCard = UIKit.Rect("Quest", safe).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -170), new Vector2(620, 130));
            _questBack = UIKit.Img("Back", _questCard, Fade(), new Color(0.08f, 0.1f, 0.1f, 0.85f));
            _questBack.rectTransform.Fill();
            _questTitle = UIKit.Label("Title", _questCard, "", 32, UIKit.Amber, TextAnchor.UpperLeft, false);
            _questTitle.rectTransform.Fill(); _questTitle.rectTransform.offsetMin = new Vector2(40, 0); _questTitle.rectTransform.offsetMax = new Vector2(-60, -10);
            _questText = UIKit.Outlined(UIKit.Label("Text", _questCard, "", 30, Color.white, TextAnchor.LowerLeft, false), 1.2f);
            _questText.supportRichText = true;
            // Below the chapter title; long steps shrink to fit instead of running up into it.
            _questText.rectTransform.Fill(); _questText.rectTransform.offsetMin = new Vector2(40, 8); _questText.rectTransform.offsetMax = new Vector2(-60, -46);
            _questText.resizeTextForBestFit = true; _questText.resizeTextMinSize = 24; _questText.resizeTextMaxSize = 32;
            _questText.verticalOverflow = VerticalWrapMode.Truncate;
            var fold = UIKit.Button("Fold", _questCard, "<<", new Color(0, 0, 0, 0), () => { _questFolded = !_questFolded; }, 40);
            ((RectTransform)fold.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(70, 100));
            fold.GetComponentInChildren<Text>().color = new Color(1, 1, 1, 0.6f);
            // LEVEL UP!: waits until tapped, then pays the level's reward.
            _levelUp = UIKit.Rect("Level up", safe).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -310), new Vector2(420, 90));
            var lbg = UIKit.Img("Back", _levelUp, Fade(), new Color(0.08f, 0.1f, 0.1f, 0.85f)); lbg.rectTransform.Fill();
            var lb = UIKit.Button("Up", _levelUp, null, new Color(0.85f, 0.62f, 0.1f), ClaimLevelUp);
            lb.GetComponent<Image>().sprite = UIKit.Circle; lb.GetComponent<Image>().type = Image.Type.Simple;
            ((RectTransform)lb.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(76, 76));
            var la = UIKit.Img("Arrow", lb.transform, UIKit.Symbol("upgrade"), Color.white); la.rectTransform.Fill(14);
            var lt = UIKit.Outlined(UIKit.Label("T", _levelUp, "LEVEL UP!", 36, UIKit.Amber, TextAnchor.MiddleLeft, false), 1.2f);
            lt.rectTransform.Fill(); lt.rectTransform.offsetMin = new Vector2(134, 0);
            var tap = UIKit.Button("Tap", _levelUp, null, new Color(0, 0, 0, 0), ClaimLevelUp);
            ((RectTransform)tap.transform).Fill();
            _levelUp.gameObject.SetActive(false);
        }

        void ClaimLevelUp()
        {
            var got = G.Session.ClaimLevelRewards();
            if (got.Count == 0) return;
            Sfx.Play("levelup");
            var parts = new System.Collections.Generic.List<string>();
            foreach (var lv in got) foreach (var r in Session.LevelReward(lv)) parts.Add("+" + r.n + " " + Content.NameOf(r.id));
            Say("Level " + got[got.Count - 1] + "!", string.Join("   ", parts) + "   +20 energy each level");
        }

        static Sprite _fade;
        /// <summary>A panel that fades out to the right, like a banner on the screen's edge.</summary>
        static Sprite Fade()
        {
            if (_fade != null) return _fade;
            var t = new Texture2D(64, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++) for (int y = 0; y < 4; y++) t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1.3f - x / 63f * 1.2f)));
            t.Apply();
            return _fade = Sprite.Create(t, new Rect(0, 0, 64, 4), new Vector2(0.5f, 0.5f));
        }

        static string ChapterTitle()
        {
            var s = G.Session;
            var ch = Story.ChapterOf(s.State.quest);
            return ch.Title;
        }

        void UpdateQuest()
        {
            var s = G.Session;
            var q = s.CurrentQuest;
            string need = "";
            // A blueprint step the backpack can't feed yet: say what to fetch (the guide arrow points at it).
            if (q.PointBuild != null && Content.Builds.ContainsKey(q.PointBuild) && !s.Built(q.PointBuild) && !s.CanDeliver(q.PointBuild))
            {
                var r = s.Remaining(q.PointBuild);
                if (r.Count > 0)
                {
                    var it = r[0];
                    int short_ = it.n - s.Count(it.id);
                    var recipe = GuideArrow.RecipeFor(it.id);
                    string name = Content.Items[it.id].Name;
                    // Crafted materials say so: "craft 2 Rope", and where (CRAFT anywhere, or at the workbench).
                    if (short_ > 0 && recipe != null && !GuideArrow.Gatherable(it.id))
                        need = "  <size=24><color=#8FE3A8>craft " + short_ + " " + name + (recipe.Station == "hand" ? " (CRAFT)" : " at the " + Session.StationName(recipe.Station).ToLower()) + "</color></size>";
                    else need = "  <size=24><color=#8FE3A8>need " + it.n + " " + name + "</color></size>";
                }
            }
            if (q.Signal != null)
            {
                var miss = GuideArrow.RescueMissing(q);
                if (miss.Count > 0)
                {
                    var it = miss[0];
                    var recipe = GuideArrow.RecipeFor(it.id);
                    string name = Content.Items[it.id].Name;
                    if (recipe != null && !GuideArrow.Gatherable(it.id))
                        need = "  <size=24><color=#8FE3A8>craft " + it.n + " " + name + (recipe.Station == "hand" ? " (CRAFT)" : " at the " + Session.StationName(recipe.Station).ToLower()) + "</color></size>";
                    else need = "  <size=24><color=#8FE3A8>need " + it.n + " more " + name + "</color></size>";
                }
            }
            if (q.Craft != null && Content.Recipes.ContainsKey(q.Craft))
            {
                var rc = Content.Recipes[q.Craft];
                foreach (var c in rc.Inputs)
                    if (s.Count(c.id) < c.n) { need = "  <size=24><color=#8FE3A8>need " + (c.n - s.Count(c.id)) + " more " + Content.Items[c.id].Name + "</color></size>"; break; }
                if (need == "" && s.Job(rc.Station) == null) need = "  <size=24><color=#8FE3A8>ready: tap " + (rc.Station == "hand" ? "CRAFT" : "the " + Session.StationName(rc.Station).ToLower()) + "</color></size>";
            }
            _questText.text = "<color=#FFC24A>\u2022</color>  " + q.Label(s) + need;
            _questText.gameObject.SetActive(!_questFolded);
            _questCard.anchoredPosition = new Vector2(_questFolded ? -540 : 0, MapOpen ? -215 : -170) * _hudK;
            if (_questFlash > 0)
            {
                _questFlash -= Time.deltaTime;
                float t = Mathf.Clamp01(_questFlash / 1.6f);
                _questBack.color = Color.Lerp(new Color(0.08f, 0.1f, 0.1f, 0.85f), new Color(0.2f, 0.55f, 0.18f, 0.95f), t);
                _questTitle.text = t > 0.3f ? "Done!" : ChapterTitle();
            }
            else { _questBack.color = new Color(0.08f, 0.1f, 0.1f, 0.85f); _questTitle.text = ChapterTitle(); }
            _levelUp.gameObject.SetActive(s.LevelRewardsWaiting > 0 && !MapOpen);
            if (_levelUp.gameObject.activeSelf) _levelUp.GetChild(1).localScale = Vector3.one * (1 + 0.08f * Mathf.Sin(Time.time * 6));
        }

        // ------------------------------------------------------------------ level, XP and doubloons

        Text _levelText, _coinsIsland;
        Image _xpRing;
        float _levelFlash;

        /// <summary>The XP bar along the bottom edge, and doubloons by the minimap.</summary>
        void BuildLevel(RectTransform parent)
        {
            var bar = UIKit.Img("XP", parent, null, new Color(0, 0, 0, 0.45f));
            bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(1, 0);
            bar.rectTransform.offsetMin = new Vector2(380, 6); bar.rectTransform.offsetMax = new Vector2(-260, 16);
            _xpRing = UIKit.Img("Fill", bar.transform, null, new Color(0.2f, 0.7f, 0.7f));
            _xpRing.rectTransform.anchorMin = Vector2.zero; _xpRing.rectTransform.anchorMax = new Vector2(0, 1);
            _xpRing.rectTransform.offsetMin = _xpRing.rectTransform.offsetMax = Vector2.zero;
            _levelText = UIKit.Outlined(UIKit.Label("Lv", parent, "Level 1", 28, Color.white, TextAnchor.LowerRight, false), 1.2f);
            _levelText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(370, 0), new Vector2(200, 34));
            var coins = UIKit.Img("Coins", parent, null, new Color(0, 0, 0, 0.35f)).rectTransform;
            coins.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -330), new Vector2(240, 56));
            UIKit.ItemIcon(UIKit.Rect("Coin", coins).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(56, 56)), "doubloon", 56);
            _coinsIsland = UIKit.Outlined(UIKit.Label("N", coins, "0", 32, Color.white, TextAnchor.MiddleLeft, false), 1.2f);
            _coinsIsland.rectTransform.Fill(); _coinsIsland.rectTransform.offsetMin = new Vector2(66, 0);
            var plus = UIKit.Button("Plus", coins, "+", new Color(0, 0, 0, 0), OpenShop, 46);
            ((RectTransform)plus.transform).Fill();
            _later.Add(plus.gameObject);
            plus.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleRight;
            plus.GetComponentInChildren<Text>().color = new Color(0.3f, 0.85f, 0.8f);
        }

        void UpdateLevel()
        {
            var s = G.Session;
            _levelText.text = "Level " + s.State.level;
            _xpRing.rectTransform.anchorMax = new Vector2(s.Xp01, 1);
            _coinsIsland.text = s.State.coins.ToString();
            if (_playerName != null) _playerName.text = s.DisplayName;
            if (_levelBadge != null) _levelBadge.text = s.State.level.ToString();
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

        /// <summary>Shrinks a stretched panel body uniformly from its top-left corner when it's narrower than the
        /// width its fixed layout needs (the canvas is 1920 wide on 16:9 phones and iPads, ~2340 on newer iPhones).</summary>
        static void FitWidth(RectTransform body, float need)
        {
            Vector2 min = body.offsetMin, max = body.offsetMax;
            body.pivot = new Vector2(0, 1);
            body.offsetMin = min; body.offsetMax = max;
            body.localScale = Vector3.one * Mathf.Min(1, body.rect.width / need);
        }

        void UpdateJoystick()
        {
            Vector2 pos = Vector2.zero;
            bool down = false, began = false;
            if (MapOpen || _radioOpen || _offersOpen || _shopOpen || _helpOpen || StoryPlaying || _confirm != null || _bpOpen || _pathOpen || _seasonOpen || _rewardCard.gameObject.activeSelf || _trip != null || _plan != null || _building || (InvOpen && _station != "radio")) { _finger = -2; Stick = Vector2.zero; return; }
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
            _healthFill.color = s.State.health < 30 ? UIKit.Red : new Color(0.35f, 0.85f, 0.3f);
            SetMeter(_foodFill, _foodText, s.State.hunger);
            SetMeter(_waterFill, _waterText, s.State.thirst);
            UpdateQuest();
            UpdateLevel();
            UpdateMap();
            UpdateRadio();
            UpdateSay();
            UpdateStory();
            UpdateBuild();
            UpdateActions();
            UpdateMinimap();
            if (_offersOpen && Time.frameCount % 30 == 0) RefreshOffers();
            _worldLayer.gameObject.SetActive(!MapOpen);
            UpdateWorldLabels();
            UpdateCombat();
            UpdateTrip();
            UpdatePacing();
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
                RefreshOffers(); // also hides it once closed
                if (_shopOpen) RefreshShop(); else _shop.gameObject.SetActive(false);
                if (_bpOpen) RefreshBlueprints(); else _bp.gameObject.SetActive(false);
                if (_pathOpen) RefreshPath(); else _path.gameObject.SetActive(false);
                if (_seasonOpen) RefreshSeason(); else _season.gameObject.SetActive(false);
                RefreshBuild();
                if (_helpOpen) RefreshHelp(); else _help.gameObject.SetActive(false);
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
                    bool plan = sp is NodeSpot ns && ns.NodeKind == "plan";   // the plan calls from a little way off
                    bool show = sp == p.Current || ((site ? d < 16f : plan && d < 12f) && sp.Label != null && sp.Detail != null);
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
            UIKit.ItemIcon(icon, id, 64, false); // floats over the field: never catch a touch meant for the stick
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
        Image _toastBack;   // a dark pill behind the line, so it reads over open panels as well as the island
        float _toastT;
        Image _flash, _black;
        Text _passText;
        float _flashT, _passOut;

        void BuildOverlays()
        {
            _toastBack = UIKit.Img("Toast", _root, UIKit.Round, new Color(0.05f, 0.06f, 0.06f, 0));
            _toastBack.raycastTarget = false;
            _toastBack.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(600, 84));
            _toast = UIKit.Outlined(UIKit.Label("Text", _toastBack.transform, "", 40, Color.white, TextAnchor.MiddleCenter), 2.5f);
            _toast.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 60));
            _toast.raycastTarget = false;
            _flash = UIKit.Img("Hurt", _root, null, new Color(0.8f, 0, 0, 0));
            _flash.rectTransform.Fill();
            _black = UIKit.Img("PassOut", _root, null, new Color(0, 0, 0, 0));
            _black.rectTransform.Fill();
            _black.raycastTarget = false;
            _passText = UIKit.Label("Text", _black.transform, "", 56, Color.white, TextAnchor.MiddleCenter);
            _passText.rectTransform.Fill();
        }

        public void Toast(string msg)
        {
            _toast.text = msg;
            _toastT = 2.2f;
            float w = Mathf.Min(_toast.preferredWidth, 1000) + 70;
            _toastBack.rectTransform.sizeDelta = new Vector2(w, 84);
            _toastBack.transform.SetAsLastSibling();
        }

        public void Flash() { _flashT = 0.35f; }

        void PassOut()
        {
            _passOut = 3.2f;
            _passText.text = "You passed out...\n<size=34>" + (G.Session.LastFallLeftBag
                ? "Your backpack lies where you fell. Your pockets came with you."
                : "Everything you carried was in your pockets: nothing was left behind.") + "</size>";
            _passText.supportRichText = true;
            CloseAll();
            if (_confirm != null) { Destroy(_confirm.gameObject); _confirm = null; }  // a question asked a moment ago no longer applies
            _black.transform.SetAsLastSibling();                                       // over every panel and card
        }

        void UpdateOverlays()
        {
            UpdateItemTip();
            _toastT -= Time.deltaTime;
            _toast.color = new Color(1, 1, 1, Mathf.Clamp01(_toastT * 2));
            _toastBack.color = new Color(0.05f, 0.06f, 0.06f, 0.82f * Mathf.Clamp01(_toastT * 2));
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
