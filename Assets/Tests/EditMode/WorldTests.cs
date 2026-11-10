using System.Linq;
using LastTide.Core;
using NUnit.Framework;

namespace LastTide.Tests
{
    public class WorldTests
    {
        double _now = 1000000;
        Session Fresh() { var s = new Session(GameState.New()); s.RealNow = () => _now; return s; }

        [Test]
        public void ExperienceLevelsUp()
        {
            var g = Fresh();
            int ups = 0;
            g.LevelUp += l => ups++;
            g.AddXp(Session.XpToNext(1) + 5);
            Assert.AreEqual(2, g.State.level);
            Assert.AreEqual(5, g.State.xp);
            Assert.AreEqual(1, ups);
            Assert.AreEqual(1, g.LevelRewardsWaiting);
            Assert.AreEqual(new System.Collections.Generic.List<int> { 2 }, g.ClaimLevelRewards());
            Assert.AreEqual(7, g.State.coins, "a few doubloons per level, taken with LEVEL UP");
        }

        [Test]
        public void EnergyRefillsInRealTime()
        {
            var g = Fresh();
            g.State.energy = 100;
            g.UpdateEnergy();
            _now += World.EnergyEvery * 10 + 1;
            g.UpdateEnergy();
            Assert.AreEqual(110, g.State.energy);
            _now += World.EnergyEvery * 1000;
            g.UpdateEnergy();
            Assert.AreEqual(World.EnergyMax, g.State.energy);
        }

        [Test]
        public void RunningCostsEnergyWalkingCostsTime()
        {
            var g = Fresh();
            var grove = World.Location("grove");
            float before = g.State.energy;
            Assert.IsTrue(g.Travel(grove, true));
            Assert.AreEqual(before - g.RunCost(grove), g.State.energy);
            Assert.IsFalse(g.Arrived);
            Assert.IsNotNull(g.TravelBlocked(grove, true), "already on the way");
            _now += 60;
            Assert.IsTrue(g.Arrived);
            Assert.AreEqual(grove, g.Arrive());
            Assert.AreEqual("grove", g.State.location);
            Assert.IsTrue(g.Flag("visited_grove"));

            var home = World.Location(World.HomeId);
            g.UpdateEnergy();
            float e = g.State.energy;
            Assert.IsTrue(g.Travel(home, false));
            Assert.AreEqual(e, g.State.energy, "walking is free");
            _now += g.WalkSeconds(home) - 5;
            Assert.IsFalse(g.Arrived);
            _now += 10;
            g.Arrive();
            Assert.IsTrue(g.AtHome);
        }

        [Test]
        public void LockedLocationsNeedALevel()
        {
            var g = Fresh();
            Assert.AreEqual("Level 12 required", g.TravelBlocked(World.Location("pirates"), true));
        }

        [Test]
        public void LocationLootGrowsBackAfterAWhile()
        {
            var g = Fresh();
            var grove = World.Location("grove");
            g.Travel(grove, true); _now += 100; g.Arrive();
            g.Give("hatchet", 1);
            string key = Session.ZonePrefix("grove") + "palm#1";
            for (int i = 0; i < 4; i++) g.Harvest(key, "palm");
            Assert.IsFalse(g.Standing(key, "palm"));
            g.Travel(World.Location(World.HomeId), true); _now += 100; g.Arrive();
            g.Travel(grove, true); _now += 100; g.Arrive();
            Assert.IsFalse(g.Standing(key, "palm"), "too soon");
            g.Travel(World.Location(World.HomeId), true); _now += 100; g.Arrive();
            _now += grove.Refresh + 1;
            g.Travel(grove, true); _now += 100; g.Arrive();
            Assert.IsTrue(g.Standing(key, "palm"), "grown back");
        }

        [Test]
        public void RadioCallLeadsToARescue()
        {
            var g = Fresh();
            Assert.AreEqual(0, g.Signals.Length);
            g.State.level = 3;
            var s = g.Signals.Single();
            g.Tune(s);
            Assert.IsTrue(g.Heard(s));
            Assert.IsNull(g.RescueHere, "Tomas is at the grove, not here");
            g.Travel(World.Location(s.Location), true); _now += 100; g.Arrive();
            Assert.AreEqual(s, g.RescueHere);
            Assert.IsFalse(g.Rescue(s), "no bandages yet");
            g.Give("bandage", 2);
            int coins = g.State.coins;
            Assert.IsTrue(g.Rescue(s));
            Assert.AreEqual(1, g.Count("rod"));
            Assert.AreEqual(0, g.Count("bandage"));
            Assert.Greater(g.State.coins, coins);
            Assert.IsNull(g.RescueHere);
        }

        [Test]
        public void TheHarpoonHasToBeTaught()
        {
            var g = Fresh();
            Assert.AreEqual("Rescue Mara at Fisherman's Pier to learn this", g.CraftBlocked("harpoon"));
            g.SetFlag("recipe_harpoon");
            StringAssert.DoesNotContain("learn this", g.CraftBlocked("harpoon") ?? "");
        }

        [Test]
        public void DoubloonsBuyOffersOnce()
        {
            var g = Fresh();
            var o = World.Offers[0];
            Assert.AreEqual("Not enough doubloons", g.BuyBlocked(o));
            g.Give("doubloon", 100);
            Assert.AreEqual(100, g.State.coins);
            Assert.AreEqual(0, g.Count("doubloon"), "doubloons are coins, not backpack items");
            Assert.IsTrue(g.Buy(o));
            Assert.AreEqual(100 - o.Price, g.State.coins);
            Assert.AreEqual("Sold out", g.BuyBlocked(o));
            _now += Session.OfferHours * 3600 + 1;
            Assert.Greater(g.OffersLeft, 0);
            Assert.IsNull(g.BuyBlocked(o), "restocked");
        }

        [Test]
        public void SmugglersBarrelsHoldRum()
        {
            // Ned needs two bottles at level 10, before the pirate camp opens: the cave's barrels must provide them.
            var g = Fresh();
            g.State.level = 10;
            g.Travel(World.Location("cave"), true); _now += 1000; g.Arrive();
            Assert.IsTrue(g.Harvest(Session.ZonePrefix("cave") + "barrel#0", "barrel"));
            Assert.IsTrue(g.Harvest(Session.ZonePrefix("cave") + "barrel#1", "barrel"));
            Assert.GreaterOrEqual(g.Count("rum"), 2);
        }

        [Test]
        public void FishingNeedsARod()
        {
            var g = Fresh();
            StringAssert.StartsWith("Needs a fishing rod", g.Blocked("fishspot"));
            g.Give("rod", 1);
            Assert.IsTrue(g.Harvest("pier:fishspot#0", "fishspot"));
            Assert.AreEqual(1, g.Count("fish"));
        }

        [Test]
        public void EveryLocationNamesRealThings()
        {
            foreach (var l in World.Locations)
            {
                foreach (var i in l.MainLoot.Concat(l.PossibleLoot)) Assert.IsTrue(i == "doubloon" || Content.Item(i) != null, l.Id + ": " + i);
                foreach (var n in l.Nodes) Assert.IsTrue(Content.Nodes.ContainsKey(n.kind), l.Id + ": " + n.kind);
                foreach (var e in l.Enemies) Assert.IsTrue(Enemies.All.ContainsKey(e.kind), l.Id + ": " + e.kind);
            }
            foreach (var s in World.Signals) Assert.IsNotNull(World.Location(s.Location), s.Id);
        }
    }
}
