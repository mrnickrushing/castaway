using System.Linq;
using LastTide.Core;
using NUnit.Framework;

namespace LastTide.Tests
{
    /// <summary>The small things: item actions, pearls, Ned's powder, names, where things come from.</summary>
    public class DetailTests
    {
        static Session Fresh() { return new Session(GameState.New()); }

        [Test]
        public void DrinksAreDrunkAndFoodIsEaten()
        {
            Assert.IsTrue(Content.Item("water").Drunk);
            Assert.IsTrue(Content.Item("coconut").Drunk);
            Assert.IsTrue(Content.Item("rum").Drunk);
            Assert.IsFalse(Content.Item("meat").Drunk);
            Assert.IsFalse(Content.Item("stew").Drunk);
        }

        [Test]
        public void EveryItemHasADescription()
        {
            foreach (var d in Content.Items.Values)
                Assert.IsFalse(string.IsNullOrEmpty(d.Blurb), d.Id + " has no description");
        }

        [Test]
        public void PearlsSellForDoubloons()
        {
            var g = Fresh();
            g.Give("pearl", 2);
            int coins = g.State.coins;
            int slot = g.State.backpack.FindIndex(s => !s.Empty && s.id == "pearl");
            Assert.IsTrue(g.Use(slot));
            Assert.AreEqual(coins + Content.Item("pearl").Worth, g.State.coins);
            Assert.AreEqual(1, g.Count("pearl"));
        }

        [Test]
        public void NedTakesTheGunpowder()
        {
            var g = Fresh();
            g.State.quest = Quests.All.FindIndex(q => q.Id == "powder");
            g.Give("gunpowder", 8);
            g.CheckQuests();
            Assert.AreEqual("redjack", Quests.All[g.State.quest].Id);
            Assert.AreEqual(2, g.Count("gunpowder"), "six kegs handed over, the rest kept");
        }

        [Test]
        public void NamesAreTidied()
        {
            Assert.AreEqual("Anne Bonny", Session.CleanName("  Anne   Bonny "));
            Assert.AreEqual("bJackb", Session.CleanName("<b>Jack</b>"), "tags lose their brackets, no rich text gets through");
            Assert.AreEqual(Session.NameMax, Session.CleanName(new string('a', 40)).Length);
            var g = Fresh();
            Assert.AreEqual("Castaway", g.DisplayName);
            Assert.IsNotNull(g.Rename(" ! "));
            Assert.IsNull(g.Rename("Mary O'Reilly"));
            Assert.AreEqual("Mary O'Reilly", g.DisplayName);
        }

        [Test]
        public void EveryIngredientSaysWhereItComesFrom()
        {
            foreach (var id in Content.Recipes.Values.SelectMany(r => r.Inputs).Select(c => c.id).Distinct())
                Assert.IsNotNull(Content.Origin(id), id + " has no source");
            Assert.AreEqual("Made at the Furnace", Content.Origin("iron_bar"));
        }

        /// <summary>No circles: iron ore needs a pickaxe, and the first pickaxe is made by hand from wood, stone and fiber.</summary>
        [Test]
        public void TheFirstPickaxeNeedsNoIron()
        {
            var r = Content.Recipes["pickaxe"];
            Assert.AreEqual("hand", r.Station);
            Assert.IsFalse(r.Inputs.Any(c => c.id == "iron_bar" || c.id == "iron_ore"));
            Assert.AreEqual(ToolUse.Mine, Content.Item("pickaxe").Use);
            StringAssert.Contains("pickaxe: craft one (CRAFT)", Content.Origin("iron_ore"));
            var g = Fresh();
            StringAssert.Contains("craft one (CRAFT)", g.Blocked("ironvein"));
        }

        /// <summary>Every item says where it comes from, the harpoon who teaches it, rum every place it's found.</summary>
        [Test]
        public void EveryItemSaysWhereItComesFrom()
        {
            foreach (var d in Content.Items.Values)
                Assert.IsNotNull(Content.Origin(d.Id), d.Id + " has no source");
            StringAssert.Contains("Mara", Content.Origin("harpoon"));
            StringAssert.Contains("Smuggler's Cave", Content.Origin("rum"));
            StringAssert.Contains("Found on", Content.Origin("pearl"));
            StringAssert.Contains("season", Content.Origin("gold_chest"));
        }

        [Test]
        public void FullMetersDontWasteFoodOrBandages()
        {
            var g = Fresh();
            g.State.backpack.Clear();
            g.State.backpack.Add(new Slot { id = "bandage", n = 2 });
            g.State.backpack.Add(new Slot { id = "water", n = 1 });
            g.State.backpack.Add(new Slot { id = "raw_meat", n = 1 });
            g.State.health = 100; g.State.thirst = 100; g.State.hunger = 100;
            Assert.AreEqual("Health is already full", g.Wasted(0));
            Assert.AreEqual("Not thirsty", g.Wasted(1));
            Assert.IsNull(g.Wasted(2), "raw meat only hurts: still allowed");
            g.State.health = 60;
            Assert.IsNull(g.Wasted(0));
        }
    }
}
