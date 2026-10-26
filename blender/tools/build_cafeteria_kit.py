"""
Bouwt de refter-assets (Fase 4 van het VR School asset-plan, eerste ruimte): lange tafel, stoel, buffet-toog,
microgolfoven en twee grab-props (dienblad, brooddoos).
Hergebruikt build_classroom_kit.py (en daarmee MB / build_kit / alle materialen van Fase 1 + 2).

Gebruik (Blender 5.x): Scripting-tab -> Run Script, of via Blender MCP exec(open(<pad>).read()).

Resultaat:
  - blender/models/props/SM_Cafeteria_Kit.blend (+ _report.json)
  - mixed_reality/Assets/Art/Cafeteria/SM_Cafe_*.fbx
  - mixed_reality/Assets/Art/Props/SM_Prop_Tray.fbx, SM_Prop_LunchBox.fbx

Conventies (zelfde als Fase 1-3, Blender Z-up, voorkant = -Y):
  - Alle assets: oorsprong onderaan-midden.
  - Stoel kijkt naar -Y (rugleuning aan +Y), zoals SM_Class_Chair_A.
  - Toog: klantenzijde (dienbladglijbaan, glazen kap) = -Y, personeelszijde = +Y.
  - Microgolfoven: deur is child "<naam>_Door", scharnier links (-X) vooraan, blad loopt naar +X,
    zelfde opbouw als de lockerdeuren -> in Unity +95° = openen naar voren.
"""

import os

from mathutils import Matrix

TOOLS = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() \
    else r"C:\0_school\3deJaar\Mixed_Reality\project\blender\tools"
CLASSROOM_SCRIPT = os.path.join(TOOLS, "build_classroom_kit.py")
cls = {"__name__": "build_classroom_kit", "__file__": CLASSROOM_SCRIPT}
exec(open(CLASSROOM_SCRIPT, encoding="utf-8").read(), cls)
kit = cls["kit"]
MB = kit["MB"]
child = cls["child"]
tilt_x = cls["tilt_x"]

REPO = kit["REPO"]
BLEND_PATH = os.path.join(REPO, "blender", "models", "props", "SM_Cafeteria_Kit.blend")
ART = os.path.join(REPO, "mixed_reality", "Assets", "Art")


# ----------------------------------------------------------------------------
# Meubels
# ----------------------------------------------------------------------------

def long_table(name, coll):
    # Reftertafel 2,4 x 0,8 m, blad op 75 cm (8 plaatsen).
    mb = MB()
    L, D, H, t = 2.4, 0.8, 0.75, 0.03
    mb.box((-L / 2, -D / 2, H - t), (L / 2, D / 2, H), "M_Plastic_White")
    for sx in (-1, 1):
        x = sx * (L / 2 - 0.12)
        for sy in (-1, 1):
            y = sy * (D / 2 - 0.08)
            mb.box((x - 0.025, y - 0.025, 0), (x + 0.025, y + 0.025, H - t), "M_Metal_Painted", skip=('+z',))
        mb.box((x - 0.02, -D / 2 + 0.1, H - t - 0.06), (x + 0.02, D / 2 - 0.1, H - t), "M_Metal_Painted", skip=('+z', '-y', '+y'))
    for sy in (-1, 1):
        y = sy * (D / 2 - 0.08)
        mb.box((-L / 2 + 0.145, y - 0.01, H - t - 0.08), (L / 2 - 0.145, y + 0.01, H - t), "M_Metal_Painted", skip=('+z', '-x', '+x'))
    return mb.finish(name, coll)


def cafe_chair(name, coll):
    # Stapelstoel: rode kunststof kuip op chromen poten, zit op 45 cm; kijkt naar -Y.
    mb = MB()
    s = 0.21
    mb.box((-s, -s, 0.425), (s, s, 0.45), "M_Paint_Red")
    for x in (-0.18, 0.18):
        for y in (-0.18, 0.18):
            mb.cyl((x, y, 0), 0.011, 0.425, "M_Metal_Chrome", seg=6, caps=(False, False))
    start = mb.mark()
    for x in (-0.18, 0.18):
        mb.cyl((x, 0.19, 0.45), 0.011, 0.25, "M_Metal_Chrome", seg=6, caps=(False, False))
    mb.box((-0.2, 0.19, 0.56), (0.2, 0.215, 0.82), "M_Paint_Red")
    tilt_x(start, mb, -10, (0, 0.19, 0.45))
    return mb.finish(name, coll)


def counter(name, coll):
    # Buffet-toog 3 x 0,8 m, blad op 90 cm. Klantenzijde -Y: dienbladglijbaan + glazen kap; 3 warmhoudbakken.
    mb = MB()
    L, D, H = 3.0, 0.8, 0.9
    mb.box((-L / 2 + 0.03, -D / 2 + 0.05, 0), (L / 2 - 0.03, D / 2 - 0.05, 0.1), "M_Rubber_Black", skip=('-z', '+z'))
    mb.box((-L / 2, -D / 2, 0.1), (L / 2, D / 2, H - 0.02), "M_Metal_White", skip=('-z',))
    mb.box((-L / 2 - 0.02, -D / 2 - 0.02, H - 0.02), (L / 2 + 0.02, D / 2 + 0.02, H), "M_Metal_Chrome")
    for x in (-0.8, 0.0, 0.8):
        mb.box((x - 0.3, -0.05, H), (x + 0.3, 0.3, H + 0.03), "M_Metal_Chrome", skip=('-z',))
    # glijbaan: twee buizen op beugels
    for x in (-1.35, -0.45, 0.45, 1.35):
        mb.box((x - 0.015, -0.6, H - 0.07), (x + 0.015, -D / 2, H - 0.05), "M_Metal_Chrome", skip=('+y',))
    for y in (-0.57, -0.47):
        mb.cyl((-L / 2, y, H - 0.04), 0.014, L, "M_Metal_Chrome", axis='X', seg=8)
    # glazen kap (sneeze guard) op 3 staanders
    for x in (-1.45, 0.0, 1.45):
        mb.cyl((x, -0.15, H), 0.012, 0.45, "M_Metal_Chrome", seg=6, caps=(False, True))
    mb.box((-1.45, -0.155, H + 0.12), (1.45, -0.145, H + 0.44), "M_Glass", skip=('-x', '+x'))
    return mb.finish(name, coll)


FEET = 0.008


def microwave(name, coll):
    # Microgolfoven 48 x 36 x 28 cm; open binnenruimte links, bedieningspaneel rechts. Deur = child met scharnier links.
    mb = MB()
    W, D, H, t = 0.48, 0.36, 0.28, 0.02
    x0, cx = -W / 2, 0.12                     # binnenruimte van x0+t tot cx
    mb.box((x0, -D / 2, 0), (W / 2, D / 2, t), "M_Metal_White")
    mb.box((x0, -D / 2, H - t), (W / 2, D / 2, H), "M_Metal_White")
    mb.box((x0, D / 2 - t, t), (cx, D / 2, H - t), "M_Metal_White", skip=('-z', '+z', '+x'))
    mb.box((x0, -D / 2, t), (x0 + t, D / 2 - t, H - t), "M_Metal_White", skip=('-z', '+z', '+y'))
    mb.box((cx, -D / 2, t), (W / 2, D / 2, H - t), "M_Metal_White", skip=('-z', '+z'))
    # bedieningspaneel: display + 6 toetsen + draaiknop
    mb.box((cx + 0.015, -D / 2 - 0.003, 0.03), (W / 2 - 0.015, -D / 2, H - 0.03), "M_Plastic_Black", skip=('+y',))
    mb.box((cx + 0.025, -D / 2 - 0.005, 0.2), (W / 2 - 0.025, -D / 2 - 0.003, 0.235), "M_Screen", skip=('+y',))
    for r in range(3):
        for c in range(2):
            x, z = cx + 0.03 + c * 0.04, 0.17 - r * 0.03
            mb.box((x, -D / 2 - 0.008, z - 0.02), (x + 0.03, -D / 2 - 0.003, z), "M_Metal_White", skip=('+y',))
    mb.cyl((cx + 0.06, -D / 2 - 0.003, 0.06), 0.02, -0.02, "M_Metal_White", axis='Y', seg=12, caps=(False, True))
    mb.transform_since(0, Matrix.Translation((0, 0, FEET)))     # kast op de pootjes, oorsprong = onderkant pootjes
    for x in (-0.1, 0.1):
        for y in (-0.12, 0.12):
            mb.box((x - 0.015, y - 0.015, 0), (x + 0.015, y + 0.015, FEET), "M_Plastic_Black", skip=('+z',))
    root = mb.finish(name, coll)

    # deur: frame rond een venster, handgreep aan de vrije rand
    mb = MB()
    w, h = cx - x0 - 0.004, H - 0.006
    mb.grid_panel([0, 0.045, w - 0.06, w], [0, 0.045, h - 0.045, h], -0.02, 0, {(1, 1)}, "M_Plastic_Black")
    mb.box((0.045, -0.012, 0.045), (w - 0.06, -0.008, h - 0.045), "M_Glass", skip=('-x', '+x', '-z', '+z'))
    mb.box((w - 0.035, -0.045, 0.06), (w - 0.02, -0.02, h - 0.06), "M_Metal_White", skip=('+y',))
    child(mb, name + "_Door", coll, root, location=(x0 + 0.002, -D / 2, FEET + 0.003))
    return root


# ----------------------------------------------------------------------------
# Grab-props (oorsprong onderaan-midden)
# ----------------------------------------------------------------------------

def tray(name, coll):
    # Dienblad 45 x 35 cm met opstaande rand van 2 cm.
    mb = MB()
    w, d, r, t = 0.225, 0.175, 0.01, 0.006
    mb.box((-w, -d, 0), (w, d, t), "M_Wood_Dark")
    mb.box((-w, -d, t), (w, -d + r, 0.02), "M_Wood_Dark", skip=('-z',))
    mb.box((-w, d - r, t), (w, d, 0.02), "M_Wood_Dark", skip=('-z',))
    mb.box((-w, -d + r, t), (-w + r, d - r, 0.02), "M_Wood_Dark", skip=('-z', '-y', '+y'))
    mb.box((w - r, -d + r, t), (w, d - r, 0.02), "M_Wood_Dark", skip=('-z', '-y', '+y'))
    return mb.finish(name, coll)


def lunch_box(name, coll):
    # Brooddoos 20 x 14 x 7,5 cm: groene bak, wit deksel met twee klipsluitingen.
    mb = MB()
    mb.box((-0.1, -0.07, 0), (0.1, 0.07, 0.062), "M_Plastic_Green")
    mb.box((-0.102, -0.072, 0.062), (0.102, 0.072, 0.075), "M_Plastic_White")
    mb.box((-0.03, -0.076, 0.045), (0.03, -0.072, 0.07), "M_Plastic_White", skip=('+y',))
    mb.box((-0.03, 0.072, 0.045), (0.03, 0.076, 0.07), "M_Plastic_White", skip=('-y',))
    return mb.finish(name, coll)


CAFETERIA = [
    ("SM_Cafe_LongTable", long_table),
    ("SM_Cafe_Chair", cafe_chair),
    ("SM_Cafe_Counter", counter),
    ("SM_Cafe_Microwave", microwave),
]

PROPS = [
    ("SM_Prop_Tray", tray),
    ("SM_Prop_LunchBox", lunch_box),
]


def export_dir_for(name):
    return os.path.join(ART, "Props" if name.startswith("SM_Prop_") else "Cafeteria")


def main(export=True):
    return kit["build_kit"](CAFETERIA + PROPS, "Cafeteria_Kit", BLEND_PATH, export_dir_for, (), export)


if __name__ == "__main__":
    main()
