#!/usr/bin/env python3
"""Loading key art and story movies (Higgsfield): GPT Image 2.5 stills, then Seedance 2.0 image-to-video with sound.

  python3 Tools/art/ai_media.py still [id...]   # 16:9 still (2.75 credits; skips ids that already have one)
  python3 Tools/art/ai_media.py video [id...]   # animate the approved still, 5 s 720p with audio (22.5 credits; skips existing)
  python3 Tools/art/ai_media.py install         # copy finished media into the Unity project

Review each still before paying for its clip. Work files go to art-src/media/. Never re-run a paid step for an id that
already has its output; check `higgsfield account transactions` before retrying a failed one.
Everything is original: no real ships, brands, logos or text in frame. The movies play before the story scene whose
SceneDef.Movie names them (Core/Story.cs); `loading` is the loading-screen art.
"""
import os
import shutil
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ai_assets import ROOT, fetch, hf  # noqa: E402

SRC = os.path.join(ROOT, "art-src", "media")
STYLE = ("Cinematic semi-realistic 3D render like premium mobile survival game key art, a tropical archipelago in the age "
         "of sail, rich natural color, dramatic light, crisp detail, wide 16:9 film composition. No text, no letters, no logos, "
         "no watermarks, no borders.")
HERO = "a young shipwrecked sailor in a torn off-white linen shirt, rolled dark trousers and a red sash, barefoot, seen from behind or in profile"

M = {
    "loading": dict(still="Epic key art: on a white-sand tropical beach at golden hour, " + HERO + " stands beside a small campfire looking "
                          "out at the broken hull of a two-masted wooden brig wrecked on a coral reef in a turquoise lagoon; a ship with "
                          "black sails on the far horizon; jungle, palms and green volcanic peaks to the side; open sky in the upper third. " + STYLE),
    "wreck": dict(still="Night storm at sea: a two-masted wooden merchant brig smashing onto a jagged coral reef in huge white-capped waves, "
                        "torn sails, a snapped mast, lightning splitting a black sky, and a ship with black sails looming in the rain behind it. " + STYLE,
                  motion="Huge waves crash over the brig's deck as it grinds onto the reef; the main mast cracks and topples into the sea; "
                         "lightning flashes; rain lashes sideways; the black-sailed ship turns away into the dark; slow dramatic push-in. "
                         "Sound: roaring storm, thunder, cracking timber."),
    "firstnight": dict(still="Night on a tropical beach under a sky full of stars: " + HERO + " kneels beside a small newly lit campfire of "
                             "driftwood ringed with stones; the dark silhouette of the wrecked brig on the reef across the moonlit water. " + STYLE,
                       motion="The campfire catches and flares up, sparks spiral into the starry sky, the sailor holds his hands to the warmth; "
                              "gentle surf glitters in the moonlight; slow orbit around the fire. Sound: crackling fire, soft surf, night insects."),
    "raft": dict(still="Dawn on a calm turquoise lagoon: " + HERO + " pushes a lashed log raft with a small patched canvas sail off the "
                       "white beach into the water; distant green islands on the horizon under a pink and gold sky. " + STYLE,
                 motion="The sailor shoves the raft into the water and climbs aboard; the patched sail fills with wind and the raft glides "
                        "out across the lagoon toward the islands; seabirds wheel overhead; camera rises and pulls back. "
                        "Sound: lapping water, wind in the sail, gulls."),
    "radio": dict(still="Inside a palm-thatched beach shelter at dusk, rain falling outside: close on a battered old wooden ship's wireless "
                        "set with brass dials and glass valves on a crate, the sailor's hands twisting wires into it, a lantern glowing. " + STYLE,
                  motion="The radio's glass valves flicker and glow warm orange, the dial needle swings, the sailor leans in to listen; rain "
                         "drips from the thatch; slow push-in on the glowing dial. Sound: crackling static, rain, a faint distant voice."),
    "lasttide": dict(still="Moonlit night over a jagged coral reef: the sea has drawn far back, revealing a narrow channel through the reef; "
                           "an old stone lighthouse on a rocky headland; a black-sailed pirate ship at anchor in the bay; silver moonlight. " + STYLE,
                     motion="A great tide surges back in and floods over the reef, lifting the water high through the channel; the lighthouse "
                            "beam sweeps across the sea; moonlight shimmers on the waves; slow aerial drift. Sound: rushing water, wind."),
    "redjack": dict(still="Night assault on a pirate camp: a wooden palisade with spiked logs and burning torches on a jungle shore, kegs of "
                          "gunpowder stacked at its gate, the black-sailed pirate ship anchored in the bay behind, firelight and smoke. " + STYLE,
                    motion="The powder kegs explode and blow the palisade gate apart in a burst of fire and splinters; sparks and smoke billow; "
                           "torches topple; the pirates' silhouettes scatter; the camera shakes and pushes through the gap. "
                           "Sound: a huge explosion, shouting, crackling fire."),
    "ending": dict(still="Sunrise: a tall ship with black sails glides over a coral reef on a high tide, leaving the archipelago behind; "
                         "green islands glow gold in the dawn light; seabirds; a small figure at the bow looking toward the open sea. " + STYLE,
                   motion="The ship sails over the reef and out to the open sea as the sun rises; the sails billow and catch the light; "
                          "spray at the bow; the islands slip behind; slow sweeping tracking shot. Sound: wind, waves, creaking rigging."),
}


# Story-card paintings (Resources/UI/Story/<key>.jpg): full-screen backdrops behind the story lines, art keys in Core/Story.cs.
# Keys without one here fall back to a movie still (STORY_FROM) or the small map painting.
STORY = {
    "story_home": "The castaway's camp on a tropical beach in warm afternoon light: a palm-thatch lean-to shelter, a stone-ringed campfire, "
                  "a workbench of driftwood with tools, crates and a sea chest, palms, and the wrecked two-masted brig on the reef beyond. " + STYLE,
    "story_grove": "A shady jungle grove of tall palms and ferns: an old grey-bearded fisherman in a faded blue coat and straw hat sits wounded "
                   "against a palm trunk, a bandage on his leg, a battered tin radio receiver beside him, shafts of sunlight through the leaves. " + STYLE,
    "story_shore": "A wild beach piled with bleached driftwood and wreckage after a storm, foam on the sand, and a small ginger-and-white "
                   "shiba-like dog standing on a driftwood log barking toward the viewer, the sea and distant islands behind. " + STYLE,
    "story_pier": "An old weathered wooden fishing pier on stilts reaching into a turquoise bay at golden hour, nets and lobster pots, a "
                  "fishing hut; a determined young woman with dark braided hair, a sailcloth shirt and a knife at her belt stands on it. " + STYLE,
    "story_jungle": "Deep tropical jungle in green morning light: huge ferns and broad leaves torn up and trampled, fresh hoof prints in "
                    "the mud, and a big bristly wild boar with curved tusks half hidden in the undergrowth, staring at the viewer. " + STYLE,
    "story_cave": "Inside a sea cave used by smugglers: torchlight on wet rock, stacked barrels and crates of gunpowder, chains on the wall, "
                  "seawater pooling on the floor, and the ragged silhouettes of drowned sailors shambling out of the dark. " + STYLE,
}
# Story art key -> painting id.
STORY_KEYS = {"home": "story_home", "tomas": "story_grove", "shore": "story_shore", "pier": "story_pier", "cave": "story_cave",
              "jungle": "story_jungle"}
# Story keys that reuse a movie still or the loading art instead of a painting of their own.
STORY_FROM = {"keyart": "loading", "pirates": "redjack", "lighthouse": "lasttide", "wreck": "wreck"}


def story(i):
    p = os.path.join(SRC, i + ".png")
    if os.path.exists(p):
        return print("have", p)
    url = hf("generate", "create", "gpt_image_2_5", "--aspect_ratio", "16:9", "--quality", "high", "--resolution", "2k",
             "--background", "opaque", "--prompt", STORY[i], "--wait")
    fetch(url, p)
    print("story", i, url)


def install_story():
    from PIL import Image
    d = os.path.join(ROOT, "Assets", "Resources", "UI", "Story")
    os.makedirs(d, exist_ok=True)
    pairs = list(STORY_KEYS.items()) + list(STORY_FROM.items())
    for key, src in pairs:
        p = os.path.join(SRC, src + ".png")
        if not os.path.exists(p):
            continue
        im = Image.open(p).convert("RGB")
        im.thumbnail((1920, 1920))
        im.save(os.path.join(d, key + ".jpg"), quality=86)
    print("installed story art")


def still(i):
    p = os.path.join(SRC, i + ".png")
    if os.path.exists(p):
        return print("have", p)
    url = hf("generate", "create", "gpt_image_2_5", "--aspect_ratio", "16:9", "--quality", "high", "--resolution", "2k",
             "--background", "opaque", "--prompt", M[i]["still"], "--wait")
    fetch(url, p)
    print("still", i, url)


def video(i):
    if "motion" not in M[i]:
        return print("no video for", i)
    p, v = os.path.join(SRC, i + ".png"), os.path.join(SRC, i + ".mp4")
    if os.path.exists(v):
        return print("have", v)
    if not os.path.exists(p):
        return print("no still for", i)
    url = hf("generate", "create", "seedance_2_0", "--start-image", p, "--prompt", M[i]["motion"], "--duration", "5",
             "--resolution", "720p", "--aspect_ratio", "16:9", "--generate_audio", "true", "--wait", "--wait-timeout", "25m")
    fetch(url, v)
    print("video", i, url)


def install():
    """Clips -> StreamingAssets/Videos (H.264 for phones) + VP8 copies in Library/ for the Linux Editor;
    stills -> Resources/UI/Cinematics (shown until the first frame); loading art -> Resources/UI/LoadingArt.jpg."""
    from PIL import Image
    vid_dir = os.path.join(ROOT, "Assets", "StreamingAssets", "Videos")
    os.makedirs(vid_dir, exist_ok=True)
    for i in M:
        v = os.path.join(SRC, i + ".mp4")
        if os.path.exists(v):
            out = os.path.join(vid_dir, i + ".mp4")
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", v, "-vf", "scale=1280:-2", "-c:v", "libx264",
                            "-profile:v", "main", "-crf", "24", "-preset", "slow", "-pix_fmt", "yuv420p", "-c:a", "aac",
                            "-b:a", "128k", "-movflags", "+faststart", out], check=True)
            lib = os.path.join(ROOT, "Library", "EditorVideos")
            os.makedirs(lib, exist_ok=True)
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", v, "-vf", "scale=1280:-2", "-c:v", "libvpx", "-b:v", "2M",
                            "-c:a", "libvorbis", os.path.join(lib, i + ".webm")], check=True)
            print("installed", out)
        p = os.path.join(SRC, i + ".png")
        if not os.path.exists(p):
            continue
        im = Image.open(p).convert("RGB")
        if i == "loading":
            im.thumbnail((2048, 2048))
            im.save(os.path.join(ROOT, "Assets", "Resources", "UI", "LoadingArt.jpg"), quality=88)
            print("installed loading art")
        else:
            d = os.path.join(ROOT, "Assets", "Resources", "UI", "Cinematics")
            os.makedirs(d, exist_ok=True)
            im.thumbnail((1280, 1280))
            im.save(os.path.join(d, i + ".jpg"), quality=85)


if __name__ == "__main__":
    if not shutil.which("higgsfield") and sys.argv[1] != "install":
        sys.exit("higgsfield CLI not found")
    os.makedirs(SRC, exist_ok=True)
    cmd, ids = sys.argv[1], sys.argv[2:] or list(M)
    if cmd == "install":
        install()
        install_story()
    elif cmd == "story":           # ai_media.py story [story_home ...]   (2.75 credits each)
        for i in (sys.argv[2:] or list(STORY)):
            try:
                story(i)
            except Exception as e:
                print("FAILED", i, e)
    else:
        for i in ids:
            try:
                {"still": still, "video": video}[cmd](i)
            except Exception as e:
                print("FAILED", i, e)
