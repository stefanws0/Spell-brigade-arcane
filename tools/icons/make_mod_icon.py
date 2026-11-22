"""Thunderstore icon: the mod icon scaled to exactly 256x256 PNG.

usage: python -I make_mod_icon.py <source image> <out icon.png>
"""
import sys

from PIL import Image


def main():
    src, out = sys.argv[1:3]
    img = Image.open(src).convert("RGBA")
    side = min(img.size)
    left = (img.width - side) // 2
    top = (img.height - side) // 2
    img.crop((left, top, left + side, top + side)).resize((256, 256), Image.LANCZOS).save(out)
    print(f"{src} -> {out} (256x256)")


main()
