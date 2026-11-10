using System.Collections.Generic;
using LastTide.Core;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>The camp grid: 3 m cells around the clearing at Wreck Cove.</summary>
    public static class CampGrid
    {
        public static Vector3 Cell(int i, int j) { return Island.Ground(Island.Camp + new Vector3(i * Content.CellSize, 0, j * Content.CellSize)); }

        /// <summary>
        /// Why a piece can't stand here because of the island itself (a palm, standing or felled, a rock, a camp
        /// structure or its site, the wreck), or null. Session.PlaceBlocked only knows about other pieces.
        /// </summary>
        public static string InTheWay(string id, int i, int j, int r)
        {
            var d = Content.Pieces[id];
            Vector3 pos; Quaternion rot;
            Pose(id, i, j, r, out pos, out rot);
            float half = Content.CellSize / 2, hx, hz;
            if (d.Kind == PieceKind.Edge) { hx = half - 0.1f; hz = 0.2f; }
            else if (d.Kind == PieceKind.Floor) hx = hz = half - 0.05f;
            else hx = hz = 0.9f;
            if (Mathf.Abs(Mathf.Sin(rot.eulerAngles.y * Mathf.Deg2Rad)) > 0.5f) { float t = hx; hx = hz; hz = t; }
            foreach (var s in Spot.All)
            {
                float reach = s is NodeSpot ? ((NodeSpot)s).Footprint : s is SiteSpot ? ((SiteSpot)s).Footprint : 0;
                if (reach > 0 && Obstacles.RectTouches(pos, hx, hz, new Vector2(s.Pos.x, s.Pos.z), reach)) return "Something is in the way";
            }
            // Scenery and the wreck (spots and pieces were dealt with above / by the rules).
            if (Obstacles.Overlaps(pos, hx, hz, o => o is Spot)) return "Something is in the way";
            return null;
        }

        /// <summary>Where a piece stands and which way it faces.</summary>
        public static void Pose(PieceState p, out Vector3 pos, out Quaternion rot)
        {
            Pose(p.id, p.i, p.j, p.rot, out pos, out rot);
        }

        public static void Pose(string id, int i, int j, int r, out Vector3 pos, out Quaternion rot)
        {
            var d = Content.Pieces[id];
            var c = Island.Camp + new Vector3(i * Content.CellSize, 0, j * Content.CellSize);
            float h = Content.CellSize / 2;
            if (d.Kind == PieceKind.Edge)
            {
                bool east = (r & 1) == 1;
                pos = c + (east ? new Vector3(h, 0, 0) : new Vector3(0, 0, h));
                rot = Quaternion.Euler(0, east ? 90 : 0, 0);
            }
            else { pos = c; rot = Quaternion.Euler(0, r * 90, 0); }
            pos = Island.Ground(pos);
            if (d.Kind == PieceKind.Floor || HasFloor(i, j, d.Kind)) pos.y = FloorTop(i, j);
        }

        /// <summary>Things standing on a floor sit on its planks.</summary>
        static bool HasFloor(int i, int j, PieceKind kind)
        {
            if (kind == PieceKind.Floor) return false;
            foreach (var p in G.Session.State.pieces) if (p.id == "foundation" && p.i == i && p.j == j) return true;
            return false;
        }

        /// <summary>Height of a floor's top: the highest ground under the tile, so it never sinks in.</summary>
        public static float FloorTop(int i, int j)
        {
            var c = Island.Camp + new Vector3(i * Content.CellSize, 0, j * Content.CellSize);
            float h = Content.CellSize / 2, top = -99;
            for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++) top = Mathf.Max(top, Island.Height(c.x + a * h, c.z + b * h));
            return top + 0.25f;
        }

        /// <summary>The nearest cell (and, for walls, the nearest edge) to a point on the ground.</summary>
        public static void Snap(string id, Vector3 p, int rot, out int i, out int j, out int r)
        {
            float s = Content.CellSize;
            float fx = (p.x - Island.Camp.x) / s, fz = (p.z - Island.Camp.z) / s;
            i = Mathf.RoundToInt(fx); j = Mathf.RoundToInt(fz); r = rot;
            if (Content.Pieces[id].Kind != PieceKind.Edge) return;
            // Walls: the edge whose middle is nearest, keeping the chosen direction.
            if ((rot & 1) == 0) { i = Mathf.RoundToInt(fx); j = Mathf.FloorToInt(fz); r = 0; }
            else { i = Mathf.FloorToInt(fx); j = Mathf.RoundToInt(fz); r = 1; }
        }
    }

    /// <summary>The look of every building piece (procedural, or a free kit model where one fits).</summary>
    public static class PieceModels
    {
        static readonly Color Plank = Palette.Hex(0xB08458), PlankDark = Palette.Hex(0x7E5A38), Post = Palette.Hex(0x6A4A30);

        public static GameObject Create(string id, Transform parent, bool lit = true)
        {
            GameObject kit = ScanPiece(id, parent);   // photoscans first, then the free kit, then built in code
            if (kit != null) return kit;
            switch (id)
            {
                case "small_box": kit = Kit("box", parent, 1.1f); break;
                case "storage_chest": kit = Kit("chest", parent, 1.25f); break;
                case "bed":
                    // The kit's cot frame is bare legs: lay its bedroll on top.
                    kit = Kit("bedframe", parent, 1f);
                    if (kit != null && ModelRegistry.Has(ModelRegistry.Free, "bedroll"))
                        ModelRegistry.Spawn(ModelRegistry.Free, "bedroll", kit.transform).transform.localPosition += new Vector3(0, 0.42f, 0);
                    break;
            }
            if (kit != null) return kit;
            var mb = new MeshBuilder();
            float L = Content.CellSize, H = 2.6f;
            switch (id)
            {
                case "foundation":
                    mb.Surf = SurfaceKind.Planks;
                    for (int k = 0; k < 6; k++)
                        mb.Box(new Vector3(-L / 2 + L / 12 + k * L / 6, -0.06f, 0), new Vector3(L / 6 - 0.03f, 0.12f, L), k % 2 == 0 ? Plank : MeshBuilder.Darken(Plank, 0.93f));
                    mb.Surf = SurfaceKind.WoodGrain;
                    foreach (float x in new[] { -L / 2, L / 2 }) foreach (float z in new[] { -L / 2, L / 2 })
                        mb.Box(new Vector3(x * 0.96f, -0.5f, z * 0.96f), new Vector3(0.22f, 0.9f, 0.22f), Post);
                    mb.Box(new Vector3(0, -0.2f, L / 2 * 0.97f), new Vector3(L, 0.2f, 0.14f), PlankDark);
                    mb.Box(new Vector3(0, -0.2f, -L / 2 * 0.97f), new Vector3(L, 0.2f, 0.14f), PlankDark);
                    break;
                case "wall":
                case "doorway":
                case "window":
                {
                    // Vertical planks between posts, with a gap for the door or window.
                    int n = 10;
                    for (int k = 0; k < n; k++)
                    {
                        float x0 = -L / 2 + k * L / n, x = x0 + L / n / 2;
                        float lo = 0, hi = H;
                        if (id == "doorway" && Mathf.Abs(x) < 0.65f) lo = 2.1f;
                        mb.Surf = SurfaceKind.Planks;
                        var col = k % 3 == 0 ? MeshBuilder.Darken(Plank, 0.9f) : k % 3 == 1 ? Plank : MeshBuilder.Lighten(Plank, 0.05f);
                        if (id == "window" && Mathf.Abs(x) < 0.6f)
                        {
                            mb.Box(new Vector3(x, 0.5f, 0), new Vector3(L / n - 0.02f, 1.0f, 0.12f), col);
                            mb.Box(new Vector3(x, 2.35f, 0), new Vector3(L / n - 0.02f, 0.5f, 0.12f), col);
                        }
                        else mb.Box(new Vector3(x, (lo + hi) / 2, 0), new Vector3(L / n - 0.02f, hi - lo, 0.12f), col);
                    }
                    mb.Surf = SurfaceKind.WoodGrain;
                    foreach (float x in new[] { -L / 2, L / 2 }) mb.Box(new Vector3(x, H / 2 + 0.05f, 0), new Vector3(0.2f, H + 0.1f, 0.2f), Post);
                    mb.Box(new Vector3(0, 0.6f, 0.09f), new Vector3(L, 0.12f, 0.06f), PlankDark);
                    mb.Box(new Vector3(0, H - 0.3f, 0.09f), new Vector3(L, 0.12f, 0.06f), PlankDark);
                    if (id == "window")
                    {
                        foreach (float x in new[] { -0.85f, 0.85f })
                            mb.Box(new Vector3(x, 1.55f, 0.18f), new Vector3(0.55f, 1.0f, 0.06f), Palette.Hex(0x5E7A5A));
                    }
                    break;
                }
                case "furnace":
                    mb.Surf = SurfaceKind.Stone;
                    mb.Cylinder(Vector3.zero, new Vector3(0, 1.1f, 0), 0.95f, 0.75f, 10, Tropic.Rock);
                    mb.Sphere(new Vector3(0, 1.1f, 0), new Vector3(0.75f, 0.5f, 0.75f), 10, Tropic.RockDark);
                    mb.Cylinder(new Vector3(0, 1.4f, 0), new Vector3(0, 2.6f, 0), 0.22f, 0.18f, 8, Tropic.RockDark);
                    mb.Surf = SurfaceKind.None;
                    if (lit) mb.Lit();
                    mb.Box(new Vector3(0, 0.45f, 0.9f), new Vector3(0.6f, 0.5f, 0.1f), Palette.Hex(0xFF8A30));
                    mb.Mat(0.15f);
                    break;
                case "bed":
                    mb.Surf = SurfaceKind.WoodGrain;
                    mb.Box(new Vector3(0, 0.3f, 0), new Vector3(1.2f, 0.3f, 2.2f), Post);
                    mb.Surf = SurfaceKind.None;
                    mb.Box(new Vector3(0, 0.52f, 0), new Vector3(1.1f, 0.14f, 2.0f), Tropic.Sail);
                    break;
                case "doghouse":
                {
                    // A plank kennel with a sailcloth roof; the door faces the camp side (-Z).
                    mb.Surf = SurfaceKind.Planks;
                    mb.Box(new Vector3(0, 0.06f, 0), new Vector3(1.5f, 0.12f, 1.2f), PlankDark);
                    mb.Box(new Vector3(0, 0.55f, 0.55f), new Vector3(1.5f, 0.9f, 0.1f), Plank);
                    foreach (float x in new[] { -0.7f, 0.7f }) mb.Box(new Vector3(x, 0.55f, 0), new Vector3(0.1f, 0.9f, 1.2f), MeshBuilder.Darken(Plank, 0.93f));
                    foreach (float x in new[] { -0.48f, 0.48f }) mb.Box(new Vector3(x, 0.55f, -0.55f), new Vector3(0.54f, 0.9f, 0.1f), Plank);
                    mb.Box(new Vector3(0, 0.9f, -0.55f), new Vector3(0.42f, 0.2f, 0.1f), Plank);
                    mb.Surf = SurfaceKind.None;
                    mb.Box(new Vector3(0, 0.5f, -0.4f), new Vector3(0.42f, 0.7f, 0.02f), Palette.Hex(0x2A1E14)); // the dark inside
                    mb.GableRoof(new Vector3(0, 1.0f, 0), new Vector3(1.5f, 0.55f, 1.2f), MeshBuilder.Darken(Tropic.Sail, 0.78f), Plank, 0.15f);
                    break;
                }
                case "table":
                    mb.Surf = SurfaceKind.Planks;
                    mb.Box(new Vector3(0, 0.85f, 0), new Vector3(1.8f, 0.1f, 1.1f), Plank);
                    mb.Surf = SurfaceKind.WoodGrain;
                    foreach (float x in new[] { -0.75f, 0.75f }) foreach (float z in new[] { -0.42f, 0.42f })
                        mb.Box(new Vector3(x, 0.4f, z), new Vector3(0.1f, 0.8f, 0.1f), Post);
                    mb.Surf = SurfaceKind.None;
                    mb.Cylinder(new Vector3(0.4f, 0.9f, 0), new Vector3(0.4f, 1.0f, 0), 0.12f, 8, Tropic.Coconut);
                    break;
                case "chair":
                    mb.Surf = SurfaceKind.Planks;
                    mb.Box(new Vector3(0, 0.45f, 0), new Vector3(0.55f, 0.08f, 0.55f), Plank);
                    mb.Box(new Vector3(0, 0.85f, -0.25f), new Vector3(0.55f, 0.75f, 0.06f), Plank);
                    mb.Surf = SurfaceKind.WoodGrain;
                    foreach (float x in new[] { -0.22f, 0.22f }) foreach (float z in new[] { -0.22f, 0.22f })
                        mb.Box(new Vector3(x, 0.22f, z), new Vector3(0.06f, 0.45f, 0.06f), Post);
                    break;
                case "torch":
                    mb.Surf = SurfaceKind.WoodGrain;
                    mb.Cylinder(Vector3.zero, new Vector3(0, 1.8f, 0), 0.06f, 0.05f, 6, Post);
                    mb.Surf = SurfaceKind.None;
                    mb.Cylinder(new Vector3(0, 1.75f, 0), new Vector3(0, 1.95f, 0), 0.1f, 0.08f, 6, Tropic.Sail);
                    break;
                case "flag":
                    mb.Surf = SurfaceKind.WoodGrain;
                    mb.Cylinder(Vector3.zero, new Vector3(0, 5f, 0), 0.08f, 0.06f, 6, Post);
                    mb.Surf = SurfaceKind.None;
                    mb.Quad(new Vector3(0, 4.9f, 0), new Vector3(1.8f, 4.7f, 0), new Vector3(1.8f, 3.6f, 0), new Vector3(0, 3.8f, 0), Tropic.Sail);
                    mb.Quad(new Vector3(0, 3.8f, 0), new Vector3(1.8f, 3.6f, 0), new Vector3(1.8f, 4.7f, 0), new Vector3(0, 4.9f, 0), Tropic.Sail);
                    mb.Sphere(new Vector3(0.9f, 4.25f, 0.02f), new Vector3(0.28f, 0.28f, 0.02f), 8, Palette.Hex(0x2A5A8A));
                    break;
                default: return null;
            }
            return mb.ToObject(id, parent);
        }

        /// <summary>Pieces made of free scans: the crate and the chest for storage, the forge, the table and the chair.</summary>
        static GameObject ScanPiece(string id, Transform parent)
        {
            string scan = id == "small_box" ? "wooden_crate_01" : id == "storage_chest" ? "treasure_chest" : id == "furnace" ? "forge"
                : id == "table" ? "WoodenTable_03" : id == "chair" ? "painted_wooden_chair_02" : null;
            if (scan == null || ModelRegistry.Prefab(ModelRegistry.Scan, scan) == null) return null;
            var holder = new GameObject(id);
            if (parent != null) holder.transform.SetParent(parent, false);
            var go = ModelRegistry.Spawn(ModelRegistry.Scan, scan, holder.transform);
            float scale = id == "small_box" ? 0.85f : id == "storage_chest" ? 1.1f : id == "table" ? 0.85f : 1f;
            go.transform.localScale *= scale;
            // Stood on the ground, centred on its cell.
            var b = ModelRegistry.RendererBounds(go);
            go.transform.position += holder.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            if (id == "table")
            {
                // A coconut-shell cup on it, as before.
                var mb = new MeshBuilder();
                float top = ModelRegistry.RendererBounds(go).max.y - holder.transform.position.y;
                mb.Cylinder(new Vector3(0.3f, top, 0), new Vector3(0.3f, top + 0.1f, 0), 0.1f, 8, Tropic.Coconut);
                mb.ToObject("Cup", holder.transform).GetComponent<MeshRenderer>().sharedMaterial = Materials.Kit;
            }
            return holder;
        }

        static GameObject Kit(string art, Transform parent, float scale)
        {
            if (!ModelRegistry.Has(ModelRegistry.Free, art)) return null;
            var holder = new GameObject(art);
            if (parent != null) holder.transform.SetParent(parent, false);
            var go = ModelRegistry.Spawn(ModelRegistry.Free, art, holder.transform);
            go.transform.localScale *= scale;
            return holder;
        }
    }

    /// <summary>A built piece in the world: its model, what it blocks, and what you can do at it.</summary>
    public sealed class PieceView : Spot
    {
        public PieceState State;
        PieceDef Def { get { return Content.Pieces[State.id]; } }
        float _t;

        public override string Kind { get { return State.id; } }
        public override bool Active { get { return Def.Slots > 0 || Def.Station != null || State.id == "bed"; } }
        public override string Label { get { return Active ? Def.Name : null; } }

        public static PieceView Create(Transform parent, PieceState p)
        {
            Vector3 pos; Quaternion rot;
            CampGrid.Pose(p, out pos, out rot);
            var go = new GameObject("Piece " + p.id);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            var v = go.AddComponent<PieceView>();
            v.State = p;
            var model = PieceModels.Create(p.id, go.transform);
            if (model != null) { model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity * model.transform.localRotation; }
            var d = Content.Pieces[p.id];
            if (d.Kind == PieceKind.Edge)
            {
                // A wall blocks along its length; a doorway leaves the middle open.
                var along = rot * Vector3.right;
                for (int k = -2; k <= 2; k++)
                {
                    if (p.id == "doorway" && Mathf.Abs(k) < 1) continue;
                    Obstacles.Add(pos + along * k * 0.6f, 0.4f, v);
                }
            }
            else if (d.Blocks && d.Kind == PieceKind.Object) Obstacles.Add(pos, 0.7f, v);
            if (p.id == "torch" || p.id == "furnace")
            {
                var l = new GameObject("Light").AddComponent<Light>();
                l.type = LightType.Point; l.range = p.id == "torch" ? 7 : 5; l.intensity = 1.6f; l.color = new Color(1f, 0.62f, 0.3f);
                l.transform.SetParent(go.transform, false);
                l.transform.localPosition = new Vector3(0, p.id == "torch" ? 2.1f : 0.8f, p.id == "furnace" ? 1.2f : 0);
                if (p.id == "torch") Flames.Create(go.transform, new Vector3(0, 1.9f, 0)).transform.localScale = Vector3.one * 0.35f;
            }
            if (v.Active) v.MakeRing(d.Kind == PieceKind.Object ? 1.7f : 2f);
            return v;
        }

        public override Act Work(Player p, float dt)
        {
            if (State.id == "bed")
            {
                if (G.Session.State.health >= 99.5f) return Act.None;
                G.Session.Rest(dt);
                return Act.Rest;
            }
            if (Def.Station != null) { Hud.Instance.OpenStation(Def.Station); return Act.None; }
            if (Def.Slots > 0) { Hud.Instance.OpenStorage(State); return Act.None; }
            return Act.None;
        }

        public override void Leave()
        {
            if (Hud.Instance == null) return;
            if (Def.Station != null) Hud.Instance.CloseStation(Def.Station);
            if (Def.Slots > 0) Hud.Instance.CloseStorage(State);
        }

        protected override void OnDisable() { base.OnDisable(); Obstacles.Remove(this); }

        float _work;

        void Update()
        {
            // A station built in build mode (the furnace) shows its work while a job runs.
            var st = Def.Station;
            if (st == null || G.Session.Job(st) == null) return;
            _work -= Time.deltaTime;
            if (_work <= 0) { _work = Random.Range(0.5f, 0.85f); BuildFx.Working(st, transform.position); }
        }
    }

    /// <summary>Keeps a <see cref="PieceView"/> for every built piece (built when the home island is).</summary>
    public sealed class PieceViews : MonoBehaviour
    {
        public static PieceViews Instance { get; private set; }
        readonly Dictionary<PieceState, PieceView> _views = new Dictionary<PieceState, PieceView>();

        public static PieceViews Create(Transform world)
        {
            var go = new GameObject("Built pieces");
            go.transform.SetParent(world, false);
            var v = go.AddComponent<PieceViews>();
            Instance = v;
            v.Sync();
            G.Session.Changed += v.Sync;
            return v;
        }

        void OnDestroy() { if (G.Session != null) G.Session.Changed -= Sync; if (Instance == this) Instance = null; }

        bool _ready;

        public void Sync()
        {
            var live = G.Session.State.pieces;
            foreach (var p in live)
                if (!_views.ContainsKey(p))
                {
                    var v = PieceView.Create(transform, p);
                    _views[p] = v;
                    // No grass growing up through floors and boxes.
                    if (GrassField.Instance != null && Content.Pieces[p.id].Kind != PieceKind.Edge)
                        GrassField.Instance.ClearAround(v.transform.position, Content.CellSize * 0.75f);
                    // Pieces placed now (not the ones loaded with the island) are built before your eyes.
                    if (_ready && v.transform.childCount > 0)
                    {
                        var kind = Content.Pieces[p.id].Kind;
                        Appear.Play(v.transform.GetChild(0), kind == PieceKind.Floor ? Appear.Style.Floor : kind == PieceKind.Edge ? Appear.Style.Wall : Appear.Style.Drop);
                    }
                }
            var gone = new List<PieceState>();
            foreach (var kv in _views) if (!live.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var p in gone)
            {
                var v = _views[p];
                _views.Remove(p);
                if (v == null) continue;
                // Taken down: it stops blocking the way at once, then tips over and breaks up.
                Obstacles.Remove(v);
                v.enabled = false;
                Crumble.Play(v.gameObject);
            }
            _ready = true;
        }

        public PieceState At(Vector3 world, float r)
        {
            PieceState best = null; float bd = r;
            foreach (var kv in _views)
            {
                if (kv.Value == null) continue;
                float d = Vector3.Distance(kv.Value.transform.position, world);
                if (d < bd) { bd = d; best = kv.Key; }
            }
            return best;
        }
    }
}
