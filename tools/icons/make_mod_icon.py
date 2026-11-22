"""Thunderstore icon: the badge art turned into a 256x256 PNG, transparent outside the badge.

usage: python -I make_mod_icon.py <source image> <out icon.png>

The source is a round badge (white ring, black disc, white hat with a coloured sparkle) on a black background.
The script finds the ring along the middle row and column, lifts the art off the black disc, and rebuilds the badge:
a black disc with a soft grey glow along the bottom rim, the art a little smaller, the ring, nothing outside it.
"""
import sys

import numpy as np
from PIL import Image

SIZE = 256
ART_SCALE = 0.84  # the hat's size against the source
GLOW = 165  # brightness of the glow where it meets the ring at the very bottom
GLOW_REACH = 0.17  # how far the glow fades into the disc, as a part of the disc's radius


def ring_edges(values):
    """Outer and inner edge of the ring on both sides of one line through the centre."""
    white = values > 128
    idx = np.flatnonzero(white)
    first, last = idx[0], idx[-1]
    first_end = first + np.argmin(white[first:])  # first dark pixel after the ring
    last_start = last - np.argmin(white[last::-1])  # last dark pixel before the ring
    return first, first_end, last_start + 1, last + 1


def smoothstep(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3 - 2 * x)


def main():
    src, out = sys.argv[1:3]
    img = Image.open(src).convert("RGB")
    w, h = img.size
    grey = np.asarray(img.convert("L"))
    l0, l1, r0, r1 = ring_edges(grey[h // 2])
    t0, t1, b0, b1 = ring_edges(grey[:, w // 2])
    cx, cy = (l0 + r1) / 2, (t0 + b1) / 2
    r_out = (r1 - l0 + b1 - t0) / 4
    r_in = (r0 - l1 + b0 - t1) / 4
    print(f"{src} {img.size}: centre ({cx:.0f}, {cy:.0f}), ring {r_in:.0f}..{r_out:.0f}")

    # The art: everything well inside the ring. On black, the brightest channel is the coverage.
    rgb = np.asarray(img).astype(np.float32)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    dist = np.hypot(xx + 0.5 - cx, yy + 0.5 - cy)
    cover = rgb.max(axis=2) * (dist < r_in * 0.9)
    colour = np.where(cover[..., None] > 0, rgb / np.maximum(cover[..., None], 1) * 255, 0)
    art = Image.fromarray(np.dstack([colour, cover]).clip(0, 255).astype(np.uint8), "RGBA")
    small = art.resize((round(w * ART_SCALE), round(h * ART_SCALE)), Image.LANCZOS)
    art = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    art.paste(small, (round(cx - cx * ART_SCALE), round(cy - cy * ART_SCALE)))

    # The badge behind it: black disc, glow along the bottom rim, white ring.
    depth = (r_in - dist) / (r_in * GLOW_REACH)  # 0 at the ring, growing inwards
    low = smoothstep(((yy - cy) / r_in + 0.15) / 0.9)  # 0 above the middle, 1 at the bottom
    shade = GLOW * np.exp(-np.maximum(depth, 0)) * low
    ring = np.clip(dist - r_in + 0.5, 0, 1)
    shade = shade * (1 - ring) + 255 * ring
    alpha = np.clip(r_out - dist + 0.5, 0, 1) * 255
    badge = Image.fromarray(np.dstack([shade, shade, shade, alpha]).clip(0, 255).astype(np.uint8), "RGBA")
    badge.alpha_composite(art)

    half = r_out + 2
    box = (round(cx - half), round(cy - half), round(cx + half), round(cy + half))
    badge.crop(box).resize((SIZE, SIZE), Image.LANCZOS).save(out)
    print(f"wrote {out} ({SIZE}x{SIZE})")


main()
