using System.Collections.Generic;

namespace LastTide.Core
{
    public sealed class StoryLine
    {
        public string Who, Text, Art;
        public StoryLine(string who, string text, string art = null) { Who = who; Text = text; Art = art; }
    }

    /// <summary>A short scene of story cards: who speaks, what they say, the picture behind them.</summary>
    public sealed class SceneDef
    {
        public string Id, Title;
        public StoryLine[] Lines;
    }

    /// <summary>A chapter of the story: the guide steps it spans and what it's about (for the journal).</summary>
    public sealed class ChapterDef
    {
        public string Title, Summary, FirstQuest;
    }

    /// <summary>
    /// The story of Last Tide: the merchant brig Gull, driven onto the reef by Red Jack's raiders; a castaway who
    /// builds a camp, rebuilds the ship's radio and answers the calls of other survivors; and the Last Tide, the one
    /// night a year a ship can cross the reef. Scenes play when the guide reaches their step.
    /// </summary>
    public static class Story
    {
        const string You = "You", Tale = "";

        public static readonly List<ChapterDef> Chapters = new List<ChapterDef>
        {
            new ChapterDef { Title = "Washed Ashore", FirstQuest = "hatchet", Summary = "The Gull is lost. Alone on Wreck Cove, you make fire, shelter and tools from what the sea gave back." },
            new ChapterDef { Title = "Voices on the Radio", FirstQuest = "map", Summary = "A raft, a map of the islands and a waterlogged radio. Someone out there is calling for help." },
            new ChapterDef { Title = "The Fisherman's Pier", FirstQuest = "pier", Summary = "Old Tomas speaks of Red Jack and of the Last Tide, the one night ships can cross the reef. Mara at the pier knows the tides." },
            new ChapterDef { Title = "Chains in the Dark", FirstQuest = "captive", Summary = "Mara's brother Ned is held in the smugglers' cave, guarded by men the sea gave back wrong." },
            new ChapterDef { Title = "The Last Tide", FirstQuest = "powder", Summary = "Powder for the palisade, a reckoning with Red Jack, and a ship to sail home on the Last Tide." },
            new ChapterDef { Title = "Captain of the Islands", FirstQuest = "free", Summary = "The first voyage is over. The islands are still yours to explore, build on and survive." },
        };

        /// <summary>Scenes keyed by the guide step they open ("intro" plays on a new game).</summary>
        public static readonly Dictionary<string, SceneDef> Scenes = new Dictionary<string, SceneDef>
        {
            { "hatchet", new SceneDef { Id = "intro", Title = "Washed Ashore", Lines = new[] {
                new StoryLine(Tale, "Three weeks out of Port Royal, the merchant brig Gull carried sugar, salt and forty souls.", "keyart"),
                new StoryLine(Tale, "Then the black sails came. Red Jack's raiders chased her into a storm, and the reef did the rest.", "pirates"),
                new StoryLine(Tale, "You wake on warm sand. The Gull lies broken in the shallows. No one answers when you call.", "home"),
                new StoryLine(You, "Alright. Fire, water, shelter. One thing at a time. There was a hatchet in the bosun's locker...", "home"),
            } } },
            { "fiber", new SceneDef { Id = "fire", Title = "First Night", Lines = new[] {
                new StoryLine(You, "A fire. The first thing that's gone right since the storm.", "home"),
                new StoryLine(You, "The nights here will be cold, and the rain comes hard. I need a roof over my head.", "home"),
            } } },
            { "hunt", new SceneDef { Id = "tracks", Title = "Tracks", Lines = new[] {
                new StoryLine(You, "Fresh tracks, and the ferns torn up. Something big roots about in the jungle.", "grove"),
                new StoryLine(You, "Meat for the fire... if it doesn't make a meal of me first.", "grove"),
            } } },
            { "map", new SceneDef { Id = "raft", Title = "The Raft", Lines = new[] {
                new StoryLine(You, "The Gull's raft held together. Lash on a sail and it'll carry me between the islands.", "home"),
                new StoryLine(You, "There's land on every horizon. If anyone else made it ashore, they're out there.", "shore"),
            } } },
            { "radio", new SceneDef { Id = "radio", Title = "The Ship's Radio", Lines = new[] {
                new StoryLine(You, "The ship's radio. Waterlogged, but the valves look whole.", "home"),
                new StoryLine(You, "Scrap for the casing, rope for an aerial, sailcloth to keep the rain off. Then we'll see who's listening.", "home"),
            } } },
            { "keeper", new SceneDef { Id = "tomas", Title = "Someone's Alive", Lines = new[] {
                new StoryLine(You, "A voice. Someone else is alive out there.", "grove"),
                new StoryLine(You, "Hold on, Tomas. I'm coming, and I'm bringing bandages.", "grove"),
            } } },
            { "pier", new SceneDef { Id = "lasttide", Title = "The Last Tide", Lines = new[] {
                new StoryLine("Old Tomas", "Red Jack... he's ruled these islands for years. He hunts the shipwrecked for his crew. The ones who won't join, he leaves for the sharks.", "pirates"),
                new StoryLine("Old Tomas", "There's only one way off. Once a year the Last Tide lifts a ship clean over the reef. Jack's ship will sail on it.", "lighthouse"),
                new StoryLine("Old Tomas", "There's a woman at the old pier, Mara. If anyone knows when the tide comes, she does. Get strong first. Jack's men don't play fair.", "pier"),
            } } },
            { "captive", new SceneDef { Id = "mara", Title = "Mara", Lines = new[] {
                new StoryLine("Mara", "The Last Tide is close. A few weeks, no more. Jack keeps his powder in the smugglers' cave and his ship at the camp.", "pier"),
                new StoryLine("Mara", "His men dragged my brother Ned into that cave. And the ones who guard it now... they came out of the sea. Don't laugh.", "cave"),
                new StoryLine(You, "I'm not laughing. I'll bring him back.", "cave"),
            } } },
            { "powder", new SceneDef { Id = "ned", Title = "Ned's Plan", Lines = new[] {
                new StoryLine("Ned Barlow", "Thank you, friend. I thought I'd rot down there.", "cave"),
                new StoryLine("Ned Barlow", "Jack won't hand over his ship. But with enough powder we can blow his palisade wide open.", "pirates"),
                new StoryLine("Ned Barlow", "Bring six kegs of gunpowder. The drowned men in the cave and Jack's raiders carry plenty. Then we go for the ship.", "pirates"),
            } } },
            { "redjack", new SceneDef { Id = "assault", Title = "Red Jack", Lines = new[] {
                new StoryLine("Ned Barlow", "That's enough powder to wake the dead. Again.", "pirates"),
                new StoryLine("Ned Barlow", "Jack sits by his fire at the heart of the camp. Bring him down, and the ship is ours.", "pirates"),
                new StoryLine(You, "Eat well, mend your wounds, sharpen your blade. He won't go easy.", "pirates"),
            } } },
            { "sail", new SceneDef { Id = "jackfalls", Title = "The Tide Turns", Lines = new[] {
                new StoryLine("Red Jack", "The tide... was mine...", "pirates"),
                new StoryLine(You, "Not anymore. The ship is anchored off the camp. Time to go home.", "pirates"),
            } } },
            { "free", new SceneDef { Id = "ending", Title = "The Last Tide", Lines = new[] {
                new StoryLine(Tale, "On the night of the Last Tide, the black ship slipped over the reef with a new crew.", "pirates"),
                new StoryLine(Tale, "Tomas at the helm, Mara on the lines, Ned at the bow. And you, who came ashore with nothing.", "lighthouse"),
                new StoryLine(Tale, "Behind you the islands glowed under the moon. You will be back. Some places don't let go of you.", "keyart"),
                new StoryLine(Tale, "THE END of the first voyage. The islands are still yours: keep building, exploring and surviving.", "home"),
            } } },
        };

        /// <summary>The scene that opens the guide step <paramref name="questId"/>, or null.</summary>
        public static SceneDef SceneFor(string questId) { SceneDef s; return Scenes.TryGetValue(questId, out s) ? s : null; }

        /// <summary>The chapter a guide step belongs to.</summary>
        public static ChapterDef ChapterOf(int questIndex)
        {
            ChapterDef best = Chapters[0];
            foreach (var c in Chapters)
            {
                int start = Quests.All.FindIndex(q => q.Id == c.FirstQuest);
                if (start >= 0 && questIndex >= start) best = c;
            }
            return best;
        }
    }
}
