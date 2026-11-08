using System.Collections.Generic;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>Dust, wood chips and the sounds of building.</summary>
    public static class BuildFx
    {
        static ParticleSystem _dust, _chips;

        /// <summary>A low ring of dust kicked up around a point (something set down, a wall raised).</summary>
        public static void Dust(Vector3 pos, float radius, int count = 14)
        {
            if (_dust == null) _dust = Fx.Quiet(FxKind.Dust);
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                var d = Quaternion.Euler(0, i * 360f / count + Random.Range(-10f, 10f), 0) * Vector3.forward;
                p.position = pos + d * radius + Vector3.up * 0.1f;
                p.velocity = d * Random.Range(0.8f, 2f) + Vector3.up * Random.Range(0.2f, 0.7f);
                p.startSize = Random.Range(0.5f, 1.1f);
                p.startLifetime = Random.Range(0.6f, 1.1f);
                _dust.Emit(p, 1);
            }
        }

        /// <summary>Splinters and chips flying from timber being hammered or broken.</summary>
        public static void Chips(Vector3 pos, int count = 8)
        {
            if (_chips == null)
            {
                _chips = Fx.Quiet(FxKind.Dust);
                var main = _chips.main;
                main.gravityModifier = 1.6f;
            }
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                p.position = pos + Random.insideUnitSphere * 0.2f;
                p.velocity = Random.insideUnitSphere * 2.2f + Vector3.up * 2.6f;
                p.startSize = Random.Range(0.06f, 0.14f);
                p.startLifetime = Random.Range(0.5f, 0.9f);
                p.startColor = Color.Lerp(new Color(0.55f, 0.38f, 0.22f), new Color(0.8f, 0.66f, 0.45f), Random.value);
                _chips.Emit(p, 1);
            }
        }

        static ParticleSystem _sparks;

        /// <summary>Orange sparks from a furnace or a hot hammer blow.</summary>
        public static void Sparks(Vector3 pos, int count = 6)
        {
            if (_sparks == null)
            {
                _sparks = Fx.Quiet(FxKind.Sparkle);
                var main = _sparks.main;
                main.gravityModifier = 0.8f;
            }
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                p.position = pos;
                p.velocity = Random.insideUnitSphere * 1.6f + Vector3.up * 2.2f;
                p.startSize = Random.Range(0.05f, 0.11f);
                p.startLifetime = Random.Range(0.4f, 0.8f);
                p.startColor = Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.85f, 0.4f), Random.value);
                _sparks.Emit(p, 1);
            }
        }

        /// <summary>The look of a station at work: hammering at the workbench, sparks at the furnace, a bubbling pot.</summary>
        public static void Working(string station, Vector3 at)
        {
            switch (station)
            {
                case "workbench": Hammer(at + Vector3.up * 1.0f); break;
                case "furnace": Sparks(at + Vector3.up * 1.1f, 8); Sfx.Play("mine", at, 0.15f, 1.6f); break;
                case "campfire": Sparks(at + Vector3.up * 0.5f, 3); break;
            }
        }

        public static void Thud(Vector3 pos, float size = 1)
        {
            Sfx.Play("deliver", pos, 0.55f, Mathf.Lerp(1.1f, 0.6f, Mathf.Clamp01(size / 3)));
            Dust(pos, 0.3f + size * 0.25f, 8 + (int)(size * 4));
        }

        public static void Hammer(Vector3 pos) { Sfx.Play("chop", pos, 0.3f, 1.55f); Chips(pos, 4); }

        static float Ease(float t) { return 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 3); }

        /// <summary>0 -> 1 with a little overshoot at the end (a part settling into place).</summary>
        public static float Back(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.9f;
            return 1 + (c + 1) * Mathf.Pow(t - 1, 3) + c * Mathf.Pow(t - 1, 2);
        }

        public static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) if (!(r is ParticleSystemRenderer)) b.Encapsulate(r.bounds);
            return b;
        }
    }

    /// <summary>
    /// A camp structure going up as materials are delivered: its parts (in the order they were made: poles before the
    /// ridge, the ridge before the sail) drop into place one by one as the build fills, inside a rough driftwood
    /// scaffold. When the last delivery lands the rest follow in quick succession, the scaffold falls away and the
    /// whole thing settles with a puff of dust.
    /// </summary>
    public sealed class Assembly : MonoBehaviour
    {
        sealed class Part
        {
            public Transform T;
            public Vector3 Pos, Scale;
            public Quaternion Rot;
            public float Since = -1;   // time it started dropping in (-1 hidden)
            public bool Landed;
            public float Size;
        }

        readonly List<Part> _parts = new List<Part>();
        GameObject _scaffold;
        float _scaffoldFall = -1;
        int _shown;
        float _queueAt;

        public static Assembly Attach(GameObject built)
        {
            var a = built.AddComponent<Assembly>();
            var t = built.transform;
            // Parts: the children that draw something (a single-mesh model is one part).
            foreach (Transform c in t)
                if (c.GetComponentInChildren<Renderer>() != null)
                    a._parts.Add(new Part { T = c, Pos = c.localPosition, Rot = c.localRotation, Scale = c.localScale, Size = BuildFx.Bounds(c).size.magnitude });
            if (a._parts.Count == 0) a._parts.Add(new Part { T = t, Pos = t.localPosition, Rot = t.localRotation, Scale = t.localScale, Size = 2 });
            return a;
        }

        /// <summary>Shows the parts the delivered share has paid for (all of them at 1), dropping in the new ones.</summary>
        public void SetProgress(float p, bool animate)
        {
            int n = _parts.Count;
            int want = p >= 1 ? n : Mathf.Min(n - 1, Mathf.FloorToInt(p * n));
            if (want == _shown) return;
            if (!animate)
            {
                for (int i = 0; i < n; i++) Show(_parts[i], i < want, false);
                _shown = want;
                return;
            }
            if (want < _shown) { for (int i = want; i < _shown; i++) Show(_parts[i], false, false); _shown = want; return; }
            // New parts drop in one after another (a quick cascade when the build completes).
            float gap = p >= 1 ? 0.09f : 0.18f;
            _queueAt = Mathf.Max(Time.time, _queueAt);
            enabled = true;
            for (int i = _shown; i < want; i++) { _parts[i].Since = _queueAt; _parts[i].Landed = false; _parts[i].T.gameObject.SetActive(true); Pose(_parts[i], 0); _queueAt += gap; }
            _shown = want;
        }

        void Show(Part p, bool on, bool animate)
        {
            p.T.gameObject.SetActive(on);
            p.Since = on ? (animate ? Time.time : -2) : -1;
            p.Landed = !animate;
            if (on && !animate) { p.T.localPosition = p.Pos; p.T.localRotation = p.Rot; p.T.localScale = p.Scale; }
        }

        void Pose(Part p, float t)
        {
            // Lowered in from above, turning the last few degrees, growing from 60% with a little overshoot.
            float drop = 1 - BuildFx.Back(t);
            p.T.localPosition = p.Pos + Vector3.up * (0.9f + p.Size * 0.25f) * Mathf.Max(0, drop);
            p.T.localRotation = p.Rot * Quaternion.Euler(0, (1 - Mathf.Clamp01(t)) * 25, 0);
            p.T.localScale = p.Scale * Mathf.Lerp(0.6f, 1, BuildFx.Back(t));
        }

        /// <summary>The rough scaffold around a structure while it's being built.</summary>
        public void Scaffold(bool on)
        {
            if (on && _scaffold == null && _scaffoldFall < 0)
            {
                var b = BuildFx.Bounds(transform);
                var t = transform.parent;
                _scaffold = new GameObject("Scaffold");
                _scaffold.transform.SetParent(t, false);
                var c = t.InverseTransformPoint(b.center);
                float hx = Mathf.Max(1.1f, b.extents.x + 0.35f), hz = Mathf.Max(1.1f, b.extents.z + 0.35f), h = Mathf.Clamp(b.size.y + 0.5f, 1.6f, 4f);
                var corners = new[] { new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(hx, 0, hz), new Vector3(-hx, 0, hz) };
                for (int i = 0; i < 4; i++)
                {
                    var bottom = new Vector3(c.x, 0, c.z) + corners[i];
                    Models.Pole(_scaffold.transform, bottom + Vector3.down * 0.1f, bottom + Vector3.up * h);
                    var next = new Vector3(c.x, 0, c.z) + corners[(i + 1) % 4];
                    Models.Pole(_scaffold.transform, bottom + Vector3.up * h * 0.55f, next + Vector3.up * h * 0.6f);
                }
            }
            else if (!on && _scaffold != null && _scaffoldFall < 0) { _scaffoldFall = 0; enabled = true; }
        }

        void Update()
        {
            bool any = false;
            foreach (var p in _parts)
            {
                if (p.Since < 0 || p.Landed) continue;
                float t = (Time.time - p.Since) / 0.42f;
                if (t < 0) { p.T.gameObject.SetActive(false); any = true; continue; }
                p.T.gameObject.SetActive(true);
                Pose(p, t);
                if (t >= 1)
                {
                    p.Landed = true;
                    p.T.localPosition = p.Pos; p.T.localRotation = p.Rot; p.T.localScale = p.Scale;
                    var b = BuildFx.Bounds(p.T);
                    BuildFx.Thud(new Vector3(b.center.x, b.min.y, b.center.z), p.Size);
                    BuildFx.Chips(b.center, 5);
                }
                else any = true;
            }
            if (_scaffoldFall >= 0 && _scaffold != null)
            {
                // The scaffold poles topple outward and sink away.
                _scaffoldFall += Time.deltaTime;
                float k = Mathf.Clamp01(_scaffoldFall / 0.9f);
                int i = 0;
                foreach (Transform pole in _scaffold.transform)
                {
                    var away = (pole.position - transform.position); away.y = 0;
                    var axis = Vector3.Cross(Vector3.up, away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward);
                    pole.RotateAround(pole.position, axis, Time.deltaTime * (60 + i * 7) * k);
                    pole.position += Vector3.down * Time.deltaTime * k * k * 2.5f;
                    i++;
                }
                if (k >= 1) { Destroy(_scaffold); _scaffold = null; _scaffoldFall = -1; }
            }
            if (!any && _scaffold == null) enabled = _scaffoldFall >= 0;
        }

        /// <summary>How many parts are standing (or dropping in).</summary>
        public int Shown { get { return _shown; } }
        public int Parts { get { return _parts.Count; } }

        /// <summary>Seconds until the parts still dropping in have all landed.</summary>
        public float Settling { get { return Mathf.Max(0, _queueAt - Time.time) + 0.42f; } }
    }

    /// <summary>A piece placed in build mode coming into being: floors settle, walls rise, objects drop into place.</summary>
    public sealed class Appear : MonoBehaviour
    {
        public enum Style { Floor, Wall, Drop }
        Transform _t;
        Vector3 _pos, _scale;
        Style _style;
        float _time;
        int _taps;

        public static void Play(Transform target, Style style)
        {
            var a = target.gameObject.AddComponent<Appear>();
            a._t = target; a._pos = target.localPosition; a._scale = target.localScale; a._style = style;
            a.Apply(0);
        }

        void Apply(float t)
        {
            switch (_style)
            {
                case Style.Floor:
                    _t.localPosition = _pos + Vector3.down * 0.25f * (1 - BuildFx.Back(t));
                    _t.localScale = Vector3.Scale(_scale, new Vector3(Mathf.Lerp(0.7f, 1, BuildFx.Back(t)), 1, Mathf.Lerp(0.7f, 1, BuildFx.Back(t))));
                    break;
                case Style.Wall:
                    // Raised from the ground up, overshooting a hair and settling.
                    _t.localScale = Vector3.Scale(_scale, new Vector3(1, Mathf.Max(0.02f, BuildFx.Back(t)), 1));
                    break;
                default:
                    // Falls the last metre and a half, lands with a squash, springs back.
                    float fall = Mathf.Clamp01(t / 0.6f);
                    _t.localPosition = _pos + Vector3.up * 1.6f * (1 - fall * fall);
                    float squash = t < 0.6f ? 0 : Mathf.Sin((t - 0.6f) / 0.4f * Mathf.PI) * (1 - (t - 0.6f) / 0.4f);
                    _t.localScale = Vector3.Scale(_scale, new Vector3(1 + squash * 0.12f, 1 - squash * 0.18f, 1 + squash * 0.12f));
                    break;
            }
        }

        void Update()
        {
            float dur = _style == Style.Drop ? 0.65f : 0.5f;
            _time += Time.deltaTime;
            float t = Mathf.Clamp01(_time / dur);
            Apply(_style == Style.Drop ? t : t);
            var at = _t.position;
            if (_style == Style.Wall && _taps < 3 && _time > 0.08f + _taps * 0.14f) { BuildFx.Hammer(at + Vector3.up * (0.4f + _taps * 0.4f)); _taps++; }
            if (_style == Style.Drop && _taps == 0 && t >= 0.6f) { BuildFx.Thud(at, 1.2f); _taps = 1; }
            if (t >= 1)
            {
                _t.localPosition = _pos; _t.localScale = _scale;
                if (_style == Style.Floor) BuildFx.Thud(at, 1.4f);
                if (_style == Style.Wall) BuildFx.Dust(at, 1.2f, 16);
                Destroy(this);
            }
        }
    }

    /// <summary>A piece taken down: it tips, sinks and breaks up in a cloud of dust and splinters, then is gone.</summary>
    public sealed class Crumble : MonoBehaviour
    {
        float _time;
        Vector3 _scale, _axis;

        public static void Play(GameObject go)
        {
            var c = go.AddComponent<Crumble>();
            c._scale = go.transform.localScale;
            c._axis = Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.right;
            var b = BuildFx.Bounds(go.transform);
            Sfx.Play("chop", b.center, 0.5f, 0.75f);
            BuildFx.Chips(b.center, 14);
            BuildFx.Dust(new Vector3(b.center.x, b.min.y, b.center.z), Mathf.Max(0.6f, b.extents.magnitude * 0.5f), 18);
        }

        void Update()
        {
            _time += Time.deltaTime;
            float t = Mathf.Clamp01(_time / 0.55f);
            transform.RotateAround(transform.position, _axis, Time.deltaTime * 70 * t);
            transform.position += Vector3.down * Time.deltaTime * t * 2.2f;
            transform.localScale = _scale * (1 - t * 0.6f);
            if (t >= 1) Destroy(gameObject);
        }
    }
}
