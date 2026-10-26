"""Generates the built-in, seamlessly tiling render textures shipped with Guanacaste 3D.

Every image is periodic (noise is filtered in the Fourier domain, cells wrap around the edges), so it repeats
without seams when the renderer projects it in world space. Each image covers the real size given in TEXTURES of
src/Guanacaste3D.Desktop/render-texture-library.cs. Run:

    python tools/textures/generate_textures.py
"""
from pathlib import Path

import numpy as np
from PIL import Image

SIZE = 1024
OUT = Path(__file__).resolve().parents[2] / "src" / "Guanacaste3D.Desktop" / "resources" / "textures"
rng = np.random.default_rng(20261026)


def periodic_noise(scale, size=SIZE, seed=None):
    """Tileable noise with features about `scale` pixels wide, normalized to 0..1."""
    generator = np.random.default_rng(seed) if seed is not None else rng
    white = generator.standard_normal((size, size))
    fx = np.fft.fftfreq(size)[:, None]
    fy = np.fft.fftfreq(size)[None, :]
    radius = np.sqrt(fx * fx + fy * fy) * scale
    filtered = np.real(np.fft.ifft2(np.fft.fft2(white) * np.exp(-(radius ** 2) * 6)))
    filtered -= filtered.min()
    return filtered / max(filtered.max(), 1e-9)


def fbm(scales, weights):
    total = sum(weight * periodic_noise(scale) for scale, weight in zip(scales, weights))
    total -= total.min()
    return total / total.max()


def save(name, rgb):
    OUT.mkdir(parents=True, exist_ok=True)
    image = Image.fromarray(np.clip(rgb * 255 + 0.5, 0, 255).astype(np.uint8), "RGB")
    image.save(OUT / f"{name}.png", optimize=True)
    print("wrote", OUT / f"{name}.png")


def color(*rgb):
    return np.array(rgb, dtype=np.float64)[None, None, :] / 255


ys, xs = np.mgrid[0:SIZE, 0:SIZE]


def brick():
    """1 m × 1 m: twelve courses of four bricks in running bond with recessed mortar."""
    courses, per_row = 12, 4
    course_h = SIZE / courses
    brick_w = SIZE / per_row
    row = (ys // course_h).astype(int)
    shifted = (xs + (row % 2) * brick_w / 2) % SIZE
    column = (shifted // brick_w).astype(int)
    joint = 9
    in_x = shifted % brick_w
    in_y = ys % course_h
    edge = np.minimum(np.minimum(in_x, brick_w - in_x), np.minimum(in_y, course_h - in_y))
    mortar = np.clip(1 - (edge - joint * 0.5) / 3, 0, 1)
    ids = (row * 7919 + column * 104729) % 9973
    tone = (np.sin(ids * 12.9898) * 43758.5453) % 1
    grain = fbm([6, 20, 70], [0.5, 0.3, 0.2])
    base = color(150, 66, 44) * (0.78 + 0.32 * tone[..., None]) * (0.85 + 0.3 * grain[..., None])
    burnt = color(95, 45, 35) * (0.9 + 0.2 * grain[..., None])
    base = np.where((tone > 0.86)[..., None], burnt, base)
    mortar_color = color(176, 170, 158) * (0.85 + 0.25 * periodic_noise(4)[..., None])
    save("brick-red", base * (1 - mortar[..., None]) + mortar_color * mortar[..., None])


def wood_floor():
    """1 m × 1 m: six boards 16.7 cm wide, staggered end joints, grain along the boards."""
    boards = 6
    board_w = SIZE / boards
    board = (ys // board_w).astype(int)
    offset = (np.array([0.0, 0.43, 0.17, 0.71, 0.29, 0.58]) * SIZE)[board]
    along = (xs + offset) % SIZE
    in_y = ys % board_w
    seam = np.clip(1 - np.minimum(in_y, board_w - in_y) / 2.2, 0, 1)
    end_joint = np.clip(1 - np.minimum(along, SIZE - along) / 2.2, 0, 1)
    tone = np.array([0.92, 1.05, 0.97, 1.1, 0.88, 1.0])[board]
    # Grain: noise stretched along the board, plus fine pores.
    stretched = periodic_noise(40)
    grain = np.roll(stretched, 0, axis=1)
    lines = 0.5 + 0.5 * np.sin((ys + 25 * fbm([60, 200], [0.6, 0.4])) * 0.9 + board * 3)
    pores = periodic_noise(2.5)
    shade = (0.75 + 0.15 * lines + 0.12 * grain + 0.06 * pores) * tone
    base = color(156, 104, 64) * shade[..., None]
    gap = np.maximum(seam, end_joint)[..., None]
    save("wood-floor", base * (1 - 0.65 * gap))


def stone_paving():
    """2 m × 2 m: irregular flagstones (wrapped Voronoi cells) with sandy joints."""
    count = 34
    points = rng.random((count, 2)) * SIZE
    nearest = np.full((SIZE, SIZE), np.inf)
    second = np.full((SIZE, SIZE), np.inf)
    owner = np.zeros((SIZE, SIZE), dtype=int)
    for index, (px, py) in enumerate(points):
        dx = np.abs(xs - px)
        dy = np.abs(ys - py)
        dx = np.minimum(dx, SIZE - dx)
        dy = np.minimum(dy, SIZE - dy)
        distance = np.sqrt(dx * dx + dy * dy)
        closer = distance < nearest
        second = np.where(closer, nearest, np.minimum(second, distance))
        owner = np.where(closer, index, owner)
        nearest = np.where(closer, distance, nearest)
    edge = second - nearest
    joint = np.clip(1 - (edge - 7) / 5, 0, 1)
    tones = rng.random(count)
    hues = np.stack([0.95 + 0.1 * rng.random(count), 0.93 + 0.08 * rng.random(count), 0.86 + 0.1 * rng.random(count)], -1)
    grain = fbm([5, 25, 90], [0.4, 0.35, 0.25])
    base = color(150, 142, 128) * (0.72 + 0.4 * tones[owner])[..., None] * hues[owner] * (0.85 + 0.3 * grain[..., None])
    sand = color(120, 108, 88) * (0.8 + 0.3 * periodic_noise(3)[..., None])
    save("stone-paving", base * (1 - joint[..., None]) + sand * joint[..., None])


def concrete():
    """2.4 m × 1.2 m formwork panel read as a square 1.2 m tile: mottling, pores and one tie-hole row."""
    mottle = fbm([30, 120, 400], [0.3, 0.4, 0.3])
    pores = (periodic_noise(1.5) > 0.86).astype(float) * 0.25
    shade = 0.8 + 0.25 * mottle - pores
    holes = np.zeros((SIZE, SIZE))
    for cx in (SIZE * 0.25, SIZE * 0.75):
        for cy in (SIZE * 0.25, SIZE * 0.75):
            holes = np.maximum(holes, np.clip(1 - np.hypot(xs - cx, ys - cy) / 14, 0, 1))
    seam = np.clip(1 - np.minimum(ys, SIZE - ys) / 3, 0, 1)
    shade = shade * (1 - 0.45 * holes) * (1 - 0.25 * seam)
    save("concrete", color(168, 166, 160) * shade[..., None])


def stucco():
    """1 m × 1 m white trowelled render: soft swirls and fine sand."""
    swirls = fbm([60, 180], [0.6, 0.4])
    sand = periodic_noise(2)
    shade = 0.9 + 0.08 * swirls + 0.05 * (sand - 0.5)
    save("stucco-white", color(236, 232, 222) * shade[..., None])


def gravel():
    """1 m × 1 m: densely packed, rounded pebbles of mixed tones (wrapped Voronoi cells shaded as domes)."""
    from scipy.spatial import cKDTree

    count = 1400
    points = rng.random((count, 2)) * SIZE
    # Copies shifted by the image size make the nearest-cell search wrap around the edges.
    shifts = [(dx, dy) for dx in (-SIZE, 0, SIZE) for dy in (-SIZE, 0, SIZE)]
    tiled = np.concatenate([points + shift for shift in shifts])
    tree = cKDTree(tiled)
    query = np.stack([xs.ravel(), ys.ravel()], -1)
    distances, indices = tree.query(query, k=2)
    nearest = distances[:, 0].reshape(SIZE, SIZE)
    edge = (distances[:, 1] - distances[:, 0]).reshape(SIZE, SIZE)
    owner = (indices[:, 0] % count).reshape(SIZE, SIZE)
    dome = np.clip(edge / 9, 0, 1) ** 0.5
    tones = 0.65 + 0.55 * rng.random(count)
    warm = rng.random(count)
    hue = np.stack([1 + 0.08 * warm, 1 + 0.02 * warm, 0.92 - 0.06 * warm], -1)
    speckle = periodic_noise(2)
    base = color(168, 160, 148) * tones[owner][..., None] * hue[owner] * (0.88 + 0.2 * speckle[..., None])
    save("gravel", base * (0.45 + 0.55 * dome[..., None]))


if __name__ == "__main__":
    import sys

    chosen = sys.argv[1:] or ["brick", "wood_floor", "stone_paving", "concrete", "stucco", "gravel"]
    for name in chosen:
        globals()[name]()
