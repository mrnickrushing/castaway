using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>Help: how to play, the story so far (chapters), and settings (sound, restore purchases, start over, credits).</summary>
    public sealed partial class Hud
    {
        RectTransform _help, _helpBody;
        bool _helpOpen;
        int _helpTab;

        static readonly (string icon, string title, string text)[] HowTo =
        {
            ("person", "Moving", "Touch the left side of the screen and drag to walk. Let go to stop."),
            ("pin", "Green rings", "Walk into a green ring and let go of the stick: your castaway chops, mines, gathers, builds or drinks there by themselves. An amber ring means you need a tool or room in your backpack."),
            ("heart", "Staying alive", "Health, food and water run down. Eat and drink from the backpack, cook at the campfire, rest in the shelter. If health hits zero you pass out and your backpack stays where you fell."),
            ("pack", "Building and crafting", "Camp blueprints glow green. Stand in their ring with the materials in your backpack to build them. The workbench crafts tools and weapons, the campfire cooks."),
            ("map", "The map", "Take the raft on the beach (or the MAP button) to see the islands. RUN there for energy, or WALK for free and wait. Energy refills over time. Some islands need a higher level."),
            ("radio", "The radio", "Rebuild the ship's radio at camp and tune it in. Survivors call for help: bring them what they need to rescue them and move the story on."),
            ("coin", "Doubloons", "Earn doubloons from treasure, pirates, rescues and levelling up. Spend them at the trading post, to refill energy, to arrive at once or finish a craft. You can buy more in the shop."),
            ("bolt", "Fighting", "Hold a weapon from the backpack and your castaway fights anything that comes close. Boars, crabs and snakes are only the start: Red Jack's raiders hit hard."),
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
            _helpBody = UIKit.Rect("Body", _help).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(1600, 860));
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
            if (_helpTab == 0) HelpHowTo();
            else if (_helpTab == 1) HelpStory();
            else HelpSettings();
        }

        void HelpHowTo()
        {
            for (int i = 0; i < HowTo.Length; i++)
            {
                var (icon, title, text) = HowTo[i];
                int c = i % 2, r = i / 2;
                var row = UIKit.Img("Row", _helpBody, UIKit.Round, new Color(1, 1, 1, 0.06f)).rectTransform;
                row.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(c * 810, -r * 210), new Vector2(790, 196));
                var disc = UIKit.Img("Disc", row, UIKit.Circle, UIKit.Ink);
                disc.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(110, 110));
                var ic = UIKit.Img("Icon", disc.transform, UIKit.Symbol(icon), UIKit.Sand);
                ic.rectTransform.Fill(22);
                var t = UIKit.Label("Title", row, title, 36, UIKit.Amber, TextAnchor.UpperLeft);
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(150, 0); t.rectTransform.offsetMax = new Vector2(-20, -14);
                var b = UIKit.Label("Text", row, text, 25, Color.white, TextAnchor.UpperLeft, false);
                b.rectTransform.Fill(); b.rectTransform.offsetMin = new Vector2(150, 8); b.rectTransform.offsetMax = new Vector2(-20, -60);
            }
        }

        void HelpStory()
        {
            var s = G.Session;
            int qi = s.State.quest;
            var current = Story.ChapterOf(qi);
            for (int i = 0; i < Story.Chapters.Count; i++)
            {
                var ch = Story.Chapters[i];
                int start = Quests.All.FindIndex(q => q.Id == ch.FirstQuest);
                bool reached = qi >= start;
                bool now = ch == current;
                var row = UIKit.Img("Chapter", _helpBody, UIKit.Round, now ? new Color(0.8f, 0.62f, 0.12f, 0.25f) : new Color(1, 1, 1, 0.06f)).rectTransform;
                row.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * 140), new Vector2(1600, 128));
                var t = UIKit.Label("Title", row, (i + 1) + ".  " + (reached ? ch.Title : "???"), 38, reached ? UIKit.Amber : new Color(1, 1, 1, 0.4f), TextAnchor.UpperLeft);
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(30, 0); t.rectTransform.offsetMax = new Vector2(-260, -14);
                var b = UIKit.Label("Text", row, reached ? ch.Summary : "Keep surviving to find out.", 27, Color.white, TextAnchor.UpperLeft, false);
                b.rectTransform.Fill(); b.rectTransform.offsetMin = new Vector2(30, 6); b.rectTransform.offsetMax = new Vector2(-260, -62);
                var status = UIKit.Label("Status", row, now ? "NOW" : reached ? "DONE" : "", 32, now ? UIKit.Amber : UIKit.Green, TextAnchor.MiddleRight);
                status.rectTransform.Fill(); status.rectTransform.offsetMax = new Vector2(-30, 0);
            }
            var replay = UIKit.Button("Replay", _helpBody, "REPLAY THE INTRO", new Color(0.25f, 0.45f, 0.45f), () => { _helpOpen = false; PlayScene(Story.SceneFor("hatchet")); }, 30);
            ((RectTransform)replay.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -40), new Vector2(420, 76));
        }

        void HelpSettings()
        {
            var s = G.Session;
            float y = 0;
            Setting(ref y, "Sound", s.State.muted ? "OFF" : "ON", () => { s.State.muted = !s.State.muted; Sfx.Muted = s.State.muted; RefreshHelp(); });
            Setting(ref y, "Restore purchases", "RESTORE", RestorePurchases);
            Setting(ref y, "Shop", "OPEN", () => { _helpOpen = false; OpenShop(); });
            Setting(ref y, "Start a new game (erases this one)", "START OVER", () => Confirm("Start over?", "Your island, camp and backpack will be gone for good. Purchases stay with your Apple account and can be restored.", "START OVER", () =>
            {
                SaveSystem.Delete();
                GameRoot.Instance.Restart();
            }));
            var credits = UIKit.Label("Credits", _helpBody,
                "LAST TIDE  ·  a Rushing Technologies game\n" +
                "3D models: Kenney (kenney.nl) Pirate, Survival and Nature kits, and Quaternius (quaternius.com) pirate characters, all CC0. " +
                "Castaway, boar and palm models, icons and paintings made for this game. Fonts: Barlow (SIL Open Font License).",
                26, new Color(1, 1, 1, 0.6f), TextAnchor.UpperLeft, false);
            credits.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y - 30), new Vector2(1600, 200));
        }

        void Setting(ref float y, string label, string button, System.Action act)
        {
            var row = UIKit.Img("Row", _helpBody, UIKit.Round, new Color(1, 1, 1, 0.06f)).rectTransform;
            row.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y), new Vector2(1600, 110));
            var t = UIKit.Label("Label", row, label, 36, Color.white, TextAnchor.MiddleLeft);
            t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(30, 0);
            var b = UIKit.Button("Button", row, button, new Color(0.25f, 0.45f, 0.45f), act, 30);
            ((RectTransform)b.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(300, 80));
            y += 124;
        }
    }
}
