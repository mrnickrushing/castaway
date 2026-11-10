using System;
using System.Collections.Generic;
using System.Linq;

namespace LastTide.Core
{
    /// <summary>Progress in one run of an event (keyed by event id and the day it started).</summary>
    [Serializable]
    public sealed class EventProgress
    {
        public string key;
        public int n, claimed;
        public bool seen;
    }

    /// <summary>One run of an event on the calendar.</summary>
    public struct EventRun
    {
        public EventDef Def;
        public int Start;                   // local day number (days since 1970)
        public int End { get { return Start + Def.Days; } }   // first day after it
        public string Key { get { return Def.Id + "@" + Start; } }
    }

    /// <summary>
    /// The season pass and the event calendar. Season points come from experience (every XP point is a season point)
    /// and three free daily takes; each level pays a free reward and, with the premium pass for this season, a premium
    /// one. Events run on a fortnightly cycle and count one thing each (boars killed, wood gathered, bags looted...).
    /// Season and event rewards include supply crates and gold chests (opened from the backpack) and companions.
    /// </summary>
    public sealed partial class Session
    {
        /// <summary>Seconds east of UTC on this device, so days turn at local midnight (the game sets it).</summary>
        public static int UtcOffsetSeconds;
        public event Action PetChanged;

        public int Today { get { return (int)Math.Floor((RealNow() + UtcOffsetSeconds) / 86400.0); } }

        // ------------------------------------------------------------------ season

        public int SeasonNumber { get { return Math.Max(1, (Today - Season.EpochDay) / Season.Days + 1); } }
        public int SeasonStartDay { get { return Season.EpochDay + (SeasonNumber - 1) * Season.Days; } }

        /// <summary>Seconds until the season ends (local midnight).</summary>
        public double SeasonLeft { get { return (SeasonStartDay + Season.Days) * 86400.0 - UtcOffsetSeconds - RealNow(); } }

        /// <summary>Starts a fresh season when the calendar has moved on (points and claims reset; pets stay).</summary>
        void SeasonCheck()
        {
            int n = SeasonNumber;
            if (State.seasonNo == n) return;
            State.seasonNo = n;
            State.seasonPts = 0;
            State.seasonFree.Clear();
            State.seasonPrem.Clear();
        }

        public int SeasonPoints { get { SeasonCheck(); return State.seasonPts; } }
        public int SeasonLevel { get { return Math.Min(Season.Levels, SeasonPoints / Season.PointsPerLevel + 1); } }
        /// <summary>Points into the current season level (all of them at the top level).</summary>
        public int SeasonInLevel { get { return SeasonLevel >= Season.Levels ? Season.PointsPerLevel : SeasonPoints % Season.PointsPerLevel; } }
        public bool SeasonPremium { get { SeasonCheck(); return State.seasonPremium == SeasonNumber; } }

        public void AddSeasonPoints(int n)
        {
            if (n <= 0) return;
            SeasonCheck();
            int before = SeasonLevel;
            State.seasonPts = Math.Min(State.seasonPts + n, (Season.Levels - 1) * Season.PointsPerLevel + Season.PointsPerLevel);
            if (SeasonLevel > before) Toast?.Invoke("Season level " + SeasonLevel + "!");
        }

        public int TakesLeft { get { return State.takesDay == Today ? Math.Max(0, Season.TakesPerDay - State.takes) : Season.TakesPerDay; } }

        /// <summary>The free daily bonus of season points (three a day).</summary>
        public bool TakeSeasonBonus()
        {
            if (TakesLeft <= 0) return false;
            if (State.takesDay != Today) { State.takesDay = Today; State.takes = 0; }
            State.takes++;
            AddSeasonPoints(Season.TakePoints);
            Changed?.Invoke();
            return true;
        }

        public bool SeasonClaimed(int level, bool premium) { SeasonCheck(); return (premium ? State.seasonPrem : State.seasonFree).Contains(level); }

        public bool SeasonClaimable(int level, bool premium)
        {
            return level <= SeasonLevel && !SeasonClaimed(level, premium) && (!premium || SeasonPremium);
        }

        public int SeasonWaiting
        {
            get
            {
                int n = 0;
                for (int lv = 1; lv <= Season.Levels; lv++) { if (SeasonClaimable(lv, false)) n++; if (SeasonClaimable(lv, true)) n++; }
                return n;
            }
        }

        /// <summary>Takes one season reward. Returns what was given (empty if nothing).</summary>
        public List<ItemCount> ClaimSeason(int level, bool premium)
        {
            var got = new List<ItemCount>();
            if (level < 1 || level > Season.Levels || !SeasonClaimable(level, premium)) return got;
            var r = premium ? Season.Premium[level] : Season.Free[level];
            if (!Fits(new[] { r })) { NoRoom(); return got; } // stays on the track until there's room
            (premium ? State.seasonPrem : State.seasonFree).Add(level);
            Give(r.id, r.n);
            got.Add(r);
            Changed?.Invoke();
            return got;
        }

        public List<ItemCount> ClaimAllSeason()
        {
            var got = new List<ItemCount>();
            for (int lv = 1; lv <= Season.Levels; lv++)
            {
                got.AddRange(ClaimSeason(lv, false));
                got.AddRange(ClaimSeason(lv, true));
            }
            return got;
        }

        /// <summary>The premium pass for the current season (bought in the shop).</summary>
        void GrantSeasonPremium() { SeasonCheck(); State.seasonPremium = SeasonNumber; }

        // ------------------------------------------------------------------ events

        /// <summary>Runs of every event that overlap the days [from, to).</summary>
        public List<EventRun> EventRuns(int from, int to)
        {
            var runs = new List<EventRun>();
            int baseDay = from - Mod(from - Season.EpochDay, Season.CycleDays) - Season.CycleDays; // cycles start with the seasons
            for (int c = baseDay; c < to; c += Season.CycleDays)
                foreach (var e in Season.Events)
                {
                    var r = new EventRun { Def = e, Start = c + e.StartDay };
                    if (r.Start < to && r.End > from) runs.Add(r);
                }
            return runs.OrderBy(r => r.Start).ThenBy(r => r.Def.Id).ToList();
        }

        static int Mod(int a, int m) { return ((a % m) + m) % m; }

        public List<EventRun> ActiveEvents { get { int d = Today; return EventRuns(d, d + 1); } }

        public EventProgress Progress(EventRun r, bool make = false)
        {
            var p = State.events.FirstOrDefault(e => e.key == r.Key);
            if (p == null && make) State.events.Add(p = new EventProgress { key = r.Key });
            return p;
        }

        public int EventCount(EventRun r) { var p = Progress(r); return p == null ? 0 : p.n; }
        public int EventClaimed(EventRun r) { var p = Progress(r); return p == null ? 0 : p.claimed; }

        /// <summary>Milestones reached but not yet taken.</summary>
        public int EventWaiting(EventRun r)
        {
            int n = EventCount(r), reached = r.Def.Targets.Count(t => n >= t);
            return Math.Max(0, reached - EventClaimed(r));
        }

        public bool EventNew(EventRun r) { var p = Progress(r); return p == null || !p.seen; }
        public void SeeEvent(EventRun r) { Progress(r, true).seen = true; }

        /// <summary>Events with something new or waiting (the badge on the Events tab).</summary>
        public int EventsBadge { get { return ActiveEvents.Count(r => EventNew(r) || EventWaiting(r) > 0); } }

        /// <summary>Takes every reached milestone of an event run (also after it ended). Returns what was given.</summary>
        public List<ItemCount> ClaimEvent(EventRun r)
        {
            var got = new List<ItemCount>();
            var p = Progress(r, true);
            while (p.claimed < r.Def.Targets.Length && p.n >= r.Def.Targets[p.claimed])
            {
                if (!Fits(r.Def.Rewards[p.claimed])) { NoRoom(); break; }
                foreach (var it in r.Def.Rewards[p.claimed]) { Give(it.id, it.n); got.Add(it); }
                p.claimed++;
                AddSeasonPoints(60);
            }
            if (got.Count > 0) Changed?.Invoke();
            return got;
        }

        /// <summary>Counts something towards every running event that tracks it.</summary>
        public void Tally(string stat, int n = 1)
        {
            if (n <= 0) return;
            foreach (var r in ActiveEvents)
            {
                if (r.Def.Stat != stat) continue;
                var p = Progress(r, true);
                int before = EventWaiting(r);
                p.n += n;
                if (EventWaiting(r) > before) Toast?.Invoke(r.Def.Name + ": reward ready!");
            }
        }

        // ------------------------------------------------------------------ crates and companions

        /// <summary>Opens a supply crate or gold chest from the backpack. Returns what was inside (null if not a crate).</summary>
        public List<ItemCount> OpenCrate(int slot)
        {
            if (slot < 0 || slot >= State.backpack.Count) return null;
            var s = State.backpack[slot];
            if (s.Empty || (s.id != "supply_crate" && s.id != "gold_chest")) return null;
            bool gold = s.id == "gold_chest";
            var got = new List<ItemCount> { Content.I("doubloon", gold ? 40 + _rng.Next(41) : 5 + _rng.Next(11)) };
            var pool = gold ? Season.ChestLoot : Season.CrateLoot;
            for (int i = 0, rolls = gold ? 2 : 3; i < rolls; i++) got.Add(pool[_rng.Next(pool.Length)]);
            // Opened only when what's inside fits: else it stays shut in the backpack.
            if (!Fits(got, s.id)) { NoRoom(); return new List<ItemCount>(); }
            s.n--;
            if (s.n <= 0) s.Clear();
            foreach (var g in got) Give(g.id, g.n);
            Changed?.Invoke();
            return got;
        }

        public bool OwnsPet(string id) { return id == "biscuit" ? Flag("pet") : Flag("pet_" + id); }
        public List<PetDef> OwnedPets { get { return Season.Pets.Where(p => OwnsPet(p.Id)).ToList(); } }

        /// <summary>The companion at heel (null when the castaway has none).</summary>
        public PetDef ActivePet
        {
            get
            {
                var p = Season.Pet(State.pet);
                if (p != null && OwnsPet(p.Id)) return p;
                return OwnedPets.FirstOrDefault();
            }
        }

        public void ChoosePet(string id)
        {
            if (!OwnsPet(id) || State.pet == id) return;
            State.pet = id;
            PetChanged?.Invoke();
            Changed?.Invoke();
        }

        /// <summary>A companion arrives (season or event reward): it takes over at heel.</summary>
        void UnlockPet(string id)
        {
            var p = Season.Pet(id);
            if (p == null) return;
            SetFlag("pet_" + id);
            State.pet = id;
            Toast?.Invoke(p.Name + " joins you!");
            PetChanged?.Invoke();
        }
    }
}
