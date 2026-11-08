using System;
using System.Collections.Generic;

namespace LastTide.Core
{
    public enum ItemKind { Resource, Food, Drink, Tool, Weapon, Medical }

    /// <summary>What a tool is good for: chopping palms, breaking rock.</summary>
    public enum ToolUse { None, Chop, Mine }

    public sealed class ItemDef
    {
        public string Id, Name;
        public ItemKind Kind;
        public int Stack = 20;
        public int Food, Drink, Heal;        // restored when used
        public int Damage;                   // weapons (and tools used as weapons)
        public ToolUse Use;                  // tools
        public int Durability;               // tools and weapons: hits before it breaks (0 = unbreakable)
    }

    /// <summary>One item and how many.</summary>
    [Serializable]
    public struct ItemCount
    {
        public string id;
        public int n;
        public ItemCount(string id, int n) { this.id = id; this.n = n; }
        public override string ToString() { return n + " " + id; }
    }

    /// <summary>A crafting recipe (made at the workbench) or a cooking recipe (made at the campfire).</summary>
    public sealed class RecipeDef
    {
        public string Id, Output, Station;
        public int Count = 1;
        public ItemCount[] Inputs;
        public float Seconds = 2f;
    }

    /// <summary>A camp structure, built by standing in its blueprint ring and feeding it materials.</summary>
    public sealed class BuildDef
    {
        public string Id, Name, Blurb;
        public ItemCount[] Cost;
    }

    /// <summary>Something on the island the player harvests by standing in its ring.</summary>
    public sealed class NodeDef
    {
        public string Id, Name;
        public ToolUse Needs;           // tool required (None = bare hands)
        public float Seconds = 1.6f;    // per harvest with bare hands / the right tool
        public int Hits = 3;            // harvests before it is used up
        public float Respawn = 180f;    // seconds until it grows back
        public ItemCount[] Yield;       // per harvest
        public ItemCount[] Bonus;       // extra drop on the last harvest
        public int ThirstPerHit;        // freshwater spring: drink instead of loot
    }

    /// <summary>All game content and tuning.</summary>
    public static class Content
    {
        public static readonly Dictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>();
        public static readonly Dictionary<string, RecipeDef> Recipes = new Dictionary<string, RecipeDef>();
        public static readonly Dictionary<string, BuildDef> Builds = new Dictionary<string, BuildDef>();
        public static readonly Dictionary<string, NodeDef> Nodes = new Dictionary<string, NodeDef>();

        // Survival tuning (per second unless noted).
        public const float HungerDecay = 1f / 10f;  // a full belly lasts about 16 minutes
        public const float ThirstDecay = 1f / 7f;   // full of water, about 12 minutes
        public const float StarveDamage = 0.5f;     // health lost per second at 0 hunger or thirst
        public const float RegenRate = 0.35f;       // health regained per second when fed and watered
        public const float RegenAbove = 40f;        // hunger and thirst both above this to regenerate
        public const int BackpackSlots = 16;
        public const int ChestSlots = 24;
        public const int FistDamage = 6;
        public const float RainSeconds = 45f;       // rain catcher fills one water flask
        public const int RainCap = 5;

        static Content()
        {
            // Resources
            Item("wood", "Wood", ItemKind.Resource, 40);
            Item("stone", "Stone", ItemKind.Resource, 40);
            Item("fiber", "Plant Fiber", ItemKind.Resource, 40);
            Item("scrap", "Ship Scrap", ItemKind.Resource, 30);
            Item("cloth", "Sailcloth", ItemKind.Resource, 30);
            Item("hide", "Boar Hide", ItemKind.Resource, 20);
            Item("rope", "Rope", ItemKind.Resource, 20);
            // Food and drink
            Food("coconut", "Coconut", 6, 18, 0);
            Food("berries", "Wild Berries", 8, 4, 0);
            Food("raw_meat", "Raw Meat", 6, 0, -4);
            Food("meat", "Roast Meat", 32, 0, 6);
            Food("crab", "Raw Crab", 4, 0, -2);
            Food("crab_cooked", "Grilled Crab", 22, 2, 4);
            Food("water", "Flask of Water", 0, 40, 0);
            var water = Items["water"]; water.Kind = ItemKind.Drink;
            Items["coconut"].Kind = ItemKind.Drink;
            // Medicine
            Item("bandage", "Bandage", ItemKind.Medical, 10).Heal = 25;
            // Tools and weapons
            Tool("hatchet", "Rusty Hatchet", ToolUse.Chop, 10, 60);
            Tool("pickaxe", "Stone Pickaxe", ToolUse.Mine, 9, 60);
            Tool("spear", "Bamboo Spear", ToolUse.None, 18, 45);
            Tool("club", "Spiked Club", ToolUse.None, 26, 70);

            // Workbench recipes
            Recipe("pickaxe", "workbench", 1, 3f, I("wood", 4), I("stone", 4), I("fiber", 3));
            Recipe("hatchet", "workbench", 1, 3f, I("wood", 3), I("stone", 3), I("scrap", 2));
            Recipe("spear", "workbench", 1, 3f, I("wood", 5), I("stone", 2), I("fiber", 4));
            Recipe("club", "workbench", 1, 4f, I("wood", 8), I("scrap", 4), I("rope", 2));
            Recipe("rope", "workbench", 1, 2f, I("fiber", 4));
            Recipe("bandage", "workbench", 1, 2f, I("fiber", 3), I("cloth", 1));
            // Campfire recipes
            Recipe("meat", "campfire", 1, 4f, I("raw_meat", 1));
            Recipe("crab_cooked", "campfire", 1, 3f, I("crab", 1));

            // Camp structures
            Build("campfire", "Campfire", "Cook food and keep the night at bay.", I("wood", 6), I("stone", 5));
            Build("shelter", "Palm Shelter", "Rest to heal. You wake up here if you fall.", I("wood", 12), I("fiber", 10), I("cloth", 2));
            Build("workbench", "Workbench", "Craft tools, weapons and rope.", I("wood", 14), I("stone", 8), I("scrap", 4));
            Build("raincatcher", "Rain Catcher", "Collects fresh water over time.", I("wood", 8), I("cloth", 3), I("scrap", 2));
            Build("chest", "Sea Chest", "Store what you can't carry. Safe if you fall.", I("wood", 16), I("scrap", 3), I("rope", 2));

            // Harvestable things
            Node("palm", "Palm Tree", ToolUse.Chop, 1.5f, 4, 150f, new[] { I("wood", 3) }, new[] { I("coconut", 2) });
            Node("driftwood", "Driftwood", ToolUse.None, 1.4f, 2, 120f, new[] { I("wood", 2) }, null);
            Node("stones", "Loose Stones", ToolUse.None, 1.4f, 2, 120f, new[] { I("stone", 2) }, null);
            Node("boulder", "Boulder", ToolUse.Mine, 1.8f, 4, 200f, new[] { I("stone", 4) }, new[] { I("scrap", 1) });
            Node("bush", "Fern Bush", ToolUse.None, 1.2f, 2, 90f, new[] { I("fiber", 3) }, null);
            Node("berrybush", "Berry Bush", ToolUse.None, 1.2f, 2, 150f, new[] { I("berries", 2), I("fiber", 1) }, null);
            Node("crate", "Wreck Crate", ToolUse.None, 2.2f, 1, 420f, new[] { I("scrap", 3), I("cloth", 2) }, null);
            Node("barrel", "Ship Barrel", ToolUse.None, 2.0f, 1, 420f, new[] { I("water", 1), I("scrap", 1) }, null);
            Node("tidepool", "Tide Pool", ToolUse.None, 2.0f, 2, 160f, new[] { I("crab", 1) }, null);
            Node("spring", "Fresh Spring", ToolUse.None, 1.2f, 99, 1f, new ItemCount[0], null).ThirstPerHit = 25;
            Node("hatchet_pickup", "Hatchet", ToolUse.None, 0.6f, 1, -1f, new[] { I("hatchet", 1) }, null);
        }

        public static ItemCount I(string id, int n) { return new ItemCount(id, n); }

        static ItemDef Item(string id, string name, ItemKind kind, int stack)
        {
            var d = new ItemDef { Id = id, Name = name, Kind = kind, Stack = stack };
            Items[id] = d;
            return d;
        }

        static void Food(string id, string name, int food, int drink, int heal)
        {
            var d = Item(id, name, ItemKind.Food, 20);
            d.Food = food; d.Drink = drink; d.Heal = heal;
        }

        static void Tool(string id, string name, ToolUse use, int damage, int durability)
        {
            var d = Item(id, name, use == ToolUse.None ? ItemKind.Weapon : ItemKind.Tool, 1);
            d.Use = use; d.Damage = damage; d.Durability = durability;
        }

        static void Recipe(string output, string station, int count, float seconds, params ItemCount[] inputs)
        {
            Recipes[output] = new RecipeDef { Id = output, Output = output, Station = station, Count = count, Seconds = seconds, Inputs = inputs };
        }

        static void Build(string id, string name, string blurb, params ItemCount[] cost)
        {
            Builds[id] = new BuildDef { Id = id, Name = name, Blurb = blurb, Cost = cost };
        }

        static NodeDef Node(string id, string name, ToolUse needs, float seconds, int hits, float respawn, ItemCount[] yield, ItemCount[] bonus)
        {
            var d = new NodeDef { Id = id, Name = name, Needs = needs, Seconds = seconds, Hits = hits, Respawn = respawn, Yield = yield, Bonus = bonus };
            Nodes[id] = d;
            return d;
        }

        public static ItemDef Item(string id) { ItemDef d; return Items.TryGetValue(id, out d) ? d : null; }
        public static string NameOf(string id) { var d = Item(id); return d != null ? d.Name : id; }
    }
}
