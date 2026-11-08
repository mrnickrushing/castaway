using System;
using System.Collections.Generic;

namespace LastTide.Core
{
    /// <summary>One inventory slot. Tools and weapons keep their wear in <see cref="wear"/>.</summary>
    [Serializable]
    public sealed class Slot
    {
        public string id;
        public int n;
        public int wear; // hits used (tools/weapons)
        public bool Empty { get { return string.IsNullOrEmpty(id) || n <= 0; } }
        public void Clear() { id = null; n = 0; wear = 0; }
    }

    /// <summary>A harvestable thing that is used up, and when it grows back (game seconds).</summary>
    [Serializable]
    public sealed class NodeState
    {
        public string key;        // world placement key, e.g. "palm#12"
        public int hitsUsed;
        public double respawnAt;  // 0 = standing
    }

    /// <summary>A camp structure: materials delivered so far, and whether it stands.</summary>
    [Serializable]
    public sealed class BuildState
    {
        public string id;
        public List<ItemCount> delivered = new List<ItemCount>();
        public bool done;
        public double stamp;      // raincatcher: when it last filled a flask
        public int stored;        // raincatcher: flasks waiting
    }

    /// <summary>A craft or cook in progress at a station.</summary>
    [Serializable]
    public sealed class JobState
    {
        public string station, recipe;
        public double doneAt;
        public int count;
    }

    /// <summary>Backpack contents dropped where the player fell.</summary>
    [Serializable]
    public sealed class LostBag
    {
        public float x, z;
        public List<Slot> slots = new List<Slot>();
    }

    [Serializable]
    public sealed class GameState
    {
        public int version = 1;
        public double time;                 // game seconds played
        public float health = 100, hunger = 100, thirst = 100;
        public float px, pz, facing;        // player position on the island
        public bool placed;                 // false until the world puts the player on the beach
        public List<Slot> backpack = new List<Slot>();
        public List<Slot> chest = new List<Slot>();
        public int equipped = -1;           // backpack slot of the held weapon/tool (-1 = fists)
        public List<NodeState> nodes = new List<NodeState>();
        public List<BuildState> builds = new List<BuildState>();
        public List<JobState> jobs = new List<JobState>();
        public LostBag lostBag;
        public int quest;                   // index into Quests.All
        public int kills, deaths, days;
        public List<string> flags = new List<string>();
        public bool muted;

        public static GameState New()
        {
            var s = new GameState();
            for (int i = 0; i < Content.BackpackSlots; i++) s.backpack.Add(new Slot());
            for (int i = 0; i < Content.ChestSlots; i++) s.chest.Add(new Slot());
            return s;
        }

        /// <summary>Fills in anything an older or damaged save lacks.</summary>
        public void Repair()
        {
            if (backpack == null) backpack = new List<Slot>();
            while (backpack.Count < Content.BackpackSlots) backpack.Add(new Slot());
            if (chest == null) chest = new List<Slot>();
            while (chest.Count < Content.ChestSlots) chest.Add(new Slot());
            if (nodes == null) nodes = new List<NodeState>();
            if (builds == null) builds = new List<BuildState>();
            if (jobs == null) jobs = new List<JobState>();
            if (flags == null) flags = new List<string>();
            foreach (var b in builds) if (b.delivered == null) b.delivered = new List<ItemCount>();
            if (equipped >= backpack.Count) equipped = -1;
        }
    }
}
