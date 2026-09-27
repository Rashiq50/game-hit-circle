"""Shared helpers for the stage background generators.

Everything is laid out in world units at S pixels per unit. TILE (32) is the canonical tile size: every wall, obstacle and
doorway edge sits on that grid so the images line up with a Tiled map and with future tile art.

Themed stages are authored on a *tile-resolution terrain image*: one pixel per tile, whose value is a ground code
(boundary, floor, water, ...). Shapes are drawn onto it with the tile-space helpers on Stage, then each code is upscaled
with NEAREST into a full-size mask, so every ground edge lands on the grid by construction. Obstacles (trees, rocks, walls)
are separate objects with a tile footprint, drawn over the ground afterwards.
"""
import os
import random
from collections import deque
from PIL import Image, ImageChops, ImageDraw, ImageFilter

S = 1                      # pixels per world unit
TILE = 32                  # canonical tile size; every layout edge is a multiple of it
LINE = 4                   # outline weight

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
STAGES_DIR = os.path.join(REPO, "textures", "stages")
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")

VOID = (16, 17, 26)
DARK = (12, 12, 20)
GOLD = (255, 200, 60)


# ---- Drawing helpers (world units) ---------------------------------------------------------------------------------


def s(v): return int(round(v * S))


def rect(d, x0, y0, x1, y1, fill, outline=None, width=LINE):
    d.rectangle([s(x0), s(y0), s(x1), s(y1)], fill=fill, outline=outline, width=width if outline else 0)


def hline(d, x0, x1, y, col, width=LINE): d.line([(s(x0), s(y)), (s(x1), s(y))], fill=col, width=width)
def vline(d, x, y0, y1, col, width=LINE): d.line([(s(x), s(y0)), (s(x), s(y1))], fill=col, width=width)


def circle(d, cx, cy, r, fill, outline=None, width=LINE):
    d.ellipse([s(cx - r), s(cy - r), s(cx + r), s(cy + r)], fill=fill, outline=outline, width=width if outline else 0)


def poly(d, pts, fill, outline=None, width=LINE):
    d.polygon([(s(x), s(y)) for x, y in pts], fill=fill, outline=outline, width=width if outline else 0)


def arch(d, x, y, w, h, fill, outline):
    r = w / 2
    d.pieslice([s(x), s(y), s(x + w), s(y + 2 * r)], 180, 360, fill=fill, outline=outline, width=LINE)
    d.rectangle([s(x), s(y + r), s(x + w), s(y + h)], fill=fill)
    vline(d, x, y + r, y + h, outline); vline(d, x + w, y + r, y + h, outline); hline(d, x, x + w, y + h, outline)


def grid(d, x0, y0, x1, y1, step, col, width=2):
    y = y0
    while y <= y1:
        hline(d, x0, x1, y, col, width); y += step
    x = x0
    while x <= x1:
        vline(d, x, y0, y1, col, width); x += step


def pillar(d, cx, cy, r, t):
    circle(d, cx, cy, r, (60, 62, 80), t["dark"])
    circle(d, cx, cy, r * 0.55, (96, 100, 122))


def spaced(x0, x1, item_w, pitch):
    """Centres of evenly spaced items across [x0, x1]; returns [] when even one doesn't fit."""
    n = int((x1 - x0 - 40) // pitch)
    if n <= 0:
        return [] if x1 - x0 < item_w + 40 else [(x0 + x1) / 2]
    span = (n - 1) * pitch
    start = (x0 + x1) / 2 - span / 2
    return [start + i * pitch for i in range(n)]


def outline_ring(mask, width=LINE):
    """The part of `mask` within `width` px of its edge: the mask minus its erosion."""
    w, h = mask.size
    edge = Image.eval(mask.filter(ImageFilter.MinFilter(2 * width + 1)), lambda v: 255 - v)
    return Image.composite(mask, Image.new("L", (w, h), 0), edge)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


# ---- Tile-space stage authoring --------------------------------------------------------------------------------------

BOUND = 0  # every stage uses code 0 for the impassable surround


class Stage:
    """A map `tw` x `th` tiles. `ground` holds one ground code per tile; `objects` are solid props with tile footprints."""

    def __init__(self, name, tw, th, seed, open_codes):
        self.name, self.tw, self.th = name, tw, th
        self.W, self.H = tw * TILE * S, th * TILE * S
        self.rng = random.Random(seed)
        self.open_codes = set(open_codes)        # ground the player can walk on (before objects)
        self.ground = Image.new("L", (tw, th), BOUND)
        self.gd = ImageDraw.Draw(self.ground)
        self.objects = []                         # (kind, tx, ty, w, h, kw)
        self.blocked = set()                      # tiles covered by an object
        self.spawn = None                         # player spawn, tile coords (may be fractional)
        self.img = Image.new("RGB", (self.W, self.H), VOID)
        self.d = ImageDraw.Draw(self.img)

    # -- ground shapes. `only` limits which existing codes a shape may overwrite. --

    def _apply(self, code, draw_fn, only):
        if only is None:
            draw_fn(self.gd, code)
            return
        m = Image.new("L", self.ground.size, 0)
        draw_fn(ImageDraw.Draw(m), 255)
        allowed = self.ground.point(lambda v: 255 if v in only else 0)
        m = Image.composite(m, Image.new("L", m.size, 0), allowed)
        self.ground.paste(code, (0, 0), m)

    def area(self, code, x0, y0, x1, y1, only=None):
        """Half-open tile rect [x0, x1) x [y0, y1)."""
        self._apply(code, lambda g, c: g.rectangle([x0, y0, x1 - 1, y1 - 1], fill=c), only)

    def blob(self, code, cx, cy, rx, ry, only=None):
        """Tile-stepped ellipse centred on (cx, cy) with radii in tiles."""
        self._apply(code, lambda g, c: g.ellipse([cx - rx, cy - ry, cx + rx - 1, cy + ry - 1], fill=c), only)

    def path(self, code, pts, width, only=None):
        """Thick polyline through tile points, `width` tiles wide, with round joints."""
        def draw(g, c):
            g.line(pts, fill=c, width=width)
            for x, y in pts:
                g.ellipse([x - width / 2, y - width / 2, x + width / 2 - 1, y + width / 2 - 1], fill=c)
        self._apply(code, draw, only)

    def polygon(self, code, pts, only=None):
        self._apply(code, lambda g, c: g.polygon(pts, fill=c), only)

    def code(self, x, y):
        if 0 <= x < self.tw and 0 <= y < self.th:
            return self.ground.getpixel((x, y))
        return BOUND

    # -- objects --

    def put(self, kind, tx, ty, w=1, h=1, **kw):
        """Places a solid prop on open, unblocked ground; its footprint becomes blocked."""
        cells = [(x, y) for x in range(tx, tx + w) for y in range(ty, ty + h)]
        for x, y in cells:
            assert self.code(x, y) in self.open_codes, f"{self.name}: {kind} at {tx},{ty} lands on closed ground ({x},{y})"
            assert (x, y) not in self.blocked, f"{self.name}: {kind} at {tx},{ty} overlaps another object at ({x},{y})"
        self.blocked.update(cells)
        self.objects.append((kind, tx, ty, w, h, kw))

    def try_put(self, kind, tx, ty, w=1, h=1, margin=0, **kw):
        """Like put, but returns False instead of failing, and keeps `margin` free tiles around it."""
        for x in range(tx - margin, tx + w + margin):
            for y in range(ty - margin, ty + h + margin):
                if self.code(x, y) not in self.open_codes or (x, y) in self.blocked:
                    return False
        self.put(kind, tx, ty, w, h, **kw)
        return True

    def scatter(self, kind, count, codes, sizes=((1, 1),), margin=1, keep_clear=(), tries=4000, **kw):
        """Randomly places `count` props on the given ground codes, keeping `margin` open tiles around each. A prop
        that would cut any ground off from the spawn is taken back out, so scatter never creates pockets."""
        placed = 0
        stuck = len(self.check_reachable())
        for _ in range(tries):
            if placed >= count:
                break
            w, h = self.rng.choice(sizes)
            tx, ty = self.rng.randrange(0, self.tw - w + 1), self.rng.randrange(0, self.th - h + 1)
            if any(x0 <= tx + w and tx < x1 and y0 <= ty + h and ty < y1 for x0, y0, x1, y1 in keep_clear):
                continue
            if all(self.code(x, y) in codes for x in range(tx, tx + w) for y in range(ty, ty + h)) \
                    and self.try_put(kind, tx, ty, w, h, margin=margin, **kw):
                if len(self.check_reachable()) > stuck:
                    self.objects.pop()
                    self.blocked.difference_update((x, y) for x in range(tx, tx + w) for y in range(ty, ty + h))
                    continue
                placed += 1
        return placed

    # -- masks --

    def tile_mask(self, *codes):
        cs = set(codes)
        return self.ground.point(lambda v: 255 if v in cs else 0)

    def mask(self, *codes):
        return self.tile_mask(*codes).resize((self.W, self.H), Image.NEAREST)

    def open_tiles(self):
        return [(x, y) for y in range(self.th) for x in range(self.tw)
                if self.code(x, y) in self.open_codes and (x, y) not in self.blocked]

    # -- rendering building blocks --

    def layer(self, color):
        lay = Image.new("RGB", (self.W, self.H), color)
        return lay, ImageDraw.Draw(lay)

    def paint(self, lay, mask):
        self.img.paste(lay, (0, 0), mask)

    def outline(self, mask, color=DARK, width=LINE):
        self.img.paste(Image.new("RGB", (self.W, self.H), color), (0, 0), outline_ring(mask, width))

    def glow(self, centres, radius, color, strength=0.4, mask=None):
        """Soft light pooled around world-space points, optionally clipped to `mask`."""
        g = Image.new("L", (self.W, self.H), 0)
        gd = ImageDraw.Draw(g)
        for x, y in centres:
            gd.ellipse([s(x - radius), s(y - radius), s(x + radius), s(y + radius)], fill=255)
        g = g.filter(ImageFilter.GaussianBlur(s(radius) * 0.6)).point(lambda v: int(v * strength))
        if mask is not None:
            g = ImageChops.multiply(g, mask)
        self.img.paste(Image.new("RGB", (self.W, self.H), color), (0, 0), g)

    def glow_from(self, source, radius, color, strength=0.5, mask=None):
        """Light spilling out of a whole region (a full-size mask, e.g. lava) onto what's around it."""
        g = source.filter(ImageFilter.GaussianBlur(s(radius))).point(lambda v: int(min(255, v * 2) * strength))
        if mask is not None:
            g = ImageChops.multiply(g, mask)
        self.img.paste(Image.new("RGB", (self.W, self.H), color), (0, 0), g)

    def split_by_run(self, code):
        """Tiles of `code`, split into those in east-west runs and those in north-south runs (longer run wins), so
        planks and slabs can be laid across each stretch's walking direction."""
        def run(x, y, dx, dy):
            n = 0
            while self.code(x + dx * (n + 1), y + dy * (n + 1)) == code:
                n += 1
            return n
        tiles = [(x, y) for y in range(self.th) for x in range(self.tw) if self.code(x, y) == code]
        horiz = [(x, y) for x, y in tiles if run(x, y, 1, 0) + run(x, y, -1, 0) > run(x, y, 0, 1) + run(x, y, 0, -1)]
        hs = set(horiz)
        return horiz, [t for t in tiles if t not in hs]

    def faces(self, solid_codes, open_codes, depth, draw_fn):
        """Wall faces: for each run of solid tiles directly above open ground, calls draw_fn(d, x0, x1, y0, y1)
        in world units, with the face covering up to `depth` solid tiles above the open edge. Adjacent tiles are merged
        into horizontal runs so the decoration can be laid out along them."""
        solid, opn = set(solid_codes), set(open_codes)
        for y in range(1, self.th):
            x = 0
            while x < self.tw:
                if self.code(x, y - 1) in solid and self.code(x, y) in opn:
                    x0 = x
                    while x < self.tw and self.code(x, y - 1) in solid and self.code(x, y) in opn:
                        x += 1
                    # how many solid tiles stack above every tile of this run (at least 1)
                    k = 1
                    while k < depth and all(self.code(i, y - 1 - k) in solid for i in range(x0, x)):
                        k += 1
                    draw_fn(self.d, x0 * TILE, x * TILE, (y - k) * TILE, y * TILE)
                else:
                    x += 1

    def draw_objects(self, drawers):
        # Top to bottom so lower props overlap the ones behind them.
        for kind, tx, ty, w, h, kw in sorted(self.objects, key=lambda o: (o[2] + o[4], o[1])):
            drawers[kind](self.d, tx * TILE, ty * TILE, (tx + w) * TILE, (ty + h) * TILE, self.rng, **kw)

    # -- validation --

    def check_reachable(self, footprint=2):
        """Flood fills from the spawn with a `footprint` x `footprint` tile body (a 50px demon needs 2x2) and returns
        the open tiles no such body can reach, so chokepoints narrower than the footprint show up as unreachable."""
        fits = lambda x, y: all(self.code(x + i, y + j) in self.open_codes and (x + i, y + j) not in self.blocked
                                for i in range(footprint) for j in range(footprint))
        sx, sy = int(self.spawn[0]), int(self.spawn[1])
        start = next(((x, y) for x in range(sx - 2, sx + 1) for y in range(sy - 2, sy + 1) if fits(x, y)), None)
        assert start, f"{self.name}: spawn {self.spawn} has no room for a {footprint}x{footprint} body"
        seen, q = {start}, deque([start])
        while q:
            x, y = q.popleft()
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if (nx, ny) not in seen and fits(nx, ny):
                    seen.add((nx, ny)); q.append((nx, ny))
        covered = {(x + i, y + j) for x, y in seen for i in range(footprint) for j in range(footprint)}
        return [t for t in self.open_tiles() if t not in covered]

    def seal_pockets(self, code=BOUND, max_tiles=6):
        """Turns tiny unreachable slivers (left by stepped curves) into `code` so no walkable-looking ground is cut off.
        Bigger pockets are left alone so check_reachable still reports them as layout mistakes."""
        stuck = self.check_reachable()
        if 0 < len(stuck) <= max_tiles:
            for x, y in stuck:
                self.ground.putpixel((x, y), code)


# ---- Output ----------------------------------------------------------------------------------------------------------


def write_tmx(path, tw, th, png_name, spawn_px):
    """A starter Tiled map mirroring castle_floor.tmx: the background as an image layer, an empty Walls layer and a
    SpawnLayers group holding just the player spawn. CollisionMap.cs reads objects from every object group."""
    sx, sy = spawn_px
    xml = f"""<?xml version="1.0" encoding="UTF-8"?>
<map version="1.10" tiledversion="1.12.2" orientation="orthogonal" renderorder="right-down" width="{tw}" height="{th}" tilewidth="{TILE}" tileheight="{TILE}" infinite="0" nextlayerid="4" nextobjectid="2">
 <imagelayer id="1" name="bg">
  <image source="{png_name}" width="{tw * TILE * S}" height="{th * TILE * S}"/>
 </imagelayer>
 <objectgroup id="2" name="Walls"/>
 <objectgroup id="3" name="SpawnLayers">
  <object id="1" name="Player Spawn" x="{sx:g}" y="{sy:g}">
   <point/>
  </object>
 </objectgroup>
</map>
"""
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(xml)


def save_stage(stage, write_tmx_file=True):
    """Writes textures/stages/<name>.png, a starter .tmx if none exists yet, and a half-size preview."""
    assert stage.img.size == (stage.tw * TILE * S, stage.th * TILE * S)
    png = os.path.join(STAGES_DIR, stage.name + ".png")
    # Leave an identical PNG alone so rebuilding doesn't churn the committed binary.
    if os.path.exists(png) and ImageChops.difference(Image.open(png).convert("RGB"), stage.img).getbbox() is None:
        msg = f"unchanged {png} {stage.img.size}"
    else:
        stage.img.save(png, optimize=True)
        msg = f"wrote {png} {stage.img.size}"
    if write_tmx_file:
        tmx = os.path.join(STAGES_DIR, stage.name + ".tmx")
        if os.path.exists(tmx):
            msg += " (kept existing .tmx)"
        else:
            write_tmx(tmx, stage.tw, stage.th, stage.name + ".png",
                      (stage.spawn[0] * TILE * S, stage.spawn[1] * TILE * S))
            msg += " + starter .tmx"
    os.makedirs(OUT_DIR, exist_ok=True)
    stage.img.resize((stage.W // 2, stage.H // 2), Image.LANCZOS).save(os.path.join(OUT_DIR, stage.name + "_preview.png"))
    print(msg)
