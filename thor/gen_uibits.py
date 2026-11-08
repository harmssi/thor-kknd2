#!/usr/bin/env python3
"""Erzeugt die eigenen (nicht aus dem Spiel extrahierten) UI-Hilfsgrafiken fuer openkrush_gen2."""
import zlib, struct, math, os, random
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'mods', 'openkrush_gen2', 'uibits')  # run make_glyphs.py afterwards
os.makedirs(OUT, exist_ok=True)

def chunk(t, d):
    c = struct.pack('>I', len(d)) + t + d
    return c + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)

def write_png(path, w, h, px, text=None, palette=None):
    """px: bytes RGBA (w*h*4) oder, mit palette, Indizes (w*h)."""
    raw = bytearray()
    bpp = 1 if palette else 4
    for y in range(h):
        raw.append(0)
        raw += px[y * w * bpp:(y + 1) * w * bpp]
    data = b'\x89PNG\r\n\x1a\n'
    data += chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 3 if palette else 6, 0, 0, 0))
    if palette:
        data += chunk(b'PLTE', b''.join(bytes(c[:3]) for c in palette))
        data += chunk(b'tRNS', bytes(c[3] for c in palette))
    for k, v in (text or {}).items():
        data += chunk(b'tEXt', k.encode() + b'\0' + v.encode())
    data += chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b'')
    open(path, 'wb').write(data)

class Img:
    def __init__(s, w, h, bg=(0, 0, 0, 0)):
        s.w, s.h = w, h
        s.p = bytearray(bytes(bg) * (w * h))
    def px(s, x, y, c):
        if 0 <= x < s.w and 0 <= y < s.h:
            i = (y * s.w + x) * 4
            if len(c) == 4 and c[3] < 255 and c[3] > 0:
                a = c[3] / 255.0
                for k in range(3): s.p[i + k] = int(s.p[i + k] * (1 - a) + c[k] * a)
                s.p[i + 3] = max(s.p[i + 3], c[3])
            else:
                s.p[i:i + 4] = bytes(c if len(c) == 4 else (*c, 255))
    def rect(s, x, y, w, h, c):
        for yy in range(y, y + h):
            for xx in range(x, x + w): s.px(xx, yy, c)
    def frame(s, x, y, w, h, c, t=1):
        s.rect(x, y, w, t, c); s.rect(x, y + h - t, w, t, c); s.rect(x, y, t, h, c); s.rect(x + w - t, y, t, h, c)
    def text(s, x, y, t, c, sc=1, sp=1):
        for ch in t:
            g = FONT.get(ch)
            if g:
                for r, row in enumerate(g):
                    for q, b in enumerate(row):
                        if b == '1': s.rect(x + q * sc, y + r * sc, sc, sc, c)
            x += 5 * sc + sp * sc
    def save(s, path, text=None): write_png(path, s.w, s.h, bytes(s.p), text)
    def blit(s, o, x, y):
        for yy in range(o.h):
            for xx in range(o.w):
                i = (yy * o.w + xx) * 4
                if o.p[i + 3]: s.px(x + xx, y + yy, tuple(o.p[i:i + 4]))

G = """A:01110,10001,10001,11111,10001,10001,10001
B:11110,10001,10001,11110,10001,10001,11110
C:01110,10001,10000,10000,10000,10001,01110
D:11110,10001,10001,10001,10001,10001,11110
E:11111,10000,10000,11110,10000,10000,11111
F:11111,10000,10000,11110,10000,10000,10000
G:01110,10001,10000,10111,10001,10001,01111
H:10001,10001,10001,11111,10001,10001,10001
I:01110,00100,00100,00100,00100,00100,01110
J:00111,00010,00010,00010,00010,10010,01100
K:10001,10010,10100,11000,10100,10010,10001
L:10000,10000,10000,10000,10000,10000,11111
M:10001,11011,10101,10101,10001,10001,10001
N:10001,11001,10101,10011,10001,10001,10001
O:01110,10001,10001,10001,10001,10001,01110
P:11110,10001,10001,11110,10000,10000,10000
Q:01110,10001,10001,10001,10101,10010,01101
R:11110,10001,10001,11110,10100,10010,10001
S:01111,10000,10000,01110,00001,00001,11110
T:11111,00100,00100,00100,00100,00100,00100
U:10001,10001,10001,10001,10001,10001,01110
V:10001,10001,10001,10001,10001,01010,00100
W:10001,10001,10001,10101,10101,10101,01010
X:10001,10001,01010,00100,01010,10001,10001
Y:10001,10001,01010,00100,00100,00100,00100
Z:11111,00001,00010,00100,01000,10000,11111
0:01110,10001,10011,10101,11001,10001,01110
1:00100,01100,00100,00100,00100,00100,01110
2:01110,10001,00001,00010,00100,01000,11111
3:11110,00001,00001,01110,00001,00001,11110
4:00010,00110,01010,10010,11111,00010,00010
5:11111,10000,11110,00001,00001,10001,01110
6:00110,01000,10000,11110,10001,10001,01110
7:11111,00001,00010,00100,01000,01000,01000
8:01110,10001,10001,01110,10001,10001,01110
9:01110,10001,10001,01111,00001,00010,01100
*:00000,10101,01110,11111,01110,10101,00000
-:00000,00000,00000,11111,00000,00000,00000
+:00000,00100,00100,11111,00100,00100,00000
 :00000,00000,00000,00000,00000,00000,00000"""
FONT = {l[0]: l[2:].split(',') for l in G.split('\n')}

def shade(c, f): return tuple(max(0, min(255, int(v * f))) for v in c[:3]) + (255,)

# ---------------------------------------------------------------- Palette (Spielerfarbe 8-13 wie in OpenKrush, Rest Verlauf)
pal = [(0, 0, 0, 0)] + [(min(255, i), min(255, i), min(255, i), 255) for i in range(1, 256)]
for k, i in enumerate(range(8, 14)):
    v = 235 - k * 36
    pal[i] = (v, v, v, 255)
write_png(OUT + '/palette.png', 16, 16, bytes(range(256)), palette=pal)

# ---------------------------------------------------------------- Dialog (gleiches Layout wie openkrush_gen1/uibits/dialog.png)
d = Img(1024, 512, (0, 0, 0, 0))
random.seed(7)
def noisy(x, y, w, h, base, amp=6):
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            n = random.randint(-amp, amp)
            d.px(xx, yy, (base[0] + n, base[1] + n, base[2] + n, 255))
def panel(x, y, w, h, base, border):
    noisy(x, y, w, h, base)
    d.frame(x, y, w, h, border, 2 if w > 40 else 1)
BG = (58, 44, 30); BTN = (86, 62, 48); BTN_H = (104, 76, 60); BTN_P = (46, 32, 24); BTN_D = (128, 128, 128)
HL = (118, 84, 64); DARK = (8, 14, 24); BORDER = (24, 16, 10)
noisy(1, 1, 480, 480, BG, 5)
noisy(492, 1, 9, 190, BORDER, 4); noisy(483, 1, 9, 190, BORDER, 4)
noisy(1, 492, 189, 9, BORDER, 4); noisy(1, 483, 189, 9, BORDER, 4)
for x, y in ((192, 483), (201, 483), (192, 492), (201, 492)): noisy(x, y, 9, 9, BORDER, 4)
panel(513, 1, 122, 122, BTN, BORDER); panel(641, 1, 122, 122, BTN_P, BORDER)
panel(513, 129, 122, 122, BTN_H, BORDER); panel(641, 129, 122, 122, DARK, BORDER)
panel(513, 257, 122, 122, BTN_D, BORDER); panel(641, 257, 122, 122, BTN_P, BORDER)
panel(769, 257, 122, 122, HL, BORDER); panel(897, 257, 122, 122, BTN_H, BORDER)
panel(769, 1, 122, 122, DARK, BORDER); panel(897, 1, 122, 122, DARK, BORDER)
panel(769, 129, 122, 122, (6, 22, 56), BORDER); panel(897, 129, 122, 122, (6, 22, 56), BORDER)
panel(769, 385, 122, 122, DARK, BORDER); panel(897, 385, 122, 122, DARK, BORDER)
panel(512, 387, 64, 64, BTN, BORDER)
d.rect(580, 388, 62, 62, (0, 0, 0, 255))
panel(650, 389, 116, 116, BG, BORDER)
d.rect(513, 2, 1, 19, BORDER); d.rect(513, 130, 1, 19, BORDER); d.rect(766, 2, 1, 19, BORDER); d.rect(513, 258, 1, 19, BORDER); d.rect(769, 258, 1, 19, BORDER)
d.save(OUT + '/dialog.png')

# ---------------------------------------------------------------- Chrome (1x1 Platzhalter, nur fuer ^Chrome-Leerregionen)
write_png(OUT + '/chrome.png', 1, 1, bytes((255, 255, 255, 255)))

# ---------------------------------------------------------------- Glyphs (nur die im Chrome benoetigten, eigene Formen)
gl = Img(256, 256)
GOLD = (200, 170, 100, 255)
def play(x, y): [gl.rect(x + 3, y + 2 + i, 1 + (7 - abs(i - 6)) , 1, GOLD) for i in range(12)]
# 0,0 .. Musik: pause, stop, play, next, prev, ff (16x16)
gl.rect(3, 2, 3, 12, GOLD); gl.rect(10, 2, 3, 12, GOLD)
gl.rect(17 + 3, 3, 10, 10, GOLD); play(34, 0)
gl.rect(51 + 9, 2, 3, 12, GOLD); gl.rect(68 + 3, 2, 3, 12, GOLD)
gl.text(85 + 3, 4, '>>', GOLD)
# Haken / Kreuz (checkmarks)
gl.text(187 + 2, 4, 'V', GOLD, 1); gl.rect(204 + 2, 2, 12, 12, (255, 255, 255, 255)); gl.text(221 + 2, 4, 'X', GOLD, 1); gl.rect(238 + 2, 2, 12, 12, (255, 255, 255, 255))
# Pfeile (scrollpanel/dropdown), 16x16 je Feld bei y=17
for i, (x, dirn) in enumerate(((68, 'down'), (102, 'up'), (136, 'right'), (170, 'left'))):
    for dx in (0, 17):
        for k in range(6):
            w = 12 - 2 * k
            if dirn in ('down', 'up'):
                yy = (3 + k) if dirn == 'down' else (8 - k)
                gl.rect(x + dx + 8 - w // 2, 17 + yy, w, 1, GOLD if dx == 0 else (110, 100, 80, 255))
            else:
                xx = (3 + k) if dirn == 'right' else (8 - k)
                gl.rect(x + dx + xx, 17 + 8 - w // 2, 1, w, GOLD if dx == 0 else (110, 100, 80, 255))
# Stummschaltung / Spawn-Marker (rund)
for (x, y) in ((27, 119), (50, 119), (73, 119)):
    c = (240, 240, 240, 255) if x == 27 else ((150, 150, 150, 255) if x == 50 else (90, 90, 90, 255))
    for yy in range(22):
        for xx in range(22):
            if (xx - 10.5) ** 2 + (yy - 10.5) ** 2 <= 100: gl.px(x + xx, y + yy, c)
gl.rect(0, 170, 16, 16, (80, 60, 40, 255)); gl.rect(17, 170, 16, 16, (200, 60, 40, 255))
for k in range(12):  # Reload-Symbol (leer genug, Chrome nutzt nur die Groesse)
    pass
gl.text(2, 36, 'R', GOLD)
gl.save(OUT + '/glyphs.png')

# ---------------------------------------------------------------- Logo und Ladebilder (eigene Schrift)
lg = Img(512, 128)
lg.text(40, 20, 'KKND2', (222, 176, 78, 255), 14, 2)
lg.text(46, 108, 'THOR', (180, 150, 100, 255), 2, 4)
lg.save(OUT + '/logo.png')
for name, label in (('splashscreen', 'KKND2 THOR'), ('loadscreen', 'LOADING')):
    im = Img(1024, 512, (18, 14, 10, 255))
    for yy in range(512):
        shade_ = int(10 * (yy / 480.0))
        im.rect(0, yy, 1024, 1, (18 + shade_, 14 + shade_, 10 + shade_, 255))
    im.text(320 - len(label) * 6 * 8 // 2, 214, label, (222, 176, 78, 255), 8, 1)
    im.save(OUT + f'/{name}.png')

# ---------------------------------------------------------------- Seitenleiste (48x48 pro Frame, 19 Frames je Fraktion)
FACTIONS = {'survivors': (176, 132, 64), 'evolved': (96, 140, 70), 'series9': (80, 118, 168)}
SIDEBAR = ['button', 'button-down', 'unit', 'unit-down', 'infantry', 'vehicle', 'aircraft', 'building', 'tower', 'wall',
           'oil', 'sell', 'radar', 'options', 'repair', 'research', 'button-small', 'button-small-up', 'button-small-down']
LABEL = {'infantry': 'INF', 'vehicle': 'VEH', 'aircraft': 'AIR', 'building': 'BLD', 'tower': 'TWR', 'wall': 'WAL', 'oil': 'OIL',
         'sell': 'SEL', 'radar': 'RDR', 'options': 'OPT', 'repair': 'REP', 'research': 'RES', 'button-small-up': '+', 'button-small-down': '-'}
for f, col in FACTIONS.items():
    cols = 8
    sheet = Img(cols * 48, ((len(SIDEBAR) + cols - 1) // cols) * 48)
    for i, name in enumerate(SIDEBAR):
        fr = Img(48, 48)
        down = name.endswith('down')
        small = name.startswith('button-small')
        base = shade(col, 0.32 if down else 0.45)
        if small: fr.rect(10, 4, 28, 40, base); fr.frame(10, 4, 28, 40, shade(col, 0.9 if not down else 0.5), 2)
        else:
            fr.rect(2, 2, 44, 44, base); fr.frame(2, 2, 44, 44, shade(col, 0.9 if not down else 0.5), 2)
        if name in LABEL:
            t = LABEL[name]; sc = 2 if len(t) < 3 else 1
            w = len(t) * 6 * sc - sc
            fr.text(24 - w // 2, 24 - 7 * sc // 2, t, (240, 232, 210, 255), sc, 1)
        sheet.blit(fr, (i % cols) * 48, (i // cols) * 48)
    sheet.save(OUT + f'/sidebar-{f}.png', {'FrameSize': '48,48', 'FrameAmount': str(len(SIDEBAR))})

# ---------------------------------------------------------------- Produktionsziffern (20x20, 11 Frames: 0-9 und unendlich)
fnt = Img(11 * 20, 20)
for i in range(11):
    fr = Img(20, 20)
    ch = str(i) if i < 10 else '*'
    fr.text(7, 5, ch, (0, 0, 0, 255), 1); fr.text(5, 5, ch, (0, 0, 0, 255), 1); fr.text(6, 4, ch, (0, 0, 0, 255), 1); fr.text(6, 6, ch, (0, 0, 0, 255), 1)
    fr.text(6, 5, ch, (255, 240, 120, 255), 1)
    fnt.blit(fr, i * 20, 0)
fnt.save(OUT + '/font-production.png', {'FrameSize': '20,20', 'FrameAmount': '11', 'Offset': '0,0'})

# ---------------------------------------------------------------- Shroud / Fog: Frame i = verdunkelte Ecken Index[i] (TL=1, TR=2, BR=4, BL=8)
INDEX = [0, 15, 3, 6, 12, 9, 11, 7, 14, 13, 1, 2, 4, 8, 5, 10]
def shroud_sheet(maxalpha, path):
    sh = Img(16 * 32, 32)
    for i, mask in enumerate(INDEX):
        for y in range(32):
            for x in range(32):
                u, v = (x + 0.5) / 32.0, (y + 0.5) / 32.0
                tl, tr, br, bl = [(mask >> k) & 1 for k in range(4)]
                a = tl * (1 - u) * (1 - v) + tr * u * (1 - v) + br * u * v + bl * (1 - u) * v
                if mask == 15: a = 1.0
                a = a * a * (3 - 2 * a)  # weicher Rand
                sh.px(x + i * 32, y, (0, 0, 0, int(round(a * maxalpha))))
    sh.save(path, {'FrameSize': '32,32', 'FrameAmount': '16'})
shroud_sheet(255, OUT + '/shroud.png')
shroud_sheet(150, OUT + '/fog.png')

# ---------------------------------------------------------------- Beacon (10 Frames, wachsender Ring), Rallypoint-Flagge entfaellt (Image leer)
bc = Img(10 * 32, 32)
for i in range(10):
    r = 3 + i * 1.4
    for y in range(32):
        for x in range(32):
            dd = math.hypot(x - 15.5, y - 15.5)
            if abs(dd - r) < 1.2: bc.px(x + i * 32, y, (255, 230, 80, int(255 * (1 - i / 11.0))))
bc.save(OUT + '/beacon.png', {'FrameSize': '32,32', 'FrameAmount': '10'})
print('ok', sorted(os.listdir(OUT)))
