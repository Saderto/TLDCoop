"""Pack textures for the big catalogue (0.2.0): each product's label is put together from its own mix of layout, palette, picture and type,
no two alike (checked). Also the shared cup textures (one per vessel and drink colour).
python3 labels2.py <out_dir> [ids... | cups]   -> <out_dir>/textures/<Id>.png, textures/cup_<vessel>_<liq>.png"""
import sys, os, json, math, random, zlib
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit import *
import labels as L1
from labels import rgba, shade, info_block, brew_lines, cup_steam
from catalog import NEW, ALL, LIQ

OUT = sys.argv[1]
LAY = json.load(open(os.path.join(OUT, 'layout.json')))


def hx(s, a=255):
    s = s.lstrip('#'); return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


PAPER, INK = hx('F4EAD2'), hx('2A1A14')
PALS = {k: tuple(hx(x) for x in v.split()) for k, v in {
    'crimson': '8E1B1F F4E6C8 D9B25F', 'navy': '16284A F1E7CF D6B260', 'forest': '1F4A32 F3EBD3 D8B45A', 'teal': '0F5D63 F2EDDC F0B24A',
    'mustard': 'D9A521 2B1B10 7A1E14', 'cream': 'EFE3C4 3A2416 A3271F', 'black': '161413 EADFC6 C9A24B', 'kraft': 'B8946A 2C1C10 7B1F1A',
    'sky': '7FB4D6 14233C F3E9CF', 'mint': 'A9D6BC 17402E C0392B', 'rose': 'E7B7B5 5A1E2A 2F5D46', 'orange': 'D9662B FFF1D6 3A1E12',
    'plum': '4B2145 F1E3D0 D9A84E', 'olive': '6B6B2A F5EFD8 E0B64A', 'brick': 'A4432A F6E8CF F2C14E', 'ivory': 'F5F0E1 1E3A5F B8862B',
    'chocolate': '4A2A1B F3E2C2 E0A84B', 'turquoise': '2FA7A0 FFFFFF F6D365', 'yellow': 'F2C230 1D1D1B C62828', 'white': 'F6F4EE 1B1B1B C8102E',
    'grey': '5E6369 F2F2F0 F2B134', 'burgundy': '5E1224 F0DDBF C79A3B', 'jade': '3C7A5B F4F1DE F0C987', 'indigo': '2B2F77 F5E9D3 F08A5D',
    'sand': 'D8C39A 3B2A1A 1F5673', 'coral': 'E8674E FFF4E0 1F3B4D', 'lime': '9BBF3B 1E2A12 FFFFFF', 'ice': 'DCEAF2 1C3D5A C0392B',
    'gold': 'C9A24B 231708 6E1414', 'pine': '12332B EADBB6 C9772B', 'violet': '6A4C93 FAF3DD F2C14E', 'rust': '7A3B1D F2E2C4 8FB339',
    'peach': 'F3C9A5 4A1F1A 2D6A6A', 'steel': '27445C EAF0F2 E36414', 'red': 'C8102E FFFFFF F4D35E', 'emerald': '0B6E4F FDF6E3 F2B705',
}.items()}
# pressed tea wrappers: paper, ink, seal
WRAPS = [tuple(hx(x) for x in v.split()) for v in (
    'F3EEDD 1F3D2B B3261E', 'EFE6CC 7A1712 7A1712', 'E9D9A6 3A2A12 B3261E', 'C9A877 2B1A0E 8C1C13', '8C1C13 F0D58A F0D58A', '1B1B1B D9B25F B3261E',
    '1F4A32 F1E3B5 F1E3B5', '22366B F2E8CF D9B25F', 'F3EEDD 22366B B3261E', 'EFE6CC 4A2A6A B3261E', 'F5F1E6 151515 B3261E', 'E6CFA0 1F4A32 7A1712',
    'D8C8A0 6B3A12 B3261E', 'F1E9D8 0F5D63 B3261E', '5E1224 F0DDBF F0DDBF', 'E8DCC0 3A2416 1F4A32')]
FONTS = [('playfair-display-900', 'playfair-display-900'), ('abril-fatface-400', 'yeseva-one-400'), ('cinzel-900', 'forum-400'), ('lobster-400', 'lobster-400'),
         ('pacifico-400', 'pacifico-400'), ('oswald-700', 'oswald-700'), ('bebas-neue-400', 'oswald-600'), ('yeseva-one-400', 'yeseva-one-400'),
         ('prata-400', 'prata-400'), ('alegreya-sc-800', 'alegreya-sc-800'), ('kelly-slab-400', 'kelly-slab-400'), ('russo-one-400', 'russo-one-400'),
         ('unbounded-700', 'unbounded-700'), ('old-standard-tt-700', 'old-standard-tt-700'), ('pt-serif-700', 'pt-serif-700'),
         ('philosopher-700', 'philosopher-700'), ('amatic-sc-700', 'amatic-sc-700'), ('marck-script-400', 'marck-script-400'),
         ('great-vibes-400', 'great-vibes-400'), ('cinzel-decorative-700', 'forum-400'), ('ruslan-display-400', 'ruslan-display-400'),
         ('montserrat-800', 'montserrat-800'), ('tenor-sans-400', 'tenor-sans-400'), ('alice-400', 'alice-400'), ('playfair-display-sc-700', 'playfair-display-sc-700')]
SMALL = ['oswald-400', 'pt-serif-400', 'montserrat-500', 'tenor-sans-400', 'alice-400']


# ------------------------------------------------------------------ pictures: m(c, x, y, s, col, col2, bg) inside a circle of radius s
def ell(c, x, y, rx, ry, ang, fill):
    a = math.radians(ang)
    c.poly([(x + rx * math.cos(t) * math.cos(a) - ry * math.sin(t) * math.sin(a), y + rx * math.cos(t) * math.sin(a) + ry * math.sin(t) * math.cos(a))
            for t in np.linspace(0, 2 * math.pi, 40)], fill=fill)


def m_bean(c, x, y, s, a, b, bg):
    ell(c, x, y, s * .62, s * .9, 25, a)
    t = np.linspace(-1, 1, 20); r = math.radians(25)
    pts = [(x + (math.sin(v * 2.4) * s * .14) * math.cos(r) - v * s * .8 * math.sin(r), y + (math.sin(v * 2.4) * s * .14) * math.sin(r) + v * s * .8 * math.cos(r)) for v in t]
    c.line(pts, bg, s * .09)


def m_beans3(c, x, y, s, a, b, bg):
    m_bean(c, x - s * .5, y + s * .25, s * .48, a, b, bg); m_bean(c, x + s * .5, y + s * .25, s * .48, b, a, bg); m_bean(c, x, y - s * .4, s * .5, a, b, bg)


def m_sprig(c, x, y, s, a, b, bg):
    c.line([(x, y + s * .9), (x, y - s * .3)], a, s * .06)
    for ang, yy, L in ((-150, .5, .8), (-30, .5, .8), (-140, 0, .7), (-40, 0, .7), (-90, -.3, .65)):
        leaf(c, x, y + s * yy, s * L, s * L * .42, ang, a, bg)


def m_tealeaf(c, x, y, s, a, b, bg):
    leaf(c, x, y + s * .5, s * 1.1, s * .5, -140, a, bg); leaf(c, x, y + s * .5, s * 1.1, s * .5, -40, a, bg); leaf(c, x, y + s * .5, s * 1.3, s * .3, -90, b, bg)


def m_flower(c, x, y, s, a, b, bg): flower(c, x, y, s * .95, 8, a, b)
def m_daisy(c, x, y, s, a, b, bg): flower(c, x, y, s * .95, 14, a, b, petal_w=.3)
def m_blossom(c, x, y, s, a, b, bg):
    for i in range(5):
        t = math.radians(-90 + 72 * i); c.circle(x + s * .5 * math.cos(t), y + s * .5 * math.sin(t), s * .42, fill=a)
    c.circle(x, y, s * .22, fill=b)


def m_rose(c, x, y, s, a, b, bg):
    c.circle(x, y, s * .85, fill=a)
    for k, r in enumerate((.68, .5, .32, .15)):
        c.arc(x + (k % 2 - .5) * s * .1, y, s * r, 20 + 70 * k, 300 + 70 * k, bg, s * .06)
    leaf(c, x - s * .5, y + s * .6, s * .7, s * .35, 150, b); leaf(c, x + s * .5, y + s * .6, s * .7, s * .35, 30, b)


def m_lotus(c, x, y, s, a, b, bg):
    for ang, L in ((-90, 1.3), (-125, 1.15), (-55, 1.15), (-160, .95), (-20, .95)):
        leaf(c, x, y + s * .45, s * L, s * L * .42, ang, a if abs(ang + 90) != 35 else b)
    for k in (-1, 1): c.line([(x + k * s * .2, y + s * .7), (x + k * s * .9, y + s * .7)], b, s * .05)


def m_mountains(c, x, y, s, a, b, bg):
    mountains(c, x - s, x + s, y + s * .6, [(x - s * .45, y - s * .2), (x - s * .15, y + s * .25), (x + s * .3, y - s * .7), (x + s * .7, y + s * .2)], a, b)


def m_volcano(c, x, y, s, a, b, bg):
    c.poly([(x - s, y + s * .7), (x - s * .2, y - s * .25), (x + s * .2, y - s * .25), (x + s, y + s * .7)], fill=a)
    c.poly([(x - s * .2, y - s * .25), (x + s * .2, y - s * .25), (x + s * .08, y + s * .05), (x - s * .02, y - s * .1), (x - s * .1, y + s * .1)], fill=b)
    for dx, dy, r in ((0, -.5, .16), (.15, -.72, .2), (-.05, -.95, .13)): c.circle(x + s * dx, y + s * dy, s * r, fill=b)


def m_sun(c, x, y, s, a, b, bg):
    sun_rays(c, x, y, s * .6, s, 16, a, None, 0, 360); c.circle(x, y, s * .48, fill=a); c.circle(x, y, s * .36, fill=b)


def m_rising(c, x, y, s, a, b, bg):
    sun_rays(c, x, y + s * .4, s * .55, s * 1.1, 10, a, None, 180, 360); c.poly([(x + s * .5 * math.cos(t), y + s * .4 + s * .5 * math.sin(t)) for t in np.linspace(math.pi, 2 * math.pi, 30)], fill=a)
    c.line([(x - s, y + s * .46), (x + s, y + s * .46)], b, s * .08)


def m_moon(c, x, y, s, a, b, bg):
    c.circle(x, y, s * .85, fill=a); c.circle(x + s * .35, y - s * .15, s * .7, fill=bg)
    for dx, dy, r in ((.45, -.3, .1), (.7, .2, .07), (.3, .3, .05)): star(c, x + s * dx, y + s * dy, s * r * 1.6, b, 4, .4)


def m_wave(c, x, y, s, a, b, bg):
    for k in range(3):
        pts = [(x - s + 2 * s * i / 40, y + (k - 1) * s * .5 + math.sin(i / 40 * 4 * math.pi + k) * s * .16) for i in range(41)]
        c.line(pts, a if k != 1 else b, s * .1)


def m_bird(c, x, y, s, a, b, bg):
    ell(c, x, y, s * .5, s * .16, -10, a)
    c.poly([(x - s * .1, y), (x - s * .5, y - s * .9), (x + s * .25, y - s * .1)], fill=a); c.poly([(x, y), (x + s * .2, y + s * .8), (x + s * .3, y)], fill=b)
    c.line([(x + s * .4, y - s * .08), (x + s * .95, y - s * .3)], a, s * .07); c.poly([(x - s * .45, y + s * .05), (x - s * 1.0, y + s * .25), (x - s * .5, y - s * .08)], fill=a)


def m_pagoda(c, x, y, s, a, b, bg):
    for k in range(3):
        w = s * (.95 - k * .22); yy = y + s * (.55 - k * .5)
        c.poly([(x - w, yy), (x - w * .55, yy - s * .22), (x + w * .55, yy - s * .22), (x + w, yy)], fill=a)
        c.rect(x - w * .5, yy, x + w * .5, yy + s * .28, fill=b)
    c.line([(x, y - s * .68), (x, y - s)], a, s * .06)


def m_torii(c, x, y, s, a, b, bg):
    c.rect(x - s * .62, y - s * .5, x - s * .48, y + s * .8, fill=a); c.rect(x + s * .48, y - s * .5, x + s * .62, y + s * .8, fill=a)
    c.poly([(x - s, y - s * .8), (x + s, y - s * .8), (x + s * .88, y - s * .58), (x - s * .88, y - s * .58)], fill=a); c.rect(x - s * .75, y - s * .35, x + s * .75, y - s * .22, fill=a)
    c.circle(x, y + s * .25, s * .25, fill=b)


def m_fan(c, x, y, s, a, b, bg):
    c.poly([(x, y + s * .7)] + [(x + s * math.cos(t), y + s * .7 + s * 1.3 * math.sin(t)) for t in np.linspace(math.radians(-150), math.radians(-30), 24)], fill=a)
    for t in np.linspace(math.radians(-150), math.radians(-30), 9): c.line([(x, y + s * .7), (x + s * math.cos(t), y + s * .7 + s * 1.3 * math.sin(t))], bg, s * .03)
    c.circle(x, y - s * .15, s * .2, fill=b)


def m_bamboo(c, x, y, s, a, b, bg):
    for dx, h0 in ((-.25, 1.0), (.2, .8)):
        for k in range(3):
            y0 = y + s * .9 - k * s * h0 * .62
            c.rect(x + s * dx - s * .09, y0 - s * h0 * .56, x + s * dx + s * .09, y0, fill=a, r=s * .04)
    for ang, px_, py_ in ((-20, .25, -.5), (-50, .25, -.5), (200, -.3, -.2), (160, -.3, .1), (10, .25, .1)):
        leaf(c, x + s * px_, y + s * py_, s * .65, s * .2, ang, b)


def m_cup(c, x, y, s, a, b, bg): cup_steam(c, x, y + s * .3, s * 1.2, a, b, a)


def m_teapot(c, x, y, s, a, b, bg):
    c.ellipse(x - s * .62, y - s * .3, x + s * .62, y + s * .7, fill=a)
    c.poly([(x - s * .5, y + s * .1), (x - s, y - s * .35), (x - s * .85, y - s * .4), (x - s * .45, y - s * .1)], fill=a)
    c.arc(x + s * .62, y + s * .15, s * .32, -100, 100, a, s * .1)
    c.ellipse(x - s * .35, y - s * .42, x + s * .35, y - s * .18, fill=b); c.circle(x, y - s * .5, s * .1, fill=a)
    steam(c, x - s * .95, y - s * .45, s * .45, a, s * .05, 2, s * .16)


def m_cezve(c, x, y, s, a, b, bg):
    c.poly([(x - s * .55, y + s * .75), (x - s * .32, y - s * .35), (x - s * .5, y - s * .55), (x + s * .4, y - s * .55), (x + s * .3, y - s * .35), (x + s * .5, y + s * .75)], fill=a)
    c.line([(x + s * .32, y - s * .3), (x + s * 1.0, y - s * .75)], a, s * .1); c.rect(x - s * .5, y + s * .3, x + s * .46, y + s * .42, fill=b)
    steam(c, x - s * .05, y - s * .6, s * .4, a, s * .05, 2, s * .2)


def m_ship(c, x, y, s, a, b, bg):
    c.poly([(x - s * .9, y + s * .35), (x + s * .9, y + s * .35), (x + s * .6, y + s * .75), (x - s * .6, y + s * .75)], fill=a)
    for dx, h in ((-.3, 1.1), (.3, .9)):
        c.line([(x + s * dx, y + s * .35), (x + s * dx, y + s * .35 - s * h)], a, s * .05)
        c.poly([(x + s * dx + s * .05, y + s * .25), (x + s * dx + s * .05, y + s * .4 - s * h), (x + s * dx + s * .5, y + s * .25)], fill=b)
        c.poly([(x + s * dx - s * .05, y + s * .25), (x + s * dx - s * .05, y + s * .55 - s * h), (x + s * dx - s * .35, y + s * .25)], fill=a)


def m_anchor(c, x, y, s, a, b, bg):
    c.circle(x, y - s * .72, s * .16, outline=a, width=s * .08); c.line([(x, y - s * .56), (x, y + s * .8)], a, s * .11); c.line([(x - s * .4, y - s * .3), (x + s * .4, y - s * .3)], a, s * .09)
    c.arc(x, y + s * .15, s * .65, 10, 170, a, s * .11)
    for k in (-1, 1): c.poly([(x + k * s * .8, y + s * .05), (x + k * s * .5, y + s * .3), (x + k * s * .78, y + s * .42)], fill=b)


def m_compass(c, x, y, s, a, b, bg):
    c.circle(x, y, s * .92, outline=a, width=s * .06); star(c, x, y, s * .62, b, 4, .3, -45); star(c, x, y, s * .88, a, 4, .22, -90); c.circle(x, y, s * .08, fill=bg)


def m_star(c, x, y, s, a, b, bg): star(c, x, y, s, a, 5, .45); star(c, x, y, s * .45, b, 5, .45)
def m_crown(c, x, y, s, a, b, bg):
    c.poly([(x - s * .8, y + s * .4), (x - s * .9, y - s * .5), (x - s * .4, y), (x, y - s * .7), (x + s * .4, y), (x + s * .9, y - s * .5), (x + s * .8, y + s * .4)], fill=a)
    c.rect(x - s * .8, y + s * .45, x + s * .8, y + s * .65, fill=a)
    for dx, dy in ((-.9, -.5), (0, -.7), (.9, -.5)): c.circle(x + s * dx, y + s * dy, s * .1, fill=b)


def m_palm(c, x, y, s, a, b, bg):
    c.line([(x + s * .15, y + s * .9), (x + s * .05, y + s * .2), (x - s * .1, y - s * .35)], a, s * .1)
    for ang in (-170, -135, -95, -50, -15, 20, 200): leaf(c, x - s * .1, y - s * .35, s * .85, s * .22, ang, b if ang in (-95, 20) else a)


def m_ear(c, x, y, s, a, b, bg):
    c.line([(x, y + s * .95), (x, y - s * .2)], a, s * .05)
    for k in range(5):
        yy = y + s * .35 - k * s * .26
        leaf(c, x, yy, s * .42, s * .2, -135, a); leaf(c, x, yy, s * .42, s * .2, -45, a)
        c.line([(x - s * .28, yy - s * .28), (x - s * .45, yy - s * .7)], b, s * .025); c.line([(x + s * .28, yy - s * .28), (x + s * .45, yy - s * .7)], b, s * .025)
    leaf(c, x, y - s * .65, s * .4, s * .2, -90, a)


def m_cacao(c, x, y, s, a, b, bg):
    ell(c, x, y, s * .5, s * .95, 20, a)
    for k in (-.25, 0, .25):
        r = math.radians(20); pts = [(x + (k * s * math.cos(v * 1.4)) * math.cos(r) - v * s * .85 * math.sin(r), y + (k * s * math.cos(v * 1.4)) * math.sin(r) + v * s * .85 * math.cos(r)) for v in np.linspace(-1, 1, 14)]
        c.line(pts, b, s * .05)
    leaf(c, x + s * .2, y - s * .8, s * .7, s * .3, -20, b)


def m_mill(c, x, y, s, a, b, bg):
    c.poly([(x - s * .32, y + s * .9), (x - s * .18, y - s * .1), (x + s * .18, y - s * .1), (x + s * .32, y + s * .9)], fill=a)
    c.poly([(x - s * .2, y - s * .1), (x, y - s * .35), (x + s * .2, y - s * .1)], fill=b)
    for k in range(4):
        t = math.radians(30 + 90 * k); ex, ey = x + s * .95 * math.cos(t), y - s * .15 + s * .95 * math.sin(t)
        c.line([(x, y - s * .15), (ex, ey)], a, s * .05)
        n = (-math.sin(t), math.cos(t))
        c.poly([(x + s * .3 * math.cos(t), y - s * .15 + s * .3 * math.sin(t)), (ex, ey), (ex + n[0] * s * .2, ey + n[1] * s * .2), (x + s * .3 * math.cos(t) + n[0] * s * .2, y - s * .15 + s * .3 * math.sin(t) + n[1] * s * .2)], fill=b)


def m_yurt(c, x, y, s, a, b, bg):
    c.rect(x - s * .8, y, x + s * .8, y + s * .7, fill=a); c.poly([(x - s * .9, y), (x, y - s * .75), (x + s * .9, y)], fill=a)
    c.rect(x - s * .16, y + s * .15, x + s * .16, y + s * .7, fill=b); zigzag(c, x - s * .8, y + s * .05, x + s * .8, s * .12, b, s * .2, s * .03)
    steam(c, x, y - s * .75, s * .35, a, s * .04, 1, s * .1)


def m_bee(c, x, y, s, a, b, bg):
    ell(c, x, y + s * .1, s * .6, s * .38, 0, a)
    for k in (-.2, .12): c.rect(x + s * k, y - s * .27, x + s * (k + .12), y + s * .47, fill=bg)
    ell(c, x - s * .15, y - s * .5, s * .4, s * .2, -30, b); ell(c, x + s * .25, y - s * .5, s * .4, s * .2, 20, b); c.circle(x - s * .65, y + s * .1, s * .2, fill=a)


def m_moth(c, x, y, s, a, b, bg):
    for k in (-1, 1):
        ell(c, x + k * s * .48, y - s * .25, s * .5, s * .38, k * 25, a); ell(c, x + k * s * .38, y + s * .35, s * .36, s * .28, -k * 25, b)
        c.circle(x + k * s * .5, y - s * .28, s * .12, fill=bg)
    ell(c, x, y, s * .08, s * .55, 0, a)


def m_tree(c, x, y, s, a, b, bg):
    c.poly([(x - s * .1, y + s * .9), (x - s * .06, y), (x + s * .06, y), (x + s * .1, y + s * .9)], fill=b)
    for dx, dy, r in ((0, -.45, .5), (-.4, -.1, .38), (.4, -.1, .38), (-.2, -.65, .3), (.25, -.62, .3)): c.circle(x + s * dx, y + s * dy, s * r, fill=a)


def m_pine(c, x, y, s, a, b, bg):
    for k in range(3): c.poly([(x - s * (.75 - k * .18), y + s * (.5 - k * .45)), (x, y + s * (-.15 - k * .45)), (x + s * (.75 - k * .18), y + s * (.5 - k * .45))], fill=a)
    c.rect(x - s * .08, y + s * .5, x + s * .08, y + s * .9, fill=b)


def m_kettle(c, x, y, s, a, b, bg):
    c.ellipse(x - s * .55, y - s * .35, x + s * .55, y + s * .45, fill=a); c.arc(x, y - s * .3, s * .5, 190, 350, a, s * .06)
    for dx in (-.3, 0, .3): leaf(c, x + s * dx, y + s * .95, s * .5, s * .26, -90, b)
    c.line([(x - s * .8, y + s * .95), (x + s * .8, y + s * .95)], a, s * .08)


def m_snow(c, x, y, s, a, b, bg):
    for k in range(6):
        t = math.radians(60 * k); ex, ey = x + s * math.cos(t), y + s * math.sin(t)
        c.line([(x, y), (ex, ey)], a, s * .07)
        for f in (.55, .8):
            for d in (-1, 1):
                t2 = t + d * math.radians(50); px_, py_ = x + s * f * math.cos(t), y + s * f * math.sin(t)
                c.line([(px_, py_), (px_ + s * .22 * math.cos(t2), py_ + s * .22 * math.sin(t2))], a, s * .05)
    c.circle(x, y, s * .12, fill=b)


def m_citrus(c, x, y, s, a, b, bg):
    c.circle(x, y + s * .1, s * .78, fill=a); c.circle(x, y + s * .1, s * .62, outline=bg, width=s * .04)
    for k in range(8):
        t = math.radians(45 * k); c.line([(x, y + s * .1), (x + s * .6 * math.cos(t), y + s * .1 + s * .6 * math.sin(t))], bg, s * .035)
    leaf(c, x + s * .1, y - s * .65, s * .7, s * .3, -25, b)


def m_drop(c, x, y, s, a, b, bg):
    c.poly([(x, y - s * .95)] + [(x + s * .6 * math.cos(t), y + s * .25 + s * .6 * math.sin(t)) for t in np.linspace(math.radians(-30), math.radians(210), 30)], fill=a)
    c.arc(x, y + s * .25, s * .38, 20, 100, b, s * .08)


def m_diamond(c, x, y, s, a, b, bg):
    for i in range(-1, 2):
        for j in range(-1, 2):
            if abs(i) + abs(j) > 1: continue
            cx, cy = x + i * s * .62, y + j * s * .62
            c.poly([(cx, cy - s * .3), (cx + s * .3, cy), (cx, cy + s * .3), (cx - s * .3, cy)], fill=a if (i + j) % 2 == 0 else b)


def m_camel(c, x, y, s, a, b, bg):
    ell(c, x - s * .05, y + s * .05, s * .6, s * .28, 0, a); ell(c, x - s * .1, y - s * .28, s * .22, s * .25, 0, a)
    c.line([(x + s * .45, y - s * .05), (x + s * .7, y - s * .6)], a, s * .14); ell(c, x + s * .82, y - s * .62, s * .2, s * .1, 15, a)
    for dx in (-.45, -.25, .25, .42): c.line([(x + s * dx, y + s * .2), (x + s * dx, y + s * .85)], a, s * .08)
    c.line([(x - s, y + s * .9), (x + s, y + s * .9)], b, s * .05)


def m_elephant(c, x, y, s, a, b, bg):
    """side view, walking right: a round back, two stout legs with the belly arched between them, a big ear, a curled trunk, a tusk"""
    ell(c, x - s * .12, y - s * .05, s * .62, s * .44, 0, a)                         # body
    c.circle(x + s * .5, y - s * .2, s * .33, fill=a)                                 # head
    for x0_, x1_ in ((-.66, -.3), (.02, .36)):                                        # legs, wider at the foot
        c.poly([(x + s * (x0_ + .03), y + s * .1), (x + s * (x1_ - .03), y + s * .1), (x + s * x1_, y + s * .74), (x + s * x0_, y + s * .74)], fill=a)
        c.rect(x + s * (x0_ - .02), y + s * .68, x + s * (x1_ + .02), y + s * .78, fill=a, r=s * .04)
    pts = [(x + s * (.74 + .1 * math.sin(t * 2.6)), y - s * .1 + s * .72 * t) for t in [i / 12 for i in range(11)]]
    pts += [(x + s * .86, y + s * .56), (x + s * .98, y + s * .5)]                   # the trunk curls up at the tip
    c.line(pts, a, s * .15)
    c.poly([(x + s * .66, y + s * .02), (x + s * .98, y + s * .2), (x + s * .68, y + s * .13)], fill=b)   # tusk
    ell(c, x + s * .3, y - s * .2, s * .2, s * .3, 12, b); ell(c, x + s * .3, y - s * .2, s * .13, s * .22, 12, a)   # ear, an outline
    c.circle(x + s * .6, y - s * .3, s * .035, fill=bg)                               # eye
    c.line([(x - s * .72, y - s * .15), (x - s * .82, y + s * .2)], a, s * .05)      # tail


def m_horse(c, x, y, s, a, b, bg):
    ell(c, x - s * .05, y, s * .58, s * .26, 0, a)
    c.poly([(x + s * .35, y - s * .15), (x + s * .62, y - s * .75), (x + s * .8, y - s * .7), (x + s * .55, y + s * .05)], fill=a); ell(c, x + s * .82, y - s * .68, s * .22, s * .1, 25, a)
    for dx, k in ((-.5, -.1), (-.3, .08), (.25, -.08), (.42, .12)): c.line([(x + s * dx, y + s * .15), (x + s * (dx + k), y + s * .85)], a, s * .07)
    c.poly([(x - s * .6, y - s * .1), (x - s * .95, y + s * .3), (x - s * .6, y + s * .05)], fill=b); c.rect(x - s * .25, y - s * .3, x + s * .15, y - s * .18, fill=b)


MOTIFS = {k[2:]: v for k, v in list(globals().items()) if k.startswith('m_')}
POOL = {
    'tea': ['tealeaf', 'sprig', 'teapot', 'ship', 'mountains', 'sun', 'compass', 'crown', 'elephant', 'rising', 'camel', 'star', 'diamond', 'tree'],
    'east': ['pagoda', 'bird', 'wave', 'bamboo', 'fan', 'torii', 'moon', 'lotus', 'blossom', 'mountains', 'tealeaf', 'pine', 'rising', 'tree', 'horse'],
    'herb': ['flower', 'daisy', 'sprig', 'blossom', 'bee', 'sun', 'rose', 'tealeaf', 'moth', 'tree', 'pine', 'lotus', 'drop'],
    'coffee': ['bean', 'beans3', 'cup', 'volcano', 'palm', 'ship', 'sun', 'mountains', 'compass', 'cezve', 'star', 'anchor', 'bird', 'rising', 'kettle', 'crown', 'diamond', 'tree', 'moon', 'camel', 'elephant'],
    'drink': ['ear', 'cup', 'cacao', 'crown', 'star', 'mill', 'anchor', 'yurt', 'bee', 'sun', 'kettle', 'drop', 'diamond', 'tree', 'citrus'],
}
HINT = dict(TheVeryOne='elephant', SaharaCamel='camel', CopperCezve='cezve', BluePercolator='kettle', BirchMushroom='tree', TaigaBlend='pine', OrchardCider='tree', GingerLemon='citrus', LadyOrange='citrus', MochaJava='ship', ChristmasMarket='star', QuickCocoa='cup', SilverLabel='cacao',
            ThreeVolcanoes='volcano', OldDock='anchor', DutchMill='mill', GoldenAnchor='anchor', NightCivet='moon', ParadiseBird='bird', BlueMoth='moth',
            SheafBarley='ear', MillersRye='ear', PastorsMalt='ear', BogatyrOat='ear', BeeBalm='bee', CapeBee='bee', FairSbiten='drop', GoheungYuja='citrus',
            GreenTangerine='citrus', BuckthornNoon='citrus', PeakLemonTea='citrus', DonaCanela='cacao', BatangasTablea='cacao', CacaoShell='cacao',
            Churreria='cup', SteppeSuutei='yurt', LhasaPoCha='mountains', ElephantPass='elephant', ElephantBean='elephant', PortMokha='camel',
            GoldenDallah='cezve', Kafenio='cezve', CampfireCoffee='kettle', FjordKokekaffe='kettle', PineSmoke='pine', MoonWhite='moon', Moonlight='moon',
            JasminePearl='blossom', HotayLotus='lotus', PingyinRose='rose', RoseNest='rose', AutumnChrys='daisy', ChrysCake='daisy', SakuraSalt='blossom',
            BambooJoint='bamboo', TeaRoadBrick='horse', SteppeBrick='horse', KangBrick='mountains', MengkuSnow='mountains', IceIsland='snow', BlueRidge='mountains',
            SantosWharf='ship', MaunaKona='volcano', SaigonPhin='palm', SierraMaestra='palm', ExileIsland='ship', VictoriaShore='wave', XiguiBank='wave',
            ThousandHills='mountains', YellowMountain='mountains', BlueHills='mountains', MonkeyChief='tree', OldTreeYiwu='tree', LoneBush='tree',
            MandhelingTiger='palm', HazelGrove='tree', JulyLinden='tree', YouleDrum='sun', DoubleKettle='teapot', BatumiBouquet='tealeaf', SpringWell='tealeaf',
            ThreeTwigs='sprig', SparrowTongue='bird', MandarinDucks='bird', ShizuokaMorning='rising', AssamDawn='rising', KyotoEmbers='torii', GreenFroth='fan',
            JadeDew='drop', AmazonEye='palm', KelpTea='wave', ColdSencha='wave', PearlBean='bean', GoldGranule='cup', FoamCappuccino='cup', RestStop='kettle',
            HeatherMoor='flower', MeadowQueen='daisy', JuneElder='blossom', GoldenDaylily='lotus', DragonPearl='wave', MaghrebWhisper='camel', SaffronKahwa='flower',
            GoldenBuckwheat='ear', BarleySummer='ear', GangwonCob='ear', TarahumaraPinole='ear', Courland='crown', ViennaSalon='crown', ViennaFig='drop',
            QuietNight='moon', Nightcap='moon', NightWatchman='moon', SouthernSlope='sun', SiamSunset='elephant', Savanna='tree', CyprusGold='tree',
            DateStone='palm', TropicStalk='palm', ScarletRobe='mountains', CliffCinnamon='mountains', HighMist='mountains', FrozenPeak='snow')


RUS = set("""ColchisTile SteppeBrick KoporyeRound HankouTile BatumiBouquet SouthernSlope PassThyme MeadowOregano BuckthornNoon DachaCurrant AltaiBergenia SayanWing
Cinquefoil MeadowQueen JulyLinden BogatyrOat FairSbiten CranberryKissel PeoplesBlend TheVeryOne TeaNo36 TaigaBlend BirchMushroom ScarletHip SilverLabel CowAndBean BlueRoot""".split())
TAGS = {'pressed': ['Old Tree Leaf', 'Spring Harvest', 'Mountain Leaf', 'Stone Pressed', 'Sun Dried Leaf', 'Aged in Clay', 'Tea Horse Road', 'Hand Pressed', 'First Flush Leaf'],
        'tea': ['Est. #', 'Selected Leaf', 'Finest Quality', 'Hand Picked', 'Since #', 'Garden Fresh', 'Purveyors of Tea', 'Whole Leaf', 'Estate Grown', 'Family Blend'],
        'coffee': ['Est. #', 'Slow Roasted', 'Since #', 'Small Batch', 'Roasted Fresh', 'Rich & Smooth', 'Master Roasters', 'Single Origin', 'High Grown', 'Hand Sorted'],
        'drink': ['Est. #', 'Family Recipe', 'Since #', 'Just Add Water', 'The Original', 'Finest Quality', 'Old Recipe', 'Good Hot or Cold', 'Made Simple']}


class Look:
    pass


_used = set()


def look_of(p, kind):
    """the product's own mix; the same on every run (seeded by its id), and never the same as another's on the same pack shape."""
    rng = random.Random(zlib.crc32(p['id'].encode()))
    east = p['line'] == 'pressed' or p['k'] in ('green', 'jgreen', 'matcha', 'roast', 'oolong', 'yancha', 'yellow', 'grain', 'tonic')
    pool = POOL['east'] if east else POOL['herb'] if p['k'] in ('herb', 'herbh', 'rooibos', 'blue') else POOL['coffee'] if p['line'] == 'coffee' else POOL['drink'] if p['line'] == 'drink' else POOL['tea']
    for _ in range(200):
        lk = Look()
        lk.motif = HINT.get(p['id']) or rng.choice(pool)
        lk.style = rng.randrange(8)
        lk.pal = rng.choice(sorted(PALS))
        lk.wrap = rng.randrange(len(WRAPS)); lk.wstyle = rng.randrange(5)
        key = (kind, p['model'], lk.style if kind != 'cake' else lk.wstyle, lk.pal if kind != 'cake' else lk.wrap, lk.motif)
        key2 = (kind, lk.pal if kind != 'cake' else lk.wrap, lk.motif)
        if key in _used or key2 in _used: continue
        _used.add(key); _used.add(key2)
        break
    if p['id'] == 'TheVeryOne': lk.pal = 'yellow'      # the text says a yellow pack
    lk.bg, lk.fg, lk.acc = PALS[lk.pal]
    lk.f_en, lk.f_ru = rng.choice(FONTS); lk.f_sm = rng.choice(SMALL)
    lk.seed = rng.randrange(1, 9999); lk.rng = rng
    lk.en = p['en'].replace('ữ', 'u'); lk.ru = p['ru']
    lk.rus = p['id'] in RUS
    if not lk.rus:      # the main text is English; Russian is only a small nod on what comes from Russia
        tags = TAGS['pressed' if p['line'] == 'pressed' else p['kind'] if p['kind'] in TAGS else 'drink']
        lk.ru = rng.choice(tags).replace('#', str(rng.randrange(1840, 1962)))
    if rng.random() < .45 and lk.f_en not in ('lobster-400', 'pacifico-400', 'marck-script-400', 'great-vibes-400', 'amatic-sc-700'): lk.en = lk.en.upper(); lk.ru = lk.ru.upper()
    if lk.f_en in ('bebas-neue-400', 'cinzel-900', 'cinzel-decorative-700', 'unbounded-700'): lk.en = lk.en.upper()
    return lk


def weight_of(p):
    g = {'cake': 357, 'cake_small': 100, 'tuocha': 100, 'tuo_mini': 5, 'mushroom': 250, 'melon': 500, 'ball': 8, 'tangerine': 10, 'coin': 7, 'brick_tea': 250, 'tile': 250,
         'square': 200, 'square_mini': 9, 'briquette': 100, 'log': 725, 'bamboo': 100, 'basket': 250, 'sack': 500, 'tin_small': 40, 'tablet': 90, 'stick': 20, 'sachet': 25,
         'can': 250, 'can_short': 185, 'can_wide': 340, 'bottle': 250, 'barrel': 250, 'jar': 200, 'jar_small': 95, 'jar_wide': 190}.get(p['model'], p['servings'] * (12 if p['kind'] == 'coffee' else 10 if p['line'] == 'drink' else 6))
    return f'{g} ml' if p['model'].startswith('can') or p['model'] == 'bottle' else f'{g} g'


def lum(col): return .3 * col[0] + .59 * col[1] + .11 * col[2]
def contrast(a, b): return abs(lum(a) - lum(b)) > 70


# ------------------------------------------------------------------ the front of a pack, eight layouts, in a rectangle
def front(c, R, p, lk, cjk=None):
    x0, y0, x1, y1 = R; w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
    bg, fg, acc = lk.bg, lk.fg, lk.acc
    if not contrast(acc, bg): acc = fg
    M = MOTIFS[lk.motif]; st = lk.style
    u = min(w, h * .75)                      # the unit sizes hang on
    tw = w * .86
    wt = weight_of(p)
    def title(y, col, size=None, f=None):
        return c.text(cx, y, lk.en, f or lk.f_en, size or u * .17, col, maxw=tw)
    def sub(y, col, size=None):
        c.text(cx, y, lk.ru, lk.f_ru, size or u * .085, col, maxw=tw)
    def sorts(y, col, size=None):
        size = size or u * .05
        c.text(cx, y, p['sort_en'].upper(), lk.f_sm, size, col, maxw=tw); c.text(cx, y + size * 1.5, p['sort_ru'].upper(), lk.f_sm, size, col, maxw=tw)
    def han(y, col, size):
        if cjk: c.text(cx, y, cjk, CJK, size, col, maxw=tw)
    if st == 0:      # classic: frame, roundel, name, kind
        border(c, x0 + w * .05, y0 + h * .04, x1 - w * .05, y1 - h * .04, acc, max(2, u * .012), u * .03)
        c.circle(cx, y0 + h * .28, u * .2, fill=acc); M(c, cx, y0 + h * .28, u * .14, bg, fg if contrast(fg, acc) else bg, acc)
        han(y0 + h * .475, acc, u * .07)
        title(y0 + h * .57, fg); sub(y0 + h * .68, fg)
        c.line([(cx - u * .25, y0 + h * .745), (cx + u * .25, y0 + h * .745)], acc, max(1, u * .008))
        sorts(y0 + h * .8, fg); c.text(cx, y0 + h * .915, wt, lk.f_sm, u * .045, acc)
    elif st == 1:    # a band across
        M(c, cx, y0 + h * .24, u * .2, fg, acc, bg); han(y0 + h * .44, acc, u * .07)
        c.rect(x0, y0 + h * .5, x1, y0 + h * .72, fill=acc)
        tc = bg if contrast(bg, acc) else fg if contrast(fg, acc) else INK
        title(y0 + h * .575, tc, u * .15); sub(y0 + h * .665, tc, u * .07)
        sorts(y0 + h * .79, fg); c.text(cx, y0 + h * .92, wt, lk.f_sm, u * .045, fg)
    elif st == 2:    # two halves
        c.rect(x0, y0, x1, y0 + h * .46, fill=acc); c.rect(x0, y0 + h * .46, x1, y1, fill=PAPER)
        mc = bg if contrast(bg, acc) else fg
        M(c, cx, y0 + h * .23, u * .2, mc, fg if contrast(fg, acc) and fg != mc else mc, acc)
        zigzag(c, x0, y0 + h * .46 - u * .012, x1, u * .024, bg, u * .05, max(1, u * .01))
        han(y0 + h * .535, hx('B3261E'), u * .07)
        title(y0 + h * .63, INK); sub(y0 + h * .74, INK); sorts(y0 + h * .82, rgba(INK, 200)); c.text(cx, y0 + h * .935, wt, lk.f_sm, u * .042, rgba(INK, 200))
    elif st == 3:    # a round badge with the name around it
        r = u * .4; cy = y0 + h * .4
        c.circle(cx, cy, r, fill=acc); c.circle(cx, cy, r * .93, outline=bg, width=max(1, u * .008)); c.circle(cx, cy, r * .62, fill=bg)
        tc = bg if contrast(bg, acc) else INK
        fs = min(r * .2, r * .78 * math.pi / max(6, len(lk.en)) * 1.5)
        c.text_arc(cx, cy, r * .775, lk.en, lk.f_en, fs, tc, -90, fs * .08)
        c.text_arc(cx, cy, r * .775, wt, lk.f_sm, r * .1, tc, 90, 1, inside=True)
        M(c, cx, cy, r * .42, fg, acc, bg)
        han(y0 + h * .715, acc, u * .07)
        sub(y0 + h * .79, fg, u * .1); sorts(y0 + h * .87, fg)
    elif st == 4:    # modern: words stacked to the left
        words = lk.en.split(); y = y0 + h * .1; fs = min(u * .2, h * .5 / max(1, len(words)))
        for wd in words:
            s = c.text(x0 + w * .08, y, wd, lk.f_en, fs, fg, anchor='lt', maxw=w * .84); y += s * 1.08
        c.rect(x0 + w * .08, y + u * .03, x0 + w * .3, y + u * .045, fill=acc)
        c.text(x0 + w * .08, y + u * .09, lk.ru, lk.f_ru, u * .075, fg, anchor='lt', maxw=w * .84)
        if cjk: c.text(x0 + w * .08, y + u * .2, cjk, CJK, u * .08, acc, anchor='lt')
        M(c, x1 - w * .3, y1 - h * .24, u * .22, acc, fg, bg)
        c.text(x0 + w * .08, y1 - h * .14, p['sort_en'].upper(), lk.f_sm, u * .042, fg, anchor='lt', maxw=w * .5)
        c.text(x0 + w * .08, y1 - h * .1, p['sort_ru'].upper(), lk.f_sm, u * .042, fg, anchor='lt', maxw=w * .5); c.text(x0 + w * .08, y1 - h * .055, wt, lk.f_sm, u * .04, acc, anchor='lt')
    elif st == 5:    # stripes and a plate
        n = 9; sw = w / n
        for k in range(n):
            if k % 2: c.rect(x0 + k * sw, y0, x0 + (k + 1) * sw, y1, fill=rgba(shade(bg, .82 if lum(bg) > 60 else 1.6)))
        c.circle(cx, y0 + h * .22, u * .2, fill=bg, outline=acc, width=max(2, u * .014)); M(c, cx, y0 + h * .22, u * .13, acc, fg, bg)
        c.rect(x0 + w * .07, y0 + h * .44, x1 - w * .07, y0 + h * .8, fill=PAPER, outline=acc, width=max(2, u * .012), r=u * .03)
        han(y0 + h * .49, hx('B3261E'), u * .055)
        c.text(cx, y0 + h * .565, lk.en, lk.f_en, u * .14, INK, maxw=w * .76); c.text(cx, y0 + h * .66, lk.ru, lk.f_ru, u * .07, INK, maxw=w * .76)
        c.text(cx, y0 + h * .725, p['sort_en'].upper(), lk.f_sm, u * .042, rgba(INK, 210), maxw=w * .76); c.text(cx, y0 + h * .765, p['sort_ru'].upper(), lk.f_sm, u * .042, rgba(INK, 210), maxw=w * .76)
        c.text(cx, y0 + h * .9, wt, lk.f_sm, u * .05, fg)
    elif st == 6:    # old-fashioned: ornament bands, an arched name
        bh = u * .06
        (greek_band if lk.seed % 3 == 0 else None) and greek_band(c, x0, y0 + h * .04, x1, bh, acc)
        if lk.seed % 3 == 1: zigzag(c, x0, y0 + h * .04, x1, bh, acc, bh * 1.4, max(1, u * .012)); zigzag(c, x0, y1 - h * .04 - bh, x1, bh, acc, bh * 1.4, max(1, u * .012))
        if lk.seed % 3 == 2: dots_band(c, x0, y0 + h * .06, x1, bh * .3, acc); dots_band(c, x0, y1 - h * .06, x1, bh * .3, acc)
        if lk.seed % 3 == 0: greek_band(c, x0, y1 - h * .04 - bh, x1, bh, acc)
        R_ = w * .9; fs = min(u * .15, R_ * 1.1 / max(6, len(lk.en)) * 1.6)
        c.text_arc(cx, y0 + h * .2 + R_, R_, lk.en, lk.f_en, fs, fg, -90, fs * .06)
        M(c, cx, y0 + h * .47, u * .2, acc, fg, bg)
        han(y0 + h * .655, acc, u * .07)
        c.poly([(x0 + w * .1, y0 + h * .71), (x1 - w * .1, y0 + h * .71), (x1 - w * .14, y0 + h * .755), (x1 - w * .1, y0 + h * .8), (x0 + w * .1, y0 + h * .8), (x0 + w * .14, y0 + h * .755)], fill=acc)
        c.text(cx, y0 + h * .755, lk.ru, lk.f_ru, u * .075, bg if contrast(bg, acc) else INK, maxw=w * .68)
        sorts(y0 + h * .845, fg, u * .045)
    else:            # a slanting cut
        c.poly([(x0, y0 + h * .58), (x1, y0 + h * .36), (x1, y1), (x0, y1)], fill=acc)
        tc = bg if contrast(bg, acc) else INK
        M(c, x0 + w * .3, y0 + h * .22, u * .2, fg, acc, bg)
        if cjk: c.text(x1 - w * .2, y0 + h * .2, cjk, CJK, u * .09, acc, maxw=w * .34)
        title(y0 + h * .64, tc, u * .16); sub(y0 + h * .745, tc)
        sorts(y0 + h * .83, tc); c.text(cx, y0 + h * .935, wt, lk.f_sm, u * .042, tc)


def side_text(c, R, p, lk, col):
    """a narrow side: the name written up it"""
    x0, y0, x1, y1 = R; w, h = int(x1 - x0), int(y1 - y0)
    if w < 8 or h < 8: return
    if w > h * .8:
        c.text((x0 + x1) / 2, (y0 + y1) / 2 - h * .1, lk.en, lk.f_en, min(h * .3, w * .12), col, maxw=w * .9)
        c.text((x0 + x1) / 2, (y0 + y1) / 2 + h * .2, p['sort_en'], lk.f_sm, min(h * .14, w * .06), col, maxw=w * .9); return
    t = Canvas(h, w, (0, 0, 0)); t.im = Image.new('RGBA', (h * SS, w * SS), (0, 0, 0, 0)); t.d = ImageDraw.Draw(t.im, 'RGBA')
    t.text(h / 2, w * .36, lk.en, lk.f_en, w * .34, col, maxw=h * .9); t.text(h / 2, w * .74, p['sort_en'].upper(), lk.f_sm, w * .16, col, maxw=h * .9)
    c.im.alpha_composite(t.im.rotate(90, expand=True), (int(x0 * SS), int(y0 * SS)))


def back_strip(c, p, y0, h, bw, bc, seed, x_seam=1024):
    """the small print on the back of a round pack, written once across the seam (x 0 = x 1024)"""
    tcol = INK if lum(bc) > 120 else PAPER
    strip = Canvas(bw, int(h), bc[:3])
    ru, en = brew_lines(p)
    info_block(strip, 10, 18, bw - 20, p, ru, en, tcol, 12)
    if h > 300: barcode(strip, bw / 2 - 60, strip.H - 62, 120, 30, seed)
    s_im = strip.final(); half = bw // 2
    c.im.alpha_composite(s_im.crop((half, 0, bw, s_im.height)).resize((half * SS, s_im.height * SS)), (0, int(y0 * SS)))
    c.im.alpha_composite(s_im.crop((0, 0, half, s_im.height)).resize((half * SS, s_im.height * SS)), (int((x_seam - half) * SS), int(y0 * SS)))


def arr_paste(c, a, x, y):
    im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).convert('RGBA')
    c.im.alpha_composite(im.resize((im.width * SS, im.height * SS)), (int(x * SS), int(y * SS)))


def leaf_tex(w, h, base, tips, seed, coarse=1.0):
    """pressed tea leaf: dark matted leaves, short pale stems and tips lying every way"""
    rng = np.random.default_rng(seed)
    a = np.ones((h, w, 3)) * np.array(base[:3], float)
    a *= (.82 + .36 * noise(w, h, int(60 * coarse), seed, 3))[..., None]
    S = int(max(w, h) * 1.5); ox, oy = (S - w) // 2, (S - h) // 2
    g = Image.fromarray((rng.random((S, S)) * 255).astype(np.uint8))
    for k, (ang, amt) in enumerate(((0, .22), (60, .22), (120, .22))):
        big = g.rotate(ang + k * 7, resample=Image.BICUBIC).filter(ImageFilter.GaussianBlur((2.2 * coarse, .5))).rotate(-ang - k * 7, resample=Image.BICUBIC)
        st = np.asarray(big, float)[oy:oy + h, ox:ox + w] / 255
        m = np.clip((st - .56) * 9, 0, 1)[..., None]
        a = a * (1 - m * .5) + np.array(tips[:3], float) * m * .5
        d = np.clip((.45 - st) * 7, 0, 1)[..., None]; a *= 1 - d * .35
    a += rng.normal(0, 5, (h, w, 1))
    return a


TEA_COL = {'sheng': ((74, 70, 38), (190, 184, 140)), 'shou': ((52, 32, 20), (150, 104, 50)), 'hei': ((46, 36, 24), (120, 100, 60)), 'pwhite': ((120, 104, 70), (214, 208, 186)),
           'pblack': ((40, 26, 18), (170, 120, 50)), 'oolong': ((60, 62, 36), (150, 150, 90)), 'jgreen': ((70, 80, 40), (160, 170, 110)), 'herb': ((70, 52, 30), (150, 110, 70))}


def glass_body(c, L, content, seed=1, fill=.84, grain=1.0):
    """a glass jar's wall with what is inside: the stuff up to its level, clear glass over it, a thick base, soft upright reflections"""
    x0, y0, x1, y1 = L['body']; H = int(y1 - y0)
    rng = np.random.default_rng(seed)
    a = np.ones((H, 1024, 3)) * np.array(content[:3], float)
    a += (rng.random((H, 1024, 1)) - .5) * np.array([60, 42, 28]) * 1.3 * grain
    a += (noise(1024, H, 120, seed, 3) - .5)[..., None] * 36
    top = int(H * (1 - fill))
    yy = np.arange(H)[:, None]
    lvl = top + (noise(1024, 1, 6, seed + 4, 2)[0] - .5)[None] * 16
    empty = yy < lvl
    glass = np.ones((H, 1024, 3)) * np.array([176, 190, 188.0]) * (.86 + .2 * (yy / max(1, top)).clip(0, 1))[..., None]
    a = np.where(empty[..., None], glass, a)
    dust = (yy >= lvl) & (yy < lvl + 10)
    a[dust] = a[dust] * .75 + np.array(content[:3]) * .5
    a[H - 26:] = a[H - 26:] * .45 + np.array([150, 170, 164.0]) * .55      # the thick glass base
    a[H - 30:H - 26] = a[H - 30:H - 26] * .5 + 120
    arr_paste(c, a, x0, y0)
    # owner 11-15: no painted upright reflections, they read as white lines on the model (the game lights the glass itself)
    lay = c.layer(); lay.rect(0, y0, 1024, y0 + 46, fill=(235, 245, 242, 130)); lay.rect(0, y0 + 60, 1024, y0 + 66, fill=(20, 30, 30, 60))
    lay.im = lay.im.filter(ImageFilter.GaussianBlur(6 * SS)); c.over(lay)


# ------------------------------------------------------------------ painters by shape
def P_box(p, L, lk):
    model = p['model']; soft = model in ('pack_paper', 'bag_valve', 'doypack', 'sack', 'sachet', 'stick', 'brick')
    wrap = model in ('brick_tea', 'tile', 'square', 'square_mini', 'briquette')
    bg = lk.bg
    if model == 'sack':
        bg = hx('B79A6B') if lk.seed % 2 else hx('C8B08A'); lk.bg = bg; lk.fg = hx('2B1C10'); lk.acc = hx('7B1F1A') if lk.seed % 3 else hx('1F3D2B')
        if lk.style in (2, 5): lk.style = 6
    if wrap and p.get('cjk') is not None and lk.style in (4,): lk.style = 0
    c = Canvas(1024, 1024, bg[:3])
    for k in ('front', 'back', 'left', 'right', 'top', 'bottom'):
        x0, y0, x1, y1 = L[k]; c.rect(x0 - 3, y0 - 3, x1 + 3, y1 + 3, fill=bg)
    F = L['front']; fw, fh = F[2] - F[0], F[3] - F[1]
    if fw < fh * .3:      # a stick: the design lies along it
        t = Canvas(int(fh), int(fw), bg[:3]); lk2 = lk
        t.rect(0, 0, fh * .2, fw, fill=lk.acc); MOTIFS[lk.motif](t, fh * .1, fw / 2, fw * .32, bg if contrast(bg, lk.acc) else lk.fg, lk.fg, lk.acc)
        t.text(fh * .56, fw * .36, lk.en, lk.f_en, fw * .4, lk.fg, maxw=fh * .62); t.text(fh * .56, fw * .76, p['sort_en'], lk.f_sm, fw * .17, lk.fg, maxw=fh * .62)
        c.im.alpha_composite(t.im.rotate(90, expand=True), (int(F[0] * SS), int(F[1] * SS)))
    else:
        top = {'pack_paper': .13, 'bag_valve': .2, 'doypack': .2, 'sack': .2}.get(model, 0)
        front(c, (F[0], F[1] + fh * top, F[2], F[3]), p, lk, p.get('cjk') or None)
    if soft and model not in ('brick',):
        for k in ('front', 'back'):      # heat seals
            x0, y0, x1, y1 = L[k]; sh = (y1 - y0) * (.06 if model != 'stick' else .05)
            c.rect(x0, y0, x1, y0 + sh, fill=rgba(shade(bg, .86 if lum(bg) > 60 else 1.5))); L1.seal_lines(c, x0, y0 + 2, x1, y0 + sh, rgba(shade(bg, .7 if lum(bg) > 60 else 1.9)), 4)
            if model in ('sachet', 'stick'):
                c.rect(x0, y1 - sh, x1, y1, fill=rgba(shade(bg, .86 if lum(bg) > 60 else 1.5))); L1.seal_lines(c, x0, y1 - sh, x1, y1 - 2, rgba(shade(bg, .7 if lum(bg) > 60 else 1.9)), 4)
    if model == 'bag_valve':
        x0, y0, x1, y1 = L['back']; c.circle((x0 + x1) / 2, y0 + (y1 - y0) * .16, 13, fill=rgba(shade(bg, .6 if lum(bg) > 60 else 2.2))); c.circle((x0 + x1) / 2, y0 + (y1 - y0) * .16, 5, fill=rgba(shade(bg, .35 if lum(bg) > 60 else 3)))
    # back: the small print
    x0, y0, x1, y1 = L['back']; w = x1 - x0; hh = y1 - y0
    tcol = INK if lum(bg) > 120 else PAPER
    if w > 60 and hh > 110:
        ru, en = brew_lines(p)
        yb = info_block(c, x0 + 8, y0 + hh * max(.12, {'pack_paper': .21, 'bag_valve': .36, 'doypack': .40, 'sack': .28}.get(model, 0)) + 10, w - 16, p, ru, en, tcol, max(7, min(13, w / 22, hh / 20)))
        if y1 - yb > 70 and w > 130: barcode(c, x0 + w / 2 - 50, y1 - 52, 100, 28, lk.seed)
    else:
        side_text(c, L['back'], p, lk, tcol)
    side_text(c, L['left'], p, lk, lk.fg); side_text(c, L['right'], p, lk, lk.fg)
    x0, y0, x1, y1 = L['top']
    if y1 - y0 > 24 and not soft: c.text((x0 + x1) / 2, (y0 + y1) / 2, lk.en, lk.f_en, (y1 - y0) * .34, lk.fg, maxw=(x1 - x0) * .86)
    im = c.final()
    if model == 'sack':
        a = np.asarray(im, float); h_, w_ = a.shape[:2]; yy, xx = np.mgrid[0:h_, 0:w_]
        weave = (np.sin(xx * 1.9) * .5 + np.sin(yy * 1.9) * .5) * .09; a[..., :3] = np.clip(a[..., :3] * (1 + weave)[..., None], 0, 255); im = Image.fromarray(a.astype(np.uint8), 'RGBA')
        return texturize(im, .12, lk.seed, 20, .05, fibres=.1)
    if wrap: return texturize(im, .1, lk.seed, 18, .03, fibres=.1)
    return texturize(im, .06, lk.seed, 26, .025, fibres=.04 if not soft else 0)


def fronts(t, W, h, p, lk, cx=None, cjk=None, back=300, top=0, bot=0):
    """the front of a round pack on an unstretched band W x h: a front as wide as the eye takes in at once, on long low packs three of them,
    and the brand's sign in the bare gaps"""
    hh = h - top - bot; fw = min(.38 * W, hh * 1.05)
    n = 1 if W / fw < 3.2 else 3
    xs = [W * (i + .5) / n for i in range(n)] if cx is None else [cx]
    for x in xs: front(t, (x - fw / 2, top, x + fw / 2, h - bot), p, lk, cjk)
    a = lk.acc if contrast(lk.acc, lk.bg) else lk.fg
    if cx is None:
        gaps = [((xs[i] + xs[i + 1]) / 2, xs[i + 1] - xs[i] - fw) for i in range(n - 1)]
        if n == 1: g = (W - fw - back) / 2; gaps = [(back / 2 + g / 2, g), (W - back / 2 - g / 2, g)]
        for gx, g in gaps:
            if g > hh * .42: s_ = min(g, hh) * .2; t.circle(gx, top + hh / 2, s_ * 1.25, outline=a, width=max(2, s_ * .06)); MOTIFS[lk.motif](t, gx, top + hh / 2, s_ * .8, a, lk.fg, lk.bg)


def P_tin(p, L, lk):
    def fr(t, cx, ya, yb): fronts(t, t.W, yb - ya, p, lk)
    def lid_top(c, cx, cy, r):
        c.circle(cx, cy, r - 8, outline=lk.acc if contrast(lk.acc, lk.bg) else lk.fg, width=3)
        MOTIFS[lk.motif](c, cx, cy - r * .18, r * .34, lk.fg, lk.acc, lk.bg); c.text(cx, cy + r * .45, lk.en, lk.f_en, r * .2, lk.fg, maxw=r * 1.5)
    def band(c, ya, yb):
        a = lk.acc if contrast(lk.acc, lk.bg) else lk.fg
        c.rect(0, ya + (yb - ya) * .38, 1024, ya + (yb - ya) * .62, fill=a)
    return L1.paint_tin(p, L, lk.bg, fr, lk.bg, lid_top, PAPER, (176, 176, 180), band, lk.seed, .35 + (lk.seed % 5) * .1, native=True)


def P_cyl(p, L, lk):
    model = p['model']; x0, y0, x1, y1 = L['body']; H = y1 - y0
    c = Canvas(1024, 1024, lk.bg[:3])
    mat = {'bamboo': 'bamboo', 'log': 'weave', 'basket': 'weave', 'barrel': 'wood'}.get(model, 'paper')
    rng = np.random.default_rng(lk.seed)
    k = kof(L); W = max(8, int(round(1024 * k))); H = int(H)
    if mat == 'paper':
        a = lk.acc if contrast(lk.acc, lk.bg) else lk.fg
        c.rect(x0, y0, x1, y1, fill=lk.bg)
        fit(c, (x0, y0 + 14, x1, y1 - 14), k, lambda t, W_, h_: fronts(t, W_, h_, p, lk, cjk=p.get('cjk') or None, top=4, bot=4))
        L1.back_fit(c, p, 0, y0 + 60, H - 120, PAPER, lk.seed, k)
        c.rect(x0, y0, x1, y0 + 14, fill=a); c.rect(x0, y1 - 14, x1, y1, fill=a)
        tcx, tcy, tr = L['top']; c.circle(tcx, tcy, tr + 4, fill=rgba(shade(lk.bg, .8 if lum(lk.bg) > 60 else 1.5))); c.circle(tcx, tcy, tr - 10, outline=a, width=3)
        MOTIFS[lk.motif](c, tcx, tcy, tr * .45, lk.fg, lk.acc, lk.bg)
        bx, by, br = L['bottom']; c.circle(bx, by, br + 4, fill=rgba(shade(lk.bg, .7 if lum(lk.bg) > 60 else 1.4)))
        return texturize(c.final(), .06, lk.seed, 26, .025, fibres=.05)
    c0 = c; c = Canvas(W, H, lk.bg[:3]); y0 = 0; y1 = H; M_ = W / 2      # the patterned body is painted unstretched and squeezed onto the wrap after
    if mat == 'bamboo':
        base = np.array([150, 146, 84.0]) if lk.seed % 2 else np.array([176, 150, 96.0])
        a = np.ones((int(H), W, 3)) * base
        a *= (.9 + .2 * noise(W, int(H), 8, lk.seed, 2))[..., None]
        a += (rng.normal(0, 1, (1, W, 1)) * 7)            # fibres along the stalk
        arr_paste(c, a, x0, y0)
        for yn in (y0 + H * .08, y0 + H * .93):               # the joints
            c.rect(0, yn - 5, W, yn + 5, fill=(96, 86, 44, 255)); c.rect(0, yn - 9, W, yn - 5, fill=(196, 188, 130, 255))
        for _ in range(26):
            xx = rng.uniform(0, W); yy = rng.uniform(y0 + 20, y1 - 20); c.line([(xx, yy), (xx + rng.uniform(-3, 3), yy + rng.uniform(20, 90))], (60, 40, 20, 90), 2)   # scorch marks
        lab = (M_ - min(W * .2, H * .36), y0 + H * .2, M_ + min(W * .2, H * .36), y0 + H * .8)
    elif mat == 'weave':
        a = np.zeros((int(H), W, 3)); yy, xx = np.mgrid[0:int(H), 0:W]
        s = 26 if model == 'log' else 34
        u_ = ((xx + yy) // s) % 2; v_ = ((xx - yy) // s) % 2
        tone = np.where(u_ == v_, 1.0, .78) * (.85 + .3 * noise(W, int(H), 30, lk.seed, 3))
        base = np.array([186, 160, 104.0]) if model == 'log' else np.array([166, 132, 84.0])
        a = base[None, None] * tone[..., None]
        edge = (((xx + yy) % s) < 2) | (((xx - yy) % s) < 2); a[edge] *= .6
        arr_paste(c, a, x0, y0)
        ropes = (.12, .32, .5, .68, .88) if model == 'log' else (.1, .9)
        for f in ropes:
            yr = y0 + H * f; c.rect(0, yr - 6, W, yr + 6, fill=(120, 92, 50, 255))
            for k in range(0, W, 10): c.line([(k, yr + 6), (k + 6, yr - 6)], (70, 52, 26, 255), 2)
        lab = (M_ - W * .2, y0 + H * .36, M_ + W * .2, y0 + H * .64) if model == 'log' else (M_ - min(W * .19, H * .4), y0 + H * .2, M_ + min(W * .19, H * .4), y0 + H * .82)
    else:   # wood staves and iron hoops
        a = np.ones((int(H), W, 3)) * np.array([164, 118, 66.0])
        a *= (.8 + .4 * noise(W, 1, 200, lk.seed, 2))[0][None, :, None]
        a *= (.92 + .16 * noise(W, int(H), 6, lk.seed + 1, 2))[..., None]
        arr_paste(c, a, x0, y0)
        for k in range(max(8, round(W / 64))): c.line([(k * W / max(8, round(W / 64)), y0), (k * W / max(8, round(W / 64)), y1)], (70, 44, 20, 255), 3)
        for f in (.1, .28, .72, .9):
            yr = y0 + H * f; c.rect(0, yr - 11, W, yr + 11, fill=(60, 60, 64, 255)); c.rect(0, yr - 11, W, yr - 8, fill=(120, 120, 126, 255))
            for k in range(6): c.circle((k + .5) * W / 6, yr, 4, fill=(30, 30, 32, 255))
        # the brand burnt in
        c.text(M_, y0 + H * .42, lk.en.upper(), 'kelly-slab-400', H * .1, (50, 28, 12, 255), maxw=min(420, W * .36)); c.text(M_, y0 + H * .52, p['sort_en'].upper(), 'oswald-600', H * .055, (50, 28, 12, 255), maxw=min(420, W * .36))
        c.text(M_, y0 + H * .6, lk.ru.upper(), 'kelly-slab-400', H * .06, (50, 28, 12, 255), maxw=min(420, W * .36))
        MOTIFS[lk.motif](c, 0, y0 + H * .5, H * .12, (50, 28, 12, 255), (50, 28, 12, 255), (164, 118, 66, 255)); MOTIFS[lk.motif](c, W, y0 + H * .5, H * .12, (50, 28, 12, 255), (50, 28, 12, 255), (164, 118, 66, 255))
        lab = None
    if lab:
        W = WRAPS[lk.wrap]; paper, ink, seal = W
        if lum(paper) < 120: paper, ink = hx('EFE6CC'), hx('3A2416')
        c.rect(*lab, fill=paper); lx0, ly0, lx1, ly1 = lab; lw, lh = lx1 - lx0, ly1 - ly0; cx = M_
        c.rect(lx0 + 6, ly0 + 6, lx1 - 6, ly1 - 6, outline=ink, width=2)
        yq = ly0 + lh * .1
        if p.get('cjk'): c.text(cx, ly0 + lh * .17, p['cjk'], CJK, min(lh * .16, lw * .2), ink, maxw=lw * .8); yq = ly0 + lh * .3
        if lh > 200: MOTIFS[lk.motif](c, cx, yq + lh * .13, min(lw, lh) * .13, seal, ink, paper); yq += lh * .26
        c.text(cx, yq + lh * .08, lk.en, lk.f_en, lw * .13, ink, maxw=lw * .86); c.text(cx, yq + lh * .2, lk.ru, lk.f_ru, lw * .085, ink, maxw=lw * .86)
        c.text(cx, ly1 - lh * .17, p['sort_en'], lk.f_sm, lw * .055, ink, maxw=lw * .86); c.text(cx, ly1 - lh * .09, p['sort_ru'], lk.f_sm, lw * .055, ink, maxw=lw * .86)
        c.rect(lx1 - lw * .2, ly0 + lh * .04, lx1 - lw * .06, ly0 + lh * .04 + lw * .14, fill=seal)
    c0.im.alpha_composite(c.im.resize((1024 * SS, H * SS), Image.LANCZOS), (0, int(L['body'][1]) * SS)); c = c0
    for (tcx, tcy, tr), col in ((L['top'], (150, 120, 76)), (L['bottom'], (130, 100, 60))):
        if mat == 'bamboo': col = (120, 110, 60) if tcx < 512 else (170, 150, 100)
        if mat == 'wood': col = (150, 106, 60)
        c.circle(tcx, tcy, tr + 6, fill=col + (255,))
        if mat == 'weave' and tcx < 512 or mat == 'bamboo' and tcx < 512:
            arr_paste(c, leaf_tex(int(tr * 2), int(tr * 2), *TEA_COL.get(p['k'], TEA_COL['shou']), lk.seed), tcx - tr, tcy - tr)
            c.circle(tcx, tcy, tr + 30, outline=col + (255,), width=62)
        if mat == 'wood':
            for k in range(-2, 3): c.line([(tcx + k * tr * .4, tcy - tr), (tcx + k * tr * .4, tcy + tr)], (70, 44, 20, 255), 2)
            if tcx < 512: c.text(tcx, tcy, weight_of(p), 'kelly-slab-400', tr * .3, (50, 28, 12, 255))
    return texturize(c.final(), .08, lk.seed, 22, .03, fibres=.06)


def P_jar(p, L, lk):
    c = Canvas(1024, 1024, (60, 40, 30))
    k = p['k']; fine = k in ('cocoa', 'malt', 'milk', 'sub') or 'powder' in p['sort_en']
    content = {'instant': (62, 34, 18), 'sub': (70, 42, 22), 'cocoa': (86, 48, 30), 'malt': (190, 160, 110), 'milk': (212, 150, 30), 'fruit': (222, 160, 40), 'honey': (150, 84, 20),
               'broth': (46, 22, 12), 'herb': (226, 170, 176), 'milkc': (48, 26, 14)}.get(k, (62, 34, 18))
    if p['id'] == 'FiveFlavours': content = (140, 24, 30)
    syrup = k in ('fruit', 'honey', 'broth', 'milkc')
    glass_body(c, L, content, lk.seed, fill=.86 if not syrup else .9, grain=.35 if (fine or syrup) else 1.0)
    x0, y0, x1, y1 = L['body']; hz = L['bodyz'][1]
    ly0 = y1 - (y1 - y0) * (L['label'][1] + .009) / hz; ly1 = y1 - (y1 - y0) * (L['label'][0] - .009) / hz
    kk = kof(L); lh = ly1 - ly0
    bw = int(min(.22 * 1024, lh * 1.9 / kk / 2))   # half-width of the paper label on the wrap: glass shows at the sides and the back
    def lab(t, W, h):
        t.rect(0, 0, W, h, fill=lk.bg); front(t, (0, 0, W, h), p, lk); t.rect(0, 0, W, h, outline=rgba(shade(lk.bg, .7)), width=2)
    fit(c, (512 - bw, ly0, 512 + bw, ly1), kk, lab)
    if lh > 150:    # a small back label
        sw = int(min(300, 1024 * kk * .3) / kk) + 12
        c.rect(0, ly0 + 14, sw / 2, ly1 - 14, fill=PAPER); c.rect(1024 - sw / 2, ly0 + 14, 1024, ly1 - 14, fill=PAPER)
        L1.back_fit(c, p, 0, ly0 + 18, lh - 36, PAPER, lk.seed, kk)
    lx0, la, lx1, lb = L['lidside']; lidc = lk.acc if lum(lk.acc) < 200 else lk.bg
    if p['model'] == 'bottle': lidc = (40, 40, 44, 255) if lk.seed % 2 else hx('C9A24B')
    c.rect(lx0, la, lx1, lb, fill=lidc)
    for kx in range(0, 1024, 12): c.line([(kx, la), (kx, lb)], rgba(shade(lidc, .72)), 3)
    tcx, tcy, tr = L['top']; c.circle(tcx, tcy, tr + 4, fill=lidc); c.circle(tcx, tcy, tr - 10, outline=rgba(shade(lidc, 1.35)), width=3)
    tc = lk.bg if contrast(lk.bg, lidc) else lk.fg if contrast(lk.fg, lidc) else PAPER
    c.text(tcx, tcy, lk.en, lk.f_en, tr * .3, tc, maxw=tr * 1.5)
    bx, by, br = L['bottom']; c.circle(bx, by, br + 4, fill=rgba(shade(content, .9))); c.circle(bx, by, br * .7, outline=(170, 190, 184, 160), width=6)
    return texturize(c.final(), .035, lk.seed, 30, .012)


def P_can(p, L, lk):
    """the aluminium can of the food mod: the wall is the band y 210..869 right round, the front at x 256, the back at x 768, bare metal above and below"""
    x0, y0, x1, y1 = L['body']; H = y1 - y0; k = kof(L); fx = L['front']
    c = Canvas(1024, 1024, (190, 190, 194))
    c.im.alpha_composite(can_metal(lk.seed).resize((1024 * SS, 1024 * SS)))
    ya, yb = y0 + 10, y1 - 12
    c.rect(x0, ya, x1, yb, fill=lk.bg)
    fw = 1024 * .33
    fit(c, (fx - fw / 2, ya + 12, fx + fw / 2, yb - 12), k, lambda t, W, h: front(t, (0, 0, W, h), p, lk))
    L1.back_fit(c, p, fx + 512, y0 + H * .2, H * .6, lk.bg, lk.seed, k)
    a = lk.acc if contrast(lk.acc, lk.bg) else lk.fg
    c.rect(x0, ya, x1, ya + 9, fill=a); c.rect(x0, yb - 9, x1, yb, fill=a)
    return scratches(texturize(c.final(), .03, lk.seed, 30, .012), 30, lk.seed)


def wrapper(c, cx, cy, R, p, lk, small=False):
    """the printed paper of a tea cake, five layouts"""
    paper, ink, seal = WRAPS[lk.wrap]; cjk = p.get('cjk') or ''
    st = lk.wstyle; M = MOTIFS[lk.motif]
    if not cjk and st == 1: st = 2
    dark = lum(paper) < 120
    c.circle(cx, cy, R * .93, outline=ink, width=max(2, R * .012))
    en = lk.en.upper(); sort = p['sort_en'].upper()
    def arcs(r_en=.8):
        fs = min(R * .1, R * r_en * 2.2 / max(8, len(en)) * 1.5)
        c.text_arc(cx, cy, R * r_en, en, lk.f_en if lk.f_en not in ('great-vibes-400', 'marck-script-400', 'pacifico-400', 'lobster-400') else 'playfair-display-700', fs, ink, 90, fs * .12, inside=True)
    if st == 0:      # ring of text, an emblem in the middle
        c.circle(cx, cy, R * .66, outline=ink, width=max(1, R * .006))
        if cjk: c.text_arc(cx, cy, R * .78, cjk + ' · ' + {'sheng': '生茶', 'shou': '熟茶', 'hei': '黑茶', 'pwhite': '白茶', 'pblack': '红茶'}.get(p['k'], '茶'), CJK, R * .12, ink, -90, R * .05)
        else: c.text_arc(cx, cy, R * .78, lk.ru.upper(), 'playfair-display-700', R * .09, ink, -90, R * .01)
        arcs(.8)
        c.circle(cx, cy - R * .08, R * .3, fill=seal); M(c, cx, cy - R * .08, R * .2, paper if contrast(paper, seal) else ink, paper, seal)
        for k in range(12):
            t = math.radians(30 * k); c.circle(cx + R * .42 * math.cos(t), cy - R * .08 + R * .42 * math.sin(t), R * .018, fill=ink)
        c.text(cx, cy + R * .42, lk.ru if cjk else sort, lk.f_ru if cjk else lk.f_sm, R * .085, ink, maxw=R * 1.0); c.text(cx, cy + R * .54, weight_of(p), lk.f_sm, R * .06, ink)
    elif st == 1:    # big brush characters and a seal
        n = len(cjk); fs = min(R * .5, R * 1.25 / n)
        for i, ch in enumerate(cjk): c.text(cx - R * .08, cy - R * .12 + (i - (n - 1) / 2) * fs * 1.02, ch, CJK, fs, ink)
        c.rect(cx + R * .32, cy + R * .1, cx + R * .52, cy + R * .3, fill=seal); c.text(cx + R * .42, cy + R * .2, '茶', CJK, R * .15, paper if contrast(paper, seal) else hx('F4EAD2'))
        arcs(.8); c.text_arc(cx, cy, R * .8, lk.ru.upper(), 'playfair-display-700', R * .075, ink, -90, R * .01)
        M(c, cx - R * .5, cy + R * .25, R * .13, seal, ink, paper)
    elif st == 2:    # a woodcut picture
        c.circle(cx, cy - R * .1, R * .46, outline=ink, width=max(2, R * .012)); c.circle(cx, cy - R * .1, R * .42, outline=ink, width=max(1, R * .005))
        M(c, cx, cy - R * .1, R * .32, ink, seal, paper)
        if cjk:
            for i, ch in enumerate(cjk): c.text(cx - R * .68, cy - R * .2 + i * R * .16, ch, CJK, R * .14, seal if not dark else ink)
        c.text(cx, cy + R * .5, lk.ru, lk.f_ru, R * .11, ink, maxw=R * 1.1); arcs(.82)
        c.text_arc(cx, cy, R * .82, sort, lk.f_sm, R * .06, ink, -90, R * .012)
    elif st == 3:    # a coloured band across
        c.rect(cx - R * .95, cy - R * .16, cx + R * .95, cy + R * .16, fill=seal)
        tc = paper if contrast(paper, seal) else hx('F4EAD2') if lum(seal) < 150 else INK
        c.text(cx, cy - R * .02, lk.en, lk.f_en, R * .17, tc, maxw=R * 1.6)
        if cjk: c.text(cx, cy - R * .42, cjk, CJK, R * .26, ink, maxw=R * 1.1)
        else: M(c, cx, cy - R * .46, R * .2, ink, seal, paper)
        c.text(cx, cy + R * .32, lk.ru, lk.f_ru, R * .1, ink, maxw=R * 1.2); c.text(cx, cy + R * .46, sort, lk.f_sm, R * .06, ink, maxw=R * 1.0)
        if cjk: M(c, cx, cy + R * .68, R * .12, ink, seal, paper)
        c.text_arc(cx, cy, R * .82, weight_of(p), lk.f_sm, R * .06, ink, -90, 2)
    else:            # plain paper, a round stamp and a hand-written name
        c.circle(cx, cy - R * .18, R * .3, outline=seal, width=max(3, R * .02)); c.circle(cx, cy - R * .18, R * .24, outline=seal, width=max(1, R * .008))
        if cjk: c.text(cx, cy - R * .18, cjk[:2], CJK, R * (.2 if len(cjk) > 1 else .3), seal)
        else: M(c, cx, cy - R * .18, R * .17, seal, seal, paper)
        c.text(cx, cy + R * .3, lk.ru, 'bad-script-400', R * .15, ink, maxw=R * 1.3); c.text(cx, cy + R * .48, lk.en, 'marck-script-400', R * .11, ink, maxw=R * 1.2)
        c.text(cx, cy + R * .62, sort, lk.f_sm, R * .055, ink, maxw=R * 1.0)


def P_cake(p, L, lk):
    model = p['model']; tx, ty, tr = L['top']; bx, by, br = L['bottom']
    paper, ink, seal = WRAPS[lk.wrap]
    base, tips = TEA_COL.get(p['k'], TEA_COL['shou'])
    surf = {'mushroom': 'bare', 'melon': 'bare', 'coin': 'bare', 'tangerine': 'peel', 'tablet': 'choc'}.get(model, 'paper')
    if model == 'tuocha' and lk.seed % 3 == 0: surf = 'bare'
    c = Canvas(1024, 1024, paper[:3])
    if surf == 'paper':
        c.rect(0, 0, 1024, 1024, fill=paper)
        wrapper(c, tx, ty, tr, p, lk)
        # underneath: the paper gathered to the middle, the tea showing in the gap (in the hollow of a tuocha)
        hole = .14 if model.startswith('cake') else .34 if model in ('tuocha', 'tuo_mini') else .08
        rng = random.Random(lk.seed)
        for k in range(22):
            t = math.radians(k * 360 / 22 + rng.uniform(-5, 5)); c.line([(bx + br * hole * math.cos(t), by + br * hole * math.sin(t)), (bx + br * math.cos(t + .25), by + br * math.sin(t + .25))], rgba(shade(paper, .82 if lum(paper) > 80 else 1.5)), 3)
        arr_paste(c, leaf_tex(int(br * hole * 2), int(br * hole * 2), base, tips, lk.seed), bx - br * hole, by - br * hole)
        c.circle(bx, by, br * hole + 24, outline=paper, width=50)
        if model.startswith('cake'): c.rect(bx - 50, by + br * .45, bx + 50, by + br * .45 + 34, fill=rgba(shade(paper, .92))); c.text(bx, by + br * .45 + 17, weight_of(p), lk.f_sm, 16, ink)
        im = texturize(c.final(), .12, lk.seed, 14, .03, fibres=.12)
        return im
    if surf == 'bare':
        arr_paste(c, leaf_tex(1024, 620, base, tips, lk.seed, 1.4 if model != 'coin' else .6), 0, 0)
        if model == 'coin':
            c.circle(tx, ty, tr * .2, outline=rgba(shade(base, .5)), width=6)
        else:      # the maker's ticket pressed into the top, a ribbon round it
            tw = tr * .34
            if model == 'melon':   # owner 11-15: the ribbons run right up to the ticket (drawn first, the ticket covers their ends)
                for k in range(4):
                    t = math.radians(45 * k); c.line([(tx - tr * math.cos(t), ty - tr * math.sin(t)), (tx + tr * math.cos(t), ty + tr * math.sin(t))], seal, 9)
                    c.line([(bx - br * math.cos(t), by - br * math.sin(t)), (bx + br * math.cos(t), by + br * math.sin(t))], seal, 9)
            c.rect(tx - tw, ty - tw * .8, tx + tw, ty + tw * .8, fill=paper); c.rect(tx - tw + 5, ty - tw * .8 + 5, tx + tw - 5, ty + tw * .8 - 5, outline=ink, width=2)
            if p.get('cjk'): c.text(tx, ty - tw * .3, p['cjk'], CJK, tw * .5, ink, maxw=tw * 1.7)
            c.text(tx, ty + tw * .2, lk.en, lk.f_en, tw * .26, ink, maxw=tw * 1.7); c.text(tx, ty + tw * .52, lk.ru, lk.f_ru, tw * .18, ink, maxw=tw * 1.7)
        return Image.fromarray(np.asarray(c.final()))
    if surf == 'peel':
        a = np.ones((620, 1024, 3)) * np.array([52, 60, 30.0])
        a *= (.7 + .6 * noise(1024, 620, 60, lk.seed, 3))[..., None]; a += np.random.default_rng(lk.seed).normal(0, 6, (620, 1024, 1))
        pores = noise(1024, 620, 250, lk.seed + 5, 1) > .7; a[pores] *= .75
        arr_paste(c, a, 0, 0)
        arr_paste(c, leaf_tex(int(tr * .7), int(tr * .7), base, tips, lk.seed, .6), tx - tr * .35, ty - tr * .35)   # the hole the tea went in by
        c.circle(tx, ty, tr * .35 + 28, outline=(40, 46, 22, 255), width=58); c.circle(tx, ty, tr * .36, outline=(90, 96, 50, 255), width=4)
        c.circle(bx, by, br * .08, fill=(30, 34, 16, 255))
        return c.final()
    # chocolate tablet: wedges scored in, the name pressed in
    brown = np.array([74, 44, 28.0]) if p['id'] != 'BatangasTablea' else np.array([50, 30, 20.0])
    a = np.ones((620, 1024, 3)) * brown; a *= (.88 + .24 * noise(1024, 620, 30, lk.seed, 3))[..., None]; a += np.random.default_rng(lk.seed).normal(0, 5, (620, 1024, 1))
    arr_paste(c, a, 0, 0)
    hi, lo = tuple(int(v * 1.5) for v in brown) + (255,), tuple(int(v * .55) for v in brown) + (255,)
    for cxx, cyy, rr in ((tx, ty, tr), (bx, by, br)):
        for k in range(3):
            t = math.radians(60 * k + 30)
            c.line([(cxx - rr * math.cos(t), cyy - rr * math.sin(t)), (cxx + rr * math.cos(t), cyy + rr * math.sin(t))], lo, 7); c.line([(cxx - rr * math.cos(t) + 4, cyy - rr * math.sin(t) + 4), (cxx + rr * math.cos(t) + 4, cyy + rr * math.sin(t) + 4)], hi, 2)
        c.circle(cxx, cyy, rr * .9, outline=lo, width=5)
    c.circle(tx, ty, tr * .3, fill=tuple(int(v) for v in brown) + (255,), outline=lo, width=5)
    c.text(tx + 2, ty + 2, lk.en.upper(), 'oswald-700', tr * .1, hi, maxw=tr * .5); c.text(tx, ty, lk.en.upper(), 'oswald-700', tr * .1, lo, maxw=tr * .5)
    return c.final()


def P_emboss(p, L, lk):
    """a bare pressed tile: the pattern and words are pressed into the tea itself"""
    c = Canvas(1024, 1024, (40, 28, 18))
    base, tips = TEA_COL.get(p['k'], TEA_COL['pblack'])
    arr_paste(c, leaf_tex(1024, 1024, base, base if p['id'] == 'HankouTile' else tips, lk.seed, .5), 0, 0)
    x0, y0, x1, y1 = L['front']; w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
    hi, lo = rgba(shade(base, 2.6), 255), rgba(shade(base, .3), 255)
    def emb(fn):
        for dx, col in ((2.5, hi), (0, lo)): fn(dx, col)
    emb(lambda d, col: c.rect(x0 + w * .06 + d, y0 + h * .04 + d, x1 - w * .06 + d, y1 - h * .04 + d, outline=col, width=4))
    if p['id'] == 'FourSeals':
        for i, ch in enumerate('福禄寿禧'):
            px_, py_ = x0 + w * (.3 + .4 * (i % 2)), y0 + h * (.3 + .4 * (i // 2))
            emb(lambda d, col: c.text(px_ + d, py_ + d, ch, CJK, w * .3, col))
        emb(lambda d, col: c.line([(cx + d, y0 + h * .08 + d), (cx + d, y1 - h * .08 + d)], col, 3)); emb(lambda d, col: c.line([(x0 + w * .1 + d, (y0 + y1) / 2 + d), (x1 - w * .1 + d, (y0 + y1) / 2 + d)], col, 3))
    else:
        emb(lambda d, col: c.rect(x0 + w * .12 + d, y0 + h * .08 + d, x1 - w * .12 + d, y1 - h * .08 + d, outline=col, width=2))
        emb(lambda d, col: star(c, cx + d, y0 + h * .27 + d, w * .16, col, 5, .45))
        emb(lambda d, col: c.text(cx + d, y0 + h * .47 + d, p.get('cjk') or 'ЧАЙ', CJK if p.get('cjk') else 'kelly-slab-400', w * .22, col))
        emb(lambda d, col: c.text(cx + d, y0 + h * .63 + d, lk.en.upper(), 'kelly-slab-400', w * .1, col, maxw=w * .68))
        emb(lambda d, col: c.text(cx + d, y0 + h * .72 + d, lk.ru.upper(), 'kelly-slab-400', w * .085, col, maxw=w * .68))
        for k in range(5):
            emb(lambda d, col: c.line([(x0 + w * .2 + d, y0 + h * (.8 + .025 * k) + d), (x1 - w * .2 + d, y0 + h * (.8 + .025 * k) + d)], col, 2))
    bx0, by0, bx1, by1 = L['back']
    for i in range(1, 4):       # the break lines on the back
        yy = by0 + (by1 - by0) * i / 4; emb(lambda d, col: c.line([(bx0 + d, yy + d), (bx1 + d, yy + d)], col, 3))
    return c.final()


def cup_piala(liquid, seed=1):
    """a white porcelain tea bowl with a cobalt rim and a few painted sprigs"""
    lay = LAY['piala']; white = (240, 240, 236, 255); blue = (36, 64, 140, 255)
    c = Canvas(1024, 1024, white[:3])
    x0, y0, x1, y1 = lay['outside']
    c.rect(x0, y0 + 10, x1, y0 + 20, fill=blue); c.rect(x0, y0 + 28, x1, y0 + 31, fill=blue)
    for k in range(6):
        cx = 85 + k * 170.7; m_sprig(c, cx, y0 + 190, 70, blue, blue, white)
    c.rect(x0, y1 - 40, x1, y1 - 34, fill=blue)
    rx0, ry0, rx1, ry1 = lay['rim']; c.rect(rx0, ry0, rx1, ry1, fill=blue)
    ix0, iy0, ix1, iy1 = lay['inner']; c.rect(ix0, iy0, ix1, iy1, fill=white); c.rect(ix0, iy0 + 6, ix1, iy0 + 10, fill=blue)
    c.rect(ix0, iy1 - 16, ix1, iy1, fill=rgba(shade(liquid, 1.25)))
    lx, ly, lr = lay['liquid']; c.circle(lx, ly, lr + 6, fill=rgba(liquid)); c.circle(lx, ly, lr * .98, outline=rgba(shade(liquid, 1.3)), width=5)
    l2 = c.layer(); l2.ellipse(lx - lr * .6, ly - lr * .5, lx + lr * .1, ly - lr * .1, fill=(255, 255, 255, 60)); l2.im = l2.im.filter(ImageFilter.GaussianBlur(20 * SS)); c.over(l2)
    bx, by, br = lay['bottom']; c.circle(bx, by, br + 6, fill=(226, 224, 216, 255)); c.circle(bx, by, br * .5, outline=blue, width=3)
    return texturize(c.final(), .02, seed, 30, .008)


def _cup_common(c, lay, liquid, inner, crema=False):
    ix0, iy0, ix1, iy1 = lay['inner']; c.rect(ix0, iy0, ix1, iy1, fill=inner)
    c.rect(ix0, iy1 - 16, ix1, iy1, fill=rgba(shade(liquid, 1.25)))
    lx, ly, lr = lay['liquid']; c.circle(lx, ly, lr + 6, fill=rgba(liquid))
    if crema: c.circle(lx, ly, lr * .99, outline=rgba(shade(liquid, 2.2) if sum(liquid) < 200 else shade(liquid, 1.3)), width=14)
    else: c.circle(lx, ly, lr * .98, outline=rgba(shade(liquid, 1.3)), width=5)
    l2 = c.layer(); l2.ellipse(lx - lr * .6, ly - lr * .5, lx + lr * .1, ly - lr * .1, fill=(255, 255, 255, 55)); l2.im = l2.im.filter(ImageFilter.GaussianBlur(20 * SS)); c.over(l2)


def cup_espresso(liquid, seed=1):
    """a white porcelain demitasse with a thin gold line and a band of the maker's colour"""
    lay = LAY['espresso']; white = (244, 243, 238, 255); gold = (196, 160, 70, 255)
    band = [(150, 30, 34, 255), (28, 40, 90, 255), (24, 24, 26, 255), (30, 96, 70, 255)][seed % 4]
    c = Canvas(1024, 1024, white[:3])
    x0, y0, x1, y1 = lay['outside']
    c.rect(x0, y0 + 8, x1, y0 + 12, fill=gold); c.rect(x0, y0 + 40, x1, y0 + 120, fill=band); c.rect(x0, y0 + 126, x1, y0 + 129, fill=gold)
    rx0, ry0, rx1, ry1 = lay['rim']; c.rect(rx0, ry0, rx1, ry1, fill=gold)
    hx0, hy0, hx1, hy1 = lay['handle']; c.rect(hx0, hy0 - 4, hx1, hy1 + 4, fill=white)
    _cup_common(c, lay, liquid, white, crema=True)
    bx, by, br = lay['bottom']; c.circle(bx, by, br + 6, fill=(232, 230, 222, 255))
    return c.final()


GLAZES = [((176, 74, 44), (120, 52, 30)), ((40, 70, 130), (24, 40, 80)), ((196, 150, 50), (140, 100, 30)), ((52, 96, 72), (30, 60, 44)),
          ((226, 218, 196), (180, 168, 140)), ((90, 60, 50), (60, 40, 32))]


def cup_ceramic(liquid, seed=1, glaze=0):
    """a stoneware mug: a glaze running down unevenly to a band of bare clay at the foot, the inside cream"""
    lay = LAY['ceramic']; g, dark = GLAZES[glaze % len(GLAZES)]; clay = (184, 150, 116, 255); cream = (238, 230, 212, 255)
    c = Canvas(1024, 1024, g)
    x0, y0, x1, y1 = lay['outside']
    a = np.zeros((y1 - y0, 1024)) + np.array(g, float).mean()
    lay2 = c.layer()
    rng = np.random.default_rng(seed)
    edge = y1 - 70 + np.cumsum(rng.normal(0, 3, 64)).clip(-20, 20)
    pts = [(0, y1)] + [(k * 1024 / 63, edge[k]) for k in range(64)] + [(1024, y1)]
    c.rect(x0, y1 - 90, x1, y1, fill=clay)
    c.poly([(0, y0)] + [(k * 1024 / 63, edge[k]) for k in range(64)] + [(1024, y0)], fill=g + (255,))
    # the glaze is thicker where it pooled at its lower edge: one soft darker line (owner 11-15: the row of dots read as beads)
    c.line([(k * 1024 / 63, edge[k] - 5) for k in range(64)], dark + (150,), 9)
    lay2.rect(x0, y0, x1, y0 + 60, fill=(255, 255, 255, 18)); c.over(lay2)
    rx0, ry0, rx1, ry1 = lay['rim']; c.rect(rx0, ry0, rx1, ry1, fill=dark + (255,))
    hx0, hy0, hx1, hy1 = lay['handle']; c.rect(hx0, hy0 - 4, hx1, hy1 + 4, fill=g + (255,))
    _cup_common(c, lay, liquid, cream)
    bx, by, br = lay['bottom']; c.circle(bx, by, br + 6, fill=clay)
    return texturize(c.final(), .03, seed, 30, .0)


def cup_camp(liquid, seed=1):
    """a plain steel camp cup, inside and out"""
    lay = LAY['camp']
    c = Canvas(1024, 1024, (170, 172, 176))
    c.im.alpha_composite(metal(1024, 1024, (176, 178, 182), seed).resize((1024 * SS, 1024 * SS)))
    x0, y0, x1, y1 = lay['outside']
    c.rect(x0, y1 - 24, x1, y1 - 20, fill=(130, 132, 138, 255))
    rx0, ry0, rx1, ry1 = lay['rim']; c.rect(rx0, ry0, rx1, ry1, fill=(204, 206, 210, 255))
    _cup_common(c, lay, liquid, (160, 162, 166, 255))
    bx, by, br = lay['bottom']; c.circle(bx, by, br + 6, fill=(150, 152, 156, 255)); c.circle(bx, by, br * .8, outline=(120, 122, 128, 255), width=3)
    return c.final()


def cup_yunomi(liquid, seed=1):
    """a speckled grey-green stoneware cup with one brush stroke round it"""
    lay = LAY['yunomi']; body = [(170, 176, 160), (196, 184, 160), (120, 130, 120)][seed % 3]; ink = (60, 56, 52, 255)
    c = Canvas(1024, 1024, body)
    x0, y0, x1, y1 = lay['outside']
    rng = random.Random(seed)
    for _ in range(900):
        x, y = rng.uniform(0, 1024), rng.uniform(y0, y1)
        r = rng.uniform(1, 3); c.circle(x, y, r, fill=(90, 80, 70, rng.randint(80, 160)))
    yy = y0 + 160
    pts = [(k * 1024 / 40, yy + 18 * math.sin(k / 40 * 2 * math.pi * 2 + seed)) for k in range(41)]
    c.line(pts, ink, 22)
    c.rect(x0, y1 - 60, x1, y1, fill=(150, 120, 96, 255))
    rx0, ry0, rx1, ry1 = lay['rim']; c.rect(rx0, ry0, rx1, ry1, fill=(110, 100, 90, 255))
    _cup_common(c, lay, liquid, (226, 224, 214, 255))
    bx, by, br = lay['bottom']; c.circle(bx, by, br + 6, fill=(150, 120, 96, 255))
    return texturize(c.final(), .03, seed, 30, .0)


KIND = {'box': P_box, 'tin': P_tin, 'cyl': P_cyl, 'jar': P_jar, 'can': P_can, 'cake': P_cake}


def plan():
    """every product's look, decided in catalogue order so that it does not depend on what is being painted"""
    out = {}
    for p in NEW:
        kind = LAY[p['model']]['kind']
        out[p['id']] = (kind, look_of(p, kind))
    return out


def paint(args):
    p, kind, lk = args
    if not lk.rus: p = dict(p); p['sort_ru'] = ''
    L = dict(LAY[p['model']])
    fn = P_emboss if p['id'] in ('HankouTile', 'FourSeals') else KIND[kind]
    im = fn(p, L, lk)
    im.convert('RGB').save(os.path.join(OUT, 'textures', p['id'] + '.png'), optimize=True)
    return p['id']


# owner 11-15: glass-holders of different metals and patterns
HOLDER_METALS = [((198, 198, 204), (110, 110, 116), (236, 236, 242)),   # silver
                 ((216, 182, 96), (128, 92, 34), (250, 228, 160)),     # gold
                 ((184, 188, 184), (96, 100, 98), (226, 230, 226)),    # cupronickel
                 ((192, 158, 92), (104, 76, 36), (236, 210, 150)),     # brass
                 ((168, 104, 72), (84, 46, 30), (226, 168, 132)),      # copper
                 ((128, 128, 136), (40, 40, 46), (214, 214, 222))]     # blackened silver


def holder_pattern(c, kind, hx0, hy0, hx1, hy1, dark, light):
    cy = (hy0 + hy1) // 2 - 6
    if kind == 0:     # ovals and stars, the classic
        for k in range(0, 1024, 128):
            cx_ = k + 64
            c.ellipse(cx_ - 46, cy - 60, cx_ + 46, cy + 60, outline=dark + (255,), width=5)
            c.ellipse(cx_ - 30, cy - 42, cx_ + 30, cy + 42, outline=light + (255,), width=3)
            star(c, cx_, cy, 18, dark + (255,), 6, .45)
    elif kind == 1:   # fluted: tall grooves
        for k in range(0, 1024, 32):
            c.rect(k + 4, hy0 + 64, k + 12, hy1 - 84, fill=dark + (200,))
            c.rect(k + 14, hy0 + 64, k + 18, hy1 - 84, fill=light + (200,))
    elif kind == 2:   # diamond lattice
        for k in range(-128, 1100, 64):
            c.line([(k, hy0 + 64), (k + 150, hy1 - 84)], dark + (255,), 4)
            c.line([(k + 150, hy0 + 64), (k, hy1 - 84)], dark + (255,), 4)
        for k in range(0, 1024, 64):
            c.circle(k + 32 - (0 if (k // 64) % 2 else 0), cy, 6, fill=light + (255,))
    elif kind == 3:   # laurel wreath: a stem with leaves
        c.rect(0, cy - 3, 1024, cy + 3, fill=dark + (255,))
        for k in range(0, 1024, 64):
            for sgn in (-1, 1):
                x0 = k + 8; y0 = cy + sgn * 6
                c.poly([(x0, y0), (x0 + 26, y0 + sgn * 60), (x0 + 50, y0 + sgn * 20)], fill=dark + (255,))
                c.line([(x0 + 6, y0 + sgn * 6), (x0 + 28, y0 + sgn * 42)], light + (255,), 3)
    else:             # rocket and stars over the hills, a souvenir holder
        for k in range(0, 1024, 256):
            c.poly([(k, hy1 - 90), (k + 70, hy1 - 150), (k + 128, hy1 - 110), (k + 190, hy1 - 160), (k + 256, hy1 - 90)], fill=dark + (255,))
            c.poly([(k + 120, cy - 70), (k + 132, cy - 30), (k + 132, cy + 20), (k + 108, cy + 20), (k + 108, cy - 30)], fill=light + (255,))
            c.line([(k + 120, cy + 24), (k + 120, cy + 56)], dark + (255,), 6)
            for j in range(4):
                star(c, k + 30 + 60 * j, cy - 80 + (j % 2) * 30, 10, light + (255,), 5, .45)


def cup_holder(liquid, seed, metal_k, pattern):
    lay = L1.LAY['tea_glass']
    base, dark, light = HOLDER_METALS[metal_k]
    c = Canvas(1024, 1024, base)
    c.im.alpha_composite(metal(1024, 1024, base, seed).resize((1024 * SS, 1024 * SS)))
    x0, y0, x1, y1 = lay['glass']
    tea = tuple(liquid)
    c.rect(x0, y0, x1, y1, fill=rgba(shade(tea, 1.0)))
    c.rect(x0, y0, x1, y0 + 80, fill=(214, 220, 222, 255))
    c.rect(x0, y0 + 76, x1, y0 + 84, fill=rgba(shade(tea, 1.35)))
    hx0, hy0, hx1, hy1 = lay['holder']
    c.rect(hx0, hy0 + 20, hx1, hy0 + 26, fill=dark + (255,))
    dots_band(c, hx0, hy0 + 40, hx1, 5, light + (255,), 18)
    holder_pattern(c, pattern, hx0, hy0, hx1, hy1, dark, light)
    c.rect(hx0, hy1 - 70, hx1, hy1 - 64, fill=dark + (255,))
    ix0, iy0, ix1, iy1 = lay['inner']
    c.rect(ix0, iy0 + 50, ix1, iy1, fill=(214, 220, 222, 255))
    c.rect(ix0, iy1 - 8, ix1, iy1, fill=rgba(shade(tea, 1.2)))
    lx, ly, lr = lay['liquid']
    c.circle(lx, ly, lr + 6, fill=rgba(shade(tea, .85)))
    lay2 = c.layer(); lay2.ellipse(lx - lr * .6, ly - lr * .5, lx + lr * .1, ly - lr * .1, fill=(255, 255, 255, 50)); lay2.im = lay2.im.filter(ImageFilter.GaussianBlur(20 * SS)); c.over(lay2)
    c.circle(lx, ly, lr * .98, outline=rgba(shade(tea, 1.3)), width=4)
    hx0_, hy0_, hx1_, hy1_ = lay['handle']
    hm = metal(1024, hy1_ - hy0_ + 6, base, seed).resize((1024 * SS, (hy1_ - hy0_ + 6) * SS))
    c.im.paste(hm, (0, (hy0_ - 4) * SS))
    return texturize(c.final(), .03, seed, 30, .01)


def paint_cup(key):
    parts = key.split('_'); vessel, liq = parts[1], parts[2]
    seed = zlib.crc32(key.encode()) % 1000
    fn = {'glass': L1.cup_glass, 'mug': L1.cup_mug, 'piala': cup_piala, 'espresso': cup_espresso, 'camp': cup_camp, 'yunomi': cup_yunomi}.get(vessel)
    if vessel == 'ceramic': im = cup_ceramic(LIQ[liq], seed, int(parts[3]))
    elif vessel == 'glass' and len(parts) > 3: im = cup_holder(LIQ[liq], seed, int(parts[3]) % 6, int(parts[3]) // 6)
    else: im = fn(LIQ[liq], seed)
    im.convert('RGB').resize((512, 512), Image.LANCZOS).save(os.path.join(OUT, 'textures', key + '.png'), optimize=True)
    return key


if __name__ == '__main__':
    from multiprocessing import Pool
    only = sys.argv[2:]
    pl = plan()
    jobs = [(p, *pl[p['id']]) for p in NEW if not only or p['id'] in only]
    cups = sorted({p['cuptex'] for p in NEW if p['cuptex']}) if not only or 'cups' in only else []
    with Pool(4) as pool:
        for r in pool.imap_unordered(paint, jobs): print('pack', r, flush=True)
        for r in pool.imap_unordered(paint_cup, cups): print('cup', r, flush=True)
