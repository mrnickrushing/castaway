using System.Collections.Generic;
using System.Linq;

namespace LastTide.Core
{
    /// <summary>
    /// The trip log: from landing on another island until getting back home, what was found, what was defeated, the
    /// XP and doubloons earned, recipes learned and survivors rescued. Home again, it waits in
    /// <see cref="GameState.lastTrip"/> until the HUD has shown it (<see cref="TripSeen"/>).
    /// </summary>
    public sealed partial class Session
    {
        public TripLog LastTrip { get { return State.lastTrip; } }

        void TripStart()
        {
            if (State.trip != null) return;
            State.trip = new TripLog
            {
                active = true,
                knew = Content.Recipes.Keys.Where(Knows).ToList(),
                rescuedBefore = World.Signals.Where(Rescued).Select(s => s.Id).ToList(),
                perksBefore = Content.Islands.Select(i => i.Perk).Where(HasPerk).ToList(),
            };
        }

        static void AddTo(List<ItemCount> list, string id, int n)
        {
            int i = list.FindIndex(x => x.id == id);
            if (i < 0) list.Add(new ItemCount(id, n)); else list[i] = new ItemCount(id, list[i].n + n);
        }

        void TripGained(string id, int n)
        {
            var t = State.trip;
            if (t == null || n <= 0) return;
            if (id == "doubloon") t.coins += n;
            else if (!id.StartsWith("pet_")) AddTo(t.gained, id, n);
        }

        void TripEnd()
        {
            var t = State.trip;
            if (t == null) return;
            State.trip = null;
            // A plan's improvement is told as the plan found, not as a recipe learned.
            t.learned = Content.Recipes.Keys.Where(Knows).Except(t.knew).Where(r => !Content.IsPerk(r)).ToList();
            t.perks = Content.Islands.Select(i => i.Perk).Where(HasPerk).Except(t.perksBefore).ToList();
            t.rescued = World.Signals.Where(Rescued).Select(s => s.Id).Except(t.rescuedBefore).ToList();
            bool happened = t.gained.Count > 0 || t.kills.Count > 0 || t.learned.Count > 0 || t.rescued.Count > 0 || t.plans.Count > 0 || t.perks.Count > 0 || t.xp > 0 || t.coins > 0 || t.falls > 0;
            State.lastTrip = happened ? t : null;
        }

        /// <summary>A plan picked up on this trip (the summary names it).</summary>
        void TripPlan(string location)
        {
            var t = State.trip;
            if (t != null && !t.plans.Contains(location)) t.plans.Add(location);
        }

        /// <summary>The HUD has shown the summary.</summary>
        public void TripSeen() { State.lastTrip = null; }

        /// <summary>What the castaway can do at home right now that they couldn't before setting out, as far as the
        /// backpack and storages go: upgrades and camp structures they can pay for in full, and recipes they can craft.</summary>
        public List<string> NowPossible(int max = 4)
        {
            var list = new List<string>();
            foreach (var b in Content.Builds.Values)
            {
                if (list.Count >= max) break;
                if (IsBuilt(b.Id))
                {
                    var up = UpgradeCost(b.Id);
                    if (up != null && CanPay(up)) list.Add("Upgrade the " + b.Name.ToLower() + " to level " + (Level(b.Id) + 1));
                }
                else
                {
                    var left = Remaining(b.Id);
                    if (left.Count > 0 && CanPay(left.ToArray())) list.Add("Build the " + b.Name.ToLower());
                }
            }
            if (State.lastTrip != null)
                foreach (var loc in State.lastTrip.plans)
                {
                    var isl = Content.IslandOf(loc);
                    if (list.Count < max && isl != null && CraftBlocked(isl.Perk, 1) == null) list.Add("Build the " + Content.NameOf(isl.Perk) + " at the workbench");
                }
            if (State.lastTrip != null)
                foreach (var r in State.lastTrip.learned)
                    if (list.Count < max && Content.Recipes.ContainsKey(r) && CraftBlocked(r, 1) == null) list.Add("Craft " + Content.NameOf(Content.Recipes[r].Output));
            return list;
        }
    }
}
