"""
Bouwt de buiten-assets rond de schoolingang (Fase 5 van het VR School asset-plan + gevel/ingang):
gevelmodules, ingangsgevel met glazen deuren en luifel, schoolbord, bestrating, gras, bomen, hek, poort,
bank, fietsrek en lantaarnpaal.
Hergebruikt build_hallway_kit.py (en daarmee MB / build_kit / alle materialen van Fase 1-3).

Gebruik (Blender 5.x): Scripting-tab -> Run Script, of via Blender MCP exec(open(<pad>).read()).

Resultaat:
  - blender/models/environment/SM_Outdoor_Kit.blend (+ _report.json)
  - mixed_reality/Assets/Art/Outdoor/SM_Out_*.fbx

Conventies (Blender Z-up):
  - Gevels: zoals muren uit Fase 1 -> lengte langs +X vanaf de oorsprong, dikte 0,3 m gecentreerd op de gridlijn,
    BUITENKANT = -Y. Hoogte 6,4 m (2 verdiepingen van 3,2 m) + dakrand.
  - Ingangsgevel (8 m): portaal in het midden met glazen deuren als children "_Door_L" / "_Door_R"
    (scharnier aan de buitenkant van het portaal; openen naar buiten = naar -Y), inkomhal van 3 m diep erachter.
  - Bestrating/gras: oorsprong in de hoek (zoals SM_Build_Floor_2x2), bovenkant op z = 0,02 / 0.
  - Hek: lengte langs +X vanaf de oorsprong. Poort (4 m): bladen als children "_Door_L" / "_Door_R".
  - Losse objecten (bord, bank, fietsrek, boom, lantaarnpaal): oorsprong onderaan-midden, voorkant -Y.
"""

import math
import os

import bpy
from mathutils import Matrix

TOOLS = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() \
    else r"C:\0_school\3deJaar\Mixed_Reality\project\blender\tools"
HALLWAY_SCRIPT = os.path.join(TOOLS, "build_hallway_kit.py")
hall = {"__name__": "build_hallway_kit", "__file__": HALLWAY_SCRIPT}
exec(open(HALLWAY_SCRIPT, encoding="utf-8").read(), hall)
kit = hall["kit"]
MB = kit["MB"]
child = hall["child"]

REPO = kit["REPO"]
BLEND_PATH = os.path.join(REPO, "blender", "models", "environment", "SM_Outdoor_Kit.blend")
EXPORT_DIR = os.path.join(REPO, "mixed_reality", "Assets", "Art", "Outdoor")

# Extra materialen voor buiten (moeten ook in VRSchoolBuildCore.Materials staan)
kit["MATERIALS"].update({
    "M_Brick": ((0.36, 0.15, 0.09, 1), 0.0, 0.9, None, 0),
    "M_Glass_Dark": ((0.04, 0.06, 0.08, 1), 0.3, 0.08, None, 0),
    "M_Paving": ((0.42, 0.42, 0.40, 1), 0.0, 0.9, None, 0),
    "M_Grass": ((0.10, 0.22, 0.05, 1), 0.0, 1.0, None, 0),
    "M_Plant_Conifer": ((0.03, 0.15, 0.05, 1), 0.0, 0.8, None, 0),
    "M_Sign_Letters": ((1.0, 0.97, 0.90, 1), 0.0, 0.5, (1.0, 0.95, 0.85), 2.5),
})

FT, FH, FLOOR = 0.3, 6.4, 3.2      # geveldikte, gevelhoogte, verdiepingshoogte


# ----------------------------------------------------------------------------
# Hulpen
# ----------------------------------------------------------------------------

def facade_details(mb, length):
    """Plint, verdiepingsband en dakrand op de buitenkant (-Y) over de hele lengte."""
    y = -FT / 2
    mb.box((0, y - 0.03, 0), (length, y, 0.4), "M_Concrete", skip=('+y',))
    mb.box((0, y - 0.04, FLOOR - 0.1), (length, y, FLOOR + 0.1), "M_Concrete", skip=('+y',))
    mb.box((-0.02, y - 0.05, FH), (length + 0.02, -y + 0.02, FH + 0.12), "M_Concrete")


def window_fill(mb, x0, x1, z0, z1):
    """Donker glas iets terugliggend in de opening + kader en middenstijl."""
    f, y = 0.05, -0.02
    mb.box((x0, y - 0.005, z0), (x1, y, z1), "M_Glass_Dark", skip=('-x', '+x', '-z', '+z'))
    mb.box((x0, y - 0.06, z0), (x1, y - 0.005, z0 + f), "M_Metal_Painted", skip=('-x', '+x'))
    mb.box((x0, y - 0.06, z1 - f), (x1, y - 0.005, z1), "M_Metal_Painted", skip=('-x', '+x'))
    xm = (x0 + x1) / 2
    for xa, xb in ((x0, x0 + f), (xm - f / 2, xm + f / 2), (x1 - f, x1)):
        mb.box((xa, y - 0.06, z0 + f), (xb, y - 0.005, z1 - f), "M_Metal_Painted", skip=('-z', '+z'))
    # vensterbank
    mb.box((x0 - 0.03, -FT / 2 - 0.06, z0 - 0.05), (x1 + 0.03, y - 0.06, z0), "M_Concrete")


def text_into(mb, body, size, center, mat):
    """Voegt 3D-tekst (rechtop in het XZ-vlak, leesbaar vanaf -Y) toe aan de mesh-bouwer."""
    cu = bpy.data.curves.new("TmpText", 'FONT')
    cu.body = body
    cu.size = size
    cu.align_x, cu.align_y = 'CENTER', 'CENTER'
    cu.extrude = 0.004
    cu.resolution_u = 2
    ob = bpy.data.objects.new("TmpText", cu)
    bpy.context.scene.collection.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob)
    bpy.data.curves.remove(cu)
    # tekst ligt in XY met dikte langs Z -> 90° rond X: voorkant (+Z) wijst naar -Y
    m = Matrix.Translation(center) @ Matrix.Rotation(math.radians(90), 4, 'X')
    verts = [mb.bm.verts.new(m @ v.co) for v in me.vertices]
    for p in me.polygons:
        mb._face([verts[i] for i in p.vertices], mat)
    bpy.data.meshes.remove(me)


# ----------------------------------------------------------------------------
# Gevel
# ----------------------------------------------------------------------------

def facade_4m(name, coll):
    mb = MB()
    x0, x1 = 0.8, 3.2
    mb.grid_panel([0, x0, x1, 4.0], [0, 0.9, 2.4, FLOOR + 0.9, FLOOR + 2.4, FH], -FT / 2, FT / 2,
                  {(1, 1), (1, 3)}, "M_Brick", skip_outer=('-z', '+z'))
    for z0 in (0.9, FLOOR + 0.9):
        window_fill(mb, x0, x1, z0, z0 + 1.5)
    facade_details(mb, 4.0)
    return mb.finish(name, coll)


def facade_corner(name, coll):
    # Hoekkolom 0,3 x 0,3 m (vult de buitenhoek waar twee gevels samenkomen); oorsprong = gridkruising.
    mb = MB()
    h = FT / 2
    mb.box((-h, -h, 0), (h, h, FH), "M_Brick", skip=('-z',))
    mb.box((-h - 0.05, -h - 0.05, FH), (h + 0.05, h + 0.05, FH + 0.12), "M_Concrete")
    return mb.finish(name, coll)


PX0, PX1, PH = 2.5, 5.5, 3.0          # portaal in de ingangsgevel
DX0, DX1, DH = 3.1, 4.9, 2.4          # deuropening (2 bladen van 0,9 m)
HALL_D = 3.0                          # diepte inkomhal


def glass_leaf(mb, w, h, mirror=False):
    s = -1 if mirror else 1
    f = 0.06
    xs = sorted((0, s * w))
    mb.box((xs[0], -0.025, 0), (xs[1], 0.025, f), "M_Metal_Painted")
    mb.box((xs[0], -0.025, h - f), (xs[1], 0.025, h), "M_Metal_Painted")
    mb.box((xs[0], -0.025, f), (xs[0] + f, 0.025, h - f), "M_Metal_Painted", skip=('-z', '+z'))
    mb.box((xs[1] - f, -0.025, f), (xs[1], 0.025, h - f), "M_Metal_Painted", skip=('-z', '+z'))
    mb.box((xs[0] + f, -0.004, f), (xs[1] - f, 0.004, h - f), "M_Glass", skip=('-x', '+x', '-z', '+z'))
    # duwstang aan de vrije rand
    xg = s * (w - 0.12)
    mb.cyl((xg, -0.09, 0.8), 0.016, 0.8, "M_Metal_Chrome", seg=8)
    for z in (0.85, 1.55):
        mb.box((xg - 0.01, -0.09, z - 0.01), (xg + 0.01, -0.025, z + 0.01), "M_Metal_Chrome", skip=('+y',))


def facade_entrance(name, coll):
    mb = MB()
    L = 8.0
    xc = [0, 0.6, 2.0, PX0, PX1, 6.0, 7.4, L]
    zc = [0, 0.9, 2.4, PH, FLOOR + 0.2, FLOOR + 0.9, FLOOR + 2.4, FH - 0.4, FH]
    holes = {(1, 1), (5, 1), (1, 5), (5, 5),                # ramen
             (3, 0), (3, 1), (3, 2),                        # portaal
             (3, 4), (3, 5), (3, 6)}                        # glasgevel boven de luifel
    mb.grid_panel(xc, zc, -FT / 2, FT / 2, holes, "M_Brick", skip_outer=('-z', '+z'))
    for x0, x1 in ((0.6, 2.0), (6.0, 7.4)):
        for z0 in (0.9, FLOOR + 0.9):
            window_fill(mb, x0, x1, z0, z0 + 1.5)
    # glasgevel boven de luifel: donker glas + 4 stijlen
    z0, z1 = FLOOR + 0.2, FH - 0.4
    mb.box((PX0, -0.02, z0), (PX1, -0.015, z1), "M_Glass_Dark", skip=('-x', '+x', '-z', '+z'))
    for k in range(5):
        x = PX0 + k * (PX1 - PX0) / 4
        mb.box((max(PX0, x - 0.03), -0.09, z0), (min(PX1, x + 0.03), -0.02, z1), "M_Metal_Painted", skip=('-z', '+z'))
    # portaal: zijlichten + bovenlicht (vast glas) rond de deuren, kader
    f = 0.06
    for xa, xb in ((PX0, DX0), (DX1, PX1)):
        mb.box((xa, -0.004, 0), (xb, 0.004, DH), "M_Glass", skip=('-z', '+z'))
    for x in (PX0 + f / 2, DX0, DX1, PX1 - f / 2):
        mb.box((x - f / 2, -0.03, 0), (x + f / 2, 0.03, PH), "M_Metal_Painted", skip=('-z',))
    mb.box((PX0, -0.004, DH + f), (PX1, 0.004, PH), "M_Glass", skip=('-x', '+x', '+z'))
    mb.box((PX0, -0.03, DH), (PX1, 0.03, DH + f), "M_Metal_Painted", skip=('-x', '+x'))
    # luifel met 2 kolommen en 3 downlights
    cy = -2.5
    mb.box((PX0 - 1.0, cy, PH), (PX1 + 1.0, -FT / 2, PH + 0.25), "M_Concrete", skip=('+y',))
    for x in (PX0 - 0.8, PX1 + 0.8):
        mb.cyl((x, cy + 0.2, 0), 0.07, PH, "M_Metal_Painted", seg=10, caps=(False, False))
    for x in (3.3, 4.0, 4.7):
        mb.cyl((x, -1.2, PH), 0.1, -0.01, "M_Emissive_Light", seg=12, caps=(False, True))
    # inkomhal achter het portaal (dunne dozen, binnenzijde zichtbaar door het glas)
    t, yb = 0.05, FT / 2
    mb.box((PX0, yb, -0.05), (PX1, yb + HALL_D, 0), "M_Floor_Linoleum", skip=('-z',))
    mb.box((PX0, yb, PH), (PX1, yb + HALL_D, PH + t), "M_Wall_Paint_White")
    mb.box((PX0 - t, yb, 0), (PX0, yb + HALL_D, PH), "M_Wall_Paint_White", skip=('-z', '+z'))
    mb.box((PX1, yb, 0), (PX1 + t, yb + HALL_D, PH), "M_Wall_Paint_White", skip=('-z', '+z'))
    mb.box((PX0, yb + HALL_D, 0), (PX1, yb + HALL_D + t, PH), "M_Wall_Paint_White", skip=('-z', '+z', '-x', '+x'))
    mb.box((PX0 + 0.6, yb + 0.6, PH - 0.01), (PX1 - 0.6, yb + HALL_D - 0.6, PH), "M_Emissive_Light", skip=('+z',))
    facade_details(mb, L)
    root = mb.finish(name, coll)

    # deurbladen tussen de stijlen op DX0 en DX1
    w = (DX1 - DX0 - f) / 2 - 0.005
    mb = MB()
    glass_leaf(mb, w, DH - 0.005)
    child(mb, name + "_Door_L", coll, root, location=(DX0 + f / 2 + 0.002, 0, 0.003))
    mb = MB()
    glass_leaf(mb, w, DH - 0.005, mirror=True)
    child(mb, name + "_Door_R", coll, root, location=(DX1 - f / 2 - 0.002, 0, 0.003))
    return root


# ----------------------------------------------------------------------------
# Grond
# ----------------------------------------------------------------------------

def paving_4x4(name, coll):
    mb = MB()
    mb.box((0, 0, -0.1), (4, 4, 0.02), "M_Paving", skip=('-z',))
    return mb.finish(name, coll)


def grass_20x20(name, coll):
    mb = MB()
    mb.box((0, 0, -0.1), (20, 20, 0), "M_Grass", skip=('-z',))
    return mb.finish(name, coll)


# ----------------------------------------------------------------------------
# Buitenmeubilair en groen
# ----------------------------------------------------------------------------

def school_sign(name, coll):
    # Naambord 1,6 m breed op betonnen sokkel, verlichte letters op de voorkant (-Y).
    mb = MB()
    mb.box((-0.8, -0.2, 0), (0.8, 0.2, 0.5), "M_Concrete")
    mb.box((-0.8, -0.04, 0.5), (0.8, 0.04, 1.4), "M_Metal_Painted")
    text_into(mb, "VR SCHOOL", 0.24, (0, -0.045, 1.05), "M_Sign_Letters")
    mb.box((-0.6, -0.042, 0.72), (0.6, -0.04, 0.735), "M_Sign_Letters", skip=('+y',))
    return mb.finish(name, coll)


def outdoor_bench(name, coll):
    # Parkbank 1,8 m: twee betonblokken met houten latten, zit op 45 cm.
    mb = MB()
    for x in (-0.7, 0.7):
        mb.box((x - 0.1, -0.22, 0), (x + 0.1, 0.22, 0.4), "M_Concrete", skip=('-z',))
    for i in range(5):
        y = -0.22 + i * 0.092
        mb.box((-0.9, y, 0.4), (0.9, y + 0.075, 0.45), "M_Wood_Light")
    return mb.finish(name, coll)


def bike_rack(name, coll):
    # 5 fietsbeugels om de 0,7 m (beugels in het YZ-vlak, fietsen staan langs Y).
    mb = MB()
    for i in range(5):
        x = -1.4 + i * 0.7
        for y in (-0.3, 0.3):
            mb.cyl((x, y, 0), 0.025, 0.75, "M_Metal_Chrome", seg=6, caps=(False, False))
        mb.cyl((x, -0.325, 0.75), 0.025, 0.65, "M_Metal_Chrome", axis='Y', seg=6)
    return mb.finish(name, coll)


def tree_a(name, coll):
    # Loofboom ~5,5 m: stam + drie kruinbollen.
    mb = MB()
    mb.cyl((0, 0, 0), 0.16, 2.6, "M_Wood_Dark", seg=7, caps=(False, False), r_top=0.09)
    blob = [(0, 0), (1.1, 0.4), (1.6, 1.3), (1.4, 2.3), (0.8, 2.9), (0, 3.1)]
    for (bx, by, bz, sc) in ((0, 0, 2.2, 1.0), (0.9, -0.4, 1.9, 0.6), (-0.8, 0.5, 2.1, 0.65)):
        mb.lathe([(r * sc, h * sc) for r, h in blob], "M_Plant_Leaf", base=(bx, by, bz), seg=8)
    return mb.finish(name, coll)


def tree_b(name, coll):
    # Naaldboom ~6 m: stam + drie gestapelde kegels.
    mb = MB()
    mb.cyl((0, 0, 0), 0.12, 1.2, "M_Wood_Dark", seg=6, caps=(False, False))
    for z, r, h in ((0.8, 1.6, 2.4), (2.2, 1.25, 2.2), (3.6, 0.85, 2.3)):
        mb.cyl((0, 0, z), r, h, "M_Plant_Conifer", seg=8, r_top=0.0, caps=(True, False))
    return mb.finish(name, coll)


FENCE_H = 1.5


def fence_bars(mb, x0, x1, mat="M_Metal_Painted"):
    mb.box((x0, -0.02, 0.1), (x1, 0.02, 0.14), mat)
    mb.box((x0, -0.02, FENCE_H - 0.14), (x1, 0.02, FENCE_H - 0.1), mat)
    n = int(round((x1 - x0) / 0.15))
    for k in range(1, n):
        x = x0 + k * (x1 - x0) / n
        mb.box((x - 0.008, -0.008, 0.02), (x + 0.008, 0.008, FENCE_H), mat)


def fence_segment(name, coll):
    # Spijlenhek 2 m x 1,5 m; paal op x = 0 (het volgende segment levert de paal op x = 2).
    mb = MB()
    mb.box((-0.04, -0.04, 0), (0.04, 0.04, FENCE_H + 0.05), "M_Metal_Painted", skip=('-z',))
    fence_bars(mb, 0.04, 2.0)
    return mb.finish(name, coll)


def gate(name, coll):
    # Dubbele poort 4 m (tussen paal op x = 0 en x = 4); bladen als children met scharnierpivot.
    mb = MB()
    for x in (0.0, 4.0):
        mb.box((x - 0.06, -0.06, 0), (x + 0.06, 0.06, FENCE_H + 0.1), "M_Metal_Painted", skip=('-z',))
    root = mb.finish(name, coll)
    w = 1.94 - 0.01
    mb = MB()
    fence_bars(mb, 0, w)
    mb.box((0, -0.02, 0.02), (0.04, 0.02, FENCE_H), "M_Metal_Painted")
    mb.box((w - 0.04, -0.02, 0.02), (w, 0.02, FENCE_H), "M_Metal_Painted")
    child(mb, name + "_Door_L", coll, root, location=(0.06, 0, 0))
    mb = MB()
    fence_bars(mb, -w, 0)
    mb.box((-0.04, -0.02, 0.02), (0, 0.02, FENCE_H), "M_Metal_Painted")
    mb.box((-w, -0.02, 0.02), (-w + 0.04, 0.02, FENCE_H), "M_Metal_Painted")
    child(mb, name + "_Door_R", coll, root, location=(3.94, 0, 0))
    return root


def lamppost(name, coll):
    # Lantaarnpaal 4 m met lamp bovenaan (lichtvlak naar beneden).
    mb = MB()
    mb.cyl((0, 0, 0), 0.09, 0.4, "M_Metal_Painted", seg=8)
    mb.cyl((0, 0, 0.4), 0.06, 3.5, "M_Metal_Painted", seg=8, caps=(False, False), r_top=0.045)
    mb.cyl((0, 0, 3.9), 0.22, 0.15, "M_Metal_Painted", seg=10, r_top=0.12)
    mb.cyl((0, 0, 3.9), 0.2, -0.01, "M_Emissive_Light", seg=10, caps=(False, True))
    return mb.finish(name, coll)


OUTDOOR = [
    ("SM_Out_Facade_4m", facade_4m),
    ("SM_Out_Facade_Corner", facade_corner),
    ("SM_Out_Facade_Entrance_8m", facade_entrance),
    ("SM_Out_Ground_Paving_4x4", paving_4x4),
    ("SM_Out_Ground_Grass_20x20", grass_20x20),
    ("SM_Out_SchoolSign", school_sign),
    ("SM_Out_Bench", outdoor_bench),
    ("SM_Out_BikeRack", bike_rack),
    ("SM_Out_Tree_A", tree_a),
    ("SM_Out_Tree_B", tree_b),
    ("SM_Out_Fence_Segment", fence_segment),
    ("SM_Out_Gate", gate),
    ("SM_Out_Lamppost", lamppost),
]

# Wereldschaal-UV's: textures lopen door over gevelmodules en tegels.
WORLD_UV = {"SM_Out_Facade_4m", "SM_Out_Facade_Corner", "SM_Out_Facade_Entrance_8m",
            "SM_Out_Ground_Paving_4x4", "SM_Out_Ground_Grass_20x20"}


def main(export=True):
    return kit["build_kit"](OUTDOOR, "Outdoor_Kit", BLEND_PATH, lambda name: EXPORT_DIR, WORLD_UV, export)


if __name__ == "__main__":
    main()
