using System;
using System.Collections.Generic;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// The season window: the SEASON tab (season level and points, three free daily takes, a scrolling track of free and
    /// premium rewards, TAKE ALL and the premium pass) and the EVENTS tab (a week's calendar of event runs as bars, each
    /// opening its goal, milestones and rewards). Rewards taken show in a SEASON REWARDS card.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _season, _seasonBody, _seasonTabs, _rewardCard;
        bool _seasonOpen;
        int _seasonTab;                     // 0 season, 1 events
        string _eventShown;                 // event run key in the detail view
        float _trackScroll = -1;
        Text _seasonBadge;
        static readonly Color Teal = new Color(0.1f, 0.5f, 0.52f), Slate = new Color(0.24f, 0.26f, 0.24f, 0.96f), Gold = new Color(0.86f, 0.66f, 0.12f);

        void BuildSeason()
        {
            _season = UIKit.Img("Season", _root, null, new Color(0.36f, 0.38f, 0.33f, 1f)).rectTransform;
            _season.Fill();
            _season.GetComponent<Image>().raycastTarget = true;
            _seasonTabs = UIKit.Rect("Tabs", _season);
            _seasonTabs.anchorMin = new Vector2(0, 1); _seasonTabs.anchorMax = new Vector2(1, 1); _seasonTabs.pivot = new Vector2(0.5f, 1);
            _seasonTabs.offsetMin = new Vector2(120, -120); _seasonTabs.offsetMax = new Vector2(-260, -40);
            _seasonBody = UIKit.Rect("Body", _season);
            _seasonBody.anchorMin = Vector2.zero; _seasonBody.anchorMax = Vector2.one; _seasonBody.offsetMin = new Vector2(120, 50); _seasonBody.offsetMax = new Vector2(-260, -140);
            var x = UIKit.Button("Close", _season, "X", new Color(0, 0, 0, 0), CloseSeason, 80);
            ((RectTransform)x.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -20), new Vector2(120, 120));
            var cl = UIKit.Label("CLOSE", _season, "CLOSE", 40, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, false);
            cl.rectTransform.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-130, -230), new Vector2(200, 50));
            cl.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            _season.gameObject.SetActive(false);

            _rewardCard = UIKit.Img("Rewards", _root, null, new Color(0.03f, 0.05f, 0.05f, 0.9f)).rectTransform;
            _rewardCard.Fill();
            _rewardCard.GetComponent<Image>().raycastTarget = true;
            _rewardCard.gameObject.SetActive(false);
        }

        public void OpenSeason(int tab = -1)
        {
            var s = G.Session;
            _seasonOpen = true;
            _seasonTab = tab >= 0 ? tab : s.SeasonWaiting == 0 && s.EventsBadge > 0 ? 1 : 0;
            _eventShown = null;
            _trackScroll = -1;
            Sfx.Play("click");
            RefreshSeason();
        }

        void CloseSeason() { _seasonOpen = false; _season.gameObject.SetActive(false); _dirty = true; }

        void RefreshSeason()
        {
            _season.gameObject.SetActive(_seasonOpen);
            if (!_seasonOpen) return;
            var s = G.Session;
            // Remember where the reward track was scrolled to, so taking a reward doesn't jump it.
            var oldScroll = _seasonBody.GetComponentInChildren<ScrollRect>();
            if (oldScroll != null) _trackScroll = oldScroll.horizontalNormalizedPosition;
            Clear(_seasonTabs);
            Clear(_seasonBody);
            var bar = UIKit.Img("Bar", _seasonTabs, null, new Color(0.2f, 0.22f, 0.2f, 0.95f)); bar.rectTransform.Fill();
            Tab(0, "season", "SEASON " + s.SeasonNumber, s.SeasonWaiting, 0);
            Tab(1, "calendar", "EVENTS", s.EventsBadge, 380);
            if (_seasonTab == 0) SeasonTab(); else if (_eventShown != null) EventDetail(); else EventsTab();
        }

        void Tab(int i, string sym, string text, int badge, float x)
        {
            bool on = _seasonTab == i;
            var b = UIKit.Button("Tab", _seasonTabs, null, on ? new Color(0.24f, 0.26f, 0.24f) : new Color(0, 0, 0, 0), () => { _seasonTab = i; _eventShown = null; RefreshSeason(); });
            var rt = (RectTransform)b.transform;
            rt.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 0), new Vector2(360, 80));
            if (on) { var u = UIKit.Img("Under", rt, null, new Color(0.2f, 0.75f, 0.72f)); u.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(360, 6)); }
            var ic = UIKit.Img("Icon", rt, UIKit.Symbol(sym), Color.white); ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(54, 54));
            var t = UIKit.Label("T", rt, text, 40, Color.white, TextAnchor.MiddleLeft, false); t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(100, 0);
            if (badge > 0) { var n = Badge(rt); n.text = badge > 9 ? "9+" : badge.ToString(); n.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(-6, -14); }
        }

        // ------------------------------------------------------------------ season tab

        void SeasonTab()
        {
            var s = G.Session;
            // Banner: the season's painting, time left, level and points, the daily take, and the premium highlights.
            var banner = UIKit.Img("Banner", _seasonBody, null, Slate).rectTransform;
            banner.anchorMin = new Vector2(0, 1); banner.anchorMax = new Vector2(1, 1); banner.pivot = new Vector2(0.5f, 1);
            banner.offsetMin = new Vector2(0, -300); banner.offsetMax = Vector2.zero;
            // Premium highlights on the right (the companions and the gold chest): as many as fit beside a 1000-wide
            // painting, three on a wide phone, two on 16:9 and iPad. The painting fills the rest.
            int room = Mathf.Clamp(Mathf.FloorToInt((_root.rect.width - 380 - 1020) / 220f), 0, 3);
            float artW = Mathf.Max(1000, _root.rect.width - 380 - room * 220 - 30);
            banner.gameObject.AddComponent<RectMask2D>();
            var sprite = UIKit.Place("pirates");
            var art = UIKit.Img("Art", banner, sprite, new Color(0.75f, 0.75f, 0.75f));
            art.preserveAspect = false;
            // A little squeezed (the painting is 3:2, the banner over 4:1) and cropped from the bottom, so the flag
            // and Red Jack's ship stay in view.
            float artH = sprite != null ? Mathf.Max(300, artW * sprite.rect.height / sprite.rect.width * 0.55f) : 300;
            art.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 10), new Vector2(artW, artH));
            var fade = UIKit.Img("Fade", banner, Fade(), new Color(0.24f, 0.26f, 0.24f, 1f));
            fade.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(artW - 130, 0), new Vector2(260, 300)); // over the painting's right end, flipped: clear to slate
            fade.rectTransform.localScale = new Vector3(-1, 1, 1);
            var clock = UIKit.Outlined(UIKit.Label("Time", banner, "◷ " + Left(s.SeasonLeft), 34, Color.white, TextAnchor.MiddleLeft, false), 1.5f);
            clock.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -14), new Vector2(400, 50));
            var lvl = UIKit.Outlined(UIKit.Label("Level", banner, "Level " + s.SeasonLevel, 46, Color.white, TextAnchor.MiddleLeft, false), 1.5f);
            lvl.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 92), new Vector2(400, 60));
            string pts = s.SeasonLevel >= Season.Levels ? "MAX" : s.SeasonInLevel + "/" + Season.PointsPerLevel;
            var pt = UIKit.Outlined(UIKit.Label("Pts", banner, pts, 38, Color.white, TextAnchor.MiddleLeft, false), 1.5f);
            pt.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(84, 30), new Vector2(260, 56));
            var pi = UIKit.Img("PIcon", banner, UIKit.Symbol("season"), UIKit.Sand); pi.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 34), new Vector2(48, 48));
            var prog = UIKit.Img("Prog", banner, null, new Color(0, 0, 0, 0.4f)).rectTransform;
            prog.anchorMin = new Vector2(0, 0); prog.anchorMax = new Vector2(1, 0); prog.pivot = new Vector2(0, 0); prog.offsetMin = Vector2.zero; prog.offsetMax = new Vector2(0, 8);
            var pf = UIKit.Img("Fill", prog, null, new Color(0.95f, 0.95f, 0.9f)).rectTransform;
            pf.anchorMin = Vector2.zero; pf.anchorMax = new Vector2(s.SeasonInLevel / (float)Season.PointsPerLevel, 1); pf.offsetMin = pf.offsetMax = Vector2.zero;

            int takes = s.TakesLeft;
            var take = UIKit.Button("Take", banner, "TAKE  +" + Season.TakePoints, takes > 0 ? new Color(0.62f, 0.46f, 0.08f) : new Color(0.3f, 0.3f, 0.28f), () =>
            {
                if (s.TakeSeasonBonus()) { Sfx.Play("coin"); RefreshSeason(); } else Toast("Come back tomorrow for more");
            }, 36);
            ((RectTransform)take.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(380, 40), new Vector2(320, 76));
            var tl = UIKit.Img("Count", take.transform, UIKit.Round, new Color(0.45f, 0.33f, 0.05f)); tl.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 34));
            var tt = UIKit.Label("N", tl.transform, takes + "/" + Season.TakesPerDay, 24, Color.white, TextAnchor.MiddleCenter); tt.rectTransform.Fill();
            var free = UIKit.Label("Free", banner, "free, three a day", 24, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleLeft, false);
            free.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(720, 52), new Vector2(260, 50));

            float hx = -30;
            foreach (var (id, col) in new[] { ("pet_wolf", new Color(0.45f, 0.36f, 0.6f)), ("pet_husky", new Color(0.62f, 0.52f, 0.2f)), ("gold_chest", new Color(0.62f, 0.52f, 0.2f)) })
            {
                if (room-- <= 0) break;
                var h = UIKit.Img("Highlight", banner, null, new Color(col.r, col.g, col.b, 0.75f)).rectTransform;
                h.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(hx, 0), new Vector2(210, 300));
                UIKit.ItemIcon(h, id, 180);
                var hn = UIKit.Outlined(UIKit.Label("Name", h, Content.NameOf(id), 22, Color.white, TextAnchor.LowerCenter, false), 1.2f); hn.rectTransform.Fill(8);
                hx -= 220;
            }

            // The reward track.
            var track = UIKit.Img("Track", _seasonBody, null, new Color(0.42f, 0.42f, 0.36f, 0.9f)).rectTransform;
            track.anchorMin = new Vector2(0, 0); track.anchorMax = new Vector2(1, 1); track.offsetMin = new Vector2(0, 120); track.offsetMax = new Vector2(0, -320);
            var view = UIKit.Rect("View", track); view.Fill(16);
            view.gameObject.AddComponent<RectMask2D>();
            var content = UIKit.Rect("Content", view);
            content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 0.5f);
            const float colW = 150;
            content.sizeDelta = new Vector2(colW * Season.Levels + 20, 0);
            content.anchoredPosition = Vector2.zero;
            var sr = track.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view; sr.content = content; sr.horizontal = true; sr.vertical = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.inertia = true; sr.scrollSensitivity = 30;
            float trackH = _root.rect.height - 140 - 50 - 320 - 120 - 32;   // about the visible height of the track
            float tile = Mathf.Min(128, (trackH - 90) / 2 - 10);
            float lift = Mathf.Max(0, (trackH - (70 + 2 * tile + 34)) / 2);  // centred in the box (an iPad's is much taller)
            int level = s.SeasonLevel;
            // Progress line along the diamonds.
            var line = UIKit.Img("Line", content, null, new Color(0, 0, 0, 0.35f)).rectTransform;
            line.anchorMin = line.anchorMax = new Vector2(0, 0); line.pivot = new Vector2(0, 0.5f);
            line.anchoredPosition = new Vector2(10, 34 + lift); line.sizeDelta = new Vector2(colW * Season.Levels, 10);
            var lineFill = UIKit.Img("Fill", content, null, new Color(1f, 0.8f, 0.2f)).rectTransform;
            lineFill.anchorMin = lineFill.anchorMax = new Vector2(0, 0); lineFill.pivot = new Vector2(0, 0.5f);
            lineFill.anchoredPosition = new Vector2(10, 34 + lift);
            lineFill.sizeDelta = new Vector2(colW * (level - 1) + colW * 0.5f + colW * s.SeasonInLevel / (float)Season.PointsPerLevel * (level < Season.Levels ? 1 : 0), 10);
            for (int lv = 1; lv <= Season.Levels; lv++)
            {
                float cx = 10 + (lv - 1) * colW + colW / 2;
                SeasonTile(content, lv, true, new Vector2(cx, lift + 70 + tile + 14 + tile / 2), tile);
                SeasonTile(content, lv, false, new Vector2(cx, lift + 70 + tile / 2), tile);
                var dia = UIKit.Img("Diamond", content, null, lv <= level ? new Color(1f, 0.8f, 0.2f) : new Color(0.15f, 0.16f, 0.15f)).rectTransform;
                dia.anchorMin = dia.anchorMax = new Vector2(0, 0);
                dia.anchoredPosition = new Vector2(cx, 34 + lift); dia.sizeDelta = new Vector2(46, 46); dia.localRotation = Quaternion.Euler(0, 0, 45);
                var dn = UIKit.Label("N", content, lv.ToString(), 26, lv <= level ? new Color(0.2f, 0.15f, 0.05f) : Color.white, TextAnchor.MiddleCenter);
                dn.rectTransform.anchorMin = dn.rectTransform.anchorMax = new Vector2(0, 0);
                dn.rectTransform.anchoredPosition = new Vector2(cx, 34 + lift); dn.rectTransform.sizeDelta = new Vector2(60, 46);
            }
            // Open on the first reward waiting, else around the current level.
            if (_trackScroll < 0)
            {
                int first = level;
                for (int lv = 1; lv <= Season.Levels; lv++) if (s.SeasonClaimable(lv, false) || s.SeasonClaimable(lv, true)) { first = lv; break; }
                _trackScroll = Mathf.Clamp01((first - 2) / (float)(Season.Levels - 8));
            }
            Canvas.ForceUpdateCanvases();
            sr.horizontalNormalizedPosition = _trackScroll;

            // Bottom: the track legend, TAKE ALL and the premium pass.
            var legend = UIKit.Label("Legend", _seasonBody, "Top row: premium   Bottom row: free\nSeason points come from everything that earns experience.", 24, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft, false);
            legend.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, 10), new Vector2(760, 90));
            int waiting = s.SeasonWaiting;
            var all = UIKit.Button("All", _seasonBody, waiting > 0 ? "TAKE ALL (" + waiting + ")" : "TAKE ALL", waiting > 0 ? Teal : new Color(0.3f, 0.32f, 0.3f), () =>
            {
                var got = s.ClaimAllSeason();
                if (got.Count > 0) { Sfx.Play("coin"); ShowRewards("SEASON REWARDS", got); }
                RefreshSeason();
            }, 38);
            ((RectTransform)all.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(s.SeasonPremium ? 0 : -560, 10), new Vector2(400, 96));
            if (!s.SeasonPremium)
            {
                var prem = UIKit.Button("Premium", _seasonBody, "GET PREMIUM  " + Store.PriceOf(Season.PremiumProduct), Gold, () =>
                    Store.Buy(Season.PremiumProduct, (ok, msg) => { if (ok) Sfx.Play("buy"); else if (!string.IsNullOrEmpty(msg)) Toast(msg); RefreshSeason(); }), 36);
                ((RectTransform)prem.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 10), new Vector2(540, 96));
                var li = UIKit.Img("Lock", prem.transform, UIKit.Symbol("lock"), Color.white); li.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(44, 44));
            }
        }

        void SeasonTile(RectTransform content, int lv, bool premium, Vector2 pos, float size)
        {
            var s = G.Session;
            var r = premium ? Season.Premium[lv] : Season.Free[lv];
            bool locked = premium && !s.SeasonPremium, reached = lv <= s.SeasonLevel, taken = s.SeasonClaimed(lv, premium), ready = s.SeasonClaimable(lv, premium);
            Color back = premium ? new Color(0.36f, 0.3f, 0.16f) : new Color(0.24f, 0.27f, 0.25f);
            if (r.id.StartsWith("pet_")) back = premium ? new Color(0.36f, 0.26f, 0.45f) : new Color(0.18f, 0.36f, 0.4f);
            var b = UIKit.Button("Tile", content, null, back, () =>
            {
                if (ready) { var got = s.ClaimSeason(lv, premium); if (got.Count > 0) { Sfx.Play("coin"); ShowRewards("SEASON REWARDS", got); } RefreshSeason(); }
                else if (locked) Toast("Premium reward: get the premium pass");
                else if (!reached) Toast("Reach season level " + lv);
                else Toast(Content.NameOf(r.id));
            });
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size + 6, size);
            // The FREE / PREMIUM band across the top; the picture sits under it, the count in the corner (raised clear of
            // TAKE when there is one).
            const float band = 28;
            var pic = UIKit.Rect("Pic", rt); pic.Fill(); pic.offsetMax = new Vector2(0, -band);
            UIKit.ItemIcon(pic, r.id, (size - band) * 0.86f);
            if (r.n > 1) { var n = UIKit.Outlined(UIKit.Label("N", rt, r.n.ToString(), 26, Color.white, TextAnchor.LowerRight), 1.5f); n.rectTransform.Fill(6); if (ready) n.rectTransform.offsetMin = new Vector2(6, 18); }
            var tag = UIKit.Img("Tag", rt, null, premium ? new Color(0.62f, 0.46f, 0.08f) : new Color(0.25f, 0.6f, 0.25f)).rectTransform;
            tag.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, -band), new Vector2(size + 6, band));
            var tagT = UIKit.Label("T", tag, premium ? "PREMIUM" : "FREE", 18, Color.white, TextAnchor.MiddleCenter, false); tagT.rectTransform.Fill();
            if (locked) { var l = UIKit.Img("Lock", rt, UIKit.Symbol("lock"), new Color(1f, 0.85f, 0.4f)); l.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(4, 4), new Vector2(30, 30)); } // bottom left: the count has the right
            if (taken)
            {
                var c = UIKit.Img("Taken", rt, null, new Color(0, 0, 0, 0.55f)); c.rectTransform.Fill();
                var k = UIKit.Img("Tick", rt, UIKit.Symbol("check"), new Color(0.55f, 0.9f, 0.45f)); k.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -30), new Vector2(40, 40));
            }
            else if (ready)
            {
                foreach (var (amin, amax) in new[] { (new Vector2(0, 0), new Vector2(1, 0)), (new Vector2(0, 1), new Vector2(1, 1)), (new Vector2(0, 0), new Vector2(0, 1)), (new Vector2(1, 0), new Vector2(1, 1)) })
                {
                    var e = UIKit.Img("Edge", rt, null, new Color(1f, 0.8f, 0.2f)).rectTransform;
                    e.anchorMin = amin; e.anchorMax = amax; e.sizeDelta = new Vector2(amin.x == amax.x ? 5 : 0, amin.y == amax.y ? 5 : 0); e.anchoredPosition = Vector2.zero;
                    e.GetComponent<Image>().raycastTarget = false;
                }
                var take = UIKit.Img("Take", rt, UIKit.Round, new Color(0.8f, 0.62f, 0.12f)); take.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 30));
                var tk = UIKit.Label("T", take.transform, "TAKE", 20, Color.white, TextAnchor.MiddleCenter, false); tk.rectTransform.Fill();
            }
            else if (!reached) { var c = UIKit.Img("Dim", rt, null, new Color(0, 0, 0, 0.3f)); c.rectTransform.Fill(); c.raycastTarget = false; }
        }

        // ------------------------------------------------------------------ events tab

        static DateTime DayDate(int day) { return new DateTime(1970, 1, 1).AddDays(day); }
        static string Md(int day) { var d = DayDate(day); return d.Month + "/" + d.Day.ToString("00"); }

        void EventsTab()
        {
            var s = G.Session;
            int today = s.Today, from = today - 1, to = from + 7;
            var runs = s.EventRuns(from, to);

            // Left: the list of running and coming events.
            var list = UIKit.Rect("List", _seasonBody);
            list.anchorMin = new Vector2(0, 0); list.anchorMax = new Vector2(0, 1); list.pivot = new Vector2(0, 1);
            list.offsetMin = new Vector2(0, 0); list.offsetMax = new Vector2(300, 0);
            var cal = UIKit.Img("Calendar", _seasonBody, null, new Color(0.42f, 0.42f, 0.36f, 0.85f)).rectTransform;
            cal.anchorMin = Vector2.zero; cal.anchorMax = Vector2.one; cal.offsetMin = new Vector2(320, 0); cal.offsetMax = Vector2.zero;
            var head = UIKit.Img("Head", list, null, Teal).rectTransform;
            head.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 100));
            var ht = UIKit.Label("T", head, "EVENT CALENDAR", 30, Color.white, TextAnchor.MiddleCenter, false); ht.rectTransform.Fill();
            float y = -110;
            foreach (var r in runs)
            {
                if (r.End <= today) continue;
                var run = r;
                bool live = r.Start <= today;
                var b = UIKit.Button("Event", list, null, Slate, () => { _eventShown = run.Key; RefreshSeason(); });
                ((RectTransform)b.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(300, 96));
                var t = UIKit.Label("T", b.transform, r.Def.Name.ToUpper() + (live ? "" : "\n<size=20>STARTS " + Md(r.Start) + "</size>"), 26, live ? Color.white : new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, false);
                t.supportRichText = true; t.rectTransform.Fill(8);
                if (live && s.EventWaiting(r) > 0) { var n = Badge((RectTransform)b.transform); n.text = "!"; }
                else if (live && s.EventNew(r)) Pill((RectTransform)b.transform, "NEW", UIKit.Red);
                y -= 104;
                if (y < -860) break;
            }

            // Right: seven days, the event runs as bars across them.
            float colW;
            var grid = UIKit.Rect("Grid", cal); grid.Fill(10);
            colW = 1f / 7;
            for (int i = 0; i < 7; i++)
            {
                int d = from + i;
                var colR = UIKit.Img("Col", grid, null, i % 2 == 0 ? new Color(1, 1, 1, 0.03f) : new Color(0, 0, 0, 0.03f)).rectTransform;
                colR.anchorMin = new Vector2(i * colW, 0); colR.anchorMax = new Vector2((i + 1) * colW, 1); colR.offsetMin = colR.offsetMax = Vector2.zero;
                var sep = UIKit.Img("Sep", colR, null, new Color(0, 0, 0, 0.18f)).rectTransform; sep.anchorMin = new Vector2(1, 0); sep.anchorMax = new Vector2(1, 1); sep.sizeDelta = new Vector2(3, 0); sep.anchoredPosition = Vector2.zero;
                var date = DayDate(d);
                var dl = UIKit.Label("Day", colR, date.DayOfWeek.ToString().Substring(0, 3) + "\n" + Md(d), 30, d == today ? new Color(0.3f, 0.9f, 0.85f) : new Color(1, 1, 1, 0.85f), TextAnchor.UpperLeft, false);
                dl.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -6), new Vector2(220, 80));
            }
            var rows = UIKit.Rect("Rows", grid);
            rows.anchorMin = Vector2.zero; rows.anchorMax = Vector2.one; rows.offsetMin = Vector2.zero; rows.offsetMax = new Vector2(0, -96);
            float rowH = Mathf.Min(120, 760f / Mathf.Max(1, runs.Count));
            float ry = 0;
            foreach (var r in runs)
            {
                var run = r;
                int a = Math.Max(r.Start, from) - from, e = Math.Min(r.End, to) - from;
                var bar = UIKit.Button("Run", rows, null, r.End <= today ? new Color(0.3f, 0.35f, 0.35f) : Teal, () => { _eventShown = run.Key; RefreshSeason(); });
                var rt = (RectTransform)bar.transform;
                rt.anchorMin = new Vector2(a * colW, 1); rt.anchorMax = new Vector2(e * colW, 1); rt.pivot = new Vector2(0, 1);
                rt.offsetMin = new Vector2(2, -ry - rowH + 8); rt.offsetMax = new Vector2(-2, -ry);
                var pic = UIKit.Img("Art", rt, UIKit.Place(r.Def.Art), new Color(1, 1, 1, 0.55f)); pic.preserveAspect = false;
                pic.rectTransform.anchorMin = Vector2.zero; pic.rectTransform.anchorMax = new Vector2(0, 1); pic.rectTransform.pivot = new Vector2(0, 0.5f);
                pic.rectTransform.sizeDelta = new Vector2(Mathf.Min(420, (e - a) * 1600f / 7), 0); pic.rectTransform.anchoredPosition = Vector2.zero; pic.raycastTarget = false;
                var top = UIKit.Img("Top", rt, null, new Color(0.3f, 0.85f, 0.8f)).rectTransform; top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one; top.pivot = new Vector2(0.5f, 1); top.sizeDelta = new Vector2(0, 5); top.anchoredPosition = Vector2.zero;
                var t = UIKit.Outlined(UIKit.Label("T", rt, r.Def.Name.ToUpper() + "\n" + Md(r.Start) + " - " + Md(r.End - 1), Mathf.RoundToInt(Mathf.Min(30, rowH * 0.3f)), Color.white, TextAnchor.MiddleLeft, false), 1.4f);
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(Mathf.Min(140, (e - a) * 40), 0);
                var arrow = UIKit.Label(">", rt, ">", 40, Color.white, TextAnchor.MiddleRight, false); arrow.rectTransform.Fill(); arrow.rectTransform.offsetMax = new Vector2(-16, 0);
                if (s.EventWaiting(r) > 0) { var n = Badge(rt); n.text = "!"; }
                ry += rowH;
            }
        }

        static void Pill(RectTransform on, string text, Color c)
        {
            var p = UIKit.Img("Pill", on, UIKit.Round, c).rectTransform;
            p.Place(new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-8, -6), new Vector2(76, 32));
            var t = UIKit.Label("T", p, text, 20, Color.white, TextAnchor.MiddleCenter, false); t.rectTransform.Fill();
        }

        void EventDetail()
        {
            var s = G.Session;
            var run = s.EventRuns(s.Today - 14, s.Today + 14).Find(r => r.Key == _eventShown);
            if (run.Def == null) { _eventShown = null; EventsTab(); return; }
            bool live = run.Start <= s.Today && run.End > s.Today, over = run.End <= s.Today;
            if (live) s.SeeEvent(run);
            var back = UIKit.Button("Back", _seasonBody, "<  CALENDAR", new Color(0, 0, 0, 0.3f), () => { _eventShown = null; RefreshSeason(); }, 30);
            ((RectTransform)back.transform).Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(260, 70));

            var card = UIKit.Img("Card", _seasonBody, null, Slate).rectTransform;
            card.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -90), new Vector2(620, 780));
            var art = UIKit.Img("Art", card, UIKit.Place(run.Def.Art), Color.white); art.preserveAspect = false;
            art.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(620, 380));
            var title = UIKit.Label("Title", card, run.Def.Name.ToUpper(), 46, Color.white, TextAnchor.UpperLeft, false);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -396), new Vector2(580, 60));
            double end = run.End * 86400.0 - Session.UtcOffsetSeconds - s.RealNow(), start = run.Start * 86400.0 - Session.UtcOffsetSeconds - s.RealNow();
            string when = over ? "Ended " + Md(run.End - 1) : live ? "◷ " + Left(end) + " left" : "Starts in " + Left(start);
            var w = UIKit.Label("When", card, Md(run.Start) + " - " + Md(run.End - 1) + "     " + when, 28, UIKit.Amber, TextAnchor.UpperLeft, false);
            w.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -460), new Vector2(580, 40));
            var blurb = UIKit.Label("Blurb", card, run.Def.Blurb, 28, new Color(1, 1, 1, 0.8f), TextAnchor.UpperLeft, false);
            blurb.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -515), new Vector2(570, 240));

            // Milestones.
            int n = s.EventCount(run), claimed = s.EventClaimed(run);
            var goal = UIKit.Label("Goal", _seasonBody, "GOAL: " + run.Def.Unit.ToUpper() + "   " + n + " / " + run.Def.Targets[run.Def.Targets.Length - 1], 36, Color.white, TextAnchor.MiddleLeft, false);
            goal.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(660, -90), new Vector2(1000, 60));
            float y = -170;
            for (int i = 0; i < run.Def.Targets.Length; i++)
            {
                int target = run.Def.Targets[i];
                bool got = i < claimed, reached = n >= target;
                var row = UIKit.Img("Milestone", _seasonBody, null, reached && !got ? new Color(0.3f, 0.38f, 0.3f) : new Color(0.28f, 0.3f, 0.27f)).rectTransform;
                row.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(660, y), new Vector2(1060, 180));
                var lab = UIKit.Label("T", row, (i + 1) + ".  " + target + " " + run.Def.Unit, 32, got ? new Color(0.55f, 0.9f, 0.45f) : Color.white, TextAnchor.UpperLeft, false);
                lab.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -14), new Vector2(500, 44));
                var pb = UIKit.Img("Bar", row, UIKit.Round, new Color(0, 0, 0, 0.4f)).rectTransform;
                pb.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 30), new Vector2(420, 26));
                var pf = UIKit.Img("Fill", pb, UIKit.Round, reached ? UIKit.Green : new Color(0.3f, 0.75f, 0.72f)).rectTransform;
                pf.anchorMin = Vector2.zero; pf.anchorMax = new Vector2(Mathf.Clamp01(n / (float)target), 1); pf.offsetMin = pf.offsetMax = Vector2.zero;
                var pn = UIKit.Label("N", pb, Mathf.Min(n, target) + " / " + target, 20, Color.white, TextAnchor.MiddleCenter, false); pn.rectTransform.Fill();
                float x = 480;
                foreach (var r in run.Def.Rewards[i])
                {
                    var tile = UIKit.Img("Reward", row, null, new Color(0.18f, 0.2f, 0.18f)).rectTransform;
                    tile.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(130, 130));
                    UIKit.ItemIcon(tile, r.id, 104);
                    if (r.n > 1) { var rn = UIKit.Outlined(UIKit.Label("N", tile, r.n.ToString(), 26, Color.white, TextAnchor.LowerRight), 1.5f); rn.rectTransform.Fill(6); }
                    if (got) { var c = UIKit.Img("Taken", tile, null, new Color(0, 0, 0, 0.55f)); c.rectTransform.Fill(); var k = UIKit.Img("Tick", tile, UIKit.Symbol("check"), new Color(0.55f, 0.9f, 0.45f)); k.rectTransform.Fill(30); }
                    x += 140;
                }
                y -= 196;
            }
            int waiting = s.EventWaiting(run);
            var claim = UIKit.Button("Claim", _seasonBody, waiting > 0 ? "TAKE REWARDS" : claimed >= run.Def.Targets.Length ? "ALL TAKEN" : over ? "EVENT OVER" : "KEEP GOING", waiting > 0 ? Gold : new Color(0.3f, 0.32f, 0.3f), () =>
            {
                var got = s.ClaimEvent(run);
                if (got.Count > 0) { Sfx.Play("coin"); ShowRewards(run.Def.Name.ToUpper(), got); }
                RefreshSeason();
            }, 38);
            ((RectTransform)claim.transform).Place(new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(460, 96));
            if (!live && !over)
            {
                var note = UIKit.Label("Note", _seasonBody, "This event hasn't started yet.", 28, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft, false);
                note.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(660, 20), new Vector2(600, 60));
            }
        }

        static string Left(double secs)
        {
            secs = Math.Max(0, secs);
            int d = (int)(secs / 86400), h = (int)(secs % 86400 / 3600), m = (int)(secs % 3600 / 60);
            return d > 0 ? d + "d " + h + "h" : h > 0 ? h + "h " + m + "m" : m + "m";
        }

        // ------------------------------------------------------------------ the rewards card

        /// <summary>A full-screen card listing what was just received (season, events, crates).</summary>
        public void ShowRewards(string title, List<ItemCount> items)
        {
            Clear(_rewardCard);
            _rewardCard.gameObject.SetActive(true);
            _rewardCard.SetAsLastSibling();
            // Merge duplicates.
            var merged = new List<ItemCount>();
            foreach (var it in items) { int i = merged.FindIndex(m => m.id == it.id); if (i < 0) merged.Add(it); else merged[i] = new ItemCount(it.id, merged[i].n + it.n); }
            // A band that fades out towards both ends: two halves from the middle, the left one mirrored.
            foreach (float side in new[] { 1f, -1f })
            {
                var band = UIKit.Img("Band", _rewardCard, Fade(), new Color(0.2f, 0.36f, 0.36f, 0.85f)).rectTransform;
                band.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 10), new Vector2(800, 520));
                band.localScale = new Vector3(side, 1, 1);
            }
            foreach (float ly in new[] { 270f, -250f })
            {
                var l = UIKit.Img("Line", _rewardCard, null, new Color(0.95f, 0.78f, 0.15f)).rectTransform;
                l.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, ly), new Vector2(1400, 6));
            }
            var t = UIKit.Label("Title", _rewardCard, Spaced(title), 64, new Color(1f, 0.82f, 0.2f), TextAnchor.MiddleCenter, false);
            t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 330), new Vector2(1600, 90));
            // Up to eight to a row, as many rows as it takes (three rows at most fit between the lines).
            int perRow = Mathf.Min(8, merged.Count), rows = Mathf.Min(3, (merged.Count + 7) / 8);
            float size = rows == 1 ? (merged.Count > 6 ? 140 : 170) : rows == 2 ? 120 : 92, gap = 26, rowH = size + 50;
            for (int i = 0; i < merged.Count && i < 24; i++)
            {
                var it = merged[i];
                int row = i / 8, inRow = Mathf.Min(perRow, merged.Count - row * 8);
                float total = inRow * size + (inRow - 1) * gap;
                var tile = UIKit.Img("Item", _rewardCard, null, new Color(0.14f, 0.18f, 0.18f, 0.95f)).rectTransform;
                tile.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-total / 2 + size / 2 + (i % 8) * (size + gap), 20 + (rows - 1) * rowH / 2 - row * rowH), new Vector2(size, size));
                UIKit.ItemIcon(tile, it.id, size * 0.82f);
                if (it.n > 1) { var n = UIKit.Outlined(UIKit.Label("N", tile, it.n.ToString(), 32, Color.white, TextAnchor.LowerRight), 1.5f); n.rectTransform.Fill(6); }
                var nm = UIKit.Label("Name", tile, Content.NameOf(it.id), rows > 2 ? 18 : 22, new Color(1, 1, 1, 0.8f), TextAnchor.UpperCenter, false);
                nm.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(size + gap, 60));
                tile.localScale = Vector3.one * 0.2f;
                StartCoroutine(PopIn(tile, 0.08f * i));
            }
            var close = UIKit.Button("Close", _rewardCard, "CLOSE", new Color(0, 0, 0, 0), () => { _rewardCard.gameObject.SetActive(false); }, 40);
            ((RectTransform)close.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(300, 80));
            close.GetComponentInChildren<Text>().color = new Color(1f, 0.82f, 0.2f);
        }

        System.Collections.IEnumerator PopIn(RectTransform r, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            for (float t = 0; t < 1 && r != null; t += Time.unscaledDeltaTime * 4)
            {
                float k = 1 + Mathf.Sin(t * Mathf.PI) * 0.25f * (1 - t);
                r.localScale = Vector3.one * Mathf.Lerp(0.2f, 1, Mathf.Clamp01(t * 1.3f)) * k;
                yield return null;
            }
            if (r != null) r.localScale = Vector3.one;
        }

        static string Spaced(string s) { return string.Join(" ", s.ToCharArray()).Replace("   ", "     "); }

        void UpdateSeasonBadge()
        {
            var s = G.Session;
            int n = s.SeasonWaiting + s.EventsBadge;
            _seasonBadge.transform.parent.gameObject.SetActive(n > 0);
            _seasonBadge.text = n > 9 ? "9+" : n.ToString();
        }
    }
}
