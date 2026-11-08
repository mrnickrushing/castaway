using System.Collections.Generic;

namespace LastTide.Core
{
    /// <summary>What a placeable piece is: a floor tile, a wall on a cell edge, or an object standing in a cell.</summary>
    public enum PieceKind { Floor, Edge, Object }

    /// <summary>Something the castaway builds freely on the camp grid (build mode).</summary>
    public sealed class PieceDef
    {
        public string Id, Name, Group, Blurb;
        public PieceKind Kind;
        public ItemCount[] Cost;
        public int Slots;           // storage
        public string Station;      // crafting station it provides (e.g. "furnace")
        public bool Blocks = true;  // in the way of walking (doorways and floors aren't)
        public int Level = 1;       // castaway level needed
    }

    public static partial class Content
    {
        public const float CellSize = 3f;
        public const int GridHalf = 9;  // the camp grid runs from -9 to 9 cells around the clearing
        public static readonly Dictionary<string, PieceDef> Pieces = new Dictionary<string, PieceDef>();
        public static readonly string[] PieceGroups = { "Building", "Storage", "Workbenches", "Furniture" };

        /// <summary>The second half of the content: clothes, iron, station levels, free building pieces, descriptions.</summary>
        static void ExtendBase()
        {
            // Iron and clothes.
            Item("iron_bar", "Iron Bar", ItemKind.Resource, 30);
            Item("plank", "Wooden Plank", ItemKind.Resource, 30).Blurb = "Sawn and planed at the workbench. Better building, better boxes.";
            Recipe("plank", "workbench", 1, 15f, I("wood", 5));
            Node("lootbag", "Sailor's Bag", ToolUse.None, 0.8f, 1, 900f, new ItemCount[0], null);
            Tool("iron_hatchet", "Iron Hatchet", ToolUse.Chop, 16, 160);
            Tool("iron_pickaxe", "Iron Pickaxe", ToolUse.Mine, 15, 160);
            Gear("palm_hat", "Palm-Leaf Hat", GearSlot.Head, 2, "Woven from palm leaves. Keeps the sun off and a little of the blows.");
            Gear("hide_vest", "Hide Vest", GearSlot.Body, 6, "Boar hide stitched with fibre. Turns a tusk, mostly.");
            Gear("hide_pants", "Hide Leggings", GearSlot.Legs, 4, "Tough leggings for the undergrowth.");
            Gear("hide_boots", "Hide Boots", GearSlot.Feet, 2, "Better than bare feet on coral.");
            Gear("tricorn", "Raider's Tricorn", GearSlot.Head, 5, "Taken from one of Red Jack's men.");
            Gear("pirate_coat", "Red Jack's Coat", GearSlot.Body, 14, "Heavy red wool with brass buttons. It has seen a lot of fights.");
            Food("stew", "Island Stew", 55, 20, 10);

            Recipe("palm_hat", "hand", 1, 3f, I("fiber", 6), I("wood", 1));
            Recipe("hide_vest", "workbench", 1, 6f, I("hide", 3), I("fiber", 4), I("rope", 1));
            Recipe("hide_pants", "workbench", 1, 5f, I("hide", 2), I("fiber", 4));
            Recipe("hide_boots", "workbench", 1, 4f, I("hide", 1), I("fiber", 2));
            Recipe("iron_bar", "furnace", 1, 12f, I("iron_ore", 2), I("wood", 1));
            Recipe("iron_hatchet", "workbench", 1, 8f, I("iron_bar", 3), I("wood", 2), I("rope", 1)).MinLevel = 2;
            Recipe("iron_pickaxe", "workbench", 1, 8f, I("iron_bar", 3), I("wood", 2), I("rope", 1)).MinLevel = 2;
            Recipe("stew", "campfire", 1, 8f, I("meat", 1), I("berries", 2), I("water", 1)).MinLevel = 2;
            Recipes["nails"].Inputs = new[] { I("scrap", 2) };
            Recipe("nails_iron", "workbench", 8, 3f, I("iron_bar", 1)).Output = "nails";

            Builds["campfire"].Upgrades = new[] { new[] { I("stone", 12), I("wood", 10), I("scrap", 2) } };
            Builds["campfire"].UpgradeBlurb = "A stone hearth with a spit: cooks twice as fast and can make island stew.";
            Builds["workbench"].Upgrades = new[] { new[] { I("wood", 20), I("iron_bar", 4), I("nails", 8) } };
            Builds["workbench"].UpgradeBlurb = "An iron vice and a proper anvil: iron tools.";

            // Free building on the camp grid.
            Piece("foundation", "Foundation", "Building", PieceKind.Floor, "A plank floor. Builds a proper house when you add walls.", 0, null, false, I("wood", 6));
            Piece("wall", "Wall", "Building", PieceKind.Edge, "A plank wall on the edge of a floor tile.", 0, null, true, I("wood", 4), I("fiber", 2));
            Piece("doorway", "Doorway", "Building", PieceKind.Edge, "A wall with a way through.", 0, null, false, I("wood", 4), I("fiber", 2));
            Piece("window", "Window", "Building", PieceKind.Edge, "A wall with a shuttered window.", 0, null, true, I("wood", 4), I("fiber", 3));
            Piece("small_box", "Small Box", "Storage", PieceKind.Object, "Eight more slots of storage.", 8, null, true, I("wood", 10), I("fiber", 5));
            Piece("storage_chest", "Storage Chest", "Storage", PieceKind.Object, "A big iron-bound chest: 16 slots.", 16, null, true, I("plank", 6), I("nails", 6), I("rope", 2)).Level = 4;
            Piece("furnace", "Furnace", "Workbenches", PieceKind.Object, "Smelts iron ore into bars.", 0, "furnace", true, I("stone", 20), I("wood", 10)).Level = 4;
            Piece("bed", "Bed", "Furniture", PieceKind.Object, "Rest here to heal. You wake here after passing out.", 0, null, true, I("plank", 3), I("cloth", 4), I("fiber", 6));
            Piece("doghouse", "Dog House", "Furniture", PieceKind.Object, "A kennel for Biscuit to curl up in.", 0, null, true, I("plank", 4), I("cloth", 2));
            Piece("table", "Table", "Furniture", PieceKind.Object, "A rough plank table.", 0, null, true, I("wood", 8));
            Piece("chair", "Chair", "Furniture", PieceKind.Object, "Somewhere to sit at last.", 0, null, false, I("wood", 4));
            Piece("torch", "Torch", "Furniture", PieceKind.Object, "Lights up the camp.", 0, null, false, I("wood", 2), I("cloth", 1));
            Piece("flag", "Camp Flag", "Furniture", PieceKind.Object, "Your own colours, made from the Gull's sail.", 0, null, true, I("wood", 4), I("cloth", 3));

            // Descriptions for the blueprints screen.
            Blurb("hatchet", "A rusty hatchet from the Gull's locker. Chops palms.");
            Blurb("pickaxe", "A sharp stone lashed to a handle. Breaks rock and iron veins.");
            Blurb("spear", "Bamboo with a stone point. Keeps boars at a distance.");
            Blurb("club", "Heavy wood studded with ship's iron.");
            Blurb("rope", "Plaited plant fibre. Holds most things together.");
            Blurb("bandage", "Clean sailcloth and fibre. Stops the bleeding.");
            Blurb("nails", "Iron nails for chests and better buildings.");
            Blurb("rod", "A springy rod with a line. Fish bite at the pier.");
            Blurb("harpoon", "An iron-barbed harpoon, as Mara taught you.");
            Blurb("cutlass", "A raider's curved blade.");
            Blurb("iron_bar", "Smelted iron, ready for the anvil.");
            Blurb("iron_hatchet", "Bites deep and lasts long.");
            Blurb("iron_pickaxe", "Makes short work of rock.");
            Blurb("meat", "Roasted over the fire.");
            Blurb("crab_cooked", "Sweet and smoky.");
            Blurb("fish_cooked", "Grilled on a stick.");
            Blurb("stew", "Meat, berries and fresh water in one pot. Fills you up for a long time.");
        }

        static void Gear(string id, string name, GearSlot slot, int armor, string blurb)
        {
            var d = Item(id, name, ItemKind.Tool, 1);
            d.Gear = slot; d.Armor = armor; d.Blurb = blurb; d.Durability = 80 + armor * 10;
        }

        static void Blurb(string id, string text) { var d = Item(id); if (d != null) d.Blurb = text; }

        static PieceDef Piece(string id, string name, string group, PieceKind kind, string blurb, int slots, string station, bool blocks, params ItemCount[] cost)
        {
            var p = new PieceDef { Id = id, Name = name, Group = group, Kind = kind, Blurb = blurb, Slots = slots, Station = station, Blocks = blocks, Cost = cost };
            Pieces[id] = p;
            return p;
        }

        /// <summary>All recipes in display order for the blueprints screen, by category.</summary>
        public static string CategoryOf(RecipeDef r)
        {
            var d = Item(r.Output);
            if (d == null) return "Other";
            if (d.Gear != GearSlot.None) return "Clothes";
            if (d.Kind == ItemKind.Weapon) return "Weapons";
            if (d.Kind == ItemKind.Tool) return "Tools";
            if (d.Kind == ItemKind.Food || d.Kind == ItemKind.Drink || d.Kind == ItemKind.Medical) return "Food";
            return "Materials";
        }
    }
}
