"""Builds the enemy sprite strips into textures/enemies/, in the same flat, dark-outlined style as the stage maps.

    python tools/enemies/make_enemies.py              # every look
    python tools/enemies/make_enemies.py imp wisp     # just the named ones

Every frame is a square CANVAS world units across with the body centre in the middle, drawn at SS px per unit and
downscaled to OUT px per unit (2x the game's world scale, so zoom and the ultimate's hover stay crisp). Each look gets
{look}_idle.png (IDLE_FRAMES looping frames) and {look}_death.png (DEATH_FRAMES, played once). All art faces right; the
game mirrors it to face the player. Previews go to tools/enemies/out/: a contact sheet with the hit boxes drawn on, and
every look pasted at game scale onto each stage.

LOOKS mirrors EnemyIcons.BodyScale / HitExtent / TopExtent / IdleFps in EnemyIcons.cs; change both together.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "stages"))
from PIL import Image, ImageChops, ImageDraw, ImageFilter  # noqa: E402
from common import REPO, STAGES_DIR, mix  # noqa: E402

CANVAS = 128               # frame size in world units; EnemyIcons.Canvas
BASE_RADIUS = 30           # Enemy.IconRadius
SS = 8                     # supersampled px per world unit while drawing
OUT = 2                    # px per world unit in the shipped strips
IDLE_FRAMES = 8
DEATH_FRAMES = 6
LINE = 2.75                # outline weight in world units, close to the props' 3 px

ENEMIES_DIR = os.path.join(REPO, "textures", "enemies")
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
TAU = math.tau
WHITE = (255, 255, 255)

# scale: body radius = BASE_RADIUS * scale. hit: solid half-size / radius. top: drawing's reach above the centre / radius.
LOOKS = {
    "imp": dict(scale=0.75, hit=(0.7, 0.95), top=1.4),
    "brute": dict(scale=1.25, hit=(0.95, 0.9), top=1.2),
    "tank": dict(scale=1.3, hit=(0.98, 0.93), top=0.95),
    "sniper": dict(scale=1.0, hit=(0.55, 1.2), top=1.65),
    "warlord": dict(scale=1.5, hit=(0.92, 0.92), top=1.2),
    "wisp": dict(scale=0.8, hit=(0.6, 0.65), top=1.35),
}


def u(v): return int(round(v * SS))


def wave(ph, k=1, shift=0.0): return math.sin(TAU * (ph * k + shift))


# ---- Shapes (world units); grow > 0 draws the shape fattened by that much, which is how outlines are made ------------


class E:
    """Ellipse."""
    def __init__(self, cx, cy, rx, ry=None):
        self.cx, self.cy, self.rx, self.ry = cx, cy, rx, rx if ry is None else ry

    def draw(self, d, fill, grow=0):
        d.ellipse([u(self.cx - self.rx - grow), u(self.cy - self.ry - grow),
                   u(self.cx + self.rx + grow), u(self.cy + self.ry + grow)], fill=fill)


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


class R:
    """Rounded rectangle."""
    def __init__(self, x0, y0, x1, y1, radius=0):
        self.box, self.rad = (x0, y0, x1, y1), radius

    def draw(self, d, fill, grow=0):
        x0, y0, x1, y1 = self.box
        d.rounded_rectangle([u(x0 - grow), u(y0 - grow), u(x1 + grow), u(y1 + grow)], radius=u(self.rad + grow), fill=fill)


class Art:
    """One supersampled frame. Parts are painted back to front, each with its own outline, like the map props."""

    def __init__(self):
        self.size = CANVAS * SS
        self.img = Image.new("RGBA", (self.size, self.size), (0, 0, 0, 0))
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


# ---- The creatures (cx, cy = body centre, r = body radius, ph = loop phase 0..1) --------------------------------------


def draw_imp(a, cx, cy, r, ph):
    body, shade, light, edge = (232, 104, 44), (182, 62, 30), (255, 170, 98), (60, 20, 10)
    wing, wing_s = (168, 46, 44), (122, 30, 36)
    horn, horn_s = (246, 226, 184), (196, 170, 128)
    cy += wave(ph, 2) * 1.2           # twitchy hop
    flap = wave(ph)
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
    # arms: one hangs, the other holds up an ember
    a.part([L([(cx - 0.42 * r, cy + 0.05 * r), (cx - 0.7 * r, cy + 0.3 * r)], 0.2 * r)], body, edge)
    a.part([L([(cx + 0.42 * r, cy + 0.05 * r), (cx + 0.78 * r, cy - 0.08 * r), (cx + 0.88 * r, cy - 0.3 * r)], 0.2 * r)], body, edge)
    ex, ey = cx + 0.92 * r, cy - 0.55 * r
    flick = 1 + 0.12 * wave(ph, 3)
    a.glow(ex, ey, 0.75 * r, (255, 150, 60), 0.55)
    a.part([P([(ex - 0.2 * r * flick, ey + 0.05 * r), (ex - 0.02 * r, ey - 0.42 * r * flick), (ex + 0.2 * r * flick, ey + 0.05 * r),
               (ex, ey + 0.2 * r)]), E(ex, ey + 0.02 * r, 0.19 * r)], (255, 150, 60), (120, 40, 10), w=1.6)
    a.fill([E(ex, ey + 0.04 * r, 0.1 * r * flick, 0.12 * r)], (255, 234, 150))


def draw_brute(a, cx, cy, r, ph):
    hide, shade, light, edge = (150, 70, 58), (106, 44, 40), (192, 112, 90), (42, 16, 18)
    leather, gold = (112, 74, 46), (214, 170, 70)
    wood, wood_s = (138, 94, 56), (98, 64, 38)
    metal, tusk = (196, 200, 210), (244, 232, 200)
    breathe = wave(ph)
    lift = max(0.0, breathe) * 0.14 * r
    k = 1 + 0.03 * breathe
    for side in (-1, 1):
        a.part([E(cx + side * 0.38 * r, cy + 0.72 * r, 0.26 * r, 0.19 * r)], shade, edge)
    # spiked club, held up in the right fist
    hx, hy = cx + 0.88 * r, cy + 0.16 * r - lift
    top = (cx + 1.1 * r, cy - 0.8 * r - lift)
    for ang in (-2.3, -1.5, -0.7, 0.1):
        sx, sy = top[0] + math.cos(ang) * 0.26 * r, top[1] + math.sin(ang) * 0.24 * r
        a.part([P([(sx + math.cos(ang + 1.6) * 0.07 * r, sy + math.sin(ang + 1.6) * 0.07 * r),
                   (sx + math.cos(ang) * 0.17 * r, sy + math.sin(ang) * 0.17 * r),
                   (sx + math.cos(ang - 1.6) * 0.07 * r, sy + math.sin(ang - 1.6) * 0.07 * r)])], metal, edge, w=1.4)
    m = a.part([P([(hx - 0.08 * r, hy + 0.1 * r), (hx + 0.08 * r, hy + 0.1 * r), (top[0] + 0.14 * r, top[1] + 0.1 * r),
                   (top[0] - 0.16 * r, top[1] + 0.1 * r)]), E(top[0], top[1], 0.26 * r, 0.24 * r)],
               wood, edge, shade=wood_s, off=0.08 * r)
    a.clip(m, [L([(top[0] - 0.1 * r, top[1] - 0.08 * r), (top[0] + 0.02 * r, top[1] - 0.15 * r)], 1.6)], mix(wood, WHITE, 0.3))
    a.clip(m, [L([(hx - 0.01 * r, hy - 0.2 * r), (top[0] - 0.05 * r, top[1] + 0.3 * r)], 1.2)], wood_s)
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
    # fists
    a.part([L([(cx - 0.72 * r, cy - 0.12 * r), (cx - 0.9 * r, cy + 0.3 * r)], 0.3 * r)], hide, edge, shade=shade, off=0.06 * r)
    for fx, fy in ((cx - 0.92 * r, cy + 0.38 * r), (hx, hy)):
        m = a.part([E(fx, fy, 0.24 * r)], hide, edge, shade=shade, off=0.07 * r)
        a.clip(m, [L([(fx - 0.14 * r, fy - 0.05 * r), (fx + 0.14 * r, fy - 0.05 * r)], 1.2)], shade)


def draw_tank(a, cx, cy, r, ph):
    stone, stone_s, stone_l = (128, 126, 118), (90, 88, 86), (166, 164, 154)
    moss, moss_l = (96, 138, 72), (132, 170, 92)
    steel, steel_s, steel_l, edge = (170, 176, 190), (116, 122, 140), (216, 222, 234), (28, 32, 44)
    bronze, bronze_s = (196, 150, 72), (148, 104, 44)
    pulse = 0.5 + 0.5 * wave(ph)
    sway = wave(ph) * 0.9
    for side in (-1, 1):
        a.part([R(cx + side * 0.42 * r - 0.17 * r, cy + 0.5 * r, cx + side * 0.42 * r + 0.17 * r, cy + 0.93 * r, 0.08 * r)],
               stone, edge, shade=stone_s, off=0.06 * r)
    ux = cx + sway
    # stone fist on the right
    a.part([L([(ux + 0.62 * r, cy - 0.1 * r), (ux + 0.86 * r, cy + 0.3 * r)], 0.3 * r)], stone, edge, shade=stone_s, off=0.06 * r)
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


def draw_sniper(a, cx, cy, r, ph):
    robe, robe_s, robe_l, edge = (108, 60, 170), (72, 38, 124), (150, 106, 214), (30, 14, 52)
    trim, hood_in, skin = (222, 178, 78), (20, 10, 34), (196, 176, 208)
    wood, crystal = (96, 66, 42), (170, 236, 255)
    eye = (246, 150, 255)
    pulse = 0.5 + 0.5 * wave(ph)
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
    # staff with a crystal lens
    sx0, sy0, sx1, sy1 = cx + 0.6 * r, cy + 1.22 * r, cx + 0.74 * r, cy - 1.18 * r
    a.part([L([(sx0, sy0), (sx1, sy1)], 3.0)], wood, edge, w=1.6)
    kx, ky = sx1 + 0.01 * r, sy1 - 0.24 * r
    a.glow(kx, ky, 0.8 * r, (150, 220, 255), 0.25 + 0.4 * pulse)
    for side in (-1, 1):
        a.part([L([(sx1, sy1 + 0.02 * r), (kx + side * 0.16 * r, ky + 0.02 * r), (kx + side * 0.1 * r, ky - 0.24 * r)], 2.0)], trim, edge, w=1.2)
    km = a.part([P([(kx, ky - 0.28 * r), (kx + 0.13 * r, ky), (kx, ky + 0.2 * r), (kx - 0.13 * r, ky)])], crystal, edge, w=1.6,
                shade=(100, 180, 230), off=0.04 * r)
    a.clip(km, [P([(kx, ky - 0.24 * r), (kx + 0.05 * r, ky - 0.04 * r), (kx, ky), (kx - 0.07 * r, ky - 0.02 * r)])],
           mix(crystal, WHITE, 0.4 + 0.5 * pulse))
    # sleeve and hand gripping the staff
    a.part([P([(cx + 0.18 * r, cy - 0.4 * r), (cx + 0.46 * r, cy - 0.3 * r), (cx + 0.7 * r, cy + 0.05 * r),
               (cx + 0.54 * r, cy + 0.22 * r), (cx + 0.2 * r, cy + 0.0 * r)])], robe, edge, shade=robe_s, off=0.06 * r)
    a.part([E(sx0 + (sx1 - sx0) * 0.46, sy0 + (sy1 - sy0) * 0.46, 0.1 * r)], skin, edge, w=1.4)
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


def draw_warlord(a, cx, cy, r, ph):
    arm, arm_s, arm_l, edge = (150, 24, 36), (102, 14, 26), (206, 66, 68), (36, 6, 12)
    gold, gold_s = (255, 200, 60), (196, 136, 34)
    cape, cape_s = (62, 22, 46), (40, 14, 30)
    horn, horn_s = (232, 216, 184), (176, 156, 126)
    steel, steel_s = (206, 212, 226), (140, 146, 166)
    eye = (255, 220, 110)
    pulse = 0.5 + 0.5 * wave(ph)
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
    gx, gy = cx + 0.76 * r, cy + 0.12 * r               # crossguard centre
    tx, ty = cx + 1.25 * r, cy - 1.05 * r
    dx, dy = tx - gx, ty - gy
    n = math.hypot(dx, dy)
    nx, ny = -dy / n, dx / n
    bw = 0.08 * r
    bm = a.part([P([(gx + nx * bw, gy + ny * bw), (tx - dx / n * 0.2 * r + nx * bw, ty - dy / n * 0.2 * r + ny * bw), (tx, ty),
                    (tx - dx / n * 0.2 * r - nx * bw, ty - dy / n * 0.2 * r - ny * bw), (gx - nx * bw, gy - ny * bw)])],
                steel, edge, w=1.8, shade=steel_s, off=0.03 * r)
    a.clip(bm, [L([(gx, gy), (tx - dx / n * 0.25 * r, ty - dy / n * 0.25 * r)], 1.2)], steel_s)
    a.part([L([(gx - dx / n * 0.26 * r, gy - dy / n * 0.26 * r), (gx, gy)], 0.09 * r)], (70, 36, 30), edge, w=1.6)
    a.part([L([(gx + nx * 0.22 * r, gy + ny * 0.22 * r), (gx - nx * 0.22 * r, gy - ny * 0.22 * r)], 0.09 * r)], gold, edge, w=1.6)
    a.part([E(gx - dx / n * 0.3 * r, gy - dy / n * 0.3 * r, 0.06 * r)], gold, edge, w=1.4)
    # gauntlets
    for fx, fy in ((gx - dx / n * 0.13 * r, gy - dy / n * 0.13 * r), (cx - 0.8 * r, cy + 0.3 * r)):
        fm = a.part([E(fx, fy, 0.14 * r)], arm, edge, shade=arm_s, off=0.04 * r)
        a.clip(fm, [L([(fx - 0.09 * r, fy - 0.05 * r), (fx + 0.09 * r, fy - 0.05 * r)], 1.4)], gold)


def draw_wisp(a, cx, cy, r, ph):
    glow, core, edge = (80, 220, 255), (170, 240, 255), (16, 62, 96)
    flame, flame_in = (90, 206, 250), (196, 246, 255)
    cy += wave(ph) * 2.5
    flick = wave(ph, 2) * 0.12 * r
    a.glow(cx, cy, 1.5 * r, glow, 0.5 + 0.1 * wave(ph, 2))
    motes = [(TAU * ph + i * TAU / 3) for i in range(3)]

    def mote(ang):
        mx, my = cx + math.cos(ang) * 1.0 * r, cy + math.sin(ang) * 0.45 * r
        a.glow(mx, my, 0.35 * r, glow, 0.6)
        a.part([E(mx, my, 0.12 * r)], (228, 255, 255), edge, w=1.2)

    for ang in motes:
        if math.sin(ang) < 0:
            mote(ang)
    # flame licking upward
    tip = (cx - 0.1 * r + flick, cy - 1.25 * r)
    a.part([P([(cx - 0.5 * r, cy - 0.05 * r), (cx - 0.5 * r + flick * 0.5, cy - 0.55 * r), (cx - 0.28 * r, cy - 0.72 * r),
               tip, (cx + 0.1 * r + flick * 0.6, cy - 0.8 * r), (cx + 0.34 * r - flick * 0.4, cy - 0.95 * r),
               (cx + 0.5 * r, cy - 0.45 * r), (cx + 0.5 * r, cy - 0.05 * r)])], flame, edge, w=2.2)
    a.fill([P([(cx - 0.28 * r, cy - 0.2 * r), (cx - 0.2 * r + flick * 0.4, cy - 0.7 * r), (cx - 0.02 * r + flick * 0.8, cy - 0.98 * r),
               (cx + 0.12 * r, cy - 0.62 * r), (cx + 0.28 * r, cy - 0.2 * r)])], flame_in)
    m = a.part([E(cx, cy, 0.55 * r)], core, edge, w=2.4, shade=(110, 206, 240), off=0.1 * r)
    a.clip(m, [E(cx - 0.22 * r, cy - 0.22 * r, 0.14 * r, 0.09 * r)], WHITE)
    for ex in (cx - 0.06 * r, cx + 0.2 * r):
        a.fill([E(ex, cy + 0.02 * r, 0.055 * r, 0.1 * r)], edge)
    a.fill([E(cx + 0.08 * r, cy + 0.24 * r, 0.05 * r, 0.04 * r)], edge)
    for ang in motes:
        if math.sin(ang) >= 0:
            mote(ang)


DRAW = {"imp": draw_imp, "brute": draw_brute, "tank": draw_tank, "sniper": draw_sniper, "warlord": draw_warlord, "wisp": draw_wisp}
# puff colours for the death burst: fill, outline
PUFF = {
    "imp": ((255, 150, 60), (60, 20, 10)), "brute": ((150, 70, 58), (42, 16, 18)), "tank": ((170, 176, 190), (28, 32, 44)),
    "sniper": ((150, 106, 214), (30, 14, 52)), "warlord": ((255, 200, 60), (36, 6, 12)), "wisp": ((170, 240, 255), (16, 62, 96)),
}


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


# ---- Previews --------------------------------------------------------------------------------------------------------


def frames_1x(strip_img):
    n = CANVAS * OUT
    return [strip_img.crop((i * n, 0, (i + 1) * n, n)).resize((CANVAS, CANVAS), Image.LANCZOS) for i in range(strip_img.width // n)]


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
    built = {n: build(n) for n in names}
    contact_sheet(built)
    on_maps(built)
    print(f"previews in {OUT_DIR}")


if __name__ == "__main__":
    main(sys.argv[1:])
