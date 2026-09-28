"""Stage 7 (finale): Castle Keep (125x75 tiles). From the south: a grassy approach, a moat crossed by a drawbridge, the
curtain wall's gatehouse, then a wide bailey (stables, market stalls, well, smithy, training yard) up to the inner wall.
Its three gates lead into the keep: the pillared throne hall in the middle (the Warlord's arena), barracks over a library
and armoury to the west, and a chapel over a walled garden to the east. Walls are four tiles thick, with two-tile faces
of coursed brick hung with banners, torches and, in the chapel, stained glass."""
import math
from functools import partial
from common import Stage, TILE, LINE, GOLD, rect, hline, vline, circle, poly, mix, s
import props
from graveyard import pew, altar, lantern

BOUND, STONE, COBBLE, WOOD, MARBLE, GRASS, DIRT, WATER, BRIDGE = range(9)
OPEN = (STONE, COBBLE, WOOD, MARBLE, GRASS, DIRT, BRIDGE)

EDGE = (16, 14, 22)
TOP, TOP_SEAM = (62, 64, 80), (48, 50, 64)          # wall tops seen from above
BRICK, MORTAR, LIP = (98, 100, 118), (72, 74, 90), (132, 134, 152)
BANNER, CARPET = (150, 24, 36), (146, 22, 34)
WOOD_C, WOOD_D = (134, 98, 64), (94, 64, 40)
LEAF = ((52, 104, 58), (62, 118, 64), (46, 94, 52), (70, 128, 66))
LEAF_EDGE = (18, 34, 22)
FIRE = (255, 150, 60)

HALL = (44, 6, 81, 36)       # throne hall, tiles [x0, x1) x [y0, y1)
CHAPEL = (85, 6, 117, 20)
CURTAIN_FACE_Y = 65          # the outer wall's face looks out over the moat


def build():
    st = Stage("castle_floor", 125, 75, seed=77, open_codes=OPEN, pits={WATER: "Moat"})

    # -- outside: the approach road, the moat and the drawbridge, and the gate passage through the curtain wall
    st.area(GRASS, 0, 69, 125, 75)
    st.area(DIRT, 59, 69, 65, 75)
    st.area(WATER, 0, 65, 125, 69)
    st.area(BRIDGE, 59, 65, 65, 69)
    st.area(COBBLE, 59, 61, 65, 65)
    st.spawn = (62, 73)

    # -- bailey: a dirt yard crossed by cobbled roads
    st.area(DIRT, 8, 40, 117, 61)
    st.area(COBBLE, 59, 40, 65, 61)
    st.area(COBBLE, 8, 49, 117, 53)

    # -- inner wall gates, then the keep
    st.area(STONE, 59, 36, 65, 40); st.area(STONE, 20, 36, 26, 40); st.area(STONE, 99, 36, 105, 40)
    st.area(STONE, *HALL)
    st.area(WOOD, 8, 6, 40, 20)        # barracks
    st.area(WOOD, 8, 24, 40, 36)       # library and armoury
    st.area(MARBLE, *CHAPEL)
    st.area(GRASS, 85, 24, 117, 36)    # garden
    for x0, y0, x1, y1 in ((40, 11, 44, 16), (40, 28, 44, 33), (81, 11, 85, 16), (81, 28, 85, 33)):
        st.area(STONE, x0, y0, x1, y1)  # doors between the hall and the wings
    st.area(WOOD, 21, 20, 27, 24)       # barracks <-> library
    st.area(MARBLE, 98, 20, 104, 24)    # chapel <-> garden

    # -- throne hall: throne and braziers on the dais, two rows of pillars, armour stands along the walls
    st.put("throne", 60, 6, 4, 3)
    st.put("brazier", 56, 6); st.put("brazier", 67, 6)
    for y in (13, 18, 23, 28):
        st.put("pillar", 49, y, 2, 2); st.put("pillar", 74, y, 2, 2)
    for y in (20, 24):
        st.put("armour", 44, y); st.put("armour", 80, y)

    # -- barracks: bunks along the north wall with chests, a mess table, the weapon rack by the door
    for i, x in enumerate(range(10, 36, 4)):
        st.put("bed", x, 6, 2, 3)
        if i % 2 == 0:
            st.put("chest", x, 9)
    st.put("table", 16, 14, 9, 2)
    st.put("rack", 8, 19, 3, 1)
    # -- library and armoury: shelves on the north wall, reading tables, racks down the west wall
    for x in (8, 13, 30, 35):
        st.put("shelf", x, 24, 5, 1)
    st.put("desk", 12, 29, 3, 2); st.put("desk", 28, 29, 3, 2)
    st.put("rack", 8, 31, 1, 3)

    # -- chapel: altar under the windows, pews either side of the aisle, candelabras
    st.put("altar", 99, 6, 5, 2)
    for y in (10, 13, 16):
        st.put("pew", 89, y, 5, 1); st.put("pew", 107, y, 5, 1)
    st.put("candelabra", 91, 6); st.put("candelabra", 111, 6)
    # -- garden: fountain in the middle, hedged beds, two trees
    st.put("fountain", 99, 28, 5, 5)
    for x, y in ((88, 26), (108, 26), (88, 33), (108, 33)):
        st.put("hedge", x, y, 7, 1)
    st.put("tree", 90, 29, 2, 2); st.put("tree", 112, 29, 2, 2)

    # -- bailey
    st.put("stable", 8, 40, 12, 5)
    st.put("cart", 23, 44, 3, 2)
    for x, y in ((28, 45), (35, 45), (28, 55), (35, 55)):
        st.put("stall", x, y, 4, 2)
    st.put("well", 47, 44, 2, 2)
    st.put("forge", 108, 40, 4, 3)
    st.put("anvil", 106, 44)
    for x in range(80, 95, 4):
        for y in (54, 57):
            st.put("dummy", x, y)
    st.put("target", 100, 57, 2, 1); st.put("target", 105, 57, 2, 1)
    for x, y in ((57, 42), (66, 42), (57, 58), (66, 58), (57, 70), (66, 70)):
        st.put("lantern", x, y)

    st.put("cart", 23, 56, 3, 2)
    st.put("trough", 8, 47, 3, 1)
    for x, y in ((12, 57), (14, 57), (13, 56)):   # a stacked pile, no gaps to get stuck in
        st.put("hay", x, y, 2, 1)
    for x, y in ((42, 56), (74, 45), (88, 44)):
        st.put("stall", x, y, 4, 2)
    st.put("statue", 76, 55, 2, 2)
    st.put("tent", 113, 55, 4, 4)
    road = [(55, 40, 70, 75)]  # main road from the drawbridge to the throne hall
    st.scatter("hay", 6, {DIRT}, sizes=((1, 1), (2, 1)), margin=2, keep_clear=road + [(20, 40, 60, 49)])
    st.scatter("barrel", 12, {DIRT}, margin=2, keep_clear=road)
    st.scatter("crate", 10, {DIRT}, margin=2, keep_clear=road)
    st.scatter("bush", 10, {GRASS}, margin=2, keep_clear=road + [(84, 23, 118, 37)])
    st.scatter("tree", 12, {GRASS}, sizes=((2, 2), (3, 3)), margin=2, keep_clear=[(52, 69, 72, 75), (84, 23, 118, 37)])
    st.seal_pockets()
    st.pick_enemy_spawns(52)   # MaxAtOnce 40, plus headroom

    render(st)
    return st


# ---- props ---------------------------------------------------------------------------------------------------------


def throne(d, x0, y0, x1, y1, rng):
    cx = (x0 + x1) / 2
    rect(d, x0 + 8, y0 + 4, x1 - 8, y1 - 4, (70, 14, 22), GOLD, LINE)                   # high back
    for i, h in ((-1, 16), (0, 28), (1, 16)):                                            # crown finials
        px = cx + i * 26
        poly(d, [(px - 10, y0 + 20), (px, y0 + 20 - h), (px + 10, y0 + 20)], GOLD, EDGE, 2)
    rect(d, x0 + 20, y0 + 34, x1 - 20, y1 - 10, CARPET, EDGE, 3)                           # cushion
    for side in (-1, 1):                                                                 # arm rests
        ax = cx + side * (x1 - x0) * 0.36
        rect(d, ax - 9, y0 + 40, ax + 9, y1 - 6, (90, 20, 28), GOLD, 3)
    circle(d, cx, y0 + 26, 7, (120, 220, 255), EDGE, 2)                                  # jewel


def brazier(d, x0, y0, x1, y1, rng):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    poly(d, [(cx - 12, cy - 4), (cx + 12, cy - 4), (cx + 7, y1 - 3), (cx - 7, y1 - 3)], (80, 70, 60), EDGE, 3)
    for r, c in ((10, (255, 110, 30)), (6, (255, 190, 70)), (3, (255, 245, 180))):
        poly(d, [(cx - r, cy - 3), (cx, cy - 3 - r * 2), (cx + r, cy - 3)], c)


def armour(d, x0, y0, x1, y1, rng):
    cx = (x0 + x1) / 2
    steel, dark = (170, 176, 190), (90, 96, 110)
    rect(d, cx - 12, y1 - 6, cx + 12, y1 - 2, (80, 70, 60), EDGE, 2)                     # plinth
    poly(d, [(cx - 10, y0 + 14), (cx + 10, y0 + 14), (cx + 7, y1 - 7), (cx - 7, y1 - 7)], steel, EDGE, 2)
    circle(d, cx, y0 + 9, 7, steel, EDGE, 2)                                              # helm
    hline(d, cx - 4, cx + 4, y0 + 9, dark, 2)                                             # visor
    vline(d, cx + 13, y0 + 3, y1 - 4, dark, 2)                                            # halberd
    poly(d, [(cx + 13, y0 + 2), (cx + 18, y0 + 8), (cx + 13, y0 + 11)], steel, EDGE, 1)


def bed(d, x0, y0, x1, y1, rng):
    rect(d, x0 + 4, y0 + 2, x1 - 4, y1 - 4, WOOD_D, EDGE, 3)                               # frame
    rect(d, x0 + 8, y0 + 18, x1 - 8, y1 - 8, (70, 90, 150), EDGE, 2)                       # blanket
    rect(d, x0 + 10, y0 + 6, x1 - 10, y0 + 18, (226, 222, 214), EDGE, 2)                   # pillow
    hline(d, x0 + 8, x1 - 8, y0 + 40, (96, 116, 176), 3)


def chest(d, x0, y0, x1, y1, rng):
    rect(d, x0 + 4, y0 + 8, x1 - 4, y1 - 3, WOOD_C, EDGE, 3)
    rect(d, x0 + 4, y0 + 8, x1 - 4, y0 + 15, WOOD_D, EDGE, 2)
    rect(d, (x0 + x1) / 2 - 3, y0 + 13, (x0 + x1) / 2 + 3, y0 + 19, GOLD)


def table(d, x0, y0, x1, y1, rng):
    rect(d, x0 + 2, y0 + 6, x1 - 2, y1 - 6, WOOD_C, EDGE, LINE)
    for x in range(int(x0) + 28, int(x1) - 16, 40):                                        # plates and cups
        circle(d, x, y0 + 22, 7, (220, 216, 206), EDGE, 2)
        circle(d, x + 14, y0 + 38, 4, (200, 160, 60), EDGE, 1)
    hline(d, x0 + 6, x1 - 6, (y0 + y1) / 2 + 4, WOOD_D, 2)


def rack(d, x0, y0, x1, y1, rng):
    """Weapon rack: a beam with blades and spears standing in it."""
    steel = (180, 190, 205)
    if x1 - x0 >= y1 - y0:
        rect(d, x0 + 2, y1 - 12, x1 - 2, y1 - 4, WOOD_D, EDGE, 2)
        for x in range(int(x0) + 10, int(x1) - 4, 14):
            vline(d, x, y0 + 4, y1 - 10, steel, 3)
            poly(d, [(x - 4, y0 + 8), (x, y0 + 1), (x + 4, y0 + 8)], steel)
    else:
        rect(d, x0 + 4, y0 + 2, x0 + 12, y1 - 2, WOOD_D, EDGE, 2)
        for y in range(int(y0) + 10, int(y1) - 4, 14):
            hline(d, x0 + 10, x1 - 3, y, steel, 3)
            poly(d, [(x1 - 8, y - 4), (x1 - 1, y), (x1 - 8, y + 4)], steel)


def shelf(d, x0, y0, x1, y1, rng):
    rect(d, x0 + 1, y0 + 2, x1 - 1, y1 - 2, WOOD_D, EDGE, 3)
    x = x0 + 5
    while x < x1 - 6:
        w = rng.uniform(4, 8)
        rect(d, x, y0 + 6 + rng.uniform(0, 5), x + w, y1 - 6,
             rng.choice([(150, 40, 40), (40, 80, 140), (60, 120, 60), (170, 130, 50), (110, 60, 120)]))
        x += w + 1


def desk(d, x0, y0, x1, y1, rng):
    rect(d, x0 + 4, y0 + 6, x1 - 4, y1 - 6, WOOD_C, EDGE, LINE)
    rect(d, x0 + 14, y0 + 16, x0 + 40, y0 + 34, (236, 228, 200), EDGE, 2)                  # open book
    vline(d, x0 + 27, y0 + 16, y0 + 34, EDGE, 2)
    rect(d, x1 - 26, y0 + 14, x1 - 20, y0 + 28, (236, 228, 200))                          # candle
    circle(d, x1 - 23, y0 + 11, 3, (255, 200, 90))


def candelabra(d, x0, y0, x1, y1, rng):
    cx = (x0 + x1) / 2
    vline(d, cx, y0 + 12, y1 - 4, GOLD, 3)
    hline(d, cx - 10, cx + 10, y0 + 14, GOLD, 3)
    for dx in (-10, 0, 10):
        rect(d, cx + dx - 2, y0 + 6, cx + dx + 2, y0 + 14, (240, 234, 214))
        circle(d, cx + dx, y0 + 4, 2, (255, 210, 110))


def fountain(d, x0, y0, x1, y1, rng):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    r = (x1 - x0) / 2 - 6
    circle(d, cx, cy, r, (150, 146, 158), EDGE, LINE)                                      # basin rim
    circle(d, cx, cy, r - 12, (70, 140, 190), EDGE, 3)                                     # water
    for k in range(3):
        d.arc([s(cx - r + 24 + k * 8), s(cy - 10 + k * 12), s(cx + r - 40 - k * 6), s(cy + 6 + k * 12)], 200, 340,
              fill=(150, 200, 235), width=2)
    circle(d, cx, cy, 20, (150, 146, 158), EDGE, 3)                                        # centre plinth
    circle(d, cx, cy, 8, (190, 230, 250))


def hedge(d, x0, y0, x1, y1, rng):
    d.rounded_rectangle([s(x0 + 2), s(y0 + 3), s(x1 - 2), s(y1 - 2)], radius=s(12), fill=(50, 100, 54),
                        outline=LEAF_EDGE, width=3)
    for x in range(int(x0) + 10, int(x1) - 6, 12):
        circle(d, x + rng.uniform(-2, 2), y0 + 12 + rng.uniform(-2, 2), 5, (70, 128, 66))
        if rng.random() < 0.3:
            circle(d, x, y0 + 20, 3, (230, 90, 110))                                       # roses


def stable(d, x0, y0, x1, y1, rng):
    roof_bottom = y0 + (y1 - y0) * 0.55
    rect(d, x0 + 2, roof_bottom, x1 - 2, y1 - 2, WOOD_C, EDGE, LINE)                         # front wall
    for x in range(int(x0) + 16, int(x1) - 30, 64):                                          # stall doors
        rect(d, x, roof_bottom + 10, x + 44, y1 - 4, WOOD_D, EDGE, 3)
        d.line([(s(x), s(roof_bottom + 10)), (s(x + 44), s(y1 - 4))], fill=EDGE, width=2)
    rect(d, x0, y0 + 2, x1, roof_bottom + 4, (120, 60, 46), EDGE, LINE)                      # shingled roof
    for y in range(int(y0) + 12, int(roof_bottom), 12):
        hline(d, x0 + 3, x1 - 3, y, (96, 46, 36), 2)
    hline(d, x0, x1, y0 + 8, (150, 80, 60), 4)                                               # ridge


def cart(d, x0, y0, x1, y1, rng):
    rect(d, x0 + 6, y0 + 8, x1 - 16, y1 - 14, WOOD_C, EDGE, 3)
    for x in range(int(x0) + 10, int(x1) - 20, 10):                                          # hay load
        circle(d, x + 4, y0 + 18 + rng.uniform(-3, 3), 7, (214, 184, 90))
    for wx in (x0 + 18, x1 - 30):
        circle(d, wx, y1 - 10, 9, (80, 56, 36), EDGE, 3)
    d.line([(s(x1 - 16), s(y0 + 24)), (s(x1 - 2), s(y0 + 20))], fill=WOOD_D, width=4)       # shafts


def stall(d, x0, y0, x1, y1, rng):
    """Market stall: a counter of goods under a striped awning."""
    rect(d, x0 + 4, y0 + (y1 - y0) * 0.5, x1 - 4, y1 - 4, WOOD_C, EDGE, 3)
    for x in range(int(x0) + 12, int(x1) - 8, 14):
        circle(d, x, y0 + (y1 - y0) * 0.5 + 10, 5, rng.choice([(220, 70, 60), (240, 190, 60), (110, 180, 70)]))
    stripe = rng.choice([((200, 50, 50), (236, 226, 210)), ((50, 90, 170), (236, 226, 210)), ((60, 130, 70), (236, 226, 210))])
    w = (x1 - x0 - 4) / 8
    for k in range(8):
        rect(d, x0 + 2 + k * w, y0 + 3, x0 + 2 + (k + 1) * w, y0 + (y1 - y0) * 0.42, stripe[k % 2])
    rect(d, x0 + 2, y0 + 3, x1 - 2, y0 + (y1 - y0) * 0.42, None, EDGE, 3)


def well(d, x0, y0, x1, y1, rng):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2 + 4
    circle(d, cx, cy, 26, (140, 136, 148), EDGE, LINE)
    circle(d, cx, cy, 16, (24, 30, 44))
    rect(d, cx - 28, y0 + 4, cx + 28, y0 + 12, WOOD_D, EDGE, 2)                            # windlass beam
    vline(d, cx, y0 + 12, cy - 4, (170, 150, 110), 2)


def forge(d, x0, y0, x1, y1, rng):
    rect(d, x0 + 4, y0 + 6, x1 - 4, y1 - 4, (84, 80, 88), EDGE, LINE)
    rect(d, x0 + 20, y0 + 36, x1 - 20, y1 - 12, (255, 120, 50), EDGE, 3)                    # hearth
    rect(d, x0 + 32, y0 + 44, x1 - 32, y1 - 18, (255, 210, 110))
    rect(d, x1 - 36, y0 + 2, x1 - 14, y0 + 30, (70, 66, 74), EDGE, 3)                       # chimney


def anvil(d, x0, y0, x1, y1, rng):
    cx = (x0 + x1) / 2
    rect(d, cx - 5, y0 + 16, cx + 5, y1 - 4, (60, 60, 70), EDGE, 2)
    poly(d, [(cx - 13, y0 + 8), (cx + 9, y0 + 8), (cx + 14, y0 + 12), (cx + 9, y0 + 17), (cx - 13, y0 + 17)],
         (90, 92, 104), EDGE, 2)


def barrel(d, x0, y0, x1, y1, rng):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    circle(d, cx, cy, 12, WOOD_C, EDGE, 3)
    d.ellipse([s(cx - 12), s(cy - 5), s(cx + 12), s(cy + 5)], outline=(70, 70, 80), width=2)
    circle(d, cx, cy, 5, WOOD_D)


def crate(d, x0, y0, x1, y1, rng):
    rect(d, x0 + 4, y0 + 4, x1 - 4, y1 - 4, (160, 124, 80), EDGE, 3)
    d.line([(s(x0 + 5), s(y0 + 5)), (s(x1 - 5), s(y1 - 5))], fill=WOOD_D, width=3)
    d.line([(s(x1 - 5), s(y0 + 5)), (s(x0 + 5), s(y1 - 5))], fill=WOOD_D, width=3)


def hay(d, x0, y0, x1, y1, rng):
    rect(d, x0 + 3, y0 + 6, x1 - 3, y1 - 4, (214, 184, 90), EDGE, 3)
    for x in range(int(x0) + 8, int(x1) - 4, 6):
        vline(d, x, y0 + 9, y1 - 7, (186, 156, 70), 2)
    hline(d, x0 + 3, x1 - 3, (y0 + y1) / 2, (130, 90, 50), 2)


def dummy(d, x0, y0, x1, y1, rng):
    cx = (x0 + x1) / 2
    vline(d, cx, y0 + 8, y1 - 3, WOOD_D, 4)
    hline(d, cx - 11, cx + 11, y0 + 15, WOOD_D, 4)
    d.ellipse([s(cx - 7), s(y0 + 12), s(cx + 7), s(y1 - 6)], fill=(200, 170, 100), outline=EDGE, width=2)
    circle(d, cx, y0 + 7, 5, (200, 170, 100), EDGE, 2)


def target(d, x0, y0, x1, y1, rng):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    rect(d, cx - 22, cy + 4, cx + 22, y1 - 2, (186, 156, 70), EDGE, 2)                     # straw butt
    for r, c in ((15, (240, 236, 226)), (10, (200, 50, 50)), (5, GOLD)):
        circle(d, cx, cy - 2, r, c, EDGE, 2)


def trough(d, x0, y0, x1, y1, rng):
    rect(d, x0 + 3, y0 + 6, x1 - 3, y1 - 4, WOOD_D, EDGE, 3)
    rect(d, x0 + 8, y0 + 10, x1 - 8, y1 - 9, (70, 130, 170))


def statue(d, x0, y0, x1, y1, rng):
    """A knight on a plinth, sword planted."""
    cx = (x0 + x1) / 2
    stone, shade = (160, 158, 170), (116, 114, 128)
    rect(d, x0 + 8, y1 - 22, x1 - 8, y1 - 3, shade, EDGE, LINE)
    rect(d, x0 + 4, y1 - 28, x1 - 4, y1 - 20, stone, EDGE, 3)
    poly(d, [(cx - 14, y1 - 28), (cx - 10, y0 + 22), (cx + 10, y0 + 22), (cx + 14, y1 - 28)], stone, EDGE, 3)
    circle(d, cx, y0 + 14, 9, stone, EDGE, 3)
    vline(d, cx, y0 + 26, y1 - 30, shade, 4)
    hline(d, cx - 8, cx + 8, y0 + 30, shade, 3)


def tent(d, x0, y0, x1, y1, rng):
    """A striped pavilion seen from above: four canvas panels meeting at a peak."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    a, b = (200, 50, 50), (236, 226, 210)
    pts = [(x0 + 6, y0 + 6), (x1 - 6, y0 + 6), (x1 - 6, y1 - 6), (x0 + 6, y1 - 6)]
    for i in range(4):
        poly(d, [pts[i], pts[(i + 1) % 4], (cx, cy)], a if i % 2 else b, EDGE, 3)
    circle(d, cx, cy, 6, GOLD, EDGE, 2)
    poly(d, [(cx, cy - 4), (cx + 22, cy - 12), (cx, cy - 18)], BANNER, EDGE, 2)           # pennant


def tower(d, cx, cy, r, bottom):
    """A round tower: its lit, crenellated top at (cx, cy) over a brick body running down to `bottom`."""
    rect(d, cx - r, cy, cx + r, bottom, BRICK, EDGE, LINE)
    for y in range(int(cy) + 14, int(bottom), 16):
        hline(d, cx - r + 3, cx + r - 3, y, MORTAR, 2)
    for x in (cx - r * 0.45, cx + r * 0.45):
        rect(d, x - 3, cy + 18, x + 3, min(bottom - 6, cy + 40), (20, 18, 28))           # arrow slits
    circle(d, cx, cy, r, LIP, EDGE, LINE)
    circle(d, cx, cy, r * 0.72, TOP, EDGE, 3)
    for k in range(10):                                                                  # merlons
        a = k / 10 * math.tau
        circle(d, cx + math.cos(a) * r * 0.86, cy + math.sin(a) * r * 0.86, r * 0.1, (150, 152, 170), EDGE, 1)
    circle(d, cx, cy, r * 0.2, BANNER, EDGE, 2)                                            # flag on the roof


# ---- render --------------------------------------------------------------------------------------------------------


def slabs(st, d, fill, seam, w=64, h=32, vary=10):
    """Staggered rectangular flagstones with a little colour variation."""
    rng = st.rng
    for row, y in enumerate(range(0, st.H, h)):
        off = (w // 2) if row % 2 else 0
        for x in range(-off, st.W, w):
            k = rng.randint(-vary, vary)                     # brightness only, so the stone stays one hue
            c = tuple(max(0, min(255, v + k)) for v in fill)
            rect(d, x, y, x + w, y + h, c)
            hline(d, x, x + w, y, seam, 2)
            vline(d, x, y, y + h, seam, 2)


def render(st):
    rng = st.rng
    torches = []

    # wall tops: big ashlar blocks
    lay, d = st.layer(TOP)
    slabs(st, d, TOP, TOP_SEAM, 96, 48, 6)
    st.paint(lay, st.mask(BOUND))

    # floors
    lay, d = st.layer((0, 0, 0))
    slabs(st, d, (96, 92, 108), (78, 74, 90))
    props.cracks(st, d, (74, 70, 86), 0.03, 2)
    st.paint(lay, st.mask(STONE))

    lay, d = st.layer((118, 114, 122))
    for x, y in props.points(st, props.density(st, 4.5)):
        r = rng.uniform(5, 9)
        d.ellipse([s(x - r), s(y - r * 0.8), s(x + r), s(y + r * 0.8)],
                  fill=rng.choice([(140, 134, 138), (130, 126, 132), (150, 142, 140)]), outline=(96, 92, 100), width=2)
    st.paint(lay, st.mask(COBBLE))

    lay, d = st.layer(WOOD_C)
    for row, y in enumerate(range(0, st.H, 16)):
        hline(d, 0, st.W, y, WOOD_D, 2)
        for x in range((row * 37) % 96, st.W, 96):
            vline(d, x, y, y + 16, WOOD_D, 2)
    props.specks(st, d, [(150, 112, 74)], 0.3, 1, 2)
    st.paint(lay, st.mask(WOOD))

    lay, d = st.layer((196, 192, 206))
    for y in range(0, st.H, TILE):
        for x in range(0, st.W, TILE):
            if (x // TILE + y // TILE) % 2:
                rect(d, x, y, x + TILE, y + TILE, (164, 160, 180))
    st.paint(lay, st.mask(MARBLE))

    lay, d = st.layer((98, 150, 74))
    props.tile_grid(st, d, (92, 143, 70))
    props.tufts(st, d, (122, 174, 88), 0.7)
    props.specks(st, d, [(236, 214, 110), (232, 150, 170), (240, 240, 235)], 0.1, 2, 3)
    st.paint(lay, st.mask(GRASS))

    lay, d = st.layer((150, 122, 90))
    props.tile_grid(st, d, (142, 115, 84))
    props.specks(st, d, [(126, 100, 72), (172, 144, 108)], 0.8, 2, 4)
    props.tufts(st, d, (140, 150, 80), 0.05)
    st.paint(lay, st.mask(DIRT))

    lay, d = st.layer((52, 96, 124))
    props.ripples(st, d, (100, 150, 180), 0.5)
    st.paint(lay, st.mask(WATER))
    st.outline(st.mask(WATER), (40, 76, 104), 10)

    lay, d = st.layer((0, 0, 0))
    props.planks(st, d, [(x, y) for y in range(st.th) for x in range(st.tw) if st.code(x, y) == BRIDGE],
                 (122, 86, 54), (78, 52, 32), across_x=False)
    st.paint(lay, st.mask(BRIDGE))

    # -- floor dressing (walkable): dais, carpets, rugs
    d = st.d
    hx0, hy0, hx1, hy1 = HALL
    rect(d, 54 * TILE, hy0 * TILE, 71 * TILE, 11 * TILE, (120, 112, 130), EDGE, 3)          # dais
    for k in (1, 2):
        hline(d, 54 * TILE + 3, 71 * TILE - 3, 11 * TILE - k * 10, (104, 96, 114), 2)
    rect(d, 60 * TILE + 6, 9 * TILE, 64 * TILE - 6, 40 * TILE, CARPET, GOLD, 4)             # carpet to the throne
    rect(d, 97 * TILE + 8, 8 * TILE, 105 * TILE - 8, 20 * TILE, (60, 50, 120), GOLD, 3)     # chapel aisle runner
    rect(d, 15 * TILE, 12 * TILE, 26 * TILE, 18 * TILE, (120, 40, 44), (200, 160, 70), 3)   # mess rug
    d.ellipse([s(18 * TILE), s(27 * TILE), s(28 * TILE), s(34 * TILE)], fill=(50, 70, 120), outline=GOLD, width=3)
    for x, y in props.points(st, props.density(st, 0.05)):                                  # straw about the stables
        if st.code(int(x // TILE), int(y // TILE)) == DIRT and x < 32 * TILE:
            d.line([(s(x), s(y)), (s(x + rng.uniform(-8, 8)), s(y + rng.uniform(-4, 4)))], fill=(214, 184, 90), width=2)

    # -- wall faces: two courses of brick with a crenellated lip, hung with whatever suits the room below
    def face(d, x0, x1, y0, y1):
        rect(d, x0, y0, x1, y1, BRICK)
        for row, y in enumerate(range(int(y0), int(y1), 16)):
            hline(d, x0, x1, y, MORTAR, 2)
            for x in range(int(x0) + (16 if row % 2 else 0), int(x1), 32):
                vline(d, x, y, y + 16, MORTAR, 2)
        rect(d, x0, y0, x1, y0 + 8, LIP)
        for x in range(int(x0), int(x1), 24):
            rect(d, x + 2, y0 - 6, x + 12, y0 + 2, LIP, EDGE, 1)                             # merlons
        hline(d, x0, x1, y0 + 8, EDGE, 2)
        tx, ty = x0 / TILE, y1 / TILE
        in_chapel = CHAPEL[0] <= tx < CHAPEL[2] and ty == CHAPEL[1]
        in_hall = HALL[0] <= tx < HALL[2] and ty == HALL[1]
        outer = ty == CURTAIN_FACE_Y
        pitch = 110 if in_chapel else 150
        for i, cx in enumerate(props_spaced(x0, x1, pitch)):
            if in_chapel:                                          # stained glass
                glass = [(200, 60, 70), (60, 110, 210), (230, 190, 70), (80, 170, 110)][i % 4]
                d.pieslice([s(cx - 18), s(y0 + 12), s(cx + 18), s(y0 + 48)], 180, 360, fill=glass, outline=EDGE, width=3)
                rect(d, cx - 18, y0 + 30, cx + 18, y1 - 8, glass, EDGE, 3)
                vline(d, cx, y0 + 14, y1 - 8, EDGE, 2); hline(d, cx - 18, cx + 18, y0 + 40, EDGE, 2)
                stained.append((cx, y1 + 60, glass))
            elif outer and i % 2 == 0:                               # arrow slits on the curtain
                rect(d, cx - 4, y0 + 18, cx + 4, y1 - 12, (20, 18, 28))
            elif (i % 2 == 0) or in_hall:                            # banners
                poly(d, [(cx - 16, y0 + 10), (cx + 16, y0 + 10), (cx + 16, y1 - 12), (cx, y1 - 4), (cx - 16, y1 - 12)],
                     BANNER, EDGE, 3)
                hline(d, cx - 16, cx + 16, y0 + 16, GOLD, 3)
                circle(d, cx, y0 + 34, 7, GOLD, EDGE, 2)
            else:                                                     # torch sconce
                rect(d, cx - 3, y0 + 30, cx + 3, y0 + 48, (70, 60, 50), EDGE, 2)
                poly(d, [(cx - 7, y0 + 30), (cx, y0 + 14), (cx + 7, y0 + 30)], FIRE)
                poly(d, [(cx - 3, y0 + 30), (cx, y0 + 21), (cx + 3, y0 + 30)], (255, 240, 170))
                torches.append((cx, y1 + 10))
        hline(d, x0, x1, y1 - 2, EDGE, 3)

    stained = []
    st.faces({BOUND}, set(OPEN) | {WATER}, 2, face)

    # towers: on the keep's corners, the curtain's ends and flanking the gatehouse
    for cx, cy, r, bottom in ((4, 2.6, 2.3, 5.6), (121, 2.6, 2.3, 5.6), (4, 61.9, 1.8, 65), (121, 61.9, 1.8, 65),
                              (56.5, 62.7, 1.6, 65), (67.5, 62.7, 1.6, 65)):
        tower(st.d, cx * TILE, cy * TILE, r * TILE, bottom * TILE)

    open_mask = st.mask(*OPEN)
    st.glow(torches, 70, (255, 190, 110), 0.3, open_mask)
    for cx, cy, glass in stained:
        st.glow([(cx, cy)], 64, glass, 0.3, open_mask)
    lights = [((tx + w / 2) * TILE, (ty + h / 2) * TILE) for kind, tx, ty, w, h, kw in st.objects
              if kind in ("brazier", "forge", "lantern", "candelabra")]
    st.glow(lights, 80, (255, 180, 90), 0.35, open_mask)
    st.outline(open_mask)

    st.draw_objects({
        "throne": throne, "brazier": brazier, "armour": armour,
        "pillar": partial(props.column, top=(150, 146, 160), face=(104, 100, 116), edge=EDGE),
        "bed": bed, "chest": chest, "table": table, "rack": rack, "shelf": shelf, "desk": desk,
        "altar": altar, "pew": pew, "candelabra": candelabra,
        "fountain": fountain, "hedge": hedge, "tree": partial(props.tree, leaf=LEAF, edge=LEAF_EDGE),
        "stable": stable, "cart": cart, "stall": stall, "well": well, "forge": forge, "anvil": anvil,
        "barrel": barrel, "crate": crate, "hay": hay, "dummy": dummy, "target": target, "lantern": lantern,
        "trough": trough, "statue": statue, "tent": tent, "bush": partial(props.bush, edge=LEAF_EDGE),
    })


def props_spaced(x0, x1, pitch):
    """Centres for face decorations along a wall run, evenly spread and kept clear of its ends."""
    n = int((x1 - x0 - 40) // pitch)
    if n <= 0:
        return [(x0 + x1) / 2] if x1 - x0 >= 64 else []
    start = (x0 + x1) / 2 - (n - 1) * pitch / 2
    return [start + i * pitch for i in range(n)]
