using System.Collections.Generic;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>The backpack, the crafting/cooking list at the workbench and campfire, and the sea chest.</summary>
    public sealed partial class Hud
    {
        RectTransform _bagPanel, _stationPanel, _bagGrid, _stationList, _chestGrid;
        Text _stationTitle, _bagTitle, _bagHint;
        Button _storeAll;
        string _station;
        bool _bagOpen;
        Image _weaponIcon;
        RectTransform _weaponSlot;
        string _weaponShown = "-";

        void BuildButtons(RectTransform safe)
        {
            // Backpack button, bottom right, with the held weapon beside it.
            var bag = UIKit.Button("Backpack", safe, null, new Color(0.24f, 0.3f, 0.2f, 0.95f), ToggleBag);
            bag.GetComponent<Image>().sprite = UIKit.Circle;
            bag.GetComponent<Image>().type = Image.Type.Simple;
            ((RectTransform)bag.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(150, 150));
            var ic = UIKit.Img("Icon", bag.transform, UIKit.Symbol("pack"), UIKit.Sand);
            ic.rectTransform.Fill(34);
            var lbl = UIKit.Outlined(UIKit.Label("Label", bag.transform, "BAG", 26, Color.white, TextAnchor.LowerCenter), 1.5f);
            lbl.rectTransform.Fill(); lbl.rectTransform.offsetMin = new Vector2(0, -30);

            // The map, top right.
            var map = UIKit.Button("Map", safe, null, new Color(0.2f, 0.32f, 0.38f, 0.95f), OpenMap);
            map.GetComponent<Image>().sprite = UIKit.Circle; map.GetComponent<Image>().type = Image.Type.Simple;
            ((RectTransform)map.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -30), new Vector2(130, 130));
            var mi = UIKit.Img("Icon", map.transform, UIKit.Symbol("map"), UIKit.Sand);
            mi.rectTransform.Fill(28);
            var ml = UIKit.Outlined(UIKit.Label("Label", map.transform, "MAP", 26, Color.white, TextAnchor.LowerCenter), 1.5f);
            ml.rectTransform.Fill(); ml.rectTransform.offsetMin = new Vector2(0, -30);

            // Help, beside the map.
            var help = UIKit.Button("Help", safe, "?", new Color(0.2f, 0.32f, 0.38f, 0.95f), () => OpenHelp(), 60);
            help.GetComponent<Image>().sprite = UIKit.Circle; help.GetComponent<Image>().type = Image.Type.Simple;
            ((RectTransform)help.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-190, -40), new Vector2(100, 100));

            _weaponSlot = UIKit.Img("Weapon", safe, UIKit.Circle, UIKit.Ink).rectTransform;
            _weaponSlot.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-210, 50), new Vector2(110, 110));
        }

        void BuildPanels(RectTransform safe)
        {
            // Backpack: a 4x4 grid on the right.
            _bagPanel = UIKit.Img("Bag Panel", safe, UIKit.Round, UIKit.Panel).rectTransform;
            _bagPanel.GetComponent<Image>().raycastTarget = true;
            _bagPanel.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 40), new Vector2(560, 700));
            _bagTitle = UIKit.Label("Title", _bagPanel, "BACKPACK", 40, UIKit.Sand, TextAnchor.UpperCenter);
            _bagTitle.rectTransform.Fill(); _bagTitle.rectTransform.offsetMax = new Vector2(0, -14);
            var close = UIKit.Button("Close", _bagPanel, "X", UIKit.Red, () => { _bagOpen = false; _dirty = true; }, 36);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(64, 64));
            _bagGrid = UIKit.Rect("Grid", _bagPanel).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(500, 500));
            _bagHint = UIKit.Label("Hint", _bagPanel, "Tap food to eat, a weapon to hold it.", 26, new Color(1, 1, 1, 0.7f), TextAnchor.LowerCenter, false);
            _bagHint.rectTransform.Fill(); _bagHint.rectTransform.offsetMin = new Vector2(16, 18);
            _bagPanel.gameObject.SetActive(false);

            // Station: recipe list (or the chest grid) on the left of the backpack.
            _stationPanel = UIKit.Img("Station Panel", safe, UIKit.Round, UIKit.Panel).rectTransform;
            _stationPanel.GetComponent<Image>().raycastTarget = true;
            _stationPanel.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-610, 40), new Vector2(620, 700));
            _stationTitle = UIKit.Label("Title", _stationPanel, "", 40, UIKit.Sand, TextAnchor.UpperCenter);
            _stationTitle.rectTransform.Fill(); _stationTitle.rectTransform.offsetMax = new Vector2(0, -14);
            _stationList = UIKit.Rect("List", _stationPanel).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(580, 600));
            _chestGrid = UIKit.Rect("Chest", _stationPanel).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(560, 480));
            _storeAll = UIKit.Button("Store", _stationPanel, "STORE RESOURCES", new Color(0.25f, 0.55f, 0.3f), () => { if (G.Session.StoreResources() > 0) Sfx.Play("deliver"); }, 32);
            ((RectTransform)_storeAll.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(380, 80));
            _stationPanel.gameObject.SetActive(false);
        }

        void ToggleBag()
        {
            _bagOpen = !_bagOpen;
            _dirty = true;
        }

        public void OpenStation(string id)
        {
            if (id == "radio") { if (!_radioOpen && _station != "radio") { _station = "radio"; OpenRadio(); } return; }
            if (_station == id) return;
            _station = id;
            if (id == "chest") _bagOpen = true;
            _dirty = true;
        }

        public void CloseStation(string id)
        {
            if (_station != id) return;
            if (id == "radio") { _station = null; return; }
            if (id == "chest") _bagOpen = false;
            _station = null;
            _dirty = true;
        }

        void CloseAll() { _station = null; _bagOpen = false; _dirty = true; }

        void RefreshPanels()
        {
            var s = G.Session;
            _bagPanel.gameObject.SetActive(_bagOpen);
            if (_bagOpen)
            {
                int used = 0;
                foreach (var sl in s.State.backpack) if (!sl.Empty) used++;
                _bagTitle.text = "BACKPACK  " + used + "/" + s.State.backpack.Count;
                _bagHint.text = _station == "chest" ? "Tap an item to put it in the chest." : "Tap food to eat, a weapon to hold it.";
                Grid(_bagGrid, s.State.backpack, 4, 118, i =>
                {
                    if (_station == "chest") { if (s.StoreSlot(i) > 0) Sfx.Play("deliver"); }
                    else if (s.Use(i)) Sfx.Play(Content.Item(s.State.backpack[i].id ?? "wood") != null ? "eat" : "click");
                }, s.State.equipped);
            }
            _stationPanel.gameObject.SetActive(_station != null && _station != "radio");
            if (_station != null && _station != "radio")
            {
                bool chest = _station == "chest";
                // Sits at the right edge, or beside the backpack when that's open; as tall as its contents.
                int rows = 0;
                foreach (var r in Content.Recipes.Values) if (r.Station == _station && s.Knows(r.Id)) rows++;
                float h = chest ? 700 : 110 + rows * 100;
                _stationPanel.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(_bagOpen ? -610 : -30, 60), new Vector2(620, h));
                _stationTitle.text = Content.Builds[_station].Name.ToUpper();
                _chestGrid.gameObject.SetActive(chest);
                _storeAll.gameObject.SetActive(chest);
                _stationList.gameObject.SetActive(!chest);
                if (chest) Grid(_chestGrid, s.State.chest, 6, 92, i => { if (s.TakeFromChest(i) > 0) Sfx.Play("gather"); }, -1);
                else Recipes(_station);
            }
            // The held weapon.
            var w = s.WeaponId;
            if (w != _weaponShown)
            {
                _weaponShown = w;
                for (int i = _weaponSlot.childCount - 1; i >= 0; i--) Destroy(_weaponSlot.GetChild(i).gameObject);
                if (w != null) UIKit.ItemIcon(_weaponSlot, w, 84);
                else { var f = UIKit.Label("Fists", _weaponSlot, "FISTS", 24, UIKit.Sand, TextAnchor.MiddleCenter); f.rectTransform.Fill(); }
            }
        }

        /// <summary>Rebuilds a grid of item slots.</summary>
        void Grid(RectTransform grid, List<Slot> slots, int cols, float cell, System.Action<int> tap, int equipped)
        {
            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
            float w = cols * cell;
            for (int i = 0; i < slots.Count; i++)
            {
                int idx = i;
                var sl = slots[i];
                int r = i / cols, c = i % cols;
                var b = UIKit.Button("Slot", grid, null, i == equipped ? new Color(0.35f, 0.6f, 0.3f, 0.95f) : new Color(1, 1, 1, 0.1f), () => tap(idx));
                ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(-w / 2 + c * cell + 4, -r * cell - 4), new Vector2(cell - 8, cell - 8));
                if (sl.Empty) { b.interactable = false; continue; }
                UIKit.ItemIcon(b.transform, sl.id, cell * 0.66f);
                var d = Content.Item(sl.id);
                if (sl.n > 1)
                {
                    var n = UIKit.Outlined(UIKit.Label("Count", b.transform, sl.n.ToString(), Mathf.RoundToInt(cell * 0.26f), Color.white, TextAnchor.LowerRight), 1.5f);
                    n.rectTransform.Fill(6);
                }
                if (d != null && d.Durability > 0)
                {
                    var bar = UIKit.Img("Wear", b.transform, UIKit.Round, UIKit.Ink);
                    bar.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(cell * 0.7f, 10));
                    float left = 1 - sl.wear / (float)d.Durability;
                    var f = UIKit.Img("Fill", bar.transform, UIKit.Round, left > 0.3f ? UIKit.Green : UIKit.Red);
                    f.rectTransform.anchorMin = Vector2.zero; f.rectTransform.anchorMax = new Vector2(left, 1);
                    f.rectTransform.offsetMin = f.rectTransform.offsetMax = Vector2.zero;
                }
            }
        }

        void Recipes(string station)
        {
            var s = G.Session;
            for (int i = _stationList.childCount - 1; i >= 0; i--) Destroy(_stationList.GetChild(i).gameObject);
            var job = s.Job(station);
            int row = 0;
            foreach (var r in Content.Recipes.Values)
            {
                if (r.Station != station || !s.Knows(r.Id)) continue;
                var rt = UIKit.Img("Recipe", _stationList, UIKit.Round, new Color(1, 1, 1, 0.08f)).rectTransform;
                rt.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -row * 100), new Vector2(580, 92));
                var icon = UIKit.Rect("Icon", rt).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(76, 76));
                UIKit.ItemIcon(icon, r.Output, 72);
                var name = UIKit.Label("Name", rt, Content.NameOf(r.Output), 30, Color.white, TextAnchor.UpperLeft);
                name.rectTransform.Fill(); name.rectTransform.offsetMin = new Vector2(98, 0); name.rectTransform.offsetMax = new Vector2(-170, -8);
                var cost = new System.Text.StringBuilder();
                foreach (var c in r.Inputs)
                {
                    int have = s.Count(c.id);
                    if (cost.Length > 0) cost.Append("   ");
                    cost.Append("<color=" + (have >= c.n ? "#B8F0A0" : "#FF9A80") + ">" + Content.NameOf(c.id) + " " + have + "/" + c.n + "</color>");
                }
                var costs = UIKit.Label("Cost", rt, cost.ToString(), 22, Color.white, TextAnchor.LowerLeft, false);
                costs.supportRichText = true;
                costs.rectTransform.Fill(); costs.rectTransform.offsetMin = new Vector2(98, 10); costs.rectTransform.offsetMax = new Vector2(-170, 0);
                string recipe = r.Id;
                if (job != null && job.recipe == r.Id)
                {
                    float t = 1 - (float)((job.doneAt - s.Now) / r.Seconds);
                    var bar = UIKit.Img("Progress", rt, UIKit.Round, UIKit.Ink);
                    bar.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(150, 30));
                    var f = UIKit.Img("Fill", bar.transform, UIKit.Round, UIKit.Amber);
                    f.rectTransform.anchorMin = Vector2.zero; f.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(t), 1);
                    f.rectTransform.offsetMin = f.rectTransform.offsetMax = Vector2.zero;
                    bar.rectTransform.anchoredPosition = new Vector2(-14, 18);
                    var rush = UIKit.Button("Rush", rt, "RUSH " + Session.RushCraftCost, new Color(0.8f, 0.62f, 0.12f), () => { if (s.RushCraft(station)) Sfx.Play("coin"); }, 22);
                    ((RectTransform)rush.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, -22), new Vector2(150, 36));
                }
                else
                {
                    bool ok = s.CraftBlocked(recipe) == null;
                    var b = UIKit.Button("Make", rt, station == "campfire" ? "COOK" : "CRAFT", ok ? new Color(0.25f, 0.6f, 0.28f) : new Color(0.35f, 0.35f, 0.35f, 0.8f), () =>
                    {
                        if (s.Craft(recipe)) Sfx.Play(station == "campfire" ? "sizzle" : "craft");
                        else Toast(s.CraftBlocked(recipe));
                    }, 30);
                    ((RectTransform)b.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(150, 70));
                }
                row++;
            }
        }
    }
}
