"""
Bouwt de modulaire gebouwkit (Fase 1 van het VR School asset-plan) procedureel op.

Gebruik (Blender 5.x):
  - Scripting-tab -> Open dit bestand -> Run Script
  - of via Blender MCP: exec(open(<pad>).read())

Resultaat:
  - blender/models/environment/SM_Build_Kit.blend
  - mixed_reality/Assets/Art/Building/<AssetNaam>.fbx  (1 FBX per asset)

Conventies:
  - 1 unit = 1 m, Z-up in Blender. "Voorkant" (kamerzijde) = -Y.
  - Muren: lengte langs +X vanaf de oorsprong, dikte 0.2 m gecentreerd op de gridlijn (Y=0).
  - Wandprops (radiator, stopcontact, ...): oorsprong achteraan-midden, object steekt uit naar -Y.
  - Plafondprops (TL, rookmelder): oorsprong bovenaan-midden.
  - Deuren: oorsprong op de scharnierzijde onderaan.
  - UV0 = textures (cube project, 1 m schaal), UV1 = lightmap (smart project, geen overlap).
  - UCX_<naam>_NN = convexe collision-boxen als child.
"""

import json
import math
import os

import bmesh
import bpy
from mathutils import Vector

REPO = r"C:\0_school\3deJaar\Mixed_Reality\project"
BLEND_PATH = os.path.join(REPO, "blender", "models", "environment", "SM_Build_Kit.blend")
EXPORT_DIR = os.path.join(REPO, "mixed_reality", "Assets", "Art", "Building")
KIT_COLLECTION = "Build_Kit"

# name: (base rgba, metallic, roughness, emission rgb or None, emission strength)
MATERIALS = {
    "M_Wall_Paint_White": ((0.82, 0.81, 0.77, 1), 0.0, 0.9, None, 0),
    "M_Floor_Linoleum": ((0.30, 0.36, 0.34, 1), 0.0, 0.55, None, 0),
    "M_Concrete": ((0.50, 0.50, 0.48, 1), 0.0, 0.95, None, 0),
    "M_Wood_Light": ((0.62, 0.45, 0.28, 1), 0.0, 0.6, None, 0),
    "M_Metal_Painted": ((0.28, 0.35, 0.42, 1), 0.3, 0.5, None, 0),
    "M_Metal_White": ((0.88, 0.88, 0.86, 1), 0.2, 0.4, None, 0),
    "M_Metal_Chrome": ((0.80, 0.80, 0.80, 1), 1.0, 0.15, None, 0),
    "M_Glass": ((0.80, 0.90, 0.95, 0.2), 0.0, 0.05, None, 0),
    "M_Plastic_White": ((0.90, 0.90, 0.88, 1), 0.0, 0.5, None, 0),
    "M_Rubber_Black": ((0.03, 0.03, 0.03, 1), 0.0, 0.9, None, 0),
    "M_Paint_Red": ((0.70, 0.03, 0.02, 1), 0.0, 0.35, None, 0),
    "M_Emissive_Light": ((1.0, 0.98, 0.92, 1), 0.0, 0.5, (1.0, 0.98, 0.92), 3.0),
    "M_ExitSign": ((0.05, 0.75, 0.25, 1), 0.0, 0.4, (0.05, 0.9, 0.3), 2.0),
}

WT = 0.2      # wanddikte
WH = 3.0      # wandhoogte


# ----------------------------------------------------------------------------
# Materialen
# ----------------------------------------------------------------------------

def get_mat(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    rgba, metal, rough, emis, emis_str = MATERIALS[name]
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Alpha"].default_value = rgba[3]
    if emis:
        bsdf.inputs["Emission Color"].default_value = (*emis, 1)
        bsdf.inputs["Emission Strength"].default_value = emis_str
    if rgba[3] < 1:
        mat.surface_render_method = 'BLENDED'
    mat.diffuse_color = rgba
    return mat


# ----------------------------------------------------------------------------
# Mesh-bouwer
# ----------------------------------------------------------------------------

class MB:
    """Kleine bmesh-helper: dozen, cilinders, muren met openingen, profielen."""

    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []

    def mi(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _face(self, verts, mat, smooth=False):
        f = self.bm.faces.new(verts)
        f.material_index = self.mi(mat)
        f.smooth = smooth
        return f

    def box(self, mn, mx, mat, skip=()):
        """As-uitgelijnde doos. skip: zijden weglaten ('-x','+x','-y','+y','-z','+z')."""
        x0, y0, z0 = mn
        x1, y1, z1 = mx
        v = [self.bm.verts.new(p) for p in (
            (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
            (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
        faces = {
            '-z': (v[0], v[3], v[2], v[1]), '+z': (v[4], v[5], v[6], v[7]),
            '-y': (v[0], v[1], v[5], v[4]), '+y': (v[2], v[3], v[7], v[6]),
            '-x': (v[3], v[0], v[4], v[7]), '+x': (v[1], v[2], v[6], v[5]),
        }
        for k, f in faces.items():
            if k not in skip:
                self._face(f, mat)

    def cyl(self, base, radius, depth, mat, axis='Z', seg=8, caps=(True, True), r_top=None):
        """Cilinder (of kegel met r_top) vanaf 'base' langs de positieve as."""
        r_top = radius if r_top is None else r_top
        bx, by, bz = base

        def p(r, a, h):
            c, s = r * math.cos(a), r * math.sin(a)
            if axis == 'Z':
                return (bx + c, by + s, bz + h)
            if axis == 'Y':
                return (bx + c, by + h, bz + s)
            return (bx + h, by + c, bz + s)

        angs = [2 * math.pi * i / seg for i in range(seg)]
        bot = [self.bm.verts.new(p(radius, a, 0)) for a in angs]
        top = [self.bm.verts.new(p(r_top, a, depth)) for a in angs]
        for i in range(seg):
            j = (i + 1) % seg
            self._face((bot[i], bot[j], top[j], top[i]), mat, smooth=True)
        if caps[0]:
            self._face(list(reversed(bot)), mat)
        if caps[1]:
            self._face(top, mat)

    def grid_panel(self, xcuts, zcuts, y0, y1, holes, mat, skip_outer=()):
        """Plaat in het XZ-vlak van y0 tot y1, opgedeeld in cellen; 'holes' = {(i,j)} open cellen.
        Bouwt voor/achtervlakken + alle randen tussen gevulde en lege cellen (manifold)."""
        nx, nz = len(xcuts) - 1, len(zcuts) - 1
        filled = lambda i, j: 0 <= i < nx and 0 <= j < nz and (i, j) not in holes
        vf, vb = {}, {}

        def vert(cache, i, j, y):
            if (i, j) not in cache:
                cache[(i, j)] = self.bm.verts.new((xcuts[i], y, zcuts[j]))
            return cache[(i, j)]

        for i in range(nx):
            for j in range(nz):
                if not filled(i, j):
                    continue
                # voorkant (-Y) en achterkant (+Y)
                self._face((vert(vf, i, j, y0), vert(vf, i + 1, j, y0),
                            vert(vf, i + 1, j + 1, y0), vert(vf, i, j + 1, y0)), mat)
                self._face((vert(vb, i, j + 1, y1), vert(vb, i + 1, j + 1, y1),
                            vert(vb, i + 1, j, y1), vert(vb, i, j, y1)), mat)
                # randen
                edges = [
                    ((i - 1, j), '-x', (i, j), (i, j + 1)),
                    ((i + 1, j), '+x', (i + 1, j + 1), (i + 1, j)),
                    ((i, j - 1), '-z', (i + 1, j), (i, j)),
                    ((i, j + 1), '+z', (i, j + 1), (i + 1, j + 1)),
                ]
                for nb, side, a, b in edges:
                    if filled(*nb):
                        continue
                    outer = not (0 <= nb[0] < nx and 0 <= nb[1] < nz)
                    if outer and side in skip_outer:
                        continue
                    self._face((vert(vf, *a, y0), vert(vf, *b, y0),
                                vert(vb, *b, y1), vert(vb, *a, y1)), mat)

    def profile_x(self, pts_yz, x0, x1, mat):
        """Extrudeer een (gesloten, CCW in YZ) profiel van x0 naar x1."""
        a = [self.bm.verts.new((x0, y, z)) for y, z in pts_yz]
        b = [self.bm.verts.new((x1, y, z)) for y, z in pts_yz]
        n = len(pts_yz)
        self._face(list(reversed(a)), mat)
        self._face(b, mat)
        for i in range(n):
            j = (i + 1) % n
            self._face((a[i], a[j], b[j], b[i]), mat)

    def finish(self, name, coll, parent=None, location=(0, 0, 0)):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        ngons = [f for f in bm.faces if len(f.verts) > 4]
        if ngons:
            bmesh.ops.triangulate(bm, faces=ngons, quad_method='BEAUTY', ngon_method='BEAUTY')
        # "auto smooth": randen scherper dan 35 graden of tussen vlakke vlakken -> sharp
        for e in bm.edges:
            if len(e.link_faces) != 2:
                e.smooth = False
                continue
            f1, f2 = e.link_faces
            sharp = (not f1.smooth or not f2.smooth) or e.calc_face_angle(0) > math.radians(35)
            e.smooth = not sharp
        for f in bm.faces:
            f.smooth = True
        me = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
        # Materialslots vóór to_mesh: anders klemt Blender material_index op 0.
        me.materials.clear()
        for m in self.mats:
            me.materials.append(get_mat(m))
        bm.to_mesh(me)
        bm.free()
        obj = bpy.data.objects.new(name, me)
        coll.objects.link(obj)
        obj.location = location
        if parent:
            obj.parent = parent
        return obj


def ucx(name, idx, mn, mx, parent, coll):
    """Convexe collision-doos als child (geen materiaal)."""
    mb = MB()
    mb.box(mn, mx, "M_Concrete")
    mb.mats = []
    obj = mb.finish(f"UCX_{name}_{idx:02d}", coll, parent=parent)
    obj.data.materials.clear()
    obj.display_type = 'WIRE'
    obj.hide_render = True
    return obj


# ----------------------------------------------------------------------------
# Assets
# ----------------------------------------------------------------------------

def wall(length):
    def build(name, coll):
        mb = MB()
        mb.grid_panel([0, length], [0, WH], -WT / 2, WT / 2, set(), "M_Wall_Paint_White",
                      skip_outer=('-z',))
        return mb.finish(name, coll)
    return build


def wall_corner_in(name, coll):
    mb = MB()
    mb.box((-WT / 2, -WT / 2, 0), (WT / 2, WT / 2, WH), "M_Wall_Paint_White", skip=('-z',))
    return mb.finish(name, coll)


def wall_corner_out(name, coll):
    # Kolom met hoekbeschermer (metalen L-profiel op de buitenhoek, -X/-Y kant)
    mb = MB()
    h = WT / 2
    mb.box((-h, -h, 0), (h, h, WH), "M_Wall_Paint_White", skip=('-z',))
    g, t = 0.05, 0.004
    mb.box((-h - t, -h - t, 0.1), (-h + g, -h, 2.0), "M_Metal_Chrome", skip=('+y',))
    mb.box((-h - t, -h, 0.1), (-h, -h + g, 2.0), "M_Metal_Chrome", skip=('+x', '-y'))
    return mb.finish(name, coll)


def wall_window(name, coll):
    x0, x1, z0, z1 = 0.3, 1.7, 0.9, 2.1
    mb = MB()
    mb.grid_panel([0, x0, x1, 2.0], [0, z0, z1, WH], -WT / 2, WT / 2, {(1, 1)},
                  "M_Wall_Paint_White", skip_outer=('-z',))
    # kader (metaal) in de dagkant
    f, d = 0.05, 0.05
    # (vlakken die tegen de dagkant liggen weglaten -> geen z-fighting)
    mb.box((x0, -d, z0), (x0 + f, d, z1), "M_Metal_Painted", skip=('-x', '-z', '+z'))
    mb.box((x1 - f, -d, z0), (x1, d, z1), "M_Metal_Painted", skip=('+x', '-z', '+z'))
    mb.box((x0 + f, -d, z0), (x1 - f, d, z0 + f), "M_Metal_Painted", skip=('-z', '-x', '+x'))
    mb.box((x0 + f, -d, z1 - f), (x1 - f, d, z1), "M_Metal_Painted", skip=('+z', '-x', '+x'))
    # vensterbank aan de kamerzijde (-Y)
    mb.box((x0 - 0.05, -WT / 2 - 0.05, z0 - 0.03), (x1 + 0.05, -d, z0 + 0.01), "M_Concrete")
    obj = mb.finish(name, coll)
    ucx(name, 1, (0, -WT / 2, 0), (x0, WT / 2, WH), obj, coll)
    ucx(name, 2, (x1, -WT / 2, 0), (2.0, WT / 2, WH), obj, coll)
    ucx(name, 3, (x0, -WT / 2, 0), (x1, WT / 2, z0), obj, coll)
    ucx(name, 4, (x0, -WT / 2, z1), (x1, WT / 2, WH), obj, coll)
    return obj


def door_wall(length, x0, x1, zh=2.1):
    def build(name, coll):
        mb = MB()
        mb.grid_panel([0, x0, x1, length], [0, zh, WH], -WT / 2, WT / 2, {(1, 0)},
                      "M_Wall_Paint_White", skip_outer=('-z',))
        # kozijn
        f, d = 0.05, WT / 2
        # kozijn vlak met de muur; vlakken tegen de dagkant weglaten -> geen z-fighting
        mb.box((x0, -d, 0), (x0 + f, d, zh), "M_Metal_Painted", skip=('-z', '-x', '+z'))
        mb.box((x1 - f, -d, 0), (x1, d, zh), "M_Metal_Painted", skip=('-z', '+x', '+z'))
        mb.box((x0 + f, -d, zh - f), (x1 - f, d, zh), "M_Metal_Painted", skip=('+z', '-x', '+x'))
        obj = mb.finish(name, coll)
        ucx(name, 1, (0, -WT / 2, 0), (x0 + f, WT / 2, WH), obj, coll)
        ucx(name, 2, (x1 - f, -WT / 2, 0), (length, WT / 2, WH), obj, coll)
        ucx(name, 3, (x0 + f, -WT / 2, zh - f), (x1 - f, WT / 2, WH), obj, coll)
        return obj
    return build


DOOR_W, DOOR_H, DOOR_T = 0.89, 2.04, 0.04


def door_leaf(mb, w, h, mat, window=None, mirror=False):
    """Deurblad vanaf x=0 (scharnier) tot x=w (of -w als mirror), optioneel glasvak."""
    s = -1 if mirror else 1
    t = DOOR_T / 2
    if window:
        wx0, wx1, wz0, wz1 = window
        xc = sorted([0, s * wx0, s * wx1, s * w])
        hole_i = 1
        mb.grid_panel(xc, [0.005, wz0, wz1, h], -t, t, {(hole_i, 1)}, mat)
        gx = sorted([s * wx0, s * wx1])
        mb.box((gx[0], -0.004, wz0), (gx[1], 0.004, wz1), "M_Glass", skip=('-x', '+x', '-z', '+z'))
    else:
        xc = sorted([0, s * w])
        mb.box((xc[0], -t, 0.005), (xc[1], t, h), mat)


def door_classroom(name, coll):
    mb = MB()
    door_leaf(mb, DOOR_W, DOOR_H, "M_Wood_Light", window=(0.55, 0.75, 1.2, 1.8))
    return mb.finish(name, coll)


def door_emergency(name, coll):
    mb = MB()
    door_leaf(mb, DOOR_W, DOOR_H, "M_Metal_Painted")
    # paniekbalk aan de -Y zijde
    t = DOOR_T / 2
    for x in (0.12, 0.80):
        mb.box((x - 0.02, -t - 0.07, 0.97), (x + 0.02, -t, 1.05), "M_Metal_Chrome", skip=('+y',))
    mb.cyl((0.12, -t - 0.06, 1.01), 0.018, 0.68, "M_Metal_Chrome", axis='X', seg=8)
    return mb.finish(name, coll)


def door_double(name, coll):
    # Root = empty op het linkerscharnier; twee bladen als children met eigen scharnierpivot.
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = 'ARROWS'
    root.empty_display_size = 0.3
    coll.objects.link(root)
    w = 0.85
    win = (0.2, 0.65, 0.9, 1.9)
    mb = MB()
    door_leaf(mb, w, DOOR_H, "M_Wood_Light", window=win)
    mb.box((w - 0.08, -DOOR_T / 2 - 0.05, 1.0), (w - 0.05, -DOOR_T / 2, 1.3), "M_Metal_Chrome", skip=('+y',))
    mb.finish(name + "_L", coll, parent=root)
    mb = MB()
    door_leaf(mb, w, DOOR_H, "M_Wood_Light", window=win, mirror=True)
    mb.box((-w + 0.05, -DOOR_T / 2 - 0.05, 1.0), (-w + 0.08, -DOOR_T / 2, 1.3), "M_Metal_Chrome", skip=('+y',))
    mb.finish(name + "_R", coll, parent=root, location=(2 * w, 0, 0))
    return root


def door_handle(name, coll):
    # Pivot = midden van de rozet op het deuroppervlak; klink wijst naar -X (scharnierzijde).
    mb = MB()
    mb.cyl((0, 0, 0), 0.026, -0.012, "M_Metal_Chrome", axis='Y', seg=12)
    mb.cyl((0, -0.012, 0), 0.009, -0.045, "M_Metal_Chrome", axis='Y', seg=8)
    mb.cyl((0.012, -0.057, 0), 0.011, -0.13, "M_Metal_Chrome", axis='X', seg=8)
    return mb.finish(name, coll)


def floor_2x2(name, coll):
    mb = MB()
    mb.box((0, 0, -0.1), (2, 2, 0), "M_Floor_Linoleum", skip=('-z',))
    return mb.finish(name, coll)


def ceiling_2x2(name, coll):
    # Systeemplafond: plaat + T-profielen per 0.5 m. Pivot = hoek onderaan -> op 3 m plaatsen.
    mb = MB()
    mb.box((0, 0, 0), (2, 2, 0.02), "M_Wall_Paint_White", skip=('+z',))
    s, t = 0.012, 0.006
    for k in (0.5, 1.0, 1.5):
        mb.box((k - s, 0, -t), (k + s, 2, 0), "M_Metal_White", skip=('+z', '-y', '+y'))
        mb.box((0, k - s, -t), (2, k + s, 0), "M_Metal_White", skip=('+z', '-x', '+x'))
    return mb.finish(name, coll)


def stairs_straight(name, coll):
    # 18 optreden x 0.1667 = 3.0 m, aantrede 0.28 m, breedte 1.2 m. Loopt op langs +Y.
    n, rise, run, width = 18, 3.0 / 18, 0.28, 1.2
    pts = [(0.0, 0.0)]
    for k in range(n):
        pts.append((k * run, (k + 1) * rise))
        if k < n - 1:
            pts.append(((k + 1) * run, (k + 1) * rise))
    last_y = (n - 1) * run
    pts += [(last_y, 3.0 - 0.3), (1.2, 0.0)]
    # profile_x verwacht een gesloten polygoon; volgorde maakt niet uit (normals worden herberekend)
    mb = MB()
    mb.profile_x(pts, 0, width, "M_Concrete")
    obj = mb.finish(name, coll)
    return obj


def railing(name, coll):
    # 2 m recht segment, leuning op 1.0 m, tussenregel op 0.5 m.
    mb = MB()
    for x in (0.0, 1.0, 2.0):
        mb.cyl((x, 0, 0), 0.02, 1.0, "M_Metal_Chrome", seg=6, caps=(False, False))
    mb.cyl((-0.03, 0, 1.0), 0.025, 2.06, "M_Metal_Chrome", axis='X', seg=8)
    mb.cyl((0, 0, 0.5), 0.012, 2.0, "M_Metal_Chrome", axis='X', seg=6, caps=(False, False))
    return mb.finish(name, coll)


def window_glass(name, coll):
    # Past in de opening van SM_Build_Wall_Window (binnen het kader): plaatsen op (0.35, 0, 0.95).
    mb = MB()
    mb.box((0, -0.005, 0), (1.3, 0.005, 1.1), "M_Glass")
    return mb.finish(name, coll)


def light_tl(name, coll):
    mb = MB()
    mb.box((-0.62, -0.16, -0.07), (0.62, 0.16, 0), "M_Metal_White", skip=('+z',))
    mb.box((-0.6, -0.14, -0.075), (0.6, 0.14, -0.07), "M_Emissive_Light", skip=('+z',))
    return mb.finish(name, coll)


def radiator(name, coll):
    mb = MB()
    w, h0, h1, d0, d1 = 0.5, 0.15, 0.75, -0.11, -0.03
    mb.box((-w, d0, h0), (w, d1, h1), "M_Metal_White")
    n = 16
    for i in range(n):
        x = -w + 0.03 + i * (2 * w - 0.06) / (n - 1)
        mb.box((x - 0.012, d0 - 0.008, h0 + 0.03), (x + 0.012, d0, h1 - 0.03), "M_Metal_White", skip=('+y',))
    for x in (-w + 0.1, w - 0.1):
        mb.box((x - 0.02, d1, h1 - 0.1), (x + 0.02, 0, h1 - 0.06), "M_Metal_White")
    mb.cyl((w - 0.03, -0.07, h0), 0.012, -0.08, "M_Metal_Chrome", seg=8)
    mb.cyl((w - 0.03, -0.07, h1), 0.018, 0.06, "M_Plastic_White", seg=8)
    return mb.finish(name, coll)


def socket(name, coll):
    mb = MB()
    mb.box((-0.04, -0.012, -0.04), (0.04, 0, 0.04), "M_Plastic_White", skip=('+y',))
    mb.cyl((0, -0.012, 0), 0.024, -0.004, "M_Plastic_White", axis='Y', seg=12, caps=(False, True))
    for x in (-0.0095, 0.0095):
        mb.box((x - 0.002, -0.0165, -0.002), (x + 0.002, -0.016, 0.002), "M_Rubber_Black", skip=('+y',))
    return mb.finish(name, coll)


def light_switch(name, coll):
    mb = MB()
    mb.box((-0.04, -0.01, -0.04), (0.04, 0, 0.04), "M_Plastic_White", skip=('+y',))
    mb.box((-0.025, -0.018, -0.025), (0.025, -0.01, 0.025), "M_Plastic_White", skip=('+y',))
    return mb.finish(name, coll)


def fire_extinguisher(name, coll):
    mb = MB()
    r = 0.075
    mb.cyl((0, 0, 0), r, 0.44, "M_Paint_Red", seg=16)
    mb.cyl((0, 0, 0.44), r, 0.04, "M_Paint_Red", seg=16, r_top=0.03, caps=(False, True))
    mb.cyl((0, 0, 0.48), 0.018, 0.05, "M_Rubber_Black", seg=8)
    mb.box((-0.015, -0.015, 0.53), (0.09, 0.015, 0.56), "M_Rubber_Black")
    mb.box((-0.01, -0.012, 0.565), (0.08, 0.012, 0.58), "M_Metal_Chrome")
    mb.cyl((0.0, -r - 0.012, 0.08), 0.012, 0.42, "M_Rubber_Black", seg=6)
    mb.box((-0.06, -r - 0.001, 0.12), (0.06, -r + 0.01, 0.32), "M_Plastic_White", skip=('+y',))
    return mb.finish(name, coll)


def exit_sign(name, coll):
    mb = MB()
    mb.box((-0.18, -0.06, -0.08), (0.18, 0, 0.08), "M_Plastic_White", skip=('+y',))
    mb.box((-0.165, -0.062, -0.065), (0.165, -0.06, 0.065), "M_ExitSign", skip=('+y',))
    return mb.finish(name, coll)


def smoke_detector(name, coll):
    mb = MB()
    mb.cyl((0, 0, 0), 0.055, -0.03, "M_Plastic_White", seg=16, caps=(False, True))
    mb.cyl((0, 0, -0.03), 0.035, -0.012, "M_Plastic_White", seg=12, caps=(False, True))
    mb.box((0.04, -0.003, -0.032), (0.046, 0.003, -0.03), "M_ExitSign", skip=('+z',))
    return mb.finish(name, coll)


def speaker(name, coll):
    mb = MB()
    mb.box((-0.125, -0.12, -0.175), (0.125, 0, 0.175), "M_Plastic_White", skip=('+y',))
    mb.box((-0.105, -0.123, -0.155), (0.105, -0.12, 0.155), "M_Rubber_Black", skip=('+y',))
    return mb.finish(name, coll)


ASSETS = [
    ("SM_Build_Wall_2m", wall(2.0)),
    ("SM_Build_Wall_4m", wall(4.0)),
    ("SM_Build_Wall_Corner_In", wall_corner_in),
    ("SM_Build_Wall_Corner_Out", wall_corner_out),
    ("SM_Build_Wall_Window", wall_window),
    ("SM_Build_Wall_Door", door_wall(2.0, 0.5, 1.5)),
    ("SM_Build_Wall_Door_Double", door_wall(4.0, 1.1, 2.9)),
    ("SM_Build_Floor_2x2", floor_2x2),
    ("SM_Build_Ceiling_2x2", ceiling_2x2),
    ("SM_Build_Stairs_Straight", stairs_straight),
    ("SM_Build_Railing", railing),
    ("SM_Build_Door_Classroom", door_classroom),
    ("SM_Build_Door_Double", door_double),
    ("SM_Build_Door_Emergency", door_emergency),
    ("SM_Build_DoorHandle", door_handle),
    ("SM_Build_Window_Glass", window_glass),
    ("SM_Build_Light_TL", light_tl),
    ("SM_Build_Radiator", radiator),
    ("SM_Build_Socket", socket),
    ("SM_Build_LightSwitch", light_switch),
    ("SM_Build_FireExtinguisher", fire_extinguisher),
    ("SM_Build_ExitSign", exit_sign),
    ("SM_Build_SmokeDetector", smoke_detector),
    ("SM_Build_Speaker", speaker),
]

# Architectuur krijgt wereldschaal-UV's (textures lopen door over modules), props smart project.
WORLD_UV = {"SM_Build_Wall_2m", "SM_Build_Wall_4m", "SM_Build_Wall_Corner_In", "SM_Build_Wall_Corner_Out",
            "SM_Build_Wall_Window", "SM_Build_Wall_Door", "SM_Build_Wall_Door_Double",
            "SM_Build_Floor_2x2", "SM_Build_Ceiling_2x2", "SM_Build_Stairs_Straight"}


# ----------------------------------------------------------------------------
# UV's, export, rapport
# ----------------------------------------------------------------------------

def unwrap(obj, world):
    me = obj.data
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    me.uv_layers.new(name="UVMap")
    me.uv_layers.new(name="Lightmap")
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    for idx, layer in enumerate(("UVMap", "Lightmap")):
        me.uv_layers.active = me.uv_layers[layer]
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        if idx == 0 and world:
            bpy.ops.uv.cube_project(cube_size=1.0, scale_to_bounds=False, correct_aspect=True)
        else:
            bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.03 if idx else 0.01,
                                     scale_to_bounds=False, correct_aspect=True)
        bpy.ops.object.mode_set(mode='OBJECT')
    me.uv_layers.active = me.uv_layers["UVMap"]
    me.uv_layers["UVMap"].active_render = True


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons) if obj.type == 'MESH' else 0


def export_fbx(root, path):
    hierarchy = [root] + list(root.children_recursive)
    loc = root.location.copy()
    root.location = (0, 0, 0)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    for o in hierarchy:
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
        global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', use_space_transform=True, bake_space_transform=True,
        use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False,
        bake_anim=False, path_mode='AUTO', embed_textures=False)
    root.location = loc


def main(export=True):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')

    # Oude kit opruimen (alleen onze eigen collection)
    old = bpy.data.collections.get(KIT_COLLECTION)
    if old:
        for o in list(old.all_objects):
            bpy.data.objects.remove(o, do_unlink=True)
        for c in list(old.children_recursive):
            bpy.data.collections.remove(c)
        bpy.data.collections.remove(old)
    for me in [m for m in bpy.data.meshes if m.users == 0]:
        bpy.data.meshes.remove(me)

    kit = bpy.data.collections.new(KIT_COLLECTION)
    bpy.context.scene.collection.children.link(kit)

    report = {}
    cursor_x = 0.0
    for name, build in ASSETS:
        coll = bpy.data.collections.new(name)
        kit.children.link(coll)
        root = build(name, coll)
        meshes = [o for o in [root] + list(root.children_recursive)
                  if o.type == 'MESH' and not o.name.startswith("UCX_")]
        for o in meshes:
            unwrap(o, name in WORLD_UV)
        bpy.context.view_layer.update()
        # Bounding box (in lokale ruimte van root, incl. children)
        pts = []
        for o in meshes:
            m = root.matrix_world.inverted() @ o.matrix_world
            pts += [m @ Vector(c) for c in o.bound_box]
        mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        root.location = (cursor_x - mn.x, 0, 0)
        cursor_x += (mx.x - mn.x) + 0.5
        mats = sorted({s.material.name for o in meshes for s in o.material_slots if s.material})
        report[name] = {
            "tris": sum(tris(o) for o in meshes),
            "materials": mats,
            "size_m": [round(v, 3) for v in (mx - mn)],
            "ucx": len([o for o in root.children_recursive if o.name.startswith("UCX_")]),
        }

    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH, relative_remap=True)

    if export:
        os.makedirs(EXPORT_DIR, exist_ok=True)
        for name, _ in ASSETS:
            export_fbx(bpy.data.objects[name], os.path.join(EXPORT_DIR, name + ".fbx"))
        bpy.ops.wm.save_mainfile()

    with open(os.path.join(os.path.dirname(BLEND_PATH), "SM_Build_Kit_report.json"), "w") as f:
        json.dump(report, f, indent=2)
    return report


if __name__ == "__main__":
    main()
