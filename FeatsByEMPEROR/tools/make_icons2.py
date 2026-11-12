# Badges for the second batch (37 feats), same look as make_icons.py: its badge() is reused, only glyphs/colours are new.
import sys, os, colorsys
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'make_icons.py')).read()
exec(src.split('W = \'fill="#000"\'')[0])     # everything up to the glyph table: badge(), svg_mask, textures

OUT = sys.argv[1] if len(sys.argv) > 1 else './icons'
W = 'fill="#000"'
LIGHT = 'fill="#fff"'
def SK(w, c='#000'): return f'fill="none" stroke="{c}" stroke-width="{w}" stroke-linecap="round" stroke-linejoin="round"'

def pal(h, s=0.45, emblem=False):
    """Colours from one hue like the vanilla badges: light watercolour disc, dark ring and glyph."""
    def c(l, sat): r, g, b = colorsys.hls_to_rgb(h / 360., l, sat); return '#%02x%02x%02x' % (int(r * 255), int(g * 255), int(b * 255))
    d = dict(bg=(c(0.80, s), c(0.55, s * 0.9)), ring=(c(0.25, s), c(0.14, s)), glyph=(c(0.24, s), c(0.13, s)))
    if emblem: d['emblem'] = (c(0.86, s), c(0.70, s))
    return d

STEAM = lambda x, y: f'<path {SK(12)} d="M{x} {y} C{x-18} {y-28} {x+18} {y-44} {x} {y-74}"/>'

G = {
 'Cook': (30, False,   # skillet on a flame
   f'<ellipse {W} cx="224" cy="250" rx="118" ry="40"/><path {W} d="M106 250 L342 250 L330 290 C320 312 290 322 224 322 C158 322 128 312 118 290 Z"/>'
   f'<rect {W} x="330" y="246" width="120" height="22" rx="10" transform="rotate(-8 330 256)"/>'
   f'<path {W} d="M224 352 C192 380 186 412 206 438 C200 414 216 400 224 388 C232 400 248 414 242 438 C262 412 256 380 224 352 Z"/>'
   + STEAM(186, 214) + STEAM(260, 214)),
 'HotMeal': (18, False,  # bowl with steam
   f'<path {W} d="M110 262 L402 262 C398 344 340 404 256 404 C172 404 114 344 110 262 Z"/><rect {W} x="206" y="400" width="100" height="24" rx="8"/>'
   + STEAM(196, 236) + STEAM(256, 228) + STEAM(316, 236)),
 'CoffeeLover': (25, True,  # mug with steam
   f'<path {W} d="M140 210 L330 210 L318 404 C316 420 304 428 290 428 L180 428 C166 428 154 420 152 404 Z"/>'
   f'<path {SK(26)} d="M326 250 C392 250 396 350 322 352"/>'
   f'<g {LIGHT}><ellipse cx="235" cy="300" rx="22" ry="34" transform="rotate(25 235 300)"/></g>'
   f'<path {SK(6, '#fff')} d="M224 272 C246 290 228 312 246 330"/>'
   + STEAM(196, 186) + STEAM(266, 186)),
 'TeaDrinker': (110, True,  # one mug in the middle
   f'<path {W} d="M160 176 L322 176 L310 400 C308 418 294 428 278 428 L204 428 C188 428 174 418 172 400 Z"/>'
   f'<path {SK(28)} d="M318 226 C394 222 400 340 314 344"/>'
   f'<rect {LIGHT} x="210" y="250" width="58" height="72" rx="6"/><path {SK(5, "#fff")} d="M239 250 L239 196"/>'),
 'Hoarder': (40, True,  # stacked cans
   ''.join(f'<rect {W} x="{x}" y="{y}" width="96" height="110" rx="10"/><rect {LIGHT} x="{x+8}" y="{y+36}" width="80" height="34" rx="4"/>'
           for x, y in [(152, 300), (264, 300), (208, 180)])),
 'SweetTooth': (330, True,  # wrapped candy
   f'<ellipse {W} cx="256" cy="270" rx="92" ry="64"/>'
   f'<path {W} d="M170 270 L96 214 L110 270 L96 326 Z"/><path {W} d="M342 270 L416 214 L402 270 L416 326 Z"/>'
   f'<path {LIGHT} d="M214 226 C236 254 236 290 214 316 L236 322 C256 292 256 252 236 220 Z M276 220 C296 252 296 292 276 322 L298 316 C276 290 276 254 298 226 Z"/>'),
 'Tanner': (28, False,  # stretched hide on a frame
   f'<rect {SK(14)} x="110" y="104" width="292" height="316" rx="8"/>'
   f'<path {W} d="M256 136 C290 136 300 160 330 150 C346 176 330 200 352 222 C360 270 340 300 352 340 C330 356 330 384 300 388 C280 380 270 396 256 396 C242 396 232 380 212 388 C182 384 182 356 160 340 C172 300 152 270 160 222 C182 200 166 176 182 150 C212 160 222 136 256 136 Z"/>'
   f'<path {SK(5)} d="M110 104 L182 150 M402 104 L330 150 M110 420 L212 388 M402 420 L300 388"/>'),
 'Blacksmith': (12, False,  # anvil + hammer
   f'<path {W} d="M110 250 L380 250 C392 250 420 258 430 282 L340 290 L330 318 L292 330 L310 380 L350 390 L350 414 L170 414 L170 390 L210 380 L228 330 L190 318 L180 290 C140 288 112 278 110 250 Z"/>'
   f'<g transform="rotate(-35 300 160)"><rect {W} x="290" y="120" width="22" height="150" rx="8"/><rect {W} x="246" y="96" width="112" height="48" rx="8"/></g>'),
 'Sharpener': (200, True,  # knife drawn along a whetstone
   f'<path {W} d="M104 336 L408 336 L420 360 L420 420 L92 420 L92 360 Z"/><path {LIGHT} d="M112 344 L400 344 L410 362 L102 362 Z"/>'
   f'<g transform="rotate(-14 256 270)"><rect {W} x="70" y="252" width="128" height="40" rx="16"/><rect {W} x="196" y="244" width="16" height="56" rx="4"/>'
   f'<path {W} d="M212 252 L376 252 C404 256 422 270 432 290 C380 302 300 304 212 300 Z"/>'
   f'<g {LIGHT}><circle cx="104" cy="272" r="7"/><circle cx="140" cy="272" r="7"/><circle cx="176" cy="272" r="7"/></g>'
   f'<path {SK(4, "#fff")} d="M226 288 L410 288"/></g>'
   f'<path {SK(9)} d="M300 176 L320 140 M340 186 L374 160 M262 176 L262 136"/>'),
 'Burglar': (270, True,  # crowbar prying a crate lid
   f'<rect {W} x="176" y="276" width="250" height="150" rx="6"/>'
   f'<g {LIGHT}><rect x="190" y="314" width="222" height="10"/><rect x="190" y="370" width="222" height="10"/></g>'
   f'<path {W} d="M176 248 L424 214 L428 246 L180 280 Z"/>'
   f'<path {SK(28)} d="M226 262 L142 150 C126 128 140 102 166 104 C184 106 192 120 186 134"/>'
   f'<path {W} d="M214 250 L262 276 L232 296 Z"/>'),
 'Torchbearer': (22, False,  # torch
   f'<path {W} d="M232 230 L280 230 L270 440 L242 440 Z"/><rect {W} x="222" y="214" width="68" height="30" rx="8"/>'
   f'<path {W} d="M256 70 C210 120 196 160 214 200 L298 200 C316 160 302 120 256 70 Z"/>'
   f'<path {W} d="M200 120 C176 150 178 180 196 198 C190 170 198 150 200 120 Z M312 120 C336 150 334 180 316 198 C322 170 314 150 312 120 Z"/>'),
 'Lamplighter': (45, True,  # hurricane lantern
   f'<path {SK(14)} d="M196 160 C196 80 316 80 316 160"/>'
   f'<path {W} d="M196 140 L316 140 L340 176 L172 176 Z"/>'
   f'<path {W} d="M192 176 L320 176 C356 230 356 300 320 340 L192 340 C156 300 156 230 192 176 Z"/>'
   f'<path {LIGHT} d="M206 190 L306 190 C334 236 334 296 306 326 L206 326 C178 296 178 236 206 190 Z"/>'
   f'<path {W} d="M256 222 C234 254 232 284 256 304 C280 284 278 254 256 222 Z"/>'
   f'<g {W}><rect x="196" y="176" width="10" height="164"/><rect x="306" y="176" width="10" height="164"/></g>'
   f'<path {W} d="M170 340 L342 340 L356 420 C358 430 350 436 340 436 L172 436 C162 436 154 430 156 420 Z"/>'
   f'<rect {LIGHT} x="186" y="372" width="140" height="10" rx="4"/>'),
 'Hearthkeeper': (15, True,  # campfire
   f'<rect {W} x="110" y="370" width="292" height="34" rx="16" transform="rotate(14 256 387)"/><rect {W} x="110" y="370" width="292" height="34" rx="16" transform="rotate(-14 256 387)"/>'
   f'<path {W} d="M256 100 C196 170 170 230 200 300 C206 316 226 340 256 342 C286 340 306 316 312 300 C342 230 316 170 256 100 Z"/>'
   f'<path {LIGHT} d="M256 210 C230 250 226 286 256 316 C286 286 282 250 256 210 Z"/>'),
 'LightSleeper': (235, True,  # rolled bedroll and an alert mark
   f'<path {W} d="M100 318 L330 318 C390 318 410 410 330 410 L100 410 Z"/><circle {W} cx="330" cy="364" r="46"/>'
   f'<circle {LIGHT} cx="330" cy="364" r="30"/><circle {W} cx="330" cy="364" r="12"/>'
   f'<path {W} d="M236 108 L276 108 L268 238 L244 238 Z"/><circle {W} cx="256" cy="272" r="20"/>'),
 'IceWalker': (195, True,  # boot with crampons on cracked ice
   f'<path {W} d="M168 110 L272 110 L276 262 C330 268 380 284 392 316 L396 348 L150 348 L150 300 C150 240 160 170 168 110 Z"/>'
   f'<rect {LIGHT} x="176" y="150" width="88" height="12" rx="5"/><rect {LIGHT} x="176" y="190" width="88" height="12" rx="5"/>'
   f'<path {W} d="M156 348 L172 384 L188 348 Z M214 348 L230 384 L246 348 Z M272 348 L288 384 L304 348 Z M330 348 L346 384 L362 348 Z"/>'
   f'<path {W} d="M84 392 L428 392 L420 432 L92 432 Z"/>'
   f'<path {SK(5, "#fff")} d="M150 398 L170 414 L160 428 M300 398 L284 412 L304 426 M380 400 L396 418"/>'),
 'IceCutter': (205, True,  # ice auger drilling a hole
   f'<ellipse {W} cx="256" cy="380" rx="170" ry="52"/><ellipse {LIGHT} cx="256" cy="376" rx="150" ry="40"/>'
   f'<ellipse {W} cx="256" cy="378" rx="62" ry="18"/>'
   f'<rect {W} x="150" y="96" width="212" height="26" rx="12"/><rect {W} x="244" y="96" width="24" height="170" rx="8"/>'
   f'<path {W} d="M224 262 L288 250 L288 274 L224 286 Z M224 296 L288 284 L288 308 L224 320 Z M224 330 L288 318 L288 342 L224 354 Z"/>'
   f'<rect {W} x="242" y="250" width="28" height="122"/>'),
 'Together': (150, True,  # two survivors shoulder to shoulder
   f'<circle {W} cx="196" cy="160" r="48"/>'
   f'<path {W} d="M196 218 C140 218 112 250 106 300 L96 420 L296 420 L286 300 C280 250 252 218 196 218 Z"/>'
   f'<circle {SK(14, "#fff")} cx="316" cy="182" r="50"/><circle {W} cx="316" cy="182" r="46"/>'
   f'<path {SK(14, "#fff")} d="M316 238 C262 238 236 268 230 316 L222 428 L410 428 L402 316 C396 268 370 238 316 238 Z"/>'
   f'<path {W} d="M316 238 C262 238 236 268 230 316 L222 428 L410 428 L402 316 C396 268 370 238 316 238 Z"/>'),
 'Phoenix': (10, False,  # bird with raised wings and a flame tail
   f'<ellipse {W} cx="256" cy="282" rx="36" ry="62"/><circle {W} cx="250" cy="196" r="26"/>'
   f'<path {W} d="M230 192 L196 206 L230 210 Z"/><path {W} d="M240 210 L272 210 L280 240 L232 240 Z"/>'
   f'<path {W} d="M236 262 C200 236 160 196 104 112 L130 178 L92 168 L138 222 L100 222 L156 262 L120 272 L196 300 L232 300 Z"/>'
   f'<path {W} d="M276 262 C312 236 352 196 408 112 L382 178 L420 168 L374 222 L412 222 L356 262 L392 272 L316 300 L280 300 Z"/>'
   f'<path {W} d="M236 330 C224 376 200 404 168 432 C214 424 240 400 256 366 C272 400 298 424 344 432 C312 404 288 376 276 330 Z"/>'
   f'<path {W} d="M248 350 L256 446 L264 350 Z"/>'),
 'Prometheus': (38, False,  # lens focusing sunlight into a flame below it
   f'<circle {SK(20)} cx="236" cy="168" r="76"/><path {SK(30)} d="M302 130 L372 92"/>'
   f'<path {SK(6)} stroke-dasharray="14 12" d="M180 232 L246 352 M292 232 L250 352"/>'
   f'<path {W} d="M248 300 C214 336 210 378 228 404 C220 380 236 364 244 352 C252 366 266 380 260 404 C290 380 286 334 248 300 Z"/>'
   f'<rect {W} x="180" y="404" width="136" height="20" rx="8" transform="rotate(8 248 414)"/><rect {W} x="180" y="404" width="136" height="20" rx="8" transform="rotate(-8 248 414)"/>'),
 'Minimalist': (175, False,  # feather
   f'<path {W} d="M380 110 C260 130 170 220 150 360 C200 330 250 330 290 300 C260 300 240 296 222 288 C270 280 310 260 340 226 C310 228 290 226 274 220 C320 200 360 160 380 110 Z"/>'
   f'<path {SK(10)} d="M380 110 C290 190 210 290 120 420"/>'),
 'ForestDweller': (125, False,  # fir tree
   f'<path {W} d="M256 92 L322 190 L292 190 L350 270 L314 270 L380 360 L132 360 L198 270 L162 270 L220 190 L190 190 Z"/><rect {W} x="236" y="352" width="40" height="70" rx="6"/>'),
 'Vegetarian': (90, True,  # carrot with leaves
   f'<path {W} d="M196 186 C240 166 300 196 304 230 L170 420 C156 434 140 420 148 404 L180 230 C180 210 186 194 196 186 Z"/>'
   f'<path {SK(6, '#fff')} d="M196 250 L222 262 M180 320 L206 330 M186 370 L204 378"/>'
   f'<path {W} d="M250 186 C250 130 280 100 320 92 C312 130 290 160 262 190 Z M270 196 C300 160 350 150 392 168 C360 196 320 206 280 206 Z M240 180 C220 140 200 120 168 116 C176 150 200 172 236 190 Z"/>'),
 'Fortune': (48, False,  # horseshoe
   f'<path {W} fill-rule="evenodd" d="M150 140 C120 260 150 420 256 420 C362 420 392 260 362 140 L304 146 C324 250 312 360 256 360 C200 360 188 250 208 146 Z"/>'
   f'<g fill="#fff"><circle cx="170" cy="196" r="7"/><circle cx="166" cy="260" r="7"/><circle cx="182" cy="324" r="7"/><circle cx="342" cy="196" r="7"/><circle cx="346" cy="260" r="7"/><circle cx="330" cy="324" r="7"/></g>'
   f'<rect {W} x="132" y="118" width="88" height="34" rx="8"/><rect {W} x="292" y="118" width="88" height="34" rx="8"/>'),
 'Canner': (0, True,  # opened can with lid
   f'<path {W} d="M150 230 L330 230 L330 410 C330 426 300 434 240 434 C180 434 150 426 150 410 Z"/><ellipse {W} cx="240" cy="230" rx="90" ry="24"/>'
   f'<rect {LIGHT} x="150" y="290" width="180" height="70"/>'
   f'<g transform="rotate(-40 300 180)"><ellipse {W} cx="300" cy="150" rx="88" ry="22"/></g>'),
 'StewMaster': (20, True,  # pot with a ladle
   f'<path {W} d="M118 250 L394 250 L380 400 C376 420 360 428 340 428 L172 428 C152 428 136 420 132 400 Z"/><rect {W} x="100" y="232" width="312" height="30" rx="12"/>'
   f'<path {W} d="M80 270 L118 270 L118 290 L80 290 Z M394 270 L432 270 L432 290 L394 290 Z"/>'
   f'<path {SK(16)} d="M290 236 L350 96"/><ellipse {W} cx="354" cy="96" rx="30" ry="18" transform="rotate(-20 354 96)"/>'
   f'<path {LIGHT} d="M150 300 C200 290 300 290 360 300 L356 330 C300 320 200 320 154 330 Z"/>'),
 'EarlyBird': (32, False,  # rising sun and a bird
   f'<path {W} d="M136 360 A120 120 0 0 1 376 360 Z"/><rect {W} x="96" y="368" width="320" height="18" rx="8"/>'
   + ''.join(f'<rect {W} x="250" y="170" width="12" height="40" rx="6" transform="rotate({a} 256 360)"/>' for a in (-60, -30, 0, 30, 60))
   + f'<path {W} d="M300 150 C320 130 350 130 362 146 C376 126 406 124 424 138 C400 140 380 152 366 170 C350 156 326 150 300 150 Z"/>'),
 'MorningRitual': (35, True,  # kettle
   f'<path {W} d="M150 410 C140 330 170 250 256 250 C342 250 372 330 362 410 Z"/><rect {W} x="132" y="400" width="248" height="28" rx="10"/>'
   f'<path {SK(16)} d="M190 250 C190 170 322 170 322 250"/><rect {W} x="236" y="232" width="40" height="24" rx="8"/>'
   f'<path {W} d="M150 330 L92 270 L104 262 L172 300 Z"/>'
   f'<circle {LIGHT} cx="256" cy="340" r="30"/>' + STEAM(96, 248)),
 'SunWorshipper': (50, False,  # sun
   f'<circle {W} cx="256" cy="262" r="76"/>'
   + ''.join(f'<path {W} d="M246 150 L266 150 L256 96 Z" transform="rotate({a} 256 262)"/>' for a in range(0, 360, 30))),
 'Homeowner': (24, True,  # house
   f'<path {W} d="M256 110 L420 250 L380 250 L380 420 L132 420 L132 250 L92 250 Z"/><rect {W} x="318" y="130" width="36" height="80"/>'
   f'<rect {LIGHT} x="164" y="290" width="64" height="56"/><path {LIGHT} d="M284 300 L344 300 L344 420 L284 420 Z"/>'),
 'Nomad': (60, True,  # backpack
   f'<path {W} d="M180 160 C180 120 332 120 332 160 L350 200 C370 210 380 230 380 260 L380 400 C380 420 366 430 346 430 L166 430 C146 430 132 420 132 400 L132 260 C132 230 142 210 162 200 Z"/>'
   f'<path {SK(14)} d="M220 160 C220 100 292 100 292 160"/>'
   f'<rect {LIGHT} x="186" y="300" width="140" height="80" rx="12"/><rect {W} x="186" y="300" width="140" height="22"/>'),
 'Traveler': (210, True,  # compass rose
   f'<circle {SK(14)} cx="256" cy="262" r="140"/>'
   f'<path {W} d="M256 108 L282 236 L256 262 L230 236 Z M256 416 L282 288 L256 262 L230 288 Z M102 262 L230 236 L256 262 L230 288 Z M410 262 L282 236 L256 262 L282 288 Z"/>'
   f'<path {LIGHT} d="M256 124 L274 236 L256 252 Z"/>'),
 'Perfectionist': (100, False,  # wrench with a star
   f'<g transform="rotate(-45 256 280)"><rect {W} x="236" y="230" width="40" height="210" rx="16"/>'
   f'<path {W} fill-rule="evenodd" d="M256 150 a70 70 0 1 0 0.1 0 Z M236 150 L276 150 L276 210 L236 210 Z"/></g>'
   f'<path {W} d="M360 96 L374 136 L416 136 L382 160 L396 200 L360 176 L324 200 L338 160 L304 136 L346 136 Z"/>'),
 'Gunsmith': (40, False,  # three cartridges
   ''.join(f'<path {W} d="M{x-26} 410 L{x-26} 220 C{x-26} 180 {x} 140 {x} 140 C{x} 140 {x+26} 180 {x+26} 220 L{x+26} 410 Z"/><rect {W} x="{x-32}" y="410" width="64" height="18" rx="4"/>'
           for x in (172, 256, 340))
   + ''.join(f'<rect fill="#fff" x="{x-26}" y="236" width="52" height="8"/>' for x in (172, 256, 340))),
 'LuckyHand': (280, True,  # crate with a star
   f'<path {W} d="M110 250 L402 250 L392 420 L120 420 Z"/><path {W} d="M100 214 L412 214 L402 250 L110 250 Z"/>'
   f'<rect {LIGHT} x="220" y="270" width="72" height="40" rx="6"/>'
   f'<path {W} d="M256 70 L274 124 L330 124 L286 156 L302 210 L256 178 L210 210 L226 156 L182 124 L238 124 Z"/>'),
 'Machinist': (0, True,  # locomotive
   f'<rect {W} x="110" y="240" width="200" height="110" rx="12"/><rect {W} x="290" y="180" width="110" height="170" rx="10"/>'
   f'<rect {W} x="140" y="170" width="40" height="80"/><path {W} d="M126 150 L194 150 L184 176 L136 176 Z"/>'
   f'<rect {LIGHT} x="310" y="204" width="70" height="56" rx="6"/>'
   f'<path {W} d="M100 350 L60 410 L120 410 Z"/>'
   + ''.join(f'<circle {W} cx="{x}" cy="384" r="34"/><circle {LIGHT} cx="{x}" cy="384" r="12"/>' for x in (170, 250, 350))),
 'Merchant': (42, False,  # balance scale
   f'<rect {W} x="244" y="140" width="24" height="250" rx="8"/><rect {W} x="176" y="390" width="160" height="30" rx="10"/>'
   f'<rect {W} x="110" y="150" width="292" height="18" rx="8"/><circle {W} cx="256" cy="130" r="22"/>'
   f'<path {SK(6)} d="M130 168 L96 290 M130 168 L164 290 M382 168 L348 290 M382 168 L416 290"/>'
   f'<path {W} d="M80 290 L180 290 C176 320 156 336 130 336 C104 336 84 320 80 290 Z M332 290 L432 290 C428 320 408 336 382 336 C356 336 336 320 332 290 Z"/>'),
 'SodaLover': (350, True,  # soda can with bubbles
   f'<path {W} d="M186 150 L326 150 L340 180 L340 400 L326 430 L186 430 L172 400 L172 180 Z"/><rect {W} x="196" y="132" width="120" height="20" rx="6"/>'
   f'<path {LIGHT} d="M172 250 C220 230 290 290 340 260 L340 320 C290 350 220 290 172 310 Z"/>'
   f'<g {W}><circle cx="380" cy="150" r="14"/><circle cx="404" cy="200" r="10"/><circle cx="370" cy="110" r="9"/><circle cx="130" cy="180" r="11"/><circle cx="116" cy="230" r="8"/></g>'),
}

ORDER = ['Cook', 'HotMeal', 'CoffeeLover', 'TeaDrinker', 'Hoarder', 'SweetTooth', 'Tanner', 'Blacksmith', 'Sharpener', 'Burglar',
         'Torchbearer', 'Lamplighter', 'Hearthkeeper', 'LightSleeper', 'IceWalker', 'IceCutter', 'Together', 'Phoenix', 'Prometheus',
         'Minimalist', 'ForestDweller', 'Vegetarian', 'Fortune', 'Canner', 'StewMaster', 'EarlyBird', 'MorningRitual', 'SunWorshipper',
         'Homeowner', 'Nomad', 'Traveler', 'Perfectionist', 'Gunsmith', 'LuckyHand', 'Machinist', 'Merchant', 'SodaLover']
assert sorted(ORDER) == sorted(G), set(ORDER) ^ set(G)

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    only = set(sys.argv[2:])
    for i, name in enumerate(ORDER):
        if only and name not in only: continue
        hue, emb, svg = G[name]
        spec = pal(hue, emblem=emb); spec['svg'] = svg
        badge(spec, 300 + i * 7).save(f'{OUT}/badge_feat_sgl_{name}.png')
    print('ok', len(ORDER))
