"""
Bouwt de gang-assets (Fase 3 van het VR School asset-plan): lockers, bank, infoscherm, trofeeënkast,
fotokader, plantenbak, waterfontein, drankautomaat en twee trofeeën (grab-props).
Hergebruikt build_classroom_kit.py (en daarmee MB / build_kit / alle materialen van Fase 1 + 2).

Gebruik (Blender 5.x): Scripting-tab -> Run Script, of via Blender MCP exec(open(<pad>).read()).

Resultaat:
  - blender/models/props/SM_Hallway_Kit.blend (+ _report.json)
  - mixed_reality/Assets/Art/Hallway/SM_Hall_*.fbx
  - mixed_reality/Assets/Art/Props/SM_Prop_Trophy_*.fbx

Conventies (zelfde als Fase 1/2, Blender Z-up, voorkant = -Y):
  - Tegen de muur staande meubels (lockers, trofeeënkast, fontein, drankautomaat): oorsprong achteraan-midden
    op de vloer -> in Unity op het muuroppervlak plaatsen.
  - Wandobjecten (infoscherm, fotokader): oorsprong achteraan-midden in het centrum.
  - Vrijstaand (bank, plantenbak, plant) en grab-props: oorsprong onderaan-midden.
  - Lockerdeuren zijn aparte children "<naam>_Door[_N]", pivot op het scharnier (links, voorkant, onderrand
    van de deur); openen = draaien rond lokale Z (Unity Y), zelfde zin als SM_Build_Door_Classroom.
  - Beeldvlakken (infoscherm, foto) zijn aparte children met UV 0..1 -> materiaal/texture wisselen.
"""

import os

TOOLS = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() \
    else r"C:\0_school\3deJaar\Mixed_Reality\project\blender\tools"
CLASSROOM_SCRIPT = os.path.join(TOOLS, "build_classroom_kit.py")
cls = {"__name__": "build_classroom_kit", "__file__": CLASSROOM_SCRIPT}
exec(open(CLASSROOM_SCRIPT, encoding="utf-8").read(), cls)
kit = cls["kit"]
MB = kit["MB"]
child = cls["child"]

REPO = kit["REPO"]
BLEND_PATH = os.path.join(REPO, "blender", "models", "props", "SM_Hallway_Kit.blend")
ART = os.path.join(REPO, "mixed_reality", "Assets", "Art")

# Extra materialen voor Fase 3 (moeten ook in VRSchoolBuildCore.Materials staan)
kit["MATERIALS"].update({
    "M_Metal_Gold": ((0.85, 0.62, 0.20, 1), 1.0, 0.25, None, 0),
    "M_Soil": ((0.10, 0.06, 0.035, 1), 0.0, 1.0, None, 0),
    "M_Plant_Leaf": ((0.08, 0.30, 0.06, 1), 0.0, 0.7, None, 0),
})


# ----------------------------------------------------------------------------
# Lockers
# ----------------------------------------------------------------------------

LW, LD, LH, PLINTH = 0.3, 0.45, 1.8, 0.1     # breedte per vak, diepte, hoogte, sokkel
SHEET = 0.01                                  # plaatdikte
DOOR_T = 0.012


def locker_body(mb, n):
    """Open kast (voorkant open) met n vakken naast elkaar, gecentreerd op X; achterkant op y=0."""
    W = n * LW
    x0, y0 = -W / 2, -LD
    mb.box((x0, y0, 0), (-x0, -0.02, PLINTH), "M_Rubber_Black", skip=('-z',))   # sokkel, iets terug
    mb.box((x0, -SHEET, PLINTH), (-x0, 0, LH), "M_Metal_Painted", skip=('-z',))    # achterwand
    mb.box((x0, y0, PLINTH), (-x0, -SHEET, PLINTH + SHEET), "M_Metal_Painted", skip=('+y',))   # bodem
    mb.box((x0, y0, LH - SHEET), (-x0, -SHEET, LH), "M_Metal_Painted", skip=('+y',))          # dak
    for k in range(n + 1):                     # zijwanden en tussenschotten
        x = x0 + k * LW
        a, b = (x, x + SHEET) if k == 0 else (x - SHEET, x) if k == n else (x - SHEET / 2, x + SHEET / 2)
        mb.box((a, y0, PLINTH + SHEET), (b, -SHEET, LH - SHEET), "M_Metal_Painted", skip=('+y', '-z', '+z'))
    for k in range(n):                         # hoedenplank per vak
        a = x0 + k * LW + (SHEET if k == 0 else SHEET / 2)
        b = x0 + (k + 1) * LW - (SHEET if k == n - 1 else SHEET / 2)
        mb.box((a, y0 + 0.02, 1.48), (b, -SHEET, 1.48 + SHEET), "M_Metal_Painted", skip=('-x', '+x', '+y'))


def locker_door(name, coll, root, x_hinge):
    """Deur als child; lokaal loopt het blad van x=0 (scharnier) naar +X, voorkant -Y."""
    mb = MB()
    w, h = LW - 0.004, LH - PLINTH - 0.004
    mb.box((0, -DOOR_T, 0), (w, 0, h), "M_Metal_Painted")
    # verluchtingsgleuven boven en onder
    for zc in (h - 0.12, h - 0.16, h - 0.20, 0.12, 0.16, 0.20):
        mb.box((0.07, -DOOR_T - 0.002, zc - 0.006), (w - 0.07, -DOOR_T, zc + 0.006), "M_Rubber_Black", skip=('+y',))
    # handgreep aan de vrije rand
    mb.box((w - 0.05, -DOOR_T - 0.025, h / 2 - 0.06), (w - 0.03, -DOOR_T, h / 2 + 0.06), "M_Metal_Chrome", skip=('+y',))
    return child(mb, name, coll, root, location=(x_hinge + 0.002, -LD, PLINTH + 0.002))


def locker_row(n):
    def build(name, coll):
        mb = MB()
        locker_body(mb, n)
        root = mb.finish(name, coll)
        for k in range(n):
            door = f"{name}_Door" if n == 1 else f"{name}_Door_{k + 1}"
            locker_door(door, coll, root, -n * LW / 2 + k * LW)
        return root
    return build


# ----------------------------------------------------------------------------
# Meubels en wandobjecten
# ----------------------------------------------------------------------------

def bench(name, coll):
    # Gangbank 1,6 x 0,4 m, zit op 45 cm: 4 houten latten op een metalen frame.
    mb = MB()
    L, D, H = 1.6, 0.4, 0.45
    slat, gap = 0.085, (0.4 - 4 * 0.085) / 3
    for i in range(4):
        y = -D / 2 + i * (slat + gap)
        mb.box((-L / 2, y, H - 0.03), (L / 2, y + slat, H), "M_Wood_Light")
    for x in (-L / 2 + 0.12, L / 2 - 0.12):
        for y in (-D / 2 + 0.03, D / 2 - 0.03):
            mb.box((x - 0.02, y - 0.02, 0), (x + 0.02, y + 0.02, H - 0.06), "M_Metal_Painted", skip=('+z',))
        mb.box((x - 0.02, -D / 2 + 0.01, H - 0.06), (x + 0.02, D / 2 - 0.01, H - 0.03), "M_Metal_Painted")
    mb.box((-L / 2 + 0.14, -0.015, H - 0.06), (L / 2 - 0.14, 0.015, H - 0.03), "M_Metal_Painted", skip=('-x', '+x'))
    return mb.finish(name, coll)


def info_screen(name, coll):
    # 55"-scherm aan de muur (1,25 x 0,73 m); oorsprong achteraan-midden in het centrum.
    mb = MB()
    w, h = 0.625, 0.365
    mb.box((-0.2, -0.03, -0.15), (0.2, 0, 0.15), "M_Plastic_Black", skip=('+y',))       # muurbeugel
    mb.box((-w, -0.075, -h), (w, -0.03, h), "M_Plastic_Black")
    root = mb.finish(name, coll)
    mb = MB()
    mb.box((-w + 0.015, 0, -h + 0.015), (w - 0.015, 0, h - 0.015), "M_Screen", skip=('+y', '-x', '+x', '-z', '+z'))
    child(mb, name + "_Screen", coll, root, location=(0, -0.0755, 0), uv_fill=True)
    return root


def trophy_case(name, coll):
    # Vitrinekast 1,2 x 0,45 x 1,9 m: dichte houten onderkast, glazen bovenkast met 2 glasplaten.
    mb = MB()
    W, D, H, base = 1.2, 0.45, 1.9, 0.8
    x0, y0 = -W / 2, -D
    mb.box((x0, y0, 0), (-x0, 0, base), "M_Wood_Dark", skip=('-z',))
    mb.box((x0 + 0.01, y0 - 0.003, 0.08), (-0.003, y0, base - 0.03), "M_Wood_Dark", skip=('+y',))   # deurtjes
    mb.box((0.003, y0 - 0.003, 0.08), (-x0 - 0.01, y0, base - 0.03), "M_Wood_Dark", skip=('+y',))
    for x in (-0.03, 0.03):
        mb.box((x - 0.005, y0 - 0.02, 0.4), (x + 0.005, y0 - 0.003, 0.5), "M_Metal_Chrome", skip=('+y',))
    mb.box((x0, y0, H - 0.05), (-x0, 0, H), "M_Wood_Dark")                                          # dak
    mb.box((x0, -0.02, base), (-x0, 0, H - 0.05), "M_Wood_Dark", skip=('-z', '+z'))                  # achterwand
    p = 0.03                                                                                        # hoekstijlen
    for xa in (x0, -x0 - p):
        mb.box((xa, y0, base), (xa + p, y0 + p, H - 0.05), "M_Wood_Dark", skip=('-z', '+z'))
    g = 0.006                                                                                       # glas
    mb.box((x0 + p, y0, base), (-x0 - p, y0 + g, H - 0.05), "M_Glass", skip=('-z', '+z', '-x', '+x'))
    for xa in (x0, -x0 - g):
        mb.box((xa, y0 + p, base), (xa + g, -0.02, H - 0.05), "M_Glass", skip=('-z', '+z', '-y', '+y'))
    for z in (1.15, 1.5):
        mb.box((x0 + g, y0 + g, z - 0.005), (-x0 - g, -0.02, z + 0.005), "M_Glass", skip=('-x', '+x', '-y', '+y'))
    return mb.finish(name, coll)


def picture_frame(name, coll):
    # Liggend fotokader 60 x 45 cm (buitenmaat); de foto is een apart child met UV 0..1 (M_Poster wisselen).
    mb = MB()
    w, h, f = 0.27, 0.195, 0.03
    mb.box((-w - f, -0.02, -h - f), (w + f, 0, -h), "M_Wood_Dark", skip=('+y',))
    mb.box((-w - f, -0.02, h), (w + f, 0, h + f), "M_Wood_Dark", skip=('+y',))
    mb.box((-w - f, -0.02, -h), (-w, 0, h), "M_Wood_Dark", skip=('+y', '-z', '+z'))
    mb.box((w, -0.02, -h), (w + f, 0, h), "M_Wood_Dark", skip=('+y', '-z', '+z'))
    root = mb.finish(name, coll)
    mb = MB()
    mb.box((-w, -0.012, -h), (w, -0.012, h), "M_Poster", skip=('+y', '-x', '+x', '-z', '+z'))
    child(mb, name + "_Picture", coll, root, uv_fill=True)
    return root


PLANTER_SOIL = 0.40


def planter(name, coll):
    # Vierkante betonnen bak 50 x 50 x 45 cm met aarde op 40 cm.
    mb = MB()
    s, H, t = 0.25, 0.45, 0.03
    i = s - t
    mb.box((-s, -s, 0), (s, s, H), "M_Concrete", skip=('+z',))
    # bovenrand als 4 stroken, binnenwand naar beneden tot de aarde
    for (a, b) in (((-s, -s), (s, -i)), ((-s, i), (s, s)), ((-s, -i), (-i, i)), ((i, -i), (s, i))):
        mb.box((a[0], a[1], H), (b[0], b[1], H), "M_Concrete", skip=('-z', '-x', '+x', '-y', '+y'))
    mb.box((-i, -i, PLANTER_SOIL), (i, i, H), "M_Concrete", skip=('+z', '-z'))
    mb.box((-i, -i, PLANTER_SOIL), (i, i, PLANTER_SOIL), "M_Soil", skip=('-x', '+x', '-y', '+y', '-z'))
    return mb.finish(name, coll)


def plant(name, coll):
    # Gestileerde kamerplant (~1,2 m): stam + drie bladbollen. Oorsprong onderaan; op de aarde van de bak zetten.
    mb = MB()
    mb.cyl((0, 0, 0), 0.025, 0.75, "M_Wood_Dark", seg=6, caps=(False, False), r_top=0.015)
    blob = [(0, 0), (0.18, 0.05), (0.26, 0.17), (0.22, 0.30), (0.12, 0.38), (0, 0.41)]
    for (bx, by, bz, sc) in ((0, 0, 0.62, 1.0), (0.12, -0.08, 0.45, 0.7), (-0.13, 0.07, 0.5, 0.75)):
        mb.lathe([(r * sc, h * sc) for r, h in blob], "M_Plant_Leaf", base=(bx, by, bz), seg=8)
    return mb.finish(name, coll)


def water_fountain(name, coll):
    # Drinkfontein tegen de muur: kolom + RVS-bak op 90 cm, drukknop vooraan. Oorsprong achteraan-midden, vloer.
    mb = MB()
    mb.box((-0.15, -0.28, 0), (0.15, 0, 0.82), "M_Metal_White", skip=('-z',))
    top = 0.92
    mb.box((-0.2, -0.36, 0.82), (0.2, 0, top), "M_Metal_Chrome", skip=('+z',))
    # bak: rand + verzonken bodem
    r = 0.02
    for (a, b) in (((-0.2, -0.36), (0.2, -0.36 + r)), ((-0.2, -r), (0.2, 0)),
                   ((-0.2, -0.36 + r), (-0.2 + r, -r)), ((0.2 - r, -0.36 + r), (0.2, -r))):
        mb.box((a[0], a[1], top - 0.001), (b[0], b[1], top), "M_Metal_Chrome", skip=('-z',))
    i0, i1 = (-0.2 + r, -0.36 + r), (0.2 - r, -r)
    mb.box((i0[0], i0[1], top - 0.05), (i1[0], i1[1], top), "M_Metal_Chrome", skip=('+z',))
    mb.cyl((0, -0.06, top - 0.05), 0.012, 0.02, "M_Metal_Chrome", seg=8)                 # afvoer
    mb.cyl((0.08, -0.06, top), 0.012, 0.06, "M_Metal_Chrome", seg=8)                     # kraantje
    mb.cyl((0.08, -0.06, top + 0.06), 0.012, -0.05, "M_Metal_Chrome", axis='Y', seg=8)
    mb.cyl((0, -0.36, 0.87), 0.018, -0.015, "M_Plastic_Black", axis='Y', seg=10, caps=(False, True))   # knop
    return mb.finish(name, coll)


def vending_machine(name, coll):
    # Drankautomaat 0,9 x 0,8 x 1,85 m. Oorsprong achteraan-midden, vloer. Glasvenster links, bediening rechts.
    mb = MB()
    W, D, H = 0.9, 0.8, 1.85
    x0, y0 = -W / 2, -D
    gx0, gx1, gz0, gz1 = x0 + 0.05, 0.17, 0.55, 1.78
    mb.grid_panel([x0, gx0, gx1, -x0], [0.05, gz0, gz1, H], y0, y0 + 0.04, {(1, 1)}, "M_Paint_Red")
    mb.box((x0, y0 + 0.04, 0.05), (-x0, 0, H), "M_Paint_Red", skip=('-y',))
    mb.box((x0 + 0.02, y0 + 0.02, 0), (-x0 - 0.02, -0.02, 0.05), "M_Plastic_Black", skip=('-z', '+z'))
    mb.box((gx0, y0 + 0.30, gz0), (gx1, y0 + 0.30, gz1), "M_Plastic_Black", skip=('+y', '-x', '+x', '-z', '+z'))
    mb.box((gx0, y0 + 0.04, gz0), (gx1, y0 + 0.30, gz0), "M_Plastic_Black", skip=('-z', '-x', '+x', '-y', '+y'))
    mb.box((gx0, y0 + 0.04, gz0), (gx0, y0 + 0.30, gz1), "M_Plastic_Black", skip=('-x', '-z', '+z', '-y', '+y'))
    mb.box((gx1, y0 + 0.04, gz0), (gx1, y0 + 0.30, gz1), "M_Plastic_Black", skip=('+x', '-z', '+z', '-y', '+y'))
    mb.box((gx0, y0 + 0.04, gz1), (gx1, y0 + 0.30, gz1), "M_Plastic_Black", skip=('+z', '-x', '+x', '-y', '+y'))
    # 5 schappen met elk 6 producten (afwisselend geel / groen)
    cols = 6
    pw = (gx1 - gx0 - 0.04) / cols
    for r in range(5):
        z = gz0 + 0.03 + r * 0.24
        mb.box((gx0, y0 + 0.08, z - 0.01), (gx1, y0 + 0.30, z), "M_Plastic_White", skip=('-x', '+x', '+y'))
        for c in range(cols):
            x = gx0 + 0.02 + c * pw
            mat = "M_Plastic_Yellow" if (r + c) % 2 else "M_Plastic_Green"
            mb.box((x + 0.01, y0 + 0.10, z), (x + pw - 0.01, y0 + 0.16, z + 0.14), mat, skip=('-z', '+y'))
    mb.box((gx0, y0 + 0.035, gz0), (gx1, y0 + 0.041, gz1), "M_Glass", skip=('-x', '+x', '-z', '+z'))
    # bediening: scherm, toetsen, muntgleuf
    mb.box((0.22, y0 - 0.003, 1.35), (0.40, y0, 1.47), "M_Screen", skip=('+y',))
    for r in range(4):
        for c in range(3):
            x, z = 0.24 + c * 0.055, 1.27 - r * 0.055
            mb.box((x, y0 - 0.012, z - 0.04), (x + 0.04, y0, z), "M_Plastic_White", skip=('+y',))
    mb.box((0.29, y0 - 0.006, 0.98), (0.33, y0, 1.06), "M_Plastic_Black", skip=('+y',))
    # uitgifteklep onderaan
    mb.box((-0.3, y0 - 0.004, 0.15), (0.15, y0, 0.35), "M_Plastic_Black", skip=('+y',))
    return mb.finish(name, coll)


# ----------------------------------------------------------------------------
# Grab-props: trofeeën (oorsprong onderaan-midden)
# ----------------------------------------------------------------------------

def trophy_a(name, coll):
    # Beker (25 cm) op een houten voet met twee oren.
    mb = MB()
    mb.box((-0.045, -0.045, 0), (0.045, 0.045, 0.05), "M_Wood_Dark")
    mb.lathe([(0.03, 0.05), (0.03, 0.06), (0.008, 0.075), (0.008, 0.13), (0.02, 0.14), (0.05, 0.17),
              (0.055, 0.25), (0.05, 0.25), (0.045, 0.18), (0, 0.17)], "M_Metal_Gold", seg=12)
    for s in (-1, 1):
        mb.box((s * 0.055, -0.004, 0.185), (s * 0.075, 0.004, 0.193), "M_Metal_Gold")
        mb.box((s * 0.067, -0.004, 0.193), (s * 0.075, 0.004, 0.235), "M_Metal_Gold")
        mb.box((s * 0.055, -0.004, 0.235), (s * 0.075, 0.004, 0.243), "M_Metal_Gold")
    return mb.finish(name, coll)


def trophy_b(name, coll):
    # Zuiltje (22 cm) met gouden bol op een zwarte voet.
    mb = MB()
    mb.box((-0.04, -0.04, 0), (0.04, 0.04, 0.04), "M_Plastic_Black")
    mb.box((-0.025, -0.025, 0.04), (0.025, 0.025, 0.06), "M_Plastic_Black", skip=('-z',))
    mb.box((-0.012, -0.012, 0.06), (0.012, 0.012, 0.17), "M_Metal_Gold", skip=('-z',))
    mb.lathe([(0, 0), (0.017, 0.006), (0.024, 0.024), (0.017, 0.042), (0, 0.048)], "M_Metal_Gold",
             base=(0, 0, 0.168), seg=10)
    return mb.finish(name, coll)


HALLWAY = [
    ("SM_Hall_Locker_Single", locker_row(1)),
    ("SM_Hall_Locker_Row_5", locker_row(5)),
    ("SM_Hall_Bench", bench),
    ("SM_Hall_InfoScreen", info_screen),
    ("SM_Hall_TrophyCase", trophy_case),
    ("SM_Hall_PictureFrame", picture_frame),
    ("SM_Hall_Planter", planter),
    ("SM_Hall_Plant", plant),
    ("SM_Hall_WaterFountain", water_fountain),
    ("SM_Hall_VendingMachine", vending_machine),
]

PROPS = [
    ("SM_Prop_Trophy_A", trophy_a),
    ("SM_Prop_Trophy_B", trophy_b),
]


def export_dir_for(name):
    return os.path.join(ART, "Props" if name.startswith("SM_Prop_") else "Hallway")


def main(export=True):
    return kit["build_kit"](HALLWAY + PROPS, "Hallway_Kit", BLEND_PATH, export_dir_for, (), export)


if __name__ == "__main__":
    main()
