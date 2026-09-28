"""Stage 2: Desert Ruins (68x40 tiles). A sand basin walled in by sandstone cliffs, an old paved road from the south
entry up to a ruined temple, a second broken ruin in the west, a pillar courtyard with an obelisk in the east and a palm
oasis in the south-east. The broken walls give ranged enemies cover for the first time."""
import math
from functools import partial
from common import Stage, TILE, LINE, GOLD, s, rect, hline, vline, circle, poly, mix
import props

BOUND, SAND, PAVE, WATER, GRASS = 0, 1, 2, 3, 4

STONE_TOP = (214, 178, 122)
STONE_FACE = (164, 122, 78)
EDGE = (46, 26, 16)


def build():
    st = Stage("desert_ruins", 68, 40, seed=22, open_codes=(SAND, PAVE, GRASS), pits={WATER: "Water"})

    # -- basin, with cliff outcrops biting into it and the entry gap in the south
    st.area(SAND, 5, 5, 63, 35)
    st.blob(SAND, 34, 20, 32, 18)
    for cx, cy, rx, ry in ((12, 4, 5, 3), (56, 3, 6, 3), (66, 20, 4, 5), (52, 37, 6, 3), (14, 36, 5, 3), (2, 12, 3, 4)):
        st.blob(BOUND, cx, cy, rx, ry)
    st.blob(BOUND, 22, 11, 3, 2)   # a small mesa between the west ruin and the temple
    st.area(SAND, 30, 34, 37, 40)
    st.spawn = (33.5, 38)

    # -- ruin floors and the old road
    st.area(PAVE, 26, 5, 42, 15)   # temple
    st.area(PAVE, 7, 18, 19, 30)   # west ruin
    st.area(PAVE, 47, 6, 61, 14)   # obelisk courtyard
    st.path(PAVE, [(33, 40), (33, 15)], 4, only={SAND})
    st.path(PAVE, [(33, 24), (19, 24)], 2, only={SAND})

    # -- oasis
    st.blob(GRASS, 50, 27, 9, 6, only={SAND})
    st.blob(WATER, 50, 27, 4, 3, only={GRASS})

    # -- temple: broken walls with a 6-wide doorway facing the road
    wall = lambda x0, x1, y: st.put("wall", x0, y, x1 - x0, 1)
    post = lambda x, y0, y1: st.put("wall", x, y0, 1, y1 - y0)
    wall(26, 33, 5); wall(36, 42, 5)
    post(26, 6, 10); post(41, 6, 8); post(41, 11, 14)
    wall(26, 31, 14); wall(37, 42, 14)
    for x, y in ((29, 8), (29, 11), (38, 8), (38, 11)):
        st.put("pillar", x, y)
    st.put("altar", 32, 8, 4, 2)
    # -- west ruin
    wall(7, 13, 18); post(7, 19, 24); wall(12, 19, 29); post(18, 22, 26)
    for x, y, broken in ((10, 21, True), (15, 21, False), (10, 26, False), (15, 26, True)):
        st.put("pillar", x, y, broken=broken)
    # -- courtyard
    for x, y in ((49, 8), (58, 8), (49, 12), (58, 12)):
        st.put("pillar", x, y)
    st.put("obelisk", 53, 9, 2, 2)

    clear = [(28, 32, 39, 40)]
    for x, y in ((42, 25), (56, 25), (44, 30), (54, 30), (45, 21)):
        st.try_put("palm", x, y, 2, 2)
    st.scatter("cactus", 9, {SAND}, margin=2, keep_clear=clear)
    st.scatter("rock", 6, {SAND}, sizes=((1, 1), (2, 2)), margin=2, keep_clear=clear)
    st.seal_pockets()
    st.pick_enemy_spawns(14)

    render(st)
    return st


# ---- props ---------------------------------------------------------------------------------------------------------


def altar(d, x0, y0, x1, y1, rng):
    rect(d, x0 + 4, y0 + 6, x1 - 4, y1 - 2, STONE_FACE, EDGE, LINE)
    rect(d, x0 + 4, y0 + 6, x1 - 4, y1 - 16, STONE_TOP, EDGE, LINE)
    cx = (x0 + x1) / 2
    circle(d, cx, (y0 + y1 - 10) / 2, 9, GOLD, EDGE, 3)
    for side in (-1, 1):
        hline(d, cx + side * 20, cx + side * 44, y1 - 9, GOLD, 3)


def obelisk(d, x0, y0, x1, y1, rng):
    cx = (x0 + x1) / 2
    rect(d, x0 + 6, y1 - 20, x1 - 6, y1 - 3, STONE_FACE, EDGE, LINE)            # plinth
    poly(d, [(cx - 14, y1 - 20), (cx - 10, y0 + 16), (cx, y0 + 4), (cx + 10, y0 + 16), (cx + 14, y1 - 20)], STONE_TOP, EDGE, LINE)
    poly(d, [(cx, y0 + 4), (cx + 10, y0 + 16), (cx + 14, y1 - 20), (cx + 2, y1 - 20), (cx + 2, y0 + 10)], mix(STONE_TOP, EDGE, 0.2))
    for k in range(3):   # glyphs
        circle(d, cx - 4, y0 + 24 + k * 10, 2, EDGE)
    circle(d, cx, y0 + 7, 3, GOLD)


def palm(d, x0, y0, x1, y1, rng):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2 - 4
    rect(d, cx - 5, cy, cx + 5, y1 - 4, (130, 92, 56), EDGE, 3)
    leaf, leaf_dark = (96, 160, 70), (64, 120, 50)
    for k in range(6):
        a = k / 6 * math.tau + rng.uniform(-0.2, 0.2)
        l = (x1 - x0) * 0.44
        tip = (cx + math.cos(a) * l, cy + math.sin(a) * l * 0.8)
        side = (-math.sin(a) * 7, math.cos(a) * 7)
        mid = (cx + math.cos(a) * l * 0.5, cy + math.sin(a) * l * 0.4)
        poly(d, [(cx, cy), (mid[0] + side[0], mid[1] + side[1]), tip, (mid[0] - side[0], mid[1] - side[1])],
             leaf if k % 2 else leaf_dark, (30, 60, 26), 3)
    circle(d, cx, cy, 6, (150, 104, 50), EDGE, 2)


def cactus(d, x0, y0, x1, y1, rng):
    cx = (x0 + x1) / 2
    body, dark = (92, 150, 84), (30, 64, 36)
    d.rounded_rectangle([s(cx - 5), s(y0 + 3), s(cx + 5), s(y1 - 4)], radius=s(5), fill=body, outline=dark, width=3)
    for side, hy in ((-1, 12), (1, 8)):
        ax = cx + side * 11
        d.rounded_rectangle([s(min(cx, ax) - 3), s(y0 + hy + 8), s(max(cx, ax) + 3), s(y0 + hy + 13)], radius=s(3), fill=body, outline=dark, width=2)
        d.rounded_rectangle([s(ax - 3), s(y0 + hy), s(ax + 3), s(y0 + hy + 13)], radius=s(3), fill=body, outline=dark, width=2)
    vline(d, cx, y0 + 7, y1 - 8, mix(body, (255, 255, 255), 0.25), 2)


# ---- render --------------------------------------------------------------------------------------------------------
# The painters are shared with desert_pyramid.py so both desert maps look like the same place.


def paint_cliffs(st):
    """Sandstone strata and cracks over the whole impassable surround."""
    rng = st.rng
    lay, d = st.layer((150, 96, 60))
    y = 0
    while y < st.H:
        pts, x = [], 0
        while x <= st.W + 40:
            pts.append((x, y + rng.uniform(-5, 5))); x += 40
        d.line(pts, fill=rng.choice([(132, 82, 50), (170, 112, 70), (120, 74, 46)]), width=rng.randint(4, 9))
        y += rng.randint(14, 26)
    props.cracks(st, d, (96, 58, 36), 0.12, 3)
    st.paint(lay, st.mask(BOUND))


def paint_sand(st, *codes):
    lay, d = st.layer((226, 194, 136))
    props.tile_grid(st, d, (219, 187, 130))
    props.ripples(st, d, (206, 172, 116), 0.45, 30, 3)
    props.specks(st, d, [(196, 160, 106), (238, 212, 160)], 0.4, 2, 3)
    st.paint(lay, st.mask(*codes))


def paint_flagstones(st, *codes):
    """A grid of worn slabs, some cracked or sand-filled."""
    rng = st.rng
    lay, d = st.layer((196, 168, 120))
    props.tile_grid(st, d, (176, 148, 104), 2)
    for x, y in props.points(st, props.density(st, 0.06)):
        tx, ty = int(x // TILE) * TILE, int(y // TILE) * TILE
        if rng.random() < 0.5:
            rect(d, tx + 3, ty + 3, tx + TILE - 3, ty + TILE - 3, (220, 188, 132))                     # sand-filled gap
        else:
            d.line([(tx + 6, ty + 8), (tx + 16, ty + 18), (tx + 26, ty + 14)], fill=(150, 124, 86), width=2)
    st.paint(lay, st.mask(*codes))


def paint_oasis(st, grass, water):
    lay, d = st.layer((128, 164, 80))
    props.tile_grid(st, d, (120, 156, 74))
    props.tufts(st, d, (150, 188, 96), 1.0)
    st.paint(lay, st.mask(grass))

    lay, d = st.layer((58, 170, 190))
    props.ripples(st, d, (150, 220, 230), 0.6, 20)
    st.paint(lay, st.mask(water))
    st.outline(st.mask(water), (40, 132, 160), 10)


def cliff_faces(st):
    """Cliff faces two tiles deep with vertical striations, above every open tile."""
    rng = st.rng

    def face(d, x0, x1, y0, y1):
        rect(d, x0, y0, x1, y1, (178, 114, 70))
        hline(d, x0, x1, y0 + 3, (204, 146, 94), 5)
        x = x0 + rng.uniform(4, 16)
        while x < x1 - 4:
            vline(d, x, y0 + 10, y1 - rng.uniform(4, 16), (140, 86, 52), 3)
            x += rng.uniform(12, 26)
        hline(d, x0, x1, y1 - 3, (120, 72, 44), 5)
    st.faces({BOUND}, set(st.open_codes), 2, face)


def render(st):
    paint_cliffs(st)
    paint_sand(st, SAND)
    paint_flagstones(st, PAVE)
    paint_oasis(st, GRASS, WATER)
    cliff_faces(st)

    st.outline(st.mask(SAND, PAVE, GRASS))

    st.draw_objects({
        "wall": partial(props.stone_wall, top=STONE_TOP, face=STONE_FACE, edge=EDGE),
        "pillar": partial(props.column, top=STONE_TOP, face=STONE_FACE, edge=EDGE),
        "altar": altar,
        "obelisk": obelisk,
        "palm": palm,
        "cactus": cactus,
        "rock": partial(props.boulder, fill=(176, 130, 88), edge=EDGE),
    })
