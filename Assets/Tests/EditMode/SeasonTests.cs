using System.Linq;
using LastTide.Core;
using NUnit.Framework;

namespace LastTide.Tests
{
    public class SeasonTests
    {
        static Session At(int day, double hour = 12)
        {
            Session.UtcOffsetSeconds = 0;   // the game sets the device's zone; tests run on UTC days
            var g = new Session(GameState.New());
            double t = day * 86400.0 + hour * 3600;
            g.RealNow = () => t;
            return g;
        }

        [Test]
        public void SeasonsRunFourWeeksAndReset()
        {
            var g = At(Season.EpochDay + 3);
            Assert.AreEqual(1, g.SeasonNumber);
            Assert.AreEqual(1, g.SeasonLevel);
            g.AddXp(Season.PointsPerLevel * 2 + 10);
            Assert.AreEqual(3, g.SeasonLevel, "experience fills the season bar");
            Assert.IsNotEmpty(g.ClaimSeason(2, false));
            Assert.IsEmpty(g.ClaimSeason(2, false), "once only");
            Assert.IsEmpty(g.ClaimSeason(2, true), "premium needs the pass");
            Assert.IsEmpty(g.ClaimSeason(5, false), "not reached yet");

            // A new season: points and claims start again.
            double next = (Season.EpochDay + Season.Days + 1) * 86400.0;
            g.RealNow = () => next;
            Assert.AreEqual(2, g.SeasonNumber);
            Assert.AreEqual(1, g.SeasonLevel);
            Assert.IsFalse(g.SeasonClaimed(2, false));
        }

        [Test]
        public void PremiumPassIsForThisSeason()
        {
            var g = At(Season.EpochDay + 1);
            g.AddSeasonPoints(Season.PointsPerLevel * 9);
            Assert.IsTrue(g.GrantPurchase(Season.PremiumProduct, "tx1"));
            Assert.IsTrue(g.SeasonPremium);
            var got = g.ClaimAllSeason();
            Assert.IsTrue(got.Any(i => i.id == "pet_husky"), "level 9 premium is Nanook");
            Assert.IsTrue(g.OwnsPet("husky"));
            Assert.AreEqual("husky", g.ActivePet.Id);
            Assert.AreEqual(0, g.SeasonWaiting);
            double next = (Season.EpochDay + Season.Days + 2) * 86400.0;
            g.RealNow = () => next;
            Assert.IsFalse(g.SeasonPremium, "a new season needs a new pass");
            Assert.IsTrue(g.OwnsPet("husky"), "companions stay");
        }

        [Test]
        public void DailyTakesRefreshAtMidnight()
        {
            var g = At(Season.EpochDay + 5, 9);
            for (int i = 0; i < Season.TakesPerDay; i++) Assert.IsTrue(g.TakeSeasonBonus());
            Assert.IsFalse(g.TakeSeasonBonus());
            Assert.AreEqual(Season.TakesPerDay * Season.TakePoints, g.SeasonPoints);
            double tomorrow = (Season.EpochDay + 6) * 86400.0 + 60;
            g.RealNow = () => tomorrow;
            Assert.AreEqual(Season.TakesPerDay, g.TakesLeft);
        }

        [Test]
        public void EventsCountAndPayMilestones()
        {
            var boar = Season.Event("boar_hunt");
            int day = Season.EpochDay + boar.StartDay + 1;       // the cycle lines up with the season epoch
            day += Season.CycleDays * 3;
            var g = At(day);
            var run = g.ActiveEvents.First(r => r.Def.Id == "boar_hunt");
            Assert.IsTrue(g.EventNew(run));
            for (int i = 0; i < boar.Targets[0]; i++) g.Killed("boar");
            Assert.AreEqual(boar.Targets[0], g.EventCount(run));
            Assert.AreEqual(1, g.EventWaiting(run));
            int coins = g.State.coins;
            var got = g.ClaimEvent(run);
            Assert.IsNotEmpty(got);
            Assert.Greater(g.State.coins, coins);
            Assert.AreEqual(0, g.EventWaiting(run));
            g.Tally("gather:stone", 50);
            Assert.AreEqual(boar.Targets[0], g.EventCount(run), "other things don't count");
        }

        [Test]
        public void CalendarShowsOverlappingRuns()
        {
            var g = At(Season.EpochDay + 40);
            var runs = g.EventRuns(g.Today - 1, g.Today + 6);
            Assert.Greater(runs.Count, 2);
            Assert.IsTrue(runs.All(r => r.Start < g.Today + 6 && r.End > g.Today - 1));
            Assert.AreEqual(runs.Count, runs.Select(r => r.Key).Distinct().Count());
        }

        [Test]
        public void CratesOpenIntoSupplies()
        {
            var g = At(Season.EpochDay);
            g.Give("gold_chest", 1);
            int slot = g.State.backpack.FindIndex(s => s.id == "gold_chest");
            int coins = g.State.coins;
            var got = g.OpenCrate(slot);
            Assert.IsNotNull(got);
            Assert.GreaterOrEqual(g.State.coins - coins, 40);
            Assert.IsFalse(g.State.backpack.Any(s => s.id == "gold_chest"));
            Assert.IsNull(g.OpenCrate(0), "only crates open");
        }

        [Test]
        public void BiscuitStaysAfterANewCompanion()
        {
            var g = At(Season.EpochDay);
            g.AdoptPet();
            Assert.AreEqual("biscuit", g.ActivePet.Id);
            g.Give("pet_fox", 1);
            Assert.AreEqual("fox", g.ActivePet.Id);
            g.ChoosePet("biscuit");
            Assert.AreEqual("biscuit", g.ActivePet.Id);
            Assert.AreEqual(2, g.OwnedPets.Count);
        }
    
        [Test]
        public void CratesHoldWhatTheirOddsSay()
        {
            foreach (var id in new[] { "supply_crate", "gold_chest" })
            {
                bool gold = id == "gold_chest";
                var pool = gold ? Season.ChestLoot : Season.CrateLoot;
                string text = Season.OddsText(id);
                StringAssert.Contains("1 in " + pool.Length, text);
                StringAssert.Contains(gold ? "40-80 doubloons" : "5-15 doubloons", text);
                foreach (var e in pool) StringAssert.Contains(e.n + " " + Content.NameOf(e.id), text);
                var g = TestGame.New();
                for (int k = 0; k < 60; k++)
                {
                    foreach (var sl in g.State.backpack) sl.Clear();
                    g.Give(id, 1);
                    var got = g.OpenCrate(g.State.backpack.FindIndex(x => x.id == id));
                    Assert.AreEqual(1 + (gold ? Season.ChestDraws : Season.CrateDraws), got.Count, id);
                    int coins = got[0].n;
                    Assert.That(coins >= (gold ? Season.ChestCoinsMin : Season.CrateCoinsMin) && coins <= (gold ? Season.ChestCoinsMax : Season.CrateCoinsMax), id + " coins " + coins);
                    foreach (var d in got.Skip(1)) Assert.IsTrue(pool.Any(e => e.id == d.id && e.n == d.n), id + " gave " + d);
                }
            }
        }
    }
}
