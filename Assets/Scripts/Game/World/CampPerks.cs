using System.Collections.Generic;
using LastTide.Core;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// The camp improvements, standing where you can see them: the honey shelf by the fire, the salt barrel, the
    /// signal lamp on its post (lit), the lime bucket by the shelter, the weir down by the raft. Each appears the
    /// moment its improvement is built (with the build sound and a puff of dust) and is there on every later visit.
    /// Scenery only: nothing to walk into or tap.
    /// </summary>
    public sealed class CampPerks : MonoBehaviour
    {
        // Where each stands, from the camp's middle (the weir: from the raft), its facing and its size in the world.
        static readonly (string perk, Vector3 at, float yaw, float scale, bool byRaft)[] Spots =
        {
            ("perk_larder",   new Vector3(-3.6f, 0, 2.0f),  25,  1.0f, false),
            ("perk_salt",     new Vector3(3.3f, 0, -2.3f),  60,  0.9f, false),
            ("perk_lamp",     new Vector3(2.8f, 0, 3.1f),   0,   1.0f, false),
            ("perk_limewash", new Vector3(-6.4f, 0, 3.4f),  40,  1.0f, false),
            ("perk_weir",     Vector3.zero,                 200, 2.4f, true),
        };

        readonly Dictionary<string, GameObject> _props = new Dictionary<string, GameObject>();
        Vector3 _raft;
        Session _session;

        public static CampPerks Create(Transform world, Vector3 raftAt)
        {
            var go = new GameObject("Camp improvements");
            go.transform.SetParent(world, false);
            var c = go.AddComponent<CampPerks>();
            c._raft = raftAt;
            c._session = G.Session;
            c._session.Gained += c.OnGained;
            c.Refresh(false);
            return c;
        }

        Light _lamp;

        void LateUpdate()
        {
            // Like the campfire: a soft glow by day, the camp's second light after dark.
            if (_lamp == null) { var p = transform.Find("perk_lamp"); if (p != null) _lamp = p.GetComponentInChildren<Light>(); if (_lamp == null) return; }
            float night = DayNight.Instance != null ? DayNight.Instance.Night : 0;
            _lamp.intensity = (1.5f + Mathf.PerlinNoise(Time.time * 4, 3) * 0.4f) * (1 + night * 2.6f);
            _lamp.range = 8 + night * 5;
        }

        void OnDestroy() { if (_session != null) _session.Gained -= OnGained; }

        void OnGained(string id, int n) { if (Content.IsPerk(id)) Refresh(true); }

        void Refresh(bool fresh)
        {
            foreach (var (perk, at, yaw, scale, byRaft) in Spots)
            {
                if (_props.ContainsKey(perk) || !_session.HasPerk(perk)) continue;
                var pos = byRaft ? Shallows() : Island.Ground(Island.Camp + at);
                var prop = Build(perk);
                if (prop == null) continue;
                prop.name = perk;
                prop.transform.SetParent(transform, false);
                prop.transform.position = pos;
                prop.transform.rotation = Quaternion.Euler(0, yaw, 0) * prop.transform.rotation;
                prop.transform.localScale *= scale;
                foreach (var t in prop.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
                if (GrassField.Instance != null) GrassField.Instance.ClearAround(pos, 1.2f * scale);
                _props[perk] = prop;
                if (fresh)
                {
                    Burst.Play(pos + Vector3.up * 0.5f);
                    Sfx.Play("built", pos);
                }
            }
        }

        /// <summary>The first knee-deep water straight out to sea from the raft: where the weir is set.</summary>
        Vector3 Shallows()
        {
            var outward = new Vector3(_raft.x, 0, _raft.z).normalized;
            var side = new Vector3(-outward.z, 0, outward.x) * 5f;       // clear of the raft's own launch
            for (float d = 2; d < 40; d += 0.5f)
            {
                var p = _raft + side + outward * d;
                if (Island.Height(p.x, p.z) < Island.Sea - 0.45f) return new Vector3(p.x, Island.Height(p.x, p.z), p.z);
            }
            return Island.Ground(_raft + side);
        }

        static GameObject Build(string perk)
        {
            if (perk == "perk_lamp")
            {
                // The lantern hung on a driftwood post, burning.
                if (ModelRegistry.Prefab(ModelRegistry.Scan, "wooden_lantern_01") == null) return null;
                var holder = new GameObject(perk);
                Models.Pole(holder.transform, new Vector3(0, -0.1f, 0), new Vector3(0, 2.3f, 0));
                Models.Pole(holder.transform, new Vector3(0, 2.15f, 0), new Vector3(0.55f, 2.25f, 0));
                var lantern = ModelRegistry.Spawn(ModelRegistry.Scan, "wooden_lantern_01", holder.transform);
                var b = ModelRegistry.RendererBounds(lantern);
                lantern.transform.position += new Vector3(0.5f, 2.2f, 0) - new Vector3(b.center.x, b.max.y, b.center.z);
                var l = new GameObject("Light").AddComponent<Light>();
                l.type = LightType.Point; l.range = 9; l.intensity = 1.8f; l.color = new Color(1f, 0.72f, 0.4f);
                l.transform.SetParent(holder.transform, false);
                l.transform.localPosition = new Vector3(0.5f, 1.9f, 0);
                Flames.Create(holder.transform, new Vector3(0.5f, 1.82f, 0)).transform.localScale = Vector3.one * 0.16f;   // the wick
                return holder;
            }
            if (perk == "perk_limewash") return IslandModels.IconArt("lime");   // the lime bucket by the shelter's door
            return IslandModels.IconArt(perk);
        }
    }
}
