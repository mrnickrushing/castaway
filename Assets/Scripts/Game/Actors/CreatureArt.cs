using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LastTide.Game
{
    /// <summary>
    /// A realistic animal from Resources/Models/Creatures (Tools/art/sketchfab.py creature, CC0 scans): the crab plays
    /// its own scuttle cycle faster or slower with its pace; the snake has no usable clip, so its spine (a chain of
    /// ~100 bones from the head back) is bent in code into a travelling S that rears up to strike; the gull's wings
    /// are beaten in code too (<see cref="Wings"/>).
    /// </summary>
    public sealed class CreatureArt : MonoBehaviour
    {
        Transform _pivot, _model;
        PlayableGraph _graph;
        AnimationClipPlayable _clip;
        // Snake spine.
        Transform[] _spine;
        Quaternion[] _rest;
        Vector3 _headPos, _headRestPos;
        float _bellyY; // rest height of the middle of the body (pivot space)
        Transform _head;
        Quaternion _headRest;
        float[] _along;
        float _length;
        float _phase;
        bool _snake;
        // Bird wings: inner and outer joints, left then right.
        Transform[] _wing;
        Quaternion[] _wingRest;
        float[] _wingSide;

        public static CreatureArt TryCreate(string id, Transform parent)
        {
            if (ModelRegistry.Prefab(ModelRegistry.Creatures, id) == null) return null;
            var pivot = new GameObject(id).transform;
            pivot.SetParent(parent, false);
            var model = ModelRegistry.Spawn(ModelRegistry.Creatures, id, pivot);
            if (model == null) { Destroy(pivot.gameObject); return null; }
            var c = pivot.gameObject.AddComponent<CreatureArt>();
            c._pivot = pivot;
            c._model = model.transform;
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = false;
            var an = model.GetComponent<Animator>();
            AnimationClip walk = null;
            foreach (var clip in Resources.LoadAll<AnimationClip>("Models/Creatures/" + id))
                if (!clip.name.StartsWith("__preview__")) walk = clip;
            if (an != null && walk != null)
            {
                an.applyRootMotion = false;
                an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                c._clip = AnimationPlayableUtilities.PlayClip(an, walk, out c._graph);
            }
            else
            {
                if (an != null) an.enabled = false;
                c.BuildSpine();
                c.BuildWings();
            }
            return c;
        }

        /// <summary>Finds the chain head.001, head.002... (head first) and the head bone riding beside its start.</summary>
        void BuildSpine()
        {
            var bones = new SortedDictionary<int, Transform>();
            foreach (var t in _model.GetComponentsInChildren<Transform>())
            {
                if (t.name == "head") _head = t;
                int n;
                if (t.name.StartsWith("head.") && int.TryParse(t.name.Substring(5), out n)) bones[n] = t;
            }
            if (bones.Count < 4) return;
            _snake = true;
            _spine = new Transform[bones.Count];
            bones.Values.CopyTo(_spine, 0);
            _rest = new Quaternion[_spine.Length];
            _along = new float[_spine.Length];
            for (int i = 0; i < _spine.Length; i++)
            {
                _rest[i] = _spine[i].localRotation;
                if (i > 0) _along[i] = _along[i - 1] + Vector3.Distance(_spine[i].position, _spine[i - 1].position);
            }
            _length = Mathf.Max(0.1f, _along[_along.Length - 1]);
            _bellyY = _pivot.InverseTransformPoint(_spine[_spine.Length / 2].position).y;
            _headPos = _spine[0].localPosition;
            if (_head != null) { _headRest = _head.localRotation; _headRestPos = _head.localPosition; }
            // The model is centred on its length: slide it back so the head sits a little ahead of the enemy's spot.
            _model.localPosition = new Vector3(0, 0, -_pivot.InverseTransformPoint(_spine[0].position).z * 0.6f);
        }

        /// <summary>A bird's wing joints (wing1/wing2, .l/.r), with the side each spreads to.</summary>
        void BuildWings()
        {
            var names = new[] { "wing1.l", "wing2.l", "wing1.r", "wing2.r" };
            var found = new Transform[4];
            foreach (var t in _model.GetComponentsInChildren<Transform>())
                for (int i = 0; i < 4; i++) if (t.name == names[i]) found[i] = t;
            if (System.Array.IndexOf(found, null) >= 0) return;
            _wing = found;
            _wingRest = new Quaternion[4];
            _wingSide = new float[4];
            for (int i = 0; i < 4; i++)
            {
                _wingRest[i] = found[i].localRotation;
                _wingSide[i] = Mathf.Sign(_pivot.InverseTransformPoint(found[i < 2 ? 1 : 3].position).x);
            }
        }

        /// <summary>Beats a bird's wings: beat is the stroke phase (radians), flap from 0 (gliding, wings held a
        /// little up) to 1 (full strokes, the tips trailing the shoulders).</summary>
        public void Wings(float beat, float flap)
        {
            if (_wing == null) return;
            var fwd = _pivot.forward;
            float inner = Mathf.Lerp(7, Mathf.Sin(beat) * 30 + 4, flap);
            float outer = Mathf.Lerp(-5, Mathf.Sin(beat - 0.9f) * 22, flap);
            for (int i = 0; i < 4; i++)
            {
                // Inner joint first: the outer one is its child and rides on it.
                _wing[i].localRotation = _wingRest[i];
                _wing[i].rotation = Quaternion.AngleAxis((i % 2 == 0 ? inner : outer) * _wingSide[i], fwd) * _wing[i].rotation;
            }
        }

        /// <summary>gait: distance walked (radians of stride), speed m/s, attack 0..1 through a strike or -1, dead 0..1.</summary>
        public void Pose(float gait, float speed, float attack, float dead)
        {
            float strike = attack >= 0 ? Mathf.Sin(attack * Mathf.PI) : 0;
            if (_graph.IsValid())
            {
                // The scuttle cycle is long and slow: speed it up with the pace, keep a little twitch at rest.
                _clip.SetSpeed(dead > 0 ? 0 : speed > 0.15f ? Mathf.Clamp(speed * 1.6f, 0.8f, 3.5f) : 0.25f + strike * 2);
                // A strike: rear up on the back legs and lunge, claws first.
                _pivot.localRotation = Quaternion.Euler(-strike * 22, 0, dead * 180);
                _pivot.localPosition = new Vector3(0, strike * 0.12f + dead * 0.55f, strike * 0.3f);
                return;
            }
            if (!_snake) return;
            _phase += Time.deltaTime * (1.2f + speed * 2.6f) * (1 - dead);
            float amp = Mathf.Lerp(0.18f, 0.55f, Mathf.Clamp01(speed / 1.5f)) * (1 - dead);
            float rear = dead > 0 ? 0 : Mathf.Max(0.35f, strike) * 0.9f; // always a little raised: it's watching you
            Vector3 up = _model.up, back = -_model.forward;
            var down = Quaternion.FromToRotation(back, (back * Mathf.Cos(rear) - up * Mathf.Sin(rear)).normalized);
            int k = 0;
            while (k < _spine.Length - 1 && _along[k] < _length * 0.2f) k++;
            float prev = 0, firstYaw = 0;
            for (int i = 0; i < _spine.Length; i++)
            {
                float s = _along[i] / _length;
                float env = Mathf.SmoothStep(0.25f, 1f, Mathf.Clamp01(s / 0.35f));
                float yaw = Mathf.Sin(s * Mathf.PI * 3.2f - _phase) * amp * env * Mathf.Rad2Deg;
                _spine[i].localRotation = _rest[i];
                _spine[i].rotation = Quaternion.AngleAxis(yaw - prev, up) * _spine[i].rotation;
                prev = yaw;
                if (i == 0) firstYaw = yaw;
                if (i == 0) _spine[i].rotation = down * _spine[i].rotation;           // the neck slopes up to the head
                if (i == k) _spine[i].rotation = Quaternion.Inverse(down) * _spine[i].rotation; // and the body lies flat
            }
            // Lift the head by the height the sloping neck climbs, so the body stays on the ground.
            var lift = up * (_along[k] * Mathf.Sin(rear));
            var parent = _spine[0].parent;
            var liftLocal = parent != null ? parent.InverseTransformVector(lift) : lift;
            _spine[0].localPosition = _headPos + liftLocal;
            if (_head != null)
            {
                // The skull rides beside the first spine bone: same lift, same turn, but it keeps looking ahead.
                _head.localRotation = _headRest;
                _head.rotation = Quaternion.AngleAxis(firstYaw, up) * _head.rotation;
                _head.localPosition = _headRestPos + liftLocal;
            }
            // The neck bend leaves the body a few centimetres up: set the belly back on the ground.
            float sink = _bellyY - _pivot.InverseTransformPoint(_spine[_spine.Length / 2].position).y;
            _pivot.localPosition = new Vector3(0, sink, strike * 0.35f);
            _pivot.localRotation = Quaternion.Euler(0, 0, dead * 150);
        }

        void OnDestroy() { if (_graph.IsValid()) _graph.Destroy(); }
    }
}
