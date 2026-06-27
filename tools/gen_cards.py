#!/usr/bin/env python3
"""Generate all 44 Scoundrel card art images as SVGs (vector, scales to any resolution).

Requires fonttools:  python3 -m pip install fonttools
"""

import os, math, json

from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen

W, H = 150, 210
OUT       = "/home/aileen/Repositories/godot/scoundrel-with-help/card_assets"
JSON_DIR  = "/home/aileen/Repositories/godot/scoundrel-with-help/card_data"
FONT_PATH = "/home/aileen/Repositories/godot/scoundrel-with-help/assets/fonts/DejaVuSans-Bold.ttf"

# ── Palettes ─────────────────────────────────────────────────────────────────
P = {
    'clubs':    {'bg':(8,18,12),   'border':(40,105,62),  'accent':(65,175,105), 'glow':(95,255,140), 'text':(210,255,225), 'dim':(22,50,32)},
    'spades':   {'bg':(8,8,22),    'border':(58,32,118),  'accent':(115,65,205), 'glow':(165,95,255), 'text':(215,185,255), 'dim':(28,15,58)},
    'hearts':   {'bg':(22,7,7),    'border':(138,26,26),  'accent':(212,48,48),  'glow':(255,72,72),  'text':(255,195,195), 'dim':(58,15,15)},
    'diamonds': {'bg':(22,17,4),   'border':(124,93,20),  'accent':(202,162,44), 'glow':(255,222,82), 'text':(255,242,185), 'dim':(56,40,10)},
}

RANK_VAL = {'ace':14,'2':2,'3':3,'4':4,'5':5,'6':6,'7':7,'8':8,'9':9,'10':10,'jack':11,'queen':12,'king':13}
RANK_STR = {'ace':'A','2':'2','3':'3','4':'4','5':'5','6':'6','7':'7','8':'8','9':'9','10':'10','jack':'J','queen':'Q','king':'K'}
SUIT_SYM = {'clubs':'♣','spades':'♠','hearts':'♥','diamonds':'◆'}

NAMES = {
    ('clubs','ace'):'VOID WRAITH',      ('clubs','2'):'CAVE RAT',        ('clubs','3'):'GOBLIN',
    ('clubs','4'):'ORC GRUNT',          ('clubs','5'):'HOBGOBLIN',       ('clubs','6'):'CAVE TROLL',
    ('clubs','7'):'OGRE BRUTE',         ('clubs','8'):'STONE GOLEM',     ('clubs','9'):'MINOTAUR',
    ('clubs','10'):'CYCLOPS',           ('clubs','jack'):'ORC WARCHIEF', ('clubs','queen'):'FOREST WITCH',
    ('clubs','king'):'FIRE DRAKE',
    ('spades','ace'):'SHADOW REAPER',   ('spades','2'):'GRAVE SLUG',     ('spades','3'):'SKELETON',
    ('spades','4'):'ZOMBIE',            ('spades','5'):'GHOUL',          ('spades','6'):'SPECTER',
    ('spades','7'):'BANSHEE',           ('spades','8'):'WIGHT',          ('spades','9'):'LICH',
    ('spades','10'):'VAMPIRE',          ('spades','jack'):'DEATH KNIGHT',('spades','queen'):'WRAITH QUEEN',
    ('spades','king'):'DRAGON LORD',
    ('hearts','2'):'MINOR SALVE',       ('hearts','3'):'HEALING HERB',   ('hearts','4'):'MENDING TONIC',
    ('hearts','5'):'LIFE ELIXIR',       ('hearts','6'):'STAMINA BREW',   ('hearts','7'):'BLOOD MENDER',
    ('hearts','8'):'GRAND POTION',      ('hearts','9'):'LIFE ESSENCE',   ('hearts','10'):'HOLY DRAUGHT',
    ('diamonds','2'):'CRACKED DAGGER',  ('diamonds','3'):'IRON DAGGER',  ('diamonds','4'):'SHORT SWORD',
    ('diamonds','5'):'BROAD SWORD',     ('diamonds','6'):'BATTLE AXE',   ('diamonds','7'):'WAR HAMMER',
    ('diamonds','8'):'RUNED BLADE',     ('diamonds','9'):'ELVEN BOW',    ('diamonds','10'):'VOID REAVER',
}


# ── Glyph renderer — converts text to SVG <path> outlines ────────────────────

class GlyphRenderer:
    """Rasterizes text as SVG <path> elements using fontTools SVGPathPen.

    ThorVG (Godot's SVG backend) cannot resolve font-family references, so
    <text> elements are invisible. Outline paths have no runtime font dependency.
    """

    def __init__(self, font_path):
        font = TTFont(font_path)
        self._glyph_set = font.getGlyphSet()
        self._cmap      = font.getBestCmap()
        self._hmtx      = font['hmtx'].metrics
        self._upm       = font['head'].unitsPerEm
        self._ascender  = font['hhea'].ascent  # for Pillow-style top-left y coord

    def render(self, x, y, txt, size, fill):
        """Return list of SVG <path> elements for txt at Pillow top-left (x, y)."""
        scale    = size / self._upm
        baseline = y + self._ascender * scale
        elems    = []
        cx       = float(x)
        for ch in str(txt):
            cp = ord(ch)
            if cp not in self._cmap:
                cx += size * 0.3
                continue
            gname = self._cmap[cp]
            pen   = SVGPathPen(self._glyph_set)
            self._glyph_set[gname].draw(pen)
            if pen.getCommands():
                elems.append(
                    f'<path transform="translate({cx:.2f},{baseline:.2f})'
                    f' scale({scale:.5f},{-scale:.5f})"'
                    f' d="{pen.getCommands()}" fill="{fill}"/>'
                )
            cx += self._hmtx[gname][0] * scale
        return elems

    def measure(self, txt, size):
        """Return the pixel width of txt rendered at the given size."""
        scale = size / self._upm
        total = 0
        for ch in str(txt):
            cp = ord(ch)
            if cp in self._cmap:
                total += self._hmtx[self._cmap[cp]][0]
            else:
                total += int(self._upm * 0.3)
        return total * scale


_GLYPHS: 'GlyphRenderer | None' = None


# ── SVG canvas ───────────────────────────────────────────────────────────────

def _rgb(c):
    return f'rgb({c[0]},{c[1]},{c[2]})'


class SVGCanvas:
    """Drop-in replacement for Pillow ImageDraw that emits SVG elements."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self._e = []

    def rectangle(self, bbox, fill=None, outline=None, width=1):
        x1, y1, x2, y2 = bbox
        f = _rgb(fill) if fill else 'none'
        s = f' stroke="{_rgb(outline)}" stroke-width="{width}"' if outline else ''
        self._e.append(f'<rect x="{x1}" y="{y1}" width="{x2-x1}" height="{y2-y1}" fill="{f}"{s}/>')

    def ellipse(self, bbox, fill=None, outline=None, width=1):
        x1, y1, x2, y2 = bbox
        cx = (x1+x2)/2; cy = (y1+y2)/2
        rx = (x2-x1)/2; ry = (y2-y1)/2
        f = _rgb(fill) if fill else 'none'
        s = f' stroke="{_rgb(outline)}" stroke-width="{width}"' if outline else ''
        self._e.append(f'<ellipse cx="{cx:.2f}" cy="{cy:.2f}" rx="{rx:.2f}" ry="{ry:.2f}" fill="{f}"{s}/>')

    def polygon(self, pts, fill=None, outline=None, width=1):
        ps = ' '.join(f'{x},{y}' for x, y in pts)
        f = _rgb(fill) if fill else 'none'
        s = f' stroke="{_rgb(outline)}" stroke-width="{width}"' if outline else ''
        self._e.append(f'<polygon points="{ps}" fill="{f}"{s} stroke-linejoin="round"/>')

    def line(self, pts, fill=None, width=1):
        col = _rgb(fill) if fill else 'none'
        if len(pts) == 2:
            (x1, y1), (x2, y2) = pts
            self._e.append(
                f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}"'
                f' stroke="{col}" stroke-width="{width}" stroke-linecap="round"/>'
            )
        else:
            ps = ' '.join(f'{x},{y}' for x, y in pts)
            self._e.append(
                f'<polyline points="{ps}" fill="none"'
                f' stroke="{col}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"/>'
            )

    def text(self, pos, txt, fill=None, font=12):
        x, y = pos
        size = font if isinstance(font, (int, float)) else 12
        f    = _rgb(fill) if fill else 'black'
        self._e.extend(_GLYPHS.render(x, y, str(txt), size, f))

    def textbbox(self, _pos, txt, font=12):
        size = font if isinstance(font, (int, float)) else 12
        return (0, 0, _GLYPHS.measure(str(txt), size), size)

    def arc(self, bbox, start, end, fill=None, width=1):
        x1, y1, x2, y2 = bbox
        cx = (x1+x2)/2; cy = (y1+y2)/2
        rx = (x2-x1)/2; ry = (y2-y1)/2
        sr = math.radians(start); er = math.radians(end)
        sx = cx + rx*math.cos(sr); sy = cy + ry*math.sin(sr)
        ex = cx + rx*math.cos(er); ey = cy + ry*math.sin(er)
        large = 1 if (end - start) % 360 > 180 else 0
        col = _rgb(fill) if fill else 'none'
        d = f'M {sx:.2f},{sy:.2f} A {rx:.2f},{ry:.2f} 0 {large},1 {ex:.2f},{ey:.2f}'
        self._e.append(f'<path d="{d}" fill="none" stroke="{col}" stroke-width="{width}"/>')

    def to_svg(self):
        inner = '\n'.join(self._e)
        return (
            f'<svg xmlns="http://www.w3.org/2000/svg"'
            f' width="{self.w}" height="{self.h}" viewBox="0 0 {self.w} {self.h}">\n'
            f'{inner}\n</svg>\n'
        )


# ── Font sizes (SVG doesn't need font objects) ────────────────────────────────

def load_fonts():
    return [22, 15, 11, 9]


def draw_bg(draw, p):
    draw.rectangle([0, 0, W, H], fill=p['bg'])
    lc = tuple(min(255, c + 7) for c in p['bg'])
    for i in range(-H, W + H, 14):
        draw.line([(i, 0), (i + H, H)], fill=lc, width=1)
    draw.rectangle([1, 1, W-2, H-2], outline=p['border'], width=3)
    draw.rectangle([5, 5, W-6, H-6], outline=p['dim'], width=1)
    for cx2, cy2 in [(2,2),(W-3,2),(2,H-3),(W-3,H-3)]:
        draw.polygon([(cx2,cy2-3),(cx2+3,cy2),(cx2,cy2+3),(cx2-3,cy2)], fill=p['accent'])


def draw_pip(draw, rank, suit, p, fonts):
    draw.text((9, 7),  RANK_STR[rank],   fill=p['text'],   font=fonts[0])
    draw.text((10, 31), SUIT_SYM[suit],  fill=p['accent'], font=fonts[1])


def draw_name_banner(draw, name, p, font):
    draw.rectangle([7, 162, W-8, H-8], fill=p['dim'])
    draw.rectangle([7, 162, W-8, H-8], outline=p['border'], width=1)
    bbox = draw.textbbox((0, 0), name, font=font)
    tw = bbox[2] - bbox[0]
    if tw > W - 18 and ' ' in name:
        parts = name.split(' ', 1)
        for i, part in enumerate(parts):
            bb = draw.textbbox((0, 0), part, font=font)
            pw = bb[2] - bb[0]
            draw.text(((W - pw) // 2, 165 + i * 13), part, fill=p['text'], font=font)
    else:
        draw.text(((W - tw) // 2, 172), name, fill=p['text'], font=font)


# ── Skull helper ──────────────────────────────────────────────────────────────

def skull(draw, cx, cy, sz, p, crown=False, horns=False, one_eye=False, crack=False):
    bone = tuple(min(255, int(c * 0.35 + 165)) for c in p['accent'])
    dark = tuple(int(c * 0.22) for c in bone)
    ec   = p['glow']
    cr   = int(sz * 0.48)
    jw   = int(sz * 0.38)
    jh   = int(sz * 0.22)
    jy   = cy + sz // 5

    draw.ellipse([cx-cr, cy-sz//2, cx+cr, cy+sz//5], fill=bone)
    draw.rectangle([cx-jw, jy, cx+jw, jy+jh], fill=bone)

    if one_eye:
        er = int(sz * 0.24)
        ey = cy - sz // 14
        draw.ellipse([cx-er, ey-er, cx+er, ey+er], fill=dark)
        draw.ellipse([cx-er+4, ey-er+4, cx+er-4, ey+er-4], fill=ec)
        draw.ellipse([cx-5, ey-4, cx+5, ey+6], fill=dark)
    else:
        ew = int(sz * 0.16); eh = int(sz * 0.18); ex_off = int(sz * 0.20)
        ey = cy - sz // 10
        for ex in [cx - ex_off, cx + ex_off]:
            draw.ellipse([ex-ew, ey-eh, ex+ew, ey+eh], fill=dark)
            draw.ellipse([ex-ew+3, ey-eh+3, ex+ew-3, ey+eh-3], fill=ec)

    np_y = cy + sz // 9
    draw.polygon([(cx, np_y-5),(cx-5, np_y+6),(cx+5, np_y+6)], fill=dark)

    tw2 = int(sz * 0.07)
    for i in range(5):
        tx = cx - jw + 4 + i * (tw2 + 3)
        draw.rectangle([tx, jy+2, tx+tw2, jy+jh-4], fill=dark)

    if crack:
        draw.line([(cx-6, cy-sz//2+2),(cx+4, cy+2)], fill=dark, width=2)

    if horns:
        hc = tuple(min(255, int(c*0.55+85)) for c in p['accent'])
        hy = cy - sz//2 + 4
        draw.polygon([(cx-cr+5, hy),(cx-cr-12, hy-22),(cx-cr+10, hy-4)], fill=hc)
        draw.polygon([(cx+cr-5, hy),(cx+cr+12, hy-22),(cx+cr-10, hy-4)], fill=hc)

    if crown:
        cw = cr + 5
        chy = cy - sz//2 - 14
        draw.rectangle([cx-cw, chy+8, cx+cw, cy-sz//2+2], fill=p['glow'])
        for sx2 in [-cw, -cw//2, 0, cw//2, cw]:
            draw.polygon([(cx+sx2-4, chy+8),(cx+sx2+4, chy+8),(cx+sx2, chy)], fill=p['glow'])


def knight_figure(draw, cx, cy, p, flaming=False):
    ac = (175, 180, 195) if flaming else p['accent']
    dc = p['dim']
    draw.ellipse([cx-16, cy-55, cx+16, cy-30], fill=ac)
    draw.rectangle([cx-16, cy-42, cx+16, cy-20], fill=ac)
    draw.rectangle([cx-10, cy-40, cx+10, cy-30], fill=dc)
    draw.polygon([(cx-22,cy-20),(cx+22,cy-20),(cx+26,cy+18),(cx-26,cy+18)], fill=ac)
    draw.rectangle([cx-30, cy-18, cx-22, cy+12], fill=ac)
    draw.rectangle([cx+22, cy-18, cx+30, cy+12], fill=ac)
    sx = cx + 32
    draw.rectangle([sx-3, cy-52, sx+3, cy+15], fill=tuple(min(255,c+55) for c in ac))
    draw.rectangle([sx-12, cy-26, sx+12, cy-20], fill=ac)
    if flaming:
        for fy2 in range(cy-52, cy-26, 5):
            fw2 = 3
            draw.ellipse([sx-fw2, fy2, sx+fw2, fy2+5], fill=(255, max(0, 55+(fy2-(cy-52))*9), 0))


def dragon_head(draw, cx, cy, p, skeletal=False):
    dc = tuple(min(255, c//3+130) for c in p['accent']) if skeletal else tuple(min(255, int(c*0.45+75)) for c in p['accent'])
    ec = p['glow']
    draw.ellipse([cx-32, cy-30, cx+18, cy+22], fill=dc)
    draw.ellipse([cx-12, cy-15, cx+45, cy+14], fill=dc)
    draw.ellipse([cx-4, cy-22, cx+12, cy-9], fill=(0,0,0))
    draw.ellipse([cx-1, cy-20, cx+9, cy-11], fill=ec)
    draw.polygon([(cx+32,cy+14),(cx+40,cy+24),(cx+38,cy+14)], fill=tuple(min(255,c+70) for c in dc))
    draw.polygon([(cx+20,cy+14),(cx+27,cy+26),(cx+26,cy+14)], fill=tuple(min(255,c+70) for c in dc))
    draw.polygon([(cx-28,cy-28),(cx-44,cy-55),(cx-14,cy-32)], fill=dc)
    draw.polygon([(cx-10,cy-28),(cx-6, cy-50),(cx+8, cy-28)], fill=dc)
    if not skeletal:
        for fx,fy2,fr2,fc2 in [(cx+46,cy+2,9,(255,100,0)),(cx+56,cy-5,6,(255,200,0)),(cx+64,cy+4,4,(255,55,0))]:
            draw.ellipse([fx-fr2,fy2-fr2,fx+fr2,fy2+fr2], fill=fc2)


# ── Clubs monsters ────────────────────────────────────────────────────────────

def draw_clubs(draw, cx, cy, p, rank):
    rv = RANK_VAL[rank]

    if rank == 'ace':
        for r in range(38, 0, -4):
            fc2 = tuple(int(c * r / 38) for c in p['glow'])
            draw.ellipse([cx-r, cy-r, cx+r, cy+r], fill=fc2)
        draw.ellipse([cx-16, cy-16, cx+16, cy+16], fill=p['bg'])
        for angle in range(0, 360, 45):
            rad = math.radians(angle)
            for r in [12, 20, 28]:
                px = cx + int(r*math.cos(rad)); py = cy + int(r*math.sin(rad))
                draw.ellipse([px-3,py-3,px+3,py+3], fill=p['glow'])
        for ex,ey in [(cx-8,cy-5),(cx+8,cy-5)]:
            draw.ellipse([ex-4,ey-3,ex+4,ey+3], fill=p['glow'])
    elif rank == '2':
        bc = (85, 72, 58)
        draw.ellipse([cx-22,cy-10,cx+22,cy+18], fill=bc)
        draw.ellipse([cx-30,cy-24,cx-6, cy-2],  fill=bc)
        draw.ellipse([cx-32,cy-34,cx-20,cy-20], fill=bc)
        draw.ellipse([cx-18,cy-34,cx-8, cy-22], fill=bc)
        draw.ellipse([cx-26,cy-26,cx-22,cy-22], fill=p['glow'])
        draw.line([(cx+22,cy+8),(cx+36,cy-2),(cx+42,cy+6)], fill=bc, width=3)
    elif rank == '3':
        sc = (48, 96, 58)
        draw.ellipse([cx-16,cy+6, cx+16,cy+28], fill=sc)
        draw.ellipse([cx-20,cy-28,cx+20,cy+10], fill=sc)
        draw.polygon([(cx-20,cy-12),(cx-34,cy-28),(cx-12,cy-20)], fill=sc)
        draw.polygon([(cx+20,cy-12),(cx+34,cy-28),(cx+12,cy-20)], fill=sc)
        for ex in [cx-8, cx+8]:
            draw.ellipse([ex-6,cy-18,ex+6,cy-6], fill=(0,0,0))
            draw.ellipse([ex-4,cy-16,ex+4,cy-8], fill=p['glow'])
        draw.line([(cx-7,cy+4),(cx+7,cy+4)], fill=(0,0,0), width=2)
    elif rank in ('4','5'):
        sz = 52 + (rv-4)*4
        skull(draw, cx, cy, sz, p)
        tc = (225, 212, 195)
        for tx in [cx-13, cx+5]:
            draw.polygon([(tx,cy+6),(tx-5,cy+22),(tx+5,cy+22)], fill=tc)
    elif rank in ('6','7'):
        sz = 58 + (rv-6)*4
        skull(draw, cx, cy, sz, p)
        for side in [-1, 1]:
            draw.polygon([(cx+side*(sz//2),cy-8),(cx+side*(sz//2+10),cy-22),(cx+side*(sz//2-6),cy-18)], fill=p['accent'])
    elif rank == '8':
        sc = (122, 112, 100); dc2 = (72, 65, 58)
        draw.rectangle([cx-32,cy-38,cx+32,cy+28], fill=sc)
        draw.line([(cx-10,cy-38),(cx+6, cy+8)], fill=dc2, width=2)
        draw.line([(cx+10,cy-22),(cx-8, cy+8)], fill=dc2, width=2)
        for ex in [cx-14, cx+14]:
            draw.rectangle([ex-8,cy-22,ex+8,cy-10], fill=(0,0,0))
            draw.rectangle([ex-5,cy-19,ex+5,cy-13], fill=p['glow'])
        draw.rectangle([cx-4,cy-4,cx+4,cy+6], fill=dc2)
    elif rank == '9':
        skull(draw, cx, cy, 62, p, horns=True, crack=True)
        hc2 = (200, 180, 148)
        for side in [-1, 1]:
            draw.polygon([(cx+side*28,cy-26),(cx+side*48,cy-55),(cx+side*18,cy-36)], fill=hc2)
    elif rank == '10':
        skull(draw, cx, cy, 66, p, one_eye=True, crack=True)
    elif rank == 'jack':
        skull(draw, cx, cy-5, 55, p, crown=True)
        draw.line([(cx+32,cy-42),(cx+32,cy+22)], fill=p['dim'], width=5)
        draw.polygon([(cx+32,cy-38),(cx+52,cy-48),(cx+54,cy-28),(cx+32,cy-18)], fill=p['accent'])
    elif rank == 'queen':
        wc = tuple(int(c*0.45+28) for c in p['accent'])
        draw.ellipse([cx-26,cy-42,cx+26,cy-30], fill=wc)
        draw.polygon([(cx-14,cy-42),(cx+14,cy-42),(cx,cy-75)], fill=wc)
        draw.ellipse([cx-18,cy-38,cx+18,cy-8], fill=(148,118,78))
        for ex in [cx-7, cx+7]:
            draw.ellipse([ex-4,cy-28,ex+4,cy-20], fill=p['glow'])
        draw.polygon([(cx,cy-18),(cx+13,cy-8),(cx+4,cy-10)], fill=(118,92,58))
        draw.polygon([(cx-15,cy-8),(cx+15,cy-8),(cx+24,cy+26),(cx-24,cy+26)], fill=wc)
        draw.line([(cx-26,cy-18),(cx-20,cy+26)], fill=p['border'], width=3)
        draw.ellipse([cx-30,cy-28,cx-20,cy-18], fill=p['glow'])
    elif rank == 'king':
        dragon_head(draw, cx, cy, p, skeletal=False)


# ── Spades monsters ───────────────────────────────────────────────────────────

def draw_spades(draw, cx, cy, p, rank):
    rv = RANK_VAL[rank]

    if rank == 'ace':
        cc = tuple(int(c*0.18) for c in p['text'])
        draw.polygon([(cx-32,cy+32),(cx+32,cy+32),(cx+12,cy-42),(cx-12,cy-42)], fill=cc)
        draw.ellipse([cx-16,cy-52,cx+16,cy-30], fill=cc)
        for ex,ey in [(cx-7,cy-43),(cx+7,cy-43)]:
            draw.ellipse([ex-4,ey-3,ex+4,ey+3], fill=p['glow'])
        draw.line([(cx+15,cy-55),(cx+20,cy+32)], fill=p['border'], width=4)
        for r in range(28, 14, -2):
            for a in range(200, 335, 6):
                rad = math.radians(a)
                px = cx+15+int(r*math.cos(rad)); py = cy-40+int(r*0.55*math.sin(rad))
                draw.ellipse([px-2,py-2,px+2,py+2], fill=p['glow'])
    elif rank == '2':
        sc = (72, 98, 58)
        draw.ellipse([cx-28,cy-12,cx+32,cy+20], fill=sc)
        draw.ellipse([cx-22,cy-22,cx-2, cy-4],  fill=sc)
        for stx,sty in [(cx-18,cy-38),(cx-7,cy-35)]:
            draw.line([(stx,cy-22),(stx,sty)], fill=sc, width=3)
            draw.ellipse([stx-4,sty-4,stx+4,sty+4], fill=p['glow'])
        for sx2 in range(cx+28, cx+50, 7):
            draw.ellipse([sx2-3,cy+8,sx2+3,cy+14], fill=(45,72,30))
    elif rank == '3':
        skull(draw, cx, cy-18, 42, p)
        draw.line([(cx,cy+4),(cx,cy+38)], fill=p['accent'], width=3)
        for ry in range(cy+5, cy+34, 9):
            for side in [-1, 1]:
                draw.line([(cx,ry),(cx+side*26,ry-9)], fill=p['accent'], width=2)
    elif rank == '4':
        skull(draw, cx-5, cy-22, 40, p, crack=True)
        draw.line([(cx-28,cy+8),(cx-52,cy-8)], fill=p['accent'], width=5)
        draw.line([(cx+18,cy+8),(cx+46,cy-6)], fill=p['accent'], width=5)
        draw.polygon([(cx-16,cy+8),(cx+16,cy+8),(cx+20,cy+38),(cx-20,cy+38)], fill=p['dim'])
    elif rank == '5':
        skull(draw, cx, cy-16, 48, p, crack=True)
        for side,hx in [(-1,cx-35),(1,cx+35)]:
            for ci in range(3):
                cx3 = hx + ci*6*side
                draw.line([(hx,cy+8),(cx3,cy-12)], fill=p['accent'], width=2)
    elif rank == '6':
        gc2 = tuple(int(c*0.38) for c in p['text'])
        draw.ellipse([cx-24,cy-50,cx+24,cy+2], fill=gc2)
        for yo in range(2, 36, 5):
            bw2 = max(3, 22-yo)
            bc2 = tuple(max(0,int(c*(0.38-yo/90))) for c in p['text'])
            draw.ellipse([cx-bw2,cy+yo-4,cx+bw2,cy+yo+4], fill=bc2)
        for ex in [cx-10, cx+10]:
            draw.ellipse([ex-6,cy-30,ex+6,cy-20], fill=(0,0,0))
            draw.ellipse([ex-3,cy-28,ex+3,cy-22], fill=p['glow'])
    elif rank == '7':
        gc2 = tuple(int(c*0.35) for c in p['text'])
        draw.ellipse([cx-22,cy-46,cx+22,cy+4], fill=gc2)
        for yo in range(4, 32, 6):
            bw2 = max(2, 20-yo)
            draw.ellipse([cx-bw2,cy+yo-4,cx+bw2,cy+yo+4], fill=tuple(max(0,int(c*max(0,0.35-yo/85))) for c in p['text']))
        for ex in [cx-8, cx+8]:
            draw.ellipse([ex-5,cy-28,ex+5,cy-20], fill=(0,0,0))
        draw.ellipse([cx-10,cy-6,cx+10,cy+10], fill=(0,0,0))
        for hx2 in [-28,-14,0,14,28]:
            draw.line([(cx+hx2,cy-44),(cx+hx2*2,cy-68)], fill=p['accent'], width=2)
    elif rank == '8':
        knight_figure(draw, cx, cy, p, flaming=False)
        for r in range(42, 32, -3):
            draw.arc([cx-r,cy-r,cx+r,cy+r], 200, 340, fill=tuple(int(c*0.2) for c in p['glow']), width=2)
    elif rank == '9':
        skull(draw, cx, cy-12, 56, p, crown=True, crack=True)
        for side2,hx2 in [(-1,cx-38),(1,cx+38)]:
            for f2 in range(4):
                draw.line([(hx2,cy+f2*6),(hx2+side2*14,cy+f2*6-10)], fill=p['accent'], width=2)
        oy = cy+34
        draw.ellipse([cx-11,oy-11,cx+11,oy+11], fill=p['dim'])
        draw.ellipse([cx-7, oy-7, cx+7, oy+7],  fill=p['glow'])
    elif rank == '10':
        skull(draw, cx, cy-16, 50, p)
        for side in [-1, 1]:
            pts = [(cx,cy-6),(cx+side*40,cy-36),(cx+side*56,cy-14),
                   (cx+side*34,cy+12),(cx+side*50,cy+22),(cx+side*18,cy+18),(cx,cy+16)]
            draw.polygon(pts, fill=p['dim'])
            draw.polygon(pts, outline=p['border'], width=1)
        for fx2 in [cx-6, cx+2]:
            draw.polygon([(fx2-3,cy+6),(fx2+3,cy+6),(fx2,cy+20)], fill=(228,218,208))
    elif rank == 'jack':
        knight_figure(draw, cx, cy, p, flaming=True)
    elif rank == 'queen':
        skull(draw, cx, cy-16, 50, p, crown=True)
        wc2 = tuple(int(c*0.28) for c in p['text'])
        draw.polygon([(cx-22,cy+6),(cx+22,cy+6),(cx+36,cy+36),(cx-36,cy+36)], fill=wc2)
        draw.polygon([(cx+36,cy+36),(cx+22,cy+6),(cx+46,cy+16)], fill=wc2)
        draw.polygon([(cx-36,cy+36),(cx-22,cy+6),(cx-46,cy+16)], fill=wc2)
    elif rank == 'king':
        dragon_head(draw, cx, cy, p, skeletal=True)


# ── Potions ───────────────────────────────────────────────────────────────────

def draw_potion(draw, cx, cy, p, rank):
    rv = RANK_VAL[rank]
    t  = (rv - 2) / 8.0
    fc2 = (int(190 + 65*t), int(85 - 57*t), int(105 - 50*t))
    glass_c = (168, 215, 225)
    fw = int(18 + t*20); fh = int(32 + t*22)
    nw = max(6, fw//3);  nh = int(8 + t*6)
    fill_frac = 0.28 + t*0.67
    liq_top = int(cy - fh//2 + fh*(1-fill_frac))
    if rv >= 7:
        for r in range(fw+20, fw+2, -4):
            gc2 = tuple(max(0,int(c*(r-fw-2)/18*0.28)) for c in fc2)
            draw.ellipse([cx-r,cy-r//2,cx+r,cy+r//2], fill=gc2)
    draw.ellipse([cx-fw,cy-fh//2,cx+fw,cy+fh//2], fill=glass_c)
    draw.ellipse([cx-fw+3,liq_top,cx+fw-3,cy+fh//2-3], fill=fc2)
    draw.line([(cx-fw+5,cy-fh//4),(cx-fw+5,cy+fh//4)], fill=(255,255,255), width=2)
    neck_top = cy - fh//2 - nh
    draw.rectangle([cx-nw,neck_top,cx+nw,cy-fh//2+2], fill=glass_c)
    if fill_frac > 0.82:
        draw.rectangle([cx-nw+2,neck_top+2,cx+nw-2,cy-fh//2], fill=fc2)
    draw.rectangle([cx-nw-2,neck_top-5,cx+nw+2,neck_top+2], fill=(155,95,45))
    draw.rectangle([cx-fw+3,cy-6,cx+fw-3,cy+6], fill=p['dim'])
    draw.rectangle([cx-fw+3,cy-6,cx+fw-3,cy+6], outline=p['border'], width=1)
    draw.line([(cx,cy-5),(cx,cy+5)], fill=p['text'], width=2)
    draw.line([(cx-5,cy),(cx+5,cy)], fill=p['text'], width=2)
    if rv >= 6:
        bc2 = tuple(min(255,c+65) for c in fc2)
        for bx2,by2 in [(cx-7,cy+8),(cx+9,cy-4),(cx+2,cy+14)]:
            draw.ellipse([bx2-3,by2-3,bx2+3,by2+3], fill=bc2)
    if rv == 10:
        cw2 = fw+10
        draw.polygon([(cx-cw2,cy+fh//2),(cx-fw,cy+fh//2),(cx-fw//2,cy+fh//2+16),
                      (cx+fw//2,cy+fh//2+16),(cx+fw,cy+fh//2),(cx+cw2,cy+fh//2)], fill=p['accent'])
        draw.ellipse([cx-cw2,cy+fh//2+10,cx+cw2,cy+fh//2+22], fill=p['accent'])


# ── Weapons ───────────────────────────────────────────────────────────────────

def draw_weapon(draw, cx, cy, p, rank):
    rv = RANK_VAL[rank]
    mc = p['accent']
    hc = (98, 68, 38)
    li = tuple(min(255,c+45) for c in mc)

    if rv == 2:
        draw.polygon([(cx,cy-48),(cx-5,cy-5),(cx+5,cy-5)], fill=mc)
        draw.rectangle([cx-13,cy-8,cx+13,cy-4], fill=mc)
        draw.rectangle([cx-4,cy-4,cx+4,cy+20], fill=hc)
        draw.ellipse([cx-6,cy+18,cx+6,cy+28], fill=mc)
        draw.line([(cx+2,cy-44),(cx-1,cy-18)], fill=p['dim'], width=2)
    elif rv == 3:
        draw.polygon([(cx,cy-52),(cx-6,cy-4),(cx+6,cy-4)], fill=mc)
        draw.polygon([(cx,cy-52),(cx-2,cy-12),(cx+2,cy-12)], fill=li)
        draw.rectangle([cx-13,cy-7,cx+13,cy-3], fill=mc)
        draw.rectangle([cx-4,cy-3,cx+4,cy+22], fill=hc)
        draw.ellipse([cx-6,cy+20,cx+6,cy+30], fill=mc)
    elif rv == 4:
        draw.polygon([(cx,cy-58),(cx-8,cy-4),(cx+8,cy-4)], fill=mc)
        draw.polygon([(cx,cy-58),(cx-2,cy-10),(cx+2,cy-10)], fill=li)
        draw.rectangle([cx-17,cy-8,cx+17,cy-3], fill=mc)
        draw.rectangle([cx-5,cy-3,cx+5,cy+22], fill=hc)
        draw.ellipse([cx-7,cy+20,cx+7,cy+30], fill=mc)
    elif rv == 5:
        draw.polygon([(cx,cy-62),(cx-11,cy-4),(cx+11,cy-4)], fill=mc)
        draw.polygon([(cx,cy-62),(cx-3,cy-10),(cx+3,cy-10)], fill=li)
        draw.rectangle([cx-20,cy-8,cx+20,cy-3], fill=mc)
        draw.rectangle([cx-5,cy-3,cx+5,cy+24], fill=hc)
        draw.ellipse([cx-8,cy+22,cx+8,cy+32], fill=mc)
    elif rv == 6:
        draw.line([(cx-6,cy-58),(cx+6,cy+28)], fill=hc, width=6)
        ay = cy-32
        draw.polygon([(cx,ay-20),(cx+42,ay-30),(cx+46,ay),(cx+42,ay+24),(cx,ay+14)], fill=mc)
        draw.line([(cx+42,ay-30),(cx+46,ay),(cx+42,ay+24)], fill=li, width=2)
        draw.polygon([(cx,ay-20),(cx-10,ay-10),(cx-10,ay+4),(cx,ay+14)], fill=mc)
    elif rv == 7:
        draw.rectangle([cx-4,cy-46,cx+4,cy+28], fill=hc)
        hh2 = cy-52
        draw.rectangle([cx-28,hh2,cx+28,hh2+22], fill=mc)
        draw.rectangle([cx-26,hh2+2,cx+26,hh2+6], fill=li)
        draw.line([(cx-28,hh2+11),(cx+28,hh2+11)], fill=li, width=2)
    elif rv == 8:
        draw.polygon([(cx,cy-65),(cx-10,cy-4),(cx+10,cy-4)], fill=mc)
        draw.polygon([(cx,cy-65),(cx-3,cy-10),(cx+3,cy-10)], fill=li)
        rc2 = p['glow']
        for ry in range(cy-58, cy-10, 11):
            draw.rectangle([cx-3,ry,cx+3,ry+7], fill=rc2)
        draw.rectangle([cx-20,cy-8,cx+20,cy-3], fill=mc)
        draw.rectangle([cx-5,cy-3,cx+5,cy+24], fill=hc)
        draw.ellipse([cx-7,cy+22,cx+7,cy+32], fill=mc)
    elif rv == 9:
        bow_pts = []
        for a in range(-75, 76, 8):
            rad = math.radians(a)
            bow_pts.append((cx-28+int(12*math.cos(rad)), cy+int(58*math.sin(rad))))
        draw.line(bow_pts, fill=mc, width=4)
        draw.line([(cx-16,cy-58),(cx-5,cy),(cx-16,cy+58)], fill=(200,195,175), width=1)
        ar = cx+55; al = cx-5
        draw.line([(al,cy),(ar,cy)], fill=p['text'], width=2)
        draw.polygon([(ar,cy-4),(ar,cy+4),(ar+10,cy)], fill=p['text'])
        draw.polygon([(al,cy),(al-8,cy-6),(al-5,cy)], fill=(95,72,50))
        draw.polygon([(al,cy),(al-8,cy+6),(al-5,cy)], fill=(72,52,35))
    elif rv == 10:
        dm = (75, 55, 115)
        gc2 = p['glow']
        for r in range(16, 5, -2):
            draw.polygon([(cx,cy-68),(cx-r,cy-5),(cx+r,cy-5)], fill=tuple(int(c*r/16*0.38) for c in gc2))
        draw.polygon([(cx,cy-68),(cx-11,cy-5),(cx+11,cy-5)], fill=dm)
        for i2,vy in enumerate(range(cy-62, cy-10, 10)):
            draw.line([(cx-5+i2%2,vy),(cx+5-i2%2,vy+7)], fill=gc2, width=1)
        draw.rectangle([cx-23,cy-9,cx+23,cy-4], fill=dm)
        draw.ellipse([cx-27,cy-13,cx-19,cy+1], fill=dm)
        draw.ellipse([cx+19, cy-13,cx+27, cy+1], fill=dm)
        draw.ellipse([cx-5,cy-11,cx+5,cy-2], fill=gc2)
        draw.rectangle([cx-5,cy-4,cx+5,cy+24], fill=(58,38,78))
        draw.polygon([(cx,cy+22),(cx-8,cy+30),(cx,cy+36),(cx+8,cy+30)], fill=dm)


# ── Main ──────────────────────────────────────────────────────────────────────

def make_card(suit, rank, fonts):
    p = P[suit]
    d = SVGCanvas(W, H)
    draw_bg(d, p)
    cx, cy = 75, 97
    if   suit == 'clubs':    draw_clubs(d, cx, cy, p, rank)
    elif suit == 'spades':   draw_spades(d, cx, cy, p, rank)
    elif suit == 'hearts':   draw_potion(d, cx, cy, p, rank)
    elif suit == 'diamonds': draw_weapon(d, cx, cy, p, rank)
    draw_pip(d, rank, suit, p, fonts)
    draw_name_banner(d, NAMES[(suit, rank)], p, fonts[3])
    return d.to_svg()


def main():
    global _GLYPHS
    print("Loading font…")
    _GLYPHS = GlyphRenderer(FONT_PATH)
    fonts = load_fonts()
    cards = (
        [(s, r) for s in ('clubs','spades')
         for r in ('ace','2','3','4','5','6','7','8','9','10','jack','queen','king')] +
        [(s, r) for s in ('hearts','diamonds')
         for r in ('2','3','4','5','6','7','8','9','10')]
    )
    for suit, rank in cards:
        svg = make_card(suit, rank, fonts)
        fname = f"{rank}_{suit}"

        svg_path = os.path.join(OUT, f"{fname}.svg")
        with open(svg_path, 'w', encoding='utf-8') as f:
            f.write(svg)

        json_path = os.path.join(JSON_DIR, f"{fname}.json")
        if os.path.exists(json_path):
            with open(json_path) as f:
                data = json.load(f)
            data['front_image'] = f"{fname}.svg"
            with open(json_path, 'w') as f:
                json.dump(data, f, indent=2)

        print(f"  {fname}.svg")
    print(f"\nDone — {len(cards)} cards.")


if __name__ == '__main__':
    main()
