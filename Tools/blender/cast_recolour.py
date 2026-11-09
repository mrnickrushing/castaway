"""Dresses the castaway like the story paintings: cream linen shirt, a red sash at the waist, dark brown breeches and
leather sandals, by recolouring the scan's own albedo (the model's shape is untouched).

  blender -b --factory-startup -P Tools/blender/cast_recolour.py -- Assets/Resources/Models/Cast/castaway.fbx \
      Assets/Resources/Models/Cast/tex/castaway_0_albedo.png

Each texel is placed on the body by rasterising the mesh's UV triangles with their height, so the sash is a band at the
waist whatever the UV layout. Cloth is found by colour (blue shirt, green camo shorts, navy sandals); once recoloured
none of those colours are left, so running it again changes nothing.
"""
import sys

import bpy
import numpy as np

a = sys.argv[sys.argv.index("--") + 1:]
fbx, albedo = a[0], a[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
o = next(o for o in bpy.data.objects if o.type == "MESH")
m = o.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
mw = o.matrix_world
uv = m.uv_layers.active.data

img = bpy.data.images.load(albedo)
W, Hh = img.size
px = np.empty(W * Hh * 4, dtype=np.float32)
img.pixels.foreach_get(px)
t = px.reshape(Hh, W, 4)  # row 0 is the bottom, like UV v

# Height above the feet (0..1 of the body) of every texel the mesh uses; -1 elsewhere.
H = np.full((Hh, W), -1.0, np.float32)
zs = [(mw @ v.co).z for v in m.vertices]
z0, z1 = min(zs), max(zs)
for p in m.polygons:
    li = list(p.loop_indices)
    for k in range(1, len(li) - 1):
        tri = [li[0], li[k], li[k + 1]]
        U = np.array([uv[i].uv[:] for i in tri]) * (W, Hh)
        Z = np.array([((mw @ m.vertices[m.loops[i].vertex_index].co).z - z0) / (z1 - z0) for i in tri])
        x0, y0 = np.floor(U.min(0)).astype(int)
        x1, y1 = np.ceil(U.max(0)).astype(int)
        xs, ys = np.meshgrid(np.arange(max(x0, 0), min(x1 + 1, W)), np.arange(max(y0, 0), min(y1 + 1, Hh)))
        if xs.size == 0:
            continue
        (ax, ay), (bx, by), (cx, cy) = U
        d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(d) < 1e-9:
            continue
        l1 = ((by - cy) * (xs + .5 - cx) + (cx - bx) * (ys + .5 - cy)) / d
        l2 = ((cy - ay) * (xs + .5 - cx) + (ax - cx) * (ys + .5 - cy)) / d
        l3 = 1 - l1 - l2
        ins = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02)
        H[ys[ins], xs[ins]] = (l1 * Z[0] + l2 * Z[1] + l3 * Z[2])[ins]

rgb = t[..., :3]
r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
mx, mn = rgb.max(-1), rgb.min(-1)
sat = (mx - mn) / np.maximum(mx, 1e-3)
lum = rgb @ np.array([0.2126, 0.7152, 0.0722], np.float32)
on = H >= 0


def near(mask, rad):
    """Share of mask texels in the (2 rad + 1)^2 box around each texel."""
    c = np.pad(mask.astype(np.float32), rad + 1).cumsum(0).cumsum(1)
    n = 2 * rad + 1
    return (c[n:, n:] - c[:-n, n:] - c[n:, :-n] + c[:-n, :-n])[:Hh, :W] / (n * n)


skin = (r > g + 0.06) & (g > b) & (sat > 0.15)
blue = on & (b > r + 0.04) & (b >= g)
shirt = blue & (H > 0.3)  # the hems' darker insides too
sandals = blue & (H < 0.06)
green = on & (g > r) & (g > b) & (sat > 0.12) & (H > 0.25) & (H < 0.62)
# Camo: the dark blotches aren't green, so take every non-skin texel in a mostly green neighbourhood.
shorts = on & ~skin & (near(green, 6) > 0.25) & (H > 0.25) & (H < 0.62)
# The sash: a hand's width at the waist, over the shirt's hem and the top of the breeches.
sash = (shirt | shorts) & (H > 0.545) & (H < 0.6)
print("[recolour] shirt", int(shirt.sum()), "shorts", int(shorts.sum()), "sash", int(sash.sum()), "sandals", int(sandals.sum()))


def dye(mask, colour, keep):
    """Repaint mask in colour, keeping `keep` of the cloth's own light and dark (folds, weave) around its mean."""
    if not mask.any():
        return
    rel = lum[mask] / max(float(lum[mask].mean()), 1e-3)
    shade = 1 + (rel - 1) * keep
    rgb[mask] = np.clip(np.array(colour, np.float32)[None, :] * shade[:, None], 0, 1)


# sRGB colours (an 8-bit PNG's pixels come back as stored).
dye(shirt & ~sash, (0.86, 0.79, 0.66), 1.0)   # undyed linen
dye(shorts & ~sash, (0.30, 0.22, 0.15), 0.35)  # dark brown breeches; the camo blotches mostly fade out
dye(sash, (0.62, 0.13, 0.10), 0.8)            # faded red cloth
dye(sandals, (0.33, 0.24, 0.16), 0.8)          # leather
t[..., :3] = rgb
img.pixels.foreach_set(t.ravel())
img.save()
print("[recolour] wrote", albedo)
