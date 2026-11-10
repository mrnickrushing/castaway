using System.Collections.Generic;
using System.Linq;

namespace LastTide.Core
{
    /// <summary>
    /// What each island is worth the trip for: a material found only there (its own harvest node, its enemies'
    /// drop, its sailors' bags), a plan found once at its landmark with a story to it, and the permanent camp
    /// improvement that plan teaches, built at the workbench from that material.
    /// </summary>
    public sealed class IslandDef
    {
        public string Location;
        public string Ingredient, Node;         // the material and the node that yields it
        public string PlanTitle, PlanStory;     // the landmark find
        public string Perk;                     // the improvement: a workbench recipe whose output is a flag, not an item
        public string Effect;                   // what it does, in a few words (map, help)
    }

    public static partial class Content
    {
        public static readonly List<IslandDef> Islands = new List<IslandDef>();

        /// <summary>The island whose material, plan or improvement this is (location id, item, node or perk).</summary>
        public static IslandDef IslandOf(string id)
        {
            return Islands.FirstOrDefault(i => i.Location == id || i.Ingredient == id || i.Node == id || i.Perk == id);
        }

        public static bool IsPerk(string id) { return id != null && id.StartsWith("perk_"); }

        static void ExtendIslands()
        {
            // The plan left at each landmark: picked up once (the session sets plan_<island>).
            Node("plan", "Something Left Behind", ToolUse.None, 1.5f, 1, -1f, new ItemCount[0], null);
            void Island(string loc, string ingredient, string name, string blurb, string node, string nodeName, ToolUse tool,
                ItemCount[] yield, ItemCount[] bonus, string planTitle, string planStory, string perk, string perkName, string perkBlurb, string effect, params ItemCount[] cost)
            {
                Item(ingredient, name, ItemKind.Resource, 20).Blurb = blurb;
                Node(node, nodeName, tool, 2.0f, 2, 300f, yield, bonus);
                Item(perk, perkName, ItemKind.Resource, 1).Blurb = perkBlurb;
                Recipe(perk, "workbench", 1, 20f, cost);
                Islands.Add(new IslandDef { Location = loc, Ingredient = ingredient, Node = node, PlanTitle = planTitle, PlanStory = planStory, Perk = perk, Effect = effect });
            }

            Island("grove", "honey", "Wild Honey", "Dark palm honey, taken from the old beehives on Palm Grove. It keeps for months.",
                "hive", "Old Beehive", ToolUse.None, new[] { I("honey", 1) }, new[] { I("fiber", 2) },
                "The Beekeeper's Box", "At the foot of the oldest palm, a little box scratched with tallies. Someone kept bees here, and wrote down how to keep their honey through the wet months.",
                "perk_larder", "Honey Larder", "Honey jars and a cool shelf at camp: everything you eat fills you up a fifth more.", "food fills you up 20% more",
                I("honey", 4), I("wood", 10), I("rope", 2));
            Island("shore", "ship_timber", "Ship's Timber", "Seasoned oak from a broken hull, still hard as iron. Only the shore's wrecks give it up.",
                "hull", "Hull Wreckage", ToolUse.Chop, new[] { I("ship_timber", 1) }, new[] { I("nails", 2) },
                "The Carpenter's Log", "A carpenter's chest washed up above the tideline, his log wrapped in oilcloth inside. The last page shows how he braced the captain's chest to hold more.",
                "perk_chest", "Timber-Braced Chest", "Oak braces and a second tray for the sea chest: six more slots.", "6 more slots in the sea chest",
                I("ship_timber", 4), I("nails", 8), I("rope", 2));
            Island("ridge", "lime", "Lime", "White chalk from the seams in Limestone Ridge. Burnt and slaked, it makes a wash that keeps out damp and rot.",
                "limeseam", "Chalk Seam", ToolUse.Mine, new[] { I("lime", 1) }, new[] { I("stone", 2) },
                "The Mason's Box", "Among the fallen stones of the ruins, a mason's box, and a carving of how the builders washed their walls with lime to keep the sickness out.",
                "perk_limewash", "Limewashed Shelter", "A clean, dry roof over your head: you heal two fifths faster.", "health comes back 40% faster",
                I("lime", 4), I("fiber", 8), I("water", 2));
            Island("pier", "net_cord", "Net Cord", "Tarred cord from the fishermen's old nets on the pier. Strong, and it doesn't rot in the sea.",
                "oldnet", "Tangled Net", ToolUse.None, new[] { I("net_cord", 1) }, new[] { I("rope", 1) },
                "Mara's Father's Weir", "A fisherman's box by the pier, and in it a drawing of a fish weir, the kind Mara's father set at the mouth of every creek. A fish that swims in can't find its way out.",
                "perk_weir", "Fish Weir", "A woven weir off the beach: fishing often brings in a second fish.", "fishing often brings a second fish",
                I("net_cord", 4), I("wood", 8), I("rope", 2));
            Island("lighthouse", "lamp_oil", "Lamp Oil", "Whale oil from the lighthouse stores, enough to keep a lamp burning all night.",
                "oildrum", "Oil Cask", ToolUse.None, new[] { I("lamp_oil", 1) }, new[] { I("scrap", 1) },
                "The Keeper's Last Entry", "The keeper's sea chest by the tower, and his logbook's last entry: as long as the lamp burned, nothing came up from the dark to the door. He ran out of oil in the winter.",
                "perk_lamp", "Signal Lamp", "A lamp that burns all night: animals and raiders notice you hardly further at night than by day.", "night barely makes enemies keener",
                I("lamp_oil", 3), I("scrap", 6), I("cloth", 2));
            Island("cave", "sea_salt", "Sea Salt", "Coarse salt scraped from the walls of the Smuggler's Cave, where the tide leaves it to dry.",
                "saltcrust", "Salt Crust", ToolUse.None, new[] { I("sea_salt", 1) }, new[] { I("stone", 1) },
                "The Smugglers' Ledger", "In a dry chest near the cave mouth, the smugglers' accounts, salt-stained: barrels of salted pork, enough for a crossing. Packed in salt, food lasts.",
                "perk_salt", "Salt Barrel", "Salted rations at camp: you get hungry a fifth more slowly.", "hunger drops 20% slower",
                I("sea_salt", 4), I("wood", 10), I("nails", 4));
            Island("pirates", "brass", "Brass Fittings", "Blocks, rings and sheaves from Red Jack's broken cannon carriages. Nothing runs a line like brass.",
                "brasswreck", "Broken Cannon", ToolUse.Mine, new[] { I("brass", 1) }, new[] { I("iron_ore", 1) },
                "Red Jack's Rigging Plan", "In a chest near Red Jack's fire: how he rigged his ship to run before the wind on half the canvas.",
                "perk_sail", "Brass-Rigged Sail", "Brass blocks for the raft's sail: SAIL costs a quarter less energy.", "SAIL costs 25% less energy",
                I("brass", 3), I("rope", 4), I("cloth", 4));

        }
    }
}
