#!/usr/bin/env python3
"""AI art pipeline for Last Tide (Higgsfield: GPT Image 2.5 concept -> Tripo H3.1 3D, or multi-image 3D with
rigging for characters -> Blender cleanup).

  python3 Tools/art/ai_assets.py concept <id>...   # concept image (skips ids that already have one)
  python3 Tools/art/ai_assets.py model <id>...     # 3D GLB from the approved concept (skips existing)
  python3 Tools/art/ai_assets.py import <id>...    # Blender cleanup -> Assets/Resources/Models/...
  python3 Tools/art/ai_assets.py list

Work files live in art-src/ (gitignored, large). Never re-run a paid step for an id that already has its output:
delete the file first if a redo is really wanted. Every design is original.
"""
import json
import os
import subprocess
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "art-src")

STYLE = ("Semi-realistic stylized like a premium mobile survival game, rich natural colour, clean studio lighting with "
         "soft shadows. No text, no logos.")
OBJECT = ("Game asset reference render of {what}. Slightly elevated three-quarter view, isolated on a plain flat white "
          "background, the whole thing visible and nothing cropped. " + STYLE)
ANIMAL = ("Game asset reference render of {what}, seen from its left side with the head pointing to the LEFT, standing square "
          "and calm with all four legs straight and clearly separated, tail hanging down, isolated on a plain flat white "
          "background, nothing cropped, no ground. " + STYLE)
CHARACTER = ("Full-body character reference of {what}. Standing straight facing the viewer in a relaxed A-pose with the arms "
             "held down and slightly away from the body, legs slightly apart, the whole figure visible from head to feet, isolated "
             "on a plain flat white background. " + STYLE + " No weapons or tools in the hands.")

A = {
    # The castaway (rigged; Walk/Idle clips from Tools/blender/townsfolk.py; work poses are driven in code).
    "survivor": dict(cat="People", height=1.78, ar="3:4", prompt=CHARACTER.format(what=(
        "an original rugged shipwreck survivor, a sailor in his thirties with short dark brown hair under a faded red bandana and "
        "a short beard, a sun-faded cream linen shirt with the sleeves rolled up and a torn hem, a dark leather belt with a small "
        "pouch, patched navy blue canvas trousers rolled to mid-calf, worn brown leather boots, a small olive canvas backpack with "
        "a rolled blanket strapped under it"))),
    # A wild boar (rigged and given a walk by Tools/blender/horse.py).
    "boar": dict(cat="Props", length=1.6, faces=6000, front="auto", animal=True, prompt=ANIMAL.format(what=(
        "a single wild island boar with a bristly dark brown and grey coat, a ridge of coarse black bristles along its back, "
        "a long snout and short curved ivory tusks, small ears, stocky and muscular"))),
    # Palm (a harvestable node; near mesh only, drawn as normal objects).
    "palm": dict(cat="Trees", footprint=1.0, tree=True, prompt=OBJECT.format(what=(
        "a single tall tropical coconut palm tree with a gently curving ringed grey-brown trunk and a full crown of long arching "
        "feathery green fronds with a cluster of brown coconuts under the crown, the whole tree from the ground to the top, no "
        "ground patch")), tint=[0.62, 0.8, 0.5]),
    # The wreck on the beach.
    "wreck": dict(cat="Buildings", footprint=24.0, wide=True, prompt=OBJECT.format(what=(
        "an original wrecked two-masted wooden sailing ship run aground and heeled over on its side, a dark tarred hull with "
        "weathered planks and a gaping hole torn in the side, a snapped main mast, the fore mast still standing with a torn "
        "off-white sail and tangled rigging, broken deck rails, seaweed on the lower hull, on a thin flat base of wet sand"))),
}

def hf(*args):
    print("+ higgsfield", " ".join(a if len(a) < 60 else a[:57] + "..." for a in args))
    r = subprocess.run(["higgsfield", *args], capture_output=True, text=True)
    out = (r.stdout + r.stderr).strip().splitlines()
    url = next((l for l in reversed(out) if l.startswith("http")), None)
    if not url:
        raise RuntimeError("\n".join(out[-5:]))
    return url


def fetch(url, path):
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=300) as r, open(path + ".part", "wb") as f:
        f.write(r.read())
    os.replace(path + ".part", path)  # never expose a half-written file to the importer


def concept(i):
    p = os.path.join(SRC, i + ".png")
    if os.path.exists(p):
        return print("have", p)
    a = A[i]
    ar = a.get("ar") or ("16:9" if a["cat"] in ("Props", "Animals") else "16:9" if a.get("wide") else "1:1")
    url = hf("generate", "create", "gpt_image_2_5", "--aspect_ratio", ar, "--quality", "high", "--resolution", "2k",
             "--background", "opaque", "--prompt", a["prompt"], "--wait")
    fetch(url, p)
    print("concept", i, url)


def model(i):
    p, g = os.path.join(SRC, i + ".png"), os.path.join(SRC, i + ".glb")
    if os.path.exists(g):
        return print("have", g)
    if not os.path.exists(p):
        raise SystemExit("no concept for " + i)
    if a_rigged(i):
        url = hf("generate", "create", "multi_image_to_3d", "--image", p, "--should_texture", "true", "--enable_pbr", "false",
                 "--enable_rigging", "true", "--enable_animation", "false", "--pose_mode", "a-pose",
                 "--rigging_height_meters", str(A[i].get("height", 1.75)), "--target_polycount", "6000", "--topology", "triangle",
                 "--wait", "--wait-timeout", "25m")
    elif os.environ.get("MODEL3D") == "hunyuan":  # fallback when Tripo is failing
        url = hf("generate", "create", "hunyuan3d_v3_image_to_3d", "--image", p, "--face_count", "300000",
                 "--wait", "--wait-timeout", "30m")
    else:
        url = hf("generate", "create", "tripo_h3_1_image_to_3d", "--image", p, "--texture_quality", "detailed",
                 "--geometry_quality", "standard", "--pbr", "true", "--wait", "--wait-timeout", "25m")
    fetch(url, g)
    print("model", i, url)


TREE_HEIGHT = {"palm": 8.5}
# (near, far) triangle budgets: near cells draw the detailed mesh, distant cells the light one.
TREE_VOXELS = {"palm": (120, 55)}  # voxels across the plant height for the canopy remesh (near, far)
TREE_FACES = {"palm": (3200, 700)}


def a_rigged(i):
    return A[i]["cat"] == "People"


def do_import(i, out_id=None, faces=None):
    a = A[i]
    if a_rigged(i):
        r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "townsfolk.py"), "--",
                            os.path.join(SRC, i + ".glb"), i, "--height", str(a.get("height", 1.75))], capture_output=True, text=True)
        print("\n".join(l for l in r.stdout.splitlines() if "[" in l and "]" in l)[-3000:] or (r.stdout + r.stderr)[-2000:])
        return
    if a.get("tree") and out_id is None:
        near, far = TREE_FACES[i]
        do_import(i, i, near)
        do_import(i, i + "_far", far)
        return
    out_id = out_id or i
    g = os.path.join(SRC, i + ".glb")
    cmd = ["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "import_ai.py"), "--", g, a["cat"], out_id]
    if a.get("tree"):
        cmd += ["--height", str(TREE_HEIGHT[i]), "--front", "none", "--faces", str(faces), "--tex", "512",
                "--remesh", str(TREE_VOXELS.get(i, (70, 36))[0 if out_id == i else 1])]
    elif a["cat"] == "Buildings":
        small = a["footprint"] < 4
        faces = "6000" if small else "40000" if a.get("hero") else "20000"
        cmd += ["--footprint", str(a["footprint"]), "--faces", faces, "--tex", "512" if small else "2048" if a.get("hero") else "1024"]
        if "height" in a:
            cmd += ["--height", str(a["height"])]
        if a.get("smoke"):
            cmd += ["--smoke"]
        if a.get("wide"):
            cmd += ["--wide"]
    else:
        cmd += ["--length", str(a["length"]), "--front", a.get("front", "auto"),
                "--faces", str(a.get("faces", 15000)),
                "--tex", "1024"]
        if a.get("twin"):
            cmd += ["--twin"]
        if a.get("flip"):
            cmd += ["--flip"]
    r = subprocess.run(cmd, capture_output=True, text=True)
    print("\n".join(l for l in r.stdout.splitlines() if "[ai]" in l) or r.stdout[-2000:] + r.stderr[-2000:])
    rebake(out_id, a, src=i)
    if a.get("animal"):
        # Rig + walk cycle, then move the result (and its textures) to Models/Animals.
        props, animals = (os.path.join(ROOT, "Assets", "Resources", "Models", c) for c in ("Props", "Animals"))
        r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "horse.py"), "--",
                            os.path.join(props, i + ".fbx"), animals], capture_output=True, text=True)
        print("\n".join(l for l in r.stdout.splitlines() if "[horse]" in l) or r.stdout[-2000:] + r.stderr[-2000:])
        for kind in ("_albedo.jpg", "_normal.jpg"):
            os.replace(os.path.join(props, i + kind), os.path.join(animals, i + kind))
        for f in (i + ".fbx", i + ".fbx.meta", i + "_albedo.jpg.meta", i + "_normal.jpg.meta"):
            if os.path.exists(os.path.join(props, f)):
                os.remove(os.path.join(props, f))


def rebake(i, a, src=None):
    """Bakes the full-detail colours onto the decimated mesh with clean UVs (see Tools/blender/rebake.py)."""
    from PIL import Image
    d = os.path.join(ROOT, "Assets", "Resources", "Models", a["cat"])
    fbx = os.path.join(d, i + ".fbx")
    tmp = os.path.join(ROOT, "Temp", "bake", i)
    os.makedirs(os.path.dirname(tmp), exist_ok=True)
    size = "2048" if a.get("hero") else "512" if a.get("tree") or a.get("footprint", 9) < 4 else "1024"
    cmd = ["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "rebake.py"), "--",
           os.path.join(SRC, (src or i) + ".glb"), fbx, tmp, "--size", size, "--normal"] + (["--twin"] if a.get("twin") else [])
    r = subprocess.run(cmd, capture_output=True, text=True)
    lines = [l for l in r.stdout.splitlines() if "[bake]" in l]
    if not any("exported" in l for l in lines):
        print("BAKE FAILED", i, (r.stdout + r.stderr)[-1500:])
        return
    for kind in ("albedo", "normal"):
        im = Image.open(tmp + "_" + kind + ".png").convert("RGB")
        if kind == "albedo" and a.get("tint"):
            import numpy as np
            im = Image.fromarray((np.asarray(im) * a["tint"]).clip(0, 255).astype("uint8"))
        im.save(os.path.join(d, i + "_" + kind + ".jpg"), quality=90)
    ms = os.path.join(d, i + "_ms.png")  # old-UV metal map no longer lines up
    for f in (ms, ms + ".meta"):
        if os.path.exists(f):
            os.remove(f)
    print("[bake]", i, lines[0])


if __name__ == "__main__":
    os.makedirs(SRC, exist_ok=True)
    cmd, ids = sys.argv[1], sys.argv[2:]
    if cmd == "list":
        for k, v in A.items():
            print(k, v["cat"], "concept" if os.path.exists(os.path.join(SRC, k + ".png")) else "-", "glb" if os.path.exists(os.path.join(SRC, k + ".glb")) else "-")
        sys.exit()
    fn = {"concept": concept, "model": model, "import": do_import}[cmd]
    for i in ids or list(A):
        try:
            fn(i)
        except Exception as e:  # keep going; report at the end
            print("FAILED", i, e)
