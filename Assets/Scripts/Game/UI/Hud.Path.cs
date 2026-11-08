using System.Linq;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// The Survivor's Path: the story's chapters as cards, each with its steps and their rewards. Finished steps pay
    /// on the free track, and on the golden Ultra track for owners of the Ultra pass. Take them one by one or all at once.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _path, _pathBody;
        bool _pathOpen;
        int _pathChapter = -1;
        float _tile = 92;
        static readonly string[] ChapterArt = { "home", "shore", "pier", "cave", "pirates", "keyart" };

        void BuildPath()
        {
            _path = UIKit.Img("Path", _root, null, new Color(0.36f, 0.38f, 0.33f, 1f)).rectTransform;
            _path.Fill();
            _path.GetComponent<Image>().raycastTarget = true;
            var tab = UIKit.Img("Tab", _path, null, new Color(0.15f, 0.5f, 0.5f)).rectTransform;
            tab.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(90, -20), new Vector2(460, 80));
            var tt = UIKit.Label("T", tab, "SURVIVOR'S PATH", 40, Color.white, TextAnchor.MiddleCenter, false);
            tt.rectTransform.Fill();
            _pathBody = UIKit.Rect("Body", _path);
            _pathBody.anchorMin = Vector2.zero; _pathBody.anchorMax = Vector2.one; _pathBody.offsetMin = new Vector2(90, 30); _pathBody.offsetMax = new Vector2(-240, -120);
            var x = UIKit.Button("Close", _path, "X", new Color(0, 0, 0, 0), () => { _pathOpen = false; _dirty = true; }, 80);
            ((RectTransform)x.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -20), new Vector2(120, 120));
            var cl = UIKit.Label("CLOSE", _path, "CLOSE", 40, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, false);
            cl.rectTransform.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-130, -230), new Vector2(200, 50));
            cl.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            _path.gameObject.SetActive(false);
        }

        public void OpenPath()
        {
            _pathOpen = true;
            var s = G.Session;
            // Open on the earliest chapter with something to claim, else the current one.
            int first = -1;
            for (int i = 0; i < s.State.quest && first < 0; i++) if (!s.Claimed(Quests.All[i]) || (s.Ultra && !s.ClaimedUltra(Quests.All[i]))) first = i;
            _pathChapter = Story.Chapters.IndexOf(Story.ChapterOf(first >= 0 ? first : s.State.quest));
            Sfx.Play("click");
            RefreshPath();
        }

        void RefreshPath()
        {
            _path.gameObject.SetActive(_pathOpen);
            if (!_pathOpen) return;
            var s = G.Session;
            Clear(_pathBody);
            var ch = Story.Chapters[_pathChapter];
            int start = Quests.All.FindIndex(q => q.Id == ch.FirstQuest);
            int end = _pathChapter + 1 < Story.Chapters.Count ? Quests.All.FindIndex(q => q.Id == Story.Chapters[_pathChapter + 1].FirstQuest) : Quests.All.Count;

            // The chapter card.
            var card = UIKit.Img("Card", _pathBody, null, new Color(0.3f, 0.32f, 0.28f)).rectTransform;
            card.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(560, 760));
            var art = UIKit.Img("Art", card, UIKit.Place(ChapterArt[_pathChapter % ChapterArt.Length]), Color.white);
            art.preserveAspect = false;
            art.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 400));
            var title = UIKit.Label("Title", card, (_pathChapter + 1) + ". " + ch.Title.ToUpper(), 44, Color.white, TextAnchor.UpperLeft, false);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -420), new Vector2(520, 60));
            var sum = UIKit.Label("Summary", card, s.State.quest >= start ? ch.Summary : "Keep surviving to open this chapter.", 28, new Color(1, 1, 1, 0.75f), TextAnchor.UpperLeft, false);
            sum.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -490), new Vector2(510, 220));
            foreach (int dir in new[] { -1, 1 })
            {
                int to = _pathChapter + dir;
                if (to < 0 || to >= Story.Chapters.Count) continue;
                var a = UIKit.Button("Arrow", _pathBody, dir < 0 ? "<" : ">", new Color(0, 0, 0, 0.3f), () => { _pathChapter = to; RefreshPath(); }, 70);
                ((RectTransform)a.transform).Place(new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(dir < 0 ? -40 : 600, -380), new Vector2(70, 140));
            }

            // The steps and their rewards, rows sized so a long chapter still fits above the buttons.
            float y = 0, rowH = Mathf.Min(120, 820f / Mathf.Max(1, end - start)), tile = Mathf.Min(92, rowH - 10);
            _tile = tile;
            for (int i = start; i < end; i++)
            {
                var q = Quests.All[i];
                bool done = s.Completed(i), now = i == s.State.quest;
                var row = UIKit.Img("Step", _pathBody, null, now ? new Color(0.3f, 0.38f, 0.3f) : new Color(0.28f, 0.3f, 0.27f)).rectTransform;
                row.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(640, -y), new Vector2(1060, rowH - 8));
                var t = UIKit.Label("T", row, (i - start + 1) + ". " + q.Text.ToUpper(), 26, done ? new Color(0.55f, 0.9f, 0.45f) : now ? Color.white : new Color(1, 1, 1, 0.45f), TextAnchor.MiddleLeft, false);
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(20, 0); t.rectTransform.offsetMax = new Vector2(-600, 0);
                float x = -590;
                foreach (var r in q.Reward) { RewardTile(row, r, x, false, s.Claimed(q)); x += tile + 8; }
                x = Mathf.Max(x, -400);
                foreach (var r in q.Premium) { RewardTile(row, r, x, !s.Ultra, s.ClaimedUltra(q)); x += tile + 8; }
                bool waiting = done && (!s.Claimed(q) || (s.Ultra && !s.ClaimedUltra(q)));
                if (waiting)
                {
                    int idx = i;
                    var b = UIKit.Button("Claim", row, "CLAIM", new Color(0.8f, 0.62f, 0.12f), () => { if (s.Claim(idx)) Sfx.Play("coin"); RefreshPath(); }, 28);
                    ((RectTransform)b.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(150, 70));
                }
                else if (done)
                {
                    var ok = UIKit.Label("OK", row, "DONE", 26, new Color(0.55f, 0.9f, 0.45f), TextAnchor.MiddleRight, false);
                    ok.rectTransform.Fill(); ok.rectTransform.offsetMax = new Vector2(-30, 0);
                }
                y += rowH;
            }

            int claim = s.Claimable;
            var all = UIKit.Button("All", _pathBody, "TAKE ALL (" + claim + ")", claim > 0 ? new Color(0.12f, 0.5f, 0.5f) : new Color(0.3f, 0.32f, 0.3f), () =>
            {
                if (s.ClaimAll() > 0) Sfx.Play("coin");
                RefreshPath();
            }, 38);
            ((RectTransform)all.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(120, 0), new Vector2(400, 90));
            if (!s.Ultra)
            {
                var ultra = UIKit.Button("Ultra", _pathBody, "BUY ULTRA  " + Store.PriceOf("lasttide.ultra"), new Color(0.8f, 0.62f, 0.12f), () =>
                    Store.Buy("lasttide.ultra", (ok, msg) => { if (ok) Sfx.Play("buy"); else if (!string.IsNullOrEmpty(msg)) Toast(msg); RefreshPath(); }), 34);
                ((RectTransform)ultra.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(560, 0), new Vector2(420, 90));
            }
        }

        void RewardTile(RectTransform row, ItemCount r, float x, bool locked, bool taken)
        {
            var tile = UIKit.Img("Reward", row, null, locked ? new Color(0.45f, 0.38f, 0.15f) : new Color(0.18f, 0.2f, 0.18f)).rectTransform;
            tile.Place(new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(_tile, _tile));
            UIKit.ItemIcon(tile, r.id, _tile * 0.8f);
            var n = UIKit.Outlined(UIKit.Label("N", tile, r.n.ToString(), 24, Color.white, TextAnchor.LowerRight), 1.5f);
            n.rectTransform.Fill(4);
            if (locked) { var l = UIKit.Img("Lock", tile, UIKit.Symbol("lock"), new Color(1f, 0.85f, 0.4f)); l.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-2, -2), new Vector2(30, 30)); }
            if (taken) { var c = UIKit.Img("Taken", tile, null, new Color(0, 0, 0, 0.55f)); c.rectTransform.Fill(); var k = UIKit.Img("Tick", tile, UIKit.Symbol("check"), new Color(0.55f, 0.9f, 0.45f)); k.rectTransform.Fill(22); }
        }
    }
}
