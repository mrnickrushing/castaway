using System.Linq;
using LastTide.Core;
using NUnit.Framework;

namespace LastTide.Tests
{
    public class StoryStoreTests
    {
        double _now = 5000;
        Session Fresh() { var s = new Session(GameState.New()); s.RealNow = () => _now; return s; }

        [Test]
        public void EveryPurchaseIsGrantedOncePerTransaction()
        {
            var g = Fresh();
            g.BeginPurchase("lasttide.doubloons.medium");
            Assert.IsTrue(g.HasPendingPurchases);
            Assert.IsTrue(g.GrantPurchase("lasttide.doubloons.medium", "tx1"));
            Assert.AreEqual(650, g.State.coins);
            Assert.IsFalse(g.GrantPurchase("lasttide.doubloons.medium", "tx1"), "the same transaction pays once");
            Assert.AreEqual(650, g.State.coins);
            Assert.IsFalse(g.HasPendingPurchases);
        }

        [Test]
        public void APurchaseLostInACrashIsDeliveredLater()
        {
            var g = Fresh();
            g.BeginPurchase("lasttide.doubloons.small");
            var got = g.ReconcilePurchases(new[] { new StoreReceipt("lasttide.doubloons.small", "txA", _now + 3) });
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(120, g.State.coins);
            Assert.AreEqual(0, g.ReconcilePurchases(new[] { new StoreReceipt("lasttide.doubloons.small", "txA", _now + 3) }).Count);
        }

        [Test]
        public void TheBigBackpackAddsSlotsAndComesBackOnRestore()
        {
            var g = Fresh();
            Assert.AreEqual(Content.BackpackSlots, g.State.backpack.Count);
            g.GrantPurchase("lasttide.bigpack", "tx9");
            Assert.AreEqual(Content.BackpackSlots + Session.BigPackSlots, g.State.backpack.Count);
            var other = Fresh();
            Assert.AreEqual(1, other.RestoreNonConsumables(new[] { "lasttide.bigpack", "lasttide.doubloons.small" }));
            Assert.AreEqual(Content.BackpackSlots + Session.BigPackSlots, other.State.backpack.Count);
            Assert.AreEqual(0, other.State.coins, "consumables aren't restored");
        }

        [Test]
        public void StarterPackOnlyOnce()
        {
            var g = Fresh();
            Assert.IsTrue(g.GrantPurchase("lasttide.starter", "a"));
            Assert.IsFalse(g.GrantPurchase("lasttide.starter", "b"));
            Assert.AreEqual(300, g.State.coins);
            Assert.AreEqual(1, g.Count("spear"));
        }

        [Test]
        public void DoubloonsRefillEnergyAndRushTheJourney()
        {
            var g = Fresh();
            g.State.coins = 100;
            g.State.energy = 10;
            Assert.IsTrue(g.RefillEnergy());
            Assert.AreEqual(World.EnergyMax, g.State.energy);
            Assert.AreEqual(100 - Session.RefillEnergyCost, g.State.coins);
            g.Travel(World.Location("grove"), false);
            Assert.IsFalse(g.Arrived);
            int cost = g.RushTravelCost;
            Assert.IsTrue(g.RushTravel());
            Assert.IsTrue(g.Arrived);
            Assert.AreEqual(100 - Session.RefillEnergyCost - cost, g.State.coins);
        }

        [Test]
        public void TheStoryHasAChapterAndScenesForItsSteps()
        {
            foreach (var key in Story.Scenes.Keys) Assert.IsTrue(Quests.All.Any(q => q.Id == key), "scene for unknown step " + key);
            foreach (var c in Story.Chapters) Assert.IsTrue(Quests.All.Any(q => q.Id == c.FirstQuest), c.Title);
            Assert.AreEqual("Washed Ashore", Story.ChapterOf(0).Title);
            Assert.AreEqual("The Last Tide", Story.ChapterOf(Quests.All.FindIndex(q => q.Id == "redjack")).Title);
        }

        [Test]
        public void RushingACraftCostsWhatTheSkipButtonSays()
        {
            var g = Fresh();
            g.Give("doubloon", 50);
            g.Give("fiber", 40);
            Assert.IsTrue(g.Craft("rope", 10));         // 20 seconds
            g.Tick(19);
            Assert.AreEqual(1, g.RushCost("hand"));
            Assert.IsTrue(g.RushCraft("hand"));
            Assert.AreEqual(49, g.State.coins);
            g.Tick(0.1f);
            Assert.AreEqual(10, g.Count("rope"));

            g.Give("fiber", 40); g.Give("fiber", 40);
            Assert.IsTrue(g.Craft("rope", 20));         // 40 seconds: two half-minutes
            Assert.AreEqual(2, g.RushCost("hand"));
            Assert.IsTrue(g.RushCraft("hand"));
            Assert.AreEqual(47, g.State.coins, "the price on the button, not a flat five");

            // A finished job only waiting for room in the backpack isn't charged again.
            foreach (var s in g.State.backpack) if (s.Empty) { s.id = "stone"; s.n = 40; }
            g.Tick(0.1f);
            Assert.IsNotNull(g.Job("hand"));
            Assert.IsFalse(g.RushCraft("hand"));
            Assert.AreEqual(47, g.State.coins);
        }

        [Test]
        public void DefeatingRedJackOpensTheVoyageHome()
        {
            var g = Fresh();
            g.State.quest = Quests.All.FindIndex(q => q.Id == "redjack");
            g.Killed("redjack");
            Assert.IsTrue(g.Flag("redjack_dead"));
            Assert.AreEqual("sail", g.CurrentQuest.Id);
            g.SetFlag("ending"); g.CheckQuests();
            Assert.AreEqual("free", g.CurrentQuest.Id);
        }
    }
}
