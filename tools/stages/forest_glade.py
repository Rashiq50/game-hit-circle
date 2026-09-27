"""Stage 1: Forest Glade (56x32 tiles). One open clearing ringed by dense canopy, a dirt trail up from the south entry,
a stream cutting off the east side with a log bridge, and a light scatter of trees, rocks and bushes. Mostly open
ground so melee rushers can reach the player."""
from functools import partial
from common import Stage, TILE, rect, circle, mix
import props

BOUND, GRASS, DIRT, WATER, BRIDGE = 0, 1, 2, 3, 4

LEAF = ((52, 104, 58), (62, 118, 64), (46, 94, 52), (70, 128, 66))
EDGE = (18, 34, 22)


def build():
    st = Stage("forest_glade", 56, 32, seed=11, open_codes=(GRASS, DIRT, BRIDGE), pits={WATER: "Water"})

    # -- clearing: a few overlapping blobs, open to the south edge for the entry trail
    st.blob(GRASS, 28, 16, 23, 12)
    st.blob(GRASS, 15, 13, 10, 8)
    st.blob(GRASS, 42, 17, 11, 9)
    st.area(GRASS, 9, 8, 47, 24)
    st.area(GRASS, 25, 24, 31, 32)
    st.blob(GRASS, 46, 9, 6, 5)   # room north-east of the stream, reached over the bridge
    # -- trail from the entry north, with a branch east over the stream
    st.path(DIRT, [(28, 32), (28, 23), (24, 17), (25, 10), (30, 6)], 3, only={GRASS})
    st.path(DIRT, [(26, 17), (35, 15), (50, 15)], 3, only={GRASS})
    # -- stream from the north treeline to the east one; pond in the south-west
    st.path(WATER, [(38, 0), (39, 5), (42, 10), (44, 14), (47, 19), (49, 24), (50, 32)], 2, only={GRASS, DIRT})
    st.blob(WATER, 15, 19, 4, 3, only={GRASS})
    st.path(BRIDGE, [(35, 15), (50, 15)], 3, only={WATER})
    st.spawn = (28, 30)

    # -- props: a few hand-placed landmarks, then a light random scatter
    st.put("tree", 16, 7, 3, 3)
    st.put("tree", 35, 21, 3, 3)
    st.put("boulder", 20, 20, 2, 2)
    st.put("boulder", 45, 9, 2, 1)
    clear = [(24, 22, 33, 32), (20, 14, 31, 19)]  # entry and the trail's junction
    st.scatter("tree", 4, {GRASS}, sizes=((2, 2), (3, 3)), margin=2, keep_clear=clear)
    st.scatter("boulder", 4, {GRASS}, sizes=((1, 1), (2, 1)), margin=2, keep_clear=clear)
    st.scatter("bush", 7, {GRASS}, margin=2, keep_clear=clear)
    st.seal_pockets()
    st.pick_enemy_spawns(10)

    render(st)
    return st


def render(st):
    rng = st.rng
    # forest surround
    lay, d = st.layer((26, 48, 32))
    props.canopy(st, d, LEAF, EDGE)
    st.paint(lay, st.mask(BOUND))

    # grass
    lay, d = st.layer((98, 150, 74))
    props.tile_grid(st, d, (92, 143, 70))
    props.tufts(st, d, (122, 174, 88), 0.8)
    props.specks(st, d, [(236, 214, 110), (232, 150, 170), (240, 240, 235)], 0.12, 2, 3)
    st.paint(lay, st.mask(GRASS))

    # dirt trail
    lay, d = st.layer((158, 124, 84))
    props.tile_grid(st, d, (148, 115, 77))
    props.specks(st, d, [(128, 100, 68), (180, 148, 108)], 0.9, 2, 4)
    st.paint(lay, st.mask(DIRT))

    # water with a darker band along its banks
    lay, d = st.layer((72, 140, 192))
    props.ripples(st, d, (130, 188, 226), 0.5)
    st.paint(lay, st.mask(WATER))
    st.outline(st.mask(WATER), (54, 112, 164), 10)

    # log bridge
    lay, d = st.layer((0, 0, 0))
    props.planks(st, d, [(x, y) for y in range(st.th) for x in range(st.tw) if st.code(x, y) == BRIDGE],
                 (132, 92, 56), (84, 56, 34), across_x=True)
    st.paint(lay, st.mask(BRIDGE))

    # treeline face: the shadowed underside of the canopy with trunks, wherever it meets open ground
    def face(d, x0, x1, y0, y1):
        rect(d, x0, y1 - TILE, x1, y1, (20, 38, 24))
        x = x0 + rng.uniform(8, 24)
        while x < x1 - 10:
            w = rng.uniform(9, 14)
            rect(d, x - w / 2, y1 - 26, x + w / 2, y1, (96, 66, 42), EDGE, 3)
            x += rng.uniform(34, 60)
        for x in range(int(x0) + 12, int(x1) - 6, 28):  # crown hems overhanging the trunks
            circle(d, x, y1 - TILE, 14, mix(LEAF[0], (0, 0, 0), 0.25))
    st.faces({BOUND}, {GRASS, DIRT}, 1, face)

    st.outline(st.mask(GRASS, DIRT, BRIDGE))

    st.draw_objects({
        "tree": partial(props.tree, leaf=LEAF, edge=EDGE),
        "boulder": props.boulder,
        "bush": partial(props.bush, edge=EDGE),
    })
