using System.Linq;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// Blueprints: everything the castaway can make, by category, with a blueprint card for the chosen one (stats,
    /// description, where it's made, the cost with what you have) and a quantity slider. Crafting queues the job at
    /// its station (or by hand) wherever you are.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _bp, _bpGrid, _bpCard, _bpTabs;
        bool _bpOpen;
        int _bpTab;
        string _bpSel;
        int _bpQty = 1;
        static readonly string[] BpTabs = { "All", "Tools", "Weapons", "Clothes", "Food", "Materials", "Camp" };
        static readonly string[] BpIcons = { "craft", "pickaxe", "spear", "hide_vest", "meat", "rope", "perk_larder" };

        void BuildBlueprints()
        {
            _bp = UIKit.Img("Blueprints", _root, null, new Color(0.2f, 0.22f, 0.2f, 1f)).rectTransform;
            _bp.Fill();
            _bp.GetComponent<Image>().raycastTarget = true;
            var ic = UIKit.Img("Icon", _bp, UIKit.Symbol("craft"), new Color(0.9f, 0.9f, 0.85f));
            ic.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -30), new Vector2(60, 60));
            var t = UIKit.Label("Title", _bp, "BLUEPRINTS", 52, new Color(0.9f, 0.9f, 0.85f), TextAnchor.MiddleLeft, false);
            t.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(136, -30), new Vector2(500, 60));
            _bpGrid = UIKit.Rect("Grid", _bp).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(220, -120), new Vector2(5 * 168, 800));
            _bpTabs = UIKit.Rect("Tabs", _bp).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(220, 40), new Vector2(7 * 140, 100));
            _bpCard = UIKit.Rect("Card", _bp).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-250, 0), new Vector2(720, 1000));
            var x = UIKit.Button("Close", _bp, "X", new Color(0, 0, 0, 0), () => { _bpOpen = false; _dirty = true; }, 80);
            ((RectTransform)x.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -20), new Vector2(120, 120));
            var cl = UIKit.Label("CLOSE", _bp, "CLOSE", 40, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, false);
            cl.rectTransform.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-130, -230), new Vector2(200, 50));
            cl.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            _bp.gameObject.SetActive(false);
        }

        public void OpenBlueprints()
        {
            _bpOpen = true;
            if (_bpSel == null) _bpSel = "spear";
            _bpQty = 1;
            Sfx.Play("click");
            RefreshBlueprints();
        }

        void RefreshBlueprints()
        {
            _bp.gameObject.SetActive(_bpOpen);
            if (!_bpOpen) return;
            var s = G.Session;
            // The grid ends 1060 from the left and the card takes 970 from the right: on a canvas narrower than that
            // (16:9 phones, iPads) the grid moves left so the card doesn't cover its last column.
            float gx = _root.rect.width >= 2050 ? 220 : 60;
            _bpGrid.anchoredPosition = new Vector2(gx, _bpGrid.anchoredPosition.y);
            _bpTabs.anchoredPosition = new Vector2(gx, _bpTabs.anchoredPosition.y);
            Clear(_bpGrid); Clear(_bpTabs); Clear(_bpCard);
            for (int i = 0; i < BpTabs.Length; i++)
            {
                int k = i;
                var b = UIKit.Button("Tab", _bpTabs, null, i == _bpTab ? new Color(0.15f, 0.5f, 0.5f) : new Color(0.12f, 0.13f, 0.12f), () => { _bpTab = k; RefreshBlueprints(); });
                ((RectTransform)b.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(i * 140, 0), new Vector2(132, 96));
                b.GetComponent<Image>().sprite = null;
                if (i == 0) { var im = UIKit.Img("I", b.transform, UIKit.Symbol("craft"), Color.white); im.rectTransform.Fill(20); }
                else UIKit.ItemIcon(b.transform, BpIcons[i], 76);
            }
            var list = Content.Recipes.Values.Where(r => s.Knows(r.Id) && (_bpTab == 0 || Content.CategoryOf(r) == BpTabs[_bpTab]) && r.Id != "nails_iron")
                .OrderBy(r => s.Level(r.Station) >= r.MinLevel ? 0 : 1).ThenBy(r => r.Station == "hand" ? 0 : 1).ToList();
            for (int i = 0; i < list.Count && i < 20; i++)
            {
                var r = list[i];
                string id = r.Id;
                bool can = s.Level(r.Station) >= r.MinLevel;
                var cell = UIKit.Button("Bp", _bpGrid, null, id == _bpSel ? new Color(0.15f, 0.5f, 0.5f) : new Color(0.13f, 0.14f, 0.13f), () => { _bpSel = id; _bpQty = 1; RefreshBlueprints(); });
                ((RectTransform)cell.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2((i % 5) * 168, -(i / 5) * 190), new Vector2(160, 182));
                cell.GetComponent<Image>().sprite = null;
                var icon = UIKit.Rect("I", cell.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(120, 120));
                UIKit.ItemIcon(icon, r.Output, 120);
                var nm = UIKit.Label("N", cell.transform, Content.NameOf(r.Output), 22, can ? Color.white : new Color(1, 1, 1, 0.45f), TextAnchor.LowerLeft, false);
                nm.rectTransform.Fill(8);
                if (!can) { foreach (var g in cell.GetComponentsInChildren<Image>()) if (g.name == "Icon") g.color = new Color(1, 1, 1, 0.35f); }
            }
            if (_bpSel == null || !Content.Recipes.ContainsKey(_bpSel)) return;
            BlueprintCard(Content.Recipes[_bpSel]);
        }

        static Sprite _bpPaper;
        static Sprite BlueprintPaper()
        {
            if (_bpPaper != null) return _bpPaper;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool line = x % 16 == 0 || y % 16 == 0, major = x % 64 == 0 || y % 64 == 0;
                    var c = new Color(0.17f, 0.3f, 0.5f);
                    if (line) c = Color.Lerp(c, Color.white, major ? 0.25f : 0.1f);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return _bpPaper = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        void BlueprintCard(RecipeDef r)
        {
            var s = G.Session;
            var d = Content.Item(r.Output);
            var paper = UIKit.Img("Paper", _bpCard, BlueprintPaper(), Color.white).rectTransform;
            paper.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 300));
            paper.localRotation = Quaternion.Euler(0, 0, 1.5f);
            var nm = UIKit.Label("Name", paper, (d?.Name ?? r.Output).ToUpper(), 38, Color.white, TextAnchor.UpperLeft);
            nm.rectTransform.Fill(); nm.rectTransform.offsetMin = new Vector2(24, 0); nm.rectTransform.offsetMax = new Vector2(0, -14);
            var icon = UIKit.Rect("I", paper).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(210, 210));
            UIKit.ItemIcon(icon, r.Output, 210);
            var stats = new System.Collections.Generic.List<string>();
            if (d != null)
            {
                if (d.Damage > 0) stats.Add("Damage " + d.Damage);
                if (d.Armor > 0) stats.Add("Armor " + d.Armor);
                if (d.Durability > 0) stats.Add("Lasts " + d.Durability + " hits");
                if (d.Food > 0) stats.Add("Food +" + d.Food);
                if (d.Drink > 0) stats.Add("Water +" + d.Drink);
                if (d.Heal > 0) stats.Add("Health +" + d.Heal);
            }
            var st = UIKit.Label("Stats", _bpCard, string.Join("    ", stats), 30, UIKit.Amber, TextAnchor.UpperCenter, false);
            st.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -320), new Vector2(680, 44));
            var bl = UIKit.Label("Blurb", _bpCard, d?.Blurb ?? "", 28, new Color(1, 1, 1, 0.85f), TextAnchor.UpperCenter, false);
            bl.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -366), new Vector2(660, 90));
            string where = r.Station == "hand" ? "Made by hand" : "Made at the " + Session.StationName(r.Station).ToLower() + (r.MinLevel > 1 ? " (level " + r.MinLevel + ")" : "");
            var wh = UIKit.Label("Where", _bpCard, where, 26, new Color(0.45f, 0.9f, 0.85f), TextAnchor.UpperCenter, false);
            wh.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -452), new Vector2(660, 40));
            for (int i = 0; i < r.Inputs.Length; i++)
            {
                var c = r.Inputs[i];
                int have = s.Count(c.id), need = c.n * _bpQty;
                var cell = UIKit.Img("Cost", _bpCard, null, new Color(0.13f, 0.14f, 0.13f)).rectTransform;
                cell.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - (r.Inputs.Length - 1) / 2f) * 140, -580), new Vector2(128, 128));
                UIKit.ItemIcon(cell, c.id, 100);
                var n = UIKit.Outlined(UIKit.Label("N", cell, have + "/" + need, 28, have >= need ? Color.white : new Color(1f, 0.5f, 0.45f), TextAnchor.LowerRight), 1.5f);
                n.rectTransform.Fill(6);
            }
            int max = s.MaxCraft(r.Id);
            _bpQty = Mathf.Clamp(_bpQty, 1, max);
            bool afford = s.Short(r.Inputs) == null;   // "1/1" when not even one can be paid for reads as "ready"
            if (Content.IsPerk(r.Id))
            {
                // A camp improvement is built once: no quantity to choose.
                var once = UIKit.Label("Once", _bpCard, s.HasPerk(r.Id) ? "Built at camp" : "Built once, for good", 30, new Color(0.45f, 0.9f, 0.85f), TextAnchor.MiddleCenter, false);
                once.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -750), new Vector2(560, 60));
            }
            else
            {
                var q = UIKit.Label("Q", _bpCard, afford ? "Quantity: " + _bpQty + "/" + max : "Quantity: 1", 30, new Color(0.45f, 0.9f, 0.85f), TextAnchor.MiddleCenter, false);
                q.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -726), new Vector2(400, 44));
                var minus = UIKit.Button("Minus", _bpCard, "-", new Color(0.33f, 0.35f, 0.33f), () => { _bpQty = Mathf.Max(1, _bpQty - 1); RefreshBlueprints(); }, 54);
                ((RectTransform)minus.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-280, -770), new Vector2(90, 80));
                var plus = UIKit.Button("Plus", _bpCard, "+", new Color(0.15f, 0.55f, 0.5f), () => { _bpQty = Mathf.Min(max, _bpQty + 1); RefreshBlueprints(); }, 54);
                ((RectTransform)plus.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(280, -770), new Vector2(90, 80));
                var track = UIKit.Img("Track", _bpCard, null, new Color(0, 0, 0, 0.5f)).rectTransform;
                track.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -800), new Vector2(420, 12));
                var knob = UIKit.Img("Knob", track, null, Color.white).rectTransform;
                knob.Place(new Vector2(max <= 1 ? 0 : (_bpQty - 1) / (float)(max - 1), 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 44));
            }
            string why = s.CraftBlocked(r.Id, _bpQty);
            var craft = UIKit.Button("Craft", _bpCard, "CRAFT", why == null ? new Color(0.8f, 0.62f, 0.12f) : new Color(0.4f, 0.38f, 0.3f), () =>
            {
                if (s.Craft(r.Id, _bpQty)) { Sfx.Play("craft"); Toast(r.Station == "hand" ? "Crafting..." : "Crafting at the " + Session.StationName(r.Station).ToLower()); _bpQty = 1; }
                else Toast(s.CraftBlocked(r.Id, _bpQty));
                RefreshBlueprints();
            }, 44);
            ((RectTransform)craft.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -864), new Vector2(560, 96));
            if (why != null)
            {
                var w = UIKit.Label("Why", _bpCard, why, 26, UIKit.Red, TextAnchor.UpperCenter, false);
                w.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -966), new Vector2(660, 40));
            }
        }
    }
}
