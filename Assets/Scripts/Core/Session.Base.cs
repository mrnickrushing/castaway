using System;
using System.Collections.Generic;
using System.Linq;

namespace LastTide.Core
{
    /// <summary>A piece the castaway built on the camp grid. Edge pieces sit on the north (rot 0) or east (rot 1) edge
    /// of their cell; objects face rot × 90°.</summary>
    [Serializable]
    public sealed class PieceState
    {
        public string id;
        public int i, j, rot;
        public List<Slot> slots = new List<Slot>();
    }

    /// <summary>
    /// The base: clothes and armour, pockets that survive passing out, splitting and sorting, station levels and
    /// upgrades, crafting several at once from the blueprints, free building on the camp grid, storage boxes, and the
    /// Survivor's Path rewards (a free track and the Ultra track).
    /// </summary>
    public sealed partial class Session
    {
        // ------------------------------------------------------------------ clothes

        public Slot GearIn(GearSlot g) { return State.gear[(int)g - 1]; }

        public int Armor
        {
            get { int a = 0; foreach (var s in State.gear) if (!s.Empty) a += Content.Item(s.id).Armor; return a; }
        }

        /// <summary>Share of a blow that gets through the clothes (40 armour halves it).</summary>
        public float ArmorFactor { get { return 40f / (40f + Armor); } }

        /// <summary>Puts on (or swaps in) the clothing in a backpack slot.</summary>
        public bool PutOn(int slot)
        {
            var s = State.backpack[slot];
            var d = s.Empty ? null : Content.Item(s.id);
            if (d == null || d.Gear == GearSlot.None) return false;
            var g = GearIn(d.Gear);
            var old = new Slot { id = g.id, n = g.n, wear = g.wear };
            g.id = s.id; g.n = 1; g.wear = s.wear;
            s.Clear();
            if (State.equipped == slot) State.equipped = -1;   // whatever lands in this slot next isn't held
            if (!old.Empty) { s.id = old.id; s.n = 1; s.wear = old.wear; }
            Changed?.Invoke();
            return true;
        }

        public bool TakeOff(GearSlot g)
        {
            var s = GearIn(g);
            if (s.Empty) return false;
            if (Inventory.Add(State.backpack, s.id, 1, s.wear) == 0) { Toast?.Invoke("Backpack full"); return false; }
            s.Clear();
            Changed?.Invoke();
            return true;
        }

        /// <summary>A blow landed: each piece of clothing wears a little.</summary>
        void WearGear()
        {
            foreach (var s in State.gear)
            {
                if (s.Empty) continue;
                var d = Content.Item(s.id);
                if (++s.wear >= d.Durability) { Toast?.Invoke(d.Name + " fell apart!"); s.Clear(); }
            }
        }

        // ------------------------------------------------------------------ backpack tools

        public bool IsPocket(int slot) { return slot < Content.PocketSlots; }

        /// <summary>Halves a stack into the first empty slot.</summary>
        public bool Split(int slot)
        {
            var s = State.backpack[slot];
            if (s.Empty || s.n < 2) return false;
            var empty = State.backpack.FirstOrDefault(x => x.Empty);
            if (empty == null) { Toast?.Invoke("No room to split"); return false; }
            int half = s.n / 2;
            s.n -= half;
            empty.id = s.id; empty.n = half;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Tops up stacks and orders the backpack (pockets stay as they are): tools, food, then materials.</summary>
        public void Sort()
        {
            var bag = State.backpack.Skip(Content.PocketSlots).ToList();
            var items = bag.Where(s => !s.Empty).Select(s => new Slot { id = s.id, n = s.n, wear = s.wear }).ToList();
            foreach (var s in bag) s.Clear();
            int Rank(Slot s) { var d = Content.Item(s.id); return d == null ? 9 : d.Gear != GearSlot.None ? 0 : d.Kind == ItemKind.Weapon ? 1 : d.Kind == ItemKind.Tool ? 2 : d.Kind == ItemKind.Medical ? 3 : d.Kind == ItemKind.Food || d.Kind == ItemKind.Drink ? 4 : 5; }
            foreach (var s in items.OrderBy(Rank).ThenBy(s => s.id).ThenByDescending(s => s.n))
            {
                var stack = Content.Item(s.id)?.Stack ?? 20;
                var into = bag.FirstOrDefault(b => !b.Empty && b.id == s.id && b.n < stack && stack > 1);
                if (into != null) { int k = Math.Min(s.n, stack - into.n); into.n += k; s.n -= k; }
                if (s.n > 0) { var e = bag.First(b => b.Empty); e.id = s.id; e.n = s.n; e.wear = s.wear; }
            }
            State.equipped = -1;
            AutoEquip();
            Changed?.Invoke();
        }

        /// <summary>Empty slots, pockets included: room for that many new kinds of thing.</summary>
        public int FreeSlots { get { return State.backpack.Count(s => s.Empty); } }

        /// <summary>Moves a stack from the backpack into the pockets (kept when the castaway passes out), or back.
        /// It goes to the first empty slot on the other side; the held item stays held. Returns where it went, or -1.</summary>
        public int MoveSlot(int slot)
        {
            var s = State.backpack[slot];
            if (s.Empty) return -1;
            bool toPocket = !IsPocket(slot);
            int from = toPocket ? 0 : Content.PocketSlots, to = toPocket ? Content.PocketSlots : State.backpack.Count;
            int into = -1;
            for (int i = from; i < to; i++) if (State.backpack[i].Empty) { into = i; break; }
            if (into < 0) { Toast?.Invoke(toPocket ? "Pockets full: move something to the backpack first" : "No room in the backpack"); return -1; }
            var e = State.backpack[into];
            e.id = s.id; e.n = s.n; e.wear = s.wear;
            s.Clear();
            if (State.equipped == slot) State.equipped = into;
            Changed?.Invoke();
            return into;
        }

        /// <summary>One tap to free the backpack: tops up stacks, then (at home) stores every material that isn't in
        /// <paramref name="keep"/> (what the current goal needs) in the chest and boxes. Pockets, tools, weapons,
        /// clothes, food and medicine stay. Returns how many items were stored.</summary>
        public int MakeRoom(ICollection<string> keep = null)
        {
            Sort();
            int moved = 0;
            if (AtHome)
            {
                foreach (var store in Storages)
                    for (int i = Content.PocketSlots; i < State.backpack.Count; i++)
                    {
                        var s = State.backpack[i];
                        if (s.Empty || i == State.equipped || Content.Item(s.id)?.Kind != ItemKind.Resource) continue;
                        if (keep != null && keep.Contains(s.id)) continue;
                        int k = Inventory.Add(store, s.id, s.n, s.wear);
                        s.n -= k; moved += k;
                        if (s.n <= 0) s.Clear();
                    }
                if (moved > 0) Sort();
            }
            Changed?.Invoke();
            return moved;
        }

        public bool Discard(int slot)
        {
            var s = State.backpack[slot];
            if (s.Empty) return false;
            s.Clear();
            if (State.equipped == slot) State.equipped = -1;
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ storages and paying

        /// <summary>Every storage at home: the sea chest and the boxes built on the grid.</summary>
        public IEnumerable<List<Slot>> Storages
        {
            get
            {
                if (IsBuilt("chest")) yield return State.chest;
                foreach (var p in State.pieces) if (p.slots.Count > 0) yield return p.slots;
            }
        }

        /// <summary>How many of an item the castaway has, in the backpack and (at home) every storage.</summary>
        public int CountAll(string id)
        {
            int n = Count(id);
            if (AtHome) foreach (var s in Storages) n += Inventory.Count(s, id);
            return n;
        }

        public bool CanPay(ItemCount[] cost) { return cost.All(c => CountAll(c.id) >= c.n); }

        /// <summary>Takes a cost from the backpack first, then the storages.</summary>
        bool Pay(ItemCount[] cost)
        {
            if (!CanPay(cost)) return false;
            foreach (var c in cost)
            {
                int left = c.n - Inventory.Remove(State.backpack, c.id, c.n);
                if (AtHome) foreach (var s in Storages) { if (left <= 0) break; left -= Inventory.Remove(s, c.id, left); }
            }
            return true;
        }

        public int StoreSlotTo(List<Slot> target, int slot)
        {
            var s = State.backpack[slot];
            if (s.Empty) return 0;
            int k = Inventory.Add(target, s.id, s.n, s.wear);
            s.n -= k;
            if (s.n <= 0) { s.Clear(); if (State.equipped == slot) State.equipped = -1; }
            if (k > 0) Changed?.Invoke();
            return k;
        }

        public int TakeFrom(List<Slot> source, int slot)
        {
            var s = source[slot];
            if (s.Empty) return 0;
            int k = Inventory.Add(State.backpack, s.id, s.n, s.wear);
            s.n -= k;
            if (s.n <= 0) s.Clear();
            if (k > 0) { Changed?.Invoke(); CheckQuests(); }
            return k;
        }

        public int StoreResourcesTo(List<Slot> target)
        {
            int moved = 0;
            for (int i = Content.PocketSlots; i < State.backpack.Count; i++)
            {
                var s = State.backpack[i];
                if (s.Empty || Content.Item(s.id)?.Kind != ItemKind.Resource) continue;
                int k = Inventory.Add(target, s.id, s.n);
                s.n -= k; moved += k;
                if (s.n <= 0) s.Clear();
            }
            if (moved > 0) Changed?.Invoke();
            return moved;
        }

        // ------------------------------------------------------------------ stations

        /// <summary>Level of a camp structure (0 = not built).</summary>
        public int Level(string station)
        {
            if (station == "hand") return 1;
            var b = Build(station);
            if (b != null && b.done) return Math.Max(1, b.level);
            return State.pieces.Any(p => Content.Pieces[p.id].Station == station) ? 1 : 0;
        }

        public ItemCount[] UpgradeCost(string id)
        {
            var d = Content.Builds[id];
            int lv = Level(id);
            return lv >= 1 && lv - 1 < d.Upgrades.Length ? d.Upgrades[lv - 1] : null;
        }

        public bool Upgrade(string id)
        {
            var cost = UpgradeCost(id);
            if (cost == null) return false;
            var short_ = Short(cost, 1, true);
            if (short_ != null || !Pay(cost)) { Toast?.Invoke(short_ ?? "Missing materials"); return false; }
            var b = Build(id);
            b.level = Math.Max(1, b.level) + 1;
            Toast?.Invoke(Content.Builds[id].Name + " upgraded to level " + b.level + "!");
            AddXp(30);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Time multiplier at a station (an upgraded campfire cooks twice as fast).</summary>
        float StationSpeed(string station) { return station == "campfire" && Level("campfire") >= 2 ? 2f : 1f; }

        public string CraftBlocked(string recipe, int qty)
        {
            var r = Content.Recipes[recipe];
            var perk = PerkBlocked(recipe);
            if (perk != null) return perk;
            if (!Knows(recipe))
            {
                var t = World.Signals.FirstOrDefault(sg => sg.Unlocks == recipe);
                var at = t != null ? World.Location(t.Location) : null;
                return t != null ? "Rescue " + t.Npc + (at != null ? " at " + at.Name : "") + " to learn this" : "A survivor must teach you this";
            }
            int lv = Level(r.Station);
            if (lv == 0) return r.Station == "furnace" ? "Build a furnace first" : "Build the " + Content.Builds[r.Station].Name.ToLower() + " first";
            if (lv < r.MinLevel) return "Needs " + StationName(r.Station) + " level " + r.MinLevel;
            if (Job(r.Station) != null) return r.Station == "hand" ? "Your hands are busy: wait for this one" : "The " + StationName(r.Station).ToLower() + " is busy";
            return Short(r.Inputs, qty);
        }

        /// <summary>"Need 1 Stone, 2 Plant Fiber": what the backpack is short of to pay <paramref name="cost"/>
        /// <paramref name="times"/> over, or null when it can pay. Crafting pays from the backpack only; building and
        /// upgrades also from the camp's storage (<paramref name="storages"/>). Long maths: a huge quantity must not wrap.</summary>
        public string Short(ItemCount[] cost, long times = 1, bool storages = false)
        {
            var miss = new List<string>();
            foreach (var c in cost)
            {
                long need = c.n * times - (storages ? CountAll(c.id) : Count(c.id));
                if (need > 0) miss.Add((need > 9999 ? "lots of" : need.ToString()) + " " + Content.NameOf(c.id));
            }
            return miss.Count == 0 ? null : "Need " + string.Join(", ", miss);
        }

        public static string StationName(string station)
        {
            if (station == "hand") return "By hand";
            if (Content.Builds.ContainsKey(station)) return Content.Builds[station].Name;
            var p = Content.Pieces.Values.FirstOrDefault(x => x.Station == station);
            return p != null ? p.Name : station;
        }

        /// <summary>Most of a recipe the backpack can pay for (at least 1 for the slider).</summary>
        public int MaxCraft(string recipe)
        {
            var r = Content.Recipes[recipe];
            int n = 99;
            foreach (var c in r.Inputs) n = Math.Min(n, Count(c.id) / c.n);
            return Math.Max(1, Math.Min(n, Content.IsPerk(recipe) ? 1 : 20));  // an improvement is built once
        }

        public bool Craft(string recipe, int qty)
        {
            if (qty < 1 || CraftBlocked(recipe, qty) != null || (Content.IsPerk(recipe) && qty > 1)) return false;
            var r = Content.Recipes[recipe];
            foreach (var c in r.Inputs) Inventory.Remove(State.backpack, c.id, c.n * qty);
            State.jobs.Add(new JobState { station = r.Station, recipe = recipe, doneAt = Now + r.Seconds * qty / StationSpeed(r.Station), count = r.Count * qty });
            AddXp(4 * qty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Doubloons to finish a station's job now: one per half minute left.</summary>
        public int RushCost(string station)
        {
            var j = Job(station);
            return j == null ? 0 : Math.Max(1, (int)Math.Ceiling((j.doneAt - Now) / 30.0));
        }

        // ------------------------------------------------------------------ the camp grid

        public static bool EdgeKind(string id) { return Content.Pieces[id].Kind == PieceKind.Edge; }

        /// <summary>Why a piece can't go here, or null.</summary>
        public string PlaceBlocked(string id, int i, int j, int rot)
        {
            var d = Content.Pieces[id];
            if (!AtHome) return "Only at your camp";
            if (State.level < d.Level) return "Needs level " + d.Level;
            if (Math.Abs(i) > Content.GridHalf || Math.Abs(j) > Content.GridHalf) return "Too far from camp";
            foreach (var p in State.pieces)
            {
                var pd = Content.Pieces[p.id];
                if (pd.Kind != d.Kind || p.i != i || p.j != j) continue;
                if (d.Kind == PieceKind.Edge && p.rot != (rot & 1)) continue;
                return "Something is already there";
            }
            if (!CanPay(d.Cost)) return Short(d.Cost, 1, true) ?? "Missing materials";
            return null;
        }

        public PieceState Place(string id, int i, int j, int rot)
        {
            if (PlaceBlocked(id, i, j, rot) != null) return null;
            var d = Content.Pieces[id];
            Pay(d.Cost);
            var p = new PieceState { id = id, i = i, j = j, rot = d.Kind == PieceKind.Edge ? rot & 1 : rot & 3 };
            for (int k = 0; k < d.Slots; k++) p.slots.Add(new Slot());
            State.pieces.Add(p);
            AddXp(3);
            SetFlag("built_piece");
            Changed?.Invoke();
            CheckQuests();
            return p;
        }

        /// <summary>Takes a piece down: half its materials back, and whatever was stored in it.</summary>
        public bool Demolish(PieceState p)
        {
            if (!State.pieces.Contains(p)) return false;
            // What's stored in it comes out first and must all fit, or the box stays up (it would be lost with it).
            var room = State.backpack.Select(s => new Slot { id = s.id, n = s.n, wear = s.wear }).ToList();
            foreach (var s in p.slots)
                if (!s.Empty && Inventory.Add(room, s.id, s.n, s.wear) < s.n) { Toast?.Invoke("Backpack full: empty the " + Content.Pieces[p.id].Name.ToLower() + " first"); return false; }
            foreach (var s in p.slots) if (!s.Empty) Inventory.Add(State.backpack, s.id, s.n, s.wear);
            foreach (var c in Content.Pieces[p.id].Cost) Inventory.Add(State.backpack, c.id, Math.Max(1, c.n / 2));
            State.pieces.Remove(p);
            Changed?.Invoke();
            return true;
        }

        public PieceState Bed { get { return State.pieces.FirstOrDefault(p => p.id == "bed"); } }

        // ------------------------------------------------------------------ Survivor's Path

        public bool Ultra { get { return Owns("lasttide.ultra"); } }

        public bool Completed(int questIndex) { return questIndex < State.quest; }
        public bool Claimed(Quest q) { return Flag("claim_" + q.Id); }
        public bool ClaimedUltra(Quest q) { return Flag("claimu_" + q.Id); }

        /// <summary>Rewards waiting to be taken (free ones, plus Ultra ones if the pass is owned).</summary>
        public int Claimable
        {
            get
            {
                int n = 0;
                for (int i = 0; i < State.quest && i < Quests.All.Count; i++)
                {
                    var q = Quests.All[i];
                    if (!Claimed(q)) n++;
                    if (Ultra && !ClaimedUltra(q)) n++;
                }
                return n;
            }
        }

        /// <summary>Takes a finished step's rewards. One that doesn't fit in the backpack stays here to claim later.</summary>
        public bool Claim(int questIndex)
        {
            if (!Completed(questIndex)) return false;
            var q = Quests.All[questIndex];
            bool any = false;
            if (!Claimed(q))
            {
                if (Fits(q.Reward)) { foreach (var r in q.Reward) Give(r.id, r.n); SetFlag("claim_" + q.Id); any = true; }
                else NoRoom();
            }
            if (Ultra && !ClaimedUltra(q))
            {
                if (Fits(q.Premium)) { foreach (var r in q.Premium) Give(r.id, r.n); SetFlag("claimu_" + q.Id); any = true; }
                else NoRoom();
            }
            if (any) Changed?.Invoke();
            return any;
        }

        public int ClaimAll()
        {
            int n = 0;
            for (int i = 0; i < State.quest && i < Quests.All.Count; i++) if (Claim(i)) n++;
            return n;
        }
    }
}
