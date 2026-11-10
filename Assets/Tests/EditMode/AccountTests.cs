using System.Linq;
using LastTide.Core;
using NUnit.Framework;

namespace LastTide.Tests
{
    /// <summary>The iCloud save never overwrites a newer island from another device; reminders fit the rules.</summary>
    public class AccountTests
    {
        static Session Fresh(double now = 1_800_000_000)
        {
            var g = new Session(GameState.New());
            g.RealNow = () => now;
            return g;
        }

        [Test]
        public void MetaRoundTrips()
        {
            var m = new CloudMeta { SavedAt = 1799999999.25, Level = 7, Device = "phone-a" };
            var back = CloudMeta.Parse(m.ToString());
            Assert.AreEqual(m.SavedAt, back.SavedAt);
            Assert.AreEqual(7, back.Level);
            Assert.AreEqual("phone-a", back.Device);
            Assert.IsNull(CloudMeta.Parse(""));
            Assert.IsNull(CloudMeta.Parse("garbage"));
        }

        [Test]
        public void ANewPhoneContinuesFromICloud()
        {
            var cloud = new CloudMeta { SavedAt = 100, Level = 9, Device = "old-phone" };
            Assert.AreEqual(CloudStep.Adopt, CloudRules.Decide(false, cloud, "new-phone", 0));
            Assert.AreEqual(CloudStep.Upload, CloudRules.Decide(false, null, "new-phone", 0), "nothing in iCloud: start and upload");
        }

        [Test]
        public void ANewerSaveFromAnotherDeviceIsOfferedNotOverwritten()
        {
            var cloud = new CloudMeta { SavedAt = 500, Level = 12, Device = "ipad" };
            Assert.AreEqual(CloudStep.Offer, CloudRules.Decide(true, cloud, "phone", 100));
            // Once taken or kept (seen), this device may write again.
            Assert.AreEqual(CloudStep.Upload, CloudRules.Decide(true, cloud, "phone", 500));
            // Its own saves never stop it.
            Assert.AreEqual(CloudStep.Upload, CloudRules.Decide(true, new CloudMeta { SavedAt = 900, Device = "phone" }, "phone", 100));
        }

        [Test]
        public void RemindersFollowTheRules()
        {
            var g = Fresh();
            g.State.energy = 10;
            g.State.energyStamp = 1_800_000_000;
            g.State.travel = new TravelState { from = "home", to = "grove", leftAt = 1_800_000_000, arriveAt = 1_800_000_000 + 300 };
            var plan = g.ReminderPlan();
            Assert.IsTrue(plan.Any(r => r.Title == "Land ho!" && r.Body.Contains("Palm Grove") && System.Math.Abs(r.In - 300) < 1));
            Assert.IsTrue(plan.Any(r => r.Title == "Energy is full" && r.In > 600));
            Assert.IsTrue(plan.All(r => r.In > 0));
            g.State.noReminders = true;
            Assert.AreEqual(0, g.ReminderPlan().Count);
        }
    }
}
