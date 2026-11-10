using System;
using System.Linq;

namespace LastTide.Core
{
    /// <summary>
    /// The wider game: experience and levels, energy that refills in real time, doubloons, travelling between the
    /// islands on the map (run for energy, or walk for free and wait), locations whose loot grows back, radio
    /// distress calls and the rescues they lead to, and the trading post.
    /// </summary>
    public sealed partial class Session
    {
        /// <summary>Real wall-clock time in unix seconds (tests replace it).</summary>
        public Func<double> RealNow = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        public event Action<int> LevelUp;
        readonly Random _rng = new Random();

        public LocationDef Here { get { return World.Location(State.location) ?? World.Location(World.HomeId); } }
        public bool AtHome { get { return Here.Home; } }

        // ------------------------------------------------------------------ levels

        public static int XpToNext(int level) { return 40 + (level - 1) * 45; }

        public void AddXp(int n)
        {
            if (n <= 0) return;
            if (State.trip != null) State.trip.xp += n;
            AddSeasonPoints(n);
            State.xp += n;
            while (State.xp >= XpToNext(State.level))
            {
                State.xp -= XpToNext(State.level);
                State.level++;
                Toast?.Invoke("Level " + State.level + "!");
                LevelUp?.Invoke(State.level);
                CheckQuests();
            }
        }

        public float Xp01 { get { return State.xp / (float)XpToNext(State.level); } }

        // ------------------------------------------------------------------ energy

        /// <summary>Tops energy up for the real time that has passed (also while the game was closed).</summary>
        public void UpdateEnergy()
        {
            double now = RealNow();
            if (State.energyStamp <= 0) State.energyStamp = now;
            if (State.energy >= World.EnergyMax) { State.energy = World.EnergyMax; State.energyStamp = now; return; }
            double gained = Math.Floor((now - State.energyStamp) / World.EnergyEvery);
            if (gained <= 0) return;
            State.energy = (float)Math.Min(World.EnergyMax, State.energy + gained);
            State.energyStamp += gained * World.EnergyEvery;
        }

        /// <summary>Seconds until the next energy point (0 when full).</summary>
        public double NextEnergyIn
        {
            get
            {
                if (State.energy >= World.EnergyMax) return 0;
                return Math.Max(0, World.EnergyEvery - (RealNow() - State.energyStamp));
            }
        }

        // ------------------------------------------------------------------ travel

        static float Dist(LocationDef a, LocationDef b) { float dx = a.MapX - b.MapX, dz = a.MapZ - b.MapZ; return (float)Math.Sqrt(dx * dx + dz * dz); }

        /// <summary>Energy to run from where the castaway is now to <paramref name="to"/>.</summary>
        public int RunCost(LocationDef to) { return Math.Max(3, (int)Math.Round(Dist(Here, to) / 13f)); }
        public int RunSeconds(LocationDef to) { return Math.Max(3, (int)Math.Round(Dist(Here, to) / 30f)); }
        public int WalkSeconds(LocationDef to) { return Math.Max(30, (int)Math.Round(Dist(Here, to) * 1.6f)); }

        public bool Unlocked(LocationDef l) { return State.level >= l.Level; }

        /// <summary>Why the castaway can't set off for <paramref name="to"/>, or null.</summary>
        public string TravelBlocked(LocationDef to, bool run)
        {
            if (to.Id == State.location) return "You are here";
            if (!Unlocked(to)) return "Level " + to.Level + " required";
            if (State.travel != null) return "Already on the way";
            UpdateEnergy();
            if (run && State.energy < RunCost(to)) return "Not enough energy";
            return null;
        }

        /// <summary>Sets off: running spends energy and takes seconds, walking is free and takes minutes.</summary>
        public bool Travel(LocationDef to, bool run)
        {
            if (TravelBlocked(to, run) != null) return false;
            double now = RealNow();
            if (run) { State.energy -= RunCost(to); if (State.energy < World.EnergyMax && State.energyStamp <= 0) State.energyStamp = now; }
            State.travel = new TravelState { from = State.location, to = to.Id, leftAt = now, arriveAt = now + (run ? RunSeconds(to) : WalkSeconds(to)) };
            Changed?.Invoke();
            return true;
        }

        /// <summary>0..1 along the current journey.</summary>
        public float TravelProgress
        {
            get
            {
                var t = State.travel;
                if (t == null) return 0;
                return (float)Math.Max(0, Math.Min(1, (RealNow() - t.leftAt) / Math.Max(0.01, t.arriveAt - t.leftAt)));
            }
        }

        public bool Arrived { get { return State.travel != null && RealNow() >= State.travel.arriveAt; } }

        /// <summary>Finishes a journey: the castaway is now at the destination (its loot fresh if it has been long enough).</summary>
        public LocationDef Arrive()
        {
            if (!Arrived) return null;
            var from = State.location;
            var to = World.Location(State.travel.to);
            State.travel = null;
            Leave(from);
            State.location = to.Id;
            if (to.Home) TripEnd();
            else
            {
                TripStart();
                if (!State.trip.places.Contains(to.Id)) State.trip.places.Add(to.Id);
            }
            if (!to.Home)
            {
                var v = State.visits.FirstOrDefault(x => x.id == to.Id);
                if (v == null || RealNow() - v.leftAt > to.Refresh)
                    State.nodes.RemoveAll(n => n.key.StartsWith(ZonePrefix(to.Id)));
                SetFlag("visited_" + to.Id);
            }
            Changed?.Invoke();
            CheckQuests();
            return to;
        }

        void Leave(string id)
        {
            var v = State.visits.FirstOrDefault(x => x.id == id);
            if (v == null) State.visits.Add(v = new VisitState { id = id });
            v.leftAt = RealNow();
        }

        /// <summary>Harvest keys of a location's things start with this (home keys have none).</summary>
        public static string ZonePrefix(string location) { return location == World.HomeId ? "" : location + ":"; }

        /// <summary>Away from home, every harvest has a chance of turning up something from the location's possible loot.</summary>
        void RollPossibleLoot()
        {
            var here = Here;
            if (here.Home || here.PossibleLoot.Length == 0 || _rng.NextDouble() > 0.14) return;
            var id = here.PossibleLoot[_rng.Next(here.PossibleLoot.Length)];
            Give(id, id == "doubloon" ? 3 : 1);
        }

        // ------------------------------------------------------------------ radio

        public bool Heard(SignalDef s) { return Flag("heard_" + s.Id); }
        public bool Rescued(SignalDef s) { return Flag("done_" + s.Id); }

        /// <summary>The calls the radio can pick up now: the castaway is high enough level and hasn't finished the rescue.</summary>
        public SignalDef[] Signals { get { return World.Signals.Where(s => State.level >= s.Level && !Rescued(s)).ToArray(); } }

        public void Tune(SignalDef s)
        {
            if (Heard(s)) return;
            SetFlag("heard_" + s.Id);
            AddXp(10);
            Changed?.Invoke();
            CheckQuests();
        }

        /// <summary>The rescue waiting at this location (heard on the radio, not finished), or null.</summary>
        public SignalDef RescueHere { get { return World.Signals.FirstOrDefault(s => s.Location == State.location && Heard(s) && !Rescued(s)); } }

        public bool CanRescue(SignalDef s) { return Inventory.Has(State.backpack, s.Need); }

        public bool Rescue(SignalDef s)
        {
            if (!Heard(s) || Rescued(s) || !CanRescue(s)) return false;
            foreach (var c in s.Need) Inventory.Remove(State.backpack, c.id, c.n);
            foreach (var r in s.Reward) GiveOrHold(r.id, r.n);
            State.coins += s.RewardCoins;
            if (s.Unlocks != null) SetFlag("recipe_" + s.Unlocks);
            SetFlag("done_" + s.Id);
            AddXp(s.RewardXp);
            Toast?.Invoke(s.Npc + " is saved!");
            Changed?.Invoke();
            CheckQuests();
            return true;
        }

        /// <summary>Recipes that a survivor has to teach first.</summary>
        public bool Knows(string recipe) { return recipe != "harpoon" || Flag("recipe_harpoon"); }

        // ------------------------------------------------------------------ trading post

        public const double OfferHours = 30;

        public int Bought(OfferDef o) { var e = State.offers.FirstOrDefault(x => x.id == o.Id); return e.id == null ? 0 : e.n; }

        /// <summary>Seconds left in the current offer rotation (the packs restock when it runs out).</summary>
        public double OffersLeft
        {
            get
            {
                double now = RealNow();
                if (State.offerStart <= 0 || now - State.offerStart > OfferHours * 3600) { State.offerStart = now; State.offers.Clear(); }
                return OfferHours * 3600 - (now - State.offerStart);
            }
        }

        public string BuyBlocked(OfferDef o)
        {
            if (Bought(o) >= o.Limit) return "Sold out";
            if (State.coins < o.Price) return "Not enough doubloons";
            if (!Fits(o.Items)) return "Make room in your backpack";
            return null;
        }

        public bool Buy(OfferDef o)
        {
            if (BuyBlocked(o) != null) return false;
            State.coins -= o.Price;
            foreach (var i in o.Items) Give(i.id, i.n);
            if (o.Energy > 0) State.energy = Math.Min(World.EnergyMax, State.energy + o.Energy);
            int idx = State.offers.FindIndex(x => x.id == o.Id);
            if (idx < 0) State.offers.Add(new ItemCount(o.Id, 1)); else State.offers[idx] = new ItemCount(o.Id, State.offers[idx].n + 1);
            Toast?.Invoke(o.Name + " bought!");
            Changed?.Invoke();
            return true;
        }
    }
}
