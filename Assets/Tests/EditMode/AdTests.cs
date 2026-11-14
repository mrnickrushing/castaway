using LastTide.Core;
using NUnit.Framework;

namespace LastTide.Tests
{
    public class AdTests
    {
        [Test]
        public void AnAdvertGivesAQuarterBarOfEnergy()
        {
            var g = TestGame.New();
            g.State.energy = 10; g.State.energyStamp = TestGame.Now;
            Assert.IsTrue(g.AdOffered(AdReward.Energy));
            Assert.IsTrue(g.GrantAd(AdReward.Energy));
            Assert.AreEqual(10 + Session.AdEnergy, g.State.energy, 0.01f);
            Assert.AreEqual(Session.AdsPerDay - 1, g.AdsLeft);
        }

        [Test]
        public void EnergyNeverGoesOverTheBarAndAFullBarIsNotOffered()
        {
            var g = TestGame.New();
            g.State.energy = World.EnergyMax - 5; g.State.energyStamp = TestGame.Now;
            Assert.IsTrue(g.GrantAd(AdReward.Energy));
            Assert.AreEqual(World.EnergyMax, g.State.energy, 0.01f);
            Assert.IsFalse(g.AdOffered(AdReward.Energy));
            Assert.IsFalse(g.GrantAd(AdReward.Energy));
            Assert.AreEqual(Session.AdsPerDay - 1, g.AdsLeft, "a refused advert is not counted");
        }

        [Test]
        public void FiveADayThenTomorrow()
        {
            var g = TestGame.New();
            for (int i = 0; i < Session.AdsPerDay; i++)
            {
                g.State.energy = 0; g.State.energyStamp = TestGame.Now;
                Assert.IsTrue(g.GrantAd(AdReward.Energy));
            }
            g.State.energy = 0;
            Assert.AreEqual(0, g.AdsLeft);
            Assert.IsFalse(g.GrantAd(AdReward.Energy));
            TestGame.Now += 86400;
            Assert.AreEqual(Session.AdsPerDay, g.AdsLeft);
            Assert.IsTrue(g.GrantAd(AdReward.Energy));
        }

        [Test]
        public void NothingToRecoverOrRushMeansNoOffer()
        {
            var g = TestGame.New();
            Assert.IsFalse(g.AdOffered(AdReward.Bag));
            Assert.IsFalse(g.AdOffered(AdReward.Travel));
            Assert.IsFalse(g.GrantAd(AdReward.Bag));
            Assert.AreEqual(Session.AdsPerDay, g.AdsLeft);
        }
    }
}
