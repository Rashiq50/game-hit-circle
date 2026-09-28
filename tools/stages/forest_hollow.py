"""Stage 2: Forest Hollow (64x36 tiles). The second forest map, laid out unlike the glade: the trail comes in from the west
edge through a meadow with a fallen log and a fairy ring, then a wide waist between two canopy spurs opens into the
hollow, where a ring of mossy standing stones circles an ancient stump. A brook runs out of the north treeline into a
pond in the north-east, with the trail's spur ending on its bank. Autumn crowns are mixed into the canopy. Still mostly open ground, since enemies chase in
straight lines, but the stones and stumps give the first Imp something to hide behind."""
import math
from functools import partial
from PIL import ImageFilter
from common import Stage, TILE, LINE, rect, hline, circle, poly, mix
import props

BOUND, GRASS, DIRT, WATER, MOSS = 0, 1, 2, 3, 4

LEAF = ((48, 98, 54), (58, 112, 60), (42, 88, 48), (66, 120, 62))
AUTUMN = ((196, 124, 52), (214, 156, 62), (172, 88, 44))
EDGE = (18, 32, 20)
STONE_TOP, STONE_FACE = (150, 154, 142), (104, 108, 100)
MOSS_COL = (86, 132, 60)
BARK, WOOD, RING = (104, 72, 46), (176, 138, 92), (140, 104, 66)


def build():
    st = Stage("forest_hollow", 64, 36, seed=12, open_codes=(GRASS, DIRT, MOSS), pits={WATER: "Water"})

    # -- the west meadow and the bigger east hollow, joined by a waist between a north and a south canopy spur
    st.blob(GRASS, 16, 18, 12, 11)
    st.area(GRASS, 7, 11, 24, 26)
    st.blob(GRASS, 44, 18, 17, 13)
    st.area(GRASS, 34, 9, 56, 28)
    st.area(GRASS, 22, 12, 36, 25)
    st.blob(BOUND, 29, 6, 5, 6)    # north spur
    st.blob(BOUND, 27, 31, 4, 6)   # south spur
    st.blob(GRASS, 57, 27, 5, 4)   # south-east nook
    st.blob(GRASS, 10, 8, 5, 4)    # north-west corner of the meadow
    # -- entry from the west edge
    st.area(GRASS, 0, 16, 6, 22)
    st.spawn = (2.5, 19)

    # -- trail from the entry east to the stones, with a spur up to the pond's bank
    st.path(DIRT, [(0, 19), (7, 19), (14, 21), (22, 19), (30, 18), (37, 18)], 3, only={GRASS})
    st.path(DIRT, [(42, 12), (45, 10), (49, 10)], 2, only={GRASS})
    # -- brook out of the north treeline into the pond
    st.path(WATER, [(53, 0), (52, 3), (54, 7)], 2, only={BOUND, GRASS})
    st.blob(WATER, 55, 10, 5, 3, only={GRASS, DIRT})
    # -- moss floor inside the stone ring
    st.blob(MOSS, 44, 19, 8, 6, only={GRASS, DIRT})

    # -- standing stones around the ancient stump; every gap is at least 3 tiles so a brute fits through
    st.put("stump", 43, 18, 3, 2)
    for a in range(0, 360, 45):
        if a == 180:
            continue                                   # the trail's way in
        x = int(round(44 + math.cos(math.radians(a)) * 7))
        y = int(round(18 + math.sin(math.radians(a)) * 5))
        st.put("menhir", x, y, 1, 2)
    # -- landmarks
    st.put("log", 9, 24, 5, 1)
    st.put("log", 51, 25, 4, 1)
    st.put("old_tree", 17, 10, 3, 3)
    st.put("old_tree", 36, 26, 3, 3, leaf=AUTUMN)
    st.put("stump", 57, 27, 2, 2)
    st.put("boulder", 24, 14, 2, 2)

    clear = [(0, 15, 9, 23), (36, 15, 52, 23)]  # the entry and the stone ring
    st.scatter("tree", 5, {GRASS}, sizes=((2, 2), (3, 3)), margin=2, keep_clear=clear)
    st.scatter("autumn_tree", 3, {GRASS}, sizes=((2, 2),), margin=2, keep_clear=clear)
    st.scatter("stump", 3, {GRASS}, sizes=((1, 1),), margin=2, keep_clear=clear)
    st.scatter("boulder", 3, {GRASS}, sizes=((1, 1), (2, 1)), margin=2, keep_clear=clear)
    st.scatter("fern", 6, {GRASS}, margin=2, keep_clear=clear)
    st.seal_pockets()
    st.pick_enemy_spawns(12)

    render(st)
    return st


# ---- props ---------------------------------------------------------------------------------------------------------


def menhir(d, x0, y0, x1, y1, rng):
    """A standing stone seen from above-front: a rounded lit top over a shaded face, with moss creeping up the base."""
    w = x1 - x0
    top = y0 + 6
    d.rounded_rectangle([x0 + 4, top, x1 - 4, y1 - 4], radius=10, fill=STONE_FACE, outline=EDGE, width=LINE)
    d.rounded_rectangle([x0 + 4, top, x1 - 4, top + (y1 - top) * 0.45], radius=10, fill=STONE_TOP, outline=EDGE, width=3)
    for _ in range(3):   # moss patches
        circle(d, x0 + w * rng.uniform(0.3, 0.7), y1 - rng.uniform(8, 18), rng.uniform(4, 6), MOSS_COL)
    hline(d, x0 + 10, x1 - 12, top + 12, mix(STONE_TOP, EDGE, 0.3), 2)   # a carved notch


def stump(d, x0, y0, x1, y1, rng):
    """A sawn stump from above-front: bark sides and a lit top with growth rings; big ones get roots."""
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, y0 + h * 0.42
    rx, ry = w / 2 - 5, h * 0.3
    if w > TILE:
        for side in (-1, 1):
            poly(d, [(cx + side * rx * 0.5, y1 - 14), (cx + side * (rx + 3), y1 - 5), (cx + side * rx * 0.3, y1 - 6)],
                 BARK, EDGE, 3)
    rect(d, cx - rx, cy, cx + rx, y1 - 8, BARK, EDGE, 3)
    d.ellipse([cx - rx, y1 - 8 - ry, cx + rx, y1 - 8 + ry * 0.6], fill=BARK, outline=EDGE, width=3)
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=WOOD, outline=EDGE, width=3)
    for k in (0.65, 0.35):
        d.ellipse([cx - rx * k, cy - ry * k, cx + rx * k, cy + ry * k], outline=RING, width=2)
    if w > TILE:   # a crack across the old one
        d.line([(cx - rx * 0.1, cy - ry), (cx + rx * 0.15, cy - ry * 0.2), (cx, cy + ry * 0.3)], fill=EDGE, width=3)


def log(d, x0, y0, x1, y1, rng):
    """A fallen trunk lying east-west, with a cut end showing its rings and a few moss tufts along the top."""
    ym = (y0 + y1) / 2
    r = (y1 - y0) / 2 - 3
    rect(d, x0 + r, ym - r, x1 - r - 4, ym + r, BARK, EDGE, 3)
    circle(d, x0 + r + 2, ym, r, BARK, EDGE, 3)
    hline(d, x0 + r, x1 - r - 6, ym - r * 0.35, mix(BARK, (255, 255, 255), 0.15), 3)
    hline(d, x0 + r + 8, x1 - r - 12, ym + r * 0.4, mix(BARK, (0, 0, 0), 0.3), 2)
    d.ellipse([x1 - 2 * r - 6, ym - r, x1 - 4, ym + r], fill=WOOD, outline=EDGE, width=3)
    d.ellipse([x1 - r - 10, ym - r * 0.45, x1 - r, ym + r * 0.45], outline=RING, width=2)
    x = x0 + r + rng.uniform(6, 16)
    while x < x1 - 2 * r - 10:
        circle(d, x, ym - r + 3, rng.uniform(3, 5), MOSS_COL)
        x += rng.uniform(18, 34)


def fern(d, x0, y0, x1, y1, rng, leaf=(54, 110, 56)):
    """A low fan of fronds."""
    cx, by = (x0 + x1) / 2, y1 - 6
    for k in range(5):
        a = math.radians(200 + k * 35)
        ex, ey = cx + math.cos(a) * 13, by + math.sin(a) * 18
        d.line([(cx, by), (ex, ey)], fill=EDGE, width=7)
        d.line([(cx, by), (ex, ey)], fill=mix(leaf, (255, 255, 255), 0.1 * (k % 2)), width=4)


def toadstools(st, d, per_tile):
    """Tiny red-capped mushrooms scattered through the grass (floor decoration only, no collision)."""
    for x, y in props.points(st, props.density(st, per_tile)):
        for dx, dy, r in ((0, 0, 4), (7, 3, 3)):
            rect(d, x + dx - 1, y + dy, x + dx + 1, y + dy + 5, (230, 222, 200))
            d.pieslice([x + dx - r, y + dy - r, x + dx + r, y + dy + r], 180, 360, fill=(200, 54, 48), outline=EDGE)


def fairy_ring(d, cx, cy, radius):
    for k in range(11):
        a = k / 11 * math.tau
        x, y = cx + math.cos(a) * radius, cy + math.sin(a) * radius * 0.7
        rect(d, x - 1, y, x + 1, y + 5, (230, 222, 200))
        d.pieslice([x - 5, y - 5, x + 5, y + 5], 180, 360, fill=(226, 214, 180), outline=EDGE)


# ---- rendering -----------------------------------------------------------------------------------------------------


def render(st):
    rng = st.rng
    # forest surround, one crown in four turning
    lay, d = st.layer((24, 44, 30))
    props.canopy(st, d, LEAF * 3 + AUTUMN, EDGE)
    st.paint(lay, st.mask(BOUND))

    # grass, a shade deeper than the glade's, with fallen leaves and toadstools
    lay, d = st.layer((92, 142, 70))
    props.tile_grid(st, d, (86, 134, 66))
    props.tufts(st, d, (116, 166, 84), 0.7)
    props.specks(st, d, list(AUTUMN) + [(236, 214, 110)], 0.15, 2, 4)
    toadstools(st, d, 0.03)
    fairy_ring(d, 12 * TILE, 14 * TILE, 64)
    st.paint(lay, st.mask(GRASS))

    # moss floor of the stone ring
    lay, d = st.layer((78, 118, 64))
    props.tile_grid(st, d, (72, 110, 60))
    props.specks(st, d, [(98, 142, 72), (64, 100, 54)], 1.2, 3, 6)
    st.paint(lay, st.mask(MOSS))

    # dirt trail
    lay, d = st.layer((150, 116, 78))
    props.tile_grid(st, d, (140, 108, 72))
    props.specks(st, d, [(122, 94, 64), (174, 142, 102)], 0.9, 2, 4)
    st.paint(lay, st.mask(DIRT))

    # brook and pond
    lay, d = st.layer((66, 130, 178))
    props.ripples(st, d, (124, 182, 220), 0.5)
    pads = st.mask(WATER).filter(ImageFilter.MinFilter(29))   # lily pads keep 14px clear of the banks
    for x, y in props.points(st, props.density(st, 0.6)):
        if pads.getpixel((int(x), int(y))):
            d.pieslice([x - 9, y - 7, x + 9, y + 7], 30, 330, fill=(70, 128, 62), outline=EDGE)
    st.paint(lay, st.mask(WATER))
    st.outline(st.mask(WATER), (50, 104, 150), 10)

    # treeline face: the canopy's shadowed underside with trunks, wherever it meets open ground
    def face(d, x0, x1, y0, y1):
        rect(d, x0, y1 - TILE, x1, y1, (20, 36, 24))
        x = x0 + rng.uniform(8, 24)
        while x < x1 - 10:
            w = rng.uniform(9, 14)
            rect(d, x - w / 2, y1 - 26, x + w / 2, y1, (92, 64, 42), EDGE, 3)
            x += rng.uniform(34, 60)
        for x in range(int(x0) + 12, int(x1) - 6, 28):   # crown hems overhanging the trunks
            circle(d, x, y1 - TILE, 14, mix(rng.choice(LEAF * 3 + AUTUMN), (0, 0, 0), 0.25))
    st.faces({BOUND}, {GRASS, DIRT, MOSS}, 1, face)

    st.outline(st.mask(GRASS, DIRT, MOSS))

    st.draw_objects({
        "tree": partial(props.tree, leaf=LEAF, edge=EDGE),
        "autumn_tree": partial(props.tree, leaf=AUTUMN, edge=EDGE),
        "old_tree": lambda d, x0, y0, x1, y1, rng, leaf=LEAF: props.tree(d, x0, y0, x1, y1, rng, leaf=leaf, edge=EDGE,
                                                                            trunk=(80, 56, 38)),
        "boulder": props.boulder,
        "stump": stump,
        "menhir": menhir,
        "log": log,
        "fern": fern,
    })

