using System.Collections.Generic;
using LastTide.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastTide.Game
{
    /// <summary>
    /// The archipelago map: a miniature 3D sea with every location's island (terrain, little palms, its landmark),
    /// drifting clouds and a parchment edge beyond the charted waters, seen by its own camera. Drag to pan, pinch to
    /// zoom, tap an island to see it in the panel. The HUD draws the pins, names and the travel line over it.
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        public static MapView Instance { get; private set; }
        public static readonly Vector3 Origin = new Vector3(6000, 0, 6000);
        public Camera Cam { get; private set; }
        public bool Open { get { return gameObject.activeSelf; } }
        Vector3 _focus, _focusGoal;
        float _dist = 330, _distGoal = 330;
        const float Pitch = 50f;
        readonly List<Transform> _clouds = new List<Transform>();
        Transform _selRing;

        public static float IslandRadius(LocationDef l) { return l.Home ? 34 : 15 + l.Size * 0.12f; }
        public static Vector3 World(LocationDef l) { return Origin + new Vector3(l.MapX, 0, l.MapZ); }

        public static MapView Create()
        {
            var go = new GameObject("Map");
            var m = go.AddComponent<MapView>();
            Instance = m;
            m.Build();
            go.SetActive(false);
            return m;
        }

        void Build()
        {
            transform.position = Vector3.zero;
            var camGo = new GameObject("Map camera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            // The island's camera (and its listener) is switched off while the chart is up: hear the map's sounds here.
            camGo.AddComponent<AudioListener>();
            Cam.fieldOfView = 34;
            Cam.nearClipPlane = 5; Cam.farClipPlane = 2500;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Parchment;
#if LASTTIDE_URP
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
#endif
            BuildSea();
            BuildParchment();
            foreach (var l in LastTide.Core.World.Locations) BuildIsland(l);
            BuildClouds();
            _selRing = new GameObject("Selection").transform;
            _selRing.SetParent(transform, false);
            _focus = _focusGoal = Origin;
        }

        static readonly Color Parchment = new Color(0.86f, 0.78f, 0.6f);

        /// <summary>Distance to the nearest island's coast, in map metres (negative inside).</summary>
        static float Coast(float x, float z)
        {
            float best = 1e9f;
            foreach (var l in LastTide.Core.World.Locations)
            {
                float r = IslandRadius(l) * Wobble(l, x - l.MapX, z - l.MapZ);
                float d = Mathf.Sqrt((x - l.MapX) * (x - l.MapX) + (z - l.MapZ) * (z - l.MapZ)) - r;
                if (d < best) best = d;
            }
            return best;
        }

        static float Wobble(LocationDef l, float dx, float dz)
        {
            float a = Mathf.Atan2(dz, dx);
            int h = l.Id.GetHashCode() & 0xFF;
            return 1 + (Mathf.PerlinNoise(Mathf.Cos(a) * 1.4f + h, Mathf.Sin(a) * 1.4f + h * 0.7f) - 0.5f) * 0.55f;
        }

        const float Reach = 460;

        void BuildSea()
        {
            // Water out to a ragged edge; past it the parchment of the uncharted map shows through.
            var mb = new MeshBuilder();
            const float step = 8;
            // Charted water only near the islands; past it the old paper chart shows through. The edge is cut
            // smoothly through each cell (clipped where the field crosses zero) so it doesn't step.
            float F(float px, float pz) { return Coast(px, pz) - 48 - Mathf.PerlinNoise(px * 0.012f + 3, pz * 0.012f) * 30; }
            Color C(Vector3 v) { float c = Coast(v.x, v.z); return new Color(Mathf.Clamp01((c + 4) / 26f), 0, 0); }
            var poly = new System.Collections.Generic.List<Vector3>(8);
            for (float z = -Reach; z < Reach; z += step)
                for (float x = -Reach; x < Reach; x += step)
                {
                    var q = new[] { new Vector3(x, 0, z), new Vector3(x, 0, z + step), new Vector3(x + step, 0, z + step), new Vector3(x + step, 0, z) };
                    var f = new float[4];
                    int inside = 0;
                    for (int i = 0; i < 4; i++) { f[i] = F(q[i].x, q[i].z); if (f[i] <= 0) inside++; }
                    if (inside == 0) continue;
                    if (inside == 4)
                    {
                        mb.QuadSmooth(q[0], q[1], q[2], q[3], Vector3.up, Vector3.up, Vector3.up, Vector3.up, C(q[0]), C(q[1]), C(q[2]), C(q[3]));
                        continue;
                    }
                    poly.Clear();
                    for (int i = 0; i < 4; i++)
                    {
                        int j = (i + 1) % 4;
                        if (f[i] <= 0) poly.Add(q[i]);
                        if ((f[i] <= 0) != (f[j] <= 0)) poly.Add(Vector3.Lerp(q[i], q[j], f[i] / (f[i] - f[j])));
                    }
                    for (int i = 1; i + 1 < poly.Count; i++)
                        mb.QuadSmooth(poly[0], poly[i], poly[i + 1], poly[i + 1], Vector3.up, Vector3.up, Vector3.up, Vector3.up, C(poly[0]), C(poly[i]), C(poly[i + 1]), C(poly[i + 1]));
                }
            var go = mb.ToObject("Sea", transform, false);
            go.transform.position = Origin;
            var mat = new Material(Materials.Water);
            mat.SetColor("_ShallowColor", new Color(0.3f, 0.7f, 0.68f, 0.85f));
            mat.SetColor("_DeepColor", new Color(0.12f, 0.38f, 0.45f, 1f));
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        void BuildParchment()
        {
            // Old chart paper with faint contour lines, and the mapmaker's ink sketches: palms, waves, rocks, a dashed
            // trail and a compass rose.
            const int n = 1024;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float g = Noise2.Tileable(x, y, n, 6) * 0.6f + Noise2.Tileable(x, y, n, 40) * 0.4f;
                    var c = Color.Lerp(new Color(0.76f, 0.66f, 0.47f), new Color(0.9f, 0.82f, 0.64f), g);
                    float contour = Mathf.Abs(Mathf.Sin(Noise2.Tileable(x, y, n, 4) * 36f));
                    if (contour < 0.035f) c = Color.Lerp(c, new Color(0.45f, 0.36f, 0.24f), 0.45f);
                    px[y * n + x] = c;
                }
            Sketch(px, n);
            tex.SetPixels32(px); tex.Apply(true);
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = "Parchment";
            go.transform.SetParent(transform, false);
            go.transform.position = Origin + Vector3.down * 3;
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            go.transform.localScale = new Vector3(2400, 2400, 1);
            // URP Unlit is in the Always Included shaders (ProjectSetup): a build only has the shaders it references.
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("LastTide/VertexLit");
            var mat = new Material(sh);
            mat.mainTexture = tex;
            mat.mainTextureScale = new Vector2(3, 3);
            if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", tex); mat.SetTextureScale("_BaseMap", new Vector2(3, 3)); }
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static readonly Color32 Ink = new Color32(92, 66, 40, 255);

        static void Plot(Color32[] px, int n, int x, int y, float a = 0.85f)
        {
            x = ((x % n) + n) % n; y = ((y % n) + n) % n;
            var c = px[y * n + x];
            px[y * n + x] = Color32.Lerp(c, Ink, a);
        }

        static void Line(Color32[] px, int n, Vector2 a, Vector2 b, float w = 1.6f)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 1.5f) + 1;
            for (int i = 0; i <= steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)steps);
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                    if (dx * dx + dy * dy <= w * w) Plot(px, n, Mathf.RoundToInt(p.x) + dx, Mathf.RoundToInt(p.y) + dy, dx == 0 && dy == 0 ? 0.85f : 0.35f);
            }
        }

        /// <summary>Ink drawings scattered over the chart, like a mapmaker's margins.</summary>
        static void Sketch(Color32[] px, int n)
        {
            var rng = new System.Random(77);
            float R() { return (float)rng.NextDouble(); }
            // Palm sketches: a curved trunk and a burst of drooping fronds.
            for (int k = 0; k < 40; k++)
            {
                var b = new Vector2(R() * n, R() * n);
                float h = 26 + R() * 18, lean = (R() - 0.5f) * 16;
                Vector2 prev = b;
                for (int i = 1; i <= 6; i++) { var p = b + new Vector2(lean * i * i / 36f, h * i / 6f); Line(px, n, prev, p, 1.2f); prev = p; }
                for (int f = 0; f < 6; f++)
                {
                    float a = f * Mathf.PI / 3 + R() * 0.4f;
                    var mid = prev + new Vector2(Mathf.Cos(a) * 9, Mathf.Sin(a) * 5 + 3);
                    var tip = prev + new Vector2(Mathf.Cos(a) * 17, Mathf.Sin(a) * 6 - 6);
                    Line(px, n, prev, mid, 1.1f); Line(px, n, mid, tip, 1.1f);
                }
            }
            // Waves: little arcs in rows.
            for (int k = 0; k < 160; k++)
            {
                var c = new Vector2(R() * n, R() * n);
                for (int j = 0; j < 2; j++)
                {
                    var o = c + new Vector2(j * 16, 0);
                    Vector2 prev = o;
                    for (int i = 1; i <= 8; i++) { var p = o + new Vector2(i * 1.6f, Mathf.Sin(i / 8f * Mathf.PI) * 4); Line(px, n, prev, p, 1f); prev = p; }
                }
            }
            // Rock clusters: angular outlines.
            for (int k = 0; k < 18; k++)
            {
                var c = new Vector2(R() * n, R() * n);
                for (int r = 0; r < 5; r++)
                {
                    var o = c + new Vector2(R() * 50 - 25, R() * 30 - 15);
                    float s = 7 + R() * 9;
                    var pts = new[] { o + new Vector2(-s, 0), o + new Vector2(-s * 0.5f, s * 1.3f), o + new Vector2(s * 0.6f, s), o + new Vector2(s, 0) };
                    for (int i = 0; i < pts.Length - 1; i++) Line(px, n, pts[i], pts[i + 1], 1.2f);
                    Line(px, n, pts[0], pts[3], 1.2f);
                }
            }
            // A winding dashed trail.
            Vector2 at = new Vector2(R() * n, 0); float dir = 1.2f;
            for (int i = 0; i < 260; i++)
            {
                dir += (R() - 0.5f) * 0.35f;
                var next = at + new Vector2(Mathf.Cos(dir), Mathf.Sin(dir)) * 6;
                if (i % 3 != 2) Line(px, n, at, next, 1.6f);
                at = next;
            }
            // A compass rose.
            var cr = new Vector2(n * 0.72f, n * 0.28f);
            for (int a = 0; a < 8; a++)
            {
                float ang = a * Mathf.PI / 4, len = a % 2 == 0 ? 60 : 32;
                Line(px, n, cr, cr + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len, 1.6f);
            }
            for (int i = 0; i < 64; i++) { float a = i / 64f * Mathf.PI * 2; Plot(px, n, Mathf.RoundToInt(cr.x + Mathf.Cos(a) * 24), Mathf.RoundToInt(cr.y + Mathf.Sin(a) * 24)); }
        }

        void BuildIsland(LocationDef l)
        {
            float r = IslandRadius(l);
            int cells = 40;
            float half = r * 1.35f, cell = half * 2 / cells;
            int n = cells + 1;
            var verts = new Vector3[n * n]; var cols = new Color32[n * n]; var w01 = new Vector2[n * n]; var w23 = new Vector2[n * n]; var w45 = new Vector2[n * n];
            var heights = new float[n * n];
            int h = l.Id.GetHashCode() & 0xFF;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = -half + i * cell, z = -half + j * cell;
                    float d = Mathf.Sqrt(x * x + z * z) / (r * Wobble(l, x, z));
                    float y = d < 0.75f ? Mathf.Lerp(3.5f, 1.6f, d / 0.75f) : Mathf.Lerp(1.6f, -2.5f, Mathf.Clamp01((d - 0.75f) / 0.4f));
                    y += (Mathf.PerlinNoise(x * 0.08f + h, z * 0.08f) - 0.5f) * 3f * Mathf.Clamp01(1 - d);
                    if (l.Biome == Biome.Ridge || l.Biome == Biome.Cave || l.Biome == Biome.Lighthouse || l.Home)
                        y += Mathf.Max(0, Noise2.Ridged(x * 0.05f + h, z * 0.05f, 3) - 0.45f) * 18 * Mathf.Clamp01((0.8f - d) * 3) * (l.Home ? Mathf.Clamp01(z / r + 0.3f) : 1);
                    heights[j * n + i] = y;
                    verts[j * n + i] = new Vector3(x, y, z);
                    float sand = Mathf.Clamp01((d - 0.62f) / 0.12f);
                    float rock = Mathf.Clamp01((y - 7f) / 4f);
                    w01[j * n + i] = new Vector2((1 - sand) * (1 - rock), 0);
                    w23[j * n + i] = new Vector2(0, sand * (1 - rock));
                    w45[j * n + i] = new Vector2(rock, 0);
                    cols[j * n + i] = (Color32)(sand > 0.5f ? Palette.Sand : Palette.Grass);
                }
            var tris = new List<int>();
            for (int j = 0; j < cells; j++)
                for (int i = 0; i < cells; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    if (heights[a] < -2 && heights[b] < -2 && heights[c] < -2 && heights[d] < -2) continue;
                    tris.Add(a); tris.Add(c); tris.Add(d); tris.Add(a); tris.Add(d); tris.Add(b);
                }
            var mesh = new Mesh { name = "Map " + l.Id };
            mesh.vertices = verts; mesh.colors32 = cols; mesh.uv = w01; mesh.uv3 = w23; mesh.uv4 = w45;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            var surf = new Vector2[n * n];
            for (int k = 0; k < surf.Length; k++) surf[k] = new Vector2(0.08f, 0);
            mesh.uv2 = surf;
            var go = new GameObject("Island " + l.Id);
            go.transform.SetParent(transform, false);
            go.transform.position = World(l);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Materials.Terrain;
            mr.shadowCastingMode = ShadowCastingMode.On;

            // Little trees and the landmark.
            var deco = new MeshBuilder();
            var rng = new System.Random(h);
            int trees = l.Biome == Biome.Grove ? 36 : l.Home ? 40 : 18;
            for (int i = 0; i < trees; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, rr = Mathf.Sqrt((float)rng.NextDouble()) * r * 0.62f;
                float x = Mathf.Cos(a) * rr, z = Mathf.Sin(a) * rr;
                int ix = Mathf.Clamp(Mathf.RoundToInt((x + half) / cell), 0, cells), iz = Mathf.Clamp(Mathf.RoundToInt((z + half) / cell), 0, cells);
                float y = heights[iz * n + ix];
                if (y < 1.2f || y > 8) continue;
                deco.Push().Translate(x, y - 0.2f, z).Scale(0.45f + (float)rng.NextDouble() * 0.3f);
                if (rng.NextDouble() < 0.6) deco.Append(Models.Palm(i + h, 7f));
                else Models.Rock(deco, new Vector3(0, 2.5f, 0), new Vector3(4f, 3f, 4f), i, Color.Lerp(Tropic.Frond, Tropic.FrondDark, (float)rng.NextDouble()));
                deco.Pop();
            }
            MeshBuilder mark = null;
            float s = 0.35f;
            switch (l.Biome)
            {
                case Biome.Home: mark = Models.Wreck(); s = 0.42f; break;
                case Biome.Lighthouse: mark = Models.Lighthouse(); s = 0.6f; break;
                case Biome.Cave: mark = Models.CaveMouth(); s = 0.6f; break;
                case Biome.PirateCamp: mark = Models.PirateCamp(); s = 0.55f; break;
                case Biome.Pier: mark = Models.Pier(26); s = 0.5f; break;
                case Biome.Shore: mark = Models.Crate(3); s = 1.6f; break;
            }
            if (mark != null)
            {
                Vector3 at = l.Home ? new Vector3(8, 0, -r * 0.72f) : l.Biome == Biome.Pier ? new Vector3(r * 0.55f, 0.6f, -r * 0.35f) : Vector3.zero;
                int ix = Mathf.Clamp(Mathf.RoundToInt((at.x + half) / cell), 0, cells), iz = Mathf.Clamp(Mathf.RoundToInt((at.z + half) / cell), 0, cells);
                at.y = Mathf.Max(at.y, heights[iz * n + ix] - (l.Home ? 0.5f : 0));
                deco.Push().Translate(at).RotateY(l.Biome == Biome.Pier ? 125 : l.Home ? 0 : 180).Scale(s);
                deco.Append(mark);
                deco.Pop();
            }
            var dg = deco.ToObject("Decor", go.transform);
        }

        void BuildClouds()
        {
            var rng = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                var c = new GameObject("Cloud");
                c.transform.SetParent(transform, false);
                c.transform.position = Origin + new Vector3((float)rng.NextDouble() * 800 - 400, 45 + (float)rng.NextDouble() * 20, (float)rng.NextDouble() * 800 - 400);
                var ps = c.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.loop = false; main.playOnAwake = false; main.startLifetime = 1e6f; main.startSpeed = 0; main.maxParticles = 12; main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var em = ps.emission; em.rateOverTime = 0;
                var r = c.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = Materials.Smoke;
                var p = new ParticleSystem.EmitParams();
                for (int k = 0; k < 7; k++)
                {
                    p.position = new Vector3((float)rng.NextDouble() * 40 - 20, (float)rng.NextDouble() * 6, (float)rng.NextDouble() * 20 - 10);
                    p.startSize = 22 + (float)rng.NextDouble() * 18;
                    p.startColor = new Color(1, 1, 1, 0.55f);
                    p.startLifetime = 1e6f;
                    ps.Emit(p, 1);
                }
                ps.Pause();
                _clouds.Add(c.transform);
            }
        }

        public void Show(LocationDef focus)
        {
            gameObject.SetActive(true);
            _focus = _focusGoal = World(focus);
            _dist = _distGoal = 330;
            Place();
        }

        public void Hide() { gameObject.SetActive(false); }

        public void FocusOn(LocationDef l) { _focusGoal = World(l) + new Vector3(55, 0, 0); }

        public void Select(LocationDef l)
        {
            // A golden ring round the chosen island.
            for (int i = _selRing.childCount - 1; i >= 0; i--) Destroy(_selRing.GetChild(i).gameObject);
            if (l == null) return;
            _selRing.position = World(l) + Vector3.up * 2.5f;
            var ring = Ring.Create(_selRing, IslandRadius(l) * 1.12f);
            ring.Gold = true;
        }

        Vector2 _lastDrag;
        bool _dragging;
        float _pressTime;
        Vector2 _pressPos;

        /// <summary>Drag to pan, pinch or scroll to zoom. Returns true when the finger lifted after a tap (not a drag).</summary>
        public bool UpdateInput(bool blocked, out Vector2 tapScreen)
        {
            tapScreen = Vector2.zero;
            bool tapped = false;
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0); var b = Input.GetTouch(1);
                float now = Vector2.Distance(a.position, b.position), before = Vector2.Distance(a.position - a.deltaPosition, b.position - b.deltaPosition);
                _distGoal = Mathf.Clamp(_distGoal * before / Mathf.Max(1, now), 140, 620);
                _dragging = false;
            }
            else
            {
                bool down = Input.touchCount == 1 || Input.GetMouseButton(0);
                Vector2 pos = Input.touchCount == 1 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
                bool began = (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began) || Input.GetMouseButtonDown(0);
                if (began && !blocked) { _dragging = true; _lastDrag = pos; _pressTime = Time.unscaledTime; _pressPos = pos; }
                if (_dragging && down)
                {
                    var d = pos - _lastDrag;
                    _lastDrag = pos;
                    float k = _dist / Screen.height * 1.1f;
                    _focusGoal -= new Vector3(d.x, 0, d.y * 1.3f) * k;
                    _focus = _focusGoal;
                }
                if (_dragging && !down)
                {
                    _dragging = false;
                    if (Time.unscaledTime - _pressTime < 0.35f && Vector2.Distance(pos, _pressPos) < 20) { tapped = true; tapScreen = pos; }
                }
            }
            _distGoal = Mathf.Clamp(_distGoal - Input.mouseScrollDelta.y * 20, 140, 620);
            _focusGoal.x = Mathf.Clamp(_focusGoal.x, Origin.x - 380, Origin.x + 380);
            _focusGoal.z = Mathf.Clamp(_focusGoal.z, Origin.z - 380, Origin.z + 380);
            return tapped;
        }

        void LateUpdate()
        {
            _focus = Vector3.Lerp(_focus, _focusGoal, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 6));
            _dist = Mathf.Lerp(_dist, _distGoal, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 6));
            Place();
            foreach (var c in _clouds)
            {
                c.position += new Vector3(2.2f, 0, 0.8f) * Time.unscaledDeltaTime;
                if (c.position.x > Origin.x + 460) c.position -= new Vector3(920, 0, 0);
            }
        }

        void Place()
        {
            var rot = Quaternion.Euler(Pitch, 0, 0);
            Cam.transform.rotation = rot;
            Cam.transform.position = _focus - rot * Vector3.forward * _dist;
        }
    }
}
