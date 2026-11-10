using System;
using System.Collections.Generic;

namespace LastTide.Core
{
    /// <summary>One local notification: how long from now, its title and its line.</summary>
    public struct Reminder
    {
        public double In;
        public string Title, Body;
        public Reminder(double secs, string title, string body) { In = secs; Title = title; Body = body; }
    }

    public sealed partial class Session
    {
        /// <summary>
        /// What's worth a reminder while the game is closed, all on real time: the raft landing, energy full again,
        /// tomorrow's free season takes, and a nudge after a day away. Crafting runs on game time (it waits for the
        /// player), so it has none.
        /// </summary>
        public List<Reminder> ReminderPlan()
        {
            var list = new List<Reminder>();
            if (State.noReminders) return list;
            double now = RealNow();
            var t = State.travel;
            if (t != null && t.arriveAt > now + 30)
            {
                var to = World.Location(t.to);
                list.Add(new Reminder(t.arriveAt - now, "Land ho!", "Your raft has reached " + (to != null ? to.Name : "the island") + ". Time to go ashore."));
            }
            UpdateEnergy();
            if (State.energy < World.EnergyMax)
            {
                double full = NextEnergyIn + (World.EnergyMax - Math.Floor(State.energy) - 1) * World.EnergyEvery;
                if (full > 600) list.Add(new Reminder(full, "Energy is full", "Your castaway is rested: " + World.EnergyMax + " energy to sail anywhere."));
            }
            // Tomorrow's free season takes, mid-morning local time.
            double nextDay = (Today + 1) * 86400.0 - UtcOffsetSeconds;
            double takes = nextDay + 10 * 3600 - now;
            list.Add(new Reminder(takes, "Free season points", Season.TakesPerDay + " free season takes are waiting for you today."));
            if (Math.Abs(takes - 24 * 3600) > 6 * 3600) list.Add(new Reminder(24 * 3600, "The tide has turned", "New loot has washed up on the islands. Your castaway is waiting on the beach."));
            return list;
        }
    }
}
