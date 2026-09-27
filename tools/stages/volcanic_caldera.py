"""Stage 6: Volcanic Caldera (116x70 tiles). An ash plain inside a basalt crater, split by lava rivers into three outer
regions around a central obsidian arena ringed by a lava moat. Six stone bridges link everything in loops, so the biggest
waves can come from several sides at once."""
import math
from functools import partial
from common import Stage, TILE, LINE, rect, hline, circle, poly, s
import props

BOUND, ASH, LAVA, BRIDGE, OBSID = 0, 1, 2, 3, 4

EDGE = (14, 8, 10)
BASALT = (40, 32, 36)
LAVA_COL = (232, 92, 30)
EMBER = (255, 150, 50)
CX, CY = 58, 33   # arena centre, in tiles


def build():
    st = Stage("volcanic_caldera", 116, 70, seed=66, open_codes=(ASH, BRIDGE, OBSID))

    st.area(ASH, 5, 5, 111, 66)
    st.blob(ASH, 58, 35, 57, 34)
    for cx, cy, rx, ry in ((3, 3, 7, 6), (113, 3, 7, 6), (3, 67, 8, 6), (113, 67, 8, 6), (36, 3, 6, 3), (84, 67, 7, 3), (113, 45, 3, 5)):
        st.blob(BOUND, cx, cy, rx, ry)
    st.area(ASH, 55, 64, 62, 70)
    st.spawn = (58.5, 68)

    # lava: a moat around the arena and three rivers running out to the crater wall
    st.blob(LAVA, CX, CY, 23, 15)
    st.blob(ASH, CX, CY, 18, 11)
    st.blob(OBSID, CX, CY, 13, 8)
    st.path(LAVA, [(57, 0), (55, 9), (58, 18)], 4, only={ASH})           # north
    st.path(LAVA, [(0, 42), (18, 45), (37, 37)], 4, only={ASH})          # west
    st.path(LAVA, [(116, 22), (98, 27), (79, 29)], 4, only={ASH})        # east
    st.blob(LAVA, 20, 16, 5, 3, only={ASH})
    st.blob(LAVA, 96, 54, 5, 4, only={ASH})
    st.blob(LAVA, 90, 11, 4, 3, only={ASH})

    # bridges: south and both sides into the arena, and one over each river
    for pts in (
        [(58, 54), (58, 44)],     # south region -> arena
        [(28, 29), (42, 29)],     # north-west -> arena
        [(72, 39), (88, 39)],     # north-east -> arena
        [(14, 34), (14, 52)],     # over the west river
        [(104, 16), (104, 34)],   # over the east river
        [(46, 9), (68, 9)],       # over the north river
    ):
        st.path(BRIDGE, pts, 4, only={LAVA})

    # arena: braziers at its four corners, the rest left open for the big fights
    for x, y in ((CX - 9, CY - 5), (CX + 8, CY - 5), (CX - 9, CY + 4), (CX + 8, CY + 4)):
        st.put("brazier", x, y)

    clear = [(52, 60, 66, 70), (CX - 13, CY - 8, CX + 13, CY + 8)]
    st.scatter("spike", 16, {ASH}, sizes=((1, 1), (2, 2), (2, 2)), margin=2, keep_clear=clear)
    st.scatter("boulder", 10, {ASH}, sizes=((1, 1), (2, 2), (3, 2)), margin=2, keep_clear=clear)
    st.scatter("vent", 6, {ASH}, sizes=((2, 2),), margin=2, keep_clear=clear)
    st.seal_pockets()

    render(st)
    return st


# ---- props ---------------------------------------------------------------------------------------------------------


def brazier(d, x0, y0, x1, y1, rng):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    poly(d, [(cx - 12, cy - 4), (cx + 12, cy - 4), (cx + 7, y1 - 3), (cx - 7, y1 - 3)], (70, 60, 66), EDGE, 3)
    for r, c in ((10, (255, 110, 30)), (6, (255, 190, 70)), (3, (255, 245, 180))):
        poly(d, [(cx - r, cy - 3), (cx, cy - 3 - r * 2), (cx + r, cy - 3)], c)


def vent(d, x0, y0, x1, y1, rng):
    """A small cone with a glowing crater."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2 + 4
    w = (x1 - x0) / 2 - 4
    poly(d, [(cx - w, y1 - 4), (cx - w * 0.4, cy - 10), (cx + w * 0.4, cy - 10), (cx + w, y1 - 4)], (62, 50, 54), EDGE, LINE)
    d.ellipse([s(cx - w * 0.42), s(cy - 16), s(cx + w * 0.42), s(cy - 4)], fill=(255, 120, 40), outline=EDGE, width=3)
    d.ellipse([s(cx - w * 0.2), s(cy - 13), s(cx + w * 0.2), s(cy - 7)], fill=(255, 220, 120))


# ---- render --------------------------------------------------------------------------------------------------------


def hexagon(d, cx, cy, r, fill, edge):
    poly(d, [(cx + r * math.cos(a), cy + r * math.sin(a)) for a in (k * math.pi / 3 for k in range(6))], fill, edge, 3)


def render(st):
    rng = st.rng
    # basalt: the tops of hexagonal columns
    lay, d = st.layer(BASALT)
    r = 22
    for row, y in enumerate(range(-r, st.H + r, int(r * math.sqrt(3) / 2))):
        for x in range(-r, st.W + 2 * r, 3 * r):
            hx = x + (1.5 * r if row % 2 else 0)
            hexagon(d, hx, y, r - 1, rng.choice([(44, 36, 40), (50, 40, 44), (38, 30, 34), (56, 44, 46)]), (24, 18, 22))
    props.cracks(st, d, (60, 20, 14), 0.05, 3, glow=(255, 120, 40))
    st.paint(lay, st.mask(BOUND))

    # ash plain with ember seams
    lay, d = st.layer((80, 70, 70))
    props.tile_grid(st, d, (74, 64, 64))
    props.specks(st, d, [(104, 94, 92), (60, 52, 52)], 0.7, 2, 4)
    props.cracks(st, d, (56, 30, 26), 0.02, 3, glow=(210, 96, 40))
    st.paint(lay, st.mask(ASH))

    # obsidian arena floor
    lay, d = st.layer((42, 32, 48))
    props.tile_grid(st, d, (60, 46, 70), 3)
    for x, y in props.points(st, props.density(st, 0.06)):
        d.line([(s(x - 8), s(y + 5)), (s(x + 8), s(y - 5))], fill=(110, 90, 140), width=2)
    st.paint(lay, st.mask(OBSID))

    # lava: bright molten body, hotter swirls, drifting crust
    lay, d = st.layer(LAVA_COL)
    for x, y in props.points(st, props.density(st, 0.25)):
        rr = rng.uniform(10, 28)
        d.ellipse([s(x - rr), s(y - rr * 0.5), s(x + rr), s(y + rr * 0.5)], fill=rng.choice([EMBER, (255, 190, 70)]))
    for x, y in props.points(st, props.density(st, 0.08)):
        rr = rng.uniform(8, 18)
        pts = [(x + math.cos(a) * rr * rng.uniform(0.6, 1), y + math.sin(a) * rr * rng.uniform(0.6, 1))
               for a in (k / 6 * math.tau for k in range(6))]
        poly(d, pts, (110, 36, 20), (70, 20, 12), 2)
    st.paint(lay, st.mask(LAVA))
    lava = st.mask(LAVA)
    st.outline(lava, (170, 50, 20), 8)                # cooled crust along the banks

    # stone bridges: slabs laid across each span, with parapet lips
    lay, d = st.layer((0, 0, 0))
    horiz, vert = st.split_by_run(BRIDGE)
    props.planks(st, d, horiz, (116, 104, 104), (78, 68, 70), across_x=True)
    props.planks(st, d, vert, (116, 104, 104), (78, 68, 70), across_x=False)
    st.paint(lay, st.mask(BRIDGE))

    # crater wall faces: column fronts with glowing seams
    def face(d, x0, x1, y0, y1):
        x = x0
        while x < x1:
            w = min(rng.uniform(18, 30), x1 - x)
            rect(d, x, y0, x + w, y1, rng.choice([(58, 46, 50), (50, 40, 44), (64, 52, 54)]), (24, 18, 22), 2)
            rect(d, x, y0, x + w, y0 + 8, (78, 64, 66))
            x += w
        for _ in range(int((x1 - x0) / 90) + 1):
            fx = rng.uniform(x0 + 6, x1 - 6)
            d.line([(s(fx), s(y0 + 10)), (s(fx + rng.uniform(-6, 6)), s(y0 + 30)), (s(fx + rng.uniform(-6, 6)), s(y1 - 4))],
                   fill=(255, 110, 40), width=2)
        hline(d, x0, x1, y1 - 2, (20, 14, 16), 4)
    st.faces({BOUND}, {ASH, OBSID, LAVA}, 2, face)

    open_mask = st.mask(ASH, BRIDGE, OBSID)
    st.glow_from(lava, 26, (255, 120, 40), 0.35, open_mask)

    # the summoning circle in the middle of the arena
    cx, cy = (CX + 0.5) * TILE, (CY + 0.5) * TILE
    rune = (200, 40, 40)
    for rr, w in ((150, 5), (132, 3), (52, 3)):
        st.d.ellipse([s(cx - rr), s(cy - rr * 0.62), s(cx + rr), s(cy + rr * 0.62)], outline=rune, width=w)
    star = [(cx + 132 * math.cos(a), cy + 132 * 0.62 * math.sin(a)) for a in (-math.pi / 2 + k * 4 * math.pi / 5 for k in range(5))]
    st.d.line([(s(x), s(y)) for x, y in star + star[:1]], fill=rune, width=3)
    for k in range(10):
        a = k / 10 * math.tau
        circle(st.d, cx + 141 * math.cos(a), cy + 141 * 0.62 * math.sin(a), 4, (255, 90, 60))
    st.glow([(cx, cy)], 150, (220, 40, 40), 0.18, open_mask)

    st.outline(open_mask)

    centres = [((tx + w / 2) * TILE, (ty + h / 2) * TILE) for kind, tx, ty, w, h, kw in st.objects if kind in ("brazier", "vent")]
    st.glow(centres, 60, (255, 150, 60), 0.35, open_mask)

    st.draw_objects({
        "brazier": brazier,
        "vent": vent,
        "spike": partial(props.spike, fill=(36, 26, 44), light=(120, 90, 150), edge=EDGE),
        "boulder": partial(props.boulder, fill=(76, 64, 68), edge=EDGE),
    })
