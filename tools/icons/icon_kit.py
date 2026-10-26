"""Drawing kit for the command icons: a 24 x 24 grid, one ink pen, one accent pen and soft fills.

Every base pictogram is a list of (geometry, fill, pen) operations. A badge is a small mark drawn in the lower right
corner, so two commands that share a pictogram (Move and Copy, say) still look different at 16 pixels.
"""

import math

FILLS = {
    "blue": "#D9F0F6", "orange": "#FFE2BF", "green": "#D9F2E4", "purple": "#E9E1F7",
    "yellow": "#FFE8A3", "red": "#FADDE0", "slate": "#E8EDF1", "white": "#FFFFFF",
    "ink": "#263746", "accent": "#007C9F", "redsolid": "#C83F49", "greensolid": "#2E8B57", "orangesolid": "#D9822B",
}
PENS = {"I": "ProfessionalIconPen", "A": "ProfessionalAccentPen", "R": "ProfessionalDangerPen", "G": "CmdGreenPen", "O": "CmdOrangePen"}


def R(x, y, w, h):
    return f"M{x},{y} H{x + w} V{y + h} H{x} Z"


def C(cx, cy, r):
    return f"M{cx - r},{cy} A{r},{r} 0 1 0 {cx + r},{cy} A{r},{r} 0 1 0 {cx - r},{cy} Z"


def P(*pts):
    head, *rest = pts
    return f"M{head[0]},{head[1]} " + " ".join(f"L{x},{y}" for x, y in rest) + " Z"


def L(*pts):
    head, *rest = pts
    return f"M{head[0]},{head[1]} " + " ".join(f"L{x},{y}" for x, y in rest)


def arrow_head(x, y, dx, dy, size=2.6):
    """A small open arrow head at (x, y) pointing along (dx, dy)."""
    n = math.hypot(dx, dy) or 1
    dx, dy = dx / n, dy / n
    px, py = -dy, dx
    a = (x - dx * size + px * size * 0.7, y - dy * size + py * size * 0.7)
    b = (x - dx * size - px * size * 0.7, y - dy * size - py * size * 0.7)
    return f"M{a[0]:.2f},{a[1]:.2f} L{x},{y} L{b[0]:.2f},{b[1]:.2f}"


def arc(cx, cy, r, a0, a1):
    """An arc from angle a0 to a1 (degrees, 0 = east, counter-clockwise on screen is negative)."""
    x0, y0 = cx + r * math.cos(math.radians(a0)), cy + r * math.sin(math.radians(a0))
    x1, y1 = cx + r * math.cos(math.radians(a1)), cy + r * math.sin(math.radians(a1))
    large = 1 if abs(a1 - a0) > 180 else 0
    sweep = 1 if a1 > a0 else 0
    return f"M{x0:.2f},{y0:.2f} A{r},{r} 0 {large} {sweep} {x1:.2f},{y1:.2f}"


def hatch(x0, y0, x1, y1, step=3.5):
    """Diagonal hatch lines clipped by construction to the box (x0, y0)-(x1, y1)."""
    segments = []
    h = y1 - y0
    x = x0 - h
    while x < x1:
        sx, sy, ex, ey = x, y1, x + h, y0
        if sx < x0:
            sy -= (x0 - sx)
            sx = x0
        if ex > x1:
            ey += (ex - x1)
            ex = x1
        if sx < ex:
            segments.append(f"M{sx:.2f},{sy:.2f} L{ex:.2f},{ey:.2f}")
        x += step
    return " ".join(segments)


def dots(points, r=1.0):
    return " ".join(C(x, y, r) for x, y in points)


# --------------------------------------------------------------------------------------------- pictograms

BASES = {}


def base(name):
    def register(function):
        BASES[name] = function()
        return function
    return register


@base("wall")
def _():
    return [(R(2, 7, 20, 10), "orange", "I"), (hatch(2, 7, 22, 17, 4), None, "A")]


@base("wall_face")
def _():
    return [(R(2, 12, 20, 8), "orange", "I"), (L((4, 12), (4, 6), (10, 6)), None, "A"), (R(8, 3, 5, 3), "blue", "A"),
            (L((2, 16), (22, 16)), None, "A")]


@base("wall_sweep")
def _():
    return [(R(4, 4, 8, 16), "orange", "I"), (P((12, 8), (20, 8), (20, 20), (12, 20)), "slate", "I"), (L((12, 12), (20, 12)), None, "A")]


@base("wall_corner")
def _():
    return [(P((3, 4), (21, 4), (21, 9), (8, 9), (8, 20), (3, 20)), "orange", "I"), (hatch(3, 4, 21, 9, 4), None, "A")]


@base("door")
def _():
    return [(R(2, 15, 6, 4), "slate", "I"), (R(16, 15, 6, 4), "slate", "I"), (L((8, 17), (8, 4)), None, "A"),
            (arc(8, 17, 13, -90, -10), None, "A")]


@base("window")
def _():
    return [(R(2, 9, 7, 6), "slate", "I"), (R(15, 9, 7, 6), "slate", "I"), (R(9, 9, 6, 6), "blue", "A"), (L((9, 12), (15, 12)), None, "A")]


@base("window_el")
def _():
    return [(R(4, 3, 16, 18), "blue", "I"), (L((12, 3), (12, 21)), None, "A"), (L((4, 12), (20, 12)), None, "A")]


@base("column")
def _():
    return [(R(5, 5, 14, 14), "slate", "I"), (L((5, 5), (19, 19)), None, "A"), (L((19, 5), (5, 19)), None, "A")]


@base("column_round")
def _():
    return [(C(12, 12, 8), "slate", "I"), (C(12, 12, 2.2), "accent", None)]


@base("beam")
def _():
    return [(P((3, 6), (21, 6), (21, 9), (14, 9), (14, 15), (21, 15), (21, 18), (3, 18), (3, 15), (10, 15), (10, 9), (3, 9)), "blue", "I")]


@base("brace")
def _():
    return [(R(3, 3, 18, 18), None, "I"), (L((3, 21), (21, 3)), None, "A"), (dots([(3, 21), (21, 3)], 1.4), "accent", None)]


@base("truss")
def _():
    return [(L((2, 18), (22, 18), (12, 6), (2, 18)), None, "I"), (L((7, 18), (12, 6), (17, 18)), None, "A"), (L((7, 18), (9.5, 12)), None, "A"),
            (dots([(2, 18), (22, 18), (12, 6)], 1.3), "ink", None)]


@base("beam_system")
def _():
    return [(R(3, 4, 18, 4), "blue", "I"), (R(3, 10, 18, 4), "blue", "I"), (R(3, 16, 18, 4), "blue", "I"), (L((12, 2), (12, 22)), None, "A")]


@base("floor")
def _():
    return [(P((2, 13), (12, 8), (22, 13), (12, 18)), "slate", "I"), (P((2, 13), (12, 18), (22, 13), (22, 16), (12, 21), (2, 16)), "orange", "I"),
            (L((12, 18), (12, 21)), None, "I")]


@base("slab_edge")
def _():
    return [(P((3, 10), (12, 6), (21, 10), (12, 14)), "slate", "I"), (P((3, 10), (12, 14), (12, 18), (3, 14)), "orange", "I"),
            (P((12, 14), (21, 10), (21, 14), (12, 18)), "orange", "A")]


@base("roof")
def _():
    return [(P((2, 14), (12, 5), (22, 14), (19, 14), (12, 8), (5, 14)), "red", "I"), (R(6, 14, 12, 7), "slate", "I"), (R(10, 16, 4, 5), "blue", "A")]


@base("roof_extrusion")
def _():
    return [(L((3, 17), (8, 7), (13, 17)), None, "I"), (P((8, 7), (13, 17), (21, 17), (16, 7)), "red", "I"), (L((13, 17), (16, 7)), None, "A")]


@base("roof_soffit")
def _():
    return [(P((2, 9), (12, 4), (22, 9)), "red", "I"), (R(4, 12, 16, 3), "orange", "I"), (L((4, 18), (20, 18)), None, "A"), (L((4, 20), (20, 20)), None, "A")]


@base("roof_fascia")
def _():
    return [(P((2, 8), (12, 4), (22, 8), (22, 12), (12, 8), (2, 12)), "red", "I"), (R(2, 14, 20, 3), "slate", "I")]


@base("gutter")
def _():
    return [(P((3, 9), (21, 9), (19, 15), (5, 15)), "blue", "I"), (L((6, 18), (18, 18)), None, "A"), (L((9, 12), (15, 12)), None, "A")]


@base("downspout")
def _():
    return [(R(9, 3, 6, 14), "slate", "I"), (L((12, 17), (12, 21)), None, "A"), (arrow_head(12, 21.5, 0, 1), None, "A")]


@base("ceiling")
def _():
    return [(R(3, 5, 18, 14), "slate", "I"), (L((3, 9.7), (21, 9.7)), None, "A"), (L((3, 14.3), (21, 14.3)), None, "A"), (L((9, 5), (9, 19)), None, "A"),
            (L((15, 5), (15, 19)), None, "A")]


@base("foundation")
def _():
    return [(R(8, 3, 8, 9), "slate", "I"), (P((4, 21), (20, 21), (17, 12), (7, 12)), "orange", "I"), (L((12, 12), (12, 21)), None, "A")]


@base("foundation_wall")
def _():
    return [(R(8, 3, 8, 11), "orange", "I"), (R(3, 14, 18, 6), "slate", "I"), (hatch(3, 14, 21, 20, 4), None, "A")]


@base("foundation_slab")
def _():
    return [(R(2, 9, 20, 7), "slate", "I"), (hatch(2, 9, 22, 16, 4), None, "A"), (L((2, 19), (22, 19)), None, "A"), (L((4, 21), (20, 21)), None, "A")]


@base("stair")
def _():
    return [(P((3, 20), (3, 16), (8, 16), (8, 12), (13, 12), (13, 8), (18, 8), (18, 4), (21, 4), (21, 20)), "slate", "I"),
            (arrow_head(15, 14, 1, -1), None, "A"), (L((8, 19), (15, 14)), None, "A")]


@base("ramp")
def _():
    return [(P((3, 19), (21, 8), (21, 19)), "slate", "I"), (L((6, 17), (17, 11)), None, "A"), (arrow_head(17, 11, 2, -1), None, "A")]


@base("railing")
def _():
    return [(L((2, 7), (22, 7)), None, "I"), (L((2, 12), (22, 12)), None, "A"), (L((4, 7), (4, 20)), None, "I"), (L((12, 7), (12, 20)), None, "I"),
            (L((20, 7), (20, 20)), None, "I"), (L((8, 7), (8, 20)), None, "A"), (L((16, 7), (16, 20)), None, "A")]


@base("curtain")
def _():
    return [(R(3, 3, 18, 18), "blue", "I"), (L((9, 3), (9, 21)), None, "A"), (L((15, 3), (15, 21)), None, "A"), (L((3, 9), (21, 9)), None, "A"),
            (L((3, 15), (21, 15)), None, "A")]


@base("mullion")
def _():
    return [(R(10, 2, 4, 20), "slate", "I"), (R(3, 7, 7, 10), "blue", "A"), (R(14, 7, 7, 10), "blue", "A")]


@base("room")
def _():
    return [(R(3, 3, 18, 18), "green", "I"), (R(8, 9, 8, 6), "white", "A"), (L((10, 12), (14, 12)), None, "A")]


@base("room_auto")
def _():
    return [(R(3, 3, 18, 18), "green", "I"), (L((12, 7), (12, 17)), None, "A"), (L((7, 12), (17, 12)), None, "A")]


@base("separator")
def _():
    return [(R(3, 3, 18, 18), "slate", "I"), (L((12, 3), (12, 21)), None, "R"), (dots([(12, 8), (12, 16)], 1.2), "redsolid", None)]


@base("area")
def _():
    return [(P((3, 8), (11, 3), (21, 7), (19, 19), (6, 20)), "yellow", "I"), (hatch(5, 6, 19, 19, 4), None, "O")]


@base("area_boundary")
def _():
    return [(P((3, 8), (11, 3), (21, 7), (19, 19), (6, 20)), None, "O"), (dots([(3, 8), (11, 3), (21, 7), (19, 19), (6, 20)], 1.4), "orangesolid", None)]


@base("space")
def _():
    return [(R(3, 3, 18, 18), "purple", "I"), (C(12, 12, 4), "white", "A"), (L((12, 9), (12, 12), (14, 13.5)), None, "A")]


@base("zone")
def _():
    return [(R(3, 3, 10, 10), "purple", "I"), (R(11, 11, 10, 10), "blue", "I")]


@base("level")
def _():
    return [(L((2, 14), (22, 14)), None, "A"), (P((6, 14), (10, 14), (8, 10.5)), "accent", None), (L((11, 14), (11, 14)), None, "A"),
            (R(14, 4, 7, 6), "blue", "A"), (L((17.5, 10), (17.5, 14)), None, "A")]


@base("grid")
def _():
    return [(L((12, 9), (12, 22)), None, "A"), (C(12, 6, 4), "white", "A"), (L((10, 6), (14, 6)), None, "A"), (L((4, 14), (20, 14)), None, "I")]


@base("ref_plane")
def _():
    return [(P((3, 17), (8, 6), (21, 6), (16, 17)), "blue", "A"), (L((6, 12), (19, 12)), None, "A")]


@base("work_plane")
def _():
    return [(P((2, 18), (7, 8), (22, 8), (17, 18)), "slate", "I"), (L((4.5, 13), (19.5, 13)), None, "A"), (L((12, 8), (9.5, 18)), None, "A"),
            (L((17, 8), (14.5, 18)), None, "A")]


@base("viewer")
def _():
    return [(P((2, 12), (12, 6), (22, 12), (12, 18)), "blue", "I"), (C(12, 12, 3), "white", "A"), (C(12, 12, 1.2), "accent", None)]


@base("text")
def _():
    return [(R(3, 4, 18, 16), "white", "I"), (L((8, 17), (12, 7), (16, 17)), None, "A"), (L((9.5, 13.5), (14.5, 13.5)), None, "A")]


@base("text_model")
def _():
    return [(P((4, 8), (14, 4), (21, 7), (21, 17), (11, 21), (4, 17)), "slate", "I"), (L((9, 16), (12, 9), (15, 16)), None, "A")]


@base("line")
def _():
    return [(L((4, 19), (20, 5)), None, "I"), (dots([(4, 19), (20, 5)], 2.2), "accent", None)]


@base("model_line")
def _():
    return [(L((4, 19), (20, 5)), None, "A"), (P((2, 21), (22, 21), (19, 15), (5, 15)), "slate", "I")]


@base("group")
def _():
    return [(R(3, 3, 18, 18), None, "A"), (R(6, 6, 6, 6), "orange", "I"), (R(12, 12, 6, 6), "blue", "I"), (L((12, 9), (15, 9), (15, 12)), None, "A")]


@base("assembly")
def _():
    return [(R(3, 3, 12, 12), "slate", "I"), (R(9, 9, 12, 12), "blue", "I"), (L((9, 9), (15, 15)), None, "A")]


@base("dimension")
def _():
    return [(L((4, 8), (4, 18)), None, "I"), (L((20, 8), (20, 18)), None, "I"), (L((4, 14), (20, 14)), None, "A"), (arrow_head(4, 14, -1, 0), None, "A"),
            (arrow_head(20, 14, 1, 0), None, "A"), (R(9, 8, 6, 4), "white", "A")]


@base("dimension_aligned")
def _():
    return [(L((4, 17), (10, 5)), None, "I"), (L((14, 21), (20, 9)), None, "I"), (L((6, 13), (18, 19)), None, "A"), (arrow_head(6, 13, -2, -1), None, "A"),
            (arrow_head(18, 19, 2, 1), None, "A")]


@base("dimension_angular")
def _():
    return [(L((4, 19), (20, 19)), None, "I"), (L((4, 19), (16, 5)), None, "I"), (arc(4, 19, 9, -50, 0), None, "A"), (arrow_head(13, 19, 1, 0), None, "A")]


@base("dimension_radial")
def _():
    return [(C(12, 12, 8), None, "I"), (L((12, 12), (18, 6)), None, "A"), (arrow_head(18, 6, 1, -1), None, "A"), (C(12, 12, 1.2), "ink", None)]


@base("dimension_diameter")
def _():
    return [(C(12, 12, 8), None, "I"), (L((5, 17), (19, 7)), None, "A"), (arrow_head(5, 17, -1.4, 1), None, "A"), (arrow_head(19, 7, 1.4, -1), None, "A")]


@base("dimension_arc")
def _():
    return [(arc(12, 20, 15, -130, -50), None, "I"), (arc(12, 20, 11, -128, -52), None, "A"), (arrow_head(5.6, 11.2, -1, 1), None, "A")]


@base("spot_elevation")
def _():
    return [(L((3, 16), (21, 16)), None, "I"), (P((12, 16), (8, 10), (16, 10)), "accent", None), (L((16, 10), (21, 10)), None, "A")]


@base("spot_coordinate")
def _():
    return [(L((12, 3), (12, 21)), None, "I"), (L((3, 12), (21, 12)), None, "I"), (C(12, 12, 3.2), "white", "A"), (R(15, 3, 6, 4), "blue", "A")]


@base("spot_slope")
def _():
    return [(P((3, 19), (21, 19), (21, 8)), "slate", "I"), (L((12, 15), (16, 12)), None, "A"), (R(4, 4, 8, 5), "blue", "A")]


@base("tag")
def _():
    return [(P((3, 12), (12, 3), (21, 3), (21, 12), (12, 21)), "yellow", "I"), (C(16.5, 7.5, 1.6), "white", "I")]


@base("tag_all")
def _():
    return [(P((2, 11), (8, 5), (15, 5), (15, 12), (8, 18)), "yellow", "I"), (P((9, 14), (15, 8), (22, 8), (22, 15), (15, 21)), "yellow", "I")]


@base("keynote")
def _():
    return [(P((3, 6), (21, 6), (21, 16), (13, 16), (9, 20), (9, 16), (3, 16)), "yellow", "I"), (L((7, 9), (17, 9)), None, "A"), (L((7, 12), (14, 12)), None, "A")]


@base("symbol")
def _():
    return [(P((12, 2), (14.6, 9), (22, 9.4), (16.2, 14), (18.2, 21), (12, 17), (5.8, 21), (7.8, 14), (2, 9.4), (9.4, 9)), "yellow", "I")]


@base("detail_line")
def _():
    return [(L((3, 17), (21, 7)), None, "A"), (L((3, 21), (21, 21)), None, "I"), (L((3, 21), (3, 17)), None, "I")]


@base("filled_region")
def _():
    return [(P((3, 6), (18, 3), (21, 14), (8, 20)), "green", "I"), (hatch(5, 5, 20, 19, 3.2), None, "G")]


@base("detail_component")
def _():
    return [(R(4, 6, 16, 12), "slate", "I"), (L((8, 6), (8, 18)), None, "A"), (L((12, 6), (12, 18)), None, "A"), (L((16, 6), (16, 18)), None, "A")]


@base("revision_cloud")
def _():
    return [("M6,17 A3,3 0 0 1 4,12 A3.2,3.2 0 0 1 7,8 A4,4 0 0 1 13,5 A4,4 0 0 1 19,8 A3.2,3.2 0 0 1 20,14 A3,3 0 0 1 17,17 Z", "red", "R")]


@base("insulation")
def _():
    return [(R(3, 7, 18, 10), "yellow", "I"), (L((3, 12), (5.5, 8), (8, 16), (10.5, 8), (13, 16), (15.5, 8), (18, 16), (21, 12)), None, "O")]


@base("legend")
def _():
    return [(R(3, 3, 18, 18), "white", "I"), (R(5.5, 6, 4, 3), "red", "R"), (R(5.5, 10.5, 4, 3), "green", "G"), (R(5.5, 15, 4, 3), "blue", "A"),
            (L((12, 7.5), (19, 7.5)), None, "I"), (L((12, 12), (19, 12)), None, "I"), (L((12, 16.5), (19, 16.5)), None, "I")]


@base("view_plan")
def _():
    return [(R(3, 3, 18, 18), "white", "I"), (R(3, 3, 18, 18), None, "I"), (L((3, 11), (10, 11), (10, 3)), None, "A"), (L((14, 21), (14, 14), (21, 14)), None, "A"),
            (arc(10, 11, 4, 0, 90), None, "A")]


@base("view_elevation")
def _():
    return [(P((3, 21), (3, 11), (12, 4), (21, 11), (21, 21)), "orange", "I"), (R(9, 13, 6, 8), "white", "A"), (L((1.5, 21), (22.5, 21)), None, "A")]


@base("view_section")
def _():
    return [(R(4, 6, 16, 14), "slate", "I"), (L((2, 12), (22, 12)), None, "R"), (arrow_head(22, 12, 1, 0), None, "R"), (L((12, 6), (12, 3)), None, "R"),
            (arrow_head(12, 3, 0, -1), None, "R")]


@base("cube")
def _():
    return [(P((12, 2), (21, 7), (12, 12), (3, 7)), "blue", "I"), (P((3, 7), (12, 12), (12, 22), (3, 17)), "slate", "I"),
            (P((12, 12), (21, 7), (21, 17), (12, 22)), "orange", "I")]


@base("cube_plus")
def _():
    return [(P((12, 2), (20, 6.5), (12, 11), (4, 6.5)), "blue", "I"), (P((4, 6.5), (12, 11), (12, 20), (4, 15.5)), "slate", "I"),
            (P((12, 11), (20, 6.5), (20, 15.5), (12, 20)), "orange", "I")]


@base("camera")
def _():
    return [(R(3, 7, 18, 12), "slate", "I"), (P((8, 7), (9.5, 4), (14.5, 4), (16, 7)), "slate", "I"), (C(12, 13, 3.6), "blue", "A"), (C(12, 13, 1.3), "accent", None)]


@base("walkthrough")
def _():
    return [(R(2, 4, 11, 8), "slate", "I"), (C(7.5, 8, 2.2), "blue", "A"), ("M5,21 C8,15 12,19 15,15 S20,15 21,13", None, "A"), (arrow_head(21, 13, 1, -0.4), None, "A")]


@base("callout")
def _():
    return [(R(3, 3, 12, 12), "white", "I"), (R(9, 9, 12, 12), "blue", "A"), (L((9, 9), (15, 9)), None, "A")]


@base("schedule")
def _():
    return [(R(3, 3, 18, 18), "white", "I"), (R(3, 3, 18, 4.5), "blue", "I"), (L((3, 12), (21, 12)), None, "A"), (L((3, 16.5), (21, 16.5)), None, "A"),
            (L((9.5, 3), (9.5, 21)), None, "A")]


@base("sheet")
def _():
    return [(P((5, 2), (15, 2), (19, 6), (19, 22), (5, 22)), "white", "I"), (L((15, 2), (15, 6), (19, 6)), None, "I"), (R(8, 10, 8, 7), "blue", "A"),
            (L((8, 19), (16, 19)), None, "A")]


@base("render")
def _():
    return [(C(12, 12, 9), "orange", "I"), ("M6,10 A7,7 0 0 1 12,5", None, "A"), (C(9, 9, 1.6), "white", None), (dots([(19, 4), (21, 7.5), (17, 2.5)], 0.6), "ink", None)]


@base("render_cloud")
def _():
    return [("M6,17 A4,4 0 0 1 6.6,9 A5.5,5.5 0 0 1 17.2,8.2 A4.3,4.3 0 0 1 17.6,17 Z", "blue", "I"), (C(12, 13, 2.8), "orange", "I")]


@base("realtime")
def _():
    return [(R(2, 4, 20, 13), "slate", "I"), (P((10, 7.5), (16, 10.5), (10, 13.5)), "accent", None), (L((8, 21), (16, 21)), None, "I"), (L((12, 17), (12, 21)), None, "I")]


@base("sun")
def _():
    return [(C(12, 12, 4.4), "yellow", "I"), (L((12, 2), (12, 5)) + " " + L((12, 19), (12, 22)) + " " + L((2, 12), (5, 12)) + " " + L((19, 12), (22, 12)) + " " +
                                              L((5, 5), (7, 7)) + " " + L((17, 17), (19, 19)) + " " + L((19, 5), (17, 7)) + " " + L((7, 17), (5, 19)), None, "O")]


@base("sun_path")
def _():
    return [(arc(12, 20, 15, -150, -30), None, "O"), (C(12, 6, 3), "yellow", "O"), (L((2, 21), (22, 21)), None, "I")]


@base("shadows")
def _():
    return [(R(4, 6, 8, 12), "slate", "I"), (P((12, 18), (20, 18), (16, 14), (12, 14)), "ink", None), (C(18, 6, 2.6), "yellow", "O")]


@base("visual_style")
def _():
    return [(C(12, 12, 9), "white", "I"), ("M12,3 A9,9 0 0 1 12,21 Z", "slate", None), (L((12, 3), (12, 21)), None, "A")]


@base("orbit")
def _():
    return [(C(12, 12, 3.2), "blue", "A"), (arc(12, 12, 9, -160, 100), None, "A"), (arrow_head(12, 21, -1, 0.1), None, "A")]


@base("pan")
def _():
    return [(L((12, 3), (12, 21)), None, "I"), (L((3, 12), (21, 12)), None, "I"), (arrow_head(12, 3, 0, -1), None, "A"), (arrow_head(12, 21, 0, 1), None, "A"),
            (arrow_head(3, 12, -1, 0), None, "A"), (arrow_head(21, 12, 1, 0), None, "A")]


@base("zoom")
def _():
    return [(C(10, 10, 6.5), "blue", "I"), (L((15, 15), (21, 21)), None, "I"), (L((7, 10), (13, 10)), None, "A"), (L((10, 7), (10, 13)), None, "A")]


@base("view_cube")
def _():
    return [(P((12, 3), (20, 7.5), (12, 12), (4, 7.5)), "white", "I"), (P((4, 7.5), (12, 12), (12, 21), (4, 16.5)), "slate", "I"),
            (P((12, 12), (20, 7.5), (20, 16.5), (12, 21)), "blue", "I"), (L((7, 8), (12, 10.5), (17, 8)), None, "A")]


@base("perspective")
def _():
    return [(P((3, 19), (21, 19), (16, 8), (8, 8)), "slate", "I"), (L((12, 8), (12, 19)), None, "A"), (L((3, 19), (8, 8)), None, "A"), (L((21, 19), (16, 8)), None, "A")]


@base("orthographic")
def _():
    return [(R(5, 5, 14, 14), "slate", "I"), (L((5, 5), (8, 2)), None, "A"), (L((19, 5), (22, 2)), None, "A"), (L((8, 2), (22, 2), (22, 16), (19, 19)), None, "A")]


@base("section_box")
def _():
    return [(P((12, 2), (20, 6), (12, 10), (4, 6)), "white", "A"), (P((4, 6), (12, 10), (12, 21), (4, 17)), "slate", "A"), (P((12, 10), (20, 6), (20, 17), (12, 21)), "blue", "A"),
            (L((2, 12), (22, 12)), None, "R")]


@base("lock_view")
def _():
    return [(P((12, 3), (20, 7.5), (12, 12), (4, 7.5)), "blue", "I"), (R(8, 14, 8, 7), "yellow", "I"), ("M9.5,14 V12 A2.5,2.5 0 0 1 14.5,12 V14", None, "I")]


@base("reset_camera")
def _():
    return [(R(3, 7, 15, 11), "slate", "I"), (C(10.5, 12.5, 3), "blue", "A"), (arc(18, 7, 4, 180, 40), None, "A"), (arrow_head(21.6, 8.6, 0.2, 1), None, "A")]


@base("view_reference")
def _():
    return [(R(3, 5, 14, 14), "white", "I"), (R(9, 10, 12, 11), "blue", "A"), (P((9, 10), (14, 10), (11.5, 13)), "accent", None)]


@base("matchline")
def _():
    return [(R(2, 4, 8, 16), "slate", "I"), (R(14, 4, 8, 16), "slate", "I"), (L((12, 2), (12, 22)), None, "R"), (dots([(12, 5), (12, 12), (12, 19)], 0.9), "redsolid", None)]


@base("guide_grid")
def _():
    return [(R(3, 3, 18, 18), "white", "I"), (L((9, 3), (9, 21)) + " " + L((15, 3), (15, 21)) + " " + L((3, 9), (21, 9)) + " " + L((3, 15), (21, 15)), None, "A")]


@base("windows_switch")
def _():
    return [(R(3, 3, 12, 10), "slate", "I"), (R(9, 11, 12, 10), "blue", "I"), (L((3, 6), (15, 6)), None, "A"), (L((9, 14), (21, 14)), None, "A")]


@base("windows_close")
def _():
    return [(R(3, 3, 12, 10), "slate", "I"), (R(9, 11, 12, 10), "slate", "I"), (L((13, 15), (17, 19)) + " " + L((17, 15), (13, 19)), None, "R")]


@base("windows_tab")
def _():
    return [(R(3, 8, 18, 13), "white", "I"), (P((3, 8), (3, 4), (10, 4), (12, 8)), "blue", "I"), (P((12, 8), (14, 5), (20, 5), (21, 8)), "slate", "I")]


@base("windows_tile")
def _():
    return [(R(3, 3, 8, 8), "blue", "I"), (R(13, 3, 8, 8), "slate", "I"), (R(3, 13, 8, 8), "slate", "I"), (R(13, 13, 8, 8), "blue", "I")]


@base("view_templates")
def _():
    return [(R(3, 3, 18, 18), "white", "I"), (R(3, 3, 18, 5), "purple", "I"), (R(6, 11, 5, 7), "blue", "A"), (L((14, 12), (19, 12)) + " " + L((14, 15), (19, 15)) + " " + L((14, 18), (17, 18)), None, "I")]


@base("visibility")
def _():
    return [("M2,12 C6,5 18,5 22,12 C18,19 6,19 2,12 Z", "white", "I"), (C(12, 12, 3.5), "blue", "A"), (C(12, 12, 1.4), "ink", None)]


@base("filter")
def _():
    return [(P((3, 4), (21, 4), (14, 13), (14, 20), (10, 18), (10, 13)), "blue", "I")]


@base("thin_lines")
def _():
    return [(L((3, 6), (21, 6)), None, "I"), (L((3, 12), (21, 12)), None, "A"), (L((3, 18), (21, 18)), None, "I"), (arrow_head(21, 12, 1, 0), None, "A")]


@base("hidden_lines")
def _():
    return [(R(4, 4, 12, 12), None, "I"), ("M8,8 H20 V20 H8 Z", None, "A"), (L((16, 8), (20, 8)), None, "R")]


@base("cut_profile")
def _():
    return [(R(4, 4, 16, 16), "slate", "I"), (L((4, 12), (20, 12)), None, "R"), (R(8, 8, 8, 4), "orange", "I")]


@base("move")
def _():
    return [(L((12, 3), (12, 21)), None, "I"), (L((3, 12), (21, 12)), None, "I"), (arrow_head(12, 3, 0, -1), None, "A"), (arrow_head(12, 21, 0, 1), None, "A"),
            (arrow_head(3, 12, -1, 0), None, "A"), (arrow_head(21, 12, 1, 0), None, "A"), (C(12, 12, 2), "orange", "I")]


@base("copy")
def _():
    return [(R(3, 3, 12, 12), "slate", "I"), (R(9, 9, 12, 12), "blue", "I")]


@base("create_similar")
def _():
    return [(R(3, 8, 10, 10), "orange", "I"), (R(14, 5, 7, 7), None, "A"), (R(14, 14, 7, 7), None, "A")]


@base("rotate")
def _():
    return [(R(8, 8, 8, 8), "slate", "I"), (arc(12, 12, 9, -60, 200), None, "A"), (arrow_head(3.8, 15.4, -0.6, -1), None, "A")]


@base("mirror")
def _():
    return [(P((3, 5), (10, 12), (3, 19)), "blue", "I"), (P((21, 5), (14, 12), (21, 19)), "slate", "I"), (L((12, 2), (12, 22)), None, "R")]


@base("align")
def _():
    return [(L((4, 3), (4, 21)), None, "R"), (R(6, 5, 10, 4), "blue", "I"), (R(6, 11, 14, 4), "slate", "I"), (R(6, 17, 7, 4), "blue", "I")]


@base("offset")
def _():
    return [(L((3, 17), (21, 17)), None, "I"), (L((3, 8), (21, 8)), None, "A"), (L((12, 15), (12, 10)), None, "A"), (arrow_head(12, 10, 0, -1), None, "A")]


@base("array")
def _():
    return [(R(3, 3, 5, 5), "blue", "I"), (R(10, 3, 5, 5), "slate", "I"), (R(17, 3, 5, 5), "slate", "I"), (R(3, 10, 5, 5), "slate", "I"), (R(10, 10, 5, 5), "slate", "I"),
            (R(17, 10, 5, 5), "slate", "I"), (R(3, 17, 5, 5), "slate", "I"), (R(10, 17, 5, 5), "slate", "I")]


@base("scale")
def _():
    return [(R(3, 11, 9, 9), "slate", "I"), (R(3, 3, 18, 18), None, "A"), (L((12, 12), (20, 4)), None, "A"), (arrow_head(20, 4, 1, -1), None, "A")]


@base("split")
def _():
    return [(R(3, 8, 8, 8), "slate", "I"), (R(13, 8, 8, 8), "slate", "I"), (L((12, 3), (12, 21)), None, "R"), (dots([(12, 3), (12, 21)], 1.2), "redsolid", None)]


@base("trim")
def _():
    return [(L((3, 21), (3, 9), (15, 9)), None, "I"), (L((3, 3), (21, 21)), None, "R"), (L((3, 21), (21, 21)), None, "A")]


@base("pin")
def _():
    return [(P((9, 3), (15, 3), (14, 11), (18, 14), (6, 14), (10, 11)), "red", "R"), (L((12, 14), (12, 21)), None, "I")]


@base("delete")
def _():
    return [(L((4, 6), (20, 6)), None, "I"), (P((6, 6), (7.5, 21), (16.5, 21), (18, 6)), "red", "R"), (L((9, 3), (15, 3)), None, "I"), (L((10, 10), (10, 17)) + " " + L((14, 10), (14, 17)), None, "R")]


@base("measure")
def _():
    return [(P((2, 17), (17, 2), (22, 7), (7, 22)), "yellow", "I"), (L((6, 14), (8, 16)) + " " + L((9, 11), (11, 13)) + " " + L((12, 8), (14, 10)), None, "O")]


@base("measure_along")
def _():
    return [("M3,18 C8,6 14,18 21,6", None, "I"), (dots([(3, 18), (21, 6)], 2), "accent", None), (arrow_head(21, 6, 1.5, -2.3), None, "A")]


@base("paste")
def _():
    return [(R(4, 5, 16, 16), "yellow", "I"), (R(8, 3, 8, 4), "slate", "I"), (R(8, 10, 8, 8), "white", "A")]


@base("cut")
def _():
    return [(C(7, 18, 3), None, "I"), (C(17, 18, 3), None, "I"), (L((9, 16), (17, 4)) + " " + L((15, 16), (7, 4)), None, "A")]


@base("match")
def _():
    return [(R(3, 3, 11, 6), "orange", "I"), (L((14, 6), (19, 6), (19, 12), (11, 12), (11, 15)), None, "I"), (R(9, 15, 4, 7), "blue", "I")]


@base("join")
def _():
    return [(R(3, 5, 11, 14), "slate", "I"), (R(10, 9, 11, 10), "blue", "I"), (L((10, 9), (14, 9), (14, 19)), None, "A")]


@base("unjoin")
def _():
    return [(R(2, 6, 9, 12), "slate", "I"), (R(13, 6, 9, 12), "blue", "I"), (L((11.5, 3), (11.5, 21)), None, "R")]


@base("attach")
def _():
    return [(R(3, 3, 12, 8), "slate", "I"), (P((9, 21), (21, 21), (21, 15), (9, 11)), "orange", "I"), (L((9, 11), (9, 15)), None, "A")]


@base("detach")
def _():
    return [(R(3, 3, 12, 6), "slate", "I"), (P((9, 21), (21, 21), (21, 15), (9, 11)), "orange", "I"), (L((12, 10), (14, 13)), None, "R")]


@base("profile")
def _():
    return [(P((3, 20), (3, 6), (12, 3), (21, 6), (21, 20)), "orange", "I"), (dots([(3, 20), (3, 6), (12, 3), (21, 6), (21, 20)], 1.5), "accent", None)]


@base("flip")
def _():
    return [(P((3, 18), (3, 8), (11, 8)), "blue", "I"), (P((21, 18), (21, 8), (13, 8)), None, "A"), (L((12, 4), (12, 22)), None, "R"), (arc(12, 6, 6, 200, 340), None, "A")]


@base("joins")
def _():
    return [(P((3, 3), (11, 3), (11, 11), (3, 11)), "orange", "I"), (P((13, 13), (21, 13), (21, 21), (13, 21)), "orange", "I"), (L((11, 11), (13, 13)), None, "A")]


@base("miter_join")
def _():
    return [(P((3, 4), (21, 4), (14, 11), (14, 21), (4, 21), (4, 11)), "orange", "I"), (L((14, 11), (21, 4)), None, "A")]


@base("butt_join")
def _():
    return [(R(3, 4, 18, 6), "orange", "I"), (R(10, 10, 6, 11), "orange", "I"), (L((10, 10), (16, 10)), None, "A")]


@base("square_off")
def _():
    return [(L((4, 20), (4, 4), (20, 4)), None, "I"), (L((4, 8), (8, 8), (8, 4)), None, "A"), (R(4, 4, 4, 4), "accent", None)]


@base("opening_wall")
def _():
    return [(R(2, 5, 20, 14), "orange", "I"), (R(8, 8, 8, 8), "white", "A")]


@base("opening_vertical")
def _():
    return [(P((3, 14), (12, 9), (21, 14), (12, 19)), "slate", "I"), (P((8, 14), (12, 12), (16, 14), (12, 16)), "white", "A"), (L((12, 12), (12, 3)), None, "A")]


@base("opening_dormer")
def _():
    return [(P((2, 20), (12, 11), (22, 20)), "red", "I"), (P((8, 20), (8, 14), (12, 10), (16, 14), (16, 20)), "slate", "I"), (R(10, 14, 4, 4), "blue", "A")]


@base("opening_shaft")
def _():
    return [(R(3, 8, 18, 12), "slate", "I"), (R(8, 5, 8, 8), "white", "A"), (L((8, 5), (8, 20)) + " " + L((16, 5), (16, 20)), None, "A")]


@base("opening_face")
def _():
    return [(P((3, 18), (7, 6), (21, 6), (17, 18)), "slate", "I"), (R(9, 9, 7, 5), "white", "A")]


@base("family")
def _():
    return [(P((12, 2), (20, 6.5), (12, 11), (4, 6.5)), "blue", "I"), (P((4, 6.5), (12, 11), (12, 20), (4, 15.5)), "slate", "I"),
            (P((12, 11), (20, 6.5), (20, 15.5), (12, 20)), "orange", "I")]


@base("library")
def _():
    return [(R(3, 4, 5, 16), "blue", "I"), (R(9, 4, 5, 16), "orange", "I"), (P((15, 5), (19, 4), (22, 19), (18, 20)), "green", "I")]


@base("file_insert")
def _():
    return [(P((5, 2), (15, 2), (19, 6), (19, 22), (5, 22)), "white", "I"), (L((15, 2), (15, 6), (19, 6)), None, "I"), (L((12, 10), (12, 18)), None, "A"),
            (arrow_head(12, 18, 0, 1), None, "A")]


@base("import")
def _():
    return [(P((3, 14), (3, 21), (21, 21), (21, 14)), "slate", "I"), (L((12, 3), (12, 15)), None, "A"), (arrow_head(12, 15, 0, 1), None, "A")]


@base("export")
def _():
    return [(P((3, 14), (3, 21), (21, 21), (21, 14)), "slate", "I"), (L((12, 16), (12, 4)), None, "A"), (arrow_head(12, 4, 0, -1), None, "A")]


@base("cad")
def _():
    return [(P((4, 2), (14, 2), (18, 6), (18, 22), (4, 22)), "slate", "I"), (L((14, 2), (14, 6), (18, 6)), None, "I"), (L((7, 14), (15, 14)), None, "A"),
            (L((7, 14), (7, 10), (11, 10)), None, "A"), (R(8, 16, 7, 3), "blue", "A")]


@base("ifc")
def _():
    return [(P((12, 2), (20, 6.5), (12, 11), (4, 6.5)), "blue", "I"), (P((4, 6.5), (12, 11), (12, 20), (4, 15.5)), "white", "I"),
            (P((12, 11), (20, 6.5), (20, 15.5), (12, 20)), "green", "I")]


@base("pdf")
def _():
    return [(P((5, 2), (15, 2), (19, 6), (19, 22), (5, 22)), "red", "R"), (L((15, 2), (15, 6), (19, 6)), None, "R"), (R(7.5, 12, 9, 5), "white", "R")]


@base("image")
def _():
    return [(R(3, 4, 18, 16), "white", "I"), (P((3, 20), (9, 12), (13, 17), (16, 13), (21, 20)), "green", "G"), (C(16, 8.5, 1.8), "yellow", "O")]


@base("point_cloud")
def _():
    return [(dots([(5, 18), (8, 12), (11, 16), (13, 8), (16, 13), (19, 6), (19, 17), (7, 6), (14, 19)], 1.3), "accent", None)]


@base("decal")
def _():
    return [(P((3, 3), (17, 3), (21, 7), (21, 21), (3, 21)), "yellow", "I"), (P((17, 3), (17, 7), (21, 7)), "orange", "I")]


@base("topography")
def _():
    return [("M2,17 C6,8 10,18 14,10 S20,6 22,8", None, "G"), ("M2,20 C7,12 11,21 15,14 S21,11 22,12", None, "G"), ("M2,13 C5,6 9,13 12,6", None, "I")]


@base("link")
def _():
    return [(P((7, 2.5), (12.5, 8), (7, 13.5), (1.5, 8)), "blue", "I"), (P((17, 10.5), (22.5, 16), (17, 21.5), (11.5, 16)), "orange", "I"),
            (L((9.5, 10.5), (14.5, 13.5)), None, "A")]


@base("link_cad")
def _():
    return [(P((3, 4), (11, 4), (14, 7), (14, 18), (3, 18)), "slate", "I"), (L((6, 12), (11, 12)), None, "A"), (R(15, 12, 6, 8), "blue", "A")]


@base("link_topo")
def _():
    return [("M2,16 C6,8 10,17 14,10 S20,7 22,9", None, "G"), ("M2,20 C7,13 11,21 15,15 S21,12 22,13", None, "G"), (C(18, 5, 2.5), "yellow", "O")]


@base("markup")
def _():
    return [(R(3, 4, 18, 14), "white", "I"), ("M6,14 C8,6 12,16 15,8 S18,10 19,7", None, "R"), (L((8, 21), (16, 21)), None, "I")]


@base("coordination")
def _():
    return [(C(8, 12, 5), "blue", "I"), (C(16, 12, 5), "orange", "I"), ("M12,8 A5,5 0 0 1 12,16 A5,5 0 0 1 12,8 Z", "green", None)]


@base("properties")
def _():
    return [(R(3, 3, 18, 18), "white", "I"), (L((6, 8), (18, 8)), None, "I"), (L((6, 12), (18, 12)), None, "I"), (L((6, 16), (13, 16)), None, "I"),
            (dots([(8, 8), (14, 12), (10, 16)], 1.6), "accent", None)]


@base("parameters")
def _():
    return [(L((4, 7), (20, 7)) + " " + L((4, 12), (20, 12)) + " " + L((4, 17), (20, 17)), None, "I"), (C(8, 7, 2.2), "blue", "A"), (C(15, 12, 2.2), "orange", "O"),
            (C(10, 17, 2.2), "green", "G")]


@base("gear")
def _():
    teeth = []
    for k in range(8):
        a = math.radians(k * 45)
        teeth.append((12 + 9.2 * math.cos(a), 12 + 9.2 * math.sin(a)))
    ring = []
    for k in range(16):
        a = math.radians(k * 22.5 - 11)
        r = 9 if k % 2 == 0 else 6.7
        ring.append((12 + r * math.cos(a), 12 + r * math.sin(a)))
    return [(P(*ring), "slate", "I"), (C(12, 12, 2.8), "white", "A")]


@base("materials")
def _():
    return [(C(12, 12, 9), "orange", "I"), ("M5,9 H19 M3.5,14 H20.5 M7,5.5 H17 M7,18.5 H17", None, "A"), ("M12,3 V21 M7.5,5 V19 M16.5,5 V19", None, "A")]


@base("object_styles")
def _():
    return [(R(3, 3, 8, 8), "red", "R"), (R(13, 3, 8, 8), "green", "G"), (R(3, 13, 8, 8), "blue", "A"), (R(13, 13, 8, 8), "yellow", "O")]


@base("shared_parameters")
def _():
    return [(L((4, 7), (20, 7)) + " " + L((4, 12), (20, 12)) + " " + L((4, 17), (20, 17)), None, "I"), (C(8, 7, 2.2), "blue", "A"), (C(15, 12, 2.2), "orange", "O"),
            (R(14, 15, 7, 6), "purple", "I")]


@base("numbering")
def _():
    return [(R(3, 3, 18, 18), "white", "I"), (L((8, 7), (8, 17)), None, "A"), (L((8, 7), (6, 9)), None, "A"), (L((13, 9), (16, 7), (16, 10), (13, 13), (17, 13)), None, "A")]


@base("units")
def _():
    return [(P((2, 16), (16, 2), (22, 8), (8, 22)), "yellow", "I"), (L((5, 13), (7, 15)) + " " + L((8, 10), (10, 12)) + " " + L((11, 7), (13, 9)), None, "O"), (R(16, 15, 6, 6), "white", "A")]


@base("settings_more")
def _():
    return [(R(3, 3, 18, 18), "white", "I"), (L((6, 9), (18, 9)) + " " + L((6, 15), (18, 15)), None, "I"), (C(10, 9, 2), "blue", "A"), (C(15, 15, 2), "orange", "O")]


@base("location")
def _():
    return [("M12,22 C6,14 5,11 5,9 A7,7 0 0 1 19,9 C19,11 18,14 12,22 Z", "red", "R"), (C(12, 9, 2.6), "white", "R")]


@base("coordinates")
def _():
    return [(L((4, 20), (4, 4)), None, "I"), (L((4, 20), (20, 20)), None, "I"), (arrow_head(4, 4, 0, -1), None, "A"), (arrow_head(20, 20, 1, 0), None, "A"), (C(4, 20, 2), "accent", None)]


@base("position")
def _():
    return [(C(12, 12, 8), None, "I"), (L((12, 2), (12, 7)) + " " + L((12, 17), (12, 22)) + " " + L((2, 12), (7, 12)) + " " + L((17, 12), (22, 12)), None, "A"), (C(12, 12, 2), "accent", None)]


@base("design_options")
def _():
    return [(C(6, 12, 2.5), "blue", "A"), (L((8.5, 12), (13, 12), (13, 6), (18, 6)) + " " + L((13, 12), (13, 18), (18, 18)), None, "I"), (R(18, 3.5, 4, 5), "orange", "I"),
            (R(18, 15.5, 4, 5), "green", "I")]


@base("phases")
def _():
    return [(P((3, 8), (12, 3), (21, 8), (12, 13)), "blue", "I"), (P((3, 12), (12, 17), (21, 12)), None, "A"), (P((3, 16), (12, 21), (21, 16)), None, "I")]


@base("select_id")
def _():
    return [(R(3, 3, 18, 18), "white", "I"), (L((9, 6), (8, 18)) + " " + L((15, 6), (14, 18)) + " " + L((6, 10), (19, 10)) + " " + L((5, 14), (18, 14)), None, "A")]


@base("warning")
def _():
    return [(P((12, 3), (22, 20), (2, 20)), "yellow", "O"), (L((12, 9), (12, 14)), None, "O"), (C(12, 17, 0.9), "orangesolid", None)]


@base("automation")
def _():
    return [(R(3, 4, 7, 5), "blue", "I"), (R(14, 4, 7, 5), "orange", "I"), (R(8, 15, 8, 5), "green", "I"), (L((10, 6.5), (14, 6.5)) + " " + L((6.5, 9), (6.5, 12), (12, 12), (12, 15)) + " " + L((17.5, 9), (17.5, 12), (12, 12)), None, "A")]


@base("player")
def _():
    return [(R(3, 4, 18, 16), "slate", "I"), (P((10, 8), (16, 12), (10, 16)), "accent", None)]


@base("ai")
def _():
    return [(P((12, 2), (14.2, 9.8), (22, 12), (14.2, 14.2), (12, 22), (9.8, 14.2), (2, 12), (9.8, 9.8)), "purple", "I"), (dots([(19, 4.5), (5, 19.5)], 1.4), "accent", None)]


@base("help")
def _():
    return [(C(12, 12, 9), "blue", "I"), ("M9,9.5 A3,3 0 1 1 13,12.3 C12.2,12.8 12,13.3 12,14.5", None, "A"), (C(12, 17.5, 0.9), "accent", None)]


@base("about")
def _():
    return [(C(12, 12, 9), "blue", "I"), (L((12, 11), (12, 17)), None, "A"), (C(12, 7.5, 1), "accent", None)]


@base("family_converter")
def _():
    return [(P((3, 4), (11, 8), (3, 12)), "blue", "I"), (P((21, 12), (13, 16), (21, 20)), "orange", "I"), (arc(12, 12, 8, -40, 40), None, "A")]


@base("transmit")
def _():
    return [(P((3, 8), (12, 3), (21, 8), (12, 13)), "blue", "I"), (L((12, 13), (12, 21)), None, "A"), (arrow_head(12, 21, 0, 1), None, "A")]


@base("collaborate")
def _():
    return [(C(8, 8, 3), "blue", "I"), (C(16, 8, 3), "orange", "I"), ("M3,19 A5,5 0 0 1 13,19 Z", "blue", "I"), ("M11,19 A5,5 0 0 1 21,19 Z", "orange", "I")]


@base("worksets")
def _():
    return [(P((3, 7), (12, 3), (21, 7), (12, 11)), "blue", "I"), (P((3, 11), (12, 15), (21, 11)), None, "A"), (C(18, 18, 3), "orange", "I"), ("M13,22 A5,5 0 0 1 23,22", None, "I")]


@base("sync")
def _():
    return [(arc(12, 12, 8, -160, -20), None, "A"), (arrow_head(19.5, 9.5, 0.5, 1), None, "A"), (arc(12, 12, 8, 20, 160), None, "I"), (arrow_head(4.5, 14.5, -0.5, -1), None, "I")]


@base("reload")
def _():
    return [(arc(12, 12, 8, -40, 250), None, "A"), (arrow_head(19, 6, 1, 1), None, "A"), (P((9, 9), (15, 9), (15, 15), (9, 15)), "blue", "I")]


@base("relinquish")
def _():
    return [(P((6, 20), (6, 11), (8, 10), (8, 5), (10, 5), (10, 10), (12, 4), (14, 4), (14, 10), (16, 6), (18, 6), (18, 14), (20, 15), (20, 20)), "orange", "I")]


@base("monitor")
def _():
    return [(R(3, 4, 18, 12), "slate", "I"), (L((5, 12), (9, 8), (12, 11), (16, 6)), None, "A"), (L((8, 20), (16, 20)) + " " + L((12, 16), (12, 20)), None, "I")]


@base("history")
def _():
    return [(C(12, 12, 8), "white", "I"), (L((12, 7), (12, 12), (16, 14)), None, "A"), (arc(12, 12, 10.5, 200, 330), None, "A"), (arrow_head(3.4, 8.5, 0.2, 1), None, "A")]


@base("backup")
def _():
    return [(R(3, 3, 18, 18), "slate", "I"), (R(6, 3, 10, 6), "white", "A"), (R(7, 13, 10, 8), "blue", "I"), (R(13, 4.2, 2, 3.6), "accent", None)]


@base("publish")
def _():
    return [("M6,17 A4,4 0 0 1 6.6,9 A5.5,5.5 0 0 1 17.2,8.2 A4.3,4.3 0 0 1 17.6,17 Z", "blue", "I"), (L((12, 18), (12, 10)), None, "A"), (arrow_head(12, 10, 0, -1), None, "A")]


@base("cloud")
def _():
    return [("M6,18 A4,4 0 0 1 6.6,10 A5.5,5.5 0 0 1 17.2,9.2 A4.3,4.3 0 0 1 17.6,18 Z", "blue", "I")]


@base("coordinate")
def _():
    return [(C(8, 12, 5), "blue", "I"), (C(16, 12, 5), "blue", "I"), (L((8, 12), (16, 12)), None, "A")]


@base("share")
def _():
    return [(C(6, 12, 2.6), "blue", "I"), (C(18, 6, 2.6), "orange", "I"), (C(18, 18, 2.6), "green", "I"), (L((8.3, 11), (15.7, 7)) + " " + L((8.3, 13), (15.7, 17)), None, "A")]


@base("edit_requests")
def _():
    return [(R(3, 4, 18, 13), "white", "I"), (P((8, 17), (8, 21), (13, 17)), "white", "I"), (L((7, 8), (17, 8)) + " " + L((7, 12), (13, 12)), None, "A")]


@base("analysis")
def _():
    return [(R(3, 3, 18, 18), "white", "I"), (R(6, 12, 3, 6), "blue", "A"), (R(11, 8, 3, 10), "orange", "O"), (R(16, 5, 3, 13), "green", "G")]


@base("structural_analysis")
def _():
    return [(L((3, 19), (3, 6), (21, 6), (21, 19)), None, "I"), ("M3,6 C8,12 16,12 21,6", None, "R"), (P((1.5, 19), (4.5, 19), (3, 17)), "slate", "I"), (P((19.5, 19), (22.5, 19), (21, 17)), "slate", "I")]


@base("systems_analysis")
def _():
    return [(C(6, 6, 2.5), "blue", "I"), (C(18, 6, 2.5), "orange", "I"), (C(12, 18, 2.5), "green", "I"), (L((8, 7), (11, 16)) + " " + L((16, 7), (13, 16)) + " " + L((8.5, 6), (15.5, 6)), None, "A")]


@base("energy")
def _():
    return [(P((13, 2), (5, 13), (11, 13), (9, 22), (19, 10), (13, 10)), "yellow", "O")]


@base("form_creation")
def _():
    return [(P((3, 17), (8, 6), (16, 3), (21, 11), (14, 20)), "blue", "I"), (dots([(3, 17), (8, 6), (16, 3), (21, 11), (14, 20)], 1.3), "accent", None)]


@base("results_fill")
def _():
    return [(R(3, 3, 18, 18), "white", "I"), (P((3, 21), (3, 12), (9, 10), (15, 14), (21, 6), (21, 21)), "green", "G"), (L((3, 12), (9, 10), (15, 14), (21, 6)), None, "A")]


@base("reports")
def _():
    return [(P((5, 2), (15, 2), (19, 6), (19, 22), (5, 22)), "white", "I"), (L((15, 2), (15, 6), (19, 6)), None, "I"), (R(8, 14, 2, 5), "blue", "A"), (R(11.5, 11, 2, 8), "orange", "O"),
            (R(15, 9, 2, 10), "green", "G")]


@base("model_check")
def _():
    return [(P((12, 2), (20, 5), (20, 12), (12, 21), (4, 12), (4, 5)), "green", "G"), (L((8, 12), (11, 15), (16, 9)), None, "G")]


@base("mass")
def _():
    return [(P((3, 18), (3, 9), (9, 5), (15, 9), (21, 6), (21, 17), (14, 21)), "purple", "I"), (L((9, 5), (9, 14), (3, 18)) + " " + L((9, 14), (14, 21)), None, "A")]


@base("mass_inplace")
def _():
    return [(P((4, 17), (4, 9), (11, 4), (18, 9), (18, 17), (11, 21)), "purple", "I"), (L((11, 4), (11, 21)), None, "A")]


@base("site")
def _():
    return [("M2,19 C7,15 12,20 22,13 V22 H2 Z", "green", "G"), (C(8, 8, 4), "green", "G"), (L((8, 12), (8, 17)), None, "O")]


@base("toposolid")
def _():
    return [("M2,11 C7,6 12,12 22,8 V19 H2 Z", "green", "G"), (L((2, 19), (22, 19)), None, "I"), (L((6, 14), (6, 19)) + " " + L((12, 15), (12, 19)) + " " + L((18, 12), (18, 19)), None, "A")]


@base("parking")
def _():
    return [(R(3, 3, 18, 18), "blue", "I"), ("M9,17 V7 H13 A3,3 0 0 1 13,13 H9", None, "A")]


@base("property_line")
def _():
    return [(P((4, 5), (20, 3), (21, 16), (8, 20)), None, "O"), (dots([(4, 5), (20, 3), (21, 16), (8, 20)], 1.5), "orangesolid", None)]


@base("contours")
def _():
    return [("M2,17 C6,9 12,12 14,6 S22,9 22,9", None, "G"), ("M2,13 C6,6 11,9 13,3", None, "G"), (R(14, 14, 8, 6), "yellow", "O")]


@base("graded_region")
def _():
    return [("M2,18 C7,10 14,16 22,8 V21 H2 Z", "orange", "O"), (hatch(2, 12, 22, 21, 4), None, "O")]


@base("site_component")
def _():
    return [(C(12, 8, 5), "green", "G"), (L((12, 13), (12, 21)), None, "O"), (L((3, 21), (21, 21)), None, "I")]


@base("duct")
def _():
    return [(R(2, 8, 20, 8), "blue", "I"), (L((7, 8), (7, 16)) + " " + L((12, 8), (12, 16)) + " " + L((17, 8), (17, 16)), None, "A")]


@base("duct_placeholder")
def _():
    return [(R(2, 8, 20, 8), None, "A"), (L((7, 8), (7, 16)) + " " + L((17, 8), (17, 16)), None, "A")]


@base("flex_duct")
def _():
    return [("M2,16 C6,4 10,20 14,8 S20,6 22,8", None, "A"), ("M2,19 C6,7 10,23 14,11 S20,9 22,11", None, "I")]


@base("air_terminal")
def _():
    return [(R(3, 10, 18, 4), "blue", "I"), (R(7, 7, 10, 3), "slate", "I"), (L((6, 17), (4, 21)) + " " + L((12, 17), (12, 21)) + " " + L((18, 17), (20, 21)), None, "A")]


@base("fabrication")
def _():
    return [(P((3, 9), (12, 4), (21, 9), (21, 15), (12, 20), (3, 15)), "slate", "I"), (L((12, 4), (12, 20)) + " " + L((3, 9), (21, 15)), None, "A")]


@base("routing")
def _():
    return [(L((3, 18), (9, 18), (9, 8), (16, 8), (16, 14), (21, 14)), None, "A"), (dots([(3, 18), (9, 8), (21, 14)], 1.8), "ink", None)]


@base("stiffener")
def _():
    return [(R(3, 8, 18, 8), "blue", "I"), (L((3, 8), (21, 16)) + " " + L((3, 16), (21, 8)), None, "A")]


@base("pid")
def _():
    return [(C(6, 12, 3), "white", "I"), (R(14, 7, 7, 10), "blue", "I"), (L((9, 12), (14, 12)), None, "A"), (L((17.5, 17), (17.5, 21)) + " " + L((17.5, 3), (17.5, 7)), None, "A")]


@base("mechanical_equipment")
def _():
    return [(R(3, 3, 18, 18), "slate", "I"), (C(12, 12, 6.5), "white", "I"), (P((12, 12), (12, 6), (15.5, 8)) + " " + P((12, 12), (18, 12), (16, 15.5)) + " " + P((12, 12), (12, 18), (8.5, 16)) + " " + P((12, 12), (6, 12), (8, 8.5)), "blue", "A")]


@base("control_device")
def _():
    return [(R(6, 3, 12, 18), "slate", "I"), (C(12, 10, 3.5), "white", "A"), (L((12, 10), (13.5, 8)), None, "A"), (R(8.5, 16, 7, 2.5), "blue", "A")]


@base("pipe")
def _():
    return [(R(2, 9, 20, 6), "green", "G"), (L((6, 9), (6, 15)) + " " + L((18, 9), (18, 15)), None, "A")]


@base("flex_pipe")
def _():
    return [("M2,16 C6,6 10,18 14,8 S20,8 22,10", None, "G"), ("M2,19 C6,9 10,21 14,11 S20,11 22,13", None, "I")]


@base("plumbing_fixture")
def _():
    return [("M9,3 H15 V8 H9 Z", "blue", "A"), (P((4, 10), (20, 10), (17, 20), (7, 20)), "white", "I"), (L((10, 14), (14, 14)), None, "A")]


@base("electrical_equipment")
def _():
    return [(R(5, 2, 14, 20), "slate", "I"), (L((8, 6), (16, 6)) + " " + L((8, 10), (16, 10)) + " " + L((8, 14), (13, 14)), None, "A"), (P((14, 13), (11, 19), (14, 19), (12, 22)), "yellow", "O")]


@base("wire")
def _():
    return [("M2,17 C6,5 11,19 16,8 S21,9 22,6", None, "O"), (dots([(2, 17), (22, 6)], 1.8), "orangesolid", None)]


@base("cable_tray")
def _():
    return [(P((3, 9), (3, 17), (21, 17), (21, 9)), "slate", "I"), (L((7, 17), (7, 12)) + " " + L((12, 17), (12, 12)) + " " + L((17, 17), (17, 12)), None, "A"), (L((3, 20), (21, 20)), None, "A")]


@base("conduit")
def _():
    return [(R(2, 9, 14, 6), "orange", "O"), ("M16,12 C19,12 20,14 20,17 V21", None, "O")]


@base("rebar")
def _():
    return [(R(4, 4, 16, 16), None, "I"), (R(7, 7, 10, 10), None, "A"), (dots([(7, 7), (17, 7), (7, 17), (17, 17), (12, 7), (12, 17)], 1.5), "accent", None)]


@base("rebar_path")
def _():
    return [("M3,18 L3,8 L12,8 L12,18 L21,18", None, "A"), (dots([(3, 18), (21, 18)], 1.6), "accent", None), (R(2, 3, 20, 3), "slate", "I")]


@base("rebar_area")
def _():
    return [(R(3, 3, 18, 18), "slate", "I"), (L((3, 9), (21, 9)) + " " + L((3, 15), (21, 15)) + " " + L((9, 3), (9, 21)) + " " + L((15, 3), (15, 21)), None, "A")]


@base("fabric")
def _():
    return [(R(3, 5, 18, 14), "white", "I"), (L((3, 9.7), (21, 9.7)) + " " + L((3, 14.3), (21, 14.3)) + " " + L((9, 5), (9, 19)) + " " + L((15, 5), (15, 19)), None, "A"), (dots([(9, 9.7), (15, 9.7), (9, 14.3), (15, 14.3)], 1), "accent", None)]


@base("fabric_transformed")
def _():
    return [(P((3, 7), (20, 3), (21, 17), (5, 21)), "white", "I"), (L((4, 11.5), (20.5, 10)) + " " + L((4.5, 16), (20.8, 13.5)) + " " + L((9, 6), (10, 20)) + " " + L((15, 4.5), (15.5, 18.8)), None, "A")]


@base("cover")
def _():
    return [(R(3, 3, 18, 18), "slate", "I"), (R(7, 7, 10, 10), "white", "A"), (L((3, 7), (7, 7)) + " " + L((7, 3), (7, 7)), None, "A")]


@base("precast")
def _():
    return [(R(3, 6, 18, 12), "slate", "I"), (R(7, 9, 4, 6), "white", "A"), (R(13, 9, 4, 6), "white", "A")]


@base("shop_drawings")
def _():
    return [(R(3, 3, 18, 18), "white", "I"), (R(6, 6, 8, 6), "blue", "A"), (L((6, 15), (18, 15)) + " " + L((6, 18), (14, 18)), None, "I"), (arrow_head(18, 12, 1, 0), None, "A")]


@base("cam_export")
def _():
    return [(R(4, 9, 16, 12), "slate", "I"), (L((12, 9), (12, 2)) + " " + L((8, 5), (12, 2), (16, 5)), None, "A")]


@base("configuration")
def _():
    return [(R(3, 3, 18, 18), "slate", "I"), (L((6, 9), (18, 9)) + " " + L((6, 15), (18, 15)), None, "I"), (C(10, 9, 2), "blue", "A"), (C(15, 15, 2), "orange", "O")]


@base("plate")
def _():
    return [(R(4, 4, 16, 16), "slate", "I"), (dots([(7.5, 7.5), (16.5, 7.5), (7.5, 16.5), (16.5, 16.5)], 1.4), "ink", None)]


@base("bolts")
def _():
    return [(P((7, 4), (17, 4), (20, 9), (17, 14), (7, 14), (4, 9)), "slate", "I"), (C(12, 9, 2.4), "white", "A"), (L((12, 14), (12, 22)), None, "I")]


@base("welds")
def _():
    return [(R(3, 12, 18, 3), "slate", "I"), (R(10, 3, 3, 9), "slate", "I"), (P((10, 12), (6, 12), (10, 8)), "orange", "O"), (P((13, 12), (17, 12), (13, 8)), "orange", "O")]


@base("corner_cut")
def _():
    return [(P((3, 3), (14, 3), (21, 10), (21, 21), (3, 21)), "slate", "I"), (L((14, 3), (21, 10)), None, "R")]


@base("cope")
def _():
    return [(P((3, 6), (21, 6), (21, 18), (3, 18)), "slate", "I"), (R(3, 6, 6, 4), "white", "R")]


@base("shorten")
def _():
    return [(R(3, 8, 14, 8), "slate", "I"), (L((17, 5), (17, 19)), None, "R"), (arrow_head(21, 12, -1, 0), None, "A"), (L((21, 12), (18, 12)), None, "A")]


@base("miter")
def _():
    return [(P((3, 4), (21, 4), (14, 11), (14, 21), (4, 21), (4, 11)), "slate", "I"), (L((14, 11), (21, 4)), None, "R")]


@base("cut_through")
def _():
    return [(R(3, 3, 8, 18), "slate", "I"), (R(13, 8, 8, 8), "slate", "I"), (L((11, 12), (13, 12)), None, "R"), (arrow_head(13, 12, 1, 0), None, "R")]


@base("cut_by")
def _():
    return [(R(3, 3, 18, 18), "slate", "I"), (C(12, 12, 5), "white", "R")]


@base("ui")
def _():
    return [(R(2, 4, 20, 16), "white", "I"), (R(2, 4, 20, 4), "blue", "I"), (R(4, 10, 5, 8), "slate", "A")]


@base("theme")
def _():
    return [(C(12, 12, 9), "white", "I"), ("M12,3 A9,9 0 0 0 12,21 Z", "ink", None), (dots([(8, 9), (8, 15)], 1), "white", None)]


@base("save")
def _():
    return [(P((3, 3), (18, 3), (21, 6), (21, 21), (3, 21)), "blue", "I"), (R(7, 3, 9, 6), "white", "A"), (R(6, 13, 12, 8), "white", "I"), (R(13, 4.3, 2, 3.6), "accent", None)]


@base("new_file")
def _():
    return [(P((5, 2), (15, 2), (19, 6), (19, 22), (5, 22)), "white", "I"), (L((15, 2), (15, 6), (19, 6)), None, "I"), (L((12, 10), (12, 18)) + " " + L((8, 14), (16, 14)), None, "A")]


@base("open_file")
def _():
    return [(P((2, 6), (9, 6), (11, 8), (21, 8), (21, 20), (2, 20)), "yellow", "O"), (P((2, 20), (5, 12), (23, 12), (21, 20)), "orange", "I")]


@base("undo")
def _():
    return [("M8,8 H15 A5,5 0 0 1 15,18 H8", None, "A"), (arrow_head(8, 8, -1, 0, 3), None, "A")]


@base("redo")
def _():
    return [("M16,8 H9 A5,5 0 0 0 9,18 H16", None, "A"), (arrow_head(16, 8, 1, 0, 3), None, "A")]


@base("modify")
def _():
    return [(P((5, 3), (5, 18), (9, 14), (12, 21), (14.5, 20), (11.5, 13), (17, 13)), "white", "I")]


@base("select")
def _():
    return [(P((5, 3), (5, 18), (9, 14), (12, 21), (14.5, 20), (11.5, 13), (17, 13)), "blue", "I")]


@base("model_group")
def _():
    return [(R(3, 3, 18, 18), None, "A"), (P((12, 6), (17, 9), (12, 12), (7, 9)), "blue", "I"), (P((7, 9), (12, 12), (12, 18), (7, 15)), "slate", "I"),
            (P((12, 12), (17, 9), (17, 15), (12, 18)), "orange", "I")]


# ---------------------------------------------------------------------------------------------- badges

BADGES = {
    "plus": lambda cx, cy: (L((cx - 2, cy), (cx + 2, cy)) + " " + L((cx, cy - 2), (cx, cy + 2)), "G"),
    "minus": lambda cx, cy: (L((cx - 2, cy), (cx + 2, cy)), "R"),
    "check": lambda cx, cy: (L((cx - 2, cy), (cx - 0.5, cy + 1.6), (cx + 2.2, cy - 1.8)), "G"),
    "x": lambda cx, cy: (L((cx - 1.8, cy - 1.8), (cx + 1.8, cy + 1.8)) + " " + L((cx + 1.8, cy - 1.8), (cx - 1.8, cy + 1.8)), "R"),
    "arrow": lambda cx, cy: (L((cx - 2, cy), (cx + 2, cy)) + " " + arrow_head(cx + 2, cy, 1, 0, 1.8), "A"),
    "down": lambda cx, cy: (L((cx, cy - 2), (cx, cy + 2)) + " " + arrow_head(cx, cy + 2, 0, 1, 1.8), "A"),
    "up": lambda cx, cy: (L((cx, cy + 2), (cx, cy - 2)) + " " + arrow_head(cx, cy - 2, 0, -1, 1.8), "A"),
    "gear": lambda cx, cy: (C(cx, cy, 1.8) + " " + L((cx, cy - 2.8), (cx, cy - 2)) + " " + L((cx, cy + 2), (cx, cy + 2.8)) + " " + L((cx - 2.8, cy), (cx - 2, cy)) + " " + L((cx + 2, cy), (cx + 2.8, cy)), "A"),
    "link": lambda cx, cy: (L((cx - 2, cy + 1), (cx + 2, cy - 1)), "A"),
    "lock": lambda cx, cy: (R(cx - 1.8, cy - 0.2, 3.6, 2.6) + " M" + f"{cx - 1},{cy - 0.2} V{cy - 1.2} A1,1 0 0 1 {cx + 1},{cy - 1.2} V{cy - 0.2}", "I"),
    "pencil": lambda cx, cy: (L((cx - 2, cy + 2), (cx - 2, cy + 0.2), (cx + 1.8, cy - 2), (cx + 2.2, cy - 1.2), (cx - 1.6, cy + 1)), "O"),
    "question": lambda cx, cy: (f"M{cx - 1.2},{cy - 1} A1.3,1.3 0 1 1 {cx + 0.6},{cy + 0.2} C{cx},{cy + 0.6} {cx},{cy + 0.9} {cx},{cy + 1.3}", "A"),
    "star": lambda cx, cy: (P((cx, cy - 2.4), (cx + 0.7, cy - 0.7), (cx + 2.4, cy - 0.6), (cx + 1.1, cy + 0.5), (cx + 1.5, cy + 2.2), (cx, cy + 1.2), (cx - 1.5, cy + 2.2), (cx - 1.1, cy + 0.5), (cx - 2.4, cy - 0.6), (cx - 0.7, cy - 0.7)), "O"),
    "eye": lambda cx, cy: (f"M{cx - 2.4},{cy} C{cx - 1},{cy - 2} {cx + 1},{cy - 2} {cx + 2.4},{cy} C{cx + 1},{cy + 2} {cx - 1},{cy + 2} {cx - 2.4},{cy} Z", "A"),
    "tag": lambda cx, cy: (P((cx - 2, cy), (cx, cy - 2), (cx + 2, cy - 2), (cx + 2, cy), (cx, cy + 2)), "O"),
    "rotate": lambda cx, cy: (arc(cx, cy, 2, -60, 220), "A"),
    "dots": lambda cx, cy: (C(cx - 2, cy, 0.5) + " " + C(cx, cy, 0.5) + " " + C(cx + 2, cy, 0.5), "I"),
}
