using System.Collections.Generic;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// Drop-in art: if a prefab exists at Resources/Models/{Category}/{id}, the game uses it instead of the
    /// procedural model. Categories: Props, Buildings, Animals, People.
    /// </summary>
    public static class ModelRegistry
    {
        public const string Props = "Props", Buildings = "Buildings", Animals = "Animals", People = "People", Free = "Free", Scan = "Scan";

        /// <summary>
        /// Photoscanned Poly Haven replacements (Resources/Models/Scan, Tools/art/polyhaven.py) for the free kit models:
        /// wherever the game asks for a kit model listed here, it gets one of these instead (picked by position, so a
        /// field of rocks isn't one rock repeated).
        /// </summary>
        public static readonly Dictionary<string, string[]> ScanFor = new Dictionary<string, string[]>
        {
            { "rock_a", new[] { "boulder_01", "rock_07" } },
            { "rock_b", new[] { "rock_moss_set_01_a", "rock_moss_set_01_c", "stone_01" } },
            { "rock_c", new[] { "rock_moss_set_01_b", "rock_moss_set_01_d", "rock_07" } },
            { "rock_tall", new[] { "coast_rocks_05" } },
            { "rock_tall2", new[] { "coast_rocks_05" } },
            { "rocks_sand_a", new[] { "rock_moss_set_01_e", "stone_01" } },
            { "rocks_sand_b", new[] { "rock_07", "rock_moss_set_01_a" } },
            { "crate", new[] { "wooden_crate_01", "wooden_crate_02" } },
            { "crate_bottles", new[] { "wooden_crate_02" } },
            { "barrel", new[] { "wooden_barrels_01_d" } },
            { "chest", new[] { "treasure_chest" } },
            { "bucket", new[] { "wooden_bucket_01" } },
            { "box", new[] { "wooden_crate_01" } },
            { "log", new[] { "dead_tree_trunk" } },
            { "stump", new[] { "tree_stump_01" } },
            { "bush_large", new[] { "shrub_04", "fern_02_a", "fern_02_c" } },
            { "bush_detailed", new[] { "fern_02_b", "fern_02_d", "anthurium_botany_01_a", "calathea_orbifolia_01_a" } },
            { "flower_red", new[] { "anthurium_botany_01_c", "anthurium_botany_01_e" } },
            { "flower_yellow", new[] { "shrub_sorrel_01_c", "shrub_sorrel_01_h" } },
            { "flower_purple", new[] { "calathea_orbifolia_01_c", "calathea_orbifolia_01_e" } },
            { "mushrooms", new[] { "grass_medium_02_c", "dry_branches_medium_01_a" } },
            { "tree_oak", new[] { "island_tree_02" } },
            { "tree_fat", new[] { "island_tree_02" } },
            { "tree_detailed", new[] { "island_tree_02" } },
            { "pirate_ship", new[] { "dutch_ship_large_01" } },
        };

        static int _pick;

        /// <summary>The scanned stand-in for a kit model, or null.</summary>
        public static string ScanId(string id, Vector3 at)
        {
            string[] alts;
            if (!ScanFor.TryGetValue(id, out alts)) return null;
            int h = Mathf.Abs(Mathf.RoundToInt(at.x * 7.3f) * 31 + Mathf.RoundToInt(at.z * 3.7f) * 17 + _pick++);
            for (int i = 0; i < alts.Length; i++)
            {
                var a = alts[(h + i) % alts.Length];
                if (Prefab(Scan, a) != null) return a;
            }
            return null;
        }

        /// <summary>A free kit model (Resources/Models/Free/{id}) placed at a world position, turned and scaled; null if missing.</summary>
        public static GameObject Free_(string id, Transform parent, Vector3 pos, float yaw = 0, float scale = 1)
        {
            var scan = ScanId(id, pos);
            var go = scan != null ? Spawn(Scan, scan, parent) : Spawn(Free, id, parent);
            if (go == null) return null;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0) * go.transform.rotation;
            go.transform.localScale *= scale;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }
        static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();

        public static GameObject Prefab(string category, string id)
        {
            string key = category + "/" + id;
            GameObject prefab;
            if (!_cache.TryGetValue(key, out prefab))
            {
                prefab = Resources.Load<GameObject>("Models/" + key);
                _cache[key] = prefab;
            }
            return prefab;
        }

        public static bool Has(string category, string id) { return Prefab(category, id) != null || (category == Free && ScanId(id, Vector3.zero) != null); }

        /// <summary>Instantiates the prefab (or returns null). Colliders inside art are removed; the game adds its own.</summary>
        public static GameObject Spawn(string category, string id, Transform parent)
        {
            // A photoscanned stand-in wins over the kit model wherever there is one.
            if (category == Free) { var alt = ScanId(id, Vector3.zero); if (alt != null) { category = Scan; id = alt; } }
            var prefab = Prefab(category, id);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = id;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            if (category == Scan) ScanMaterials(go, id);
            else
            {
                var mat = Material(category, id);
                // Free kit models and the Quaternius characters carry their colours in the vertices.
                if (mat == null && (category == Free || category == People)) mat = Materials.Kit;
                if (mat != null)
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            }
            if (!id.EndsWith("_far")) AddFarLod(go, category, id);
            return go;
        }

        /// <summary>
        /// Trees drawn by the hundred carry a light stand-in (<c>id_far</c>, same textures) that takes over when the tree
        /// is small on screen, including in the shadow pass. Keeps the jungle inside the phone's triangle budget.
        /// </summary>
        static void AddFarLod(GameObject go, string category, string id)
        {
            if (Prefab(category, id + "_far") == null) return;
            var far = Spawn(category, id + "_far", go.transform);
            if (far == null) return;
            far.transform.localPosition = Vector3.zero;
            far.transform.localRotation = Quaternion.identity;
            far.transform.localScale = Vector3.one;
            // A far model built at the same size as the near one, but imported with its own scale: match bounds.
            var nb = RendererBoundsOf(go, far); var fb = RendererBounds(far);
            if (fb.size.y > 0.01f) far.transform.localScale = Vector3.one * (nb.size.y / fb.size.y);
            var nearR = new List<Renderer>();
            foreach (var r in go.GetComponentsInChildren<Renderer>()) if (!r.transform.IsChildOf(far.transform)) nearR.Add(r);
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.6f, nearR.ToArray()), new LOD(0.0f, far.GetComponentsInChildren<Renderer>()) });
            lod.RecalculateBounds();
        }

        /// <summary>Bounds of <paramref name="go"/>'s renderers, leaving out those under <paramref name="skip"/>.</summary>
        static Bounds RendererBoundsOf(GameObject go, GameObject skip)
        {
            bool any = false; var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r.transform.IsChildOf(skip.transform)) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

        /// <summary>
        /// URP Lit material from the model's maps next to it in Resources (id_albedo, id_ms with metallic
        /// in R and smoothness in A, id_normal). Null if the model ships no albedo map.
        /// </summary>
        public static Material Material(string category, string id)
        {
            string key = category + "/" + id;
            Material m;
            if (_mats.TryGetValue(key, out m)) return m;
            var albedo = Resources.Load<Texture2D>("Models/" + key + "_albedo");
            var template = Resources.Load<Material>("Models/ArtLit");
            if (albedo != null && template != null)
            {
                m = new Material(template) { name = id };
                m.SetTexture("_BaseMap", albedo);
                m.SetColor("_BaseColor", new Color(1.14f, 1.1f, 1.04f)); // AI albedo maps run a bit dark next to the sunlit world
                // AI metal/gloss maps vary patch by patch, so painted wood and iron show as a patchwork
                // of sky reflections. Only locomotives (real brass and steel) keep theirs.
                var ms = Resources.Load<Texture2D>("Models/" + key + "_ms");
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", category == "People" ? 0.15f : 0.22f); // even finish: varnished paint / matte
                if (ms != null)
                {
                    m.SetTexture("_MetallicGlossMap", ms);
                    m.SetFloat("_Smoothness", 0.6f);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                var n = Resources.Load<Texture2D>("Models/" + key + "_normal");
                if (n != null)
                {
                    m.SetTexture("_BumpMap", n);
                    m.SetFloat("_BumpScale", 1f);
                    m.EnableKeyword("_NORMALMAP");
                }
                m.enableInstancing = true;
            }
            _mats[key] = m;
            return m;
        }

        [System.Serializable] class ScanSlot { public string name; public bool alpha, normal, ms; public float smooth = -1; public float[] color; }
        [System.Serializable] class ScanInfo { public string kind; public ScanSlot[] slots; }
        static readonly Dictionary<string, Material[]> _scanMats = new Dictionary<string, Material[]>();
        static readonly Dictionary<string, Material> _slotMats = new Dictionary<string, Material>();

        /// <summary>The first material of a scanned model (grass blades for the grass field), or null.</summary>
        public static Material FirstMaterial(string scanId)
        {
            if (Prefab(Scan, scanId) == null) return null;
            var probe = Spawn(Scan, scanId, null);
            var r = probe.GetComponentInChildren<Renderer>();
            var m = r != null ? r.sharedMaterial : null;
            Object.Destroy(probe);
            return m;
        }

        /// <summary>The Dutch ship's sailcloth (for the castaway's lean-to), or null before the ship is imported.</summary>
        static Material _sail;
        public static Material SailMaterial
        {
            get
            {
                if (_sail != null) return _sail;
                if (Prefab(Scan, "dutch_ship_large_01") == null) return null;
                // Found once (the probe is a whole ship) and kept.
                var probe = Spawn(Scan, "dutch_ship_large_01", null);
                foreach (var r in probe.GetComponentsInChildren<Renderer>())
                    foreach (var m in r.sharedMaterials) if (m != null && m.name.Contains("sail")) _sail = m;
                Object.Destroy(probe);
                return _sail;
            }
        }

        /// <summary>Red Jack's ship: the same sailcloth dyed black.</summary>
        public static void BlackSails(GameObject ship)
        {
            if (ship == null) return;
            Material black = null;
            foreach (var r in ship.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.Contains("sail")) continue;
                    if (black == null)
                    {
                        black = new Material(mats[i]) { name = "Black sails" };
                        black.SetColor("_BaseColor", new Color(0.16f, 0.14f, 0.14f));
                        black.SetFloat("_Smoothness", 0.05f);
                    }
                    mats[i] = black; changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        /// <summary>URP Lit materials for a scanned model, one per submesh, from its manifest (Models/Scan/{id}.json).</summary>
        static void ScanMaterials(GameObject go, string id)
        {
            Material[] mats;
            if (!_scanMats.TryGetValue(id, out mats))
            {
                var json = Resources.Load<TextAsset>("Models/Scan/" + id);
                var info = json != null ? JsonUtility.FromJson<ScanInfo>(json.text) : null;
                var template = Resources.Load<Material>("Models/ArtLit");
                mats = new Material[info != null && info.slots != null ? info.slots.Length : 0];
                for (int i = 0; i < mats.Length; i++)
                {
                    var s = info.slots[i];
                    Material m;
                    if (!_slotMats.TryGetValue(s.name, out m))
                    {
                        m = new Material(template) { name = s.name, enableInstancing = true };
                        var alb = Resources.Load<Texture2D>("Models/Scan/tex/" + s.name + "_albedo");
                        m.SetTexture("_BaseMap", alb);
                        m.SetColor("_BaseColor", alb != null ? new Color(1.06f, 1.05f, 1.02f) : (s.color != null && s.color.Length == 3 ? new Color(s.color[0], s.color[1], s.color[2]) : Color.gray));
                        m.SetFloat("_Metallic", 0);
                        m.SetFloat("_Smoothness", s.smooth >= 0 ? s.smooth : 0.5f);
                        var ms = s.ms ? Resources.Load<Texture2D>("Models/Scan/tex/" + s.name + "_ms") : null;
                        if (ms != null) { m.SetTexture("_MetallicGlossMap", ms); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 0.85f); }
                        var n = s.normal ? Resources.Load<Texture2D>("Models/Scan/tex/" + s.name + "_normal") : null;
                        if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
                        if (s.alpha)
                        {
                            // Leaves and sails: cut out, both sides drawn.
                            m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", 0.45f); m.EnableKeyword("_ALPHATEST_ON");
                            m.SetFloat("_Cull", 0); m.doubleSidedGI = true;
                            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                            m.SetFloat("_Smoothness", Mathf.Min(m.GetFloat("_Smoothness"), 0.35f));
                        }
                        _slotMats[s.name] = m;
                    }
                    mats[i] = m;
                }
                _scanMats[id] = mats;
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (mats.Length == 0) continue;
                var arr = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = mats[Mathf.Min(i, mats.Length - 1)];
                r.sharedMaterials = arr;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        /// <summary>World-space bounds of all renderers under a model.</summary>
        public static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>Children whose names start with the prefix (e.g. "Wheel", "Smoke", "Spin").</summary>
        public static List<Transform> Anchors(GameObject go, string prefix)
        {
            var list = new List<Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>())
                if (t != go.transform && t.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) list.Add(t);
            return list;
        }
    }
}
