using System.Collections.Generic;

namespace LastTide.Core
{
    /// <summary>A time-limited event: a goal counted while it runs, with milestone rewards.</summary>
    public sealed class EventDef
    {
        public string Id, Name, Blurb, Art, Stat, Unit;
        public int StartDay, Days;          // within the event cycle
        public int[] Targets;
        public ItemCount[][] Rewards;
    }

    /// <summary>A companion the castaway can have at heel (the model is a Quaternius animal in Models/People).</summary>
    public sealed class PetDef
    {
        public string Id, Name, Model, Blurb;
        public float Height = 0.6f;         // shoulder-to-ear height in metres
    }

    /// <summary>
    /// The season pass (four weeks of season points, a free and a premium reward on every level) and the event calendar
    /// (a fortnight's cycle of overlapping events, each with its own goal). Content and tuning; rules in Session.Season.
    /// </summary>
    public static class Season
    {
        public const int Days = 28, Levels = 30, PointsPerLevel = 250;
        public const int EpochDay = 20759;          // Monday 2 November 2026: season 1 began (launch)
        public const int TakesPerDay = 3, TakePoints = 50;
        public const int CycleDays = 14;
        public const string PremiumProduct = "lasttide.season";

        public static readonly List<PetDef> Pets = new List<PetDef>
        {
            new PetDef { Id = "biscuit", Name = "Biscuit", Model = "shiba", Height = 0.55f, Blurb = "The bosun's dog, found on Driftwood Shore." },
            new PetDef { Id = "husky", Name = "Nanook", Model = "husky", Height = 0.72f, Blurb = "A sled dog from a whaler's crew. Never tires." },
            new PetDef { Id = "fox", Name = "Ember", Model = "fox", Height = 0.48f, Blurb = "A ship's fox, quick and sly. Bites above her size." },
            new PetDef { Id = "wolf", Name = "Grey", Model = "wolf", Height = 0.82f, Blurb = "Raised by the lighthouse keeper. The raiders fear him." },
        };

        public static PetDef Pet(string id) { return Pets.Find(p => p.Id == id); }

        // Rewards on each level: free track and premium track.
        public static readonly ItemCount[] Free = new ItemCount[Levels + 1], Premium = new ItemCount[Levels + 1];

        public static readonly List<EventDef> Events = new List<EventDef>
        {
            Ev("boar_hunt", "Boar Hunt", "The boars are rooting up the island. Hunt them down before they ruin every garden.", "grove", "kill:boar", "boars", 0, 7,
                new[] { 3, 10, 25 }, R(Content.I("meat", 6), Content.I("doubloon", 10)), R(Content.I("hide", 8), Content.I("supply_crate", 1)), R(Content.I("hide_vest", 1), Content.I("doubloon", 40))),
            Ev("driftwood", "Driftwood Rush", "A storm has thrown timber all over the beaches. Gather wood while it lasts.", "shore", "gather:wood", "wood", 2, 7,
                new[] { 40, 120, 300 }, R(Content.I("rope", 4), Content.I("doubloon", 10)), R(Content.I("plank", 15), Content.I("supply_crate", 1)), R(Content.I("iron_hatchet", 1), Content.I("doubloon", 40))),
            Ev("treasure", "Treasure Tide", "Sailors' bags and sea chests wash in on every tide. Loot them before the gulls do.", "home", "loot", "things looted", 4, 5,
                new[] { 8, 25, 60 }, R(Content.I("doubloon", 15)), R(Content.I("supply_crate", 2)), R(Content.I("gold_chest", 1))),
            Ev("fishing", "Fisherman's Luck", "The shoals are running close to shore. Old Tomas swears this happens once a year.", "pier", "gather:fish", "fish", 5, 4,
                new[] { 5, 15, 35 }, R(Content.I("fish_cooked", 4), Content.I("doubloon", 10)), R(Content.I("rum", 3), Content.I("supply_crate", 1)), R(Content.I("harpoon", 1), Content.I("doubloon", 30))),
            Ev("raiders", "Raiders' Reckoning", "Red Jack's raiders are spreading across the islands. Drive them back.", "pirates", "kill:raider", "raiders", 7, 7,
                new[] { 5, 15, 35 }, R(Content.I("gunpowder", 2), Content.I("doubloon", 15)), R(Content.I("tricorn", 1), Content.I("supply_crate", 1)), R(Content.I("cutlass", 1), Content.I("gold_chest", 1))),
            Ev("carpenter", "Carpenter's Week", "Every survivor on the islands wants a roof. Make things at your stations and campfire.", "home", "craft", "things made", 9, 5,
                new[] { 10, 30, 70 }, R(Content.I("nails", 10), Content.I("doubloon", 10)), R(Content.I("iron_bar", 6), Content.I("supply_crate", 1)), R(Content.I("iron_pickaxe", 1), Content.I("doubloon", 40))),
            Ev("moonlit", "Moonlit Hunt", "Things prowl the islands after dark. Win your fights at night.", "lighthouse", "nightkill", "night kills", 11, 5,
                new[] { 4, 12, 30 }, R(Content.I("bandage", 4), Content.I("doubloon", 10)), R(Content.I("stew", 3), Content.I("supply_crate", 1)), R(Content.I("pirate_coat", 1), Content.I("gold_chest", 1))),
        };

        public static EventDef Event(string id) { return Events.Find(e => e.Id == id); }

        static EventDef Ev(string id, string name, string blurb, string art, string stat, string unit, int start, int days, int[] targets, params ItemCount[][] rewards)
        {
            return new EventDef { Id = id, Name = name, Blurb = blurb, Art = art, Stat = stat, Unit = unit, StartDay = start, Days = days, Targets = targets, Rewards = rewards };
        }

        static ItemCount[] R(params ItemCount[] r) { return r; }

        // What's inside a supply crate and a captain's gold chest: always some doubloons, then draws from the table,
        // every entry as likely as the next (an entry listed twice is twice as likely). OddsText shows exactly this.
        public const int CrateCoinsMin = 5, CrateCoinsMax = 15, CrateDraws = 3;
        public const int ChestCoinsMin = 40, ChestCoinsMax = 80, ChestDraws = 2;

        /// <summary>The odds as the player reads them (App Store rule 3.1.1: randomised paid rewards say their odds).</summary>
        public static string OddsText(string id)
        {
            bool gold = id == "gold_chest";
            var pool = gold ? ChestLoot : CrateLoot;
            var parts = new List<string>();
            var seen = new List<string>();
            foreach (var e in pool)
            {
                string key = e.id + "x" + e.n;
                if (seen.Contains(key)) continue;
                seen.Add(key);
                int k = 0; foreach (var o in pool) if (o.id == e.id && o.n == e.n) k++;
                parts.Add(e.n + " " + Content.NameOf(e.id) + (k > 1 ? " (" + k + " in " + pool.Length + ")" : ""));
            }
            double each = 100.0 / pool.Length;
            return Content.NameOf(id) + ": always " + (gold ? ChestCoinsMin + "-" + ChestCoinsMax : CrateCoinsMin + "-" + CrateCoinsMax)
                + " doubloons, plus " + (gold ? ChestDraws : CrateDraws) + " draws. Each draw is one of these, each 1 in " + pool.Length
                + " (" + each.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%): " + string.Join(", ", parts) + ".";
        }

        // What's inside a supply crate and a captain's gold chest.
        public static readonly ItemCount[] CrateLoot =
        {
            Content.I("rope", 3), Content.I("cloth", 4), Content.I("nails", 6), Content.I("bandage", 2), Content.I("meat", 3),
            Content.I("water", 3), Content.I("plank", 6), Content.I("iron_bar", 2), Content.I("scrap", 6),
        };
        public static readonly ItemCount[] ChestLoot =
        {
            Content.I("iron_bar", 5), Content.I("gunpowder", 2), Content.I("pearl", 2), Content.I("stew", 3), Content.I("hide_vest", 1),
            Content.I("tricorn", 1), Content.I("iron_hatchet", 1), Content.I("cutlass", 1), Content.I("rum", 3),
        };

        static Season()
        {
            ItemCount[] freeCycle = { Content.I("rope", 4), Content.I("bandage", 3), Content.I("meat", 4), Content.I("nails", 8), Content.I("water", 4), Content.I("cloth", 5), Content.I("doubloon", 10), Content.I("plank", 8) };
            ItemCount[] premCycle = { Content.I("doubloon", 25), Content.I("iron_bar", 4), Content.I("stew", 2), Content.I("bandage", 6), Content.I("gunpowder", 2), Content.I("supply_crate", 1), Content.I("pearl", 2), Content.I("rum", 2) };
            for (int lv = 1; lv <= Levels; lv++)
            {
                Free[lv] = lv % 5 == 0 ? Content.I("supply_crate", lv >= 20 ? 2 : 1) : freeCycle[lv % freeCycle.Length];
                Premium[lv] = lv % 10 == 0 ? Content.I("gold_chest", 1) : premCycle[lv % premCycle.Length];
            }
            Premium[9] = Content.I("pet_husky", 1);
            Free[15] = Content.I("pet_fox", 1);
            Premium[30] = Content.I("pet_wolf", 1);
            Premium[29] = Content.I("gold_chest", 2);
        }
    }
}
