#!/usr/bin/env python3
"""Realistic characters and creatures from Sketchfab (CC BY / CC0 only, original designs only, no fan art).

  python3 Tools/art/sketchfab.py fetch [id...]    # download glb (needs SKETCHFAB_API_KEY; or use the Sketchfab MCP)
  python3 Tools/art/sketchfab.py cast [id...]     # rigged people -> Assets/Resources/Models/Cast (Tools/blender/cast_import.py)
  python3 Tools/art/sketchfab.py creature [id...] # animals -> Assets/Resources/Models/Creatures (Tools/blender/creature_import.py)
  python3 Tools/art/sketchfab.py prop [id...]     # props -> Assets/Resources/Models/Scan (Tools/blender/scan_import.py)
  python3 Tools/art/sketchfab.py moves            # the shared humanoid clips (Quaternius UAL, CC0) -> Cast/moves.fbx
  python3 Tools/art/sketchfab.py credits          # rewrite the Sketchfab section of CREDITS.md

Sources live in art-src/sketchfab/<id>.glb (not committed). Every CC BY model must stay credited in CREDITS.md and
on the in-game credits (UI/Hud.Help.cs reads Resources/credits.txt). Check a model's page before adding it: the
license must be CC Attribution or CC0, and the design must not be a character, logo or likeness from someone
else's game, film or brand.
"""
import json
import os
import subprocess
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "art-src", "sketchfab")
CAST_DIR = os.path.join(ROOT, "Assets", "Resources", "Models", "Cast")
UAL = os.path.join(ROOT, "art-src", "ual", "x", "Universal Animation Library[Standard]", "Unity", "UAL1_Standard.fbx")


def cat(prefix, side):
    """CAT rig (3ds Max) arm/leg names for one side."""
    S = side.upper()
    return {prefix + S + "ArmCollarbone": "clavicle_" + side, prefix + S + "Arm1": "upperarm_" + side,
            prefix + S + "Arm2": "lowerarm_" + side, prefix + S + "Leg1": "thigh_" + side, prefix + S + "Leg2": "calf_" + side}


SKELETON_MAP = {"Pelvis": "pelvis", "Spine3": "spine_01", "Spine2": "spine_02", "Spine1": "spine_03", "neck": "neck_01",
                "Headbase": "Head", "L_Collar": "clavicle_l", "L_shoulder": "upperarm_l", "L_forarm": "lowerarm_l",
                "L_leg": "thigh_l", "L_knee": "calf_l", "L_foot": "foot_l", "R_collar": "clavicle_r",
                "R_shoulder": "upperarm_r", "R_forarm": "lowerarm_r", "R_leg": "thigh_r", "R_Knee": "calf_r", "R_foot": "foot_r"}
SKELETON_HANDS = {"l": ["L_forarm", ["L_hand1", "L_hand2", "L_hand3", "L_hand4", "L_hand5"]],
                  "r": ["R_forarm", ["Bone.040", "Bone.046", "Bone.049", "Bone.052", "HandleBone"]]}

# id: Sketchfab uid, title, author (username), role in the game, height (m), triangle budget, import cfg.
CAST = {
    "castaway": dict(uid="6a481c17d68a4a138f726ccb76caedff", title="Castaway", author="katarzynaszemla",
                     role="the player's castaway", height=1.8, tris=30000, recolour=True,
                     done="decimated, rig renamed, clothes recoloured to match the story paintings (linen shirt, red sash, brown breeches)",
                     cfg={"map": dict(cat("CATRig", "l"), **cat("CATRig", "r"), **{
                         "CATRigpelvis": "pelvis", "CATRigSpinespine1": "spine_01", "CATRigSpinespine2": "spine_02",
                         "CATRigchest": "spine_03", "CATRigSpineneck1": "neck_01", "CATRighead": "Head",
                         "CATRigLArmLPalm": "hand_l", "CATRigRArmRPalm": "hand_r", "CATRigLLegLAnkle": "foot_l",
                         "CATRigRLegRAnkle": "foot_r", "CATRigLLegLfoot1": "ball_l", "CATRigRLegRfoot1": "ball_r"})}),
    "captain": dict(uid="bae2223031094f629ad86242e26a37e6", title="Pirate Captain", author="byenbyen",
                    role="Red Jack", height=1.86, tris=22000,
                    cfg={"map": dict(cat("CATRig", "l"), **cat("CATRig", "r"), **{
                        "CATRigHub001": "pelvis", "CATRigSpine1_015": "spine_01", "CATRigSpine2_016": "spine_02",
                        "CATRigHub002": "spine_03", "CATRigSpine1_056": "neck_01", "CATRigHub003": "Head",
                        "CATRigLArmPalm": "hand_l", "CATRigRArmPalm": "hand_r", "CATRigLLegAnkle": "foot_l",
                        "CATRigRLegAnkle": "foot_r"})}),
    "hunter": dict(uid="9f51d396733b451bab550b406b067805", title="Game ready Pirate Hunter / Viking / Medieval man",
                   author="chenuka_wy", extra="its jacket is \"Gambeson\" by Leon.Steiner (https://sketchfab.com/3d-models/0fc44d2876ed449ca9473e78c83a8d39), CC BY 4.0",
                   role="Red Jack's raiders", height=1.8, tris=14000, cfg={}),
    "mara": dict(uid="8e71658a2f93410e8905aa7a160bc5d9", title="Pirate Girl", author="Anriil_Kl",
                 role="Mara", height=1.68, tris=18000, cfg={}),
    "tomas": dict(uid="ae5182ccddfd47acb59400f739581743", title="Farmer", author="LxNazarov",
                  role="Old Tomas", height=1.74, tris=9000,
                  cfg={"drop": ["shovel"], "map": {
                      "Bone.001": "pelvis", "Bone.002": "spine_01", "Bone.003": "spine_02", "Bone.004": "spine_03",
                      "Bone.028": "neck_01", "Bone.029": "Head", "Shoulder.L": "clavicle_l", "Arm1.L": "upperarm_l",
                      "Arm2.L": "lowerarm_l", "Hand.L": "hand_l", "Leg.L": "thigh_l", "Leg2.L": "calf_l",
                      "Foot1.L": "foot_l", "Foot2.L": "ball_l", "Shoulder.R": "clavicle_r", "Arm1.R": "upperarm_r",
                      "Arm2.R": "lowerarm_r", "Hand.R": "hand_r", "Leg.R": "thigh_r", "Leg2.R": "calf_r",
                      "Foot1.R": "foot_r", "Foot2.R": "ball_r"}}),
    "ned": dict(uid="1da6ed53db67461eb0d36b80ddcf6ffb", title="Virtual model linen tunic Svend", author="chambersu1996",
                role="Ned Barlow and other survivors", height=1.8, tris=16000, cfg={}),
    "revna": dict(uid="9ac96b0353264685b1290bab355fe876", title="Virtual model linen tunic & slim Revna",
                  author="chambersu1996", role="rescued survivors", height=1.66, tris=16000, cfg={}),
    "smuggler": dict(uid="65cbe69004924bbc940cd74276ba90fb", title='Skeleton Pirate " The Captin "', author="ShadowCoffee",
                     role="drowned smugglers", height=1.8, tris=12000, cfg={"map": SKELETON_MAP, "hands": SKELETON_HANDS}),
    "smuggler2": dict(uid="9ed1650abdce48d4a1a4d34a4a56cfc5", title='Skeleton Pirate " The Drunk "', author="ShadowCoffee",
                      role="drowned smugglers", height=1.75, tris=12000, cfg={"map": SKELETON_MAP, "hands": SKELETON_HANDS}),
}
FILES = {"snake": "stripesnake", "gull": "seagull", "castaway": "castaway", "captain": "captain", "hunter": "hunter", "mara": "pirategirl", "tomas": "farmer",
         "ned": "svend", "revna": "revna", "smuggler": "skelcaptain", "smuggler2": "skeldrunk"}

# Creatures (Tools/blender/creature_import.py -> Models/Creatures) and props (Tools/blender/scan_import.py ->
# Models/Scan), credited the same way. Both well-animated Sketchfab boars ship without their textures, and the
# realistic animated dogs have no walk or run: the game keeps its own boar and the Quaternius pets.
CREATURES = {
    "crab": dict(uid="35559c2236d04c1a80ccbe08cae863c6", title="Japanese Freshwater Crab", author="ffishAsia-and-floraZia",
                 license="CC0", role="the giant crabs", size=1.7, tris=9000, cfg={"clips": {"Animation": "Walk"}}),
    "snake": dict(uid="0f4f583d5e3842eca1152c0bb021ec32", title="Striped Snake, Elaphe quadrivirgata",
                  author="ffishAsia-and-floraZia", license="CC0", role="the vipers (thickened, spine bent in code)",
                  size=2.4, tris=7000, cfg={"yaw": 90, "rest": 0, "girth": 1.8}),
    "gull": dict(uid="985024328902444c8270c0f09acc897e", title="Low-Poly Seagull (with Animation & Rigged)",
                 author="simonaskLDE", role="the gulls over the shore (wings flapped in code)", done="rig kept", size=1.3, tris=1000,
                 cfg={"pitch": -90}),
}
PROPS = {
    "rowboat": dict(uid="9922d5678af84adeb1c9b479856446ca", title="Old Rowboat", author="toomanydemons",
                    role="the rowboat, the beached boat on Driftwood Shore and the boat at the pier", kind="prop", size=3.6, tris=6000),
    "ironore": dict(uid="93118b922cbb48caa4f45b16592fa519", title="Iron Ore (FREE)", author="vxdesigns",
                    role="the iron veins", kind="prop", size=2.4, tris=2000),
    "lighthouse": dict(uid="9cc7e0ae47824e198ad5dd296644c125", title="Lighthouse", author="stnabeal",
                       role="the lighthouse on Lighthouse Point", kind="prop", size=17, tris=9000),
    "raft": dict(uid="48fb810caf0941328a6619a9a948b565", title="Raft", author="_SeF_",
                 role="the castaway's raft (to the map) on every island", kind="prop", size=4.2, tris=7000),
}
OTHER = dict(CREATURES, **PROPS)


def src_of(i):
    return os.path.join(SRC, FILES.get(i, i) + ".glb")


def fetch(i):
    key = os.environ.get("SKETCHFAB_API_KEY")
    if not key:
        sys.exit("set SKETCHFAB_API_KEY (or download with the Sketchfab MCP into art-src/sketchfab/)")
    uid = (CAST.get(i) or OTHER[i])["uid"]
    req = urllib.request.Request("https://api.sketchfab.com/v3/models/%s/download" % uid, headers={"Authorization": "Token " + key})
    url = json.load(urllib.request.urlopen(req))["glb"]["url"]
    os.makedirs(SRC, exist_ok=True)
    urllib.request.urlretrieve(url, src_of(i))
    print("fetched", i)


def cast(i):
    c = CAST[i]
    os.makedirs(CAST_DIR, exist_ok=True)
    r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "cast_import.py"), "--",
                        src_of(i), CAST_DIR, i, str(c["height"]), str(c["tris"]), json.dumps(c["cfg"])],
                       capture_output=True, text=True)
    out = [l for l in r.stdout.splitlines() if l.startswith("[cast]") or "Error" in l or "Traceback" in l]
    print("\n".join(out) or r.stdout[-2000:] + r.stderr[-2000:])
    if c.get("recolour"):
        # Dressed like the story paintings (Tools/blender/cast_recolour.py).
        r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "cast_recolour.py"), "--",
                            os.path.join(CAST_DIR, i + ".fbx"), os.path.join(CAST_DIR, "tex", i + "_0_albedo.png")],
                           capture_output=True, text=True)
        print("\n".join(l for l in r.stdout.splitlines() if l.startswith("[recolour]") or "Error" in l) or r.stderr[-2000:])


def creature(i):
    c = CREATURES[i]
    out = os.path.join(ROOT, "Assets", "Resources", "Models", "Creatures")
    r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "creature_import.py"), "--",
                        src_of(i), out, i, str(c["size"]), str(c["tris"]), json.dumps(c["cfg"])], capture_output=True, text=True)
    print("\n".join(l for l in r.stdout.splitlines() if l.startswith("[creature]") or "Error" in l) or r.stderr[-2000:])


def prop(i):
    c = PROPS[i]
    out = os.path.join(ROOT, "Assets", "Resources", "Models", "Scan")
    r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "scan_import.py"), "--",
                        src_of(i), out, i, c["kind"], str(c["size"]), str(c["tris"])], capture_output=True, text=True)
    print("\n".join(l for l in r.stdout.splitlines() if "[scan]" in l) or r.stderr[-2000:])


def moves():
    os.makedirs(CAST_DIR, exist_ok=True)
    r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "cast_moves.py"), "--",
                        UAL, os.path.join(CAST_DIR, "moves.fbx")], capture_output=True, text=True)
    print("\n".join(l for l in r.stdout.splitlines() if l.startswith("[moves]") or "Error" in l) or r.stderr[-2000:])


def credit_lines():
    lines = []
    for i, c in list(CAST.items()) + list(OTHER.items()):
        extra = ("; " + c["extra"]) if c.get("extra") else ""
        done = c.get("done") or ("decimated, rig renamed" if i in CAST else "decimated, rig kept" if i in CREATURES else "maps repacked")
        lines.append('- "%s" by %s (https://sketchfab.com/3d-models/%s), %s%s. Used as %s; rescaled, %s.'
                     % (c["title"], c["author"], c["uid"], c.get("license", "CC BY 4.0"), extra, c["role"], done))
    return lines


def credits():
    path = os.path.join(ROOT, "CREDITS.md")
    text = open(path).read() if os.path.exists(path) else "# Credits\n"
    head = "## Sketchfab (CC BY 4.0 and CC0)"
    body = head + "\n\n" + "\n".join(credit_lines()) + "\n\n" + \
        "- Universal Animation Library by Quaternius (https://quaternius.com), CC0. Every character's moves.\n"
    old = "## Sketchfab (CC BY 4.0)"
    if old in text and head not in text:
        text = text.replace(old, head)
    if head in text:
        start = text.index(head)
        end = text.find("\n## ", start + len(head))
        text = text[:start] + body + (text[end:] if end >= 0 else "")
    else:
        text = text.rstrip() + "\n\n" + body
    open(path, "w").write(text)
    print("credits written")


if __name__ == "__main__":
    cmd, ids = sys.argv[1], sys.argv[2:]
    if cmd == "fetch":
        for i in ids or CAST:
            fetch(i)
    elif cmd == "cast":
        for i in ids or CAST:
            cast(i)
    elif cmd == "creature":
        for i in ids or CREATURES:
            creature(i)
    elif cmd == "prop":
        for i in ids or PROPS:
            prop(i)
    elif cmd == "moves":
        moves()
    elif cmd == "credits":
        credits()
