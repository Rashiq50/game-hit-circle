"""Stage 6: Swamp Mire (86x52 tiles). The second swamp map, laid out unlike the bog's islands: one broad mud mainland entered
from the west edge, with a sluggish river along the north crossed by three boardwalk bridges. Past the river, a sunken
shrine's mossy walls ring a stone idol. On the mainland a giant mangrove stands in the middle, bog pools pock the mud, a
stilt village sits over a lagoon in the south-east, and fireflies and glowcaps light the gloom. There is more open ground
than on the bog, so bigger mixed waves can come at the player from every side."""
import math
from functools import partial
from common import Stage, TILE, LINE, rect, hline, vline, circle, poly, mix
import props
from swamp_bog import (LEAF, EDGE, WOOD, WOOD_DARK, hut, stump,
                       paint_canopy, paint_water, paint_mud, paint_boards, reeds, treeline_faces, board_posts)

BOUND, MUD, WATER, BOARD = 0, 1, 2, 3

MOSS_TOP, MOSS_FACE = (120, 130, 108), (82, 90, 76)
GLOW = (190, 255, 120)


def build():
    st = Stage("swamp_mire", 86, 52, seed=34, open_codes=(MUD, BOARD), pits={WATER: "Water"})

    # -- the mainland, its rim bitten by the treeline, the entry on the west edge
    st.area(MUD, 4, 4, 82, 48)
    st.blob(MUD, 43, 26, 41, 24)
    for cx, cy, rx, ry in ((4, 4, 6, 5), (82, 49, 6, 5), (84, 4, 4, 6), (2, 49, 5, 4), (30, 51, 7, 3), (60, 1, 6, 3),
                           (84, 28, 3, 5)):
        st.blob(BOUND, cx, cy, rx, ry)
    st.area(MUD, 0, 33, 6, 40)
    st.spawn = (2.5, 36.5)

    # -- the river out of the west treeline and back into the east one, and bog pools on the mainland
    st.path(WATER, [(0, 18), (14, 16), (28, 19), (44, 16), (60, 19), (74, 15), (86, 17)], 6, only={BOUND, MUD})
    for cx, cy, rx, ry in ((24, 33, 4, 3), (38, 41, 3, 2), (56, 29, 3, 2), (13, 44, 3, 2), (48, 45, 3, 2)):
        st.blob(WATER, cx, cy, rx, ry, only={MUD})
    st.blob(WATER, 69, 39, 11, 7, only={MUD})   # the village lagoon

    # -- boardwalks: three bridges over the river, a fishing pier, and the stilt village's platforms and walks
    for x in (16, 46, 72):
        st.path(BOARD, [(x, 9), (x, 26)], 4, only={WATER})
    st.area(BOARD, 32, 19, 35, 25, only={WATER})   # fishing pier
    # each platform is exactly a hut wide, with three rows of deck in front so nothing is left as a sliver
    st.area(BOARD, 63, 33, 68, 39, only={WATER})
    st.area(BOARD, 68, 36, 71, 42, only={WATER})
    st.area(BOARD, 71, 36, 76, 42, only={WATER})
    st.area(BOARD, 55, 36, 63, 39, only={WATER})

    # -- sunken shrine on the north bank: a broken U of mossy walls, open to the river, around the idol
    wall = lambda x0, x1, y: st.put("wall", x0, y, x1 - x0, 1)
    post = lambda x, y0, y1: st.put("wall", x, y0, 1, y1 - y0)
    wall(38, 44, 5); wall(48, 55, 5)
    post(38, 6, 10); post(54, 6, 9)
    st.put("idol", 45, 7, 2, 2)
    for x, y in ((41, 8), (51, 8)):
        st.put("pillar", x, y, broken=True)

    # -- the mainland's landmarks and the village huts
    st.put("mangrove", 40, 27, 5, 4)
    st.put("hut", 63, 33, 5, 3)
    st.put("hut", 71, 36, 5, 3)
    st.try_put("barrel", 57, 34, margin=1)   # on the shore by the walkway

    clear = [(0, 31, 10, 42), (36, 25, 49, 33), (54, 32, 80, 46), (36, 4, 57, 13)]
    st.scatter("dead_tree", 10, {MUD}, sizes=((2, 2),), margin=2, keep_clear=clear)
    st.scatter("tree", 8, {MUD}, sizes=((2, 2), (3, 3)), margin=2, keep_clear=clear)
    st.scatter("stump", 8, {MUD}, margin=2, keep_clear=clear)
    st.scatter("glowcap", 7, {MUD}, margin=2, keep_clear=clear)
    st.seal_pockets()
    st.pick_enemy_spawns(18)

    render(st)
    return st


# ---- props ---------------------------------------------------------------------------------------------------------


def mangrove(d, x0, y0, x1, y1, rng):
    """A giant mangrove: arching stilt roots under a heavy, drooping crown."""
    w, h = x1 - x0, y1 - y0
    cx = (x0 + x1) / 2
    base = y1 - 6
    trunk_top = y0 + h * 0.45
    bark = (86, 70, 56)
    for k in range(-3, 4):   # stilt roots arching down from the trunk
        ex = cx + k * w * 0.13
        d.arc([min(cx, ex) - 6, trunk_top + 10, max(cx, ex) + 6, base * 2 - trunk_top - 10], 180 if k < 0 else 270,
              270 if k < 0 else 360, fill=EDGE, width=9)
        d.arc([min(cx, ex) - 6, trunk_top + 10, max(cx, ex) + 6, base * 2 - trunk_top - 10], 180 if k < 0 else 270,
              270 if k < 0 else 360, fill=bark, width=5)
    rect(d, cx - 13, trunk_top, cx + 13, base - 18, bark, EDGE, 3)
    blobs = [(cx, y0 + h * 0.3, h * 0.26)]
    for k in range(7):
        a = k / 7 * math.tau + rng.uniform(-0.3, 0.3)
        blobs.append((cx + math.cos(a) * w * 0.3, y0 + h * 0.3 + math.sin(a) * h * 0.1, h * rng.uniform(0.2, 0.24)))
    # keep every crown circle inside the footprint
    blobs = [(min(max(bx, x0 + br + 3), x1 - br - 3), max(by, y0 + br + 3), br) for bx, by, br in blobs]
    for bx, by, br in sorted(blobs, key=lambda b: b[1]):
        c = rng.choice(LEAF)
        circle(d, bx, by, br, c, EDGE, 3)
        circle(d, bx - br * 0.3, by - br * 0.3, br * 0.4, mix(c, (255, 255, 255), 0.12))
    for _ in range(9):   # hanging moss off the crown
        mx = cx + rng.uniform(-w * 0.4, w * 0.4)
        my = y0 + h * rng.uniform(0.35, 0.5)
        vline(d, mx, my, my + rng.uniform(10, 22), (110, 134, 80), 3)


def idol(d, x0, y0, x1, y1, rng):
    """A squat stone frog-idol with glowing eyes, moss on its shoulders."""
    cx = (x0 + x1) / 2
    rect(d, x0 + 6, y1 - 16, x1 - 6, y1 - 4, MOSS_FACE, EDGE, LINE)                               # plinth
    d.rounded_rectangle([x0 + 10, y0 + 8, x1 - 10, y1 - 14], radius=14, fill=MOSS_TOP, outline=EDGE, width=LINE)
    for side in (-1, 1):
        circle(d, cx + side * 10, y0 + 18, 7, (40, 44, 36), EDGE, 2)
        circle(d, cx + side * 10, y0 + 18, 4, GLOW)
    hline(d, cx - 12, cx + 12, y0 + 32, EDGE, 3)                                                   # mouth
    for _ in range(4):
        circle(d, cx + rng.uniform(-18, 18), y0 + rng.uniform(10, 16), rng.uniform(3, 5), (96, 132, 70))


def glowcap(d, x0, y0, x1, y1, rng):
    """A clump of pale, softly glowing mushrooms."""
    for dx, dy, r in ((-6, 4, 6), (5, 1, 8), (0, 9, 4)):
        x, y = (x0 + x1) / 2 + dx, (y0 + y1) / 2 + dy
        rect(d, x - 2, y, x + 2, y + r + 2, (220, 226, 200), EDGE, 1)
        d.pieslice([x - r, y - r, x + r, y + r], 180, 360, fill=(170, 240, 150), outline=EDGE, width=2)
        circle(d, x - r * 0.3, y - r * 0.45, 1.5, (240, 255, 220))


def barrel(d, x0, y0, x1, y1, rng):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    circle(d, cx, cy, 12, WOOD, EDGE, 3)
    d.ellipse([cx - 12, cy - 5, cx + 12, cy + 5], outline=WOOD_DARK, width=2)
    circle(d, cx, cy, 5, WOOD_DARK)


def rowboat(d, cx, cy):
    """A moored rowboat lying east-west on the water (decoration only; the water is already a pit)."""
    poly(d, [(cx - 44, cy), (cx - 30, cy - 14), (cx + 34, cy - 14), (cx + 44, cy), (cx + 34, cy + 14), (cx - 30, cy + 14)],
         WOOD, EDGE, LINE)
    poly(d, [(cx - 36, cy), (cx - 26, cy - 8), (cx + 30, cy - 8), (cx + 36, cy), (cx + 30, cy + 8), (cx - 26, cy + 8)],
         WOOD_DARK)
    for x in (cx - 12, cx + 14):
        rect(d, x - 4, cy - 9, x + 4, cy + 9, WOOD, EDGE, 2)
    d.line([(cx - 44, cy), (cx - 62, cy + 4)], fill=(200, 190, 150), width=2)   # mooring line to the pier


# ---- render --------------------------------------------------------------------------------------------------------


def render(st):
    rng = st.rng
    paint_canopy(st)
    paint_water(st, WATER)
    paint_mud(st, MUD)
    boards = paint_boards(st, BOARD)
    reeds(st, WATER, MUD)
    treeline_faces(st, {MUD, WATER})
    st.outline(st.mask(MUD, BOARD))
    board_posts(st, boards, WATER)
    rowboat(st.d, 36.5 * TILE, 21 * TILE)

    st.draw_objects({
        "wall": partial(props.stone_wall, top=MOSS_TOP, face=MOSS_FACE, edge=EDGE),
        "pillar": lambda d, x0, y0, x1, y1, rng, broken=False:
            props.column(d, x0, y0, x1, y1, rng, top=MOSS_TOP, face=MOSS_FACE, edge=EDGE, broken=broken),
        "idol": idol,
        "mangrove": mangrove,
        "hut": hut,
        "barrel": barrel,
        "stump": stump,
        "glowcap": glowcap,
        "dead_tree": props.dead_tree,
        "tree": partial(props.tree, leaf=LEAF, edge=EDGE, trunk=(84, 66, 52)),
    })

    # light: the idol's eyes, the glowcaps, the hut windows, and fireflies drifting over the mud
    caps = [((tx + w / 2) * TILE, (ty + h / 2) * TILE) for kind, tx, ty, w, h, kw in st.objects if kind == "glowcap"]
    st.glow(caps + [(46 * TILE, 8 * TILE)], 70, GLOW, 0.3)
    mud = st.mask(MUD)
    flies = [(x, y) for x, y in props.points(st, props.density(st, 0.025)) if mud.getpixel((int(x), int(y)))]
    st.glow(flies, 18, (230, 255, 140), 0.5)
    for x, y in flies:
        circle(st.d, x, y, 2, (250, 255, 200))
