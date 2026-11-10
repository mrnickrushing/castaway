using System;

namespace LastTide.Core
{
    /// <summary>
    /// The islands' plans and camp improvements (<see cref="Content.Islands"/>): finding a plan at a landmark teaches
    /// its improvement; building it at the workbench sets a flag for good, and the rules below read those flags.
    /// </summary>
    public sealed partial class Session
    {
        /// <summary>A plan was found (the HUD shows its story).</summary>
        public event Action<IslandDef> PlanFound;

        public bool HasPerk(string perk) { return Flag(perk); }
        public bool HasPlan(IslandDef i) { return i != null && Flag("plan_" + i.Location); }

        /// <summary>Picks up the plan at this island's landmark (once).</summary>
        public bool FindPlan(string location)
        {
            var i = Content.IslandOf(location);
            if (i == null || i.Location != location || HasPlan(i)) return false;
            SetFlag("plan_" + location);
            TripPlan(location);
            AddXp(25);
            PlanFound?.Invoke(i);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Why the improvement can't be started, before materials (null: it can).</summary>
        string PerkBlocked(string perk)
        {
            var i = Content.IslandOf(perk);
            if (i == null) return null;
            if (HasPerk(perk)) return "Already built at camp";
            if (!HasPlan(i)) { var l = World.Location(i.Location); return "Find the plan on " + (l != null ? l.Name : i.Location) + " to learn this"; }
            return null;
        }

        void ApplyPerk(string perk)
        {
            if (HasPerk(perk)) return;
            SetFlag(perk);
            if (perk == "perk_chest") for (int k = 0; k < ChestBrace; k++) State.chest.Add(new Slot());
            var i = Content.IslandOf(perk);
            Toast?.Invoke(Content.NameOf(perk) + " built: " + (i != null ? i.Effect : ""));
            Gained?.Invoke(perk, 0);
        }

        /// <summary>Slots the Timber-Braced Chest adds.</summary>
        public const int ChestBrace = 6;

        // What the improvements do, read where each rule lives.
        float FoodScale { get { return HasPerk("perk_larder") ? 1.2f : 1f; } }
        float RegenScale { get { return HasPerk("perk_limewash") ? 1.4f : 1f; } }
        float HungerScale { get { return HasPerk("perk_salt") ? 0.8f : 1f; } }
        float NightAggro { get { return HasPerk("perk_lamp") ? 1.1f : 1.35f; } }
        float SailScale { get { return HasPerk("perk_sail") ? 0.75f : 1f; } }

        /// <summary>The island's own material, now and then, from what lives there.</summary>
        void IslandDrop()
        {
            var i = Content.IslandOf(State.location);
            if (i != null && i.Location == State.location && _rng.NextDouble() < 0.3) Give(i.Ingredient, 1);
        }
    }
}
