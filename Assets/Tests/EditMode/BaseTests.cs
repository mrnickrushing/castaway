using System.Linq;
using LastTide.Core;
using NUnit.Framework;

namespace LastTide.Tests
{
    public class BaseTests
    {
        static Session Fresh() { return new Session(GameState.New()); }

        [Test]
        public void ClothesSoakUpBlows()
        {
            var g = Fresh();
            g.Give("hide_vest", 1);
            int slot = g.State.backpack.FindIndex(s => s.id == "hide_vest");
            Assert.IsTrue(g.PutOn(slot));
            Assert.AreEqual(6, g.Armor);
            g.Damage(20, "boar");
            Assert.AreEqual(100 - 20 * 40f / 46f, g.State.health, 0.01);
            g.State.health = 100; g.Damage(10, "starving");
            Assert.AreEqual(90, g.State.health, 0.01, "armour doesn't help hunger");
            Assert.IsTrue(g.TakeOff(GearSlot.Body));
            Assert.AreEqual(0, g.Armor);
        }

        [Test]
        public void SplitAndSort()
        {
            var g = Fresh();
            g.Give("wood", 10);
            Assert.IsTrue(g.Split(0));
            Assert.AreEqual(5, g.State.backpack[0].n);
            Assert.AreEqual(10, g.Count("wood"));
            Inventory.Add(g.State.backpack.GetRange(Content.PocketSlots, 15), "stone", 3);
            Inventory.Add(g.State.backpack.GetRange(Content.PocketSlots, 15), "spear", 1);
            g.Sort();
            Assert.AreEqual("spear", g.State.backpack[Content.PocketSlots].id, "weapons first");
        }

        [Test]
        public void CraftingSeveralAtOnceAtTheRightStation()
        {
            var g = Fresh();
            g.Give("fiber", 12);
            Assert.AreEqual(3, g.MaxCraft("rope"));
            Assert.IsTrue(g.Craft("rope", 3));
            g.Tick(10);
            Assert.AreEqual(3, g.Count("rope"));
            Assert.AreEqual("Build a furnace first", g.CraftBlocked("iron_bar", 1));
        }

        [Test]
        public void UpgradingTheCampfireUnlocksStew()
        {
            var g = Fresh();
            g.Give("wood", 16); g.Give("stone", 17); g.Give("scrap", 2);
            g.Deliver("campfire", 99);
            Assert.AreEqual(1, g.Level("campfire"));
            Assert.AreEqual("Needs Campfire level 2", g.CraftBlocked("stew", 1));
            Assert.IsTrue(g.Upgrade("campfire"));
            Assert.AreEqual(2, g.Level("campfire"));
            Assert.IsNull(g.UpgradeCost("campfire"), "top level");
        }

        [Test]
        public void BuildingOnTheGrid()
        {
            var g = Fresh();
            g.Give("wood", 30); g.Give("fiber", 10);
            Assert.IsNotNull(g.Place("foundation", 2, 2, 0));
            Assert.AreEqual("Something is already there", g.PlaceBlocked("foundation", 2, 2, 0));
            Assert.IsNotNull(g.Place("wall", 2, 2, 0));
            Assert.IsNotNull(g.Place("wall", 2, 2, 1), "the other edge is free");
            Assert.AreEqual("Too far from camp", g.PlaceBlocked("foundation", 40, 0, 0));
            var box = g.Place("small_box", 3, 2, 0);
            Assert.AreEqual(8, box.slots.Count);
            g.Give("stone", 5);
            int slot = g.State.backpack.FindIndex(s => s.id == "stone");
            Assert.AreEqual(5, g.StoreSlotTo(box.slots, slot));
            Assert.AreEqual(5, g.CountAll("stone"), "storage counts for building");
            Assert.IsTrue(g.Demolish(box));
            Assert.AreEqual(5, g.Count("stone"), "contents come back");
        }

        [Test]
        public void TheUltraTrackPaysExtra()
        {
            var g = Fresh();
            g.State.quest = 3;
            Assert.AreEqual(3, g.Claimable);
            Assert.AreEqual(3, g.ClaimAll());
            Assert.AreEqual(0, g.Claimable);
            g.GrantPurchase("lasttide.ultra", "u1");
            Assert.AreEqual(3, g.Claimable, "past Ultra rewards wait too");
            g.ClaimAll();
            Assert.AreEqual(0, g.Claimable);
        }

        [Test]
        public void EveryPieceAndRecipeUsesKnownItems()
        {
            foreach (var p in Content.Pieces.Values) foreach (var c in p.Cost) Assert.IsNotNull(Content.Item(c.id), p.Id);
            foreach (var r in Content.Recipes.Values)
                Assert.IsTrue(r.Station == "hand" || Content.Builds.ContainsKey(r.Station) || Content.Pieces.Values.Any(p => p.Station == r.Station), r.Id);
            foreach (var q in Quests.All) foreach (var c in q.Premium.Concat(q.Reward)) Assert.IsTrue(c.id == "doubloon" || Content.Item(c.id) != null, q.Id);
        }
    }
}
