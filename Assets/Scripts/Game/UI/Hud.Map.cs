using System.Collections.Generic;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// The map screen's HUD: energy and doubloons, pins and island names over the map, the travel line, the location
    /// panel (picture, loot, run or walk, level required), the current-location bar and the enter button.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _mapUi, _islandUi, _pinLayer, _locPanel, _travelDots;
        Image _energyFill, _locArt;
        Text _energyText, _energyTimer, _coinText, _hereName, _locTitle, _locBody, _bigButtonText, _bigButtonSub, _lockBanner, _travelText;
        RectTransform _lootMain, _lootMaybe, _runWalk, _bigButton, _playerPin;
        Image _bigButtonRing;
        LocationDef _selected;
        RectTransform _rush, _enterBox;
        readonly Dictionary<string, RectTransform> _pins = new Dictionary<string, RectTransform>();
        RectTransform _mapTop, _mapBottom;

        /// <summary>True when a map pin or its name sits behind the energy box, the quest card or the bottom bar.</summary>
        bool UnderMapHud(RectTransform pin)
        {
            var name = (RectTransform)pin.GetChild(1);
            foreach (var r in new[] { _mapTop, _questCard, _mapBottom })
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                var c = new Vector3[4]; r.GetWorldCorners(c);
                var box = new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
                foreach (var t in new[] { pin, name })
                {
                    var p = new Vector3[4]; t.GetWorldCorners(p);
                    var pr = new Rect(p[0].x, p[0].y, p[2].x - p[0].x, p[2].y - p[0].y);
                    if (box.Overlaps(pr)) return true;
                }
            }
            return false;
        }
        public bool MapOpen { get { return GameRoot.Instance != null && GameRoot.Instance.OnMap; } }

        public void OpenMap()
        {
            CloseAll();
            GameRoot.Instance.ShowMap(true);
            Sfx.Play("map");
            Select(G.Session.State.travel != null ? null : null);
            _dirty = true;
        }

        public void CloseMap()
        {
            if (G.Session.State.travel != null) { Toast("You're on the way"); return; }
            Select(null);
            GameRoot.Instance.ShowMap(false);
        }

        void BuildMap(RectTransform safe)
        {
            _mapUi = UIKit.Rect("Map UI", safe);
            _mapUi.Fill();
            _pinLayer = UIKit.Rect("Pins", _mapUi); _pinLayer.Fill();
            _travelDots = UIKit.Rect("Travel", _pinLayer); _travelDots.Fill();

            // Energy and doubloons, top left.
            var top = _mapTop = UIKit.Rect("Energy", _mapUi).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -26), new Vector2(460, 180));
            var bolt = UIKit.Img("Bolt", top, UIKit.Symbol("bolt"), UIKit.Amber);
            bolt.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -4), new Vector2(46, 56));
            var el = UIKit.Outlined(UIKit.Label("Label", top, "Energy", 38, Color.white), 1.5f);
            el.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(54, 0), new Vector2(200, 56));
            var box = UIKit.Img("Value", top, UIKit.Round, new Color(0.55f, 0.47f, 0.18f, 0.92f));
            box.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(270, -6), new Vector2(170, 46));
            _energyText = UIKit.Label("Text", box.transform, "200/200", 34, Color.white, TextAnchor.MiddleCenter);
            _energyText.rectTransform.Fill();
            var bar = UIKit.Img("Bar", top, UIKit.Round, new Color(0, 0, 0, 0.45f));
            bar.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -60), new Vector2(440, 14));
            _energyFill = UIKit.Img("Fill", bar.transform, UIKit.Round, new Color(1f, 0.78f, 0.15f));
            _energyFill.rectTransform.anchorMin = Vector2.zero; _energyFill.rectTransform.anchorMax = Vector2.one;
            _energyFill.rectTransform.offsetMin = _energyFill.rectTransform.offsetMax = Vector2.zero;
            _energyTimer = UIKit.Outlined(UIKit.Label("Timer", top, "", 30, Color.white, TextAnchor.UpperRight), 1.5f);
            _energyTimer.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(240, -78), new Vector2(200, 40));
            var coin = UIKit.Img("Coin", top, UIKit.Symbol("coin"), new Color(1f, 0.82f, 0.3f));
            coin.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -112), new Vector2(60, 60));
            _coinText = UIKit.Outlined(UIKit.Label("Coins", top, "0", 36, Color.white), 1.5f);
            _coinText.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(76, -118), new Vector2(200, 50));
            var plus = UIKit.Button("Plus", top, "+", new Color(1, 1, 1, 0.12f), OpenShop, 46);
            _later.Add(plus.gameObject);
            // Tap the energy bar to refill it with doubloons.
            var refill = UIKit.Button("Refill", top, null, new Color(0, 0, 0, 0), () => Confirm("Refill energy?", "Fill your energy to " + World.EnergyMax + " now for " + Session.RefillEnergyCost + " doubloons.\nYou have " + G.Session.State.coins + ".", "REFILL", () => { if (G.Session.RefillEnergy()) Sfx.Play("coin"); }));
            ((RectTransform)refill.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(450, 100));
            ((RectTransform)plus.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(230, -116), new Vector2(64, 56));

            // Current location, bottom bar.
            var strip = UIKit.Img("Bottom", _mapUi, null, new Color(0.13f, 0.15f, 0.16f, 0.96f));
            _mapBottom = strip.rectTransform;
            strip.rectTransform.anchorMin = new Vector2(0, 0); strip.rectTransform.anchorMax = new Vector2(1, 0);
            strip.rectTransform.pivot = new Vector2(0.5f, 0); strip.rectTransform.offsetMin = new Vector2(-200, -200); strip.rectTransform.offsetMax = new Vector2(200, 150);
            strip.raycastTarget = true;
            var cl = UIKit.Label("Current", strip.transform, "CURRENT LOCATION:", 30, new Color(1, 1, 1, 0.85f), TextAnchor.UpperLeft, false);
            cl.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(330, -24), new Vector2(600, 40));
            _hereName = UIKit.Label("Here", strip.transform, "", 64, Color.white, TextAnchor.UpperLeft);
            _hereName.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(328, -58), new Vector2(900, 80));
            float bx = -1030;
            // Captioned like the field HUD's bottom row, so nobody has to guess what a symbol opens.
            foreach (var (sym, label, act) in new (string, string, System.Action)[] { ("radio", "RADIO", OpenRadio), ("gift", "TRADE", OpenOffers), ("coin", "SHOP", OpenShop),
                ("pack", "BAG", () => { _bagOpen = !_bagOpen; _dirty = true; }), ("?", "HELP", () => OpenHelp()) })
            {
                var b = UIKit.Button(label, strip.transform, null, new Color(1, 1, 1, 0.08f), act);
                ((RectTransform)b.transform).Place(new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(bx, -24), new Vector2(104, 104));
                if (label == "RADIO" || label == "TRADE" || label == "SHOP") _later.Add(b.gameObject);
                if (sym == "?")
                {
                    var q = UIKit.Label("Icon", b.transform, "?", 56, new Color(0.85f, 0.88f, 0.9f), TextAnchor.MiddleCenter, false);
                    q.rectTransform.Fill(); q.rectTransform.offsetMin = new Vector2(0, 24);
                }
                else
                {
                    var ic = UIKit.Img("Icon", b.transform, UIKit.Symbol(sym), sym == "coin" ? new Color(1f, 0.82f, 0.3f) : new Color(0.85f, 0.88f, 0.9f));
                    ic.preserveAspect = true;
                    ic.rectTransform.Fill(); ic.rectTransform.offsetMin = new Vector2(24, 32); ic.rectTransform.offsetMax = new Vector2(-24, -10);
                }
                var cap = UIKit.Label("Cap", b.transform, label, 22, new Color(1, 1, 1, 0.8f), TextAnchor.LowerCenter, false);
                cap.rectTransform.anchorMin = Vector2.zero; cap.rectTransform.anchorMax = new Vector2(1, 0); cap.rectTransform.pivot = new Vector2(0.5f, 0);
                cap.rectTransform.anchoredPosition = new Vector2(0, 4); cap.rectTransform.sizeDelta = new Vector2(0, 26);
                bx += 124;
            }
            // The big round button: back to the island, or enter where the journey ended.
            var big = UIKit.Button("Big", _mapUi, null, new Color(0.13f, 0.15f, 0.16f, 1), OnBigButton);
            big.GetComponent<Image>().sprite = UIKit.Circle; big.GetComponent<Image>().type = Image.Type.Simple;
            _bigButton = (RectTransform)big.transform;
            _bigButton.Place(new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-150, 112), new Vector2(230, 230));
            _bigButtonRing = UIKit.Img("Ring", _bigButton, UIKit.RingSprite, new Color(0.3f, 0.75f, 0.7f));
            _bigButtonRing.rectTransform.Fill(10);
            _bigButtonRing.type = Image.Type.Filled; _bigButtonRing.fillMethod = Image.FillMethod.Radial360; _bigButtonRing.fillOrigin = 2;
            _bigButtonText = UIKit.Label("Text", _bigButton, "BACK", 40, new Color(0.45f, 0.95f, 0.85f), TextAnchor.MiddleCenter);
            _bigButtonText.rectTransform.Fill(); _bigButtonText.rectTransform.offsetMin = new Vector2(0, 18);
            _bigButtonSub = UIKit.Label("Sub", _bigButton, "", 26, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, false);
            _bigButtonSub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -32), new Vector2(170, 38)); // one line under BACK
            _bigButtonSub.resizeTextMaxSize = Mathf.Max(26, _bigButtonSub.fontSize); _bigButtonSub.resizeTextForBestFit = true; _bigButtonSub.resizeTextMinSize = 16; // "Fisherman's Pier" stays inside the ring

            var rushBtn = UIKit.Button("Rush", _mapUi, "RUSH", new Color(0.8f, 0.62f, 0.12f), () =>
            {
                var s = G.Session;
                Confirm("Arrive now?", "Skip the rest of the journey for " + s.RushTravelCost + " doubloons.\nYou have " + s.State.coins + ".", "ARRIVE NOW", () => { if (s.RushTravel()) Sfx.Play("coin"); });
            }, 28);
            _rush = (RectTransform)rushBtn.transform;
            _rush.Place(new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-150, 250), new Vector2(230, 60));
            _travelText = UIKit.Outlined(UIKit.Label("Travel", _mapUi, "", 38, Color.white, TextAnchor.MiddleCenter), 2f);
            _travelText.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-200, 180), new Vector2(900, 60));
            _lockBanner = UIKit.Outlined(UIKit.Label("Locked", _mapUi, "", 46, new Color(1f, 0.32f, 0.25f), TextAnchor.MiddleCenter), 2f);
            _lockBanner.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-330, 190), new Vector2(800, 80));
            var stripes = UIKit.Img("Stripes", _lockBanner.transform, UIKit.Round, new Color(0.25f, 0.06f, 0.05f, 0.75f));
            stripes.rectTransform.Fill(); stripes.transform.SetAsFirstSibling();

            BuildLocationPanel();
            // ENTER, in brackets under the island you're at (or have just reached).
            _enterBox = UIKit.Rect("Enter", _pinLayer).Place(Vector2.zero, new Vector2(0.5f, 1), Vector2.zero, new Vector2(240, 84));
            var eb = UIKit.Button("Enter", _enterBox, "ENTER", new Color(0.12f, 0.2f, 0.2f, 0.75f), OnBigButton, 40);
            ((RectTransform)eb.transform).Fill();
            eb.GetComponentInChildren<Text>().color = new Color(0.4f, 0.95f, 0.85f);
            foreach (var corner in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) })
            {
                var c = UIKit.Img("Corner", _enterBox, null, new Color(0.85f, 0.95f, 0.92f));
                c.rectTransform.Place(corner, corner, Vector2.zero, new Vector2(26, 4));
                var c2 = UIKit.Img("Corner", _enterBox, null, new Color(0.85f, 0.95f, 0.92f));
                c2.rectTransform.Place(corner, corner, Vector2.zero, new Vector2(4, 26));
            }
            _playerPin = UIKit.Img("You", _pinLayer, UIKit.Symbol("person"), Color.white).rectTransform;
            _playerPin.Place(Vector2.zero, new Vector2(0.5f, 0), Vector2.zero, new Vector2(70, 84));
            var pbg = UIKit.Img("Back", _playerPin, UIKit.Symbol("pin"), new Color(0.15f, 0.17f, 0.18f, 0.95f));
            pbg.rectTransform.Fill(-14); pbg.transform.SetAsFirstSibling();
            _mapUi.gameObject.SetActive(false);
        }

        void BuildLocationPanel()
        {
            _locPanel = UIKit.Img("Location", _mapUi, null, new Color(0.33f, 0.35f, 0.32f, 1f)).rectTransform;
            _locPanel.GetComponent<Image>().raycastTarget = true;
            _locPanel.anchorMin = new Vector2(1, 0); _locPanel.anchorMax = new Vector2(1, 1); _locPanel.pivot = new Vector2(1, 0.5f);
            _locPanel.offsetMin = new Vector2(-660, 0); _locPanel.offsetMax = new Vector2(200, 200);
            _locArt = UIKit.Img("Art", _locPanel, null, new Color(0.5f, 0.6f, 0.55f));
            _locArt.rectTransform.anchorMin = new Vector2(0, 1); _locArt.rectTransform.anchorMax = new Vector2(1, 1); _locArt.rectTransform.pivot = new Vector2(0.5f, 1);
            _locArt.rectTransform.offsetMin = new Vector2(0, -430); _locArt.rectTransform.offsetMax = new Vector2(-200, -200);
            _locArt.preserveAspect = false;
            var shade = UIKit.Img("Shade", _locArt.transform, null, new Color(0, 0, 0, 0.25f));
            shade.rectTransform.anchorMin = new Vector2(0, 1); shade.rectTransform.anchorMax = new Vector2(1, 1); shade.rectTransform.pivot = new Vector2(0.5f, 1);
            shade.rectTransform.offsetMin = new Vector2(0, -90); shade.rectTransform.offsetMax = Vector2.zero;
            _locTitle = UIKit.Outlined(UIKit.Label("Title", _locArt.transform, "", 48, Color.white, TextAnchor.UpperLeft), 2f);
            _locTitle.rectTransform.Fill(); _locTitle.rectTransform.offsetMin = new Vector2(34, 0); _locTitle.rectTransform.offsetMax = new Vector2(-100, -22);
            var x = UIKit.Button("Close", _locArt.transform, "✕", new Color(0, 0, 0, 0), () => Select(null), 60);
            ((RectTransform)x.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -10), new Vector2(84, 84));
            _locBody = UIKit.Label("Body", _locPanel, "", 32, new Color(1, 1, 1, 0.9f), TextAnchor.UpperLeft, false);
            _locBody.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -446), new Vector2(600, 190));
            _lootMain = UIKit.Rect("Main loot", _locPanel).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -680), new Vector2(600, 110));
            _lootMaybe = UIKit.Rect("Possible loot", _locPanel).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -835), new Vector2(600, 110));
            _runWalk = UIKit.Rect("Run walk", _locPanel).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -960), new Vector2(600, 260));
            _locPanel.gameObject.SetActive(false);
        }

        void Select(LocationDef l)
        {
            _selected = l;
            if (MapView.Instance != null)
            {
                MapView.Instance.Select(l);
                if (l != null) MapView.Instance.FocusOn(l);
            }
            if (l != null) Sfx.Play("click");
            _dirty = true;
            RefreshLocationPanel();
        }

        void RefreshLocationPanel()
        {
            var l = _selected;
            _locPanel.gameObject.SetActive(l != null);
            if (l == null) return;
            var s = G.Session;
            _locTitle.text = l.Name.ToUpper();
            var art = UIKit.Place(l.Id, 2.85f);
            _locArt.sprite = art;
            _locArt.color = art != null ? Color.white : PlaceColor(l);
            Clear(_lootMain); Clear(_lootMaybe); Clear(_runWalk);
            bool loot = l.MainLoot.Length > 0;
            _locBody.text = loot ? "" : l.Blurb;
            if (loot)
            {
                _locBody.text = l.Blurb;
                UIKit.SetSize(_locBody, 26);
                LootRow(_lootMain, "Main loot:", l.MainLoot, new Color(0.85f, 0.65f, 0.25f));
                LootRow(_lootMaybe, "Possible loot:", l.PossibleLoot, new Color(0.3f, 0.7f, 0.68f));
            }
            else UIKit.SetSize(_locBody, 32);
            // Run or walk (or why not).
            var travel = s.State.travel;
            if (l.Id == s.State.location && travel == null)
            {
                var here = UIKit.Label("Here", _runWalk, "You are here", 34, UIKit.Green, TextAnchor.MiddleCenter);
                here.rectTransform.Fill();
            }
            else if (!s.Unlocked(l)) { }
            else if (travel == null)
            {
                RoundButton(_runWalk, 0, "sail", "SAIL", s.RunCost(l) + " energy", Duration(s.RunSeconds(l)), () => Go(l, true), s.State.energy >= s.RunCost(l));
                RoundButton(_runWalk, 1, "paddle", "PADDLE", "free", Duration(s.WalkSeconds(l)), () => Go(l, false), true);
            }
        }

        static Color PlaceColor(LocationDef l)
        {
            switch (l.Biome)
            {
                case Biome.PirateCamp: return new Color(0.45f, 0.2f, 0.16f);
                case Biome.Cave: return new Color(0.25f, 0.24f, 0.28f);
                case Biome.Lighthouse: return new Color(0.4f, 0.45f, 0.55f);
                case Biome.Ridge: return new Color(0.55f, 0.52f, 0.45f);
                default: return new Color(0.3f, 0.55f, 0.45f);
            }
        }

        void Go(LocationDef l, bool run, bool roomChecked = false)
        {
            var s = G.Session;
            var why = s.TravelBlocked(l, run);
            if (why != null) { Toast(why); return; }
            if (!roomChecked && TripRoomCheck(() => Go(l, run, true))) return;
            s.Travel(l, run);
            Reminders.AskOnce(); // "Land ho!" is the first reminder worth having
            Sfx.Play("depart"); // the ship's bell, under sail or paddling
            Select(null);
        }

        void OnBigButton()
        {
            var s = G.Session;
            if (s.State.travel != null)
            {
                if (s.Arrived) GameRoot.Instance.EnterArrived();
                else Toast("Still on the way...");
                return;
            }
            CloseMap();
        }

        static void Clear(RectTransform r) { for (int i = r.childCount - 1; i >= 0; i--) Destroy(r.GetChild(i).gameObject); }

        void LootRow(RectTransform row, string title, string[] items, Color frame)
        {
            var t = UIKit.Label("Title", row, title, 32, new Color(1, 1, 1, 0.85f), TextAnchor.UpperLeft, false);
            t.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 44), new Vector2(500, 40));
            for (int i = 0; i < items.Length; i++)
            {
                var cell = UIKit.Img("Item", row, UIKit.Round, new Color(0.25f, 0.26f, 0.24f, 0.95f));
                cell.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * 104, -4), new Vector2(96, 96));
                var edge = UIKit.Img("Edge", cell.transform, UIKit.Round, frame);
                edge.rectTransform.Fill(); edge.type = Image.Type.Sliced; edge.fillCenter = false;
                if (items[i] == "doubloon") { var c = UIKit.Img("Icon", cell.transform, UIKit.Symbol("coin"), new Color(1f, 0.82f, 0.3f)); c.rectTransform.Fill(16); }
                else UIKit.ItemIcon(cell.transform, items[i], 86);
            }
        }

        void RoundButton(RectTransform parent, int slot, string sym, string label, string cost, string time, System.Action act, bool ok)
        {
            var b = UIKit.Button(label, parent, null, ok ? new Color(0.15f, 0.55f, 0.55f) : new Color(0.3f, 0.3f, 0.3f), act);
            b.GetComponent<Image>().sprite = UIKit.Circle; b.GetComponent<Image>().type = Image.Type.Simple;
            ((RectTransform)b.transform).Place(new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(170 + slot * 240, 0), new Vector2(150, 150));
            var ic = UIKit.Img("Icon", b.transform, UIKit.Symbol(sym), Color.white);
            ic.rectTransform.Fill(28); ic.rectTransform.offsetMin = new Vector2(28, 44);
            var c = UIKit.Label("Cost", b.transform, cost, 24, Color.white, TextAnchor.LowerCenter, false);
            c.rectTransform.Fill(14);
            var l = UIKit.Label("Label", parent, label, 36, new Color(0.45f, 0.95f, 0.85f), TextAnchor.UpperCenter);
            l.rectTransform.Place(new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(170 + slot * 240, -158), new Vector2(200, 44));
            var t = UIKit.Label("Time", parent, time, 28, new Color(1, 1, 1, 0.75f), TextAnchor.UpperCenter, false);
            t.rectTransform.Place(new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(170 + slot * 240, -198), new Vector2(200, 40));
        }

        static string Duration(double seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt((float)seconds));
            if (s >= 3600) return (s / 3600) + "h " + (s % 3600 / 60) + "m";
            if (s >= 60) return (s / 60) + "m " + (s % 60).ToString("00") + "s";
            return s + "s";
        }

        static string Clock(double seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt((float)seconds));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        void UpdateMap()
        {
            bool open = MapOpen;
            if (_mapUi.gameObject.activeSelf != open) { _mapUi.gameObject.SetActive(open); _islandUi.gameObject.SetActive(!open); _stickBase.gameObject.SetActive(!open); }
            if (!open) return;
            var s = G.Session;
            var map = MapView.Instance;
            // Energy and coins.
            _energyFill.rectTransform.anchorMax = new Vector2(s.State.energy / World.EnergyMax, 1);
            _energyText.text = Mathf.FloorToInt(s.State.energy) + "/" + World.EnergyMax;
            _energyTimer.text = s.State.energy >= World.EnergyMax ? "" : Clock(s.NextEnergyIn);
            _coinText.text = s.State.coins.ToString();
            _hereName.text = (s.State.travel != null ? "AT SEA" : s.Here.Name.ToUpper());

            // ENTER under the island you can step onto now (placed first: that island's name goes below it).
            var enterAt = s.State.travel != null ? (s.Arrived ? World.Location(s.State.travel.to) : null) : s.Here;
            var ec = enterAt != null
                ? MapToCanvas(MapView.World(enterAt) - new Vector3(0, 0, MapView.IslandRadius(enterAt) * 0.6f))
                : Vector3.zero;

            // Pins and names over the islands.
            foreach (var l in World.Locations)
            {
                var pin = Pin(l);
                var c = MapToCanvas(MapView.World(l) + Vector3.up * 6);
                pin.gameObject.SetActive(c.z > 0);
                pin.anchoredPosition = c;
                var name = pin.GetChild(1).GetComponent<RectTransform>();
                float ny = -MapPixels(MapView.IslandRadius(l)) * 0.8f - 40;
                // Zoomed out, the island is small and ENTER (84 high, hanging from ec) would cover its name.
                if (l == enterAt) ny = Mathf.Min(ny, ec.y - 84 - 32 - c.y);
                name.anchoredPosition = new Vector2(0, ny);
                // Fade a pin that has slid behind the HUD (pan to bring it out) instead of drawing it under the panels.
                var cg = pin.GetComponent<CanvasGroup>();
                if (cg == null) cg = pin.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = Mathf.MoveTowards(cg.alpha, UnderMapHud(pin) ? 0f : 1f, Time.unscaledDeltaTime * 6);
            }
            // The castaway: on the island they're at, or along the way.
            Vector3 me;
            var tr = s.State.travel;
            if (tr != null)
            {
                Vector3 a = MapView.World(World.Location(tr.from)), b = MapView.World(World.Location(tr.to));
                me = Vector3.Lerp(a, b, s.TravelProgress);
                DrawDots(a, b, s.TravelProgress);
            }
            else { me = MapView.World(s.Here); DrawDots(Vector3.zero, Vector3.zero, -1); }
            var mc = MapToCanvas(me + Vector3.up * 6);
            _playerPin.anchoredPosition = (Vector2)mc + new Vector2(tr != null ? 0 : 60, tr != null ? 10 : 30) + Vector2.up * Mathf.Abs(Mathf.Sin(UnityEngine.Time.time * (tr != null ? 9 : 2))) * 8;

            // Big button: back, the journey's countdown, or enter.
            if (tr != null)
            {
                bool there = s.Arrived;
                _bigButtonText.text = there ? "ENTER" : Clock(tr.arriveAt - s.RealNow());
                _bigButtonText.color = there ? new Color(0.5f, 1f, 0.6f) : Color.white;
                _bigButtonSub.text = there ? World.Location(tr.to).Name : (tr.arriveAt - tr.leftAt < 20 ? "Sailing..." : "Paddling...");
                _bigButtonRing.fillAmount = there ? 1 : s.TravelProgress;
                _bigButtonRing.color = there ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.3f, 0.75f, 0.7f);
                _bigButton.localScale = Vector3.one * (there ? 1 + 0.05f * Mathf.Sin(UnityEngine.Time.time * 6) : 1);
                _travelText.text = there ? "" : "On the way to " + World.Location(tr.to).Name;
                _rush.gameObject.SetActive(!there);
                if (!there) _rush.GetComponentInChildren<Text>().text = "RUSH  " + s.RushTravelCost;
            }
            else
            {
                _bigButtonText.text = "BACK";
                _bigButtonText.color = new Color(0.45f, 0.95f, 0.85f);
                _bigButtonSub.text = s.Here.Name;
                _bigButtonRing.fillAmount = 1;
                _bigButtonRing.color = new Color(0.3f, 0.75f, 0.7f);
                _bigButton.localScale = Vector3.one;
                _travelText.text = "";
                _rush.gameObject.SetActive(false);
            }
            _enterBox.gameObject.SetActive(enterAt != null);
            if (enterAt != null)
            {
                _enterBox.anchoredPosition = ec;
                _enterBox.localScale = Vector3.one * (tr != null ? 1 + 0.05f * Mathf.Sin(UnityEngine.Time.time * 6) : 1);
            }
            _lockBanner.gameObject.SetActive(_selected != null && !s.Unlocked(_selected));
            if (_selected != null) _lockBanner.text = "LEVEL " + _selected.Level + " REQUIRED";

            // Taps on the map choose an island.
            Vector2 tap;
            bool overUi = OverUI(Input.touchCount > 0 ? Input.GetTouch(0).fingerId : -1);
            if (map.UpdateInput(overUi || _radioOpen || _offersOpen || _shopOpen || _helpOpen || StoryPlaying || _confirm != null || _bpOpen || _pathOpen || InvOpen, out tap))
            {
                LocationDef best = null;
                float bd = 160 / Scale;
                foreach (var l in World.Locations)
                {
                    var sp = map.Cam.WorldToScreenPoint(MapView.World(l));
                    float d = Vector2.Distance(sp, tap);
                    float r = MapPixels(MapView.IslandRadius(l)) / Scale;
                    if (d < Mathf.Max(bd, r) && d < bd + r) { bd = d; best = l; }
                }
                Select(best);
            }
        }

        /// <summary>Canvas pixels per map metre near the focus (for sizing things to the islands).</summary>
        float MapPixels(float metres)
        {
            var cam = MapView.Instance.Cam;
            var a = cam.WorldToScreenPoint(MapView.Origin);
            var b = cam.WorldToScreenPoint(MapView.Origin + Vector3.right * metres);
            return Mathf.Abs(b.x - a.x) * Scale;
        }

        Vector3 MapToCanvas(Vector3 world)
        {
            var sp = MapView.Instance.Cam.WorldToScreenPoint(world);
            var off = ((RectTransform)_worldLayer.parent).offsetMin;
            return new Vector3(sp.x * Scale - off.x, sp.y * Scale - off.y, sp.z);
        }

        RectTransform Pin(LocationDef l)
        {
            RectTransform pin;
            if (_pins.TryGetValue(l.Id, out pin)) { Recolor(pin, l); return pin; }
            pin = UIKit.Rect("Pin " + l.Id, _pinLayer).Place(Vector2.zero, new Vector2(0.5f, 0), Vector2.zero, new Vector2(72, 92));
            var icon = UIKit.Img("Icon", pin, UIKit.Symbol(l.Home ? "home" : "pin"), Color.white);
            icon.rectTransform.Fill();
            var name = UIKit.Outlined(UIKit.Label("Name", pin, l.Name, 34, Color.white, TextAnchor.MiddleCenter), 2f);
            name.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 50));
            _pins[l.Id] = pin;
            Recolor(pin, l);
            return pin;
        }

        void Recolor(RectTransform pin, LocationDef l)
        {
            var s = G.Session;
            var icon = pin.GetChild(0).GetComponent<Image>();
            bool rescue = false;
            foreach (var sig in World.Signals) if (sig.Location == l.Id && s.Heard(sig) && !s.Rescued(sig)) rescue = true;
            icon.color = l.Home ? new Color(0.92f, 0.92f, 0.9f) : !s.Unlocked(l) ? new Color(0.85f, 0.2f, 0.2f) : rescue ? new Color(1f, 0.75f, 0.2f) : new Color(0.35f, 0.85f, 0.3f);
            pin.localScale = Vector3.one * (rescue ? 1.15f + 0.08f * Mathf.Sin(UnityEngine.Time.time * 5) : 1);
        }

        readonly List<Image> _dots = new List<Image>();

        void DrawDots(Vector3 a, Vector3 b, float progress)
        {
            int n = progress < 0 ? 0 : 22;
            for (int i = 0; i < n; i++)
            {
                while (_dots.Count <= i)
                {
                    var d = UIKit.Img("Dot", _travelDots, UIKit.Circle, Color.white);
                    d.rectTransform.Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 14));
                    _dots.Add(d);
                }
                float t = (i + 0.5f) / n;
                var c = MapToCanvas(Vector3.Lerp(a, b, t) + Vector3.up * 2);
                _dots[i].gameObject.SetActive(true);
                _dots[i].rectTransform.anchoredPosition = c;
                _dots[i].color = t <= progress ? new Color(1, 1, 1, 0.35f) : new Color(1f, 0.9f, 0.4f, 0.95f);
            }
            for (int i = n; i < _dots.Count; i++) _dots[i].gameObject.SetActive(false);
        }
    }
}
