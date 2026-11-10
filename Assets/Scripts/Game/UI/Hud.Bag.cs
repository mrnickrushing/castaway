using System.Collections.Generic;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// Less fuss with a full backpack: items the guide's current step needs carry a green QUEST tab, the BAG button
    /// warns when the backpack is (nearly) full, MAKE ROOM stores spare materials at home in one tap, items move
    /// between the pockets and the backpack, and leaving home with little room asks first.
    /// </summary>
    public sealed partial class Hud
    {
        static readonly Color QuestGreen = new Color(0.56f, 0.89f, 0.66f);
        RectTransform _bagWarn;
        Text _bagWarnText;
        const int TripRoom = 3; // fewer free slots than this when leaving home: ask first

        /// <summary>What the guide's current step still uses (a build's materials, a recipe's ingredients, what a
        /// survivor asked for) and, for crafted materials, what they are made from.</summary>
        public HashSet<string> QuestNeeds()
        {
            var s = G.Session;
            var q = s.CurrentQuest;
            var need = new HashSet<string>();
            void Add(string id, bool deep)
            {
                if (id == null || id == "doubloon" || !need.Add(id) || !deep) return;
                var r = GuideArrow.RecipeFor(id);
                if (r != null && !GuideArrow.Gatherable(id)) foreach (var c in r.Inputs) Add(c.id, false);
            }
            if (q == null) return need;
            if (q.Item != null) Add(q.Item, false);
            if (q.PointBuild != null && Content.Builds.ContainsKey(q.PointBuild) && !s.Built(q.PointBuild))
                foreach (var it in s.Remaining(q.PointBuild)) Add(it.id, s.Count(it.id) < it.n);
            var sig = q.Signal != null ? World.Signal(q.Signal) : null;
            if (sig != null) foreach (var c in sig.Need) Add(c.id, s.Count(c.id) < c.n);
            if (q.Craft != null && Content.Recipes.TryGetValue(q.Craft, out var rc)) foreach (var c in rc.Inputs) Add(c.id, false);
            return need;
        }

        /// <summary>The green tab on a slot whose item the current step needs.</summary>
        static void QuestTab(Transform cell, float size)
        {
            var tab = UIKit.Img("Quest", cell, UIKit.Round, new Color(0.1f, 0.42f, 0.22f, 0.95f));
            tab.raycastTarget = false;
            float w = Mathf.Max(58, size * 0.5f), h = Mathf.Max(22, size * 0.2f);
            tab.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(3, -3), new Vector2(w, h));
            var t = UIKit.Label("T", tab.transform, "QUEST", Mathf.RoundToInt(h * 0.72f), QuestGreen, TextAnchor.MiddleCenter, false);
            t.rectTransform.Fill();
        }

        /// <summary>Under the BAG button: "FULL" in red, or "2 LEFT" in amber when it's nearly full.</summary>
        void BagWarning(RectTransform bagButton)
        {
            _bagWarn = UIKit.Img("Room", bagButton, UIKit.Round, UIKit.Red).rectTransform;
            _bagWarn.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -2), new Vector2(96, 30));
            _bagWarn.GetComponent<Image>().raycastTarget = false;
            _bagWarnText = UIKit.Label("T", _bagWarn, "", 20, Color.white, TextAnchor.MiddleCenter, false);
            _bagWarnText.rectTransform.Fill();
            _bagWarn.gameObject.SetActive(false);
        }

        void UpdateBagWarning()
        {
            if (_bagWarn == null) return;
            int free = G.Session.FreeSlots;
            _bagWarn.gameObject.SetActive(free <= 2);
            if (free > 2) return;
            _bagWarn.GetComponent<Image>().color = free == 0 ? UIKit.Red : new Color(0.75f, 0.5f, 0.08f);
            _bagWarnText.text = free == 0 ? "FULL" : free + " LEFT";
        }

        /// <summary>Stores what the current step doesn't need (at home) and says what happened.</summary>
        void MakeRoom()
        {
            var s = G.Session;
            int before = s.FreeSlots;
            int stored = s.MakeRoom(QuestNeeds());
            int gained = s.FreeSlots - before;
            if (stored > 0) Sfx.Play("deliver"); else Sfx.Play("click");
            _sel = -1; _dirty = true;
            if (stored > 0) Toast("Stored " + stored + " in your chest and boxes: " + s.FreeSlots + " free slots");
            else if (gained > 0) Toast("Stacks topped up: " + s.FreeSlots + " free slots");
            else if (!s.AtHome) Toast("Your chest is at home: drop something here, or take it back");
            else Toast("Nothing spare to store: the guide needs the rest");
        }

        /// <summary>Leaving home with little room for what's out there: offer to make room first.</summary>
        bool TripRoomCheck(System.Action go)
        {
            var s = G.Session;
            int free = s.FreeSlots;
            if (!s.AtHome || free >= TripRoom || !s.Storages.GetEnumerator().MoveNext()) return false;
            Confirm("Room for the trip?", (free == 0 ? "Your backpack is full." : "Only " + free + " free slot" + (free == 1 ? "" : "s") + " in your backpack.")
                + "\nSTORE & GO puts the materials you don't need right now in your chest, then sets off.",
                "STORE & GO", () => { MakeRoom(); go(); }, go, "GO ANYWAY");
            return true;
        }

        /// <summary>The side of the inventory when nothing is picked: how pockets work, and MAKE ROOM.</summary>
        void BagOverview()
        {
            var s = G.Session;
            int free = s.FreeSlots;
            var hint = UIKit.Label("Hint", _invSide, "Tap an item to see it. Tap it again (or USE) to eat, drink, hold or wear it.\n\n<color=#E8C66A>Pockets</color> are kept when you pass out; the rest of the backpack is left where you fell. Move things between them with TO POCKETS.", 28, new Color(1, 1, 1, 0.65f), TextAnchor.UpperCenter, false);
            hint.supportRichText = true;
            hint.rectTransform.Fill(40); hint.rectTransform.offsetMax = new Vector2(-40, -40); hint.rectTransform.offsetMin = new Vector2(40, 300);
            var room = UIKit.Label("Room", _invSide, free == 0 ? "Backpack full" : free + " free slot" + (free == 1 ? "" : "s"), 34, free == 0 ? UIKit.Red : free <= 2 ? UIKit.Amber : UIKit.Sand, TextAnchor.MiddleCenter, false);
            room.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 240), new Vector2(560, 50));
            bool home = s.AtHome && s.Storages.GetEnumerator().MoveNext();
            var why = UIKit.Label("Why", _invSide, home ? "Stores the materials you don't need right now in your chest. What the guide asks for stays."
                : "Your chest is at home: store materials there before a trip. SORT tops up stacks.", 24, new Color(1, 1, 1, 0.55f), TextAnchor.UpperCenter, false);
            why.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 128), new Vector2(560, 104));
            if (!home) return;
            var b = UIKit.Button("MakeRoom", _invSide, "MAKE ROOM", new Color(0.15f, 0.45f, 0.45f), MakeRoom, 36);
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(360, 84));
        }
    }
}
