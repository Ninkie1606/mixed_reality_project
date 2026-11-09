"""
Bouwt de klaslokaal-assets (Fase 2 van het VR School asset-plan): meubels, borden, IT en grab-props.
Hergebruikt MB / build_kit uit build_modular_kit.py.

Gebruik (Blender 5.x): Scripting-tab -> Run Script, of via Blender MCP exec(open(<pad>).read()).

Resultaat:
  - blender/models/props/SM_Classroom_Kit.blend (+ _report.json)
  - mixed_reality/Assets/Art/Classroom/SM_Class_*.fbx
  - mixed_reality/Assets/Art/Props/SM_Prop_*.fbx

Conventies (zelfde als Fase 1, Blender Z-up):
  - "Voorkant" = -Y: kant waar je zit/staat (bank, stoel, bureau) of het beschreven/beeldvlak (borden, schermen).
  - Vrijstaande meubels en grab-props: oorsprong onderaan-midden.
  - Wandobjecten (borden, prikbord, klok, poster, kaart, kapstok): oorsprong achteraan-midden;
    borden/scherm: achteraan-midden aan de ONDERRAND (montagehoogte = y in Unity).
  - Plafond (projector) / bovenrand (projectiescherm): oorsprong bovenaan.
  - Bewegende delen zijn aparte children met eigen pivot: laptopdeksel (scharnier), klokwijzers (centrum),
    projectiedoek (bovenrand, schalen in Z = oprollen).
"""

import math
import os

import bpy
from mathutils import Matrix, Vector

KIT_SCRIPT = os.path.join(os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals()
                          else r"C:\0_school\3deJaar\Mixed_Reality\project\blender\tools", "build_modular_kit.py")
kit = {"__name__": "build_modular_kit"}
exec(open(KIT_SCRIPT, encoding="utf-8").read(), kit)
MB = kit["MB"]

REPO = kit["REPO"]
BLEND_PATH = os.path.join(REPO, "blender", "models", "props", "SM_Classroom_Kit.blend")
ART = os.path.join(REPO, "mixed_reality", "Assets", "Art")

# Extra materialen voor Fase 2 (moeten ook in VRSchoolKitBuilder.Materials staan)
kit["MATERIALS"].update({
    "M_Wood_Dark": ((0.22, 0.13, 0.07, 1), 0.0, 0.6, None, 0),
    "M_Chalkboard": ((0.03, 0.07, 0.05, 1), 0.0, 0.95, None, 0),
    "M_Whiteboard": ((0.90, 0.90, 0.90, 1), 0.0, 0.15, None, 0),
    "M_Plastic_Chair": ((0.06, 0.20, 0.50, 1), 0.0, 0.5, None, 0),
    "M_Plastic_Black": ((0.02, 0.02, 0.02, 1), 0.0, 0.4, None, 0),
    "M_Plastic_Yellow": ((0.80, 0.55, 0.02, 1), 0.0, 0.5, None, 0),
    "M_Plastic_Green": ((0.05, 0.35, 0.10, 1), 0.0, 0.5, None, 0),
    "M_Plastic_Pink": ((0.90, 0.40, 0.45, 1), 0.0, 0.7, None, 0),
    "M_Screen": ((0.01, 0.012, 0.015, 1), 0.0, 0.1, None, 0),
    "M_Paper": ((0.85, 0.85, 0.82, 1), 0.0, 0.9, None, 0),
    "M_Cork": ((0.45, 0.30, 0.16, 1), 0.0, 0.95, None, 0),
    "M_Fabric_Blue": ((0.05, 0.12, 0.35, 1), 0.0, 0.95, None, 0),
    "M_Fabric_Red": ((0.45, 0.04, 0.04, 1), 0.0, 0.95, None, 0),
    "M_Fabric_Dark": ((0.03, 0.03, 0.035, 1), 0.0, 0.95, None, 0),
    "M_Poster": ((0.60, 0.60, 0.60, 1), 0.0, 0.8, None, 0),
    "M_WorldMap": ((0.15, 0.35, 0.60, 1), 0.0, 0.8, None, 0),
})


def tilt_x(start, mb, angle_deg, pivot):
    """Kantel alles sinds 'start' rond een X-as door 'pivot'."""
    p = Vector(pivot)
    mb.transform_since(start, Matrix.Translation(p) @ Matrix.Rotation(math.radians(angle_deg), 4, 'X')
                       @ Matrix.Translation(-p))


def rot_z(start, mb, angle_deg):
    mb.transform_since(start, Matrix.Rotation(math.radians(angle_deg), 4, 'Z'))


def child(mb, name, coll, parent, location=(0, 0, 0), **props):
    obj = mb.finish(name, coll, parent=parent, location=location)
    for k, v in props.items():
        obj[k] = v
    return obj


# ----------------------------------------------------------------------------
# Meubels
# ----------------------------------------------------------------------------

def desk_a(name, coll):
    # Eenpersoonsbank 70 x 50 cm, blad op 76 cm; leerling zit aan -Y, boekenrek opent naar -Y.
    W, D, H, t, leg = 0.7, 0.5, 0.76, 0.025, 0.03
    mb = MB()
    mb.box((-W / 2, -D / 2, H - t), (W / 2, D / 2, H), "M_Wood_Light")
    for sx in (-1, 1):
        x = sx * (W / 2 - 0.04)
        for sy in (-1, 1):
            y = sy * (D / 2 - 0.04)
            mb.box((x - leg / 2, y - leg / 2, 0), (x + leg / 2, y + leg / 2, H - t), "M_Metal_Painted", skip=('+z',))
        mb.box((x - 0.01, -D / 2 + 0.055, 0.12), (x + 0.01, D / 2 - 0.055, 0.15), "M_Metal_Painted", skip=('-y', '+y'))
    # boekenrek onder het blad
    mb.box((-W / 2 + 0.055, -D / 2 + 0.10, H - 0.17), (W / 2 - 0.055, D / 2 - 0.055, H - 0.16), "M_Metal_Painted")
    mb.box((-W / 2 + 0.055, D / 2 - 0.07, H - 0.16), (W / 2 - 0.055, D / 2 - 0.055, H - t), "M_Metal_Painted", skip=('+z',))
    return mb.finish(name, coll)


def chair_a(name, coll):
    # Schoolstoel, zit op 45 cm; kijkrichting -Y, rugleuning aan +Y.
    mb = MB()
    s, leg = 0.2, 0.022
    mb.box((-s, -s, 0.43), (s, s, 0.45), "M_Plastic_Chair")
    for x in (-0.17, 0.17):
        for y in (-0.17, 0.17):
            mb.box((x - leg / 2, y - leg / 2, 0), (x + leg / 2, y + leg / 2, 0.43), "M_Metal_Painted", skip=('+z',))
    start = mb.mark()
    for x in (-0.17, 0.17):
        mb.box((x - 0.011, 0.17, 0.45), (x + 0.011, 0.19, 0.78), "M_Metal_Painted", skip=('-z',))
    mb.box((-0.19, 0.19, 0.55), (0.19, 0.21, 0.80), "M_Plastic_Chair")
    tilt_x(start, mb, -8, (0, 0.18, 0.45))
    return mb.finish(name, coll)


def teacher_desk(name, coll):
    # 140 x 70 cm, blad op 75 cm; leerkracht zit aan -Y (lades), modesty-paneel aan +Y (klaszijde).
    mb = MB()
    mb.box((-0.7, -0.35, 0.72), (0.7, 0.35, 0.75), "M_Wood_Light")
    mb.box((-0.69, -0.33, 0), (-0.66, 0.33, 0.72), "M_Wood_Light", skip=('+z',))
    mb.box((0.25, -0.33, 0), (0.68, 0.33, 0.72), "M_Wood_Light", skip=('+z',))
    mb.box((-0.66, 0.30, 0.25), (0.25, 0.32, 0.72), "M_Wood_Light", skip=('+z', '-x', '+x'))
    for z0, z1 in ((0.04, 0.24), (0.26, 0.47), (0.49, 0.70)):
        mb.box((0.27, -0.336, z0), (0.66, -0.33, z1), "M_Wood_Light", skip=('+y',))
        mb.box((0.40, -0.352, (z0 + z1) / 2 + 0.05), (0.53, -0.336, (z0 + z1) / 2 + 0.065), "M_Metal_Chrome", skip=('+y',))
    return mb.finish(name, coll)


def office_chair(name, coll):
    mb = MB()
    for i in range(5):
        start = mb.mark()
        mb.box((0, -0.02, 0.05), (0.30, 0.02, 0.08), "M_Plastic_Black")
        mb.cyl((0.28, 0, 0), 0.025, 0.05, "M_Plastic_Black", axis='Y', seg=6)
        mb.transform_since(len(mb.bm.verts) - 12, Matrix.Translation((0, -0.025, 0.025)))
        rot_z(start, mb, i * 72 + 90)
    mb.cyl((0, 0, 0.06), 0.045, 0.06, "M_Plastic_Black", seg=10)
    mb.cyl((0, 0, 0.12), 0.025, 0.30, "M_Metal_Chrome", seg=10, caps=(False, False))
    mb.box((-0.24, -0.24, 0.42), (0.24, 0.24, 0.50), "M_Fabric_Dark")
    mb.box((-0.03, 0.20, 0.44), (0.03, 0.24, 0.62), "M_Plastic_Black")
    start = mb.mark()
    mb.box((-0.22, 0.22, 0.58), (0.22, 0.29, 1.06), "M_Fabric_Dark")
    tilt_x(start, mb, -6, (0, 0.24, 0.5))
    for x in (-0.25, 0.25):
        mb.box((x - 0.015, -0.02, 0.50), (x + 0.015, 0.06, 0.66), "M_Plastic_Black", skip=('-z',))
        mb.box((x - 0.03, -0.14, 0.66), (x + 0.03, 0.10, 0.685), "M_Plastic_Black")
    return mb.finish(name, coll)


def board(width, height, surface, frame_mat, tray_mat, depth=0.02, frame=0.04):
    """Wandbord; oorsprong achteraan-midden aan de onderrand. Schrijfvlak naar -Y."""
    def build(name, coll):
        mb = MB()
        w = width / 2
        mb.box((-w, -depth, 0), (w, 0, height), surface, skip=('+y',))
        f, d = frame, -depth - 0.015
        mb.box((-w - f, d, -f), (w + f, -depth, 0), frame_mat, skip=('+y',))
        mb.box((-w - f, d, height), (w + f, -depth, height + f), frame_mat, skip=('+y',))
        mb.box((-w - f, d, 0), (-w, -depth, height), frame_mat, skip=('+y', '-z', '+z'))
        mb.box((w, d, 0), (w + f, -depth, height), frame_mat, skip=('+y', '-z', '+z'))
        # krijt-/stiftengoot
        mb.box((-w * 0.9, d - 0.06, -f - 0.015), (w * 0.9, d, -f), tray_mat, skip=('+y',))
        mb.box((-w * 0.9, d - 0.065, -f - 0.015), (w * 0.9, d - 0.06, -f + 0.015), tray_mat)
        return mb.finish(name, coll)
    return build


def smartboard(name, coll):
    mb = MB()
    w, h = 0.975, 1.2
    mb.box((-w, -0.07, 0), (w, 0, h), "M_Plastic_Black", skip=('+y',))
    mb.box((-w + 0.05, -0.072, 0.05), (w - 0.05, -0.07, h - 0.05), "M_Screen", skip=('+y',))
    mb.box((-0.3, -0.14, -0.03), (0.3, -0.07, 0), "M_Plastic_Black", skip=('+y',))
    return mb.finish(name, coll)


def projector(name, coll):
    # Plafondmontage; oorsprong bovenaan-midden. Lens naar -Y.
    mb = MB()
    mb.box((-0.08, -0.08, -0.01), (0.08, 0.08, 0), "M_Metal_White", skip=('+z',))
    mb.cyl((0, 0, -0.32), 0.02, 0.31, "M_Metal_White", seg=8, caps=(False, False))
    mb.box((-0.15, -0.12, -0.42), (0.15, 0.12, -0.32), "M_Plastic_White")
    mb.cyl((0.07, -0.12, -0.37), 0.035, -0.03, "M_Plastic_Black", axis='Y', seg=12, caps=(False, True))
    return mb.finish(name, coll)


def projector_screen(name, coll):
    # Rolscherm; oorsprong achteraan-midden aan de bovenkant van de cassette.
    mb = MB()
    mb.cyl((-1.0, -0.05, -0.045), 0.045, 2.0, "M_Metal_White", axis='X', seg=10)
    root = mb.finish(name, coll)
    # Doek als child: pivot aan de bovenrand -> in Unity lokaal Y schalen = oprollen
    mb = MB()
    mb.box((-0.9, -0.002, -1.4), (0.9, 0.002, 0), "M_Whiteboard", skip=('+z',))
    mb.cyl((-0.92, 0, -1.42), 0.012, 1.84, "M_Plastic_Black", axis='X', seg=6)
    child(mb, name + "_Cloth", coll, root, location=(0, -0.05, -0.09))
    return root


def cabinet(name, coll):
    mb = MB()
    W, D, H = 1.0, 0.45, 1.0
    mb.box((-W / 2, -D / 2 + 0.02, 0.05), (W / 2, D / 2, H - 0.02), "M_Wood_Light")
    mb.box((-W / 2 + 0.02, -D / 2 + 0.03, 0), (W / 2 - 0.02, D / 2 - 0.02, 0.05), "M_Plastic_Black", skip=('-z', '+z'))
    mb.box((-W / 2 - 0.01, -D / 2 - 0.005, H - 0.02), (W / 2 + 0.01, D / 2 + 0.005, H), "M_Wood_Light")
    for x0, x1 in ((-W / 2 + 0.005, -0.0025), (0.0025, W / 2 - 0.005)):
        mb.box((x0, -D / 2, 0.06), (x1, -D / 2 + 0.02, H - 0.03), "M_Wood_Light", skip=('+y',))
    for x in (-0.04, 0.04):
        mb.box((x - 0.006, -D / 2 - 0.025, H / 2 - 0.08), (x + 0.006, -D / 2, H / 2 + 0.08), "M_Metal_Chrome", skip=('+y',))
    return mb.finish(name, coll)


def bookshelf(name, coll, W=0.9, D=0.32, H=1.8, shelves=4, mat="M_Wood_Light"):
    mb = MB()
    t = 0.02
    mb.box((-W / 2, -D / 2, 0), (-W / 2 + t, D / 2, H), mat)
    mb.box((W / 2 - t, -D / 2, 0), (W / 2, D / 2, H), mat)
    mb.box((-W / 2 + t, -D / 2, H - t), (W / 2 - t, D / 2, H), mat, skip=('-x', '+x'))
    mb.box((-W / 2 + t, -D / 2, 0), (W / 2 - t, D / 2, 0.06), mat, skip=('-x', '+x', '-z'))
    mb.box((-W / 2 + t, D / 2 - 0.006, 0.06), (W / 2 - t, D / 2, H - t), mat, skip=('-x', '+x', '-z', '+z'))
    step = (H - t - 0.06) / (shelves + 1)
    for k in range(1, shelves + 1):
        z = 0.06 + k * step
        mb.box((-W / 2 + t, -D / 2 + 0.01, z - t / 2), (W / 2 - t, D / 2 - 0.006, z + t / 2), mat, skip=('-x', '+x', '+y'))
    return mb.finish(name, coll)


def coat_rack(name, coll):
    # Kapstok aan de muur; oorsprong achteraan-midden.
    mb = MB()
    mb.box((-0.6, -0.02, -0.05), (0.6, 0, 0.05), "M_Wood_Light", skip=('+y',))
    for i in range(6):
        x = -0.5 + i * 0.2
        mb.cyl((x, -0.02, 0.0), 0.007, -0.07, "M_Metal_Chrome", axis='Y', seg=6, caps=(False, True))
        mb.box((x - 0.006, -0.095, 0.0), (x + 0.006, -0.083, 0.035), "M_Metal_Chrome")
    return mb.finish(name, coll)


def trash_bin(name, coll):
    mb = MB()
    mb.lathe([(0, 0), (0.14, 0), (0.16, 0.36), (0.153, 0.36), (0.133, 0.012), (0, 0.012)], "M_Plastic_Black", seg=16)
    return mb.finish(name, coll)


def wall_clock(name, coll):
    # Oorsprong = achterkant-midden van de klok. Wijzers zijn children, pivot in het centrum, 12 uur = +Z.
    mb = MB()
    mb.lathe([(0, 0), (0.15, 0), (0.155, -0.035), (0.14, -0.042), (0.136, -0.03)], "M_Plastic_Black", axis='Y', seg=24)
    mb.cyl((0, -0.03, 0), 0.137, -0.001, "M_Plastic_White", axis='Y', seg=24, caps=(False, True))
    for i in range(12):
        start = mb.mark()
        long = 0.025 if i % 3 == 0 else 0.012
        mb.box((-0.003, -0.032, 0.125 - long), (0.003, -0.031, 0.125), "M_Plastic_Black", skip=('+y',))
        mb.transform_since(start, Matrix.Rotation(math.radians(i * 30), 4, 'Y'))
    root = mb.finish(name, coll)
    for hand, length, width, y, mat in (("HandHour", 0.07, 0.008, -0.034, "M_Plastic_Black"),
                                        ("HandMinute", 0.11, 0.005, -0.036, "M_Plastic_Black"),
                                        ("HandSecond", 0.12, 0.002, -0.038, "M_Paint_Red")):
        mb = MB()
        mb.box((-width / 2, -0.001, -0.02), (width / 2, 0.001, length), mat)
        child(mb, f"{name}_{hand}", coll, root, location=(0, y, 0))
    return root


def laptop(name, coll):
    # Gesloten modelstand; deksel is child met scharnierpivot achteraan. Openen = draaien rond lokale X.
    mb = MB()
    mb.box((-0.16, -0.11, 0), (0.16, 0.11, 0.018), "M_Metal_White")
    mb.box((-0.14, -0.015, 0.018), (0.14, 0.095, 0.0185), "M_Plastic_Black", skip=('-z',))
    mb.box((-0.045, -0.095, 0.018), (0.045, -0.03, 0.0185), "M_Plastic_Black", skip=('-z',))
    root = mb.finish(name, coll)
    mb = MB()
    mb.box((-0.16, -0.22, 0.0005), (0.16, 0, 0.008), "M_Metal_White")
    mb.box((-0.15, -0.21, 0), (0.15, -0.012, 0.0005), "M_Screen", skip=('+z',))
    child(mb, name + "_Lid", coll, root, location=(0, 0.11, 0.018))
    return root


def pc_monitor(name, coll):
    mb = MB()
    mb.box((-0.11, -0.09, 0), (0.11, 0.09, 0.012), "M_Plastic_Black")
    mb.box((-0.025, 0.02, 0.012), (0.025, 0.05, 0.22), "M_Plastic_Black", skip=('-z',))
    mb.box((-0.27, -0.01, 0.12), (0.27, 0.02, 0.44), "M_Plastic_Black")
    mb.box((-0.26, -0.0105, 0.13), (0.26, -0.01, 0.43), "M_Screen", skip=('+y',))
    return mb.finish(name, coll)


def keyboard(name, coll):
    mb = MB()
    mb.box((-0.22, -0.07, 0), (0.22, 0.07, 0.015), "M_Plastic_White")
    for r in range(5):
        y = -0.055 + r * 0.025
        segments = ((-0.21, -0.05), (-0.045, 0.12), (0.125, 0.21)) if r else ((-0.21, -0.08), (-0.075, 0.08), (0.085, 0.21))
        for x0, x1 in segments:
            mb.box((x0, y, 0.015), (x1, y + 0.02, 0.022), "M_Plastic_Black", skip=('-z',))
    return mb.finish(name, coll)


def corkboard(name, coll):
    mb = MB()
    mb.box((-0.6, -0.015, -0.45), (0.6, 0, 0.45), "M_Cork", skip=('+y',))
    f = 0.03
    mb.box((-0.6 - f, -0.025, -0.45 - f), (0.6 + f, -0.015, -0.45), "M_Wood_Light", skip=('+y',))
    mb.box((-0.6 - f, -0.025, 0.45), (0.6 + f, -0.015, 0.45 + f), "M_Wood_Light", skip=('+y',))
    mb.box((-0.6 - f, -0.025, -0.45), (-0.6, -0.015, 0.45), "M_Wood_Light", skip=('+y', '-z', '+z'))
    mb.box((0.6, -0.025, -0.45), (0.6 + f, -0.015, 0.45), "M_Wood_Light", skip=('+y', '-z', '+z'))
    return mb.finish(name, coll)


def paper_note(name, coll):
    # A6-briefje, verticaal met punaise; oorsprong achteraan-midden -> direct op het prikbord te plaatsen.
    mb = MB()
    mb.box((-0.0525, -0.001, -0.074), (0.0525, 0, 0.074), "M_Paper")
    root = mb.finish(name, coll)
    root["uv_fill"] = True
    mb = MB()
    mb.cyl((0, -0.0012, 0.06), 0.006, -0.008, "M_Paint_Red", axis='Y', seg=8, caps=(False, True))
    child(mb, name + "_Pin", coll, root)
    return root


def poster_frame(name, coll):
    # A1-kader (59,4 x 84,1 cm). De poster is een apart child met UV 0..1 -> M_Poster wisselen voor andere posters.
    mb = MB()
    w, h, f = 0.297, 0.4205, 0.015
    mb.box((-w - f, -0.012, -h - f), (w + f, 0, -h), "M_Metal_Chrome", skip=('+y',))
    mb.box((-w - f, -0.012, h), (w + f, 0, h + f), "M_Metal_Chrome", skip=('+y',))
    mb.box((-w - f, -0.012, -h), (-w, 0, h), "M_Metal_Chrome", skip=('+y', '-z', '+z'))
    mb.box((w, -0.012, -h), (w + f, 0, h), "M_Metal_Chrome", skip=('+y', '-z', '+z'))
    root = mb.finish(name, coll)
    mb = MB()
    mb.box((-w, -0.01, -h), (w, -0.01, h), "M_Poster", skip=('+y', '-x', '+x', '-z', '+z'))
    child(mb, name + "_Poster", coll, root, uv_fill=True)
    return root


def world_map(name, coll):
    mb = MB()
    for z in (-0.52, 0.52):
        mb.cyl((-0.86, -0.015, z), 0.012, 1.72, "M_Wood_Dark", axis='X', seg=8)
    root = mb.finish(name, coll)
    mb = MB()
    mb.box((-0.8, -0.015, -0.5), (0.8, -0.015, 0.5), "M_WorldMap", skip=('+y', '-x', '+x', '-z', '+z'))
    child(mb, name + "_Map", coll, root, uv_fill=True)
    return root


# ----------------------------------------------------------------------------
# Grab-props (oorsprong onderaan-midden, liggend)
# ----------------------------------------------------------------------------

def book(w, d, t, cover):
    def build(name, coll):
        mb = MB()
        c = 0.003
        mb.box((-w / 2, -d / 2, 0), (w / 2, d / 2, c), cover)
        mb.box((-w / 2, -d / 2, t - c), (w / 2, d / 2, t), cover)
        mb.box((-w / 2, -d / 2, c), (-w / 2 + c, d / 2, t - c), cover, skip=('-z', '+z'))
        mb.box((-w / 2 + c, -d / 2 + 0.003, c), (w / 2 - 0.003, d / 2 - 0.003, t - c), "M_Paper", skip=('-z', '+z', '-x'))
        return mb.finish(name, coll)
    return build


def pen(name, coll):
    mb = MB()
    mb.lathe([(0, 0), (0.0035, 0.004), (0.005, 0.02), (0.005, 0.14), (0, 0.142)], "M_Plastic_Chair",
             base=(-0.071, 0, 0.005), axis='X', seg=6)
    mb.box((0.025, -0.001, 0.009), (0.06, 0.001, 0.011), "M_Metal_Chrome")
    return mb.finish(name, coll)


def pencil(name, coll):
    mb = MB()
    b = (-0.09, 0, 0.0045)
    mb.lathe([(0, 0), (0.0015, 0.004)], "M_Rubber_Black", base=b, axis='X', seg=6)
    mb.lathe([(0.0015, 0.004), (0.0045, 0.02)], "M_Wood_Light", base=b, axis='X', seg=6)
    mb.lathe([(0.0045, 0.02), (0.0045, 0.165), (0, 0.165)], "M_Plastic_Yellow", base=b, axis='X', seg=6)
    mb.cyl((0.075, 0, 0.0045), 0.0042, 0.015, "M_Plastic_Pink", axis='X', seg=6, caps=(False, True))
    return mb.finish(name, coll)


def eraser(name, coll):
    mb = MB()
    mb.box((-0.025, -0.011, 0), (0.025, 0.011, 0.012), "M_Plastic_Pink")
    return mb.finish(name, coll)


def ruler(name, coll):
    mb = MB()
    mb.box((-0.15, -0.016, 0), (0.15, 0.016, 0.003), "M_Wood_Light")
    return mb.finish(name, coll)


def calculator(name, coll):
    mb = MB()
    mb.box((-0.04, -0.08, 0), (0.04, 0.08, 0.014), "M_Plastic_Black")
    mb.box((-0.032, 0.035, 0.014), (0.032, 0.068, 0.0145), "M_Screen", skip=('-z',))
    for r in range(5):
        for c in range(4):
            x, y = -0.03 + c * 0.0165, -0.07 + r * 0.019
            mb.box((x, y, 0.014), (x + 0.012, y + 0.013, 0.017), "M_Plastic_White", skip=('-z',))
    return mb.finish(name, coll)


def pencil_case(name, coll):
    mb = MB()
    mb.lathe([(0, -0.1), (0.035, -0.095), (0.035, 0.095), (0, 0.1)], "M_Fabric_Blue", base=(0, 0, 0.035), axis='X', seg=10)
    mb.box((-0.09, -0.002, 0.068), (0.09, 0.002, 0.071), "M_Metal_Chrome")
    return mb.finish(name, coll)


def backpack(fabric):
    def build(name, coll):
        # Rugzak rechtop; schouderbanden aan +Y.
        mb = MB()
        mb.box((-0.15, -0.09, 0), (0.15, 0.09, 0.40), fabric)
        mb.cyl((-0.15, 0, 0.40), 0.09, 0.30, fabric, axis='X', seg=10)
        mb.box((-0.12, -0.13, 0.04), (0.12, -0.09, 0.24), fabric, skip=('+y',))
        mb.box((-0.11, -0.132, 0.215), (0.11, -0.13, 0.222), "M_Metal_Chrome", skip=('+y',))
        for x in (-0.08, 0.08):
            mb.box((x - 0.025, 0.09, 0.04), (x + 0.025, 0.115, 0.42), "M_Fabric_Dark", skip=('-y',))
        mb.box((-0.04, -0.01, 0.48), (0.04, 0.01, 0.52), "M_Fabric_Dark")
        return mb.finish(name, coll)
    return build


def water_bottle(name, coll):
    mb = MB()
    mb.lathe([(0, 0), (0.034, 0), (0.035, 0.006), (0.035, 0.18), (0.022, 0.205), (0.02, 0.215), (0, 0.215)],
             "M_Plastic_Green", seg=12)
    mb.cyl((0, 0, 0.212), 0.021, 0.03, "M_Plastic_Black", seg=12)
    return mb.finish(name, coll)


def chalk(name, coll):
    mb = MB()
    mb.cyl((-0.04, 0, 0.0055), 0.0055, 0.08, "M_Plastic_White", axis='X', seg=8)
    return mb.finish(name, coll)


def marker(name, coll):
    mb = MB()
    b = (-0.07, 0, 0.0095)
    mb.lathe([(0, 0), (0.008, 0.002), (0.009, 0.01), (0.009, 0.11)], "M_Plastic_White", base=b, axis='X', seg=8)
    mb.lathe([(0.0095, 0.105), (0.0095, 0.14), (0, 0.142)], "M_Plastic_Black", base=b, axis='X', seg=8)
    mb.lathe([(0.006, -0.008), (0.006, 0), (0, 0)], "M_Plastic_Black", base=b, axis='X', seg=8)
    return mb.finish(name, coll)


CLASSROOM = [
    ("SM_Class_Desk_A", desk_a),
    ("SM_Class_Chair_A", chair_a),
    ("SM_Class_TeacherDesk", teacher_desk),
    ("SM_Class_OfficeChair", office_chair),
    ("SM_Class_Chalkboard", board(3.0, 1.2, "M_Chalkboard", "M_Wood_Light", "M_Wood_Light")),
    ("SM_Class_Whiteboard", board(2.4, 1.2, "M_Whiteboard", "M_Metal_Chrome", "M_Metal_Chrome", frame=0.02)),
    ("SM_Class_SmartBoard", smartboard),
    ("SM_Class_Projector", projector),
    ("SM_Class_ProjectorScreen", projector_screen),
    ("SM_Class_Cabinet", cabinet),
    ("SM_Class_Bookshelf", bookshelf),
    ("SM_Class_CoatRack", coat_rack),
    ("SM_Class_TrashBin", trash_bin),
    ("SM_Class_WallClock", wall_clock),
    ("SM_Class_Laptop", laptop),
    ("SM_Class_PC_Monitor", pc_monitor),
    ("SM_Class_Keyboard", keyboard),
    ("SM_Class_Corkboard", corkboard),
    ("SM_Class_PaperNote", paper_note),
    ("SM_Class_Poster_Frame", poster_frame),
    ("SM_Class_WorldMap", world_map),
]

PROPS = [
    ("SM_Prop_Book_A", book(0.17, 0.24, 0.03, "M_Paint_Red")),
    ("SM_Prop_Book_B", book(0.21, 0.297, 0.02, "M_Fabric_Blue")),
    ("SM_Prop_Book_C", book(0.15, 0.21, 0.04, "M_Plastic_Green")),
    ("SM_Prop_Notebook", book(0.21, 0.297, 0.008, "M_Plastic_Yellow")),
    ("SM_Prop_Pen", pen),
    ("SM_Prop_Pencil", pencil),
    ("SM_Prop_Eraser", eraser),
    ("SM_Prop_Ruler", ruler),
    ("SM_Prop_Calculator", calculator),
    ("SM_Prop_PencilCase", pencil_case),
    ("SM_Prop_Backpack_A", backpack("M_Fabric_Blue")),
    ("SM_Prop_Backpack_B", backpack("M_Fabric_Red")),
    ("SM_Prop_WaterBottle", water_bottle),
    ("SM_Prop_Chalk", chalk),
    ("SM_Prop_Marker", marker),
]


def export_dir_for(name):
    return os.path.join(ART, "Props" if name.startswith("SM_Prop_") else "Classroom")


def main(export=True):
    return kit["build_kit"](CLASSROOM + PROPS, "Classroom_Kit", BLEND_PATH, export_dir_for, (), export)


if __name__ == "__main__":
    main()
