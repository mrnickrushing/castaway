using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>Help: how to play, the story so far (chapters), and settings (sound, restore purchases, start over, credits).</summary>
    public sealed partial class Hud
    {
        RectTransform _help, _helpBody, _helpIn; // _helpIn: where a tab lays out (a scrolling page on phones)
        bool _helpOpen;
        int _helpTab;

        static readonly (string icon, string title, string text)[] HowTo =
        {
            ("person", "Moving", "Touch the left side of the screen and drag to walk. Let go to stop."),
            ("pin", "Green rings", "Walk into a green ring and let go of the stick: your castaway chops, mines, gathers, builds or drinks there by themselves. An amber ring means you need a tool or room in your backpack. The hand button walks you to the nearest ring; sailors' bags open a loot window (TAKE ALL)."),
            ("heart", "Staying alive", "Health, food and water run down. Eat and drink from the backpack, cook at the campfire, rest in the shelter. Nights are dark and animals hunt farther: stay by the fire or sneak. If health hits zero you pass out and your backpack stays where you fell."),
            ("pack", "Backpack and crafting", "Pockets (the top row) stay with you when you pass out. CRAFT opens the blueprints: some things are made by hand, others at the campfire, workbench (wood into planks) or furnace. Stand at a station to cook, smelt and upgrade it."),
            ("build", "Building your base", "Story blueprints glow green: stand in their ring to build them. BUILD at camp lets you lay floors, walls, doors and windows on the grid, and add boxes, a bed, a furnace and more. Tap a piece in build mode to take it down."),
            ("map", "The map", "Take the raft on the beach (or the MAP button) to see the islands. SAIL there fast for energy, or PADDLE for free and wait. Energy refills over time. Some islands need a higher level."),
            ("radio", "The radio", "Rebuild the ship's radio at camp and tune it in. Survivors call for help: bring them what they need to rescue them and move the story on."),
            ("coin", "Doubloons", "Earn doubloons from treasure, pirates, rescues and leveling up, or SELL pearls from the backpack. Spend them at the trading post (the gift button), to refill energy, to arrive at once, finish a craft or recover a lost backpack. You can buy more in the shop."),
            ("bolt", "Fighting", "With AUTO on your castaway fights anything that comes close; turn it off and tap the weapon button to strike, the target button to pick an enemy. Sneak (the crouching button) to slip past. Biscuit the dog fights at your side once you find him. Wear clothes for armor."),
            ("season", "Season and events", "The star-shield button at the top opens the season: everything that earns experience fills the season bar, and each level pays a free reward (premium pass: a second one, with companions). EVENTS shows the week's calendar: each event counts one thing, like boars hunted or wood gathered, and pays at three milestones."),
            ("path", "Survivor's Path", "Every finished step of the guide pays a reward: claim it on the PATH screen. The Ultra pass adds a golden reward to every step. LEVEL UP! under the quest card collects the gifts for each new level."),
        };

        void BuildHelp()
        {
            _help = UIKit.Img("Help", _root, null, new Color(0.1f, 0.13f, 0.13f, 1f)).rectTransform;
            _help.Fill();
            _help.GetComponent<Image>().raycastTarget = true;
            var x = UIKit.Button("Close", _help, "X", new Color(0, 0, 0, 0), () => { _helpOpen = false; _dirty = true; }, 80);
            ((RectTransform)x.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -30), new Vector2(120, 120));
            string[] tabs = { "HOW TO PLAY", "STORY", "SETTINGS" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int k = i;
                var b = UIKit.Button("Tab", _help, tabs[i], new Color(1, 1, 1, 0.1f), () => { _helpTab = k; RefreshHelp(); }, 34);
                ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - 1) * 380, -40), new Vector2(360, 84));
            }
            // Phones get the full width of their wide screen and a page that scrolls, so the type can be large.
            _helpBody = UIKit.Rect("Body", _help).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), UIKit.Phone ? new Vector2(1900, 910) : new Vector2(1600, 860));
            _help.gameObject.SetActive(false);
        }

        public void OpenHelp(int tab = 0)
        {
            _helpOpen = true;
            _helpTab = tab;
            Sfx.Play("click");
            RefreshHelp();
        }

        void RefreshHelp()
        {
            _help.gameObject.SetActive(_helpOpen);
            if (!_helpOpen) return;
            for (int i = 0; i < 3; i++)
            {
                var tab = _help.GetChild(i + 1).GetComponent<Image>();
                tab.color = i == _helpTab ? new Color(0.8f, 0.62f, 0.12f) : new Color(1, 1, 1, 0.1f);
            }
            Clear(_helpBody);
            _helpIn = UIKit.Scroller(_helpBody); // pages that fit don't move; Settings with all its rows scrolls on an iPad too
            if (_helpTab == 0) HelpHowTo();
            else if (_helpTab == 1) HelpStory();
            else HelpSettings();
        }

        void HelpHowTo()
        {
            // Three columns, four rows: every card fits the 1600 x 860 body, text shrinks to its card if it must.
            // Phones: two wide columns of big type on a page that scrolls.
            bool phone = UIKit.Phone;
            int cols = phone ? 2 : 3;
            float cw = phone ? (_helpBody.sizeDelta.x - 20) / 2 : 525, ch = phone ? 300 : 205;
            for (int i = 0; i < HowTo.Length; i++)
            {
                var (icon, title, text) = HowTo[i];
                int c = i % cols, r = i / cols;
                var row = UIKit.Img("Row", _helpIn, UIKit.Round, new Color(1, 1, 1, 0.06f)).rectTransform;
                row.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(c * (cw + 15), -r * (ch + 12)), new Vector2(cw, ch));
                var disc = UIKit.Img("Disc", row, UIKit.Circle, UIKit.Ink);
                disc.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -14), new Vector2(62, 62));
                var ic = UIKit.Img("Icon", disc.transform, UIKit.Symbol(icon), UIKit.Sand);
                ic.rectTransform.Fill(12);
                var t = UIKit.Label("Title", row, title, 32, UIKit.Amber, TextAnchor.MiddleLeft);
                t.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(90, -14), new Vector2(cw - 105, 62));
                var b = UIKit.Label("Text", row, text, 22, Color.white, TextAnchor.UpperLeft, false);
                b.rectTransform.Fill(); b.rectTransform.offsetMin = new Vector2(16, 8); b.rectTransform.offsetMax = new Vector2(-14, -84);
                b.resizeTextForBestFit = true; b.resizeTextMinSize = phone ? 26 : 15; b.resizeTextMaxSize = phone ? 32 : 22; b.fontSize = b.resizeTextMaxSize;
                b.verticalOverflow = VerticalWrapMode.Truncate;
            }
            if (phone) UIKit.ScrollHeight(_helpIn, (HowTo.Length + cols - 1) / cols * (ch + 12) + 20);
        }

        void HelpStory()
        {
            var s = G.Session;
            int qi = s.State.quest;
            var current = Story.ChapterOf(qi);
            float step = UIKit.Phone ? 190 : 140; // a phone's larger type takes two lines for a summary
            for (int i = 0; i < Story.Chapters.Count; i++)
            {
                var ch = Story.Chapters[i];
                int start = Quests.All.FindIndex(q => q.Id == ch.FirstQuest);
                bool reached = qi >= start;
                bool now = ch == current;
                var row = UIKit.Img("Chapter", _helpIn, UIKit.Round, now ? new Color(0.8f, 0.62f, 0.12f, 0.25f) : new Color(1, 1, 1, 0.06f)).rectTransform;
                row.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * step), new Vector2(_helpBody.sizeDelta.x, step - 12));
                var t = UIKit.Label("Title", row, (i + 1) + ".  " + (reached ? ch.Title : "???"), 38, reached ? UIKit.Amber : new Color(1, 1, 1, 0.4f), TextAnchor.UpperLeft);
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(30, 0); t.rectTransform.offsetMax = new Vector2(-260, -14);
                var b = UIKit.Label("Text", row, reached ? ch.Summary : "Keep surviving to find out.", 27, Color.white, TextAnchor.UpperLeft, false);
                b.rectTransform.Fill(); b.rectTransform.offsetMin = new Vector2(30, 6); b.rectTransform.offsetMax = new Vector2(-260, -62);
                var status = UIKit.Label("Status", row, now ? "NOW" : reached ? "DONE" : "", 32, now ? UIKit.Amber : UIKit.Green, TextAnchor.MiddleRight);
                status.rectTransform.Fill(); status.rectTransform.offsetMax = new Vector2(-30, 0);
            }
            var replay = UIKit.Button("Replay", _helpIn, "REPLAY THE INTRO", new Color(0.25f, 0.45f, 0.45f), () => { _helpOpen = false; PlayScene(Story.SceneFor("hatchet")); }, 30);
            if (UIKit.Phone)
            {
                // At the end of the scrolling page (the view masks anything below it).
                ((RectTransform)replay.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -Story.Chapters.Count * step - 10), new Vector2(420, 80));
                UIKit.ScrollHeight(_helpIn, Story.Chapters.Count * step + 110);
            }
            else ((RectTransform)replay.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -40), new Vector2(420, 76));
        }

        /// <summary>The game's web pages (GitHub Pages, branch gh-pages): privacy policy and support.</summary>
        public const string Site = "https://mrnickrushing.github.io/castaway/";

        void HelpSettings()
        {
            var s = G.Session;
            float y = 0;
            Setting(ref y, "Name: " + s.DisplayName, "CHANGE", AskName);
            Setting(ref y, "Sound", s.State.muted ? "OFF" : "ON", () => { s.State.muted = !s.State.muted; Sfx.Muted = s.State.muted; RefreshHelp(); });
            Setting(ref y, "Battery saver: 30 frames a second, a cooler phone", FrameBudget.BatterySaver ? "ON" : "OFF", () => { FrameBudget.BatterySaver = !FrameBudget.BatterySaver; RefreshHelp(); });
            var pets = s.OwnedPets;
            if (pets.Count > 1)
                Setting(ref y, "Companion: " + s.ActivePet.Name, "CHANGE", () =>
                {
                    int i = pets.FindIndex(p => p.Id == s.ActivePet.Id);
                    s.ChoosePet(pets[(i + 1) % pets.Count].Id);
                    RefreshHelp();
                });
            // Sign in with Apple: purchases follow the Apple ID to any device.
            if (AppleAccount.SignedIn) Setting(ref y, "Signed in with Apple: purchases follow your Apple ID", "SIGN OUT", () => { AppleAccount.SignOut(); RefreshHelp(); });
            else AppleSignIn(Setting(ref y, "Keep your purchases with your Apple ID", "Sign in with Apple", () =>
                AppleAccount.SignIn((ok, msg) => { if (!string.IsNullOrEmpty(msg)) Toast(msg); RefreshHelp(); })));
            Setting(ref y, CloudLine(), "SAVE NOW", () =>
            {
                if (!CloudSave.Available) { Toast("Sign in to iCloud in your iPhone's Settings to back up your island"); return; }
                GameRoot.Instance.Save(); CloudSave.Push(s.State, true); Toast("Saved to iCloud"); RefreshHelp();
            });
            Setting(ref y, "Reminders (raft landed, energy full)", s.State.noReminders ? "OFF" : "ON", () => { s.State.noReminders = !s.State.noReminders; if (!s.State.noReminders) Reminders.AskOnce(); RefreshHelp(); });
            Setting(ref y, "Restore purchases", "RESTORE", RestorePurchases);
            Setting(ref y, "Shop", "OPEN", () => { _helpOpen = false; OpenShop(); });
            Setting(ref y, "Privacy policy and support", "OPEN", () => Application.OpenURL(Site));
            Setting(ref y, "Start a new game (erases this one)", "START OVER", () => Confirm("Start over?", "Your island, camp and backpack will be gone for good. Purchases stay with your Apple account and can be restored.", "START OVER", () =>
            {
                SaveSystem.Delete();
                GameRoot.Instance.Restart();
            }));
            Setting(ref y, "Delete your account and data", "DELETE", ConfirmDeleteAccount);
            var credits = UIKit.Label("Credits", _helpIn,
                "LAST TIDE  ·  a Rushing Technologies game\n" +
                "Photoscanned models, ground and sky: Poly Haven (polyhaven.com) and ambientCG (ambientcg.com), CC0. " +
                "3D models: Kenney (kenney.nl) and Quaternius (quaternius.com), CC0; animations: Quaternius Universal Animation Library, CC0. " +
                "From Sketchfab (sketchfab.com), CC BY 4.0, adapted for the game: characters \"Castaway\" by katarzynaszemla, " +
                "\"Pirate Captain\" by byenbyen, \"Pirate Hunter\" by chenuka_wy (jacket \"Gambeson\" by Leon.Steiner), \"Pirate Girl\" by Anriil_Kl, " +
                "\"Farmer\" by LxNazarov, \"Svend\" and \"Revna\" by chambersu1996, \"Skeleton Pirate\" captain and drunk by ShadowCoffee; " +
                "props \"Old Rowboat\" by toomanydemons, \"Lighthouse\" by stnabeal, \"Raft\" by _SeF_ and \"Iron Ore\" by vxdesigns; " +
                "gulls \"Low-Poly Seagull\" by simonaskLDE. " +
                "Crab and snake: scans by ffishAsia-and-floraZia, CC0. Dog bark: \"Ladrido de un perro\" by Edo.pt2 (Wikimedia Commons), CC0. " +
                "Story art, movies, icons, boar and palms made for this game. Fonts: Barlow (SIL Open Font License).",
                26, new Color(1, 1, 1, 0.6f), TextAnchor.UpperLeft, false);
            // The page scrolls: the credits take the room they need at full size.
            credits.resizeTextForBestFit = false;
            credits.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y - 30), new Vector2(_helpBody.sizeDelta.x, 100));
            float h = credits.preferredHeight + 20;
            credits.rectTransform.sizeDelta = new Vector2(_helpBody.sizeDelta.x, h);
            UIKit.ScrollHeight(_helpIn, y + 30 + h + 20);
        }

        /// <summary>"iCloud save: saved 3 min ago", or why there's none.</summary>
        static string CloudLine()
        {
            if (!CloudSave.Available) return "iCloud save: sign in to iCloud on this device to back up";
            double at = CloudSave.LastPushed;
            if (at <= 0) return "iCloud save: not yet";
            double ago = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - at;
            return "iCloud save: " + (ago < 90 ? "just now" : ago < 3600 ? Mathf.RoundToInt((float)(ago / 60)) + " min ago" : ago < 86400 ? Mathf.RoundToInt((float)(ago / 3600)) + " h ago" : "over a day ago");
        }

        static Font _systemFont;

        /// <summary>The row's button as Apple's black "Sign in with Apple" button: mixed case and the Apple logo, in the
        /// iOS system font (which has the logo glyph); elsewhere the game font without it.</summary>
        static void AppleSignIn(Button b)
        {
            b.GetComponent<Image>().color = Color.black;
            var t = b.GetComponentInChildren<Text>();
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                if (_systemFont == null) _systemFont = Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Helvetica" }, 30);
                t.font = _systemFont;
                t.text = "\uF8FF  Sign in with Apple";
            }
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 20;
        }

        /// <summary>Account deletion (App Store guideline 5.1.1(v)): ends the Sign in with Apple link, unlinks purchases
        /// from it and erases the island here and in iCloud. Last Tide keeps nothing on a server of its own.</summary>
        void ConfirmDeleteAccount()
        {
            Confirm("Delete your account?",
                "This signs you out of Sign in with Apple, unlinks your purchases and erases your island, camp and backpack on this device and in iCloud. "
                + "Purchases come back with Restore purchases. This cannot be undone.\n<size=26>To remove the app from your Apple ID too: iPhone Settings > your name > Sign in with Apple.</size>",
                "DELETE", () =>
                {
                    CloudSave.Erase();
                    AppleAccount.SignOut();
                    SaveSystem.Delete();
                    PlayerPrefs.DeleteAll();
                    PlayerPrefs.Save();
                    GameRoot.Instance.Restart();
                }, null, "KEEP IT");
        }

        Button Setting(ref float y, string label, string button, System.Action act)
        {
            var row = UIKit.Img("Row", _helpIn, UIKit.Round, new Color(1, 1, 1, 0.06f)).rectTransform;
            row.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y), new Vector2(_helpBody.sizeDelta.x, 96));
            var t = UIKit.Label("Label", row, label, 36, Color.white, TextAnchor.MiddleLeft);
            t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(30, 0);
            var b = UIKit.Button("Button", row, button, new Color(0.25f, 0.45f, 0.45f), act, 30);
            ((RectTransform)b.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(300, 72));
            t.rectTransform.offsetMax = new Vector2(-340, 0); // long labels stop short of the button
            y += 106;
            return b;
        }
    }
}
