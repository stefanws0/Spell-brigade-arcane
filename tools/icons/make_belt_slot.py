"""Draw the Potion Belt slot for the health bar: a dark disc with a bottle-shaped recess (empty), and the potion
bottle that sits in it (full). Both 256 px, drawn at 4x and scaled down.

usage: python -I make_belt_slot.py <out empty.png> <out bottle.png>
"""
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

S = 4
N = 256 * S


def bottle_mask(grow=0):
    """The flask silhouette: cork, neck, round body."""
    m = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(m)
    g = grow * S
    cx = N // 2
    body_r = 62 * S + g
    body_cy = 150 * S
    d.ellipse([cx - body_r, body_cy - body_r, cx + body_r, body_cy + body_r], fill=255)
    neck_w = 22 * S + g
    d.rectangle([cx - neck_w, 62 * S - g, cx + neck_w, 110 * S], fill=255)
    lip_w = 30 * S + g
    d.rounded_rectangle([cx - lip_w, 54 * S - g, cx + lip_w, 70 * S], radius=6 * S, fill=255)
    return m


def shift(img, dx, dy):
    return ImageChops.offset(img, dx * S, dy * S)


def empty_slot():
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r = 124 * S
    c = N // 2
    d.ellipse([c - r, c - r, c + r, c + r], fill=(20, 21, 25, 255))                      # dark edge
    d.ellipse([c - r + 5 * S, c - r + 5 * S, c + r - 5 * S, c + r - 5 * S], fill=(96, 99, 110, 255))   # rim
    d.ellipse([c - r + 11 * S, c - r + 11 * S, c + r - 11 * S, c + r - 11 * S], fill=(52, 54, 62, 255))  # face

    # a soft light from the top left on the face
    light = Image.new("L", (N, N), 0)
    ImageDraw.Draw(light).ellipse([c - r, c - r, c + r // 3, c + r // 3], fill=70)
    light = light.filter(ImageFilter.GaussianBlur(40 * S))
    face = Image.new("L", (N, N), 0)
    ImageDraw.Draw(face).ellipse([c - r + 11 * S, c - r + 11 * S, c + r - 11 * S, c + r - 11 * S], fill=255)
    img.paste(Image.new("RGBA", (N, N), (255, 255, 255, 255)), mask=ImageChops.multiply(light, face))

    m = bottle_mask()
    img.paste(Image.new("RGBA", (N, N), (24, 25, 30, 255)), mask=m)                      # the recess floor
    inner = ImageChops.subtract(m, shift(m, 5, 6)).filter(ImageFilter.GaussianBlur(2 * S))
    img.paste(Image.new("RGBA", (N, N), (6, 6, 9, 255)), mask=ImageChops.multiply(inner, m))   # shadow under the top-left edge
    rim = ImageChops.subtract(shift(m, 3, 4), m).filter(ImageFilter.GaussianBlur(1 * S))
    rim = ImageChops.multiply(rim, face).point(lambda v: int(v * 0.55))
    img.paste(Image.new("RGBA", (N, N), (150, 154, 168, 255)), mask=rim)                 # lit bottom-right edge
    return img.resize((256, 256), Image.LANCZOS)


def bottle():
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = N // 2
    outline = bottle_mask()
    glass = bottle_mask(-5)
    img.paste(Image.new("RGBA", (N, N), (30, 16, 22, 255)), mask=outline)                # dark outline
    img.paste(Image.new("RGBA", (N, N), (196, 214, 232, 255)), mask=glass)               # glass

    liquid = Image.new("L", (N, N), 0)
    ImageDraw.Draw(liquid).rectangle([0, 122 * S, N, N], fill=255)
    liquid = ImageChops.multiply(liquid, glass)
    img.paste(Image.new("RGBA", (N, N), (214, 42, 58, 255)), mask=liquid)                # potion
    deep = Image.new("L", (N, N), 0)
    ImageDraw.Draw(deep).ellipse([c - 20 * S, 150 * S, c + 90 * S, 250 * S], fill=255)
    deep = ImageChops.multiply(deep.filter(ImageFilter.GaussianBlur(14 * S)), liquid)
    img.paste(Image.new("RGBA", (N, N), (140, 16, 40, 255)), mask=deep)                  # darker lower right
    surface = Image.new("L", (N, N), 0)
    ImageDraw.Draw(surface).ellipse([c - 56 * S, 114 * S, c + 56 * S, 130 * S], fill=255)
    img.paste(Image.new("RGBA", (N, N), (242, 96, 104, 255)), mask=ImageChops.multiply(surface, glass))  # liquid surface

    shine = Image.new("L", (N, N), 0)
    sd = ImageDraw.Draw(shine)
    sd.ellipse([c - 46 * S, 128 * S, c - 22 * S, 172 * S], fill=230)
    sd.ellipse([c - 12 * S, 78 * S, c - 4 * S, 104 * S], fill=200)
    shine = ImageChops.multiply(shine.filter(ImageFilter.GaussianBlur(3 * S)), glass)
    img.paste(Image.new("RGBA", (N, N), (255, 255, 255, 255)), mask=shine)               # highlights

    d.rounded_rectangle([c - 20 * S, 34 * S, c + 20 * S, 60 * S], radius=5 * S, fill=(30, 16, 22, 255))   # cork outline
    d.rounded_rectangle([c - 15 * S, 39 * S, c + 15 * S, 58 * S], radius=3 * S, fill=(176, 122, 70, 255))  # cork
    d.rectangle([c - 15 * S, 39 * S, c + 15 * S, 45 * S], fill=(208, 158, 100, 255))
    return img.resize((256, 256), Image.LANCZOS)


def main():
    empty_slot().save(sys.argv[1])
    bottle().save(sys.argv[2])
    print("wrote", sys.argv[1], sys.argv[2])


main()
