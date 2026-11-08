#!/usr/bin/env python3
"""KKND2 Thor launcher icon (own art, no game artwork): near-black tile, rust crosshair, "KK2" over "THOR" in Inter Bold (OFL).
Writes android/Resources/mipmap-*/ic_launcher.png. Needs Pillow."""
import os
from PIL import Image, ImageDraw, ImageFont
ROOT = os.path.dirname(os.path.abspath(__file__))
FONT = os.path.join(ROOT, 'android/Assets/fonts/Inter-Bold.ttf')
S = 512
img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
d.rounded_rectangle((16, 16, S - 16, S - 16), 96, fill=(8, 9, 11, 255), outline=(50, 56, 68, 255), width=4)
rust, rust2 = (216, 115, 46, 255), (240, 170, 110, 255)
d.ellipse((76, 66, S - 76, S - 116), outline=(216, 115, 46, 140), width=10)
for x0, y0, x1, y1 in ((S / 2, 40, S / 2, 110), (S / 2, 336, S / 2, 400), (50, 233, 120, 233), (S - 120, 233, S - 50, 233)):
    d.line((x0, y0, x1, y1), fill=(216, 115, 46, 200), width=10)
d.text((S / 2, 232), 'KK2', font=ImageFont.truetype(FONT, 150), anchor='mm', fill=rust)
d.text((S / 2, 440), 'THOR', font=ImageFont.truetype(FONT, 74), anchor='mm', fill=rust2)
for name, px in (('mdpi', 48), ('hdpi', 72), ('xhdpi', 96), ('xxhdpi', 144), ('xxxhdpi', 192)):
    os.makedirs(os.path.join(ROOT, f'android/Resources/mipmap-{name}'), exist_ok=True)
    img.resize((px, px), Image.LANCZOS).save(os.path.join(ROOT, f'android/Resources/mipmap-{name}/ic_launcher.png'))
img.save(os.path.join(ROOT, 'icon-512.png'))
print('ok')
