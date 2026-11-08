using System.Collections.Generic;
using System.Linq;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// The inventory screen: clothes, pockets and backpack on the left with USE, SPLIT, SORT and DROP; on the right the
    /// station the castaway stands at (recipes, ingredients, timer, quantity, upgrade, skip), the storage they opened,
    /// or the details of the chosen item. The HUD buttons along the bottom right open it and the other screens.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _inv, _invGrid, _invGear, _invSide, _invButtons;
        Text _invCoins;
        string _station;
        PieceState _storage;            // a box on the grid (null = the sea chest when _station == "chest")
        bool _bagOpen;
        int _sel = -1;
        string _recipe;
        int _qty = 1;
        bool _recipeList;
        RectTransform _weaponSlot;
        string _weaponShown = "-";
        const float Cell = 128;

        bool InvOpen { get { return _bagOpen || _station != null; } }

        /// <summary>
        /// Bottom right: the hand (do the nearest job), the weapon (strike; its ring is the cooldown), target lock and
        /// sneak; a row of small menu buttons along the bottom; AUTO beside the stick. Top right: gift and settings by
        /// the minimap, the map under it.
        /// </summary>
        void BuildButtons(RectTransform safe)
        {
            ActionButton(safe, "hand", new Vector2(-190, 70), 180, () => Player.Instance.Interact(), new Color(0.08f, 0.24f, 0.26f, 0.9f));
            _weaponSlot = ActionButton(safe, null, new Vector2(-120, 290), 160, () => Player.Instance.Attack(), new Color(0.08f, 0.1f, 0.1f, 0.6f));
            _weaponRing = UIKit.Img("Cooldown", _weaponSlot, UIKit.RingSprite, new Color(1, 1, 1, 0.8f));
            _weaponRing.rectTransform.Fill(-4);
            _weaponRing.type = Image.Type.Filled; _weaponRing.fillMethod = Image.FillMethod.Radial360; _weaponRing.fillOrigin = 2;
            _targetBtn = ActionButton(safe, "target", new Vector2(-320, 320), 110, () => { Player.Instance.CycleTarget(); Sfx.Play("click"); }, new Color(0.08f, 0.1f, 0.1f, 0.6f));
            _sneakBtn = ActionButton(safe, "sneak", new Vector2(-30, 80), 110, () => { Player.Instance.Sneaking = !Player.Instance.Sneaking; Sfx.Play("click"); }, new Color(0.08f, 0.1f, 0.1f, 0.6f));
            _autoBtn = ActionButton(safe, "auto", new Vector2(0, 0), 120, () => { Player.Instance.AutoAttack = !Player.Instance.AutoAttack; Toast(Player.Instance.AutoAttack ? "Auto attack on" : "Auto attack off: use the weapon button"); }, new Color(0.08f, 0.1f, 0.1f, 0.6f));
            _autoBtn.anchorMin = _autoBtn.anchorMax = new Vector2(0, 0); _autoBtn.pivot = new Vector2(0, 0); _autoBtn.anchoredPosition = new Vector2(40, 30);
            var al = UIKit.Label("AUTO", _autoBtn, "AUTO", 26, Color.white, TextAnchor.LowerCenter, false); al.rectTransform.Fill(10);

            // Menu row along the bottom.
            float x = -420;
            foreach (var (sym, label, act) in new (string, string, System.Action)[] { ("pack", "BAG", ToggleBag), ("craft", "CRAFT", () => OpenBlueprints()), ("build", "BUILD", () => EnterBuild()), ("path", "PATH", () => OpenPath()) })
            {
                var b = UIKit.Button(label, safe, null, new Color(0, 0, 0, 0), act);
                var rt = (RectTransform)b.transform;
                rt.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(x, 22), new Vector2(96, 96));
                var ic = UIKit.Img("Icon", b.transform, UIKit.Symbol(sym), new Color(0.88f, 0.9f, 0.88f)); ic.rectTransform.Fill(18);
                if (label == "BUILD") _buildButton = rt;
                if (label == "PATH") _pathBadge = Badge(rt);
                x -= 104;
            }

            // Top right: gift, settings and the map.
            var gift = UIKit.Button("Gift", safe, null, new Color(0, 0, 0, 0), OpenOffers);
            ((RectTransform)gift.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-330, -20), new Vector2(80, 80));
            var gi = UIKit.Img("I", gift.transform, UIKit.Symbol("gift"), Color.white); gi.rectTransform.Fill(8);
            var gear = UIKit.Button("Settings", safe, null, new Color(0, 0, 0, 0), () => OpenHelp(2));
            ((RectTransform)gear.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-330, -110), new Vector2(80, 80));
            var ge = UIKit.Img("I", gear.transform, UIKit.Symbol("gear"), Color.white); ge.rectTransform.Fill(8);
            var help = UIKit.Button("Help", safe, "?", new Color(0, 0, 0, 0), () => OpenHelp(), 56);
            ((RectTransform)help.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-330, -200), new Vector2(80, 80));
            RoundButton(safe, "map", "MAP", new Color(0.2f, 0.32f, 0.38f, 0.95f), OpenMap, new Vector2(-40, -400), 110, top: true);
            BuildMinimap(safe);
        }

        Image _weaponRing;
        RectTransform _targetBtn, _sneakBtn, _autoBtn;

        RectTransform ActionButton(RectTransform parent, string sym, Vector2 pos, float size, System.Action act, Color c)
        {
            var b = UIKit.Button("Action", parent, null, c, act);
            b.GetComponent<Image>().sprite = UIKit.Circle; b.GetComponent<Image>().type = Image.Type.Simple;
            var rt = (RectTransform)b.transform;
            rt.Place(new Vector2(1, 0), new Vector2(1, 0), pos, new Vector2(size, size));
            var ring = UIKit.Img("Ring", rt, UIKit.RingSprite, new Color(0.3f, 0.75f, 0.72f, 0.9f)); ring.rectTransform.Fill(2);
            if (sym != null) { var ic = UIKit.Img("Icon", rt, UIKit.Symbol(sym), Color.white); ic.rectTransform.Fill(size * 0.22f); }
            return rt;
        }

        void UpdateActions()
        {
            var p = Player.Instance;
            if (p == null) return;
            _weaponRing.fillAmount = 1 - p.Cooldown01;
            Tint(_sneakBtn, p.Sneaking);
            Tint(_targetBtn, p.Locked != null);
            Tint(_autoBtn, p.AutoAttack);
        }

        static void Tint(RectTransform b, bool on)
        {
            var ring = b.Find("Ring").GetComponent<Image>();
            ring.color = on ? new Color(1f, 0.8f, 0.3f, 1f) : new Color(0.3f, 0.75f, 0.72f, 0.6f);
        }

        RectTransform _buildButton;
        Text _pathBadge;

        RectTransform RoundButton(RectTransform parent, string sym, string label, Color c, System.Action act, Vector2 pos, float size, bool top = false)
        {
            var b = UIKit.Button(label, parent, null, c, act);
            b.GetComponent<Image>().sprite = UIKit.Circle; b.GetComponent<Image>().type = Image.Type.Simple;
            var rt = (RectTransform)b.transform;
            rt.Place(new Vector2(1, top ? 1 : 0), new Vector2(1, top ? 1 : 0), pos, new Vector2(size, size));
            var ic = UIKit.Img("Icon", b.transform, UIKit.Symbol(sym), UIKit.Sand);
            ic.rectTransform.Fill(size * 0.24f);
            var l = UIKit.Outlined(UIKit.Label("Label", b.transform, label, 24, Color.white, TextAnchor.LowerCenter), 1.5f);
            l.rectTransform.Fill(); l.rectTransform.offsetMin = new Vector2(-20, -28); l.rectTransform.offsetMax = new Vector2(20, 0);
            return rt;
        }

        static Text Badge(RectTransform on)
        {
            var b = UIKit.Img("Badge", on, UIKit.Circle, UIKit.Red);
            b.rectTransform.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-10, -10), new Vector2(40, 40));
            var t = UIKit.Label("N", b.transform, "", 24, Color.white, TextAnchor.MiddleCenter);
            t.rectTransform.Fill();
            return t;
        }

        void BuildPanels(RectTransform safe)
        {
            _inv = UIKit.Img("Inventory", _root, null, new Color(0.2f, 0.22f, 0.2f, 1f)).rectTransform;
            _inv.Fill();
            _inv.GetComponent<Image>().raycastTarget = true;
            var head = UIKit.Img("Pack", _inv, UIKit.Symbol("pack"), new Color(0.85f, 0.85f, 0.8f));
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(230, -38), new Vector2(62, 62));
            var title = UIKit.Label("Title", _inv, "INVENTORY", 48, new Color(0.9f, 0.9f, 0.85f), TextAnchor.MiddleLeft, false);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(305, -38), new Vector2(400, 62));
            var coinBox = UIKit.Img("Coins", _inv, null, new Color(0, 0, 0, 0.25f)).rectTransform;
            coinBox.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(720, -40), new Vector2(300, 60));
            UIKit.ItemIcon(UIKit.Rect("Coin", coinBox).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(64, 64)), "doubloon", 64);
            _invCoins = UIKit.Label("N", coinBox, "0", 36, Color.white, TextAnchor.MiddleLeft, false);
            _invCoins.rectTransform.Fill(); _invCoins.rectTransform.offsetMin = new Vector2(80, 0);
            var plus = UIKit.Button("Plus", coinBox, "+", new Color(0, 0, 0, 0), OpenShop, 56);
            ((RectTransform)plus.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(64, 60));
            plus.GetComponentInChildren<Text>().color = new Color(0.3f, 0.8f, 0.75f);
            _invGear = UIKit.Rect("Gear", _inv).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -120), new Vector2(130, 4 * Cell));
            _invGrid = UIKit.Rect("Grid", _inv).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(260, -120), new Vector2(5 * Cell, 820));
            _invButtons = UIKit.Rect("Buttons", _inv).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(230, 30), new Vector2(820, 90));
            string[] labels = { "USE", "SPLIT", "SORT", "DROP" };
            System.Action[] acts = { UseSelected, () => { if (_sel >= 0 && G.Session.Split(_sel)) Sfx.Play("click"); }, () => { G.Session.Sort(); _sel = -1; Sfx.Play("click"); }, DropSelected };
            for (int i = 0; i < 4; i++)
            {
                var b = UIKit.Button(labels[i], _invButtons, labels[i], i == 2 ? new Color(0.15f, 0.45f, 0.45f) : new Color(0.33f, 0.35f, 0.33f), acts[i], 38);
                ((RectTransform)b.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(i * 205, 0), new Vector2(190, 84));
            }
            _invSide = UIKit.Img("Side", _inv, null, new Color(0.14f, 0.16f, 0.15f, 1)).rectTransform;
            _invSide.anchorMin = new Vector2(0, 0); _invSide.anchorMax = new Vector2(1, 1);
            _invSide.offsetMin = new Vector2(1080, 130); _invSide.offsetMax = new Vector2(-240, -110);
            var x = UIKit.Button("Close", _inv, "X", new Color(0, 0, 0, 0), CloseAll, 80);
            ((RectTransform)x.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -20), new Vector2(120, 120));
            var cl = UIKit.Label("CLOSE", _inv, "CLOSE", 40, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, false);
            cl.rectTransform.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-130, -230), new Vector2(200, 50));
            cl.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            _inv.gameObject.SetActive(false);
        }

        void ToggleBag() { _bagOpen = !_bagOpen; _sel = -1; _dirty = true; Sfx.Play("click"); }

        public void OpenStation(string id)
        {
            if (Equals(_dismissed, id)) return;
            if (id == "radio") { if (!_radioOpen && _station != "radio") { _station = "radio"; OpenRadio(); } return; }
            if (_station == id && _storage == null) return;
            _station = id;
            _storage = null;
            _recipeList = false;
            _recipe = Content.Recipes.Values.FirstOrDefault(r => r.Station == id && G.Session.Knows(r.Id))?.Id;
            _qty = 1;
            _dirty = true;
        }

        public void CloseStation(string id)
        {
            if (Equals(_dismissed, id)) _dismissed = null;
            if (_station != id) return;
            _station = null;
            _dirty = true;
        }

        public void OpenStorage(PieceState box)
        {
            if (_dismissed == box) return;
            if (_storage == box) return;
            _storage = box;
            _station = "storage";
            _dirty = true;
        }

        string _lootKey;

        /// <summary>A sailor's bag: its contents in the side panel, with TAKE ALL.</summary>
        public void OpenLoot(string key)
        {
            if (Equals(_dismissed, key)) return;
            if (_lootKey == key) return;
            _lootKey = key;
            _storage = null;
            _station = "loot";
            Sfx.Play("gather");
            _dirty = true;
        }

        public void CloseLoot(string key) { if (Equals(_dismissed, key)) _dismissed = null; if (_lootKey == key) { _lootKey = null; _station = null; _dirty = true; } }

        void LootPanel()
        {
            var s = G.Session;
            var bag = s.Loot(_lootKey, "lootbag");
            if (bag == null) { _lootKey = null; _station = null; _dirty = true; return; }
            var t = UIKit.Label("Title", _invSide, "SAILOR'S BAG", 42, UIKit.Sand, TextAnchor.UpperLeft);
            t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(30, 0); t.rectTransform.offsetMax = new Vector2(0, -20);
            var grid = UIKit.Rect("Grid", _invSide).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(4 * 140, 300));
            for (int i = 0; i < bag.Count; i++)
            {
                int idx = i;
                string key = _lootKey;
                SlotCell(grid, bag[i], new Vector2((i % 4) * 140, -(i / 4) * 140), 132, false, false, () => { if (s.TakeLoot(key, "lootbag", idx) > 0) Sfx.Play("gather"); });
            }
            string k2 = _lootKey;
            var all = UIKit.Button("All", _invSide, "TAKE ALL", new Color(0.8f, 0.62f, 0.12f), () =>
            {
                if (s.TakeLoot(k2, "lootbag", -1) > 0) Sfx.Play("gather");
                if (!s.Standing(k2, "lootbag")) { _lootKey = null; _station = null; }
                _dirty = true;
            }, 38);
            ((RectTransform)all.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(380, 84));
        }

        public void CloseStorage(PieceState box) { if (_dismissed == box) _dismissed = null; if (_storage == box) { _storage = null; _station = null; _dirty = true; } }

        /// <summary>What the castaway closed while still standing at it: it doesn't pop open again until they walk away.</summary>
        object _dismissed;

        void CloseAll()
        {
            _dismissed = (object)_storage ?? (object)_lootKey ?? _station;
            _station = null; _storage = null; _lootKey = null; _bagOpen = false; _sel = -1; _dirty = true;
        }

        List<Slot> OpenStorageSlots { get { return _storage != null ? _storage.slots : _station == "chest" ? G.Session.State.chest : null; } }

        void UseSelected()
        {
            var s = G.Session;
            if (_sel < 0 || s.State.backpack[_sel].Empty) { Toast("Choose an item first"); return; }
            var d = Content.Item(s.State.backpack[_sel].id);
            if (d != null && d.Gear != GearSlot.None) { if (s.PutOn(_sel)) Sfx.Play("click"); }
            else if (s.Use(_sel)) Sfx.Play(d != null && (d.Kind == ItemKind.Food || d.Kind == ItemKind.Drink) ? "eat" : "click");
            if (s.State.backpack[_sel].Empty) _sel = -1;
        }

        void DropSelected()
        {
            var s = G.Session;
            if (_sel < 0 || s.State.backpack[_sel].Empty) { Toast("Choose an item first"); return; }
            int slot = _sel;
            var sl = s.State.backpack[slot];
            Confirm("Throw away?", sl.n + " " + Content.NameOf(sl.id) + " will be gone for good.", "THROW AWAY", () => { s.Discard(slot); _sel = -1; });
        }

        void RefreshPanels()
        {
            var s = G.Session;
            bool open = InvOpen && _station != "radio";
            _inv.gameObject.SetActive(open);
            if (open)
            {
                _invCoins.text = s.State.coins.ToString();
                InvGrid();
                InvGear();
                Clear(_invSide);
                if (_station == "loot") LootPanel();
                else if (_station == "storage" || _station == "chest") StoragePanel();
                else if (_station != null) StationPanel(_station);
                else ItemPanel();
            }
            // The held weapon.
            var w = s.WeaponId;
            if (w != _weaponShown)
            {
                _weaponShown = w;
                var holder = _weaponSlot.Find("WIcon") as RectTransform;
                if (holder == null) { holder = UIKit.Rect("WIcon", _weaponSlot); holder.Fill(); }
                for (int i = holder.childCount - 1; i >= 0; i--) Destroy(holder.GetChild(i).gameObject);
                if (w != null) UIKit.ItemIcon(holder, w, 120);
                else { var f = UIKit.Label("Fists", holder, "FISTS", 28, UIKit.Sand, TextAnchor.MiddleCenter); f.rectTransform.Fill(); }
            }
            _buildButton.gameObject.SetActive(s.AtHome);
            int claim = s.Claimable;
            _pathBadge.transform.parent.gameObject.SetActive(claim > 0);
            _pathBadge.text = claim > 9 ? "9+" : claim.ToString();
        }

        // ------------------------------------------------------------------ the left: clothes, pockets, backpack

        void InvGrid()
        {
            var s = G.Session;
            Clear(_invGrid);
            var store = OpenStorageSlots;
            float y = 0;
            SideLabel(_invGrid, "POCKETS", 0, Cell);
            for (int i = 0; i < s.State.backpack.Count; i++)
            {
                if (i == Content.PocketSlots)
                {
                    y = Cell + 30;
                    var line = UIKit.Img("Line", _invGrid, null, new Color(1, 1, 1, 0.15f));
                    line.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -Cell - 14), new Vector2(5 * Cell, 3));
                    int rows = (s.State.backpack.Count - Content.PocketSlots + 4) / 5;
                    SideLabel(_invGrid, "BACKPACK", y, rows * Cell);
                }
                int k = i < Content.PocketSlots ? i : i - Content.PocketSlots;
                int idx = i;
                SlotCell(_invGrid, s.State.backpack[i], new Vector2((k % 5) * Cell, -y - (k / 5) * Cell), Cell - 6, i == _sel, i == s.State.equipped, () =>
                {
                    if (store != null) { if (s.StoreSlotTo(store, idx) > 0) Sfx.Play("deliver"); return; }
                    if (_sel == idx) UseSelected(); else { _sel = idx; _dirty = true; }
                });
            }
        }

        static void SideLabel(RectTransform parent, string text, float y, float h)
        {
            var t = UIKit.Label("Side", parent, text, 26, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, false);
            t.rectTransform.Place(new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(-30, -y - h / 2), new Vector2(h, 40));
            t.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
        }

        RectTransform SlotCell(RectTransform parent, Slot sl, Vector2 at, float size, bool selected, bool held, System.Action tap)
        {
            var b = UIKit.Button("Slot", parent, null, selected ? new Color(0.15f, 0.5f, 0.5f) : held ? new Color(0.3f, 0.45f, 0.25f) : new Color(0.12f, 0.13f, 0.12f), tap);
            var rt = (RectTransform)b.transform;
            rt.Place(new Vector2(0, 1), new Vector2(0, 1), at, new Vector2(size, size));
            b.GetComponent<Image>().sprite = null;
            if (sl == null || sl.Empty) return rt;
            UIKit.ItemIcon(b.transform, sl.id, size * 0.78f);
            var d = Content.Item(sl.id);
            if (sl.n > 1)
            {
                var n = UIKit.Outlined(UIKit.Label("Count", b.transform, sl.n.ToString(), Mathf.RoundToInt(size * 0.24f), Color.white, TextAnchor.LowerRight), 1.5f);
                n.rectTransform.Fill(6);
            }
            if (d != null && d.Durability > 0)
            {
                float left = 1 - sl.wear / (float)d.Durability;
                var bar = UIKit.Img("Wear", b.transform, null, left > 0.3f ? new Color(0.2f, 0.7f, 0.7f) : UIKit.Red);
                bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(0, left);
                bar.rectTransform.offsetMin = new Vector2(2, 2); bar.rectTransform.offsetMax = new Vector2(8, -2);
            }
            return rt;
        }

        void InvGear()
        {
            var s = G.Session;
            Clear(_invGear);
            string[] names = { "HEAD", "BODY", "LEGS", "FEET" };
            for (int g = 0; g < 4; g++)
            {
                var slot = (GearSlot)(g + 1);
                var sl = s.GearIn(slot);
                var rt = SlotCell(_invGear, sl, new Vector2(0, -g * Cell), Cell - 6, false, false, () => { if (s.TakeOff(slot)) Sfx.Play("click"); });
                if (sl.Empty)
                {
                    var t = UIKit.Label("Name", rt, names[g], 22, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleCenter, false);
                    t.rectTransform.Fill();
                }
            }
            var armor = UIKit.Label("Armor", _invGear, "ARMOUR " + s.Armor, 24, UIKit.Sand, TextAnchor.UpperCenter, false);
            armor.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(-20, -4 * Cell - 10), new Vector2(160, 40));
        }

        // ------------------------------------------------------------------ the right: item, storage, station

        void ItemPanel()
        {
            var s = G.Session;
            if (_sel < 0 || s.State.backpack[_sel].Empty)
            {
                var hint = UIKit.Label("Hint", _invSide, "Tap an item to see it. Tap it again (or USE) to eat, drink, hold or wear it.\n\nPockets are kept when you pass out; the rest of the backpack is left where you fell.", 30, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter, false);
                hint.rectTransform.Fill(40);
                return;
            }
            var sl = s.State.backpack[_sel];
            var d = Content.Item(sl.id);
            var icon = UIKit.Rect("Icon", _invSide).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(220, 220));
            UIKit.ItemIcon(icon, sl.id, 220);
            var name = UIKit.Label("Name", _invSide, d != null ? d.Name.ToUpper() : sl.id, 46, Color.white, TextAnchor.UpperCenter);
            name.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -262), new Vector2(560, 60));
            var lines = new List<string>();
            if (d != null)
            {
                if (d.Damage > 0) lines.Add("Damage " + d.Damage);
                if (d.Armor > 0) lines.Add("Armour " + d.Armor + " (" + d.Gear.ToString().ToLower() + ")");
                if (d.Durability > 0) lines.Add("Condition " + (d.Durability - sl.wear) + "/" + d.Durability);
                if (d.Food > 0) lines.Add("Food +" + d.Food);
                if (d.Drink > 0) lines.Add("Water +" + d.Drink);
                if (d.Heal != 0) lines.Add("Health " + (d.Heal > 0 ? "+" : "") + d.Heal);
            }
            var stats = UIKit.Label("Stats", _invSide, string.Join("    ", lines), 30, UIKit.Amber, TextAnchor.UpperCenter, false);
            stats.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(560, 50));
            var blurb = UIKit.Label("Blurb", _invSide, d?.Blurb ?? "", 28, new Color(1, 1, 1, 0.75f), TextAnchor.UpperCenter, false);
            blurb.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -390), new Vector2(560, 160));
            string act = d == null ? null : d.Gear != GearSlot.None ? "WEAR" : d.Kind == ItemKind.Weapon || d.Kind == ItemKind.Tool ? (s.State.equipped == _sel ? "PUT AWAY" : "HOLD")
                : d.Kind == ItemKind.Food || d.Kind == ItemKind.Drink ? "EAT" : d.Kind == ItemKind.Medical ? "USE" : null;
            if (act != null)
            {
                var b = UIKit.Button("Act", _invSide, act, new Color(0.8f, 0.62f, 0.12f), UseSelected, 38);
                ((RectTransform)b.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(320, 84));
            }
        }

        void StoragePanel()
        {
            var s = G.Session;
            var store = OpenStorageSlots;
            if (store == null) return;
            string name = _storage != null ? Content.Pieces[_storage.id].Name : Content.Builds["chest"].Name;
            var t = UIKit.Label("Title", _invSide, name.ToUpper(), 42, UIKit.Sand, TextAnchor.UpperLeft);
            t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(30, 0); t.rectTransform.offsetMax = new Vector2(0, -20);
            var grid = UIKit.Rect("Grid", _invSide).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(6 * 100, 400));
            for (int i = 0; i < store.Count; i++)
            {
                int idx = i;
                SlotCell(grid, store[i], new Vector2((i % 6) * 100, -(i / 6) * 100), 94, false, false, () => { if (s.TakeFrom(store, idx) > 0) Sfx.Play("gather"); });
            }
            var hint = UIKit.Label("Hint", _invSide, "Tap your items to store them, stored items to take them.", 24, new Color(1, 1, 1, 0.55f), TextAnchor.LowerCenter, false);
            hint.rectTransform.Fill(); hint.rectTransform.offsetMin = new Vector2(20, 120);
            var all = UIKit.Button("Store", _invSide, "STORE RESOURCES", new Color(0.15f, 0.45f, 0.45f), () => { if (s.StoreResourcesTo(store) > 0) Sfx.Play("deliver"); }, 32);
            ((RectTransform)all.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(400, 80));
        }

        void StationPanel(string station)
        {
            var s = G.Session;
            // Header: name, level, upgrade.
            var gear = UIKit.Img("Gear", _invSide, UIKit.Symbol("craft"), new Color(0.85f, 0.85f, 0.8f));
            gear.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, 20), new Vector2(64, 64));
            var hdr = UIKit.Img("Header", _invSide, null, new Color(0.15f, 0.42f, 0.42f)).rectTransform;
            hdr.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(100, 16), new Vector2(380, 60));
            var name = UIKit.Label("Name", hdr, Session.StationName(station).ToUpper() + " LVL " + Mathf.Max(1, s.Level(station)), 36, Color.white, TextAnchor.MiddleLeft, false);
            name.rectTransform.Fill(); name.rectTransform.offsetMin = new Vector2(16, 0);
            if (Content.Builds.ContainsKey(station) && s.UpgradeCost(station) != null)
            {
                var up = UIKit.Button("Upgrade", _invSide, null, new Color(0.15f, 0.55f, 0.5f), () => AskUpgrade(station));
                ((RectTransform)up.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, 16), new Vector2(64, 64));
                var ic = UIKit.Img("Up", up.transform, UIKit.Symbol("upgrade"), Color.white); ic.rectTransform.Fill(10);
            }
            // Recipe chooser.
            var choose = UIKit.Button("All", _invSide, null, new Color(0.1f, 0.11f, 0.1f), () => { _recipeList = !_recipeList; _dirty = true; });
            ((RectTransform)choose.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -64), new Vector2(380, 84));
            var ct = UIKit.Label("T", choose.transform, "ALL RECIPES", 34, UIKit.Amber, TextAnchor.MiddleLeft, false);
            ct.rectTransform.Fill(); ct.rectTransform.offsetMin = new Vector2(24, 0);
            var ar = UIKit.Label("A", choose.transform, _recipeList ? "v" : ">", 36, UIKit.Amber, TextAnchor.MiddleRight);
            ar.rectTransform.Fill(); ar.rectTransform.offsetMax = new Vector2(-20, 0);
            var area = UIKit.Rect("Area", _invSide);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = new Vector2(20, 120); area.offsetMax = new Vector2(-20, -160);
            var recipes = Content.Recipes.Values.Where(r => r.Station == station && s.Knows(r.Id)).ToList();
            if (_recipeList || _recipe == null)
            {
                for (int i = 0; i < recipes.Count; i++)
                {
                    var r = recipes[i];
                    string id = r.Id;
                    bool ok = s.Level(station) >= r.MinLevel;
                    var cell = UIKit.Button("Recipe", area, null, id == _recipe ? new Color(0.15f, 0.5f, 0.5f) : new Color(0.12f, 0.13f, 0.12f), () => { _recipe = id; _qty = 1; _recipeList = false; _dirty = true; });
                    ((RectTransform)cell.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2((i % 5) * 116, -(i / 5) * 116), new Vector2(110, 110));
                    UIKit.ItemIcon(cell.transform, r.Output, 86);
                    if (!ok) { var lk = UIKit.Label("Lv", cell.transform, "LV" + r.MinLevel, 22, UIKit.Red, TextAnchor.UpperRight); lk.rectTransform.Fill(4); }
                }
                return;
            }
            var rec = Content.Recipes[_recipe];
            var job = s.Job(station);
            // Ingredients down the left, an arrow, the result with its timer.
            for (int i = 0; i < rec.Inputs.Length; i++)
            {
                var c = rec.Inputs[i];
                int have = s.Count(c.id), need = c.n * _qty;
                var slot = UIKit.Img("In", area, null, new Color(0.12f, 0.13f, 0.12f)).rectTransform;
                slot.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -i * 150), new Vector2(140, 140));
                UIKit.ItemIcon(slot, c.id, 110);
                var n = UIKit.Outlined(UIKit.Label("N", slot, have + "/" + need, 26, have >= need ? Color.white : new Color(1f, 0.5f, 0.45f), TextAnchor.LowerRight), 1.5f);
                n.rectTransform.Fill(6);
            }
            var arrow = UIKit.Label("Arrow", area, ">", 80, new Color(1, 1, 1, 0.3f), TextAnchor.MiddleCenter);
            arrow.rectTransform.Place(new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(250, -140), new Vector2(80, 100));
            var outCell = UIKit.Img("Out", area, null, new Color(0.12f, 0.13f, 0.12f)).rectTransform;
            outCell.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(320, -60), new Vector2(180, 180));
            UIKit.ItemIcon(outCell, rec.Output, 150);
            var cnt = UIKit.Outlined(UIKit.Label("N", outCell, "x" + rec.Count * _qty, 30, Color.white, TextAnchor.LowerRight), 1.5f);
            cnt.rectTransform.Fill(8);
            var oname = UIKit.Label("Name", area, Content.NameOf(rec.Output), 30, Color.white, TextAnchor.UpperCenter, false);
            oname.rectTransform.Place(new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(410, -250), new Vector2(300, 40));
            if (job != null)
            {
                var jr = Content.Recipes[job.recipe];
                float total = jr.Seconds * Mathf.Max(1, job.count / jr.Count);
                float t = 1 - (float)((job.doneAt - s.Now) / total);
                var bar = UIKit.Img("Bar", area, null, new Color(0, 0, 0, 0.5f));
                bar.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(320, -296), new Vector2(180, 16));
                var f = UIKit.Img("Fill", bar.transform, null, new Color(0.2f, 0.65f, 0.65f));
                f.rectTransform.anchorMin = Vector2.zero; f.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(t), 1);
                f.rectTransform.offsetMin = f.rectTransform.offsetMax = Vector2.zero;
                var tl = UIKit.Label("Time", area, Clock(job.doneAt - s.Now) + "  " + Content.NameOf(jr.Output), 26, new Color(1, 1, 1, 0.7f), TextAnchor.UpperCenter, false);
                tl.rectTransform.Place(new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(410, -318), new Vector2(360, 40));
            }
            // Quantity, make, skip.
            if (job == null)
            {
                int max = s.MaxCraft(_recipe);
                var minus = UIKit.Button("Minus", area, "-", new Color(0.33f, 0.35f, 0.33f), () => { _qty = Mathf.Max(1, _qty - 1); _dirty = true; }, 50);
                ((RectTransform)minus.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 10), new Vector2(84, 74));
                var q = UIKit.Label("Qty", area, "Quantity: " + _qty + "/" + max, 30, new Color(0.4f, 0.9f, 0.85f), TextAnchor.MiddleCenter, false);
                q.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(130, 10), new Vector2(240, 74));
                var plus = UIKit.Button("Plus", area, "+", new Color(0.15f, 0.55f, 0.5f), () => { _qty = Mathf.Min(max, _qty + 1); _dirty = true; }, 50);
                ((RectTransform)plus.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(380, 10), new Vector2(84, 74));
            }
            string why = s.CraftBlocked(_recipe, _qty);
            if (job != null)
            {
                int cost = s.RushCost(station);
                var skip = UIKit.Button("Skip", _invSide, "SKIP  " + cost, new Color(0.12f, 0.42f, 0.42f), () => { if (s.RushCraft(station)) Sfx.Play("coin"); }, 38);
                ((RectTransform)skip.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(380, 84));
                UIKit.ItemIcon(UIKit.Rect("Coin", skip.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(56, 56)), "doubloon", 56);
            }
            else
            {
                string verb = station == "campfire" ? "COOK" : station == "furnace" ? "SMELT" : "CRAFT";
                var make = UIKit.Button("Make", _invSide, verb, why == null ? new Color(0.8f, 0.62f, 0.12f) : new Color(0.4f, 0.38f, 0.3f), () =>
                {
                    if (s.Craft(_recipe, _qty)) { Sfx.Play(station == "campfire" ? "sizzle" : "craft"); _qty = 1; }
                    else Toast(s.CraftBlocked(_recipe, _qty));
                }, 40);
                ((RectTransform)make.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(380, 84));
            }
        }

        void AskUpgrade(string station)
        {
            var s = G.Session;
            var cost = s.UpgradeCost(station);
            string need = string.Join(", ", cost.Select(c => c.n + " " + Content.NameOf(c.id) + " (" + s.CountAll(c.id) + ")"));
            Confirm("Upgrade " + Content.Builds[station].Name + "?", Content.Builds[station].UpgradeBlurb + "\nNeeds: " + need, "UPGRADE", () => { if (s.Upgrade(station)) Sfx.Play("built"); });
        }
    }
}
