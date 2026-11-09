using System;
using System.Collections.Generic;

namespace LastTide.Core
{
    /// <summary>One step of the survival guide: what to do, how far along it is, and where to point.</summary>
    public sealed class Quest
    {
        public string Id, Text;
        public Func<Session, bool> Done;
        public Func<Session, int> Have;     // progress shown as (have/need); null = no counter
        public int Need;
        public string PointNode, PointBuild; // what the guide arrow points at
        /// <summary>A crafting step: the recipe to make (the guide fetches its ingredients, then sends you to CRAFT).</summary>
        public string Craft;
        /// <summary>A rescue step: the radio signal whose survivor needs things brought.</summary>
        public string Signal;
        public ItemCount[] Reward = new ItemCount[0];
        /// <summary>The Ultra track's reward for this step (Survivor's Path).</summary>
        public ItemCount[] Premium = new ItemCount[0];

        public string Label(Session s)
        {
            if (Have == null) return Text;
            return Text + " (" + Math.Min(Need, Have(s)) + "/" + Need + ")";
        }
    }

    public static class Quests
    {
        public static readonly List<Quest> All = new List<Quest>
        {
            Q("hatchet", "Grab the hatchet from the wreck", s => s.Has("hatchet") || s.Flag("got_hatchet"), node: "hatchet_pickup"),
            Count("wood", "Chop palm trees for wood", "wood", 6, "campfire", node: "palm"),
            Count("stone", "Pick up loose stones", "stone", 5, "campfire", node: "stones"),
            Q("campfire", "Build a campfire", s => s.Built("campfire"), build: "campfire", reward: new[] { Content.I("coconut", 2) }),
            Count("fiber", "Pull fiber from fern bushes", "fiber", 10, "shelter", node: "bush"),
            Count("cloth", "Search the wreck crates for sailcloth", "cloth", 2, "shelter", node: "crate"),
            Q("shelter", "Build a palm shelter", s => s.Built("shelter"), build: "shelter", reward: new[] { Content.I("bandage", 1) }),
            Count("scrap", "Salvage ship scrap from the wreck", "scrap", 4, "workbench", node: "crate"),
            Q("workbench", "Build a workbench", s => s.Built("workbench"), build: "workbench"),
            new Quest { Id = "spear", Text = "Craft a bamboo spear (CRAFT)", Done = s => s.Has("spear") || s.Has("club") || s.State.kills > 0, Craft = "spear" },
            Q("hunt", "Hunt a wild boar in the jungle", s => s.State.kills > 0, node: "boar", reward: new[] { Content.I("bandage", 1) }),
            Q("cook", "Roast meat on the campfire", s => s.Flag("cooked"), build: "campfire"),
            Q("rain", "Build a rain catcher", s => s.Built("raincatcher"), build: "raincatcher", reward: new[] { Content.I("water", 2) }),
            Q("chest", "Build a sea chest for your supplies", s => s.Built("chest"), build: "chest"),
            Q("map", "Open the map and run to Palm Grove", s => s.Flag("visited_grove"), node: "exit", reward: new[] { Content.I("doubloon", 10) }),
            Q("level3", "Gather, build and hunt to reach level 3", s => s.State.level >= 3),
            Q("dog", "Find the Gull's dog on Driftwood Shore", s => s.FoundBiscuit, node: "pet"),
            Q("radio", "Rebuild the ship's radio at camp", s => s.Built("radio"), build: "radio", reward: new[] { Content.I("doubloon", 10) }),
            Q("tune", "Tune the radio to the distress signal", s => s.Flag("heard_keeper"), build: "radio"),
            Q("keeper", "Bring 2 bandages to Old Tomas at Palm Grove", s => s.Flag("done_keeper"), node: "npc"),
            Q("pier", "Reach level 6, then answer Mara's call from the pier", s => s.Flag("done_fisher"), node: "npc"),
            Q("captive", "Free Ned from the Smuggler's Cave (level 10)", s => s.Flag("done_captive"), node: "npc"),
            new Quest { Id = "powder", Text = "Gather gunpowder for Ned's plan", Need = 6, Have = s => s.Count("gunpowder"),
                Done = s => s.Count("gunpowder") >= 6 || s.Flag("redjack_dead"), PointNode = "skeleton", Reward = new[] { Content.I("bandage", 3) } },
            Q("redjack", "Defeat Red Jack at the Pirate Camp (level 12)", s => s.Flag("redjack_dead"), node: "redjack", reward: new[] { Content.I("doubloon", 50) }),
            Q("sail", "Board Red Jack's ship and sail on the Last Tide", s => s.Flag("ending"), node: "ship"),
            Q("free", "Captain of the islands: explore, build and survive", s => false),
        };

        /// <summary>Every step pays something on the free track and more on the Ultra track.</summary>
        static Quests()
        {
            All.Find(q => q.Id == "keeper").Signal = "keeper";
            All.Find(q => q.Id == "pier").Signal = "fisher";
            All.Find(q => q.Id == "captive").Signal = "captive";
            var ultra = new[]
            {
                new[] { Content.I("meat", 3), Content.I("water", 3) }, new[] { Content.I("bandage", 2) }, new[] { Content.I("rope", 4) },
                new[] { Content.I("doubloon", 25) }, new[] { Content.I("nails", 8) }, new[] { Content.I("iron_bar", 2) },
                new[] { Content.I("hide_vest", 1) }, new[] { Content.I("doubloon", 40) }, new[] { Content.I("stew", 2) },
            };
            for (int i = 0; i < All.Count; i++)
            {
                var q = All[i];
                if (q.Reward.Length == 0) q.Reward = new[] { Content.I("doubloon", 5 + i) };
                q.Premium = ultra[i % ultra.Length];
            }
        }

        static Quest Q(string id, string text, Func<Session, bool> done, string node = null, string build = null, ItemCount[] reward = null)
        {
            return new Quest { Id = id, Text = text, Done = done, PointNode = node, PointBuild = build, Reward = reward ?? new ItemCount[0] };
        }

        /// <summary>"Gather n of an item" — also done once the structure it is for stands.</summary>
        static Quest Count(string id, string text, string item, int need, string forBuild, string node)
        {
            return new Quest
            {
                Id = id, Text = text, Need = need, PointNode = node,
                Have = s => s.Count(item) + s.Delivered(forBuild, item),
                Done = s => s.Built(forBuild) || s.Count(item) + s.Delivered(forBuild, item) >= need,
            };
        }
    }
}
