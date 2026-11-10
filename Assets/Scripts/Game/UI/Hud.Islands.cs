using System.Linq;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// The islands' own rewards on screen: the card for a plan found at a landmark (its story, the improvement it
    /// teaches and what that takes), and the line on the map's island panel saying what the island is good for.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _plan;

        public bool PlanShowing { get { return _plan != null; } }

        void ShowPlan(IslandDef isl)
        {
            if (_plan != null) Destroy(_plan.gameObject);
            var s = G.Session;
            var loc = World.Location(isl.Location);
            _plan = UIKit.Img("Plan", _root, null, new Color(0, 0, 0, 0.72f)).rectTransform;
            _plan.Fill();
            _plan.GetComponent<Image>().raycastTarget = true;
            var box = UIKit.Img("Box", _plan, UIKit.Round, UIKit.Panel).rectTransform;
            const float W = 1240;
            float y = -30;
            Text Line(string text, int size, Color col, float h, bool bold = false, TextAnchor a = TextAnchor.UpperCenter)
            {
                var l = UIKit.Label("Line", box, text, size, col, a, bold);
                l.supportRichText = true;
                l.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(W - 120, h));
                y -= h;
                return l;
            }
            Line("FOUND ON " + (loc != null ? loc.Name.ToUpper() : ""), 26, new Color(1, 1, 1, 0.5f), 36, true);
            Line(isl.PlanTitle, 50, UIKit.Sand, 66, true);
            Line(isl.PlanStory, 30, new Color(1, 1, 1, 0.85f), 130);
            y -= 6;

            // What it teaches: the improvement's picture, then what it does and what it takes.
            const float icon = 150;
            var pic = UIKit.Img("Pic", box, null, new Color(0.12f, 0.15f, 0.15f, 1)).rectTransform;
            pic.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-(W - 120) / 2 + icon / 2, y), new Vector2(icon, icon));
            UIKit.ItemIcon(pic, isl.Perk, icon * 0.85f, false);
            var learned = UIKit.Label("Learned", box, "<color=#8FE3A8>LEARNED</color>  " + Content.NameOf(isl.Perk) + "\n<size=28><color=#FFFFFFB0>" + char.ToUpper(isl.Effect[0]) + isl.Effect.Substring(1) + ". Build it at the workbench.</color></size>", 36, Color.white, TextAnchor.UpperLeft, true);
            learned.supportRichText = true;
            learned.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(icon / 2 + 20, y - 4), new Vector2(W - 120 - icon - 40, 90));
            var cost = Content.Recipes[isl.Perk].Inputs;
            var row = UIKit.Rect("Cost", box).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(icon / 2 + 20, y - 96), new Vector2(W - 120 - icon - 40, 60));
            float x = 0;
            foreach (var c in cost)
            {
                bool own = c.id == isl.Ingredient;
                var ic = UIKit.Rect("I", row).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(56, 56));
                UIKit.ItemIcon(ic, c.id, 56);
                var n = UIKit.Label("N", row, c.n + " " + Content.NameOf(c.id), 26, own ? new Color(0.56f, 0.89f, 0.66f) : Color.white, TextAnchor.MiddleLeft, false);
                n.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x + 62, 0), new Vector2(230, 50));
                x += 62 + Mathf.Max(120, n.preferredWidth) + 26;
            }
            y -= icon + 14;
            Line("<color=#8FE3A8>" + Content.NameOf(isl.Ingredient) + "</color> is only found on " + (loc != null ? loc.Name : "this island") + ": " + Plural(Content.Nodes[isl.Node].Name.ToLower(), 2) + ", sailors' bags, and now and then on the creatures here.", 26, new Color(1, 1, 1, 0.65f), 44);

            var ok = UIKit.Button("Ok", box, "GOT IT", new Color(0.8f, 0.62f, 0.12f), ClosePlan, 36);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y - 16), new Vector2(320, 84));
            y -= 16 + 84 + 30;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, -y));
            StartCoroutine(PopIn(box, 0));
            Sfx.Play("quest");
            GameRoot.Instance.Save();
        }

        public void ClosePlan()
        {
            if (_plan != null) Destroy(_plan.gameObject);
            _plan = null;
            Sfx.Play("click");
        }

        /// <summary>The map panel's line about an island's own reward: its material and where the plan stands.</summary>
        static string IslandLine(LocationDef l)
        {
            var s = G.Session;
            var isl = Content.IslandOf(l.Id);
            if (isl == null || isl.Location != l.Id) return null;
            string state = s.HasPerk(isl.Perk) ? "built" : s.HasPlan(isl) ? "plan found" : "plan to find";
            return "<color=#8FE3A8>Own:</color> " + Content.NameOf(isl.Ingredient) + "  ·  " + Content.NameOf(isl.Perk) + " (" + state + ")";
        }
    }
}
