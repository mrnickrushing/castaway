using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastTide.EditorTools
{
    /// <summary>
    /// One-time project configuration so a fresh clone opens ready to play and build: creates the (empty) main
    /// scene, adds it to Build Settings, and applies the iOS player settings. Runs once on editor load.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string BundleId = "com.rushingtech.lasttide";
        public const string ProductName = "Last Tide";
        /// <summary>CFBundleShortVersionString; must match the App Store Connect version.</summary>
        public const string AppVersion = "1.0.1";
        const string DoneKey = "LastTide.ProjectSetup.v1";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode) return;
                if (File.Exists(ScenePath) && EditorPrefs.GetBool(DoneKey + Application.dataPath, false)) return;
                Run();
                EditorPrefs.SetBool(DoneKey + Application.dataPath, true);
            };
        }

        [MenuItem("LastTide/Set Up Project")]
        public static void Run()
        {
            EnsureScene();
            ApplyPlayerSettings();
#if LASTTIDE_URP
            UrpSetup.Run();
#endif
            AppIcon.Generate();
            IncludeShaders();
            AssetDatabase.SaveAssets();
            Debug.Log("[LastTide] Project set up. Open " + ScenePath + " and press Play.");
        }

        /// <summary>Shaders the code finds by name at runtime (Shader.Find) but no asset references. A build only
        /// carries referenced shaders, so without this Shader.Find returns null on device (the map's parchment).</summary>
        public static readonly string[] RuntimeShaders = { "Universal Render Pipeline/Unlit" };

        /// <summary>Adds <see cref="RuntimeShaders"/> to Graphics settings → Always Included Shaders.</summary>
        public static void IncludeShaders()
        {
            var gs = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            bool changed = false;
            foreach (var name in RuntimeShaders)
            {
                var sh = Shader.Find(name);
                if (sh == null) { Debug.LogError("[LastTide] Shader not found: " + name); continue; }
                bool has = false;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) has = true;
                if (has) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
                changed = true;
            }
            if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets(); }
        }

        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Rushing Technologies";
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.bundleVersion = AppVersion;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.05f, 0.3f, 0.38f);
            // iOS launch screen: the key art, so launch flows into the in-game title splash.
            var launch = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/UI/LoadingArt.jpg");
            if (launch != null)
            {
                PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
                PlayerSettings.iOS.SetiPadLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
                PlayerSettings.iOS.SetLaunchScreenImage(launch, iOSLaunchScreenImageType.iPhoneLandscapeImage);
                PlayerSettings.iOS.SetLaunchScreenImage(launch, iOSLaunchScreenImageType.iPadImage);
            }
        }
    }

    /// <summary>
    /// The app icon, Assets/Art/AppIcon.png: Red Jack's black-sailed ship at sunset, a square crop of the ending still
    /// (art-src/media/ending.png, made by Tools/art/ai_media.py). If the file is missing it paints a plain placeholder.
    /// </summary>
    public static class AppIcon
    {
        public const string Path_ = "Assets/Art/AppIcon.png";

        public static void Generate()
        {
            if (!File.Exists(Path_))
            {
                const int n = 1024;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = x / (float)n, v = y / (float)n;
                        Color c = Color.Lerp(new Color(1f, 0.55f, 0.3f), new Color(0.35f, 0.25f, 0.5f), v);
                        if (Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.42f)) < 0.17f) c = new Color(1f, 0.85f, 0.45f);
                        if (v < 0.42f) c = Color.Lerp(new Color(0.03f, 0.25f, 0.38f), new Color(0.15f, 0.55f, 0.6f), v / 0.42f);
                        if (v < 0.3f && Mathf.Abs(u - 0.5f) < 0.42f - v * 0.6f && v > 0.18f) c = new Color(0.93f, 0.8f, 0.55f);
                        bool mast = Mathf.Abs(u - (0.62f - (v - 0.25f) * 0.25f)) < 0.018f && v > 0.25f && v < 0.75f;
                        bool yard = Mathf.Abs(v - 0.62f) < 0.012f && u > 0.48f && u < 0.72f;
                        if (mast || yard) c = new Color(0.15f, 0.1f, 0.08f);
                        px[y * n + x] = c;
                    }
                tex.SetPixels32(px);
                Directory.CreateDirectory(Path.GetDirectoryName(Path_));
                File.WriteAllBytes(Path_, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(Path_);
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path_);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }
    }
}
