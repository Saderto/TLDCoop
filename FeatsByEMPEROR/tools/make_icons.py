# Builds the 12 feat badges in the style of the game's own (watercolor disc, rough ring, flat silhouette).
# Texture comes from the vanilla Celestial Navigator badge with its stars painted out; colours and glyphs are ours.
import numpy as np, cv2, cairosvg, io, sys, os
from PIL import Image

VAN = './vanilla_badges'
OUT = sys.argv[1] if len(sys.argv) > 1 else './icons'
S = 512
rng = np.random.default_rng(7)

def load(n): return np.array(Image.open(f'{VAN}/{n}.png').convert('RGBA')).astype(np.float32)

base = load('badge_feat_CelestialNavigator')
alpha = base[..., 3] / 255.
lum = base[..., :3].mean(2)
yy, xx = np.mgrid[0:S, 0:S]
cx, cy = 259.5, 257.5
r = np.hypot(xx - cx, yy - cy)
ring = (lum > 140) & (r > 175) & (alpha > 0.05)
ring = cv2.morphologyEx(ring.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8)) > 0
inner = (alpha > 0.5) & ~ring
# paint out stars/lines inside the disc
med = cv2.medianBlur(lum.astype(np.uint8), 31).astype(np.float32)
stars = inner & (lum > med + 18)
stars = cv2.dilate(stars.astype(np.uint8), np.ones((7, 7), np.uint8)) > 0
img8 = base[..., :3].astype(np.uint8)
clean = cv2.inpaint(img8, (stars & inner).astype(np.uint8) * 255, 9, cv2.INPAINT_TELEA).astype(np.float32)
L = clean.mean(2)
lo, hi = np.percentile(L[inner], 3), np.percentile(L[inner], 97)
Tin = np.clip((L - lo) / (hi - lo), 0, 1)
lo2, hi2 = np.percentile(lum[ring], 3), np.percentile(lum[ring], 97)
Tring = np.clip((lum - lo2) / (hi2 - lo2), 0, 1)

def hexc(h): h = h.lstrip('#'); return np.array([int(h[i:i+2], 16) for i in (0, 2, 4)], np.float32)

def noise(scale, seed):
    g = np.random.default_rng(seed).random((S // scale + 2, S // scale + 2)).astype(np.float32)
    return cv2.resize(g, (S, S), interpolation=cv2.INTER_CUBIC)

def svg_mask(body):
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="{S}" height="{S}" viewBox="0 0 {S} {S}">{body}</svg>'
    png = cairosvg.svg2png(bytestring=svg.encode(), output_width=S, output_height=S)
    a = np.array(Image.open(io.BytesIO(png)).convert('RGBA')).astype(np.float32)
    return a[..., 3] / 255., a[..., :3]

def roughen(m, seed, amp=2.2):
    # wobble the outline a little, like a brush edge
    dx = (noise(24, seed) - .5) * 2 * amp; dy = (noise(24, seed + 1) - .5) * 2 * amp
    mapx = (xx + dx).astype(np.float32); mapy = (yy + dy).astype(np.float32)
    m = cv2.remap(m.astype(np.float32), mapx, mapy, cv2.INTER_LINEAR)
    return cv2.GaussianBlur(m, (0, 0), 0.8)

def badge(spec, seed):
    k = seed % 4
    tin = np.rot90(Tin, k); tri = np.rot90(Tring, 0)
    fl = np.rot90(np.fliplr(Tin), (k + 1) % 4)
    bg0, bg1 = hexc(spec['bg'][0]), hexc(spec['bg'][1])
    rg0, rg1 = hexc(spec['ring'][0]), hexc(spec['ring'][1])
    gl0, gl1 = hexc(spec['glyph'][0]), hexc(spec['glyph'][1])
    out = np.zeros((S, S, 3), np.float32)
    t = tin[..., None]
    out[:] = bg1 + (bg0 - bg1) * t            # bg0 = light, bg1 = dark
    # small paint specks
    sp = (np.random.default_rng(seed).random((S, S)) > 0.9975).astype(np.float32)
    sp = cv2.GaussianBlur(cv2.dilate(sp, np.ones((3, 3))), (0, 0), 0.7)
    out = out + (bg0 * 1.08 - out) * np.clip(sp * 1.6, 0, 0.6)[..., None] * (r < 190)[..., None]
    # glyph
    gm, gcol = svg_mask(spec['svg'])
    gm = roughen(gm, seed + 10)
    is_hi = (gcol.mean(2) > 127).astype(np.float32)        # white-filled parts of the svg = emblem in light colour
    gt = (0.35 + 0.65 * fl)[..., None]
    gcolor = gl1 + (gl0 - gl1) * gt
    emb = spec.get('emblem')
    if emb is not None:
        e0, e1 = hexc(emb[0]), hexc(emb[1])
        ecolor = e1 + (e0 - e1) * gt
        is_hi_s = roughen(is_hi * (gm > 0.5), seed + 20, 1.2)[..., None]
        gcolor = gcolor * (1 - is_hi_s) + ecolor * is_hi_s
    out = out * (1 - gm[..., None]) + gcolor * gm[..., None]
    # ring
    rc = rg1 + (rg0 - rg1) * tri[..., None]
    rm = cv2.GaussianBlur(ring.astype(np.float32), (0, 0), 0.6)[..., None]
    out = out * (1 - rm) + rc * rm
    out = np.clip(out, 0, 255)
    rgba = np.dstack([out, base[..., 3]]).astype(np.uint8)
    return Image.fromarray(rgba, 'RGBA')

W = 'fill="#000"'
LIGHT = 'fill="#fff"'
TORSO = ('<ellipse ' + W + ' cx="256" cy="142" rx="44" ry="52"/>'
         '<path ' + W + ' d="M238 180 L274 180 L280 214 L232 214 Z"/>'
         '<path ' + W + ' d="M232 204 C204 210 160 216 146 244 C134 270 136 330 134 452 L378 452 C376 330 378 270 366 244 C352 216 308 210 280 204 Z"/>')

# hand saw teeth along the left edge of the blade
_teeth = ' '.join(f'L{224 - (8 if i % 2 else 0) + i*0.25:.0f} {196 + i*11}' for i in range(22))
SAW = ('<path ' + W + ' d="M226 190 L292 190 L286 420 L252 444 ' + 'L228 432 ' + ' '.join(f'L{224 - (9 if i % 2 else 0)} {426 - i*11}' for i in range(22)) + ' Z"/>')

SPECS = {
 'Butcher': dict(bg=('#f0b09d', '#c9644f'), ring=('#8a2a1e', '#5a140c'), glyph=('#7a2216', '#4f0f08'), emblem=('#f3c3b4', '#d98d7a'), svg=
   '<g transform="rotate(-38 256 270)">'
   '<ellipse ' + W + ' cx="200" cy="270" rx="118" ry="92"/>'
   '<rect ' + W + ' x="300" y="250" width="110" height="38" rx="14"/>'
   '<circle ' + W + ' cx="414" cy="246" r="26"/><circle ' + W + ' cx="414" cy="292" r="26"/>'
   '<path ' + LIGHT + ' d="M118 238 C150 206 210 200 250 222 C214 222 166 230 138 256 Z"/>'
   '<ellipse ' + LIGHT + ' cx="282" cy="270" rx="16" ry="22"/></g>'),
 'Tailor': dict(bg=('#e4c3e0', '#b07aa9'), ring=('#5c2558', '#3a1238'), glyph=('#5a2356', '#3a1036'), svg=
   '<g transform="rotate(42 256 256)">'
   '<path ' + W + ' fill-rule="evenodd" d="M256 70 C268 70 272 90 272 110 L262 420 L256 446 L250 420 L240 110 C240 90 244 70 256 70 Z M256 92 c5 0 6 8 6 18 l0 22 c0 6 -12 6 -12 0 l0 -22 c0 -10 1 -18 6 -18 Z"/></g>'
   '<path fill="none" stroke="#000" stroke-width="13" stroke-linecap="round" d="M196 130 C120 120 96 200 150 236 C210 276 330 214 360 280 C384 336 300 380 250 346 C214 322 236 286 270 300"/>'),
 'WolfHunter': dict(bg=('#c3cdd8', '#7d8fa2'), ring=('#2a3746', '#17202b'), glyph=('#2b3846', '#141c25'), svg=
   '<path ' + W + ' d="M150 420 L168 330 L140 300 L96 292 L82 270 L120 246 L150 210 L176 196 L186 140 L214 176 '
   'L234 168 L262 108 L280 170 C330 190 368 240 384 300 C396 350 396 396 390 420 Z"/>'
   '<circle fill="#fff" cx="196" cy="238" r="8"/>'),
 'BearHide': dict(bg=('#dfbb94', '#a8744a'), ring=('#5a321a', '#3a1d0b'), glyph=('#5a3218', '#371b09'), svg=
   '<path ' + W + ' d="M256 262 C304 262 352 300 364 344 C374 384 346 420 306 414 C286 411 272 402 256 402 C240 402 226 411 206 414 C166 420 138 384 148 344 C160 300 208 262 256 262 Z"/>'
   '<ellipse ' + W + ' cx="140" cy="250" rx="30" ry="40" transform="rotate(-30 140 250)"/>'
   '<ellipse ' + W + ' cx="204" cy="196" rx="30" ry="42" transform="rotate(-12 204 196)"/>'
   '<ellipse ' + W + ' cx="308" cy="196" rx="30" ry="42" transform="rotate(12 308 196)"/>'
   '<ellipse ' + W + ' cx="372" cy="250" rx="30" ry="40" transform="rotate(30 372 250)"/>'
   '<g ' + W + '><path transform="rotate(-30 140 250)" d="M126 220 Q132 194 140 170 Q148 194 154 220 Z"/><path transform="rotate(-12 204 196)" d="M190 164 Q196 138 204 114 Q212 138 218 164 Z"/><path transform="rotate(12 308 196)" d="M294 164 Q300 138 308 114 Q316 138 322 164 Z"/><path transform="rotate(30 372 250)" d="M358 220 Q364 194 372 170 Q380 194 386 220 Z"/></g>'),
 'NightEye': dict(bg=('#3c4880', '#161c3a'), ring=('#efe0a4', '#c4ad62'), glyph=('#f1e3a8', '#c9b066'), svg=
   '<path ' + W + ' fill-rule="evenodd" d="M86 300 C150 214 362 214 426 300 C362 386 150 386 86 300 Z '
   'M256 246 a54 54 0 1 0 0.1 0 Z"/>'
   '<circle ' + W + ' cx="256" cy="300" r="30"/>'
   '<path ' + W + ' d="M300 92 a62 62 0 1 0 62 92 a50 50 0 1 1 -62 -92 Z"/>'),
 'Angler': dict(bg=('#a9ddd5', '#5ea79e'), ring=('#14504a', '#0a3330'), glyph=('#155049', '#0a2f2b'), svg=
   '<path ' + W + ' fill-rule="evenodd" d="M96 262 C150 190 280 186 350 250 L420 196 L404 264 L424 334 L350 284 C280 344 150 338 96 262 Z '
   'M160 246 a12 12 0 1 0 0.1 0 Z"/>'
   '<path fill="none" stroke="#000" stroke-width="12" stroke-linecap="round" d="M256 90 L256 170 C256 200 220 200 218 176"/>'
   '<path ' + W + ' d="M210 186 L222 168 L230 186 Z"/>'),
 'IronStomach': dict(bg=('#d4d39a', '#9c9a55'), ring=('#45441a', '#2b2a0b'), glyph=('#46451a', '#2a290a'), emblem=('#e8e7be', '#c4c288'), svg=
   '<path ' + W + ' d="M256 92 L388 134 L380 284 C372 366 318 414 256 446 C194 414 140 366 132 284 L124 134 Z"/>'
   '<g ' + LIGHT + '><rect x="186" y="168" width="10" height="74" rx="4"/><rect x="204" y="168" width="10" height="74" rx="4"/>'
   '<rect x="222" y="168" width="10" height="74" rx="4"/><rect x="240" y="168" width="10" height="74" rx="4"/>'
   '<path d="M186 236 L250 236 C250 260 236 272 226 274 L210 274 C200 272 186 260 186 236 Z"/>'
   '<rect x="208" y="266" width="20" height="124" rx="8"/>'
   '<path d="M282 168 C320 182 332 246 318 296 L282 296 Z"/><rect x="280" y="290" width="22" height="100" rx="8"/></g>'),
 'Woodcutter': dict(bg=('#f4cc88', '#d18c3c'), ring=('#6e3812', '#4a2008'), glyph=('#6d3611', '#451f06'), emblem=('#f7d9a6', '#e0aa62'), svg=
   '<g transform="translate(316 172) rotate(-78) scale(0.82) translate(-256 -150) translate(512 0) scale(-1 1)"><rect ' + W + ' x="244" y="40" width="24" height="240" rx="11"/>'
   '<path ' + W + ' d="M224 52 L268 50 C294 42 322 24 342 20 C356 58 356 112 342 148 C322 142 294 126 268 118 L224 118 Z"/></g>'
   '<g ' + W + '><circle cx="256" cy="306" r="54"/><circle cx="196" cy="394" r="54"/><circle cx="316" cy="394" r="54"/></g>'
   '<g ' + LIGHT + '><circle cx="256" cy="306" r="42"/><circle cx="196" cy="394" r="42"/><circle cx="316" cy="394" r="42"/></g>'
   '<g fill="none" stroke="#000" stroke-width="6"><circle cx="256" cy="306" r="25"/><circle cx="196" cy="394" r="25"/><circle cx="316" cy="394" r="25"/><circle cx="256" cy="306" r="8"/><circle cx="196" cy="394" r="8"/><circle cx="316" cy="394" r="8"/></g>'),
 'Climber': dict(bg=('#b6bce0', '#737db3'), ring=('#283061', '#151a3c'), glyph=('#272e5e', '#141938'), svg=
   '<path fill="none" stroke="#000" stroke-width="15" stroke-linecap="round" d="M206 56 C202 170 212 320 204 470"/>'
   '<circle ' + W + ' cx="306" cy="146" r="34"/>'
   '<path ' + W + ' d="M276 190 C296 178 334 186 342 206 C336 240 318 278 306 306 L262 298 C262 260 266 220 276 190 Z"/>'
   '<g fill="none" stroke="#000" stroke-linecap="round" stroke-linejoin="round">'
   '<path stroke-width="28" d="M300 204 L254 168 L214 150"/>'
   '<path stroke-width="26" d="M292 232 L250 254 L214 250"/>'
   '<path stroke-width="36" d="M272 298 L262 352"/><path stroke-width="30" d="M262 352 L256 404"/>'
   '<path stroke-width="36" d="M298 302 L304 358"/><path stroke-width="30" d="M304 358 L306 414"/></g>'
   '<circle ' + W + ' cx="212" cy="150" r="17"/><circle ' + W + ' cx="212" cy="250" r="16"/>'
   '<path ' + W + ' d="M238 400 L270 404 L268 422 L232 418 Z M292 410 L330 414 L328 432 L288 430 Z"/>'),
 'WaterCarrier': dict(bg=('#bfe0f3', '#6da7cf'), ring=('#164160', '#0b2840'), glyph=('#174262', '#0b273e'), emblem=('#d6ecf8', '#a7cde6'), svg=
   '<path ' + W + ' d="M126 262 L386 262 L372 400 C368 420 352 428 334 428 L178 428 C160 428 144 420 140 400 Z"/>'
   '<rect ' + W + ' x="108" y="246" width="296" height="26" rx="12"/>'
   '<path ' + W + ' d="M94 286 L130 286 L130 302 L94 302 Z M382 286 L418 286 L418 302 L382 302 Z"/>'
   '<path ' + LIGHT + ' d="M256 300 C256 300 222 344 222 366 a34 34 0 0 0 68 0 C290 344 256 300 256 300 Z"/>'
   '<g fill="none" stroke="#000" stroke-width="12" stroke-linecap="round">'
   '<path d="M196 226 C176 196 216 176 196 144"/><path d="M256 226 C236 196 276 176 256 128"/><path d="M316 226 C296 196 336 176 316 144"/></g>'),
 'Craftsman': dict(bg=('#d9d0c0', '#a39882'), ring=('#3d3427', '#241e14'), glyph=('#3c3326', '#221c12'), svg=
   '<g transform="rotate(-40 256 256)"><rect ' + W + ' x="242" y="150" width="28" height="272" rx="10"/>'
   '<path ' + W + ' d="M176 104 L330 104 C346 104 352 116 352 128 L352 156 L176 156 L160 130 Z"/></g>'
   '<g transform="rotate(40 256 256)"><path ' + W + ' fill-rule="evenodd" d="M214 82 L300 82 Q312 82 312 96 L312 178 Q312 192 298 192 L216 192 Q202 192 202 178 L202 96 Q202 82 214 82 Z M232 108 L282 108 L282 166 L232 166 Z"/>'
   + SAW + '</g>'),
 'Marksman': dict(bg=('#a2b68c', '#5d7049'), ring=('#232f17', '#121a0a'), glyph=('#24301a', '#121a0b'), svg=
   '<circle fill="none" stroke="#000" stroke-width="20" cx="256" cy="272" r="136"/>'
   '<g ' + W + '><rect x="246" y="98" width="20" height="80" rx="6"/><rect x="246" y="366" width="20" height="80" rx="6"/>'
   '<rect x="82" y="262" width="80" height="20" rx="6"/><rect x="350" y="262" width="80" height="20" rx="6"/></g>'
   '<path ' + W + ' d="M256 214 C274 228 280 252 280 272 L280 330 L232 330 L232 272 C232 252 238 228 256 214 Z"/>'
   '<rect ' + W + ' x="228" y="336" width="56" height="14" rx="4"/>'),
}

os.makedirs(OUT, exist_ok=True)
for i, (name, spec) in enumerate(SPECS.items()):
    im = badge(spec, 100 + i * 7)
    im.save(f'{OUT}/badge_feat_sgl_{name}.png')
print('ok', len(SPECS))
