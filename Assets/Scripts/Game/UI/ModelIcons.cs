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
        const int Size = 192, Layer = 31;
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
                _light.intensity = 0.6f;
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
            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            _cam.targetTexture = rt;
            _cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            _cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(go);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100);
        }

        /// <summary>A small model of an item that has no painted icon.</summary>
        static GameObject ItemModel(string id)
        {
            string kit = id == "iron_hatchet" ? "tool_axe" : id == "iron_pickaxe" ? "tool_pickaxe" : null;
            if (kit != null && ModelRegistry.Has(ModelRegistry.Free, kit))
            {
                var holder = new GameObject(id);
                var t = ModelRegistry.Spawn(ModelRegistry.Free, kit, holder.transform);
                t.transform.localRotation = Quaternion.Euler(0, 0, 45) * t.transform.localRotation;
                return holder;
            }
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
