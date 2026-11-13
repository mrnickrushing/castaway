using System.Linq;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// The first-run tutorial, in the castaway's own voice. After the intro it walks a new player through the first
    /// hour on Wreck Cove: the stick, the green rings, the guide card, the backpack, building at a site, the meters,
    /// crafting and the first fight, then a send-off card. The screen dims round a soft spotlight on what matters (a
    /// button, or a ring out in the world), a hand points at it, and a bubble with the castaway's portrait says what
    /// to do. Steps that only explain something hold the world still and wait for NEXT; the rest let the game run
    /// and move on when the player has done the thing. Progress is saved in <see cref="GameState.tutorial"/>; SKIP
    /// ends it, and saves already past the first hunt never see it.
    /// </summary>
    public sealed partial class Hud
    {
        enum Tut { Move, Ring, Guide, Bag, Build, Meters, Craft, Fight, Finish, Done }

        static readonly Color TutDim = new Color(0.02f, 0.05f, 0.05f, 0.62f);
        static readonly Color TutGold = new Color(1f, 0.8f, 0.3f);
        const int TutDots = 8;  // Move..Fight

        RectTransform _tutLayer, _coach, _tutHand, _tutDotRow, _tutCard;
        Image _tutDim; Material _tutMat;
        Text _coachText; Button _coachNext;
        TutHole _tutHole;
        Tut _tutShown = Tut.Done; string _tutSay = ""; float _tutType, _tutPop, _tutStepT;
        Vector4 _hole, _holeGoal; float _dimA;
        Vector3 _tutFrom; bool _tutFromSet;
        bool _tutHold;

        /// <summary>The tutorial is showing something to read (a NEXT step or its card): the world waits.</summary>
        bool TutorialHolding { get { return _tutHold; } }

        Tut TutStep
        {
            get { return (Tut)Mathf.Clamp(G.Session.State.tutorial, 0, (int)Tut.Done); }
            set { G.Session.State.tutorial = (int)value; }
        }

        static int QuestIndex(string id) { return Quests.All.FindIndex(q => q.Id == id); }
        static bool QuestPast(string id) { return G.Session.State.quest > QuestIndex(id); }
        static bool QuestIs(string id) { return G.Session.CurrentQuest.Id == id; }

        void BuildTutorial()
        {
            // A game that got going before this tutorial existed (or is past the first hunt) doesn't start one.
            if (TutStep < Tut.Done && QuestPast("hunt")) TutStep = Tut.Done;

            _tutLayer = UIKit.Rect("Tutorial", _root); _tutLayer.Fill();
            _tutDim = UIKit.Img("Dim", _tutLayer, null, Color.white);
            _tutDim.rectTransform.Fill();
            var sh = Resources.Load<Shader>("Shaders/Spotlight");
            _tutMat = sh != null ? new Material(sh) : null;
            if (_tutMat != null) { _tutDim.material = _tutMat; _tutMat.SetColor("_Color", TutDim); _tutMat.SetColor("_RimColor", TutGold); }
            else _tutDim.color = TutDim;
            _tutHole = _tutDim.gameObject.AddComponent<TutHole>();

            // The hand: its fingertip is the pivot, so placing it places the tip.
            var hand = UIKit.Img("Hand", _tutLayer, CoachSprite("hand"), Color.white);
            _tutHand = hand.rectTransform;
            _tutHand.Place(new Vector2(0.5f, 0.5f), new Vector2(0.43f, 0.86f), Vector2.zero, new Vector2(150, 150));

            // The bubble: the castaway's portrait, a line of thought, the step dots.
            // A gold edge, then the dark panel over it (children draw over their parent, so both are children).
            _coach = UIKit.Img("Coach", _tutLayer, null, new Color(0, 0, 0, 0)).rectTransform;
            _coach.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 262));
            _coach.GetComponent<Image>().raycastTarget = true;
            UIKit.Img("Edge", _coach, UIKit.Round, new Color(TutGold.r, TutGold.g, TutGold.b, 0.7f)).rectTransform.Fill(-3);
            UIKit.Img("Back", _coach, UIKit.Round, new Color(0.06f, 0.09f, 0.09f, 1f)).rectTransform.Fill(0);
            var accent = UIKit.Img("Accent", _coach, null, TutGold).rectTransform;
            accent.anchorMin = new Vector2(0, 1); accent.anchorMax = new Vector2(1, 1); accent.offsetMin = new Vector2(212, -14); accent.offsetMax = new Vector2(-26, -10);
            var frame = UIKit.Img("Frame", _coach, UIKit.Round, TutGold).rectTransform;
            frame.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(184, 184));
            var face = UIKit.Img("Face", frame, CoachSprite("castaway"), Color.white).rectTransform; face.Fill(5);
            frame.gameObject.AddComponent<CoachBob>();
            var who = UIKit.Label("Who", _coach, "YOU  ·  WASHED ASHORE", 22, TutGold, TextAnchor.UpperLeft);
            who.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(222, -24), new Vector2(520, 30));
            _coachText = UIKit.Label("Say", _coach, "", 32, Color.white, TextAnchor.UpperLeft, false);
            _coachText.supportRichText = true;
            _coachText.rectTransform.Fill(); _coachText.rectTransform.offsetMin = new Vector2(222, 58); _coachText.rectTransform.offsetMax = new Vector2(-30, -60);
            _coachText.resizeTextForBestFit = true; _coachText.resizeTextMinSize = 26; _coachText.verticalOverflow = VerticalWrapMode.Truncate;
            _coachText.lineSpacing = 1.05f;
            // Tapping the bubble finishes the line at once.
            var tapAll = _coach.gameObject.AddComponent<Button>(); tapAll.transition = Selectable.Transition.None;
            tapAll.onClick.AddListener(() => _tutType = 999);
            _tutDotRow = UIKit.Rect("Dots", _coach).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(222, 22), new Vector2(260, 24));
            for (int i = 0; i < TutDots; i++)
            {
                var d = UIKit.Img("Dot", _tutDotRow, UIKit.Circle, Color.white);
                d.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(i * 30, 0), new Vector2(16, 16));
            }
            _coachNext = UIKit.Button("Next", _coach, "NEXT", new Color(0.8f, 0.62f, 0.12f), TutNext, 32);
            ((RectTransform)_coachNext.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 14), new Vector2(190, 76));
            var skip = UIKit.Button("Skip", _coach, "SKIP", new Color(0, 0, 0, 0), () => EndTutorial(), 22);
            ((RectTransform)skip.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -10), new Vector2(110, 42));
            skip.GetComponentInChildren<Text>().color = new Color(1, 1, 1, 0.55f);

            _tutLayer.gameObject.SetActive(false);
        }

        static Sprite CoachSprite(string name)
        {
            var tex = Resources.Load<Texture2D>("UI/Coach/" + name);
            return tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)) : null;
        }

        void EndTutorial()
        {
            TutStep = Tut.Done;
            _tutHold = false;
            if (_tutCard != null) { Destroy(_tutCard.gameObject); _tutCard = null; }
            _tutLayer.gameObject.SetActive(false);
            GameRoot.Instance.Save();
        }

        /// <summary>Ends the tutorial now (the PlayBot does, so it never waits on a NEXT).</summary>
        public void SkipTutorial() { if (TutStep != Tut.Done) EndTutorial(); }

        void TutNext()
        {
            var s = TutStep;
            if (s == Tut.Guide) TutStep = Tut.Bag;
            else if (s == Tut.Bag) TutStep = Tut.Build;
            else if (s == Tut.Meters) TutStep = Tut.Craft;
            GameRoot.Instance.Save();
        }

        /// <summary>Something else has the screen (a panel, the map, a story card...): the tutorial waits.</summary>
        bool TutorialWaits(Tut step)
        {
            if (StoryPlaying || Cinema.Playing || LoadingScreen.Active || _passOut > 0 || MapOpen || Player.Instance == null) return true;
            if (_trip != null || _plan != null || _confirm != null || _rewardCard.gameObject.activeSelf) return true;
            if (_radioOpen || _offersOpen || _shopOpen || _helpOpen || _pathOpen || _seasonOpen || _building || InvOpen) return true;
            if (_bpOpen && step != Tut.Craft) return true;
            return !G.Session.AtHome;
        }

        void UpdateTutorial()
        {
            var step = TutStep;
            if (step == Tut.Bag && _bagOpen && (G.Session.Has("wood") || QuestPast("wood"))) step = TutStep = Tut.Build;
            if (step == Tut.Done || TutorialWaits(step)) { Hide(); return; }
            var s = G.Session;
            var p = Player.Instance;

            // Moving on when the player has done the thing (or is already past it).
            if (step == Tut.Move)
            {
                if (!_tutFromSet) { _tutFrom = p.transform.position; _tutFromSet = true; }
                if (QuestPast("hatchet") || Vector3.Distance(p.transform.position, _tutFrom) > 4f) step = TutStep = Tut.Ring;
            }
            if (step == Tut.Ring && QuestPast("hatchet")) step = TutStep = Tut.Guide;
            if (step == Tut.Guide && QuestPast("wood")) step = TutStep = Tut.Bag;
            if (step == Tut.Bag && QuestPast("campfire")) step = TutStep = Tut.Build;
            if (step == Tut.Build && QuestPast("campfire")) step = TutStep = Tut.Meters;
            if (step == Tut.Meters && QuestPast("spear")) step = TutStep = Tut.Craft;
            if (step == Tut.Craft && QuestPast("spear")) step = TutStep = Tut.Fight;
            if (step == Tut.Fight && QuestPast("hunt") && Enemy.Nearest(p.transform.position, 12f) == null) step = TutStep = Tut.Finish;

            // Steps that wait for the guide to get there show nothing meanwhile.
            bool ready = true;
            switch (step)
            {
                case Tut.Bag: ready = s.Has("wood") || QuestPast("wood"); break;
                case Tut.Build: ready = QuestIs("campfire"); break;
                case Tut.Craft: ready = QuestIs("spear"); break;
                case Tut.Fight: ready = QuestIs("hunt"); break;
                case Tut.Finish: ready = !StoryPlaying; break;
            }
            if (!ready) { Hide(); return; }

            if (step != _tutShown) { _tutShown = step; _tutType = 0; _tutPop = 0; _tutStepT = 0; }
            _tutStepT += Time.unscaledDeltaTime;
            _tutLayer.gameObject.SetActive(true);
            _tutLayer.SetAsLastSibling();

            if (step == Tut.Finish) { _tutHold = true; TutCard(); return; }
            if (_tutCard != null) { Destroy(_tutCard.gameObject); _tutCard = null; }
            _coach.gameObject.SetActive(true);

            // What to say, what to light up, whether the world waits for NEXT, and how the hand moves.
            RectTransform[] targets = null; Spot spot = null;
            string say; bool next = false, hand = true, drag = false; float dim = TutDim.a;
            switch (step)
            {
                case Tut.Move:
                    targets = new[] { _stickBase };
                    say = "First, my legs. <color=#FFD15C>Drag on the left of the screen</color> to walk. A green arrow by my feet points to the next job.";
                    drag = true; dim = 0.5f;
                    break;
                case Tut.Ring:
                    spot = NearestSpot("hatchet_pickup", 40);
                    say = spot != null && HoleAt(spot).z > 0
                        ? "The hatchet! See the <color=#7CE36A>green ring</color>? Walk into it and stand still: I pick up, chop and build just by being there."
                        : "The hatchet's by the wreck, in a <color=#7CE36A>green ring</color>. Walk into it and stand still: I pick up, chop and build just by being there.";
                    dim = 0.5f;
                    break;
                case Tut.Guide:
                    targets = new[] { _questCard };
                    say = "One thing at a time. What I need next is always written <color=#FFD15C>here</color>, and the arrow leads the way.";
                    next = true; hand = false;
                    break;
                case Tut.Bag:
                    targets = new[] { (RectTransform)_islandUi.Find("BAG") };
                    say = "Everything I gather goes in my <color=#FFD15C>BAG</color>. The first 5 slots are pockets: I keep those even if I pass out.";
                    next = true;
                    break;
                case Tut.Build:
                    spot = NearestSpot("campfire", 60);
                    say = spot != null && HoleAt(spot).z > 0
                        ? "Wood and stone for a fire. Stand in the <color=#FFD15C>campfire's ring</color> and I'll build it piece by piece."
                        : "Wood and stone for a fire. Follow the arrow to the <color=#FFD15C>campfire's ring</color>, stand in it and I'll build it piece by piece.";
                    dim = 0.5f;
                    break;
                case Tut.Meters:
                    targets = new[] { _healthText.rectTransform, (RectTransform)_healthFill.transform.parent, (RectTransform)_foodFill.transform.parent, (RectTransform)_waterFill.transform.parent };
                    say = "Health, food and water. Keep them up: eat and drink from the <color=#FFD15C>BAG</color>, and rest by the fire to heal.";
                    next = true; hand = false;
                    break;
                case Tut.Craft:
                {
                    var craft = _bpOpen ? _bpCard.Find("Craft") as RectTransform : null;
                    bool making = s.State.jobs.Any(j => j.recipe == "spear");
                    if (_bpOpen && craft != null && !making)
                    {
                        targets = new[] { craft };
                        say = "The spear's blueprint. If I have what it takes, tap <color=#FFD15C>CRAFT</color>.";
                    }
                    else if (_bpOpen || making)
                    {
                        say = making ? "Whittling a spear. It goes in my BAG when it's done." : "Not enough yet: the guide will point me to what's missing.";
                        hand = false; dim = 0f;
                    }
                    else
                    {
                        targets = new[] { (RectTransform)_islandUi.Find("CRAFT") };
                        say = "Time I had a weapon. Tools and weapons are made in <color=#FFD15C>CRAFT</color>.";
                    }
                    break;
                }
                default: // Fight
                {
                    var foe = Enemy.Nearest(p.transform.position, 16f);
                    if (foe != null)
                    {
                        targets = new[] { _weaponSlot };
                        say = "Tap to strike (AUTO does it for me). When a <color=#FF6A5A>red ring</color> fills under it, step back out of reach!";
                        dim = 0.25f;
                    }
                    else
                    {
                        say = "Boar tracks lead into the jungle. Follow the arrow, spear ready.";
                        hand = false; dim = 0f;
                    }
                    break;
                }
            }

            // The world holds still while there's something to read and NEXT to tap.
            _tutHold = next;

            // The hole: round the buttons, or round a ring out in the world.
            Vector4 goal = Vector4.zero;
            if (targets != null && targets.All(t => t != null && t.gameObject.activeInHierarchy)) goal = HoleAround(targets);
            else if (spot != null) goal = HoleAt(spot);
            bool any = goal.z > 0;
            _holeGoal = goal;
            float k = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 12);
            if (_hole.z <= 0 || _holeGoal.z <= 0) _hole = _holeGoal; else _hole = Vector4.Lerp(_hole, _holeGoal, k);
            _dimA = Mathf.Lerp(_dimA, any || next ? (next ? 0.66f : dim) : 0f, k);
            if (_tutMat != null)
            {
                _tutMat.SetVector("_Hole", _hole);
                _tutMat.SetFloat("_Radius", targets == null && spot != null ? 999f : 26f);   // round over a ring, rounded corners over buttons
                _tutMat.SetColor("_Color", new Color(TutDim.r, TutDim.g, TutDim.b, _dimA));
                _tutMat.SetFloat("_RimAlpha", !any ? 0f : 0.55f + 0.3f * Mathf.Sin(Time.unscaledTime * 4));
            }
            // Only NEXT steps catch taps (round the hole); otherwise the island stays playable under the dimming.
            _tutDim.raycastTarget = next;
            _tutHole.Hole = _hole; _tutHole.Block = next; _tutHole.Layer = _tutLayer;

            // The hand: taps on a button, presses into a ring, or drags the stick.
            _tutHand.gameObject.SetActive(hand && any);
            if (hand && any)
            {
                float t = Mathf.Repeat(Time.unscaledTime * (drag ? 0.7f : 1.1f), 1f);
                if (drag)
                {
                    var dir = StickHint(p);
                    float go = t < 0.15f ? 0 : t < 0.65f ? Mathf.SmoothStep(0, 1, (t - 0.15f) / 0.5f) : 1;
                    var tip = new Vector2(_hole.x, _hole.y) + dir * 95 * go;
                    _tutHand.anchoredPosition = tip;
                    _tutHand.localScale = Vector3.one * (t > 0.85f ? 1.05f : 0.95f);
                    _tutHand.GetComponent<Image>().color = new Color(1, 1, 1, t > 0.85f ? Mathf.Lerp(1, 0, (t - 0.85f) / 0.15f) : Mathf.Min(1, t / 0.08f));
                }
                else
                {
                    float press = t < 0.25f ? t / 0.25f : t < 0.4f ? 1 : Mathf.Max(0, 1 - (t - 0.4f) / 0.3f);
                    // Under the target, pointing up; for a button along the bottom edge, over it pointing down.
                    bool low = spot == null && _hole.y - _hole.w < -_tutLayer.rect.height * 0.5f + 170;
                    var tip = spot != null ? new Vector2(_hole.x, _hole.y)
                            : low ? new Vector2(_hole.x - _hole.z * 0.2f, _hole.y + _hole.w * 0.45f)
                            : new Vector2(_hole.x + _hole.z * 0.3f, _hole.y - _hole.w * 0.55f);
                    _tutHand.anchoredPosition = tip + (low ? new Vector2(-18, 26) : new Vector2(18, -26)) * (1 - press);
                    _tutHand.localScale = Vector3.one * (1f - 0.1f * press);
                    _tutHand.GetComponent<Image>().color = Color.white;
                    _tutHand.localRotation = Quaternion.Euler(0, 0, low ? 198 : 18);
                }
                if (drag) _tutHand.localRotation = Quaternion.Euler(0, 0, 18);
            }

            // The bubble: top centre, or along the bottom when the spotlight is up top; never over the stick.
            _tutType += Time.unscaledDeltaTime * 55;
            if (_tutSay != say) { _tutSay = say; _tutType = 0; _tutPop = 0; }
            _coachText.text = TypeOut(say, (int)_tutType);
            // Up top between the guide card and the minimap, or (when the spotlight is up there) low down between the
            // stick and the action buttons, above the menu row.
            float H = _tutLayer.rect.height, W = _tutLayer.rect.width;
            bool high = any && _hole.y > 0;
            float by = high ? -H * 0.5f + 280 : H * 0.5f - 185;
            float bx = high ? -W * 0.5f + 420 + 480 : 160;
            if (step == Tut.Craft && _bpOpen) { by = -H * 0.5f + 400; bx = -W * 0.5f + 640; }   // over the lower blueprints, clear of the spear and its card
            _coach.anchoredPosition = Vector2.Lerp(_coach.anchoredPosition, new Vector2(bx, by), _tutPop < 0.05f ? 1 : k);
            _tutPop = Mathf.Min(1, _tutPop + Time.unscaledDeltaTime * 4);
            _coach.localScale = Vector3.one * PopIn(_tutPop);
            _coachNext.gameObject.SetActive(next && _tutType > 20);
            _coachText.rectTransform.offsetMin = new Vector2(222, next ? 92 : 58);   // clear of NEXT
            int dot = Mathf.Clamp((int)step, 0, TutDots - 1);
            for (int i = 0; i < _tutDotRow.childCount; i++)
                _tutDotRow.GetChild(i).GetComponent<Image>().color = i == dot ? TutGold : i < dot ? new Color(TutGold.r, TutGold.g, TutGold.b, 0.5f) : new Color(1, 1, 1, 0.18f);
            _tutHand.SetAsLastSibling();
        }

        void Hide()
        {
            _tutHold = false;
            if (_tutLayer.gameObject.activeSelf) _tutLayer.gameObject.SetActive(false);
            _hole = Vector4.zero; _dimA = 0;
        }

        /// <summary>The nearest working ring of a kind within range of the castaway.</summary>
        static Spot NearestSpot(string kind, float range)
        {
            var p = Player.Instance.transform.position;
            Spot best = null; float bd = range;
            foreach (var sp in Spot.All)
            {
                if (sp.Kind != kind || !sp.Active) continue;
                float d = Vector3.Distance(sp.Pos, p);
                if (d < bd) { bd = d; best = sp; }
            }
            return best;
        }

        /// <summary>Which way the hand drags the stick: toward the guide's goal as the camera sees it.</summary>
        Vector2 StickHint(Player p)
        {
            var goal = GuideArrow.GoalPosition(p.transform.position);
            var cam = CameraRig.Instance != null ? CameraRig.Instance.Cam : Camera.main;
            if (goal.HasValue && cam != null)
            {
                var a = cam.WorldToScreenPoint(p.transform.position); var b = cam.WorldToScreenPoint(goal.Value);
                var d = new Vector2(b.x - a.x, b.y - a.y);
                if (d.sqrMagnitude > 1) return d.normalized;
            }
            return new Vector2(0.7f, 0.7f);
        }

        /// <summary>A hole (centre, half size in the tutorial layer) round all of the given rects, with a margin.</summary>
        Vector4 HoleAround(RectTransform[] rts)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            var c = new Vector3[4];
            foreach (var rt in rts)
            {
                rt.GetWorldCorners(c);
                foreach (var w in c) { Vector2 l = _tutLayer.InverseTransformPoint(w); min = Vector2.Min(min, l); max = Vector2.Max(max, l); }
            }
            var half = (max - min) * 0.5f + new Vector2(16, 16);
            var mid = (max + min) * 0.5f;
            return new Vector4(mid.x, mid.y, half.x, half.y);
        }

        /// <summary>A hole round a ring on the ground, as the camera sees it (none when it's off screen).</summary>
        Vector4 HoleAt(Spot sp)
        {
            var cam = CameraRig.Instance != null ? CameraRig.Instance.Cam : Camera.main;
            if (cam == null) return Vector4.zero;
            var c = cam.WorldToScreenPoint(sp.Pos);
            if (c.z <= 0) return Vector4.zero;
            var e = cam.WorldToScreenPoint(sp.Pos + cam.transform.right * sp.Radius);
            var f = cam.WorldToScreenPoint(sp.Pos + Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized * sp.Radius);
            Vector2 lc, le, lf;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_tutLayer, c, null, out lc);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_tutLayer, e, null, out le);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_tutLayer, f, null, out lf);
            float hw = Vector2.Distance(lc, le) + 40, hh = Mathf.Max(Mathf.Abs(lf.y - lc.y), hw * 0.45f) + 40;
            var r = _tutLayer.rect;
            if (lc.x < r.xMin - hw || lc.x > r.xMax + hw || lc.y < r.yMin - hh || lc.y > r.yMax + hh) return Vector4.zero;
            return new Vector4(lc.x, lc.y, hw, hh);
        }

        /// <summary>The send-off card after the first hunt, over the dimmed island.</summary>
        void TutCard()
        {
            _coach.gameObject.SetActive(false); _tutHand.gameObject.SetActive(false);
            _tutDim.raycastTarget = true; _tutHole.Block = true; _tutHole.Hole = Vector4.zero;
            _dimA = Mathf.Lerp(_dimA, 0.72f, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 8));
            if (_tutMat != null) { _tutMat.SetVector("_Hole", Vector4.zero); _tutMat.SetColor("_Color", new Color(TutDim.r, TutDim.g, TutDim.b, _dimA)); _tutMat.SetFloat("_RimAlpha", 0); }
            if (_tutCard == null)
            {
                _tutCard = UIKit.Img("Card", _tutLayer, null, new Color(0, 0, 0, 0)).rectTransform;
                _tutCard.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1180, 640));
                _tutCard.GetComponent<Image>().raycastTarget = true;
                UIKit.Img("Edge", _tutCard, UIKit.Round, new Color(TutGold.r, TutGold.g, TutGold.b, 0.7f)).rectTransform.Fill(-4);
                UIKit.Img("Back", _tutCard, UIKit.Round, new Color(0.06f, 0.09f, 0.09f, 1f)).rectTransform.Fill(0);
                // The key art on the left, the castaway looking out to sea.
                var art = UIKit.Rect("Art", _tutCard).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(460, 568));
                art.gameObject.AddComponent<RectMask2D>();
                var key = Resources.Load<Texture2D>("UI/Story/keyart");
                if (key != null)
                {
                    var img = UIKit.Img("Key", art, Sprite.Create(key, new Rect(0, 0, key.width, key.height), new Vector2(0.5f, 0.5f)), Color.white).rectTransform;
                    // Scaled so the castaway (a third of the way in) fills the frame.
                    img.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-180, -40), new Vector2(1180, 668));
                }
                var shade = UIKit.Img("Shade", art, Gradient(), new Color(0, 0, 0, 0.55f)).rectTransform;
                shade.anchorMin = Vector2.zero; shade.anchorMax = new Vector2(1, 0.4f); shade.offsetMin = shade.offsetMax = Vector2.zero;
                var cap = UIKit.Outlined(UIKit.Label("Cap", art, "CHAPTER 1  ·  WASHED ASHORE", 22, TutGold, TextAnchor.LowerCenter), 1.5f);
                cap.rectTransform.Fill(); cap.rectTransform.offsetMin = new Vector2(10, 20);
                var head = UIKit.Outlined(UIKit.Label("H", _tutCard, "I CAN DO THIS", 66, TutGold, TextAnchor.UpperLeft), 2f);
                head.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(540, -50), new Vector2(600, 90));
                var body = UIKit.Label("B", _tutCard,
                    "Fire, a roof of sorts, a spear, and meat for the fire. Three weeks ago I was hauling sugar sacks.\n\n" +
                    "The guide on the left always says what's next, and the arrow leads the way. There's a raft in the wreck, and land on every horizon.",
                    30, Color.white, TextAnchor.UpperLeft, false);
                body.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(540, -150), new Vector2(600, 330));
                body.resizeTextForBestFit = true; body.resizeTextMinSize = 24; body.resizeTextMaxSize = UIKit.Boosted(30); body.verticalOverflow = VerticalWrapMode.Truncate;
                body.lineSpacing = 1.08f;
                var go = UIKit.Button("Go", _tutCard, "CARRY ON", new Color(0.8f, 0.62f, 0.12f), EndTutorial, 44);
                ((RectTransform)go.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-44, 40), new Vector2(340, 104));
                _tutPop = 0;
            }
            _tutPop = Mathf.Min(1, _tutPop + Time.unscaledDeltaTime * 3.5f);
            _tutCard.localScale = Vector3.one * PopIn(_tutPop);
        }

        /// <summary>Ease-out with a little overshoot, for things popping in.</summary>
        static float PopIn(float t) { t = Mathf.Clamp01(t); float s = 1.6f; t -= 1; return 0.85f + 0.15f * (t * t * ((s + 1) * t + s) + 1); }

        /// <summary>The first n visible characters of a line with colour tags, the tags kept whole.</summary>
        static string TypeOut(string s, int n)
        {
            var sb = new System.Text.StringBuilder(); int shown = 0; bool open = false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '<') { int j = s.IndexOf('>', i); if (j < 0) break; string tag = s.Substring(i, j - i + 1); if (shown < n || tag.StartsWith("</")) { sb.Append(tag); open = !tag.StartsWith("</"); } i = j; continue; }
                if (shown >= n) { if (open) { sb.Append("</color>"); open = false; } continue; }
                sb.Append(s[i]); shown++;
            }
            return sb.ToString();
        }
    }

    /// <summary>Lets taps through the tutorial's spotlight hole and blocks them everywhere else.</summary>
    public sealed class TutHole : MonoBehaviour, ICanvasRaycastFilter
    {
        public Vector4 Hole; public bool Block; public RectTransform Layer;
        public bool IsRaycastLocationValid(Vector2 sp, Camera cam)
        {
            if (!Block) return false;
            if (Hole.z <= 0 || Layer == null) return true;
            Vector2 p;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Layer, sp, cam, out p)) return true;
            return Mathf.Abs(p.x - Hole.x) > Hole.z || Mathf.Abs(p.y - Hole.y) > Hole.w;
        }
    }

    /// <summary>A gentle up-and-down, so a portrait looks alive.</summary>
    public sealed class CoachBob : MonoBehaviour
    {
        Vector2 _base; bool _set;
        void Update()
        {
            var rt = (RectTransform)transform;
            if (!_set) { _base = rt.anchoredPosition; _set = true; }
            rt.anchoredPosition = _base + new Vector2(0, Mathf.Sin(Time.unscaledTime * 1.8f) * 4);
        }
    }
}
