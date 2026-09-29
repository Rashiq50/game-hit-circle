"""Builds the enemy sprite strips into textures/enemies/, in the same flat, dark-outlined style as the stage maps.

    python tools/enemies/make_enemies.py              # every look
    python tools/enemies/make_enemies.py imp wisp     # just the named ones

Every frame is a square CANVAS world units across with the body centre in the middle, drawn at SS px per unit and
downscaled to OUT px per unit (2x the game's world scale, so zoom and the ultimate's hover stay crisp). Each look gets
{look}_idle.png (IDLE_FRAMES looping frames) and {look}_death.png (DEATH_FRAMES, played once). All art faces right; the
game mirrors it to face the player.

Each look also gets an attack sheet per kind of attack it has, {look}_melee.png and/or {look}_ranged.png: one row per
direction in ATTACK_ROWS, each ATTACK_FRAMES long (the windup, then the frame that lands the blow or looses the shot at
ATTACK_HIT, then the follow-through). Attacks reach further than the idle pose, so each sheet's frames are cropped to fit
its own art, still square and centred on the body; the game sizes them from their pixel size.

Previews go to tools/enemies/out/: a contact sheet with the hit boxes drawn on, every look pasted at game scale onto each
stage, and attacks.png with the swing reach and the muzzles drawn on.

LOOKS mirrors EnemyIcons.BodyScale / HitExtent / TopExtent / Muzzle in EnemyIcons.cs; change both together.
"""
import math
import os
import random
import sys
from concurrent.futures import ProcessPoolExecutor

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "stages"))
from PIL import Image, ImageChops, ImageDraw, ImageFilter  # noqa: E402
from common import REPO, STAGES_DIR, mix  # noqa: E402

CANVAS = 128               # frame size in world units for the idle and death strips
BASE_RADIUS = 30           # Enemy.IconRadius
SS = 8                     # supersampled px per world unit while drawing
OUT = 2                    # px per world unit in the shipped strips; EnemyIcons.PxPerUnit
IDLE_FRAMES = 8
DEATH_FRAMES = 6
LINE = 2.75                # outline weight in world units, close to the props' 3 px

ATTACK_ROWS = ("side", "down", "up")     # row order in the attack sheets; EnemyIcons.AttackRow
ATTACK_FRAMES = 8
ATTACK_HIT = 5             # frames before this one wind up; this one lands the blow; EnemyIcons.AttackHitFrame
WIND = (0.3, 0.6, 0.85, 0.97, 1.0)       # windup on each windup frame; the last holds, with a glint, just before the blow
FOLLOW = (1.0, 0.6, 0.25)  # how far from rest each later frame still is, from the blow itself back toward the idle pose
ATTACK_SCRATCH = 240       # canvas the attack frames are drawn on before each sheet is cropped to fit
MELEE_REACH, MELEE_ARC = 20, 120         # Enemy.MeleeReach / MeleeArc; only for drawing the reach on the preview

ENEMIES_DIR = os.path.join(REPO, "textures", "enemies")
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
TAU = math.tau
WHITE = (255, 255, 255)
DUST, DUST_EDGE = (206, 190, 160), (84, 66, 48)

# scale: body radius = BASE_RADIUS * scale. hit: solid half-size / radius. top: drawing's reach above the centre / radius.
# attacks: which attack sheets the look gets. muzzle: where a shot leaves on the release frame, per direction, in body
# radii from the centre with the art facing right.
LOOKS = {
    "imp": dict(scale=0.75, hit=(0.7, 0.95), top=1.4, attacks=("ranged",),
                muzzle=dict(side=(1.79, -0.25), down=(1.06, 0.71), up=(0.98, -1.34))),
    "brute": dict(scale=1.25, hit=(0.95, 0.9), top=1.2, attacks=("melee",)),
    "tank": dict(scale=1.3, hit=(0.98, 0.93), top=0.95, attacks=("melee",)),
    "sniper": dict(scale=1.0, hit=(0.55, 1.2), top=1.65, attacks=("ranged",),
                   muzzle=dict(side=(2.15, -0.84), down=(1.63, 1.45), up=(0.59, -1.9))),
    "warlord": dict(scale=1.5, hit=(0.92, 0.92), top=1.2, attacks=("melee", "ranged"),
                    muzzle=dict(side=(1.77, -0.32), down=(1.05, 1.03), up=(0.83, -1.73))),
    "wisp": dict(scale=0.8, hit=(0.6, 0.65), top=1.35, attacks=("ranged",),
                 muzzle=dict(side=(1.04, 0.06), down=(0.08, 0.89), up=(-0.06, -1.49))),
}


def u(v): return int(round(v * SS))


def wave(ph, k=1, shift=0.0): return math.sin(TAU * (ph * k + shift))


def lerp(a, b, t):
    if isinstance(a, tuple):
        return tuple(lerp(x, y, t) for x, y in zip(a, b))
    return a + (b - a) * t


def frange(t0, t1, n): return [t0 + (t1 - t0) * i / (n - 1) for i in range(n)]


# ---- Shapes (world units); grow > 0 draws the shape fattened by that much, which is how outlines are made ------------


class E:
    """Ellipse."""
    def __init__(self, cx, cy, rx, ry=None):
        self.cx, self.cy, self.rx, self.ry = cx, cy, rx, rx if ry is None else ry

    def draw(self, d, fill, grow=0):
        d.ellipse([u(self.cx - self.rx - grow), u(self.cy - self.ry - grow),
                   u(self.cx + self.rx + grow), u(self.cy + self.ry + grow)], fill=fill)

    def moved(self, xf):
        return E(*xf.pt(self.cx, self.cy), self.rx, self.ry)


class P:
    """Polygon; grown by stroking its edges with round joints."""
    def __init__(self, pts):
        self.pts = pts

    def draw(self, d, fill, grow=0):
        pts = [(u(x), u(y)) for x, y in self.pts]
        d.polygon(pts, fill=fill)
        if grow:
            d.line(pts + [pts[0]], fill=fill, width=u(2 * grow), joint="curve")
            for x, y in pts:
                d.ellipse([x - u(grow), y - u(grow), x + u(grow), y + u(grow)], fill=fill)

    def moved(self, xf):
        return P([xf.pt(x, y) for x, y in self.pts])


class L:
    """Thick polyline with round caps."""
    def __init__(self, pts, width):
        self.pts, self.w = pts, width

    def draw(self, d, fill, grow=0):
        w = self.w + 2 * grow
        pts = [(u(x), u(y)) for x, y in self.pts]
        d.line(pts, fill=fill, width=u(w), joint="curve")
        for x, y in pts:
            d.ellipse([x - u(w / 2), y - u(w / 2), x + u(w / 2), y + u(w / 2)], fill=fill)

    def moved(self, xf):
        return L([xf.pt(x, y) for x, y in self.pts], self.w)


class R:
    """Rounded rectangle."""
    def __init__(self, x0, y0, x1, y1, radius=0):
        self.box, self.rad = (x0, y0, x1, y1), radius

    def draw(self, d, fill, grow=0):
        x0, y0, x1, y1 = self.box
        d.rounded_rectangle([u(x0 - grow), u(y0 - grow), u(x1 + grow), u(y1 + grow)], radius=u(self.rad + grow), fill=fill)

    def moved(self, xf):
        """Boxes slide with the move but don't turn."""
        x0, y0, x1, y1 = self.box
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        nx, ny = xf.pt(mx, my)
        return R(x0 + nx - mx, y0 + ny - my, x1 + nx - mx, y1 + ny - my, self.rad)


class Xf:
    """Move for a weapon or limb: turn `deg` degrees clockwise (on screen) about `pivot`, then carry the pivot to `to`.
    `scale` shortens it, for a long blade foreshortened as it comes down toward the ground. Called on shapes it returns
    the moved copies; the identity move hands them back untouched, so rest poses stay exactly as they were drawn."""

    def __init__(self, pivot, deg=0.0, to=None, scale=1.0):
        self.px, self.py = pivot
        self.tx, self.ty = pivot if to is None else to
        self.c, self.s = math.cos(math.radians(deg)) * scale, math.sin(math.radians(deg)) * scale
        self.still = deg == 0 and scale == 1 and (self.tx, self.ty) == (self.px, self.py)

    def pt(self, x, y):
        if self.still:
            return x, y
        dx, dy = x - self.px, y - self.py
        return self.tx + dx * self.c - dy * self.s, self.ty + dx * self.s + dy * self.c

    def __call__(self, *shapes):
        return [sh if self.still else sh.moved(self) for sh in shapes]


class Pose:
    """Where a frame sits in an attack. `wind` climbs 0..1 through the windup, then `hit` falls 1..0 from the blow back to
    rest, and `dir` ('side', 'down' or 'up') is where the blow goes. The idle loop draws REST, with both at zero."""

    def __init__(self, wind=0.0, hit=0.0, dir="side"):
        self.wind, self.hit, self.dir = wind, hit, dir

    @property
    def resting(self): return self.wind == 0 and self.hit == 0

    def key(self, k):
        return k[self.dir] if isinstance(k, dict) else k

    def at(self, rest, wound, struck):
        """This frame's value from three keys: at rest, fully wound up, and as the blow lands. `wound` and `struck` may be
        dicts by direction. A ranged attack keeps `wound` the same for every direction, so the game can settle which row
        to draw right up to the release. Numbers and tuples both blend."""
        if self.hit > 0:
            return lerp(rest, self.key(struck), self.hit)
        if self.wind > 0:
            return lerp(rest, self.key(wound), self.wind)
        return rest

    def swing(self, wound, struck, t):
        """The value `t` of the way through the blow itself, from the wound key to the struck one; for smears."""
        return lerp(self.key(wound), self.key(struck), t)


REST = Pose()


class Art:
    """One supersampled frame. Parts are painted back to front, each with its own outline, like the map props."""

    def __init__(self, canvas=CANVAS, img=None):
        self.size = canvas * SS
        self.img = img if img is not None else Image.new("RGBA", (self.size, self.size), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    def mask(self, shapes, grow=0):
        m = Image.new("L", (self.size, self.size), 0)
        md = ImageDraw.Draw(m)
        for sh in shapes:
            sh.draw(md, 255, grow)
        return m

    def part(self, shapes, fill, edge, w=LINE, shade=None, off=None, light=None):
        """Outlined solid: every shape grown by `w` in `edge`, then every shape in `fill`, so the union gets one outline.
        `shade` paints the rim facing away from the top-left light, `off` world units thick; `light` the rim facing it.
        Returns the fill mask for clipping details to the part."""
        for sh in shapes:
            sh.draw(self.d, edge, w)
        for sh in shapes:
            sh.draw(self.d, fill)
        m = self.mask(shapes)
        if shade or light:
            o = u(off if off is not None else 2.5)
            if shade:
                self.img.paste(shade, mask=ImageChops.subtract(m, self._shift(m, -o, -o)))
            if light:
                self.img.paste(light, mask=ImageChops.subtract(m, self._shift(m, o // 2, o // 2)))
        return m

    def clip(self, m, shapes, fill, edge=None, w=1.4):
        """Paint `shapes` only where mask `m` is set; optional outline around them (also clipped)."""
        if edge:
            self.img.paste(edge, mask=ImageChops.multiply(m, self.mask(shapes, w)))
        self.img.paste(fill, mask=ImageChops.multiply(m, self.mask(shapes)))

    def fill(self, shapes, fill):
        for sh in shapes:
            sh.draw(self.d, fill)

    def line(self, pts, width, col):
        L(pts, width).draw(self.d, col)

    def glow(self, cx, cy, radius, color, alpha):
        layer = Image.new("RGBA", (self.size, self.size), color + (0,))
        ImageDraw.Draw(layer).ellipse([u(cx - radius * 0.6), u(cy - radius * 0.6), u(cx + radius * 0.6), u(cy + radius * 0.6)],
                                      fill=color + (int(255 * max(0.0, min(1.0, alpha))),))
        layer = layer.filter(ImageFilter.GaussianBlur(u(radius * 0.35)))
        self.img.alpha_composite(layer)
        self.d = ImageDraw.Draw(self.img)

    def _shift(self, m, dx, dy):
        out = Image.new("L", m.size, 0)
        out.paste(m, (dx, dy))
        return out

    # -- attack effects: translucent paint goes on a layer of its own and is composited on --

    def layer(self):
        return Image.new("RGBA", (self.size, self.size), (0, 0, 0, 0))

    def blend(self, layer, alpha=1.0):
        if alpha < 1:
            layer.putalpha(layer.getchannel("A").point(lambda v: int(v * alpha)))
        self.img.alpha_composite(layer)
        self.d = ImageDraw.Draw(self.img)

    def smear(self, path, width, color, alpha):
        """Motion smear trailing a swung weapon along `path` (world units), widening from nothing to `width` at its head,
        with a pale core."""
        if alpha <= 0 or len(path) < 2:
            return
        lay = self.layer()
        d = ImageDraw.Draw(lay)
        n = len(path)
        for frac, col, a in ((1.0, color, 0.38), (0.35, WHITE, 0.62)):
            left, right = [], []
            for i, (x, y) in enumerate(path):
                (x0, y0), (x1, y1) = path[max(0, i - 1)], path[min(n - 1, i + 1)]
                ln = math.hypot(x1 - x0, y1 - y0) or 1
                nx, ny = (y0 - y1) / ln, (x1 - x0) / ln
                w = width * frac * 0.5 * (i / (n - 1)) ** 0.7
                left.append((u(x + nx * w), u(y + ny * w)))
                right.append((u(x - nx * w), u(y - ny * w)))
            d.polygon(left + right[::-1], fill=col + (int(255 * a),))
            E(*path[-1], width * frac * 0.5).draw(d, col + (int(255 * a),))
        self.blend(lay, alpha)

    def burst(self, x, y, size, t, edge, ground=True):
        """Where a blow lands. The blow itself (t = 0) flashes a spiky star; a blow on the ground also kicks up a ring of
        outlined dust puffs that spread and fade as `t` climbs to 1."""
        flat = 0.55 if ground else 1.0
        if ground:
            lay = self.layer()
            d = ImageDraw.Draw(lay)
            rng = random.Random(3)
            puffs = []
            for i in range(7):
                ang = i / 7 * TAU + rng.uniform(-0.3, 0.3)
                reach = size * (0.5 + 0.6 * t) * rng.uniform(0.85, 1.1)
                pr = size * rng.uniform(0.22, 0.32) * (0.8 + 0.4 * t)
                puffs.append((x + math.cos(ang) * reach, y + math.sin(ang) * reach * flat, pr, mix(DUST, WHITE, rng.uniform(0, 0.35))))
            for px, py, pr, col in sorted(puffs, key=lambda p: p[1]):  # far puffs first
                E(px, py, pr).draw(d, DUST_EDGE + (255,), 1.4)
                E(px, py, pr).draw(d, col + (255,))
            self.blend(lay, (1 - t) ** 0.9)
        if t < 0.05:
            pts = [(x + math.cos(i / 16 * TAU) * size * (1 if i % 2 == 0 else 0.42),
                    y + math.sin(i / 16 * TAU) * size * (1 if i % 2 == 0 else 0.42) * flat) for i in range(16)]
            self.glow(x, y, size * 1.2, (255, 236, 170), 0.55)
            self.part([P(pts)], (255, 244, 190), edge, w=1.4)
            self.fill([E(x, y, size * 0.28, size * 0.28 * flat)], WHITE)

    def glint(self, x, y, size):
        """Four-point sparkle on a weapon: the held frame just before the blow."""
        self.glow(x, y, size * 1.6, (255, 250, 220), 0.55)
        k = 0.2
        self.fill([P([(x, y - size), (x + k * size, y - k * size), (x + size, y), (x + k * size, y + k * size),
                      (x, y + size), (x - k * size, y + k * size), (x - size, y), (x - k * size, y - k * size)])], WHITE)

    def flash(self, x, y, size, t, color):
        """Muzzle flash where a shot leaves: a star of the shot's colour on the release frame (t = 0), then an afterglow
        with a sparkle that shrinks and fades as `t` climbs to 1."""
        if t < 0.05:
            self.glow(x, y, size * 2.2, color, 0.85)
            pts = [(x + math.cos(i / 12 * TAU) * size * (1 if i % 2 == 0 else 0.4),
                    y + math.sin(i / 12 * TAU) * size * (1 if i % 2 == 0 else 0.4)) for i in range(12)]
            self.part([P(pts)], mix(color, WHITE, 0.45), mix(color, (0, 0, 0), 0.6), w=1.2)
            self.fill([E(x, y, size * 0.35)], WHITE)
            return
        fade = 1 - t
        self.glow(x, y, size * 1.8, color, 0.6 * fade)
        if fade > 0.3:
            self.glint(x, y, size * 0.8 * fade)


def bowed(p0, p1, bulge, away, n=10):
    """Points from `p0` to `p1` bowed out by `bulge` at the middle, on the side facing away from point `away`: the
    curved path a thrown fist takes, for its smear."""
    (x0, y0), (x1, y1) = p0, p1
    ln = math.hypot(x1 - x0, y1 - y0) or 1
    nx, ny = (y0 - y1) / ln, (x1 - x0) / ln
    mx, my = (x0 + x1) / 2, (y0 + y1) / 2
    if (mx - away[0]) * nx + (my - away[1]) * ny < 0:
        nx, ny = -nx, -ny
    return [(x0 + (x1 - x0) * t + nx * bulge * math.sin(math.pi * t), y0 + (y1 - y0) * t + ny * bulge * math.sin(math.pi * t))
            for t in frange(0, 1, n)]


# ---- The creatures (cx, cy = body centre, r = body radius, ph = loop phase 0..1, pose = the attack frame) -------------
# Attack keys are in body radii from the centre, with turns in degrees clockwise from the resting pose.


def draw_imp(a, cx, cy, r, ph, pose=REST, kind=None):
    body, shade, light, edge = (232, 104, 44), (182, 62, 30), (255, 170, 98), (60, 20, 10)
    wing, wing_s = (168, 46, 44), (122, 30, 36)
    horn, horn_s = (246, 226, 184), (196, 170, 128)
    cy += wave(ph, 2) * 1.2           # twitchy hop
    flap = pose.at(wave(ph), 1.0, -0.8)  # the wings rise through a throw's windup and beat down as it lets go
    # tail with a spade tip, curling behind
    tail = [(cx - 0.3 * r, cy + 0.55 * r), (cx - 0.8 * r, cy + 0.7 * r), (cx - 1.1 * r, cy + 0.4 * r),
            (cx - 1.05 * r, cy + 0.05 * r + flap * 0.06 * r)]
    tx, ty = tail[-1]
    spade = P([(tx - 0.2 * r, ty + 0.05 * r), (tx, ty - 0.32 * r), (tx + 0.2 * r, ty + 0.05 * r), (tx, ty + 0.12 * r)])
    a.part([L(tail, 0.16 * r), spade], shade, edge)
    # bat wings
    for side in (-1, 1):
        lift = flap * 0.28 * r
        sx, sy = cx + side * 0.3 * r, cy - 0.2 * r
        tip = (cx + side * 1.32 * r, cy - 0.95 * r - lift)
        scallops = [(cx + side * 1.22 * r, cy - 0.3 * r - lift * 0.6), (cx + side * 0.98 * r, cy - 0.02 * r - lift * 0.35),
                    (cx + side * 0.7 * r, cy + 0.12 * r - lift * 0.1)]
        m = a.part([P([(sx, sy), tip] + scallops + [(sx, sy + 0.3 * r)])], wing, edge, shade=wing_s, off=0.12 * r)
        joint = ((sx + tip[0]) / 2, (sy + tip[1]) / 2)
        for fx, fy in scallops:
            a.clip(m, [L([joint, (fx, fy)], 1.3)], edge)
        a.line([(sx, sy), joint, tip], 1.8, edge)
    # feet
    a.part([E(cx - 0.3 * r, cy + 0.82 * r, 0.22 * r, 0.15 * r), E(cx + 0.34 * r, cy + 0.82 * r, 0.22 * r, 0.15 * r)], shade, edge)
    # horns and ears behind the head
    for side in (-1, 1):
        base = cx + 0.05 * r + side * 0.3 * r
        tipx = cx + 0.05 * r + side * 0.62 * r
        a.part([P([(base - 0.14 * r, cy - 0.72 * r), (base + side * 0.1 * r, cy - 1.12 * r), (tipx, cy - 1.38 * r),
                   (base + side * 0.2 * r, cy - 0.98 * r), (base + 0.14 * r, cy - 0.72 * r)])], horn, edge, shade=horn_s, off=0.06 * r)
        a.part([P([(cx + side * 0.42 * r, cy - 0.58 * r), (cx + side * 0.9 * r, cy - 0.74 * r), (cx + side * 0.5 * r, cy - 0.28 * r)])],
               body, edge, shade=shade, off=0.08 * r)
    # body + big head as one outlined blob
    m = a.part([E(cx, cy + 0.28 * r, 0.52 * r, 0.56 * r), E(cx + 0.05 * r, cy - 0.42 * r, 0.6 * r, 0.52 * r)],
               body, edge, shade=shade, off=0.14 * r)
    a.clip(m, [E(cx + 0.06 * r, cy + 0.38 * r, 0.28 * r, 0.3 * r)], mix(body, light, 0.6))
    a.clip(m, [E(cx - 0.24 * r, cy - 0.66 * r, 0.2 * r, 0.11 * r)], light)
    # angry face
    for i, ex in enumerate((cx - 0.12 * r, cx + 0.3 * r)):
        a.part([E(ex, cy - 0.4 * r, 0.12 * r, 0.1 * r)], (255, 228, 90), edge, w=1.2)
        a.fill([E(ex + 0.03 * r, cy - 0.4 * r, 0.035 * r, 0.08 * r)], edge)
        inner, outer = (ex + 0.13 * r, ex - 0.14 * r) if i == 0 else (ex - 0.13 * r, ex + 0.14 * r)
        a.line([(outer, cy - 0.6 * r), (inner, cy - 0.52 * r)], 1.8, edge)
    a.line([(cx - 0.04 * r, cy - 0.17 * r), (cx + 0.12 * r, cy - 0.11 * r), (cx + 0.3 * r, cy - 0.2 * r)], 1.5, edge)
    a.part([P([(cx + 0.18 * r, cy - 0.14 * r), (cx + 0.24 * r, cy - 0.15 * r), (cx + 0.21 * r, cy - 0.05 * r)])], WHITE, edge, w=0.8)
    # arms: one hangs, the other holds up an ember. A throw cocks the ember back over the head, swelling it, then flings
    # it the way the shot goes; it grows back in the hand as the arm comes home.
    a.part([L([(cx - 0.42 * r, cy + 0.05 * r), (cx - 0.7 * r, cy + 0.3 * r)], 0.2 * r)], body, edge)
    elbow = pose.at((0.78, -0.08), (0.58, -0.52), dict(side=(0.9, -0.12), down=(0.74, 0.22), up=(0.84, -0.42)))
    hand = pose.at((0.88, -0.3), (0.18, -0.86), dict(side=(1.25, -0.3), down=(0.92, 0.46), up=(0.94, -0.96)))
    a.part([L([(cx + 0.42 * r, cy + 0.05 * r), (cx + elbow[0] * r, cy + elbow[1] * r), (cx + hand[0] * r, cy + hand[1] * r)],
              0.2 * r)], body, edge)
    if not pose.resting:
        a.part([E(cx + hand[0] * r, cy + hand[1] * r, 0.12 * r)], body, edge, w=1.6)
    shot = dict(side=(1.42, -0.34), down=(1.0, 0.62), up=(1.02, -1.12))[pose.dir]  # just past the hand as it lets go
    shot = (cx + shot[0] * r, cy + shot[1] * r)
    grow = 1 + 0.35 * pose.wind if pose.hit == 0 else max(0.0, 1 - 1.6 * pose.hit)
    if grow < 0.2:
        return shot
    ember = pose.at((0.92, -0.55), (0.12, -1.12), dict(side=(1.29, -0.55), down=(0.96, 0.21), up=(0.98, -1.21)))
    ex, ey = cx + ember[0] * r, cy + ember[1] * r
    er = r * grow
    flick = 1 + 0.12 * wave(ph, 3)
    a.glow(ex, ey, 0.75 * er, (255, 150, 60), 0.55 + 0.3 * pose.wind)
    a.part([P([(ex - 0.2 * er * flick, ey + 0.05 * er), (ex - 0.02 * er, ey - 0.42 * er * flick), (ex + 0.2 * er * flick, ey + 0.05 * er),
               (ex, ey + 0.2 * er)]), E(ex, ey + 0.02 * er, 0.19 * er)], (255, 150, 60), (120, 40, 10), w=1.6)
    a.fill([E(ex, ey + 0.04 * er, 0.1 * er * flick, 0.12 * er)], (255, 234, 150))
    return shot


def draw_brute(a, cx, cy, r, ph, pose=REST, kind=None):
    hide, shade, light, edge = (150, 70, 58), (106, 44, 40), (192, 112, 90), (42, 16, 18)
    leather, gold = (112, 74, 46), (214, 170, 70)
    wood, wood_s = (138, 94, 56), (98, 64, 38)
    metal, tusk = (196, 200, 210), (244, 232, 200)
    breathe = wave(ph)
    lift = max(0.0, breathe) * 0.14 * r
    k = 1 + 0.03 * breathe
    for side in (-1, 1):
        a.part([E(cx + side * 0.38 * r, cy + 0.72 * r, 0.26 * r, 0.19 * r)], shade, edge)
    # spiked club, held up in the right fist. A swing carries the fist and turns the club about it: back over the
    # shoulder and down onto the ground in front for a side blow, overhead and down toward the viewer, or from low in
    # front up over the head.
    hx, hy = cx + 0.88 * r, cy + 0.16 * r - lift
    top = (cx + 1.1 * r, cy - 0.8 * r - lift)
    grip = (dict(side=(0.3, -0.95), down=(0.45, -0.85), up=(1.0, 0.45)), dict(side=(0.85, 0.15), down=(0.35, 0.3), up=(0.7, -0.7)))
    turn = (dict(side=-73, down=-43, up=137), dict(side=127, down=162, up=-18))

    def club_at(g, deg): return Xf((hx, hy), deg, (cx + g[0] * r, cy + g[1] * r - lift))

    xf = club_at(pose.at((0.88, 0.16), *grip), pose.at(0, *turn))
    fist = xf.pt(hx, hy)
    struck = club_at(pose.key(grip[1]), pose.key(turn[1])).pt(*top)  # where the head lands

    def club():
        if pose.hit > 0.5:
            if pose.dir == "down":  # coming at the viewer, the head drops straight down the front
                path = bowed(club_at(pose.key(grip[0]), pose.key(turn[0])).pt(*top), struck, 0.3 * r, (cx, cy))[4:]
            else:
                path = [club_at(pose.swing(*grip, t), pose.swing(*turn, t)).pt(*top) for t in frange(0.3, 1, 12)]
            a.smear(path, 0.5 * r, (255, 236, 200), (pose.hit - 0.5) / 0.5)
        for ang in (-2.3, -1.5, -0.7, 0.1):
            sx, sy = top[0] + math.cos(ang) * 0.26 * r, top[1] + math.sin(ang) * 0.24 * r
            a.part(xf(P([(sx + math.cos(ang + 1.6) * 0.07 * r, sy + math.sin(ang + 1.6) * 0.07 * r),
                         (sx + math.cos(ang) * 0.17 * r, sy + math.sin(ang) * 0.17 * r),
                         (sx + math.cos(ang - 1.6) * 0.07 * r, sy + math.sin(ang - 1.6) * 0.07 * r)])), metal, edge, w=1.4)
        m = a.part(xf(P([(hx - 0.08 * r, hy + 0.1 * r), (hx + 0.08 * r, hy + 0.1 * r), (top[0] + 0.14 * r, top[1] + 0.1 * r),
                         (top[0] - 0.16 * r, top[1] + 0.1 * r)]), E(top[0], top[1], 0.26 * r, 0.24 * r)),
                   wood, edge, shade=wood_s, off=0.08 * r)
        a.clip(m, xf(L([(top[0] - 0.1 * r, top[1] - 0.08 * r), (top[0] + 0.02 * r, top[1] - 0.15 * r)], 1.6)), mix(wood, WHITE, 0.3))
        a.clip(m, xf(L([(hx - 0.01 * r, hy - 0.2 * r), (top[0] - 0.05 * r, top[1] + 0.3 * r)], 1.2)), wood_s)
        if pose.wind >= 1:
            a.glint(*xf.pt(top[0] + 0.1 * r, top[1] - 0.1 * r), 0.3 * r)

    in_front = pose.hit > 0 and pose.dir == "down"  # smashing toward the viewer, the club comes down in front of the body
    if not in_front:
        club()
    # shoulders + gut as one mass
    torso = [E(cx, cy + 0.08 * r, 0.78 * r * k, 0.66 * r * k),
             E(cx - 0.58 * r, cy - 0.28 * r, 0.36 * r * k, 0.3 * r * k), E(cx + 0.58 * r, cy - 0.28 * r, 0.36 * r * k, 0.3 * r * k)]
    m = a.part(torso, hide, edge, shade=shade, off=0.16 * r)
    a.clip(m, [E(cx + 0.05 * r, cy + 0.2 * r, 0.42 * r, 0.36 * r)], mix(hide, light, 0.45))
    a.clip(m, [E(cx - 0.68 * r, cy - 0.42 * r, 0.16 * r, 0.08 * r), E(cx + 0.46 * r, cy - 0.44 * r, 0.16 * r, 0.08 * r)], light)
    a.clip(m, [L([(cx - 0.66 * r, cy - 0.5 * r), (cx + 0.62 * r, cy + 0.52 * r)], 0.15 * r)], leather, edge)
    a.clip(m, [R(cx - 0.9 * r, cy + 0.4 * r, cx + 0.9 * r, cy + 0.56 * r)], leather, edge)
    a.part([R(cx - 0.08 * r, cy + 0.38 * r, cx + 0.12 * r, cy + 0.58 * r, 0.03 * r)], gold, edge, w=1.4)
    # head sunk between the shoulders
    hcx, hcy = cx + 0.1 * r, cy - 0.5 * r
    for side in (-1, 1):
        a.part([E(hcx + side * 0.32 * r, hcy - 0.02 * r, 0.1 * r, 0.08 * r)], hide, edge, w=1.8)
    m = a.part([E(hcx, hcy, 0.34 * r, 0.3 * r)], hide, edge, shade=shade, off=0.07 * r)
    a.clip(m, [P([(hcx - 0.34 * r, hcy - 0.14 * r), (hcx + 0.34 * r, hcy - 0.14 * r), (hcx + 0.3 * r, hcy - 0.02 * r),
                  (hcx - 0.3 * r, hcy - 0.02 * r)])], shade)
    for ex in (hcx - 0.12 * r, hcx + 0.14 * r):
        a.part([E(ex, hcy - 0.05 * r, 0.07 * r, 0.06 * r)], (255, 214, 90), edge, w=1.0)
        a.fill([E(ex + 0.02 * r, hcy - 0.045 * r, 0.03 * r)], edge)
    a.line([(hcx - 0.14 * r, hcy + 0.17 * r), (hcx + 0.18 * r, hcy + 0.17 * r)], 1.4, edge)
    for tx in (hcx - 0.13 * r, hcx + 0.17 * r):
        a.part([P([(tx - 0.05 * r, hcy + 0.18 * r), (tx, hcy + 0.07 * r), (tx + 0.05 * r, hcy + 0.18 * r)])], tusk, edge, w=1.2)
    # the swinging arm, which at rest hides behind its fist
    if not pose.resting:
        a.part([L([(cx + 0.6 * r, cy - 0.28 * r), fist], 0.28 * r)], hide, edge, shade=shade, off=0.06 * r)
    if in_front:
        club()
    # fists
    a.part([L([(cx - 0.72 * r, cy - 0.12 * r), (cx - 0.9 * r, cy + 0.3 * r)], 0.3 * r)], hide, edge, shade=shade, off=0.06 * r)
    for fx, fy in ((cx - 0.92 * r, cy + 0.38 * r), fist):
        m = a.part([E(fx, fy, 0.24 * r)], hide, edge, shade=shade, off=0.07 * r)
        a.clip(m, [L([(fx - 0.14 * r, fy - 0.05 * r), (fx + 0.14 * r, fy - 0.05 * r)], 1.2)], shade)
    if pose.hit > 0:
        ground = pose.dir != "up"
        a.burst(struck[0], struck[1] + (0.24 * r if ground else -0.1 * r), 0.4 * r, 1 - pose.hit, edge, ground)


def draw_tank(a, cx, cy, r, ph, pose=REST, kind=None):
    stone, stone_s, stone_l = (128, 126, 118), (90, 88, 86), (166, 164, 154)
    moss, moss_l = (96, 138, 72), (132, 170, 92)
    steel, steel_s, steel_l, edge = (170, 176, 190), (116, 122, 140), (216, 222, 234), (28, 32, 44)
    bronze, bronze_s = (196, 150, 72), (148, 104, 44)
    pulse = pose.at(0.5 + 0.5 * wave(ph), 1.0, 1.0)  # the visor blazes through a swing
    sway = wave(ph) * 0.9
    for side in (-1, 1):
        a.part([R(cx + side * 0.42 * r - 0.17 * r, cy + 0.5 * r, cx + side * 0.42 * r + 0.17 * r, cy + 0.93 * r, 0.08 * r)],
               stone, edge, shade=stone_s, off=0.06 * r)
    ux = cx + sway
    # stone fist on the right. A swing raises it and hammers it down: forward onto the ground for a side blow, down in
    # front toward the viewer, or from low at the hip up over the head.
    grip = (dict(side=(0.45, -1.2), down=(0.3, -1.25), up=(1.1, 0.55)), dict(side=(1.5, 0.3), down=(0.35, 1.0), up=(0.72, -1.45)))
    fx, fy = pose.at((0.86, 0.3), *grip)
    fist = (ux + fx * r, cy + fy * r)
    base = (ux + 0.62 * r, cy - 0.1 * r)

    def arm():
        if pose.resting:
            a.part([L([(ux + 0.62 * r, cy - 0.1 * r), (ux + 0.86 * r, cy + 0.3 * r)], 0.3 * r)], stone, edge, shade=stone_s, off=0.06 * r)
            return
        if pose.hit > 0.5:
            (wx, wy), (sx, sy) = pose.key(grip[0]), pose.key(grip[1])
            path = bowed((ux + wx * r, cy + wy * r), (ux + sx * r, cy + sy * r), 0.4 * r, (ux, cy))
            a.smear(path[3:], 0.6 * r, (225, 232, 245), (pose.hit - 0.5) / 0.5)
        a.part([L([base, fist], 0.34 * r)], stone, edge, shade=stone_s, off=0.06 * r)
        fm = a.part([E(fist[0], fist[1], 0.3 * r, 0.27 * r)], stone, edge, shade=stone_s, off=0.08 * r)
        for k in (-1, 0, 1):  # knuckles
            a.clip(fm, [L([(fist[0] + k * 0.1 * r, fist[1] - 0.2 * r), (fist[0] + k * 0.1 * r, fist[1] - 0.04 * r)], 1.2)], stone_s)
        a.clip(fm, [E(fist[0] - 0.1 * r, fist[1] - 0.14 * r, 0.08 * r, 0.045 * r)], stone_l)
        if pose.wind >= 1:
            a.glint(fist[0] + 0.12 * r, fist[1] - 0.16 * r, 0.3 * r)

    in_front = pose.hit > 0 and pose.dir == "down"  # hammering toward the viewer, the fist comes down in front
    if not in_front:
        arm()
    # breastplate
    m = a.part([R(ux - 0.68 * r, cy - 0.5 * r, ux + 0.68 * r, cy + 0.68 * r, 0.32 * r)], steel, edge, shade=steel_s, off=0.12 * r)
    for yy in (cy + 0.12 * r, cy + 0.38 * r):
        a.clip(m, [L([(ux - 0.7 * r, yy), (ux + 0.7 * r, yy)], 1.4)], edge)
    a.clip(m, [L([(ux, cy - 0.45 * r), (ux, cy + 0.05 * r)], 2.0)], steel_l)
    a.clip(m, [E(ux - 0.36 * r, cy - 0.3 * r, 0.2 * r, 0.1 * r)], steel_l)
    for px, py in ((-0.5, -0.1), (0.5, -0.1), (-0.5, 0.25), (0.5, 0.25), (-0.3, 0.52), (0.3, 0.52)):
        a.part([E(ux + px * r, cy + py * r, 0.045 * r)], steel_l, edge, w=1.0)
    # boulder pauldrons with moss on top
    rng = random.Random(7)
    for side in (-1, 1):
        pcx, pcy = ux + side * 0.7 * r, cy - 0.42 * r
        pts = [(pcx + math.cos(k / 8 * TAU) * 0.34 * r * rng.uniform(0.85, 1), pcy + math.sin(k / 8 * TAU) * 0.28 * r * rng.uniform(0.85, 1))
               for k in range(8)]
        pm = a.part([P(pts)], stone, edge, shade=stone_s, off=0.08 * r)
        a.clip(pm, [E(pcx - 0.04 * r, pcy - 0.2 * r, 0.3 * r, 0.14 * r)], moss)
        a.clip(pm, [E(pcx - 0.1 * r, pcy - 0.24 * r, 0.08 * r, 0.04 * r), E(pcx + 0.12 * r, pcy - 0.2 * r, 0.05 * r, 0.03 * r)], moss_l)
        a.clip(pm, [E(pcx - 0.12 * r, pcy + 0.02 * r, 0.06 * r, 0.04 * r)], stone_l)
    # helmet with a glowing visor
    hcx, hcy = ux + 0.04 * r, cy - 0.62 * r
    m = a.part([E(hcx, hcy, 0.34 * r, 0.28 * r)], steel, edge, shade=steel_s, off=0.07 * r)
    a.clip(m, [L([(hcx - 0.02 * r, hcy - 0.28 * r), (hcx - 0.02 * r, hcy - 0.06 * r)], 2.2)], steel_l)
    visor = R(hcx - 0.24 * r, hcy - 0.02 * r, hcx + 0.28 * r, hcy + 0.12 * r, 0.05 * r)
    a.clip(m, [visor], (24, 20, 26), edge, w=1.0)
    a.glow(hcx + 0.02 * r, hcy + 0.05 * r, 0.45 * r, (255, 130, 50), 0.25 + 0.45 * pulse)
    a.fill([R(hcx - 0.18 * r, hcy + 0.03 * r, hcx + 0.22 * r, hcy + 0.07 * r, 0.02 * r)], mix((200, 80, 30), (255, 210, 120), pulse))
    if in_front:
        arm()
    # tower shield in front of the left side
    sy = cy + wave(ph, 1, 0.25) * 0.6
    a.part([R(cx - 1.0 * r, sy - 0.3 * r, cx - 0.44 * r, sy + 0.86 * r, 0.14 * r)], bronze, edge, shade=bronze_s, off=0.05 * r)
    m = a.part([R(cx - 0.93 * r, sy - 0.23 * r, cx - 0.51 * r, sy + 0.79 * r, 0.1 * r)], steel_s, edge, w=1.2,
               shade=mix(steel_s, (0, 0, 0), 0.2), off=0.06 * r)
    a.clip(m, [L([(cx - 0.72 * r, sy - 0.2 * r), (cx - 0.72 * r, sy + 0.76 * r)], 1.4)], edge)
    a.clip(m, [E(cx - 0.84 * r, sy - 0.12 * r, 0.05 * r, 0.1 * r)], steel)
    a.part([E(cx - 0.72 * r, sy + 0.28 * r, 0.12 * r)], bronze, edge, w=1.4, shade=bronze_s, off=0.03 * r)
    for yy in (sy - 0.14 * r, sy + 0.7 * r):
        a.part([E(cx - 0.72 * r, yy, 0.035 * r)], bronze, edge, w=0.9)
    if pose.hit > 0:
        sx, sy = pose.key(grip[1])
        ground = pose.dir != "up"
        a.burst(ux + sx * r, cy + sy * r + (0.28 * r if ground else -0.2 * r), 0.42 * r, 1 - pose.hit, edge, ground)


def draw_sniper(a, cx, cy, r, ph, pose=REST, kind=None):
    robe, robe_s, robe_l, edge = (108, 60, 170), (72, 38, 124), (150, 106, 214), (30, 14, 52)
    trim, hood_in, skin = (222, 178, 78), (20, 10, 34), (196, 176, 208)
    wood, crystal = (96, 66, 42), (170, 236, 255)
    eye = (246, 150, 255)
    pulse = pose.at(0.5 + 0.5 * wave(ph), 1.0, 0.15)  # a shot charges the crystal to the full, and spends it
    charge = pose.at(0.0, 1.0, 0.0)
    hem = cy + 1.2 * r
    # robe with a swaying hem
    hem_pts = [(cx + x * r + wave(ph, 1, i * 0.2) * 0.03 * r, hem + wave(ph, 1, i * 0.23) * 0.04 * r)
               for i, x in enumerate((0.6, 0.3, 0.0, -0.3, -0.6))]
    robe_shape = P([(cx - 0.34 * r, cy - 0.45 * r), (cx + 0.34 * r, cy - 0.45 * r)] + hem_pts)
    m = a.part([robe_shape, E(cx, cy - 0.35 * r, 0.4 * r, 0.22 * r)], robe, edge, shade=robe_s, off=0.12 * r)
    for x0, x1 in ((-0.22, -0.32), (0.26, 0.38)):
        a.clip(m, [L([(cx + x0 * r, cy + 0.1 * r), (cx + x1 * r, hem)], 1.5)], robe_s)
    a.clip(m, [R(cx - 0.07 * r, cy - 0.45 * r, cx + 0.07 * r, hem + r)], trim, edge)
    a.clip(m, [E(cx - 0.22 * r, cy - 0.38 * r, 0.12 * r, 0.06 * r)], robe_l)
    # staff with a crystal lens. A shot lifts it and tips it back while the crystal charges, then levels it at the target.
    sx0, sy0, sx1, sy1 = cx + 0.6 * r, cy + 1.22 * r, cx + 0.74 * r, cy - 1.18 * r
    hx, hy = sx0 + (sx1 - sx0) * 0.46, sy0 + (sy1 - sy0) * 0.46
    grips = ((0.02, -0.18), dict(side=(-0.12, -0.15), down=(0.0, 0.05), up=(-0.04, -0.3)))
    turns = (-14.0, dict(side=52.0, down=140.0, up=-4.0))

    def staff_at(g, deg): return Xf((hx, hy), deg, (hx + g[0] * r, hy + g[1] * r))

    xf = staff_at(pose.at((0.0, 0.0), *grips), pose.at(0.0, *turns))
    a.part(xf(L([(sx0, sy0), (sx1, sy1)], 3.0)), wood, edge, w=1.6)
    kx, ky = sx1 + 0.01 * r, sy1 - 0.24 * r
    gx, gy = xf.pt(kx, ky)
    a.glow(gx, gy, 0.8 * r * (1 + 0.5 * charge), (150, 220, 255), 0.25 + 0.4 * pulse)
    for side in (-1, 1):
        a.part(xf(L([(sx1, sy1 + 0.02 * r), (kx + side * 0.16 * r, ky + 0.02 * r), (kx + side * 0.1 * r, ky - 0.24 * r)], 2.0)), trim, edge, w=1.2)
    km = a.part(xf(P([(kx, ky - 0.28 * r), (kx + 0.13 * r, ky), (kx, ky + 0.2 * r), (kx - 0.13 * r, ky)])), crystal, edge, w=1.6,
                shade=(100, 180, 230), off=0.04 * r)
    a.clip(km, xf(P([(kx, ky - 0.24 * r), (kx + 0.05 * r, ky - 0.04 * r), (kx, ky), (kx - 0.07 * r, ky - 0.02 * r)])),
           mix(crystal, WHITE, 0.4 + 0.5 * pulse))
    for i in range(4) if charge > 0 else ():  # motes of light drawn in to the crystal
        ang, dist = i / 4 * TAU + 1.6 * charge, 0.6 * r * (1.15 - charge)
        a.part([E(gx + math.cos(ang) * dist, gy + math.sin(ang) * dist, 0.06 * r)], mix(crystal, WHITE, 0.6), edge, w=1.0)
    # sleeve and hand gripping the staff; the forearm follows the hand
    mx, my = xf.pt(hx, hy)
    ox, oy = mx - hx, my - hy
    a.part([P([(cx + 0.18 * r, cy - 0.4 * r), (cx + 0.46 * r, cy - 0.3 * r), (cx + 0.7 * r + ox, cy + 0.05 * r + oy),
               (cx + 0.54 * r + ox, cy + 0.22 * r + oy), (cx + 0.2 * r + ox * 0.5, cy + 0.0 * r + oy * 0.5)])], robe, edge, shade=robe_s, off=0.06 * r)
    a.part([E(mx, my, 0.1 * r)], skin, edge, w=1.4)
    # pointed hood, face hidden but for two lit eyes
    hcx, hcy = cx + 0.02 * r, cy - 0.72 * r
    m = a.part([E(hcx, hcy, 0.4 * r, 0.4 * r), P([(hcx - 0.34 * r, hcy - 0.14 * r), (hcx - 0.16 * r, hcy - 0.62 * r),
                                                  (hcx + 0.14 * r, hcy - 0.3 * r)])], robe, edge, shade=robe_s, off=0.08 * r)
    a.clip(m, [E(hcx - 0.18 * r, hcy - 0.26 * r, 0.12 * r, 0.06 * r)], robe_l)
    a.clip(m, [E(hcx + 0.07 * r, hcy + 0.08 * r, 0.3 * r, 0.28 * r)], trim, edge)
    a.clip(m, [E(hcx + 0.07 * r, hcy + 0.1 * r, 0.24 * r, 0.23 * r)], hood_in)
    for ex in (hcx - 0.02 * r, hcx + 0.17 * r):
        a.glow(ex, hcy + 0.08 * r, 0.22 * r, eye, 0.4 + 0.3 * pulse)
        a.fill([E(ex, hcy + 0.08 * r, 0.05 * r, 0.035 * r)], mix(eye, WHITE, 0.5))
    return staff_at(pose.key(grips[1]), pose.key(turns[1])).pt(kx, ky)  # the crystal, levelled


def draw_warlord(a, cx, cy, r, ph, pose=REST, kind="melee"):
    arm, arm_s, arm_l, edge = (150, 24, 36), (102, 14, 26), (206, 66, 68), (36, 6, 12)
    gold, gold_s = (255, 200, 60), (196, 136, 34)
    cape, cape_s = (62, 22, 46), (40, 14, 30)
    horn, horn_s = (232, 216, 184), (176, 156, 126)
    steel, steel_s = (206, 212, 226), (140, 146, 166)
    eye = (255, 220, 110)
    pulse = pose.at(0.5 + 0.5 * wave(ph), 1.0, 0.8)  # the eyes flare through an attack
    a.glow(cx, cy + 0.2 * r, 1.25 * r, (220, 40, 40), 0.18 + 0.14 * pulse)
    # cape rippling behind
    hem = [(cx + x * r, cy + 0.95 * r + wave(ph, 1, i * 0.18) * 0.05 * r) for i, x in enumerate((0.95, 0.5, 0.0, -0.5, -0.95))]
    m = a.part([P([(cx - 0.55 * r, cy - 0.45 * r), (cx + 0.55 * r, cy - 0.45 * r)] + hem)], cape, edge, shade=cape_s, off=0.1 * r)
    for x in (-0.6, -0.2, 0.25, 0.65):
        a.clip(m, [L([(cx + x * 0.5 * r, cy - 0.2 * r), (cx + x * r, cy + 0.95 * r)], 1.5)], cape_s)
    # greaves
    for side in (-1, 1):
        gm = a.part([R(cx + side * 0.3 * r - 0.15 * r, cy + 0.45 * r, cx + side * 0.3 * r + 0.15 * r, cy + 0.92 * r, 0.07 * r)],
                    arm_s, edge, shade=mix(arm_s, (0, 0, 0), 0.3), off=0.05 * r)
        a.clip(gm, [R(cx + side * 0.3 * r - 0.15 * r, cy + 0.6 * r, cx + side * 0.3 * r + 0.15 * r, cy + 0.66 * r)], gold)
    # breastplate
    m = a.part([E(cx, cy + 0.1 * r, 0.58 * r, 0.6 * r)], arm, edge, shade=arm_s, off=0.12 * r)
    a.clip(m, [E(cx - 0.24 * r, cy - 0.12 * r, 0.16 * r, 0.1 * r)], arm_l)
    a.clip(m, [L([(cx, cy - 0.45 * r), (cx, cy + 0.45 * r)], 1.6)], gold_s)
    a.clip(m, [R(cx - 0.6 * r, cy + 0.44 * r, cx + 0.6 * r, cy + 0.56 * r)], gold, edge)
    a.part([P([(cx, cy - 0.08 * r), (cx + 0.13 * r, cy + 0.1 * r), (cx, cy + 0.28 * r), (cx - 0.13 * r, cy + 0.1 * r)])], gold, edge, w=1.4)
    a.fill([E(cx, cy + 0.1 * r, 0.05 * r)], (220, 30, 50))
    # the greatsword's grip, which an attack carries (turning the blade about it). A swing cleaves from over the shoulder
    # down onto the ground in front, overhead and down toward the viewer, or from low up over the head; a cast raises the
    # sword overhead while a fireball gathers on its tip, then points it at the target. The blade is long, so it's
    # foreshortened (`reach`) as it comes down, landing where the swing actually reaches.
    gx, gy = cx + 0.76 * r, cy + 0.12 * r               # crossguard centre
    tx, ty = cx + 1.25 * r, cy - 1.05 * r
    if kind == "ranged":
        grip = ((0.5, -0.45), dict(side=(0.5, -0.12), down=(0.5, 0.1), up=(0.6, -0.55)))
        turn = (-53, dict(side=52, down=127, up=-8))
        reach = (1.0, 0.8)
    else:
        grip = (dict(side=(0.3, -0.8), down=(0.45, -0.7), up=(0.8, 0.45)), dict(side=(0.55, 0.2), down=(0.4, 0.35), up=(0.6, -0.45)))
        turn = (dict(side=-78, down=-58, up=142), dict(side=102, down=149, up=-23))
        reach = (dict(side=1.0, down=1.0, up=0.85), 0.8)

    def sword_at(g, deg, k): return Xf((gx, gy), deg, (cx + g[0] * r, cy + g[1] * r), k)

    def struck_sword(): return sword_at(pose.key(grip[1]), pose.key(turn[1]), pose.key(reach[1]))

    xf = sword_at(pose.at((0.76, 0.12), *grip), pose.at(0, *turn), pose.at(1.0, *reach))
    dx, dy = tx - gx, ty - gy
    n = math.hypot(dx, dy)
    nx, ny = -dy / n, dx / n
    hand = xf.pt(gx - dx / n * 0.13 * r, gy - dy / n * 0.13 * r)
    if not pose.resting:  # the sword arm, which at rest hides behind its gauntlet
        a.part([L([(cx + 0.55 * r, cy - 0.25 * r), hand], 0.2 * r)], arm, edge, shade=arm_s, off=0.05 * r)
    # pauldrons with gold rims and bone spikes
    for side in (-1, 1):
        px, py = cx + side * 0.62 * r, cy - 0.3 * r
        for k, ang in enumerate((-2.0, -1.4) if side < 0 else (-1.14, -1.74)):
            ang = ang if side < 0 else ang
            bx, by = px + math.cos(ang) * 0.26 * r, py + math.sin(ang) * 0.2 * r
            a.part([P([(bx + math.cos(ang + 1.57) * 0.07 * r, by + math.sin(ang + 1.57) * 0.07 * r),
                       (bx + math.cos(ang) * 0.24 * r, by + math.sin(ang) * 0.24 * r),
                       (bx + math.cos(ang - 1.57) * 0.07 * r, by + math.sin(ang - 1.57) * 0.07 * r)])], horn, edge, w=1.6)
        pm = a.part([E(px, py, 0.34 * r, 0.28 * r)], gold, edge, shade=gold_s, off=0.05 * r)
        a.clip(pm, [E(px, py - 0.03 * r, 0.28 * r, 0.22 * r)], arm)
        a.clip(pm, [E(px - 0.08 * r, py - 0.1 * r, 0.12 * r, 0.06 * r)], arm_l)
    # horned helmet under a gold crown
    hcx, hcy = cx + 0.03 * r, cy - 0.52 * r
    for side in (-1, 1):
        bx = hcx + side * 0.2 * r
        a.part([P([(bx, hcy - 0.2 * r), (bx + side * 0.3 * r, hcy - 0.3 * r), (hcx + side * 0.62 * r, hcy - 0.6 * r),
                   (bx + side * 0.28 * r, hcy - 0.14 * r), (bx + side * 0.06 * r, hcy + 0.02 * r)])], horn, edge, shade=horn_s, off=0.05 * r)
    m = a.part([E(hcx, hcy, 0.3 * r, 0.28 * r)], arm_s, edge, shade=mix(arm_s, (0, 0, 0), 0.3), off=0.06 * r)
    a.clip(m, [P([(hcx - 0.2 * r, hcy - 0.02 * r), (hcx + 0.24 * r, hcy - 0.02 * r), (hcx + 0.24 * r, hcy + 0.06 * r),
                  (hcx + 0.06 * r, hcy + 0.06 * r), (hcx + 0.06 * r, hcy + 0.24 * r), (hcx - 0.02 * r, hcy + 0.24 * r),
                  (hcx - 0.02 * r, hcy + 0.06 * r), (hcx - 0.2 * r, hcy + 0.06 * r)])], (18, 4, 8))
    for ex in (hcx - 0.1 * r, hcx + 0.15 * r):
        a.glow(ex, hcy + 0.02 * r, 0.2 * r, eye, 0.35 + 0.45 * pulse)
        a.fill([E(ex, hcy + 0.02 * r, 0.045 * r, 0.03 * r)], mix(eye, WHITE, 0.4))
    band_y = hcy - 0.2 * r
    spikes = [P([(hcx + x * r - 0.08 * r, band_y), (hcx + x * r, band_y - h * r), (hcx + x * r + 0.08 * r, band_y)])
              for x, h in ((-0.18, 0.26), (0.0, 0.42), (0.18, 0.26))]
    cm = a.part([R(hcx - 0.27 * r, band_y - 0.02 * r, hcx + 0.27 * r, band_y + 0.1 * r, 0.02 * r)] + spikes, gold, edge,
                shade=gold_s, off=0.03 * r)
    a.clip(cm, [E(hcx - 0.06 * r, band_y - 0.14 * r, 0.03 * r, 0.08 * r)], mix(gold, WHITE, 0.5))
    for x in (-0.16, 0.0, 0.16):
        a.fill([E(hcx + x * r, band_y + 0.04 * r, 0.035 * r)], (220, 30, 50) if x else (80, 200, 255))
    # greatsword on the right, point up
    edge_pt = (tx - dx / n * 0.3 * r, ty - dy / n * 0.3 * r)  # near the tip, where the smear rides
    if kind == "melee" and pose.hit > 0.5:
        if pose.dir == "down":  # coming at the viewer, the blade drops straight down the front
            path = bowed(sword_at(pose.key(grip[0]), pose.key(turn[0]), pose.key(reach[0])).pt(*edge_pt),
                         struck_sword().pt(*edge_pt), 0.3 * r, (cx, cy))[4:]
        else:
            path = [sword_at(pose.swing(*grip, t), pose.swing(*turn, t), pose.swing(*reach, t)).pt(*edge_pt) for t in frange(0.3, 1, 12)]
        a.smear(path, 0.6 * r, (255, 214, 190), (pose.hit - 0.5) / 0.5)
    bw = 0.08 * r
    bm = a.part(xf(P([(gx + nx * bw, gy + ny * bw), (tx - dx / n * 0.2 * r + nx * bw, ty - dy / n * 0.2 * r + ny * bw), (tx, ty),
                      (tx - dx / n * 0.2 * r - nx * bw, ty - dy / n * 0.2 * r - ny * bw), (gx - nx * bw, gy - ny * bw)])),
                steel, edge, w=1.8, shade=steel_s, off=0.03 * r)
    a.clip(bm, xf(L([(gx, gy), (tx - dx / n * 0.25 * r, ty - dy / n * 0.25 * r)], 1.2)), steel_s)
    a.part(xf(L([(gx - dx / n * 0.26 * r, gy - dy / n * 0.26 * r), (gx, gy)], 0.09 * r)), (70, 36, 30), edge, w=1.6)
    a.part(xf(L([(gx + nx * 0.22 * r, gy + ny * 0.22 * r), (gx - nx * 0.22 * r, gy - ny * 0.22 * r)], 0.09 * r)), gold, edge, w=1.6)
    a.part(xf(E(gx - dx / n * 0.3 * r, gy - dy / n * 0.3 * r, 0.06 * r)), gold, edge, w=1.4)
    tip = xf.pt(tx, ty)
    if kind == "melee" and pose.wind >= 1:
        a.glint(*tip, 0.32 * r)
    if kind == "ranged" and pose.hit == 0 and pose.wind > 0:  # the fireball gathering on the tip
        orb = 0.2 * r * pose.wind
        a.glow(tip[0], tip[1], 2.0 * orb, (255, 110, 40), 0.3 + 0.5 * pose.wind)
        a.part([E(tip[0], tip[1], orb)], (255, 140, 50), (120, 30, 10), w=1.4)
        a.fill([E(tip[0] - 0.25 * orb, tip[1] - 0.25 * orb, 0.45 * orb)], (255, 230, 150))
    # gauntlets
    for fx, fy in (hand, (cx - 0.8 * r, cy + 0.3 * r)):
        fm = a.part([E(fx, fy, 0.14 * r)], arm, edge, shade=arm_s, off=0.04 * r)
        a.clip(fm, [L([(fx - 0.09 * r, fy - 0.05 * r), (fx + 0.09 * r, fy - 0.05 * r)], 1.4)], gold)
    if kind == "melee" and pose.hit > 0:
        a.burst(*struck_sword().pt(tx, ty), 0.36 * r, 1 - pose.hit, edge, ground=pose.dir != "up")
    return struck_sword().pt(tx, ty)


def draw_wisp(a, cx, cy, r, ph, pose=REST, kind=None):
    glow, core, edge = (80, 220, 255), (170, 240, 255), (16, 62, 96)
    flame, flame_in = (90, 206, 250), (196, 246, 255)
    cy += wave(ph) * 2.5
    flick = wave(ph, 2) * 0.12 * r
    shot = dict(side=(0.92, 0.06), down=(0.08, 0.88), up=(-0.06, -1.35))[pose.dir]  # the mouth, or the flame for a shot up
    shot = (cx + shot[0] * r, cy + shot[1] * r)
    # A shot: the wisp draws itself in (squashed, its motes pulled close), then lunges the way the shot goes and spits it,
    # flinging the motes wide.
    shift = pose.at((0.0, 0.0), (-0.06, 0.04), dict(side=(0.14, 0.0), down=(0.0, 0.12), up=(0.0, -0.12)))
    sx, sy = pose.at((1.0, 1.0), (1.22, 0.8), dict(side=(1.2, 0.88), down=(0.88, 1.18), up=(0.88, 1.18)))
    orbit = pose.at(1.0, 0.7, 1.3)
    rise = pose.at(1.0, 0.8, dict(side=1.05, down=0.95, up=1.3))  # flame height
    cx += shift[0] * r
    cy += shift[1] * r
    a.glow(cx, cy, 1.5 * r, glow, 0.5 + 0.1 * wave(ph, 2) + 0.2 * pose.wind)
    motes = [(TAU * ph + i * TAU / 3) for i in range(3)]

    def mote(ang):
        mx, my = cx + math.cos(ang) * 1.0 * r * orbit, cy + math.sin(ang) * 0.45 * r * orbit
        a.glow(mx, my, 0.35 * r, glow, 0.6)
        a.part([E(mx, my, 0.12 * r)], (228, 255, 255), edge, w=1.2)

    for ang in motes:
        if math.sin(ang) < 0:
            mote(ang)
    # flame licking upward
    fy = cy - (0.55 * r * sy - 0.55 * r)  # the flame rides on the top of the (possibly squashed) core
    tip = (cx - 0.1 * r + flick, fy - 1.25 * r * rise)
    a.part([P([(cx - 0.5 * r, fy - 0.05 * r), (cx - 0.5 * r + flick * 0.5, fy - 0.55 * r * rise), (cx - 0.28 * r, fy - 0.72 * r * rise),
               tip, (cx + 0.1 * r + flick * 0.6, fy - 0.8 * r * rise), (cx + 0.34 * r - flick * 0.4, fy - 0.95 * r * rise),
               (cx + 0.5 * r, fy - 0.45 * r * rise), (cx + 0.5 * r, fy - 0.05 * r)])], flame, edge, w=2.2)
    a.fill([P([(cx - 0.28 * r, fy - 0.2 * r), (cx - 0.2 * r + flick * 0.4, fy - 0.7 * r * rise), (cx - 0.02 * r + flick * 0.8, fy - 0.98 * r * rise),
               (cx + 0.12 * r, fy - 0.62 * r * rise), (cx + 0.28 * r, fy - 0.2 * r)])], flame_in)
    m = a.part([E(cx, cy, 0.55 * r * sx, 0.55 * r * sy)], core, edge, w=2.4, shade=(110, 206, 240), off=0.1 * r)
    a.clip(m, [E(cx - 0.22 * r, cy - 0.22 * r, 0.14 * r, 0.09 * r)], WHITE)
    for ex in (cx - 0.06 * r, cx + 0.2 * r):
        a.fill([E(ex, cy + 0.02 * r, 0.055 * r, 0.1 * r)], edge)
    mouth = pose.at(1.0, 0.6, 2.4)  # pursed on the draw, wide on the spit
    a.fill([E(cx + 0.08 * r, cy + 0.24 * r, 0.05 * r * mouth, 0.04 * r * mouth)], edge)
    for ang in motes:
        if math.sin(ang) >= 0:
            mote(ang)
    return shot


DRAW = {"imp": draw_imp, "brute": draw_brute, "tank": draw_tank, "sniper": draw_sniper, "warlord": draw_warlord, "wisp": draw_wisp}
# puff colours for the death burst: fill, outline
PUFF = {
    "imp": ((255, 150, 60), (60, 20, 10)), "brute": ((150, 70, 58), (42, 16, 18)), "tank": ((170, 176, 190), (28, 32, 44)),
    "sniper": ((150, 106, 214), (30, 14, 52)), "warlord": ((255, 200, 60), (36, 6, 12)), "wisp": ((170, 240, 255), (16, 62, 96)),
}
# muzzle-flash colour for each look's shots
FLASH = {"imp": (255, 150, 60), "sniper": (150, 220, 255), "warlord": (255, 120, 50), "wisp": (140, 230, 255)}
# how much of the whole-body lean and squash each look puts into an attack, and whether it pivots on its centre (it
# floats) rather than its feet
BODY = {"imp": (1.2, 1.0, False), "brute": (1.0, 1.0, False), "tank": (0.6, 0.6, False), "sniper": (0.7, 0.5, False),
        "warlord": (0.8, 0.7, False), "wisp": (0.5, 0.6, True)}


# ---- Strips ----------------------------------------------------------------------------------------------------------


def radius(look): return BASE_RADIUS * LOOKS[look]["scale"]


def render(look, ph):
    a = Art()
    DRAW[look](a, CANVAS / 2, CANVAS / 2, radius(look), ph)
    return a.img


def death_frame(body, look, t):
    """Idle pose knocked back and squashed flat onto its feet, fading under a burst of outlined puffs."""
    r = radius(look)
    size = CANVAS * SS
    ax, ay = size / 2, u(CANVAS / 2 + LOOKS[look]["hit"][1] * r)          # anchor at the feet
    sx, sy, rot = 1 + 0.2 * t, 1 - 0.55 * t, math.radians(12 * t)
    c, s_ = math.cos(rot), math.sin(rot)
    m00, m01, m10, m11 = c / sx, s_ / sx, -s_ / sy, c / sy                  # inverse of rotate-then-scale
    img = body.transform(body.size, Image.AFFINE, (m00, m01, ax - m00 * ax - m01 * ay, m10, m11, ay - m10 * ax - m11 * ay),
                         resample=Image.BICUBIC)
    alpha = img.getchannel("A").point(lambda v: int(v * (1 - t) ** 1.5))
    img.putalpha(alpha)
    fill, edge = PUFF[look]
    rng = random.Random(look)
    puffs = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    pd = ImageDraw.Draw(puffs)
    ease = 1 - (1 - t) ** 2
    for _ in range(11):
        ang, reach, pr = rng.uniform(0, TAU), rng.uniform(0.4, 1.0), rng.uniform(0.12, 0.22)
        col = mix(fill, WHITE, rng.uniform(0, 0.4))
        px, py = CANVAS / 2 + math.cos(ang) * r * reach * (0.3 + 0.8 * ease), CANVAS / 2 + math.sin(ang) * r * reach * (0.3 + 0.8 * ease) * 0.8
        rad = r * pr * (0.7 + 0.8 * t) * (1 - 0.6 * t)
        E(px, py, rad).draw(pd, edge, 1.6)
        E(px, py, rad).draw(pd, col)
    puffs.putalpha(puffs.getchannel("A").point(lambda v: int(v * (1 - t) ** 0.7)))
    img.alpha_composite(puffs)
    return img


def strip(frames):
    n = CANVAS * OUT
    out = Image.new("RGBA", (n * len(frames), n), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        out.paste(f.resize((n, n), Image.LANCZOS), (i * n, 0))
    return out


def build(look):
    idle = [render(look, i / IDLE_FRAMES) for i in range(IDLE_FRAMES)]
    death = [death_frame(idle[0], look, (i + 0.5) / DEATH_FRAMES) for i in range(DEATH_FRAMES)]
    idle_strip, death_strip = strip(idle), strip(death)
    idle_strip.save(os.path.join(ENEMIES_DIR, f"{look}_idle.png"))
    death_strip.save(os.path.join(ENEMIES_DIR, f"{look}_death.png"))
    print(f"{look}: {idle_strip.width}x{idle_strip.height} idle, {death_strip.width}x{death_strip.height} death")
    return idle_strip, death_strip


# ---- Attack sheets ---------------------------------------------------------------------------------------------------


def body_move(look, pose):
    """Whole-body anticipation and impact on top of the posed limbs: lean back and stretch up through the windup, then
    lurch toward the blow and squash into it. Pivots on the feet so the body stays planted, or on the centre for a look
    that floats. Returns (anchor, sx, sy, lean, dx, dy) in world units on the scratch canvas: scale about the anchor,
    shear so each unit above it moves `lean` right, then shift."""
    lean_k, squash_k, floats = BODY[look]
    r = radius(look)
    c = ATTACK_SCRATCH / 2
    ay = c if floats else c + LOOKS[look]["hit"][1] * r
    if pose.hit > 0:
        h = pose.hit
        lean, sx, sy, dx, dy = dict(side=(0.14, 1.05, 0.93, 0.1, 0.0), down=(0.0, 1.06, 0.91, 0.0, 0.06),
                                    up=(0.0, 0.96, 1.07, 0.0, -0.08))[pose.dir]
    else:
        h = pose.wind
        lean, sx, sy, dx, dy = -0.12, 0.96, 1.06, -0.04, 0.0
    return (c, ay), 1 + (sx - 1) * squash_k * h, 1 + (sy - 1) * squash_k * h, lean * lean_k * h, dx * r * h, dy * r * h


def moved(move, x, y):
    """Where the point (x, y) ends up under a body move."""
    (ax, ay), sx, sy, lean, dx, dy = move
    X, Y = sx * (x - ax), sy * (y - ay)
    return ax + X - lean * Y + dx, ay + Y + dy


def affine(img, move):
    """`img` under a body move; PIL wants the inverse map, in px."""
    (ax, ay), sx, sy, lean, dx, dy = move
    ax, ay, dx, dy = ax * SS, ay * SS, dx * SS, dy * SS
    return img.transform(img.size, Image.AFFINE, (1 / sx, lean / sx, ax - (ax + dx + lean * (ay + dy)) / sx,
                                                  0, 1 / sy, ay - (ay + dy) / sy), resample=Image.BICUBIC)


def attack_frame(look, kind, pose):
    """One attack frame on the scratch canvas, and for a shot, where it leaves on the release frame (body radii from the
    centre, art facing right)."""
    c, r = ATTACK_SCRATCH / 2, radius(look)
    a = Art(ATTACK_SCRATCH)
    shot = DRAW[look](a, c, c, r, 0.0, pose, kind)
    a = Art(ATTACK_SCRATCH, affine(a.img, body_move(look, pose)))
    if kind != "ranged":
        return a.img, None
    mx, my = moved(body_move(look, Pose(hit=1.0, dir=pose.dir)), *shot)
    if pose.hit > 0:
        a.flash(mx, my, min(0.32 * r, 10), 1 - pose.hit, FLASH[look])  # about the size of the shot itself
    return a.img, ((mx - c) / r, (my - c) / r)


def fitted(rows):
    """Frame size (world units) that holds every frame of a sheet, glow tails included, centred on the body: a multiple
    of 16 so the shipped frames are whole pixels and the body centre lands on one, and never smaller than the idle
    frame, whose glows it shares."""
    c = ATTACK_SCRATCH * SS / 2
    half = 0
    for f in (f for row in rows for f in row):
        box = f.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
        if box:
            half = max(half, c - box[0], box[2] - c, c - box[1], box[3] - c)
    size = int(math.ceil((2 * half / SS + 2) / 16)) * 16
    if size > ATTACK_SCRATCH:
        print(f"  warning: the art spills off the {ATTACK_SCRATCH}-unit scratch canvas and will be clipped")
    return max(CANVAS, min(ATTACK_SCRATCH, size))


def build_attack(look, kind):
    rows = []
    for d in ATTACK_ROWS:
        row = [attack_frame(look, kind, p) for p in [Pose(wind=w, dir=d) for w in WIND] + [Pose(hit=h, dir=d) for h in FOLLOW]]
        rows.append([img for img, _ in row])
        muzzle = row[ATTACK_HIT][1]
        if muzzle and math.dist(muzzle, LOOKS[look]["muzzle"][d]) > 0.03:
            print(f"  warning: {look}'s {d} shot leaves at ({muzzle[0]:.2f}, {muzzle[1]:.2f}), not where LOOKS says;"
                  " update LOOKS and EnemyIcons.Muzzle")
    canvas = fitted(rows)
    n, c, h = canvas * OUT, ATTACK_SCRATCH * SS // 2, canvas * SS // 2
    sheet = Image.new("RGBA", (n * ATTACK_FRAMES, n * len(rows)), (0, 0, 0, 0))
    for j, row in enumerate(rows):
        for i, f in enumerate(row):
            sheet.paste(f.crop((c - h, c - h, c + h, c + h)).resize((n, n), Image.LANCZOS), (i * n, j * n))
    sheet.save(os.path.join(ENEMIES_DIR, f"{look}_{kind}.png"))
    print(f"{look}: {sheet.width}x{sheet.height} {kind} ({canvas} units a frame)")
    return sheet, canvas


# ---- Previews --------------------------------------------------------------------------------------------------------


def frames_1x(strip_img, size=CANVAS, row=0):
    n = size * OUT
    return [strip_img.crop((i * n, row * n, (i + 1) * n, (row + 1) * n)).resize((size, size), Image.LANCZOS)
            for i in range(strip_img.width // n)]


def shadow(d, x, y, look):
    """The game's drop shadow (Enemy.DrawShadow), so previews show where the body meets the ground."""
    r = radius(look)
    hx, hy = LOOKS[look]["hit"]
    rx = hx * r * 0.9
    ry = max(4, rx * 0.35)
    cy = y + hy * r - ry * 0.5
    d.ellipse([x - rx, cy - ry, x + rx, cy + ry], fill=(0, 0, 0, 46))
    d.ellipse([x - rx * 0.65, cy - ry * 0.65, x + rx * 0.65, cy + ry * 0.65], fill=(0, 0, 0, 56))


def contact_sheet(built):
    pad, label = 12, 16
    rows = len(built)
    w = pad + (IDLE_FRAMES + DEATH_FRAMES) * CANVAS + pad * 2
    sheet = Image.new("RGBA", (w, pad + rows * (CANVAS + label + pad)), (58, 62, 72, 255))
    d = ImageDraw.Draw(sheet)
    y = pad
    for look, (idle, death) in built.items():
        d.text((pad, y), f"{look}  (yellow = hit box, cyan = top extent)", fill=(230, 230, 240))
        x = pad
        for i, f in enumerate(frames_1x(idle) + frames_1x(death)):
            if i == IDLE_FRAMES:
                x += pad
            sheet.alpha_composite(f, (x, y + label))
            if i == 0:
                r = radius(look)
                hx, hy = LOOKS[look]["hit"]
                c = (x + CANVAS / 2, y + label + CANVAS / 2)
                d.rectangle([c[0] - hx * r, c[1] - hy * r, c[0] + hx * r, c[1] + hy * r], outline=(255, 230, 0))
                ty = c[1] - LOOKS[look]["top"] * r
                d.line([(c[0] - 30, ty), (c[0] + 30, ty)], fill=(0, 230, 255))
            x += CANVAS
        y += CANVAS + label + pad
    sheet.convert("RGB").save(os.path.join(OUT_DIR, "contact_sheet.png"))


def attack_preview(attacks):
    """Every attack sheet at game scale, a row per direction, with the hit box on the first frame and, on the frame that
    lands, the swing's reach (red) or the muzzle (yellow)."""
    pad, label = 12, 16
    blocks = [(look, kind, sheet, canvas) for look, sheets in attacks.items() for kind, (sheet, canvas) in sheets.items()]
    w = pad * 2 + max(canvas for *_, canvas in blocks) * ATTACK_FRAMES
    h = pad + sum(label + canvas * len(ATTACK_ROWS) + pad for *_, canvas in blocks)
    out = Image.new("RGBA", (w, h), (58, 62, 72, 255))
    d = ImageDraw.Draw(out)
    y = pad
    for look, kind, sheet, canvas in blocks:
        d.text((pad, y), f"{look} {kind}  (rows: {', '.join(ATTACK_ROWS)})", fill=(230, 230, 240))
        y += label
        r = radius(look)
        hx, hy = LOOKS[look]["hit"]
        for j, dir_ in enumerate(ATTACK_ROWS):
            for i, f in enumerate(frames_1x(sheet, canvas, j)):
                x = pad + i * canvas
                out.alpha_composite(f, (x, y))
                cx, cy = x + canvas / 2, y + canvas / 2
                if i == 0:
                    d.rectangle([cx - hx * r, cy - hy * r, cx + hx * r, cy + hy * r], outline=(255, 230, 0))
                if i == ATTACK_HIT and kind == "melee":
                    reach = max(hx, hy) * r + MELEE_REACH
                    mid = dict(side=0, down=90, up=-90)[dir_]
                    d.arc([cx - reach, cy - reach, cx + reach, cy + reach], mid - MELEE_ARC / 2, mid + MELEE_ARC / 2, fill=(255, 60, 60))
                    for ang in (mid - MELEE_ARC / 2, mid + MELEE_ARC / 2):
                        d.line([(cx, cy), (cx + math.cos(math.radians(ang)) * reach, cy + math.sin(math.radians(ang)) * reach)], fill=(255, 60, 60))
                if i == ATTACK_HIT and kind == "ranged":
                    mx, my = LOOKS[look]["muzzle"][dir_]
                    d.ellipse([cx + mx * r - 3, cy + my * r - 3, cx + mx * r + 3, cy + my * r + 3], outline=(255, 230, 0))
            y += canvas
        y += pad
    out.convert("RGB").save(os.path.join(OUT_DIR, "attacks.png"))


def on_maps(built):
    stages = ["forest_glade", "desert_ruins", "swamp_bog", "frozen_cavern", "graveyard", "volcanic_caldera", "castle_floor"]
    w, h = 150 * len(built) + 40, 190
    sheet = Image.new("RGBA", (w, h * len(stages)), (0, 0, 0, 255))
    for row, name in enumerate(stages):
        path = os.path.join(STAGES_DIR, name + ".png")
        if not os.path.exists(path):
            continue
        bg = Image.open(path).convert("RGBA")
        x0, y0 = bg.width // 2 - w // 2, bg.height // 2 - h // 2 + 120
        tile = bg.crop((x0, y0, x0 + w, y0 + h))
        d = ImageDraw.Draw(tile, "RGBA")
        for i, (look, (idle, _)) in enumerate(built.items()):
            x, y = 40 + i * 150 + 55, h // 2
            shadow(d, x, y, look)
            tile.alpha_composite(frames_1x(idle)[0], (int(x - CANVAS / 2), int(y - CANVAS / 2)))
        ImageDraw.Draw(tile).text((6, 4), name, fill=(255, 255, 255))
        sheet.paste(tile, (0, row * h))
    sheet.convert("RGB").save(os.path.join(OUT_DIR, "on_maps.png"))


def main(args):
    names = [a for a in args if not a.startswith("--")] or list(LOOKS)
    unknown = [n for n in names if n not in LOOKS]
    if unknown:
        sys.exit(f"unknown look(s): {', '.join(unknown)}; expected {', '.join(LOOKS)}")
    os.makedirs(ENEMIES_DIR, exist_ok=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    # Every strip and sheet draws on its own, so they go out to all the cores; the attack sheets take most of the time.
    jobs = [(n, kind) for n in names for kind in LOOKS[n]["attacks"]]
    with ProcessPoolExecutor() as pool:
        idle_jobs = {n: pool.submit(build, n) for n in names}
        attack_jobs = {job: pool.submit(build_attack, *job) for job in jobs}
        built = {n: job.result() for n, job in idle_jobs.items()}
        attacks = {n: {kind: attack_jobs[(n, kind)].result() for kind in LOOKS[n]["attacks"]} for n in names}
    contact_sheet(built)
    attack_preview(attacks)
    on_maps(built)
    print(f"previews in {OUT_DIR}")


if __name__ == "__main__":
    main(sys.argv[1:])
