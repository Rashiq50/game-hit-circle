"""Stage 3: Swamp Bog (80x48 tiles). Mossy islands in deep murky water, joined by 3-wide plank boardwalks that act as
chokepoints and loops. A witch's hut sits on the central island; dead trees and stumps give scattered cover."""
from functools import partial
from common import Stage, TILE, LINE, rect, hline, vline, circle, poly, mix, s
import props

BOUND, MUD, WATER, BOARD = 0, 1, 2, 3

LEAF = ((44, 66, 46), (52, 76, 50), (38, 58, 42), (58, 82, 52))
EDGE = (14, 24, 16)
WATER_COL = (48, 86, 84)
WOOD, WOOD_DARK = (120, 92, 62), (70, 50, 32)


def build():
    st = Stage("swamp_bog", 80, 48, seed=33, open_codes=(MUD, BOARD), pits={WATER: "Water"})

    st.area(WATER, 3, 3, 77, 45)
    st.blob(WATER, 40, 24, 40, 24)
    for cx, cy, rx, ry in ((6, 4, 6, 4), (74, 44, 6, 4), (76, 6, 4, 5), (4, 44, 5, 5)):
        st.blob(BOUND, cx, cy, rx, ry)

    # islands
    st.blob(MUD, 40, 40, 8, 5); st.area(MUD, 37, 43, 44, 48)   # entry island, open to the south edge
    st.blob(MUD, 14, 32, 9, 7)     # west
    st.blob(MUD, 40, 24, 11, 7)    # centre, with the hut
    st.blob(MUD, 16, 11, 9, 6)     # north-west
    st.blob(MUD, 62, 10, 10, 6)    # north-east
    st.blob(MUD, 66, 31, 8, 7)     # east
    st.blob(MUD, 40, 7, 6, 4)      # north
    st.spawn = (40.5, 46)

    # boardwalks: drawn generously end to end, only the water they cross becomes planks
    for pts in (
        [(40, 38), (40, 28)],                 # entry -> centre
        [(36, 40), (20, 40), (20, 34)],       # entry -> west
        [(44, 40), (64, 40), (64, 34)],       # entry -> east
        [(32, 22), (22, 22), (22, 14)],       # centre -> north-west
        [(48, 22), (62, 22), (62, 12)],       # centre -> north-east
        [(40, 20), (40, 8)],                  # centre -> north
        [(22, 8), (38, 8)],                   # north-west -> north
        [(12, 30), (12, 12)],                 # west -> north-west
        [(68, 30), (68, 12)],                 # east -> north-east
    ):
        st.path(BOARD, pts, 3, only={WATER})

    st.put("hut", 37, 20, 6, 4)
    st.put("cauldron", 44, 26, 1, 1)
    clear = [(35, 41, 46, 48), (34, 18, 47, 29)]
    st.scatter("dead_tree", 10, {MUD}, sizes=((2, 2),), margin=2, keep_clear=clear)
    st.scatter("tree", 8, {MUD}, sizes=((2, 2), (3, 3)), margin=2, keep_clear=clear)
    st.scatter("stump", 10, {MUD}, margin=2, keep_clear=clear)
    st.seal_pockets()
    st.pick_enemy_spawns(18)

    render(st)
    return st


# ---- props ---------------------------------------------------------------------------------------------------------


def hut(d, x0, y0, x1, y1, rng):
    """Stilt hut: plank front wall with a door under a thatched roof."""
    w = x1 - x0
    wall_top = y0 + (y1 - y0) * 0.45
    rect(d, x0 + 8, wall_top, x1 - 8, y1 - 4, WOOD, EDGE, LINE)
    for x in range(int(x0) + 24, int(x1) - 8, 16):
        vline(d, x, wall_top + 4, y1 - 6, WOOD_DARK, 2)
    cx = (x0 + x1) / 2
    rect(d, cx - 14, y1 - 44, cx + 14, y1 - 4, (40, 28, 20), EDGE, 3)               # door
    for wx in (x0 + 30, x1 - 30):                                                  # windows with a green glow
        rect(d, wx - 10, wall_top + 12, wx + 10, wall_top + 30, (150, 230, 110), EDGE, 3)
    thatch = (150, 128, 70)
    poly(d, [(x0 + 2, wall_top + 4), (x0 + 22, y0 + 4), (x1 - 22, y0 + 4), (x1 - 2, wall_top + 4)], thatch, EDGE, LINE)
    for k in range(1, 5):
        yy = y0 + 4 + (wall_top - y0) * k / 5
        hline(d, x0 + 22 - 20 * k / 5 + 4, x1 - 22 + 20 * k / 5 - 4, yy, mix(thatch, EDGE, 0.3), 2)
    # a skull on the ridge
    circle(d, cx, y0 + 16, 8, (230, 226, 210), EDGE, 2)
    circle(d, cx - 3, y0 + 15, 2, EDGE); circle(d, cx + 3, y0 + 15, 2, EDGE)


def cauldron(d, x0, y0, x1, y1, rng):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2 + 2
    circle(d, cx, cy, 13, (46, 44, 50), EDGE, 3)
    d.ellipse([s(cx - 10), s(cy - 9), s(cx + 10), s(cy - 1)], fill=(130, 220, 90))
    circle(d, cx - 3, cy - 6, 2, (200, 255, 170)); circle(d, cx + 4, cy - 4, 2, (200, 255, 170))


def stump(d, x0, y0, x1, y1, rng):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    rect(d, cx - 11, cy - 4, cx + 11, cy + 11, WOOD_DARK, EDGE, 3)
    d.ellipse([s(cx - 11), s(cy - 11), s(cx + 11), s(cy + 1)], fill=(170, 140, 96), outline=EDGE, width=3)
    d.ellipse([s(cx - 5), s(cy - 7), s(cx + 5), s(cy - 3)], outline=WOOD_DARK, width=2)
    circle(d, cx + 6, cy + 6, 3, (120, 160, 80))                                    # moss


# ---- render --------------------------------------------------------------------------------------------------------


def render(st):
    rng = st.rng
    lay, d = st.layer((18, 32, 22))
    props.canopy(st, d, LEAF, EDGE, 28, 58, 0.85)
    # hanging moss strands
    for x, y in props.points(st, props.density(st, 0.25)):
        d.line([(s(x), s(y)), (s(x + rng.uniform(-3, 3)), s(y + rng.uniform(10, 22)))], fill=(96, 120, 70), width=3)
    st.paint(lay, st.mask(BOUND))

    # bog water: murky, with lily pads, bubbles and drifting mist
    lay, d = st.layer(WATER_COL)
    for x, y in props.points(st, props.density(st, 0.05)):
        r = rng.uniform(40, 110)
        d.ellipse([s(x - r), s(y - r * 0.45), s(x + r), s(y + r * 0.45)], fill=mix(WATER_COL, (200, 220, 200), 0.08))
    props.ripples(st, d, (80, 122, 114), 0.3, 24)
    for x, y in props.points(st, props.density(st, 0.12)):
        r = rng.uniform(7, 12)
        a = rng.uniform(0, 360)
        d.pieslice([s(x - r), s(y - r), s(x + r), s(y + r)], a + 40, a + 360, fill=(82, 140, 70), outline=(40, 70, 36), width=2)
        if rng.random() < 0.15:
            circle(d, x, y, 3, (236, 180, 210))
    props.specks(st, d, [(92, 132, 122)], 0.15, 2, 3)
    st.paint(lay, st.mask(WATER))
    st.outline(st.mask(WATER), (36, 64, 64), 10)

    # islands: mossy mud
    lay, d = st.layer((104, 122, 72))
    props.tile_grid(st, d, (98, 115, 68))
    for x, y in props.points(st, props.density(st, 0.05)):
        r = rng.uniform(18, 40)
        d.ellipse([s(x - r), s(y - r * 0.6), s(x + r), s(y + r * 0.6)], fill=(92, 96, 62))
    props.tufts(st, d, (128, 148, 84), 0.8)
    props.specks(st, d, [(200, 90, 70), (230, 220, 150)], 0.05, 3, 4)          # toadstools, pale flowers
    st.paint(lay, st.mask(MUD))

    # boardwalks: planks run across the walking direction of each stretch
    lay, d = st.layer((0, 0, 0))
    horiz, vert = st.split_by_run(BOARD)
    boards = horiz + vert
    props.planks(st, d, horiz, WOOD, WOOD_DARK, across_x=True)
    props.planks(st, d, vert, WOOD, WOOD_DARK, across_x=False)
    st.paint(lay, st.mask(BOARD))

    # reeds on the water just off each island shore
    for y in range(st.th):
        for x in range(st.tw):
            if st.code(x, y) == WATER and any(st.code(x + dx, y + dy) == MUD for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))) \
                    and rng.random() < 0.45:
                bx, by = x * TILE + rng.uniform(8, 24), y * TILE + rng.uniform(18, 28)
                for k in range(4):
                    tx = bx + (k - 1.5) * 4 + rng.uniform(-2, 2)
                    d_ = st.d
                    d_.line([(s(bx + (k - 1.5) * 2), s(by)), (s(tx), s(by - rng.uniform(14, 22)))], fill=(70, 110, 56), width=3)
                    if k == 1:
                        d_.line([(s(tx), s(by - 20)), (s(tx), s(by - 12))], fill=(110, 70, 40), width=5)   # cattail

    def face(d, x0, x1, y0, y1):
        rect(d, x0, y1 - TILE, x1, y1, (16, 28, 20))
        x = x0 + rng.uniform(8, 24)
        while x < x1 - 10:
            w = rng.uniform(10, 16)
            poly(d, [(x - w / 2 - 5, y1), (x - w / 2, y1 - 26), (x + w / 2, y1 - 26), (x + w / 2 + 5, y1)], (74, 60, 50), EDGE, 3)
            x += rng.uniform(30, 56)
        for x in range(int(x0) + 6, int(x1) - 4, 14):
            vline(d, x, y1 - TILE, y1 - TILE + rng.uniform(6, 18), (96, 120, 70), 3)
    st.faces({BOUND}, {MUD, WATER}, 1, face)

    st.outline(st.mask(MUD, BOARD))
    # boardwalk posts where planks meet water
    for x, y in boards:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if st.code(x + dx, y + dy) == WATER and (x + y) % 2 == 0:
                px = x * TILE + TILE / 2 + dx * (TILE / 2 - 6)
                py = y * TILE + TILE / 2 + dy * (TILE / 2 - 6)
                circle(st.d, px, py, 5, WOOD_DARK, EDGE, 2)

    st.draw_objects({
        "hut": hut,
        "cauldron": cauldron,
        "stump": stump,
        "dead_tree": props.dead_tree,
        "tree": partial(props.tree, leaf=LEAF, edge=EDGE, trunk=(84, 66, 52)),
    })
