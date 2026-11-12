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

        void Snap()
        {
            var rot = Quaternion.Euler(Pitch, 0, 0);
            transform.rotation = rot;
            var p = _focus + Vector3.up * 1.2f - rot * Vector3.forward * Distance;
            // Close in, the camera sits low behind the castaway: never inside the hill behind them.
            if (GameRoot.Instance != null && !GameRoot.Instance.OnMap) p.y = Mathf.Max(p.y, Island.Height(p.x, p.z) + 1.2f);
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
            _focus = Vector3.Lerp(_focus, Focus ?? (_target.position + lead), 1 - Mathf.Exp(-Time.deltaTime * 5f));
            Pinch();
            if (!Hud.OverUI(-1) && !Blocked) Distance = Mathf.Clamp(Distance - Input.mouseScrollDelta.y, MinDist, MaxDist);
            Snap();
            // Trees and roofs between the camera and the castaway (or the build ghost) step aside.
            if (GameRoot.Instance != null && GameRoot.Instance.OnMap) _seeThrough.ShowAll();
            else _seeThrough.Tick(Cam, Focus ?? _target.position);
        }
    }
}
