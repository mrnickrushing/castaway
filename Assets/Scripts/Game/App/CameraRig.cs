using UnityEngine;

namespace LastTide.Game
{
    /// <summary>The follow camera: high over the castaway's shoulder looking north, easing after them. Pinch to zoom.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }
        public Camera Cam { get; private set; }
        Transform _target;
        Vector3 _focus;
        public float Distance = 17f;
        public float Pitch = 52f;
        /// <summary>When set, the camera looks here instead of at the castaway (build mode follows the ghost).</summary>
        public Vector3? Focus;
        const float MinDist = 11f, MaxDist = 32f;
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

        void Snap()
        {
            var rot = Quaternion.Euler(Pitch, 0, 0);
            transform.rotation = rot;
            transform.position = _focus + Vector3.up * 1.2f - rot * Vector3.forward * Distance;
        }

        void LateUpdate()
        {
            if (_target == null) return;
            // Lead the castaway a little in the direction they're walking.
            var lead = Player.Instance != null ? Player.Instance.Velocity * 0.25f : Vector3.zero;
            _focus = Vector3.Lerp(_focus, Focus ?? (_target.position + lead), 1 - Mathf.Exp(-Time.deltaTime * 5f));
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0); var b = Input.GetTouch(1);
                float now = Vector2.Distance(a.position, b.position), before = Vector2.Distance(a.position - a.deltaPosition, b.position - b.deltaPosition);
                Distance = Mathf.Clamp(Distance * (before / Mathf.Max(1, now)), MinDist, MaxDist);
            }
            Distance = Mathf.Clamp(Distance - Input.mouseScrollDelta.y, MinDist, MaxDist);
            Snap();
            // Trees and roofs between the camera and the castaway (or the build ghost) step aside.
            if (GameRoot.Instance != null && GameRoot.Instance.OnMap) _seeThrough.ShowAll();
            else _seeThrough.Tick(Cam, Focus ?? _target.position);
        }
    }
}
