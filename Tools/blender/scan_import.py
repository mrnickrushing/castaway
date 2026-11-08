"""Turns a Poly Haven (CC0) photoscanned glTF into mobile game art for Last Tide.

  blender -b --factory-startup -P Tools/blender/scan_import.py -- <in.gltf> <outdir> <id> <kind> <size> <faces>

kind:
  solid  rocks, stumps, logs: a clean low-poly copy with fresh UVs; colour and normals baked from the full scan
         (one material: <piece>_albedo.jpg, <piece>_normal.png)
  plant  leaves on alpha cards: gentle decimation, the original UVs and maps kept (alpha-tested, two-sided)
  prop   crates, chests, ships: gentle decimation, original UVs and maps kept

A file holding several separate things (four ferns, five mossy rocks) is split into pieces <id>_a, <id>_b...; the
set is scaled as a whole so the biggest piece is <size> metres (height for plants, longest side otherwise). Each piece
sits on y = 0 at its centre. Writes <piece>.fbx and <piece>.json (one entry per material slot, in submesh order) and
the maps in <outdir>/tex (shared between the pieces of a set).
"""
import json
import os
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
src, outdir, mid, kind, size, faces = a[0], a[1], a[2], a[3], float(a[4]), int(a[5])
texdir = os.path.join(outdir, "tex")
os.makedirs(texdir, exist_ok=True)
MAP = 1024 if size < 20 else 2048
if size < 0.7:
    MAP = 512

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
# Bake parents/instancing into plain meshes in world space.
for o in meshes:
    o.data = o.data.copy()
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in [o for o in bpy.context.scene.objects if o.type != "MESH"]:
    bpy.data.objects.remove(o)


def bbox(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    return Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))), Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))


# Group the mesh objects into pieces: objects whose boxes touch (a chest's lid and body) are one piece.
lo, hi = bbox(meshes)
pad = (hi - lo).length * 0.004
boxes = [bbox([o]) for o in meshes]
parent = list(range(len(meshes)))


def find(i):
    while parent[i] != i:
        parent[i] = parent[parent[i]]
        i = parent[i]
    return i


for i in range(len(meshes)):
    for j in range(i + 1, len(meshes)):
        (l1, h1), (l2, h2) = boxes[i], boxes[j]
        if all(l1[k] - pad <= h2[k] and l2[k] - pad <= h1[k] for k in range(3)):
            parent[find(i)] = find(j)
groups = {}
for i, o in enumerate(meshes):
    groups.setdefault(find(i), []).append(o)
pieces = sorted(groups.values(), key=lambda g: (bbox(g)[0].x, bbox(g)[0].y))
# Tiny bits (a leaf, a pebble) next to big pieces join the nearest big piece rather than standing alone.
big = max(((bbox(g)[1] - bbox(g)[0]).length for g in pieces))
keep = [g for g in pieces if (bbox(g)[1] - bbox(g)[0]).length > big * 0.18]
for g in pieces:
    if g in keep:
        continue
    c = sum(((bbox(g)[0] + bbox(g)[1]) / 2,), Vector())
    near = min(keep, key=lambda k: ((bbox(k)[0] + bbox(k)[1]) / 2 - c).length)
    near.extend(g)
pieces = keep[:8]


def measure(g):
    l, h = bbox(g)
    d = h - l
    return d.z if kind == "plant" else max(d)


scale = size / max(measure(g) for g in pieces)


def join(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    return bpy.context.view_layer.objects.active


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def decimate(o, target):
    t = tris(o)
    if t <= target:
        return
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.to_mesh(o.data)
    bm.free()
    m = o.modifiers.new("dec", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.ratio = max(0.0005, target / max(1, tris(o)))
    m.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier="dec")


def thin_leaves(o, target):
    """Trees whose leaves are thousands of separate cards can't be decimated: drop a share of the leaf cards at
    random and enlarge the ones left, so the canopy keeps its fullness at a fraction of the triangles."""
    t = tris(o)
    if t <= target:
        return
    leaf_slots = {i for i, m in enumerate(o.data.materials) if m and "leaf" in m.name.lower() or (m and "leaves" in m.name.lower())}
    if not leaf_slots:
        return
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.faces.ensure_lookup_table()
    leaf_tris = sum(len(f.verts) - 2 for f in bm.faces if f.material_index in leaf_slots)
    other = t - leaf_tris
    keep = max(0.05, min(1.0, (target - other) / max(1, leaf_tris)))
    import random
    rnd = random.Random(7)
    seen = set()
    doomed = []
    grow = 1 / (keep ** 0.5)
    for f in bm.faces:
        if f.material_index not in leaf_slots or f.index in seen:
            continue
        # One card: the faces connected to this one.
        island, stack = [], [f]
        while stack:
            g = stack.pop()
            if g.index in seen:
                continue
            seen.add(g.index)
            island.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen and h.material_index in leaf_slots:
                        stack.append(h)
        if rnd.random() > keep:
            doomed.extend(island)
        else:
            vs = {v for g in island for v in g.verts}
            c = sum((v.co for v in vs), Vector()) / len(vs)
            for v in vs:
                v.co = c + (v.co - c) * grow
    bmesh.ops.delete(bm, geom=list(set(doomed)), context="FACES")
    bm.to_mesh(o.data)
    bm.free()


def image_of(mat, socket_names):
    """The image feeding a Principled BSDF input (through normal-map / separate nodes)."""
    if mat is None or not mat.use_nodes:
        return None
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        return None
    for name in socket_names:
        s = bsdf.inputs.get(name)
        if s is None or not s.is_linked:
            continue
        stack = [s.links[0].from_node]
        seen = set()
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


def px(img, w):
    """Image pixels as an (h, w, 4) float array, resized to w x w."""
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
    if img.file_format == "JPEG":
        bpy.context.scene.render.image_settings.quality = 90
    img.save()
    bpy.data.images.remove(img)


def export_maps(mat, w):
    """Writes the maps of one original material: albedo (AO folded in, alpha kept for leaves), normal, ms."""
    name = (mid + "_" + bpy.path.clean_name(mat.name)).lower()
    entry = {"name": name, "alpha": False, "normal": False, "ms": False}
    base = image_of(mat, ["Base Color"])
    arm = image_of(mat, ["Roughness", "Metallic"])
    nrm = image_of(mat, ["Normal"])
    if base is None:
        c = mat.diffuse_color if mat else (0.6, 0.6, 0.6, 1)
        entry["color"] = [c[0], c[1], c[2]]
        return entry
    alb = px(base, w)
    # Poly Haven ships leaf cutouts as a separate <name>_alpha map next to <name>_diff.
    fp = bpy.path.abspath(base.filepath)
    cand = fp.replace("_diff_", "_alpha_")
    if cand != fp and os.path.exists(cand):
        am = bpy.data.images.load(cand)
        alb[:, :, 3] = px(am, w)[:, :, 0]
        bpy.data.images.remove(am)
    alpha = alb[:, :, 3]
    entry["alpha"] = bool(alpha.min() < 0.5 and kind == "plant") or (bool(alpha.min() < 0.5) and mat.blend_method != "OPAQUE")
    if arm is not None:
        ao = px(arm, w)
        if arm.name != base.name:
            alb[:, :, :3] *= (0.35 + 0.65 * ao[:, :, 0:1])
        ms = np.zeros((w, w, 4), dtype=np.float32)
        ms[:, :, 0] = ao[:, :, 2]          # metallic
        ms[:, :, 3] = 1 - ao[:, :, 1]      # smoothness
        save(ms, os.path.join(texdir, name + "_ms.png"), "Non-Color")
        entry["ms"] = True
    if entry["alpha"]:
        save(alb, os.path.join(texdir, name + "_albedo.png"))
    else:
        alb[:, :, 3] = 1
        save(alb, os.path.join(texdir, name + "_albedo.jpg"), alpha=False)
    if nrm is not None:
        n = px(nrm, w)
        n[:, :, 3] = 1
        save(n, os.path.join(texdir, name + "_normal.png"), "Non-Color")
        entry["normal"] = True
    return entry


def bake_solid(high, name):
    """Low-poly copy with fresh UVs; colour and normals baked from the scan."""
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    low = high.copy()
    low.data = high.data.copy()
    scene.collection.objects.link(low)
    decimate(low, faces)
    bpy.ops.object.select_all(action="DESELECT")
    low.select_set(True)
    bpy.context.view_layer.objects.active = low
    while low.data.uv_layers:
        low.data.uv_layers.remove(low.data.uv_layers[0])
    low.data.uv_layers.new(name="UV")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.006)
    bpy.ops.object.mode_set(mode="OBJECT")
    low.data.materials.clear()
    mat = bpy.data.materials.new(name + "_bake")
    mat.use_nodes = True
    low.data.materials.append(mat)
    w = MAP
    results = {}
    for kind_, colorspace in (("DIFFUSE", "sRGB"), ("NORMAL", "Non-Color")):
        img = bpy.data.images.new(name + kind_, w, w, alpha=False)
        img.colorspace_settings.name = colorspace
        node = mat.node_tree.nodes.new("ShaderNodeTexImage")
        node.image = img
        mat.node_tree.nodes.active = node
        bpy.ops.object.select_all(action="DESELECT")
        high.select_set(True)
        low.select_set(True)
        bpy.context.view_layer.objects.active = low
        scene.render.bake.use_selected_to_active = True
        scene.render.bake.cage_extrusion = size * scale * 0.0 + (max(high.dimensions) * 0.02)
        scene.render.bake.max_ray_distance = max(high.dimensions) * 0.06
        scene.render.bake.margin = 8
        if kind_ == "DIFFUSE":
            bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"})
        else:
            bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT")
        arr = np.empty(w * w * 4, dtype=np.float32)
        img.pixels.foreach_get(arr)
        results[kind_] = arr.reshape(w, w, 4)
        mat.node_tree.nodes.remove(node)
    results["DIFFUSE"][:, :, 3] = 1
    save(results["DIFFUSE"], os.path.join(texdir, name + "_albedo.jpg"), alpha=False)
    results["NORMAL"][:, :, 3] = 1
    save(results["NORMAL"], os.path.join(texdir, name + "_normal.png"), "Non-Color")
    bpy.data.objects.remove(high)
    mat.name = name
    return low, [{"name": name, "alpha": False, "normal": True, "ms": False, "smooth": 0.12}]


letters = "abcdefgh"
exported = []
maps_done = {}
for k, g in enumerate(pieces):
    name = mid if len(pieces) == 1 else mid + "_" + letters[k]
    o = join(g)
    o.name = name
    # Centre on the ground and scale to game size.
    l, h = bbox([o])
    centre = Vector(((l.x + h.x) / 2, (l.y + h.y) / 2, l.z))
    o.location -= centre
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True)
    o.scale = (scale, scale, scale)
    bpy.ops.object.transform_apply(scale=True)
    before = tris(o)
    if kind == "solid":
        o, slots = bake_solid(o, name)
    else:
        decimate(o, faces)
        if kind == "plant":
            thin_leaves(o, faces)
        slots = []
        for m in o.data.materials:
            if m.name not in maps_done:
                maps_done[m.name] = export_maps(m, MAP)
            slots.append(maps_done[m.name])
    for p in o.data.polygons:
        p.use_smooth = True
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    fbx = os.path.join(outdir, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", object_types={"MESH"}, mesh_smooth_type="FACE",
                             path_mode="STRIP", embed_textures=False)
    with open(os.path.join(outdir, name + ".json"), "w") as f:
        json.dump({"kind": kind, "slots": slots}, f)
    d = o.dimensions
    print("[scan]", name, "tris", before, "->", tris(o), "size", tuple(round(v, 2) for v in d), "slots", [s["name"] + ("*" if s["alpha"] else "") for s in slots])
    bpy.data.objects.remove(o)
