using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LastTide.Game
{
    /// <summary>
    /// Plays the shared humanoid clips (Resources/Models/Cast/moves: Quaternius' Universal Animation Library, CC0) on a
    /// cast member (Resources/Models/Cast/{id}, imported as Humanoid) through a small Playables mixer: one looping
    /// clip crossfades into the next, and one-shots (a strike, a flinch) play over the loop and hand back to it.
    /// Death holds its last frame. No AnimatorController needed; the clips retarget through each model's Avatar.
    /// </summary>
    public sealed class HumanMotion : MonoBehaviour
    {
        static Dictionary<string, AnimationClip> _clips;

        /// <summary>The shared clips by short name (Idle_Loop, Walk_Loop, Sword_Attack...).</summary>
        public static Dictionary<string, AnimationClip> Clips
        {
            get
            {
                if (_clips == null)
                {
                    _clips = new Dictionary<string, AnimationClip>();
                    foreach (var c in Resources.LoadAll<AnimationClip>("Models/Cast/moves"))
                    {
                        if (c.name.StartsWith("__preview__")) continue;
                        _clips[c.name.Substring(c.name.LastIndexOf('|') + 1)] = c;
                    }
                }
                return _clips;
            }
        }

        public Animator Animator { get; private set; }
        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        readonly Dictionary<string, int> _slot = new Dictionary<string, int>();
        readonly List<AnimationClipPlayable> _plays = new List<AnimationClipPlayable>();
        readonly List<float> _weights = new List<float>();
        int _loop = -1, _once = -1;
        float _fade = 0.2f, _onceLeft;
        bool _hold;

        public static HumanMotion Attach(GameObject go, bool alwaysAnimate = false)
        {
            var an = go.GetComponent<Animator>();
            if (an == null || an.avatar == null || !an.avatar.isHuman || Clips.Count == 0) return null;
            an.applyRootMotion = false;
            an.cullingMode = alwaysAnimate ? AnimatorCullingMode.AlwaysAnimate : AnimatorCullingMode.CullUpdateTransforms;
            var m = go.AddComponent<HumanMotion>();
            m.Animator = an;
            m._graph = PlayableGraph.Create(go.name + " motion");
            m._graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            m._mixer = AnimationMixerPlayable.Create(m._graph, 0);
            var output = AnimationPlayableOutput.Create(m._graph, "out", an);
            output.SetSourcePlayable(m._mixer);
            m._graph.Play();
            return m;
        }

        public bool Has(string clip) { return Clips.ContainsKey(clip); }

        int Slot(string clip)
        {
            int i;
            if (_slot.TryGetValue(clip, out i)) return i;
            AnimationClip c;
            if (!Clips.TryGetValue(clip, out c)) return -1;
            var p = AnimationClipPlayable.Create(_graph, c);
            p.SetApplyFootIK(true);
            i = _mixer.GetInputCount();
            _mixer.SetInputCount(i + 1);
            _graph.Connect(p, 0, _mixer, i);
            _mixer.SetInputWeight(i, 0);
            _plays.Add(p);
            _weights.Add(0);
            _slot[clip] = i;
            return i;
        }

        /// <summary>Keeps a looping clip playing at a speed, crossfading in if it isn't already.</summary>
        public void Loop(string clip, float speed = 1, float fade = 0.2f)
        {
            if (_hold) return;
            int i = Slot(clip);
            if (i < 0) return;
            _plays[i].SetSpeed(speed);
            if (i == _loop) return;
            _loop = i;
            _fade = fade;
            var p = _plays[i];
            if (_weights[i] < 0.01f) p.SetTime(Random.value * p.GetAnimationClip().length);
        }

        /// <summary>Plays a clip once over the loop, then fades back. Returns its length in seconds.</summary>
        public float Once(string clip, float speed = 1, float fade = 0.08f, bool hold = false)
        {
            if (_hold) return 0;
            int i = Slot(clip);
            if (i < 0) return 0;
            var p = _plays[i];
            p.SetSpeed(speed);
            p.SetTime(0);
            p.SetDone(false);
            _once = i;
            _fade = fade;
            _hold = hold;
            float len = p.GetAnimationClip().length / Mathf.Max(0.05f, speed);
            _onceLeft = hold ? float.MaxValue : len * 0.94f;
            return len;
        }

        public bool Busy { get { return _once >= 0; } }

        void Update()
        {
            if (!_graph.IsValid()) return;
            if (_once >= 0)
            {
                _onceLeft -= Time.deltaTime;
                var op = _plays[_once];
                if (_hold && op.GetTime() >= op.GetAnimationClip().length) { op.SetSpeed(0); op.SetTime(op.GetAnimationClip().length - 0.001f); }
                if (_onceLeft <= 0) { _once = -1; _fade = 0.18f; }
            }
            if (_loop >= 0)
            {
                // Clips without a loop flag (kneeling at work) wrap by hand.
                var lp = _plays[_loop];
                float len = lp.GetAnimationClip().length;
                if (!lp.GetAnimationClip().isLooping && len > 0 && lp.GetTime() > len) lp.SetTime(lp.GetTime() % len);
            }
            int target = _once >= 0 ? _once : _loop;
            float step = Time.deltaTime / Mathf.Max(0.01f, _fade);
            float sum = 0;
            for (int i = 0; i < _weights.Count; i++)
            {
                _weights[i] = Mathf.MoveTowards(_weights[i], i == target ? 1 : 0, step);
                sum += _weights[i];
            }
            for (int i = 0; i < _weights.Count; i++) _mixer.SetInputWeight(i, sum > 0 ? _weights[i] / sum : 0);
        }

        void OnDestroy() { if (_graph.IsValid()) _graph.Destroy(); }
    }
}
