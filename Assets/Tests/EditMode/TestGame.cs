using System;
using System.Reflection;
using LastTide.Core;

namespace LastTide.Tests
{
    /// <summary>
    /// A new game for a test that comes out the same on every run: a fixed clock (a weekday morning inside the season
    /// calendar, not the machine's) and seeded dice for loot, drops and crates. Tests that need time to pass move
    /// <see cref="Now"/> themselves.
    /// </summary>
    public static class TestGame
    {
        public static double Now;

        public static Session New(int seed = 1)
        {
            Now = (Season.EpochDay + 3) * 86400.0 + 9 * 3600;
            Session.UtcOffsetSeconds = 0;
            var g = new Session(GameState.New());
            g.RealNow = () => Now;
            return Seed(g, seed);
        }

        /// <summary>Seeds a session's dice (one that keeps its own clock).</summary>
        public static Session Seed(Session g, int seed = 1)
        {
            typeof(Session).GetField("_rng", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(g, new Random(seed));
            return g;
        }
    }
}
