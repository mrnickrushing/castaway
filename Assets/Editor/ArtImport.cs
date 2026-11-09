using UnityEditor;
using UnityEngine;

namespace LastTide.EditorTools
{
    /// <summary>
    /// Import rules for the packed CC0 texture atlases built by Tools/art/pack_textures.py: each one
    /// becomes a Texture2DArray (one layer per flipbook cell), compressed per platform.
    /// </summary>
    public sealed class ArtImport : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/Textures/";

        const string Models = "Assets/Resources/Models/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Models)) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; // ArtMaterials builds URP materials at runtime
            if (assetPath.StartsWith(Models + "Cast/"))
            {
                // Realistic people (Tools/art/sketchfab.py): Humanoid, so the shared clips in moves.fbx retarget onto
                // every one of them. Bones stay as transforms: the castaway's work poses aim them in LateUpdate.
                bool moves = assetPath.EndsWith("/moves.fbx");
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.optimizeGameObjects = false;
                mi.importAnimation = moves;
                mi.importCameras = false;
                mi.importLights = false;
                mi.isReadable = false;
                mi.meshCompression = ModelImporterMeshCompression.Medium;
                mi.importNormals = ModelImporterNormals.Import;
                mi.importTangents = ModelImporterTangents.CalculateMikk;
                mi.skinWeights = ModelImporterSkinWeights.Standard;
                if (moves) { mi.animationCompression = ModelImporterAnimationCompression.Optimal; mi.resampleCurves = true; }
                return;
            }
            if (assetPath.StartsWith(Models + "Creatures/"))
            {
                // Realistic animals (Tools/art/sketchfab.py creature): their own Generic rig and clips; bones stay
                // transforms (the snake's spine is bent in code).
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.optimizeGameObjects = false;
                mi.importAnimation = true;
                mi.importCameras = false;
                mi.importLights = false;
                mi.isReadable = false;
                mi.meshCompression = ModelImporterMeshCompression.Medium;
                mi.importNormals = ModelImporterNormals.Import;
                mi.importTangents = ModelImporterTangents.CalculateMikk;
                mi.skinWeights = ModelImporterSkinWeights.Standard;
                mi.animationCompression = ModelImporterAnimationCompression.Optimal;
                mi.resampleCurves = true;
                return;
            }
            bool person = assetPath.StartsWith(Models + "People/") || assetPath.StartsWith(Models + "Animals/"); // rigged, Legacy clips
            mi.importAnimation = person;
            mi.importCameras = false;
            mi.importLights = false;
            mi.animationType = person ? ModelImporterAnimationType.Legacy : ModelImporterAnimationType.None;
            if (person)
            {
                mi.animationWrapMode = WrapMode.Loop;
                mi.resampleCurves = true;
            }
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
        }

        /// <summary>The shared humanoid clips: loops loop, everything stays in place (the game moves the body).</summary>
        void OnPreprocessAnimation()
        {
            var mi = (ModelImporter)assetImporter;
            if (assetPath.StartsWith(Models + "Creatures/"))
            {
                // An animal's clips are cycles played in place.
                var cc = mi.defaultClipAnimations;
                for (int i = 0; i < cc.Length; i++) { cc[i].name = cc[i].name.Substring(cc[i].name.LastIndexOf('|') + 1); cc[i].loopTime = true; }
                mi.clipAnimations = cc;
                return;
            }
            if (!assetPath.StartsWith(Models + "Cast/")) return;
            var clips = mi.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                string n = clips[i].name.Substring(clips[i].name.LastIndexOf('|') + 1);
                clips[i].name = n;
                clips[i].loopTime = n.EndsWith("_Loop") || n == "Sword_Idle" || n == "Fixing_Kneeling";
                clips[i].lockRootRotation = true; clips[i].keepOriginalOrientation = false; // face the body forward (+Z)
                clips[i].lockRootHeightY = true; clips[i].keepOriginalPositionY = true;
                clips[i].lockRootPositionXZ = true; clips[i].keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }

        /// <summary>Recorded game audio (Tools/art/audio.py): effects decompressed for instant playback, beds and music streamed.</summary>
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            string n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool music = n == "music", bed = n.StartsWith("amb_");
            var s = ai.defaultSampleSettings;
            s.loadType = music ? AudioClipLoadType.Streaming : bed ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.55f : 0.7f;
            s.preloadAudioData = !music;
            ai.defaultSampleSettings = s;
            ai.forceToMono = !music;
            ai.loadInBackground = music;
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/Resources/Sky/"))
            {
                // HDRI skies (Poly Haven, CC0): a cubemap for reflections.
                var c = (TextureImporter)assetImporter;
                c.textureShape = TextureImporterShape.TextureCube;
                c.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
                c.sRGBTexture = false;
                c.mipmapEnabled = true;
                c.maxTextureSize = 512;
                c.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }
            if (assetPath.StartsWith(Models))
            {
                var t = (TextureImporter)assetImporter;
                bool normal = assetPath.EndsWith("_normal.jpg") || assetPath.EndsWith("_normal.png"), data = assetPath.EndsWith("_ms.png");
                t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if ((assetPath.Contains("/Scan/") || assetPath.Contains("/Cast/")) && assetPath.EndsWith("_albedo.png"))
                {
                    // Leaf cutouts: keep their coverage in the small mips so distant plants don't thin out.
                    t.alphaSource = TextureImporterAlphaSource.FromInput;
                    t.alphaIsTransparency = true;
                    t.mipMapsPreserveCoverage = true;
                    t.alphaTestReferenceValue = 0.45f;
                }
                t.sRGBTexture = !normal && !data;
                t.mipmapEnabled = true;
                t.anisoLevel = 4;
                t.maxTextureSize = 2048;
                t.textureCompression = TextureImporterCompression.CompressedHQ;
                t.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "iPhone", overridden = true, maxTextureSize = 2048,
                    format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50,
                });
                return;
            }
            if (!assetPath.StartsWith(Folder)) return;
            var ti = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            int cols, rows;
            switch (file)
            {
                case "TerrainAlbedo": cols = 3; rows = 2; ti.sRGBTexture = true; break;
                case "TerrainNormal": cols = 3; rows = 2; ti.sRGBTexture = false; break;
                case "SurfaceDetail": cols = 4; rows = 4; ti.sRGBTexture = false; break;
                default: return;
            }
            ti.textureType = TextureImporterType.Default;
            ti.textureShape = TextureImporterShape.Texture2DArray;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.flipbookColumns = cols;
            settings.flipbookRows = rows;
            ti.SetTextureSettings(settings);
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 4;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "iPhone", overridden = true, maxTextureSize = 4096,
                format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50,
            });
        }
    }
}
