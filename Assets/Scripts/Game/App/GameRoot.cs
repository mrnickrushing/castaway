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
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Input.multiTouchEnabled = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(cam.gameObject);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);

            var state = SaveSystem.Load() ?? GameState.New();
            G.Session = new Session(state);
            G.Session.UpdateEnergy();

            Lighting();
            BuildWorld();
            var player = Player.Create(transform);
            if (!state.placed)
            {
                state.placed = true;
                player.Place(Island.Start, 20);
            }
            else player.Place(new Vector3(state.px, 0, state.pz), state.facing);
            CameraRig.Create(player.transform);
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            MapView.Create();
            Hud.Create();
            GuideArrow.Create(transform);
            Sfx.Init(transform);
            Sfx.Muted = state.muted;
            Store.Reconcile();
            G.Session.CheckQuests();
            // Closed the app mid-journey: open on the map to finish it.
            if (state.travel != null) Hud.Instance.OpenMap();
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

        /// <summary>True while the map is up (the island is hidden and survival pauses).</summary>
        public bool OnMap { get; private set; }

        public void ShowMap(bool on)
        {
            OnMap = on;
            if (on && G.Session.AtHome) { G.Session.State.homeX = Player.Instance.transform.position.x; G.Session.State.homeZ = Player.Instance.transform.position.z; }
            if (_world != null) _world.gameObject.SetActive(!on);
            Player.Instance.gameObject.SetActive(!on);
            CameraRig.Instance.gameObject.SetActive(!on);
            RenderSettings.fog = !on;
            if (on) MapView.Instance.Show(G.Session.State.travel != null ? LastTide.Core.World.Location(G.Session.State.travel.to) : G.Session.Here);
            else MapView.Instance.Hide();
            Save();
        }

        /// <summary>Steps ashore at the end of a journey: builds that island and puts the castaway on its landing.</summary>
        public void EnterArrived()
        {
            var to = G.Session.Arrive();
            if (to == null) return;
            BuildWorld();
            ShowMap(false);
            var p = to.Home ? Island.Start + new Vector3(-9, 0, 0) : Island.Start;
            Player.Instance.Place(p, 0);
            G.Session.State.px = p.x; G.Session.State.pz = p.z;
            Hud.Instance.Toast(to.Name);
            Save();
        }

        void Lighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.84f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(52, -35, 0);
            sun.transform.SetParent(transform, false);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.62f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.32f, 0.26f);
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
            tone.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.05f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(14f);
            color.postExposure.Override(0.15f);
            color.colorFilter.Override(new Color(1f, 0.98f, 0.94f));
            vol.profile = profile;
#endif
        }

        void Update()
        {
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
            Player.Instance.Place(p, 0);
            Save();
            if (G.Session.State.lostBag != null)
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
