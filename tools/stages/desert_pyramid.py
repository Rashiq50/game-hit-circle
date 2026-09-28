"""Stage 4: Desert Pyramid (74x44 tiles). The second desert map, laid out unlike the ruins: the entry is on the east edge
and a half-buried road runs west to a stepped pyramid on its paved plaza. A dry, cracked riverbed winds across the south,
two rock mesas stand in the open as cover, quicksand pits swallow a few corners, and a caravan has camped in the
north-east around a well."""
import math
from common import Stage, TILE, LINE, GOLD, rect, hline, vline, circle, poly, mix
import props
from desert_ruins import (STONE_TOP, STONE_FACE, EDGE, obelisk, palm, cactus,
                          paint_cliffs, paint_sand, paint_flagstones, cliff_faces)

BOUND, SAND, PAVE, CLAY, QUICK = 0, 1, 2, 3, 4

CANVAS, CANVAS_DARK, STRIPE = (232, 218, 188), (190, 170, 136), (178, 70, 52)
WOOD, WOOD_DARK = (150, 110, 70), (96, 66, 40)


def build():
    st = Stage("desert_pyramid", 74, 44, seed=23, open_codes=(SAND, PAVE, CLAY), pits={QUICK: "Quicksand"})

    # -- the basin, cliff outcrops biting into its rim, the entry gap on the east edge
    st.area(SAND, 5, 5, 69, 39)
    st.blob(SAND, 37, 22, 34, 20)
    for cx, cy, rx, ry in ((20, 2, 7, 3), (52, 2, 5, 3), (3, 28, 3, 5), (40, 42, 8, 3), (66, 39, 5, 3), (12, 41, 5, 3),
                           (70, 8, 3, 4)):
        st.blob(BOUND, cx, cy, rx, ry)
    st.area(SAND, 66, 19, 74, 26)
    st.spawn = (72, 22.5)
    # -- mesas standing out in the basin
    st.blob(BOUND, 33, 11, 3, 2)
    st.blob(BOUND, 50, 31, 4, 3)

    # -- the dry riverbed across the south, then quicksand in the sand and the clay
    st.path(CLAY, [(10, 44), (16, 34), (26, 27), (36, 28), (43, 36), (47, 44)], 4, only={SAND})
    for cx, cy, rx, ry in ((22, 35, 3, 2), (60, 27, 3, 2), (43, 8, 3, 2), (8, 22, 2, 2)):
        st.blob(QUICK, cx, cy, rx, ry, only={SAND, CLAY})

    # -- the plaza, then the road from the entry to it, half buried by drifts
    st.path(PAVE, [(74, 22), (58, 22), (44, 19), (30, 18), (20, 16), (12, 16)], 3, only={SAND, CLAY})
    for x, y in [(x, y) for y in range(st.th) for x in range(20, st.tw) if st.code(x, y) == PAVE]:
        if st.rng.random() < 0.14:
            st.ground.putpixel((x, y), SAND)
    st.area(PAVE, 6, 5, 20, 17)

    st.put("pyramid", 8, 5, 10, 8)
    st.put("obelisk", 20, 12, 2, 2)
    st.put("obelisk", 20, 18, 2, 2)
    for x, y in ((7, 14), (18, 14)):
        st.put("pillar", x, y, broken=True)

    # -- caravan camp
    st.put("tent", 55, 7, 3, 2)
    st.put("tent", 61, 7, 3, 2)
    st.put("tent", 63, 13, 3, 2)
    st.put("well", 54, 12, 2, 2)
    st.put("campfire", 59, 11)
    st.put("crate", 66, 8); st.put("crate", 66, 9)
    st.put("crate", 58, 15)
    for x, y in ((51, 10), (51, 15)):
        st.try_put("palm", x, y, 2, 2)

    clear = [(62, 17, 74, 28), (4, 4, 22, 21), (50, 5, 69, 18)]  # the entry, the plaza and the camp
    st.scatter("cactus", 10, {SAND}, margin=2, keep_clear=clear)
    st.scatter("rock", 6, {SAND}, sizes=((1, 1), (2, 2)), margin=2, keep_clear=clear)
    st.scatter("bones", 4, {SAND, CLAY}, sizes=((2, 1),), margin=2, keep_clear=clear)
    st.seal_pockets()
    st.pick_enemy_spawns(16)

    render(st)
    return st


# ---- props ---------------------------------------------------------------------------------------------------------


def pyramid(d, x0, y0, x1, y1, rng):
    """A stepped pyramid seen from above-front: five tiers, each a lit top over a shaded riser, a doorway at the foot and
    a gilded capstone."""
    for i in range(5):
        bx0, by0, bx1, by1 = x0 + 4 + i * 28, y0 + 4 + i * 16, x1 - 4 - i * 28, y1 - 4 - i * 36
        rect(d, bx0, by0, bx1, by1, STONE_FACE, EDGE, LINE)
        rect(d, bx0, by0, bx1, by1 - 16, STONE_TOP, EDGE, LINE)
        for x in range(int(bx0) + 20, int(bx1) - 8, 24):   # block seams on the riser
            vline(d, x + (6 if i % 2 else 0), by1 - 14, by1 - 2, mix(STONE_FACE, EDGE, 0.35), 2)
    cx = (x0 + x1) / 2
    rect(d, cx - 18, y0 + 4 + 4 * 16 + 6, cx + 18, y1 - 4 - 4 * 36 - 20, GOLD, EDGE, 3)
    # doorway cut up through the lowest tiers
    rect(d, cx - 18, y1 - 52, cx + 18, y1 - 4, (40, 26, 20), EDGE, 3)
    rect(d, cx - 24, y1 - 58, cx + 24, y1 - 50, STONE_TOP, EDGE, 3)   # lintel
    circle(d, cx, y1 - 54, 3, GOLD)


def tent(d, x0, y0, x1, y1, rng):
    """A ridge tent seen from above-front: two canvas slopes over a dark doorway, with a striped hem."""
    cx = (x0 + x1) / 2
    ridge = y0 + 10
    poly(d, [(x0 + 6, y1 - 8), (x0 + 22, ridge), (x1 - 22, ridge), (x1 - 6, y1 - 8)], CANVAS, EDGE, LINE)
    poly(d, [(x0 + 6, y1 - 8), (x0 + 22, ridge), (cx, ridge), (cx - 10, y1 - 8)], CANVAS_DARK, EDGE, 3)
    hline(d, x0 + 10, x1 - 10, y1 - 12, STRIPE, 5)
    poly(d, [(cx - 12, y1 - 8), (cx, ridge + 14), (cx + 12, y1 - 8)], (60, 40, 30), EDGE, 3)
    for px in (x0 + 22, x1 - 22):   # poles
        circle(d, px, ridge, 3, WOOD_DARK)


def well(d, x0, y0, x1, y1, rng):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2 + 4
    circle(d, cx, cy, 24, STONE_FACE, EDGE, LINE)
    circle(d, cx, cy - 2, 20, STONE_TOP, EDGE, 3)
    circle(d, cx, cy - 2, 12, (40, 110, 130), EDGE, 2)
    for side in (-1, 1):   # windlass posts and beam
        vline(d, cx + side * 20, y0 + 6, cy - 8, WOOD_DARK, 5)
    hline(d, cx - 22, cx + 22, y0 + 8, WOOD, 5)
    vline(d, cx, y0 + 10, cy - 8, (200, 180, 140), 2)


def campfire(d, x0, y0, x1, y1, rng):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2 + 3
    for k in range(7):
        a = k / 7 * math.tau
        circle(d, cx + math.cos(a) * 11, cy + math.sin(a) * 7, 4, (130, 120, 112), EDGE, 2)
    d.line([(cx - 8, cy + 3), (cx + 8, cy - 3)], fill=WOOD_DARK, width=5)
    d.line([(cx - 8, cy - 3), (cx + 8, cy + 3)], fill=WOOD_DARK, width=5)
    poly(d, [(cx - 6, cy + 2), (cx - 2, cy - 12), (cx + 1, cy - 5), (cx + 4, cy - 14), (cx + 7, cy + 2)], (255, 150, 50), EDGE, 2)
    poly(d, [(cx - 3, cy + 1), (cx, cy - 6), (cx + 4, cy + 1)], (255, 230, 120))


def crate(d, x0, y0, x1, y1, rng):
    rect(d, x0 + 4, y0 + 4, x1 - 4, y1 - 4, WOOD, EDGE, 3)
    d.line([(x0 + 5, y0 + 5), (x1 - 5, y1 - 5)], fill=WOOD_DARK, width=3)
    d.line([(x1 - 5, y0 + 5), (x0 + 5, y1 - 5)], fill=WOOD_DARK, width=3)


def bones(d, x0, y0, x1, y1, rng):
    """A bleached ribcage and horned skull half sunk in the sand."""
    bone = (236, 228, 206)
    ym = (y0 + y1) / 2 + 2
    hline(d, x0 + 10, x1 - 22, ym, EDGE, 7)
    hline(d, x0 + 10, x1 - 22, ym, bone, 3)
    for x in range(int(x0) + 14, int(x1) - 24, 7):
        d.arc([x - 5, ym - 12, x + 5, ym + 12], 200, 340, fill=EDGE, width=5)
        d.arc([x - 5, ym - 12, x + 5, ym + 12], 200, 340, fill=bone, width=2)
    sx = x1 - 14
    circle(d, sx, ym, 8, bone, EDGE, 2)
    circle(d, sx - 3, ym - 1, 2, EDGE); circle(d, sx + 3, ym - 1, 2, EDGE)
    for side in (-1, 1):
        d.line([(sx + side * 6, ym - 5), (sx + side * 12, ym - 12)], fill=EDGE, width=5)
        d.line([(sx + side * 6, ym - 5), (sx + side * 12, ym - 12)], fill=bone, width=2)


# ---- render --------------------------------------------------------------------------------------------------------


def render(st):
    rng = st.rng
    paint_cliffs(st)
    paint_sand(st, SAND)
    paint_flagstones(st, PAVE)

    # dry riverbed: baked clay split into plates
    lay, d = st.layer((190, 146, 100))
    props.tile_grid(st, d, (182, 140, 96))
    props.cracks(st, d, (138, 98, 66), 0.9, 2)
    props.specks(st, d, [(170, 128, 88), (212, 176, 128)], 0.3, 2, 4)
    st.paint(lay, st.mask(CLAY))

    # quicksand: darker, wetter sand with sinking swirls and a trampled rim
    lay, d = st.layer((178, 140, 92))
    for x, y in props.points(st, props.density(st, 0.35)):
        for r in (6, 12, 18):
            a = rng.uniform(0, 360)
            d.arc([x - r, y - r * 0.6, x + r, y + r * 0.6], a, a + 250, fill=(150, 114, 74), width=3)
    st.paint(lay, st.mask(QUICK))
    st.outline(st.mask(QUICK), (150, 112, 72), 10)

    cliff_faces(st)
    st.outline(st.mask(SAND, PAVE, CLAY))

    st.draw_objects({
        "pyramid": pyramid,
        "obelisk": obelisk,
        "pillar": lambda d, x0, y0, x1, y1, rng, broken=False:
            props.column(d, x0, y0, x1, y1, rng, top=STONE_TOP, face=STONE_FACE, edge=EDGE, broken=broken),
        "tent": tent,
        "well": well,
        "campfire": campfire,
        "crate": crate,
        "palm": palm,
        "cactus": cactus,
        "rock": lambda d, x0, y0, x1, y1, rng: props.boulder(d, x0, y0, x1, y1, rng, fill=(176, 130, 88), edge=EDGE),
        "bones": bones,
    })
    st.glow([(59.5 * TILE, 11.5 * TILE)], 90, (255, 170, 80), 0.35)
