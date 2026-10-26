"""
Bouwt de "controls"-graffiti (uitleg voor nieuwe spelers, buiten op de schoolmuur) procedureel op.

Gebruik (Blender 5.x):
  - blender.exe -b --factory-startup --python blender/tools/build_graffiti_controls.py
  - of Scripting-tab -> Open dit bestand -> Run Script (wist de huidige scene!)

Resultaat:
  - blender/models/props/SM_Deco_Graffiti_Controls.blend   (ontwerp + wandquad)
  - blender/textures/source/T_Graffiti_Controls_BaseColor_4k.png
  - mixed_reality/Assets/Art/Textures/T_Graffiti_Controls_BaseColor.png  (2048 x 1024, RGBA)
  - mixed_reality/Assets/Art/Props/SM_Deco_Graffiti_Controls.fbx

Werking:
  - Het ontwerp (collection "Graffiti_Design") ligt plat in het XY-vlak, 4 x 2 units, en wordt met een
    orthografische camera van bovenaf naar een PNG met alpha gerenderd (EEVEE, emissie = vlakke verfkleuren).
  - De wandquad SM_Deco_Graffiti_Controls (3,4 x 1,7 m) draagt die texture. Conventie wandprop:
    oorsprong onderaan-midden tegen de muur, de quad kijkt naar -Y en ligt 1 cm voor de muur.
  - Aanpassen = dit script wijzigen en opnieuw draaien. In Unity: menu VR School -> Place Controls Graffiti.
"""

import math
import os
import random

import bmesh
import bpy

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BLEND_PATH = os.path.join(REPO, "blender", "models", "props", "SM_Deco_Graffiti_Controls.blend")
SOURCE_TEX = os.path.join(REPO, "blender", "textures", "source", "T_Graffiti_Controls_BaseColor_4k.png")
UNITY_TEX = os.path.join(REPO, "mixed_reality", "Assets", "Art", "Textures", "T_Graffiti_Controls_BaseColor.png")
EXPORT_DIR = os.path.join(REPO, "mixed_reality", "Assets", "Art", "Props")

ASSET = "SM_Deco_Graffiti_Controls"
MAT = "M_Graffiti_Controls"
DESIGN = "Graffiti_Design"

CANVAS_W, CANVAS_H = 4.0, 2.0      # ontwerp in units (2:1 = texture)
QUAD_W, QUAD_H = 3.4, 1.7          # op de muur in meter
WALL_GAP = 0.01

FONT_BLOCK = r"C:\Windows\Fonts\ariblk.ttf"
FONT_HAND = r"C:\Windows\Fonts\Inkfree.ttf"
FONT_SHARP = r"C:\Windows\Fonts\impact.ttf"
FONT_STENCIL = r"C:\Windows\Fonts\STENCIL.TTF"

random.seed(7)


# ----------------------------------------------------------------------------
# Kleuren & materialen
# ----------------------------------------------------------------------------

def lin(hex_rgb):
    """sRGB-hex -> lineaire RGB."""
    hex_rgb = hex_rgb.lstrip("#")
    out = []
    for i in range(3):
        c = int(hex_rgb[i * 2:i * 2 + 2], 16) / 255
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return out


# Grauw en verwassen: geen felle kleuren, alles een beetje vuil.
INK = "#121212"
WHITE = "#bdb8ad"                   # vergeeld, vuil wit
SHADOW = "#1a1a1c"
RUST, DRIED = "#7a2a22", "#3b1512"  # opgedroogd rood
PALE, SLATE = "#8796a3", "#3d4650"  # ziekelijk grijsblauw
ASH, SOOT = "#77746e", "#34332f"
GREY, DARK = "#4a4c50", "#1c1d20"


def paint(top, bottom=None, grain=0.35):
    """Vlakke spuitverf: emissie met verticale gradient (Generated Y) en wat korrel."""
    name = f"P_{top}_{bottom or top}"
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    coord = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    noise = nt.nodes.new("ShaderNodeTexNoise")
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = 'RGBA'
    mix.blend_type = 'MULTIPLY'
    ramp.color_ramp.elements[0].color = (*lin(bottom or top), 1)
    ramp.color_ramp.elements[1].color = (*lin(top), 1)
    noise.inputs["Scale"].default_value = 140.0
    noise.inputs["Detail"].default_value = 8.0
    nt.links.new(coord.outputs["Generated"], sep.inputs[0])
    nt.links.new(sep.outputs["Y"], ramp.inputs["Fac"])
    nt.links.new(coord.outputs["Object"], noise.inputs["Vector"])
    mix.inputs["Factor"].default_value = grain
    nt.links.new(ramp.outputs["Color"], mix.inputs["A"])
    nt.links.new(noise.outputs["Color"], mix.inputs["B"])
    nt.links.new(mix.outputs["Result"], emit.inputs["Color"])
    nt.links.new(emit.outputs[0], out.inputs["Surface"])
    mat.diffuse_color = (*lin(top), 1)
    return mat


def haze(color, alpha=0.45):
    """Zachte overspray-wolk: emissie die radiaal uitloopt naar transparant."""
    name = f"H_{color}_{alpha}"
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.surface_render_method = 'BLENDED'
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Color"].default_value = (*lin(color), 1)
    transp = nt.nodes.new("ShaderNodeBsdfTransparent")
    mixs = nt.nodes.new("ShaderNodeMixShader")
    coord = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Location"].default_value = (-1, -1, 0)
    mapping.inputs["Scale"].default_value = (2, 2, 0)
    grad = nt.nodes.new("ShaderNodeTexGradient")
    grad.gradient_type = 'SPHERICAL'
    power = nt.nodes.new("ShaderNodeMath")
    power.operation = 'POWER'
    power.inputs[1].default_value = 1.8
    mult = nt.nodes.new("ShaderNodeMath")
    mult.operation = 'MULTIPLY'
    mult.inputs[1].default_value = alpha
    nt.links.new(coord.outputs["Generated"], mapping.inputs["Vector"])
    nt.links.new(mapping.outputs["Vector"], grad.inputs["Vector"])
    nt.links.new(grad.outputs["Fac"], power.inputs[0])
    nt.links.new(power.outputs[0], mult.inputs[0])
    nt.links.new(mult.outputs[0], mixs.inputs["Fac"])
    nt.links.new(transp.outputs[0], mixs.inputs[1])
    nt.links.new(emit.outputs[0], mixs.inputs[2])
    nt.links.new(mixs.outputs[0], out.inputs["Surface"])
    return mat


# ----------------------------------------------------------------------------
# Basisvormen (alles plat in XY, Z = tekenvolgorde)
# ----------------------------------------------------------------------------

def link(obj):
    bpy.data.collections[DESIGN].objects.link(obj)
    return obj


def poly(name, pts, mat, z, loc=(0, 0), rot=0.0):
    me = bpy.data.meshes.new(name)
    me.from_pydata([(x, y, 0) for x, y in pts], [], [list(range(len(pts)))])
    me.materials.append(mat)
    obj = link(bpy.data.objects.new(name, me))
    obj.location = (loc[0], loc[1], z)
    obj.rotation_euler.z = rot
    return obj


def ellipse_pts(rx, ry, n=48):
    return [(rx * math.cos(2 * math.pi * i / n), ry * math.sin(2 * math.pi * i / n)) for i in range(n)]


def rrect_pts(w, h, r, n=8):
    pts = []
    for cx, cy, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90),
                       (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def disc(name, c, r, mat, z, sy=1.0):
    return poly(name, ellipse_pts(r, r * sy), mat, z, c)


def stroke(name, pts, width, mat, z, closed=False, caps=True):
    """Lijn met ronde uiteinden: 3D-polycurve met bevel (van bovenaf = vlakke streep)."""
    cu = bpy.data.curves.new(name, 'CURVE')
    cu.dimensions = '3D'
    cu.bevel_depth = width / 2
    cu.bevel_resolution = 4
    sp = cu.splines.new('POLY')
    sp.points.add(len(pts) - 1)
    for p, (x, y) in zip(sp.points, pts):
        p.co = (x, y, 0, 1)
    sp.use_cyclic_u = closed
    cu.materials.append(mat)
    obj = link(bpy.data.objects.new(name, cu))
    obj.location.z = z
    if caps and not closed:
        for i, p in enumerate((pts[0], pts[-1])):
            disc(f"{name}_cap{i}", p, width / 2, mat, z)
    # ronde hoeken
    for i, p in enumerate(pts[1:-1] if not closed else pts):
        disc(f"{name}_joint{i}", p, width / 2, mat, z)
    return obj


def inked_stroke(name, pts, width, fill, z=0.3, ink=0.035, glow=0.012, closed=False):
    """Streep met zwarte contour en witte buitenrand (graffiti-stijl)."""
    stroke(f"{name}_fill", pts, width, fill, z, closed)
    stroke(f"{name}_ink", pts, width + 2 * ink, paint(INK, grain=0), z - 0.1, closed)
    stroke(f"{name}_glow", pts, width + 2 * (ink + glow), paint(WHITE, grain=0.05), z - 0.2, closed)


def inked_poly(name, make_pts, fill, z=0.3, loc=(0, 0), rot=0.0, ink=0.035, glow=0.012):
    """make_pts(grow) -> punten van de vorm, 'grow' units groter (contourlagen)."""
    poly(f"{name}_fill", make_pts(0), fill, z, loc, rot)
    poly(f"{name}_ink", make_pts(ink), paint(INK, grain=0), z - 0.1, loc, rot)
    poly(f"{name}_glow", make_pts(ink + glow), paint(WHITE, grain=0.05), z - 0.2, loc, rot)


# ----------------------------------------------------------------------------
# Graffiti-tekst: elke letter apart, met jitter, overlap en contourlagen
# ----------------------------------------------------------------------------

def text_obj(name, body, font, size, mat, z, offset=0.0):
    cu = bpy.data.curves.new(name, 'FONT')
    cu.body = body
    cu.font = font
    cu.size = size
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    cu.offset = offset
    cu.materials.append(mat)
    obj = link(bpy.data.objects.new(name, cu))
    obj.location.z = z
    return obj


def graffiti_word(name, word, center, height, top, bottom, max_width, tilt=0.0, drips=2,
                  overlap=0.10, jitter=1.0, font_path=FONT_SHARP, cap=0.79, shear=0.18, block=6,
                  edge=ASH):
    """Streetletters: scheef, vulling met gradient, zwarte lijn, dunne rand, massief 3D-blok, lange druppels."""
    font = bpy.data.fonts.load(font_path, check_existing=True)
    size = height / cap                         # cap = kapitaalhoogte in em
    root = link(bpy.data.objects.new(name, None))
    fill = paint(top, bottom)
    ink, cloud, shade = paint(INK, grain=0.1), paint(edge, grain=0.4), paint(SHADOW, grain=0.25)
    o_ink, o_cloud = 0.035 * size, 0.06 * size

    letters, x = [], 0.0
    for i, ch in enumerate(word):
        if ch == " ":
            x += 0.32 * size
            continue
        obj = text_obj(f"{name}_{i}_fill", ch, font, size, fill, 0.30, offset=0.004 * size)
        obj.data.shear = shear
        bpy.context.view_layer.update()
        w = obj.dimensions.x
        letters.append((i, ch, obj, x + w / 2, w))
        x += w * (1 - overlap)
    width = x + letters[-1][4] * overlap
    scale = min(1.0, max_width / width)

    for i, ch, obj, lx, w in letters:
        dy = random.uniform(-0.05, 0.05) * height * jitter
        rot = math.radians(random.uniform(-7, 7) * jitter)
        s = random.uniform(0.94, 1.08) if jitter else 1.0
        layers = [obj,
                  text_obj(f"{name}_{i}_ink", ch, font, size, ink, 0.20, offset=o_ink),
                  text_obj(f"{name}_{i}_cloud", ch, font, size, cloud, 0.10, offset=o_cloud)]
        # massief 3D-blok: trapsgewijze kopieën naar rechtsonder
        for b in range(block):
            layers.append(text_obj(f"{name}_{i}_block{b}", ch, font, size, shade, 0.05 - b * 0.001,
                                   offset=o_cloud))
        for k, layer in enumerate(layers):
            layer.data.shear = shear
            layer.parent = root
            layer.location.x = lx - width / 2
            layer.location.y = dy
            layer.rotation_euler.z = rot
            layer.scale = (s, s, 1)
            if k >= 3:
                step = (k - 2) / block
                layer.location.x += 0.06 * size * step
                layer.location.y -= 0.06 * size * step

    # druppels onder willekeurige letters (in de onderste verfkleur)
    for k, (i, ch, obj, lx, w) in enumerate(random.sample(letters, min(drips, len(letters)))):
        dx = lx - width / 2 + random.uniform(-0.2, 0.2) * w
        y0 = -0.40 * height
        length = random.uniform(0.3, 1.0) * height
        dw = random.uniform(0.03, 0.06) * height
        drip = paint(bottom or top, grain=0)
        for layer, extra, z in ((drip, 0, 0.29), (ink, o_ink * 0.7, 0.19)):
            st = stroke(f"{name}_drip{k}_{z}", [(dx, y0), (dx, y0 - length)], dw + 2 * extra, layer, z, caps=False)
            bulb = disc(f"{name}_drip{k}_bulb_{z}", (dx, y0 - length), dw * 0.7 + extra, layer, z, sy=1.4)
            st.parent = bulb.parent = root

    root.location = (center[0], center[1], 0)
    root.rotation_euler.z = math.radians(tilt)
    root.scale = (scale, scale, 1)
    return root


def tag(name, text, center, size, color, tilt=0.0, font_path=FONT_HAND):
    """Handgeschreven marker-tag (dun lettertype, vet gemaakt) met zwarte rand."""
    font = bpy.data.fonts.load(font_path, check_existing=True)
    root = link(bpy.data.objects.new(name, None))
    for suffix, mat, z, off in (("fill", paint(color, grain=0.35), 0.34, 0.008 * size),
                                ("ink", paint(INK, grain=0.1), 0.27, 0.045 * size)):
        t = text_obj(f"{name}_{suffix}", text, font, size, mat, z, offset=off)
        t.parent = root
    root.location = (center[0], center[1], 0)
    root.rotation_euler.z = math.radians(tilt)
    return root


# ----------------------------------------------------------------------------
# Iconen
# ----------------------------------------------------------------------------

def arrow(name, c, direction, size, color):
    ang = math.atan2(direction[1], direction[0])
    pts = [(-size * 0.55, size * 0.7), (size * 0.25, 0.0), (-size * 0.55, -size * 0.7)]
    ca, sa = math.cos(ang), math.sin(ang)
    pts = [(c[0] + x * ca - y * sa, c[1] + x * sa + y * ca) for x, y in pts]
    inked_stroke(name, pts, size * 0.36, paint(color), z=0.32, ink=0.018, glow=0.0)


def icon_joystick(c):
    x, y = c
    # voet
    inked_poly("Joy_base", lambda g: ellipse_pts(0.30 + g, 0.115 + g), paint(GREY, DARK), z=0.30,
               loc=(x, y - 0.17))
    disc("Joy_base_hole", (x, y - 0.165), 0.085, paint(INK, grain=0.1), 0.31, sy=0.42)
    # stick
    inked_stroke("Joy_stick", [(x, y - 0.16), (x + 0.03, y + 0.07)], 0.075, paint(ASH, GREY), z=0.32)
    # kop
    inked_poly("Joy_cap", lambda g: ellipse_pts(0.135 + g, 0.115 + g), paint(PALE, SLATE), z=0.34,
               loc=(x + 0.035, y + 0.12))
    # pijlen in vier richtingen rond de kop
    cx, cy = x + 0.03, y + 0.06
    for k, (d, off) in enumerate((((1, 0), (0.40, 0)), ((-1, 0), (-0.40, 0)),
                                  ((0, 1), (0, 0.33)), ((0, -1), (0, -0.40)))):
        arrow(f"Joy_arrow{k}", (cx + off[0], cy + off[1]), d, 0.085, RUST)


def icon_trigger(c):
    x, y = c
    # controller van opzij: ring bovenaan, greep schuin naar onder
    ring = [(x - 0.03 + 0.16 * math.cos(a), y + 0.20 + 0.13 * math.sin(a))
            for a in [2 * math.pi * i / 40 for i in range(40)]]
    inked_stroke("Trig_ring", ring, 0.055, paint(ASH, GREY), z=0.30, closed=True)
    inked_stroke("Trig_grip", [(x + 0.03, y + 0.08), (x + 0.13, y - 0.30)], 0.17, paint(GREY, DARK), z=0.31)
    disc("Trig_button", (x + 0.035, y + 0.03), 0.03, paint(INK, grain=0.1), 0.33)
    # de trekker (vooraan) in opgedroogd rood
    inked_stroke("Trig_trigger", [(x - 0.06, y + 0.05), (x - 0.12, y - 0.03), (x - 0.10, y - 0.11)],
                 0.07, paint(RUST, DRIED), z=0.34)
    # "klik"-streepjes
    for k, a in enumerate((150, 185, 220)):
        r0, r1 = 0.20, 0.30
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        inked_stroke(f"Trig_click{k}", [(x - 0.08 + r0 * ca, y - 0.04 + r0 * sa),
                                        (x - 0.08 + r1 * ca, y - 0.04 + r1 * sa)],
                     0.03, paint(WHITE), z=0.33, ink=0.015, glow=0.0)


def icon_phone(c):
    x, y = c
    # X-knop
    inked_poly("Phone_xbtn", lambda g: ellipse_pts(0.13 + g, 0.13 + g), paint(ASH, SOOT), z=0.30,
               loc=(x - 0.28, y + 0.02))
    font = bpy.data.fonts.load(FONT_BLOCK, check_existing=True)
    xt = text_obj("Phone_xbtn_X", "X", font, 0.21, paint(INK, grain=0.1), 0.33, offset=0.004)
    xt.location.x, xt.location.y = x - 0.28, y + 0.02
    # pijl naar de gsm
    arrow("Phone_arrow", (x - 0.06, y + 0.02), (1, 0), 0.07, RUST)
    # gsm, licht gekanteld, gebarsten scherm
    rot = math.radians(-12)
    loc = (x + 0.20, y)
    ca, sa = math.cos(rot), math.sin(rot)

    def at(dx, dy):
        return (loc[0] + dx * ca - dy * sa, loc[1] + dx * sa + dy * ca)

    inked_poly("Phone_body", lambda g: rrect_pts(0.27 + 2 * g, 0.48 + 2 * g, 0.05 + g), paint(DARK, INK),
               z=0.30, loc=loc, rot=rot)
    poly("Phone_screen", rrect_pts(0.22, 0.40, 0.03), paint(SLATE, DARK), 0.31, loc, rot)
    crack = [at(0.06, 0.15), at(0.01, 0.05), at(0.04, -0.02), at(-0.03, -0.09), at(-0.01, -0.17)]
    stroke("Phone_crack", crack, 0.008, paint(PALE, grain=0.1), 0.32, caps=True)
    stroke("Phone_crack2", [at(0.01, 0.05), at(-0.08, 0.08)], 0.006, paint(PALE, grain=0.1), 0.32)


# ----------------------------------------------------------------------------
# Scene opbouwen
# ----------------------------------------------------------------------------

def reset_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for coll in list(bpy.data.collections):
        bpy.data.collections.remove(coll)
    for block in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights,
                  bpy.data.images):
        for item in list(block):
            block.remove(item)
    design = bpy.data.collections.new(DESIGN)
    bpy.context.scene.collection.children.link(design)


def build_design():
    # vuile overspray: roet en een vleug opgedroogd rood
    for k, (cx, cy, r, col, a) in enumerate(((0.0, 0.60, 1.35, SOOT, 0.55), (0.3, 0.55, 0.8, DRIED, 0.35),
                                             (-1.32, -0.15, 0.62, SOOT, 0.45), (0.0, -0.15, 0.62, SOOT, 0.45),
                                             (1.32, -0.15, 0.62, SOOT, 0.45))):
        poly(f"Haze{k}", ellipse_pts(r, r * 0.62), haze(col, a), 0.0, (cx, cy))

    # oude, half weggepoetste tags op de achtergrond (geen contour, lage contrast)
    hand = bpy.data.fonts.load(FONT_HAND, check_existing=True)
    for k, (txt, cx, cy, sz, rot, col) in enumerate((("same day again", -1.50, 0.52, 0.13, 6, ASH),
                                                     ("no one's coming", 1.10, 0.36, 0.11, -5, ASH),
                                                     ("why bother", 0.66, -0.36, 0.09, 12, GREY),
                                                     ("tired", 1.82, 0.55, 0.10, -14, GREY),
                                                     ("2026", -0.55, 0.95, 0.09, 3, GREY))):
        t = text_obj(f"Old_{k}", txt, hand, sz, paint(col, grain=0.5), 0.015, offset=0.004)
        t.location.x, t.location.y = cx, cy
        t.rotation_euler.z = math.radians(rot)

    graffiti_word("Title", "CONTROLS", (0.04, 0.64), 0.40, RUST, DRIED, max_width=3.0, tilt=-1.5, drips=6,
                  overlap=0.0, jitter=0.6)
    tag("Tag_NewHere", "welcome... i guess", (-1.38, 0.86), 0.12, WHITE, tilt=4)
    tag("Tag_Read", "nothing else to do", (1.45, 0.88), 0.10, ASH, tilt=-3)

    icon_joystick((-1.32, -0.05))
    icon_trigger((0.0, -0.05))
    icon_phone((1.30, -0.05))

    for name, word, cx, tilt in (("Label_Move", "MOVE", -1.32, -2), ("Label_Use", "INTERACT", 0.0, 1),
                                 ("Label_Phone", "PHONE", 1.32, -1)):
        graffiti_word(name, word, (cx, -0.60), 0.15, WHITE, ASH, max_width=1.15, tilt=tilt, drips=1,
                      overlap=-0.04, jitter=0.25, font_path=FONT_STENCIL, cap=0.72, shear=0.0, block=3,
                      edge=SOOT)

    tag("Sub_Move", "push the joystick", (-1.32, -0.88), 0.11, WHITE, tilt=-3)
    tag("Sub_Use", "trigger: open doors + use items", (0.0, -0.88), 0.11, WHITE, tilt=1)
    tag("Sub_Phone", "press X for your phone", (1.32, -0.88), 0.11, WHITE, tilt=-2)

    # spetters, sommige met een druppel
    for k in range(90):
        cx, cy = random.uniform(-1.95, 1.95), random.uniform(-0.95, 0.95)
        col = random.choice([SOOT, SOOT, INK, RUST, ASH])
        r = random.uniform(0.003, 0.018)
        disc(f"Splat{k}", (cx, cy), r, paint(col, grain=0.2), 0.02)
        if r > 0.013 and random.random() < 0.35:
            stroke(f"SplatDrip{k}", [(cx, cy), (cx, cy - random.uniform(0.03, 0.09))], r * 0.6,
                   paint(col, grain=0.2), 0.02, caps=True)


def weather(img):
    """Verweerde verf: vlekkerig vervaagd, afgebladderd en wat ontkleurd (numpy op de render)."""
    import numpy as np
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    rng = np.random.default_rng(3)

    def value_noise(cells):
        grid = rng.random((cells // 2 + 2, cells + 2))
        ys = np.linspace(0, grid.shape[0] - 2, h)
        xs = np.linspace(0, grid.shape[1] - 2, w)
        y0, x0 = ys.astype(int), xs.astype(int)
        fy, fx = (ys - y0)[:, None], (xs - x0)[None, :]
        fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
        a = grid[y0][:, x0] * (1 - fx) + grid[y0][:, x0 + 1] * fx
        b = grid[y0 + 1][:, x0] * (1 - fx) + grid[y0 + 1][:, x0 + 1] * fx
        return a * (1 - fy) + b * fy

    fade = 0.5 * value_noise(6) + 0.3 * value_noise(24) + 0.2 * value_noise(96)
    alpha_mul = np.clip(0.25 + fade * 1.1, 0.35, 1.0)
    flakes = value_noise(420) > 0.9                      # kleine afgebladderde plekjes
    alpha_mul[flakes] *= 0.15
    rgb = px[..., :3]
    lum = (rgb * np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)).sum(-1, keepdims=True)
    px[..., :3] = rgb * 0.7 + lum * 0.3                   # 30 % ontkleurd
    px[..., 3] *= alpha_mul.astype(np.float32)
    img.pixels[:] = px.ravel()


def render_texture():
    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("GraffitiCam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = CANVAS_W
    cam = bpy.data.objects.new("GraffitiCam", cam_data)
    bpy.data.collections[DESIGN].objects.link(cam)
    cam.location = (0, 0, 10)
    scene.camera = cam

    for engine in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue
    scene.eevee.taa_render_samples = 32
    scene.render.film_transparent = True
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.resolution_x = 4096
    scene.render.resolution_y = 2048
    scene.render.resolution_percentage = 100

    os.makedirs(os.path.dirname(SOURCE_TEX), exist_ok=True)
    scene.render.filepath = SOURCE_TEX
    bpy.ops.render.render(write_still=True)

    img = bpy.data.images.load(SOURCE_TEX)
    weather(img)
    img.save()
    img.scale(2048, 1024)
    os.makedirs(os.path.dirname(UNITY_TEX), exist_ok=True)
    img.filepath_raw = UNITY_TEX
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def build_quad():
    """Wandquad in het XZ-vlak, kijkt naar -Y, oorsprong onderaan-midden tegen de muur."""
    mat = bpy.data.materials.new(MAT)
    mat.use_nodes = True
    mat.surface_render_method = 'BLENDED'
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 0.85
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(UNITY_TEX)
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    mat.node_tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])

    me = bpy.data.meshes.new(ASSET)
    bm = bmesh.new()
    hw, y = QUAD_W / 2, -WALL_GAP
    verts = [bm.verts.new(v) for v in ((-hw, y, 0), (hw, y, 0), (hw, y, QUAD_H), (-hw, y, QUAD_H))]
    face = bm.faces.new(verts)
    face.normal_update()
    if face.normal.y > 0:
        face.normal_flip()
    me.materials.append(mat)                    # slot vóór to_mesh (Blender 5 klemt anders material_index)
    for uv_name in ("UVMap", "Lightmap"):
        layer = bm.loops.layers.uv.new(uv_name)
        for loop in face.loops:
            v = loop.vert.co
            loop[layer].uv = ((v.x + hw) / QUAD_W, v.z / QUAD_H)
    bm.to_mesh(me)
    bm.free()

    coll = bpy.data.collections.new("Export")
    bpy.context.scene.collection.children.link(coll)
    obj = bpy.data.objects.new(ASSET, me)
    coll.objects.link(obj)
    return obj


def export_fbx(obj):
    os.makedirs(EXPORT_DIR, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(EXPORT_DIR, f"{ASSET}.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={'MESH'},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        use_space_transform=True,
        bake_space_transform=True,
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
        bake_anim=False,
        path_mode='AUTO',
        embed_textures=False,
    )
    print(f"[graffiti] -> {path}")


def main():
    reset_scene()
    build_design()
    render_texture()
    quad = build_quad()
    bpy.data.collections[DESIGN].hide_render = False
    bpy.context.view_layer.layer_collection.children[DESIGN].hide_viewport = False
    export_fbx(quad)
    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.file.make_paths_relative() if bpy.data.filepath else None
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH, relative_remap=True)
    print(f"[graffiti] saved {BLEND_PATH}")


main()
