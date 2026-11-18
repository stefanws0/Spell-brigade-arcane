"""Turn a generated round badge into an enchantment icon like the game's: a white silhouette on transparent.

usage: python -I make_enchant_icon.py <source image> <out icon.png> <out glow.png> [size]

The badge is a white ring around a black disc with a soft glow on its inner edge and a white (plus a few pastel)
shape in the middle. Only that middle shape is kept, every colour becomes white (the game's own enchantment icons are
all white), the ring, disc and glow are dropped, and the result is cropped to the disc and scaled to <size> px
(default 256). The glow PNG is a soft white halo of the same shape for the button's glow image.
"""
import math
import sys

from PIL import Image, ImageFilter

DARK = 70          # a pixel darker than this (average of RGB) belongs to the black disc
FLOOR = 90         # brightest channel at or below this: background or the disc's edge glow -> transparent
FULL = 200         # brightest channel at or above this: fully part of the shape
EDGE_FADE = (0.90, 0.97)  # fade the shape out between these fractions of the disc radius (drops the edge glow)


def find_disc(img):
    w, h = img.size
    px = img.load()
    dark = lambda p: (p[0] + p[1] + p[2]) / 3 < DARK
    cols = [sum(1 for y in range(0, h, 2) if dark(px[x, y])) for x in range(w)]
    rows = [sum(1 for x in range(0, w, 2) if dark(px[x, y])) for y in range(h)]
    left = min(i for i, c in enumerate(cols) if c >= 20)
    right = max(i for i, c in enumerate(cols) if c >= 20)
    top = min(i for i, c in enumerate(rows) if c >= 20)
    bottom = max(i for i, c in enumerate(rows) if c >= 20)
    return (left + right) / 2, (top + bottom) / 2, max(right - left, bottom - top) / 2


def silhouette(img):
    # Transparent pixels outside the ring would read as black (= disc): put the badge on white first.
    rgba = img.convert("RGBA")
    backing = Image.new("RGBA", rgba.size, (255, 255, 255, 255))
    img = Image.alpha_composite(backing, rgba).convert("RGB")
    cx, cy, r = find_disc(img)
    w, h = img.size
    px = img.load()
    out = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    o = out.load()
    for y in range(h):
        for x in range(w):
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy) / r
            if d >= EDGE_FADE[1]:
                continue
            bright = max(px[x, y])
            a = (bright - FLOOR) / (FULL - FLOOR)
            if a <= 0:
                continue
            if d > EDGE_FADE[0]:
                a *= (EDGE_FADE[1] - d) / (EDGE_FADE[1] - EDGE_FADE[0])
            o[x, y] = (255, 255, 255, int(255 * min(1.0, a)))
    half = r
    return out.crop((int(cx - half), int(cy - half), int(cx + half), int(cy + half)))


def glow(icon):
    alpha = icon.getchannel("A").filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(icon.width / 40))
    alpha = alpha.point(lambda a: int(a * 0.85))
    out = Image.new("RGBA", icon.size, (255, 255, 255, 0))
    out.putalpha(alpha)
    return out


def main():
    src, out_icon, out_glow = sys.argv[1:4]
    size = int(sys.argv[4]) if len(sys.argv) > 4 else 256
    icon = silhouette(Image.open(src)).resize((size, size), Image.LANCZOS)
    icon.save(out_icon)
    glow(icon).save(out_glow)
    print(f"{src} -> {out_icon}, {out_glow} ({size}px)")


main()
