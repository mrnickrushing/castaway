using System.Collections.Generic;
using System.Linq;

namespace LastTide.Core
{
    public static partial class Content
    {
        /// <summary>
        /// One line on where an item comes from, for the description that pops up when its picture is tapped:
        /// "Made at the Furnace", "From: Iron Vein, Boulder", "Dropped by: Drowned Smuggler". Null when nothing on the
        /// islands makes or yields it (shop and reward items).
        /// </summary>
        public static string Origin(string id)
        {
            var r = Recipes.Values.FirstOrDefault(x => x.Output == id);
            if (r != null)
            {
                string made = r.Station == "hand" ? "Crafted by hand (CRAFT)" : "Made at the " + Session.StationName(r.Station);
                // The harpoon has to be taught first: say by whom, or the recipe looks broken.
                if (IsPerk(id))
                {
                    // A camp improvement: say where its plan is, and what it does for good.
                    var isl = IslandOf(id); var home = isl != null ? World.Location(isl.Location) : null;
                    return isl == null ? made : "Built once at the workbench, from the plan on " + (home != null ? home.Name : isl.Location) + "\n" + char.ToUpper(isl.Effect[0]) + isl.Effect.Substring(1) + ", for good";
                }
                var teacher = World.Signals.FirstOrDefault(sg => sg.Unlocks == r.Id);
                return teacher != null ? made + ", once " + teacher.Npc + " shows you how" : made;
            }
            var lines = new List<string>();
            var from = new List<string>();
            var tools = new List<ToolUse>();
            foreach (var n in Nodes.Values)
            {
                bool yields = (n.Yield != null && n.Yield.Any(y => y.id == id)) || (n.Bonus != null && n.Bonus.Any(y => y.id == id));
                if (!yields || from.Contains(n.Name)) continue;
                from.Add(n.Name);
                tools.Add(n.Needs);
            }
            // The cave's and the camp's barrels hold rum (Session.Harvest).
            var barrels = id == "rum" ? World.Locations.Where(l => !l.Home && l.MainLoot.Contains("rum")).Select(l => l.Name).ToList() : new List<string>();
            if (barrels.Count > 0) { from.Add("Ship Barrel (" + string.Join(", ", barrels) + ")"); tools.Add(ToolUse.None); }
            if (from.Count > 0)
            {
                // Only by tool (iron ore: a pickaxe) says which tool and where to make it, so nobody goes round in circles.
                string line = "From: " + string.Join(", ", from.Take(3));
                if (tools.All(t => t != ToolUse.None) && tools.Distinct().Count() == 1)
                {
                    var t = tools[0];
                    line += " (needs a " + (t == ToolUse.Chop ? "hatchet" : t == ToolUse.Mine ? "pickaxe" : "fishing rod") + Session.ToolHint(t) + ")";
                }
                lines.Add(line);
            }
            var by = Enemies.All.Values.Where(e => (e.Drops != null && e.Drops.Any(d => d.id == id)) || (e.Rare.HasValue && e.Rare.Value.id == id))
                .Select(e => e.Name).Distinct().Take(3).ToList();
            if (by.Count > 0) lines.Add("Dropped by: " + string.Join(", ", by));
            // Sailors' bags and lucky finds roll from each island's loot (Session.RollPossibleLoot, the loot window).
            var at = World.Locations.Where(l => !l.Home && !barrels.Contains(l.Name) && (l.MainLoot.Contains(id) || l.PossibleLoot.Contains(id))).Select(l => l.Name).ToList();
            var own = IslandOf(id);
            if (own != null && own.Ingredient == id && at.Count > 0)
            {
                // An island's own material: nowhere else, and what it's for.
                lines.Add("Only on " + at[0] + " (also in its sailors' bags and on its creatures)");
                lines.Add("Builds the " + NameOf(own.Perk));
            }
            else if (at.Count > 0) lines.Add("Found on: " + string.Join(", ", at.Take(3)) + (at.Count > 3 ? " and more" : ""));
            if (lines.Count == 0 && (Season.CrateLoot.Any(c => c.id == id) || Season.Free.Concat(Season.Premium).Any(c => c.id == id)
                || Season.Events.Any(e => e.Rewards.Any(rw => rw.Any(c => c.id == id))) || id.StartsWith("pet_")))
                lines.Add("A reward from the season and events (the star-shield button)");
            return lines.Count > 0 ? string.Join("\n", lines) : null;
        }
    }
}
