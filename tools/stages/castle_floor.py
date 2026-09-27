"""Stage 7 (finale): a whole castle floor (rooms, halls, corridors) in flat fills with dark outlines.
Layout is in world units (4000x2400 = 125x75 tiles of 32); every edge sits on the 32 grid. Walls are generated around the
union of every rect, so rooms are just rectangles and a corridor that touches two rooms opens a doorway between them.
castle_floor.tmx holds hand-made collision, so this generator only ever writes the PNG."""
from types import SimpleNamespace
from PIL import Image, ImageDraw, ImageFilter
from common import S, TILE, LINE, DARK, GOLD, VOID, s, rect, hline, vline, circle, arch, grid, pillar, spaced

TW, TH = 125, 75
WORLD_W, WORLD_H = TW * TILE, TH * TILE
W, H = WORLD_W * S, WORLD_H * S
WALL = TILE                # wall thickness around every interior rect

WALL_STONE = (50, 53, 70)
WALL_MORTAR = (40, 42, 56)

THEMES = {
    # wall = north-face band, floor, grid = tile lines, plus a couple of accents each theme uses
    "stone":       dict(wall=(78, 84, 108), floor=(104, 112, 134), grid=(94, 102, 122), dark=(14, 14, 22)),
    "barracks":    dict(wall=(63, 72, 204), floor=(112, 148, 190), grid=(100, 134, 176), dark=(12, 12, 24)),
    "ember":       dict(wall=(78, 26, 30), floor=(98, 72, 64), grid=(88, 64, 56), dark=(30, 10, 8)),
    "foundry":     dict(wall=(95, 115, 140), floor=(128, 138, 150), grid=(112, 122, 134), dark=(30, 40, 55)),
    "observatory": dict(wall=(70, 40, 130), floor=(86, 70, 128), grid=(76, 60, 116), dark=(35, 15, 60)),
    "crypt":       dict(wall=(24, 40, 72), floor=(34, 66, 84), grid=(30, 58, 76), dark=(8, 14, 26)),
    "throne":      dict(wall=(110, 16, 28), floor=(70, 26, 30), grid=(64, 22, 26), dark=(40, 5, 10)),
}


class Room:
    def __init__(self, name, x0, y0, x1, y1, theme, band=90):
        self.name, self.x0, self.y0, self.x1, self.y1, self.theme, self.band = name, x0, y0, x1, y1, theme, band

    @property
    def w(self): return self.x1 - self.x0
    @property
    def h(self): return self.y1 - self.y0
    @property
    def cx(self): return (self.x0 + self.x1) / 2


# ---- Floor plan --------------------------------------------------------------------------------------------------
# Three rows: wings top (barracks / throne / observatory), middle (foundry / crypt), a great corridor as the spine,
# then kitchens / entrance hall / guard room along the bottom. Corridors (band=32 or 0) glue them together.
# Coordinates are tile counts * 32; bands are 3 tiles in rooms, 2 in the spine, 1 in the side passages.
ROOMS = [
    Room("throne", 1408, 160, 2592, 992, "throne", 96),
    Room("barracks", 320, 160, 1184, 800, "barracks", 96),
    Room("observatory", 2816, 160, 3680, 800, "observatory", 96),
    Room("foundry", 320, 960, 1184, 1504, "foundry", 96),
    Room("crypt", 2816, 960, 3680, 1504, "crypt", 96),
    Room("spine", 320, 1536, 3680, 1792, "stone", 64),
    Room("entrance", 1504, 1856, 2496, 2304, "stone", 96),
    Room("kitchen", 320, 1856, 1184, 2304, "ember", 96),
    Room("guard", 2816, 1856, 3680, 2304, "barracks", 96),
    # corridors: horizontal ones get a low band, vertical ones open straight into the room above (no band).
    # Doorways are 6 tiles wide; the short vertical ones are just the doorway through a shared wall.
    Room("c_bar_throne", 1184, 448, 1408, 608, "stone", 32),
    Room("c_obs_throne", 2592, 448, 2816, 608, "stone", 32),
    Room("c_bar_foundry", 640, 800, 832, 960, "stone", 0),
    Room("c_obs_crypt", 3136, 800, 3328, 960, "stone", 0),
    Room("c_throne_spine", 1920, 992, 2112, 1536, "stone", 0),
    Room("c_foundry_spine", 640, 1504, 832, 1536, "stone", 0),
    Room("c_crypt_spine", 3136, 1504, 3328, 1536, "stone", 0),
    Room("c_spine_entrance", 1920, 1792, 2112, 1856, "stone", 0),
    Room("c_spine_kitchen", 640, 1792, 832, 1856, "stone", 0),
    Room("c_spine_guard", 3136, 1792, 3328, 1856, "stone", 0),
]
GATE = (1920, 2304, 2112, 2400)  # main gate through the south wall below the entrance hall, 6 tiles wide
for _r in ROOMS + [Room("gate", *GATE, "stone", 0)]:
    assert all(v % TILE == 0 for v in (_r.x0, _r.y0, _r.x1, _r.y1, _r.band)), f"{_r.name} is off the {TILE} grid"


# ---- North-face bands: same decorations as the hallway images, laid out per wall segment --------------------------


def band_decor(d, theme, x0, x1, y0, y1, t):
    h = y1 - y0
    if theme == "stone":
        for cx in spaced(x0, x1, 12, 220):
            rect(d, cx - 4, y0 + h * 0.45, cx + 4, y0 + h * 0.8, DARK)
            circle(d, cx, y0 + h * 0.4, 8, GOLD, DARK, 2)
    elif theme == "barracks":
        for cx in spaced(x0, x1, 55, 170):
            rect(d, cx - 27, y0 + 20, cx + 28, y0 + h - 25, t["wall"], t["dark"])
    elif theme == "ember":
        for i, cx in enumerate(spaced(x0, x1, 70, 150)):
            if i % 2 == 0:
                arch(d, cx - 35, y0 + 20, 70, h - 20, (235, 110, 40), t["dark"])
                arch(d, cx - 21, y0 + 40, 42, h - 40, (255, 170, 70), t["dark"])
            else:
                rect(d, cx - 4, y0 + 40, cx + 4, y0 + 70, DARK); circle(d, cx, y0 + 35, 10, GOLD, DARK, 2)
    elif theme == "foundry":
        for x in range(int(x0), int(x1) + 1, 200):
            vline(d, x, y0, y1, t["dark"], 3)
        for x in range(int(x0) + 25, int(x1), 50):
            circle(d, x, y0 + 12, 4, (180, 195, 210)); circle(d, x, y1 - 12, 4, (180, 195, 210))
        for cx in spaced(x0, x1, 140, 300):
            rect(d, cx - 70, y0 + 25, cx + 70, y1, (70, 85, 105), t["dark"])
            for y in range(int(y0) + 40, int(y1), 18):
                hline(d, cx - 62, cx + 62, y, t["dark"], 3)
            vline(d, cx, y0 + 25, y1, t["dark"], 3)
    elif theme == "observatory":
        lens = (240, 230, 255)
        cs = spaced(x0, x1, 24, 110)
        mid = len(cs) // 2
        for i, cx in enumerate(cs):
            if i == mid:
                circle(d, cx, y0 + h / 2, 40, (120, 60, 200), t["dark"])
                circle(d, cx, y0 + h / 2, 25, (40, 20, 70), lens, 3)
                hline(d, cx - 30, cx + 30, y0 + h / 2, lens, 3); vline(d, cx, y0 + h / 2 - 30, y0 + h / 2 + 30, lens, 3)
            else:
                rect(d, cx - 12, y0 + 15, cx + 12, y1 - 10, (120, 60, 200), t["dark"])
                vline(d, cx, y0 + 15, y1 - 10, lens, 3)
    elif theme == "crypt":
        for cx in spaced(x0, x1, 68, 180):
            circle(d, cx, y0 + h / 2, 32, (40, 90, 120), t["dark"])
            circle(d, cx, y0 + h / 2, 19, (80, 220, 255), t["dark"], 3)
            circle(d, cx, y0 + h / 2, 7, (200, 250, 255))
    elif theme == "throne":
        hline(d, x0, x1, y0 + 12, GOLD, 5); hline(d, x0, x1, y1 - 12, GOLD, 5)
        for cx in spaced(x0, x1, 60, 130):
            d.polygon([(s(cx - 30), s(y0 + 25)), (s(cx + 30), s(y0 + 25)), (s(cx + 30), s(y1 - 35)),
                       (s(cx), s(y1 - 20)), (s(cx - 30), s(y1 - 35))], fill=(150, 20, 30), outline=t["dark"], width=LINE)
            hline(d, cx - 18, cx + 18, y0 + 60, GOLD, 4)
            for i, hh in ((-1, 12), (0, 20), (1, 12)):
                px = cx + i * 12
                d.polygon([(s(px), s(y0 + 60 - hh)), (s(px - 5), s(y0 + 60)), (s(px + 5), s(y0 + 60))], fill=GOLD)


def draw_band(d, room):
    if room.band == 0:
        return
    t = THEMES[room.theme]
    y0, y1 = room.y0, room.y0 + room.band
    # Openings: any rect whose bottom sits on this room's top edge is a doorway, so the band skips it.
    cuts = sorted((max(o.x0, room.x0), min(o.x1, room.x1)) for o in ROOMS
                  if o is not room and o.y1 == room.y0 and o.x1 > room.x0 and o.x0 < room.x1)
    x = room.x0
    segments = []
    for c0, c1 in cuts:
        if c0 > x: segments.append((x, c0))
        x = c1
    if x < room.x1: segments.append((x, room.x1))
    for sx0, sx1 in segments:
        rect(d, sx0, y0, sx1, y1, t["wall"])
        band_decor(d, room.theme, sx0, sx1, y0, y1, t)
        hline(d, sx0, sx1, y1, t["dark"], LINE)
        # Band ends beside an opening read as the door jambs.
        if sx0 != room.x0: vline(d, sx0, y0, y1, t["dark"], LINE)
        if sx1 != room.x1: vline(d, sx1, y0, y1, t["dark"], LINE)


# ---- Floors and furniture ----------------------------------------------------------------------------------------


def draw_floor(img, d, room):
    t = THEMES[room.theme]
    rect(d, room.x0, room.y0, room.x1, room.y1, t["floor"])
    step = TILE * 3 if room.theme == "foundry" else TILE  # foundry floor is big plates; everything else shows the tile grid
    grid(d, room.x0, room.y0, room.x1, room.y1, step, t["grid"], 3 if room.theme == "foundry" else 2)
    if room.theme == "observatory":  # diamond lattice instead of a square grid, drawn on its own layer so it clips to the room
        layer = Image.new("RGB", (s(room.w), s(room.h)), t["floor"])
        dl = ImageDraw.Draw(layer)
        for x in range(-room.h, room.w + room.h, TILE * 2):
            dl.line([(s(x), 0), (s(x + room.h), s(room.h))], fill=t["grid"], width=2)
            dl.line([(s(x), s(room.h)), (s(x + room.h), 0)], fill=t["grid"], width=2)
        img.paste(layer, (s(room.x0), s(room.y0)))
    if room.theme == "foundry":
        for x in range(int(room.x0), int(room.x1) + 1, TILE * 3):
            for y in range(int(room.y0), int(room.y1) + 1, TILE * 3):
                circle(d, x, y, 5, (180, 195, 210))


def draw_furniture(d, room):
    t = THEMES[room.theme]
    top = room.y0 + room.band  # first free row under the wall face
    if room.name == "throne":
        rect(d, room.cx - 120, top, room.cx + 120, room.y1, (150, 20, 30), GOLD, 4)           # carpet
        rect(d, room.cx - 200, top, room.cx + 200, top + 90, (90, 12, 20), t["dark"])          # dais
        rect(d, room.cx - 45, top + 10, room.cx + 45, top + 70, (60, 10, 14), GOLD, 4)          # throne
        for i, hh in ((-1, 16), (0, 26), (1, 16)):
            px = room.cx + i * 22
            d.polygon([(s(px), s(top + 10 - hh)), (s(px - 9), s(top + 10)), (s(px + 9), s(top + 10))], fill=GOLD)
        for x in (room.x0 + 220, room.x1 - 220):
            for y in range(int(top) + 200, int(room.y1) - 80, 180):
                pillar(d, x, y, 34, t)
        for x in (room.cx - 300, room.cx + 300):
            circle(d, x, top + 45, 22, (60, 10, 14), t["dark"]); circle(d, x, top + 40, 12, (255, 140, 40))
    elif room.name == "barracks":
        for i in range(4):
            x = room.x0 + 64 + i * 192
            rect(d, x, top + 40, x + 70, top + 170, (150, 160, 180), t["dark"])       # bed
            rect(d, x + 8, top + 48, x + 62, top + 80, (200, 205, 215), t["dark"], 2)  # pillow
            rect(d, x, top + 200, x + 70, top + 250, (80, 60, 40), t["dark"])          # chest
        rect(d, room.x0 + 120, room.y1 - 170, room.x1 - 120, room.y1 - 90, (80, 60, 40), t["dark"])  # long table
    elif room.name == "observatory":
        cx, cy = room.cx, (top + room.y1) / 2
        lens = (240, 230, 255)
        circle(d, cx, cy, 150, (100, 50, 170), t["dark"]); circle(d, cx, cy, 110, t["floor"], lens, 3)
        circle(d, cx, cy, 40, (100, 50, 170), lens, 3)
        hline(d, cx - 150, cx + 150, cy, lens, 3); vline(d, cx, cy - 150, cy + 150, lens, 3)
        for x in (room.x0 + 120, room.x1 - 120):
            rect(d, x - 30, top + 40, x + 30, top + 100, (60, 30, 100), t["dark"])          # lecterns
    elif room.name == "foundry":
        for i in range(3):
            x = room.x0 + 96 + i * 288
            rect(d, x, top + 40, x + 160, top + 130, (70, 85, 105), t["dark"])       # forge
            rect(d, x + 30, top + 60, x + 130, top + 110, (255, 120, 60), t["dark"])  # its fire
        for i in range(4):
            x = room.x0 + 96 + i * 224
            rect(d, x, room.y1 - 160, x + 90, room.y1 - 110, (60, 62, 80), t["dark"])  # anvils
    elif room.name == "crypt":
        for i in range(4):
            x = room.x0 + 96 + i * 192
            for y in (top + 60, room.y1 - 200):
                d.rounded_rectangle([s(x), s(y), s(x + 90), s(y + 150)], radius=s(14), fill=(40, 90, 120), outline=t["dark"], width=LINE)
                circle(d, x + 45, y + 75, 12, (200, 250, 255))
        for (x, y) in ((room.x0 + 60, room.y1 - 90), (room.x1 - 110, top + 30), (room.cx - 25, (top + room.y1) / 2)):
            rect(d, x, y, x + 50, y + 50, (48, 110, 130), (80, 220, 255), 3)
    elif room.name == "kitchen":
        rect(d, room.x0 + 150, top + 100, room.x1 - 150, top + 180, (80, 60, 40), t["dark"])     # table
        rect(d, room.x0 + 150, room.y1 - 160, room.x1 - 150, room.y1 - 80, (80, 60, 40), t["dark"])
        for i in range(5):
            circle(d, room.x0 + 200 + i * 130, top + 140, 18, (200, 120, 60), t["dark"], 2)      # pots
    elif room.name == "guard":
        for i in range(6):
            x = room.x0 + 60 + i * 140
            rect(d, x, top + 30, x + 90, top + 60, (60, 62, 80), t["dark"])                       # weapon racks
            for j in range(3):
                vline(d, x + 20 + j * 25, top + 60, top + 120, (180, 195, 210), 3)
        for i in range(3):
            rect(d, room.x1 - 150, room.y1 - 90 - i * 70, room.x1 - 80, room.y1 - 30 - i * 70, (80, 60, 40), t["dark"])  # crates
    elif room.name == "entrance":
        rect(d, room.cx - 100, top, room.cx + 100, room.y1, (150, 20, 30), GOLD, 4)   # carpet to the gate
        for x in (room.x0 + 100, room.x1 - 100):
            for y in (top + 80, room.y1 - 80):
                circle(d, x, y, 22, (60, 10, 14), t["dark"]); circle(d, x, y - 5, 12, (255, 140, 40))
    elif room.name == "spine":
        for x in range(int(room.x0) + 200, int(room.x1) - 100, 400):
            pillar(d, x, room.y1 - 60, 26, t)


# ---- Compose --------------------------------------------------------------------------------------------------------


def build():
    img = Image.new("RGB", (W, H), VOID)
    d = ImageDraw.Draw(img)

    # Walls: every rect grown by WALL. Masks give clean outlines around the merged shape without seams at the joins.
    interior = Image.new("L", (W, H), 0)
    walls = Image.new("L", (W, H), 0)
    di, dw = ImageDraw.Draw(interior), ImageDraw.Draw(walls)
    for r in ROOMS:
        dw.rectangle([s(r.x0 - WALL), s(r.y0 - WALL), s(r.x1 + WALL), s(r.y1 + WALL)], fill=255)
        di.rectangle([s(r.x0), s(r.y0), s(r.x1), s(r.y1)], fill=255)
    dw.rectangle([s(GATE[0] - WALL), s(GATE[1]), s(GATE[2] + WALL), s(GATE[3])], fill=255)

    stone = Image.new("RGB", (W, H), WALL_STONE)
    ds = ImageDraw.Draw(stone)
    for row, y in enumerate(range(0, WORLD_H, TILE)):   # brickwork, one course per tile
        hline(ds, 0, WORLD_W, y, WALL_MORTAR, 2)
        for x in range(TILE if row % 2 else 0, WORLD_W, TILE * 2):
            vline(ds, x, y, y + TILE, WALL_MORTAR, 2)
    img.paste(stone, (0, 0), walls)

    for r in ROOMS: draw_floor(img, d, r)
    for r in ROOMS: draw_band(d, r)
    for r in ROOMS: draw_furniture(d, r)

    # Outline: the interior mask minus its erosion is a ring just inside the floor edge.
    edge = Image.eval(interior.filter(ImageFilter.MinFilter(2 * LINE + 1)), lambda v: 255 - v)
    edge = Image.composite(interior, Image.new("L", (W, H), 0), edge)
    img.paste(Image.new("RGB", (W, H), DARK), (0, 0), edge)

    # Main gate: an opening in the south wall with a portcullis.
    gx0, gy0, gx1, gy1 = GATE
    rect(d, gx0, gy0, gx1, gy1, (30, 20, 20), DARK)
    for x in range(int(gx0) + 25, int(gx1), 25):
        vline(d, x, gy0, gy1, (120, 125, 140), 4)
    for y in range(int(gy0) + 25, int(gy1), 25):
        hline(d, gx0, gx1, y, (120, 125, 140), 4)

    # Shaped like a Stage so save_stage can write it; the spawn is the gate, matching castle_floor.tmx.
    return SimpleNamespace(name="castle_floor", tw=TW, th=TH, W=W, H=H, img=img, spawn=(62.5, 70))


WRITES_TMX = False  # castle_floor.tmx carries hand-authored collision
