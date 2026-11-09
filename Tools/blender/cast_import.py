"""Turns a rigged Sketchfab character (glb) into a Last Tide cast member that the shared humanoid clips can drive.

  blender -b --factory-startup -P Tools/blender/cast_import.py -- <in.glb> <outdir> <id> <height> <tris> '<json cfg>'

Every rig is renamed to the Unreal mannequin's core bone names (pelvis, spine_01..03, neck_01, Head, clavicle_l,
upperarm_l, lowerarm_l, hand_l, thigh_l, calf_l, foot_l, ball_l and _r), the names of the Quaternius Universal
Animation Library (Assets/Resources/Models/Cast/moves.fbx), so Unity's Humanoid mapping finds the same skeleton in
each and the clips retarget onto them. Mixamo-style names (any prefix) are mapped automatically; other rigs pass
their own map in cfg.

cfg (all optional):
  map    {source bone (Sketchfab's _NN suffix removed): mannequin name}
  drop   mesh name fragments to leave out (props in hand, background planes)
  hands  {"l": [forearm, [finger roots]], "r": [...]}: adds a hand bone where a rig has fingers on the forearm
  yaw    degrees to turn the model so it faces -Y in Blender (+Z in Unity)
  maps   texture size (default 1024)

Writes <id>.fbx (armature + one skinned mesh, rest pose, no clips), <id>.json (one entry per material slot, in
submesh order, same format as the scans) and the maps in <outdir>/tex.
"""
import json
import math
import os
import re
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

a = sys.argv[sys.argv.index("--") + 1:]
src, outdir, mid, height, target = a[0], a[1], a[2], float(a[3]), int(a[4])
cfg = json.loads(a[5]) if len(a) > 5 and a[5] else {}
texdir = os.path.join(outdir, "tex")
os.makedirs(texdir, exist_ok=True)
MAP = int(cfg.get("maps", 1024))

CORE = ["pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "Head"] + [
    b + "_" + s for s in "lr" for b in ("clavicle", "upperarm", "lowerarm", "hand", "thigh", "calf", "foot", "ball")]
MIXAMO = {"Hips": "pelvis", "Spine": "spine_01", "Spine1": "spine_02", "Spine2": "spine_03", "Neck": "neck_01", "Head": "Head"}
for side, s in (("Left", "l"), ("Right", "r")):
    MIXAMO.update({side + "Shoulder": "clavicle_" + s, side + "Arm": "upperarm_" + s, side + "ForeArm": "lowerarm_" + s,
                   side + "Hand": "hand_" + s, side + "UpLeg": "thigh_" + s, side + "Leg": "calf_" + s,
                   side + "Foot": "foot_" + s, side + "ToeBase": "ball_" + s})

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
scene = bpy.context.scene
arm = max((o for o in scene.objects if o.type == "ARMATURE"), key=lambda o: len(o.data.bones))
arm.animation_data_clear()
for pb in arm.pose.bones:
    pb.matrix_basis = Matrix.Identity(4)
drop = [d.lower() for d in cfg.get("drop", [])]


def deformed_by_arm(o):
    return any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers)


meshes = []
for o in list(scene.objects):
    if o.type != "MESH":
        continue
    if any(d in o.name.lower() or d in o.data.name.lower() for d in drop):
        bpy.data.objects.remove(o)
        continue
    if o.parent == arm and o.parent_type == "BONE" and not deformed_by_arm(o):
        # A rigid piece riding a bone (a hat, a sword): skin it to that bone so it survives as one skinned mesh.
        bone = o.parent_bone
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
        vg = o.vertex_groups.new(name=bone)
        vg.add(range(len(o.data.vertices)), 1.0, "REPLACE")
        mod = o.modifiers.new("Armature", "ARMATURE")
        mod.object = arm
    if deformed_by_arm(o):
        meshes.append(o)
    else:
        bpy.data.objects.remove(o)
for o in meshes:
    for m in o.modifiers:
        if m.type == "ARMATURE":
            m.object = arm

# Flatten the hierarchy into world space: no parents, no empties, transforms applied.
bpy.ops.object.select_all(action="DESELECT")
for o in meshes + [arm]:
    o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
for o in [o for o in scene.objects if o.type not in ("MESH", "ARMATURE")]:
    bpy.data.objects.remove(o)
for o in [o for o in scene.objects if o.type == "ARMATURE" and o != arm]:
    bpy.data.objects.remove(o)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Bone names: drop Sketchfab's _NN suffix, then map to the mannequin.
mapping = cfg.get("map", {})
renames = {}
used = set()
for b in arm.data.bones:
    base = re.sub(r"_\d+$", "", b.name)
    new = mapping.get(b.name) or mapping.get(base)
    if new is None:
        for k, v in MIXAMO.items():
            if base == k or (base.endswith(k) and not base[-len(k) - 1].isalnum()):
                new = v
                break
    if new is None and base.lower() in [c.lower() for c in CORE] and base.lower() not in [u.lower() for u in used]:
        new = next(c for c in CORE if c.lower() == base.lower())
    if new is None or new in used:
        new = "x_" + base  # keep extras clear of the mannequin names
    used.add(new)
    renames[b.name] = new
for o in meshes:
    for vg in o.vertex_groups:
        if vg.name in renames:
            vg.name = renames[vg.name]
for b in arm.data.bones:
    b.name = renames[b.name]

# Rigs whose fingers hang straight off the forearm get a hand bone between them.
hands = cfg.get("hands", {})
if hands:
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    for s, (fore, fingers) in hands.items():
        fore_b = eb.get(mapping.get(fore, fore)) or eb.get("lowerarm_" + s)
        roots = [eb.get(renames.get(f, f)) or eb.get("x_" + f) for f in fingers]
        roots = [r for r in roots if r is not None]
        h = eb.new("hand_" + s)
        h.head = fore_b.tail.copy()
        tip = sum((r.head for r in roots), Vector()) / len(roots) if roots else fore_b.tail + (fore_b.tail - fore_b.head) * 0.25
        if (tip - h.head).length < 1e-4:
            tip = h.head + (fore_b.tail - fore_b.head) * 0.25
        h.tail = h.head + (tip - h.head) * 1.6
        h.parent = fore_b
        h.use_deform = False
        for r in roots:
            r.parent = h
    bpy.ops.object.mode_set(mode="OBJECT")

missing = [c for c in CORE if c not in arm.data.bones and not c.startswith(("ball_", "clavicle_", "neck_", "spine_03"))]
if missing:
    print("[cast] WARNING", mid, "missing core bones", missing)

for act in list(bpy.data.actions):
    bpy.data.actions.remove(act)

# Leave out stray copies parked away from the skeleton (some files carry a second outfit metres off).
bones_w = [arm.matrix_world @ b.head_local for b in arm.data.bones]
blo = Vector((min(p.x for p in bones_w), min(p.y for p in bones_w), min(p.z for p in bones_w)))
bhi = Vector((max(p.x for p in bones_w), max(p.y for p in bones_w), max(p.z for p in bones_w)))
pad = (bhi - blo).length * 0.3
for o in list(meshes):
    c = sum((o.matrix_world @ Vector(v) for v in o.bound_box), Vector()) / 8
    if any(c[k] < blo[k] - pad or c[k] > bhi[k] + pad for k in range(3)):
        print("[cast] dropping stray mesh", o.name, tuple(round(v, 1) for v in c))
        meshes.remove(o)
        bpy.data.objects.remove(o)

# One skinned mesh.
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = mid
mods = [m for m in body.modifiers if m.type == "ARMATURE"]
for m in mods[1:]:
    body.modifiers.remove(m)


def bbox(o):
    pts = [o.matrix_world @ v.co for v in o.data.vertices]
    return Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))), Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))


# Stand up (pelvis -> head is up, the left side of the rig to +X: facing -Y, Unity's +Z), on the ground at the
# origin, scaled to height. Some rest poses lie on their back; the bones say which way is which.
def bone_w(n):
    return arm.matrix_world @ arm.data.bones[n].head_local


up = (bone_w("Head") - bone_w("pelvis")).normalized()
side = (bone_w("thigh_l") - bone_w("thigh_r")) + (bone_w("upperarm_l") - bone_w("upperarm_r"))
side = (side - up * side.dot(up)).normalized()
rot = Matrix((side, up.cross(side), up)).to_4x4()
rot = Matrix.Rotation(math.radians(cfg.get("yaw", 0)), 4, "Z") @ rot
pts = [rot @ (body.matrix_world @ v.co) for v in body.data.vertices]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
f = height / (hi.z - lo.z)
pel = rot @ bone_w("pelvis")
xf = Matrix.Scale(f, 4) @ Matrix.Translation(Vector((-pel.x, -pel.y, -lo.z))) @ rot
for o in (arm, body):
    o.matrix_world = xf @ o.matrix_world
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
body.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
body.parent = arm


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


before = tris(body)
if before > target:
    bpy.context.view_layer.objects.active = body
    m = body.modifiers.new("dec", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.ratio = target / before
    m.use_collapse_triangulate = True
    bpy.ops.object.modifier_move_to_index(modifier="dec", index=0)
    bpy.ops.object.modifier_apply(modifier="dec")

# Phones skin four bones per vertex.
bpy.context.view_layer.objects.active = body
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
for p in body.data.polygons:
    p.use_smooth = True


def image_of(mat, socket_names):
    if mat is None or not mat.use_nodes:
        return None
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        return None
    for name in socket_names:
        s = bsdf.inputs.get(name)
        if s is None or not s.is_linked:
            continue
        stack, seen = [s.links[0].from_node], set()
        while stack:
            n = stack.pop()
            if n.name in seen:
                continue
            seen.add(n.name)
            if n.type == "TEX_IMAGE" and n.image is not None:
                return n.image
            for i in n.inputs:
                if i.is_linked:
                    stack.append(i.links[0].from_node)
    return None


def alpha_linked(mat):
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if mat and mat.use_nodes else None
    return bool(bsdf and bsdf.inputs["Alpha"].is_linked)


def px(img, w):
    img = img.copy()
    img.scale(w, w)
    arr = np.empty(w * w * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    bpy.data.images.remove(img)
    return arr.reshape(w, w, 4)


def save(arr, path, colorspace="sRGB", alpha=True):
    h, w, _ = arr.shape
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=alpha)
    img.colorspace_settings.name = colorspace
    img.pixels.foreach_set(np.ascontiguousarray(arr, dtype=np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG" if path.endswith(".png") else "JPEG"
    bpy.context.scene.render.image_settings.quality = 90
    img.save()
    bpy.data.images.remove(img)


slots = []
for i, mat in enumerate(body.data.materials):
    name = "%s_%d" % (mid, i)
    entry = {"name": name, "alpha": False, "normal": False, "ms": False, "smooth": 0.18}
    base = image_of(mat, ["Base Color"])
    if base is None:
        c = mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value if mat and mat.use_nodes and "Principled BSDF" in mat.node_tree.nodes else (0.6, 0.6, 0.6, 1)
        entry["color"] = [c[0], c[1], c[2]]
        slots.append(entry)
        continue
    w = MAP
    alb = px(base, w)
    entry["alpha"] = alpha_linked(mat) and float(alb[:, :, 3].min()) < 0.5
    rough = image_of(mat, ["Roughness"])
    if rough is not None:
        r = px(rough, w)
        entry["smooth"] = round(float(np.clip(1 - r[:, :, 1].mean(), 0.05, 0.6)), 2)
    if entry["alpha"]:
        save(alb, os.path.join(texdir, name + "_albedo.png"))
    else:
        alb[:, :, 3] = 1
        save(alb, os.path.join(texdir, name + "_albedo.jpg"), alpha=False)
    nrm = image_of(mat, ["Normal"])
    if nrm is not None:
        n = px(nrm, w)
        n[:, :, 3] = 1
        save(n, os.path.join(texdir, name + "_normal.jpg"), "Non-Color", alpha=False)  # JPG like the scans: a quarter the size
        entry["normal"] = True
    slots.append(entry)

bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
body.select_set(True)
bpy.context.view_layer.objects.active = arm
arm.name = "Armature"
fbx = os.path.join(outdir, mid + ".fbx")
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                         axis_forward="-Z", axis_up="Y", object_types={"ARMATURE", "MESH"}, mesh_smooth_type="FACE",
                         add_leaf_bones=False, bake_anim=False, primary_bone_axis="Y", secondary_bone_axis="X",
                         armature_nodetype="NULL", path_mode="STRIP", embed_textures=False)
with open(os.path.join(outdir, mid + ".json"), "w") as fh:
    json.dump({"kind": "cast", "slots": slots}, fh)
d = body.dimensions
print("[cast]", mid, "tris", before, "->", tris(body), "size", tuple(round(v, 2) for v in d), "bones", len(arm.data.bones),
      "core", [c for c in CORE if c in arm.data.bones].__len__(), "slots", [s["name"] + ("*" if s["alpha"] else "") for s in slots])
