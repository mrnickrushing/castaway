#!/usr/bin/env python3
"""Sprite sheets: one Higgsfield GPT Image 2.5 picture holds a whole grid of icons or illustrations, which are cut
apart for the UI. One image instead of one per icon keeps the credits down.

  python3 Tools/art/sheets.py gen <sheet>       # paid: skips a sheet that already exists in art-src/sheets
  python3 Tools/art/sheets.py install <sheet>   # free: cut the grid -> Assets/Resources/UI/<folder>/<id>.png

Sheets: items (30 inventory icons, transparent) and places (8 location postcards, 3 offer pictures, key art).
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ai_assets import ROOT, fetch, hf  # noqa: E402

SRC = os.path.join(ROOT, "art-src", "sheets")

ITEM_ROWS = [
    [("wood", "a bundle of three chopped logs"), ("stone", "a small pile of grey stones"), ("fiber", "a tied bundle of green plant fibre"),
     ("scrap", "a pile of rusty ship scrap: bent iron bands, bolts and a broken plank"), ("cloth", "a folded off-white sailcloth"), ("hide", "a tanned brown boar hide")],
    [("rope", "a coil of hemp rope"), ("iron_ore", "a chunk of rust-red iron ore rock"), ("nails", "a handful of iron nails"),
     ("pearl", "a white pearl in an open oyster shell"), ("gunpowder", "a small wooden keg with black powder spilling"), ("doubloon", "a single shiny gold doubloon coin")],
    [("coconut", "a whole coconut and a cracked half"), ("berries", "a bunch of red wild berries with leaves"), ("raw_meat", "a raw red cut of meat on the bone"),
     ("meat", "a roasted golden-brown leg of meat"), ("crab", "a raw red crab"), ("crab_cooked", "a grilled orange crab with char marks")],
    [("water", "a glass flask of fresh water with a cork"), ("fish", "a raw silver fish"), ("fish_cooked", "a grilled fish on a stick"),
     ("rum", "a dark green bottle of rum with a cork"), ("bandage", "a rolled white cloth bandage"), ("hatchet", "a rusty hatchet with a wooden handle")],
    [("pickaxe", "a stone pickaxe with a wooden handle"), ("spear", "a bamboo spear with a stone tip, diagonal"), ("club", "a wooden club studded with iron spikes"),
     ("rod", "a bamboo fishing rod with line and a red float, diagonal"), ("harpoon", "an iron-barbed harpoon, diagonal"), ("cutlass", "a pirate cutlass with a brass guard, diagonal")],
]

PLACE_ROWS = [
    [("home", "a castaway's camp above a tropical beach: a palm-leaf lean-to shelter, a smoking campfire and a wrecked sailing ship heeled over on the sand"),
     ("grove", "a small islet thick with coconut palms and ferns, turquoise shallows, a wild boar in the undergrowth"),
     ("shore", "a beach strewn with driftwood, broken crates and barrels, a giant red crab by a tide pool"),
     ("ridge", "pale limestone cliffs above green jungle with rust-red iron streaks in the rock")],
    [("pier", "an old weathered wooden fishing pier on piles running into calm turquoise water with a rowing boat tied up"),
     ("lighthouse", "a white and red striped lighthouse on a rocky point at dusk with waves breaking"),
     ("cave", "a dark sea cave mouth in cliffs behind a sandbar, smugglers' crates and a lantern at the entrance"),
     ("pirates", "a pirate camp on a beach: patched tents, a bonfire, rum barrels, a black flag and a ship anchored offshore")],
    [("offer_starter", "a survival bundle on a wooden crate: bandages, roast meat, water flasks and a coil of rope"),
     ("offer_builder", "a pile of timber, iron nails and salvaged ship scrap"),
     ("offer_fisher", "a bamboo fishing rod, a basket of grilled fish and a small water barrel"),
     ("keyart", "a castaway sailor with a backpack standing on a tropical beach at sunset looking at a wrecked ship")],
]

SHEETS = {
    "items": dict(rows=ITEM_ROWS, folder="Items", ar="5:4", transparent=True, size=256, prompt=(
        "A sprite sheet of {n} mobile game inventory icons arranged in a precise grid of {cols} columns and {r} rows, each icon "
        "centered in its own equal square cell with generous empty space around it, on a fully transparent background. "
        "Semi-realistic painted style like a premium mobile survival game: chunky readable shapes, rich saturated colour, soft "
        "top-left lighting, a subtle dark outline. No text, no numbers, no grid lines, no borders, no labels. {rows}")),
    "places": dict(rows=PLACE_ROWS, folder="Places", ar="4:3", transparent=False, size=640, prompt=(
        "A grid of {n} separate painted illustrations, {cols} columns and {r} rows, each filling its own equal square tile, the "
        "tiles separated by thin pure white gaps. Painterly semi-realistic concept art for a premium mobile survival game set "
        "on tropical islands, warm sunlight, rich colour, no people unless named. No text, no letters, no borders inside the "
        "tiles. {rows}")),
}


def describe(rows):
    names = ["Row %d, left to right: %s." % (i + 1, "; ".join(w for _, w in row)) for i, row in enumerate(rows)]
    return " ".join(names)


def gen(name):
    os.makedirs(SRC, exist_ok=True)
    p = os.path.join(SRC, name + ".png")
    if os.path.exists(p):
        return print("have", p)
    sh = SHEETS[name]
    rows = sh["rows"]
    prompt = sh["prompt"].format(n=sum(len(r) for r in rows), cols=len(rows[0]), r=len(rows), rows=describe(rows))
    url = hf("generate", "create", "gpt_image_2_5", "--aspect_ratio", sh["ar"], "--quality", "high", "--resolution", "2k",
             "--background", "transparent" if sh["transparent"] else "opaque", "--prompt", prompt, "--wait")
    fetch(url, p)
    print("sheet", name, url)


def install(name):
    sh = SHEETS[name]
    rows = sh["rows"]
    im = Image.open(os.path.join(SRC, name + ".png")).convert("RGBA")
    W, H = im.size
    cols, nrows = len(rows[0]), len(rows)
    out = os.path.join(ROOT, "Assets", "Resources", "UI", sh["folder"])
    os.makedirs(out, exist_ok=True)
    a = np.asarray(im)
    for r, row in enumerate(rows):
        for c, (pid, _) in enumerate(row):
            x0, x1 = int(c * W / cols), int((c + 1) * W / cols)
            y0, y1 = int(r * H / nrows), int((r + 1) * H / nrows)
            cell = a[y0:y1, x0:x1]
            if sh["transparent"]:
                mask = cell[:, :, 3] > 24
            else:
                # Trim the white gutters between tiles.
                mask = cell[:, :, :3].min(axis=2) < 235
            ys, xs = np.nonzero(mask)
            if len(xs) == 0:
                print("EMPTY", pid)
                continue
            pad = 6
            bx0, bx1 = max(0, xs.min() - pad), min(cell.shape[1], xs.max() + pad)
            by0, by1 = max(0, ys.min() - pad), min(cell.shape[0], ys.max() + pad)
            crop = Image.fromarray(cell[by0:by1, bx0:bx1])
            if not sh["transparent"]:
                crop = crop.convert("RGB")
            crop.thumbnail((sh["size"], sh["size"]), Image.LANCZOS)
            if sh["transparent"]:
                crop.save(os.path.join(out, pid + ".png"))
            else:
                crop.save(os.path.join(out, pid + ".jpg"), quality=88)
            print(pid, crop.size)


if __name__ == "__main__":
    {"gen": gen, "install": install}[sys.argv[1]](sys.argv[2])
