using System;
using System.Collections.Generic;
using System.Linq;

namespace LastTide.Core
{
    /// <summary>
    /// Days and nights on the island, Biscuit the ship's dog, sailors' bags to loot, sneaking, and level-up rewards
    /// that wait for the castaway to take them.
    /// </summary>
    public sealed partial class Session
    {
        // ------------------------------------------------------------------ day and night

        /// <summary>Game seconds in one day (dawn to dawn): eight minutes of day, four of night.</summary>
        public const float DayLength = 12 * 60;
        public const float NightShare = 1 / 3f;

        /// <summary>0..1 through the day, 0 = dawn. Night is the last third.</summary>
        public float DayPhase { get { return (float)((State.time + DayLength * 0.1) % DayLength / DayLength); } }
        public bool IsNight { get { return DayPhase > 1 - NightShare; } }
        public int Day { get { return 1 + (int)((State.time + DayLength * 0.1) / DayLength); } }

        /// <summary>How far an animal or raider notices the castaway: further at night, much less when sneaking.</summary>
        public float AggroScale(bool sneaking) { return (IsNight ? 1.35f : 1f) * (sneaking ? 0.45f : 1f); }

        // ------------------------------------------------------------------ Biscuit

        public bool HasPet { get { return ActivePet != null; } }
        /// <summary>Biscuit has been found (his quest step).</summary>
        public bool FoundBiscuit { get { return Flag("pet"); } }
        public const string PetName = "Biscuit";
        public const int PetDamage = 9;

        public void AdoptPet()
        {
            if (FoundBiscuit) return;
            SetFlag("pet");
            State.pet = "biscuit";
            Toast?.Invoke(PetName + " joins you!");
            AddXp(25);
            Changed?.Invoke();
            CheckQuests();
        }

        // ------------------------------------------------------------------ sailors' bags

        /// <summary>The contents of a loot bag, rolled the first time it's opened from what the location has.</summary>
        public List<Slot> Loot(string key, string kind)
        {
            if (!Standing(key, kind)) return null;
            var n = Node(key, true);
            if (n.slots == null) n.slots = new List<Slot>();
            if (!n.rolled)
            {
                n.rolled = true;
                n.slots.Clear();
                for (int i = 0; i < 8; i++) n.slots.Add(new Slot());
                var here = Here;
                var pool = here.Home ? new[] { "scrap", "cloth", "rope", "bandage", "water", "coconut", "nails" } : here.MainLoot.Concat(here.PossibleLoot).ToArray();
                var rng = new Random(key.GetHashCode() ^ (int)(Now / 60));
                int items = 2 + rng.Next(3);
                for (int i = 0; i < items; i++)
                {
                    var id = pool[rng.Next(pool.Length)];
                    if (id == "doubloon") { n.slots[i].id = "doubloon"; n.slots[i].n = 3 + rng.Next(8); continue; }
                    var d = Content.Item(id);
                    int count = d == null || d.Stack <= 1 ? 1 : 1 + rng.Next(4);
                    Inventory.Add(n.slots, id, count);
                }
            }
            return n.slots;
        }

        /// <summary>Takes one slot (or everything) out of a bag; an emptied bag is used up until it respawns.</summary>
        public int TakeLoot(string key, string kind, int slot)
        {
            var bag = Loot(key, kind);
            if (bag == null) return 0;
            int got = 0;
            for (int i = 0; i < bag.Count; i++)
            {
                if (slot >= 0 && i != slot) continue;
                var s = bag[i];
                if (s.Empty) continue;
                if (s.id == "doubloon") { Give("doubloon", s.n); got += s.n; s.Clear(); continue; }
                int k = Inventory.Add(State.backpack, s.id, s.n, s.wear);
                if (k > 0) { Gained?.Invoke(s.id, k); Tally("loot", k); }
                s.n -= k; got += k;
                if (s.n <= 0) s.Clear();
            }
            if (bag.All(s => s.Empty))
            {
                var n = Node(key, true);
                var d = Content.Nodes[kind];
                n.hitsUsed = d.Hits;
                n.respawnAt = Now + d.Respawn;
                n.rolled = false;
            }
            else if (got == 0) Toast?.Invoke("Backpack full");
            if (got > 0) { AddXp(2); Changed?.Invoke(); CheckQuests(); }
            return got;
        }

        // ------------------------------------------------------------------ level-up rewards

        /// <summary>Levels whose reward hasn't been taken yet.</summary>
        public int LevelRewardsWaiting { get { return Math.Max(0, State.level - 1 - State.levelsClaimed); } }

        public static ItemCount[] LevelReward(int level)
        {
            return new[] { Content.I("doubloon", 5 + level), level % 3 == 0 ? Content.I("bandage", 2) : Content.I("meat", 2) };
        }

        /// <summary>Takes every waiting level-up reward; returns the levels they were for.</summary>
        public List<int> ClaimLevelRewards()
        {
            var got = new List<int>();
            while (LevelRewardsWaiting > 0)
            {
                int lv = 2 + State.levelsClaimed;
                if (!Fits(LevelReward(lv))) { NoRoom(); break; } // waits under the quest card until there's room
                foreach (var r in LevelReward(lv)) Give(r.id, r.n);
                State.energy = Math.Min(World.EnergyMax, State.energy + 20);
                State.levelsClaimed++;
                got.Add(lv);
            }
            if (got.Count > 0) Changed?.Invoke();
            return got;
        }
    }
}
