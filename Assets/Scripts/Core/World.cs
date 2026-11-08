using System;
using System.Collections.Generic;

namespace LastTide.Core
{
    /// <summary>The landscape a location's island is built from.</summary>
    public enum Biome { Home, Grove, Shore, Ridge, Pier, Lighthouse, Cave, PirateCamp }

    /// <summary>A place on the archipelago map: where it is, what it takes to get there and what's to be found.</summary>
    public sealed class LocationDef
    {
        public string Id, Name, Blurb;
        public Biome Biome;
        public int Level = 1;
        public float MapX, MapZ;            // position on the map (metres on the map plane)
        public int RunEnergy;               // energy to run there (from home)
        public int WalkSeconds;             // or walk there for free, slowly
        public string[] MainLoot = new string[0], PossibleLoot = new string[0];
        /// <summary>What stands on the island: node kind and how many.</summary>
        public (string kind, int n)[] Nodes = new (string, int)[0];
        /// <summary>Who lives there: enemy kind and how many dens.</summary>
        public (string kind, int n)[] Enemies = new (string, int)[0];
        public float Refresh = 20 * 60;     // loot grows back this long after a visit (real seconds)
        public float Size = 70;             // island radius
        public bool Home { get { return Biome == Biome.Home; } }
    }

    /// <summary>A distress call on the radio and the rescue it leads to.</summary>
    public sealed class SignalDef
    {
        public string Id, Title, Location, Npc;
        public int Level;
        public string Freq;                 // shown on the dial, e.g. "SW 9.4"
        public float Dial;                  // 0..1 position of the needle
        public string[] Chat;               // alternating: them, you, them...
        public ItemCount[] Need;            // what to bring
        public string Thanks;
        public ItemCount[] Reward = new ItemCount[0];
        public int RewardCoins, RewardXp;
        public string Unlocks;              // recipe id it teaches, if any
    }

    /// <summary>A pack in the trading post, paid for in doubloons.</summary>
    public sealed class OfferDef
    {
        public string Id, Name, Blurb, Art;
        public int Price, Limit = 1;
        public ItemCount[] Items;
        public int Energy;
        public float Hours = 30;            // how long it stays on sale after first shown
    }

    public static class World
    {
        public const int EnergyMax = 200;
        public const float EnergyEvery = 24f;   // real seconds per energy point
        public const string HomeId = "home";

        public static readonly List<LocationDef> Locations = new List<LocationDef>
        {
            new LocationDef { Id = HomeId, Name = "Wreck Cove", Biome = Biome.Home, MapX = 0, MapZ = 0, Size = 105,
                Blurb = "Home. The camp above the beach where your ship ran aground: a fire, a roof of palm leaves and everything you have built." },
            new LocationDef { Id = "grove", Name = "Palm Grove", Biome = Biome.Grove, Level = 1, MapX = 150, MapZ = 70, RunEnergy = 6, WalkSeconds = 150, Size = 62,
                Blurb = "A quiet islet thick with coconut palms and ferns. Good timber, sweet water from the coconuts, and boars rooting in the undergrowth.",
                MainLoot = new[] { "wood", "coconut", "fiber", "berries" }, PossibleLoot = new[] { "raw_meat", "hide", "rope" },
                Nodes = new[] { ("palm", 40), ("bush", 14), ("berrybush", 8), ("driftwood", 5), ("stones", 4) }, Enemies = new[] { ("boar", 3) } },
            new LocationDef { Id = "shore", Name = "Driftwood Shore", Biome = Biome.Shore, Level = 2, MapX = -150, MapZ = -60, RunEnergy = 8, WalkSeconds = 210, Size = 58,
                Blurb = "Where the currents wash up what the sea takes: broken cargo, barrels and timber. Giant crabs guard the tide pools.",
                MainLoot = new[] { "wood", "scrap", "cloth", "crab" }, PossibleLoot = new[] { "rope", "water", "pearl", "nails" },
                Nodes = new[] { ("driftwood", 14), ("crate", 7), ("barrel", 5), ("tidepool", 6), ("palm", 10), ("stones", 4) }, Enemies = new[] { ("crab", 5) } },
            new LocationDef { Id = "ridge", Name = "Limestone Ridge", Biome = Biome.Ridge, Level = 4, MapX = -40, MapZ = 190, RunEnergy = 12, WalkSeconds = 300, Size = 64,
                Blurb = "Pale cliffs above the jungle. Bring a pickaxe: the stone here is good and there's iron in the rock. Mind the snakes.",
                MainLoot = new[] { "stone", "iron_ore", "fiber" }, PossibleLoot = new[] { "scrap", "berries", "pearl" },
                Nodes = new[] { ("boulder", 16), ("ironvein", 7), ("stones", 8), ("bush", 8), ("palm", 8) }, Enemies = new[] { ("snake", 4), ("boar", 1) } },
            new LocationDef { Id = "pier", Name = "Fisherman's Pier", Biome = Biome.Pier, Level = 6, MapX = 230, MapZ = -120, RunEnergy = 14, WalkSeconds = 360, Size = 56,
                Blurb = "An old jetty built by someone who came before you. The fish bite well off the end of it, if you have a rod.",
                MainLoot = new[] { "fish", "rope", "wood" }, PossibleLoot = new[] { "nails", "cloth", "rum", "pearl" },
                Nodes = new[] { ("fishspot", 6), ("crate", 4), ("barrel", 4), ("palm", 12), ("driftwood", 6) }, Enemies = new[] { ("crab", 3) } },
            new LocationDef { Id = "lighthouse", Name = "Old Lighthouse", Biome = Biome.Lighthouse, Level = 8, MapX = -230, MapZ = 130, RunEnergy = 16, WalkSeconds = 420, Size = 60,
                Blurb = "A dark lighthouse on a rocky point. Its keeper is long gone, but the storerooms might not be empty.",
                MainLoot = new[] { "cloth", "rope", "nails", "scrap" }, PossibleLoot = new[] { "bandage", "rum", "gunpowder", "iron_ore" },
                Nodes = new[] { ("crate", 9), ("barrel", 5), ("boulder", 6), ("bush", 6), ("palm", 6) }, Enemies = new[] { ("boar", 2), ("snake", 2) } },
            new LocationDef { Id = "cave", Name = "Smuggler's Cave", Biome = Biome.Cave, Level = 10, MapX = 60, MapZ = -230, RunEnergy = 20, WalkSeconds = 540, Size = 55,
                Blurb = "A sea cave behind a sandbar, where smugglers hid their goods. Some of it is still there. So are some of the smugglers.",
                MainLoot = new[] { "gunpowder", "rum", "scrap", "iron_ore" }, PossibleLoot = new[] { "pearl", "cutlass", "nails", "doubloon" },
                Nodes = new[] { ("crate", 8), ("barrel", 6), ("ironvein", 5), ("boulder", 6), ("treasure", 2) }, Enemies = new[] { ("pirate", 3), ("snake", 2) } },
            new LocationDef { Id = "pirates", Name = "Pirate Camp", Biome = Biome.PirateCamp, Level = 12, MapX = 300, MapZ = 150, RunEnergy = 26, WalkSeconds = 720, Size = 66,
                Blurb = "Red Jack's raiders have made camp here. Their stores are full and their blades are sharp. Come armed, come fed.",
                MainLoot = new[] { "rum", "gunpowder", "cutlass", "doubloon" }, PossibleLoot = new[] { "pearl", "nails", "iron_ore", "bandage" },
                Nodes = new[] { ("crate", 8), ("barrel", 8), ("treasure", 3), ("palm", 10) }, Enemies = new[] { ("pirate", 6) } },
        };

        public static LocationDef Location(string id) { return Locations.Find(l => l.Id == id); }

        public static readonly List<SignalDef> Signals = new List<SignalDef>
        {
            new SignalDef { Id = "keeper", Title = "Distress signal", Level = 3, Freq = "SW 9.4", Dial = 0.42f, Location = "grove", Npc = "Old Tomas",
                Chat = new[] { "Hello? Is anyone out there? Please...", "I hear you! Where are you?", "Palm Grove, the islet north-east of the wreck. A boar got my leg. Bring bandages, quick..." },
                Need = new[] { Content.I("bandage", 2) }, Thanks = "Bless you, sailor. Here, take my old fishing rod. I won't be casting it for a while.",
                Reward = new[] { Content.I("rod", 1) }, RewardCoins = 20, RewardXp = 60 },
            new SignalDef { Id = "fisher", Title = "Faint voice", Level = 6, Freq = "MW 1300", Dial = 0.71f, Location = "pier", Npc = "Mara",
                Chat = new[] { "...anyone... the pier... the end of the pier...", "This is a castaway at Wreck Cove. Say again?", "Fisherman's Pier. My lines are rotten and I'm starving. Bring rope and a couple of fish and I'll show you how to forge an iron harpoon." },
                Need = new[] { Content.I("rope", 4), Content.I("fish", 2) }, Thanks = "Heat the iron, hammer the barb, bind it tight. Now you can make your own. Take these too.",
                Reward = new[] { Content.I("nails", 6) }, RewardCoins = 30, RewardXp = 100, Unlocks = "harpoon" },
            new SignalDef { Id = "captive", Title = "Coded message", Level = 10, Freq = "LW 220", Dial = 0.18f, Location = "cave", Npc = "Ned Barlow",
                Chat = new[] { "Three short, three long, three short...", "I read you. Who is this?", "Ned. Red Jack's men locked me in the smugglers' cave. Bring something to pick the lock and some rum for the guards..." },
                Need = new[] { Content.I("nails", 4), Content.I("rum", 2) }, Thanks = "Free! I know where Jack hides his doubloons. Here's a share.",
                Reward = new[] { Content.I("cutlass", 1) }, RewardCoins = 60, RewardXp = 180 },
        };

        public static SignalDef Signal(string id) { return Signals.Find(s => s.Id == id); }

        public static readonly List<OfferDef> Offers = new List<OfferDef>
        {
            new OfferDef { Id = "starter", Name = "Castaway's Bundle", Blurb = "Everything a new castaway wishes they'd saved from the wreck.", Art = "offer_starter", Price = 25,
                Items = new[] { Content.I("bandage", 3), Content.I("meat", 4), Content.I("water", 4), Content.I("rope", 4) }, Energy = 40 },
            new OfferDef { Id = "builder", Name = "Shipwright's Crate", Blurb = "Salvaged timber, iron and nails for a bigger camp.", Art = "offer_builder", Price = 45,
                Items = new[] { Content.I("wood", 40), Content.I("nails", 12), Content.I("scrap", 10), Content.I("iron_ore", 6) } },
            new OfferDef { Id = "fisher", Name = "Angler's Kit", Blurb = "A good rod, bait and a barrel of fresh water.", Art = "offer_fisher", Price = 35,
                Items = new[] { Content.I("rod", 1), Content.I("fish_cooked", 6), Content.I("water", 6) }, Energy = 20 },
        };
    }
}
