using System.Linq;
using LastTide.Core;
using NUnit.Framework;

namespace LastTide.Tests
{
    public class SurvivalTests
    {
        static Session Fresh() { return new Session(GameState.New()); }

        [Test]
        public void StacksFillThenSpill()
        {
            var s = GameState.New();
            Assert.AreEqual(50, Inventory.Add(s.backpack, "wood", 50));
            Assert.AreEqual(40, s.backpack[0].n);
            Assert.AreEqual(10, s.backpack[1].n);
            Assert.AreEqual(50, Inventory.Count(s.backpack, "wood"));
            Assert.AreEqual(15, Inventory.Remove(s.backpack, "wood", 15));
            Assert.AreEqual(35, Inventory.Count(s.backpack, "wood"));
        }

        [Test]
        public void ToolsNeverStack()
        {
            var s = GameState.New();
            Inventory.Add(s.backpack, "spear", 2);
            Assert.AreEqual(1, s.backpack[0].n);
            Assert.AreEqual(1, s.backpack[1].n);
        }

        [Test]
        public void PalmsNeedAHatchetAndFallAfterFourChops()
        {
            var g = Fresh();
            Assert.AreEqual("Needs a hatchet", g.Blocked("palm"));
            Assert.IsFalse(g.Harvest("palm#1", "palm"));
            Assert.IsTrue(g.Harvest("h", "hatchet_pickup"));
            Assert.IsFalse(g.Standing("h", "hatchet_pickup"), "the hatchet is picked up for good");
            for (int i = 0; i < 4; i++) Assert.IsTrue(g.Harvest("palm#1", "palm"));
            Assert.IsFalse(g.Standing("palm#1", "palm"));
            Assert.AreEqual(12, g.Count("wood"));
            Assert.AreEqual(2, g.Count("coconut"));
            Assert.AreEqual(4, g.State.backpack.First(s => s.id == "hatchet").wear);
            g.Tick(Content.Nodes["palm"].Respawn + 1);
            Assert.IsTrue(g.Standing("palm#1", "palm"), "palms grow back");
        }

        [Test]
        public void BuildingTakesMaterialsBitByBit()
        {
            var g = Fresh();
            g.Give("wood", 4);
            Assert.IsTrue(g.CanDeliver("campfire"));
            Assert.AreEqual(4, g.Deliver("campfire", 10));
            Assert.IsFalse(g.IsBuilt("campfire"));
            g.Give("wood", 5); g.Give("stone", 5);
            int total = 0;
            while (g.CanDeliver("campfire")) total += g.Deliver("campfire", 1);
            Assert.AreEqual(7, total);
            Assert.IsTrue(g.IsBuilt("campfire"));
            Assert.AreEqual(3, g.Count("wood"), "only what was needed is taken");
        }

        [Test]
        public void CookingNeedsAFireAndTakesTime()
        {
            var g = Fresh();
            g.Give("raw_meat", 1);
            Assert.IsNotNull(g.CraftBlocked("meat"));
            g.Give("wood", 6); g.Give("stone", 5);
            g.Deliver("campfire", 99);
            Assert.IsTrue(g.Craft("meat"));
            Assert.AreEqual(0, g.Count("raw_meat"));
            g.Tick(1);
            Assert.AreEqual(0, g.Count("meat"));
            g.Tick(5);
            Assert.AreEqual(1, g.Count("meat"));
            Assert.IsTrue(g.Flag("cooked"));
        }

        [Test]
        public void HungerAndThirstRunDownThenHurt()
        {
            var g = Fresh();
            g.Tick(60);
            Assert.Less(g.State.hunger, 100);
            Assert.Less(g.State.thirst, g.State.hunger, "thirst runs down faster");
            g.State.thirst = 0; g.State.health = 100;
            g.Tick(10);
            Assert.Less(g.State.health, 100);
        }

        [Test]
        public void EatingRestoresHunger()
        {
            var g = Fresh();
            g.State.hunger = 20;
            g.Give("meat", 1);
            int slot = g.State.backpack.FindIndex(s => s.id == "meat");
            Assert.IsTrue(g.Use(slot));
            Assert.AreEqual(52, g.State.hunger, 0.01);
            Assert.AreEqual(0, g.Count("meat"));
        }

        [Test]
        public void DyingDropsTheBackpackWhereYouFell()
        {
            var g = Fresh();
            bool died = false;
            g.Died += () => died = true;
            g.Give("wood", 10);
            g.State.px = 5; g.State.pz = 7;
            g.Damage(500);
            Assert.IsTrue(died);
            Assert.AreEqual(0, g.Count("wood"));
            Assert.IsNotNull(g.State.lostBag);
            Assert.AreEqual(5, g.State.lostBag.x);
            Assert.AreEqual(60, g.State.health);
            Assert.AreEqual(10, g.TakeBag());
            Assert.IsNull(g.State.lostBag);
            Assert.AreEqual(10, g.Count("wood"));
        }

        [Test]
        public void WeaponsWearOutAndBreak()
        {
            var g = Fresh();
            g.Give("spear", 1);
            Assert.AreEqual("spear", g.WeaponId, "a new weapon is equipped");
            Assert.AreEqual(18, g.AttackDamage());
            for (int i = 0; i < Content.Item("spear").Durability; i++) g.Swung();
            Assert.AreEqual(0, g.Count("spear"));
            Assert.AreEqual(Content.FistDamage, g.AttackDamage());
        }

        [Test]
        public void TheGuideWalksThroughTheFirstCamp()
        {
            var g = Fresh();
            Assert.AreEqual("hatchet", g.CurrentQuest.Id);
            g.Harvest("h", "hatchet_pickup");
            Assert.AreEqual("wood", g.CurrentQuest.Id);
            g.Give("wood", 6); g.CheckQuests();
            Assert.AreEqual("stone", g.CurrentQuest.Id);
            g.Give("stone", 5); g.CheckQuests();
            Assert.AreEqual("campfire", g.CurrentQuest.Id);
            g.Deliver("campfire", 3);
            Assert.AreEqual("campfire", g.CurrentQuest.Id, "delivering part of the wood doesn't step back");
            g.Deliver("campfire", 99);
            Assert.AreEqual("fiber", g.CurrentQuest.Id);
            Assert.AreEqual(2, g.Count("coconut"), "campfire reward");
        }

        [Test]
        public void RainCatcherFillsFlasks()
        {
            var g = Fresh();
            g.Give("wood", 8); g.Give("cloth", 3); g.Give("scrap", 2);
            g.Deliver("raincatcher", 99);
            Assert.IsTrue(g.IsBuilt("raincatcher"));
            g.Tick(Content.RainSeconds * 2 + 1);
            Assert.AreEqual(2, g.CollectRain());
            g.Tick(Content.RainSeconds * 20);
            Assert.AreEqual(Content.RainCap, g.CollectRain());
        }

        [Test]
        public void ChestKeepsResources()
        {
            var g = Fresh();
            g.Give("wood", 16); g.Give("scrap", 3); g.Give("rope", 2);
            g.Deliver("chest", 99);
            g.Give("stone", 9); g.Give("spear", 1);
            Assert.AreEqual(9, g.StoreResources());
            Assert.AreEqual(1, g.Count("spear"), "tools stay in the backpack");
            Assert.AreEqual(9, Inventory.Count(g.State.chest, "stone"));
        }

        [Test]
        public void EveryRecipeAndBuildUsesKnownItems()
        {
            foreach (var r in Content.Recipes.Values)
            {
                Assert.IsNotNull(Content.Item(r.Output), r.Id);
                foreach (var c in r.Inputs) Assert.IsNotNull(Content.Item(c.id), r.Id + " needs " + c.id);
            }
            foreach (var b in Content.Builds.Values)
                foreach (var c in b.Cost) Assert.IsNotNull(Content.Item(c.id), b.Id + " needs " + c.id);
            foreach (var n in Content.Nodes.Values)
                foreach (var y in n.Yield) Assert.IsNotNull(Content.Item(y.id), n.Id);
        }
    }
}
