using System.Collections.Generic;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// Places everything on the island from a fixed seed (so harvest keys stay the same between launches): the wreck
    /// and its crates, palms, rocks, bushes, tide pools, the spring, the camp blueprints and the boar dens.
    /// </summary>
    public static class WorldBuilder
    {
        const int Seed = 1717;
        static readonly List<Vector3> _taken = new List<Vector3>();
        static System.Random _rng;

        /// <summary>Camp structures: where each stands around the clearing, its facing, ring size and footprint, and the
        /// guide step that reveals its blueprint.</summary>
        static readonly (string id, Vector3 offset, float yaw, float ring, float footprint, string from)[] Sites =
        {
            ("campfire",    new Vector3(0, 0, 0),      0,   2.6f, 1.2f, "wood"),
            ("shelter",     new Vector3(-9, 0, 5),     160, 3.2f, 2.2f, "fiber"),
            ("workbench",   new Vector3(9, 0, 4),      200, 2.6f, 1.3f, "scrap"),
            ("raincatcher", new Vector3(-12, 0, -5),   0,   2.6f, 1.4f, "rain"),
            ("chest",       new Vector3(11, 0, -5),    210, 2.2f, 0.9f, "chest"),
        };

        public static Vector3 SitePosition(string id)
        {
            foreach (var s in Sites) if (s.id == id) return Island.Ground(Island.Camp + s.offset);
            return Island.Camp;
        }

        static float R() { return (float)_rng.NextDouble(); }

        public static void Populate(Transform world)
        {
            _rng = new System.Random(Seed);
            _taken.Clear();
            var nodes = new GameObject("Island things").transform;
            nodes.SetParent(world, false);

            // Camp blueprints.
            foreach (var s in Sites)
            {
                var p = Island.Ground(Island.Camp + s.offset);
                SiteSpot.Create(nodes, s.id, p, s.yaw, s.ring, s.footprint, s.from);
                Take(p, 4f);
            }
            Take(Island.Camp, Island.CampRadius * 0.5f);

            // The wreck on the beach, and what washed out of it.
            var wreck = Models.Wreck().ToObject("Wreck", world);
            wreck.transform.position = Island.Ground(Island.Wreck) + Vector3.down * 0.6f;
            for (int i = -3; i <= 3; i++)
            {
                var along = Quaternion.Euler(0, -28, 0) * new Vector3(0, 0, i * 3.2f);
                Obstacles.Add(Island.Wreck + along, 3.0f);
                Take(Island.Wreck + along, 4f);
            }
            Take(Island.Start, 4f);
            var hatchet = Island.Start + new Vector3(3.5f, 0, 2.5f);
            NodeSpot.Create(nodes, "hatchet_pickup", "hatchet#0", Island.Ground(hatchet), Models.Hatchet(), 1.3f, 0);
            Take(hatchet, 2f);
            int crates = 0, barrels = 0;
            foreach (var off in new[] { new Vector3(-8, 0, 6), new Vector3(-14, 0, 2), new Vector3(10, 0, 9), new Vector3(-4, 0, 12), new Vector3(16, 0, 5), new Vector3(-20, 0, 9) })
            {
                var p = Island.Wreck + off;
                if (!Island.Walkable(p.x, p.z)) continue;
                NodeSpot.Create(nodes, "crate", "crate#" + crates, Island.Ground(p), Models.Crate(crates + 1), 1.7f, 0.7f);
                Take(p, 2.5f);
                crates++;
            }
            foreach (var off in new[] { new Vector3(4, 0, 11), new Vector3(-11, 0, 12), new Vector3(20, 0, 10) })
            {
                var p = Island.Wreck + off;
                if (!Island.Walkable(p.x, p.z)) continue;
                NodeSpot.Create(nodes, "barrel", "barrel#" + barrels, Island.Ground(p), Models.Barrel(barrels + 3), 1.6f, 0.5f);
                Take(p, 2.5f);
                barrels++;
            }

            // A spring under the rocks north-west of camp.
            var spring = Find(p => Island.Distance01(p.x, p.z) < 0.5f && p.z > -40 && p.z < 10 && p.x < -10, 3.5f, new Vector3(-28, 0, -22));
            NodeSpot.Create(nodes, "spring", "spring#0", Island.Ground(spring) + Vector3.down * 0.05f, Models.Spring(), 2.4f, 0);
            Take(spring, 5f);

            // Palms: a band along the beach, groves inland.
            var decor = new MeshBuilder();
            int palms = 0;
            for (int i = 0; i < 1500 && palms < 180; i++)
            {
                var p = RandomOnIsland();
                float d = Island.Distance01(p.x, p.z);
                bool beach = d > 0.66f && d < 0.9f;
                float grove = Mathf.PerlinNoise(p.x * 0.03f + 7, p.z * 0.03f + 3);
                if (!beach && grove < 0.55f) continue;
                if (Island.Slope(p.x, p.z) > 0.35f || !Free(p, 3.2f)) continue;
                NodeSpot.Create(nodes, "palm", "palm#" + palms, Island.Ground(p), Models.Palm(palms, 6.5f + R() * 3f), 1.9f, 0.45f);
                Take(p, 3.2f);
                palms++;
            }
            Count("boulder", 16, nodes, p => Island.Slope(p.x, p.z) > 0.18f && Island.Distance01(p.x, p.z) < 0.75f, 5f,
                (k, i) => Models.Boulder(i * 1.3f, new Vector3(2.2f + R(), 1.6f + R() * 0.8f, 2f + R()), Color.Lerp(Tropic.Rock, Tropic.RockDark, R() * 0.6f)), 2.2f, 1.1f);
            Count("stones", 14, nodes, p => Island.Distance01(p.x, p.z) < 0.85f, 3f, (k, i) => Models.Stones(i), 1.6f, 0);
            Count("bush", 30, nodes, p => Island.Distance01(p.x, p.z) < 0.78f, 2.6f, (k, i) => Models.Fern(i, false), 1.6f, 0);
            Count("berrybush", 10, nodes, p => Island.Distance01(p.x, p.z) < 0.7f, 2.6f, (k, i) => Models.Fern(i + 50, true), 1.6f, 0);
            Count("driftwood", 12, nodes, p => { float d = Island.Distance01(p.x, p.z); return d > 0.82f && d < 0.96f; }, 3f, (k, i) => Models.Driftwood(i), 1.7f, 0);
            Count("tidepool", 5, nodes, p => { float d = Island.Distance01(p.x, p.z); return d > 0.9f && d < 0.98f; }, 6f, (k, i) => Models.TidePool(i), 2.2f, 0);

            // Jungle canopy inland (scenery: it blocks the way but isn't harvested).
            var canopy = new MeshBuilder();
            int trees = 0;
            for (int i = 0; i < 2500 && trees < 170; i++)
            {
                var p = RandomOnIsland();
                float d = Island.Distance01(p.x, p.z);
                float dense = Mathf.PerlinNoise(p.x * 0.025f + 21, p.z * 0.025f + 4);
                if (d > 0.68f || dense < 0.42f || Island.Slope(p.x, p.z) > 0.45f || !Free(p, 4.5f)) continue;
                if (Vector3.Distance(p, Island.Camp) < Island.CampRadius + 6) continue;
                var g = Island.Ground(p);
                canopy.Push().Translate(g + Vector3.down * 0.2f).RotateY(R() * 360);
                canopy.Append(Models.Jungle(trees, 6f + R() * 4f));
                canopy.Pop();
                Obstacles.Add(g, 0.5f);
                Take(p, 4.5f);
                trees++;
            }
            canopy.ToObject("Jungle", world);

            // Undergrowth that's just scenery: ferns, grass tufts and flowers inland, pebbles on the beach.
            for (int i = 0; i < 1600; i++)
            {
                var p = RandomOnIsland();
                float d = Island.Distance01(p.x, p.z);
                if (Island.Slope(p.x, p.z) > 0.5f || Island.Height(p.x, p.z) < 0.3f || Near(p, 1.0f)) continue;
                if (Vector3.Distance(p, Island.Camp) < Island.CampRadius * 0.6f) continue;
                var g = Island.Ground(p);
                decor.Push().Translate(g).RotateY(R() * 360).Scale(0.6f + R() * 0.7f);
                float pick = R();
                if (d < 0.74f) decor.Append(pick < 0.45f ? Models.Fern(i + 100, false) : Models.Tuft(i, pick > 0.85f));
                else if (d < 0.8f) decor.Append(Models.Tuft(i, false));
                else if (pick < 0.3f) Models.Rock(decor, new Vector3(0, 0.05f, 0), new Vector3(0.3f, 0.18f, 0.25f), i, Tropic.Rock);
                decor.Pop();
            }
            decor.ToObject("Undergrowth", world, false);

            // Boar dens in the jungle north of camp.
            var dens = new GameObject("Boars").AddComponent<BoarDens>();
            dens.transform.SetParent(world, false);
            for (int i = 0; i < 6; i++)
            {
                var p = Find(q => Island.Distance01(q.x, q.z) < 0.62f && q.z > -25 && Vector3.Distance(q, Island.Camp) > 40, 14f, new Vector3(-30 + i * 12, 0, 10));
                dens.Add(p);
                Take(p, 14f);
            }
        }

        static void Count(string kind, int n, Transform parent, System.Func<Vector3, bool> ok, float space, System.Func<string, int, MeshBuilder> model, float ring, float obstacle)
        {
            int made = 0;
            for (int i = 0; i < 2000 && made < n; i++)
            {
                var p = RandomOnIsland();
                if (!ok(p) || !Free(p, space) || !Island.Walkable(p.x, p.z)) continue;
                NodeSpot.Create(parent, kind, kind + "#" + made, Island.Ground(p), model(kind, made), ring, obstacle);
                Take(p, space);
                made++;
            }
        }

        static Vector3 RandomOnIsland()
        {
            float a = R() * Mathf.PI * 2, r = Mathf.Sqrt(R()) * Island.Radius * 1.3f;
            return new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
        }

        static Vector3 Find(System.Func<Vector3, bool> ok, float space, Vector3 fallback)
        {
            for (int i = 0; i < 3000; i++)
            {
                var p = RandomOnIsland();
                if (ok(p) && Island.Walkable(p.x, p.z) && Free(p, space)) return p;
            }
            return fallback;
        }

        static void Take(Vector3 p, float r) { _taken.Add(new Vector3(p.x, r, p.z)); }

        static bool Free(Vector3 p, float r)
        {
            if (!Island.Walkable(p.x, p.z) || Island.Height(p.x, p.z) < 0.15f) return false;
            foreach (var t in _taken)
            {
                float dx = t.x - p.x, dz = t.z - p.z, m = Mathf.Max(t.y, r) * 0.8f + Mathf.Min(t.y, r) * 0.2f;
                if (dx * dx + dz * dz < m * m) return false;
            }
            return true;
        }

        static bool Near(Vector3 p, float r)
        {
            foreach (var t in _taken)
            {
                float dx = t.x - p.x, dz = t.z - p.z, m = r + Mathf.Min(t.y, 2f);
                if (dx * dx + dz * dz < m * m) return true;
            }
            return false;
        }
    }

    /// <summary>A green arrow on the ground beside the castaway pointing the way to the guide's next goal.</summary>
    public sealed class GuideArrow : MonoBehaviour
    {
        Material _mat;

        public static GuideArrow Create(Transform parent)
        {
            var go = new GameObject("Guide arrow");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<GuideArrow>();
            var mb = new MeshBuilder();
            mb.Quad(new Vector3(-0.22f, 0, -0.5f), new Vector3(-0.22f, 0, 0.2f), new Vector3(0.22f, 0, 0.2f), new Vector3(0.22f, 0, -0.5f), Color.white);
            mb.Tri(new Vector3(-0.55f, 0, 0.2f), new Vector3(0, 0, 0.85f), new Vector3(0.55f, 0, 0.2f), Color.white);
            var child = mb.ToObject("Arrow", go.transform, false);
            a._mat = new Material(Shader.Find("LastTide/Ring"));
            a._mat.SetColor("_Color", new Color(0.32f, 1f, 0.3f, 0.95f));
            a._mat.SetFloat("_Fill", 0);
            child.GetComponent<MeshRenderer>().sharedMaterial = a._mat;
            // Ring shader shades by uv.y; a flat mid value gives a solid fill.
            var mesh = child.GetComponent<MeshFilter>().sharedMesh;
            var uv = new Vector2[mesh.vertexCount];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(0, 0.6f);
            mesh.uv = uv;
            return a;
        }

        void LateUpdate()
        {
            var p = Player.Instance;
            Vector3? target = Target(p != null ? p.transform.position : Vector3.zero);
            bool show = p != null && target.HasValue && !p.Working;
            var d = show ? target.Value - p.transform.position : Vector3.zero;
            d.y = 0;
            if (show && d.magnitude < 4.5f) show = false;
            var child = transform.GetChild(0).gameObject;
            if (child.activeSelf != show) child.SetActive(show);
            if (!show) return;
            var dir = d.normalized;
            var pos = p.transform.position + dir * (1.6f + Mathf.Sin(Time.time * 5) * 0.15f);
            transform.position = Island.Ground(pos) + Vector3.up * 0.12f;
            transform.rotation = Quaternion.LookRotation(dir);
            _mat.SetFloat("_Pulse", 0.5f + 0.5f * Mathf.Sin(Time.time * 5));
        }

        /// <summary>The nearest working spot for the current guide step (a boar for the hunt).</summary>
        static Vector3? Target(Vector3 from)
        {
            var q = G.Session.CurrentQuest;
            if (q.PointNode == "boar")
            {
                var b = Boar.Nearest(from, 400f);
                return b != null ? b.transform.position : (Vector3?)null;
            }
            string kind = q.PointBuild ?? q.PointNode;
            if (kind == null) return null;
            Spot best = null;
            float bd = float.MaxValue;
            foreach (var s in Spot.All)
            {
                if (s.Kind != kind || !s.Active) continue;
                float d = Vector3.Distance(s.Pos, from);
                if (d < bd) { bd = d; best = s; }
            }
            return best != null ? best.Pos : (Vector3?)null;
        }
    }
}
