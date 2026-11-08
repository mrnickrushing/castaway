using System.Collections.Generic;

namespace LastTide.Core
{
    /// <summary>Stacking rules over a list of slots (the backpack or the sea chest).</summary>
    public static class Inventory
    {
        public static int Count(List<Slot> slots, string id)
        {
            int n = 0;
            foreach (var s in slots) if (!s.Empty && s.id == id) n += s.n;
            return n;
        }

        /// <summary>How many of <paramref name="id"/> would fit.</summary>
        public static int Room(List<Slot> slots, string id)
        {
            var d = Content.Item(id);
            int stack = d != null ? d.Stack : 20, room = 0;
            foreach (var s in slots)
            {
                if (s.Empty) room += stack;
                else if (s.id == id) room += stack - s.n;
            }
            return room;
        }

        /// <summary>Adds up to <paramref name="n"/>; returns how many went in.</summary>
        public static int Add(List<Slot> slots, string id, int n, int wear = 0)
        {
            var d = Content.Item(id);
            int stack = d != null ? d.Stack : 20, added = 0;
            // Top up existing stacks first (tools never stack).
            if (stack > 1)
                foreach (var s in slots)
                {
                    if (n <= 0) break;
                    if (s.Empty || s.id != id || s.n >= stack) continue;
                    int k = System.Math.Min(n, stack - s.n);
                    s.n += k; n -= k; added += k;
                }
            foreach (var s in slots)
            {
                if (n <= 0) break;
                if (!s.Empty) continue;
                int k = System.Math.Min(n, stack);
                s.id = id; s.n = k; s.wear = wear; n -= k; added += k;
            }
            return added;
        }

        /// <summary>Removes up to <paramref name="n"/>; returns how many came out. Takes from the last stacks first.</summary>
        public static int Remove(List<Slot> slots, string id, int n)
        {
            int removed = 0;
            for (int i = slots.Count - 1; i >= 0 && n > 0; i--)
            {
                var s = slots[i];
                if (s.Empty || s.id != id) continue;
                int k = System.Math.Min(n, s.n);
                s.n -= k; n -= k; removed += k;
                if (s.n <= 0) s.Clear();
            }
            return removed;
        }

        public static bool Has(List<Slot> slots, IEnumerable<ItemCount> cost)
        {
            foreach (var c in cost) if (Count(slots, c.id) < c.n) return false;
            return true;
        }

        public static bool Full(List<Slot> slots)
        {
            foreach (var s in slots) if (s.Empty) return false;
            return true;
        }

        /// <summary>Moves everything from one list into another, as far as it fits; returns items moved.</summary>
        public static int MoveAll(List<Slot> from, List<Slot> to)
        {
            int moved = 0;
            foreach (var s in from)
            {
                if (s.Empty) continue;
                int k = Add(to, s.id, s.n, s.wear);
                moved += k;
                s.n -= k;
                if (s.n <= 0) s.Clear();
            }
            return moved;
        }
    }
}
