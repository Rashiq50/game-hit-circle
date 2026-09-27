"""Builds the stage backgrounds into textures/stages/.

    python tools/stages/make_stages.py                 # every stage
    python tools/stages/make_stages.py forest_glade    # just the named ones
    python tools/stages/make_stages.py --tmx ...       # also regenerate existing .tmx files (overwrites Tiled edits!)

Each stage module exposes build() -> Stage (see common.py). The .tmx next to each PNG gets the generated walls, pits and
spawn points, but only when it doesn't exist yet (or with --tmx), so collision edited in Tiled is never overwritten by
accident. Half-size previews, collision overlays and a contact sheet of every stage go to tools/stages/out/.
"""
import importlib
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw  # noqa: E402
from common import OUT_DIR, Stage, save_stage  # noqa: E402

# Run order = stage order: small outdoor maps first, the castle as the finale.
STAGES = ["forest_glade", "desert_ruins", "swamp_bog", "frozen_cavern", "graveyard", "volcanic_caldera", "castle_floor"]


def contact_sheet():
    thumbs = []
    for name in STAGES:
        p = os.path.join(OUT_DIR, name + "_preview.png")
        if os.path.exists(p):
            im = Image.open(p)
            scale = 480 / 2000  # every map at the same world scale, so the size progression shows
            thumbs.append((name, im.resize((max(1, int(im.width * scale)), max(1, int(im.height * scale))), Image.LANCZOS)))
    if not thumbs:
        return
    pad, label = 16, 20
    cols = 4
    rows = [thumbs[i:i + cols] for i in range(0, len(thumbs), cols)]
    w = max(sum(t.width for _, t in r) + pad * (len(r) + 1) for r in rows)
    h = sum(max(t.height for _, t in r) + label + pad for r in rows) + pad
    sheet = Image.new("RGB", (w, h), (30, 30, 40))
    d = ImageDraw.Draw(sheet)
    y = pad
    for r in rows:
        x = pad
        for i, (name, t) in enumerate(r):
            d.text((x, y), f"{STAGES.index(name) + 1}. {name}", fill=(230, 230, 240))
            sheet.paste(t, (x, y + label))
            x += t.width + pad
        y += max(t.height for _, t in r) + label + pad
    sheet.save(os.path.join(OUT_DIR, "contact_sheet.png"))


def main(args):
    overwrite = "--tmx" in args
    names = [a for a in args if not a.startswith("--")]
    for name in names or STAGES:
        mod = importlib.import_module(name)
        stage = mod.build()
        save_stage(stage, getattr(mod, "WRITES_TMX", True), overwrite)
        if isinstance(stage, Stage):
            stuck = stage.check_reachable()
            if stuck:
                print(f"  WARNING: {len(stuck)} open tiles a 2x2 body can't reach from the spawn, e.g. {stuck[:6]}")
    contact_sheet()


if __name__ == "__main__":
    main(sys.argv[1:])
