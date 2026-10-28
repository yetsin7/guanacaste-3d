"""Pictograms of the Structure, Concrete, Steel, Systems and Mass & Site tabs, in the Architecture illustration style.

Same rules as architecture-icon-theme.py (24 grid, navy outline, three-tone solids). Colour carries meaning here:
steel is blue-grey, concrete is light grey, rebar and welds are rust orange, air ducts are blue, pipes are green and
electrical parts are gold. The generator merges SHAPES and COMMAND_PICTOGRAMS exactly like the Architecture theme.
"""

import importlib.util
import math
import os
import re

from icon_kit import C, L, P, R

_here = os.path.dirname(os.path.abspath(globals().get("__file__", "")))
_spec = importlib.util.spec_from_file_location("architecture_icon_theme", os.path.join(_here, "architecture-icon-theme.py"))
_arch = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_arch)
Scene, rnd, star = _arch.Scene, _arch.rnd, _arch.star

PALETTE = {"rust": "#E07B39", "rustD": "#B4531C", "soilT": "#8FD08A", "soilL": "#B98B5E", "soilR": "#946A43"}

EXTRA_PENS = {"aRB": ("#C8601F", 1.9, None), "aRb": ("#C8601F", 1.2, None), "aWeld": ("#F29A1F", 2.0, None)}
PEN_SPECS = EXTRA_PENS


def pen_xml():
    return [f'  <Pen x:Key="Arch{name[1:]}Pen" Brush="{brush}" Thickness="{thickness}" StartLineCap="Round" EndLineCap="Round" LineJoin="Round"/>'
            for name, (brush, thickness, _) in EXTRA_PENS.items()]


PEN_NAMES = {name: f"Arch{name[1:]}Pen" for name in EXTRA_PENS}

# Gradient brushes for glossy pictograms; a fill written "@Key" refers to one of these resources.
BRUSHES = {
    "ArchSphereBody": ('RadialGradientBrush', 'GradientOrigin="0.32,0.28" Center="0.45,0.45" RadiusX="0.62" RadiusY="0.62"',
                       [("#FFFFFF", 0), ("#EAF1F8", 0.12), ("#A9BACB", 0.38), ("#5C7490", 0.68), ("#2C4259", 0.88), ("#14212F", 1)]),
    "ArchSphereGlint": ('RadialGradientBrush', '', [("#FFFFFFFF", 0), ("#00FFFFFF", 1)]),
    "ArchSphereBounce": ('LinearGradientBrush', 'StartPoint="0.5,1" EndPoint="0.5,0"', [("#B0A9D6F5", 0), ("#00A9D6F5", 1)]),
    "ArchContactShadow": ('RadialGradientBrush', '', [("#70102030", 0), ("#00102030", 1)]),
}


def brush_xml():
    lines = []
    for key, (kind, attributes, stops) in BRUSHES.items():
        inner = "".join(f'<GradientStop Color="{color}" Offset="{offset}"/>' for color, offset in stops)
        lines.append(f'  <{kind} x:Key="{key}" {attributes}>{inner}</{kind}>')
    return lines

SHAPES = {}


def shape(name):
    def register(function):
        SHAPES[name] = function()
        return function
    return register


STEEL = ("steelT", "steelL", "steelR")
CONC = ("concT", "concL", "concR")


# ------------------------------------------------------------------------------------------------ helpers

def ibeam(s, x, y, z, length, w=3.4, h=3.0, t=0.8, tw=0.8, tones=STEEL):
    """An I-section member along x: bottom flange, web, top flange."""
    s.box(x, y, z, length, w, t, *tones)
    s.box(x, y + (w - tw) / 2, z + t, length, tw, h, *tones)
    s.box(x, y, z + t + h, length, w, t, *tones)


def hcolumn(s, x, y, z, height, w=3.4, h=3.0, t=0.8, tw=0.8, tones=STEEL):
    """An H-section member standing on z."""
    s.box(x, y, z, w, t, height, *tones)
    s.box(x + (w - tw) / 2, y + t, z, tw, h, height, *tones)
    s.box(x, y + t + h, z, w, t, height, *tones)


def hcylinder(s, x0, x1, y, z, r, top, left, cap, hole="hole", pen="aN", steps=12):
    """A cylinder lying along x, open at the viewer's end."""
    def ring(x, a0, a1):
        return [(x, y + r * math.cos(math.radians(a0 + (a1 - a0) * i / steps)),
                 z + r * math.sin(math.radians(a0 + (a1 - a0) * i / steps))) for i in range(steps + 1)]
    for a0, a1, fill in ((45, 135, top), (-45, 45, left)):
        s.poly(ring(x0, a0, a1) + ring(x1, a1, a0), fill, pen)
    s.poly(ring(x1, 0, 360)[:-1], cap, pen)
    if hole:
        s.poly([(x1, y + r * 0.6 * math.cos(math.radians(a * 30)), z + r * 0.6 * math.sin(math.radians(a * 30))) for a in range(12)], hole, pen)


def prism(s, plan, z, h, top, left, right, pen="aN"):
    """Extrudes a plan polygon (x, y) up by h; only the sides that face the viewer are drawn."""
    area = sum(plan[i][0] * plan[(i + 1) % len(plan)][1] - plan[(i + 1) % len(plan)][0] * plan[i][1] for i in range(len(plan)))
    sides = []
    for i in range(len(plan)):
        (x1, y1), (x2, y2) = plan[i], plan[(i + 1) % len(plan)]
        dx, dy = x2 - x1, y2 - y1
        nx, ny = (dy, -dx) if area > 0 else (-dy, dx)
        if nx + ny <= 0:
            continue
        sides.append(((x1 + x2 + y1 + y2) / 2, [(x1, y1, z), (x2, y2, z), (x2, y2, z + h), (x1, y1, z + h)], left if ny >= nx else right))
    for _, quad, fill in sorted(sides, key=lambda item: item[0]):
        s.poly(quad, fill, pen)
    s.poly([(x, y, z + h) for x, y in plan], top, pen)


def bezier(p0, p1, p2, p3, n=24):
    pts = []
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        pts.append((u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0],
                    u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1]))
    return pts


def band(points, width):
    """The outline of a constant-width ribbon around a centre line, plus its left and right edges."""
    left, right = [], []
    for i, (x, y) in enumerate(points):
        a, b = points[max(i - 1, 0)], points[min(i + 1, len(points) - 1)]
        dx, dy = b[0] - a[0], b[1] - a[1]
        length = math.hypot(dx, dy) or 1
        nx, ny = -dy / length, dx / length
        left.append((rnd(x + nx * width / 2), rnd(y + ny * width / 2)))
        right.append((rnd(x - nx * width / 2), rnd(y - ny * width / 2)))
    return left, right


def ribbon_ops(points, width, fill, rings=0, pen="aN"):
    left, right = band(points, width)
    ops = [(P(*(left + right[::-1])), fill, pen)]
    if rings:
        step = max(1, len(points) // rings)
        rungs = " ".join(L(left[i], right[i]) for i in range(step // 2 + 1, len(points) - 1, step))
        ops.append((rungs, None, "aS"))
    return ops


# ---------------------------------------------------------------------------------------- structure, steel

@shape("a_beam")
def _():
    s = Scene()
    ibeam(s, 0, 0, 0, 9)
    return s.emit()


@shape("a_beam_system")
def _():
    s = Scene()
    for y in (0, 3.6, 7.2):
        ibeam(s, 0, y, 0, 8, w=2.2, h=1.8, t=0.6, tw=0.6)
    return s.emit()


@shape("a_hcolumn")
def _():
    s = Scene()
    s.box(-0.9, -0.9, 0, 5.2, 5.2, 0.5, *STEEL)
    hcolumn(s, 0, 0, 0.5, 7)
    return s.emit()


@shape("a_brace")
def _():
    return [
        (R(3, 3, 3, 18), "steelL", "aN"), (R(18, 3, 3, 18), "steelL", "aN"), (R(3, 3, 18, 3), "steelT", "aN"),
        (L((6, 6), (18, 21)), None, "aBB"), (L((18, 6), (6, 21)), None, "aBD"),
        (C(12, 13.5, 1.2), "aWhite", "aN"), (R(1, 21, 22, 1.8), "concL", "aN"),
    ]


@shape("a_truss")
def _():
    return [
        (R(2, 5, 20, 2.2), "steelL", "aN"), (R(2, 17, 20, 2.2), "steelL", "aN"),
        (L((3.5, 18), (7.5, 6.5), (12, 18), (16.5, 6.5), (20.5, 18)), None, "aBB"),
        (L((7.5, 7), (7.5, 17)) + " " + L((16.5, 7), (16.5, 17)), None, "aS"),
        (C(7.5, 6.1, 1.1), "aWhite", "aN"), (C(16.5, 6.1, 1.1), "aWhite", "aN"),
        (C(3.5, 18.1, 1.1), "aWhite", "aN"), (C(12, 18.1, 1.1), "aWhite", "aN"), (C(20.5, 18.1, 1.1), "aWhite", "aN"),
    ]


@shape("a_foundation")
def _():
    s = Scene()
    s.box(0, 0, 0, 8.4, 8.4, 1.6, *CONC)
    s.box(2.7, 2.7, 1.6, 3, 3, 3.8, *CONC)
    s.box(3.5, 3.5, 5.4, 1.4, 1.4, 1.2, *STEEL)
    return s.emit()


@shape("a_foundation_wall")
def _():
    s = Scene()
    s.box(0, 0, 0, 9, 4, 1.3, *CONC)
    s.box(0, 1.4, 1.3, 9, 1.2, 3.6, "plasterT", "plasterL", "plasterR")
    return s.emit()


@shape("a_foundation_slab")
def _():
    s = Scene()
    s.box(0, 0, 0, 9, 9, 1.7, *CONC)
    mesh = [[(i * 1.5 + 0.75, 0.4, 1.7), (i * 1.5 + 0.75, 8.6, 1.7)] for i in range(6)] + \
           [[(0.4, i * 1.5 + 0.75, 1.7), (8.6, i * 1.5 + 0.75, 1.7)] for i in range(6)]
    s.lines(mesh, "aRb")
    return s.emit()


@shape("a_bolts")
def _():
    s = Scene()
    s.box(0, 0, 0, 9, 9, 1.6, "concT", "steelT", "steelL")
    for x, y in ((2.4, 2.4), (6.6, 2.4), (2.4, 6.6), (6.6, 6.6)):
        s.cylinder(x, y, 1.6, 1.2, 1.2, "aWhite", "steelT", "steelL")
        s.line((x - 0.6, y, 2.8), (x + 0.6, y, 2.8), pen="aS")
    return s.emit()


@shape("a_welds")
def _():
    s = Scene()
    s.box(0, 0, 0, 9, 8, 0.9, *STEEL)
    s.box(0, 3.4, 0.9, 9, 1.0, 5, *STEEL)
    bead = [(i * 0.75, 4.4, 0.9 + (0.9 if i % 2 else 0.1)) for i in range(13)]
    s.line(*bead, pen="aWeld")
    return s.emit()


@shape("a_plate")
def _():
    s = Scene()
    s.box(0, 0, 0, 9, 9, 1.6, "concT", "steelT", "steelL")
    for x, y in ((2.2, 2.2), (6.8, 2.2), (2.2, 6.8), (6.8, 6.8)):
        s.dot((x, y, 1.6), 1.0, "aWhite", "aN")
    return s.emit()


@shape("a_corner_cut")
def _():
    s = Scene()
    prism(s, [(0, 0), (9, 0), (9, 5.4), (5.4, 9), (0, 9)], 0, 1.6, "steelT", "steelL", "steelR")
    s.line((9, 5.4, 1.6), (5.4, 9, 1.6), pen="aRx")
    return s.emit()


@shape("a_cope")
def _():
    s = Scene()
    s.extrude([(0, 0), (9, 0), (9, 1.8), (6.4, 1.8), (6.4, 3.8), (0, 3.8)], 0, 3.2, *("steelT", "steelR", "steelL"))
    return s.emit()


@shape("a_miter")
def _():
    s = Scene()
    s.extrude([(0, 0), (9, 0), (9, 2.6), (2.6, 2.6), (2.6, 8), (0, 8)], 0, 2.6, "steelT", "steelR", "steelL")
    s.line((0, 2.6, 0), (2.6, 2.6, 2.6), pen="aBB")
    return s.emit()


@shape("a_shorten")
def _():
    s = Scene()
    s.box(0, 0, 0, 9, 3, 3, "aWhite", "aWhite", "aWhite", "aBD")
    s.box(0, 0, 0, 5.8, 3, 3, *STEEL)
    return s.emit()


@shape("a_cut_through")
def _():
    s = Scene()
    s.slab_with_hole(0, 0, 0, 9, 6, 3, (3.3, 1.6, 2.4, 2.8), "steelT", "steelL", "steelR", "blueL", "blueR")
    return s.emit()


@shape("a_cut_by")
def _():
    s = Scene()
    s.extrude([(0, 0), (9, 0), (9, 3.6), (5.6, 3.6), (5.6, 1.6), (3.6, 1.6), (3.6, 3.6), (0, 3.6)], 0, 3.4, "steelT", "steelR", "steelL")
    s.box(3.6, 0, 1.6, 2, 3.4, 2, "aWhite", "aWhite", "aWhite", "aBD")
    return s.emit()


@shape("a_stiffener")
def _():
    s = Scene()
    s.box(0, 0, 0, 9, 7, 0.9, *STEEL)
    s.extrude([(0.9, 0.9), (7.4, 0.9), (0.9, 6.6)], 2.8, 3.8, "steelT", "steelR", "steelL")
    return s.emit()


# ------------------------------------------------------------------------------------------------ concrete

@shape("a_rebar")
def _():
    s = Scene()
    for x in (0.8, 3.6, 6.4):
        s.line((x, 0, 0), (x, 3.2, 0), (x, 3.2, 3.2), (x, 0, 3.2), (x, 0, 0), pen="aRb")
    for y, z in ((0, 0), (3.2, 0), (0, 3.2), (3.2, 3.2)):
        s.line((-0.4, y, z), (7.4, y, z), pen="aRB")
    return s.emit()


@shape("a_rebar_path")
def _():
    s = Scene()
    s.line((0, 0, 0), (0, 0, 5), (4, 0, 5), (4, 4, 5), pen="aRB")
    s.dot((0, 0, 0), 1.0, "rust", "aRb")
    s.dot((4, 4, 5), 1.0, "rust", "aRb")
    s.line((-1.5, 0, 0), (1.5, 0, 0), pen="aS")
    return s.emit()


@shape("a_rebar_area")
def _():
    s = Scene()
    s.poly([(0, 0, 0), (9, 0, 0), (9, 9, 0), (0, 9, 0)], "concT", "aN")
    s.lines([[(i * 1.8 + 0.9, 0.5, 0), (i * 1.8 + 0.9, 8.5, 0)] for i in range(5)], "aRB")
    s.lines([[(0.5, 1.8 * i + 0.9, 0), (8.5, 1.8 * i + 0.9, 0)] for i in range(5)], "aRb")
    return s.emit()


@shape("a_fabric")
def _():
    s = Scene()
    s.poly([(0, 0, 0), (9, 0, 0), (9, 9, 0), (0, 9, 0)], "aWhite", "aRb")
    s.lines([[(i * 1.5, 0, 0), (i * 1.5, 9, 0)] for i in range(1, 6)] + [[(0, i * 1.5, 0), (9, i * 1.5, 0)] for i in range(1, 6)], "aRb")
    return s.emit()


@shape("a_fabric_skew")
def _():
    s = Scene()
    s.poly([(0, 0, 0), (9, 0, 0), (11, 9, 0), (2, 9, 0)], "aWhite", "aRb")
    s.lines([[(i * 1.5 + 0.33 * i, 0, 0), (i * 1.5 + 2 + 0.33 * i, 9, 0)] for i in range(1, 6)] +
            [[(i * 1.5 * 0.22, i * 1.5, 0), (9 + i * 1.5 * 0.22, i * 1.5, 0)] for i in range(1, 6)], "aRb")
    return s.emit()


@shape("a_cover")
def _():
    s = Scene()
    s.box(0, 0, 0, 9, 9, 4, *CONC)
    s.poly([(1.6, 1.6, 4), (7.4, 1.6, 4), (7.4, 7.4, 4), (1.6, 7.4, 4)], None, "aBD")
    for x, y in ((1.6, 1.6), (7.4, 1.6), (7.4, 7.4), (1.6, 7.4)):
        s.dot((x, y, 4), 0.9, "rust", "aRb")
    return s.emit()


@shape("a_precast")
def _():
    s = Scene()
    s.box(0, 0, 0, 9, 1.8, 6.4, *CONC)
    for x in (2, 7):
        s.line((x - 0.7, 0.9, 6.4), (x - 0.7, 0.9, 7.8), (x + 0.7, 0.9, 7.8), (x + 0.7, 0.9, 6.4), pen="aRB")
    s.poly([(1.6, 1.8, 1.4), (7.4, 1.8, 1.4), (7.4, 1.8, 5), (1.6, 1.8, 5)], "concT", "aS")
    return s.emit()


# --------------------------------------------------------------------------------------------- systems

@shape("a_duct")
def _():
    s = Scene()
    s.box(0, 0, 0, 9, 3.6, 3.6, "blueT", "blueL", "blueR")
    s.box(-0.1, -0.3, -0.3, 0.9, 4.2, 4.2, "steelT", "steelL", "steelR")
    s.box(8.2, -0.3, -0.3, 0.9, 4.2, 4.2, "steelT", "steelL", "steelR")
    return s.emit()


@shape("a_duct_placeholder")
def _():
    s = Scene()
    s.box(0, 0, 0, 9, 3.6, 3.6, None, None, None, "aBD")
    s.line((0, 3.6, 1.8), (9, 3.6, 1.8), pen="aBD")
    return s.emit()


@shape("a_flex_duct")
def _():
    return ribbon_ops(bezier((2.5, 17), (6, 2), (14, 22), (21.5, 7)), 5.2, "blueL", rings=9)


@shape("a_air_terminal")
def _():
    s = Scene()
    s.box(0, 0, 0, 9, 9, 0.7, "aWhite", "steelL", "steelR")
    for inset in (1.2, 2.6, 4.0):
        s.poly([(inset, inset, 0.7), (9 - inset, inset, 0.7), (9 - inset, 9 - inset, 0.7), (inset, 9 - inset, 0.7)], "blueT" if inset == 2.6 else None, "aS")
    s.poly([(3.9, 3.9, 0.7), (5.1, 3.9, 0.7), (5.1, 5.1, 0.7), (3.9, 5.1, 0.7)], "blueL", "aN")
    return s.emit()


@shape("a_pipe")
def _():
    s = Scene()
    hcylinder(s, 0, 8.6, 1.8, 1.8, 1.8, "greenT", "greenL", "greenD")
    hcylinder(s, 0, 0.9, 1.8, 1.8, 2.5, "steelT", "steelL", "steelR", hole=None)
    hcylinder(s, 7.7, 8.6, 1.8, 1.8, 2.5, "steelT", "steelL", "steelR", hole="hole")
    return s.emit()


@shape("a_flex_pipe")
def _():
    return ribbon_ops(bezier((2.5, 7), (8, 22), (14, 2), (21.5, 17)), 4.8, "greenL", rings=9)


@shape("a_fixture")
def _():
    s = Scene()
    s.slab_with_hole(0, 0, 0, 9, 7, 1.6, (1.8, 1.4, 5.4, 4.2), "aWhite", "concL", "concR", "blueL", "blueR")
    s.cylinder(4.5, 0.7, 1.6, 2.6, 0.45, "steelT", "steelL", "steelR")
    s.line((4.5, 0.7, 4.2), (4.5, 2.3, 4.2), pen="aN")
    return s.emit()


@shape("a_wire")
def _():
    pts = bezier((3, 19), (3, 4), (21, 20), (21, 5))
    ops = ribbon_ops(pts, 2.4, "goldL")
    ops += [(C(3, 19, 1.7), "aWhite", "aN"), (C(21, 5, 1.7), "aWhite", "aN")]
    return ops


@shape("a_cable_tray")
def _():
    s = Scene()
    s.box(0, 0, 0, 9, 0.7, 2, "goldT", "goldL", "goldD")
    for x in (1.4, 3.2, 5.0, 6.8, 8.4):
        s.box(x, 0.7, 0, 0.6, 3.6, 0.5, "goldT", "goldL", "goldD")
    s.box(0, 4.3, 0, 9, 0.7, 2, "goldT", "goldL", "goldD")
    return s.emit()


@shape("a_conduit")
def _():
    s = Scene()
    hcylinder(s, 0, 6.4, 6.4, 1.2, 1.2, "goldT", "goldL", "goldD")
    s.cylinder(6.4, 6.4 - 0.0, 0, 5.4, 1.2, "goldT", "goldL", "goldD")
    return s.emit()


@shape("a_panel")
def _():
    s = Scene()
    s.box(0, 0, 0, 6, 2.2, 8.6, *STEEL)
    face = 2.2
    s.poly([(0.7, face, 0.7), (5.3, face, 0.7), (5.3, face, 7.9), (0.7, face, 7.9)], "aWhite", "aN")
    for row in range(4):
        z = 1.2 + row * 1.7
        s.poly([(1.2, face, z), (2.7, face, z), (2.7, face, z + 1.1), (1.2, face, z + 1.1)], "goldL", "aN")
        s.poly([(3.3, face, z), (4.8, face, z), (4.8, face, z + 1.1), (3.3, face, z + 1.1)], "goldL", "aN")
    return s.emit()


@shape("a_luminaire")
def _():
    return [
        (L((12, 1.5), (12, 4)), None, "aN"),
        (C(12, 10, 5.4), "goldT", "aO"),
        (R(9.4, 15.2, 5.2, 2.6), "steelL", "aN"), (R(10.2, 17.8, 3.6, 1.6), "steelR", "aN"),
        (L((6.6, 8.6), (6.6, 11)) + " " + L((17.4, 8.6), (17.4, 11)) + " " + L((9.8, 6.4), (11.4, 6.4)), None, "aS"),
        (L((3, 19.5), (5, 17.8)) + " " + L((21, 19.5), (19, 17.8)) + " " + L((12, 22.5), (12, 20.4)), None, "aO"),
    ]


@shape("a_outlet")
def _():
    return [
        (R(6, 3, 12, 18), "aWhite", "aN"),
        (R(8.4, 6.4, 7.2, 4.4), "concL", "aN"), (L((10.4, 7.6), (10.4, 9.6)) + " " + L((13.6, 7.6), (13.6, 9.6)), None, "aN"),
        (R(8.4, 13.2, 7.2, 4.4), "concL", "aN"), (L((10.4, 14.4), (10.4, 16.4)) + " " + L((13.6, 14.4), (13.6, 16.4)), None, "aN"),
        (C(12, 12, 0.5), "aInk", None),
    ]


@shape("a_switch")
def _():
    return [
        (R(6.5, 3, 11, 18), "aWhite", "aN"),
        (R(9, 6.4, 6, 11.2), "concL", "aN"),
        (R(9.6, 6.9, 4.8, 5.4), "goldL", "aO"),
    ]


@shape("a_mech_unit")
def _():
    return [
        (R(3, 5, 17, 14), "steelL", "aN"), (R(20, 8.5, 3, 7), "blueL", "aN"),
        (C(10, 12, 4.8), "aWhite", "aN"),
        (L((10, 7.6), (10, 16.4)) + " " + L((5.6, 12), (14.4, 12)), None, "aB"),
        (C(10, 12, 1.0), "aAccent", None),
        (R(4.6, 6.6, 1.6, 1.0), "concT", "aS"), (R(3, 19, 17, 2), "concL", "aN"),
    ]


@shape("a_control")
def _():
    return [
        (R(5, 3.5, 14, 17), "aWhite", "aN"),
        (C(12, 10.5, 4.8), "blueL", "aB"),
        (L((12, 10.5), (14.2, 8.3)), None, "aN"),
        (R(8, 16.6, 8, 2.4), "goldT", "aO"),
    ]


@shape("a_routing")
def _():
    return [
        (L((3.5, 18), (3.5, 8), (12, 8), (12, 16), (20.5, 16), (20.5, 6)), None, "aBB"),
        (C(3.5, 18, 2), "aWhite", "aB"), (C(12, 8, 2), "aWhite", "aB"), (C(12, 16, 2), "aWhite", "aB"), (C(20.5, 6, 2), "aAccent", "aB"),
    ]


@shape("a_fab_part")
def _():
    left, right = band(bezier((3, 19), (3, 11), (9, 5), (19, 5), 16), 5.6)
    return [
        (P(*(left + right[::-1])), "steelL", "aN"),
        (R(1.4, 19, 5.2, 2.4), "steelT", "aN"), (R(18.5, 2.4, 2.4, 5.2), "steelT", "aN"),
        (L((7, 8.2), (7, 11.4)), None, "aS"),
    ]


@shape("a_pid")
def _():
    return [
        (L((2, 17), (22, 17)), None, "aN"),
        (P((6, 13), (6, 21), (12, 17)) + " " + P((18, 13), (18, 21), (12, 17)), "greenL", "aN"),
        (L((12, 17), (12, 9)), None, "aN"),
        (C(12, 6.6, 4), "aWhite", "aB"), (L((9.4, 6.6), (14.6, 6.6)), None, "aB"),
    ]


# --------------------------------------------------------------------------------------------- mass, site

@shape("a_mass")
def _():
    s = Scene()
    s.box(0, 0, 0, 7, 7, 7, "blueT", "blueL", "blueR")
    s.poly([(0, 0, 0), (7, 0, 0), (7, 7, 0), (0, 7, 0)], None, "aBD")
    return s.emit()


@shape("a_mass_inplace")
def _():
    s = Scene()
    prism(s, [(0, 0), (8, 0), (8, 3.6), (3.6, 3.6), (3.6, 8), (0, 8)], 0, 6, "blueT", "blueL", "blueR")
    return s.emit()


@shape("a_toposolid")
def _():
    s = Scene()
    top = [(i, 3.0 + 1.0 * math.sin(i * 0.8) + 0.6 * math.cos(i * 0.45)) for i in range(0, 10)]
    s.extrude([(0, 0), (9, 0)] + list(reversed(top)), 0, 8, "soilT", "soilR", "soilL")
    return s.emit()


@shape("a_topography")
def _():
    return [
        (R(2, 2, 20, 20), "soilT", "aG"),
        ("M5,13 Q6,6 12,6 Q19,6 19,12 Q19,18 12,18 Q6,18 5,13 Z", "greenL", "aG"),
        ("M8.5,12.5 Q9,9.4 12,9.4 Q15.4,9.4 15.4,12 Q15.4,15 12,15 Q9,15 8.5,12.5 Z", "greenT", "aG"),
        (C(12, 12.2, 1.2), "aWhite", "aG"),
    ]


@shape("a_tree")
def _():
    return [
        (R(10.6, 14, 2.8, 8), "soilL", "aN"),
        (C(12, 9.4, 6.4), "greenL", "aN"), (C(7.4, 12.2, 3.6), "greenL", "aN"), (C(16.6, 12.2, 3.6), "greenL", "aN"),
        (C(12, 8.4, 4.6), "greenT", None),
    ]


@shape("a_parking")
def _():
    return [
        (R(3, 3, 18, 18), "aAccent", "aN"),
        ("M9,18.4 V6 H13.4 Q17.4,6 17.4,10.2 Q17.4,14.4 13.4,14.4 H11.6 V18.4 Z M11.6,8.6 V11.8 H13 Q14.8,11.8 14.8,10.2 Q14.8,8.6 13,8.6 Z", "aWhite", None),
    ]


@shape("a_property_line")
def _():
    pts = [(3, 7), (14, 3), (21, 9), (18, 20), (5, 19)]
    ops = [(P(*pts), "greenT", "aND")]
    for x, y in pts:
        ops.append((C(x, y, 1.4), "aRed", "aN"))
    return ops


@shape("a_contours")
def _():
    return [
        ("M3,16 Q4,6 12,6 Q21,6 21,13 Q21,20 12,20 Q4,20 3,16 Z", "greenT", "aG"),
        ("M7,15 Q8,10 12,10 Q17,10 17,14 Q17,17 12,17 Q8,17 7,15 Z", "greenL", "aG"),
        (R(12.5, 2.5, 9, 5), "aWhite", "aN"), (L((14, 5), (20, 5)), None, "aS"),
    ]


# ---------------------------------------------------------------------------------- "3D" and the render sphere

@shape("a_text3d")
def _():
    three = ("M1.8,15.2 L4.8,14.6 Q5.2,16.6 6.8,16.6 Q8.7,16.6 8.7,14.6 Q8.7,12.6 6.5,12.6 H5.4 V10.2 H6.3 "
             "Q8.1,10.2 8.1,8.6 Q8.1,7.4 6.7,7.4 Q5.4,7.4 5.1,8.8 L2.3,8.2 Q3,5 6.8,5 Q11,5 11,8.4 Q11,10.8 8.9,11.4 "
             "Q11.8,12.1 11.8,15 Q11.8,19.2 6.8,19.2 Q2.4,19.2 1.8,15.2 Z")
    d = "M12.8,5 H16.8 Q22.4,5 22.4,12.1 Q22.4,19.2 16.8,19.2 H12.8 Z M15.7,7.6 V16.6 H16.5 Q19.5,16.6 19.5,12.1 Q19.5,7.6 16.5,7.6 Z"

    def shift_q(path, dx, dy):
        pieces = re.findall(r"[MLQHVZ][^MLQHVZ]*", path)
        result = []
        for piece in pieces:
            letter, numbers = piece[0], [float(n) for n in re.findall(r"-?\d+\.?\d*", piece[1:])]
            if letter == "Z":
                result.append("Z")
            elif letter == "H":
                result.append(f"H{rnd(numbers[0] + dx)}")
            elif letter == "V":
                result.append(f"V{rnd(numbers[0] + dy)}")
            else:
                coords = [rnd(n + (dx if i % 2 == 0 else dy)) for i, n in enumerate(numbers)]
                result.append(letter + " ".join(f"{coords[i]},{coords[i + 1]}" for i in range(0, len(coords), 2)))
        return " ".join(result)
    return [
        (shift_q(three, 0.9, 1.0) + " " + shift_q(d, 0.9, 1.0), "blueL", None),
        (three + " " + d, "aInk", None),
    ]


@shape("a_render_sphere")
def _():
    return [
        ("M5,21 A7,1.7 0 1 0 19,21 A7,1.7 0 1 0 5,21 Z", "@ArchContactShadow", None),
        (C(12, 11.4, 8.8), "@ArchSphereBody", None),
        ("M4.8,15.4 Q12,23.2 19.2,15.4 Q16,19.8 12,19.9 Q8,19.8 4.8,15.4 Z", "@ArchSphereBounce", None),
        ("M3.4,12.2 Q12,10 20.6,12.2 L20.5,13.4 Q12,11.4 3.5,13.4 Z", "@ArchSphereGlint", None),
        ("M5.8,9 A3.8,2.3 -42 1 0 10.6,5.2 A3.8,2.3 -42 1 0 5.8,9 Z", "@ArchSphereGlint", None),
        ("M7.0,7.6 A1.9,1.05 -42 1 0 9.9,5.5 A1.9,1.05 -42 1 0 7.0,7.6 Z", "aWhite", None),
    ]


COMMAND_PICTOGRAMS = {
    "Model3D": "a_text3d", "ExportModel3D": "a_text3d", "Render": "a_render_sphere", "RenderView": "a_render_sphere",
    "Beam": "a_beam", "AnalyticalMember": "a_beam", "BeamSystem": "a_beam_system", "StructColumn": "a_hcolumn",
    "Brace": "a_brace", "Truss": "a_truss",
    "Connection": "a_bolts", "ConnectionAuto": "a_bolts", "Bolts": "a_bolts", "Welds": "a_welds", "Plate": "a_plate",
    "IsolatedFoundation": "a_foundation", "WallFoundation": "a_foundation_wall", "FoundationSlab": "a_foundation_slab",
    "CornerCut": "a_corner_cut", "ParametricCope": "a_cope", "Cope": "a_cope", "Miter": "a_miter", "Shorten": "a_shorten",
    "CutThrough": "a_cut_through", "CutBy": "a_cut_by", "ContourCut": "a_cut_by", "DuctStiffener": "a_stiffener",
    "Rebar": "a_rebar", "ReinforcementSettings": "a_rebar", "RebarPath": "a_rebar_path", "RebarArea": "a_rebar_area",
    "FabricSheet": "a_fabric", "FabricArea": "a_fabric", "CustomFabricSheet": "a_fabric", "TransformedFabric": "a_fabric_skew",
    "Cover": "a_cover", "CoverSettings": "a_cover", "PrecastReinforcement": "a_precast", "PrecastConfiguration": "a_precast",
    "Duct": "a_duct", "DuctPlaceholder": "a_duct_placeholder", "FlexDuct": "a_flex_duct", "AirTerminal": "a_air_terminal",
    "Pipe": "a_pipe", "ConnectPlumbing": "a_pipe", "FlexPipe": "a_flex_pipe", "PlumbingFixture": "a_fixture", "PlumbingService": "a_fixture",
    "Wire": "a_wire", "CableTray": "a_cable_tray", "Conduit": "a_conduit", "ElectricalEquipment": "a_panel",
    "LightingFixture": "a_luminaire", "ElectricalDevice": "a_outlet", "LightSwitch": "a_switch",
    "MechanicalEquipment": "a_mech_unit", "MechanicalControlDevice": "a_control", "MultipointRouting": "a_routing",
    "FabricationPart": "a_fab_part", "PidModeler": "a_pid",
    "ShowMass": "a_mass", "PlaceMass": "a_mass", "InPlaceMass": "a_mass_inplace", "Toposolid": "a_toposolid",
    "ContextualData": "a_topography", "SiteComponent": "a_tree", "ParkingComponent": "a_parking",
    "PropertyLine": "a_property_line", "LabelContours": "a_contours",
}
