"""Reusable prop and texture drawers for the themed stages. Every prop draws strictly inside its tile footprint
(x0, y0, x1, y1 in world units) so the art matches the collision rect a map author draws over it in Tiled."""
import math
from common import TILE, LINE, DARK, s, rect, hline, vline, circle, poly, mix


# ---- Textures (whole-layer painters) --------------------------------------------------------------------------------


def tile_grid(stage, d, col, width=2, step=TILE):
    for x in range(0, stage.W + 1, step):
        vline(d, x, 0, stage.H, col, width)
    for y in range(0, stage.H + 1, step):
        hline(d, 0, stage.W, y, col, width)


def points(stage, n, pad=0):
    r = stage.rng
    return [(r.uniform(pad, stage.W - pad), r.uniform(pad, stage.H - pad)) for _ in range(n)]


def density(stage, per_tile):
    return int(stage.tw * stage.th * per_tile)


def canopy(stage, d, shades, edge, rmin=26, rmax=54, per_tile=0.9):
    """Dense overlapping tree crowns, painted back to front so lower crowns overlap higher ones."""
    pts = sorted(points(stage, density(stage, per_tile)), key=lambda p: p[1])
    for x, y in pts:
        r = stage.rng.uniform(rmin, rmax)
        c = stage.rng.choice(shades)
        circle(d, x, y, r, c, edge, 3)
        circle(d, x - r * 0.25, y - r * 0.3, r * 0.45, mix(c, (255, 255, 255), 0.12))


def tufts(stage, d, col, per_tile=0.6, size=7):
    for x, y in points(stage, density(stage, per_tile)):
        for dx in (-size * 0.6, 0, size * 0.6):
            d.line([(s(x + dx * 0.4), s(y)), (s(x + dx), s(y - size))], fill=col, width=2)


def specks(stage, d, cols, per_tile=0.5, rmin=2, rmax=4):
    for x, y in points(stage, density(stage, per_tile)):
        circle(d, x, y, stage.rng.uniform(rmin, rmax), stage.rng.choice(cols))


def ripples(stage, d, col, per_tile=0.25, length=26, width=3):
    for x, y in points(stage, density(stage, per_tile)):
        l = stage.rng.uniform(length * 0.6, length)
        d.arc([s(x - l), s(y - l * 0.35), s(x + l), s(y + l * 0.35)], 200, 340, fill=col, width=width)


def cracks(stage, d, col, per_tile=0.08, width=3, glow=None):
    """Short jagged cracks; with `glow`, each gets a bright core line (lava seams, frost lines)."""
    r = stage.rng
    for x, y in points(stage, density(stage, per_tile)):
        pts = [(x, y)]
        a = r.uniform(0, math.tau)
        for _ in range(r.randint(3, 5)):
            a += r.uniform(-0.8, 0.8)
            l = r.uniform(10, 22)
            x, y = x + math.cos(a) * l, y + math.sin(a) * l
            pts.append((x, y))
        d.line([(s(px), s(py)) for px, py in pts], fill=col, width=width + (2 if glow else 0), joint="curve")
        if glow:
            d.line([(s(px), s(py)) for px, py in pts], fill=glow, width=max(1, width - 1), joint="curve")


# ---- Planks, bridges, fences ----------------------------------------------------------------------------------------


def planks(stage, d, mask_tiles, fill, seam, across_x):
    """Fills every tile in `mask_tiles` with planks. `across_x` = the seams run vertically (walking east-west)."""
    for x, y in mask_tiles:
        x0, y0 = x * TILE, y * TILE
        rect(d, x0, y0, x0 + TILE, y0 + TILE, fill)
        for k in (0, TILE // 2):
            if across_x:
                vline(d, x0 + k, y0, y0 + TILE, seam, 3)
            else:
                hline(d, x0, x0 + TILE, y0 + k, seam, 3)
        # nail heads, staggered per tile so the planks don't read as a grid
        off = 6 if (x + y) % 2 else TILE // 2 - 6
        for k in (4, TILE // 2 + 4):
            if across_x:
                circle(d, x0 + k + 4, y0 + off, 2, seam)
            else:
                circle(d, x0 + off, y0 + k + 4, 2, seam)


# ---- Props (footprint drawers: d, x0, y0, x1, y1, rng, **kw) ---------------------------------------------------------


def tree(d, x0, y0, x1, y1, rng, leaf=((58, 112, 62), (70, 128, 70), (50, 100, 56)), edge=(20, 38, 24),
         trunk=(96, 66, 42)):
    w, h = x1 - x0, y1 - y0
    cx = (x0 + x1) / 2
    # trunk peeks out under the crown
    rect(d, cx - w * 0.09, y1 - h * 0.34, cx + w * 0.09, y1 - 4, trunk, edge, 3)
    r = min(w, h) * 0.3
    blobs = [(cx, y0 + h * 0.36, r * 1.25)]
    for k in range(4):
        a = rng.uniform(0, math.tau)
        blobs.append((cx + math.cos(a) * w * 0.17, y0 + h * 0.4 + math.sin(a) * h * 0.12, r * rng.uniform(0.8, 1.0)))
    # keep every crown circle inside the footprint
    blobs = [(min(max(bx, x0 + br + 3), x1 - br - 3), min(max(by, y0 + br + 3), y1 - h * 0.26 - br * 0.3), br)
             for bx, by, br in blobs]
    for bx, by, br in sorted(blobs, key=lambda b: b[1]):
        c = rng.choice(leaf)
        circle(d, bx, by, br, c, edge, 3)
        circle(d, bx - br * 0.3, by - br * 0.3, br * 0.4, mix(c, (255, 255, 255), 0.15))


def dead_tree(d, x0, y0, x1, y1, rng, bark=(92, 80, 84), edge=(24, 18, 26)):
    w, h = x1 - x0, y1 - y0
    cx = (x0 + x1) / 2
    base = y1 - 6
    poly(d, [(cx - w * 0.12, base), (cx + w * 0.12, base), (cx + w * 0.05, y0 + h * 0.3), (cx - w * 0.05, y0 + h * 0.3)],
         bark, edge, 3)
    for side in (-1, 1):
        for k, (fy, reach) in enumerate(((0.55, 0.36), (0.35, 0.3))):
            sx, sy = cx + side * w * 0.04, y0 + h * fy
            ex, ey = cx + side * w * reach, y0 + h * (fy - 0.25) + rng.uniform(-4, 4)
            d.line([(s(sx), s(sy)), (s(ex), s(ey))], fill=edge, width=9)
            d.line([(s(sx), s(sy)), (s(ex), s(ey))], fill=bark, width=5)
            d.line([(s(ex), s(ey)), (s(ex + side * 8), s(ey - 10))], fill=edge, width=4)
    d.line([(s(cx), s(y0 + h * 0.32)), (s(cx + 3), s(y0 + 6))], fill=edge, width=8)
    d.line([(s(cx), s(y0 + h * 0.32)), (s(cx + 3), s(y0 + 6))], fill=bark, width=4)


def boulder(d, x0, y0, x1, y1, rng, fill=(128, 128, 136), edge=DARK, shade=None):
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    n = 8
    pts = []
    for k in range(n):
        a = k / n * math.tau + rng.uniform(-0.15, 0.15)
        pts.append((cx + math.cos(a) * (w / 2 - 4) * rng.uniform(0.82, 1.0),
                    cy + math.sin(a) * (h / 2 - 4) * rng.uniform(0.82, 1.0)))
    poly(d, pts, shade or mix(fill, (0, 0, 0), 0.25), edge, LINE)
    top = [(px, py - (py - (y0 + 4)) * 0.18 - 3) for px, py in pts]
    top = [(cx + (px - cx) * 0.8, cy - 4 + (py - cy) * 0.72) for px, py in top]
    poly(d, top, fill)
    circle(d, cx - w * 0.15, cy - h * 0.18, min(w, h) * 0.1, mix(fill, (255, 255, 255), 0.3))


def bush(d, x0, y0, x1, y1, rng, leaf=(62, 120, 60), edge=(20, 38, 24), berry=(220, 70, 80)):
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    r = min(w, h) * 0.24
    for dx, dy in ((-0.5, 0.25), (0.5, 0.25), (0, -0.2), (-0.1, 0.35)):
        circle(d, cx + dx * r * 1.1, cy + dy * r * 1.1, r, leaf, edge, 3)
    if berry:
        for _ in range(3):
            circle(d, cx + rng.uniform(-r, r), cy + rng.uniform(-r * 0.6, r * 0.6), 3, berry)


def stone_wall(d, x0, y0, x1, y1, rng, top=(214, 178, 122), face=(164, 122, 78), edge=DARK):
    """A wall seen from above-front: a lit top with block seams and a shaded south face."""
    fh = 12
    rect(d, x0, y0 + 2, x1, y1, face, edge, LINE)
    rect(d, x0, y0 + 2, x1, y1 - fh, top, edge, LINE)
    seam = mix(top, edge, 0.35)
    if x1 - x0 >= y1 - y0:
        for x in range(int(x0) + TILE, int(x1), TILE):
            vline(d, x + (8 if (x // TILE) % 2 else -8), y0 + 4, y1 - fh, seam, 2)
    else:
        for y in range(int(y0) + TILE, int(y1) - fh, TILE):
            hline(d, x0 + 3, x1 - 3, y, seam, 2)
    for cx, cy in ((x0 + 5, y0 + 7), (x1 - 5, y0 + 7)):   # chipped corners
        if rng.random() < 0.5:
            circle(d, cx, cy, 4, face)


def pillar_block(d, x0, y0, x1, y1, rng, top=(200, 170, 120), face=(160, 130, 90), edge=DARK, broken=False):
    """A square column seen from above-front: a lit cap over a darker front face."""
    w, h = x1 - x0, y1 - y0
    fy = y0 + h * (0.55 if not broken else 0.35)
    rect(d, x0 + 4, y0 + 4, x1 - 4, y1 - 4, face, edge, LINE)
    rect(d, x0 + 4, y0 + 4, x1 - 4, fy, top, edge, LINE)
    if broken:
        poly(d, [(x0 + 4, fy), (x0 + w * 0.4, fy - 8), (x0 + w * 0.7, fy + 2), (x1 - 4, fy - 6), (x1 - 4, fy)], top, edge, 3)


def column(d, x0, y0, x1, y1, rng, top=(200, 170, 120), face=(160, 130, 90), edge=DARK, broken=False):
    """A round column from above: a drum with a lit cap. Broken ones are a shorter jagged stump with rubble."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    r = min(x1 - x0, y1 - y0) / 2 - 3
    circle(d, cx, cy + 2, r, face, edge, 3)
    if broken:
        n = 7
        pts = [(cx + math.cos(k / n * math.tau) * r * rng.uniform(0.45, 0.75),
                cy - 2 + math.sin(k / n * math.tau) * r * rng.uniform(0.45, 0.75)) for k in range(n)]
        poly(d, pts, top, edge, 2)
        for _ in range(2):
            a = rng.uniform(0, math.tau)
            circle(d, cx + math.cos(a) * r * 0.8, cy + math.sin(a) * r * 0.8, 3, face, edge, 1)
    else:
        circle(d, cx, cy - 1, r * 0.78, top, edge, 2)
        circle(d, cx - r * 0.25, cy - r * 0.3, r * 0.22, mix(top, (255, 255, 255), 0.35))


def crystal(d, x0, y0, x1, y1, rng, fill=(120, 220, 245), light=(210, 250, 255), edge=(20, 40, 70)):
    """A cluster of 3-4 upright shards."""
    w, h = x1 - x0, y1 - y0
    base = y1 - 6
    shards = sorted([(x0 + w * rng.uniform(0.25, 0.75), rng.uniform(0.55, 0.9), rng.uniform(0.12, 0.2)) for _ in range(rng.randint(3, 4))],
                    key=lambda sh: -sh[1])
    for sx, tall, half in shards:
        hw = w * half
        sx = min(max(sx, x0 + hw + 3), x1 - hw - 3)
        top = base - (h - 10) * tall
        pts = [(sx - hw, base), (sx - hw, top + hw), (sx, top), (sx + hw, top + hw), (sx + hw, base)]
        poly(d, pts, fill, edge, 3)
        poly(d, [(sx - hw + 4, base - 3), (sx - hw + 4, top + hw + 2), (sx, top + 5), (sx, base - 3)], light)


def spike(d, x0, y0, x1, y1, rng, fill=(40, 30, 50), light=(110, 80, 140), edge=DARK):
    """Obsidian / stalagmite spire: one or two tall cones."""
    w, h = x1 - x0, y1 - y0
    cones = [((x0 + x1) / 2, 0.95, 0.42)]
    if w > TILE:
        cones = [(x0 + w * 0.34, 0.95, 0.3), (x0 + w * 0.7, 0.7, 0.26)]
    for cx, tall, half in cones:
        hw = w * half
        base, top = y1 - 5, y1 - 5 - (h - 8) * tall
        poly(d, [(cx - hw, base), (cx, top), (cx + hw, base)], fill, edge, 3)
        poly(d, [(cx - hw * 0.55, base - 3), (cx - 1, top + 8), (cx - 1, base - 3)], light)


def headstone(d, x0, y0, x1, y1, rng, fill=(140, 140, 150), edge=DARK):
    w, h = x1 - x0, y1 - y0
    cx = (x0 + x1) / 2
    kind = rng.randint(0, 2)
    rect(d, x0 + 3, y1 - 9, x1 - 3, y1 - 3, (70, 64, 60), edge, 2)   # little mound
    if kind == 0:     # rounded slab
        d.rounded_rectangle([s(cx - 10), s(y0 + 4), s(cx + 10), s(y1 - 8)], radius=s(9), fill=fill, outline=edge, width=3)
        hline(d, cx - 5, cx + 5, y0 + 13, mix(fill, (0, 0, 0), 0.35), 2)
    elif kind == 1:   # cross
        rect(d, cx - 4, y0 + 3, cx + 4, y1 - 8, fill, edge, 3)
        rect(d, cx - 11, y0 + 9, cx + 11, y0 + 16, fill, edge, 3)
    else:             # cracked, tilted
        poly(d, [(cx - 11, y1 - 8), (cx - 8, y0 + 7), (cx + 10, y0 + 4), (cx + 10, y1 - 8)], fill, edge, 3)
        d.line([(s(cx), s(y0 + 6)), (s(cx - 3), s(y0 + 14)), (s(cx + 2), s(y0 + 19))], fill=edge, width=2)
