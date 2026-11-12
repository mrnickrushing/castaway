using System.Collections.Generic;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// The follow camera: high over the castaway's shoulder looking north, easing after them. Pinch (or the mouse wheel)
    /// zooms from the wide view down to a close one, the camera tilting toward the horizon as it comes in.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }
        public Camera Cam { get; private set; }
        Transform _target;
        Vector3 _focus;
        public float Distance = 17f;
        /// <summary>Looking down at the usual distance and further out; flatter close in, nearly over the shoulder.</summary>
        public float Pitch { get { return Mathf.Lerp(CloseUpPitch, 52f, Mathf.InverseLerp(MinDist, 17f, Distance)); } }
        /// <summary>When set, the camera looks here instead of at the castaway (build mode follows the ghost).</summary>
        public Vector3? Focus;
        const float MinDist = 6f, MaxDist = 32f, CloseUpPitch = 30f;
        readonly SeeThrough _seeThrough = new SeeThrough();

        public static CameraRig Create(Transform target)
        {
            var go = new GameObject("Camera");
            var rig = go.AddComponent<CameraRig>();
            Instance = rig;
            rig._target = target;
            rig.Cam = go.AddComponent<Camera>();
            rig.Cam.fieldOfView = 38;
            rig.Cam.nearClipPlane = 0.5f;
            rig.Cam.farClipPlane = 900;
            rig.Cam.clearFlags = CameraClearFlags.SolidColor;
            rig.Cam.backgroundColor = new Color(0.62f, 0.8f, 0.86f);
            go.tag = "MainCamera";
            var restart = GameObject.Find("Restart"); // a game started over: its stand-in listener goes as this one comes
            if (restart != null && restart.GetComponent<AudioListener>() != null) DestroyImmediate(restart.GetComponent<AudioListener>());
            go.AddComponent<AudioListener>();
#if LASTTIDE_URP
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
#endif
            rig._focus = target.position;
            rig.Snap();
            return rig;
        }

        float _shake;

        /// <summary>A jolt that dies away in about a third of a second (the castaway taking a blow). Bigger calls win.</summary>
        public static void Shake(float amount) { if (Instance != null) Instance._shake = Mathf.Max(Instance._shake, amount); }

        float _pitchNow = 52f, _fightW;
        Vector3 _fightOff;
        const float FightPitch = 46f;

        Vector3 Behind(float pitch) { return _focus + Vector3.up * 1.2f - Quaternion.Euler(pitch, 0, 0) * Vector3.forward * Distance; }

        /// <summary>Is the line from the castaway's chest to the camera above the ground all the way (a crest between
        /// them would hide the castaway even with the camera itself in the clear)?</summary>
        bool Clear(float pitch)
        {
            var from = _focus + Vector3.up * 1.2f;
            var to = Behind(pitch);
            for (int i = 1; i <= 5; i++)
            {
                float t = i / 5f;
                var q = Vector3.Lerp(from, to, t);
                if (q.y < Island.Height(q.x, q.z) + (i == 5 ? 1.2f : 0.4f)) return false;
            }
            return true;
        }

        void Snap()
        {
            // Close in, the camera sits low behind the castaway. Where a hill or a crest rises behind them it swings up over
            // it (same distance, still looking at them) rather than into it: quickly up, slowly back down.
            float want = Mathf.Lerp(Pitch, Mathf.Max(Pitch, FightPitch), _fightW);
            if (GameRoot.Instance != null && !GameRoot.Instance.OnMap)
                for (; want < 80f; want += 3f) if (Clear(want)) break;
            _pitchNow = Mathf.Lerp(_pitchNow, want, 1 - Mathf.Exp(-Time.unscaledDeltaTime * (want > _pitchNow ? 12f : 3f)));
            if (Time.unscaledDeltaTime <= 0 || Mathf.Abs(_pitchNow - want) > 40f) _pitchNow = want;   // first frame, or a jump (landing)
            transform.rotation = Quaternion.Euler(_pitchNow, 0, 0);
            var p = Behind(_pitchNow);
            if (GameRoot.Instance != null && !GameRoot.Instance.OnMap) p.y = Mathf.Max(p.y, Island.Height(p.x, p.z) + 0.6f);  // never under ground mid-swing
            transform.position = p;
            if (_shake > 0.001f)
            {
                float t = Time.unscaledTime * 60f;
                transform.position += (transform.right * (Mathf.PerlinNoise(t, 0.37f) - 0.5f) + transform.up * (Mathf.PerlinNoise(0.71f, t) - 0.5f)) * _shake * 2f;
                _shake = Mathf.MoveTowards(_shake, 0, Time.unscaledDeltaTime * 1.2f);
            }
        }

        // Fingers that came down on the open view (not on a button or panel), and when.
        readonly Dictionary<int, float> _free = new Dictionary<int, float>();
        readonly List<Touch> _pair = new List<Touch>(2);

        bool Blocked { get { return (GameRoot.Instance != null && GameRoot.Instance.OnMap) || (Hud.Instance != null && Hud.Instance.Blocking); } }

        /// <summary>
        /// Two fingers on the open view zoom. A finger on a button never counts, so walking with the stick while tapping
        /// attack doesn't drift the camera; two fingers landing together pinch even if the first one took the stick.
        /// </summary>
        void Pinch()
        {
            _pair.Clear();
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && !Hud.OverUI(t.fingerId)) _free[t.fingerId] = Time.unscaledTime;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { _free.Remove(t.fingerId); continue; }
                if (_free.ContainsKey(t.fingerId)) _pair.Add(t);
            }
            if (_pair.Count != 2 || Blocked) return;
            var a = _pair[0]; var b = _pair[1];
            int stick = Hud.Instance != null ? Hud.Instance.StickFinger : -2;
            if (a.fingerId == stick || b.fingerId == stick)
            {
                if (Mathf.Abs(_free[a.fingerId] - _free[b.fingerId]) > 0.25f) return;   // walking, and a second finger elsewhere
                Hud.Instance.ReleaseStick();                                              // both came down together: a pinch
            }
            float now = Vector2.Distance(a.position, b.position), before = Vector2.Distance(a.position - a.deltaPosition, b.position - b.deltaPosition);
            Distance = Mathf.Clamp(Distance * (before / Mathf.Max(1, now)), MinDist, MaxDist);
        }

        void LateUpdate()
        {
            if (_target == null) return;
            // Lead the castaway a little in the direction they're walking.
            var lead = Player.Instance != null ? Player.Instance.Velocity * 0.25f : Vector3.zero;
            // Zoomed in with a foe close, the castaway would stand right between the camera and it: aim between the two
            // and look down a little more, so the foe and its wind-up show over the castaway's shoulder.
            Enemy foe = null;
            if (Focus == null && Distance < 13f && Player.Instance != null)
                foe = Player.Instance.Locked ?? Enemy.Nearest(_target.position, 7f);
            _fightW = Mathf.MoveTowards(_fightW, foe != null ? 1f : 0f, Time.deltaTime * 2.5f);
            if (foe != null) { var off = foe.transform.position - _target.position; off.y = 0; _fightOff = off; }
            var goal = Focus ?? (_target.position + lead + _fightOff * 0.4f * _fightW);
            _focus = Vector3.Lerp(_focus, goal, 1 - Mathf.Exp(-Time.deltaTime * 5f));
            Pinch();
            if (!Hud.OverUI(-1) && !Blocked) Distance = Mathf.Clamp(Distance - Input.mouseScrollDelta.y, MinDist, MaxDist);
            Snap();
            // Trees and roofs between the camera and the castaway (or the build ghost) step aside.
            if (GameRoot.Instance != null && GameRoot.Instance.OnMap) _seeThrough.ShowAll();
            else _seeThrough.Tick(Cam, Focus ?? _target.position);
        }
    }
}
