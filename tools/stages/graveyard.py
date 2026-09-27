"""Stage 5: Graveyard (104x62 tiles). A walled cemetery entered through a south gatehouse. Cobbled paths cross at an
angel statue; three iron-fenced burial plots full of headstones, a ruined chapel with pews, and a mausoleum at the head of
the main path. Fence gaps are the only ways into each plot, so fights funnel through them."""
from functools import partial
from common import Stage, TILE, LINE, GOLD, rect, hline, vline, circle, poly, mix, s
import props

BOUND, GRASS, PATH, FLAG, DIRT = 0, 1, 2, 3, 4

EDGE = (14, 10, 20)
STONE_TOP, STONE_FACE = (150, 146, 158), (104, 100, 114)
IRON = (38, 36, 46)
LEAF = ((46, 40, 56), (54, 48, 64), (40, 36, 50), (60, 50, 62))


def build():
    st = Stage("graveyard", 104, 62, seed=55, open_codes=(GRASS, PATH, FLAG, DIRT))

    st.area(GRASS, 4, 4, 100, 58)
    for cx, cy, rx, ry in ((4, 4, 5, 5), (100, 4, 5, 4), (4, 58, 5, 4), (100, 58, 4, 5)):
        st.blob(BOUND, cx, cy, rx, ry)
    st.area(PATH, 49, 57, 56, 62)                   # gatehouse passage between the towers
    st.spawn = (52.5, 60)

    # paths: the main walk north to the mausoleum, the cross walk, and the plaza around the statue
    st.path(PATH, [(52, 60), (52, 11)], 4)
    st.path(PATH, [(6, 32), (98, 32)], 3, only={GRASS})
    st.area(PATH, 44, 28, 61, 37)
    st.path(PATH, [(52, 21), (78, 21), (78, 26)], 3, only={GRASS})   # side walk to the chapel door

    # chapel: flagstone nave, broken walls, pews either side of the aisle, altar under the east window
    st.area(FLAG, 62, 6, 95, 26)
    wall = lambda x0, x1, y: st.put("wall", x0, y, x1 - x0, 1)
    post = lambda x, y0, y1: st.put("wall", x, y0, 1, y1 - y0)
    wall(62, 72, 6); wall(75, 90, 6)
    post(62, 7, 12); post(62, 16, 22)
    post(94, 7, 15); post(94, 19, 25)
    wall(62, 75, 25); wall(81, 95, 25)
    st.put("altar", 76, 7, 4, 2)
    for y in (12, 15, 18, 21):
        st.put("pew", 66, y, 6, 1)
        st.put("pew", 84, y, 6, 1)

    st.put("mausoleum", 46, 4, 13, 7)
    st.put("statue", 50, 30, 5, 4)

    # fenced plots, each with gates facing the paths
    plot(st, 10, 8, 41, 27, gaps={"bottom": (23, 28), "right": (15, 20)})
    plot(st, 10, 38, 41, 55, gaps={"top": (23, 28), "right": (44, 49)})
    plot(st, 64, 38, 95, 55, gaps={"top": (77, 82), "left": (44, 49)})

    for x, y in ((47, 24), (57, 24), (47, 40), (57, 40), (44, 14), (60, 14)):
        st.try_put("lantern", x, y)
    clear = [(46, 54, 60, 62)]
    st.scatter("dead_tree", 12, {GRASS}, sizes=((2, 2), (3, 3)), margin=2, keep_clear=clear)
    st.scatter("headstone", 10, {GRASS}, margin=2, keep_clear=clear)
    st.seal_pockets()
    st.pick_enemy_spawns(26)

    render(st)
    return st


def plot(st, x0, y0, x1, y1, gaps):
    """An iron-fenced plot over [x0, x1) x [y0, y1) with rows of headstones inside. `gaps` maps a side to the
    half-open range of tiles left open along it."""
    def run(side, a, b, place):
        g = gaps.get(side)
        pieces = [(a, b)] if not g else [(a, g[0]), (g[1], b)]
        for p0, p1 in pieces:
            if p1 > p0:
                place(p0, p1)
    run("top", x0, x1, lambda a, b: st.put("fence", a, y0, b - a, 1))
    run("bottom", x0, x1, lambda a, b: st.put("fence", a, y1 - 1, b - a, 1))
    run("left", y0 + 1, y1 - 1, lambda a, b: st.put("fence", x0, a, 1, b - a))
    run("right", y0 + 1, y1 - 1, lambda a, b: st.put("fence", x1 - 1, a, 1, b - a))
    # headstone rows, two free tiles in from the fence; some graves are freshly dug instead
    for y in range(y0 + 3, y1 - 4, 4):
        for x in range(x0 + 3, x1 - 3, 3):
            r = st.rng.random()
            if r < 0.12:
                st.area(DIRT, x, y, x + 1, y + 2)
            elif r < 0.9:
                st.put("headstone", x, y)


# ---- props ---------------------------------------------------------------------------------------------------------


def fence(d, x0, y0, x1, y1, rng):
    """Wrought-iron railings: spear-topped bars between rails, stone posts at the ends."""
    if x1 - x0 >= y1 - y0:   # runs east-west
        hline(d, x0, x1, y0 + 12, IRON, 4); hline(d, x0, x1, y1 - 7, IRON, 4)
        for x in range(int(x0) + 4, int(x1), 8):
            vline(d, x, y0 + 5, y1 - 4, IRON, 3)
            poly(d, [(x - 3, y0 + 8), (x, y0 + 2), (x + 3, y0 + 8)], IRON)
        posts = [(x0, y0), (x1 - TILE, y0)]
    else:                    # runs north-south: seen end-on, so a row of spear tips down a rail
        vline(d, (x0 + x1) / 2, y0, y1, IRON, 6)
        for y in range(int(y0) + 6, int(y1), 10):
            circle(d, (x0 + x1) / 2, y, 3, (70, 68, 80))
        posts = [(x0, y0), (x0, y1 - TILE)]
    for px, py in posts:
        rect(d, px + 9, py + 6, px + TILE - 9, py + TILE - 2, STONE_FACE, EDGE, 3)
        rect(d, px + 9, py + 6, px + TILE - 9, py + 14, STONE_TOP, EDGE, 3)


def mausoleum(d, x0, y0, x1, y1, rng):
    w = x1 - x0
    cx = (x0 + x1) / 2
    base = y1 - 10
    roof = y0 + (y1 - y0) * 0.38
    rect(d, x0 + 12, roof, x1 - 12, base, STONE_FACE, EDGE, LINE)                     # body
    for x in range(int(x0) + 40, int(x1) - 20, 48):                                     # columns
        if abs(x - cx) > 40:
            rect(d, x - 9, roof + 6, x + 9, base - 2, STONE_TOP, EDGE, 3)
    rect(d, cx - 28, base - 70, cx + 28, base, (18, 14, 26), EDGE, LINE)                 # doorway
    d.pieslice([s(cx - 28), s(base - 98), s(cx + 28), s(base - 42)], 180, 360, fill=(18, 14, 26), outline=EDGE, width=LINE)
    rect(d, cx - 28, base - 70, cx + 28, base - 66, (18, 14, 26))
    vline(d, cx, base - 64, base, (60, 56, 70), 3)
    poly(d, [(x0 + 4, roof + 4), (cx, y0 + 6), (x1 - 4, roof + 4)], STONE_TOP, EDGE, LINE)  # pediment
    poly(d, [(cx - 40, roof - 2), (cx, y0 + 26), (cx + 40, roof - 2)], mix(STONE_TOP, EDGE, 0.25))
    circle(d, cx, roof - 20, 9, (150, 230, 160), EDGE, 3)                             # a ghostly lamp
    rect(d, x0 + 4, base, x1 - 4, y1 - 2, STONE_TOP, EDGE, 3)                         # step


def statue(d, x0, y0, x1, y1, rng):
    cx = (x0 + x1) / 2
    rect(d, x0 + 16, y1 - 46, x1 - 16, y1 - 4, STONE_FACE, EDGE, LINE)               # pedestal
    rect(d, x0 + 10, y1 - 54, x1 - 10, y1 - 42, STONE_TOP, EDGE, 3)
    for side in (-1, 1):                                                              # wings
        poly(d, [(cx + side * 8, y0 + 44), (cx + side * 62, y0 + 14), (cx + side * 54, y0 + 50), (cx + side * 16, y0 + 78)],
             STONE_TOP, EDGE, 3)
        for k in range(1, 3):
            d.line([(s(cx + side * (14 + 12 * k)), s(y0 + 40 + 2 * k)), (s(cx + side * (40 + 6 * k)), s(y0 + 26 + 10 * k))],
                   fill=mix(STONE_TOP, EDGE, 0.35), width=2)
    poly(d, [(cx - 16, y1 - 54), (cx - 10, y0 + 40), (cx + 10, y0 + 40), (cx + 16, y1 - 54)], STONE_TOP, EDGE, 3)  # robe
    circle(d, cx, y0 + 30, 11, STONE_TOP, EDGE, 3)                                    # head
    d.ellipse([s(cx - 12), s(y0 + 12), s(cx + 12), s(y0 + 18)], outline=GOLD, width=3)   # halo


def lantern(d, x0, y0, x1, y1, rng):
    cx = (x0 + x1) / 2
    rect(d, cx - 5, y0 + 14, cx + 5, y1 - 3, STONE_FACE, EDGE, 2)
    rect(d, cx - 9, y0 + 3, cx + 9, y0 + 17, (60, 56, 70), EDGE, 2)
    rect(d, cx - 5, y0 + 6, cx + 5, y0 + 14, (255, 214, 120))


def pew(d, x0, y0, x1, y1, rng):
    wood, dark = (96, 64, 44), (60, 38, 26)
    rect(d, x0 + 2, y0 + 4, x1 - 2, y0 + 12, dark, EDGE, 3)       # backrest
    rect(d, x0 + 2, y0 + 12, x1 - 2, y1 - 5, wood, EDGE, 3)       # seat
    for x in (x0 + 8, x1 - 8):
        vline(d, x, y1 - 5, y1 - 1, EDGE, 4)


def altar(d, x0, y0, x1, y1, rng):
    rect(d, x0 + 4, y0 + 4, x1 - 4, y1 - 2, STONE_FACE, EDGE, LINE)
    rect(d, x0 + 4, y0 + 4, x1 - 4, y1 - 18, (120, 24, 34), EDGE, 3)       # altar cloth
    for k in range(4):
        x = x0 + 18 + k * (x1 - x0 - 36) / 3
        rect(d, x - 3, y0 + 8, x + 3, y0 + 20, (236, 228, 200))
        circle(d, x, y0 + 6, 3, (255, 200, 90))


# ---- render --------------------------------------------------------------------------------------------------------


def render(st):
    rng = st.rng
    lay, d = st.layer((24, 20, 30))
    props.canopy(st, d, LEAF, EDGE, 26, 54, 0.8)
    st.paint(lay, st.mask(BOUND))

    lay, d = st.layer((84, 96, 74))
    props.tile_grid(st, d, (78, 90, 69))
    props.tufts(st, d, (104, 116, 84), 0.7)
    props.specks(st, d, [(140, 92, 58), (120, 72, 52), (160, 120, 60)], 0.25, 2, 4)    # fallen leaves
    st.paint(lay, st.mask(GRASS))

    # cobbles
    lay, d = st.layer((104, 102, 112))
    for x, y in props.points(st, props.density(st, 4.5)):
        r = rng.uniform(5, 9)
        d.ellipse([s(x - r), s(y - r * 0.8), s(x + r), s(y + r * 0.8)], fill=rng.choice([(124, 122, 134), (116, 114, 126), (132, 128, 138)]),
                  outline=(86, 84, 96), width=2)
    st.paint(lay, st.mask(PATH))

    lay, d = st.layer((98, 94, 108))
    props.tile_grid(st, d, (82, 78, 92), 3)
    props.cracks(st, d, (74, 70, 84), 0.08, 2)
    st.paint(lay, st.mask(FLAG))
    # carpet down the chapel aisle
    rect(st.d, 75 * TILE + 8, 9 * TILE, 81 * TILE - 8, 25 * TILE, (110, 22, 32), GOLD, 3)

    lay, d = st.layer((86, 64, 50))
    props.specks(st, d, [(70, 52, 40), (110, 84, 64)], 1.2, 2, 4)
    st.paint(lay, st.mask(DIRT))
    st.outline(st.mask(DIRT), (60, 44, 34), 4)

    # cemetery wall along the treeline: coursed stone, two tiles high
    def face(d, x0, x1, y0, y1):
        rect(d, x0, y0, x1, y1, STONE_FACE)
        for row, y in enumerate(range(int(y0), int(y1), 16)):
            hline(d, x0, x1, y, mix(STONE_FACE, EDGE, 0.4), 2)
            for x in range(int(x0) + (16 if row % 2 else 0), int(x1), 32):
                vline(d, x, y, y + 16, mix(STONE_FACE, EDGE, 0.4), 2)
        rect(d, x0, y0, x1, y0 + 10, STONE_TOP)
        hline(d, x0, x1, y0 + 10, EDGE, 3)
        for x in range(int(x0) + 10, int(x1) - 6, 22):   # ivy
            if rng.random() < 0.35:
                d.line([(s(x), s(y0 + 8)), (s(x + rng.uniform(-6, 6)), s(y0 + rng.uniform(26, 50)))], fill=(70, 98, 60), width=4)
    st.faces({BOUND}, set(st.open_codes), 2, face)

    # lanterns glow, mist drifts over everything open; the outline goes on after so the mist doesn't wash it out
    open_mask = st.mask(GRASS, PATH, FLAG, DIRT)
    centres = [((tx + 0.5) * TILE, (ty + 0.3) * TILE) for kind, tx, ty, w, h, kw in st.objects if kind == "lantern"]
    st.glow(centres, 70, (255, 210, 130), 0.3, open_mask)
    st.glow(props.points(st, props.density(st, 0.012)), 120, (190, 180, 220), 0.18, open_mask)
    st.outline(open_mask)

    # gatehouse towers either side of the entry
    for gx in (46, 56):
        rect(st.d, gx * TILE, 58 * TILE, (gx + 3) * TILE, 62 * TILE, STONE_FACE, EDGE, LINE)
        rect(st.d, gx * TILE, 58 * TILE + 10, (gx + 3) * TILE, 59 * TILE + 16, STONE_TOP, EDGE, LINE)
        for k in range(3):
            rect(st.d, gx * TILE + 6 + k * 30, 58 * TILE, gx * TILE + 20 + k * 30, 58 * TILE + 12, STONE_TOP, EDGE, 3)

    st.draw_objects({
        "wall": partial(props.stone_wall, top=STONE_TOP, face=STONE_FACE, edge=EDGE),
        "fence": fence,
        "headstone": partial(props.headstone, edge=EDGE),
        "dead_tree": props.dead_tree,
        "mausoleum": mausoleum,
        "statue": statue,
        "lantern": lantern,
        "pew": pew,
        "altar": altar,
    })
