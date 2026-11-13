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
        public List<Slot> slots;  // a loot bag's contents
        public bool rolled;       // the bag's contents have been rolled
    }

    /// <summary>A camp structure: materials delivered so far, and whether it stands.</summary>
    [Serializable]
    public sealed class BuildState
    {
        public string id;
        public List<ItemCount> delivered = new List<ItemCount>();
        public bool done;
        public double stamp;      // raincatcher: when it last filled a flask
        public int level = 1;     // upgrades (campfire, workbench)
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
        public string location;
        public List<Slot> slots = new List<Slot>();
    }

    /// <summary>A walk across the archipelago in progress.</summary>
    [Serializable]
    public sealed class TravelState
    {
        public string to, from;
        public double leftAt, arriveAt;     // real unix seconds
    }

    /// <summary>What happened on a trip away from home, for the summary shown on getting back.</summary>
    [Serializable]
    public sealed class TripLog
    {
        public bool active;                 // JsonUtility can't store null: an empty log loads with this false
        public List<string> places = new List<string>();
        public List<ItemCount> gained = new List<ItemCount>(), kills = new List<ItemCount>();
        public List<string> knew = new List<string>(), rescuedBefore = new List<string>();   // at the start
        public List<string> learned = new List<string>(), rescued = new List<string>();     // filled in at the end
        public List<string> plans = new List<string>(), perksBefore = new List<string>(), perks = new List<string>(); // island plans found; improvements built meanwhile
        public int xp, coins, falls;
    }

    /// <summary>When a location was last left (its loot grows back after a while).</summary>
    [Serializable]
    public sealed class VisitState
    {
        public string id;
        public double leftAt;
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
        public List<LostBag> lostBags = new List<LostBag>(); // one per time the castaway passed out, oldest first
        public LostBag lostBag;             // saves before lostBags kept a single bag: moved into lostBags on load
        public List<ItemCount> waiting = new List<ItemCount>(); // paid-for items that didn't fit: they drop in when there's room
        public int quest;                   // index into Quests.All
        public int tutorial;                // the first-run tutorial's step (Hud.Tutorial; past the end = done)
        public int kills, deaths, days;
        public List<string> flags = new List<string>();
        public bool muted;
        // Progress and the wider world (Session.World).
        public int xp, level = 1, coins, pirates;
        public float energy = World.EnergyMax;
        public double energyStamp;          // real unix seconds when energy was last topped up
        public string location = World.HomeId;
        public TravelState travel;
        public TripLog trip, lastTrip;      // the trip under way, and the finished one the HUD hasn't shown yet
        public List<VisitState> visits = new List<VisitState>();
        public List<ItemCount> offers = new List<ItemCount>();   // offer id -> times bought
        public double offerStart;           // real time the current offer rotation started
        public float homeX, homeZ;          // where the castaway stood at home before leaving
        public List<string> grantedTx = new List<string>();               // App Store transactions already rewarded
        public List<PendingPurchase> pendingPurchases = new List<PendingPurchase>();
        public List<Slot> gear = new List<Slot>();          // head, body, legs, feet
        public List<PieceState> pieces = new List<PieceState>();
        public int levelsClaimed;           // level-up rewards taken
        public string name;                 // what the castaway is called (settings)
        // Season pass and events (Session.Season).
        public int seasonNo, seasonPts, seasonPremium, takesDay, takes;
        public List<int> seasonFree = new List<int>(), seasonPrem = new List<int>();
        public List<EventProgress> events = new List<EventProgress>();
        public string pet;                  // companion at heel
        public double savedAt;              // real time (unix seconds) of the last save, for the iCloud copy
        public bool noReminders;            // the player turned reminder notifications off (settings)

        public static GameState New()
        {
            var s = new GameState();
            for (int i = 0; i < Content.BackpackSlots; i++) s.backpack.Add(new Slot());
            for (int i = 0; i < Content.ChestSlots; i++) s.chest.Add(new Slot());
            for (int i = 0; i < 4; i++) s.gear.Add(new Slot());
            return s;
        }

        /// <summary>Fills in anything an older or damaged save lacks.</summary>
        public void Repair(int backpackSize = Content.BackpackSlots)
        {
            if (backpack == null) backpack = new List<Slot>();
            while (backpack.Count < backpackSize) backpack.Add(new Slot());
            if (chest == null) chest = new List<Slot>();
            while (chest.Count < Content.ChestSlots) chest.Add(new Slot());
            if (nodes == null) nodes = new List<NodeState>();
            if (builds == null) builds = new List<BuildState>();
            if (jobs == null) jobs = new List<JobState>();
            if (flags == null) flags = new List<string>();
            if (visits == null) visits = new List<VisitState>();
            if (offers == null) offers = new List<ItemCount>();
            if (grantedTx == null) grantedTx = new List<string>();
            if (pendingPurchases == null) pendingPurchases = new List<PendingPurchase>();
            if (gear == null) gear = new List<Slot>();
            while (gear.Count < 4) gear.Add(new Slot());
            if (pieces == null) pieces = new List<PieceState>();
            if (seasonFree == null) seasonFree = new List<int>();
            if (seasonPrem == null) seasonPrem = new List<int>();
            if (events == null) events = new List<EventProgress>();
            foreach (var p in pieces) if (p.slots == null) p.slots = new List<Slot>();
            if (level < 1) level = 1;
            if (string.IsNullOrEmpty(location) || World.Location(location) == null) location = World.HomeId;
            if (travel != null && string.IsNullOrEmpty(travel.to)) travel = null;
            if (trip != null && !trip.active) trip = null;
            if (lastTrip != null && !lastTrip.active) lastTrip = null;
            foreach (var log in new[] { trip, lastTrip })   // logs from before the island plans
                if (log != null) { if (log.plans == null) log.plans = new List<string>(); if (log.perksBefore == null) log.perksBefore = new List<string>(); if (log.perks == null) log.perks = new List<string>(); }
            // JsonUtility can't store null for a class field: a save written with no bag on the ground loads an empty one.
            if (lostBags == null) lostBags = new List<LostBag>();
            if (lostBag != null && lostBag.slots != null && lostBag.slots.Exists(s => !s.Empty)) lostBags.Add(lostBag);
            lostBag = null;
            lostBags.RemoveAll(b => b == null || b.slots == null || !b.slots.Exists(s => !s.Empty));
            if (waiting == null) waiting = new List<ItemCount>();
            waiting.RemoveAll(w => string.IsNullOrEmpty(w.id) || w.n <= 0 || Content.Item(w.id) == null);
            // Items this version doesn't know (a test grant, something since renamed) would sit in a slot as a blank tile.
            foreach (var list in new[] { backpack, chest, gear })
                foreach (var s in list) if (!s.Empty && Content.Item(s.id) == null) s.Clear();
            foreach (var p in pieces) foreach (var s in p.slots) if (!s.Empty && Content.Item(s.id) == null) s.Clear();
            foreach (var b in lostBags) foreach (var s in b.slots) if (!s.Empty && Content.Item(s.id) == null) s.Clear();
            lostBags.RemoveAll(b => !b.slots.Exists(s => !s.Empty));
            foreach (var b in builds) if (b.delivered == null) b.delivered = new List<ItemCount>();
            if (equipped >= backpack.Count) equipped = -1;
        }
    }
}
