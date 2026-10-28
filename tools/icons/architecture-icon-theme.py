"""Pictograms of the Architecture tab: isometric solids and flat elevations/plans in one illustration style.

Style rules shared by every icon here:
  * 24 x 24 grid, every solid fitted to the same margin so neighbouring buttons carry the same visual weight;
  * a navy outline (aN) on every shape, lighter "soft" lines (aS) for secondary detail such as brick courses;
  * three-tone shading for solids: light top, mid left face, dark right face, from a warm material palette;
  * the accent blue (aB) is reserved for what the command acts on (planes, openings, selections).

The generator loads this file with runpy and merges PALETTE, PEN_SPECS and SHAPES into the shared icon kit.
COMMAND_PICTOGRAMS says which Architecture command uses which pictogram; badges stay as declared in the generator.
"""

import math

from icon_kit import C, L, P, R

PALETTE = {
    "plasterT": "#F6F0E6", "plasterL": "#E3D8C4", "plasterR": "#C9BA9F",
    "concT": "#EBF0F5", "concL": "#C8D2DC", "concR": "#A3B0BF",
    "steelT": "#CBD6E1", "steelL": "#9FB0C2", "steelR": "#7A8EA3",
    "glass": "#BFE5F8", "glassD": "#8FD0F0", "sky": "#E3F4FD",
    "woodT": "#F4CE9C", "woodL": "#DDA25F", "woodR": "#B97A3C",
    "tileT": "#F6A07A", "tileL": "#E06D47", "tileR": "#B84A2C",
    "greenT": "#C2EACF", "greenL": "#86CFA1", "greenD": "#3FA66A",
    "goldT": "#FFEDB8", "goldL": "#FFD25E", "goldD": "#E0A100",
    "blueT": "#DCEFFC", "blueL": "#A4D2F3", "blueR": "#5FA8E3",
    "aWhite": "#FFFFFF", "aInk": "#1F3B57", "aAccent": "#1E88E5", "aRed": "#E5484D", "hole": "#6F8399",
}

# name -> (brush, thickness, dash pattern or None)
PEN_SPECS = {
    "aN": ("#1F3B57", 1.15, None), "aS": ("#7C93A9", 0.8, None), "aW": ("#FFFFFF", 1.0, None),
    "aB": ("#1E88E5", 1.2, None), "aBB": ("#1E88E5", 1.9, None), "aBD": ("#1E88E5", 1.1, "3 2"),
    "aG": ("#2E8B57", 1.2, None), "aGD": ("#2E8B57", 1.1, "3 2"), "aO": ("#B7791F", 1.2, None),
    "aOD": ("#B7791F", 1.1, "3 2"), "aR": ("#E5484D", 1.9, None), "aRx": ("#D23B3B", 1.3, None),
    "aK": ("#2E8B57", 1.9, None), "aND": ("#1F3B57", 1.0, "2.4 1.8"),
}


def pen_xml():
    """The Pen resources the pictograms reference, as XAML lines."""
    lines = []
    for name, (brush, thickness, dash) in PEN_SPECS.items():
        head = f'  <Pen x:Key="Arch{name[1:]}Pen" Brush="{brush}" Thickness="{thickness}" StartLineCap="Round" EndLineCap="Round" LineJoin="Round"'
        if dash:
            lines.append(f'{head}><Pen.DashStyle><DashStyle Dashes="{dash}"/></Pen.DashStyle></Pen>')
        else:
            lines.append(head + "/>")
    return lines


PEN_NAMES = {name: f"Arch{name[1:]}Pen" for name in PEN_SPECS}


def rnd(value):
    return round(value, 2)


# --------------------------------------------------------------------------------------------- isometric scene

def project(point):
    x, y, z = point
    return ((x - y) * 0.866, (x + y) * 0.5 - z)


class Scene:
    """Collects 3D shapes (x to lower right, y to lower left, z up), then projects and fits them to the 24 grid."""

    def __init__(self):
        self.items = []

    def poly(self, ring, fill, pen="aN"):
        rings = ring if isinstance(ring[0][0], (tuple, list)) else [ring]
        self.items.append(("poly", rings, fill, pen))

    def line(self, *points, pen="aN"):
        self.items.append(("line", [list(points)], None, pen))

    def lines(self, segments, pen="aN"):
        self.items.append(("line", [list(s) for s in segments], None, pen))

    def dot(self, point, radius, fill, pen="aN"):
        self.items.append(("dot", [[point]], fill, (pen, radius)))

    def box(self, x, y, z, w, d, h, top, left, right, pen="aN"):
        """A cuboid: the faces toward the viewer are the y+d face (left), the x+w face (right) and the top."""
        self.poly([(x, y + d, z), (x + w, y + d, z), (x + w, y + d, z + h), (x, y + d, z + h)], left, pen)
        self.poly([(x + w, y, z), (x + w, y + d, z), (x + w, y + d, z + h), (x + w, y, z + h)], right, pen)
        self.poly([(x, y, z + h), (x + w, y, z + h), (x + w, y + d, z + h), (x, y + d, z + h)], top, pen)

    def slab_with_hole(self, x, y, z, w, d, h, hole, top, left, right, inner_left=None, inner_right=None, pen="aN"):
        """A slab pierced by a rectangular hole (hx, hy, hw, hd); the inner faces toward the viewer are shown."""
        hx, hy, hw, hd = hole
        self.poly([(x, y + d, z), (x + w, y + d, z), (x + w, y + d, z + h), (x, y + d, z + h)], left, pen)
        self.poly([(x + w, y, z), (x + w, y + d, z), (x + w, y + d, z + h), (x + w, y, z + h)], right, pen)
        outer = [(x, y, z + h), (x + w, y, z + h), (x + w, y + d, z + h), (x, y + d, z + h)]
        inner = [(hx, hy, z + h), (hx, hy + hd, z + h), (hx + hw, hy + hd, z + h), (hx + hw, hy, z + h)]
        # the inner faces first, so the top ring covers their upper edge
        self.poly([(hx, hy, z), (hx, hy + hd, z), (hx, hy + hd, z + h), (hx, hy, z + h)], inner_right or right, pen)
        self.poly([(hx, hy, z), (hx + hw, hy, z), (hx + hw, hy, z + h), (hx, hy, z + h)], inner_left or left, pen)
        self.poly([outer, inner], top, pen)

    def cylinder(self, cx, cy, z, h, r, top, left, right, pen="aN", steps=14):
        """A vertical cylinder; the side is split into a lit and a shaded half."""
        def arc(a0, a1, level):
            return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / steps)),
                     cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / steps)), level) for i in range(steps + 1)]
        for a0, a1, fill in ((45, 135, left), (-45, 45, right)):
            self.poly(arc(a0, a1, z) + arc(a1, a0, z + h), fill, pen)
        self.poly(arc(0, 360, z + h)[:-1], top, pen)

    def extrude(self, profile, y0, y1, top, side, cap, pen="aN"):
        """Extrudes a profile given in (x, z) along y; only the faces seen from the viewer are drawn."""
        area = sum(profile[i][0] * profile[(i + 1) % len(profile)][1] - profile[(i + 1) % len(profile)][0] * profile[i][1]
                   for i in range(len(profile)))
        edges = []
        for i in range(len(profile)):
            (x1, z1), (x2, z2) = profile[i], profile[(i + 1) % len(profile)]
            dx, dz = x2 - x1, z2 - z1
            nx, nz = (dz, -dx) if area > 0 else (-dz, dx)
            length = math.hypot(nx, nz) or 1
            nx, nz = nx / length, nz / length
            if nz > 0.35:
                fill = top
            elif nx > 0.35:
                fill = side
            else:
                continue
            edges.append(((x1 + x2) / 2, [(x1, y0, z1), (x2, y0, z2), (x2, y1, z2), (x1, y1, z1)], fill))
        for _, quad, fill in sorted(edges, key=lambda e: e[0]):
            self.poly(quad, fill, pen)
        self.poly([(x, y1, z) for x, z in profile], cap, pen)

    def emit(self, margin=1.8, size=24):
        projected = []
        for kind, rings, fill, pen in self.items:
            projected.append((kind, [[project(p) for p in ring] for ring in rings], fill, pen))
        xs = [x for _, rings, _, _ in projected for ring in rings for x, _ in ring]
        ys = [y for _, rings, _, _ in projected for ring in rings for _, y in ring]
        span = size - 2 * margin
        scale = min(span / (max(xs) - min(xs)), span / (max(ys) - min(ys)))
        ox = size / 2 - (min(xs) + max(xs)) / 2 * scale
        oy = size / 2 - (min(ys) + max(ys)) / 2 * scale
        ops = []
        for kind, rings, fill, pen in projected:
            fitted = [[(rnd(x * scale + ox), rnd(y * scale + oy)) for x, y in ring] for ring in rings]
            if kind == "dot":
                (cx, cy), = fitted[0]
                ops.append((C(cx, cy, rnd(pen[1])), fill, pen[0]))
            elif kind == "poly":
                geometry = " ".join(P(*ring) for ring in fitted)
                ops.append((geometry, fill, pen))
            else:
                geometry = " ".join(L(*ring) for ring in fitted)
                ops.append((geometry, None, pen))
        return ops


def hatch_inside(polygon, step=3.0):
    """Diagonal (x + y = const) hatch lines clipped to a convex polygon."""
    pieces = []
    sums = [x + y for x, y in polygon]
    c = math.ceil(min(sums) / step) * step
    while c < max(sums):
        hits = []
        for (x1, y1), (x2, y2) in zip(polygon, polygon[1:] + polygon[:1]):
            d1, d2 = x1 + y1 - c, x2 + y2 - c
            if d1 * d2 < 0:
                t = d1 / (d1 - d2)
                hits.append((rnd(x1 + t * (x2 - x1)), rnd(y1 + t * (y2 - y1))))
        if len(hits) >= 2:
            hits.sort()
            pieces.append(L(hits[0], hits[-1]))
        c += step
    return " ".join(pieces)


def star(cx, cy, outer, inner):
    pts = []
    for i in range(8):
        radius = outer if i % 2 == 0 else inner
        angle = math.radians(-90 + i * 45)
        pts.append((rnd(cx + radius * math.cos(angle)), rnd(cy + radius * math.sin(angle))))
    return P(*pts)


SHAPES = {}


def shape(name):
    def register(function):
        SHAPES[name] = function()
        return function
    return register


def brick_courses(x0, x1, y, z0, height, courses=4, joint=1.6):
    """Soft course and joint lines on the y-plane face of a wall."""
    segments = []
    step = height / courses
    for i in range(1, courses):
        segments.append([(x0, y, z0 + i * step), (x1, y, z0 + i * step)])
    for i in range(courses):
        offset = joint / 2 if i % 2 else 0
        x = x0 + joint - offset if i % 2 else x0 + joint
        while x < x1 - 0.2:
            segments.append([(x, y, z0 + i * step), (x, y, z0 + (i + 1) * step)])
            x += joint
    return segments


# ------------------------------------------------------------------------------------------------ walls

@shape("a_wall")
def _():
    s = Scene()
    s.box(0, 0, 0, 8, 1.6, 4.4, "plasterT", "plasterL", "plasterR")
    s.lines(brick_courses(0, 8, 1.6, 0, 4.4), "aS")
    return s.emit()


@shape("a_wall_struct")
def _():
    s = Scene()
    s.box(0, 0, 0, 8, 1.8, 4.4, "concT", "concL", "concR")
    s.box(6.2, 1.8, 0, 1.8, 4.8, 4.4, "concT", "concL", "concR")
    for x in (1.5, 4.0):
        for z in (1.2, 3.2):
            s.dot((x, 1.8, z), 0.6, "concR", "aS")
    return s.emit()


@shape("a_wall_face")
def _():
    s = Scene()
    s.poly([(0, 0.6, 0), (8, 0.6, 0), (8, 0.6, 6.4), (0, 0.6, 6.4)], "blueT", "aBD")
    s.box(0, 0.6, 0, 8, 1.6, 4.0, "plasterT", "plasterL", "plasterR")
    s.lines(brick_courses(0, 8, 2.2, 0, 4.0, 3, 1.8), "aS")
    return s.emit()


@shape("a_wall_sweep")
def _():
    s = Scene()
    s.box(0, 0, 0, 8, 1.6, 4.4, "plasterT", "plasterL", "plasterR")
    s.box(0, 1.6, 1.5, 8, 0.8, 1.0, "woodT", "woodL", "woodR")
    return s.emit()


@shape("a_wall_reveal")
def _():
    s = Scene()
    s.box(0, 0, 0, 8, 1.6, 4.4, "plasterT", "plasterL", "plasterR")
    s.poly([(0, 1.6, 1.6), (8, 1.6, 1.6), (8, 1.6, 2.6), (0, 1.6, 2.6)], "plasterR", "aN")
    s.line((0, 1.6, 2.6), (8, 1.6, 2.6), pen="aW")
    return s.emit()


# ----------------------------------------------------------------------------------------- doors, windows

@shape("a_door")
def _():
    return [
        (R(5, 2, 14, 20), "woodR", "aN"),
        (R(6.8, 3.8, 10.4, 18.2), "woodL", "aN"),
        (R(8.4, 5.6, 3.2, 6), "woodT", "aS"), (R(12.4, 5.6, 3.2, 6), "woodT", "aS"),
        (R(8.4, 13, 3.2, 6.6), "woodT", "aS"), (R(12.4, 13, 3.2, 6.6), "woodT", "aS"),
        (C(15.4, 12.6, 0.95), "goldL", "aO"),
        (L((2.5, 22), (21.5, 22)), None, "aN"),
    ]


@shape("a_window")
def _():
    return [
        (R(4, 3, 16, 15.5), "aWhite", "aN"),
        (R(5.8, 4.8, 5.6, 11.9), "glass", "aN"), (R(12.6, 4.8, 5.6, 11.9), "glass", "aN"),
        (L((7.2, 13), (9.8, 7.4)) + " " + L((14, 13), (16.6, 7.4)), None, "aW"),
        (R(2.5, 18.5, 19, 2.8), "concL", "aN"),
    ]


@shape("a_chair")
def _():
    s = Scene()
    for lx, ly in ((0.2, 0.2), (3.1, 0.2), (0.2, 3.1), (3.1, 3.1)):
        s.box(lx, ly, 0, 0.7, 0.7, 2.2, "woodT", "woodL", "woodR")
    s.box(0, 0, 2.2, 4, 4, 1.0, "blueT", "blueL", "blueR")
    s.box(0, 0, 3.2, 4, 0.9, 3.4, "blueT", "blueL", "blueR")
    return s.emit()


# --------------------------------------------------------------------------------------------- columns

@shape("a_column")
def _():
    s = Scene()
    s.box(0, 0, 0, 3.6, 3.6, 0.8, "concT", "concL", "concR")
    s.box(0.5, 0.5, 0.8, 2.6, 2.6, 5.2, "concT", "concL", "concR")
    s.box(0, 0, 6.0, 3.6, 3.6, 0.8, "concT", "concL", "concR")
    return s.emit()


@shape("a_column_round")
def _():
    s = Scene()
    s.cylinder(2, 2, 0, 0.7, 2.0, "concT", "concL", "concR")
    s.cylinder(2, 2, 0.7, 5.4, 1.4, "concT", "concL", "concR")
    s.cylinder(2, 2, 6.1, 0.7, 2.0, "concT", "concL", "concR")
    return s.emit()


@shape("a_column_steel")
def _():
    s = Scene()
    s.box(-0.7, -0.7, 0, 4.4, 4.4, 0.5, "steelT", "steelL", "steelR")
    s.box(0.4, 0.4, 0.5, 2.2, 2.2, 6.2, "steelT", "steelL", "steelR")
    for x, y in ((-0.1, -0.1), (3.1, -0.1), (-0.1, 3.1), (3.1, 3.1)):
        s.dot((x, y, 0.5), 0.5, "aWhite", "aS")
    return s.emit()


# ---------------------------------------------------------------------------------------- roofs, floors

@shape("a_roof")
def _():
    s = Scene()
    s.poly([(8, 0, 0), (8, 6, 0), (8, 3, 4.4)], "plasterL", "aN")
    s.poly([(0, 6, -0.7), (8, 6, -0.7), (8, 6, 0), (0, 6, 0)], "tileR", "aN")
    s.poly([(0, 6, 0), (8, 6, 0), (8, 3, 4.4), (0, 3, 4.4)], "tileL", "aN")
    s.lines([[(0, 6 - 3 * i / 3, 4.4 * i / 3), (8, 6 - 3 * i / 3, 4.4 * i / 3)] for i in (1, 2)], "aW")
    return s.emit()


@shape("a_roof_extrusion")
def _():
    s = Scene()
    upper = [(x, 2.2 + 2.8 * math.sin(math.pi * x / 8)) for x in (0, 1, 2, 3, 4, 5, 6, 7, 8)]
    lower = [(x, z - 0.8) for x, z in reversed(upper)]
    s.extrude(upper + lower, 0, 5, "tileT", "tileR", "tileL")
    return s.emit()


@shape("a_roof_soffit")
def _():
    return [
        (R(6, 13, 12, 8.5), "plasterL", "aN"),
        (P((0.8, 13.4), (12, 4.5), (23.2, 13.4)), "tileL", "aN"),
        (R(0.8, 13.4, 5.2, 2.2), "woodL", "aN"), (R(18, 13.4, 5.2, 2.2), "woodL", "aN"),
        (L((2.4, 13.4), (2.4, 15.6)) + " " + L((4.2, 13.4), (4.2, 15.6)) + " " + L((19.8, 13.4), (19.8, 15.6)) + " " + L((21.6, 13.4), (21.6, 15.6)), None, "aS"),
        (R(9.5, 16, 5, 5.5), "glass", "aN"),
    ]


@shape("a_roof_fascia")
def _():
    return [
        (P((5.2, 13.5), (21, 5), (21, 8.6), (5.2, 17.1)), "tileL", "aN"),
        (L((8.2, 11.9), (8.2, 15.4)) + " " + L((12, 9.8), (12, 13.4)) + " " + L((15.8, 7.7), (15.8, 11.3)), None, "aW"),
        (R(2, 13, 3.2, 8), "plasterT", "aN"),
        (L((3.6, 14.4), (3.6, 19.6)), None, "aS"),
        (R(5.2, 17.1, 15.8, 4.4), "plasterL", "aN"),
    ]


@shape("a_gutter")
def _():
    return [
        (P((1.5, 4.5), (22.5, 4.5), (22.5, 7), (1.5, 7)), "tileL", "aN"),
        ("M3,9 H5 V13 Q5,17 12,17 Q19,17 19,13 V9 H21 V13 Q21,19.5 12,19.5 Q3,19.5 3,13 Z", "steelL", "aN"),
        ("M5,13 Q5,17 12,17 Q19,17 19,13 Z", "glassD", None),
        ("M12,8.6 Q14.6,12.2 12,13.8 Q9.4,12.2 12,8.6 Z", "aAccent", "aB"),
    ]


@shape("a_ceiling")
def _():
    s = Scene()
    for x, y in ((0.4, 0.4), (7.6, 0.4), (0.4, 7.6), (7.6, 7.6)):
        s.line((x, y, 0.5), (x, y, 4.2), pen="aS")
    s.box(0, 0, 0, 8, 8, 0.6, "concT", "concL", "concR")
    cells = [[(i * 8 / 3, 0, 0.6), (i * 8 / 3, 8, 0.6)] for i in (1, 2)] + [[(0, i * 8 / 3, 0.6), (8, i * 8 / 3, 0.6)] for i in (1, 2)]
    s.lines(cells, "aS")
    s.poly([(2.8, 2.8, 0.6), (5.2, 2.8, 0.6), (5.2, 5.2, 0.6), (2.8, 5.2, 0.6)], "goldT", "aO")
    return s.emit()


@shape("a_floor")
def _():
    s = Scene()
    s.poly([(0, 8, 0), (8, 8, 0), (8, 8, 0.8), (0, 8, 0.8)], "concL", "aN")
    s.poly([(8, 0, 0), (8, 8, 0), (8, 8, 0.8), (8, 0, 0.8)], "concR", "aN")
    s.poly([(0, 8, 0.8), (8, 8, 0.8), (8, 8, 1.3), (0, 8, 1.3)], "woodL", "aN")
    s.poly([(8, 0, 0.8), (8, 8, 0.8), (8, 8, 1.3), (8, 0, 1.3)], "woodR", "aN")
    s.poly([(0, 0, 1.3), (8, 0, 1.3), (8, 8, 1.3), (0, 8, 1.3)], "woodT", "aN")
    grid = [[(i * 2, 0, 1.3), (i * 2, 8, 1.3)] for i in (1, 2, 3)] + [[(0, i * 2, 1.3), (8, i * 2, 1.3)] for i in (1, 2, 3)]
    s.lines(grid, "aS")
    return s.emit()


@shape("a_slab_edge")
def _():
    s = Scene()
    s.box(0, 0, 0, 8, 6.4, 1.1, "concT", "concL", "concR")
    s.box(0, 6.4, -0.9, 8, 1.2, 2.6, "woodT", "woodL", "woodR")
    return s.emit()


# ----------------------------------------------------------------------------- curtain wall, circulation

@shape("a_curtain")
def _():
    s = Scene()
    s.box(0, 0, 0, 8, 0.6, 7, "steelT", "glass", "steelR")
    s.lines([[(x, 0.6, 0), (x, 0.6, 7)] for x in (2, 4, 6)] + [[(0, 0.6, z), (8, 0.6, z)] for z in (2.33, 4.66)], "aN")
    s.lines([[(0.8, 0.6, 1.0), (1.5, 0.6, 2.2)], [(4.8, 0.6, 4.9), (5.5, 0.6, 6.1)], [(0.8, 0.6, 5.2), (1.5, 0.6, 6.2)]], "aW")
    return s.emit()


@shape("a_curtain_grid")
def _():
    s = Scene()
    s.poly([(0, 0.6, 0), (8, 0.6, 0), (8, 0.6, 7), (0, 0.6, 7)], "blueT", "aB")
    s.lines([[(x, 0.6, 0), (x, 0.6, 7)] for x in (2, 4, 6)] + [[(0, 0.6, z), (8, 0.6, z)] for z in (2.33, 4.66)], "aBD")
    return s.emit()


@shape("a_mullion")
def _():
    s = Scene()
    s.poly([(-3.4, 0.8, 0.4), (3.2, 0.8, 0.4), (3.2, 0.8, 6.6), (-3.4, 0.8, 6.6)], "glass", "aN")
    s.poly([(4.8, 0.8, 0.4), (11.4, 0.8, 0.4), (11.4, 0.8, 6.6), (4.8, 0.8, 6.6)], "glass", "aN")
    s.box(3.2, 0, 0, 1.6, 1.6, 7, "steelT", "steelL", "steelR")
    return s.emit()


@shape("a_railing")
def _():
    s = Scene()
    s.box(0, 0, 0, 7, 0.9, 0.5, "steelT", "steelL", "steelR")
    s.lines([[(x, 0.9, 0.5), (x, 0.9, 3.9)] for x in (1.75, 3.5, 5.25)], "aS")
    for x in (0, 6.3):
        s.box(x, 0, 0.5, 0.7, 0.9, 3.5, "steelT", "steelL", "steelR")
    s.box(0, 0, 4.0, 7, 0.9, 0.6, "woodT", "woodL", "woodR")
    return s.emit()


@shape("a_ramp")
def _():
    s = Scene()
    s.extrude([(0, 0), (8, 0), (0, 3.4)], 0, 5, "concT", "concR", "concL")
    s.line((6.4, 2.5, 0.7), (2.0, 2.5, 2.4), pen="aBB")
    s.line((2.0, 2.5, 2.4), (3.4, 2.5, 2.35), (2.4, 2.5, 1.5), pen="aBB")
    return s.emit()


@shape("a_stair")
def _():
    s = Scene()
    profile = [(0, 0), (8, 0), (8, 0.9), (6, 0.9), (6, 1.8), (4, 1.8), (4, 2.7), (2, 2.7), (2, 3.6), (0, 3.6)]
    s.extrude(profile, 0, 4.5, "concT", "concR", "concL")
    return s.emit()


# ------------------------------------------------------------------------------------- model elements

@shape("a_text_model")
def _():
    s = Scene()
    outer = [(0, 0), (1.4, 0), (1.9, 1.5), (4.1, 1.5), (4.6, 0), (6, 0), (3.9, 7), (2.1, 7)]
    hole = [(2.3, 2.7), (3.7, 2.7), (3, 5.2)]
    back = [[(x, 0, z) for x, z in outer], [(x, 0, z) for x, z in hole]]
    front = [[(x, 1.6, z) for x, z in outer], [(x, 1.6, z) for x, z in hole]]
    s.poly(back, "blueR", "aN")
    s.lines([[(x, 0, z), (x, 1.6, z)] for x, z in outer[1:7]], "aN")
    s.poly(front, "blueL", "aN")
    return s.emit()


@shape("a_model_line")
def _():
    s = Scene()
    s.poly([(0, 0, 0), (8, 0, 0), (8, 8, 0), (0, 8, 0)], "blueT", "aS")
    s.lines([[(i * 8 / 3, 0, 0), (i * 8 / 3, 8, 0)] for i in (1, 2)] + [[(0, i * 8 / 3, 0), (8, i * 8 / 3, 0)] for i in (1, 2)], "aS")
    s.line((1.2, 6.8, 0), (6.8, 1.2, 0), pen="aBB")
    s.dot((1.2, 6.8, 0), 1.25, "aWhite", "aB")
    s.dot((6.8, 1.2, 0), 1.25, "aWhite", "aB")
    return s.emit()


@shape("a_model_group")
def _():
    s = Scene()
    s.poly([(-1, -1, 0), (8.6, -1, 0), (8.6, 8.6, 0), (-1, 8.6, 0)], "aWhite", "aBD")
    s.box(0, 0, 0, 3.4, 3.4, 3.4, "blueT", "blueL", "blueR")
    s.box(4.2, 0, 0, 3.4, 3.4, 3.4, "goldT", "goldL", "goldD")
    s.box(0, 4.2, 0, 3.4, 3.4, 3.4, "greenT", "greenL", "greenD")
    return s.emit()


# ------------------------------------------------------------------------------------ rooms and areas

@shape("a_room")
def _():
    return [
        (R(2, 2, 20, 20), "steelL", "aN"),
        (R(4.4, 4.4, 15.2, 15.2), "greenL", "aN"),
        (P((12, 8.2), (16.2, 12), (12, 15.8), (7.8, 12)), "aWhite", "aG"),
        (L((10.2, 12), (13.8, 12)), None, "aG"),
    ]


@shape("a_room_auto")
def _():
    return [
        (R(2, 2, 20, 20), "steelL", "aN"),
        (R(4.4, 4.4, 15.2, 15.2), "greenL", "aN"),
        (star(12, 12, 6.2, 1.9), "goldL", "aO"),
    ]


@shape("a_separator")
def _():
    return [
        (R(2, 2, 20, 20), "steelL", "aN"),
        (R(4.4, 4.4, 15.2, 7.1), "greenL", "aN"),
        (R(4.4, 12.5, 15.2, 7.1), "greenT", "aN"),
        (L((4.4, 12), (19.6, 12)), None, "aR"),
        (C(4.4, 12, 1.3), "aWhite", "aRx"), (C(19.6, 12, 1.3), "aWhite", "aRx"),
    ]


_AREA = [(3, 8), (11, 3), (21, 7), (19, 19), (6, 20)]


@shape("a_area")
def _():
    return [
        (P(*_AREA), "goldL", "aO"),
        (hatch_inside([(4.2, 8.3), (11, 4.3), (19.8, 7.8), (18, 17.8), (6.6, 18.8)], 3.2), None, "aO"),
    ]


@shape("a_area_boundary")
def _():
    ops = [(P(*_AREA), "goldT", "aOD")]
    for x, y in _AREA:
        ops.append((R(x - 1.3, y - 1.3, 2.6, 2.6), "aWhite", "aO"))
    return ops


@shape("a_tag_all")
def _():
    def tag(dx, dy, fill):
        return [("M{0},{1} L{2},{3} H{4} V{5} L{6},{7} Z".format(3 + dx, 11 + dy, 9.5 + dx, 4.5 + dy, 19 + dx, 12 + dy, 11.5 + dx, 19.5 + dy), fill, "aN"),
                (C(15.6 + dx, 8.6 + dy, 1.3), "aWhite", "aS")]
    return tag(2, 1.5, "goldT") + tag(-1.5, 1.5, "goldL")


# ---------------------------------------------------------------------------------------------- openings

@shape("a_opening_face")
def _():
    s = Scene()
    s.box(0, 0, 0, 8, 1.6, 7, "plasterT", "plasterL", "plasterR")
    # the hole is drawn on the front face; its left jamb and sill run back to the rear face
    x0, x1, z0, z1, d = 2.2, 6.0, 1.8, 5.4, 1.6
    s.poly([(x0, d, z0), (x1, d, z0), (x1, d, z1), (x0, d, z1)], "sky", "aN")
    s.poly([(x0, d, z0), (x0, 0, z0), (x0, 0, z1), (x0, d, z1)], "plasterR", "aN")
    s.poly([(x0, d, z0), (x1, d, z0), (x1, 0, z0), (x0, 0, z0)], "plasterT", "aN")
    return s.emit()


@shape("a_opening_wall")
def _():
    return [
        (R(2, 4, 20, 16), "plasterL", "aN"),
        (L((2, 8), (22, 8)) + " " + L((2, 12), (22, 12)) + " " + L((2, 16), (22, 16)), None, "aS"),
        (R(7.5, 7, 9, 10), "sky", "aN"),
        (L((7.5, 7), (7.5, 17)), None, "aN"),
        (R(6, 17, 12, 2), "concL", "aN"),
    ]


@shape("a_opening_shaft")
def _():
    s = Scene()
    s.box(2.6, 2.6, -5, 3.6, 3.6, 5, "blueT", "blueL", "blueR")
    s.slab_with_hole(0, 0, 0, 8.8, 8.8, 0.9, (2.6, 2.6, 3.6, 3.6), "concT", "concL", "concR", "blueL", "blueR")
    s.line((4.4, 4.4, 1.0), (4.4, 4.4, -4.2), pen="aBB")
    return s.emit()


@shape("a_opening_vertical")
def _():
    s = Scene()
    s.slab_with_hole(0, 0, 0, 8.8, 8.8, 0.9, (2.6, 2.6, 3.6, 3.6), "concT", "concL", "concR", "blueL", "blueR")
    s.lines([[(x, y, 0.9), (x, y, 4.6)] for x, y in ((2.6, 2.6), (6.2, 2.6), (6.2, 6.2), (2.6, 6.2))], "aBD")
    s.poly([(2.6, 2.6, 4.6), (6.2, 2.6, 4.6), (6.2, 6.2, 4.6), (2.6, 6.2, 4.6)], "blueT", "aB")
    return s.emit()


@shape("a_opening_dormer")
def _():
    return [
        (P((1, 21), (12, 5), (23, 21)), "tileL", "aN"),
        (L((4.2, 16.5), (19.8, 16.5)) + " " + L((6.6, 12.8), (17.4, 12.8)), None, "aW"),
        (P((7, 21), (7, 13), (12, 8.6), (17, 13), (17, 21)), "plasterT", "aN"),
        (P((6, 13.4), (12, 8), (18, 13.4)), "tileR", "aN"),
        (R(9.4, 14.2, 5.2, 5.2), "glass", "aN"),
        (L((12, 14.2), (12, 19.4)), None, "aN"),
    ]


# ------------------------------------------------------------------------------------- datums and planes

@shape("a_level")
def _():
    return [
        (R(2, 13, 20, 2), "blueT", None),
        (L((2, 14), (22, 14)), None, "aBD"),
        (P((5, 8.6), (11, 8.6), (8, 13.4)), "aAccent", "aB"),
        (R(13.5, 4, 8, 6), "aWhite", "aB"),
        (L((15, 6), (20, 6)) + " " + L((15, 8), (18.2, 8)), None, "aS"),
        (C(2.6, 14, 1.1), "aWhite", "aB"), (C(21.4, 14, 1.1), "aWhite", "aB"),
    ]


@shape("a_grid")
def _():
    return [
        (L((15, 8.4), (15, 22)), None, "aBD"),
        (L((8.4, 15), (22, 15)), None, "aBD"),
        (C(15, 5, 3.4), "aWhite", "aB"), (L((13.4, 5), (16.6, 5)), None, "aB"),
        (C(5, 15, 3.4), "aWhite", "aB"), (L((3.4, 15), (6.6, 15)), None, "aB"),
    ]


@shape("a_work_plane")
def _():
    s = Scene()
    s.poly([(0, 0, 0), (9, 0, 0), (9, 9, 0), (0, 9, 0)], "blueT", "aB")
    s.lines([[(i * 3, 0, 0), (i * 3, 9, 0)] for i in (1, 2)] + [[(0, i * 3, 0), (9, i * 3, 0)] for i in (1, 2)], "aS")
    s.line((1, 8, 0), (4.4, 8, 0), pen="aR")
    s.line((1, 8, 0), (1, 4.6, 0), pen="aK")
    s.line((1, 8, 0), (1, 8, 3.4), pen="aBB")
    return s.emit()


@shape("a_ref_plane")
def _():
    s = Scene()
    s.poly([(0, 0, 0), (9, 0, 0), (9, 0, 7), (0, 0, 7)], "greenT", "aGD")
    s.line((4.5, 0, -0.8), (4.5, 0, 7.8), pen="aG")
    s.dot((4.5, 0, 3.5), 1.1, "aWhite", "aG")
    return s.emit()


@shape("a_viewer")
def _():
    return [
        ("M1.5,12 Q12,2.5 22.5,12 Q12,21.5 1.5,12 Z", "aWhite", "aN"),
        (C(12, 12, 4.6), "blueL", "aB"),
        (C(12, 12, 2.1), "aInk", None),
        (C(13.1, 10.9, 0.9), "aWhite", None),
    ]


@shape("a_modify")
def _():
    return [
        (R(3.5, 3.5, 17, 17), "blueT", "aBD"),
        (P((7, 5), (7, 19.5), (10.6, 16.2), (13.4, 21.5), (15.8, 20.3), (13, 15), (18, 15)), "aWhite", "aN"),
    ]


COMMAND_PICTOGRAMS = {
    "Modify": "a_modify", "Deselect": "a_modify",
    "Wall": "a_wall", "WallArchitectural": "a_wall", "WallStructural": "a_wall_struct", "WallByFace": "a_wall_face",
    "WallSweep": "a_wall_sweep", "WallReveal": "a_wall_reveal", "ApplyWallType": "a_wall",
    "Door": "a_door", "Window": "a_window",
    "Component": "a_chair", "ComponentPlace": "a_chair", "ComponentInPlace": "a_chair",
    "Column": "a_column", "ColumnArchitectural": "a_column_round", "ColumnStructural": "a_column_steel",
    "Roof": "a_roof", "RoofByFootprint": "a_roof", "RoofByExtrusion": "a_roof_extrusion", "RoofByFace": "a_roof_extrusion",
    "RoofSoffit": "a_roof_soffit", "RoofFascia": "a_roof_fascia", "RoofGutter": "a_gutter", "Gutter": "a_gutter",
    "Ceiling": "a_ceiling", "CeilingAutomatic": "a_ceiling", "CeilingSketch": "a_ceiling",
    "Floor": "a_floor", "FloorArchitectural": "a_floor", "FloorStructural": "a_floor", "FloorByFace": "a_floor", "SlabEdge": "a_slab_edge",
    "CurtainSystem": "a_curtain", "CurtainGrid": "a_curtain_grid", "Mullion": "a_mullion",
    "Railing": "a_railing", "RailingSketch": "a_railing", "RailingOnHost": "a_railing", "Ramp": "a_ramp", "Stair": "a_stair",
    "ModelText": "a_text_model", "ModelLine": "a_model_line", "ModelGroup": "a_model_group", "GroupCreate": "a_model_group", "GroupPlace": "a_model_group",
    "Room": "a_room", "TagRoom": "a_room", "RoomAutomatic": "a_room_auto", "RoomSeparator": "a_separator", "TagAllRooms": "a_tag_all",
    "Area": "a_area", "TagArea": "a_area", "AreaBoundary": "a_area_boundary", "AreaByBoundary": "a_area_boundary", "TagAllAreas": "a_tag_all",
    "FaceOpening": "a_opening_face", "OpeningByFace": "a_opening_face", "ShaftOpening": "a_opening_shaft", "WallOpening": "a_opening_wall",
    "VerticalOpening": "a_opening_vertical", "DormerOpening": "a_opening_dormer",
    "Level": "a_level", "Grid": "a_grid",
    "SetWorkPlane": "a_work_plane", "ShowWorkPlane": "a_work_plane", "WorkPlaneByLevel": "a_work_plane", "WorkPlanePickReference": "a_work_plane",
    "ReferencePlane": "a_ref_plane", "Viewer": "a_viewer",
}
