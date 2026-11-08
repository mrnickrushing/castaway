using UnityEngine;

namespace LastTide.Game
{
    /// <summary>Island colours.</summary>
    public static class Tropic
    {
        public static readonly Color PalmBark = Palette.Hex(0x8A6A48);
        public static readonly Color PalmRing = Palette.Hex(0x6B4F35);
        public static readonly Color Frond = Palette.Hex(0x4E8A3A);
        public static readonly Color FrondDark = Palette.Hex(0x356A2C);
        public static readonly Color FrondDry = Palette.Hex(0x9C8A4A);
        public static readonly Color Coconut = Palette.Hex(0x5A3E22);
        public static readonly Color Fern = Palette.Hex(0x3F7A34);
        public static readonly Color Berry = Palette.Hex(0xC2304A);
        public static readonly Color Rock = Palette.Hex(0x8F8C86);
        public static readonly Color RockDark = Palette.Hex(0x6A6862);
        public static readonly Color Drift = Palette.Hex(0xB9AD98);
        public static readonly Color Hull = Palette.Hex(0x5A3E2A);
        public static readonly Color HullPaint = Palette.Hex(0x23384A);
        public static readonly Color Sail = Palette.Hex(0xE6DCC2);
        public static readonly Color Rope = Palette.Hex(0xB89A64);
        public static readonly Color Skin = Palette.Hex(0xC98E66);
        public static readonly Color Shirt = Palette.Hex(0xD9CDB0);
        public static readonly Color Pants = Palette.Hex(0x3E5670);
        public static readonly Color Hair = Palette.Hex(0x3A2A1E);
        public static readonly Color Pack = Palette.Hex(0x5E6A3A);
        public static readonly Color Boar = Palette.Hex(0x5A4334);
        public static readonly Color BoarDark = Palette.Hex(0x3A2C22);
        public static readonly Color Tusk = Palette.Hex(0xEEE6D2);
        public static readonly Color Water = Palette.Hex(0x4AA8B8);
        public static readonly Color Iron = Palette.Hex(0x3A3A3E);
    }

    /// <summary>Procedural models for everything on the island (until drop-in art replaces them).</summary>
    public static class Models
    {
        static float Hash(float x, float z) { return Noise2.Hash(Mathf.RoundToInt(x * 13.7f), Mathf.RoundToInt(z * 7.3f)); }

        /// <summary>A leaning palm: ringed trunk curving up, a crown of drooping fronds, coconuts.</summary>
        public static MeshBuilder Palm(float seed, float height)
        {
            var mb = new MeshBuilder();
            float lean = 0.12f + Hash(seed, 1) * 0.22f;
            Vector3 dir = Quaternion.Euler(0, Hash(seed, 2) * 360, 0) * Vector3.forward;
            int segs = 8;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i < segs; i++)
            {
                float t0 = i / (float)segs, t1 = (i + 1) / (float)segs;
                Vector3 a = Trunk(t0, height, lean, dir), b = Trunk(t1, height, lean, dir);
                float r0 = Mathf.Lerp(0.34f, 0.18f, t0), r1 = Mathf.Lerp(0.34f, 0.18f, t1);
                mb.Surf = SurfaceKind.Bark;
                mb.Cylinder(a, b, r0, r1, 7, i % 2 == 0 ? Tropic.PalmBark : Color.Lerp(Tropic.PalmBark, Tropic.PalmRing, 0.5f), false);
                prev = b;
            }
            mb.Surf = SurfaceKind.None;
            var top = prev;
            // Coconuts under the crown.
            for (int i = 0; i < 3; i++)
            {
                var o = Quaternion.Euler(0, i * 120 + seed * 30, 0) * new Vector3(0.28f, -0.25f, 0);
                mb.Sphere(top + o, new Vector3(0.2f, 0.22f, 0.2f), 6, Tropic.Coconut);
            }
            int fronds = 11;
            for (int i = 0; i < fronds; i++)
            {
                float yaw = i * 360f / fronds + Hash(seed, i) * 25;
                float droop = 45 + Hash(seed, i + 9) * 35;
                bool dry = Hash(seed, i + 20) > 0.85f;
                Frond(mb, top, yaw, droop, 3.4f + Hash(seed, i + 3) * 1.2f, dry ? Tropic.FrondDry : (i % 2 == 0 ? Tropic.Frond : Tropic.FrondDark));
            }
            return mb;
        }

        static Vector3 Trunk(float t, float h, float lean, Vector3 dir)
        {
            return Vector3.up * (t * h) + dir * (lean * h * t * t);
        }

        /// <summary>One frond: a central rib arching out and down with leaflets either side.</summary>
        static void Frond(MeshBuilder mb, Vector3 root, float yaw, float droop, float len, Color c)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            int n = 6;
            Vector3 Rib(float t)
            {
                float a = Mathf.Lerp(-15, droop + 35, t) * Mathf.Deg2Rad;
                return root + rot * new Vector3(0, -Mathf.Sin(a) * len * t * 0.55f + 0.3f * (1 - t) * t * 2, Mathf.Cos(a) * len * t);
            }
            for (int i = 0; i < n; i++)
            {
                float t0 = i / (float)n, t1 = (i + 1) / (float)n;
                Vector3 a = Rib(t0), b = Rib(t1);
                float w0 = Mathf.Sin(Mathf.PI * Mathf.Lerp(0.1f, 1f, t0)) * 0.48f, w1 = Mathf.Sin(Mathf.PI * Mathf.Lerp(0.1f, 1f, t1)) * 0.48f;
                Vector3 side = rot * Vector3.right;
                Vector3 sag = Vector3.down * 0.55f;
                // Two leaflet panels angled down from the rib, both faces drawn.
                Leaf(mb, a, b, b + side * w1 + sag * w1, a + side * w0 + sag * w0, c);
                Leaf(mb, b, a, a - side * w0 + sag * w0, b - side * w1 + sag * w1, MeshBuilder.Darken(c, 0.92f));
            }
        }

        static void Leaf(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            mb.Quad(a, b, c, d, col);
            mb.Quad(d, c, b, a, MeshBuilder.Darken(col, 0.8f));
        }

        /// <summary>A broadleaf jungle tree: a forked trunk and a canopy of leafy clumps in mixed greens.</summary>
        public static MeshBuilder Jungle(float seed, float height)
        {
            var mb = new MeshBuilder { Surf = SurfaceKind.Bark };
            var bark = Color.Lerp(Palette.Hex(0x6E5A44), Palette.Hex(0x8A7A62), Hash(seed, 1));
            mb.Cylinder(Vector3.zero, new Vector3(0, height * 0.55f, 0), 0.4f, 0.26f, 7, bark);
            // Buttress roots.
            for (int i = 0; i < 4; i++)
            {
                var d = Quaternion.Euler(0, i * 90 + seed * 20, 0) * Vector3.forward;
                mb.Cylinder(d * 0.9f + Vector3.down * 0.1f, new Vector3(0, 0.9f, 0) + d * 0.1f, 0.12f, 0.2f, 5, MeshBuilder.Darken(bark, 0.9f));
            }
            var fork = new Vector3(0, height * 0.55f, 0);
            mb.Surf = SurfaceKind.None;
            int clumps = 6;
            for (int i = 0; i < clumps; i++)
            {
                var d = Quaternion.Euler(0, i * 360f / clumps + Hash(seed, i) * 40, 0) * Vector3.forward;
                float out_ = 1.2f + Hash(seed, i + 7) * 1.4f, up = height * (0.68f + Hash(seed, i + 3) * 0.3f);
                var tip = d * out_ + Vector3.up * up;
                mb.Surf = SurfaceKind.Bark;
                mb.Cylinder(fork, tip - Vector3.up * 0.4f, 0.2f, 0.09f, 5, bark);
                mb.Surf = SurfaceKind.None;
                var g = Color.Lerp(Tropic.Frond, Tropic.FrondDark, Hash(seed, i + 11));
                g = Color.Lerp(g, Palette.Hex(0x6E9A3A), Hash(seed, i + 19) * 0.35f);
                float r = 1.3f + Hash(seed, i + 5) * 0.8f;
                Rock(mb, tip, new Vector3(r * 2.1f, r * 1.3f, r * 2.1f), seed * 3 + i, g);
            }
            Rock(mb, fork + Vector3.up * height * 0.5f, new Vector3(3.4f, 2.1f, 3.4f), seed * 7, Tropic.Frond);
            return mb;
        }

        /// <summary>A tuft of tall grass blades with a few flowers.</summary>
        public static MeshBuilder Tuft(float seed, bool flowers)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i < 9; i++)
            {
                var rot = Quaternion.Euler(Hash(seed, i) * 30 - 15, i * 40 + seed * 13, 0);
                float h = 0.5f + Hash(seed, i + 2) * 0.5f;
                var c = Color.Lerp(Palette.Hex(0x5E9A3A), Palette.Hex(0x9AB04A), Hash(seed, i + 4));
                Vector3 a = rot * new Vector3(-0.05f, 0, 0), b = rot * new Vector3(0.05f, 0, 0), t = rot * new Vector3(0, h, 0.12f);
                mb.Tri(a, t, b, c);
                mb.Tri(b, t, a, MeshBuilder.Darken(c, 0.85f));
            }
            if (flowers)
            {
                var fc = Hash(seed, 9) > 0.5f ? Palette.Hex(0xF2D24A) : Palette.Hex(0xE8608A);
                for (int i = 0; i < 3; i++)
                    mb.Sphere(Quaternion.Euler(0, i * 120 + seed, 0) * new Vector3(0.15f, 0.55f + i * 0.08f, 0), Vector3.one * 0.07f, 5, fc);
            }
            return mb;
        }

        /// <summary>A lumpy rock: a squashed sphere with its vertices pushed around.</summary>
        public static MeshBuilder Boulder(float seed, Vector3 size, Color c)
        {
            var mb = new MeshBuilder { Surf = SurfaceKind.Stone };
            Rock(mb, Vector3.up * size.y * 0.35f, size, seed, c);
            return mb;
        }

        public static void Rock(MeshBuilder mb, Vector3 center, Vector3 size, float seed, Color c)
        {
            int start = mb.VertexCount;
            mb.Sphere(center, size * 0.5f, 7, c, false);
            var m = mb.Matrix;
            var centerW = m.MultiplyPoint3x4(center);
            for (int i = start; i < mb.Verts.Count; i++)
            {
                var v = mb.Verts[i];
                float n = Mathf.PerlinNoise(v.x * 1.7f + seed * 3.1f, v.z * 1.7f + v.y * 0.9f + seed);
                var off = (v - centerW) * (0.78f + n * 0.4f);
                if (off.y < -size.y * 0.15f) off.y = -size.y * 0.15f; // flat underside sits on the ground
                mb.Verts[i] = centerW + off;
                float shade = 0.85f + n * 0.25f;
                var col = (Color)mb.Colors[i];
                mb.Colors[i] = (Color32)new Color(col.r * shade, col.g * shade, col.b * shade, col.a);
            }
            // Recompute flat normals for the moved vertices (sphere triangles are independent when smooth=false).
            for (int t = 0; t < mb.Tris.Count; t += 3)
            {
                int a = mb.Tris[t], b = mb.Tris[t + 1], cc = mb.Tris[t + 2];
                if (a < start) continue;
                var nrm = Vector3.Cross(mb.Verts[b] - mb.Verts[a], mb.Verts[cc] - mb.Verts[a]).normalized;
                mb.Normals[a] = nrm; mb.Normals[b] = nrm; mb.Normals[cc] = nrm;
            }
        }

        public static MeshBuilder Stones(float seed)
        {
            var mb = new MeshBuilder { Surf = SurfaceKind.Stone };
            for (int i = 0; i < 5; i++)
            {
                var p = Quaternion.Euler(0, i * 72 + seed * 40, 0) * new Vector3(0.35f + Hash(seed, i) * 0.5f, 0, 0);
                float s = 0.25f + Hash(seed, i + 5) * 0.25f;
                Rock(mb, p + Vector3.up * s * 0.25f, new Vector3(s * 1.3f, s, s * 1.1f), seed + i, Color.Lerp(Tropic.Rock, Tropic.RockDark, Hash(seed, i + 9)));
            }
            return mb;
        }

        public static MeshBuilder Fern(float seed, bool berries)
        {
            var mb = new MeshBuilder();
            int n = 11;
            for (int i = 0; i < n; i++)
            {
                float yaw = i * 360f / n + Hash(seed, i) * 20;
                var rot = Quaternion.Euler(0, yaw, 0);
                float len = 0.9f + Hash(seed, i + 4) * 0.5f;
                float up = 0.55f + Hash(seed, i + 8) * 0.4f;
                Vector3 a = Vector3.zero, tip = rot * new Vector3(0, up, len), mid = rot * new Vector3(0, up * 0.95f, len * 0.5f);
                Vector3 side = rot * Vector3.right * 0.22f;
                var c = i % 3 == 0 ? Tropic.FrondDark : Tropic.Fern;
                Leaf(mb, a, mid, mid + side, a + side * 0.2f, c);
                Leaf(mb, mid, tip, tip, mid + side, c);
                Leaf(mb, mid, a, a - side * 0.2f, mid - side, MeshBuilder.Darken(c, 0.9f));
                Leaf(mb, tip, mid, mid - side, tip, MeshBuilder.Darken(c, 0.9f));
            }
            if (berries)
                for (int i = 0; i < 9; i++)
                {
                    var p = Quaternion.Euler(0, i * 40 + seed * 17, 0) * new Vector3(0.3f + Hash(seed, i + 30) * 0.5f, 0.45f + Hash(seed, i + 31) * 0.3f, 0);
                    mb.Sphere(p, Vector3.one * 0.09f, 5, Tropic.Berry);
                }
            return mb;
        }

        public static MeshBuilder Driftwood(float seed)
        {
            var mb = new MeshBuilder { Surf = SurfaceKind.WoodGrain };
            var dir = Quaternion.Euler(0, seed * 97 % 360, 0);
            mb.Push().Rotate(dir);
            mb.Cylinder(new Vector3(0, 0.22f, -1.4f), new Vector3(0, 0.18f, 1.4f), 0.24f, 0.17f, 7, Tropic.Drift);
            mb.Cylinder(new Vector3(0, 0.2f, 0.4f), new Vector3(0.7f, 0.3f, 1.1f), 0.09f, 0.05f, 5, Tropic.Drift);
            mb.Pop();
            return mb;
        }

        public static MeshBuilder Crate(float seed)
        {
            var mb = new MeshBuilder { Surf = SurfaceKind.Planks };
            mb.Push().RotateY(seed * 53 % 360).Rotate(Quaternion.Euler(0, 0, 6));
            var wood = Palette.WoodPale;
            mb.Box(new Vector3(0, 0.5f, 0), new Vector3(1.1f, 0.95f, 0.9f), wood, MeshBuilder.Lighten(wood, 0.1f));
            mb.Surf = SurfaceKind.WoodGrain;
            foreach (float x in new[] { -0.5f, 0.5f })
                mb.Box(new Vector3(x, 0.5f, 0), new Vector3(0.1f, 1f, 0.95f), Palette.Wood);
            mb.Box(new Vector3(0, 0.5f, 0), new Vector3(1.15f, 0.1f, 0.95f), Palette.Wood);
            mb.Pop();
            return mb;
        }

        public static MeshBuilder Barrel(float seed)
        {
            var mb = new MeshBuilder { Surf = SurfaceKind.WoodGrain };
            bool tipped = Hash(seed, 3) > 0.5f;
            mb.Push().RotateY(seed * 71 % 360);
            if (tipped) mb.Translate(0, 0.42f, 0).Rotate(Quaternion.Euler(90, 0, 0)).Translate(0, -0.5f, 0);
            mb.Cylinder(new Vector3(0, 0, 0), new Vector3(0, 0.5f, 0), 0.38f, 0.44f, 10, Palette.Wood);
            mb.Cylinder(new Vector3(0, 0.5f, 0), new Vector3(0, 1f, 0), 0.44f, 0.38f, 10, Palette.Wood);
            mb.Surf = SurfaceKind.Rust;
            foreach (float y in new[] { 0.12f, 0.88f })
                mb.Cylinder(new Vector3(0, y - 0.04f, 0), new Vector3(0, y + 0.04f, 0), 0.43f, 0.43f, 10, Tropic.Iron, false);
            mb.Pop();
            return mb;
        }

        public static MeshBuilder TidePool(float seed)
        {
            var mb = new MeshBuilder { Surf = SurfaceKind.Stone };
            for (int i = 0; i < 9; i++)
            {
                var p = Quaternion.Euler(0, i * 40 + seed * 20, 0) * new Vector3(1.1f + Hash(seed, i) * 0.3f, 0, 0);
                float s = 0.35f + Hash(seed, i + 3) * 0.3f;
                Rock(mb, p + Vector3.up * 0.05f, new Vector3(s * 1.4f, s, s), seed + i, Tropic.RockDark);
            }
            mb.Surf = SurfaceKind.None;
            mb.Mat(0.9f);
            mb.Cylinder(new Vector3(0, -0.05f, 0), new Vector3(0, 0.05f, 0), 1.15f, 1.15f, 12, Tropic.Water);
            // A crab or two on the rocks.
            mb.Mat(0.3f);
            mb.Sphere(new Vector3(0.5f, 0.12f, 0.3f), new Vector3(0.16f, 0.07f, 0.13f), 6, Palette.Hex(0xC4502E));
            return mb;
        }

        public static MeshBuilder Spring()
        {
            var mb = new MeshBuilder { Surf = SurfaceKind.Stone };
            for (int i = 0; i < 11; i++)
            {
                var p = Quaternion.Euler(0, i * 33, 0) * new Vector3(1.5f, 0, 0);
                float s = 0.45f + Hash(i, 7) * 0.35f;
                Rock(mb, p + Vector3.up * 0.1f, new Vector3(s * 1.3f, s, s), i * 3.3f, Tropic.Rock);
            }
            mb.Surf = SurfaceKind.None;
            mb.Mat(0.92f);
            mb.Cylinder(new Vector3(0, 0f, 0), new Vector3(0, 0.12f, 0), 1.5f, 1.5f, 14, Palette.Hex(0x58B8C8));
            return mb;
        }

        public static MeshBuilder Hatchet()
        {
            var mb = new MeshBuilder();
            mb.Push().Rotate(Quaternion.Euler(0, 35, 90)).Translate(0.08f, 0, 0);
            mb.Surf = SurfaceKind.WoodGrain;
            mb.Box(new Vector3(0, 0.45f, 0), new Vector3(0.07f, 0.9f, 0.07f), Palette.Wood);
            mb.Surf = SurfaceKind.Rust;
            mb.Box(new Vector3(0.12f, 0.82f, 0), new Vector3(0.28f, 0.18f, 0.04f), Palette.Steel);
            mb.Pop();
            return mb;
        }

        /// <summary>Held tools and weapons, built along +Y from the grip (local origin) for the hand to hold.</summary>
        public static MeshBuilder Held(string id)
        {
            var mb = new MeshBuilder();
            switch (id)
            {
                case "hatchet":
                    mb.Box(new Vector3(0, 0.3f, 0), new Vector3(0.05f, 0.7f, 0.05f), Palette.Wood);
                    mb.Box(new Vector3(0, 0.6f, 0.1f), new Vector3(0.03f, 0.14f, 0.22f), Palette.Steel);
                    break;
                case "pickaxe":
                    mb.Box(new Vector3(0, 0.3f, 0), new Vector3(0.05f, 0.75f, 0.05f), Palette.Wood);
                    mb.Box(new Vector3(0, 0.64f, 0), new Vector3(0.06f, 0.08f, 0.5f), Tropic.RockDark);
                    break;
                case "spear":
                    mb.Box(new Vector3(0, 0.55f, 0), new Vector3(0.04f, 1.6f, 0.04f), Palette.Hex(0xB8A060));
                    mb.Cylinder(new Vector3(0, 1.35f, 0), new Vector3(0, 1.62f, 0), 0.06f, 0f, 4, Tropic.Rock);
                    break;
                case "cutlass":
                    mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.05f, 0.2f, 0.05f), Palette.WoodDark);
                    mb.Box(new Vector3(0, 0.16f, 0), new Vector3(0.05f, 0.03f, 0.18f), Palette.Brass);
                    mb.Box(new Vector3(0, 0.55f, 0.03f), new Vector3(0.02f, 0.75f, 0.08f), Palette.Steel);
                    break;
                case "harpoon":
                    mb.Box(new Vector3(0, 0.55f, 0), new Vector3(0.04f, 1.6f, 0.04f), Palette.Wood);
                    mb.Cylinder(new Vector3(0, 1.35f, 0), new Vector3(0, 1.7f, 0), 0.05f, 0f, 4, Palette.Steel);
                    break;
                case "rod":
                    mb.Cylinder(new Vector3(0, -0.1f, 0), new Vector3(0, 1.8f, 0), 0.03f, 0.01f, 5, Palette.Hex(0xB8A060));
                    break;
                case "club":
                    mb.Cylinder(new Vector3(0, -0.1f, 0), new Vector3(0, 0.75f, 0), 0.04f, 0.1f, 6, Palette.WoodDark);
                    for (int i = 0; i < 5; i++) mb.Box(Quaternion.Euler(0, i * 72, 0) * new Vector3(0.1f, 0.55f + i * 0.04f, 0), new Vector3(0.12f, 0.03f, 0.03f), Palette.Steel);
                    break;
            }
            return mb;
        }

        /// <summary>The ship on the reef: a broken hull lying over on its side, a snapped mast and a torn sail.</summary>
        public static MeshBuilder Wreck()
        {
            var mb = new MeshBuilder();
            mb.Push().Rotate(Quaternion.Euler(0, -28, 0)).Rotate(Quaternion.Euler(0, 0, 18));
            // Hull: lofted rings from bow to stern.
            int rings = 10, around = 10;
            float len = 22f;
            Vector3[] prev = null;
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings, z = (t - 0.5f) * len;
                float beam = 3.6f * Mathf.Sin(Mathf.Lerp(0.15f, 1f, Mathf.Min(1, t * 1.6f)) * Mathf.PI * 0.5f) * Mathf.Lerp(1f, 0.82f, Mathf.Clamp01((t - 0.75f) * 4));
                float depth = 3f * (t < 0.15f ? Mathf.Lerp(0.5f, 1f, t / 0.15f) : 1);
                float sheer = 3.4f + Mathf.Pow(Mathf.Abs(t - 0.45f) * 2, 2) * 1.2f;
                var ring = new Vector3[around + 1];
                for (int k = 0; k <= around; k++)
                {
                    float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, k / (float)around);
                    ring[k] = new Vector3(Mathf.Sin(a) * beam, sheer - Mathf.Cos(a) * depth - 1.2f, z);
                }
                if (prev != null)
                    for (int k = 0; k < around; k++)
                    {
                        // A hole torn in the port side amidships.
                        bool hole = r >= 5 && r <= 6 && k >= 2 && k <= 3;
                        if (hole) continue;
                        var c = k < 3 || k > around - 4 ? Tropic.HullPaint : Tropic.Hull;
                        if (Hash(r, k) > 0.8f) c = MeshBuilder.Darken(c, 0.8f);
                        mb.Surf = SurfaceKind.Planks;
                        mb.Quad(prev[k], ring[k], ring[k + 1], prev[k + 1], c);
                        mb.Quad(prev[k + 1], ring[k + 1], ring[k], prev[k], MeshBuilder.Darken(Tropic.Hull, 0.5f));
                    }
                prev = ring;
            }
            // Deck planks and a broken rail.
            mb.Surf = SurfaceKind.Planks;
            mb.Box(new Vector3(0, 3.0f, 0.5f), new Vector3(6.2f, 0.15f, 17f), Palette.WoodPale);
            // Mast snapped at the base, lying across the deck and into the sand.
            mb.Surf = SurfaceKind.WoodGrain;
            mb.Cylinder(new Vector3(0, 3.1f, 2f), new Vector3(0, 6.0f, 2f), 0.38f, 0.34f, 8, Palette.Wood);
            mb.Cylinder(new Vector3(0.3f, 6.0f, 2.2f), new Vector3(11f, 1.0f, 6f), 0.32f, 0.22f, 8, Palette.Wood);
            mb.Cylinder(new Vector3(0, 3.1f, -5.5f), new Vector3(0, 9f, -5.5f), 0.3f, 0.22f, 8, Palette.Wood);
            mb.Cylinder(new Vector3(-2.4f, 7.6f, -5.5f), new Vector3(2.4f, 7.9f, -5.5f), 0.12f, 0.1f, 6, Palette.Wood);
            // Torn sail hanging from the yard.
            mb.Surf = SurfaceKind.None;
            mb.Quad(new Vector3(-2.2f, 7.5f, -5.4f), new Vector3(2.2f, 7.8f, -5.4f), new Vector3(1.6f, 4.6f, -4.6f), new Vector3(-1.0f, 3.8f, -4.9f), Tropic.Sail);
            mb.Quad(new Vector3(-1.0f, 3.8f, -4.9f), new Vector3(1.6f, 4.6f, -4.6f), new Vector3(2.2f, 7.8f, -5.4f), new Vector3(-2.2f, 7.5f, -5.4f), MeshBuilder.Darken(Tropic.Sail, 0.85f));
            // Ropes.
            mb.Cylinder(new Vector3(0, 9f, -5.5f), new Vector3(3f, 3.3f, 3f), 0.04f, 0.04f, 4, Tropic.Rope, false);
            mb.Cylinder(new Vector3(0, 9f, -5.5f), new Vector3(-3.2f, 3.2f, -10f), 0.04f, 0.04f, 4, Tropic.Rope, false);
            mb.Pop();
            return mb;
        }

        public static MeshBuilder IronVein(float seed)
        {
            var mb = Boulder(seed + 7, new Vector3(2.4f, 1.7f, 2.1f), Palette.Hex(0x7A6E66));
            // Rusty orange streaks and glints of ore.
            for (int i = 0; i < 7; i++)
            {
                var d = Quaternion.Euler(Hash(seed, i) * 60 - 30, i * 51, 0) * new Vector3(0, 0.6f, 1.05f);
                mb.Surf = SurfaceKind.Rust;
                mb.Box(d + Vector3.up * 0.2f, new Vector3(0.3f, 0.12f, 0.12f), Palette.Hex(0xB0582A));
            }
            return mb;
        }

        /// <summary>A smugglers' chest spilling gold coins.</summary>
        public static MeshBuilder Treasure(float seed)
        {
            var mb = new MeshBuilder();
            mb.Push().RotateY(seed * 47 % 360);
            mb.Surf = SurfaceKind.Planks;
            mb.Box(new Vector3(0, 0.3f, 0), new Vector3(1.1f, 0.6f, 0.7f), Palette.Hex(0x5A3218));
            mb.Surf = SurfaceKind.None;
            mb.Push().Translate(0, 0.6f, -0.33f).Rotate(Quaternion.Euler(-60, 0, 0));
            mb.Box(new Vector3(0, 0.05f, 0.35f), new Vector3(1.14f, 0.12f, 0.72f), Palette.Hex(0x4A2814));
            mb.Pop();
            mb.Mat(0.7f, 0.9f);
            mb.Box(new Vector3(0, 0.6f, 0), new Vector3(0.95f, 0.06f, 0.55f), Palette.Hex(0xE8B830));
            for (int i = 0; i < 12; i++)
                mb.Cylinder(new Vector3(Hash(seed, i) * 1.6f - 0.8f, 0.01f, -0.5f - Hash(seed, i + 3) * 0.5f), new Vector3(Hash(seed, i) * 1.6f - 0.8f, 0.04f, -0.5f - Hash(seed, i + 3) * 0.5f), 0.07f, 8, Palette.Hex(0xE8B830));
            mb.Mat(0.15f);
            foreach (float x in new[] { -0.45f, 0.45f }) mb.Box(new Vector3(x, 0.3f, 0), new Vector3(0.06f, 0.62f, 0.72f), Tropic.Iron);
            mb.Pop();
            return mb;
        }

        /// <summary>Where the fish are: a ring of ripples and a floating cork.</summary>
        public static MeshBuilder FishSpot()
        {
            var mb = new MeshBuilder { Surf = SurfaceKind.None };
            mb.Mat(0.9f);
            for (int i = 0; i < 3; i++)
            {
                float r = 0.5f + i * 0.35f;
                for (int k = 0; k < 16; k++)
                {
                    float a0 = k / 16f * Mathf.PI * 2, a1 = (k + 0.6f) / 16f * Mathf.PI * 2;
                    mb.Quad(new Vector3(Mathf.Sin(a0) * r, 0.03f, Mathf.Cos(a0) * r), new Vector3(Mathf.Sin(a1) * r, 0.03f, Mathf.Cos(a1) * r),
                        new Vector3(Mathf.Sin(a1) * (r + 0.06f), 0.03f, Mathf.Cos(a1) * (r + 0.06f)), new Vector3(Mathf.Sin(a0) * (r + 0.06f), 0.03f, Mathf.Cos(a0) * (r + 0.06f)), new Color(0.9f, 0.97f, 1f));
                }
            }
            mb.Mat(0.3f);
            mb.Sphere(new Vector3(0, 0.08f, 0), new Vector3(0.1f, 0.1f, 0.1f), 6, Palette.Hex(0xD03A2A));
            mb.Sphere(new Vector3(0, 0.15f, 0), new Vector3(0.07f, 0.05f, 0.07f), 6, Color.white);
            return mb;
        }

        /// <summary>The castaway's raft drawn up on the sand: logs lashed together, a mast and a patched sail.</summary>
        public static MeshBuilder Raft()
        {
            var mb = new MeshBuilder { Surf = SurfaceKind.Bark };
            for (int i = 0; i < 6; i++)
                mb.Cylinder(new Vector3(-1.1f + i * 0.44f, 0.2f, -1.9f), new Vector3(-1.1f + i * 0.44f, 0.2f, 1.9f), 0.21f, 0.2f, 7, Color.Lerp(Palette.Hex(0x8A6A48), Palette.Hex(0x6E5236), Hash(i, 2)));
            mb.Surf = SurfaceKind.None;
            foreach (float z in new[] { -1.3f, 0, 1.3f }) mb.Box(new Vector3(0, 0.38f, z), new Vector3(2.7f, 0.06f, 0.12f), Tropic.Rope);
            mb.Surf = SurfaceKind.WoodGrain;
            mb.Cylinder(new Vector3(0, 0.3f, 0.4f), new Vector3(0, 3.6f, 0.4f), 0.08f, 0.06f, 6, Palette.Wood);
            mb.Cylinder(new Vector3(-1f, 3.2f, 0.4f), new Vector3(1f, 3.2f, 0.4f), 0.05f, 6, Palette.Wood);
            mb.Surf = SurfaceKind.None;
            mb.Quad(new Vector3(-0.95f, 3.15f, 0.45f), new Vector3(0.95f, 3.15f, 0.45f), new Vector3(0.8f, 1.0f, 0.55f), new Vector3(-0.8f, 1.0f, 0.55f), Tropic.Sail);
            mb.Quad(new Vector3(-0.8f, 1.0f, 0.55f), new Vector3(0.8f, 1.0f, 0.55f), new Vector3(0.95f, 3.15f, 0.45f), new Vector3(-0.95f, 3.15f, 0.45f), MeshBuilder.Darken(Tropic.Sail, 0.8f));
            mb.Box(new Vector3(0.2f, 2.0f, 0.5f), new Vector3(0.5f, 0.4f, 0.02f), Palette.Hex(0xA87A50)); // patch
            mb.Surf = SurfaceKind.WoodGrain;
            mb.Box(new Vector3(0.6f, 0.45f, -1.2f), new Vector3(0.12f, 0.06f, 1.6f), Palette.WoodPale); // paddle
            return mb;
        }

        /// <summary>A white and red striped lighthouse with a keeper's hut.</summary>
        public static MeshBuilder Lighthouse()
        {
            var mb = new MeshBuilder();
            mb.Surf = SurfaceKind.Stone;
            mb.Cylinder(new Vector3(0, 0, 0), new Vector3(0, 0.8f, 0), 3.4f, 3.3f, 14, Tropic.Rock);
            for (int i = 0; i < 6; i++)
            {
                float y0 = 0.8f + i * 2.2f, y1 = y0 + 2.2f;
                mb.Surf = SurfaceKind.Concrete;
                mb.Cylinder(new Vector3(0, y0, 0), new Vector3(0, y1, 0), 2.6f - i * 0.18f, 2.6f - (i + 1) * 0.18f, 14, i % 2 == 0 ? Palette.White : Palette.Hex(0xB83A2E));
            }
            float top = 0.8f + 6 * 2.2f;
            mb.Surf = SurfaceKind.PaintedMetal;
            mb.Cylinder(new Vector3(0, top, 0), new Vector3(0, top + 0.3f, 0), 2.2f, 2.2f, 14, Tropic.Iron);
            mb.Lit();
            mb.Cylinder(new Vector3(0, top + 0.3f, 0), new Vector3(0, top + 2.0f, 0), 1.3f, 1.3f, 10, Palette.Hex(0xFFE7A0));
            mb.Mat(0.4f, 0.3f);
            mb.Cylinder(new Vector3(0, top + 2.0f, 0), new Vector3(0, top + 3.2f, 0), 1.6f, 0.15f, 10, Tropic.Iron);
            mb.Mat(0.15f);
            // Keeper's hut against the tower.
            mb.Surf = SurfaceKind.Siding;
            mb.Box(new Vector3(0, 1.3f, -4.2f), new Vector3(4f, 2.6f, 3f), Palette.Cream);
            mb.Surf = SurfaceKind.Shingles;
            mb.GableRoof(new Vector3(0, 2.6f, -4.2f), new Vector3(4f, 1.2f, 3f), Palette.RoofShingle, Palette.Cream);
            mb.Surf = SurfaceKind.WoodGrain;
            mb.Box(new Vector3(0, 0.9f, -5.75f), new Vector3(0.9f, 1.8f, 0.08f), Palette.WoodDark);
            return mb;
        }

        /// <summary>The mouth of the smugglers' cave: a dark arch in a wall of rock, crates stacked by the entrance.</summary>
        public static MeshBuilder CaveMouth()
        {
            var mb = new MeshBuilder { Surf = SurfaceKind.Stone };
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-80, 80, i / 8f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * 5.5f, Mathf.Cos(a) * 3.2f, 1.5f);
                Rock(mb, p, new Vector3(3.2f, 2.6f, 3.4f), i * 2.3f, Color.Lerp(Tropic.Rock, Tropic.RockDark, Hash(i, 4)));
            }
            Rock(mb, new Vector3(-7f, 1.5f, 2f), new Vector3(4f, 4f, 4f), 31, Tropic.RockDark);
            Rock(mb, new Vector3(7f, 1.5f, 2f), new Vector3(4f, 4f, 4f), 37, Tropic.RockDark);
            mb.Surf = SurfaceKind.None;
            // The darkness inside.
            mb.Quad(new Vector3(-3.8f, 0.05f, 2.6f), new Vector3(-3.4f, 4.2f, 2.6f), new Vector3(3.4f, 4.2f, 2.6f), new Vector3(3.8f, 0.05f, 2.6f), Palette.Hex(0x0B0B0D));
            mb.Surf = SurfaceKind.Planks;
            mb.Box(new Vector3(-2.6f, 0.4f, 0.6f), new Vector3(0.8f, 0.8f, 0.8f), Palette.WoodPale);
            mb.Box(new Vector3(-2.4f, 1.15f, 0.7f), new Vector3(0.7f, 0.7f, 0.7f), Palette.Wood);
            // A lantern on a post.
            mb.Surf = SurfaceKind.WoodGrain;
            mb.Box(new Vector3(2.8f, 1.1f, 0.3f), new Vector3(0.12f, 2.2f, 0.12f), Palette.WoodDark);
            mb.Lit();
            mb.Box(new Vector3(2.8f, 2.25f, 0.3f), new Vector3(0.25f, 0.3f, 0.25f), Palette.Window);
            mb.Mat(0.15f);
            return mb;
        }

        /// <summary>Where the raiders' tents stand around their fire (offsets from the camp centre).</summary>
        public static readonly Vector3[] PirateTents = { new Vector3(-7, 0, 3), new Vector3(7, 0, 4), new Vector3(0, 0, 9), new Vector3(-9, 0, -5) };

        /// <summary>Red Jack's camp: patched tents, a fire with a spit, rum barrels and a black flag on a pole.</summary>
        public static MeshBuilder PirateCamp()
        {
            var mb = new MeshBuilder();
            Campfire(mb);
            for (int i = 0; i < PirateTents.Length; i++)
            {
                var t = PirateTents[i];
                var c = i % 2 == 0 ? Palette.Hex(0xC9B48A) : Palette.Hex(0x9A4A3A);
                mb.Push().Translate(t).RotateY(Mathf.Atan2(-t.x, -t.z) * Mathf.Rad2Deg);
                Vector3 a = new Vector3(-1.7f, 0, -1.6f), b = new Vector3(1.7f, 0, -1.6f), ridge0 = new Vector3(0, 2.3f, -1.8f), ridge1 = new Vector3(0, 2.3f, 1.8f), c0 = new Vector3(-1.7f, 0, 1.6f), d0 = new Vector3(1.7f, 0, 1.6f);
                mb.Quad(a, ridge0, ridge1, c0, c);
                mb.Quad(d0, ridge1, ridge0, b, MeshBuilder.Darken(c, 0.85f));
                mb.Tri(c0, ridge1, d0, MeshBuilder.Darken(c, 0.7f));
                mb.Surf = SurfaceKind.WoodGrain;
                mb.Cylinder(new Vector3(0, 0, 1.85f), new Vector3(0, 2.4f, 1.85f), 0.05f, 5, Palette.WoodDark);
                mb.Surf = SurfaceKind.None;
                mb.Pop();
            }
            // Barrels and a crate pile.
            for (int i = 0; i < 5; i++)
            {
                mb.Push().Translate(new Vector3(4.5f + (i % 2) * 0.9f, 0, -3.5f + i * 0.5f));
                mb.Surf = SurfaceKind.WoodGrain;
                mb.Cylinder(Vector3.zero, new Vector3(0, 1f, 0), 0.4f, 0.4f, 9, Palette.Wood);
                mb.Pop();
            }
            // The flag.
            mb.Surf = SurfaceKind.WoodGrain;
            mb.Cylinder(new Vector3(-3, 0, -2), new Vector3(-3, 7.5f, -2), 0.1f, 0.07f, 6, Palette.WoodDark);
            mb.Surf = SurfaceKind.None;
            mb.Quad(new Vector3(-3, 7.3f, -2), new Vector3(-0.9f, 7.1f, -2), new Vector3(-0.9f, 5.8f, -2), new Vector3(-3, 6.0f, -2), Palette.Hex(0x161616));
            mb.Quad(new Vector3(-3, 6.0f, -2), new Vector3(-0.9f, 5.8f, -2), new Vector3(-0.9f, 7.1f, -2), new Vector3(-3, 7.3f, -2), Palette.Hex(0x161616));
            mb.Sphere(new Vector3(-1.95f, 6.6f, -2.03f), new Vector3(0.22f, 0.24f, 0.02f), 6, Palette.White); // skull
            return mb;
        }

        /// <summary>An old wooden jetty on piles, running out from the beach (along +Z).</summary>
        public static MeshBuilder Pier(float length)
        {
            var mb = new MeshBuilder();
            for (float z = -4; z < length; z += 0.55f)
            {
                mb.Surf = SurfaceKind.Planks;
                var c = Color.Lerp(Palette.Hex(0xA88A62), Palette.Hex(0x8A6E4E), Hash(z, 3));
                mb.Box(new Vector3(0, Island.PierDeck - 0.05f, z), new Vector3(3.2f, 0.1f, 0.5f), c);
            }
            mb.Surf = SurfaceKind.WoodGrain;
            for (float z = 0; z <= length; z += 4f)
                foreach (float x in new[] { -1.5f, 1.5f })
                    mb.Cylinder(new Vector3(x, -4f, z), new Vector3(x, Island.PierDeck + (z % 8 == 0 ? 0.9f : 0.2f), z), 0.17f, 0.15f, 7, Palette.Hex(0x5E4632));
            // A rowing boat tied up at the end, and a stack of lobster pots.
            mb.Push().Translate(2.8f, 0.15f, length - 4).RotateY(8);
            mb.Surf = SurfaceKind.Planks;
            mb.Box(new Vector3(0, 0.25f, 0), new Vector3(1.3f, 0.5f, 3.6f), Palette.Hex(0x4E6A80));
            mb.Box(new Vector3(0, 0.45f, 0), new Vector3(1.1f, 0.1f, 3.3f), Palette.WoodPale);
            mb.Pop();
            mb.Surf = SurfaceKind.None;
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(-0.9f, Island.PierDeck + 0.25f + i * 0.4f, length - 2.5f), new Vector3(0.7f, 0.4f, 0.5f), Tropic.Rope);
            return mb;
        }

        // ------------------------------------------------------------------ camp structures

        /// <summary>A camp structure from the free kits (null when the art isn't there): the kit's campfire with a
        /// roasting spit, its workbench with an anvil, a canvas tent with a bedroll for the shelter, the pirate chest.</summary>
        public static GameObject StructureArt(string id, Transform parent)
        {
            (string art, Vector3 at, float yaw)[] parts;
            switch (id)
            {
                case "campfire": parts = new[] { ("campfire", Vector3.zero, 0f), ("spit", Vector3.zero, 0f) }; break;
                case "workbench": parts = new[] { ("workbench", Vector3.zero, 0f), ("anvil", new Vector3(1.9f, 0, 0.2f), 20f) }; break;
                case "shelter": parts = new[] { ("tent_canvas", Vector3.zero, 180f), ("bedroll", new Vector3(0.2f, 0.02f, 0.3f), 90f) }; break;
                case "chest": parts = new[] { ("chest", Vector3.zero, 0f), ("barrel", new Vector3(1.1f, 0, 0.3f), 0f) }; break;
                default: return null;
            }
            if (!ModelRegistry.Has(ModelRegistry.Free, parts[0].art)) return null;
            var holder = new GameObject("Built");
            holder.transform.SetParent(parent, false);
            foreach (var (art, at, yaw) in parts)
            {
                var go = ModelRegistry.Spawn(ModelRegistry.Free, art, holder.transform);
                if (go == null) continue;
                go.transform.localPosition = at;
                go.transform.localRotation = Quaternion.Euler(0, yaw, 0) * go.transform.localRotation;
            }
            return holder;
        }

        public static MeshBuilder Structure(string id)
        {
            var mb = new MeshBuilder();
            switch (id)
            {
                case "campfire": Campfire(mb); break;
                case "shelter": Shelter(mb); break;
                case "workbench": Workbench(mb); break;
                case "raincatcher": RainCatcher(mb); break;
                case "chest": Chest(mb); break;
                case "radio": Radio(mb); break;
            }
            return mb;
        }

        static void Campfire(MeshBuilder mb)
        {
            mb.Surf = SurfaceKind.Stone;
            for (int i = 0; i < 10; i++)
            {
                var p = Quaternion.Euler(0, i * 36, 0) * new Vector3(0.95f, 0, 0);
                Rock(mb, p + Vector3.up * 0.12f, new Vector3(0.5f, 0.38f, 0.42f), i * 1.7f, Color.Lerp(Tropic.Rock, Tropic.RockDark, (i % 3) * 0.4f));
            }
            mb.Surf = SurfaceKind.WoodGrain;
            for (int i = 0; i < 5; i++)
            {
                var r = Quaternion.Euler(0, i * 72, 0);
                mb.Cylinder(r * new Vector3(0.6f, 0.05f, 0), r * new Vector3(-0.05f, 0.75f, 0), 0.08f, 0.06f, 5, i % 2 == 0 ? Palette.WoodDark : Palette.Wood);
            }
            mb.Surf = SurfaceKind.None;
            mb.Cylinder(new Vector3(0, 0, 0), new Vector3(0, 0.06f, 0), 0.6f, 0.55f, 10, Palette.Hex(0x2A2420));
            // Spit for roasting.
            mb.Surf = SurfaceKind.WoodGrain;
            foreach (float x in new[] { -1.1f, 1.1f })
            {
                mb.Cylinder(new Vector3(x, 0, -0.1f), new Vector3(x * 0.9f, 1.2f, 0), 0.05f, 0.04f, 4, Palette.WoodDark);
                mb.Cylinder(new Vector3(x, 0, 0.1f), new Vector3(x * 0.9f, 1.2f, 0), 0.05f, 0.04f, 4, Palette.WoodDark);
            }
            mb.Cylinder(new Vector3(-1.2f, 1.15f, 0), new Vector3(1.2f, 1.15f, 0), 0.035f, 0.035f, 4, Palette.Wood);
        }

        static void Shelter(MeshBuilder mb)
        {
            // A lean-to: two forked posts, a ridge pole, rafters to the ground and a roof of palm thatch.
            mb.Surf = SurfaceKind.Bark;
            Color pole = Palette.Hex(0x7A5A3A);
            foreach (float x in new[] { -1.9f, 1.9f })
                mb.Cylinder(new Vector3(x, 0, 1f), new Vector3(x, 2.5f, 1f), 0.11f, 0.09f, 6, pole);
            mb.Cylinder(new Vector3(-2.3f, 2.45f, 1f), new Vector3(2.3f, 2.45f, 1f), 0.1f, 0.1f, 6, pole);
            for (int i = 0; i < 5; i++)
            {
                float x = -1.8f + i * 0.9f;
                mb.Cylinder(new Vector3(x, 2.5f, 1.05f), new Vector3(x, 0, -1.8f), 0.06f, 0.06f, 5, pole);
            }
            mb.Surf = SurfaceKind.None;
            // Thatch: overlapping rows of fronds.
            for (int row = 0; row < 5; row++)
            {
                float t0 = row / 5f, t1 = (row + 1.25f) / 5f;
                Vector3 a = Vector3.Lerp(new Vector3(0, 2.62f, 1.15f), new Vector3(0, 0.1f, -1.95f), t0), b = Vector3.Lerp(new Vector3(0, 2.62f, 1.15f), new Vector3(0, 0.1f, -1.95f), Mathf.Min(1, t1));
                var c = row % 2 == 0 ? Tropic.FrondDry : Color.Lerp(Tropic.FrondDry, Tropic.Frond, 0.35f);
                for (int k = 0; k < 6; k++)
                {
                    float x0 = -2.4f + k * 0.8f, x1 = x0 + 0.95f;
                    var jitter = Vector3.up * (Hash(row, k) * 0.08f);
                    mb.Quad(new Vector3(x0, a.y + 0.05f, a.z) + jitter, new Vector3(x1, a.y + 0.05f, a.z) + jitter, new Vector3(x1, b.y + 0.02f, b.z), new Vector3(x0, b.y + 0.02f, b.z), MeshBuilder.Darken(c, 0.9f + Hash(k, row) * 0.15f));
                    mb.Quad(new Vector3(x0, b.y, b.z), new Vector3(x1, b.y, b.z), new Vector3(x1, a.y, a.z), new Vector3(x0, a.y, a.z), MeshBuilder.Darken(c, 0.6f));
                }
            }
            // Bedding of leaves and a rolled sailcloth blanket inside.
            mb.Box(new Vector3(0, 0.08f, -0.2f), new Vector3(3f, 0.16f, 1.6f), Tropic.FrondDark);
            mb.Cylinder(new Vector3(-1.2f, 0.28f, 0.1f), new Vector3(0.6f, 0.28f, 0.1f), 0.2f, 0.2f, 8, Tropic.Sail);
        }

        static void Workbench(MeshBuilder mb)
        {
            mb.Surf = SurfaceKind.Planks;
            mb.Box(new Vector3(0, 0.95f, 0), new Vector3(2.6f, 0.14f, 1.1f), Palette.WoodPale);
            mb.Surf = SurfaceKind.WoodGrain;
            foreach (float x in new[] { -1.15f, 1.15f })
                foreach (float z in new[] { -0.42f, 0.42f })
                    mb.Box(new Vector3(x, 0.45f, z), new Vector3(0.14f, 0.9f, 0.14f), Palette.Wood);
            mb.Box(new Vector3(0, 0.3f, 0), new Vector3(2.3f, 0.08f, 0.9f), Palette.Wood);
            // A vice, a saw and an axe on the bench, offcuts on the shelf.
            mb.Box(new Vector3(-0.9f, 1.12f, 0.2f), new Vector3(0.3f, 0.2f, 0.25f), Tropic.Iron);
            mb.Box(new Vector3(0.4f, 1.04f, -0.1f), new Vector3(0.8f, 0.02f, 0.18f), Palette.Steel);
            mb.Box(new Vector3(0.6f, 0.4f, 0.1f), new Vector3(0.9f, 0.12f, 0.2f), Palette.WoodPale);
            mb.Box(new Vector3(-0.4f, 0.4f, -0.1f), new Vector3(0.7f, 0.12f, 0.2f), Palette.Wood);
            mb.Surf = SurfaceKind.Stone;
            Rock(mb, new Vector3(1.6f, 0.25f, 0.4f), new Vector3(0.6f, 0.5f, 0.5f), 4.4f, Tropic.Rock); // grindstone
        }

        static void RainCatcher(MeshBuilder mb)
        {
            mb.Surf = SurfaceKind.Bark;
            Color pole = Palette.Hex(0x7A5A3A);
            var corners = new[] { new Vector3(-1.3f, 0, -1.3f), new Vector3(1.3f, 0, -1.3f), new Vector3(1.3f, 0, 1.3f), new Vector3(-1.3f, 0, 1.3f) };
            foreach (var c in corners) mb.Cylinder(c, c + Vector3.up * 2.3f, 0.08f, 0.07f, 5, pole);
            mb.Surf = SurfaceKind.None;
            // Sailcloth funnel sagging to the middle.
            var mid = new Vector3(0, 1.2f, 0);
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i] + Vector3.up * 2.2f, b = corners[(i + 1) % 4] + Vector3.up * 2.2f;
                mb.Tri(a, b, mid, Tropic.Sail);
                mb.Tri(b, a, mid, MeshBuilder.Darken(Tropic.Sail, 0.75f));
            }
            // The barrel it drains into.
            mb.Surf = SurfaceKind.WoodGrain;
            mb.Cylinder(new Vector3(0, 0, 0), new Vector3(0, 1.0f, 0), 0.45f, 0.45f, 10, Palette.Wood);
            mb.Surf = SurfaceKind.None;
            mb.Mat(0.9f);
            mb.Cylinder(new Vector3(0, 0.9f, 0), new Vector3(0, 0.98f, 0), 0.4f, 0.4f, 10, Tropic.Water);
            mb.Mat(0.15f);
        }

        /// <summary>The ship's radio set salvaged from the wreck, on a crate under a little palm-leaf awning, with a wire aerial.</summary>
        static void Radio(MeshBuilder mb)
        {
            mb.Surf = SurfaceKind.Planks;
            mb.Box(new Vector3(0, 0.45f, 0), new Vector3(1.2f, 0.9f, 0.8f), Palette.WoodPale);
            mb.Surf = SurfaceKind.PaintedMetal;
            mb.Box(new Vector3(0, 1.15f, 0), new Vector3(0.9f, 0.5f, 0.55f), Palette.Hex(0x3E4A3A));
            mb.Lit();
            mb.Box(new Vector3(-0.12f, 1.2f, -0.28f), new Vector3(0.45f, 0.18f, 0.02f), Palette.Hex(0xE8A040)); // dial
            mb.Mat(0.15f);
            foreach (float x in new[] { 0.25f, 0.36f }) mb.Cylinder(new Vector3(x, 1.08f, -0.28f), new Vector3(x, 1.08f, -0.32f), 0.05f, 8, Palette.Hex(0x222222));
            mb.Surf = SurfaceKind.Bark;
            mb.Cylinder(new Vector3(0.9f, 0, 0.4f), new Vector3(0.9f, 4.5f, 0.4f), 0.06f, 0.04f, 5, Palette.Hex(0x7A5A3A));
            mb.Cylinder(new Vector3(0.9f, 4.5f, 0.4f), new Vector3(0.3f, 1.3f, 0.1f), 0.012f, 3, Tropic.Iron, false);
            mb.Surf = SurfaceKind.None;
            foreach (float x in new[] { -0.8f, 0.8f }) mb.Cylinder(new Vector3(x, 0, -0.6f), new Vector3(x, 2.1f, -0.6f), 0.05f, 5, Palette.Hex(0x7A5A3A));
            mb.Quad(new Vector3(-1f, 2.1f, -0.8f), new Vector3(1f, 2.1f, -0.8f), new Vector3(1f, 1.9f, 0.7f), new Vector3(-1f, 1.9f, 0.7f), Tropic.FrondDry);
            mb.Quad(new Vector3(-1f, 1.9f, 0.7f), new Vector3(1f, 1.9f, 0.7f), new Vector3(1f, 2.1f, -0.8f), new Vector3(-1f, 2.1f, -0.8f), MeshBuilder.Darken(Tropic.FrondDry, 0.7f));
        }

        static void Chest(MeshBuilder mb)
        {
            mb.Surf = SurfaceKind.Planks;
            mb.Box(new Vector3(0, 0.38f, 0), new Vector3(1.5f, 0.75f, 0.9f), Palette.Hex(0x7A4E2C));
            mb.Surf = SurfaceKind.WoodGrain;
            mb.Box(new Vector3(0, 0.86f, 0), new Vector3(1.55f, 0.22f, 0.95f), Palette.Hex(0x6A4426));
            mb.Surf = SurfaceKind.Rust;
            foreach (float x in new[] { -0.55f, 0.55f })
                mb.Box(new Vector3(x, 0.5f, 0), new Vector3(0.1f, 0.95f, 0.98f), Tropic.Iron);
            mb.Box(new Vector3(0, 0.66f, -0.48f), new Vector3(0.18f, 0.22f, 0.05f), Palette.Brass);
        }
    }
}
