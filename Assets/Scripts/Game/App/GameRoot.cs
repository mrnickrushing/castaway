using System.Collections;
using System;
using System.IO;
using LastTide.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
#if LASTTIDE_URP
using UnityEngine.Rendering.Universal;
#endif

namespace LastTide.Game
{
    /// <summary>Global access to the running game.</summary>
    public static class G
    {
        public static Session Session;
    }

    /// <summary>JSON save in the app's data folder, written through a temp file with the previous save kept as .bak.</summary>
    public static class SaveSystem
    {
        public static string Folder;
        static string Main { get { return Path.Combine(Folder ?? Application.persistentDataPath, "lasttide_save.json"); } }

        public static GameState Load()
        {
            foreach (var path in new[] { Main, Main + ".tmp", Main + ".bak" })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    var s = JsonUtility.FromJson<GameState>(File.ReadAllText(path));
                    if (s != null && s.backpack != null && s.backpack.Count > 0) return s;
                }
                catch (Exception e) { Debug.LogWarning("Save load failed for " + path + ": " + e.Message); }
            }
            return null;
        }

        public static bool Save(GameState s)
        {
            string tmp = Main + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonUtility.ToJson(s));
                var check = JsonUtility.FromJson<GameState>(File.ReadAllText(tmp));
                if (check == null || check.backpack == null || check.backpack.Count != s.backpack.Count) throw new IOException("save did not read back");
                if (File.Exists(Main)) File.Copy(Main, Main + ".bak", true);
                if (File.Exists(Main)) File.Delete(Main);
                File.Move(tmp, Main);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Save failed: " + e.Message);
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
                return false;
            }
        }

        public static void Delete()
        {
            foreach (var p in new[] { Main, Main + ".bak", Main + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }
    }

    /// <summary>
    /// Builds the whole game at runtime in an empty scene: light, camera, the island and everything on it, the
    /// castaway, the boars and the HUD. Ticks the survival rules and autosaves.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }
        float _autosave;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            new GameObject("Last Tide").AddComponent<GameRoot>();
        }

        void Awake()
        {
            Instance = this;
            // Real time, whatever the project settings carry (the editor play bot runs the game at 3x).
            Time.timeScale = 1f;
            Application.targetFrameRate = 60;
            // LOD switch points are tuned for a bias of 1 (ModelRegistry.AddFarLod): trees near the castaway stay full detail.
            QualitySettings.lodBias = 1f;
            QualitySettings.vSyncCount = 0;
            Input.multiTouchEnabled = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(cam.gameObject);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);

            var state = SaveSystem.Load() ?? GameState.New();
            Session.UtcOffsetSeconds = (int)System.TimeZoneInfo.Local.GetUtcOffset(System.DateTime.Now).TotalSeconds;
            G.Session = new Session(state);
            G.Session.PetChanged += OnPetChanged;
            G.Session.UpdateEnergy();

            _loading = LoadingScreen.Show("Washing ashore...");
        }

        LoadingScreen _loading;
        bool _ready;

        /// <summary>Builds the island a step at a time behind the loading screen, so its bar moves.</summary>
        IEnumerator Start()
        {
            var state = G.Session.State;
            yield return null; // let the loading screen draw first
            Lighting();
            _loading.SetProgress(0.15f, "Raising the island...");
            yield return null;
            BuildWorld();
            _loading.SetProgress(0.7f, "Lighting the campfire...");
            yield return null;
            var player = Player.Create(transform);
            if (!state.placed)
            {
                state.placed = true;
                player.Place(Island.Start, 20);
            }
            else player.Place(new Vector3(state.px, 0, state.pz), state.facing);
            CameraRig.Create(player.transform);
            // Each step is guarded: one that throws on a device (a shader missing from the build, say) is logged and
            // the rest still run, so the loading screen always gets out of the way instead of hanging here.
            Guard("pet", () => { if (G.Session.HasPet) SpawnPet(player.transform.position - player.transform.forward * 2); });
            Guard("input", () =>
            {
                if (FindFirstObjectByType<EventSystem>() == null)
                    new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            });
            Guard("map", () => MapView.Create());
            Guard("hud", () => Hud.Create());
            Guard("arrow", () => GuideArrow.Create(transform));
            Guard("sound", () => { Sfx.Init(transform); Sfx.Muted = state.muted; });
            Guard("store", Store.Reconcile);
            Guard("quests", G.Session.CheckQuests);
            // Closed the app mid-journey: open on the map to finish it.
            Guard("journey", () => { if (state.travel != null && Hud.Instance != null) Hud.Instance.OpenMap(); });
            _ready = true;
            yield return _loading.Finish();
        }

        /// <summary>Runs one start-up step, logging (not rethrowing) what it throws.</summary>
        static void Guard(string step, System.Action act)
        {
            try { act(); }
            catch (System.Exception e) { Debug.LogError("[Start] " + step + " failed: " + e); }
        }

        Transform _world;

        /// <summary>Builds the island the castaway is on (home or a location), replacing the one before.</summary>
        void BuildWorld()
        {
            if (_world != null) { _world.gameObject.SetActive(false); Destroy(_world.gameObject); }
            Spot.All.Clear();
            Enemy.All.Clear();
            Obstacles.Clear();
            Island.Configure(G.Session.Here);
            _world = new GameObject("World " + G.Session.Here.Id).transform;
            Island.BuildTerrain(_world);
            Island.BuildSea(_world);
            WorldBuilder.Populate(_world);
        }

        public void SpawnPet(Vector3 at)
        {
            var want = G.Session.ActivePet;
            if (Pet.Instance != null)
            {
                if (want != null && Pet.Instance.Model == want.Model) return;
                at = Pet.Instance.transform.position;
                Destroy(Pet.Instance.gameObject);
            }
            if (want == null) return;
            Pet.Create(transform, want.Model).Place(at);
        }

        /// <summary>A new companion (season reward, or picked in settings): swap the one at heel.</summary>
        void OnPetChanged()
        {
            if (OnMap || Player.Instance == null) return;
            var p = Player.Instance.transform;
            SpawnPet(p.position - p.forward * 2);
        }

        /// <summary>True while the map is up (the island is hidden and survival pauses).</summary>
        public bool OnMap { get; private set; }

        public void ShowMap(bool on)
        {
            OnMap = on;
            if (on && G.Session.AtHome) { G.Session.State.homeX = Player.Instance.transform.position.x; G.Session.State.homeZ = Player.Instance.transform.position.z; }
            if (_world != null) _world.gameObject.SetActive(!on);
            Player.Instance.gameObject.SetActive(!on);
            if (Pet.Instance != null) Pet.Instance.gameObject.SetActive(!on);
            CameraRig.Instance.gameObject.SetActive(!on);
            RenderSettings.fog = !on;
            if (on) MapView.Instance.Show(G.Session.State.travel != null ? LastTide.Core.World.Location(G.Session.State.travel.to) : G.Session.Here);
            else MapView.Instance.Hide();
            Save();
        }

        /// <summary>Steps ashore at the end of a journey: builds that island and puts the castaway on its landing.</summary>
        public void EnterArrived()
        {
            if (G.Session.State.travel == null || _arriving) return;
            StartCoroutine(Arriving());
        }

        bool _arriving;

        IEnumerator Arriving()
        {
            _arriving = true;
            var ls = LoadingScreen.Show("Making landfall...", 1.8f);
            yield return null;
            var to = G.Session.Arrive();
            if (to == null) { _arriving = false; yield return ls.Finish(); yield break; }
            ls.SetProgress(0.3f, "Making landfall at " + to.Name + "...");
            yield return null;
            BuildWorld();
            ShowMap(false);
            var p = to.Home ? Island.Start + new Vector3(-9, 0, 0) : Island.Start;
            Player.Instance.Place(p, 0);
            G.Session.State.px = p.x; G.Session.State.pz = p.z;
            if (Pet.Instance != null) Pet.Instance.Place(p + new Vector3(1.5f, 0, -1.5f));
            Hud.Instance.Toast(to.Name);
            Save();
            // Destroying the old island leaves the meshes and materials it made at runtime (rings, stumps, terrain)
            // alive: once it is gone at the end of this frame, unload them behind the loading screen (~80 MB a lap
            // of the archipelago otherwise).
            yield return null;
            yield return Resources.UnloadUnusedAssets();
            _arriving = false;
            yield return ls.Finish(to.Name);
        }

        void Lighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.84f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(52, -35, 0);
            sun.transform.SetParent(transform, false);
            RenderSettings.sun = sun;
            DayNight.Create(sun);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.62f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.32f, 0.26f);
            // Reflections of a real sky (Poly Haven HDRI) on the sea, wet rock, glass and metal.
            var sky = Resources.Load<Cubemap>("Sky/sky_day");
            if (sky != null)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = sky;
                RenderSettings.reflectionIntensity = 0.85f;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.8f, 0.86f);
            RenderSettings.fogStartDistance = 60;
            RenderSettings.fogEndDistance = 220;
            Shader.SetGlobalFloat("_NightGlow", 0);
#if LASTTIDE_URP
            // A touch of filmic grading: tonemapping, a little bloom on the sea glints, warmer and richer colour.
            var vol = new GameObject("Grading").AddComponent<Volume>();
            vol.transform.SetParent(transform, false);
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.45f);
            bloom.threshold.Override(1.0f);
            bloom.scatter.Override(0.65f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(18f);
            color.contrast.Override(14f);
            color.postExposure.Override(0.45f);
            color.colorFilter.Override(new Color(1f, 0.98f, 0.95f));
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(2f);
            var lgg = profile.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(0.98f, 1f, 1.03f, 0f));       // cooler shadows (no darkening: it would crush the night)
            lgg.gain.Override(new Vector4(1.02f, 1f, 0.98f, 0.02f));    // a little warmth in the highlights
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.24f);
            vig.smoothness.Override(0.45f);
            vol.profile = profile;
#endif
        }

        void Update()
        {
            if (!_ready) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if ((!Hud.Instance || !Hud.Instance.Blocking) && !OnMap) G.Session.Tick(dt);
            else G.Session.UpdateEnergy();
            _autosave -= Time.unscaledDeltaTime;
            if (_autosave <= 0) { _autosave = 15f; Save(); }
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F9)) { SaveSystem.Delete(); Debug.Log("[LastTide] save deleted"); }
#endif
        }

        public void Save() { SaveSystem.Save(G.Session.State); }

        /// <summary>After passing out: wake at the shelter if there is one, else back on the beach.</summary>
        public void Respawn()
        {
            var shelter = G.Session.AtHome && G.Session.IsBuilt("shelter");
            var p = shelter ? WorldBuilder.SitePosition("shelter") + new Vector3(0, 0, -2.6f) : Island.Start;
            var bed = G.Session.AtHome ? G.Session.Bed : null;
            if (bed != null) { Quaternion r; CampGrid.Pose(bed, out p, out r); p += r * new Vector3(1.6f, 0, 0); }
            Player.Instance.Place(p, 0);
            Save();
            if (G.Session.NewestBag != null)
                Hud.Instance.Confirm("Recover your backpack?", "Everything you carried lies where you fell. Get it back right now for " + Session.RecoverBagCost + " doubloons, or walk back for it.", "RECOVER " + Session.RecoverBagCost,
                    () => { if (G.Session.RecoverBag()) Sfx.Play("coin"); });
        }

        /// <summary>Starts again from a fresh save: tears the game down and boots it anew next frame.</summary>
        public void Restart()
        {
            var boot = new GameObject("Restart").AddComponent<Restarter>();
            if (Hud.Instance != null) Destroy(Hud.Instance.gameObject);
            if (MapView.Instance != null) Destroy(MapView.Instance.gameObject);
            if (CameraRig.Instance != null) Destroy(CameraRig.Instance.gameObject);
            if (_world != null) Destroy(_world.gameObject);
            Instance = null;
            Destroy(gameObject);
        }

        sealed class Restarter : MonoBehaviour
        {
            int _frames;
            void Update() { if (++_frames < 2) return; new GameObject("Last Tide").AddComponent<GameRoot>(); Destroy(gameObject); }
        }

        void OnApplicationPause(bool paused) { if (paused && G.Session != null) Save(); }
        void OnApplicationQuit() { if (G.Session != null) Save(); }
    }
}
