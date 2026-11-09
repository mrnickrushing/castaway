using LastTide.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastTide.Game
{
    /// <summary>
    /// The ground of the island the castaway is on: a height function (sand beaches, a grassy interior, rock where
    /// the location calls for it), the terrain mesh painted with the photo-texture layers, and the sea around it.
    /// Home is Wreck Cove; every other location on the map is its own smaller island, shaped by its biome.
    /// Everything that stands on the island reads <see cref="Height"/>.
    /// </summary>
    public static class Island
    {
        public const float Sea = 0f;           // water level
        const float Cell = 2f;

        public static LocationDef Location { get; private set; } = World.Location(World.HomeId);
        public static Biome Biome { get { return Location.Biome; } }
        /// <summary>Average shoreline distance from the centre.</summary>
        public static float Radius { get; private set; } = 105f;
        /// <summary>The terrain mesh covers [-Half, Half].</summary>
        public static float Half { get; private set; } = 170f;
        /// <summary>Home: the camp clearing above the wreck beach. Elsewhere: the landing where the raft comes ashore.</summary>
        public static Vector3 Camp { get; private set; } = new Vector3(0, 0, -58);
        public static float CampRadius { get; private set; } = 20f;
        /// <summary>Where the ship ran aground (home only).</summary>
        public static readonly Vector3 Wreck = new Vector3(19, 0, -93);
        /// <summary>Where the castaway stands on arrival.</summary>
        public static Vector3 Start { get; private set; } = new Vector3(2, 0, -79);
        static float _sx, _sz;

        /// <summary>Shapes the island for a location (call before building anything).</summary>
        public static void Configure(LocationDef l)
        {
            Location = l;
            if (l.Home)
            {
                Radius = 105; Half = 170; Camp = new Vector3(0, 0, -58); CampRadius = 20; Start = new Vector3(2, 0, -79); _sx = _sz = 0;
                return;
            }
            Radius = l.Size;
            Half = Mathf.Ceil((Radius * 1.45f + 16) / Cell) * Cell;
            int h = l.Id.GetHashCode();
            _sx = (h & 0xFF) * 0.37f;
            _sz = ((h >> 8) & 0xFF) * 0.41f;
            // The landing: on the south beach, where the sand starts.
            float z = -Radius * 0.4f;
            while (z > -Radius * 1.4f && Distance01(0, z) < 0.8f) z -= 0.5f;
            Camp = new Vector3(0, 0, z + 3);
            CampRadius = 8;
            Start = new Vector3(0, 0, z + 1);
        }

        /// <summary>The lighthouse, cave mouth or pirate camp on a location island.</summary>
        public static Vector3 Landmark
        {
            get
            {
                switch (Biome)
                {
                    case Biome.Lighthouse: return new Vector3(0, 0, Radius * 0.58f);
                    case Biome.Cave: return new Vector3(0, 0, Radius * 0.22f);
                    case Biome.PirateCamp: return new Vector3(0, 0, Radius * 0.05f);
                    case Biome.Pier: return new Vector3(Radius * 0.55f, 0, -Radius * 0.35f);
                    default: return Vector3.zero;
                }
            }
        }

        /// <summary>Shoreline distance in the direction of (x, z): a wobbly coast with a few headlands and coves.</summary>
        public static float Shore(float x, float z)
        {
            float a = Mathf.Atan2(z, x);
            float n = Mathf.PerlinNoise(Mathf.Cos(a) * 1.6f + 10 + _sx, Mathf.Sin(a) * 1.6f + 10 + _sz) - 0.5f;
            float m = Mathf.PerlinNoise(Mathf.Cos(a) * 4f + 30 + _sz, Mathf.Sin(a) * 4f + 30 + _sx) - 0.5f;
            float r = Radius * (1 + n * (Location.Home ? 0.45f : 0.3f) + m * 0.12f);
            // Each island its own outline (angle 0 = east, 90 = north).
            switch (Biome)
            {
                case Biome.Shore: r *= 1 + 0.38f * Mathf.Cos(2 * a); break;                               // a long sandbar, east to west
                case Biome.Lighthouse: r *= 1 + 0.42f * Bump(a, 90, 26) - 0.12f * Bump(a, 90, 70); break; // a narrow point running out north
                case Biome.PirateCamp: r *= 1 - 0.42f * Bump(a, 72, 24); break;                           // a sheltered bay for Jack's ship
                case Biome.Ridge: r *= 1 + 0.18f * Mathf.Cos(2 * a - 0.3f); break;                       // stretched along its spine
            }
            return r;
        }

        /// <summary>1 at <paramref name="deg"/> (degrees), fading to 0 about <paramref name="width"/> degrees either side.</summary>
        static float Bump(float a, float deg, float width)
        {
            float d = Mathf.DeltaAngle(a * Mathf.Rad2Deg, deg) / width;
            return Mathf.Exp(-d * d);
        }

        /// <summary>Palm Grove's lagoon: (centre, radius) of the pool of sea water inside the island.</summary>
        static Vector3 Lagoon { get { return new Vector3(Radius * 0.18f, 0, Radius * 0.22f); } }
        static float LagoonRadius { get { return Radius * 0.24f; } }

        /// <summary>0 at the island centre, 1 at the waterline, more out at sea.</summary>
        public static float Distance01(float x, float z) { return Mathf.Sqrt(x * x + z * z) / Shore(x, z); }

        public static float Height(float x, float z)
        {
            float d = Distance01(x, z);
            // Profile: a gentle rise inland, a beach that slopes into the water, then the sea floor.
            float h;
            if (d < 0.78f) h = Mathf.Lerp(3.2f, 1.6f, d / 0.78f);
            else if (d < 1f) h = Mathf.Lerp(1.6f, -0.35f, Smooth((d - 0.78f) / 0.22f));
            else h = Mathf.Lerp(-0.35f, -7f, Mathf.Clamp01((d - 1f) / 0.35f));
            float land = Mathf.Clamp01((0.92f - d) / 0.25f);
            if (Biome == Biome.Shore) h = h * 0.55f + 0.4f;
            // Rolling hills inland.
            h += (Noise2.Fbm(x * 0.018f + 3 + _sx, z * 0.018f + 7 + _sz, 3) - 0.5f) * 5f * land;
            float ridge = RidgeMask(x, z, d);
            float crag = Noise2.Ridged(x * 0.03f + 11 + _sx, z * 0.03f + 5 + _sz, 4);
            if (Location.Home)
            {
                // The rocky ridge across the north, highest in the north-west, with a pass to the east.
                float pass = 1 - Mathf.Exp(-Mathf.Pow((x - 34) / 16f, 2));
                h += ridge * ridge * (6f + crag * 16f) * Mathf.Lerp(0.35f, 1f, pass);
            }
            else h += ridge * ridge * (5f + crag * 12f);
            // Small rock outcrops through the jungle.
            h += Mathf.Max(0, Noise2.Ridged(x * 0.06f + 40 + _sx, z * 0.06f - 9 + _sz, 2) - 0.62f) * 14f * land * (1 - ridge);
            // Palm Grove: a lagoon of clear water ringed with sand in the middle of the island.
            if (Biome == Biome.Grove)
            {
                float lg = Vector2.Distance(new Vector2(x, z), new Vector2(Lagoon.x, Lagoon.z)) / LagoonRadius;
                // Wide, gentle banks: sand down to the water, not a rock-walled crater.
                float k = 1 - Smooth(Mathf.Clamp01((lg - 0.45f) / 1.4f));
                h = Mathf.Lerp(h, -1.2f, k);
            }
            // Limestone Ridge: a second, higher peak at the west end of the spine.
            if (Biome == Biome.Ridge)
            {
                float pk = Vector2.Distance(new Vector2(x, z), new Vector2(-Radius * 0.45f, Radius * 0.2f)) / (Radius * 0.3f);
                h += Mathf.Exp(-pk * pk * 2.2f) * (7f + crag * 6f) * land;
            }
            // A plateau for the lighthouse.
            if (Biome == Biome.Lighthouse)
            {
                float lh = Vector2.Distance(new Vector2(x, z), new Vector2(Landmark.x, Landmark.z));
                h = Mathf.Lerp(h, 5.5f, 1 - Smooth(Mathf.Clamp01((lh - 9) / 10f)));
            }
            // Flat camp clearing.
            float c = Vector2.Distance(new Vector2(x, z), new Vector2(Camp.x, Camp.z));
            float flat = 1 - Smooth(Mathf.Clamp01((c - CampRadius) / 12f));
            h = Mathf.Lerp(h, Location.Home ? 2.1f : 1.4f, flat);
            return h;
        }

        /// <summary>Where the land rises into rock, by biome: home's northern ridge, a spine across the middle of Limestone
        /// Ridge (with a pass in the middle), a cliff wall behind the smugglers' cave and the lighthouse point.</summary>
        static float RidgeMask(float x, float z, float d)
        {
            float inland = Mathf.Clamp01((0.95f - d) / 0.3f);
            switch (Biome)
            {
                case Biome.Home: return Mathf.Clamp01((z - 18f) / 40f) * inland;
                case Biome.Ridge:
                {
                    float band = 1 - Mathf.Clamp01(Mathf.Abs(z - Radius * 0.15f) / (Radius * 0.3f));
                    float pass = 1 - Mathf.Exp(-Mathf.Pow(x / 9f, 2));
                    return band * inland * pass;
                }
                case Biome.Cave:
                {
                    // Cliffs north of the cave mouth, with the mouth itself cut into them.
                    float wall = Mathf.Clamp01((z - Radius * 0.28f) / (Radius * 0.2f)) * inland;
                    return wall;
                }
                case Biome.Lighthouse: return Mathf.Clamp01((z - Radius * 0.2f) / (Radius * 0.4f)) * inland * 0.5f;
                default: return 0;
            }
        }

        static float Smooth(float t) { return t * t * (3 - 2 * t); }

        /// <summary>True on land the player can stand on (dry sand and up, not steep rock).</summary>
        public static bool Walkable(float x, float z)
        {
            if (WorldBuilder.OnPier(x, z)) return true;
            float h = Height(x, z);
            if (h < -0.25f) return false;
            float s = Slope(x, z);
            return s < 0.62f;
        }

        public static float Slope(float x, float z)
        {
            const float e = 1f;
            float dx = Height(x + e, z) - Height(x - e, z), dz = Height(x, z + e) - Height(x, z - e);
            return Mathf.Sqrt(dx * dx + dz * dz) / (2 * e);
        }

        public static Vector3 Ground(Vector3 p)
        {
            float y = Mathf.Max(Height(p.x, p.z), Sea - 0.3f);
            if (WorldBuilder.OnPier(p.x, p.z)) y = Mathf.Max(y, PierDeck);
            return new Vector3(p.x, y, p.z);
        }

        public const float PierDeck = 0.95f;
        public static Vector3 Ground(float x, float z) { return Ground(new Vector3(x, 0, z)); }

        static readonly float[] _gw = new float[6];

        /// <summary>How grassy the ground is here (0 on sand, rock and the trodden camp, 1 in the meadows).</summary>
        public static float GrassAmount(float x, float z)
        {
            float h = Height(x, z);
            if (h < 0.4f) return 0;
            Weights(x, z, h, Slope(x, z), _gw);
            return _gw[0] + _gw[1] * 0.6f;
        }

        /// <summary>Layer weights for LastTide/Terrain: grass, dry grass, dirt, sand, rock, gravel.</summary>
        static void Weights(float x, float z, float h, float slope, float[] w)
        {
            for (int i = 0; i < 6; i++) w[i] = 0;
            float d = Distance01(x, z);
            float n = Mathf.PerlinNoise(x * 0.07f + 3, z * 0.07f + 9);
            float sand = Mathf.Clamp01((d - 0.74f - n * 0.06f) / 0.06f);
            sand = Mathf.Max(sand, Mathf.Clamp01((1.3f - h) / 0.5f));
            float rock = Mathf.Clamp01((slope - 0.38f) / 0.22f);
            // Offsets off the noise lattice: at a whole-number input Unity's Perlin sits flat, which drew a straight
            // dry/green border down the middle of every island.
            float dry = Mathf.Clamp01((Mathf.PerlinNoise(x * 0.017f + 50.37f, z * 0.019f + 13.71f) - 0.45f) * 3f);
            float camp = Vector2.Distance(new Vector2(x, z), new Vector2(Camp.x, Camp.z));
            float dirt = Mathf.Clamp01((CampRadius * 0.75f - camp) / 6f) * (0.55f + 0.45f * n);
            float grass = 1 - sand;
            w[0] = grass * (1 - dry * 0.45f);
            w[1] = grass * dry * 0.45f;
            w[3] = sand;
            Blend(w, 2, dirt);
            Blend(w, 4, rock);
            // Gravel and pebbles at the foot of rock and along the wet sand.
            Blend(w, 5, Mathf.Clamp01((slope - 0.25f) / 0.15f) * (1 - rock) * 0.6f);
        }

        static void Blend(float[] w, int layer, float t)
        {
            if (t <= 0) return;
            for (int i = 0; i < 6; i++) w[i] *= 1 - t;
            w[layer] += t;
        }

        static Color ColorAt(float h, float d)
        {
            // Untextured fallback (and tint where no layer covers).
            return h < 1.4f ? Palette.Sand : Color.Lerp(Palette.Grass, Palette.GrassDry, 0.2f);
        }

        public static GameObject BuildTerrain(Transform parent)
        {
            int n = Mathf.RoundToInt(Half * 2 / Cell) + 1;
            var heights = new float[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    heights[j * n + i] = Height(-Half + i * Cell, -Half + j * Cell);
            var verts = new Vector3[n * n];
            var normals = new Vector3[n * n];
            var colors = new Color32[n * n];
            var w01 = new Vector2[n * n];
            var w23 = new Vector2[n * n];
            var w45 = new Vector2[n * n];
            var surf = new Vector2[n * n];
            var w = new float[6];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int k = j * n + i;
                    float x = -Half + i * Cell, z = -Half + j * Cell;
                    float hl = heights[j * n + Mathf.Max(0, i - 1)], hr = heights[j * n + Mathf.Min(n - 1, i + 1)];
                    float hd = heights[Mathf.Max(0, j - 1) * n + i], hu = heights[Mathf.Min(n - 1, j + 1) * n + i];
                    var nrm = new Vector3(hl - hr, 2 * Cell, hd - hu).normalized;
                    verts[k] = new Vector3(x, heights[k], z);
                    normals[k] = nrm;
                    float slope = Mathf.Sqrt(Mathf.Max(0, 1 - nrm.y * nrm.y)) / Mathf.Max(0.05f, nrm.y);
                    Weights(x, z, heights[k], slope, w);
                    w01[k] = new Vector2(w[0], w[1]); w23[k] = new Vector2(w[2], w[3]); w45[k] = new Vector2(w[4], w[5]);
                    colors[k] = ColorAt(heights[k], Distance01(x, z));
                    surf[k] = new Vector2(0.08f, 0);
                }
            var tris = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    // Skip cells deep under water: the sea floor far out is never seen.
                    if (heights[a] < -6.5f && heights[b] < -6.5f && heights[c] < -6.5f && heights[d] < -6.5f) continue;
                    tris[t++] = a; tris[t++] = c; tris[t++] = d;
                    tris[t++] = a; tris[t++] = d; tris[t++] = b;
                }
            System.Array.Resize(ref tris, t);
            var mesh = new Mesh { name = "Island", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.colors32 = colors;
            mesh.uv = w01;
            mesh.uv2 = surf;
            mesh.uv3 = w23;
            mesh.uv4 = w45;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            var go = new GameObject("Terrain");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Materials.Terrain;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            return go;
        }

        /// <summary>The sea: a grid out to the horizon, vertex colour r = depth (0 at the shore).</summary>
        public static GameObject BuildSea(Transform parent)
        {
            var mb = new MeshBuilder();
            const float step = 4f, reach = 900f;
            // Open ocean round the terrain: four big flat slabs of deep water.
            var deep = new Color(1, 0, 0);
            void Slab(float x0, float z0, float x1, float z1)
            {
                mb.QuadSmooth(new Vector3(x0, Sea, z0), new Vector3(x0, Sea, z1), new Vector3(x1, Sea, z1), new Vector3(x1, Sea, z0),
                    Vector3.up, Vector3.up, Vector3.up, Vector3.up, deep, deep, deep, deep);
            }
            Slab(-reach, -reach, reach, -Half);
            Slab(-reach, Half, reach, reach);
            Slab(-reach, -Half, -Half, Half);
            Slab(Half, -Half, reach, Half);
            for (float z = -Half; z < Half - 0.01f; z += step)
                for (float x = -Half; x < Half - 0.01f; x += step)
                {
                    float d0 = Depth(x, z), d1 = Depth(x + step, z), d2 = Depth(x + step, z + step), d3 = Depth(x, z + step);
                    if (Mathf.Max(Mathf.Max(d0, d1), Mathf.Max(d2, d3)) < -0.6f) continue; // all dry land
                    float y = Sea;
                    mb.QuadSmooth(new Vector3(x, y, z), new Vector3(x, y, z + step), new Vector3(x + step, y, z + step), new Vector3(x + step, y, z),
                        Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                        new Color(Mathf.Clamp01(d0), 0, 0), new Color(Mathf.Clamp01(d3), 0, 0), new Color(Mathf.Clamp01(d2), 0, 0), new Color(Mathf.Clamp01(d1), 0, 0));
                }
            var go = new GameObject("Sea");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Sea");
            var mr = go.AddComponent<MeshRenderer>();
            var mat = Materials.Water;
            mat.SetColor("_ShallowColor", new Color(0.28f, 0.78f, 0.74f, 0.5f));
            mat.SetColor("_DeepColor", new Color(0.02f, 0.26f, 0.42f, 0.96f));
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Water depth as 0..1 (past 1 = deep ocean); negative on dry land.</summary>
        static float Depth(float x, float z)
        {
            if (Mathf.Abs(x) > Half - 0.5f || Mathf.Abs(z) > Half - 0.5f) return 1f;
            return (Sea - Height(x, z)) / 5f;
        }
    }
}
