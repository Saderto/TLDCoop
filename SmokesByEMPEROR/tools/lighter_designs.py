"""The lighter collection: each design = kind + finish + engraving. Used by the preview, the bake and (keys/names/rarity) by Defs.cs.
rarity: 1 common, 2 uncommon, 3 rare (drops less often)."""
import engrave as eg
import lighters as L


def icon(name, scale=.78, frame=False, caption=None, cy=.5):
    def draw(m, W, H, ppm):
        s = min(W, H) * scale
        eg.place_icon(m, name, W / 2, H * cy, s)
        if frame:
            from PIL import ImageDraw
            d = ImageDraw.Draw(m)
            k = int(ppm * .6)
            d.rounded_rectangle([k, k, W - k, H - k], radius=int(ppm * 2.5), outline=255, width=max(2, int(ppm * .45)))
        if caption:
            eg.text(m, caption, W / 2, H * .88, H * .11)
    draw.__name__ = 'ic_%s_%s_%s' % (name, int(frame), caption or '')
    return draw


ZB = ('chrome', (1, 1, 1))
BR = ('brushed', (1, 1, 1))

# key, RU name, EN name, kind, builder, rarity
DESIGNS = [
    ('LighterZappo', 'Zappo классическая', 'Zappo Classic', 'zappo', lambda: L.zappo(ZB), 1),
    ('LighterZappoBrushed', 'Zappo матовая', 'Zappo Brushed', 'zappo', lambda: L.zappo(BR), 1),
    ('LighterZappoBlack', 'Zappo чёрная потёртая', 'Zappo Black Worn', 'zappo', lambda: L.zappo(('paint', (.035, .035, .04), .22)), 1),
    ('LighterZappoWolf', 'Zappo «Воющий волк»', 'Zappo Howling Wolf', 'zappo', lambda: L.zappo(BR, icon('wolf-howl')), 2),
    ('LighterZappoBear', 'Zappo латунная «Медведь»', 'Zappo Brass Bear', 'zappo', lambda: L.zappo(('chrome', L.BRASS), icon('bear-head')), 2),
    ('LighterZappoArmy', 'Zappo армейская', 'Zappo Army', 'zappo', lambda: L.zappo(('paint', (.22, .25, .13), 0.0), icon('eagle-head', caption='U.S.')), 2),
    ('LighterZappoLighthouse', 'Zappo «Маяк»', 'Zappo Lighthouse', 'zappo', lambda: L.zappo(ZB, icon('lighthouse', frame=True)), 2),
    ('LighterZappoRaven', 'Zappo воронёная «Ворон»', 'Zappo Gunmetal Raven', 'zappo', lambda: L.zappo(('chrome', L.GUN), icon('raven')), 2),
    ('LighterZappoAce', 'Zappo «Туз пик»', 'Zappo Ace of Spades', 'zappo', lambda: L.zappo(ZB, icon('card-ace-spades')), 2),
    ('LighterZappoMaple', 'Zappo медная «Клён»', 'Zappo Copper Maple', 'zappo', lambda: L.zappo(('chrome', L.COPPER), icon('maple-leaf')), 2),
    ('LighterZappoSkull', 'Zappo «Череп в короне»', 'Zappo Crowned Skull', 'zappo', lambda: L.zappo(('paint', (.03, .03, .035), 0.0), icon('crowned-skull')), 3),
    ('LighterDupantBlack', 'Dupant чёрный лак', 'Dupant Black Lacquer', 'dupant', lambda: L.dupant((.012, .012, .015)), 2),
    ('LighterDupantRed', 'Dupant китайский лак', 'Dupant Chinese Lacquer', 'dupant', lambda: L.dupant((.42, .03, .025)), 3),
    ('LighterDupantBlue', 'Dupant синий лак', 'Dupant Blue Lacquer', 'dupant', lambda: L.dupant((.035, .05, .20), lid_guilloche=True), 3),
    ('LighterDupantGold', 'Dupant золотой гильош', 'Dupant Gold Guilloche', 'dupant', lambda: L.dupant(guilloche=True), 3),
    ('LighterDupantSilver', 'Dupant серебряный гильош', 'Dupant Silver Guilloche', 'dupant', lambda: L.dupant(metal=L.SILVER, guilloche=True), 3),
    ('LighterRonsan', 'Ronsan Standard латунная', 'Ronsan Standard Brass', 'ronsan', lambda: L.ronsan(('brushed', L.BRASS)), 2),
    ('LighterRonsanChrome', 'Ronsan Standard хром', 'Ronsan Standard Chrome', 'ronsan', lambda: L.ronsan(('chrome', (1, 1, 1))), 2),
    ('LighterImco', 'Imko Triplex сталь', 'Imko Triplex Steel', 'imco', lambda: L.imco(('chrome', (1, 1, 1))), 1),
    ('LighterImcoGold', 'Imko Triplex золотая', 'Imko Triplex Gold', 'imco', lambda: L.imco(('chrome', L.GOLD), dotted=False), 2),
    ('LighterKolibri', 'Kolibri Slim чёрная', 'Kolibri Slim Black', 'kolibri', lambda: L.kolibri((.035, .035, .04), (.35, .35, .38)), 2),
    ('LighterKolibriChrome', 'Kolibri Slim хром', 'Kolibri Slim Chrome', 'kolibri', lambda: L.kolibri((.06, .065, .075)), 2),
    ('LighterKolibriGrey', 'Kolibri Slim серая', 'Kolibri Slim Grey', 'kolibri', lambda: L.kolibri((.30, .31, .33)), 2),
    ('LighterKolibriGold', 'Kolibri Slim чёрно-золотая', 'Kolibri Slim Black Gold', 'kolibri', lambda: L.kolibri((.05, .05, .055), L.GOLD), 3),
    ('LighterKolibriRed', 'Kolibri Slim красная', 'Kolibri Slim Red', 'kolibri', lambda: L.kolibri((.62, .04, .04), L.GOLD), 2),
    ('LighterKolibriNavy', 'Kolibri Slim синяя', 'Kolibri Slim Navy', 'kolibri', lambda: L.kolibri((.05, .08, .20), (1.0, .66, .52)), 3),
    ('LighterPipeBrass', 'Трубочная Old Boy латунь', 'Old Boy Pipe Lighter Brass', 'pipe', lambda: L.pipe_l((.80, .64, .34)), 2),
    ('LighterPipeBurl', 'Трубочная Old Boy корень', 'Old Boy Pipe Lighter Burl', 'pipe', lambda: L.pipe_l((1, 1, 1), panel='burl'), 3),
    ('LighterPipeBlackGold', 'Трубочная Old Boy чёрная', 'Old Boy Pipe Lighter Black', 'pipe', lambda: L.pipe_l(L.GOLD, ground=True, frame=(.035, .035, .04)), 3),
    ('LighterFluid', 'Бензин для зажигалок', 'Lighter Fluid', 'refill', lambda: L.fluid_tin(), 0),
    ('ButaneCan', 'Газ для зажигалок', 'Lighter Butane', 'refill', lambda: L.butane_can(), 0),
]
