#!/usr/bin/env python3
"""Free (CC0) models for Last Tide: Kenney's Pirate, Survival and Nature kits and Quaternius's animated pirates.
No credits: everything here is public domain. Credits are listed in CREDITS.md anyway.

  python3 Tools/art/free_assets.py fetch      # download the kits into art-src/free (kenney.nl, poly.pizza)
  python3 Tools/art/free_assets.py install    # Blender: vertex-colour, size and export to Assets/Resources/Models

Static models go to Models/Free/<id>.fbx (drawn with LastTide/VertexLit), characters to Models/People/<id>.fbx
with their animation clips (Idle, Walk, Run, Sword, HitReact, Death, Wave, Yes, No...).
"""
import json
import os
import subprocess
import sys
import urllib.request
import zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ai_assets import ROOT  # noqa: E402

SRC = os.path.join(ROOT, "art-src", "free")
UA = {"User-Agent": "Mozilla/5.0"}
KITS = ["pirate-kit", "survival-kit", "nature-kit"]
# Quaternius (CC0) on poly.pizza: model page id.
CHARACTERS = {"shiba": "y4wdQpg767", "husky": "wcWiuEqwzq", "fox": "Bc97C66HKi", "wolf": "P1gU3Qkr9r", "henry": "yEdSk8tRKc", "anne": "tZYaOQ4l94", "mako": "2urczqZ9Xf", "sharky": "iPV5YmAoIv", "skeleton": "yq5ATpujSt"}

P, S, N = "pirate-kit/Models/GLB format/", "survival-kit/Models/GLB format/", "nature-kit/Models/GLTF format/"
# id: (source, longest side in metres)
STATIC = {
    "wreck": (P + "ship-wreck.glb", 22),
    "pirate_ship": (P + "ship-pirate-large.glb", 26),
    "rowboat": (P + "boat-row-small.glb", 3.6),
    "chest": (P + "chest.glb", 1.4),
    "barrel": (P + "barrel.glb", 1.1),
    "crate": (P + "crate.glb", 1.15),
    "crate_bottles": (P + "crate-bottles.glb", 1.2),
    "cannon": (P + "cannon.glb", 2.4),
    "flag_pirate": (P + "flag-pirate-high.glb", 7.5),
    "watchtower": (P + "tower-watch.glb", 7.0),
    "rocks_sand_a": (P + "rocks-sand-a.glb", 3.0),
    "rocks_sand_b": (P + "rocks-sand-b.glb", 3.0),
    "campfire": (S + "campfire-pit.glb", 1.9),
    "spit": (S + "campfire-stand.glb", 2.3),
    "workbench": (S + "workbench.glb", 2.4),
    "anvil": (S + "workbench-anvil.glb", 1.6),
    "tent": (S + "tent.glb", 3.6),
    "tent_canvas": (S + "tent-canvas.glb", 4.2),
    "bedroll": (S + "bedroll.glb", 2.0),
    "signpost": (S + "signpost.glb", 2.0),
    "palisade": (S + "fence-fortified.glb", 3.2),
    "bucket": (S + "bucket.glb", 0.6),
    "box": (S + "box-large.glb", 1.3),
    "bedframe": (S + "bedroll-frame.glb", 2.3),
    "tool_axe": (S + "tool-axe.glb", 0.75),
    "tool_pickaxe": (S + "tool-pickaxe.glb", 0.8),
    "tool_hammer": (S + "tool-hammer.glb", 0.55),
    "rock_a": (S + "rock-a.glb", 2.6),
    "rock_b": (S + "rock-b.glb", 2.6),
    "rock_c": (S + "rock-c.glb", 2.6),
    "tree_oak": (N + "tree_oak.glb", 8.5),
    "tree_fat": (N + "tree_fat.glb", 7.5),
    "tree_detailed": (N + "tree_detailed.glb", 8.0),
    "bush_large": (N + "plant_bushLarge.glb", 2.2),
    "bush_detailed": (N + "plant_bushDetailed.glb", 1.6),
    "flower_red": (N + "flower_redA.glb", 0.5),
    "flower_yellow": (N + "flower_yellowB.glb", 0.5),
    "flower_purple": (N + "flower_purpleC.glb", 0.5),
    "mushrooms": (N + "mushroom_redGroup.glb", 0.6),
    "log": (N + "log_large.glb", 3.0),
    "stump": (N + "stump_roundDetailed.glb", 1.1),
    "rock_tall": (N + "rock_tallB.glb", 4.0),
    "rock_tall2": (N + "rock_tallF.glb", 4.0),
    "cliff_cave": (N + "cliff_cave_rock.glb", 9.0),
    "cliff": (N + "cliff_large_rock.glb", 9.0),
    "statue_head": (N + "statue_head.glb", 3.4),
    "obelisk": (N + "statue_obelisk.glb", 5.0),
    "ruin_column": (N + "statue_columnDamaged.glb", 3.4),
    "canoe": (N + "canoe.glb", 4.0),
    "lily": (N + "lily_large.glb", 1.2),
    "chest_gold": ("chest_gold.glb", 1.3),
    "chest_coins": ("chest_coins.glb", 1.3),
}
# Static Quaternius (CC0) models on poly.pizza: id -> page id (saved as <id>.glb).
POLY_STATIC = {"chest_gold": "4jqKCGHpUO", "chest_coins": "haqf9qoiOG"}
# Characters: height in metres.
RIGGED = {"shiba": 0.62, "husky": 0.8, "fox": 0.55, "wolf": 0.85, "henry": 1.78, "anne": 1.68, "mako": 1.82, "sharky": 1.85, "skeleton": 1.8}


def get(url, path, headers=None):
    req = urllib.request.Request(url, headers=dict(UA, **(headers or {})))
    with urllib.request.urlopen(req, timeout=120) as r, open(path, "wb") as f:
        f.write(r.read())


def fetch():
    import http.cookiejar
    import re
    os.makedirs(SRC, exist_ok=True)
    for kit in KITS:
        if os.path.isdir(os.path.join(SRC, kit)):
            print("have", kit)
            continue
        jar = http.cookiejar.CookieJar()
        op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
        page = op.open(urllib.request.Request("https://kenney.nl/assets/" + kit, headers=UA)).read().decode()
        did = re.search(r"data-download-id='([^']+)'", page).group(1)
        ref = dict(UA, Referer="https://kenney.nl/assets/" + kit)
        link = json.loads(op.open(urllib.request.Request("https://kenney.nl/download-link/" + did, headers=ref)).read())["url"]
        z = os.path.join(SRC, kit + ".zip")
        with open(z, "wb") as f:
            f.write(op.open(urllib.request.Request(link, headers=ref)).read())
        zipfile.ZipFile(z).extractall(os.path.join(SRC, kit))
        print("kit", kit)
    for name, pid in list(CHARACTERS.items()) + list(POLY_STATIC.items()):
        out = os.path.join(SRC, name + ".glb")
        if os.path.exists(out):
            continue
        import re
        page = urllib.request.urlopen(urllib.request.Request("https://poly.pizza/m/" + pid, headers=UA)).read().decode()
        glb = re.search(r"https://static\.poly\.pizza/[^\"]+\.glb", page).group(0)
        get(glb, out)
        print("character", name)


def blender(src, out, *extra):
    r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "free_import.py"), "--",
                        src, out, *extra], capture_output=True, text=True)
    line = next((l for l in r.stdout.splitlines() if "[free]" in l), None)
    print(line[:200] if line else "FAILED " + out + (r.stdout + r.stderr)[-800:])


def install(only=None):
    models = os.path.join(ROOT, "Assets", "Resources", "Models")
    for i, (src, size) in STATIC.items():
        if only and i not in only:
            continue
        blender(os.path.join(SRC, src), os.path.join(models, "Free", i + ".fbx"), "--size", str(size))
    for i, h in RIGGED.items():
        if only and i not in only:
            continue
        blender(os.path.join(SRC, i + ".glb"), os.path.join(models, "People", i + ".fbx"), "--height", str(h), "--rigged")


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "fetch":
        fetch()
    else:
        install(sys.argv[2:] or None)
