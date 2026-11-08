using System.Collections.Generic;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>Round things the player and animals can't walk through (tree trunks, rocks, buildings), in a coarse grid.</summary>
    public static class Obstacles
    {
        const float Cell = 8f;
        struct Ob { public Vector2 P; public float R; public object Owner; }
        static readonly Dictionary<long, List<Ob>> _grid = new Dictionary<long, List<Ob>>();

        static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        public static void Clear() { _grid.Clear(); }

        public static void Add(Vector3 p, float r, object owner = null)
        {
            int cx = Mathf.FloorToInt(p.x / Cell), cz = Mathf.FloorToInt(p.z / Cell);
            List<Ob> list;
            if (!_grid.TryGetValue(Key(cx, cz), out list)) _grid[Key(cx, cz)] = list = new List<Ob>();
            list.Add(new Ob { P = new Vector2(p.x, p.z), R = r, Owner = owner });
        }

        public static void Remove(object owner)
        {
            foreach (var list in _grid.Values) list.RemoveAll(o => o.Owner == owner);
        }

        /// <summary>Pushes a circle of radius <paramref name="r"/> at <paramref name="p"/> out of every obstacle it overlaps.</summary>
        public static Vector3 Resolve(Vector3 p, float r)
        {
            var q = new Vector2(p.x, p.z);
            int cx = Mathf.FloorToInt(q.x / Cell), cz = Mathf.FloorToInt(q.y / Cell);
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    List<Ob> list;
                    if (!_grid.TryGetValue(Key(cx + i, cz + j), out list)) continue;
                    foreach (var o in list)
                    {
                        var d = q - o.P;
                        float min = r + o.R, m = d.magnitude;
                        if (m >= min) continue;
                        if (m < 1e-4f) d = Vector2.right; else d /= m;
                        q = o.P + d * min;
                    }
                }
            return new Vector3(q.x, p.y, q.y);
        }
    }
}
