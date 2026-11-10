using System.Linq;
using LastTide.Core;
using NUnit.Framework;

namespace LastTide.Tests
{
    public class NightTests
    {
        [Test]
        public void DaysTurnToNight()
        {
            var g = new Session(GameState.New());
            Assert.IsFalse(g.IsNight);
            g.State.time = Session.DayLength * 0.85;
            Assert.IsTrue(g.IsNight);
            Assert.Greater(g.AggroScale(false), 1f, "animals notice you further at night");
            Assert.Less(g.AggroScale(true), g.AggroScale(false), "sneaking helps");
        }

        [Test]
        public void TheShopsWaitUntilTheShelterStandsAndANightHasPassed()
        {
            var g = new Session(GameState.New());
            Assert.IsFalse(g.Settled);
            g.State.time = Session.DayLength * 1.2;
            Assert.IsFalse(g.Settled, "a night alone isn't enough: no roof yet");
            g.State.builds.Add(new BuildState { id = "shelter", done = true });
            Assert.IsTrue(g.Settled);
            var old = new Session(GameState.New());
            old.State.level = 3;
            Assert.IsTrue(old.Settled, "saves from before this rule keep everything");
        }

        [Test]
        public void SailorsBagsHoldLootUntilEmptied()
        {
            var g = new Session(GameState.New());
            var bag = g.Loot("bag#1", "lootbag");
            Assert.IsNotNull(bag);
            int items = bag.Count(s => !s.Empty);
            Assert.Greater(items, 0);
            Assert.AreSame(bag, g.Loot("bag#1", "lootbag"), "rolled once");
            Assert.Greater(g.TakeLoot("bag#1", "lootbag", -1), 0);
            Assert.IsFalse(g.Standing("bag#1", "lootbag"), "an emptied bag is gone for a while");
            g.Tick(Content.Nodes["lootbag"].Respawn + 1);
            Assert.IsTrue(g.Standing("bag#1", "lootbag"));
        }

        [Test]
        public void BiscuitJoins()
        {
            var g = new Session(GameState.New());
            g.State.quest = Quests.All.FindIndex(q => q.Id == "dog");
            g.AdoptPet();
            Assert.IsTrue(g.HasPet);
            Assert.AreEqual("radio", g.CurrentQuest.Id);
        }

        [Test]
        public void PlanksFromTheWorkbench()
        {
            var g = new Session(GameState.New());
            g.Give("wood", 30); g.Give("stone", 8); g.Give("scrap", 4);
            g.Deliver("workbench", 99);
            Assert.IsTrue(g.Craft("plank", 2));
            g.Tick(40);
            Assert.AreEqual(2, g.Count("plank"));
        }
    }
}
