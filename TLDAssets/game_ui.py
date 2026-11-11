# Game-look drawing for the launcher: the NGUI atlas and fonts are read from the player's own game
# (nothing from the game ships with the launcher) and cached; Pillow composes the window like the game does.
import os, sys, json, glob, hashlib, math, random
from PIL import Image, ImageDraw, ImageFont, ImageChops, ImageFilter

# bundles of game 2.59/2.60 that hold them; any other bundle is scanned if these are gone
KNOWN_ATLAS = ['63b75d5f43aa3b4aa3dcfadc5c6e098e.bundle']
KNOWN_FONTS = ['6d3223da354de646ab79d5660dcac9d2.bundle']
ATLAS_TEX = 'Base Atlas_hd'
FONT_RU, FONT_EN = 'Nazhdak-Regular', 'PiS - PiS Creatinin Pro'

def cache_dir():
    base = os.environ.get('LOCALAPPDATA') or os.path.expanduser('~/.cache')
    d = os.path.join(base, 'SeagullMods', 'ui'); os.makedirs(d, exist_ok=True); return d

def _bundles_dir(game):
    return os.path.join(game, 'tld_Data', 'StreamingAssets', 'aa', 'StandaloneWindows64')

def extract(game, log=lambda s: None):
    """Writes atlas.png, sprites.json, ru.ttf, en.ttf to the cache. Returns the cache dir or None."""
    d = cache_dir()
    need = [os.path.join(d, n) for n in ('atlas.png', 'sprites.json', 'ru.ttf', 'en.ttf')]
    if all(os.path.isfile(p) for p in need): return d
    import UnityPy
    bd = _bundles_dir(game)
    if not os.path.isdir(bd): return None
    allb = sorted(os.path.basename(p) for p in glob.glob(os.path.join(bd, '*.bundle')))
    def order(known): return [b for b in known if b in allb] + [b for b in allb if b not in known]
    got_atlas = os.path.isfile(need[0]) and os.path.isfile(need[1])
    got_fonts = os.path.isfile(need[2]) and os.path.isfile(need[3])
    for b in order(KNOWN_ATLAS):
        if got_atlas: break
        try: env = UnityPy.load(os.path.join(bd, b))
        except Exception: continue
        tex = None
        for o in env.objects:
            if o.type.name == 'Texture2D':
                try:
                    if o.peek_name() == ATLAS_TEX: tex = o; break
                except Exception: pass
        if not tex: continue
        best = None
        for o in env.objects:
            if o.type.name != 'MonoBehaviour': continue
            try: t = o.read_typetree()
            except Exception: continue
            sp = t.get('mSprites')
            if sp and (best is None or len(sp) > len(best)): best = sp
        if not best: continue
        tex.read().image.save(need[0])
        json.dump(best, open(need[1], 'w'))
        got_atlas = True; log('ui: atlas from ' + b)
    for b in order(KNOWN_FONTS):
        if got_fonts: break
        try: env = UnityPy.load(os.path.join(bd, b))
        except Exception: continue
        found = {}
        for o in env.objects:
            if o.type.name != 'Font': continue
            try:
                n = o.peek_name()
                if n in (FONT_RU, FONT_EN):
                    data = o.read_typetree().get('m_FontData')
                    if data: found[n] = bytes(data)
            except Exception: pass
        if FONT_RU in found and FONT_EN in found:
            open(need[2], 'wb').write(found[FONT_RU]); open(need[3], 'wb').write(found[FONT_EN])
            got_fonts = True; log('ui: fonts from ' + b)
    return d if got_atlas and got_fonts else None

# ---------------- drawing ----------------
def sysfont():
    for p in (r'C:\Windows\Fonts\segoeui.ttf', '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'):
        if os.path.isfile(p): return p
    return None

class Skin:
    """Atlas sprites + fonts; without the game files everything is drawn as plain shapes."""
    def __init__(self, d=None, ru=True):
        self.atlas = None; self.sp = {}
        self.font_path = sysfont(); self.cyr_path = None   # the EN game font has no Cyrillic
        if d:
            try:
                self.atlas = Image.open(os.path.join(d, 'atlas.png')).convert('RGBA')
                self.sp = {s['name']: s for s in json.load(open(os.path.join(d, 'sprites.json')))}
                f = os.path.join(d, 'ru.ttf' if ru else 'en.ttf')
                if os.path.isfile(f): self.font_path = f
                f = os.path.join(d, 'ru.ttf')
                if not ru and os.path.isfile(f): self.cyr_path = f
            except Exception:
                self.atlas = None; self.sp = {}
        self._fonts = {}; self._crops = {}
    def font(self, size, t=''):
        size = max(6, int(size))
        path = self.cyr_path if self.cyr_path and any('\u0400' <= ch <= '\u04ff' for ch in t) else self.font_path
        k = (size, path)
        if k not in self._fonts:
            try: self._fonts[k] = ImageFont.truetype(path, size) if path else ImageFont.load_default()
            except Exception: self._fonts[k] = ImageFont.load_default()
        return self._fonts[k]
    def has(self, n): return self.atlas is not None and n in self.sp
    def crop(self, n):
        if n not in self._crops:
            s = self.sp[n]; self._crops[n] = self.atlas.crop((s['x'], s['y'], s['x'] + s['width'], s['y'] + s['height']))
        return self._crops[n]

def _tint(im, c):
    r, g, b, a = c
    return ImageChops.multiply(im, Image.new('RGBA', im.size, (int(r * 255), int(g * 255), int(b * 255), int(a * 255))))

def _slice9(skin, n, w, h):
    s = skin.sp[n]; im = skin.crop(n)
    L, R, T, B = s['borderLeft'], s['borderRight'], s['borderTop'], s['borderBottom']
    sw, sh = im.size
    L2, R2, T2, B2 = L, R, T, B
    if L2 + R2 > w: k = w / (L2 + R2); L2 = int(L2 * k); R2 = int(R2 * k)
    if T2 + B2 > h: k = h / (T2 + B2); T2 = int(T2 * k); B2 = int(B2 * k)
    out = Image.new('RGBA', (w, h))
    xs = [(0, L, 0, L2), (L, sw - R, L2, w - R2), (sw - R, sw, w - R2, w)]
    ys = [(0, T, 0, T2), (T, sh - B, T2, h - B2), (sh - B, sh, h - B2, h)]
    for a0, a1, b0, b1 in xs:
        for c0, c1, d0, d1 in ys:
            if a1 <= a0 or c1 <= c0 or b1 <= b0 or d1 <= d0: continue
            out.paste(im.crop((a0, c0, a1, c1)).resize((b1 - b0, d1 - d0), Image.LANCZOS), (b0, d0))
    return out

# caches: the window is redrawn on every hover, so sprites, text and widths are made once
_SPR, _TXT, _TW = {}, {}, {}

def clear_caches():
    _SPR.clear(); _TXT.clear(); _TW.clear()

def _comp(dst, src, x, y):
    if x < 0 or y < 0:
        if -x >= src.width or -y >= src.height: return
        src = src.crop((max(0, -x), max(0, -y), src.width, src.height)); x = max(0, x); y = max(0, y)
    if x >= dst.width or y >= dst.height: return
    dst.alpha_composite(src, (x, y))

class Painter:
    """Top-left pixel coordinates (unlike NGUI's centre/y-up); (x, y) of sprites is their centre."""
    def __init__(self, skin, im):
        self.s = skin; self.im = im
    def spr(self, n, cx, cy, w, h, c=(1, 1, 1, 1), sliced=False, fallback=None):
        w, h = max(1, int(w)), max(1, int(h))
        if self.s.has(n):
            k = (n, w, h, tuple(c), sliced)
            im = _SPR.get(k)
            if im is None:
                im = _slice9(self.s, n, w, h) if sliced else self.s.crop(n).resize((w, h), Image.LANCZOS)
                im = _tint(im, c)
                if len(_SPR) > 600: _SPR.clear()
                _SPR[k] = im
            _comp(self.im, im, int(cx - w / 2), int(cy - h / 2))
        elif fallback:
            fallback(self, cx, cy, w, h, c)
    def rect(self, x, y, w, h, c, outline=None):
        k = ('rect', max(1, int(w)), max(1, int(h)), tuple(c), tuple(outline) if outline else None)
        lay = _SPR.get(k)
        if lay is None:
            lay = Image.new('RGBA', k[1:3], tuple(int(v * 255) for v in c))
            if outline:
                ImageDraw.Draw(lay).rectangle((0, 0, k[1] - 1, k[2] - 1), outline=tuple(int(v * 255) for v in outline))
            _SPR[k] = lay
        _comp(self.im, lay, int(x), int(y))
    def tw(self, t, size):
        k = (t, size)
        if k not in _TW:
            if len(_TW) > 5000: _TW.clear()
            b = self.s.font(size, t).getbbox(t); _TW[k] = b[2] - b[0]
        return _TW[k]
    def text(self, t, x, y, size, c=(1, 1, 1, 1), al='l', shadow=True):
        """y is the vertical centre of the line."""
        w = self.tw(t, size)
        if al == 'c': x -= w / 2
        elif al == 'r': x -= w
        yy = y - size * 0.62
        k = (t, size, tuple(c), shadow)
        lay = _TXT.get(k)
        if lay is None:
            f = self.s.font(size, t); b = f.getbbox(t)
            lay = Image.new('RGBA', (max(1, b[2] + 4), max(1, b[3] + 4)))
            d = ImageDraw.Draw(lay)
            if shadow: d.text((2, 2), t, font=f, fill=(0, 0, 0, int(140 * c[3])))
            d.text((1, 1), t, font=f, fill=tuple(int(v * 255) for v in c))
            if len(_TXT) > 3000: _TXT.clear()
            _TXT[k] = lay
        _comp(self.im, lay, int(round(x)) - 1, int(round(yy)) - 1)
        return w
    def wrap(self, t, size, width):
        words = t.split(); lines = []; cur = ''
        for wd in words:
            nx = (cur + ' ' + wd).strip()
            if self.tw(nx, size) <= width or not cur: cur = nx
            else: lines.append(cur); cur = wd
        if cur: lines.append(cur)
        return lines

# ---------------- pieces, like the game's own windows ----------------
TEAL = (0.43, 0.51, 0.49, 1); WHITE = (1, 1, 1, 1); GREY = (0.78, 0.78, 0.78, 1)
DIM = (0.60, 0.62, 0.61, 1); GREEN = (0.55, 0.80, 0.58, 1); AMBER = (0.93, 0.72, 0.35, 1); BLUE = (0.55, 0.72, 0.90, 1)
RED = (0.85, 0.40, 0.40, 1)

def _fb_panel(p, cx, cy, w, h, c):
    p.rect(cx - w / 2, cy - h / 2, w, h, (0.07, 0.08, 0.09, 0.80), outline=(1, 1, 1, 0.10))

def window(p, x, y, w, h):
    cx, cy = x + w / 2, y + h / 2
    p.spr('clothing_slotSelected', cx, cy, w + 82, h + 80, (0.98, 0.98, 0.98, 0.20), True)
    p.spr('inv_statBack', cx, cy, w, h, (0, 0, 0, 0.62), True, fallback=_fb_panel)
    p.spr('innerGlow2', cx, cy, w, h, (0, 0, 0, 0.24), True)
    m = min(w * 0.8, h * 0.75)
    p.spr('bg_texture1', cx, cy - 10, m * 0.76, m, (0.98, 0.98, 0.98, 0.07))

def title(p, t, cx, cy, size=26):
    p.spr('backHeader', cx, cy + 4, max(240, p.tw(t, size) + 120), 90, (0.98, 0.98, 0.98, 0.45))
    p.text(t, cx, cy, size, WHITE, al='c')

def line(p, x, y, w, a=0.59):
    if p.s.has('linebreak_description'):
        p.spr('linebreak_description', x + w / 2, y + 1, w, 8, (0, 0, 0, 0.24))
        p.spr('linebreak_description', x + w / 2, y, w, 8, (a, a, a, 1))
    else:
        p.rect(x, y, w, 1, (a, a, a, 0.6))

def _fb_btn(p, cx, cy, w, h, c):
    p.rect(cx - w / 2, cy - h / 2, w, h, (c[0], c[1], c[2], max(c[3], 0.25)))

def button(p, t, cx, cy, w, h, state='n', size=15):
    """state: n normal, h hover, d disabled."""
    if state == 'h': fill = (0.43, 0.51, 0.49, 0.30); outer = (0.98, 0.98, 0.98, 0.78); lab = (0.98, 0.98, 0.98, 0.95)
    elif state == 'd': fill = (0, 0, 0, 0.12); outer = (0.98, 0.98, 0.98, 0.15); lab = (0.98, 0.98, 0.98, 0.25)
    else: fill = (0, 0, 0, 0.45); outer = (0.98, 0.98, 0.98, 0.39); lab = (0.98, 0.98, 0.98, 0.70)
    p.spr('menuButtonCenter_fill', cx, cy, w, h, fill, fallback=_fb_btn)
    p.spr('menuButtonCenter_outer', cx, cy, w * 1.24, h * 2.13, outer)
    p.text(t, cx, cy - 1, size, lab, al='c')

def _fb_check(p, cx, cy, w, h, c):
    p.rect(cx - w / 2, cy - h / 2, w, h, (0, 0, 0, 0.3), outline=(1, 1, 1, c[3]))

def check(p, cx, cy, on, size=20):
    p.spr('checkboxA', cx, cy, size, size, (0.98, 0.98, 0.98, 0.60 if on else 0.32), fallback=_fb_check)
    if on:
        if p.s.has('checkbox_checkmarkA'): p.spr('checkbox_checkmarkA', cx, cy + 1, size, size, (1, 1, 1, 0.95))
        else: p.text('✓', cx, cy, size * 0.8, WHITE, al='c', shadow=False)

def progress(p, x, y, w, frac):
    p.rect(x, y, w, 4, (0, 0, 0, 0.45))
    if frac > 0: p.rect(x, y, w * min(1, frac), 4, (0.62, 0.74, 0.70, 0.9))

def scene(W, H, seed=5):
    """The launcher's own backdrop (drawn, not from the game): a dusk slope with firs."""
    random.seed(seed)
    g = Image.new('RGB', (W, H)); d = ImageDraw.Draw(g)
    for y in range(H):
        k = y / H; d.line([(0, y), (W, y)], fill=(int(70 - 35 * k), int(84 - 38 * k), int(98 - 40 * k)))
    for i, (base, col) in enumerate([(0.55, (120, 130, 140)), (0.68, (150, 158, 166)), (0.8, (185, 190, 196))]):
        pts = [(0, H)]
        for x in range(0, W + 40, 40): pts.append((x, H * base + 40 * math.sin(x / (170 + 60 * i) + i * 2) + random.uniform(-6, 6)))
        pts.append((W, H)); d.polygon(pts, fill=col)
    for i in range(34):
        x = random.uniform(0, W); y = H * random.uniform(0.5, 0.75); h = random.uniform(40, 120)
        d.polygon([(x, y - h), (x - h * 0.22, y), (x + h * 0.22, y)], fill=(28, 36, 38))
    g = g.filter(ImageFilter.GaussianBlur(3))
    im = g.convert('RGBA')
    im.alpha_composite(Image.new('RGBA', im.size, (0, 0, 0, 70)))
    return im
