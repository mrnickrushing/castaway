using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace LastTide.Game
{
    /// <summary>
    /// Full-screen story movies: StreamingAssets/Videos/{id}.mp4 with its sound, the chapter title fading in over it and
    /// a SKIP button. Until the first frame (or instead of the clip, if it can't play) the clip's still
    /// (Resources/UI/Cinematics/{id}) slowly pushes in. Survival pauses and the music ducks while it runs.
    /// Clips come from Tools/art/ai_media.py.
    /// </summary>
    public sealed class Cinema : MonoBehaviour
    {
        static Cinema _current;
        public static bool Playing { get { return _current != null; } }

        static string VideoPath(string id)
        {
            // Linux Editors can't decode H.264; ai_media.py install keeps VP8 copies in Library/ for testing there.
            if (Application.platform == RuntimePlatform.LinuxEditor)
            {
                string webm = Path.Combine(Application.dataPath, "..", "Library", "EditorVideos", id + ".webm");
                if (File.Exists(webm)) return webm;
            }
            return Path.Combine(Application.streamingAssetsPath, "Videos", id + ".mp4");
        }

        /// <summary>True if the clip (or at least its still) is in the build.</summary>
        public static bool Has(string id)
        {
            return Application.platform == RuntimePlatform.Android || File.Exists(VideoPath(id)) || Resources.Load<Texture2D>("UI/Cinematics/" + id) != null;
        }

        public static void Play(string id, Action done)
        {
            if (_current != null) { done?.Invoke(); return; }
            var go = new GameObject("Cinema " + id, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Hud.RefW, Hud.RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1;
            go.AddComponent<GraphicRaycaster>();
            _current = go.AddComponent<Cinema>();
            _current.StartCoroutine(_current.Run(id, done));
        }

        bool _skip;

        IEnumerator Run(string id, Action done)
        {
            var root = (RectTransform)transform;
            var group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            var bg = UIKit.Img("Black", root, null, Color.black);
            bg.rectTransform.Fill();
            bg.raycastTarget = true;

            // Screen-filling 16:9 picture (a wide phone crops a little off the top and bottom).
            var frame = UIKit.Rect("Frame", root);
            frame.Fill();
            var fit = frame.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 16f / 9f;
            var still = Resources.Load<Texture2D>("UI/Cinematics/" + id);
            if (still != null)
            {
                var st = UIKit.Rect("Still", frame).gameObject.AddComponent<RawImage>();
                st.texture = still;
                st.raycastTarget = false;
                st.rectTransform.Fill();
                st.gameObject.AddComponent<KenBurns>();
            }
            var raw = UIKit.Rect("Video", frame).gameObject.AddComponent<RawImage>();
            raw.rectTransform.Fill();
            raw.raycastTarget = false;
            raw.color = new Color(1, 1, 1, 0);
            var rt = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32) { name = "Cinema" };
            raw.texture = rt;

            // Letterbox bars, and the chapter title over the lower one.
            var barTop = UIKit.Img("BarTop", root, null, Color.black);
            barTop.rectTransform.anchorMin = new Vector2(0, 1); barTop.rectTransform.anchorMax = Vector2.one;
            barTop.rectTransform.offsetMin = new Vector2(0, -70); barTop.rectTransform.offsetMax = Vector2.zero;
            var barBot = UIKit.Img("BarBottom", root, null, Color.black);
            barBot.rectTransform.anchorMin = Vector2.zero; barBot.rectTransform.anchorMax = new Vector2(1, 0);
            barBot.rectTransform.offsetMin = Vector2.zero; barBot.rectTransform.offsetMax = new Vector2(0, 70);
            var sc = TitleFor(id);
            var title = UIKit.Label("Title", root, sc, 40, new Color(1f, 0.93f, 0.78f, 0), TextAnchor.MiddleLeft);
            title.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(80, 8), new Vector2(1400, 56));

            var skip = UIKit.Button("Skip", root, "SKIP  ▶", new Color(0, 0, 0, 0.45f), () => _skip = true, 30);
            ((RectTransform)skip.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -90), new Vector2(190, 70));

            var vp = gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.source = VideoSource.Url;
            vp.url = VideoPath(id);
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.isLooping = false;
            vp.skipOnDrop = true;
            vp.audioOutputMode = Sfx.Muted ? VideoAudioOutputMode.None : VideoAudioOutputMode.Direct;
            bool failed = false, ended = false;
            vp.errorReceived += (p, msg) => { Debug.LogWarning("[Cinema] " + id + ": " + msg); failed = true; };
            vp.loopPointReached += p => ended = true;
            if (File.Exists(vp.url) || Application.platform == RuntimePlatform.Android) vp.Prepare();
            else failed = true;

            Sfx.Duck = 0;
            float t = 0, shown = 0;
            while (!_skip && !ended)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                group.alpha = Mathf.Clamp01(t * 2.5f);
                if (!failed && vp.isPrepared && !vp.isPlaying) vp.Play();
                if (vp.isPlaying && vp.frame > 0) raw.color = new Color(1, 1, 1, Mathf.Min(1, raw.color.a + dt * 4));
                // The title fades in after a beat and out again.
                shown += dt;
                float ta = Mathf.Clamp01((shown - 0.8f) * 1.5f) * Mathf.Clamp01((5.5f - shown) * 1.5f);
                title.color = new Color(title.color.r, title.color.g, title.color.b, ta);
                // No playable clip: hold the still for a few seconds instead.
                if ((failed || !vp.isPrepared) && t > (still != null ? 5f : 0.5f)) break;
                yield return null;
            }
            vp.Stop();
            for (float a = 1; a > 0; a -= Time.unscaledDeltaTime * 3f)
            {
                group.alpha = a;
                yield return null;
            }
            Sfx.Duck = 1;
            rt.Release();
            Destroy(rt);
            _current = null;
            Destroy(gameObject);
            done?.Invoke();
        }

        void OnDestroy() { if (_current == this) { _current = null; Sfx.Duck = 1; } }

        /// <summary>"CHAPTER 1 · WASHED ASHORE" for the scene the clip opens.</summary>
        static string TitleFor(string movie)
        {
            foreach (var kv in LastTide.Core.Story.Scenes)
                if (kv.Value.Movie == movie)
                {
                    int qi = LastTide.Core.Quests.All.FindIndex(q => q.Id == kv.Key);
                    var ch = LastTide.Core.Story.ChapterOf(qi);
                    int n = LastTide.Core.Story.Chapters.IndexOf(ch) + 1;
                    return (n < LastTide.Core.Story.Chapters.Count ? "CHAPTER " + n + "  ·  " : "") + kv.Value.Title.ToUpper();
                }
            return "";
        }

        sealed class KenBurns : MonoBehaviour
        {
            float _t;
            void Update() { _t += Time.unscaledDeltaTime; transform.localScale = Vector3.one * (1.02f + _t * 0.012f); }
        }
    }
}
