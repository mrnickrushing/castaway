using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// Gulls wheeling over the island (Resources/Models/Creatures/gull, wings beaten by <see cref="CreatureArt.Wings"/>):
    /// each circles a spot along the shore, banking into the turn, drifts to another spot now and then, and flaps in
    /// short bursts with long glides between. Pure scenery: no rings, no obstacles.
    /// </summary>
    public sealed class Gulls : MonoBehaviour
    {
        sealed class Bird
        {
            public Transform T;
            public CreatureArt Art;
            public Vector3 Centre, Goal;
            public float Angle, Radius, Height, Speed, Dir, Beat, Flap, FlapFor, GlideFor, Retarget, Y;
        }

        Bird[] _birds;

        public static Gulls Create(Transform world, int count, int seed)
        {
            if (count <= 0 || ModelRegistry.Prefab(ModelRegistry.Creatures, "gull") == null) return null;
            var go = new GameObject("Gulls");
            go.transform.SetParent(world, false);
            var g = go.AddComponent<Gulls>();
            var rng = new System.Random(seed);
            System.Func<float> r = () => (float)rng.NextDouble();
            g._birds = new Bird[count];
            for (int i = 0; i < count; i++)
            {
                var holder = new GameObject("Gull " + i).transform;
                holder.SetParent(go.transform, false);
                var art = CreatureArt.TryCreate("gull", holder);
                if (art == null) { Destroy(go); return null; }
                holder.localScale = Vector3.one * (0.85f + r() * 0.3f);
                var b = new Bird
                {
                    T = holder, Art = art, Dir = r() < 0.5f ? -1 : 1, Angle = r() * Mathf.PI * 2, Radius = 7 + r() * 9,
                    Height = 6 + r() * 4, Speed = 6 + r() * 2.5f, Beat = r() * Mathf.PI * 2, GlideFor = r() * 3, Retarget = 10 + r() * 30,
                };
                b.Centre = b.Goal = ShoreSpot(r(), 0.75f + r() * 0.3f);
                b.Y = b.Height;
                g._birds[i] = b;
            }
            return g;
        }

        /// <summary>A point around the coast: u picks the direction (0..1 of a turn), k the fraction of the way out.</summary>
        static Vector3 ShoreSpot(float u, float k)
        {
            float a = u * Mathf.PI * 2;
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            return d * Island.Shore(d.x, d.z) * k;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            foreach (var b in _birds)
            {
                b.Retarget -= dt;
                if (b.Retarget <= 0) { b.Goal = ShoreSpot(Random.value, Random.Range(0.7f, 1.05f)); b.Retarget = 20 + Random.value * 25; }
                b.Centre = Vector3.MoveTowards(b.Centre, b.Goal, dt * 2.5f);
                // A few strokes, then a long glide.
                if (b.FlapFor > 0) { b.FlapFor -= dt; if (b.FlapFor <= 0) b.GlideFor = 2 + Random.value * 3; }
                else { b.GlideFor -= dt; if (b.GlideFor <= 0) b.FlapFor = 0.8f + Random.value * 1.4f; }
                b.Flap = Mathf.MoveTowards(b.Flap, b.FlapFor > 0 ? 1 : 0, dt * 3);
                b.Beat += dt * 11 * b.Flap;
                b.Angle += b.Dir * b.Speed / b.Radius * dt;
                var p = b.Centre + new Vector3(Mathf.Cos(b.Angle), 0, Mathf.Sin(b.Angle)) * b.Radius;
                // Keep clear of the hills, easing up and down rather than snapping.
                float want = Mathf.Max(b.Height, Island.Height(p.x, p.z) + 5);
                b.Y = Mathf.Lerp(b.Y, want, dt * 1.2f);
                p.y = b.Y + Mathf.Sin(b.Beat) * 0.1f * b.Flap;
                var heading = new Vector3(-Mathf.Sin(b.Angle), 0, Mathf.Cos(b.Angle)) * b.Dir;
                b.T.SetPositionAndRotation(p, Quaternion.LookRotation(heading) * Quaternion.Euler(0, 0, b.Dir * 18));
                b.Art.Wings(b.Beat, b.Flap);
            }
        }
    }
}
