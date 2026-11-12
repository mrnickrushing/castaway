using System.Collections.Generic;
using System.Linq;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// Home again: a card with what the trip brought (finds, defeated enemies, XP and doubloons, rescues and
    /// recipes), what the castaway can now build, upgrade or craft at camp, and the guide's next step.
    /// Shown once, when the island is up and nothing else (a story scene, a movie, a dialog) is on screen.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _trip;
        float _tripWait;

        void UpdateTrip()
        {
            var s = G.Session;
            if (s.LastTrip == null || _trip != null) { _tripWait = 0; return; }
            if (!s.AtHome || MapOpen || LoadingScreen.Active || StoryPlaying || Cinema.Playing || _confirm != null || _rewardCard.gameObject.activeSelf || Player.Instance == null) { _tripWait = 0; return; }
            _tripWait += Time.unscaledDeltaTime;
            if (_tripWait > 0.6f) ShowTrip(s.LastTrip);  // a moment on the beach first
        }

        void ShowTrip(TripLog t)
        {
            var s = G.Session;
            _trip = UIKit.Img("Trip", _root, null, new Color(0, 0, 0, 0.72f)).rectTransform;
            _trip.Fill();
            _trip.GetComponent<Image>().raycastTarget = true;
            var box = UIKit.Img("Box", _trip, UIKit.Round, UIKit.Panel).rectTransform;
            const float W = 1320;
            float y = -34;
            Text Line(string name, string text, int size, Color col, float h, TextAnchor a = TextAnchor.UpperCenter, bool bold = false)
            {
                var l = UIKit.Label(name, box, text, size, col, a, bold);
                l.supportRichText = true;
                h = FitHeight(l, W - 120, h);   // a long list of foes wraps onto a second line instead of running into the next
                l.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(W - 120, h));
                y -= h;
                return l;
            }

            Line("Title", "BACK AT " + World.Location(World.HomeId).Name.ToUpper(), 54, UIKit.Sand, 70, TextAnchor.UpperCenter, true);
            var where = t.places.Select(p => World.Location(p)?.Name).Where(n => n != null);
            var earned = new List<string>();
            if (t.xp > 0) earned.Add("<color=#8FE3A8>+" + t.xp + " XP</color>");
            if (t.coins > 0) earned.Add("<color=#FFC24A>+" + t.coins + " doubloons</color>");
            Line("Where", string.Join("  ·  ", where.Select(Nb)), 30, new Color(1, 1, 1, 0.75f), 44);
            if (earned.Count > 0) Line("Earned", string.Join("      ", earned), 30, Color.white, 44);
            y -= 10;

            // What was found: the biggest hauls first, up to ten tiles.
            if (t.gained.Count > 0)
            {
                Line("FoundT", "FOUND", 26, new Color(1, 1, 1, 0.5f), 36, TextAnchor.UpperCenter, true);
                var items = t.gained.Where(g => Content.Item(g.id) != null).OrderByDescending(g => g.n).ToList();
                int show = Mathf.Min(10, items.Count);
                const float size = 104, gap = 14;
                float total = show * size + (show - 1) * gap;
                for (int i = 0; i < show; i++)
                {
                    var tile = UIKit.Img("Item", box, null, new Color(0.12f, 0.15f, 0.15f, 1)).rectTransform;
                    tile.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-total / 2 + size / 2 + i * (size + gap), y), new Vector2(size, size));
                    UIKit.ItemIcon(tile, items[i].id, size * 0.8f);
                    var n = UIKit.Outlined(UIKit.Label("N", tile, items[i].n.ToString(), 28, Color.white, TextAnchor.LowerRight), 1.5f);
                    n.rectTransform.Fill(5);
                }
                y -= size + 8;
                if (items.Count > show) Line("More", "and " + (items.Count - show) + " more", 24, new Color(1, 1, 1, 0.5f), 34);
                y -= 8;
            }

            var facts = new List<string>();
            if (t.kills.Count > 0)
                facts.Add("<color=#FF8A70>DEFEATED</color>  " + string.Join(", ", t.kills.OrderByDescending(k => k.n).Select(k => Nb(k.n + " " + Plural(Enemies.All.TryGetValue(k.id, out var e) ? e.Name : k.id, k.n)))));
            if (t.rescued.Count > 0)
                facts.Add("<color=#8FE3A8>RESCUED</color>  " + string.Join(", ", t.rescued.Select(r => Nb(World.Signal(r)?.Npc ?? r))));
            if (t.learned.Count > 0)
                facts.Add("<color=#8FE3A8>LEARNED</color>  " + string.Join(", ", t.learned.Where(Content.Recipes.ContainsKey).Select(r => Nb(Content.NameOf(Content.Recipes[r].Output)))));
            // The islands' own rewards: plans found, improvements finished at camp meanwhile, materials only found there.
            foreach (var loc in t.plans)
            {
                var isl = Content.IslandOf(loc);
                if (isl != null) facts.Add("<color=#FFC24A>FOUND PLAN</color>  " + isl.PlanTitle + ": teaches the " + Content.NameOf(isl.Perk));
            }
            foreach (var perk in t.perks)
            {
                var isl = Content.IslandOf(perk);
                if (isl != null) facts.Add("<color=#FFC24A>NEW CAMP IMPROVEMENT</color>  " + Content.NameOf(perk) + ": " + isl.Effect);
            }
            var own = t.gained.Where(g => { var i = Content.IslandOf(g.id); return i != null && i.Ingredient == g.id; }).ToList();
            if (own.Count > 0)
                facts.Add("<color=#8FE3A8>ISLAND REWARD</color>  " + string.Join(", ", own.Select(g =>
                {
                    var l = World.Location(Content.IslandOf(g.id).Location);
                    return g.n + " " + Content.NameOf(g.id) + " (only on " + (l != null ? l.Name : "its island") + ")";
                })));
            if (t.falls > 0)
                facts.Add("<color=#FF8A70>PASSED OUT " + (t.falls == 1 ? "ONCE" : t.falls + " TIMES") + "</color>  " + (s.State.lostBags.Count > 0 ? "your backpack waits where you fell" : "and got your things back"));
            foreach (var f in facts) Line("Fact", f, 30, Color.white, 44);

            var can = s.NowPossible(3);
            if (can.Count > 0)
            {
                y -= 10;
                Line("CanT", "NOW YOU CAN", 26, new Color(1, 1, 1, 0.5f), 36, TextAnchor.UpperCenter, true);
                foreach (var c in can) Line("Can", "<color=#FFC24A>•</color>  " + c, 30, new Color(0.56f, 0.89f, 0.66f), 42);
            }
            y -= 10;
            Line("Next", "<color=#FFFFFF80>NEXT</color>   " + s.CurrentQuest.Label(s), 30, UIKit.Sand, 46);

            var ok = UIKit.Button("Continue", box, "CONTINUE", new Color(0.8f, 0.62f, 0.12f), CloseTrip, 36);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y - 20), new Vector2(340, 84));
            y -= 20 + 84 + 34;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, Mathf.Min(1040, -y)));
            StartCoroutine(PopIn(box, 0));
            Sfx.Play("quest");
        }

        /// <summary>How tall a card line must be for its text at its real (phone-grown) size, wrapped to
        /// <paramref name="width"/>; never less than <paramref name="min"/>.</summary>
        static float FitHeight(Text l, float width, float min)
        {
            var gs = l.GetGenerationSettings(new Vector2(width, 0));
            gs.resizeTextForBestFit = false;
            return Mathf.Max(min, l.cachedTextGeneratorForLayout.GetPreferredHeight(l.text, gs) / l.pixelsPerUnit + 8);
        }

        /// <summary>Keeps a name on one line when the list it's in wraps ("Old Lighthouse", "2 Wild Boars").</summary>
        static string Nb(string s) { return s.Replace(' ', '\u00A0'); }

        static string Plural(string name, int n) { return n == 1 || name.EndsWith("s") || name.EndsWith("age") ? name : name + "s"; } // "hull wreckage" stays as it is

        public bool TripShowing { get { return _trip != null; } }

        public void CloseTrip()
        {
            if (_trip != null) Destroy(_trip.gameObject);
            _trip = null;
            G.Session.TripSeen();
            GameRoot.Instance.Save();
            Sfx.Play("click");
        }
    }
}
