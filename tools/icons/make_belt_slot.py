"""Turn the two generated Potion Belt images (on a flat green background) into the health-bar slot sprites:
the stone disc with the empty bottle recess, and the potion bottle scaled and placed to sit exactly in that recess.

usage: python -I make_belt_slot.py <slot source> <bottle source> <out slot.png> <out bottle.png>
"""
import sys

import numpy as np
from PIL import Image, ImageFilter

SIZE = 256


def cut_out(img):
    """Green background -> transparent, with a soft edge and the green spill removed."""
    a = np.asarray(img.convert("RGB")).astype(np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    green = g - np.maximum(r, b)
    alpha = 1.0 - np.clip((green - 18.0) / 50.0, 0.0, 1.0)
    g = np.minimum(g, np.maximum(r, b) + 8.0)
    out = np.dstack([r, g, b, alpha * 255.0]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def bbox_of(mask):
    ys, xs = np.nonzero(mask)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def recess_box(slot):
    """The bottle-shaped recess: the big very dark area in the middle (thin dark cracks are eroded away)."""
    a = np.asarray(slot).astype(np.float32)
    luma = a[..., :3].mean(axis=2)
    dark = ((luma < 52) & (a[..., 3] > 200)).astype(np.uint8) * 255
    core = Image.fromarray(dark).filter(ImageFilter.MinFilter(25))
    x0, y0, x1, y1 = bbox_of(np.asarray(core) > 0)
    return x0 - 12, y0 - 12, x1 + 12, y1 + 12


def main():
    slot_src, bottle_src, out_slot, out_bottle = sys.argv[1:5]
    slot = cut_out(Image.open(slot_src))
    bottle = cut_out(Image.open(bottle_src))

    sx0, sy0, sx1, sy1 = bbox_of(np.asarray(slot)[..., 3] > 40)
    side = max(sx1 - sx0, sy1 - sy0)
    cx, cy = (sx0 + sx1) / 2, (sy0 + sy1) / 2
    crop = (int(cx - side / 2), int(cy - side / 2), int(cx + side / 2), int(cy + side / 2))
    slot_sq = slot.crop(crop)
    rx0, ry0, rx1, ry1 = recess_box(slot_sq)

    bx0, by0, bx1, by1 = bbox_of(np.asarray(bottle)[..., 3] > 40)
    bottle_crop = bottle.crop((bx0, by0, bx1, by1))
    # Same height as the recess; the width follows the bottle's own shape, centred on the recess.
    scale = (ry1 - ry0) / bottle_crop.height
    placed = bottle_crop.resize((round(bottle_crop.width * scale), round(bottle_crop.height * scale)), Image.LANCZOS)
    canvas = Image.new("RGBA", slot_sq.size, (0, 0, 0, 0))
    canvas.alpha_composite(placed, (round((rx0 + rx1) / 2 - placed.width / 2), ry0))

    slot_sq.resize((SIZE, SIZE), Image.LANCZOS).save(out_slot)
    canvas.resize((SIZE, SIZE), Image.LANCZOS).save(out_bottle)
    print(f"slot {slot_sq.size}, recess {rx0},{ry0}..{rx1},{ry1}, bottle scale {scale:.2f} -> {out_slot}, {out_bottle}")


main()
