using System.Linq;
using LastTide.Core;
using NUnit.Framework;

namespace LastTide.Tests
{
    /// <summary>Rewards wait for room in the backpack; paid-for things wait and drop in; every fall leaves its own bag.</summary>
    public class RoomTests
    {
        static Session Fresh() { return TestGame.New(); }   // fixed clock, seeded dice

        /// <summary>Every slot holds a spear (they never stack): nothing else fits.</summary>
        static void Fill(Session g) { foreach (var s in g.State.backpack) { s.id = "spear"; s.n = 1; s.wear = 0; } }

        /// <summary>Throws away <paramref name="n"/> of the filler spears, from the end of the backpack.</summary>
        static void FreeSlots(Session g, int n)
        {
            for (int i = g.State.backpack.Count - 1; i >= 0 && n > 0; i--)
                if (g.State.backpack[i].id == "spear") { g.State.backpack[i].Clear(); n--; }
        }

        [Test]
        public void PathRewardWaitsUntilThereIsRoom()
        {
            var g = Fresh();
            int step = Quests.All.FindIndex(q => q.Reward.Any(r => r.id != "doubloon"));
            Assert.GreaterOrEqual(step, 0);
            g.State.quest = step + 1;
            Fill(g);
            Assert.IsFalse(g.Claim(step), "no room: not taken");
            Assert.IsFalse(g.Claimed(Quests.All[step]), "still there to claim");
            FreeSlots(g, 5);
            Assert.IsTrue(g.Claim(step));
            Assert.IsTrue(g.Claimed(Quests.All[step]));
        }

        [Test]
        public void LevelRewardsWaitUntilThereIsRoom()
        {
            var g = Fresh();
            g.State.level = 4;
            Fill(g);
            Assert.AreEqual(0, g.ClaimLevelRewards().Count);
            Assert.AreEqual(3, g.LevelRewardsWaiting);
            FreeSlots(g, 1);
            Assert.AreEqual(1, g.ClaimLevelRewards().Count, "as many as fit");
            FreeSlots(g, 5);
            Assert.AreEqual(2, g.ClaimLevelRewards().Count);
            Assert.AreEqual(0, g.LevelRewardsWaiting);
        }

        [Test]
        public void SeasonRewardWaitsUntilThereIsRoom()
        {
            var g = Fresh();
            int lv = Enumerable.Range(1, Season.Levels).First(l => Season.Free[l].id != "doubloon" && !Season.Free[l].id.StartsWith("pet_"));
            g.AddSeasonPoints(lv * 1000);
            Assert.IsTrue(g.SeasonClaimable(lv, false));
            Fill(g);
            Assert.AreEqual(0, g.ClaimSeason(lv, false).Count);
            Assert.IsTrue(g.SeasonClaimable(lv, false), "still on the track");
            FreeSlots(g, 3);
            Assert.AreEqual(1, g.ClaimSeason(lv, false).Count);
        }

        [Test]
        public void ACrateStaysShutWhenItsInsidesDontFit()
        {
            var g = Fresh();
            Fill(g);
            var s = g.State.backpack[0];
            s.id = "supply_crate"; s.n = 1;
            var got = g.OpenCrate(0);
            Assert.IsNotNull(got);
            Assert.AreEqual(0, got.Count);
            Assert.AreEqual("supply_crate", g.State.backpack[0].id, "still in the backpack");
            FreeSlots(g, 5);
            Assert.Greater(g.OpenCrate(0).Count, 0);
            Assert.AreEqual(0, g.Count("supply_crate"));
        }

        [Test]
        public void TradingPostWontSellWhatDoesntFit()
        {
            var g = Fresh();
            var o = World.Offers.First(x => x.Items.Any(i => i.id != "doubloon"));
            g.State.coins = 100000;
            Fill(g);
            Assert.AreEqual("Make room in your backpack", g.BuyBlocked(o));
            Assert.IsFalse(g.Buy(o));
            Assert.AreEqual(100000, g.State.coins, "nothing spent");
        }

        [Test]
        public void APurchaseThatDoesntFitWaitsAndDropsIn()
        {
            var g = Fresh();
            Fill(g);
            Assert.IsTrue(g.GrantPurchase("lasttide.captain", "tx"));
            Assert.AreEqual(2, g.State.waiting.Sum(w => w.n), "cutlass and harpoon wait");
            g.Tick(1.5f);
            Assert.AreEqual(2, g.State.waiting.Count, "still no room");
            FreeSlots(g, 1);
            g.Tick(1.5f);
            Assert.AreEqual(1, g.State.waiting.Sum(w => w.n), "one fitted");
            FreeSlots(g, 3);
            g.Tick(1.5f);
            Assert.AreEqual(0, g.State.waiting.Count);
            Assert.AreEqual(1, g.Count("cutlass"));
            Assert.AreEqual(1, g.Count("harpoon"));
        }

        [Test]
        public void EveryFallLeavesItsOwnBag()
        {
            var g = Fresh();
            Inventory.Add(g.State.backpack.GetRange(Content.PocketSlots, 5), "wood", 10);
            g.State.px = 1; g.Damage(500);
            Inventory.Add(g.State.backpack.GetRange(Content.PocketSlots, 5), "stone", 7);
            g.State.px = 2; g.Damage(500);
            Assert.AreEqual(2, g.State.lostBags.Count, "the first bag is still there");
            Assert.AreEqual(2, g.NewestBag.x);
            Assert.AreEqual(7, g.TakeBag(), "the newest by default");
            Assert.AreEqual(1, g.State.lostBags.Count);
            Assert.AreEqual(10, g.TakeBag(g.State.lostBags[0]));
            Assert.AreEqual(0, g.State.lostBags.Count);
            Assert.AreEqual(10, g.Count("wood"));
            Assert.AreEqual(7, g.Count("stone"));
        }

        [Test]
        public void AnOldSavesBagIsKept()
        {
            var st = GameState.New();
            st.lostBag = new LostBag { x = 3, location = World.HomeId };
            st.lostBag.slots.Add(new Slot { id = "rope", n = 4 });
            var g = new Session(st);
            Assert.IsNull(g.State.lostBag);
            Assert.AreEqual(1, g.State.lostBags.Count);
            Assert.AreEqual(4, g.TakeBag());
        }
    }
}
