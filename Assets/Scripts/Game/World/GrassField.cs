using System.Collections.Generic;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// Thick grass over the island's meadows: one clump of crossed cards textured with photoscanned blades
    /// (Poly Haven grass_medium_02), GPU-instanced in 16 m chunks that are drawn only when in view. Density follows the
    /// ground (Island.GrassAmount), so beaches, rock and the trodden camp stay bare.
    /// </summary>
    public sealed class GrassField : MonoBehaviour
    {
        const float Chunk = 16f;
        sealed class Cell { public Bounds Bounds; public Matrix4x4[] M; }
        readonly List<Cell> _cells = new List<Cell>();
        Mesh _mesh;
        Material _mat;
        readonly Plane[] _planes = new Plane[6];

        public static GrassField Build(Transform parent, float density, int seed)
        {
            var mat = ModelRegistry.FirstMaterial("grass_medium_02_c");
            if (mat == null) return null;
            var go = new GameObject("Grass");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<GrassField>();
            // The scanned blades are pale and dry; a fresher green to match the island's grass.
            g._mat = new Material(mat) { name = "Grass", enableInstancing = true };
            g._mat.SetColor("_BaseColor", new Color(0.72f, 0.95f, 0.55f));
            g._mesh = Clump();
            var rng = new System.Random(seed);
            float reach = Island.Radius * 1.15f;
            var list = new List<Matrix4x4>();
            for (float cz = -reach; cz < reach; cz += Chunk)
                for (float cx = -reach; cx < reach; cx += Chunk)
                {
                    list.Clear();
                    int tries = (int)(Chunk * Chunk * density);
                    for (int i = 0; i < tries && list.Count < 1000; i++)
                    {
                        float x = cx + (float)rng.NextDouble() * Chunk, z = cz + (float)rng.NextDouble() * Chunk;
                        float amount = Island.GrassAmount(x, z);
                        // Patchy: clumps thin out towards the edges of the meadows rather than stopping at a line.
                        if (amount < 0.2f || rng.NextDouble() > amount * amount) continue;
                        if (Vector3.Distance(new Vector3(x, 0, z), Island.Camp) < Island.CampRadius * 0.7f) continue;
                        var p = Island.Ground(x, z);
                        float s = 0.7f + (float)rng.NextDouble() * 0.6f;
                        list.Add(Matrix4x4.TRS(p + Vector3.down * 0.03f, Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0), new Vector3(s, s * (0.8f + amount * 0.45f), s)));
                    }
                    if (list.Count == 0) continue;
                    var b = new Bounds(new Vector3(cx + Chunk / 2, Island.Height(cx + Chunk / 2, cz + Chunk / 2), cz + Chunk / 2), new Vector3(Chunk + 2, 30, Chunk + 2));
                    g._cells.Add(new Cell { Bounds = b, M = list.ToArray() });
                }
            return g;
        }

        /// <summary>Three crossed cards, each showing the whole blade atlas; normals lean up so the blades light like the ground.</summary>
        static Mesh Clump()
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            const float w = 0.55f, h = 0.5f;
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI / 3;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var side = Vector3.Cross(Vector3.up, dir);
                int i0 = v.Count;
                v.Add(-dir * w); v.Add(dir * w); v.Add(-dir * w * 1.15f + Vector3.up * h + side * 0.06f); v.Add(dir * w * 1.15f + Vector3.up * h + side * 0.06f);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1));
                var nm = (Vector3.up * 2 + side * 0.4f).normalized;
                for (int q = 0; q < 4; q++) n.Add(nm);
                t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0 + 1, i0 + 2, i0 + 3 });
            }
            var m = new Mesh { name = "GrassClump" };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || _mat == null) return;
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            var rp = new RenderParams(_mat)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = true,
                layer = gameObject.layer,
            };
            var camPos = cam.transform.position;
            foreach (var c in _cells)
            {
                if ((c.Bounds.center - camPos).sqrMagnitude > 90 * 90) continue;
                if (!GeometryUtility.TestPlanesAABB(_planes, c.Bounds)) continue;
                rp.worldBounds = c.Bounds;
                Graphics.RenderMeshInstanced(rp, _mesh, 0, c.M);
            }
        }
    }
}
