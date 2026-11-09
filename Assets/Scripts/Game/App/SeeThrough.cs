using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastTide.Game
{
    /// <summary>
    /// Keeps the castaway in sight: a tree, roof or tower standing between the camera and them stops drawing (its
    /// shadow stays, so the ground still reads as shaded) and comes back once they've walked out from behind it.
    /// Works on the renderers' bounds, so it needs no colliders and no special shader on the scanned art.
    /// </summary>
    public sealed class SeeThrough
    {
        readonly List<Renderer> _tall = new List<Renderer>();
        readonly Dictionary<Renderer, float> _hidden = new Dictionary<Renderer, float>(); // renderer -> last time it blocked
        readonly List<Renderer> _drop = new List<Renderer>();
        readonly Dictionary<Renderer, bool> _actor = new Dictionary<Renderer, bool>(); // rides on a character: never hide
        float _rescanAt;

        /// <summary>Call after the camera has moved this frame. target: the castaway's feet (or the build ghost).</summary>
        public void Tick(Camera cam, Vector3 target)
        {
            if (Time.unscaledTime >= _rescanAt) Rescan();
            var eye = cam.transform.position;
            float now = Time.unscaledTime;
            for (int i = 0; i < _tall.Count; i++)
            {
                var r = _tall[i];
                if (r == null) continue;
                var b = r.bounds;
                // Only what stands on the camera's side (the camera looks north) and isn't what the castaway is at:
                // the palm being chopped, a structure going up around its site, the lighthouse door stay drawn.
                if (b.center.z > target.z - 0.5f) continue;
                float dx = b.center.x - target.x, dz = b.center.z - target.z;
                if (dx * dx + dz * dz < 2.5f * 2.5f) continue;
                if (Blocks(b, eye, target + Vector3.up * 0.9f) || Blocks(b, eye, target + Vector3.up * 1.7f))
                {
                    if (!_hidden.ContainsKey(r)) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    _hidden[r] = now;
                }
            }
            // Bring back what no longer blocks, after a moment so walking along an edge doesn't flicker it.
            _drop.Clear();
            foreach (var kv in _hidden)
                if (kv.Key == null || now - kv.Value > 0.35f) _drop.Add(kv.Key);
            foreach (var r in _drop)
            {
                if (r != null) r.shadowCastingMode = ShadowCastingMode.On;
                _hidden.Remove(r);
            }
        }

        /// <summary>Draws everything again (leaving the island, opening the map).</summary>
        public void ShowAll()
        {
            foreach (var kv in _hidden) if (kv.Key != null) kv.Key.shadowCastingMode = ShadowCastingMode.On;
            _hidden.Clear();
        }

        static bool Blocks(Bounds b, Vector3 eye, Vector3 p)
        {
            var d = p - eye;
            float dist = d.magnitude;
            float t;
            return b.IntersectRay(new Ray(eye, d / dist), out t) && t < dist - 0.6f;
        }

        /// <summary>Collects the things tall enough to hide the castaway: taller than 2 m, but not the terrain, sea or
        /// cliffs (wider than 16 m), nor anything that moves with an actor (worked out once per renderer).</summary>
        void Rescan()
        {
            _rescanAt = Time.unscaledTime + 3f;
            var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            _tall.Clear();
            if (_actor.Count > all.Length * 2) _actor.Clear(); // islands come and go: forget the old ones now and then
            foreach (var r in all)
            {
                var b = r.bounds;
                if (b.size.y < 2f || Mathf.Max(b.size.x, b.size.z) > 16f) continue;
                if (r.shadowCastingMode != ShadowCastingMode.On && !_hidden.ContainsKey(r)) continue; // effects, rings
                bool actor;
                if (!_actor.TryGetValue(r, out actor))
                    _actor[r] = actor = r.GetComponentInParent<Player>() != null || r.GetComponentInParent<Enemy>() != null || r.GetComponentInParent<Pet>() != null;
                if (!actor) _tall.Add(r);
            }
        }
    }
}
