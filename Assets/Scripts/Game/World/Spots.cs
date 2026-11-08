using System.Collections.Generic;
using System.Linq;
using LastTide.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastTide.Game
{
    /// <summary>What the player's hands are doing.</summary>
    public enum Act { None, Chop, Mine, Gather, Build, Drink, Rest }

    /// <summary>
    /// Anything with a green ring: stand inside it (and let go of the stick) and the castaway works on it.
    /// </summary>
    public abstract class Spot : MonoBehaviour
    {
        public static readonly List<Spot> All = new List<Spot>();
        public float Radius = 1.8f;
        protected Ring _ring;

        public Vector3 Pos { get { return transform.position; } }
        /// <summary>Shows a ring and can be worked.</summary>
        public abstract bool Active { get; }
        /// <summary>Text over the ring (null = none).</summary>
        public virtual string Label { get { return null; } }
        /// <summary>Second line, e.g. what is missing.</summary>
        public virtual string Detail { get { return null; } }
        /// <summary>What the player does here; returns the animation. Called every frame while working.</summary>
        public abstract Act Work(Player p, float dt);
        public virtual void Leave() { }
        /// <summary>Quest-arrow key: the node kind or build id.</summary>
        public abstract string Kind { get; }
        public virtual float Fill { get { return 0; } }

        protected virtual void OnEnable() { All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        protected void MakeRing(float radius)
        {
            Radius = radius;
            _ring = Ring.Create(transform, radius);
        }

        protected virtual void LateUpdate()
        {
            if (_ring == null) return;
            bool on = Active;
            var pl = Player.Instance;
            float d = pl != null ? Vector3.Distance(pl.transform.position, Pos) : 0;
            if (on && d > (this is SiteSpot ? 30f : 12f)) on = false; // far away: no ring (and no cost)
            if (on && Blocked && d > Radius + 3f) on = false; // can't use it yet: only show up close
            if (_ring.gameObject.activeSelf != on) _ring.gameObject.SetActive(on);
            if (on)
            {
                bool here = Player.Instance != null && Player.Instance.Current == this;
                _ring.Set(Fill, here, Blocked);
            }
        }

        /// <summary>The ring goes amber when the player can't work it (no tool, backpack full).</summary>
        public virtual bool Blocked { get { return false; } }
    }

    /// <summary>A palm, rock, bush, crate... harvested in timed actions.</summary>
    public sealed class NodeSpot : Spot
    {
        public string Key, NodeKind;
        GameObject _model, _stump;
        float _timer, _shake, _fall;
        bool _standing = true;
        Vector3 _baseScale;

        public override string Kind { get { return NodeKind; } }
        NodeDef Def { get { return Content.Nodes[NodeKind]; } }

        public static NodeSpot Create(Transform parent, string kind, string key, Vector3 pos, MeshBuilder model, float ring, float obstacle)
        {
            var go = new GameObject(key);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var s = go.AddComponent<NodeSpot>();
            s.Key = key; s.NodeKind = kind;
            var art = ArtFor(kind, go.transform, key);
            s._model = art ?? model.ToObject("Model", go.transform);
            s._baseScale = s._model.transform.localScale;
            s.MakeRing(ring);
            if (obstacle > 0) Obstacles.Add(pos, obstacle, s);
            if (kind == "palm")
            {
                var mb = new MeshBuilder { Surf = SurfaceKind.Bark };
                mb.Cylinder(Vector3.zero, new Vector3(0, 0.45f, 0), 0.36f, 0.33f, 7, Tropic.PalmBark);
                mb.Surf = SurfaceKind.WoodGrain;
                mb.Cylinder(new Vector3(0, 0.45f, 0), new Vector3(0, 0.47f, 0), 0.33f, 0.33f, 7, Palette.WoodPale);
                s._stump = mb.ToObject("Stump", go.transform);
                s._stump.SetActive(false);
            }
            return s;
        }

        /// <summary>Drop-in art for a node kind (Resources/Models/Trees/palm...), turned and sized a little at random.</summary>
        static GameObject ArtFor(string kind, Transform parent, string key)
        {
            if (kind != "palm") return null;
            var holder = new GameObject("Model");
            holder.transform.SetParent(parent, false);
            var go = ModelRegistry.Spawn("Trees", "palm", holder.transform);
            if (go == null) { Destroy(holder); return null; }
            float h = Noise2.Hash(key.GetHashCode(), 3);
            go.transform.localRotation = Quaternion.Euler(0, h * 360, 0) * go.transform.localRotation;
            go.transform.localScale *= 0.68f + Noise2.Hash(key.GetHashCode(), 9) * 0.32f;
            return holder;
        }

        public override bool Active { get { return _standing && _fall <= 0; } }
        public override float Fill { get { return Mathf.Clamp01(_timer / G.Session.HarvestSeconds(NodeKind)); } }
        public override bool Blocked { get { return G.Session.Blocked(NodeKind) != null; } }
        public override string Label { get { return Blocked ? G.Session.Blocked(NodeKind) : null; } }

        public override Act Work(Player p, float dt)
        {
            if (G.Session.Blocked(NodeKind) != null) { _timer = 0; return Act.None; }
            _timer += dt;
            if (_timer >= G.Session.HarvestSeconds(NodeKind))
            {
                _timer = 0;
                if (G.Session.Harvest(Key, NodeKind))
                {
                    _shake = 0.35f;
                    Sfx.Play(NodeKind == "palm" ? "chop" : NodeKind == "boulder" ? "mine" : NodeKind == "spring" ? "drink" : "gather", Pos);
                }
                Refresh();
            }
            switch (NodeKind)
            {
                case "palm": return Act.Chop;
                case "boulder": return Act.Mine;
                case "spring": return Act.Drink;
                default: return Act.Gather;
            }
        }

        public override void Leave() { _timer = 0; }

        void Refresh()
        {
            bool standing = G.Session.Standing(Key, NodeKind);
            if (standing == _standing) return;
            _standing = standing;
            if (!standing)
            {
                if (NodeKind == "palm") { _fall = 0.001f; Sfx.Play("treefall", Pos); }
                else _model.SetActive(false);
                Obstacles.Remove(this);
                if (_stump != null) _stump.SetActive(true);
            }
            else
            {
                _model.SetActive(true);
                _model.transform.localRotation = Quaternion.identity;
                _model.transform.localScale = _baseScale;
                if (_stump != null) _stump.SetActive(false);
                if (NodeKind == "palm" || NodeKind == "boulder") Obstacles.Add(Pos, NodeKind == "palm" ? 0.45f : 1.1f, this);
            }
        }

        float _check;
        void Update()
        {
            if ((_check -= Time.deltaTime) <= 0) { _check = 1f; Refresh(); }
            if (_shake > 0)
            {
                _shake -= Time.deltaTime;
                float a = Mathf.Sin(_shake * 60) * _shake * 6;
                _model.transform.localRotation = Quaternion.Euler(a, 0, a * 0.6f);
            }
            if (_fall > 0)
            {
                // The palm topples away from the player, then sinks into the sand.
                _fall += Time.deltaTime;
                var away = Player.Instance != null ? Pos - Player.Instance.transform.position : Vector3.forward;
                away.y = 0;
                var axis = Vector3.Cross(Vector3.up, away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward);
                float t = Mathf.Clamp01(_fall / 1.1f);
                _model.transform.rotation = Quaternion.AngleAxis(t * t * 88, axis);
                if (_fall > 1.6f) _model.transform.localScale = _baseScale * Mathf.Clamp01(1 - (_fall - 1.6f) * 2);
                if (_fall > 2.1f) { _model.SetActive(false); _fall = 0; }
            }
        }
    }

    /// <summary>A camp structure: a green blueprint until built, then the real thing and whatever it does.</summary>
    public sealed class SiteSpot : Spot
    {
        public string Id;
        GameObject _ghost, _built;
        Material _ghostMat;
        float _tick, _rest;
        Light _fire;
        ParticleSystem _smoke, _flames;
        string _visibleFrom;

        public override string Kind { get { return Id; } }
        bool IsBuilt { get { return G.Session.IsBuilt(Id); } }

        public static SiteSpot Create(Transform parent, string id, Vector3 pos, float yaw, float ring, float obstacle, string visibleFrom)
        {
            var go = new GameObject("Site " + id);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var s = go.AddComponent<SiteSpot>();
            s.Id = id;
            s._visibleFrom = visibleFrom;
            var mb = Models.Structure(id);
            s._built = mb.ToObject("Built", go.transform);
            s._ghost = mb.ToObject("Ghost", go.transform, false);
            s._ghostMat = new Material(Shader.Find("LastTide/Ghost"));
            s._ghost.GetComponent<MeshRenderer>().sharedMaterial = s._ghostMat;
            var b = s._built.GetComponent<MeshFilter>().sharedMesh.bounds;
            s._ghostMat.SetFloat("_MinY", pos.y);
            s._ghostMat.SetFloat("_Height", Mathf.Max(0.5f, b.max.y));
            s.MakeRing(ring);
            s._obstacle = obstacle;
            if (id == "campfire")
            {
                var lgo = new GameObject("Fire light");
                lgo.transform.SetParent(go.transform, false);
                lgo.transform.localPosition = new Vector3(0, 1.2f, 0);
                s._fire = lgo.AddComponent<Light>();
                s._fire.type = LightType.Point;
                s._fire.color = new Color(1f, 0.6f, 0.25f);
                s._fire.range = 9;
                s._fire.intensity = 2.5f;
                s._flames = Flames.Create(go.transform, new Vector3(0, 0.1f, 0));
                s._smoke = Fx.Create(FxKind.Chimney, go.transform, new Vector3(0, 1.4f, 0));
                Fx.SetRate(s._smoke, 3);
            }
            s.Sync(true);
            return s;
        }

        float _obstacle;
        bool _wasBuilt;

        bool Visible
        {
            get
            {
                if (IsBuilt) return true;
                int from = Quests.All.FindIndex(q => q.Id == _visibleFrom);
                return from < 0 || G.Session.State.quest >= from;
            }
        }

        void Sync(bool force)
        {
            bool built = IsBuilt;
            if (!force && built == _wasBuilt) return;
            if (built && !_wasBuilt && !force)
            {
                Sfx.Play("built", Pos);
                Burst.Play(Pos + Vector3.up * 0.5f);
                _pop = 0.001f;
            }
            _wasBuilt = built;
            _built.SetActive(built);
            if (_fire != null) _fire.gameObject.SetActive(built);
            if (_flames != null) _flames.gameObject.SetActive(built);
            if (_smoke != null) _smoke.gameObject.SetActive(built);
            Obstacles.Remove(this);
            if (built && _obstacle > 0) Obstacles.Add(Pos, _obstacle, this);
        }

        float _pop;

        public override bool Active
        {
            get
            {
                if (!Visible) return false;
                if (!IsBuilt) return true;
                switch (Id)
                {
                    case "raincatcher": return true;
                    default: return true;
                }
            }
        }

        public override float Fill { get { return IsBuilt ? (Id == "shelter" ? 0 : 0) : G.Session.BuildProgress(Id); } }
        public override bool Blocked { get { return !IsBuilt && !G.Session.CanDeliver(Id); } }

        public override string Label
        {
            get
            {
                if (!IsBuilt) return Content.Builds[Id].Name;
                switch (Id)
                {
                    case "raincatcher":
                        int n = G.Session.Build("raincatcher").stored;
                        return n > 0 ? "Water ready: " + n : "Collecting rain...";
                    case "shelter": return G.Session.State.health < 99.5f ? "Resting..." : null;
                    default: return null;
                }
            }
        }

        public override string Detail
        {
            get
            {
                if (IsBuilt) return null;
                return string.Join("   ", G.Session.Remaining(Id).Select(r => Content.NameOf(r.id) + " " + G.Session.Count(r.id) + "/" + r.n));
            }
        }

        public override Act Work(Player p, float dt)
        {
            if (!IsBuilt)
            {
                if (!G.Session.CanDeliver(Id)) return Act.None;
                _tick -= dt;
                if (_tick <= 0)
                {
                    _tick = 0.09f;
                    if (G.Session.Deliver(Id, 1) > 0) Sfx.Play("deliver", Pos, 0.5f);
                }
                return Act.Build;
            }
            switch (Id)
            {
                case "shelter":
                    if (G.Session.State.health >= 99.5f) return Act.None;
                    G.Session.Rest(dt);
                    return Act.Rest;
                case "raincatcher":
                    if (G.Session.CollectRain() > 0) Sfx.Play("drink", Pos);
                    return Act.None;
                case "campfire":
                case "workbench":
                case "chest":
                case "radio":
                    Hud.Instance.OpenStation(Id);
                    return Act.None;
            }
            return Act.None;
        }

        public override void Leave()
        {
            if (Hud.Instance != null) Hud.Instance.CloseStation(Id);
            if (Id == "shelter") GameRoot.Instance.Save();
        }

        protected override void LateUpdate()
        {
            Sync(false);
            bool vis = Visible;
            _ghost.SetActive(vis && !IsBuilt);
            if (_ghost.activeSelf) _ghostMat.SetFloat("_Fill", G.Session.BuildProgress(Id));
            if (_pop > 0)
            {
                _pop += Time.deltaTime;
                float t = Mathf.Clamp01(_pop / 0.45f);
                float s = 1 + Mathf.Sin(t * Mathf.PI) * 0.15f;
                _built.transform.localScale = new Vector3(s, Mathf.Lerp(0.2f, 1, Mathf.Sqrt(t)) * s, s);
                if (t >= 1) { _pop = 0; _built.transform.localScale = Vector3.one; }
            }
            if (_fire != null && _fire.gameObject.activeSelf)
                _fire.intensity = 2.2f + Mathf.PerlinNoise(Time.time * 6, 0) * 1.2f;
            base.LateUpdate();
        }
    }

    /// <summary>The backpack the castaway dropped when they fell.</summary>
    public sealed class BagSpot : Spot
    {
        float _t;
        public override string Kind { get { return "bag"; } }
        public override bool Active { get { var b = G.Session.State.lostBag; return b != null && (b.location ?? LastTide.Core.World.HomeId) == G.Session.State.location; } }
        public override string Label { get { return "Your lost backpack"; } }
        public override float Fill { get { return Mathf.Clamp01(_t / 0.8f); } }

        public static BagSpot Create(Transform parent)
        {
            var go = new GameObject("Lost bag");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<BagSpot>();
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0, 0.3f, 0), new Vector3(0.7f, 0.6f, 0.45f), Tropic.Pack);
            mb.Box(new Vector3(0, 0.62f, 0), new Vector3(0.6f, 0.12f, 0.4f), MeshBuilder.Darken(Tropic.Pack, 0.8f));
            mb.ToObject("Bag", go.transform);
            s.MakeRing(1.5f);
            return s;
        }

        public override Act Work(Player p, float dt)
        {
            _t += dt;
            if (_t >= 0.8f) { _t = 0; if (G.Session.TakeBag() > 0) Sfx.Play("gather", Pos); }
            return Act.Gather;
        }

        void Update()
        {
            var bag = G.Session.State.lostBag;
            bool on = Active;
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(on || transform.GetChild(i).GetComponent<Ring>() != null);
            if (on) transform.position = Island.Ground(bag.x, bag.z);
        }
    }

    /// <summary>The ring mesh on the ground.</summary>
    public sealed class Ring : MonoBehaviour
    {
        Material _mat;
        static readonly Color Green = new Color(0.32f, 1f, 0.3f, 0.9f), Amber = new Color(1f, 0.72f, 0.2f, 0.85f);

        float _radius;
        Vector3 _builtAt = new Vector3(float.NaN, 0, 0);
        MeshFilter _mf;
        /// <summary>A flat golden ring (the map's selection marker) that pulses by itself.</summary>
        public bool Gold;

        void Update()
        {
            if (!Gold) return;
            _mat.SetColor("_Color", new Color(1f, 0.82f, 0.25f, 0.95f));
            _mat.SetFloat("_Pulse", 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4));
            _mat.SetFloat("_Fill", 0);
            if (_mf.sharedMesh == null || float.IsNaN(_builtAt.x))
            {
                _builtAt = Vector3.zero;
                _mf.sharedMesh = Annulus(Vector3.zero, _radius, Mathf.Max(0.32f, _radius * 0.08f), true);
            }
        }

        public static Ring Create(Transform parent, float radius)
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var r = go.AddComponent<Ring>();
            r._radius = radius;
            r._mf = mf;
            r._mat = new Material(Shader.Find("LastTide/Ring"));
            mr.sharedMaterial = r._mat;
            return r;
        }

        public void Set(float fill, bool here, bool blocked)
        {
            _mat.SetFloat("_Fill", fill);
            float pulse = here ? 1 : 0.5f + 0.5f * Mathf.Sin(Time.time * 4 + transform.position.x);
            _mat.SetFloat("_Pulse", pulse);
            _mat.SetColor("_Color", blocked ? Amber : Green);
            var p = transform.parent.position;
            if ((p - _builtAt).sqrMagnitude > 0.01f || float.IsNaN(_builtAt.x))
            {
                _builtAt = p;
                if (_mf.sharedMesh != null) Destroy(_mf.sharedMesh);
                _mf.sharedMesh = Annulus(p, _radius, 0.32f);
            }
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
        }

        /// <summary>A ring of quads lying on the terrain around <paramref name="c"/> (world space).</summary>
        static Mesh Annulus(Vector3 c, float r, float width, bool flat = false)
        {
            const int seg = 48;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float u = i / (float)seg, a = u * Mathf.PI * 2;
                // Clockwise from the top of the screen (+Z).
                var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                v.Add(flat ? c + d * (r - width) : OnGround(c + d * (r - width), c.y)); uv.Add(new Vector2(u, 0));
                v.Add(flat ? c + d * r : OnGround(c + d * r, c.y)); uv.Add(new Vector2(u, 1));
                if (i < seg)
                {
                    int k = i * 2;
                    t.Add(k); t.Add(k + 1); t.Add(k + 3);
                    t.Add(k); t.Add(k + 3); t.Add(k + 2);
                }
            }
            var m = new Mesh { name = "Ring" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Vector3 OnGround(Vector3 p, float floor)
        {
            return new Vector3(p.x, Mathf.Max(Island.Height(p.x, p.z), Island.Sea, floor - 0.3f) + 0.09f, p.z);
        }
    }
}

namespace LastTide.Game
{
    /// <summary>The raft on the beach: stand in its ring to put to sea and open the map.</summary>
    public sealed class ExitSpot : Spot
    {
        float _t;
        public override string Kind { get { return "exit"; } }
        public override bool Active { get { return true; } }
        public override string Label { get { return "To the map"; } }
        public override float Fill { get { return Mathf.Clamp01(_t / 0.8f); } }

        public static ExitSpot Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Raft");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var s = go.AddComponent<ExitSpot>();
            Models.Raft().ToObject("Raft", go.transform);
            Obstacles.Add(pos, 1.2f, s);
            s.MakeRing(3.0f);
            return s;
        }

        public override Act Work(Player p, float dt)
        {
            _t += dt;
            if (_t >= 0.8f) { _t = 0; Hud.Instance.OpenMap(); }
            return Act.None;
        }

        public override void Leave() { _t = 0; }
    }

    /// <summary>A survivor the radio led the castaway to: hand over what they asked for to rescue them.</summary>
    public sealed class NpcSpot : Spot
    {
        LastTide.Core.SignalDef _sig;
        SurvivorRig _rig;
        float _t, _talk;
        public override string Kind { get { return "npc"; } }
        public override bool Active { get { var s = G.Session; return s.Heard(_sig) && !s.Rescued(_sig); } }
        public override bool Blocked { get { return !G.Session.CanRescue(_sig); } }
        public override float Fill { get { return Mathf.Clamp01(_t / 1.2f); } }
        public override string Label { get { return _sig.Npc; } }
        public override string Detail
        {
            get
            {
                return "Needs: " + string.Join("   ", System.Linq.Enumerable.Select(_sig.Need, n => LastTide.Core.Content.NameOf(n.id) + " " + G.Session.Count(n.id) + "/" + n.n));
            }
        }

        public static NpcSpot Create(Transform parent, LastTide.Core.SignalDef sig, Vector3 pos)
        {
            var go = new GameObject("Survivor " + sig.Npc);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, 180, 0);
            var s = go.AddComponent<NpcSpot>();
            s._sig = sig;
            var look = new SurvivorRig.Look { Shirt = Palette.Hex(0x6A8AA8), Pants = Palette.Hex(0x5A4A3A), Hair = Palette.Hex(0xB8B0A0), Band = Palette.Hex(0x3A5A7A), Pack = false };
            if (sig.Npc == "Mara") { look.Beard = false; look.Hair = Palette.Hex(0x5A2E1A); look.Shirt = Palette.Hex(0xC8A24A); }
            s._rig = SurvivorRig.Build(go.transform, look);
            Obstacles.Add(pos, 0.5f, s);
            s.MakeRing(2.2f);
            return s;
        }

        public override Act Work(Player p, float dt)
        {
            if (!G.Session.CanRescue(_sig)) { _t = 0; return Act.None; }
            _t += dt;
            if (_t >= 1.2f)
            {
                _t = 0;
                if (G.Session.Rescue(_sig)) { Sfx.Play("quest", Pos); Hud.Instance.Say(_sig.Npc, _sig.Thanks); _talk = 3; }
            }
            return Act.Gather;
        }

        void Update()
        {
            // Sitting hurt and waving until rescued; standing and cheering after.
            bool saved = G.Session.Rescued(_sig);
            _talk -= Time.deltaTime;
            _rig.Pose(0, 0, saved ? Act.None : Act.Rest, Time.time, saved && _talk > 0 ? Mathf.Repeat(Time.time * 1.5f, 1f) : -1);
            var pl = Player.Instance;
            if (pl != null)
            {
                var d = pl.transform.position - transform.position; d.y = 0;
                if (d.sqrMagnitude < 100 && d.sqrMagnitude > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 3);
            }
        }
    }
}
