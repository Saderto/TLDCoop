"""Engravings and inlays for the lighter collection (11-10).
Icons: game-icons.net (CC BY 3.0, authors Lorc, Delapouite and others), from the @iconify-json/game-icons package (tools/icons_src).
A decal is an RGBA picture laid on a face: RGB is multiplied into the metal (grooves dark, white = untouched) unless 'mix' (enamel)."""
import io, json, os, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
_ICONS = None


def icon_mask(name, px):
    """white-on-black mask of a game icon, px x px"""
    global _ICONS
    import cairosvg
    if _ICONS is None:
        _ICONS = json.load(open(os.path.join(HERE, 'icons_src', 'game-icons.json')))
    body = _ICONS['icons'][name]['body'].replace('currentColor', '#ffffff')
    svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="%d" height="%d">%s</svg>' % (px, px, body)
    png = cairosvg.svg2png(bytestring=svg.encode())
    im = Image.open(io.BytesIO(png)).convert('RGBA')
    return im.split()[3]


def font(path, size):
    return ImageFont.truetype(os.path.join(HERE, '..', 'fonts', path), size)


def engraving(mask, depth=.42, hatch=True, outline=True):
    """mask (L) -> RGB multiply picture: hatched dark fill, darker rim, a bright lip on the lower right like a cut catching light"""
    w, h = mask.size
    m = np.array(mask, np.float32) / 255
    fill = 1 - depth * m
    if hatch:
        yy, xx = np.mgrid[0:h, 0:w]
        lines = (np.sin((xx + yy) * 2 * math.pi / max(3, w / 60)) > .2).astype(np.float32)
        fill -= .18 * m * lines
    if outline:
        er = np.array(mask.filter(ImageFilter.MinFilter(5)), np.float32) / 255
        rim = np.clip(m - er, 0, 1)
        fill -= .35 * rim
        sh = np.roll(np.roll(m, 2, 0), 2, 1)
        lip = np.clip(sh - m, 0, 1)
        fill += .5 * lip
    v = np.clip(fill, 0, 1.6)
    rgb = np.stack([v, v, v], -1)
    return rgb


def to_img(rgb, alpha=None):
    a = np.clip(rgb, 0, 2) / 2  # store 0..2 as 0..1 so highlights (>1) survive; material scales by 2
    im = Image.fromarray((a * 255).astype(np.uint8), 'RGB')
    return im


def decal(size_mm, draw, ppm=12):
    """size_mm (w, h) of the face; draw(canvas_mask_L, W, H, ppm) paints white where engraved; returns multiply RGB image (x2 encoded)"""
    W, H = int(size_mm[0] * ppm), int(size_mm[1] * ppm)
    m = Image.new('L', (W, H), 0)
    draw(m, W, H, ppm)
    return to_img(engraving(m))


def place_icon(m, name, cx, cy, s):
    ic = icon_mask(name, int(s))
    m.paste(255, (int(cx - s / 2), int(cy - s / 2)), ic)


def text(m, s, cx, cy, size, fnt='cinzel-latin-700-normal.woff'):
    d = ImageDraw.Draw(m)
    f = font(fnt, int(size))
    bb = d.textbbox((0, 0), s, font=f)
    d.text((cx - (bb[2] - bb[0]) / 2 - bb[0], cy - (bb[3] - bb[1]) / 2 - bb[1]), s, font=f, fill=255)


def guilloche(m, W, H, step=6, width=1):
    d = ImageDraw.Draw(m)
    for i in range(-H, W, step):
        d.line([(i, 0), (i + H, H)], fill=255, width=width)
        d.line([(i + H, 0), (i, H)], fill=255, width=width)


def enamel(size_mm, draw, ppm=12):
    """colour inlay: draw(ImageDraw RGBA, W, H) -> RGBA, alpha = cover"""
    W, H = int(size_mm[0] * ppm), int(size_mm[1] * ppm)
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    draw(ImageDraw.Draw(im), W, H, ppm)
    return im
