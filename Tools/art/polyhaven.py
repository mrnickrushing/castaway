#!/usr/bin/env python3
"""Poly Haven (CC0) photoscanned ground textures and 3D models for Last Tide. Free: no credits.

  python3 Tools/art/polyhaven.py terrain          # download + pack the six ground layers into Resources/Textures
  python3 Tools/art/polyhaven.py fetch [ids...]   # download models (glTF, 2k maps) into art-src/polyhaven/<id>
  python3 Tools/art/polyhaven.py install [ids...] # Blender: split, decimate, bake, export to Resources/Models/Scan
  python3 Tools/art/polyhaven.py slim             # after install: drop unused pieces, cap maps at 1024, normals to JPG

Terrain layers are in the order Terrain.shader expects: grass, leaf litter, dirt, sand, rock, pebbles.
"""
import json
import os
import subprocess
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "art-src", "polyhaven")
UA = {"User-Agent": "LastTide-asset-pipeline"}
API = "https://api.polyhaven.com"

# "acg:" layers come from ambientCG (also CC0): Poly Haven's ground scans are all dry, none is lush tropical green.
TERRAIN = ["acg:Grass001", "forest_leaves_02", "forrest_ground_01", "coast_sand_01", "rock_face", "coast_sand_rocks_02"]
TILE = 1024

# Models: id -> (kind, target size in metres (longest side, or height for plants/trees), triangle budget per piece).
# kind: "solid" (rebake onto a clean low-poly mesh with fresh UVs), "plant" (keep the leaf cards and their alpha maps,
# decimate gently), "prop" (decimate gently, keep the UVs and maps).
MODELS = {
    "boulder_01": ("solid", 3.2, 2500),
    "coast_rocks_05": ("solid", 7.0, 4000),
    "coast_rocks_01": ("solid", 6.0, 3500),
    "rock_moss_set_01": ("solid", 2.4, 1200),
    "stone_01": ("solid", 1.4, 1200),
    "rock_07": ("solid", 2.0, 1500),
    "tree_stump_01": ("solid", 1.2, 1500),
    "dead_tree_trunk": ("solid", 4.5, 2500),
    "dry_branches_medium_01": ("solid", 2.0, 600),
    "fern_02": ("plant", 1.2, 2500),
    "anthurium_botany_01": ("plant", 0.9, 2000),
    "calathea_orbifolia_01": ("plant", 0.8, 3000),
    "shrub_04": ("plant", 1.3, 3000),
    "shrub_sorrel_01": ("plant", 0.5, 1500),
    "grass_medium_02": ("plant", 0.6, 1800),
    "island_tree_02": ("plant", 7.5, 22000),
    "tree_small_02": ("plant", 6.0, 9000),
    "treasure_chest": ("prop", 1.4, 6000),
    "wooden_crate_01": ("prop", 1.2, 3000),
    "wooden_crate_02": ("prop", 1.2, 3000),
    "wooden_bucket_01": ("prop", 0.6, 2500),
    "wooden_lantern_01": ("prop", 0.5, 3000),
    "wooden_barrels_01": ("prop", 1.2, 3000),
    "lambis_shell": ("prop", 0.3, 1500),
    "dutch_ship_large_01": ("prop", 30.0, 60000),
    "ship_pinnace": ("prop", 9.0, 25000),
    "modular_wooden_pier": ("prop", 14.0, 12000),
    "WoodenTable_03": ("prop", 2.2, 3000),
    "wooden_axe": ("prop", 0.8, 1500),
    "wooden_hammer_01": ("prop", 0.4, 1200),
    "handsaw_wood": ("prop", 0.65, 1200),
    "bananas": ("prop", 0.3, 2500),
}


def get_json(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r:
        return json.loads(r.read())


def download(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=300) as r, open(path, "wb") as f:
        f.write(r.read())
    return path


def terrain():
    from PIL import Image
    tex = os.path.join(SRC, "textures")
    albedo = Image.new("RGBA", (TILE * 3, TILE * 2))
    normal = Image.new("RGBA", (TILE * 3, TILE * 2))
    for i, tid in enumerate(TERRAIN):
        if tid.startswith("acg:"):
            import zipfile
            aid = tid[4:]
            z = download("https://ambientcg.com/get?file=" + aid + "_2K-JPG.zip", os.path.join(tex, aid + ".zip"))
            zf = zipfile.ZipFile(z)
            def part(tag):
                name = next(n for n in zf.namelist() if tag in n)
                return Image.open(zf.open(name))
            d = part("_Color").convert("RGB").resize((TILE, TILE), Image.LANCZOS)
            h = part("_Displacement").convert("L").resize((TILE, TILE), Image.LANCZOS)
            n = part("_NormalGL").convert("RGB").resize((TILE, TILE), Image.LANCZOS)
            rough = part("_Roughness").convert("L").resize((TILE, TILE), Image.LANCZOS)
            try:
                ao = part("_AmbientOcclusion").convert("L").resize((TILE, TILE), Image.LANCZOS)
            except StopIteration:
                ao = Image.new("L", (TILE, TILE), 255)
            nr, ng, _ = n.split()
        else:
            files = get_json(API + "/files/" + tid)
            diff = download(files["Diffuse"]["2k"]["jpg"]["url"], os.path.join(tex, tid + "_diff.jpg"))
            nor = download(files["nor_gl"]["2k"]["jpg"]["url"], os.path.join(tex, tid + "_nor_gl.jpg"))
            arm = download(files["arm"]["2k"]["jpg"]["url"], os.path.join(tex, tid + "_arm.jpg"))
            disp = download(files["Displacement"]["2k"]["png"]["url"], os.path.join(tex, tid + "_disp.png"))
            d = Image.open(diff).convert("RGB").resize((TILE, TILE), Image.LANCZOS)
            h = Image.open(disp).convert("L").resize((TILE, TILE), Image.LANCZOS)
            n = Image.open(nor).convert("RGB").resize((TILE, TILE), Image.LANCZOS)
            a = Image.open(arm).convert("RGB").resize((TILE, TILE), Image.LANCZOS)
            nr, ng, _ = n.split()
            ao, rough, _ = a.split()
        # Height maps come from different sources and scales; the shader blends layers by height, so give them all
        # the same spread (mean 0.5) or one layer wins every transition outright.
        from PIL import ImageStat
        st = ImageStat.Stat(h)
        mean, dev = st.mean[0], max(1.0, st.stddev[0])
        h = h.point(lambda v: max(0, min(255, int(128 + (v - mean) * 48 / dev))))
        x, y = (i % 3) * TILE, (i // 3) * TILE
        albedo.paste(Image.merge("RGBA", (*d.split(), h)), (x, y))
        # Terrain.shader reads: R = occlusion, G = normal Y, B = roughness, A = normal X.
        normal.paste(Image.merge("RGBA", (ao, ng, rough, nr)), (x, y))
        print("layer", i, tid)
    out = os.path.join(ROOT, "Assets", "Resources", "Textures")
    albedo.save(os.path.join(out, "TerrainAlbedo.png"))
    normal.save(os.path.join(out, "TerrainNormal.png"))
    print("packed", out)


def fetch(ids):
    for mid in ids:
        files = get_json(API + "/files/" + mid)
        g = files["gltf"]["2k"]["gltf"]
        d = os.path.join(SRC, mid)
        download(g["url"], os.path.join(d, os.path.basename(g["url"])))
        for rel, f in g["include"].items():
            download(f["url"], os.path.join(d, rel))
        # Leaf cutouts ship as separate alpha maps (the glTF's diffuse JPG has none).
        for key, res in files.items():
            if "alpha" in key.lower() and "2k" in res and "jpg" in res["2k"]:
                u = res["2k"]["jpg"]["url"]
                download(u, os.path.join(d, "textures", os.path.basename(u)))
        print("fetched", mid)


def install(ids):
    out = os.path.join(ROOT, "Assets", "Resources", "Models", "Scan")
    os.makedirs(out, exist_ok=True)
    for mid in ids:
        kind, size, faces = MODELS[mid]
        d = os.path.join(SRC, mid)
        gltf = next((os.path.join(d, f) for f in os.listdir(d) if f.endswith(".gltf")), None)
        if gltf is None:
            print("missing", mid)
            continue
        r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "scan_import.py"), "--",
                            gltf, out, mid, kind, str(size), str(faces)], capture_output=True, text=True)
        lines = [l for l in r.stdout.splitlines() if "[scan]" in l]
        print("\n".join(lines) if lines else "FAILED " + mid + (r.stdout + r.stderr)[-1500:])


# Pieces the game doesn't place (set leftovers, models tried and dropped): removed by slim.
UNUSED = ["tree_small_02", "coast_rocks_01", "ship_pinnace", "modular_wooden_pier", "lambis_shell", "wooden_barrels_01_a",
          "wooden_barrels_01_b", "wooden_barrels_01_c", "wooden_barrels_01_e", "wooden_barrels_01_f", "wooden_barrels_01_g",
          "wooden_barrels_01_h", "grass_medium_02_a", "grass_medium_02_b", "bananas_a", "bananas_b", "shrub_sorrel_01_d",
          "shrub_sorrel_01_e", "shrub_sorrel_01_f", "shrub_sorrel_01_g", "anthurium_botany_01_d", "anthurium_botany_01_f",
          "calathea_orbifolia_01_b", "calathea_orbifolia_01_d"]


def slim():
    from PIL import Image
    out = os.path.join(ROOT, "Assets", "Resources", "Models", "Scan")
    tex = os.path.join(out, "tex")

    def rm(path):
        for p in (path, path + ".meta"):
            if os.path.exists(p):
                os.remove(p)

    for f in sorted(os.listdir(out)):
        stem = f.split(".")[0]
        if any(stem == u or stem.startswith(u + "_") and not stem[len(u) + 1:].startswith(("0",)) for u in UNUSED) and not f.endswith(".meta"):
            if stem in UNUSED or any(stem.startswith(u + "_") and len(stem) - len(u) <= 2 for u in UNUSED) or stem.startswith(("tree_small_02", "ship_pinnace", "modular_wooden_pier", "coast_rocks_01")):
                rm(os.path.join(out, f))
    used = set()
    for f in os.listdir(out):
        if f.endswith(".json"):
            for slot in json.load(open(os.path.join(out, f)))["slots"]:
                used.add(slot["name"])
    for f in sorted(os.listdir(tex)):
        if f.endswith(".meta"):
            continue
        path = os.path.join(tex, f)
        name = f.rsplit("_", 1)[0]
        if name not in used:
            rm(path)
            continue
        im = Image.open(path)
        if max(im.size) > 1024:
            im = im.resize((1024, 1024), Image.LANCZOS)
            im.save(path)
        if f.endswith("_normal.png"):
            im.convert("RGB").save(path[:-4] + ".jpg", quality=92)
            rm(path)
    print("slim done")


if __name__ == "__main__":
    cmd, rest = sys.argv[1], sys.argv[2:]
    if cmd == "terrain":
        terrain()
    elif cmd == "slim":
        slim()
    elif cmd == "fetch":
        fetch(rest or list(MODELS))
    else:
        install(rest or list(MODELS))
