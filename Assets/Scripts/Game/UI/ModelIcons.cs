using System.Collections.Generic;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// Icons made by photographing models at runtime: the building pieces for build mode, and items that have no
    /// painted icon (clothes, iron goods, stew). An off-screen camera frames the model from the usual three-quarter
    /// view against a transparent background; the result is cached as a sprite.
    /// </summary>
    public static class ModelIcons
    {
        const int Size = 256, Layer = 31;
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        static Camera _cam;
        static Light _light;

        public static Sprite Item(string id)
        {
            Sprite s;
            if (_cache.TryGetValue("item_" + id, out s)) return s;
            var go = ItemModel(id);
            s = go != null ? Shoot(go, 20) : null;
            return _cache["item_" + id] = s;
        }

        public static Sprite Piece(string id)
        {
            Sprite s;
            if (_cache.TryGetValue("piece_" + id, out s)) return s;
            var go = PieceModels.Create(id, null, false);
            s = go != null ? Shoot(go, 28) : null;
            return _cache["piece_" + id] = s;
        }

        static Sprite Shoot(GameObject go, float pitch)
        {
            if (_cam == null)
            {
                var c = new GameObject("Icon camera");
                Object.DontDestroyOnLoad(c);
                _cam = c.AddComponent<Camera>();
                _cam.enabled = false;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0, 0, 0, 0);
                _cam.cullingMask = 1 << Layer;
                _cam.orthographic = true;
                _cam.nearClipPlane = 0.05f; _cam.farClipPlane = 100;
                var l = new GameObject("Icon light");
                Object.DontDestroyOnLoad(l);
                _light = l.AddComponent<Light>();
                _light.type = LightType.Directional;
                _light.cullingMask = 1 << Layer;
                _light.intensity = 0.95f;
                _light.transform.rotation = Quaternion.Euler(40, 150, 0);
            }
            var at = new Vector3(0, -500, 0);
            go.transform.position = at;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            var b = ModelRegistry.RendererBounds(go);
            float r = Mathf.Max(b.extents.x, b.extents.y, b.extents.z) * 1.15f;
            var rot = Quaternion.Euler(pitch, -35, 0);
            _cam.transform.rotation = rot;
            _cam.transform.position = b.center - rot * Vector3.forward * (r * 4);
            _cam.orthographicSize = r;
            _cam.farClipPlane = r * 8 + 10;
            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            _cam.targetTexture = rt;
            // Always photographed by day: the island's sun, moon and night ambient would otherwise make an icon first
            // drawn after dark come out nearly black, and it stays that way for the session.
            var lights = new List<Light>();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l != _light && l.enabled) { l.enabled = false; lights.Add(l); }
            Color sky = RenderSettings.ambientSkyColor, eq = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor;
            bool fog = RenderSettings.fog;
            float glow = Shader.GetGlobalFloat("_NightGlow");
            RenderSettings.ambientSkyColor = DayNight.SkyDay; RenderSettings.ambientEquatorColor = DayNight.EqDay; RenderSettings.ambientGroundColor = DayNight.GroundDay;
            RenderSettings.fog = false;
            Shader.SetGlobalFloat("_NightGlow", 0);
            _cam.Render();
            RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = ground;
            RenderSettings.fog = fog;
            Shader.SetGlobalFloat("_NightGlow", glow);
            foreach (var l in lights) l.enabled = true;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            _cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(go);
            return Sprite.Create(Trim(tex), new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100);
        }

        /// <summary>
        /// Crops a render to what was drawn and scales it back up to the full square (a little margin kept), so a model
        /// fills its tile the way the painted icons do instead of sitting small in the middle of its bounding sphere.
        /// </summary>
        static Texture2D Trim(Texture2D src)
        {
            src.wrapMode = TextureWrapMode.Clamp;
            var px = src.GetPixels32();
            int n = src.width, x0 = n, y0 = n, x1 = -1, y1 = -1;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (px[y * n + x].a > 8) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            if (x1 < 0) return src;
            float side = Mathf.Max(x1 - x0 + 1, y1 - y0 + 1) * 1.08f;
            if (side >= n * 0.97f) return src;                       // already fills it
            float cx = (x0 + x1 + 1) / 2f, cy = (y0 + y1 + 1) / 2f;
            var dst = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var outPx = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float sx = cx + ((x + 0.5f) / n - 0.5f) * side, sy = cy + ((y + 0.5f) / n - 0.5f) * side;
                    outPx[y * n + x] = sx < 0 || sy < 0 || sx > n || sy > n ? Color.clear : src.GetPixelBilinear(sx / n, sy / n);
                }
            dst.SetPixels(outPx);
            dst.Apply();
            Object.Destroy(src);
            return dst;
        }

        /// <summary>A small model of an item that has no painted icon.</summary>
        static GameObject ItemModel(string id)
        {
            string kit = id == "iron_hatchet" ? "tool_axe" : id == "iron_pickaxe" ? "tool_pickaxe" : id == "supply_crate" ? "crate" : id == "gold_chest" ? "chest_coins" : null;
            if (id.StartsWith("pet_"))
            {
                // Companions: the animal itself, standing side-on.
                var pet = LastTide.Core.Season.Pet(id.Substring(4));
                if (pet == null || !ModelRegistry.Has(ModelRegistry.People, pet.Model)) return null;
                var holder = new GameObject(id);
                var t = ModelRegistry.Spawn(ModelRegistry.People, pet.Model, holder.transform);
                t.transform.localRotation = Quaternion.Euler(0, 95, 0) * t.transform.localRotation;
                // A skinned mesh isn't skinned yet when the icon camera renders: bake it into a plain mesh.
                foreach (var smr in holder.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var baked = new Mesh();
                    smr.BakeMesh(baked);
                    var bk = new GameObject("Baked");
                    bk.transform.SetParent(smr.transform, false);
                    bk.AddComponent<MeshFilter>().sharedMesh = baked;
                    bk.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                    Object.DestroyImmediate(smr);
                }
                foreach (var an in holder.GetComponentsInChildren<Animation>()) an.enabled = false;
                return holder;
            }
            if (kit != null && ModelRegistry.Has(ModelRegistry.Free, kit))
            {
                var holder = new GameObject(id);
                var t = ModelRegistry.Spawn(ModelRegistry.Free, kit, holder.transform);
                if (id.StartsWith("iron_")) t.transform.localRotation = Quaternion.Euler(0, 0, 45) * t.transform.localRotation;
                return holder;
            }
            var scanned = IslandModels.IconArt(id);   // free scans where there are some
            if (scanned != null) return scanned;
            var isl = IslandModels.Icon(id);
            if (isl != null) { var ig = isl.ToObject(id, null); ig.GetComponent<MeshRenderer>().sharedMaterial = Materials.Kit; return ig; }
            var mb = new MeshBuilder();
            Color hide = Palette.Hex(0x8C5A3A), dark = Palette.Hex(0x5C3A21), fibre = Palette.Hex(0xB8A060);
            switch (id)
            {
                case "iron_bar":
                    mb.Mat(0.5f, 0.8f);
                    for (int i = 0; i < 3; i++) mb.Box(new Vector3((i - 1) * 0.36f, 0.1f + (i == 1 ? 0.22f : 0), 0), new Vector3(0.32f, 0.2f, 0.9f), Palette.Steel);
                    break;
                case "palm_hat":
                    mb.Cylinder(Vector3.zero, new Vector3(0, 0.06f, 0), 0.7f, 0.65f, 14, fibre);
                    mb.Cylinder(new Vector3(0, 0.06f, 0), new Vector3(0, 0.42f, 0), 0.36f, 0.24f, 12, MeshBuilder.Darken(fibre, 0.9f));
                    mb.Cylinder(new Vector3(0, 0.1f, 0), new Vector3(0, 0.16f, 0), 0.37f, 0.37f, 12, Tropic.FrondDark);
                    break;
                case "tricorn":
                    mb.Cylinder(Vector3.zero, new Vector3(0, 0.08f, 0), 0.75f, 0.7f, 3, Palette.Hex(0x1A1A1A));
                    mb.Cylinder(new Vector3(0, 0.08f, 0), new Vector3(0, 0.46f, 0), 0.36f, 0.3f, 10, Palette.Hex(0x222222));
                    mb.Cylinder(new Vector3(0, 0.1f, 0), new Vector3(0, 0.15f, 0), 0.37f, 0.37f, 10, Palette.Brass);
                    break;
                case "hide_vest":
                case "pirate_coat":
                {
                    var c = id == "pirate_coat" ? Palette.Hex(0x8C1F1F) : hide;
                    mb.Box(new Vector3(-0.22f, 0.5f, 0), new Vector3(0.36f, 0.95f, 0.22f), c);
                    mb.Box(new Vector3(0.22f, 0.5f, 0), new Vector3(0.36f, 0.95f, 0.22f), MeshBuilder.Darken(c, 0.92f));
                    mb.Box(new Vector3(-0.52f, 0.78f, 0), new Vector3(0.26f, 0.36f, 0.2f), c);
                    mb.Box(new Vector3(0.52f, 0.78f, 0), new Vector3(0.26f, 0.36f, 0.2f), c);
                    if (id == "pirate_coat")
                    {
                        mb.Box(new Vector3(0, 0.0f, 0), new Vector3(0.8f, 0.35f, 0.2f), MeshBuilder.Darken(c, 0.8f));
                        for (int i = 0; i < 4; i++) mb.Box(new Vector3(0.06f, 0.3f + i * 0.17f, 0.12f), new Vector3(0.06f, 0.06f, 0.04f), Palette.Brass);
                    }
                    else for (int i = 0; i < 4; i++) mb.Box(new Vector3(0, 0.3f + i * 0.18f, 0.12f), new Vector3(0.12f, 0.03f, 0.03f), fibre);
                    break;
                }
                case "hide_pants":
                    mb.Box(new Vector3(0, 0.85f, 0), new Vector3(0.7f, 0.22f, 0.26f), dark);
                    mb.Box(new Vector3(-0.19f, 0.4f, 0), new Vector3(0.3f, 0.8f, 0.24f), hide);
                    mb.Box(new Vector3(0.19f, 0.4f, 0), new Vector3(0.3f, 0.8f, 0.24f), MeshBuilder.Darken(hide, 0.92f));
                    break;
                case "hide_boots":
                    foreach (float x in new[] { -0.25f, 0.25f })
                    {
                        mb.Box(new Vector3(x, 0.3f, 0), new Vector3(0.26f, 0.6f, 0.28f), hide);
                        mb.Box(new Vector3(x, 0.08f, 0.16f), new Vector3(0.27f, 0.16f, 0.6f), dark);
                    }
                    break;
                case "plank":
                    for (int i = 0; i < 3; i++)
                        for (int j = 0; j < 2 - (i == 2 ? 1 : 0); j++)
                            mb.Box(new Vector3((j - (i == 2 ? 0 : 0.5f)) * 0.42f, 0.09f + i * 0.17f, 0), new Vector3(0.4f, 0.15f, 1.5f), MeshBuilder.Darken(Palette.WoodPale, 1 - 0.06f * ((i + j) % 2)));
                    break;
                case "stew":
                    mb.Cylinder(Vector3.zero, new Vector3(0, 0.55f, 0), 0.5f, 0.6f, 14, Tropic.Iron);
                    mb.Cylinder(new Vector3(0, 0.5f, 0), new Vector3(0, 0.52f, 0), 0.56f, 0.56f, 14, Palette.Hex(0x9A5A2A));
                    mb.Sphere(new Vector3(0.15f, 0.55f, 0.1f), Vector3.one * 0.12f, 6, Palette.Hex(0x7A3A1A));
                    mb.Sphere(new Vector3(-0.2f, 0.55f, -0.1f), Vector3.one * 0.07f, 5, Tropic.Berry);
                    break;
                default: return null;
            }
            var go = mb.ToObject(id, null);
            go.GetComponent<MeshRenderer>().sharedMaterial = Materials.Kit;
            return go;
        }
    }
}
