using System.Collections.Generic;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// Sound effects from Resources/Audio/{name} (several takes as name, name_2, name_3 are picked at random), played
    /// in 3D near the camera focus; the surf and jungle beds loop underneath. Missing clips are skipped.
    /// </summary>
    public static class Sfx
    {
        static readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        static AudioSource[] _pool;
        static int _next;
        static AudioSource _surf, _jungle, _music;
        public static bool Muted;
        /// <summary>Scales the ambience and music beds (a cinematic turns them down to 0 while it plays).</summary>
        public static float Duck = 1;

        public static void Init(Transform root)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(root, false);
            _pool = new AudioSource[16];
            for (int i = 0; i < _pool.Length; i++)
            {
                var s = new GameObject("Voice").AddComponent<AudioSource>();
                s.transform.SetParent(go.transform, false);
                s.playOnAwake = false;
                s.spatialBlend = 0.6f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 8; s.maxDistance = 60;
                _pool[i] = s;
            }
            _surf = Loop(go.transform, "amb_surf", 0.5f);
            _jungle = Loop(go.transform, "amb_jungle", 0.35f);
            _music = Loop(go.transform, "music", 0.28f);
        }

        static AudioSource Loop(Transform parent, string name, float vol)
        {
            var clip = Clip(name);
            if (clip == null) return null;
            var s = new GameObject(name).AddComponent<AudioSource>();
            s.transform.SetParent(parent, false);
            s.clip = clip; s.loop = true; s.volume = vol; s.spatialBlend = 0;
            s.Play();
            return s;
        }

        static AudioClip Clip(string name)
        {
            AudioClip[] takes;
            if (!_clips.TryGetValue(name, out takes))
            {
                var list = new List<AudioClip>();
                var a = Resources.Load<AudioClip>("Audio/" + name);
                if (a != null) list.Add(a);
                for (int i = 2; i <= 4; i++) { var b = Resources.Load<AudioClip>("Audio/" + name + "_" + i); if (b != null) list.Add(b); }
                _clips[name] = takes = list.ToArray();
            }
            return takes.Length == 0 ? null : takes[Random.Range(0, takes.Length)];
        }

        public static void Play(string name) { Play(name, null, 1f); }
        public static void Play(string name, Vector3 at, float volume = 1f) { Play(name, (Vector3?)at, volume); }

        /// <summary>A sound at a set pitch (hammer taps, thuds of timber settling).</summary>
        public static void Play(string name, Vector3 at, float volume, float pitch) { Play(name, (Vector3?)at, volume, pitch); }

        static void Play(string name, Vector3? at, float volume, float pitch = 0)
        {
            if (_pool == null || Muted) return;
            var clip = Clip(name);
            if (clip == null) return;
            var s = _pool[_next = (_next + 1) % _pool.Length];
            var cam = CameraRig.Instance != null ? CameraRig.Instance.transform.position : Vector3.zero;
            s.transform.position = at ?? cam;
            s.spatialBlend = at.HasValue ? 0.55f : 0;
            s.pitch = pitch > 0 ? pitch * Random.Range(0.97f, 1.03f) : Random.Range(0.94f, 1.06f);
            s.PlayOneShot(clip, volume);
        }

        /// <summary>Surf louder near the beach, jungle louder inland.</summary>
        public static void Ambience(Vector3 player)
        {
            if (_surf == null && _jungle == null) return;
            float d = Island.Distance01(player.x, player.z);
            float beach = Mathf.Clamp01((d - 0.55f) / 0.35f);
            if (_surf != null) _surf.volume = Muted ? 0 : Mathf.Lerp(0.15f, 0.55f, beach) * Duck;
            if (_jungle != null) _jungle.volume = Muted ? 0 : Mathf.Lerp(0.4f, 0.1f, beach) * Duck;
            if (_music != null) _music.volume = Muted ? 0 : 0.28f * Duck;
        }
    }
}
