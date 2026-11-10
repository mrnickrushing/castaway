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

        /// <summary>True if an axis-aligned rectangle (centre, half sizes) overlaps an obstacle that <paramref name="skip"/>
        /// doesn't rule out.</summary>
        public static bool Overlaps(Vector3 c, float hx, float hz, System.Predicate<object> skip)
        {
            int x0 = Mathf.FloorToInt((c.x - hx) / Cell) - 1, x1 = Mathf.FloorToInt((c.x + hx) / Cell) + 1;
            int z0 = Mathf.FloorToInt((c.z - hz) / Cell) - 1, z1 = Mathf.FloorToInt((c.z + hz) / Cell) + 1;
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    List<Ob> list;
                    if (!_grid.TryGetValue(Key(x, z), out list)) continue;
                    foreach (var o in list)
                        if ((skip == null || !skip(o.Owner)) && RectTouches(c, hx, hz, o.P, o.R)) return true;
                }
            return false;
        }

        public static bool RectTouches(Vector3 c, float hx, float hz, Vector2 p, float r)
        {
            float dx = Mathf.Max(Mathf.Abs(p.x - c.x) - hx, 0), dz = Mathf.Max(Mathf.Abs(p.y - c.z) - hz, 0);
            return dx * dx + dz * dz < r * r;
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
