#!/usr/bin/env python3
"""Own UI glyph sheet for openkrush_gen2 (mods/openkrush_gen2/uibits/glyphs.png), drawn here from simple shapes.
Regions follow chrome.yaml (^Glyphs). Released under CC0 like the other generated UI bits (see uibits/README.md). Needs Pillow."""
import os
from PIL import Image, ImageDraw
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
OUT = os.path.join(ROOT, 'mods/openkrush_gen2/uibits/glyphs.png')
im = Image.new('RGBA', (256, 256), (0, 0, 0, 0))
d = ImageDraw.Draw(im)
FG, DIM, ACC = (236, 226, 200, 255), (128, 124, 116, 255), (216, 115, 46, 255)

def box(x, y, w=16, h=16): return (x, y, x + w - 1, y + h - 1)
def tick(x, y, c): d.line([(x + 3, y + 8), (x + 6, y + 12), (x + 13, y + 3)], fill=c, width=2)
def cross(x, y, c): d.line([(x + 3, y + 3), (x + 12, y + 12)], fill=c, width=2); d.line([(x + 12, y + 3), (x + 3, y + 12)], fill=c, width=2)
def tri(x, y, c, dirn):
    pts = {'down': [(x + 3, y + 5), (x + 12, y + 5), (x + 7.5, y + 11)], 'up': [(x + 3, y + 11), (x + 12, y + 11), (x + 7.5, y + 5)],
           'right': [(x + 5, y + 3), (x + 5, y + 12), (x + 11, y + 7.5)], 'left': [(x + 11, y + 3), (x + 11, y + 12), (x + 5, y + 7.5)]}[dirn]
    d.polygon(pts, fill=c)

tick(187, 0, FG); tick(204, 0, ACC)
cross(221, 0, FG); cross(238, 0, ACC)
for i, (dirn) in enumerate(['down', 'up', 'right', 'left']):
    tri(68 + i * 34, 17, FG, dirn); tri(85 + i * 34, 17, DIM, dirn)
# music: pause stop play next prev fastforward
d.rectangle((3, 3, 6, 12), fill=FG); d.rectangle((9, 3, 12, 12), fill=FG)
d.rectangle((20, 3, 29, 12), fill=FG)
tri(34, 0, FG, 'right')
tri(49, 0, FG, 'right'); d.rectangle((62, 3, 63, 12), fill=FG)
d.rectangle((71, 3, 72, 12), fill=FG); tri(70, 0, FG, 'left')
tri(83, 0, FG, 'right'); tri(89, 0, FG, 'right')
# spawn markers + colour picker
for x, c in ((27, ACC), (50, FG), (73, DIM)):
    d.ellipse((x + 2, 121, x + 19, 138), outline=c, width=3)
# lobby bits
d.polygon([(170, 4), (173, 0), (175, 4)], fill=ACC)
d.rectangle((137, 0, 141, 14), outline=FG)
cross(151, -2, FG)
for x, c in ((0, FG), (17, DIM)):
    d.rectangle((x + 2, 23, x + 10, 29), fill=c); d.arc((x + 3, 17, x + 9, 25), 180, 360, fill=c, width=2)
for x, c in ((34, FG), (51, DIM)):
    d.ellipse((x + 3, 18, x + 9, 24), outline=c, width=2); d.line([(x + 6, 24), (x + 6, 29)], fill=c, width=2)
for x, c in ((0, ACC), (17, FG), (34, ACC), (51, DIM)):
    d.ellipse((x + 4, 52, x + 11, 59), fill=c); d.pieslice((x + 2, 58, x + 13, 70), 180, 360, fill=c)
# reload icon (enabled + 12 disabled phases)
for i in range(13):
    x = i * 17
    d.arc((x + 2, 36, x + 13, 47), 30 + i * 25, 330 + i * 25, fill=FG if i == 0 else DIM, width=2)
# mute toggle
for x, c in ((0, FG), (17, DIM)):
    d.polygon([(x + 2, 176), (x + 6, 176), (x + 10, 172), (x + 10, 183), (x + 6, 179), (x + 2, 179)], fill=c)
cross(17 + 4, 170 + 2, ACC) if False else d.line([(29, 174), (33, 181)], fill=ACC, width=2)
im.save(OUT)
print(OUT)
