"""
Builds the game's stylised models in Blender (bpy, headless) and exports them for the engine-free art layer.

    python3.11 -m venv .blenv && .blenv/bin/pip install bpy==5.0.1
    .blenv/bin/python Tools/Blender/build_models.py [--render out_dir] [--only Rider0,Board]

Coordinates: every number in this file is in GAME (Unity) units and axes - X right, Y up, Z forward - so the rig
matches ArtLibrary exactly (feet at y = 0, rider faces +Z; groups Pose > Torso > Head / ArmL / ArmR with the same
pivots; board groups Wheel0..Wheel3 at the hubs). Meshes are written as Assets/Resources/Models/<Name>.bytes
(format: Game.Art.ArtAsset) and a .blend source is saved in Tools/Blender/source/. Blender itself only sees the
numbers; the optional --render pass mirrors them into Blender's Z-up frame for a Cycles beauty shot.

Shapes: organic parts (heads, bodies, limbs, shoes) are metaballs (soft blends, cartoon look) converted to meshes
and decimated to a triangle budget; hard parts (soles, brims, deck, trucks, wheels) are bevelled primitives.
"""
import argparse
import math
import os
import struct
import sys

import bpy  # must come first: it registers bmesh and mathutils
import bmesh
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Models")
SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "source")


def hexcol(rgb):
    return ((rgb >> 16) & 255) / 255.0, ((rgb >> 8) & 255) / 255.0, (rgb & 255) / 255.0


# ---------------------------------------------------------------------------------------------- model container

class Model:
    def __init__(self, name):
        self.name = name
        self.groups = []      # (name, parent, pivot relative to parent)
        self.parts = []       # (group, name, rgb, smoothness, bpy mesh in model space)

    def group(self, name, pivot, parent=""):
        self.groups.append((name, parent, Vector(pivot)))

    def model_pivot(self, group):
        """Rest-pose model-space position of a group's pivot."""
        p = Vector((0, 0, 0))
        while group:
            g = next(g for g in self.groups if g[0] == group)
            p += g[2]
            group = g[1]
        return p

    def add(self, group, name, rgb, smooth, mesh):
        self.parts.append((group, name, rgb, smooth, mesh))

    def triangles(self):
        return sum(len(m.polygons) for *_, m in self.parts)


# ---------------------------------------------------------------------------------------------- primitives (bmesh)

def bm_to_mesh(bm, name):
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return me


def split_sharp(bm, angle_deg=42.0):
    """Hard edges keep their own vertices (the engine recomputes normals from shared vertices)."""
    edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(angle_deg)]
    if edges:
        bmesh.ops.split_edges(bm, edges=edges)


def xform(bm, matrix):
    bmesh.ops.transform(bm, matrix=matrix, verts=bm.verts[:])


def TRS(pos, rot=None, scale=(1, 1, 1)):
    m = Matrix.Translation(Vector(pos))
    if rot is not None:
        m = m @ rot.to_matrix().to_4x4()
    return m @ Matrix.Diagonal((scale[0], scale[1], scale[2], 1.0))


def euler(x, y, z):
    """Unity-style Euler (degrees, applied Z then X then Y) as a rotation in game axes."""
    q = Quaternion((0, 1, 0), math.radians(y)) @ Quaternion((1, 0, 0), math.radians(x)) @ Quaternion((0, 0, 1), math.radians(z))
    return q


def rounded_box(name, center, size, bevel, rot=None, segments=3):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    xform(bm, Matrix.Diagonal((size[0], size[1], size[2], 1.0)))
    bevel = min(bevel, min(size) * 0.49)
    bmesh.ops.bevel(bm, geom=bm.edges[:] + bm.verts[:], offset=bevel, segments=segments, profile=0.5, affect='EDGES', clamp_overlap=True)
    split_sharp(bm)
    xform(bm, TRS(center, rot))
    return bm_to_mesh(bm, name)


def ellipsoid(name, center, radii, rot=None, segs=20, rings=12, cut_below=None):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    # bmesh spheres are built around Z; turn them so the poles sit on Y (game up)
    xform(bm, Matrix.Rotation(math.radians(-90), 4, 'X'))
    if cut_below is not None:  # keep the top part only (cap domes); the open rim sits inside the head
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < cut_below], context='VERTS')
    xform(bm, TRS(center, rot, radii))
    return bm_to_mesh(bm, name)


def cylinder(name, center, radius, depth, axis='Y', rot=None, segs=24, bevel=0.0, radius2=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=radius,
                          radius2=radius if radius2 is None else radius2, depth=depth)
    if bevel > 0:
        rims = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(60)]
        bmesh.ops.bevel(bm, geom=rims, offset=bevel, segments=3, profile=0.5, affect='EDGES', clamp_overlap=True)
    split_sharp(bm)
    # bmesh cones run along Z; point them along the requested game axis
    if axis == 'Y':
        xform(bm, Matrix.Rotation(math.radians(-90), 4, 'X'))
    elif axis == 'X':
        xform(bm, Matrix.Rotation(math.radians(90), 4, 'Y'))
    xform(bm, TRS(center, rot))
    return bm_to_mesh(bm, name)


# ---------------------------------------------------------------------------------------------- metaballs

_META_K = None


def meta_scale():
    """Surface radius of a metaball of radius 1 (stiffness 2, threshold 0.6), measured once."""
    global _META_K
    if _META_K is None:
        mb = bpy.data.metaballs.new("Calib")
        mb.resolution = mb.render_resolution = 0.02
        e = mb.elements.new(type='BALL')
        e.radius, e.stiffness = 1.0, 2.0
        ob = bpy.data.objects.new("Calib", mb)
        bpy.context.scene.collection.objects.link(ob)
        bpy.context.view_layer.update()
        me = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        _META_K = max(abs(v.co.x) for v in me.vertices)
        bpy.data.objects.remove(ob)
        bpy.data.metaballs.remove(mb)
        bpy.data.meshes.remove(me)
    return _META_K


_family = [0]


def blob(name, elements, max_tris, resolution=0.012):
    """
    Soft organic shape: metaball elements that melt into each other. Each element is
    ('ball', centre, radius) | ('ell', centre, (rx, ry, rz), rot) | ('cap', a, b, radius).
    """
    k = meta_scale()
    _family[0] += 1
    fam = f"F{_family[0]}x{name}".replace(".", "")
    mb = bpy.data.metaballs.new(fam)
    mb.resolution = mb.render_resolution = resolution
    mb.threshold = 0.6
    for el in elements:
        kind = el[0]
        if kind == 'ball':
            e = mb.elements.new(type='BALL')
            e.co, e.radius = Vector(el[1]), el[2] / k
        elif kind == 'ell':
            e = mb.elements.new(type='ELLIPSOID')
            e.co, e.radius = Vector(el[1]), 1.0 / k
            e.size_x, e.size_y, e.size_z = el[2]
            if len(el) > 3 and el[3] is not None:
                e.rotation = el[3]
        else:  # capsule from a to b
            a, b, r = Vector(el[1]), Vector(el[2]), el[3]
            d = b - a
            e = mb.elements.new(type='CAPSULE')
            e.co = (a + b) * 0.5
            e.radius = r / k
            e.size_x = d.length * 0.5
            e.rotation = Vector((1, 0, 0)).rotation_difference(d.normalized())
        e.stiffness = 2.0
    ob = bpy.data.objects.new(fam, mb)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(ob)
    bpy.data.metaballs.remove(mb)
    return decimate(me, max_tris, name)


def decimate(me, max_tris, name):
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    if tris > max_tris:
        ob = bpy.data.objects.new(name + "_tmp", me)
        bpy.context.scene.collection.objects.link(ob)
        mod = ob.modifiers.new("dec", 'DECIMATE')
        mod.ratio = max_tris / tris
        bpy.context.view_layer.update()
        out = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        bpy.data.objects.remove(ob)
        bpy.data.meshes.remove(me)
        me = out
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bpy.data.meshes.remove(me)
    return bm_to_mesh(bm, name)


def surface(mesh):
    """Ray caster onto a mesh: front(x, y) -> z of the surface seen from +Z (face features sit on the real head)."""
    tree = BVHTree.FromPolygons([v.co.copy() for v in mesh.vertices], [p.vertices[:] for p in mesh.polygons])

    def front(x, y, inset=0.0):
        hit = tree.ray_cast(Vector((x, y, 3.0)), Vector((0, 0, -1)))
        loc, normal = hit[0], hit[1]
        if loc is None:
            raise ValueError(f"no surface at {x:.3f}, {y:.3f}")
        return loc.z - inset, normal
    return front


# ---------------------------------------------------------------------------------------------- riders

OUTFITS = [
    dict(name="Rider0", skin=0xF5CBA7, shirt=0xF7F7F4, sleeve=0x2F6FE0, stripe=0x2F6FE0, pants=0x3A5A8C,
         shoe=0xF7F7F4, accent=0x2F6FE0, hair=0x6B4226, hat=0x2F6FE0, hat_accent=0xF7F7F4, headwear="cap", width=1.0),
]

TORSO_PIVOT = (0.0, 0.58, -0.08)
HEAD_PIVOT = (0.0, 1.1, 0.0)


def build_rider(o):
    m = Model(o["name"])
    w = o["width"]
    m.group("Pose", (0, 0, 0))
    m.group("Torso", TORSO_PIVOT, "Pose")
    m.group("Head", Vector(HEAD_PIVOT) - Vector(TORSO_PIVOT), "Torso")
    m.group("ArmL", Vector((-0.25 * w, 0.95, 0.0)) - Vector(TORSO_PIVOT), "Torso")
    m.group("ArmR", Vector((0.25 * w, 0.95, 0.0)) - Vector(TORSO_PIVOT), "Torso")

    # ---- feet: chunky sneakers (white sole, soft upper, accent swoosh and collar)
    for s in (-1, 1):
        x = s * 0.16
        m.add("Pose", "Sole", 0xF2F2EE, 0.2, rounded_box("Sole", (x, 0.045, 0.05), (0.26, 0.09, 0.45), 0.04))
        m.add("Pose", "Toecap", 0xDCDCD6, 0.2, rounded_box("Toecap", (x, 0.06, 0.245), (0.2, 0.05, 0.08), 0.02))
        m.add("Pose", "Sneaker", o["shoe"], 0.25, blob("Sneaker", [
            ('ell', (x, 0.15, -0.01), (0.12, 0.1, 0.16)),
            ('ball', (x, 0.125, 0.15), 0.1),
        ], 700))
        m.add("Pose", "Swoosh", o["accent"], 0.3, rounded_box("Swoosh", (x + s * 0.112, 0.14, 0.04), (0.02, 0.05, 0.2), 0.009,
                                                              rot=euler(-12, 0, 0)))
        m.add("Pose", "Tongue", o["accent"], 0.3, rounded_box("ShoeTongue", (x, 0.2, 0.06), (0.11, 0.05, 0.1), 0.02, rot=euler(-30, 0, 0)))

    # ---- legs and hips: one soft pair of jeans, crouched like the procedural rig
    leg = []
    for s in (-1, 1):
        ankle, knee, hip = (s * 0.16, 0.22, 0.0), (s * 0.19, 0.39, 0.17), (s * 0.11 * w, 0.56, -0.1)
        leg += [('cap', ankle, knee, 0.085), ('cap', knee, hip, 0.1)]
    leg.append(('ell', (0, 0.6, -0.1), (0.21 * w, 0.11, 0.15)))
    m.add("Pose", "Jeans", o["pants"], 0.15, blob("Jeans", leg, 1400))
    for s in (-1, 1):  # rolled cuffs
        m.add("Pose", "Cuff", o["pants"] - 0x101010 if o["pants"] > 0x101010 else o["pants"], 0.15,
              cylinder("Cuff", (s * 0.16, 0.235, 0.012), 0.098, 0.055, segs=20, bevel=0.015, rot=euler(-40, 0, 0)))

    # ---- torso: soft tee with a chest stripe
    m.add("Torso", "Shirt", o["shirt"], 0.15, blob("Shirt", [
        ('ell', (0, 0.84, -0.02), (0.25 * w, 0.27, 0.18)),
        ('cap', (-0.17 * w, 0.97, -0.01), (0.17 * w, 0.97, -0.01), 0.1),
        ('ell', (0, 0.66, -0.06), (0.22 * w, 0.1, 0.16)),
    ], 1300))
    m.add("Torso", "Stripe", o["stripe"], 0.15, ellipsoid("Stripe", (0, 0.865, -0.02), (0.272 * w, 0.06, 0.2), segs=32, rings=10))
    m.add("Torso", "Neck", o["skin"], 0.25, cylinder("Neck", (0, 1.07, -0.005), 0.075, 0.1, segs=16))

    # ---- arms: sleeve, forearm and a round mitten hand, hanging from the shoulder
    for s, g in ((-1, "ArmL"), (1, "ArmR")):
        sh = Vector((s * 0.25 * w, 0.95, 0.0))
        elbow, wrist = sh + Vector((s * 0.03, -0.24, 0.0)), sh + Vector((s * 0.045, -0.44, 0.03))
        m.add(g, "Sleeve", o["sleeve"], 0.15, blob("Sleeve", [('ball', sh, 0.095), ('cap', sh, elbow, 0.08)], 500))
        m.add(g, "Arm", o["skin"], 0.25, blob("Arm", [
            ('cap', elbow, wrist, 0.05),
            ('ell', wrist + Vector((0, -0.085, 0.01)), (0.085, 0.09, 0.08)),
            ('ball', wrist + Vector((-s * 0.07, -0.05, 0.045)), 0.035),  # thumb
        ], 600))

    # ---- head: big soft head with ears and nose; face features are placed ON the head surface
    hy = 1.36
    head = blob("Head", [
        ('ell', (0, hy, 0.0), (0.3, 0.285, 0.27)),
        ('ell', (0, hy - 0.11, 0.03), (0.21, 0.12, 0.19)),        # cheeks / jaw
        ('ell', (-0.29, hy - 0.02, -0.01), (0.045, 0.075, 0.055)),  # ears
        ('ell', (0.29, hy - 0.02, -0.01), (0.045, 0.075, 0.055)),
    ], 2200, resolution=0.01)
    m.add("Head", "Head", o["skin"], 0.25, head)
    front = surface(head)
    ey = hy + 0.0
    for s in (-1, 1):
        ex = s * 0.105
        z, _ = front(ex, ey)
        m.add("Head", "Eye", 0x1B1C22, 0.8, ellipsoid("Eye", (ex, ey, z - 0.012), (0.052, 0.078, 0.03), segs=18, rings=12,
                                                       rot=euler(0, s * -14, 0)))
        m.add("Head", "Glint", 0xFFFFFF, 0.5, ellipsoid("Glint", (ex + 0.017, ey + 0.03, z + 0.014), (0.017, 0.022, 0.008), segs=10, rings=6))
        bz, _ = front(ex, ey + 0.11)
        m.add("Head", "Brow", o["hair"], 0.2, rounded_box("Brow", (ex, ey + 0.11, bz + 0.004), (0.095, 0.026, 0.03), 0.012,
                                                           rot=euler(-22, s * -14, s * -8)))
        cz, _ = front(s * 0.165, hy - 0.08)
        m.add("Head", "Blush", 0xFF9C8C, 0.2, ellipsoid("Blush", (s * 0.165, hy - 0.08, cz - 0.004), (0.05, 0.028, 0.012), segs=14, rings=8,
                                                       rot=euler(0, s * -30, 0)))
    nz, _ = front(0, hy - 0.05)
    m.add("Head", "Nose", o["skin"], 0.25, ellipsoid("Nose", (0, hy - 0.05, nz - 0.01), (0.04, 0.035, 0.035), segs=14, rings=8))
    mz, _ = front(0, hy - 0.15)
    m.add("Head", "Mouth", 0x7A2328, 0.3, ellipsoid("Mouth", (0, hy - 0.15, mz - 0.006), (0.07, 0.04, 0.016), segs=18, rings=10,
                                                     cut_below=None, rot=euler(-12, 0, 0)))
    m.add("Head", "Tongue", 0xE86A6A, 0.3, ellipsoid("Tongue", (0, hy - 0.168, mz + 0.004), (0.035, 0.016, 0.01), segs=12, rings=6))
    m.add("Head", "Teeth", 0xFFFFFF, 0.3, rounded_box("Teeth", (0, hy - 0.13, mz + 0.006), (0.08, 0.02, 0.012), 0.005, rot=euler(-12, 0, 0)))

    if o["headwear"] == "cap":
        m.add("Head", "Hair", o["hair"], 0.2, blob("Hair", [
            ('ell', (0, hy + 0.04, -0.05), (0.305, 0.25, 0.27)),
            ('ell', (-0.255, hy - 0.04, -0.03), (0.06, 0.1, 0.12)),
            ('ell', (0.255, hy - 0.04, -0.03), (0.06, 0.1, 0.12)),
        ], 900))
        m.add("Head", "Cap", o["hat"], 0.25, ellipsoid("Cap", (0, hy + 0.1, -0.01), (0.32, 0.29, 0.31), segs=32, rings=18, cut_below=0.08))
        m.add("Head", "CapBand", o["hat"], 0.25, cylinder("CapBand", (0, hy + 0.135, -0.01), 0.318, 0.06, segs=32, bevel=0.01))
        m.add("Head", "Brim", o["hat"], 0.3, rounded_box("Brim", (0, hy + 0.15, 0.36), (0.42, 0.035, 0.27), 0.016, rot=euler(6, 0, 0)))
        m.add("Head", "Badge", o["hat_accent"], 0.3, rounded_box("Badge", (0, hy + 0.26, 0.255), (0.15, 0.1, 0.03), 0.02, rot=euler(-36, 0, 0)))
        m.add("Head", "Button", o["hat_accent"], 0.3, ellipsoid("Button", (0, hy + 0.39, -0.01), (0.04, 0.025, 0.04), segs=12, rings=6))
    return m


# ---------------------------------------------------------------------------------------------- board

def stadium_outline(length, width, n_end=24):
    """Deck outline (XZ): straight rails with rounded, slightly pointed ends (pintail-ish), clockwise from above."""
    hw, slab = width * 0.5, length - width
    pts = []
    for end in (1, -1):
        for i in range(n_end + 1):
            a = math.pi * i / n_end
            x = hw * math.cos(a) * end
            z = end * (slab * 0.5 + hw * math.sin(a) * 1.08)
            pts.append((x, z))
    return pts


def deck_mesh(name, length, width, y0, y1, bevel, inset=0.0):
    pts = stadium_outline(length - 2 * inset, width - 2 * inset)
    bm = bmesh.new()
    bottom = [bm.verts.new((x, y0, z)) for x, z in pts]
    top = [bm.verts.new((x, y1, z)) for x, z in pts]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bottom)))
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if bevel > 0:
        rims = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(60)]
        bmesh.ops.bevel(bm, geom=rims, offset=bevel, segments=4, profile=0.5, affect='EDGES', clamp_overlap=True)
    split_sharp(bm)
    return bm_to_mesh(bm, name)


def band_mesh(name, length, width, y, z_center, angle_deg, band_w, inset):
    """A diagonal grip band clipped to the deck outline (scanline: one quad strip across the width)."""
    hw = width * 0.5 - inset
    t = math.tan(math.radians(angle_deg))
    bm = bmesh.new()
    quads = []
    steps = 8
    for i in range(steps):
        x0, x1 = -hw + 2 * hw * i / steps, -hw + 2 * hw * (i + 1) / steps
        z00, z01 = z_center + x0 * t - band_w * 0.5, z_center + x0 * t + band_w * 0.5
        z10, z11 = z_center + x1 * t - band_w * 0.5, z_center + x1 * t + band_w * 0.5
        v = [bm.verts.new((x0, y, z00)), bm.verts.new((x0, y, z01)), bm.verts.new((x1, y, z11)), bm.verts.new((x1, y, z10))]
        quads.append(bm.faces.new(v))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:
        if f.normal.y < 0:
            f.normal_flip()
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    return bm_to_mesh(bm, name)


def build_board(width=2.7, length=6.75, deck_top=0.85, r=0.35):
    m = Model("Board")
    thick = 0.16
    y0, y1 = deck_top - thick, deck_top
    m.add("", "Deck", 0xE7B57A, 0.35, deck_mesh("Deck", length, width, y0, y1, 0.05))
    m.add("", "DeckEdge", 0xC98A4B, 0.3, deck_mesh("DeckEdge", length - 0.01, width - 0.01, y0 - 0.015, y0 + 0.05, 0.02))
    m.add("", "Stripe", 0xE53935, 0.35, deck_mesh("Stripe", length + 0.012, width + 0.012, y0 + 0.055, y0 + 0.09, 0.008))
    m.add("", "Grip", 0x2B2D35, 0.1, deck_mesh("Grip", length, width, y1 - 0.01, y1 + 0.012, 0.012, inset=0.11))
    slab = length - width
    for s in (-1, 1):
        for b in range(2):
            zc = s * (slab * 0.14 + b * 0.62)
            m.add("", "Band", 0xE53935, 0.3, band_mesh("Band", length, width, y1 + 0.014, zc, 28, 0.3, 0.16))

    axle_z = length * 0.35
    k = 0
    for zs in (-1, 1):
        z = zs * axle_z
        base_h = deck_top - thick - r + 0.1
        m.add("", "Baseplate", 0x6D737D, 0.5, rounded_box("Baseplate", (0, deck_top - thick - 0.035, z), (0.62, 0.07, 0.46), 0.025))
        m.add("", "Riser", 0x26272C, 0.2, rounded_box("Riser", (0, deck_top - thick - 0.005, z), (0.66, 0.02, 0.5), 0.008))
        m.add("", "Kingpin", 0xB8BEC8, 0.6, cylinder("Kingpin", (0, r + base_h * 0.5, z - zs * 0.05), 0.11, base_h, segs=16, bevel=0.02))
        m.add("", "Bushing", 0xFFC21A, 0.3, cylinder("Bushing", (0, r + base_h * 0.75, z), 0.12, 0.09, segs=16, bevel=0.025))
        m.add("", "Hanger", 0xB8BEC8, 0.6, rounded_box("Hanger", (0, r + 0.02, z), (width * 0.62, 0.16, 0.2), 0.06))
        m.add("", "Axle", 0x8E949E, 0.6, cylinder("Axle", (0, r, z), 0.04, width * 0.86, axis='X', segs=12))
        for xs in (-1, 1):
            g = f"Wheel{k}"
            k += 1
            m.group(g, (xs * (width * 0.5 - 0.08), r, z))
            # parts in model space at rest; the exporter moves them into the wheel group's space
            hub = Vector((xs * (width * 0.5 - 0.08), r, z))
            m.add(g, "Tire", 0xE8322F, 0.45, cylinder("Tire", hub, r, 0.42, axis='X', segs=40, bevel=0.1))
            m.add(g, "Hub", 0xB8BEC8, 0.6, cylinder("Hub", hub + Vector((xs * 0.03, 0, 0)), r * 0.5, 0.42, axis='X', segs=24, bevel=0.03))
            for spoke in range(5):
                a = spoke * 72
                m.add(g, "Spoke", 0xF7F7F4, 0.4, rounded_box("Spoke", hub + Vector((xs * 0.22, 0, 0)), (0.02, r * 0.85, 0.07), 0.008,
                                                            rot=euler(a, 0, 0)))
    return m


# ---------------------------------------------------------------------------------------------- export

def write_asset(m):
    """Game.Art.ArtAsset v1 (little endian): see ArtAsset.cs."""
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, m.name + ".bytes")

    def s(f, text):
        b = text.encode("utf-8")
        f.write(struct.pack("<i", len(b)))
        f.write(b)

    with open(path, "wb") as f:
        f.write(b"DPBM")
        f.write(struct.pack("<i", 1))
        s(f, m.name)
        f.write(struct.pack("<i", len(m.groups)))
        for name, parent, pivot in m.groups:
            s(f, name)
            s(f, parent)
            f.write(struct.pack("<3f", pivot.x, pivot.y, pivot.z))
        f.write(struct.pack("<i", len(m.parts)))
        for group, name, rgb, smooth, me in m.parts:
            origin = m.model_pivot(group) if group else Vector((0, 0, 0))
            s(f, group)
            s(f, name)
            f.write(struct.pack("<If", int(rgb) & 0xFFFFFF, float(smooth)))
            f.write(struct.pack("<i", len(me.vertices)))
            for v in me.vertices:
                c = v.co - origin
                f.write(struct.pack("<3f", c.x, c.y, c.z))
            tris = [p.vertices for p in me.polygons]
            f.write(struct.pack("<i", len(tris) * 3))
            for t in tris:
                f.write(struct.pack("<3i", t[0], t[1], t[2]))
    return path


# ---------------------------------------------------------------------------------------------- beauty render

G2B = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def link_scene(models, layout):
    """Puts every part into the Blender scene (game axes mirrored into Blender's Z-up, faces flipped back), one
    collection per model, so the .blend source opens as an editable scene."""
    scn = bpy.context.scene
    for ob in list(scn.collection.all_objects):
        bpy.data.objects.remove(ob)
    for c in list(scn.collection.children):
        bpy.data.collections.remove(c)
    mats = {}
    for m, offset in zip(models, layout):
        col = bpy.data.collections.new(m.name)
        scn.collection.children.link(col)
        for group, name, rgb, smooth, me in m.parts:
            key = (rgb, round(smooth, 2))
            if key not in mats:
                mat = bpy.data.materials.new(f"M{rgb:06X}")
                mat.use_nodes = True
                bsdf = mat.node_tree.nodes["Principled BSDF"]
                bsdf.inputs["Base Color"].default_value = (*[c ** 2.2 for c in hexcol(rgb)], 1)
                bsdf.inputs["Roughness"].default_value = 1.0 - smooth * 0.7
                mats[key] = mat
            copy = me.copy()
            bm = bmesh.new()
            bm.from_mesh(copy)
            bmesh.ops.transform(bm, matrix=G2B @ Matrix.Translation(offset), verts=bm.verts[:])
            bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
            bm.to_mesh(copy)
            bm.free()
            for p in copy.polygons:
                p.use_smooth = True
            copy.materials.append(mats[key])
            ob = bpy.data.objects.new(f"{group or 'Deck'}_{name}", copy)
            col.objects.link(ob)


def render(models, out_png, layout, camera=None, target=None, lens=50):
    """Cycles shot of the models placed at the layout offsets (game units)."""
    scn = bpy.context.scene
    link_scene(models, layout)

    ground = bpy.data.meshes.new("Ground")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=40)
    bm.to_mesh(ground)
    bm.free()
    gm = bpy.data.materials.new("GroundMat")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.08, 0.09, 0.11, 1)
    ground.materials.append(gm)
    scn.collection.objects.link(bpy.data.objects.new("Ground", ground))

    world = bpy.data.worlds.new("Sky")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.72, 0.95, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    scn.world = world
    sun = bpy.data.lights.new("Sun", 'SUN')
    sun.energy = 4.0
    sun.angle = math.radians(8)
    so = bpy.data.objects.new("Sun", sun)
    so.rotation_euler = (math.radians(50), math.radians(-10), math.radians(-35))
    scn.collection.objects.link(so)

    cam = bpy.data.cameras.new("Cam")
    cam.lens = lens
    co = bpy.data.objects.new("Cam", cam)
    scn.collection.objects.link(co)
    co.location = Vector(camera or LAYOUT_CAMERA)
    target = Vector(target or LAYOUT_TARGET)
    co.rotation_euler = (target - co.location).to_track_quat('-Z', 'Y').to_euler()
    scn.camera = co

    scn.render.engine = 'CYCLES'
    scn.cycles.device = 'CPU'
    scn.cycles.samples = 48
    try:
        scn.cycles.use_denoising = True
    except Exception:
        pass
    scn.render.resolution_x, scn.render.resolution_y = 1400, 900
    scn.render.filepath = out_png
    scn.view_settings.view_transform = 'Standard'
    bpy.ops.render.render(write_still=True)


LAYOUT_CAMERA = (5.5, -8.5, 4.2)
LAYOUT_TARGET = (0.0, 0.0, 1.0)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--render", help="folder for a Cycles beauty shot")
    ap.add_argument("--only", help="comma-separated model names")
    a = ap.parse_args(argv)
    bpy.ops.wm.read_factory_settings(use_empty=True)

    models = [build_rider(o) for o in OUTFITS] + [build_board()]
    if a.only:
        keep = set(a.only.split(","))
        models = [m for m in models if m.name in keep]
    for m in models:
        path = write_asset(m)
        print(f"{m.name}: {len(m.parts)} parts, {m.triangles()} triangles -> {os.path.relpath(path, ROOT)} ({os.path.getsize(path) // 1024} KB)")

    os.makedirs(SRC, exist_ok=True)
    link_scene(models, [Vector((i * 4.0, 0, 0)) for i in range(len(models))])
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC, "models.blend"), compress=True)

    if a.render:
        os.makedirs(a.render, exist_ok=True)
        riders = [m for m in models if m.name.startswith("Rider")]
        board = [m for m in models if m.name == "Board"]
        # rider standing on the deck (deck top at 0.85), a second copy beside the board for a close look
        layout_models, offsets = [], []
        for b in board:
            layout_models.append(b); offsets.append(Vector((0, 0, 0)))
        for rm in riders:
            layout_models.append(rm); offsets.append(Vector((0.4, 0.86, 0.6)))
            layout_models.append(rm); offsets.append(Vector((2.6, 0, -1.2)))
        render(layout_models, os.path.join(a.render, "models.png"), offsets)
        # close-up from the front (riders face game +Z = Blender +Y)
        if riders:
            render(riders, os.path.join(a.render, "rider_front.png"), [Vector((0, 0, 0))] * len(riders),
                   camera=(1.6, 4.2, 1.5), target=(0.0, 0.0, 0.85), lens=60)


if __name__ == "__main__":
    main()
