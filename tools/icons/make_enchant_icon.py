"""Turn a generated round badge into an enchantment icon like the game's: a white silhouette on transparent, plus the
glow image the game shows over a Mastered enchantment.

usage: python -I make_enchant_icon.py <source image> <out icon.png> <out glow.png>

The badge is a white ring around a black disc with a soft glow on its inner edge and a white (plus a few pastel)
shape in the middle. Only that middle shape is kept, every colour becomes white (the game's own enchantment icons are
all white), the ring, disc and glow are dropped, and the result is cropped to the disc and scaled to 256 px.

The game draws an icon in a 190x190 box inside the button's pentagon opening. The shape is then scaled down (never up)
and moved until it fits that opening with MARGIN px to spare, so no part of it lies on the stone frame.

The glow PNG is the Mastered look, drawn like the game's own glow images (measured from them, no game pixels used): a
yellow pentagon outline that matches the opening, a translucent yellow fill that is stronger near the edge, and the
icon in white with a yellow rim and halo. The game shows it at 80 % over the white icon once level 6 is bought.
"""
import math
import sys

import numpy as np
from PIL import Image, ImageFilter

DARK = 70          # a pixel darker than this (average of RGB) belongs to the black disc
FLOOR = 90         # brightest channel at or below this: background or the disc's edge glow -> transparent
FULL = 200         # brightest channel at or above this: fully part of the shape
EDGE_FADE = (0.90, 0.97)  # fade the shape out between these fractions of the disc radius (drops the edge glow)

SIZE = 256
# The button's pentagon opening in the 256 px icon box, clockwise from top left (measured: the game's Mastered glow
# images outline exactly this opening).
PENTAGON = [(53.5, 10.5), (202.5, 10.5), (248.5, 151.0), (128.0, 245.5), (7.5, 151.0)]
MARGIN = 8            # px between the icon and the opening's edge
STRONG = 40           # alpha from which a pixel counts for the fit
AIM = (128.0, 120.0)  # preferred icon centre (the game's icons sit a little above the box centre)
SHRINK = 0.97         # a little below the largest fit, so the icon can move towards AIM


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


def half_planes(margin):
    """The opening as n . p <= b (one row per edge, outward unit normals), moved inwards by margin."""
    cx = sum(x for x, _ in PENTAGON) / len(PENTAGON)
    cy = sum(y for _, y in PENTAGON) / len(PENTAGON)
    rows = []
    for (x1, y1), (x2, y2) in zip(PENTAGON, PENTAGON[1:] + PENTAGON[:1]):
        nx, ny = y2 - y1, -(x2 - x1)
        length = math.hypot(nx, ny)
        nx, ny = nx / length, ny / length
        if nx * (cx - x1) + ny * (cy - y1) > 0:
            nx, ny = -nx, -ny
        rows.append((nx, ny, nx * x1 + ny * y1 - margin))
    return np.array(rows)


def closest_feasible(planes, limits, aim):
    """The point nearest to aim with n . c <= limit for every row, or None if there is none."""
    n = planes[:, :2]
    ok = lambda c: bool(np.all(n @ c <= limits + 1e-6))
    aim = np.array(aim)
    if ok(aim):
        return aim
    candidates = []
    for i in range(len(n)):
        candidates.append(aim - (n[i] @ aim - limits[i]) * n[i])  # projection onto edge i
        for j in range(i + 1, len(n)):
            m = np.array([n[i], n[j]])
            if abs(np.linalg.det(m)) > 1e-9:
                candidates.append(np.linalg.solve(m, np.array([limits[i], limits[j]])))
    candidates = [c for c in candidates if ok(c)]
    return min(candidates, key=lambda c: float(np.hypot(*(c - aim)))) if candidates else None


def fit(icon):
    """Scales (at most 1) and moves the icon so its shape lies inside the opening with MARGIN to spare."""
    alpha = np.asarray(icon.getchannel("A"))
    ys, xs = np.nonzero(alpha > STRONG)
    points = np.stack([xs + 0.5 - SIZE / 2, ys + 0.5 - SIZE / 2], axis=1)
    planes = half_planes(MARGIN)
    support = (points @ planes[:, :2].T).max(axis=0)  # how far the shape reaches towards each edge at scale 1
    limits = lambda s: planes[:, 2] - s * support
    if closest_feasible(planes, limits(1.0), AIM) is not None:
        scale = 1.0
    else:
        lo, hi = 0.2, 1.0
        for _ in range(40):
            mid = (lo + hi) / 2
            lo, hi = (mid, hi) if closest_feasible(planes, limits(mid), AIM) is not None else (lo, mid)
        scale = lo * SHRINK
    cx, cy = closest_feasible(planes, limits(scale), AIM)
    # destination = centre + scale * (source - 128); PIL wants the inverse map
    data = (1 / scale, 0, SIZE / 2 - cx / scale, 0, 1 / scale, SIZE / 2 - cy / scale)
    placed = icon.transform((SIZE, SIZE), Image.AFFINE, data, Image.BICUBIC, fillcolor=(255, 255, 255, 0))
    return placed, scale, (cx, cy)


def pentagon_distance(xx, yy):
    """Signed distance to the opening's outline: positive inside, negative outside."""
    best = np.full(xx.shape, np.inf)
    inside = np.ones(xx.shape, bool)
    for nx, ny, b in half_planes(0):
        inside &= nx * xx + ny * yy <= b
    for (x1, y1), (x2, y2) in zip(PENTAGON, PENTAGON[1:] + PENTAGON[:1]):
        dx, dy = x2 - x1, y2 - y1
        t = np.clip(((xx - x1) * dx + (yy - y1) * dy) / (dx * dx + dy * dy), 0, 1)
        best = np.minimum(best, np.hypot(xx - x1 - t * dx, yy - y1 - t * dy))
    return np.where(inside, best, -best)


def outside_distance(mask):
    """Approximate distance (px) from each pixel to the nearest pixel of mask (0 on the mask)."""
    far = 1e6
    d = np.where(mask, 0.0, far)
    steps = [(0, 1, 1.0), (1, 0, 1.0), (0, -1, 1.0), (-1, 0, 1.0),
             (1, 1, math.sqrt(2)), (1, -1, math.sqrt(2)), (-1, 1, math.sqrt(2)), (-1, -1, math.sqrt(2))]
    for _ in range(40):
        padded = np.pad(d, 1, constant_values=far)
        for dy, dx, cost in steps:
            d = np.minimum(d, padded[1 + dy:1 + dy + SIZE, 1 + dx:1 + dx + SIZE] + cost)
    return d


def over(dst, src):
    """Alpha-composites float RGBA arrays (0..1): src over dst."""
    sa, da = src[..., 3:4], dst[..., 3:4]
    out_a = sa + da * (1 - sa)
    rgb = (src[..., :3] * sa + dst[..., :3] * da * (1 - sa)) / np.maximum(out_a, 1e-6)
    return np.concatenate([rgb, out_a], axis=-1)


def layer(r, g, b, a):
    shape = (SIZE, SIZE)
    channels = [np.broadcast_to(np.asarray(v, float), shape) for v in (r / 255.0, g / 255.0, np.asarray(b) / 255.0)]
    return np.stack(channels + [np.clip(np.broadcast_to(np.asarray(a, float), shape), 0, 1)], axis=-1)


def glow(icon):
    """The Mastered glow: pentagon outline and fill, and the icon in white with a yellow rim and halo."""
    yy, xx = np.mgrid[0:SIZE, 0:SIZE] + 0.5
    d = pentagon_distance(xx, yy)
    deep = np.maximum(d, 4.0) - 4.0
    inner = (13 + 58 * np.exp(-deep / 8.5)) / 255           # fill, stronger along the edge
    outer = 15 * np.exp(np.minimum(d + 2, 0) / 2.2) / 255    # faint halo outside the outline
    base = layer(255, 251, np.where(d >= 0, 99 + 30 * np.exp(-deep / 8), 134), np.where(d >= 0, inner, outer))
    stroke = np.clip(np.minimum(d + 1.5, 3.6 - d), 0, 1)
    out = over(base, layer(255, 251, 99, stroke))

    alpha = np.asarray(icon.getchannel("A")) / 255.0
    dist = outside_distance(alpha >= 0.5)
    halo = np.where(dist >= 1, 57 / 255 * np.exp(-(dist - 1) / 4.0), 0)
    out = over(out, layer(255, 254, 25 + 74 * (1 - np.exp(-np.maximum(dist - 1, 0) / 5)), halo))
    solid = Image.fromarray((alpha * 255).astype(np.uint8))
    core = np.asarray(solid.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))) / 255.0
    out = over(out, layer(255, 255, 140 + 115 * core, alpha))  # yellow rim, white inside
    return Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA")


def main():
    src, out_icon, out_glow = sys.argv[1:4]
    shape = silhouette(Image.open(src)).resize((SIZE, SIZE), Image.LANCZOS)
    icon, scale, (cx, cy) = fit(shape)
    icon.save(out_icon)
    glow(icon).save(out_glow)
    print(f"{src} -> {out_icon}, {out_glow} (scale {scale:.2f}, centre {cx:.0f},{cy:.0f})")


main()
