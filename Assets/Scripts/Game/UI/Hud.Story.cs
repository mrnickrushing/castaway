using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// Story scenes: full-screen cards with a painting behind, the speaker and their words typed out, tap to go on.
    /// A scene plays when the survival guide reaches the step it opens (the intro on a new game).
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _story;
        Image _storyArt;
        Text _storyWho, _storyText, _storyChapter, _storyHint;
        SceneDef _scene;
        int _line;
        float _typed;

        public bool StoryPlaying { get { return _scene != null; } }

        void BuildStory()
        {
            _story = UIKit.Img("Story", _root, null, Color.black).rectTransform;
            _story.Fill();
            var bg = _story.GetComponent<Image>();
            bg.raycastTarget = true;
            var tap = _story.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(NextLine);
            _storyArt = UIKit.Img("Art", _story, null, Color.white);
            var fit = _storyArt.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1;
            // Darken toward the bottom where the words are.
            var shade = UIKit.Img("Shade", _story, Gradient(), new Color(0, 0, 0, 0.92f));
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = new Vector2(1, 0.55f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            _storyChapter = UIKit.Outlined(UIKit.Label("Chapter", _story, "", 40, UIKit.Amber, TextAnchor.UpperLeft), 2f);
            _storyChapter.rectTransform.Fill(); _storyChapter.rectTransform.offsetMin = new Vector2(90, 0); _storyChapter.rectTransform.offsetMax = new Vector2(-90, -50);
            _storyWho = UIKit.Outlined(UIKit.Label("Who", _story, "", 50, UIKit.Amber, TextAnchor.LowerLeft), 2f);
            _storyWho.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(200, 290), new Vector2(1400, 70));
            _storyText = UIKit.Outlined(UIKit.Label("Text", _story, "", 46, Color.white, TextAnchor.UpperLeft, false), 1.5f);
            _storyText.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(200, 280), new Vector2(1500, 200));
            _storyText.rectTransform.pivot = new Vector2(0, 1);
            _storyHint = UIKit.Label("Hint", _story, "Tap to continue", 30, new Color(1, 1, 1, 0.6f), TextAnchor.LowerRight, false);
            _storyHint.rectTransform.Fill(); _storyHint.rectTransform.offsetMin = new Vector2(0, 40); _storyHint.rectTransform.offsetMax = new Vector2(-90, 0);
            var skip = UIKit.Button("Skip", _story, "SKIP", new Color(0, 0, 0, 0.5f), EndScene, 32);
            ((RectTransform)skip.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -40), new Vector2(180, 74));
            _story.gameObject.SetActive(false);
        }

        static readonly System.Collections.Generic.Dictionary<string, Sprite> _storyArts = new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>The full-screen painting for a story line: Resources/UI/Story/{key} (made for the cards), else the map painting.</summary>
        static Sprite StoryArt(string key)
        {
            Sprite sp;
            if (_storyArts.TryGetValue(key, out sp)) return sp;
            var tex = Resources.Load<Texture2D>("UI/Story/" + key);
            sp = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)) : UIKit.Place(key);
            _storyArts[key] = sp;
            return sp;
        }

        static Sprite _grad;
        static Sprite Gradient()
        {
            if (_grad != null) return _grad;
            var t = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 4; x++) t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(1 - y / 63f, 1.4f)));
            t.Apply();
            return _grad = Sprite.Create(t, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Plays the scene that opens the current guide step, once.</summary>
        void CheckStory()
        {
            if (_scene != null || LoadingScreen.Active || Cinema.Playing || _passOut > 0) return;
            var s = G.Session;
            var sc = Story.SceneFor(s.CurrentQuest.Id);
            if (sc == null || s.Flag("scene_" + sc.Id)) return;
            // The chapter's movie first (once), then the scene's words.
            if (sc.Movie != null && !s.Flag("movie_" + sc.Movie) && Cinema.Has(sc.Movie))
            {
                s.SetFlag("movie_" + sc.Movie);
                CloseAll();
                Cinema.Play(sc.Movie, () => PlayScene(sc));
                return;
            }
            PlayScene(sc);
        }

        public void PlayScene(SceneDef sc)
        {
            _scene = sc;
            _line = 0;
            _typed = 0;
            CloseAll();
            _radioOpen = _offersOpen = _shopOpen = _helpOpen = _bpOpen = _pathOpen = false;
            if (_building) ExitBuild();
            _story.gameObject.SetActive(true);
            int qi = Quests.All.FindIndex(q => q.Id == G.Session.CurrentQuest.Id);
            var ch = Story.ChapterOf(qi);
            int n = Story.Chapters.IndexOf(ch) + 1;
            _storyChapter.text = (n < Story.Chapters.Count ? "CHAPTER " + n + "  ·  " : "") + sc.Title.ToUpper();
            ShowLine();
        }

        void ShowLine()
        {
            var l = _scene.Lines[_line];
            var art = StoryArt(l.Art ?? "home");
            _storyArt.sprite = art;
            _storyArt.color = art != null ? Color.white : new Color(0.1f, 0.2f, 0.25f);
            if (art != null) _storyArt.GetComponent<AspectRatioFitter>().aspectRatio = art.rect.width / art.rect.height;
            _storyWho.text = string.IsNullOrEmpty(l.Who) ? "" : l.Who.ToUpper();
            _storyText.fontStyle = string.IsNullOrEmpty(l.Who) ? FontStyle.Italic : FontStyle.Normal;
            _typed = 0;
            Sfx.Play("beep");
        }

        /// <summary>A tap on the story card (finish the line, or go to the next).</summary>
        public void TapStory() { NextLine(); }

        void NextLine()
        {
            if (_scene == null) return;
            var l = _scene.Lines[_line];
            if (_typed < l.Text.Length) { _typed = l.Text.Length; return; } // first tap finishes the line
            _line++;
            if (_line >= _scene.Lines.Length) EndScene();
            else ShowLine();
        }

        void EndScene()
        {
            if (_scene == null) return;
            G.Session.SetFlag("scene_" + _scene.Id);
            _scene = null;
            _story.gameObject.SetActive(false);
            GameRoot.Instance.Save();
        }

        void UpdateStory()
        {
            CheckStory();
            if (_scene == null) return;
            var l = _scene.Lines[_line];
            _typed = Mathf.Min(l.Text.Length, _typed + Time.unscaledDeltaTime * 55);
            _storyText.text = l.Text.Substring(0, (int)_typed);
            _storyHint.color = new Color(1, 1, 1, _typed >= l.Text.Length ? 0.45f + 0.25f * Mathf.Sin(Time.unscaledTime * 4) : 0);
            // A slow push in on the painting.
            _storyArt.rectTransform.localScale = Vector3.one * (1.04f + 0.03f * Mathf.Sin(Time.unscaledTime * 0.2f));
        }
    }
}
