using System;
using System.Collections.Generic;
using System.Linq;

namespace LastTide.Core
{
    public sealed class EnemyDef
    {
        public string Id, Name;
        public int Health, Damage;
        public float Speed, AttackEvery, Aggro;
        public int Xp;
        public ItemCount? Rare;     // a one-in-four extra drop
        public ItemCount[] Drops;
    }

    public static class Enemies
    {
        public static readonly Dictionary<string, EnemyDef> All = new Dictionary<string, EnemyDef>
        {
            { "boar", new EnemyDef { Id = "boar", Name = "Wild Boar", Health = 60, Damage = 9, Speed = 4.2f, AttackEvery = 1.3f, Aggro = 9f, Xp = 10,
                Drops = new[] { Content.I("raw_meat", 2), Content.I("hide", 1) }, Rare = Content.I("hide", 1) } },
            { "crab", new EnemyDef { Id = "crab", Name = "Giant Crab", Health = 35, Damage = 6, Speed = 2.6f, AttackEvery = 1.2f, Aggro = 6f, Xp = 6,
                Drops = new[] { Content.I("crab", 2) } } },
            { "snake", new EnemyDef { Id = "snake", Name = "Island Viper", Health = 30, Damage = 11, Speed = 4.8f, AttackEvery = 1.0f, Aggro = 7f, Xp = 9,
                Drops = new[] { Content.I("raw_meat", 1) } } },
            { "skeleton", new EnemyDef { Id = "skeleton", Name = "Drowned Smuggler", Health = 110, Damage = 11, Speed = 3.6f, AttackEvery = 1.25f, Aggro = 10f, Xp = 26,
                Drops = new[] { Content.I("gunpowder", 1), Content.I("doubloon", 3) } } },
            { "redjack", new EnemyDef { Id = "redjack", Name = "Red Jack", Health = 440, Damage = 14, Speed = 4.4f, AttackEvery = 1.3f, Aggro = 14f, Xp = 300,
                Drops = new[] { Content.I("doubloon", 120), Content.I("cutlass", 1), Content.I("pirate_coat", 1) } } },
            { "pirate", new EnemyDef { Id = "pirate", Name = "Red Jack's Raider", Health = 130, Damage = 11, Speed = 4.6f, AttackEvery = 1.2f, Aggro = 11f, Xp = 30,
                Drops = new[] { Content.I("rum", 1), Content.I("gunpowder", 1), Content.I("doubloon", 4) }, Rare = Content.I("tricorn", 1) } },
        };
    }

    /// <summary>
    /// The survival rules: meters, gathering, building, crafting, combat damage and death. Pure logic over a
    /// <see cref="GameState"/>; the Unity side calls in and listens to the events.
    /// </summary>
    public sealed partial class Session
    {
        public readonly GameState State;
        /// <summary>Short messages for the player ("+3 Wood", "Backpack full").</summary>
        public event Action<string> Toast;
        /// <summary>Items gained, for the floating pickup icons.</summary>
        public event Action<string, int> Gained;
        public event Action<string> BuildFinished;
        public event Action<Quest> QuestDone;
        public event Action Died;
        public event Action Changed;

        public Session(GameState state)
        {
            State = state ?? GameState.New();
            State.Repair();
            State.Repair(BackpackSize);
        }

        public double Now { get { return State.time; } }

        // ------------------------------------------------------------------ queries

        public int Count(string id) { return Inventory.Count(State.backpack, id); }
        public bool Has(string id) { return Count(id) > 0; }
        public bool Flag(string f) { return State.flags.Contains(f); }
        public void SetFlag(string f) { if (!State.flags.Contains(f)) State.flags.Add(f); }

        public BuildState Build(string id) { return State.builds.FirstOrDefault(b => b.id == id); }
        public bool IsBuilt(string id) { var b = Build(id); return b != null && b.done; }
        public bool Built(string id) { return IsBuilt(id); }

        public int Delivered(string buildId, string item)
        {
            var b = Build(buildId);
            if (b == null) return 0;
            if (b.done) return 0;
            int n = 0;
            foreach (var d in b.delivered) if (d.id == item) n += d.n;
            return n;
        }

        public Quest CurrentQuest { get { return Quests.All[Math.Min(State.quest, Quests.All.Count - 1)]; } }

        // ------------------------------------------------------------------ time

        /// <summary>Advances the world by <paramref name="dt"/> game seconds.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0) return;
            State.time += dt;
            UpdateEnergy();
            // Once a second: a step finished by an item that arrived some other way (a crate, a reward, a purchase).
            _questCheck -= dt;
            if (_questCheck <= 0) { _questCheck = 1; if (State.waiting.Count > 0) DeliverWaiting(); CheckQuests(); }
            State.hunger = Clamp(State.hunger - Content.HungerDecay * dt);
            State.thirst = Clamp(State.thirst - Content.ThirstDecay * dt);
            if (State.hunger <= 0 || State.thirst <= 0) Damage(Content.StarveDamage * dt * ((State.hunger <= 0 ? 1 : 0) + (State.thirst <= 0 ? 1 : 0)), "starving");
            else if (State.hunger > Content.RegenAbove && State.thirst > Content.RegenAbove) State.health = Clamp(State.health + Content.RegenRate * dt);

            // Finished crafts drop into the backpack (they wait at the station while it is full).
            for (int i = State.jobs.Count - 1; i >= 0; i--)
            {
                var j = State.jobs[i];
                if (Now < j.doneAt) continue;
                var r = Content.Recipes[j.recipe];
                int got = Inventory.Add(State.backpack, r.Output, j.count);
                if (got > 0) { Gained?.Invoke(r.Output, got); Tally("craft", got); if (r.Station == "campfire") SetFlag("cooked"); }
                j.count -= got;
                if (j.count <= 0) { State.jobs.RemoveAt(i); Changed?.Invoke(); CheckQuests(); }
            }

            // The rain catcher fills a flask now and then.
            var rain = Build("raincatcher");
            if (rain != null && rain.done)
            {
                while (rain.stored < Content.RainCap && Now - rain.stamp >= Content.RainSeconds) { rain.stored++; rain.stamp += Content.RainSeconds; }
                if (rain.stored >= Content.RainCap) rain.stamp = Now;
            }
        }

        static float Clamp(float v) { return v < 0 ? 0 : v > 100 ? 100 : v; }

        // ------------------------------------------------------------------ gathering

        internal NodeState Node(string key, bool create)
        {
            foreach (var n in State.nodes) if (n.key == key) return n;
            if (!create) return null;
            var s = new NodeState { key = key };
            State.nodes.Add(s);
            return s;
        }

        /// <summary>True when the node is there to harvest (not used up, or grown back).</summary>
        public bool Standing(string key, string kind)
        {
            var n = Node(key, false);
            if (n == null) return true;
            var d = Content.Nodes[kind];
            if (n.respawnAt > 0 && Now >= n.respawnAt) { n.respawnAt = 0; n.hitsUsed = 0; }
            return n.hitsUsed < d.Hits;
        }

        /// <summary>0..1 how much of the node is left (trees thin as they are chopped).</summary>
        public float Left(string key, string kind)
        {
            var n = Node(key, false);
            var d = Content.Nodes[kind];
            if (n == null || !Standing(key, kind)) return n == null ? 1 : 0;
            return 1f - n.hitsUsed / (float)d.Hits;
        }

        /// <summary>The backpack slot of the best tool for this job, or -1.</summary>
        public int ToolFor(ToolUse use)
        {
            int best = -1, bestDmg = -1;
            for (int i = 0; i < State.backpack.Count; i++)
            {
                var s = State.backpack[i];
                if (s.Empty) continue;
                var d = Content.Item(s.id);
                if (d != null && d.Use == use && d.Damage > bestDmg) { best = i; bestDmg = d.Damage; }
            }
            return best;
        }

        /// <summary>Why the player can't harvest this right now, or null when they can.</summary>
        public string Blocked(string kind)
        {
            var d = Content.Nodes[kind];
            if (d.Needs == ToolUse.Chop && ToolFor(ToolUse.Chop) < 0) return "Needs a hatchet";
            if (d.Needs == ToolUse.Mine && ToolFor(ToolUse.Mine) < 0) return "Needs a pickaxe";
            if (d.ThirstPerHit > 0) return State.thirst >= 99.5f ? "Not thirsty" : null;
            if (d.Needs == ToolUse.Fish && ToolFor(ToolUse.Fish) < 0) return "Needs a fishing rod";
            foreach (var y in d.Yield) if (y.id != "doubloon" && Inventory.Room(State.backpack, y.id) <= 0) return "Backpack full";
            return null;
        }

        public float HarvestSeconds(string kind) { return Content.Nodes[kind].Seconds; }

        /// <summary>One harvest: items into the backpack, tool wear, the node used up a little.</summary>
        public bool Harvest(string key, string kind)
        {
            if (!Standing(key, kind) || Blocked(kind) != null) return false;
            var d = Content.Nodes[kind];
            var n = Node(key, true);
            n.hitsUsed++;
            if (d.ThirstPerHit > 0)
            {
                State.thirst = Clamp(State.thirst + d.ThirstPerHit);
                Gained?.Invoke("water", 0);
                n.hitsUsed = 0;
                Changed?.Invoke();
                return true;
            }
            foreach (var y in d.Yield) { Give(y.id, y.n); Tally("gather:" + y.id, y.n); }
            // The smugglers' and pirates' barrels hold their rum (Ned needs two bottles before the pirate camp opens).
            if (kind == "barrel" && !Here.Home && Array.IndexOf(Here.MainLoot, "rum") >= 0) Give("rum", 1);
            if (n.hitsUsed >= d.Hits)
            {
                if (d.Bonus != null) foreach (var b in d.Bonus) Give(b.id, b.n);
                n.respawnAt = d.Respawn < 0 ? 0 : Now + d.Respawn;
            }
            if (kind == "hatchet_pickup") SetFlag("got_hatchet");
            RollPossibleLoot();
            AddXp(2);
            if (d.Needs != ToolUse.None) Wear(ToolFor(d.Needs));
            Changed?.Invoke();
            CheckQuests();
            return true;
        }

        /// <summary>Adds items to the backpack; whatever doesn't fit is lost with a warning (a harvest, a drop).
        /// Rewards check <see cref="Fits"/> first and wait where they are; paid-for things use <see cref="GiveOrHold"/>.</summary>
        public int Give(string id, int n)
        {
            int got = Put(id, n);
            if (got < n) Toast?.Invoke("Backpack full");
            return got;
        }

        static bool NeedsNoRoom(string id) { return id == "doubloon" || id.StartsWith("pet_"); }

        int Put(string id, int n)
        {
            if (id == "doubloon") { State.coins += n; Gained?.Invoke(id, n); Changed?.Invoke(); return n; }
            if (id.StartsWith("pet_")) { UnlockPet(id.Substring(4)); Changed?.Invoke(); return n; }
            int got = Inventory.Add(State.backpack, id, n);
            if (got > 0) Gained?.Invoke(id, got);
            if (got > 0 && Content.Item(id) != null && Content.Item(id).Kind == ItemKind.Weapon && State.equipped < 0) AutoEquip();
            return got;
        }

        /// <summary>Would all of these go into the backpack? Doubloons and companions need no room. <paramref name="leaving"/>:
        /// one of an item that comes out first (the crate being opened).</summary>
        public bool Fits(IEnumerable<ItemCount> items, string leaving = null)
        {
            var copy = new List<Slot>(State.backpack.Count);
            foreach (var s in State.backpack) copy.Add(new Slot { id = s.id, n = s.n, wear = s.wear });
            if (leaving != null) Inventory.Remove(copy, leaving, 1);
            foreach (var i in items)
                if (i.n > 0 && !NeedsNoRoom(i.id) && Inventory.Add(copy, i.id, i.n) < i.n) return false;
            return true;
        }

        double _noRoomAt = -1;
        /// <summary>A reward that doesn't fit stays where it is; says so once however many were tried at once.</summary>
        void NoRoom()
        {
            if (_noRoomAt == State.time) return;
            _noRoomAt = State.time;
            Toast?.Invoke("Backpack full: make room, then claim it");
        }

        /// <summary>For things already paid for (a purchase, a rescue): what doesn't fit waits and drops into the
        /// backpack as soon as there is room (<see cref="DeliverWaiting"/>).</summary>
        void GiveOrHold(string id, int n)
        {
            int left = n - Put(id, n);
            if (left <= 0) return;
            int i = State.waiting.FindIndex(w => w.id == id);
            if (i < 0) State.waiting.Add(new ItemCount(id, left)); else State.waiting[i] = new ItemCount(id, State.waiting[i].n + left);
            Toast?.Invoke("Backpack full: the rest waits until you make room");
        }

        /// <summary>Moves what's waiting into the backpack as far as it fits. Returns how many went in.</summary>
        public int DeliverWaiting()
        {
            int moved = 0;
            for (int i = 0; i < State.waiting.Count; i++)
            {
                var w = State.waiting[i];
                int k = Put(w.id, w.n);
                moved += k;
                State.waiting[i] = new ItemCount(w.id, w.n - k);
            }
            if (moved == 0) return 0;
            State.waiting.RemoveAll(w => w.n <= 0);
            Changed?.Invoke();
            return moved;
        }

        void Wear(int slot)
        {
            if (slot < 0) return;
            var s = State.backpack[slot];
            var d = Content.Item(s.id);
            if (d == null || d.Durability <= 0) return;
            s.wear++;
            if (s.wear >= d.Durability)
            {
                Toast?.Invoke(d.Name + " broke!");
                s.Clear();
                if (State.equipped == slot) { State.equipped = -1; AutoEquip(); }
            }
        }

        // ------------------------------------------------------------------ building

        public List<ItemCount> Remaining(string id)
        {
            var def = Content.Builds[id];
            var b = Build(id);
            var left = new List<ItemCount>();
            foreach (var c in def.Cost)
            {
                int have = 0;
                if (b != null) foreach (var d in b.delivered) if (d.id == c.id) have += d.n;
                if (b != null && b.done) have = c.n;
                if (have < c.n) left.Add(new ItemCount(c.id, c.n - have));
            }
            return left;
        }

        /// <summary>0..1 of the materials delivered.</summary>
        public float BuildProgress(string id)
        {
            if (IsBuilt(id)) return 1;
            int total = 0, left = 0;
            foreach (var c in Content.Builds[id].Cost) total += c.n;
            foreach (var r in Remaining(id)) left += r.n;
            return total == 0 ? 1 : 1f - left / (float)total;
        }

        /// <summary>True when the backpack holds at least one item the blueprint still needs.</summary>
        public bool CanDeliver(string id)
        {
            if (IsBuilt(id)) return false;
            foreach (var r in Remaining(id)) if (Has(r.id)) return true;
            return false;
        }

        /// <summary>Feeds up to <paramref name="max"/> items from the backpack into the blueprint. Returns what went in.</summary>
        public int Deliver(string id, int max)
        {
            if (IsBuilt(id)) return 0;
            var b = Build(id);
            if (b == null) { b = new BuildState { id = id }; State.builds.Add(b); }
            int moved = 0;
            foreach (var r in Remaining(id))
            {
                if (moved >= max) break;
                int k = Inventory.Remove(State.backpack, r.id, Math.Min(r.n, max - moved));
                if (k <= 0) continue;
                moved += k;
                bool found = false;
                for (int i = 0; i < b.delivered.Count; i++)
                    if (b.delivered[i].id == r.id) { b.delivered[i] = new ItemCount(r.id, b.delivered[i].n + k); found = true; }
                if (!found) b.delivered.Add(new ItemCount(r.id, k));
            }
            if (moved > 0 && Remaining(id).Count == 0)
            {
                b.done = true;
                b.delivered.Clear();
                b.stamp = Now;
                Toast?.Invoke(Content.Builds[id].Name + " built!");
                AddXp(20);
                BuildFinished?.Invoke(id);
            }
            if (moved > 0) { Changed?.Invoke(); CheckQuests(); }
            return moved;
        }

        // ------------------------------------------------------------------ crafting

        public JobState Job(string station) { return State.jobs.FirstOrDefault(j => j.station == station); }

        public string CraftBlocked(string recipe) { return CraftBlocked(recipe, 1); }

        public bool Craft(string recipe) { return Craft(recipe, 1); }

        // ------------------------------------------------------------------ using items

        /// <summary>Eats, drinks, bandages or equips the item in a backpack slot.</summary>
        public bool Use(int slot)
        {
            if (slot < 0 || slot >= State.backpack.Count) return false;
            var s = State.backpack[slot];
            if (s.Empty) return false;
            var d = Content.Item(s.id);
            if (d == null) return false;
            if (d.Gear != GearSlot.None) return PutOn(slot);   // clothes are worn, not held (they count as tools)
            if (d.Kind == ItemKind.Weapon || d.Kind == ItemKind.Tool)
            {
                State.equipped = State.equipped == slot ? -1 : slot;
                Changed?.Invoke();
                return true;
            }
            if (d.Food == 0 && d.Drink == 0 && d.Heal == 0) return false;
            State.hunger = Clamp(State.hunger + d.Food);
            State.thirst = Clamp(State.thirst + d.Drink);
            if (d.Heal != 0) State.health = Clamp(State.health + d.Heal);
            s.n--;
            if (s.n <= 0) s.Clear();
            if (State.health <= 0) Die();
            Changed?.Invoke();
            return true;
        }

        void AutoEquip()
        {
            int best = -1, dmg = 0;
            for (int i = 0; i < State.backpack.Count; i++)
            {
                var s = State.backpack[i];
                if (s.Empty) continue;
                var d = Content.Item(s.id);
                if (d != null && d.Kind == ItemKind.Weapon && d.Damage > dmg) { best = i; dmg = d.Damage; }
            }
            State.equipped = best;
        }

        /// <summary>Damage of the held weapon, else the best chopping tool, else fists.</summary>
        public int AttackDamage()
        {
            int slot = WeaponSlot();
            return slot < 0 ? Content.FistDamage : Content.Item(State.backpack[slot].id).Damage;
        }

        public int WeaponSlot()
        {
            if (State.equipped >= 0 && !State.backpack[State.equipped].Empty) return State.equipped;
            return ToolFor(ToolUse.Chop);
        }

        public string WeaponId { get { int s = WeaponSlot(); return s < 0 ? null : State.backpack[s].id; } }

        /// <summary>The player landed a blow: the weapon wears.</summary>
        public void Swung() { Wear(WeaponSlot()); }

        public void Killed(string enemy)
        {
            State.kills++;
            if (enemy == "pirate") State.pirates++;
            if (enemy == "redjack") SetFlag("redjack_dead");
            Tally("kill:" + enemy);
            if (enemy == "pirate" || enemy == "redjack") Tally("kill:raider");
            if (IsNight) Tally("nightkill");
            foreach (var d in Enemies.All[enemy].Drops) Give(d.id, d.n);
            var rare = Enemies.All[enemy].Rare;
            if (rare != null && _rng.NextDouble() < 0.25) Give(rare.Value.id, rare.Value.n);
            AddXp(Enemies.All[enemy].Xp);
            Changed?.Invoke();
            CheckQuests();
        }

        // ------------------------------------------------------------------ damage and death

        public void Damage(float amount, string cause = null)
        {
            if (State.health <= 0) return;
            if (cause != null && cause != "starving") { amount *= ArmorFactor; WearGear(); }
            State.health = Clamp(State.health - amount);
            if (State.health <= 0) Die();
        }

        /// <summary>The backpack stays where the player fell; they wake at the shelter (or the beach) hurt and hungry.</summary>
        void Die()
        {
            State.deaths++;
            var bag = new LostBag { x = State.px, z = State.pz, location = State.location };
            // Pockets stay with you; the backpack proper is left where you fell.
            for (int i = Content.PocketSlots; i < State.backpack.Count; i++)
            {
                var s = State.backpack[i];
                if (!s.Empty) { bag.slots.Add(new Slot { id = s.id, n = s.n, wear = s.wear }); s.Clear(); }
            }
            // Every fall leaves its own bag: one dropped earlier stays where it is.
            if (bag.slots.Count > 0) State.lostBags.Add(bag);
            if (State.equipped >= Content.PocketSlots) State.equipped = -1;
            State.health = 60;
            State.hunger = Math.Max(State.hunger, 50);
            State.thirst = Math.Max(State.thirst, 50);
            Died?.Invoke();
            Changed?.Invoke();
        }

        /// <summary>The bag dropped last (null when none lies anywhere).</summary>
        public LostBag NewestBag { get { return State.lostBags.Count > 0 ? State.lostBags[State.lostBags.Count - 1] : null; } }

        /// <summary>Picks a lost bag back up (what fits); the newest one if none is named.</summary>
        public int TakeBag(LostBag bag = null)
        {
            if (bag == null) bag = NewestBag;
            if (bag == null || !State.lostBags.Contains(bag)) return 0;
            int n = 0;
            foreach (var s in bag.slots)
            {
                if (s.Empty) continue;
                int k = Inventory.Add(State.backpack, s.id, s.n, s.wear);
                if (k > 0) Gained?.Invoke(s.id, k);
                s.n -= k; n += k;
                if (s.n <= 0) s.Clear();
            }
            if (bag.slots.All(s => s.Empty)) State.lostBags.Remove(bag);
            else Toast?.Invoke("Backpack full");
            if (State.equipped < 0) AutoEquip();
            Changed?.Invoke();
            CheckQuests();
            return n;
        }

        // ------------------------------------------------------------------ chest and shelter

        /// <summary>Puts every resource (not tools, weapons, food or medicine) into the sea chest.</summary>
        public int StoreResources()
        {
            if (!IsBuilt("chest")) return 0;
            int moved = 0;
            foreach (var s in State.backpack)
            {
                if (s.Empty) continue;
                var d = Content.Item(s.id);
                if (d == null || d.Kind != ItemKind.Resource) continue;
                int k = Inventory.Add(State.chest, s.id, s.n);
                s.n -= k; moved += k;
                if (s.n <= 0) s.Clear();
            }
            if (moved > 0) Changed?.Invoke();
            return moved;
        }

        /// <summary>Moves one chest slot into the backpack.</summary>
        public int TakeFromChest(int slot)
        {
            if (slot < 0 || slot >= State.chest.Count) return 0;
            var s = State.chest[slot];
            if (s.Empty) return 0;
            int k = Inventory.Add(State.backpack, s.id, s.n, s.wear);
            s.n -= k;
            if (s.n <= 0) s.Clear();
            if (k > 0) { Changed?.Invoke(); CheckQuests(); }
            return k;
        }

        public int StoreSlot(int slot)
        {
            if (!IsBuilt("chest") || slot < 0 || slot >= State.backpack.Count) return 0;
            var s = State.backpack[slot];
            if (s.Empty) return 0;
            int k = Inventory.Add(State.chest, s.id, s.n, s.wear);
            s.n -= k;
            if (s.n <= 0) { s.Clear(); if (State.equipped == slot) State.equipped = -1; }
            if (k > 0) Changed?.Invoke();
            return k;
        }

        /// <summary>Resting in the shelter heals quickly.</summary>
        public void Rest(float dt) { State.health = Clamp(State.health + 6f * dt); }

        /// <summary>Takes the flasks the rain catcher has collected.</summary>
        public int CollectRain()
        {
            var b = Build("raincatcher");
            if (b == null || !b.done || b.stored <= 0) return 0;
            int k = Give("water", b.stored);
            b.stored -= k;
            if (k > 0) Changed?.Invoke();
            return k;
        }

        // ------------------------------------------------------------------ quests

        float _questCheck;

        public void CheckQuests()
        {
            int guard = 0;
            while (State.quest < Quests.All.Count - 1 && guard++ < 32)
            {
                var q = Quests.All[State.quest];
                if (!q.Done(this)) break;
                State.quest++;
                AddXp(15);
                QuestDone?.Invoke(q);
            }
        }
    }
}
