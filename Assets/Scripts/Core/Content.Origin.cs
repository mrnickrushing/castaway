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
            if (r != null) return r.Station == "hand" ? "Crafted by hand (CRAFT)" : "Made at the " + Session.StationName(r.Station);
            var from = new List<string>();
            var tools = new List<ToolUse>();
            foreach (var n in Nodes.Values)
            {
                bool yields = (n.Yield != null && n.Yield.Any(y => y.id == id)) || (n.Bonus != null && n.Bonus.Any(y => y.id == id));
                if (!yields || from.Contains(n.Name)) continue;
                from.Add(n.Name);
                tools.Add(n.Needs);
            }
            if (from.Count > 0)
            {
                // Only by tool (iron ore: a pickaxe) says which tool and where to make it, so nobody goes round in circles.
                string line = "From: " + string.Join(", ", from.Take(3));
                if (tools.All(t => t != ToolUse.None) && tools.Distinct().Count() == 1)
                {
                    var t = tools[0];
                    line += " (needs a " + (t == ToolUse.Chop ? "hatchet" : t == ToolUse.Mine ? "pickaxe" : "fishing rod") + Session.ToolHint(t) + ")";
                }
                return line;
            }
            var by = Enemies.All.Values.Where(e => (e.Drops != null && e.Drops.Any(d => d.id == id)) || (e.Rare.HasValue && e.Rare.Value.id == id))
                .Select(e => e.Name).Distinct().Take(3).ToList();
            if (by.Count > 0) return "Dropped by: " + string.Join(", ", by);
            return null;
        }
    }
}
