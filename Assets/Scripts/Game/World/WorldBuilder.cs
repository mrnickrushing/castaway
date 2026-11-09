using System.Collections.Generic;
using LastTide.Core;
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
            ("radio",       new Vector3(-2, 0, 11),    180, 2.4f, 1.0f, "radio"),
        };

        /// <summary>Harvest keys on this island start with this (empty at home, "grove:" at Palm Grove...).</summary>
        static string _prefix = "";

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
            _prefix = Session.ZonePrefix(Island.Location.Id);
            var nodes = new GameObject("Island things").transform;
            nodes.SetParent(world, false);
            BagSpot.Create(nodes);
            if (!Island.Location.Home) { PopulateZone(world, nodes); return; }

            PieceViews.Create(world);

            // Drowned sailors' bags washed up around the wreck.
            for (int k = 0; k < 3; k++)
            {
                var b = Find(q => Vector3.Distance(q, Island.Wreck) < 30 && Vector3.Distance(q, Island.Wreck) > 12 && Vector3.Distance(q, Island.Start) > 9 && Island.Distance01(q.x, q.z) > 0.8f, 4f, Island.Wreck + new Vector3(-16 + k * 6, 0, 10));
                LootSpot.Create(nodes, _prefix + "lootbag#" + k, Island.Ground(b), k * 77);
                Take(b, 4f);
            }

            // The raft on the beach that takes the castaway out to the rest of the archipelago.
            var raftAt = Island.Start + new Vector3(-12, 0, -4);
            ExitSpot.Create(nodes, Island.Ground(raftAt), 200);
            Take(raftAt, 4f);

            // Camp blueprints.
            foreach (var s in Sites)
            {
                var p = Island.Ground(Island.Camp + s.offset);
                SiteSpot.Create(nodes, s.id, p, s.yaw, s.ring, s.footprint, s.from);
                Take(p, 4f);
            }
            Take(Island.Camp, Island.CampRadius * 0.5f);

            // The wreck on the beach, and what washed out of it.
            // The Gull: the scanned Dutch merchantman run aground, heeled over and settled into the sand.
            GameObject wreck = null;
            if (ModelRegistry.Prefab(ModelRegistry.Scan, "dutch_ship_large_01") != null)
            {
                wreck = ModelRegistry.Spawn(ModelRegistry.Scan, "dutch_ship_large_01", world);
                wreck.name = "Wreck";
                wreck.transform.position = Island.Ground(Island.Wreck) + Vector3.down * 2.2f;
                wreck.transform.rotation = Quaternion.Euler(0, -28 + 90, 0) * Quaternion.Euler(4, 0, 17) * wreck.transform.rotation;
                wreck.transform.localScale *= 0.85f;
            }
            if (wreck == null) wreck = ModelRegistry.Free_("wreck", world, Island.Ground(Island.Wreck) + Vector3.down * 0.9f, -28 + 90);
            if (wreck == null)
            {
                wreck = Models.Wreck().ToObject("Wreck", world);
                wreck.transform.position = Island.Ground(Island.Wreck) + Vector3.down * 0.6f;
            }
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
            bool palmArt = ModelRegistry.Has("Trees", "palm");
            for (int i = 0; i < 1500 && palms < 180; i++)
            {
                var p = RandomOnIsland();
                float d = Island.Distance01(p.x, p.z);
                bool beach = d > 0.66f && d < 0.9f;
                float grove = Mathf.PerlinNoise(p.x * 0.03f + 7, p.z * 0.03f + 3);
                if (!beach && grove < 0.55f) continue;
                if (Island.Slope(p.x, p.z) > 0.35f || !Free(p, 3.2f)) continue;
                float tall = 6.5f + R() * 3f;
                NodeSpot.Create(nodes, "palm", "palm#" + palms, Island.Ground(p), palmArt ? new MeshBuilder() : Models.Palm(palms, tall), 1.9f, 0.45f);
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
            Jungle(world, 170, 0.48f);

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
                if (d < 0.74f && ScanPlant(world, g, pick, i)) { decor.Pop(); continue; }
                if (d < 0.8f && ScanPlants.Length > 0 && ModelRegistry.Prefab(ModelRegistry.Scan, ScanPlants[0]) != null) { decor.Pop(); continue; }
                if (d < 0.74f) decor.Append(pick < 0.45f ? Models.Fern(i + 100, false) : Models.Tuft(i, pick > 0.85f));
                else if (d < 0.8f) decor.Append(Models.Tuft(i, false));
                else if (pick < 0.3f) Models.Rock(decor, new Vector3(0, 0.05f, 0), new Vector3(0.3f, 0.18f, 0.25f), i, Tropic.Rock);
                decor.Pop();
            }
            decor.ToObject("Undergrowth", world, false);
            var grass = GrassField.Build(world, 1.6f, 7);
            if (grass != null)
                foreach (var piece in G.Session.State.pieces)
                {
                    if (Content.Pieces[piece.id].Kind == PieceKind.Edge) continue;
                    Vector3 at; Quaternion turn;
                    CampGrid.Pose(piece, out at, out turn);
                    grass.ClearAround(at, Content.CellSize * 0.75f);
                }

            // Enemy dens in the jungle north of camp.
            var dens = new GameObject("Boars").AddComponent<Dens>();
            dens.transform.SetParent(world, false);
            for (int i = 0; i < 6; i++)
            {
                var p = Find(q => Island.Distance01(q.x, q.z) < 0.62f && q.z > -25 && Vector3.Distance(q, Island.Camp) > 40, 14f, new Vector3(-30 + i * 12, 0, 10));
                dens.Add("boar", p);
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
                NodeSpot.Create(parent, kind, _prefix + kind + "#" + made, Island.Ground(p), model(kind, made), ring, obstacle);
                Take(p, space);
                made++;
            }
        }

        /// <summary>
        /// A location island: the landing beach with the raft, its landmark (lighthouse, smugglers' cave, pirate camp,
        /// fishing pier), the things to harvest the location lists, jungle and undergrowth, the survivor waiting for
        /// rescue if the radio sent the castaway here, and the dens of whatever lives there.
        /// </summary>
        static void PopulateZone(Transform world, Transform nodes)
        {
            var loc = Island.Location;
            ExitSpot.Create(nodes, Island.Ground(Island.Start + new Vector3(5f, 0, -3.5f)), 160);
            Take(Island.Camp, 9f);
            Take(Island.Start, 5f);
            Landmark(world, nodes, loc);

            // The rescue (if the radio sent the castaway here).
            foreach (var sig in World.Signals)
                if (sig.Location == loc.Id)
                {
                    var at = Find(q => Vector3.Distance(q, Island.Camp) > 10 && Vector3.Distance(q, Island.Camp) < 22, 4f, Island.Camp + new Vector3(8, 0, 8));
                    NpcSpot.Create(nodes, sig, Island.Ground(at));
                    Take(at, 4f);
                }

            foreach (var (kind, n) in loc.Nodes) Place(nodes, kind, n);
            // Bags of the drowned, and on Driftwood Shore the Gull's dog waiting beside his master's.
            for (int k = 0; k < 3; k++)
            {
                var b = Find(q => Island.Distance01(q.x, q.z) < 0.9f && Vector3.Distance(q, Island.Camp) > 8, 6f, new Vector3(k * 7 - 7, 0, 0));
                LootSpot.Create(nodes, _prefix + "lootbag#" + k, Island.Ground(b), k * 91);
                Take(b, 5f);
                if (k == 0 && loc.Biome == Biome.Shore) PetSpot.Create(nodes, Island.Ground(b + new Vector3(1.8f, 0, 0.6f)));
            }
            Jungle(world, loc.Biome == Biome.Grove ? 50 : loc.Biome == Biome.Shore || loc.Biome == Biome.Pier ? 14 : 28, 0.45f);
            Undergrowth(world, 600);
            GrassField.Build(world, loc.Biome == Biome.Grove ? 2f : 1.2f, loc.Id.GetHashCode());

            var dens = new GameObject("Dens").AddComponent<Dens>();
            dens.transform.SetParent(world, false);
            foreach (var (kind, n) in loc.Enemies)
                for (int i = 0; i < n; i++)
                {
                    var p = Find(q => Vector3.Distance(q, Island.Camp) > 22 && (kind != "crab" || Island.Distance01(q.x, q.z) > 0.7f), 10f, new Vector3(i * 9 - 10, 0, 12));
                    dens.Add(kind, p);
                    Take(p, 8f);
                }

            // Red Jack by his fire, and his ship lying off the camp.
            if (loc.Biome == Biome.PirateCamp)
            {
                dens.Add("redjack", Island.Landmark + new Vector3(0, 0, 4.5f));
                var shore = Island.Landmark;
                for (float z = Island.Landmark.z; z < Island.Radius * 1.5f; z += 1) { shore = new Vector3(Island.Radius * 0.35f, 0, z); if (Island.Distance01(shore.x, shore.z) > 0.9f) break; }
                var ship = ModelRegistry.Free_("pirate_ship", world, new Vector3(shore.x + 6, -0.6f, shore.z + 20), 70);
                if (ship != null) ship.AddComponent<Bob>();
                ShipSpot.Create(nodes, Island.Ground(shore));
            }
        }

        /// <summary>Places <paramref name="n"/> harvestable things of a kind where that kind belongs.</summary>
        static void Place(Transform nodes, string kind, int n)
        {
            switch (kind)
            {
                case "palm":
                    bool art = ModelRegistry.Has("Trees", "palm");
                    Count("palm", n, nodes, p => Island.Slope(p.x, p.z) < 0.35f && Island.Distance01(p.x, p.z) < 0.92f, 3.2f, (k, i) => art ? new MeshBuilder() : Models.Palm(i, 6.5f + R() * 3), 1.9f, 0.45f);
                    break;
                case "boulder":
                    Count("boulder", n, nodes, p => Island.Distance01(p.x, p.z) < 0.8f, 4.5f,
                        (k, i) => Models.Boulder(i * 1.3f, new Vector3(2.2f + R(), 1.6f + R() * 0.8f, 2f + R()), Color.Lerp(Tropic.Rock, Tropic.RockDark, R() * 0.6f)), 2.2f, 1.1f);
                    break;
                case "ironvein":
                    Count("ironvein", n, nodes, p => Island.Distance01(p.x, p.z) < 0.8f, 4.5f, (k, i) => Models.IronVein(i), 2.2f, 1.0f);
                    break;
                case "stones": Count("stones", n, nodes, p => Island.Distance01(p.x, p.z) < 0.85f, 3f, (k, i) => Models.Stones(i), 1.6f, 0); break;
                case "bush": Count("bush", n, nodes, p => Island.Distance01(p.x, p.z) < 0.8f, 2.6f, (k, i) => Models.Fern(i, false), 1.6f, 0); break;
                case "berrybush": Count("berrybush", n, nodes, p => Island.Distance01(p.x, p.z) < 0.75f, 2.6f, (k, i) => Models.Fern(i + 50, true), 1.6f, 0); break;
                case "driftwood": Count("driftwood", n, nodes, p => { float d = Island.Distance01(p.x, p.z); return d > 0.78f && d < 0.96f; }, 3f, (k, i) => Models.Driftwood(i), 1.7f, 0); break;
                case "tidepool": Count("tidepool", n, nodes, p => { float d = Island.Distance01(p.x, p.z); return d > 0.88f && d < 0.98f; }, 5f, (k, i) => Models.TidePool(i), 2.2f, 0); break;
                case "crate": Count("crate", n, nodes, p => Island.Distance01(p.x, p.z) < 0.9f, 3f, (k, i) => Models.Crate(i + 1), 1.7f, 0.7f); break;
                case "barrel": Count("barrel", n, nodes, p => Island.Distance01(p.x, p.z) < 0.9f, 3f, (k, i) => Models.Barrel(i + 3), 1.6f, 0.5f); break;
                case "treasure": Count("treasure", n, nodes, p => Island.Distance01(p.x, p.z) < 0.6f && Vector3.Distance(p, Island.Camp) > 20, 6f, (k, i) => Models.Treasure(i), 1.8f, 0.8f); break;
                case "fishspot":
                    // Off the end and sides of the pier, else along the water's edge.
                    int made = 0;
                    if (Island.Biome == Biome.Pier)
                        foreach (var p in PierSpots())
                        {
                            if (made >= n) break;
                            NodeSpot.Create(nodes, "fishspot", _prefix + "fishspot#" + made, new Vector3(p.x, 0.9f, p.z), Models.FishSpot(), 1.6f, 0);
                            made++;
                        }
                    for (int i = 0; i < 3000 && made < n; i++)
                    {
                        var p = RandomOnIsland();
                        float d = Island.Distance01(p.x, p.z);
                        if (d < 0.97f || d > 1.03f || !Free(p, 6f, true)) continue;
                        NodeSpot.Create(nodes, "fishspot", _prefix + "fishspot#" + made, Island.Ground(p), Models.FishSpot(), 1.8f, 0);
                        Take(p, 6f);
                        made++;
                    }
                    break;
            }
        }

        /// <summary>The landmark that makes each location what it is.</summary>
        static void Landmark(Transform world, Transform nodes, LocationDef loc)
        {
            var at = Island.Ground(Island.Landmark);
            switch (loc.Biome)
            {
                case Biome.Lighthouse:
                {
                    var go = Models.Lighthouse().ToObject("Lighthouse", world);
                    go.transform.position = at;
                    Obstacles.Add(at, 3.4f);
                    Take(at, 9f);
                    var light = new GameObject("Beam").AddComponent<Light>();
                    light.type = LightType.Point; light.range = 18; light.intensity = 3; light.color = new Color(1, 0.9f, 0.6f);
                    light.transform.SetParent(go.transform, false); light.transform.localPosition = new Vector3(0, 15.5f, 0);
                    break;
                }
                case Biome.Cave:
                {
                    var go = Models.ScanCave(world, at);
                    bool scanned = go != null;
                    if (!scanned) go = ModelRegistry.Free_("cliff_cave", world, at + new Vector3(0, -0.3f, 2.5f), 180, 1.3f);
                    if (go != null && !scanned)
                    {
                        ModelRegistry.Free_("cliff", world, at + new Vector3(-10.5f, -0.3f, 3.5f), 180, 1.2f);
                        ModelRegistry.Free_("cliff", world, at + new Vector3(10.5f, -0.3f, 3.5f), 180, 1.2f);
                        ModelRegistry.Free_("crate", world, Island.Ground(at + new Vector3(-3, 0, -2)), 15);
                        ModelRegistry.Free_("barrel", world, Island.Ground(at + new Vector3(3.2f, 0, -1.5f)));
                    }
                    else if (!scanned) { go = Models.CaveMouth().ToObject("Cave", world); go.transform.position = at; }
                    foreach (float x in new[] { -6f, -3.5f, 3.5f, 6f }) Obstacles.Add(at + new Vector3(x, 0, 1.5f), 1.8f);
                    Take(at, 10f); Take(at + new Vector3(0, 0, -9), 9f); // a clear view of the mouth from the path
                    break;
                }
                case Biome.PirateCamp:
                {
                    GameObject go = Models.ScanPirateCamp(world, at);
                    if (go != null)
                    {
                        Obstacles.Add(at + new Vector3(11, 0, 9), 0.1f);
                        foreach (var c in new[] { new Vector3(-5, 0, -8), new Vector3(5, 0, -8) }) Obstacles.Add(at + c, 1.1f);
                        for (int i = 0; i < 9; i++)
                        {
                            float a = Mathf.Lerp(20, 160, i / 8f) * Mathf.Deg2Rad;
                            Obstacles.Add(at + new Vector3(Mathf.Cos(a) * 15, 0, Mathf.Sin(a) * 15), 1.6f);
                        }
                    }
                    else if (ModelRegistry.Has(ModelRegistry.Free, "tent"))
                    {
                        go = new GameObject("Pirate camp");
                        go.transform.SetParent(world, false);
                        go.transform.position = at;
                        ModelRegistry.Free_("campfire", go.transform, at);
                        ModelRegistry.Free_("spit", go.transform, at);
                        foreach (var t in Models.PirateTents) ModelRegistry.Free_("tent", go.transform, Island.Ground(at + t), Mathf.Atan2(-t.x, -t.z) * Mathf.Rad2Deg);
                        ModelRegistry.Free_("flag_pirate", go.transform, Island.Ground(at + new Vector3(-3, 0, -2)));
                        ModelRegistry.Free_("watchtower", go.transform, Island.Ground(at + new Vector3(11, 0, 9)), 200);
                        Obstacles.Add(at + new Vector3(11, 0, 9), 2.2f);
                        foreach (var c in new[] { new Vector3(-5, 0, -8), new Vector3(5, 0, -8) }) { ModelRegistry.Free_("cannon", go.transform, Island.Ground(at + c), 180); Obstacles.Add(at + c, 0.9f); }
                        ModelRegistry.Free_("crate_bottles", go.transform, Island.Ground(at + new Vector3(4.5f, 0, -3)), 30);
                        for (int i = 0; i < 4; i++) ModelRegistry.Free_("barrel", go.transform, Island.Ground(at + new Vector3(5.6f + (i % 2) * 0.9f, 0, -1.5f + i * 0.6f)));
                        // A palisade around the back of the camp.
                        for (int i = 0; i < 9; i++)
                        {
                            float a = Mathf.Lerp(20, 160, i / 8f) * Mathf.Deg2Rad;
                            var pp = at + new Vector3(Mathf.Cos(a) * 15, 0, Mathf.Sin(a) * 15);
                            ModelRegistry.Free_("palisade", go.transform, Island.Ground(pp), 90 - a * Mathf.Rad2Deg);
                            Obstacles.Add(pp, 1.6f);
                        }
                    }
                    else { go = Models.PirateCamp().ToObject("Pirate camp", world); go.transform.position = at; }
                    foreach (var t in Models.PirateTents) { Obstacles.Add(at + t, 2.0f); }
                    Take(at, 14f);
                    var fire = Flames.Create(go.transform, new Vector3(0, 0.1f, 0));
                    var l = new GameObject("Fire").AddComponent<Light>(); l.type = LightType.Point; l.range = 10; l.intensity = 2.5f; l.color = new Color(1f, 0.6f, 0.25f);
                    l.transform.SetParent(go.transform, false); l.transform.localPosition = Vector3.up * 1.2f;
                    Obstacles.Add(at, 1.2f);
                    break;
                }
                case Biome.Ridge:
                {
                    // Ancient ruins in the pass: whoever lived here before the pirates.
                    var r0 = Island.Ground(new Vector3(-6, 0, Island.Radius * 0.15f));
                    if (Models.ScanRuins(world) != null)
                    {
                        Obstacles.Add(r0, 1.6f); Take(r0, 6f);
                        foreach (var (x, zf, r) in new[] { (7f, 0.12f, 2.6f), (3f, 0.24f, 2.2f), (-12f, 0.05f, 2.2f), (12f, 0.22f, 2.2f), (-1f, 0.32f, 2.4f) })
                        { var q = new Vector3(x, 0, Island.Radius * zf); Obstacles.Add(q, r); Take(q, r + 2); }
                        break;
                    }
                    ModelRegistry.Free_("statue_head", world, r0 + Vector3.down * 0.3f, 160);
                    ModelRegistry.Free_("obelisk", world, Island.Ground(new Vector3(7, 0, Island.Radius * 0.12f)), 10);
                    ModelRegistry.Free_("ruin_column", world, Island.Ground(new Vector3(3, 0, Island.Radius * 0.22f)), 70);
                    ModelRegistry.Free_("ruin_column", world, Island.Ground(new Vector3(-12, 0, Island.Radius * 0.05f)), 200);
                    Obstacles.Add(r0, 1.6f); Take(r0, 6f);
                    break;
                }
                case Biome.Shore:
                {
                    for (int i = 0; i < 6; i++)
                    {
                        var p = Find(q => { float d = Island.Distance01(q.x, q.z); return d > 0.82f && d < 0.95f; }, 6f, Vector3.zero);
                        ModelRegistry.Free_(i % 2 == 0 ? "rocks_sand_a" : "rocks_sand_b", world, Island.Ground(p), R() * 360);
                        Take(p, 4f);
                    }
                    var c = Find(q => { float d = Island.Distance01(q.x, q.z); return d > 0.85f && d < 0.95f; }, 6f, Vector3.zero);
                    ModelRegistry.Free_("canoe", world, Island.Ground(c), R() * 360);
                    break;
                }
                case Biome.Pier:
                {
                    var go = Models.Pier(PierLength).ToObject("Pier", world);
                    go.transform.position = new Vector3(at.x, 0, at.z);
                    go.transform.rotation = Quaternion.Euler(0, PierYaw, 0);
                    Take(at, 8f);
                    break;
                }
            }
        }

        const float PierLength = 26f, PierYaw = 125f;

        /// <summary>Fishing spots along the pier (world positions on the deck).</summary>
        static IEnumerable<Vector3> PierSpots()
        {
            var at = Island.Landmark;
            var rot = Quaternion.Euler(0, PierYaw, 0);
            yield return at + rot * new Vector3(0, 0, PierLength - 1.5f);
            yield return at + rot * new Vector3(1.6f, 0, PierLength - 7f);
            yield return at + rot * new Vector3(-1.6f, 0, PierLength - 12f);
        }

        /// <summary>True on the pier deck (it can be walked on over the water).</summary>
        public static bool OnPier(float x, float z)
        {
            if (Island.Biome != Biome.Pier) return false;
            var local = Quaternion.Euler(0, -PierYaw, 0) * (new Vector3(x, 0, z) - Island.Landmark);
            return Mathf.Abs(local.x) < 1.6f && local.z > -4f && local.z < PierLength;
        }

        static readonly string[] Trees = { "tree_oak", "tree_fat", "tree_detailed" };
        static readonly string[] Plants = { "bush_large", "bush_detailed", "bush_detailed", "flower_red", "flower_yellow", "flower_purple", "mushrooms", "log", "stump" };

        static void Jungle(Transform world, int count, float maxD)
        {
            var canopy = new MeshBuilder();
            bool art = ModelRegistry.Has(ModelRegistry.Free, Trees[0]);
            var holder = new GameObject("Jungle trees").transform;
            holder.SetParent(world, false);
            int trees = 0;
            for (int i = 0; i < count * 15 && trees < count; i++)
            {
                var p = RandomOnIsland();
                float d = Island.Distance01(p.x, p.z);
                if (d > maxD + 0.2f || Island.Slope(p.x, p.z) > 0.45f || !Free(p, 4.5f)) continue;
                if (Vector3.Distance(p, Island.Camp) < Island.CampRadius + 6) continue;
                var g = Island.Ground(p);
                // Every other jungle tree is a palm: lighter to draw, and the island reads as tropical.
                if (art && trees % 2 == 1 && ModelRegistry.Has("Trees", "palm"))
                {
                    var palm = ModelRegistry.Spawn("Trees", "palm", holder);
                    palm.transform.position = g + Vector3.down * 0.1f;
                    palm.transform.rotation = Quaternion.Euler(0, R() * 360, 0) * palm.transform.rotation;
                    palm.transform.localScale *= 0.75f + R() * 0.35f;
                }
                else if (art) ModelRegistry.Free_(Trees[trees % Trees.Length], holder, g + Vector3.down * 0.1f, R() * 360, 0.8f + R() * 0.45f);
                else
                {
                    canopy.Push().Translate(g + Vector3.down * 0.2f).RotateY(R() * 360);
                    canopy.Append(Models.Jungle(trees + 300, 6f + R() * 4f));
                    canopy.Pop();
                }
                Obstacles.Add(g, 0.5f);
                Take(p, 4.5f);
                trees++;
            }
            canopy.ToObject("Jungle", world);
        }

        static readonly string[] ScanPlants = { "fern_02_a", "fern_02_b", "fern_02_c", "fern_02_d", "anthurium_botany_01_a", "anthurium_botany_01_b",
            "calathea_orbifolia_01_a", "calathea_orbifolia_01_e", "shrub_04", "anthurium_botany_01_e", "shrub_sorrel_01_c", "shrub_sorrel_01_h" };
        static Transform _plants;

        /// <summary>A photoscanned fern or jungle plant as scenery (about one decor spot in four), or false.</summary>
        static bool ScanPlant(Transform world, Vector3 g, float pick, int i)
        {
            if (pick > 0.45f) return false;
            var id = ScanPlants[i % ScanPlants.Length];
            if (ModelRegistry.Prefab(ModelRegistry.Scan, id) == null) return false;
            if (_plants == null || _plants.parent != world) { _plants = new GameObject("Scanned plants").transform; _plants.SetParent(world, false); }
            var go = ModelRegistry.Spawn(ModelRegistry.Scan, id, _plants);
            go.transform.position = g;
            go.transform.rotation = Quaternion.Euler(0, R() * 360, 0);
            go.transform.localScale *= 0.5f + R() * 0.5f;
            return true;
        }

        static void Undergrowth(Transform world, int count)
        {
            // Kit plants scattered through the jungle: bushes, flowers, mushrooms, fallen logs and stumps.
            if (ModelRegistry.Has(ModelRegistry.Free, Plants[0]))
            {
                var holder = new GameObject("Plants").transform;
                holder.SetParent(world, false);
                int made = 0;
                for (int i = 0; i < count && made < count / 5; i++)
                {
                    var p = RandomOnIsland();
                    float d = Island.Distance01(p.x, p.z);
                    if (d > 0.76f || Island.Slope(p.x, p.z) > 0.4f || Island.Height(p.x, p.z) < 0.5f || Near(p, 1.4f)) continue;
                    if (Vector3.Distance(p, Island.Camp) < Island.CampRadius + 2) continue;
                    var kind = Plants[(int)(R() * Plants.Length) % Plants.Length];
                    ModelRegistry.Free_(kind, holder, Island.Ground(p), R() * 360, 0.8f + R() * 0.5f);
                    if (kind == "log") Obstacles.Add(Island.Ground(p), 0.6f);
                    made++;
                }
            }
            var decor = new MeshBuilder();
            for (int i = 0; i < count; i++)
            {
                var p = RandomOnIsland();
                float d = Island.Distance01(p.x, p.z);
                if (Island.Slope(p.x, p.z) > 0.5f || Island.Height(p.x, p.z) < 0.3f || Near(p, 1.0f)) continue;
                if (Vector3.Distance(p, Island.Camp) < Island.CampRadius * 0.8f) continue;
                var g = Island.Ground(p);
                decor.Push().Translate(g).RotateY(R() * 360).Scale(0.6f + R() * 0.7f);
                float pick = R();
                bool scanned = ModelRegistry.Prefab(ModelRegistry.Scan, ScanPlants[0]) != null;
                if (d < 0.74f && ScanPlant(world, g, pick, i)) { decor.Pop(); continue; }
                if (scanned && d < 0.8f) { decor.Pop(); continue; }   // the grass field covers the tufts
                if (d < 0.74f) decor.Append(pick < 0.45f ? Models.Fern(i + 100, false) : Models.Tuft(i, pick > 0.85f));
                else if (d < 0.8f) decor.Append(Models.Tuft(i, false));
                else if (pick < 0.3f) Models.Rock(decor, new Vector3(0, 0.05f, 0), new Vector3(0.3f, 0.18f, 0.25f), i, Tropic.Rock);
                decor.Pop();
            }
            decor.ToObject("Undergrowth", world, false);
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

        static bool Free(Vector3 p, float r, bool wet = false)
        {
            if (!wet && (!Island.Walkable(p.x, p.z) || Island.Height(p.x, p.z) < 0.15f)) return false;
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

        public static Vector3? GoalPosition(Vector3 from) { return Target(from); }

        /// <summary>The nearest node the castaway can work right now that yields one of <paramref name="need"/>.</summary>
        static Vector3? SourceFor(List<ItemCount> need, Vector3 from)
        {
            Spot best = null;
            float bd = float.MaxValue;
            foreach (var sp in Spot.All)
            {
                var n = sp as NodeSpot;
                if (n == null || !n.Active || n.Blocked) continue;
                var def = Content.Nodes[n.NodeKind];
                bool useful = false;
                foreach (var y in def.Yield) foreach (var c in need) if (y.id == c.id) useful = true;
                if (!useful) continue;
                float d = Vector3.Distance(n.Pos, from);
                if (d < bd) { bd = d; best = n; }
            }
            return best != null ? best.Pos : (Vector3?)null;
        }

        /// <summary>The nearest working spot for the current guide step (a boar for the hunt).</summary>
        static Vector3? Target(Vector3 from)
        {
            var q = G.Session.CurrentQuest;
            if (q.PointNode == "boar" || q.PointNode == "skeleton" || q.PointNode == "redjack")
            {
                Enemy b = null; float eb = 400;
                foreach (var e in Enemy.All) { if (!e.Alive || e.Kind != q.PointNode) continue; float d = Vector3.Distance(e.transform.position, from); if (d < eb) { eb = d; b = e; } }
                return b != null ? b.transform.position : (Vector3?)null;
            }
            string kind = q.PointBuild ?? q.PointNode;
            if (kind == null) return null;
            // A blueprint short of materials the backpack doesn't hold: point at where to get them instead.
            var s0 = G.Session;
            if (q.PointBuild != null && Content.Builds.ContainsKey(kind) && !s0.Built(kind) && !s0.CanDeliver(kind))
            {
                var source = SourceFor(s0.Remaining(kind), from);
                if (source.HasValue) return source;
            }
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
