"""Turns a rigged Sketchfab animal (glb) into a Last Tide creature: one skinned mesh on its own (Generic) rig, with its
clips kept or left out.

  blender -b --factory-startup -P Tools/blender/creature_import.py -- <in.glb> <outdir> <id> <size> <tris> '<json cfg>'

size is the creature's length in metres (its longest side on the ground). cfg (all optional):
  yaw    degrees to turn it so its head faces -Y in Blender (+Z in Unity)
  clips  {source action name: game clip name}; actions not listed are dropped (none listed: no clips)
  drop   mesh name fragments to leave out
  girth  thicken the body around its bones by this factor (a real snake reads as a string from the game camera)
  rest   frame of the first clip to use as the rest pose (some rigs rest in a heap; their clip starts laid out)
  maps   texture size (default 1024)

Writes <id>.fbx, <id>.json (one entry per material slot, same format as the scans) and the maps in <outdir>/tex,
and prints the bone chain from the root (the snake is driven bone by bone in code).
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

a = sys.argv[sys.argv.index("--") + 1:]
src, outdir, mid, size, target = a[0], a[1], a[2], float(a[3]), int(a[4])
cfg = json.loads(a[5]) if len(a) > 5 and a[5] else {}
texdir = os.path.join(outdir, "tex")
os.makedirs(texdir, exist_ok=True)
MAP = int(cfg.get("maps", 1024))
clips = cfg.get("clips", {})
drop = [d.lower() for d in cfg.get("drop", [])]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
scene = bpy.context.scene
arm = max((o for o in scene.objects if o.type == "ARMATURE"), key=lambda o: len(o.data.bones))
if "rest" in cfg:
    # Bake the meshes in that pose, then make it the rig's rest pose.
    scene.frame_set(int(cfg["rest"]))
    bpy.context.view_layer.update()
    for o in [o for o in scene.objects if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers)]:
        mod = next(m for m in o.modifiers if m.type == "ARMATURE")
        rig = mod.object
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)
        o.modifiers.new("Armature", "ARMATURE").object = rig
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    arm.animation_data_clear()
for pb in arm.pose.bones:
    pb.location, pb.rotation_quaternion, pb.scale = Vector(), (1, 0, 0, 0), Vector((1, 1, 1))


def deformed(o):
    return any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers)


meshes = []
for o in list(scene.objects):
    if o.type != "MESH":
        continue
    if not deformed(o) or any(d in o.name.lower() or d in o.data.name.lower() for d in drop):
        bpy.data.objects.remove(o)
    else:
        meshes.append(o)

# Flatten into world space, one skinned mesh.
bpy.ops.object.select_all(action="DESELECT")
for o in meshes + [arm]:
    o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
for o in [o for o in scene.objects if o.type not in ("MESH", "ARMATURE")]:
    bpy.data.objects.remove(o)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = mid
for m in [m for m in body.modifiers if m.type == "ARMATURE"][1:]:
    body.modifiers.remove(m)

# Sketchfab's _NN suffixes off the bone names (fcurve paths follow the rename).
for b in arm.data.bones:
    base = b.name.rsplit("_", 1)
    if len(base) == 2 and base[1].isdigit() and base[0] not in arm.data.bones:
        b.name = base[0]
for act in list(bpy.data.actions):
    if act.name in clips:
        act.name = clips[act.name]
        act.use_fake_user = True
    else:
        bpy.data.actions.remove(act)

# Turn, stand on the ground at the origin (centred), scale to length.
rot = Matrix.Rotation(math.radians(cfg.get("yaw", 0)), 4, "Z")
pts = [rot @ (body.matrix_world @ v.co) for v in body.data.vertices]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
f = size / max(hi.x - lo.x, hi.y - lo.y)
mid_xy = (lo + hi) / 2
xf = Matrix.Scale(f, 4) @ Matrix.Translation(Vector((-mid_xy.x, -mid_xy.y, -lo.z))) @ rot
# Keyed bone locations scale with the rig (the armature object's scale is applied below).
for o in (arm, body):
    o.matrix_world = xf @ o.matrix_world
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
body.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for act in bpy.data.actions:
    for fc in act.fcurves:
        if fc.data_path.endswith(".location"):
            for k in fc.keyframe_points:
                k.co.y *= f
                k.handle_left.y *= f
                k.handle_right.y *= f
body.parent = arm

g = float(cfg.get("girth", 1))
if g != 1:
    heads = np.array([list(arm.matrix_world @ b.head_local) for b in arm.data.bones])
    for v in body.data.vertices:
        p = np.array(v.co)
        h = heads[np.argmin(((heads - p) ** 2).sum(1))]
        v.co = Vector(h + (p - h) * g)


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
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
for p in body.data.polygons:
    p.use_smooth = True


def image_of(mat, names):
    if mat is None or not mat.use_nodes:
        return None
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None and "Base Color" in names:
        # Unlit scans (an emission shader): their one image is the colour.
        return next((n.image for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and n.image is not None), None)
    for name in names:
        s = bsdf.inputs.get(name) if bsdf else None
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
            stack += [i.links[0].from_node for i in n.inputs if i.is_linked]
    return None


def px(img, w):
    img = img.copy()
    img.scale(w, w)
    arr = np.empty(w * w * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    bpy.data.images.remove(img)
    return arr.reshape(w, w, 4)


def save(arr, path, colorspace="sRGB"):
    h, w, _ = arr.shape
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=False)
    img.colorspace_settings.name = colorspace
    arr[:, :, 3] = 1
    img.pixels.foreach_set(np.ascontiguousarray(arr, dtype=np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "JPEG"
    scene.render.image_settings.quality = 90
    img.save()
    bpy.data.images.remove(img)


slots = []
for i, mat in enumerate(body.data.materials):
    name = "%s_%d" % (mid, i)
    entry = {"name": name, "alpha": False, "normal": False, "ms": False, "smooth": 0.3}
    base = image_of(mat, ["Base Color"])
    if base is None:
        entry["color"] = [0.6, 0.6, 0.6]
        slots.append(entry)
        continue
    save(px(base, MAP), os.path.join(texdir, name + "_albedo.jpg"))
    rough = image_of(mat, ["Roughness"])
    if rough is not None:
        entry["smooth"] = round(float(np.clip(1 - px(rough, 256)[:, :, 1].mean(), 0.05, 0.7)), 2)
    nrm = image_of(mat, ["Normal"])
    if nrm is not None:
        save(px(nrm, MAP), os.path.join(texdir, name + "_normal.jpg"), "Non-Color")
        entry["normal"] = True
    slots.append(entry)

# The chain from the root bone, always into the child with the most bones below it (a spine, a tail).
def count(b):
    return 1 + sum(count(c) for c in b.children)


roots = [b for b in arm.data.bones if b.parent is None]
b, chain = max(roots, key=count), []
while b is not None:
    chain.append(b.name)
    b = max(b.children, key=count) if b.children else None

arm.name = "Armature"
if bpy.data.actions:
    arm.animation_data_create()
    arm.animation_data.action = bpy.data.actions[0]
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
body.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, mid + ".fbx"), use_selection=True, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                         object_types={"ARMATURE", "MESH"}, mesh_smooth_type="FACE", add_leaf_bones=False,
                         primary_bone_axis="Y", secondary_bone_axis="X", armature_nodetype="NULL", path_mode="STRIP",
                         embed_textures=False, bake_anim=bool(bpy.data.actions), bake_anim_use_all_actions=True,
                         bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.5)
with open(os.path.join(outdir, mid + ".json"), "w") as fh:
    json.dump({"kind": "creature", "slots": slots}, fh)
print("[creature]", mid, "tris", before, "->", tris(body), "size", tuple(round(v, 2) for v in body.dimensions),
      "bones", len(arm.data.bones), "clips", [x.name for x in bpy.data.actions], "slots", [s["name"] for s in slots])
print("[creature] chain", len(chain), chain[:6], "...", chain[-3:])
