using System.Collections.Generic;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// An animated Quaternius character (Resources/Models/People/{id}: Henry, Anne, Mako, Sharky, Skeleton) driven by
    /// state: Idle, Walk or Run by speed, one-shot Sword strikes and hit reactions, Death, and gestures (Wave, Yes).
    /// </summary>
    public sealed class CharacterArt : MonoBehaviour
    {
        Animation _anim;
        readonly Dictionary<string, string> _clips = new Dictionary<string, string>();
        string _loop;
        float _oneShot;
        bool _dead;

        public static CharacterArt TryCreate(string id, Transform parent, float scale = 1)
        {
            var go = ModelRegistry.Spawn(ModelRegistry.People, id, parent);
            if (go == null) return null;
            go.transform.localScale *= scale;
            var c = go.AddComponent<CharacterArt>();
            c._anim = go.GetComponent<Animation>();
            if (c._anim == null) { Destroy(go); return null; }
            foreach (AnimationState st in c._anim)
            {
                string n = st.name.Substring(st.name.LastIndexOf('|') + 1);
                c._clips[n] = st.name;
                bool once = n == "Death" || n == "Sword" || n == "HitReact" || n == "Punch" || n == "Yes" || n == "Wave" || n == "Jump";
                st.wrapMode = once ? WrapMode.ClampForever : WrapMode.Loop;
            }
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()) { r.updateWhenOffscreen = true; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            c.Loop("Idle");
            return c;
        }

        string Clip(string n) { string c; return _clips.TryGetValue(n, out c) ? c : null; }

        void OnEnable() { if (_anim != null && _loop != null) { var l = _loop; _loop = null; Loop(l); } }

        /// <summary>Keeps a looping clip playing (crossfading in) unless a one-shot is running.</summary>
        public void Loop(string n, float speed = 1)
        {
            if (_dead) return;
            var c = Clip(n);
            if (c == null) return;
            _anim[c].speed = speed;
            if (_oneShot > 0 || _loop == n) return;
            _loop = n;
            _anim.CrossFade(c, 0.2f);
        }

        /// <summary>Plays a clip once (a strike, a flinch, a wave), then goes back to the loop.</summary>
        public void Once(string n, float speed = 1)
        {
            if (_dead) return;
            var c = Clip(n);
            if (c == null) return;
            _anim[c].speed = speed;
            _anim[c].time = 0;
            _anim.CrossFade(c, 0.08f);
            _oneShot = _anim[c].length / Mathf.Max(0.1f, speed) * 0.92f;
        }

        public void Die()
        {
            if (_dead) return;
            var c = Clip("Death");
            if (c != null) { _anim[c].time = 0; _anim.CrossFade(c, 0.1f); }
            _dead = true;
        }

        void Update()
        {
            if (_oneShot > 0)
            {
                _oneShot -= Time.deltaTime;
                if (_oneShot <= 0 && !_dead && _loop != null) { var l = _loop; _loop = null; Loop(l, _anim[Clip(l)].speed); }
            }
        }
    }
}
