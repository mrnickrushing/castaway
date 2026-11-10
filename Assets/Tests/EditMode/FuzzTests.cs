using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LastTide.Core;
using NUnit.Framework;

namespace LastTide.Tests
{
    /// <summary>
    /// Randomised play from a new game: thousands of calls into the Session with plausible (and some invalid)
    /// arguments, checking after each that the state still makes sense, that moving things around never makes or
    /// loses items, that rewards pay once, and that the save comes back the same through Unity's JSON rules.
    /// Seeds are fixed so a failure names the seed and step that reproduce it.
    /// </summary>
    public class FuzzTests
    {
        double _now;

        Session Make(GameState st, int seed)
        {
            var g = new Session(st);
            g.RealNow = () => _now;
            // Session rolls loot with its own Random; seed it so a run is the same every time.
            typeof(Session).GetField("_rng", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(g, new Random(seed));
            return g;
        }

        [TearDown]
        public void ResetZone() { Session.UtcOffsetSeconds = 0; }

        // ------------------------------------------------------------------ the random walk

        [Test]
        public void RandomPlayKeepsTheStateSound([Values(1, 2, 3, 4, 5, 6)] int seed)
        {
            Session.UtcOffsetSeconds = 0;
            _now = (Season.EpochDay + 3) * 86400.0 + 9 * 3600;
            var rng = new Random(seed);
            var g = Make(GameState.New(), seed);
            g.Give("hatchet", 1);   // the first step: lets the fuzz reach chopping early
            var ops = Ops();
            int total = ops.Sum(o => o.w);
            var recent = new Queue<string>();   // the last few steps, for the failure message
            for (int step = 0; step < 3000; step++)
            {
                int pick = rng.Next(total), i = 0;
                while (pick >= ops[i].w) pick -= ops[i++].w;
                string ctx = "seed " + seed + " step " + step + " " + ops[i].name;
                recent.Enqueue(ctx + " [held " + g.State.equipped + "]");
                if (recent.Count > 8) recent.Dequeue();
                try { ops[i].run(this, g, rng, ctx); Check(g, ctx); }
                catch (AssertionException) { TestContext.WriteLine("last steps:\n  " + string.Join("\n  ", recent)); throw; }
                catch (Exception e) { Assert.Fail(ctx + " threw " + e); }
                if (step % 20 == 19)
                {
                    var loaded = RoundTrip(g, seed + step);
                    var diff = Diff(g.State, loaded.State, typeof(GameState), "state");
                    Assert.IsNull(diff, ctx + ": save does not round-trip: " + diff);
                    Check(loaded, ctx + " (after load)");
                    if (rng.Next(3) == 0) g = loaded;   // the app was closed and opened again
                }
            }
        }

        struct Op { public string name; public int w; public Action<FuzzTests, Session, Random, string> run; }

        static Op O(string name, int w, Action<FuzzTests, Session, Random, string> run) { return new Op { name = name, w = w, run = run }; }

        static readonly string[] ItemIds = Content.Items.Keys.ToArray();
        static readonly string[] NodeKinds = Content.Nodes.Keys.ToArray();
        static readonly string[] RecipeIds = Content.Recipes.Keys.ToArray();
        static readonly string[] BuildIds = Content.Builds.Keys.ToArray();
        static readonly string[] PieceIds = Content.Pieces.Keys.ToArray();
        static readonly string[] Enemies = LastTide.Core.Enemies.All.Keys.ToArray();
        static readonly string[] Stations = { "hand", "workbench", "campfire", "furnace" };

        static T Pick<T>(Random r, IList<T> xs) { return xs[r.Next(xs.Count)]; }
        static int AnySlot(Session g, Random r) { return r.Next(g.State.backpack.Count); }
        static int OddSlot(Session g, Random r) { return r.Next(4) == 0 ? Pick(r, new[] { -1, -7, g.State.backpack.Count, 999 }) : AnySlot(g, r); }

        static List<Op> Ops()
        {
            return new List<Op>
            {
                // Sources (things the world hands over).
                O("harvest", 30, (t, g, r, c) =>
                {
                    var kind = Pick(r, NodeKinds);
                    var key = Session.ZonePrefix(g.State.location) + kind + "#" + r.Next(4);
                    g.Harvest(key, kind);
                }),
                O("give", 10, (t, g, r, c) => g.Give(Pick(r, ItemIds), 1 + r.Next(r.Next(4) == 0 ? 60 : 6))),
                O("coins", 3, (t, g, r, c) => g.Give("doubloon", r.Next(80))),
                O("killed", 6, (t, g, r, c) => g.Killed(Pick(r, Enemies))),
                O("swung", 4, (t, g, r, c) => g.Swung()),
                O("xp", 4, (t, g, r, c) => g.AddXp(r.Next(4) == 0 ? -5 : r.Next(400))),
                O("loot", 5, (t, g, r, c) =>
                {
                    var key = Session.ZonePrefix(g.State.location) + "lootbag#" + r.Next(3);
                    g.TakeLoot(key, "lootbag", r.Next(10) - 1);
                }),
                O("rain", 2, (t, g, r, c) => g.CollectRain()),
                O("tune/rescue", 3, (t, g, r, c) => { var s = Pick(r, World.Signals); if (r.Next(2) == 0) g.Tune(s); else g.Rescue(s); }),
                O("pet", 2, (t, g, r, c) => { if (r.Next(2) == 0) g.AdoptPet(); else g.ChoosePet(Pick(r, Season.Pets).Id); }),

                // Building and crafting.
                O("craft", 12, (t, g, r, c) =>
                {
                    var rec = Pick(r, RecipeIds);
                    int qty = Pick(r, new[] { -1, 0, 1, 1, 2, 3, g.MaxCraft(rec), 1000, int.MaxValue / 2 });
                    t.Conserving(g, c, () => g.Craft(rec, qty), allowLoss: true);
                }),
                O("deliver", 12, (t, g, r, c) => t.Conserving(g, c, () => g.Deliver(Pick(r, BuildIds), Pick(r, new[] { -3, 0, 1, 4, 99 })) > 0, allowLoss: true)),
                O("upgrade", 3, (t, g, r, c) => g.Upgrade(Pick(r, new[] { "campfire", "workbench" }))),
                O("place", 12, (t, g, r, c) => g.Place(r.Next(3) == 0 ? "small_box" : Pick(r, PieceIds), r.Next(23) - 11, r.Next(23) - 11, r.Next(8) - 2)),
                O("demolish", 2, (t, g, r, c) =>
                {
                    var p = g.State.pieces.Count > 0 && r.Next(5) > 0 ? Pick(r, g.State.pieces) : new PieceState { id = "wall" };
                    var stored = new Dictionary<string, int>();
                    foreach (var s in p.slots) if (!s.Empty) stored[s.id] = (stored.TryGetValue(s.id, out var k) ? k : 0) + s.n;
                    var before = Totals(g);
                    bool ok = g.Demolish(p);
                    var after = Totals(g);
                    if (ok)
                        foreach (var kv in stored)
                            Assert.GreaterOrEqual(Get(after, kv.Key), Get(before, kv.Key), c + ": taking a box down lost the " + kv.Key + " stored in it");
                    else AssertSame(before, after, c);
                }),
                O("rush craft", 3, (t, g, r, c) =>
                {
                    var st = Pick(r, Stations);
                    int coins = g.State.coins, cost = g.RushCost(st);
                    bool ok = g.RushCraft(st);
                    Assert.AreEqual(ok ? coins - cost : coins, g.State.coins, c + ": a rush costs what the SKIP button says");
                }),

                // Moving things around: never makes or loses anything.
                O("split", 5, (t, g, r, c) => { int s = AnySlot(g, r); t.Conserving(g, c, () => g.Split(s)); }),
                O("sort", 4, (t, g, r, c) => t.Conserving(g, c, () => { g.Sort(); return true; })),
                O("put on", 4, (t, g, r, c) => { int s = AnySlot(g, r); t.Conserving(g, c, () => g.PutOn(s)); }),
                O("take off", 3, (t, g, r, c) => t.Conserving(g, c, () => g.TakeOff((GearSlot)(1 + r.Next(4))))),
                O("store", 4, (t, g, r, c) => { int s = OddSlot(g, r); t.Conserving(g, c, () => g.StoreSlot(s) > 0); }),
                O("store resources", 2, (t, g, r, c) => t.Conserving(g, c, () => g.StoreResources() > 0)),
                O("take from chest", 4, (t, g, r, c) => { int s = r.Next(Content.ChestSlots + 4) - 2; t.Conserving(g, c, () => g.TakeFromChest(s) > 0); }),
                O("box", 10, (t, g, r, c) =>
                {
                    var boxes = g.Storages.ToList();
                    if (r.Next(2) == 0 && boxes.Count > 1) boxes.RemoveAt(0);   // the boxes on the grid more often than the chest
                    if (boxes.Count == 0) return;
                    var box = Pick(r, boxes);
                    int which = r.Next(3);
                    int s = which == 1 ? r.Next(box.Count) : AnySlot(g, r);
                    t.Conserving(g, c, () => which == 0 ? g.StoreSlotTo(box, s) > 0 : which == 1 ? g.TakeFrom(box, s) > 0 : g.StoreResourcesTo(box) > 0);
                }),
                O("take bag", 3, (t, g, r, c) => t.Conserving(g, c, () => g.TakeBag() > 0)),
                O("recover bag", 2, (t, g, r, c) =>
                {
                    int coins = g.State.coins;
                    bool had = g.NewestBag != null;
                    var before = Totals(g);
                    bool ok = g.RecoverBag();
                    AssertSame(before, Totals(g), c);
                    if (ok) Assert.AreEqual(coins - Session.RecoverBagCost, g.State.coins, c);
                    else Assert.AreEqual(coins, g.State.coins, c);
                    if (ok) Assert.IsTrue(had, c);
                }),

                // Using things up.
                O("use", 10, (t, g, r, c) => g.Use(OddSlot(g, r))),
                O("discard", 2, (t, g, r, c) =>
                {
                    int s = AnySlot(g, r);
                    var sl = g.State.backpack[s];
                    string id = sl.Empty ? null : sl.id; int n = sl.Empty ? 0 : sl.n;
                    var before = Totals(g);
                    g.Discard(s);
                    if (id != null) before[id] -= n;
                    AssertSame(before, Totals(g), c);
                }),
                O("open crate", 3, (t, g, r, c) => g.OpenCrate(OddSlot(g, r))),
                O("damage", 3, (t, g, r, c) => g.Damage(Pick(r, new[] { 1f, 8f, 30f, 150f }), Pick(r, new[] { null, "boar", "starving", "pirate" }))),
                O("rest", 2, (t, g, r, c) => g.Rest(r.Next(20))),

                // Time.
                O("tick", 25, (t, g, r, c) => g.Tick(Pick(r, new[] { -1f, 0f, 0.016f, 0.5f, 1f, 5f, 30f, 400f, 5000f }))),
                O("real time", 6, (t, g, r, c) => { t._now += Pick(r, new[] { 1.0, 30, 600, 3600, 6 * 3600, 30 * 3600 }); g.UpdateEnergy(); }),

                // The map.
                O("travel", 5, (t, g, r, c) =>
                {
                    var to = r.Next(2) == 0 ? World.Location(World.HomeId) : Pick(r, World.Locations);   // camp is where the boxes are
                    bool run = r.Next(2) == 0;
                    int cost = run ? g.RunCost(to) : 0;
                    g.UpdateEnergy();
                    float topped = g.State.energy;
                    if (g.Travel(to, run)) Assert.AreEqual(topped - cost, g.State.energy, 0.001, c);
                }),
                O("arrive", 6, (t, g, r, c) => { if (g.State.travel != null && r.Next(2) == 0) t._now = g.State.travel.arriveAt + 1; g.Arrive(); }),
                O("rush travel", 2, (t, g, r, c) =>
                {
                    int coins = g.State.coins, cost = g.RushTravelCost;
                    bool ok = g.RushTravel();
                    Assert.AreEqual(ok ? coins - cost : coins, g.State.coins, c);
                }),
                O("refill", 2, (t, g, r, c) => g.RefillEnergy()),

                // Rewards: each pays once.
                O("claim path", 4, (t, g, r, c) =>
                {
                    if (g.State.quest > 0 && r.Next(2) == 0) g.Claim(r.Next(g.State.quest));
                    g.ClaimAll();
                    t.PaysOnce(g, c, () => g.ClaimAll());
                }),
                O("claim levels", 3, (t, g, r, c) => { g.ClaimLevelRewards(); t.PaysOnce(g, c, () => g.ClaimLevelRewards()); }),
                O("claim season", 4, (t, g, r, c) =>
                {
                    if (r.Next(2) == 0) g.ClaimSeason(r.Next(Season.Levels + 3) - 1, r.Next(2) == 0);
                    g.ClaimAllSeason();
                    t.PaysOnce(g, c, () => g.ClaimAllSeason());
                }),
                O("season take", 2, (t, g, r, c) => g.TakeSeasonBonus()),
                O("claim event", 3, (t, g, r, c) =>
                {
                    var runs = g.EventRuns(g.Today - 20, g.Today + 1);
                    if (runs.Count == 0) return;
                    var run = Pick(r, runs);
                    g.ClaimEvent(run);
                    t.PaysOnce(g, c, () => g.ClaimEvent(run));
                }),
                O("tally", 3, (t, g, r, c) => g.Tally(Pick(r, Season.Events).Stat, r.Next(30))),
                O("buy", 3, (t, g, r, c) =>
                {
                    var o = Pick(r, World.Offers);
                    double left = g.OffersLeft;
                    int coins = g.State.coins;
                    bool ok = g.Buy(o);
                    Assert.AreEqual(ok ? coins - o.Price : coins, g.State.coins, c);
                    Assert.LessOrEqual(g.Bought(o), o.Limit, c);
                }),
                O("purchase", 3, (t, g, r, c) =>
                {
                    var p = Pick(r, Catalog.Products);
                    string tx = "tx" + r.Next(12);
                    int coins = g.State.coins;
                    if (r.Next(3) == 0) g.BeginPurchase(p.Id);
                    bool ok = g.GrantPurchase(p.Id, tx);
                    Assert.AreEqual(ok ? coins + p.Coins : coins, g.State.coins, c);
                    t.PaysOnce(g, c, () => g.GrantPurchase(p.Id, tx));
                }),
                O("reconcile", 2, (t, g, r, c) =>
                {
                    var p = Pick(r, Catalog.Products);
                    g.BeginPurchase(p.Id);
                    var receipts = new[] { new StoreReceipt(p.Id, "rx" + r.Next(6), t._now + 1), new StoreReceipt(p.Id, "rx" + r.Next(6), t._now - 500) };
                    g.ReconcilePurchases(receipts);
                    t.PaysOnce(g, c, () => g.ReconcilePurchases(receipts));
                }),
                O("restore", 1, (t, g, r, c) =>
                {
                    var ids = Catalog.Products.Where(x => r.Next(2) == 0).Select(x => x.Id).ToList();
                    g.RestoreNonConsumables(ids);
                    t.PaysOnce(g, c, () => g.RestoreNonConsumables(ids));
                }),
            };
        }

        // ------------------------------------------------------------------ conservation

        /// <summary>Every item the castaway owns: backpack, clothes, the chest, the boxes, the lost bags and what waits for room.</summary>
        static Dictionary<string, int> Totals(Session g)
        {
            var d = new Dictionary<string, int>();
            void Add(List<Slot> slots) { if (slots == null) return; foreach (var s in slots) if (!s.Empty) d[s.id] = Get(d, s.id) + s.n; }
            Add(g.State.backpack); Add(g.State.gear); Add(g.State.chest);
            foreach (var p in g.State.pieces) Add(p.slots);
            foreach (var b in g.State.lostBags) Add(b.slots);
            foreach (var w in g.State.waiting) d[w.id] = Get(d, w.id) + w.n;
            return d;
        }

        static int Get(Dictionary<string, int> d, string id) { return d.TryGetValue(id, out var n) ? n : 0; }

        static void AssertSame(Dictionary<string, int> a, Dictionary<string, int> b, string ctx)
        {
            foreach (var id in a.Keys.Union(b.Keys))
                Assert.AreEqual(Get(a, id), Get(b, id), ctx + ": " + id + " count changed");
        }

        /// <summary>Runs an action that only moves things: the totals stay the same (or only fall, when it spends).</summary>
        void Conserving(Session g, string ctx, Func<bool> act, bool allowLoss = false)
        {
            var before = Totals(g);
            int coins = g.State.coins;
            act();
            var after = Totals(g);
            Assert.AreEqual(coins, g.State.coins, ctx + ": doubloons changed");
            if (!allowLoss) { AssertSame(before, after, ctx); return; }
            foreach (var id in after.Keys) Assert.LessOrEqual(after[id], Get(before, id), ctx + ": " + id + " appeared from nowhere");
        }

        /// <summary>A second claim straight after the first gives nothing.</summary>
        void PaysOnce(Session g, string ctx, Action again)
        {
            var before = Totals(g);
            int coins = g.State.coins, pts = g.State.seasonPts;
            float energy = g.State.energy;
            again();
            AssertSame(before, Totals(g), ctx + " (claimed twice)");
            Assert.AreEqual(coins, g.State.coins, ctx + ": paid twice in doubloons");
            Assert.AreEqual(energy, g.State.energy, ctx + ": paid twice in energy");
            Assert.AreEqual(pts, g.State.seasonPts, ctx + ": paid twice in season points");
        }

        // ------------------------------------------------------------------ invariants

        static void Check(Session g, string ctx)
        {
            var st = g.State;
            Assert.GreaterOrEqual(st.coins, 0, ctx + ": doubloons");
            Assert.That(st.energy >= 0 && st.energy <= World.EnergyMax, ctx + ": energy " + st.energy);
            Assert.That(st.health > 0 && st.health <= 100, ctx + ": health " + st.health);
            Assert.That(st.hunger >= 0 && st.hunger <= 100, ctx + ": hunger " + st.hunger);
            Assert.That(st.thirst >= 0 && st.thirst <= 100, ctx + ": thirst " + st.thirst);
            Assert.GreaterOrEqual(st.level, 1, ctx);
            Assert.That(st.xp >= 0 && st.xp < Session.XpToNext(st.level), ctx + ": xp " + st.xp);
            Assert.That(st.levelsClaimed >= 0 && st.levelsClaimed <= st.level - 1, ctx + ": level rewards");
            Assert.That(st.quest >= 0 && st.quest < Quests.All.Count, ctx + ": quest");
            Assert.AreEqual(g.BackpackSize, st.backpack.Count, ctx + ": backpack size");
            Assert.AreEqual(Content.ChestSlots, st.chest.Count, ctx + ": chest size");
            Assert.AreEqual(4, st.gear.Count, ctx + ": gear slots");
            Assert.IsNotNull(World.Location(st.location), ctx + ": location");

            Slots(st.backpack, ctx + " backpack");
            Slots(st.chest, ctx + " chest");
            Slots(st.gear, ctx + " gear");
            for (int i = 0; i < 4; i++)
                if (!st.gear[i].Empty) Assert.AreEqual((GearSlot)(i + 1), Content.Item(st.gear[i].id).Gear, ctx + ": clothes in the wrong slot");
            foreach (var p in st.pieces)
            {
                Assert.AreEqual(Content.Pieces[p.id].Slots, p.slots.Count, ctx + ": box size");
                Slots(p.slots, ctx + " box");
            }

            Assert.That(st.equipped >= -1 && st.equipped < st.backpack.Count, ctx + ": equipped index");
            if (st.equipped >= 0 && !st.backpack[st.equipped].Empty)
            {
                var d = Content.Item(st.backpack[st.equipped].id);
                Assert.That(d.Kind == ItemKind.Weapon || d.Kind == ItemKind.Tool, ctx + ": holding " + d.Id);
            }

            foreach (var bag in st.lostBags)
            {
                Assert.IsTrue(bag.slots.Any(s => !s.Empty), ctx + ": an empty lost bag is still lying about");
                Slots(bag.slots, ctx + " lost bag");
            }
            foreach (var w in st.waiting) Assert.That(w.n > 0 && Content.Item(w.id) != null, ctx + ": waiting " + w.n + " " + w.id);

            Assert.AreEqual(st.builds.Count, st.builds.Select(b => b.id).Distinct().Count(), ctx + ": a structure twice");
            foreach (var b in st.builds)
            {
                if (b.done) Assert.IsEmpty(b.delivered, ctx + ": materials left on a finished " + b.id);
                foreach (var dl in b.delivered)
                {
                    var need = Content.Builds[b.id].Cost.Where(x => x.id == dl.id).Sum(x => x.n);
                    Assert.That(dl.n > 0 && dl.n <= need, ctx + ": " + dl.n + " " + dl.id + " delivered to " + b.id);
                }
                Assert.That(b.stored >= 0 && b.stored <= Content.RainCap, ctx + ": rain");
            }
            foreach (var j in st.jobs) Assert.Greater(j.count, 0, ctx + ": empty job");
            Assert.AreEqual(st.jobs.Count, st.jobs.Select(j => j.station).Distinct().Count(), ctx + ": two jobs at one station");

            Assert.AreEqual(st.flags.Count, st.flags.Distinct().Count(), ctx + ": flags");
            Assert.AreEqual(st.grantedTx.Count, st.grantedTx.Distinct().Count(), ctx + ": transactions");

            int lv = g.SeasonLevel;
            Assert.That(st.seasonPts >= 0 && st.seasonPts <= Season.Levels * Season.PointsPerLevel, ctx + ": season points");
            Assert.AreEqual(g.SeasonNumber, st.seasonNo, ctx + ": season number");
            foreach (var l in st.seasonFree) Assert.That(l >= 1 && l <= lv, ctx + ": free claim " + l);
            foreach (var l in st.seasonPrem) Assert.That(l >= 1 && l <= lv, ctx + ": premium claim " + l);
            Assert.AreEqual(st.seasonFree.Count, st.seasonFree.Distinct().Count(), ctx + ": free claimed twice");
            Assert.AreEqual(st.seasonPrem.Count, st.seasonPrem.Distinct().Count(), ctx + ": premium claimed twice");
            if (st.seasonPrem.Count > 0) Assert.IsTrue(g.SeasonPremium, ctx + ": premium claims without this season's pass");
            foreach (var e in st.events)
            {
                var def = Season.Event(e.key.Split('@')[0]);
                Assert.That(e.claimed >= 0 && e.claimed <= def.Targets.Count(x => e.n >= x), ctx + ": event " + e.key);
            }
        }

        static void Slots(List<Slot> slots, string ctx)
        {
            foreach (var s in slots)
            {
                Assert.GreaterOrEqual(s.n, 0, ctx + ": negative count");
                if (s.Empty) continue;
                var d = Content.Item(s.id);
                Assert.IsNotNull(d, ctx + ": unknown item " + s.id);
                Assert.That(s.n <= d.Stack, ctx + ": " + s.n + " " + s.id + " in one slot (stack " + d.Stack + ")");
                Assert.That(s.wear >= 0 && (d.Durability <= 0 || s.wear < d.Durability), ctx + ": worn-out " + s.id + " kept");
            }
        }

        // ------------------------------------------------------------------ the save (Unity's JsonUtility rules)

        Session RoundTrip(Session g, int seed) { return Make((GameState)UnityJson(g.State, typeof(GameState)), seed); }

        /// <summary>
        /// What JsonUtility.ToJson then FromJson does to an object: public (and [SerializeField]) instance fields of
        /// serialisable types survive; a null string comes back as "", a null list as an empty list, and a null
        /// field of a [Serializable] class as a fresh instance (Unity can't serialise null for those). Anything else
        /// (properties, dictionaries, tuples, non-serialisable classes) keeps the value its constructor gives it.
        /// </summary>
        static object UnityJson(object v, Type t)
        {
            if (t == typeof(string)) return v ?? "";
            if (t.IsPrimitive || t.IsEnum) return v;
            if (t.IsArray)
            {
                var et = t.GetElementType();
                var src = (Array)v ?? Array.CreateInstance(et, 0);
                var arr = Array.CreateInstance(et, src.Length);
                for (int i = 0; i < src.Length; i++) arr.SetValue(UnityJson(src.GetValue(i), et), i);
                return arr;
            }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var et = t.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(t);
                if (v != null) foreach (var e in (IList)v) list.Add(UnityJson(e, et));
                return list;
            }
            if (!t.IsDefined(typeof(SerializableAttribute), false)) throw new NotSupportedException(t.Name);
            var from = v ?? Activator.CreateInstance(t);
            var to = Activator.CreateInstance(t);
            foreach (var f in Serialised(t)) f.SetValue(to, UnityJson(f.GetValue(from), f.FieldType));
            return to;
        }

        static IEnumerable<FieldInfo> Serialised(Type t)
        {
            return t.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => !f.IsInitOnly && !f.IsDefined(typeof(NonSerializedAttribute), false) && Serialisable(f.FieldType));
        }

        static bool Serialisable(Type t)
        {
            if (t == typeof(string) || t.IsPrimitive || t.IsEnum) return true;
            if (t.IsArray) return t.GetArrayRank() == 1 && Serialisable(t.GetElementType());
            if (t.IsGenericType) return t.GetGenericTypeDefinition() == typeof(List<>) && Serialisable(t.GetGenericArguments()[0]);
            return t.IsDefined(typeof(SerializableAttribute), false);
        }

        /// <summary>Where two states differ (null when they're the same), treating "" as null and [] as a null list.</summary>
        static string Diff(object a, object b, Type t, string path)
        {
            if (t == typeof(string)) return ((string)a ?? "") == ((string)b ?? "") ? null : path + ": '" + a + "' vs '" + b + "'";
            if (t.IsPrimitive || t.IsEnum) return Equals(a, b) ? null : path + ": " + a + " vs " + b;
            if (t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)))
            {
                var et = t.IsArray ? t.GetElementType() : t.GetGenericArguments()[0];
                var la = (IList)a ?? new object[0]; var lb = (IList)b ?? new object[0];
                if (la.Count != lb.Count) return path + ": " + la.Count + " vs " + lb.Count + " entries";
                for (int i = 0; i < la.Count; i++) { var d = Diff(la[i], lb[i], et, path + "[" + i + "]"); if (d != null) return d; }
                return null;
            }
            if (a == null || b == null) return a == b ? null : path + ": " + (a == null ? "null" : "set") + " vs " + (b == null ? "null" : "set");
            foreach (var f in Serialised(t)) { var d = Diff(f.GetValue(a), f.GetValue(b), f.FieldType, path + "." + f.Name); if (d != null) return d; }
            return null;
        }

        // ------------------------------------------------------------------ the calendar

        /// <summary>Months of days in several time zones: seasons turn every four weeks at local midnight, the pass
        /// and the claims belong to one season, the daily takes are three a local day, and events count while they run.</summary>
        [Test]
        public void SeasonsAndEventsFollowTheLocalCalendar([Values(0, -5 * 3600, 5 * 3600 + 1800, 13 * 3600, -11 * 3600)] int zone)
        {
            Session.UtcOffsetSeconds = zone;
            var rng = new Random(zone);
            var g = Make(GameState.New(), 7);
            int premiumSeason = 0;
            var paidFree = new Dictionary<int, int>();
            for (int day = Season.EpochDay; day < Season.EpochDay + Season.Days * 5 + 3; day++)
            {
                // A few moments in this local day, including the last second before midnight.
                foreach (double sec in new[] { 0.0, 1 + rng.Next(86000), 86399.5 })
                {
                    _now = day * 86400.0 + sec - zone;
                    Assert.AreEqual(day, g.Today, "local day at " + sec + "s");
                    int season = (day - Season.EpochDay) / Season.Days + 1;
                    Assert.AreEqual(season, g.SeasonNumber, "season on day " + day);
                    Assert.AreEqual(Season.EpochDay + (season - 1) * Season.Days, g.SeasonStartDay);
                    Assert.That(g.SeasonLeft > 0 && g.SeasonLeft <= Season.Days * 86400.0, "season left " + g.SeasonLeft);
                    Assert.AreEqual(premiumSeason == season, g.SeasonPremium, "premium is for the season it was bought in (day " + day + ")");
                    if (g.State.seasonPrem.Count > 0) Assert.AreEqual(premiumSeason, season);

                    // Points: a bit of play and the daily takes.
                    g.AddXp(rng.Next(300));
                    int takes = 0;
                    while (g.TakeSeasonBonus()) Assert.LessOrEqual(++takes, Season.TakesPerDay, "takes in one day");
                    if (sec == 0) Assert.AreEqual(Season.TakesPerDay, takes, "a new local day brings three new takes");
                    else Assert.AreEqual(0, takes, "the takes were all used this local day");

                    // The pass, now and then, part-way through a season.
                    if (rng.Next(40) == 0) { Assert.IsTrue(g.GrantPurchase(Season.PremiumProduct, "season" + day + "_" + sec)); premiumSeason = season; }
                    Assert.AreEqual(premiumSeason == season, g.SeasonPremium);

                    // Claims: once each, only up to the season level, premium only with the pass.
                    foreach (var sl in g.State.backpack) sl.Clear(); // room for every reward (RoomTests covers a full backpack)
                    int lv = g.SeasonLevel, coins = g.State.coins;
                    for (int l = 0; l <= Season.Levels + 1; l++)
                    {
                        bool canFree = l >= 1 && l <= lv && !g.SeasonClaimed(l, false);
                        bool canPrem = l >= 1 && l <= lv && !g.SeasonClaimed(l, true) && premiumSeason == season;
                        Assert.AreEqual(canFree ? 1 : 0, g.ClaimSeason(l, false).Count, "free " + l);
                        Assert.AreEqual(canPrem ? 1 : 0, g.ClaimSeason(l, true).Count, "premium " + l);
                        if (canFree) paidFree[season] = (paidFree.TryGetValue(season, out var k) ? k : 0) + 1;
                    }
                    Assert.IsEmpty(g.ClaimAllSeason(), "nothing left after claiming every level");
                    Assert.LessOrEqual(paidFree.TryGetValue(season, out var pf) ? pf : 0, Season.Levels, "free rewards in one season");

                    // Events: only the ones running today count, each milestone pays once.
                    var active = g.ActiveEvents;
                    foreach (var r in active) Assert.That(r.Start <= day && day < r.End, r.Key + " is not running on " + day);
                    var stat = Pick(rng, Season.Events).Stat;
                    var counts = g.EventRuns(day - 30, day + 30).ToDictionary(r => r.Key, r => g.EventCount(r));
                    g.Tally(stat, 1 + rng.Next(20));
                    foreach (var r in g.EventRuns(day - 30, day + 30))
                    {
                        bool running = r.Start <= day && day < r.End && r.Def.Stat == stat;
                        if (!running) Assert.AreEqual(counts[r.Key], g.EventCount(r), r.Key + " counted on " + day);
                        else Assert.Greater(g.EventCount(r), counts[r.Key], r.Key + " missed a count on " + day);
                    }
                    foreach (var sl in g.State.backpack) sl.Clear();
                    foreach (var r in g.EventRuns(day - 20, day + 1))
                    {
                        g.ClaimEvent(r);
                        Assert.AreEqual(r.Def.Targets.Count(x => g.EventCount(r) >= x), g.EventClaimed(r), r.Key + " milestones");
                        Assert.IsEmpty(g.ClaimEvent(r), r.Key + " paid twice");
                    }
                }
            }
            Assert.Greater(premiumSeason, 0, "the pass was bought at least once");
        }

        /// <summary>Every day of the calendar has its runs found, and they never overlap themselves.</summary>
        [Test]
        public void EventCalendarHasNoGapsOrDoubles()
        {
            Session.UtcOffsetSeconds = 0;
            var g = Make(GameState.New(), 3);
            for (int day = Season.EpochDay - 40; day < Season.EpochDay + 400; day++)
            {
                _now = day * 86400.0 + 100;
                var runs = g.ActiveEvents;
                Assert.AreEqual(runs.Count, runs.Select(r => r.Def.Id).Distinct().Count(), "one event twice on " + day);
                foreach (var e in Season.Events)
                {
                    int phase = ((day - Season.EpochDay) % Season.CycleDays + Season.CycleDays) % Season.CycleDays;
                    bool on = Enumerable.Range(-1, 2).Any(c => phase - c * Season.CycleDays >= e.StartDay && phase - c * Season.CycleDays < e.StartDay + e.Days);
                    Assert.AreEqual(on, runs.Any(r => r.Def == e), e.Id + " on day " + day);
                }
            }
        }
    }
}
