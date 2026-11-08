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
        public ItemCount[] Reward = new ItemCount[0];

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
            Q("spear", "Craft a bamboo spear at the workbench", s => s.Has("spear") || s.Has("club") || s.State.kills > 0, build: "workbench"),
            Q("hunt", "Hunt a wild boar in the jungle", s => s.State.kills > 0, node: "boar", reward: new[] { Content.I("bandage", 1) }),
            Q("cook", "Roast meat on the campfire", s => s.Flag("cooked"), build: "campfire"),
            Q("rain", "Build a rain catcher", s => s.Built("raincatcher"), build: "raincatcher", reward: new[] { Content.I("water", 2) }),
            Q("chest", "Build a sea chest for your supplies", s => s.Built("chest"), build: "chest"),
            Q("free", "Your camp stands. Explore the island and survive!", s => false),
        };

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
