using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// Light through the day: warm morning, white noon, a golden dusk, then a deep blue moonlit night where fires,
    /// torches and the furnace glow; dawn breaks pink. Drives the sun (the moon at night), ambient light, fog, the
    /// clear colour and the world shader's night glow from <see cref="LastTide.Core.Session.DayPhase"/>.
    /// </summary>
    public sealed class DayNight : MonoBehaviour
    {
        public static DayNight Instance { get; private set; }
        Light _sun;
        public float Night { get; private set; }   // 0 day .. 1 full night

        static readonly Color SunDay = new Color(1f, 0.95f, 0.84f), SunGold = new Color(1f, 0.68f, 0.4f), Moon = new Color(0.55f, 0.65f, 1f);
        static readonly Color SkyDay = new Color(0.5f, 0.62f, 0.78f), SkyNight = new Color(0.2f, 0.28f, 0.55f);
        static readonly Color EqDay = new Color(0.45f, 0.5f, 0.44f), EqNight = new Color(0.12f, 0.17f, 0.34f);
        static readonly Color GroundDay = new Color(0.36f, 0.32f, 0.26f), GroundNight = new Color(0.04f, 0.05f, 0.1f);
        static readonly Color FogDay = new Color(0.62f, 0.8f, 0.86f), FogDusk = new Color(0.86f, 0.6f, 0.5f), FogNight = new Color(0.05f, 0.08f, 0.16f);

        UnityEngine.Rendering.Volume _grade;

        public static DayNight Create(Light sun)
        {
            var d = sun.gameObject.AddComponent<DayNight>();
            d._sun = sun;
#if LASTTIDE_URP
            // Night grading: darker, bluer, a little desaturated, blended in by the night's weight.
            var go = new GameObject("Night grading");
            go.transform.SetParent(sun.transform.parent, false);
            d._grade = go.AddComponent<UnityEngine.Rendering.Volume>();
            d._grade.isGlobal = true;
            d._grade.priority = 1;
            var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            var ca = prof.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            // A light touch: the moon and sky already make the night blue. A strong blue filter here would turn the
            // campfire and torches grey, and firelight against the dark is the look we want.
            ca.postExposure.Override(-0.1f);
            ca.saturation.Override(-8f);
            ca.colorFilter.Override(new Color(0.86f, 0.9f, 1f));
            d._grade.profile = prof;
            d._grade.weight = 0;
#endif
            Instance = d;
            return d;
        }

        void Update()
        {
            var s = G.Session;
            if (s == null) return;
            // The map is always drawn by day.
            float p = GameRoot.Instance != null && GameRoot.Instance.OnMap ? 0.3f : s.DayPhase;
            // night: 0 through the day, ramps up over dusk (0.6-0.68), 1 at night, back to 0 over dawn (0.95-1.0).
            float night = p < 0.6f ? 0 : p < 0.68f ? (p - 0.6f) / 0.08f : p < 0.95f ? 1 : 1 - (p - 0.95f) / 0.05f;
            night = night * night * (3 - 2 * night);
            float dusk = Mathf.Clamp01(1 - Mathf.Abs(p - 0.62f) / 0.07f) + Mathf.Clamp01(1 - Mathf.Abs(p - 0.02f) / 0.05f) * 0.8f;
            Night = night;

            // The sun crosses the sky by day; the moon rides high and cold at night.
            float dayT = Mathf.Clamp01(p / 0.66f);
            float elev = Mathf.Lerp(Mathf.Sin(dayT * Mathf.PI) * 55 + 12, 50, night);
            float yaw = Mathf.Lerp(-80 + dayT * 160, 30, night);
            _sun.transform.rotation = Quaternion.Euler(elev, yaw, 0);
            var dayCol = Color.Lerp(SunDay, SunGold, Mathf.Clamp01(dusk));
            _sun.color = Color.Lerp(dayCol, Moon, night);
            _sun.intensity = Mathf.Lerp(1.75f - dusk * 0.3f, 0.85f, night);
            _sun.shadowStrength = Mathf.Lerp(0.85f, 0.45f, night);

            RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(SkyDay, new Color(0.8f, 0.6f, 0.55f), dusk * 0.5f), SkyNight, night);
            RenderSettings.ambientEquatorColor = Color.Lerp(EqDay, EqNight, night);
            RenderSettings.ambientGroundColor = Color.Lerp(GroundDay, GroundNight, night);
            var fog = Color.Lerp(Color.Lerp(FogDay, FogDusk, dusk * 0.7f), FogNight, night);
            RenderSettings.fogColor = fog;
            if (CameraRig.Instance != null) CameraRig.Instance.Cam.backgroundColor = fog;
            Shader.SetGlobalFloat("_NightGlow", night);
            if (_grade != null) _grade.weight = night;
            Shader.SetGlobalColor("_GlowColor", new Color(1f, 0.75f, 0.4f));
        }
    }
}
