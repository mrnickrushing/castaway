using System.Linq;
using LastTide.Core;
using NUnit.Framework;

namespace LastTide.Tests
{
    public class IslandTests
    {
        static Session Fresh() { return TestGame.New(); }   // fixed clock, seeded dice

        [Test]
        public void EveryIslandHasItsOwnMaterialPlanAndImprovement()
        {
            var away = World.Locations.Where(l => !l.Home).ToList();
            Assert.AreEqual(away.Count, Content.Islands.Count, "one for every island");
            foreach (var i in Content.Islands)
            {
                var l = World.Location(i.Location);
                Assert.IsNotNull(l, i.Location);
                Assert.IsTrue(l.Nodes.Any(n => n.kind == i.Node), i.Location + " grows its own material");
                Assert.IsTrue(Content.Nodes[i.Node].Yield.Any(y => y.id == i.Ingredient), i.Node);
                Assert.Contains(i.Ingredient, l.PossibleLoot, "and its sailors' bags hold it");
                Assert.IsFalse(World.Locations.Any(o => o.Id != l.Id && o.Nodes.Any(n => n.kind == i.Node)), i.Node + " grows nowhere else");
                var r = Content.Recipes[i.Perk];
                Assert.AreEqual("workbench", r.Station);
                Assert.IsTrue(r.Inputs.Any(c => c.id == i.Ingredient), i.Perk + " is built from the island's material");
                Assert.IsNotEmpty(i.PlanStory); Assert.IsNotEmpty(i.Effect);
                StringAssert.Contains(l.Name, Content.Origin(i.Ingredient), "the item card says where it comes from");
            }
        }

        [Test]
        public void APlanTeachesItsImprovementWhichIsBuiltOnce()
        {
            var g = Fresh();
            g.State.builds.Add(new BuildState { id = "workbench", done = true });
            g.Give("ship_timber", 8); g.Give("nails", 16); g.Give("rope", 4);
            StringAssert.Contains("Driftwood Shore", g.CraftBlocked("perk_chest", 1));
            Assert.IsFalse(g.Craft("perk_chest", 1));
            Assert.IsTrue(g.FindPlan("grove"));
            Assert.IsNotNull(g.CraftBlocked("perk_chest", 1), "another island's plan teaches something else");
            Assert.IsTrue(g.FindPlan("shore"));
            Assert.IsFalse(g.FindPlan("shore"), "found once");
            Assert.AreEqual(1, g.MaxCraft("perk_chest"));
            Assert.IsFalse(g.Craft("perk_chest", 2));
            Assert.IsTrue(g.Craft("perk_chest", 1));
            int slots = g.State.chest.Count;
            g.Tick(60);
            Assert.IsTrue(g.HasPerk("perk_chest"));
            Assert.AreEqual(slots + Session.ChestBrace, g.State.chest.Count, "six more slots");
            Assert.AreEqual(0, g.Count("perk_chest"), "an improvement isn't an item");
            Assert.AreEqual(4, g.Count("ship_timber"));
            Assert.AreEqual("Already built at camp", g.CraftBlocked("perk_chest", 1));
            g.State.Repair();
            Assert.AreEqual(slots + Session.ChestBrace, g.State.chest.Count, "kept through a save");
        }

        [Test]
        public void ImprovementsChangeTheRules()
        {
            var g = Fresh();
            int run = g.RunCost(World.Location("pirates"));
            g.State.time = Session.DayLength * 0.85;
            float night = g.AggroScale(false);
            g.State.hunger = 50; g.Give("meat", 1);
            g.Use(g.State.backpack.FindIndex(s => s.id == "meat"));
            float plain = g.State.hunger - 50;

            foreach (var p in new[] { "perk_sail", "perk_lamp", "perk_larder" }) g.SetFlag(p);
            Assert.Less(g.RunCost(World.Location("pirates")), run, "the brass rigging saves energy");
            Assert.Less(g.AggroScale(false), night, "the lamp keeps the night calmer");
            g.State.hunger = 50; g.Give("meat", 1);
            g.Use(g.State.backpack.FindIndex(s => s.id == "meat"));
            Assert.AreEqual(plain * 1.2f, g.State.hunger - 50, 0.01f, "honey larder: food goes further");

            var a = Fresh(); var b = Fresh(); b.SetFlag("perk_salt");
            a.Tick(30); b.Tick(30);
            Assert.Greater(b.State.hunger, a.State.hunger, "salted rations");
        }

        [Test]
        public void ATripSummaryNamesThePlanFoundAndTheIslandsMaterial()
        {
            double now = 1000000;
            var g = TestGame.Seed(new Session(GameState.New())); g.RealNow = () => now;
            g.State.level = 10;
            g.State.builds.Add(new BuildState { id = "workbench", done = true });
            Assert.IsTrue(g.Travel(World.Location("grove"), true)); now += 600; g.Arrive();
            Assert.IsTrue(g.FindPlan("grove"));
            g.Give("honey", 5); g.Give("wood", 12); g.Give("rope", 3);
            g.UpdateEnergy(); g.State.energy = World.EnergyMax;
            Assert.IsTrue(g.Travel(World.Location(World.HomeId), true)); now += 600; g.Arrive();
            var t = g.LastTrip;
            Assert.IsNotNull(t);
            CollectionAssert.AreEqual(new[] { "grove" }, t.plans);
            CollectionAssert.DoesNotContain(t.learned, "perk_larder", "told as a plan found, not a recipe learned");
            Assert.AreEqual(5, t.gained.Find(x => x.id == "honey").n);
            CollectionAssert.Contains(g.NowPossible(5), "Build the Honey Larder at the workbench");

            // Old trip logs (before the plans) load with empty lists, not nulls.
            g.State.lastTrip.plans = null; g.State.lastTrip.perks = null; g.State.lastTrip.perksBefore = null;
            g.State.Repair();
            Assert.IsNotNull(g.State.lastTrip.plans); Assert.IsNotNull(g.State.lastTrip.perks);
        }
    }
}
