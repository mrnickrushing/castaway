using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// Procedural models for the islands' own materials (Content.Islands): the hive, hull wreckage, chalk seam,
    /// tangled net, oil cask, salt crust and broken cannon, the bundle of plans at each landmark, and item icons for
    /// the materials and camp improvements.
    /// </summary>
    public static class IslandModels
    {
        static float H(float s, int i) { return Noise2.Hash(Mathf.RoundToInt(s * 13.7f) + i * 31, i * 7 + 3); }

        static readonly Color Honey = Palette.Hex(0xC8861E), HiveWax = Palette.Hex(0xB98A3E), HiveDark = Palette.Hex(0x6E4A1E),
            Oak = Palette.Hex(0x7A5A3E), OakPale = Palette.Hex(0x9A7A56), Chalk = Palette.Hex(0xE4E0D2), ChalkShade = Palette.Hex(0xC9C4B4),
            Net = Palette.Hex(0x5C6650), Float = Palette.Hex(0xC8542A), Oil = Palette.Hex(0x3A2E22), Band = Palette.Hex(0x2C2C30),
            Salt = Palette.Hex(0xF2F0EA), Brass = Palette.Hex(0xC89B3C), Bronze = Palette.Hex(0x6A5232), GunIron = Palette.Hex(0x34383C), Parchment = Palette.Hex(0xE8DCB8);

        /// <summary>The harvest node of a kind, or null when it isn't one of these.</summary>
        public static MeshBuilder Node(string kind, float seed)
        {
            var mb = new MeshBuilder();
            switch (kind)
            {
                case "hive":
                    // A palm stump with a wild comb hanging off it.
                    mb.Surf = SurfaceKind.Bark;
                    mb.Cylinder(Vector3.zero, new Vector3(0, 1.1f, 0), 0.42f, 0.36f, 9, Tropic.PalmBark);
                    mb.Surf = SurfaceKind.None;
                    HiveMesh(mb, new Vector3(0, 1.08f, 0), 1.2f);
                    break;
                case "hull":
                    // A section of broken hull: planks still nailed to their frame, propped up on two ribs.
                    mb.Push().RotateY(seed * 61 % 360);
                    mb.Mat(0.2f);
                    foreach (float z in new[] { -0.75f, 0.75f })
                        mb.Cylinder(new Vector3(-1.0f, 0.02f, z), new Vector3(0.55f, 0.95f, z * 0.9f), 0.11f, 0.08f, 7, OakPale);
                    mb.Surf = SurfaceKind.Planks;
                    mb.Push().Translate(-0.2f, 0.48f, 0).Rotate(Quaternion.Euler(0, 0, 31));
                    for (int i = 0; i < 5; i++)
                        mb.Box(new Vector3((i - 2) * 0.31f, 0.06f, (H(seed, i) - 0.5f) * 0.3f), new Vector3(0.29f, 0.07f, 2.0f - (i == 4 ? 0.7f : 0) - (i == 0 ? 0.4f : 0)), i % 2 == 0 ? Oak : OakPale, i % 2 == 0 ? OakPale : Oak);
                    mb.Surf = SurfaceKind.None; mb.Mat(0.4f, 0.8f);
                    for (int i = 0; i < 5; i++) foreach (float z in new[] { -0.7f, 0.7f }) mb.Sphere(new Vector3((i - 2) * 0.31f, 0.11f, z), Vector3.one * 0.035f, 5, Band);
                    mb.Pop();
                    mb.Surf = SurfaceKind.Planks; mb.Mat(0.2f);
                    mb.Push().Rotate(Quaternion.Euler(0, 20, 4));
                    mb.Box(new Vector3(0.9f, 0.05f, -0.4f), new Vector3(0.28f, 0.08f, 1.1f), OakPale);
                    mb.Pop();
                    mb.Pop();
                    break;
                case "limeseam":
                    // A grey rock split open by a seam of white chalk.
                    mb.Surf = SurfaceKind.Stone;
                    Models.Rock(mb, new Vector3(0, 0.5f, 0), new Vector3(1.6f, 1.1f, 1.3f), seed, Tropic.Rock);
                    mb.Surf = SurfaceKind.None;
                    for (int i = 0; i < 7; i++)
                    {
                        var p = Quaternion.Euler(0, i * 51 + seed * 30, 0) * new Vector3(0.55f + H(seed, i) * 0.4f, 0, 0);
                        float s = 0.16f + H(seed, i + 9) * 0.18f;
                        Models.Rock(mb, p + new Vector3(0, s * 0.4f + (i < 3 ? 0.6f : 0), 0), new Vector3(s * 1.3f, s, s * 1.1f), seed + i, i % 2 == 0 ? Chalk : ChalkShade);
                    }
                    break;
                case "oldnet":
                    // A mooring post with an old net heaped round it, cork floats and all.
                    mb.Surf = SurfaceKind.WoodGrain;
                    mb.Cylinder(Vector3.zero, new Vector3(0, 1.2f, 0), 0.13f, 0.11f, 7, Tropic.Drift);
                    mb.Surf = SurfaceKind.None; mb.Mat(0.15f);
                    for (int i = 0; i < 9; i++)
                    {
                        var p = Quaternion.Euler(0, i * 40 + seed * 20, 0) * new Vector3(0.35f + H(seed, i) * 0.45f, 0, 0);
                        mb.Sphere(p + Vector3.up * 0.1f, new Vector3(0.38f, 0.14f + H(seed, i + 3) * 0.1f, 0.3f), 7, Color.Lerp(Net, Tropic.Rope, H(seed, i + 6) * 0.4f));
                    }
                    mb.Cylinder(new Vector3(0, 0.9f, 0), new Vector3(0, 1.0f, 0), 0.18f, 0.18f, 9, Net);
                    for (int i = 0; i < 5; i++)
                        mb.Sphere(Quaternion.Euler(0, i * 72 + 15, 0) * new Vector3(0.75f, 0.2f, 0), Vector3.one * 0.08f, 6, Float);
                    break;
                case "oildrum":
                    OilCask(mb, Vector3.zero, 1f);
                    break;
                case "saltcrust":
                    // Dark rock crusted white where the tide dries.
                    mb.Surf = SurfaceKind.Stone;
                    for (int i = 0; i < 4; i++)
                    {
                        var p = Quaternion.Euler(0, i * 90 + seed * 30, 0) * new Vector3(0.5f + H(seed, i) * 0.3f, 0, 0);
                        float s = 0.4f + H(seed, i + 4) * 0.3f;
                        Models.Rock(mb, p + Vector3.up * s * 0.3f, new Vector3(s * 1.4f, s, s * 1.2f), seed + i, Tropic.RockDark);
                        mb.Surf = SurfaceKind.None; mb.Mat(0.5f);
                        Models.Rock(mb, p + Vector3.up * s * 0.62f, new Vector3(s * 1.15f, s * 0.28f, s), seed + i + 20, Salt);
                        mb.Surf = SurfaceKind.Stone;
                    }
                    break;
                case "brasswreck":
                    // A cannon off its carriage, the brass fittings scattered.
                    mb.Mat(0.45f, 0.6f);
                    mb.Push().RotateY(seed * 47 % 360).Rotate(Quaternion.Euler(0, 0, 8));
                    mb.Cylinder(new Vector3(-0.9f, 0.32f, 0), new Vector3(0.9f, 0.28f, 0), 0.3f, 0.22f, 12, GunIron);
                    mb.Sphere(new Vector3(-0.95f, 0.32f, 0), Vector3.one * 0.26f, 8, GunIron);
                    mb.Cylinder(new Vector3(0.88f, 0.28f, 0), new Vector3(0.96f, 0.28f, 0), 0.26f, 12, GunIron);   // the muzzle's lip
                    mb.Mat(0.6f, 0.85f);
                    foreach (float bx in new[] { -0.6f, 0.05f, 0.7f })  // brass reinforcing bands, the bit worth taking
                        mb.Cylinder(new Vector3(bx - 0.05f, 0.30f - bx * 0.022f, 0), new Vector3(bx + 0.05f, 0.30f - bx * 0.022f, 0), 0.325f - (bx + 0.9f) * 0.044f, 12, Brass);
                    mb.Pop();
                    mb.Surf = SurfaceKind.WoodGrain; mb.Mat(0.2f);
                    mb.Box(new Vector3(0.2f, 0.12f, 0.6f), new Vector3(1.2f, 0.2f, 0.3f), Oak);
                    mb.Cylinder(new Vector3(0.7f, 0.25f, -0.55f), new Vector3(0.7f, 0.25f, -0.45f), 0.25f, 10, Oak);
                    mb.Surf = SurfaceKind.None; mb.Mat(0.6f, 0.9f);
                    for (int i = 0; i < 4; i++)
                        mb.Cylinder(new Vector3(-0.5f + i * 0.35f, 0.03f, -0.7f + H(seed, i) * 0.3f), new Vector3(-0.5f + i * 0.35f, 0.08f, -0.7f + H(seed, i) * 0.3f), 0.09f, 8, Brass);
                    break;
                default: return null;
            }
            return mb;
        }

        /// <summary>The plans at a landmark: a small sea chest with a rolled chart and a lantern on it.</summary>
        public static MeshBuilder Plan(float seed)
        {
            var mb = new MeshBuilder { Surf = SurfaceKind.Planks };
            mb.Push().RotateY(seed * 53 % 360);
            mb.Box(new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.5f, 0.6f), Oak, OakPale);
            mb.Surf = SurfaceKind.None; mb.Mat(0.5f, 0.8f);
            mb.Box(new Vector3(0, 0.25f, 0.31f), new Vector3(0.12f, 0.16f, 0.03f), Brass);
            mb.Mat(0.2f);
            mb.Cylinder(new Vector3(-0.35f, 0.58f, -0.05f), new Vector3(0.3f, 0.58f, 0.1f), 0.08f, 10, Parchment);
            mb.Cylinder(new Vector3(-0.1f, 0.55f, 0.18f), new Vector3(0.4f, 0.55f, 0.2f), 0.06f, 10, Palette.Hex(0xD8C89A));
            mb.Mat(0.6f, 0, true);
            mb.Sphere(new Vector3(0.32f, 0.68f, -0.15f), Vector3.one * 0.07f, 6, Palette.Hex(0xFFD27A));
            mb.Pop();
            return mb;
        }

        /// <summary>Icon models for the materials and camp improvements (ModelIcons renders them).</summary>
        public static MeshBuilder Icon(string id)
        {
            var mb = new MeshBuilder();
            switch (id)
            {
                case "honey":
                    // A clay jar of honey with a dripping comb.
                    mb.Mat(0.3f);
                    mb.Cylinder(Vector3.zero, new Vector3(0, 0.75f, 0), 0.38f, 0.34f, 14, Palette.Hex(0xA8673A));
                    mb.Mat(0.85f);
                    mb.Cylinder(new Vector3(0, 0.74f, 0), new Vector3(0, 0.8f, 0), 0.32f, 0.32f, 14, Honey);
                    mb.Sphere(new Vector3(0.3f, 0.55f, 0.18f), new Vector3(0.07f, 0.16f, 0.07f), 6, Honey);
                    
                    break;
                case "ship_timber":
                    mb.Surf = SurfaceKind.WoodGrain;
                    for (int i = 0; i < 3; i++) mb.Box(new Vector3((i - 1) * 0.32f, 0.12f + (i == 1 ? 0.22f : 0), 0), new Vector3(0.3f, 0.22f, 1.5f), i == 1 ? OakPale : Oak);
                    mb.Surf = SurfaceKind.None; mb.Mat(0.4f, 0.8f);
                    foreach (float z in new[] { -0.5f, 0.5f }) mb.Sphere(new Vector3(0, 0.46f, z), Vector3.one * 0.05f, 5, Band);
                    break;
                case "lime":
                    mb.Mat(0.15f);
                    for (int i = 0; i < 5; i++) Models.Rock(mb, new Vector3((i % 3 - 1) * 0.35f, 0.18f + (i / 3) * 0.28f, (i % 2) * 0.2f - 0.1f), new Vector3(0.36f, 0.3f, 0.32f), i * 3.1f, i % 2 == 0 ? Chalk : ChalkShade);
                    break;
                case "net_cord":
                    mb.Mat(0.2f);
                    for (int i = 0; i < 6; i++) mb.Cylinder(new Vector3(0, i * 0.11f, 0), new Vector3(0, i * 0.11f + 0.1f, 0), 0.42f - (i % 2) * 0.03f, 0.42f - (i % 2) * 0.03f, 16, Color.Lerp(Net, Tropic.Rope, (i % 3) * 0.2f));
                    mb.Cylinder(new Vector3(0, -0.01f, 0), new Vector3(0, 0.68f, 0), 0.2f, 0.2f, 12, Palette.Hex(0x2A2A24));
                    mb.Sphere(new Vector3(0.45f, 0.1f, 0.25f), Vector3.one * 0.1f, 6, Float);
                    break;
                case "lamp_oil": OilCask(mb, Vector3.zero, 0.75f); break;
                case "sea_salt":
                    // A sack of salt, open at the top.
                    mb.Mat(0.1f);
                    mb.Sphere(new Vector3(0, 0.42f, 0), new Vector3(0.48f, 0.45f, 0.44f), 10, Palette.Hex(0xB9A57A));
                    mb.Cylinder(new Vector3(0, 0.75f, 0), new Vector3(0, 0.9f, 0), 0.3f, 0.36f, 12, Palette.Hex(0xA89468));
                    mb.Mat(0.5f);
                    mb.Sphere(new Vector3(0, 0.9f, 0), new Vector3(0.32f, 0.12f, 0.32f), 9, Salt);
                    break;
                case "brass":
                    mb.Mat(0.6f, 0.95f);
                    mb.Cylinder(new Vector3(-0.3f, 0, 0), new Vector3(-0.3f, 0.12f, 0), 0.34f, 14, Brass);
                    mb.Cylinder(new Vector3(-0.3f, 0, 0), new Vector3(-0.3f, 0.14f, 0), 0.14f, 12, Bronze);
                    mb.Box(new Vector3(0.35f, 0.25f, 0.05f), new Vector3(0.36f, 0.5f, 0.2f), Brass);
                    mb.Cylinder(new Vector3(0.35f, 0.25f, -0.06f), new Vector3(0.35f, 0.25f, 0.16f), 0.13f, 12, Bronze);
                    break;
                // The camp improvements: what they look like at camp.
                case "perk_larder":
                    mb.Surf = SurfaceKind.Planks;
                    mb.Box(new Vector3(0, 0.45f, 0), new Vector3(1.3f, 0.08f, 0.45f), OakPale);
                    mb.Box(new Vector3(0, 0.02f, 0), new Vector3(1.3f, 0.06f, 0.45f), OakPale);
                    foreach (float x in new[] { -0.62f, 0.62f }) mb.Box(new Vector3(x, 0.3f, 0), new Vector3(0.06f, 0.6f, 0.45f), Oak);
                    mb.Surf = SurfaceKind.None; mb.Mat(0.4f);
                    for (int i = 0; i < 3; i++) mb.Cylinder(new Vector3((i - 1) * 0.38f, 0.49f, 0), new Vector3((i - 1) * 0.38f, 0.82f, 0), 0.14f, 0.12f, 10, i == 1 ? Honey : Palette.Hex(0xA8673A));
                    break;
                case "perk_chest":
                    mb.Surf = SurfaceKind.Planks;
                    mb.Box(new Vector3(0, 0.3f, 0), new Vector3(1.2f, 0.6f, 0.7f), Oak, OakPale);
                    mb.Surf = SurfaceKind.None; mb.Mat(0.5f, 0.8f);
                    foreach (float x in new[] { -0.45f, 0, 0.45f }) mb.Box(new Vector3(x, 0.3f, 0), new Vector3(0.08f, 0.64f, 0.74f), Band);
                    mb.Box(new Vector3(0, 0.4f, 0.37f), new Vector3(0.14f, 0.18f, 0.03f), Brass);
                    break;
                case "perk_limewash":
                    mb.Mat(0.15f);
                    mb.Box(new Vector3(0, 0.4f, 0), new Vector3(1.1f, 0.8f, 0.12f), Chalk);
                    mb.Surf = SurfaceKind.None;
                    mb.Cylinder(new Vector3(0.35f, 0, 0.35f), new Vector3(0.35f, 0.3f, 0.35f), 0.16f, 0.15f, 10, Palette.Hex(0x7A6A55));
                    mb.Cylinder(new Vector3(0.35f, 0.28f, 0.35f), new Vector3(0.35f, 0.3f, 0.35f), 0.14f, 0.14f, 10, Chalk);
                    mb.Cylinder(new Vector3(-0.3f, 0.05f, 0.35f), new Vector3(0.1f, 0.6f, 0.3f), 0.03f, 6, Tropic.Drift);
                    break;
                case "perk_weir":
                    mb.Surf = SurfaceKind.WoodGrain;
                    for (int i = 0; i < 7; i++)
                    {
                        float a = Mathf.Lerp(-70, 70, i / 6f) * Mathf.Deg2Rad;
                        var p = new Vector3(Mathf.Sin(a) * 0.6f, 0, Mathf.Cos(a) * 0.6f - 0.2f);
                        mb.Cylinder(p, p + Vector3.up * 0.7f, 0.04f, 6, Tropic.Drift);
                    }
                    mb.Surf = SurfaceKind.None; mb.Mat(0.15f);
                    mb.Cylinder(new Vector3(0, 0.3f, 0.1f), new Vector3(0, 0.55f, 0.1f), 0.62f, 0.62f, 16, Net, false);
                    mb.Mat(0.6f, 0.4f);
                    mb.Sphere(new Vector3(0, 0.1f, 0.05f), new Vector3(0.3f, 0.08f, 0.1f), 8, Palette.Hex(0x8FA4AE));
                    break;
                case "perk_lamp":
                    mb.Mat(0.5f, 0.8f);
                    mb.Cylinder(Vector3.zero, new Vector3(0, 0.08f, 0), 0.3f, 12, Band);
                    mb.Cylinder(new Vector3(0, 0.08f, 0), new Vector3(0, 0.7f, 0), 0.22f, 0.22f, 8, Band, false);
                    mb.Mat(0.8f, 0, true);
                    mb.Sphere(new Vector3(0, 0.38f, 0), new Vector3(0.18f, 0.26f, 0.18f), 9, Palette.Hex(0xFFC86A));
                    mb.Mat(0.5f, 0.8f);
                    mb.Cylinder(new Vector3(0, 0.7f, 0), new Vector3(0, 0.88f, 0), 0.26f, 0.05f, 10, Band);
                    mb.Cylinder(new Vector3(0, 0.88f, 0), new Vector3(0, 0.98f, 0), 0.08f, 8, Brass);
                    break;
                case "perk_salt":
                    mb.Surf = SurfaceKind.WoodGrain;
                    mb.Cylinder(Vector3.zero, new Vector3(0, 0.9f, 0), 0.38f, 0.38f, 14, OakPale);
                    mb.Sphere(new Vector3(0, 0.45f, 0), new Vector3(0.43f, 0.3f, 0.43f), 12, OakPale);
                    mb.Surf = SurfaceKind.None; mb.Mat(0.5f, 0.8f);
                    foreach (float y in new[] { 0.15f, 0.75f }) mb.Cylinder(new Vector3(0, y, 0), new Vector3(0, y + 0.06f, 0), 0.41f, 0.41f, 14, Band);
                    mb.Mat(0.4f);
                    mb.Sphere(new Vector3(0, 0.9f, 0), new Vector3(0.34f, 0.08f, 0.34f), 10, Salt);
                    break;
                case "perk_sail":
                    mb.Mat(0.2f);
                    mb.Cylinder(Vector3.zero, new Vector3(0, 1.4f, 0), 0.05f, 6, Tropic.Drift);
                    mb.Box(new Vector3(0.43f, 0.75f, 0), new Vector3(0.72f, 1.1f, 0.02f), Tropic.Sail);
                    mb.Mat(0.6f, 0.95f);
                    foreach (float y in new[] { 0.25f, 1.3f }) mb.Cylinder(new Vector3(0.05f, y, -0.06f), new Vector3(0.05f, y, 0.06f), 0.1f, 10, Brass);
                    break;
                default: return null;
            }
            return mb;
        }

        /// <summary>Scanned art for the hive (a stump with the comb on it) and the plans (a crate with a lantern and the
        /// rolled plans on it), built around the scans' own size; null when the scans aren't there.</summary>
        public static GameObject Art(string kind, Transform parent, float h0)
        {
            string baseScan = kind == "hive" ? "tree_stump_01" : kind == "plan" ? "wooden_crate_02" : kind == "oildrum" ? "wine_barrel_01"
                : kind == "saltcrust" ? (h0 < 0.5f ? "rock_moss_set_01_b" : "rock_moss_set_01_d") : kind == "limeseam" ? "boulder_01" : null;
            if (baseScan == null || ModelRegistry.Prefab(ModelRegistry.Scan, baseScan) == null) return null;
            var holder = new GameObject("Model");
            holder.transform.SetParent(parent, false);
            var b = ModelRegistry.Spawn(ModelRegistry.Scan, baseScan, holder.transform);
            b.transform.localRotation = Quaternion.Euler(0, h0 * 360, 0) * b.transform.localRotation;
            if (kind == "plan") b.transform.localScale *= 0.85f;
            if (kind == "limeseam") b.transform.localScale *= 0.75f;
            var top = TopOf(holder.transform, b);
            var size = SizeOf(b);
            var mb = new MeshBuilder();
            if (kind == "saltcrust" || kind == "limeseam")
            {
                // Salt dried white round the foot of the rock where the tide leaves it, or broken chalk lying at
                // the foot of the seam, and a little on top, sunk into the rock.
                bool salt = kind == "saltcrust";
                mb.Mat(salt ? 0.55f : 0.15f);
                float rad = Mathf.Max(size.x, size.z) * 0.5f;
                for (int i = 0; i < 12; i++)
                {
                    float a = i * 2.4f + h0 * 6, r = rad * (0.75f + H(h0, i) * 0.45f);
                    float k = (salt ? 0.12f : 0.08f) + H(h0, i + 7) * 0.1f;
                    var c = i % 3 == 0 ? (salt ? Salt : ChalkShade) : (salt ? Palette.Hex(0xE6E4DC) : Chalk);
                    if (salt) mb.Sphere(new Vector3(top.x + Mathf.Cos(a) * r, 0.0f, top.z + Mathf.Sin(a) * r), new Vector3(k * 2.4f, 0.04f, k * 1.9f), 8, c);
                    else Models.Rock(mb, new Vector3(top.x + Mathf.Cos(a) * r, k * 0.35f, top.z + Mathf.Sin(a) * r), new Vector3(k * 1.4f, k, k * 1.2f), h0 * 10 + i, c);
                }
                for (int i = 0; i < 2; i++)
                    mb.Sphere(new Vector3(top.x + (i - 0.5f) * 0.12f, top.y - 0.06f, top.z + (i - 0.5f) * 0.08f), new Vector3(0.12f, 0.05f, 0.1f), 7, salt ? Salt : Chalk);
            }
            else if (kind == "oildrum")
            {
                mb.Mat(0.9f);
                mb.Sphere(new Vector3(top.x + 0.35f, 0.01f, top.z + 0.25f), new Vector3(0.45f, 0.01f, 0.32f), 8, Palette.Hex(0x1C1612)); // the oil it leaks
            }
            else if (kind == "hive") HiveMesh(mb, new Vector3(top.x, top.y - 0.02f, top.z), 1.25f);
            else
            {
                mb.Mat(0.2f);
                mb.Cylinder(new Vector3(-0.3f, top.y + 0.07f, -0.08f), new Vector3(0.25f, top.y + 0.07f, 0.06f), 0.07f, 10, Parchment);
                mb.Cylinder(new Vector3(-0.15f, top.y + 0.06f, 0.15f), new Vector3(0.28f, top.y + 0.06f, 0.18f), 0.055f, 10, Palette.Hex(0xD8C89A));
            }
            var extra = mb.ToObject("Extra", holder.transform);
            if (kind == "plan")
            {
                var lamp = ModelRegistry.Spawn(ModelRegistry.Scan, "wooden_lantern_01", holder.transform);
                if (lamp != null)
                {
                    lamp.transform.localPosition = new Vector3(0.22f, top.y, -0.2f);
                    var light = new GameObject("Glow").AddComponent<Light>();
                    light.type = LightType.Point; light.range = 4; light.intensity = 1.2f; light.color = new Color(1f, 0.8f, 0.45f);
                    light.transform.SetParent(holder.transform, false); light.transform.localPosition = new Vector3(0.22f, top.y + 0.25f, -0.2f);
                }
            }
            return holder;
        }

        /// <summary>The top of a model's bounds, in the holder's space (y = height of its top, x/z = its centre).</summary>
        static Vector3 TopOf(Transform holder, GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Vector3(0, 0.5f, 0);
            var bb = rs[0].bounds;
            foreach (var r in rs) bb.Encapsulate(r.bounds);
            var c = holder.InverseTransformPoint(bb.center);
            return new Vector3(c.x, holder.InverseTransformPoint(bb.max).y, c.z);
        }

        static Vector3 SizeOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return Vector3.one;
            var bb = rs[0].bounds;
            foreach (var r in rs) bb.Encapsulate(r.bounds);
            return bb.size;
        }

        /// <summary>A straw skep, the old beekeeper's: coiled bands narrowing to the top, a dark doorway, honey
        /// running from it. <paramref name="at"/> is the middle of its base.</summary>
        static void HiveMesh(MeshBuilder mb, Vector3 at, float s)
        {
            mb.Mat(0.25f);
            Color straw = Palette.Hex(0xC9A24E), strawDark = Palette.Hex(0x9C7A32);
            for (int i = 0; i < 6; i++)
            {
                float r0 = (0.34f - i * 0.045f) * s, r1 = (0.34f - (i + 1) * 0.045f) * s;
                mb.Cylinder(at + Vector3.up * (i * 0.1f * s), at + Vector3.up * ((i + 1) * 0.1f * s), r0, Mathf.Max(0.05f * s, r1), 14, i % 2 == 0 ? straw : strawDark);
                mb.Cylinder(at + Vector3.up * ((i + 0.85f) * 0.1f * s), at + Vector3.up * ((i + 1.05f) * 0.1f * s), r0 * 0.97f + 0.012f * s, r1 + 0.012f * s, 14, strawDark);
            }
            mb.Sphere(at + Vector3.up * 0.6f * s, new Vector3(0.08f, 0.05f, 0.08f) * s, 8, straw);
            mb.Mat(0.1f);
            mb.Sphere(at + new Vector3(0, 0.05f * s, 0.31f * s), new Vector3(0.08f, 0.05f, 0.03f) * s, 8, Palette.Hex(0x1A1208));
            mb.Mat(0.85f);
            mb.Sphere(at + new Vector3(0.05f * s, 0.02f * s, 0.32f * s), new Vector3(0.04f, 0.08f, 0.03f) * s, 6, Honey);
        }

        static void OilCask(MeshBuilder mb, Vector3 at, float s)
        {
            mb.Surf = SurfaceKind.WoodGrain;
            mb.Cylinder(at, at + new Vector3(0, 1.0f * s, 0), 0.42f * s, 0.42f * s, 14, Oil);
            mb.Sphere(at + new Vector3(0, 0.5f * s, 0), new Vector3(0.47f, 0.36f, 0.47f) * s, 12, Oil);
            mb.Surf = SurfaceKind.None; mb.Mat(0.5f, 0.8f);
            foreach (float y in new[] { 0.12f, 0.5f, 0.88f }) mb.Cylinder(at + new Vector3(0, y * s, 0), at + new Vector3(0, (y + 0.06f) * s, 0), 0.47f * s, 0.47f * s, 14, Band);
            mb.Mat(0.9f);
            mb.Sphere(at + new Vector3(0.25f * s, 0.02f, 0.2f * s), new Vector3(0.3f, 0.01f, 0.25f) * s, 8, Palette.Hex(0x1C1612)); // an oily stain
            mb.Mat(0.5f, 0.9f);
            mb.Cylinder(at + new Vector3(0, 1.0f * s, 0.15f * s), at + new Vector3(0, 1.08f * s, 0.15f * s), 0.06f * s, 8, Brass);
        }
    }
}
