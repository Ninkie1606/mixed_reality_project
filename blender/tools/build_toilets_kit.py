"""
Bouwt de toilet-assets (Fase 4 van het VR School asset-plan): toiletcabine, toilet, lavabo, spiegel,
handendroger en zeepdispenser.
Hergebruikt build_classroom_kit.py (en daarmee MB / build_kit / alle materialen van Fase 1 + 2).

Gebruik (Blender 5.x): Scripting-tab -> Run Script, of via Blender MCP exec(open(<pad>).read()).

Resultaat:
  - blender/models/props/SM_Toilets_Kit.blend (+ _report.json)
  - mixed_reality/Assets/Art/Toilets/SM_WC_*.fbx

Conventies (zelfde als Fase 1-3, Blender Z-up, voorkant = -Y):
  - Tegen de muur (cabine, toilet, lavabo): oorsprong achteraan-midden op de vloer -> op het muuroppervlak plaatsen.
  - Wandobjecten (spiegel, handendroger, zeepdispenser): oorsprong achteraan-midden in het centrum.
  - Cabine: zijwanden + front met deur; de deur is child "<naam>_Door", scharnier links vooraan, blad naar +X
    (zelfde opbouw als lockers/microgolf -> in Unity +95° = openen naar buiten, weg van het toilet).
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

REPO = kit["REPO"]
BLEND_PATH = os.path.join(REPO, "blender", "models", "props", "SM_Toilets_Kit.blend")
ART = os.path.join(REPO, "mixed_reality", "Assets", "Art")

# Extra materialen voor de toiletten (moeten ook in VRSchoolBuildCore.Materials staan)
kit["MATERIALS"].update({
    "M_Ceramic_White": ((0.92, 0.92, 0.90, 1), 0.0, 0.1, None, 0),
    "M_Laminate_Teal": ((0.10, 0.38, 0.40, 1), 0.0, 0.4, None, 0),
    "M_Mirror": ((0.95, 0.95, 0.95, 1), 1.0, 0.02, None, 0),
})


# ----------------------------------------------------------------------------
# Cabine
# ----------------------------------------------------------------------------

SW, SD, SH, GAP, PT = 0.9, 1.5, 2.0, 0.15, 0.025   # breedte, diepte, hoogte, vrije ruimte onder, paneeldikte
DOOR_X0, DOOR_X1 = -0.35, 0.30                      # deuropening in het front


def stall(name, coll):
    mb = MB()
    w = SW / 2
    for xa in (-w, w - PT):                          # zijwanden
        mb.box((xa, -SD, GAP), (xa + PT, 0, SH), "M_Laminate_Teal")
    y0 = -SD
    mb.box((-w + PT, y0, GAP), (DOOR_X0, y0 + PT, SH), "M_Laminate_Teal", skip=('-x',))   # frontpilasters
    mb.box((DOOR_X1, y0, GAP), (w - PT, y0 + PT, SH), "M_Laminate_Teal", skip=('+x',))
    mb.cyl((-w, y0 + PT / 2, SH), 0.015, SW, "M_Metal_Chrome", axis='X', seg=6)          # bovenregel
    for x in (-w + PT / 2, w - PT / 2):                                                   # voetjes
        for y in (y0 + 0.05, -0.05):
            mb.cyl((x, y, 0), 0.015, GAP, "M_Metal_Chrome", seg=6, caps=(False, False))
    root = mb.finish(name, coll)

    mb = MB()
    dw, dh = DOOR_X1 - DOOR_X0 - 0.006, SH - GAP - 0.01
    mb.box((0, -PT, 0), (dw, 0, dh), "M_Laminate_Teal")
    mb.box((dw - 0.07, -PT - 0.04, 1.0), (dw - 0.05, -PT, 1.12), "M_Metal_Chrome", skip=('+y',))   # greep
    mb.box((dw - 0.075, -PT - 0.004, 1.17), (dw - 0.045, -PT, 1.2), "M_Plastic_Green", skip=('+y',))  # "vrij"-indicator
    child(mb, name + "_Door", coll, root, location=(DOOR_X0 + 0.003, y0 + PT, GAP + 0.005))
    return root


# ----------------------------------------------------------------------------
# Sanitair
# ----------------------------------------------------------------------------

def toilet(name, coll):
    # Staand toilet met spoelbak; pot steekt 70 cm uit de muur, zitting op 42 cm.
    mb = MB()
    start = mb.mark()
    mb.lathe([(0.11, 0), (0.12, 0.03), (0.13, 0.2), (0.18, 0.36), (0.185, 0.395), (0.15, 0.395),
              (0.09, 0.2), (0, 0.18)], "M_Ceramic_White", seg=16)
    mb.transform_since(start, Matrix.Translation((0, -0.47, 0)) @ Matrix.Diagonal((1, 1.25, 1, 1)))
    start = mb.mark()
    mb.lathe([(0.185, 0.395), (0.19, 0.42), (0.13, 0.42), (0.125, 0.4)], "M_Plastic_White", seg=16)
    mb.transform_since(start, Matrix.Translation((0, -0.47, 0)) @ Matrix.Diagonal((1, 1.25, 1, 1)))
    mb.box((-0.11, -0.3, 0), (0.11, -0.16, 0.36), "M_Ceramic_White", skip=('-z',))      # voet naar de muur
    mb.box((-0.19, -0.2, 0.36), (0.19, 0, 0.8), "M_Ceramic_White")                      # spoelbak
    mb.cyl((0, -0.1, 0.8), 0.025, 0.008, "M_Metal_Chrome", seg=10)                      # spoelknop
    return mb.finish(name, coll)


def sink(name, coll):
    # Hangende lavabo 55 x 45 cm, rand op 85 cm; oorsprong achteraan-midden op de vloer.
    mb = MB()
    w, d, top, z0, r = 0.275, 0.45, 0.85, 0.7, 0.03
    mb.box((-w, -d, z0), (w, -0.0, top), "M_Ceramic_White", skip=('+z', '+y'))
    for (a, b) in (((-w, -d), (w, -d + r)), ((-w, -0.12), (w, 0)), ((-w, -d + r), (-w + r, -0.12)), ((w - r, -d + r), (w, -0.12))):
        mb.box((a[0], a[1], top), (b[0], b[1], top), "M_Ceramic_White", skip=('-z', '-x', '+x', '-y', '+y'))
    mb.box((-w + r, -d + r, top - 0.12), (w - r, -0.12, top), "M_Ceramic_White", skip=('+z',))   # kom
    mb.cyl((0, -0.06, top), 0.02, 0.12, "M_Metal_Chrome", seg=8)                        # kraan
    mb.cyl((0, -0.06, top + 0.11), 0.012, -0.12, "M_Metal_Chrome", axis='Y', seg=8)
    mb.cyl((0, -0.25, 0.48), 0.02, z0 - 0.48, "M_Metal_Chrome", seg=8, caps=(False, False))   # sifon
    mb.cyl((0, -0.25, 0.48), 0.02, 0.25, "M_Metal_Chrome", axis='Y', seg=8, caps=(True, False))
    return mb.finish(name, coll)


def mirror(name, coll):
    # Spiegel 60 x 80 cm met smalle chromen rand; oorsprong achteraan-midden in het centrum.
    mb = MB()
    w, h, f = 0.3, 0.4, 0.012
    mb.box((-w, -0.006, -h), (w, 0, h), "M_Mirror", skip=('+y',))
    mb.box((-w - f, -0.01, -h - f), (w + f, 0, -h), "M_Metal_Chrome", skip=('+y',))
    mb.box((-w - f, -0.01, h), (w + f, 0, h + f), "M_Metal_Chrome", skip=('+y',))
    mb.box((-w - f, -0.01, -h), (-w, 0, h), "M_Metal_Chrome", skip=('+y', '-z', '+z'))
    mb.box((w, -0.01, -h), (w + f, 0, h), "M_Metal_Chrome", skip=('+y', '-z', '+z'))
    return mb.finish(name, coll)


def hand_dryer(name, coll):
    # Handendroger 28 x 20 x 30 cm, luchtmond onderaan.
    mb = MB()
    mb.box((-0.14, -0.2, -0.15), (0.14, 0, 0.15), "M_Plastic_White", skip=('+y',))
    mb.box((-0.06, -0.15, -0.165), (0.06, -0.06, -0.15), "M_Plastic_Black", skip=('+z',))
    mb.box((-0.03, -0.202, 0.06), (0.03, -0.2, 0.1), "M_Plastic_Black", skip=('+y',))    # sensorvenster
    return mb.finish(name, coll)


def soap_dispenser(name, coll):
    # Zeepdispenser 10 x 10 x 20 cm met kijkvenster en drukknop.
    mb = MB()
    mb.box((-0.05, -0.1, -0.1), (0.05, 0, 0.1), "M_Plastic_White", skip=('+y',))
    mb.box((-0.03, -0.102, -0.02), (0.03, -0.1, 0.06), "M_Plastic_Pink", skip=('+y',))
    mb.box((-0.035, -0.115, -0.09), (0.035, -0.1, -0.05), "M_Plastic_Black", skip=('+y',))
    return mb.finish(name, coll)


TOILETS = [
    ("SM_WC_Stall", stall),
    ("SM_WC_Toilet", toilet),
    ("SM_WC_Sink", sink),
    ("SM_WC_Mirror", mirror),
    ("SM_WC_HandDryer", hand_dryer),
    ("SM_WC_SoapDispenser", soap_dispenser),
]


def export_dir_for(name):
    return os.path.join(ART, "Toilets")


def main(export=True):
    return kit["build_kit"](TOILETS, "Toilets_Kit", BLEND_PATH, export_dir_for, (), export)


if __name__ == "__main__":
    main()
