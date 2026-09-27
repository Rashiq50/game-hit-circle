"""Stage 4: Frozen Cavern (92x56 tiles). Seven ice-cave chambers linked by winding 4-wide tunnels: a frozen lake in the
great central hall, snow drifts, bottomless crevasses, and glowing crystal clusters and stalagmites as cover. The loops
between chambers let bigger mixed waves flank the player."""
from functools import partial
from common import Stage, TILE, rect, hline, vline, poly
import props

BOUND, FLOOR, ICE, SNOW, CHASM = 0, 1, 2, 3, 4

ROCK_EDGE = (10, 14, 26)
CRYSTAL = (120, 220, 245)


def build():
    st = Stage("frozen_cavern", 92, 56, seed=44, open_codes=(FLOOR, ICE, SNOW), pits={CHASM: "Chasm"})

    # chambers
    st.blob(FLOOR, 46, 48, 9, 6); st.area(FLOOR, 43, 52, 50, 56)  # entry, open to the south edge
    st.blob(FLOOR, 18, 42, 11, 8)    # west
    st.blob(FLOOR, 46, 28, 15, 9)    # great hall
    st.blob(FLOOR, 18, 14, 12, 8)    # north-west
    st.blob(FLOOR, 74, 13, 12, 8)    # north-east
    st.blob(FLOOR, 76, 40, 10, 9)    # east
    st.blob(FLOOR, 46, 7, 8, 5)      # crystal shrine
    st.spawn = (46.5, 54)

    # tunnels
    for pts in (
        [(46, 44), (45, 39), (46, 35)],
        [(38, 48), (28, 45)],
        [(54, 48), (67, 44)],
        [(33, 26), (26, 20), (22, 17)],
        [(59, 26), (66, 20), (70, 17)],
        [(46, 20), (46, 10)],
        [(14, 34), (15, 22)],
        [(78, 22), (77, 32)],
    ):
        st.path(FLOOR, pts, 4, only={BOUND})

    # ground variety
    st.blob(ICE, 46, 29, 9, 5, only={FLOOR})     # the frozen lake
    st.blob(ICE, 20, 44, 5, 3, only={FLOOR})
    st.blob(ICE, 75, 41, 4, 3, only={FLOOR})
    for cx, cy, rx, ry in ((14, 10, 6, 3), (80, 10, 5, 3), (70, 46, 4, 2), (12, 46, 4, 3), (58, 32, 3, 3)):
        st.blob(SNOW, cx, cy, rx, ry, only={FLOOR})
    st.path(CHASM, [(70, 36), (74, 38)], 2, only={FLOOR})
    st.blob(CHASM, 22, 14, 3, 2, only={FLOOR})
    st.blob(CHASM, 80, 42, 2, 3, only={FLOOR})

    # shrine: one big crystal flanked by stalagmites
    st.put("crystal", 45, 2, 4, 3, big=True)
    st.put("stalagmite", 41, 7); st.put("stalagmite", 51, 7)
    # spires around the lake's shore
    for x, y in ((34, 25), (58, 25), (36, 33), (56, 34)):
        st.try_put("stalagmite", x, y, 2, 2)

    clear = [(41, 50, 52, 56)]
    st.scatter("crystal", 10, {FLOOR, SNOW}, sizes=((2, 2), (2, 2), (1, 1)), margin=2, keep_clear=clear)
    st.scatter("stalagmite", 12, {FLOOR}, sizes=((2, 2), (2, 2), (1, 1)), margin=2, keep_clear=clear)
    st.scatter("ice_block", 5, {FLOOR, SNOW}, sizes=((1, 1), (2, 1)), margin=2, keep_clear=clear)
    st.seal_pockets()
    st.pick_enemy_spawns(22)

    render(st)
    return st


def render(st):
    rng = st.rng
    # cave rock: faceted slabs with frost
    lay, d = st.layer((42, 50, 68))
    for x, y in props.points(st, props.density(st, 0.14)):
        r = rng.uniform(34, 72)
        props.boulder(d, x - r, y - r * 0.7, x + r, y + r * 0.7, rng, fill=rng.choice([(52, 61, 82), (48, 57, 77), (56, 66, 88)]),
                      edge=(34, 40, 56), shade=(44, 52, 70))
    props.specks(st, d, [(170, 196, 220)], 0.2, 1, 2)
    st.paint(lay, st.mask(BOUND))

    # frost stone
    lay, d = st.layer((150, 166, 188))
    props.tile_grid(st, d, (141, 157, 179))
    props.cracks(st, d, (120, 134, 156), 0.1, 3)
    props.specks(st, d, [(206, 222, 238), (126, 140, 162)], 0.4, 2, 3)
    st.paint(lay, st.mask(FLOOR))

    # ice sheet: glassy, diagonal glints
    lay, d = st.layer((178, 224, 240))
    props.tile_grid(st, d, (168, 214, 232))
    for x, y in props.points(st, props.density(st, 0.1)):
        l = rng.uniform(14, 34)
        d.line([(x - l, y + l * 0.6), (x + l, y - l * 0.6)], fill=(236, 250, 255), width=3)
    props.cracks(st, d, (140, 196, 220), 0.05, 2)
    st.paint(lay, st.mask(ICE))
    st.outline(st.mask(ICE), (138, 196, 222), 5)

    lay, d = st.layer((232, 240, 250))
    for x, y in props.points(st, props.density(st, 0.3)):
        r = rng.uniform(8, 20)
        d.ellipse([x - r, y - r * 0.4, x + r, y + r * 0.4], fill=(212, 224, 242))
    st.paint(lay, st.mask(SNOW))

    lay, d = st.layer((6, 8, 16))
    props.specks(st, d, [(40, 60, 100)], 0.2, 1, 2)
    st.paint(lay, st.mask(CHASM))

    # cave wall faces, two tiles deep, with fissures and icicles along the lip
    def face(d, x0, x1, y0, y1):
        rect(d, x0, y0, x1, y1, (70, 86, 114))
        x = x0 + rng.uniform(6, 20)
        while x < x1 - 6:
            vline(d, x, y0 + rng.uniform(0, 12), y1 - rng.uniform(8, 20), (52, 64, 88), 3)
            x += rng.uniform(16, 34)
        hline(d, x0, x1, y0 + 2, (98, 116, 146), 4)
        x = x0 + 4
        while x < x1 - 8:
            w, l = rng.uniform(5, 9), rng.uniform(10, 22)
            poly(d, [(x, y1 - TILE * 0.9), (x + w * 2, y1 - TILE * 0.9), (x + w, y1 - TILE * 0.9 + l)], (210, 236, 250))
            x += rng.uniform(10, 22)
        hline(d, x0, x1, y1 - 3, (44, 54, 76), 5)
    st.faces({BOUND}, set(st.open_codes), 2, face)

    # crevasse lips: a pale ledge ring just inside the pit
    st.outline(st.mask(CHASM), (86, 110, 150), 8)
    st.outline(st.mask(FLOOR, ICE, SNOW))

    # crystals light up the floor around them
    open_mask = st.mask(FLOOR, ICE, SNOW)
    centres = [((tx + w / 2) * TILE, (ty + h / 2) * TILE) for kind, tx, ty, w, h, kw in st.objects if kind == "crystal"]
    st.glow(centres, 80, (130, 230, 255), 0.35, open_mask)

    def crystal(d, x0, y0, x1, y1, rng, big=False):
        props.crystal(d, x0, y0, x1, y1, rng, fill=CRYSTAL, light=(220, 250, 255), edge=ROCK_EDGE)

    st.draw_objects({
        "crystal": crystal,
        "stalagmite": partial(props.spike, fill=(92, 108, 136), light=(170, 190, 216), edge=ROCK_EDGE),
        "ice_block": partial(props.boulder, fill=(196, 232, 246), edge=(40, 70, 100), shade=(130, 186, 214)),
    })
