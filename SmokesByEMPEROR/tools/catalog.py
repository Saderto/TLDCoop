"""Cigarette varieties (SmokesByEMPEROR): one record per pack kind. Used by cigvar.py (cigarette textures), packvar.py (pack atlases),
gen_catalog.py (C# Catalog.cs + texts). Strength 0 light, 1 medium, 2 strong, 3 very strong. Bonus: '' | fresh | coffee | sweet | warm | calm.
Colours are RGB tuples. Filter: cork | white | black | gold | brown | color (filter_color) | none (no filter) | papirosa (paper mouthpiece)."""

C_CORK = 'cork'

PAPER_WHITE = (240, 238, 232)


def V(key, name_en, name_ru=None, strength=1, bonus='', paper=PAPER_WHITE, pattern='', pattern_color=None, filt=C_CORK,
      filter_color=None, rings=(), ink=(170, 26, 34), pack=None, label=None, fmt='regular', old_id=None, rare=False, shape='tall', style=''):
    return dict(key=key, name_en=name_en, name_ru=name_ru or name_en, strength=strength, bonus=bonus, paper=paper, pattern=pattern,
                pattern_color=pattern_color, filter=filt, filter_color=filter_color, rings=list(rings), ink=ink, pack=pack or {},
                label=label or name_en.upper(), fmt=fmt, old_id=old_id, rare=rare, shape=shape, style=style)


GOLD = (198, 160, 80)
SILVER = (200, 202, 208)
GOLD_LIGHT = (226, 190, 110)
BROWN_PAPER = (128, 82, 50)
DARK_BROWN = (78, 48, 30)
BLACK = (26, 24, 24)

BRANDS = {
    # brand key: (display name, pack class in packvar.py, varieties)
    'chapgirl': ('Chapgirl', 'chapgirl', [
        V('Red', 'Red', 'Ред', 2, '', rings=[GOLD, (170, 30, 36)], ink=(150, 26, 34),
          pack=dict(base=(150, 26, 32), stripe=(132, 20, 28), ink=GOLD_LIGHT)),
        V('Brown', 'Brown', 'Браун', 1, '', paper=BROWN_PAPER, filt='brown', filter_color=(104, 64, 38), rings=[GOLD], ink=GOLD_LIGHT,
          pack=dict(base=(96, 58, 34), stripe=(84, 50, 28), ink=GOLD_LIGHT)),
        V('Green', 'Green', 'Грин', 1, 'sweet', rings=[GOLD, (40, 120, 60)], ink=(36, 110, 56),
          pack=dict(base=(34, 92, 52), stripe=(28, 80, 44), ink=GOLD_LIGHT)),
        V('Gold', 'Gold', 'Голд', 1, '', filt='gold', rings=[], ink=(150, 116, 50),
          pack=dict(base=(196, 156, 74), stripe=(182, 142, 62), ink=(80, 40, 20))),
        V('Purple', 'Purple', 'Пёрпл', 1, 'sweet', rings=[GOLD, (110, 50, 130)], ink=(100, 44, 120),
          pack=dict(base=(84, 38, 104), stripe=(74, 32, 92), ink=GOLD_LIGHT)),
        V('Vanilla', 'Vanilla', 'Ванилла', 1, 'sweet', paper=BROWN_PAPER, filt='color', filter_color=(232, 214, 176), rings=[GOLD], ink=GOLD_LIGHT,
          pack=dict(base=(226, 206, 166), stripe=(214, 192, 150), ink=(110, 70, 36))),
        V('Cherry', 'Cherry', 'Черри', 1, 'sweet', paper=BROWN_PAPER, filt='brown', filter_color=(110, 40, 44), rings=[GOLD, (150, 30, 50)], ink=GOLD_LIGHT,
          pack=dict(base=(110, 28, 36), stripe=(98, 22, 30), ink=GOLD_LIGHT), old_id='PackChapgirl'),
        V('Coffee', 'Coffee', 'Кофи', 1, 'coffee', paper=DARK_BROWN, filt='brown', filter_color=(70, 44, 28), rings=[GOLD], ink=GOLD_LIGHT,
          pack=dict(base=(62, 38, 24), stripe=(54, 32, 20), ink=GOLD_LIGHT)),
        V('EclipseNoir', 'Eclipse Noir', 'Эклипс Нуар', 2, '', paper=BLACK, filt='black', rings=[SILVER, SILVER], ink=SILVER, rare=True, style='noir',
          shape='wide', pack=dict(base=(22, 20, 22), stripe=(32, 30, 34), ink=(214, 216, 222))),
        V('Ash', 'Ash', 'Эш', 0, '', paper=(196, 196, 196), filt='color', filter_color=(150, 150, 154), rings=[(220, 220, 226)], ink=(70, 70, 76),
          pack=dict(base=(128, 128, 134), stripe=(118, 118, 124), ink=(236, 236, 240))),
    ]),
    'sobrania': ('Sobrania', 'sobrania', [
        V('BlackRussian', 'Black Russian', 'Блэк Рашн', 2, 'calm', paper=BLACK, filt='gold', ink=GOLD_LIGHT,
          pack=dict(base=(20, 20, 22), ink=GOLD_LIGHT, stripes=None)),
        V('CocktailPink', 'Cocktail Pink', 'Коктейль Пинк', 1, 'calm', paper=(236, 150, 178), filt='gold', ink=GOLD,
          pack=dict(base=(240, 214, 222), ink=(150, 110, 40), stripes=[(236, 150, 178)])),
        V('CocktailBlue', 'Cocktail Blue', 'Коктейль Блю', 1, 'calm', paper=(130, 180, 226), filt='gold', ink=GOLD,
          pack=dict(base=(212, 228, 242), ink=(150, 110, 40), stripes=[(130, 180, 226)])),
        V('CocktailGreen', 'Cocktail Green', 'Коктейль Грин', 1, 'calm', paper=(150, 206, 150), filt='gold', ink=GOLD,
          pack=dict(base=(218, 236, 214), ink=(150, 110, 40), stripes=[(150, 206, 150)])),
        V('CocktailYellow', 'Cocktail Yellow', 'Коктейль Йеллоу', 1, 'calm', paper=(240, 214, 110), filt='gold', ink=GOLD,
          pack=dict(base=(246, 236, 196), ink=(150, 110, 40), stripes=[(240, 214, 110)])),
        V('CocktailLilac', 'Cocktail Lilac', 'Коктейль Лайлак', 1, 'calm', paper=(186, 160, 218), filt='gold', ink=GOLD,
          pack=dict(base=(228, 220, 240), ink=(150, 110, 40), stripes=[(186, 160, 218)]), rare=True),
        V('WhiteGold', 'White Gold', 'Уайт Голд', 0, 'calm', filt='gold', ink=GOLD,
          pack=dict(base=(244, 242, 236), ink=(170, 128, 50), stripes=None)),
        V('Classic', 'Classic', 'Классик', 1, 'calm', filt='cork', rings=[GOLD, (30, 40, 100)], ink=(30, 40, 100),
          pack=dict(base=(24, 34, 86), ink=GOLD_LIGHT, stripes=None)),
    ]),
    'milston': ('Milston', 'milston', [
        V('Red', 'Red', 'Ред', 2, '', label='CLASSIC', old_id='PackMilston', pack=dict(base=(240, 236, 226), band=(176, 26, 34), ink=(150, 22, 30))),
        V('Blue', 'Blue', 'Блю', 1, '', rings=[(30, 70, 150)], ink=(30, 70, 150), pack=dict(base=(240, 238, 232), band=(30, 70, 150), top='BLUE BLEND')),
        V('Silver', 'Silver', 'Сильвер', 0, '', filt='white', rings=[(170, 174, 182)], ink=(120, 124, 132), pack=dict(base=(242, 242, 240), band=(140, 146, 158), top='SILVER')),
        V('White', 'White', 'Уайт', 0, '', filt='white', ink=(150, 150, 156), pack=dict(base=(250, 250, 248), band=(186, 188, 194), top='SUPER LIGHT')),
        V('XStyleBlue', 'XStyle Blue', 'Иксстайл Блю', 0, '', filt='white', rings=[(30, 70, 150)], ink=(30, 70, 150), fmt='superslim', shape='slim', label='XSTYLE BLUE',
          pack=dict(base=(236, 240, 248), band=(30, 70, 150), top='SUPER SLIMS')),
        V('Compact', 'Compact', 'Компакт', 1, '', fmt='short', ink=(150, 22, 30), pack=dict(base=(240, 236, 226), band=(200, 60, 40), top='COMPACT')),
        V('XSImpulse', 'XS Impulse', 'Иксэс Импульс', 1, 'fresh', filt='white', rings=[(110, 60, 170), (30, 70, 150)], ink=(90, 50, 150), fmt='superslim', shape='slim',
          label='XS IMPULSE', pack=dict(base=(232, 230, 244), band=(90, 50, 150), top='CLICK · FRESH')),
    ]),
    'marlbrrro': ('Marlbrrro', 'marlbrrro', [
        V('Red', 'Red', 'Ред', 2, '', label='FILTER CIGARETTES', old_id='PackMarlbrrro', ink=(170, 26, 34), pack=dict(base=(244, 241, 233), roof=(190, 30, 38))),
        V('Gold', 'Gold', 'Голд', 1, '', rings=[GOLD], ink=(150, 116, 50), pack=dict(base=(244, 241, 233), roof=(198, 160, 80), crown=(250, 236, 200))),
        V('Silver', 'Silver', 'Сильвер', 0, '', rings=[SILVER], ink=(120, 124, 132), pack=dict(base=(244, 243, 240), roof=(150, 156, 166), crown=(240, 240, 244))),
        V('Touch', 'Touch', 'Тач', 1, '', fmt='short', ink=(170, 26, 34), pack=dict(base=(244, 241, 233), roof=(190, 30, 38))),
        V('IceBlast', 'Ice Blast', 'Айс Бласт', 1, 'fresh', rings=[(60, 150, 220)], ink=(40, 110, 190),
          pack=dict(base=(28, 28, 32), roof=(40, 130, 200), crown=(200, 230, 250), ink=(230, 240, 250))),
        V('Crafted', 'Crafted', 'Крафтед', 1, '', filt='white', ink=(40, 60, 110), pack=dict(base=(244, 243, 238), roof=(40, 60, 110), crown=(200, 170, 100))),
        V('Black', 'Black', 'Блэк', 2, '', filt='black', ink=(30, 28, 28), pack=dict(base=(26, 26, 28), roof=(170, 174, 182), crown=(30, 30, 32), ink=(220, 222, 228))),
    ]),
    'parliamint': ('Parliamint', 'parliamint', [
        V('NightBlue', 'Night Blue', 'Найт Блю', 1, 'fresh', filt='white', rings=[(24, 46, 86)], ink=(24, 46, 86), label='MENTHOL', old_id='PackParliamint',
          pack=dict(top=(18, 36, 72), bottom=(38, 66, 98))),
        V('AquaBlue', 'Aqua Blue', 'Аква Блю', 0, 'fresh', filt='white', rings=[(60, 140, 200)], ink=(40, 110, 170), pack=dict(top=(30, 96, 150), bottom=(70, 150, 196), accent=(80, 190, 210))),
        V('SilverBlue', 'Silver Blue', 'Сильвер Блю', 0, 'fresh', filt='white', rings=[(150, 170, 200)], ink=(90, 110, 150), pack=dict(top=(90, 110, 150), bottom=(150, 166, 196), accent=(170, 190, 220))),
        V('Platinum', 'Platinum', 'Платинум', 0, 'fresh', filt='white', rings=[SILVER], ink=(110, 114, 124), pack=dict(top=(120, 124, 134), bottom=(176, 180, 188), accent=(210, 212, 220))),
        V('Reserve', 'Reserve', 'Резерв', 1, 'fresh', filt='white', rings=[SILVER, SILVER], ink=(14, 24, 50), pack=dict(top=(10, 16, 36), bottom=(24, 36, 66), accent=(200, 202, 208))),
        V('Soho', 'Soho', 'Сохо', 0, 'fresh', filt='white', rings=[(110, 80, 170)], ink=(80, 56, 140), fmt='superslim', shape='slim',
          pack=dict(top=(56, 40, 104), bottom=(110, 84, 160), accent=(196, 170, 230))),
    ]),
    'bent': ('Bent', 'bent', [
        V('Blue', 'Blue', 'Блю', 1, '', label='BLUE  ·  8', ink=(22, 44, 96), old_id='PackBent', pack=dict(base=(246, 246, 244), band=(32, 72, 150), band2=(120, 160, 214), ink=(22, 44, 96))),
        V('Silver', 'Silver', 'Сильвер', 0, '', filt='white', ink=(120, 124, 132), label='SILVER  ·  4', pack=dict(base=(246, 246, 244), band=(140, 146, 158), band2=(196, 200, 208), ink=(70, 74, 84))),
        V('White', 'White', 'Уайт', 0, '', filt='white', ink=(160, 160, 166), label='WHITE  ·  1', pack=dict(base=(250, 250, 250), band=(196, 198, 204), band2=(226, 228, 232), ink=(110, 112, 120))),
        V('Nanotek', 'Nanotek', 'Нанотек', 0, '', filt='white', rings=[(32, 72, 150)], ink=(22, 44, 96), fmt='superslim', shape='slim', label='NANOTEK',
          pack=dict(base=(236, 240, 248), band=(22, 44, 96), band2=(170, 176, 190), ink=(22, 44, 96), tag='SLIMS')),
        V('Crystal', 'Crystal', 'Кристал', 0, '', filt='white', rings=[(150, 210, 230)], ink=(60, 150, 190), label='CRYSTAL',
          pack=dict(base=(244, 250, 252), band=(80, 170, 210), band2=(170, 220, 236), ink=(30, 90, 130), tag='CLEAR')),
        V('iSwitch', 'iSwitch', 'Айсвитч', 1, 'fresh', rings=[(40, 160, 90), (32, 72, 150)], ink=(22, 44, 96), label='iSWITCH',
          pack=dict(base=(246, 246, 244), band=(32, 72, 150), band2=(60, 180, 110), ink=(22, 44, 96), tag='2 CLICKS')),
        V('Core', 'Core', 'Кор', 1, '', filt='color', filter_color=(206, 222, 244), rings=[(32, 72, 150)], ink=(22, 44, 96), label='CORE',
          pack=dict(base=(22, 44, 96), band=(120, 160, 214), band2=(236, 240, 248), ink=(240, 244, 250), tag='CORE')),
    ]),
    'caramel': ('Caramel', 'caramel', [
        V('Yellow', 'Yellow', 'Йеллоу', 2, '', label='FILTERS', ink=(92, 54, 26), old_id='PackCaramel', pack=dict(base=(204, 168, 108), ink=(92, 54, 26))),
        V('Blue', 'Blue', 'Блю', 1, '', rings=[(50, 90, 160)], ink=(40, 70, 130), pack=dict(base=(70, 110, 170), ink=(236, 230, 214), hill=(110, 146, 196), llama=(22, 36, 70))),
        V('Original', 'Original', 'Ориджинал', 3, '', filt='none', fmt='nofilter', ink=(92, 54, 26), label='NON-FILTER',
          pack=dict(base=(214, 178, 110), ink=(92, 54, 26), hill=(186, 140, 80))),
        V('Activate', 'Activate', 'Активейт', 1, 'fresh', rings=[(40, 150, 90)], ink=(30, 110, 70), pack=dict(base=(38, 96, 66), ink=(230, 236, 214), hill=(70, 130, 96), llama=(16, 44, 30))),
        V('Compact', 'Compact', 'Компакт', 1, '', fmt='short', ink=(150, 70, 20), pack=dict(base=(222, 150, 70), ink=(90, 40, 16), hill=(196, 120, 50))),
        V('Black', 'Black', 'Блэк', 2, '', filt='black', ink=(30, 28, 28), pack=dict(base=(30, 28, 28), ink=(206, 166, 82), hill=(60, 56, 54), llama=(206, 166, 82))),
        V('DesertSand', 'Desert Sand', 'Дезерт Сэнд', 1, '', paper=(226, 206, 166), ink=(120, 80, 40), label='DESERT SAND',
          pack=dict(base=(232, 212, 172), ink=(110, 70, 34), hill=(210, 180, 130))),
    ]),
    'lucky': ('Lucky Strife', 'lucky', [
        V('Red', 'Red', 'Ред', 2, '', rings=[(200, 30, 40)], ink=(190, 26, 34), pack=dict(base=(246, 244, 238), target=(200, 30, 40))),
        V('Blue', 'Blue', 'Блю', 1, '', rings=[(40, 80, 160)], ink=(40, 80, 160), pack=dict(base=(246, 244, 238), target=(40, 80, 160))),
        V('Silver', 'Silver', 'Сильвер', 0, '', filt='white', rings=[SILVER], ink=(120, 124, 132), pack=dict(base=(246, 246, 244), target=(150, 156, 166))),
        V('Original', 'Original', 'Ориджинал', 3, '', filt='none', fmt='nofilter', ink=(190, 26, 34), label='UNFILTERED',
          pack=dict(base=(38, 84, 54), target=(200, 30, 40), ring=(236, 230, 210), ink=(236, 230, 210))),
        V('ClickRoll', 'Click & Roll', 'Клик энд Ролл', 1, 'fresh', rings=[(40, 160, 90), (200, 30, 40)], ink=(190, 26, 34), label='CLICK & ROLL',
          pack=dict(base=(246, 244, 238), target=(40, 140, 80))),
        V('Ice', 'Ice', 'Айс', 1, 'fresh', rings=[(120, 190, 230)], ink=(60, 140, 200), pack=dict(base=(232, 242, 248), target=(80, 160, 210))),
    ]),
    'pell': ('Pell Mell', 'pell', [
        V('Red', 'Red', 'Ред', 2, '', rings=[(190, 30, 40)], ink=(170, 26, 34), pack=dict(base=(176, 26, 34))),
        V('Blue', 'Blue', 'Блю', 1, '', rings=[(30, 60, 140)], ink=(30, 60, 140), pack=dict(base=(30, 56, 130))),
        V('Silver', 'Silver', 'Сильвер', 0, '', filt='white', rings=[SILVER], ink=(120, 124, 132), pack=dict(base=(130, 136, 146), gold=(236, 236, 240))),
        V('Orange', 'Orange', 'Оранж', 1, '', rings=[(230, 120, 30)], ink=(200, 100, 20), pack=dict(base=(214, 104, 26))),
        V('Nanokings', 'Nanokings', 'Нанокингс', 0, '', filt='white', rings=[(176, 26, 34)], ink=(170, 26, 34), fmt='superslim', shape='slim', label='NANOKINGS',
          pack=dict(base=(150, 20, 30))),
        V('Menthol', 'Menthol', 'Ментол', 1, 'fresh', rings=[(40, 150, 90)], ink=(30, 120, 70), pack=dict(base=(28, 104, 66))),
        V('Amber', 'Amber', 'Амбер', 1, '', rings=[(200, 40, 40), (226, 150, 50)], ink=(190, 110, 30), pack=dict(base=(170, 92, 24))),
    ]),
    'dunchill': ('Dunchill', 'dunchill', [
        V('International', 'International', 'Интернэшнл', 2, 'calm', rings=[GOLD], ink=(150, 116, 50), label='INTERNATIONAL', pack=dict(base=(96, 18, 28))),
        V('FineCutBlack', 'Fine Cut Black', 'Файн Кат Блэк', 2, 'calm', filt='black', rings=[GOLD], ink=(30, 28, 28), label='FINE CUT BLACK', pack=dict(base=(24, 22, 24))),
        V('FineCutBlue', 'Fine Cut Blue', 'Файн Кат Блю', 1, 'calm', rings=[GOLD], ink=(30, 50, 110), label='FINE CUT BLUE', pack=dict(base=(24, 44, 100))),
        V('FineCutWhite', 'Fine Cut White', 'Файн Кат Уайт', 0, 'calm', filt='white', rings=[GOLD], ink=(150, 116, 50), label='FINE CUT WHITE',
          pack=dict(base=(244, 240, 230), gold=(176, 136, 60))),
        V('MasterBlend', 'Master Blend', 'Мастер Бленд', 2, 'calm', filt='brown', filter_color=(110, 70, 40), rings=[GOLD], ink=(150, 116, 50), label='MASTER BLEND',
          pack=dict(base=(84, 52, 30))),
        V('Infinite', 'Infinite', 'Инфинит', 1, 'calm', filt='white', rings=[GOLD, GOLD], ink=(150, 116, 50), label='INFINITE', pack=dict(base=(40, 40, 46), gold=(220, 220, 226))),
    ]),
    'dovidoff': ('Dovidoff', 'dovidoff', [
        V('Classic', 'Classic', 'Классик', 1, 'calm', rings=[(170, 30, 40)], ink=(30, 40, 90), pack=dict(base=(246, 244, 238), band=(170, 30, 40), ink=(30, 40, 90))),
        V('Gold', 'Gold', 'Голд', 1, 'calm', rings=[GOLD], ink=(150, 116, 50), pack=dict(base=(246, 244, 238), band=(198, 160, 80), ink=(30, 40, 90))),
        V('White', 'White', 'Уайт', 0, 'calm', filt='white', ink=(150, 150, 156), pack=dict(base=(250, 250, 250), band=(206, 208, 214), ink=(60, 64, 76))),
        V('Black', 'Black', 'Блэк', 2, 'calm', filt='black', rings=[SILVER], ink=(30, 28, 28), pack=dict(base=(26, 26, 28), band=(70, 70, 76), ink=(220, 222, 228), line=(200, 202, 208))),
        V('Evolve', 'Evolve', 'Эволв', 0, 'calm', filt='white', rings=[(60, 120, 200)], ink=(40, 90, 170), fmt='superslim', shape='slim',
          pack=dict(base=(240, 244, 250), band=(60, 120, 200), ink=(30, 40, 90))),
        V('Reach', 'Reach', 'Рич', 0, 'calm', filt='white', rings=[SILVER, SILVER], ink=(120, 124, 132), fmt='superslim', shape='slim',
          pack=dict(base=(236, 236, 240), band=(150, 156, 166), ink=(50, 54, 64), line=(220, 222, 228))),
    ]),
    'rothmunz': ('Rothmunz', 'rothmunz', [
        V('Red', 'Red', 'Ред', 2, '', rings=[(190, 30, 40)], ink=(20, 34, 80), pack=dict(base=(20, 34, 80), stripe=(190, 30, 40), ink=(236, 236, 240))),
        V('Blue', 'Blue', 'Блю', 1, '', rings=[(60, 110, 200)], ink=(20, 34, 80), pack=dict(base=(20, 34, 80), stripe=(80, 130, 210), ink=(236, 236, 240))),
        V('Silver', 'Silver', 'Сильвер', 0, '', filt='white', rings=[SILVER], ink=(20, 34, 80), pack=dict(base=(60, 70, 96), stripe=(190, 196, 206), ink=(236, 236, 240))),
        V('Demi', 'Demi', 'Деми', 1, '', fmt='short', rings=[(190, 30, 40)], ink=(20, 34, 80), pack=dict(base=(20, 34, 80), stripe=(214, 176, 96), ink=(236, 236, 240))),
        V('Nanokings', 'Nanokings', 'Нанокингс', 0, '', filt='white', rings=[(60, 110, 200)], ink=(20, 34, 80), fmt='superslim', shape='slim', label='NANOKINGS',
          pack=dict(base=(20, 34, 80), stripe=(120, 160, 220), ink=(236, 236, 240))),
    ]),
    'benson': ('Benson & Hodges', 'benson', [
        V('Gold', 'Gold', 'Голд', 2, '', rings=[GOLD, GOLD], ink=(130, 96, 40), pack=dict(base=(206, 166, 82), ink=(40, 30, 20))),
        V('Silver', 'Silver', 'Сильвер', 0, '', filt='white', rings=[SILVER], ink=(120, 124, 132), pack=dict(base=(190, 194, 202), ink=(40, 44, 54))),
        V('Blue', 'Blue', 'Блю', 1, '', rings=[(50, 90, 170)], ink=(40, 70, 150), pack=dict(base=(60, 96, 170), ink=(240, 240, 244))),
        V('Black', 'Black', 'Блэк', 2, '', filt='black', rings=[GOLD], ink=(30, 28, 28), pack=dict(base=(40, 38, 40), ink=(214, 178, 96))),
        V('Menthol', 'Menthol', 'Ментол', 1, 'fresh', rings=[(40, 150, 90)], ink=(30, 120, 70), pack=dict(base=(60, 140, 96), ink=(240, 240, 236))),
        V('SpecialFilter', 'Special Filter', 'Спешл Фильтр', 2, '', fmt='long', rings=[GOLD], ink=(130, 96, 40), label='SPECIAL FILTER',
          pack=dict(base=(214, 176, 92), ink=(110, 20, 26))),
    ]),
    'vague': ('Vague', 'vague', [
        V('Bleue', 'Bleue', 'Блю', 0, '', filt='white', pattern='flowers', pattern_color=(80, 120, 200), rings=[(80, 120, 200)], ink=(60, 96, 180),
          fmt='superslim', shape='slim', pack=dict(base=(246, 246, 250), accent=(80, 120, 200))),
        V('Lilas', 'Lilas', 'Лила', 0, 'sweet', filt='white', pattern='flowers', pattern_color=(170, 120, 200), rings=[(170, 120, 200)], ink=(140, 90, 180),
          fmt='superslim', shape='slim', pack=dict(base=(248, 244, 250), accent=(160, 110, 196))),
        V('Menthe', 'Menthe', 'Мант', 0, 'fresh', filt='white', pattern='leaves', pattern_color=(80, 160, 110), rings=[(80, 160, 110)], ink=(50, 130, 80),
          fmt='superslim', shape='slim', pack=dict(base=(244, 250, 246), accent=(60, 150, 96))),
        V('Arome', 'Arome', 'Аром', 0, 'sweet', filt='gold', pattern='scrolls', pattern_color=(200, 160, 80), rings=[GOLD], ink=(160, 120, 50), label='AROME',
          fmt='superslim', shape='slim', pack=dict(base=(250, 246, 236), accent=(196, 156, 76))),
        V('Rose', 'Rose', 'Роз', 0, 'sweet', paper=(246, 222, 228), filt='white', pattern='flowers', pattern_color=(220, 110, 140), rings=[(220, 110, 140)],
          ink=(200, 80, 120), fmt='superslim', shape='slim', pack=dict(base=(250, 232, 238), accent=(214, 100, 136))),
        V('Noir', 'Noir', 'Нуар', 1, '', paper=BLACK, filt='black', pattern='scrolls', pattern_color=(190, 192, 200), rings=[SILVER], ink=(200, 202, 208),
          fmt='superslim', shape='slim', pack=dict(base=(30, 28, 30), accent=(196, 198, 206), ink=(230, 230, 236))),
    ]),
    'glamoor': ('Glamoor', 'glamoor', [
        V('Azure', 'Azure', 'Эжур', 0, '', filt='white', pattern='scrolls', pattern_color=(90, 160, 220), rings=[(90, 160, 220)], ink=(60, 130, 200),
          fmt='superslim', shape='slim', pack=dict(base=(232, 242, 252), accent=(80, 150, 216))),
        V('Amber', 'Amber', 'Амбер', 0, 'sweet', filt='gold', pattern='scrolls', pattern_color=(206, 150, 60), rings=[GOLD], ink=(170, 120, 40),
          fmt='superslim', shape='slim', pack=dict(base=(250, 238, 216), accent=(206, 146, 56))),
        V('Lilac', 'Lilac', 'Лайлак', 0, 'sweet', filt='white', pattern='scrolls', pattern_color=(170, 120, 200), rings=[(170, 120, 200)], ink=(140, 90, 180),
          fmt='superslim', shape='slim', pack=dict(base=(244, 236, 250), accent=(160, 110, 196))),
        V('Menthol', 'Menthol', 'Ментол', 0, 'fresh', filt='white', pattern='leaves', pattern_color=(80, 170, 120), rings=[(80, 170, 120)], ink=(50, 140, 90),
          fmt='superslim', shape='slim', pack=dict(base=(234, 248, 240), accent=(60, 160, 110))),
        V('Pink', 'Pink', 'Пинк', 0, 'sweet', filt='white', pattern='scrolls', pattern_color=(226, 120, 160), rings=[(226, 120, 160)], ink=(200, 80, 130),
          fmt='superslim', shape='slim', pack=dict(base=(252, 236, 242), accent=(220, 110, 150))),
        V('Gold', 'Super Slims Gold', 'Супер Слимс Голд', 0, '', filt='gold', rings=[GOLD, GOLD], ink=(160, 120, 50), label='SUPER SLIMS GOLD',
          fmt='superslim', shape='slim', pack=dict(base=(214, 176, 96), accent=(250, 244, 226), ink=(70, 50, 20))),
    ]),
    'ezze': ('Ezze', 'ezze', [
        V('Blue', 'Blue', 'Блю', 1, '', filt='white', rings=[(40, 80, 160)], ink=(40, 80, 160), fmt='superslim', shape='slim', pack=dict(base=(246, 246, 248), accent=(40, 80, 160))),
        V('Silver', 'Silver', 'Сильвер', 0, '', filt='white', rings=[SILVER], ink=(120, 124, 132), fmt='superslim', shape='slim', pack=dict(base=(246, 246, 248), accent=(150, 156, 168))),
        V('Gold', 'Gold', 'Голд', 0, '', filt='white', rings=[GOLD], ink=(160, 120, 50), fmt='superslim', shape='slim', pack=dict(base=(246, 244, 238), accent=(196, 156, 76))),
        V('GoldenLeaf', 'Golden Leaf', 'Голден Лиф', 1, '', filt='gold', rings=[GOLD], ink=(160, 120, 50), label='GOLDEN LEAF', fmt='superslim', shape='slim',
          pack=dict(base=(40, 34, 28), accent=(206, 166, 82), ink=(236, 220, 180))),
        V('Black', 'Black', 'Блэк', 1, '', paper=BLACK, filt='black', rings=[SILVER], ink=(200, 202, 208), fmt='superslim', shape='slim',
          pack=dict(base=(28, 28, 30), accent=(170, 172, 180), ink=(236, 236, 240))),
        V('Menthol', 'Menthol', 'Ментол', 0, 'fresh', filt='white', rings=[(40, 150, 90)], ink=(30, 120, 70), fmt='superslim', shape='slim',
          pack=dict(base=(244, 248, 246), accent=(40, 150, 90))),
        V('Change', 'Change', 'Чейндж', 0, 'fresh', filt='white', rings=[(60, 170, 200), (40, 150, 90)], ink=(40, 130, 170), fmt='superslim', shape='slim',
          pack=dict(base=(244, 248, 250), accent=(50, 160, 200))),
    ]),
    'ln': ('L&N', 'ln', [
        V('Red', 'Red', 'Ред', 2, '', rings=[(190, 30, 40)], ink=(170, 26, 34), pack=dict(base=(246, 244, 240), accent=(190, 30, 40))),
        V('Blue', 'Blue', 'Блю', 1, '', rings=[(40, 80, 160)], ink=(40, 80, 160), pack=dict(base=(246, 244, 240), accent=(40, 80, 160))),
        V('Silver', 'Silver', 'Сильвер', 0, '', filt='white', rings=[SILVER], ink=(120, 124, 132), pack=dict(base=(246, 246, 246), accent=(150, 156, 168))),
        V('Loft', 'Loft', 'Лофт', 0, '', filt='white', rings=[(190, 30, 40)], ink=(170, 26, 34), fmt='superslim', shape='slim', pack=dict(base=(246, 244, 240), accent=(150, 30, 40))),
        V('Menthol', 'Menthol', 'Ментол', 1, 'fresh', rings=[(40, 150, 90)], ink=(30, 120, 70), pack=dict(base=(246, 248, 244), accent=(40, 150, 90))),
    ]),
    'galloises': ('Galloises', 'galloises', [
        V('Brunes', 'Brunes', 'Брюн', 3, 'warm', filt='none', fmt='nofilter', ink=(40, 70, 140), label='BRUNES',
          pack=dict(base=(80, 130, 196), helmet=(236, 240, 246), ink=(236, 240, 246))),
        V('BlondesRouge', 'Blondes Rouge', 'Блонд Руж', 2, '', rings=[(190, 30, 40)], ink=(40, 70, 140), label='BLONDES ROUGE',
          pack=dict(base=(190, 34, 40), helmet=(246, 240, 236), ink=(246, 240, 236))),
        V('BlondesBleu', 'Blondes Bleu', 'Блонд Блё', 1, '', rings=[(60, 110, 190)], ink=(40, 70, 140), label='BLONDES BLEU',
          pack=dict(base=(40, 80, 160), helmet=(236, 240, 246), ink=(236, 240, 246))),
        V('BlondesBlanc', 'Blondes Blanc', 'Блонд Блан', 0, '', filt='white', rings=[SILVER], ink=(40, 70, 140), label='BLONDES BLANC',
          pack=dict(base=(240, 242, 246), helmet=(60, 96, 170), ink=(40, 70, 140))),
        V('Caporal', 'Caporal', 'Капораль', 3, 'warm', paper=(236, 214, 140), filt='none', fmt='nofilter', ink=(90, 60, 20), label='CAPORAL',
          pack=dict(base=(214, 186, 100), helmet=(60, 50, 30), ink=(60, 50, 30))),
    ]),
    'gitanos': ('Gitanos', 'gitanos', [
        V('Brunes', 'Brunes', 'Брюн', 3, 'warm', filt='none', fmt='nofilter', pattern='dancer', pattern_color=(40, 70, 140), ink=(40, 70, 140), label='BRUNES',
          pack=dict(base=(40, 80, 150), smoke=(236, 236, 240), ink=(236, 236, 240))),
        V('Blondes', 'Blondes', 'Блонд', 2, '', rings=[(200, 160, 60)], ink=(40, 70, 140), label='BLONDES',
          pack=dict(base=(220, 190, 110), smoke=(40, 70, 140), ink=(40, 70, 140))),
        V('Filtre', 'Filtre', 'Фильтр', 2, '', rings=[(40, 80, 150)], ink=(40, 70, 140), label='FILTRE',
          pack=dict(base=(60, 110, 180), smoke=(240, 240, 244), ink=(240, 240, 244))),
        V('Mais', 'Mais', 'Маис', 3, 'warm', paper=(236, 206, 110), filt='none', fmt='nofilter', ink=(90, 60, 20), label='MAIS',
          pack=dict(base=(240, 200, 70), smoke=(60, 50, 30), ink=(60, 50, 30))),
        V('Blanches', 'Blanches', 'Бланш', 0, '', filt='white', rings=[SILVER], ink=(40, 70, 140), label='BLANCHES',
          pack=dict(base=(244, 244, 246), smoke=(40, 80, 150), ink=(40, 80, 150))),
    ]),
    'belomor': ('Belomorkanall', 'belomor', [
        V('Classic', 'Classic', 'Классический', 3, 'warm', filt='papirosa', fmt='papirosa', ink=(40, 70, 130), label='CLASSIC',
          pack=dict(base=(226, 230, 236), map=(40, 80, 150), ink=(40, 70, 130))),
        V('Leningrad', 'Leningrad', 'Ленинградский', 3, 'warm', filt='papirosa', fmt='papirosa', ink=(40, 70, 130), label='LENINGRAD',
          pack=dict(base=(214, 226, 240), map=(30, 60, 130), ink=(30, 60, 130))),
        V('Moscow', 'Moscow', 'Московский', 3, 'warm', filt='papirosa', fmt='papirosa', ink=(150, 30, 36), label='MOSCOW',
          pack=dict(base=(232, 228, 220), map=(150, 30, 36), ink=(150, 30, 36))),
        V('Northern', 'Northern', 'Северный', 3, 'warm', filt='papirosa', fmt='papirosa', ink=(30, 90, 110), label='NORTHERN',
          pack=dict(base=(220, 234, 236), map=(30, 100, 120), ink=(30, 90, 110))),
        V('Jubilee', 'Jubilee', 'Юбилейный', 3, 'warm', filt='papirosa', fmt='papirosa', ink=(150, 116, 50), label='JUBILEE',
          pack=dict(base=(240, 232, 214), map=(170, 130, 56), ink=(120, 90, 36))),
    ]),
    'yawa': ('Yawa', 'yawa', [
        V('GoldClassic', 'Gold Classic', 'Золотая Классическая', 2, '', rings=[GOLD], ink=(150, 116, 50), label='GOLD CLASSIC',
          pack=dict(base=(206, 166, 82), band=(150, 30, 36), ink=(60, 40, 20))),
        V('GoldOriginal', 'Gold Original', 'Золотая Оригинальная', 2, '', rings=[GOLD, GOLD], ink=(150, 116, 50), label='GOLD ORIGINAL',
          pack=dict(base=(214, 178, 96), band=(40, 40, 44), ink=(60, 40, 20))),
        V('WhiteGold', 'White Gold', 'Белая Золотая', 1, '', filt='white', rings=[GOLD], ink=(150, 116, 50), label='WHITE GOLD',
          pack=dict(base=(244, 242, 236), band=(206, 166, 82), ink=(120, 90, 36))),
        V('Blue', 'Blue', 'Синяя', 1, '', rings=[(40, 80, 160)], ink=(40, 80, 160), label='BLUE',
          pack=dict(base=(40, 70, 150), band=(206, 166, 82), ink=(240, 236, 226))),
        V('Long100', '100', 'Сотка', 2, '', fmt='long', rings=[GOLD], ink=(150, 116, 50), label='100 MM',
          pack=dict(base=(130, 26, 34), band=(206, 166, 82), ink=(240, 226, 190))),
    ]),
    'devol': ('Black Devol', 'devol', [
        V('Pink', 'Pink', 'Пинк', 1, 'sweet', paper=BLACK, filt='color', filter_color=(226, 120, 160), rings=[(226, 120, 160)], ink=(226, 120, 160),
          fmt='superslim', shape='slim', pack=dict(base=(24, 22, 24), accent=(226, 120, 160))),
        V('Special', 'Special', 'Спешл', 2, 'sweet', paper=BLACK, filt='black', rings=[(190, 30, 40)], ink=(190, 30, 40),
          pack=dict(base=(24, 22, 24), accent=(190, 30, 40))),
        V('Cherry', 'Cherry', 'Черри', 1, 'sweet', paper=BLACK, filt='color', filter_color=(150, 20, 40), rings=[(150, 20, 40)], ink=(170, 30, 50),
          fmt='superslim', shape='slim', pack=dict(base=(24, 22, 24), accent=(160, 24, 44))),
        V('Chocolate', 'Chocolate', 'Шоколад', 2, 'coffee', paper=BLACK, filt='brown', filter_color=(96, 58, 34), rings=[(150, 100, 60)], ink=(170, 120, 70),
          pack=dict(base=(24, 22, 24), accent=(150, 100, 60))),
        V('Mint', 'Mint', 'Минт', 1, 'fresh', paper=BLACK, filt='color', filter_color=(60, 170, 110), rings=[(60, 170, 110)], ink=(60, 170, 110),
          pack=dict(base=(24, 22, 24), accent=(60, 170, 110))),
        V('Gold', 'Gold', 'Голд', 2, 'sweet', paper=BLACK, filt='gold', rings=[GOLD], ink=(206, 166, 82),
          pack=dict(base=(24, 22, 24), accent=(206, 166, 82))),
    ]),
    'natsherman': ('Nat Shermann', 'natsherman', [
        V('FantasiaPink', 'Fantasia Pink', 'Фантазия Пинк', 1, 'calm', paper=(232, 150, 180), filt='gold', ink=(150, 116, 50), fmt='long', label='FANTASIA',
          pack=dict(base=(240, 236, 226), accent=(232, 150, 180))),
        V('FantasiaBlue', 'Fantasia Blue', 'Фантазия Блю', 1, 'calm', paper=(120, 160, 220), filt='gold', ink=(150, 116, 50), fmt='long', label='FANTASIA',
          pack=dict(base=(240, 236, 226), accent=(120, 160, 220))),
        V('FantasiaYellow', 'Fantasia Yellow', 'Фантазия Йеллоу', 1, 'calm', paper=(236, 210, 110), filt='gold', ink=(150, 116, 50), fmt='long', label='FANTASIA',
          pack=dict(base=(240, 236, 226), accent=(226, 196, 90))),
        V('FantasiaLilac', 'Fantasia Lilac', 'Фантазия Лайлак', 1, 'calm', paper=(186, 150, 210), filt='gold', ink=(150, 116, 50), fmt='long', label='FANTASIA',
          pack=dict(base=(240, 236, 226), accent=(176, 140, 206))),
        V('FantasiaGreen', 'Fantasia Green', 'Фантазия Грин', 1, 'calm', paper=(140, 196, 150), filt='gold', ink=(150, 116, 50), fmt='long', label='FANTASIA',
          pack=dict(base=(240, 236, 226), accent=(120, 180, 130))),
        V('ClassicBrown', 'Classic Brown', 'Классик Браун', 2, 'calm', paper=BROWN_PAPER, filt='brown', filter_color=(104, 64, 38), ink=GOLD_LIGHT, fmt='long',
          label='CLASSIC', pack=dict(base=(96, 58, 34), accent=(206, 166, 82), ink=(240, 226, 190))),
        V('BlackGold', 'Black & Gold', 'Блэк энд Голд', 2, 'calm', paper=BLACK, filt='gold', ink=(206, 166, 82), fmt='long', label='BLACK & GOLD',
          pack=dict(base=(26, 24, 24), accent=(206, 166, 82), ink=(220, 190, 120))),
        V('Naturals', 'Naturals', 'Нэчуралс', 2, 'calm', ink=(110, 80, 40), fmt='long', label='NATURALS',
          pack=dict(base=(232, 220, 196), accent=(140, 100, 50))),
    ]),
    'djarumm': ('Djarumm', 'djarumm', [
        V('Black', 'Black', 'Блэк', 3, 'warm', paper=BLACK, filt='black', rings=[GOLD], ink=(206, 166, 82), pack=dict(base=(24, 22, 24), accent=(206, 166, 82))),
        V('BlackCappuccino', 'Black Cappuccino', 'Блэк Капучино', 3, 'coffee', paper=BLACK, filt='brown', filter_color=(110, 70, 40), rings=[(170, 120, 70)],
          ink=(170, 120, 70), label='CAPPUCCINO', pack=dict(base=(24, 22, 24), accent=(170, 120, 70))),
        V('BlackMenthol', 'Black Menthol', 'Блэк Ментол', 3, 'fresh', paper=BLACK, filt='black', rings=[(60, 170, 110)], ink=(60, 170, 110), label='MENTHOL',
          pack=dict(base=(24, 22, 24), accent=(60, 170, 110))),
        V('Super', 'Super', 'Супер', 3, 'warm', paper=BROWN_PAPER, rings=[(190, 30, 40)], ink=(240, 226, 190), pack=dict(base=(150, 26, 34), accent=(236, 210, 120))),
        V('Splash', 'Splash', 'Сплэш', 2, 'fresh', rings=[(60, 160, 210)], ink=(40, 120, 180), pack=dict(base=(30, 90, 150), accent=(120, 210, 240))),
    ]),
    'treasurrer': ('Treasurrer', 'treasurrer', [
        V('Silver', 'Silver', 'Сильвер', 1, 'calm', paper=(200, 202, 208), filt='color', filter_color=(186, 188, 196), ink=(60, 64, 76),
          pack=dict(base=(196, 198, 206), accent=(70, 74, 86))),
        V('Gold', 'Gold', 'Голд', 2, 'calm', paper=(206, 170, 90), filt='gold', ink=(90, 60, 20), pack=dict(base=(206, 168, 86), accent=(80, 54, 20))),
        V('Black', 'Black', 'Блэк', 2, 'calm', paper=BLACK, filt='gold', ink=(206, 166, 82), pack=dict(base=(26, 24, 24), accent=(206, 166, 82))),
        V('AluminiumBlue', 'Aluminium Blue', 'Алюминиум Блю', 1, 'calm', paper=(150, 176, 210), filt='color', filter_color=(176, 186, 200), ink=(30, 50, 100),
          label='ALUMINIUM BLUE', pack=dict(base=(140, 166, 204), accent=(30, 50, 100))),
        V('Original', 'Original', 'Ориджинал', 2, 'calm', filt='gold', rings=[GOLD], ink=(150, 116, 50), pack=dict(base=(240, 236, 226), accent=(150, 116, 50))),
    ]),
    'newpork': ('Newpork', 'newpork', [
        V('Menthol', 'Menthol', 'Ментол', 2, 'fresh', rings=[(40, 150, 90)], ink=(30, 120, 70), pack=dict(base=(244, 244, 240), accent=(40, 150, 90))),
        V('MentholGold', 'Menthol Gold', 'Ментол Голд', 1, 'fresh', rings=[GOLD, (40, 150, 90)], ink=(30, 120, 70), label='MENTHOL GOLD',
          pack=dict(base=(244, 244, 240), accent=(196, 156, 76))),
        V('Red', 'Red', 'Ред', 2, '', rings=[(190, 30, 40)], ink=(170, 26, 34), pack=dict(base=(244, 244, 240), accent=(190, 30, 40))),
        V('Platinum', 'Platinum', 'Платинум', 0, 'fresh', filt='white', rings=[SILVER], ink=(120, 124, 132), pack=dict(base=(244, 244, 240), accent=(140, 146, 158))),
        V('NonMenthol', 'Non-Menthol', 'Без ментола', 2, '', rings=[(230, 120, 30)], ink=(200, 100, 20), label='NON-MENTHOL',
          pack=dict(base=(244, 244, 240), accent=(220, 110, 30))),
    ]),
    # ---- batch 6 (20 brands, 11-09)
    'kosmoss': ('Kosmoss', 'kosmoss', [
        V('Classic', 'Classic', 'Классические', 2, '', rings=[(40, 70, 150)], ink=(30, 50, 120), label='CLASSIC',
          pack=dict(base=(22, 40, 96), accent=(226, 196, 110), ink=(240, 240, 244))),
        V('Light', 'Light', 'Лёгкие', 1, '', filt='white', rings=[(110, 150, 210)], ink=(60, 100, 170), label='LIGHT',
          pack=dict(base=(80, 120, 190), accent=(240, 240, 246), ink=(250, 250, 252))),
        V('Long100', '100', 'Сотка', 2, '', fmt='long', rings=[GOLD], ink=(30, 50, 120), label='100 MM',
          pack=dict(base=(14, 22, 52), accent=(206, 166, 82), ink=(230, 220, 190))),
    ]),
    'primma': ('Primma', 'primma', [
        V('Original', 'Original', 'Оригинальная', 3, 'warm', filt='none', fmt='nofilter', ink=(160, 30, 36), label='WITHOUT FILTER',
          pack=dict(base=(236, 226, 200), accent=(170, 30, 36), ink=(40, 30, 26))),
        V('Filter', 'Filter', 'С фильтром', 2, '', ink=(160, 30, 36), label='FILTER',
          pack=dict(base=(170, 30, 36), accent=(240, 230, 210), ink=(240, 230, 210))),
        V('Nostalgia', 'Nostalgia', 'Ностальгия', 3, 'warm', paper=(236, 226, 196), filt='none', fmt='nofilter', ink=(90, 60, 30), label='NOSTALGIA',
          pack=dict(base=(214, 196, 150), accent=(110, 40, 30), ink=(70, 46, 26))),
    ]),
    'pyotr': ('Pyotr II', 'pyotr', [
        V('Classic', 'Classic', 'Классические', 2, '', rings=[GOLD], ink=(110, 20, 30), label='CLASSIC',
          pack=dict(base=(110, 20, 30), accent=(214, 176, 92), ink=(240, 226, 190))),
        V('Navy', 'Navy', 'Флотские', 2, '', rings=[GOLD, (30, 50, 110)], ink=(30, 50, 110), label='NAVY',
          pack=dict(base=(24, 40, 90), accent=(214, 176, 92), ink=(240, 226, 190))),
        V('Light', 'Light', 'Лёгкие', 1, '', filt='white', rings=[SILVER], ink=(80, 84, 96), label='LIGHT',
          pack=dict(base=(232, 230, 224), accent=(150, 116, 50), ink=(80, 40, 30))),
    ]),
    'optimma': ('Optimma', 'optimma', [
        V('Blue', 'Blue', 'Синие', 1, '', rings=[(40, 90, 180)], ink=(40, 90, 180), pack=dict(base=(246, 246, 244), accent=(40, 90, 180))),
        V('Red', 'Red', 'Красные', 2, '', rings=[(190, 30, 40)], ink=(170, 26, 34), pack=dict(base=(246, 246, 244), accent=(190, 30, 40))),
        V('Silver', 'Silver', 'Серебряные', 0, '', filt='white', rings=[SILVER], ink=(120, 124, 132), pack=dict(base=(246, 246, 244), accent=(140, 146, 160))),
    ]),
    'chesterfeld': ('Chesterfeld', 'chesterfeld', [
        V('Red', 'Red', 'Ред', 2, '', rings=[(180, 30, 40)], ink=(160, 26, 34), pack=dict(base=(170, 28, 36), accent=(246, 240, 228))),
        V('Blue', 'Blue', 'Блю', 1, '', rings=[(40, 70, 150)], ink=(30, 60, 130), pack=dict(base=(30, 60, 130), accent=(246, 240, 228))),
        V('Original', 'Original', 'Ориджинал', 3, '', filt='none', fmt='nofilter', ink=(160, 26, 34), label='UNFILTERED',
          pack=dict(base=(236, 224, 196), accent=(150, 26, 34), ink=(150, 26, 34))),
    ]),
    'phillip': ('Phillip Morose', 'phillip', [
        V('Blue', 'Blue', 'Блю', 1, '', rings=[(40, 80, 160)], ink=(30, 60, 130), pack=dict(base=(244, 242, 236), accent=(30, 60, 130), ink=(30, 40, 70))),
        V('Red', 'Red', 'Ред', 2, '', rings=[(180, 30, 40)], ink=(160, 26, 34), pack=dict(base=(244, 242, 236), accent=(170, 28, 36), ink=(60, 20, 20))),
        V('Novel', 'Novel', 'Новел', 0, '', filt='white', rings=[(150, 110, 190)], ink=(110, 80, 160), fmt='superslim', shape='slim', label='NOVEL',
          pack=dict(base=(238, 234, 246), accent=(110, 80, 160), ink=(50, 40, 80))),
    ]),
    'wesst': ('Wesst', 'wesst', [
        V('Red', 'Red', 'Ред', 2, '', rings=[(200, 30, 40)], ink=(200, 30, 40), pack=dict(base=(246, 246, 246), accent=(200, 30, 40), ink=(30, 30, 30))),
        V('Silver', 'Silver', 'Сильвер', 0, '', filt='white', rings=[SILVER], ink=(120, 124, 132), pack=dict(base=(246, 246, 246), accent=(130, 136, 150), ink=(30, 30, 30))),
        V('Ice', 'Ice', 'Айс', 1, 'fresh', rings=[(70, 170, 220)], ink=(40, 130, 190), pack=dict(base=(236, 246, 252), accent=(40, 140, 200), ink=(20, 40, 60))),
    ]),
    'lambert': ('Lambert & Bottler', 'lambert', [
        V('Original', 'Original', 'Ориджинал', 2, '', rings=[SILVER], ink=(60, 64, 76), pack=dict(base=(180, 184, 192), accent=(28, 30, 40), ink=(28, 30, 40))),
        V('Blue', 'Blue', 'Блю', 1, '', rings=[(50, 90, 170)], ink=(40, 70, 150), pack=dict(base=(50, 84, 160), accent=(230, 232, 240), ink=(240, 242, 248))),
        V('Menthol', 'Menthol', 'Ментол', 1, 'fresh', rings=[(40, 150, 90)], ink=(30, 120, 70), pack=dict(base=(40, 120, 80), accent=(220, 236, 226), ink=(240, 246, 242))),
    ]),
    'jps': ('John Prayer Special', 'jps', [
        V('Black', 'Black', 'Блэк', 2, '', filt='black', rings=[GOLD], ink=(30, 28, 28), label='BLACK',
          pack=dict(base=(18, 18, 20), accent=(206, 166, 82))),
        V('Blue', 'Blue', 'Блю', 1, '', rings=[GOLD], ink=(20, 36, 80), label='REAL BLUE',
          pack=dict(base=(16, 28, 70), accent=(206, 166, 82))),
        V('Silver', 'Silver', 'Сильвер', 0, '', filt='white', rings=[SILVER], ink=(120, 124, 132), label='SILVER',
          pack=dict(base=(60, 62, 70), accent=(214, 216, 222))),
    ]),
    'mayfire': ('Mayfire', 'mayfire', [
        V('Original', 'Original', 'Ориджинал', 2, 'warm', rings=[(220, 120, 30)], ink=(30, 50, 110), pack=dict(base=(30, 50, 110), accent=(236, 130, 40), ink=(246, 240, 228))),
        V('SkyBlue', 'Sky Blue', 'Скай Блю', 1, 'warm', rings=[(110, 170, 220)], ink=(40, 100, 170), label='SKY BLUE',
          pack=dict(base=(110, 166, 216), accent=(240, 160, 50), ink=(250, 250, 252))),
        V('Gold', 'Gold', 'Голд', 1, 'warm', rings=[GOLD], ink=(150, 116, 50), pack=dict(base=(214, 178, 96), accent=(190, 60, 30), ink=(50, 34, 20))),
    ]),
    'silkkut': ('Silk Kut', 'silkkut', [
        V('Purple', 'Purple', 'Пёрпл', 0, 'calm', filt='white', rings=[(120, 60, 150)], ink=(110, 50, 140), pack=dict(base=(100, 44, 130), accent=(236, 226, 246))),
        V('Silver', 'Silver', 'Сильвер', 0, 'calm', filt='white', rings=[SILVER], ink=(120, 124, 132), pack=dict(base=(150, 154, 164), accent=(246, 246, 250))),
        V('Blue', 'Blue', 'Блю', 0, 'calm', filt='white', rings=[(60, 110, 190)], ink=(40, 90, 170), pack=dict(base=(40, 80, 160), accent=(226, 236, 250))),
    ]),
    'stuyvesand': ('Peter Stuyvesand', 'stuyvesand', [
        V('Red', 'Red', 'Ред', 2, '', rings=[(190, 30, 40)], ink=(20, 34, 80), label='INTERNATIONAL', pack=dict(base=(246, 244, 238), accent=(190, 30, 40), ink=(20, 34, 80))),
        V('Blue', 'Blue', 'Блю', 1, '', rings=[(60, 110, 200)], ink=(20, 34, 80), label='BLUE', pack=dict(base=(246, 244, 238), accent=(50, 100, 190), ink=(20, 34, 80))),
        V('Silver', 'Silver', 'Сильвер', 0, '', filt='white', rings=[SILVER], ink=(80, 84, 96), label='SILVER', pack=dict(base=(246, 244, 238), accent=(150, 156, 168), ink=(50, 54, 64))),
    ]),
    'vicerroy': ('Vicerroy', 'vicerroy', [
        V('Red', 'Red', 'Ред', 2, '', rings=[(190, 30, 40)], ink=(170, 26, 34), pack=dict(base=(170, 28, 36), accent=(246, 240, 228), ink=(246, 240, 228))),
        V('Blue', 'Blue', 'Блю', 1, '', rings=[(40, 80, 160)], ink=(30, 60, 130), pack=dict(base=(30, 60, 140), accent=(246, 240, 228), ink=(246, 240, 228))),
        V('Black', 'Black', 'Блэк', 2, '', filt='black', rings=[(190, 30, 40)], ink=(30, 28, 28), pack=dict(base=(26, 24, 26), accent=(200, 40, 46), ink=(236, 236, 240))),
    ]),
    'salemm': ('Salemm', 'salemm', [
        V('Green', 'Green', 'Грин', 1, 'fresh', rings=[(40, 150, 90)], ink=(30, 110, 70), pack=dict(base=(34, 110, 70), accent=(236, 244, 238))),
        V('Silver', 'Silver', 'Сильвер', 0, 'fresh', filt='white', rings=[(150, 210, 180)], ink=(60, 140, 100), pack=dict(base=(200, 226, 212), accent=(30, 100, 64))),
        V('Black', 'Black', 'Блэк', 2, 'fresh', paper=BLACK, filt='black', rings=[(60, 170, 110)], ink=(60, 170, 110), pack=dict(base=(22, 26, 24), accent=(60, 170, 110))),
    ]),
    'kooll': ('Kooll', 'kooll', [
        V('Green', 'Green', 'Грин', 2, 'fresh', rings=[(40, 150, 90)], ink=(30, 110, 70), label='FILTER KINGS',
          pack=dict(base=(30, 120, 76), accent=(246, 246, 240), ink=(246, 246, 240))),
        V('Blue', 'Blue', 'Блю', 1, 'fresh', rings=[(40, 110, 190)], ink=(30, 80, 160), label='MILDS',
          pack=dict(base=(36, 90, 170), accent=(246, 246, 240), ink=(246, 246, 240))),
        V('ExtraIce', 'Extra Ice', 'Экстра Айс', 1, 'fresh', filt='white', rings=[(120, 200, 230)], ink=(40, 140, 190), label='EXTRA ICE',
          pack=dict(base=(220, 240, 248), accent=(30, 110, 160), ink=(20, 70, 110))),
    ]),
    'spirrit': ('American Spirrit', 'spirrit', [
        V('Yellow', 'Yellow', 'Йеллоу', 1, '', paper=(244, 238, 222), rings=[(230, 190, 60)], ink=(150, 110, 30), label='MELLOW',
          pack=dict(base=(236, 196, 70), accent=(150, 50, 30), ink=(60, 34, 20))),
        V('Turquoise', 'Turquoise', 'Тёркуаз', 2, '', paper=(244, 238, 222), rings=[(60, 170, 170)], ink=(30, 120, 130), label='TURQUOISE',
          pack=dict(base=(60, 166, 170), accent=(240, 220, 170), ink=(240, 236, 226))),
        V('Perique', 'Perique', 'Перик', 3, 'warm', paper=(244, 238, 222), filt='brown', filter_color=(110, 70, 40), rings=[(30, 28, 28)], ink=(30, 28, 28),
          label='PERIQUE', pack=dict(base=(32, 30, 30), accent=(200, 70, 40), ink=(236, 220, 180))),
    ]),
    'mewius': ('Mewius', 'mewius', [
        V('SkyBlue', 'Sky Blue', 'Скай Блю', 1, '', filt='white', rings=[(110, 170, 230)], ink=(50, 110, 190), label='SKY BLUE',
          pack=dict(base=(246, 248, 252), accent=(80, 150, 220), ink=(30, 60, 110))),
        V('Original', 'Original', 'Ориджинал', 2, '', rings=[(30, 60, 140)], ink=(30, 60, 140), label='ORIGINAL',
          pack=dict(base=(246, 248, 252), accent=(30, 56, 130), ink=(20, 34, 70))),
        V('OptionPurple', 'Option Purple', 'Опшн Пёрпл', 1, 'sweet', filt='white', rings=[(150, 90, 190)], ink=(120, 70, 170), label='OPTION PURPLE',
          pack=dict(base=(246, 244, 250), accent=(130, 76, 180), ink=(60, 30, 90))),
    ]),
    'hopp': ('Hopp', 'hopp', [
        V('Original', 'Original', 'Ориджинал', 2, '', fmt='short', rings=[(200, 160, 60)], ink=(30, 60, 130), pack=dict(base=(80, 150, 210), accent=(236, 196, 80), ink=(246, 246, 250))),
        V('Menthol', 'Menthol', 'Ментол', 1, 'fresh', fmt='short', rings=[(40, 150, 90)], ink=(30, 110, 70), pack=dict(base=(50, 150, 110), accent=(236, 196, 80), ink=(246, 246, 250))),
        V('SuperLight', 'Super Light', 'Супер Лайт', 0, '', fmt='short', filt='white', rings=[SILVER], ink=(90, 110, 140), label='SUPER LIGHT',
          pack=dict(base=(220, 232, 244), accent=(200, 150, 50), ink=(30, 60, 110))),
    ]),
    'muratty': ('Muratty', 'muratty', [
        V('Ambassador', 'Ambassador', 'Амбассадор', 2, 'calm', filt='gold', rings=[GOLD], ink=(150, 116, 50), label='AMBASSADOR',
          pack=dict(base=(150, 24, 32), accent=(214, 176, 92), ink=(240, 226, 190))),
        V('Blue', 'Blue', 'Блю', 1, 'calm', rings=[GOLD, (40, 70, 150)], ink=(30, 50, 120), pack=dict(base=(26, 46, 110), accent=(214, 176, 92), ink=(240, 226, 190))),
        V('White', 'White', 'Уайт', 0, 'calm', filt='white', rings=[GOLD], ink=(150, 116, 50), pack=dict(base=(246, 242, 234), accent=(176, 136, 60), ink=(80, 50, 20))),
    ]),
    'sovereing': ('Sovereing', 'sovereing', [
        V('Blue', 'Blue', 'Блю', 1, '', rings=[GOLD, (30, 60, 140)], ink=(30, 60, 140), pack=dict(base=(30, 58, 136), accent=(214, 176, 92), ink=(246, 240, 226))),
        V('Red', 'Red', 'Ред', 2, '', rings=[GOLD, (180, 30, 40)], ink=(160, 26, 34), pack=dict(base=(160, 26, 34), accent=(214, 176, 92), ink=(246, 240, 226))),
        V('Silver', 'Silver', 'Сильвер', 0, '', filt='white', rings=[SILVER], ink=(110, 114, 124), pack=dict(base=(170, 174, 184), accent=(250, 250, 252), ink=(30, 34, 44))),
    ]),
}

# batch 6 (11-09): new effects. Positive: hands (fire-starting chance +10%) / steady (sprint stamina drains slower) for varieties
# without a flavour of their own; side effects by strength: strong (2) dry mouth, very strong (3) dizziness.
BATCH6 = ['kosmoss', 'primma', 'pyotr', 'optimma', 'chesterfeld', 'phillip', 'wesst', 'lambert', 'jps', 'mayfire', 'silkkut', 'stuyvesand',
          'vicerroy', 'salemm', 'kooll', 'spirrit', 'mewius', 'hopp', 'muratty', 'sovereing']
VIGOR6 = {'kosmoss', 'primma', 'pyotr', 'chesterfeld', 'wesst', 'jps', 'vicerroy', 'hopp', 'spirrit'}
STEADY6 = {'optimma', 'phillip', 'lambert', 'stuyvesand', 'mewius', 'sovereing'}
for _k in BATCH6:
    for _v in BRANDS[_k][2]:
        if not _v['bonus']:
            _v['bonus'] = 'hands' if _k in VIGOR6 else 'steady' if _k in STEADY6 else ''
        _v['side'] = 'dizzy' if _v['strength'] >= 3 else 'dry' if _v['strength'] == 2 else ''

# batch 7 (11-14): the first dozen of the "local and national" brands the owner asked for: Canada (where the game is set),
# USSR / Russia, the world. Same scheme as batch 6: three kinds each, a flavour of their own or hands / steady, side effects by strength.
CREAM = (236, 226, 200)
BRANDS.update({
    'exporteh': ('Export Eh', 'exporteh', [
        V('Green', 'Green', 'Грин', 2, '', rings=[(30, 110, 60)], ink=(26, 96, 52), label='FULL FLAVOUR',
          pack=dict(base=(24, 104, 58), accent=(246, 240, 224), ink=(246, 240, 224))),
        V('Blue', 'Blue', 'Блю', 1, '', rings=[(40, 80, 160)], ink=(30, 64, 140), label='MEDIUM',
          pack=dict(base=(30, 66, 146), accent=(246, 240, 224), ink=(246, 240, 224))),
        V('Gold', 'Gold', 'Голд', 0, '', filt='white', rings=[GOLD], ink=(150, 116, 50), label='SMOOTH',
          pack=dict(base=(214, 178, 92), accent=(60, 40, 20), ink=(50, 34, 18))),
    ]),
    'dumorrier': ('du Morrier', 'dumorrier', [
        V('Signature', 'Signature', 'Сигнатюр', 1, 'calm', rings=[SILVER, (170, 26, 34)], ink=(160, 24, 32), label='SIGNATURE',
          pack=dict(base=(170, 22, 30), accent=(214, 216, 222), ink=(240, 240, 244))),
        V('Distinct', 'Distinct', 'Дистинкт', 0, 'calm', filt='white', rings=[SILVER], ink=(110, 114, 124), label='DISTINCT',
          pack=dict(base=(206, 208, 214), accent=(160, 24, 32), ink=(40, 40, 46))),
        V('Noir', 'Noir', 'Нуар', 2, 'calm', filt='black', rings=[(170, 26, 34)], ink=(30, 28, 28), label='NOIR',
          pack=dict(base=(24, 22, 24), accent=(190, 36, 44), ink=(236, 236, 240))),
    ]),
    'playerz': ('Playerz', 'playerz', [
        V('NavyCut', 'Navy Cut', 'Нэви Кат', 3, 'warm', filt='none', fmt='nofilter', ink=(24, 40, 96), label='NAVY CUT',
          pack=dict(base=(240, 236, 222), accent=(24, 40, 96), ink=(24, 40, 96))),
        V('Filter', 'Filter', 'Фильтр', 2, '', rings=[(24, 40, 96)], ink=(24, 40, 96), label='FILTER',
          pack=dict(base=(24, 44, 104), accent=(240, 236, 222), ink=(240, 236, 222))),
        V('Light', 'Light', 'Лайт', 1, '', filt='white', rings=[(110, 160, 220)], ink=(50, 100, 170), label='LIGHT',
          pack=dict(base=(150, 190, 230), accent=(24, 40, 96), ink=(20, 34, 80))),
    ]),
    'kazbekk': ('Kazbekk', 'kazbekk', [
        V('Classic', 'Classic', 'Классический', 3, 'warm', filt='papirosa', fmt='papirosa', ink=(30, 50, 110), label='CLASSIC',
          pack=dict(base=(214, 228, 240), accent=(30, 50, 110), ink=(20, 30, 70))),
        V('Summit', 'Summit', 'Вершина', 2, 'fresh', filt='papirosa', fmt='papirosa', ink=(40, 110, 150), label='SUMMIT',
          pack=dict(base=(236, 242, 246), accent=(50, 120, 160), ink=(20, 60, 90))),
        V('NightRider', 'Night Rider', 'Ночной всадник', 3, 'warm', filt='papirosa', fmt='papirosa', ink=(30, 30, 60), label='NIGHT RIDER',
          pack=dict(base=(26, 30, 62), accent=(226, 214, 160), ink=(226, 214, 160))),
    ]),
    'gertsegovina': ('Gertsegovina Flor', 'gertsegovina', [
        V('Classic', 'Classic', 'Классическая', 2, 'calm', filt='papirosa', fmt='papirosa', ink=(150, 116, 50), label='CLASSIC',
          pack=dict(base=(20, 44, 34), accent=(206, 170, 86), ink=(226, 196, 120))),
        V('Lux', 'Lux', 'Люкс', 2, 'calm', filt='papirosa', fmt='papirosa', ink=(150, 116, 50), label='LUX',
          pack=dict(base=(22, 22, 24), accent=(214, 178, 92), ink=(232, 204, 130))),
        V('Emerald', 'Emerald', 'Изумрудная', 1, 'calm', filt='papirosa', fmt='papirosa', ink=(30, 110, 76), label='EMERALD',
          pack=dict(base=(232, 226, 204), accent=(26, 100, 70), ink=(20, 70, 50))),
    ]),
    'pomir': ('Pomir', 'pomir', [
        V('Classic', 'Classic', 'Классический', 3, 'warm', paper=CREAM, filt='none', fmt='nofilter', ink=(90, 56, 30), label='WITHOUT FILTER',
          pack=dict(base=(150, 104, 62), accent=(240, 228, 200), ink=(244, 234, 208))),
        V('IceAxe', 'Ice Axe', 'Ледоруб', 3, '', paper=CREAM, filt='none', fmt='nofilter', ink=(50, 80, 110), label='ICE AXE',
          pack=dict(base=(96, 124, 150), accent=(240, 240, 236), ink=(244, 244, 240))),
        V('BaseCamp', 'Base Camp', 'Базовый лагерь', 2, 'warm', ink=(120, 50, 30), label='BASE CAMP  ·  FILTER',
          pack=dict(base=(226, 208, 170), accent=(140, 56, 30), ink=(80, 40, 22))),
    ]),
    'tu143': ('Tu-143', 'tu143', [
        V('Classic', 'Classic', 'Классик', 2, '', rings=[(40, 90, 170)], ink=(30, 70, 150), label='CLASSIC',
          pack=dict(base=(240, 242, 246), accent=(34, 80, 160), ink=(24, 50, 110))),
        V('Lights', 'Lights', 'Лайтс', 1, '', filt='white', rings=[(120, 170, 226)], ink=(50, 110, 180), label='LIGHTS',
          pack=dict(base=(120, 168, 222), accent=(244, 246, 250), ink=(246, 248, 252))),
        V('BusinessClass', 'Business Class', 'Бизнес-класс', 1, 'calm', filt='gold', rings=[GOLD], ink=(150, 116, 50), label='BUSINESS CLASS',
          pack=dict(base=(22, 34, 70), accent=(214, 178, 92), ink=(232, 204, 130))),
    ]),
    'stewardesa': ('Stewardesa', 'stewardesa', [
        V('Classic', 'Classic', 'Классик', 2, '', rings=[(40, 100, 180)], ink=(30, 80, 160), label='CLASSIC',
          pack=dict(base=(244, 244, 240), accent=(40, 100, 180), ink=(24, 60, 120))),
        V('Sky', 'Sky', 'Скай', 1, 'fresh', filt='white', rings=[(110, 180, 230)], ink=(40, 130, 190), label='SKY  ·  MENTHOL',
          pack=dict(base=(170, 214, 240), accent=(246, 248, 250), ink=(20, 70, 120))),
        V('FirstClass', 'First Class', 'Первый класс', 0, 'calm', filt='white', rings=[GOLD], ink=(150, 116, 50), fmt='superslim', shape='slim',
          label='FIRST CLASS', pack=dict(base=(246, 240, 230), accent=(190, 150, 70), ink=(90, 60, 24))),
    ]),
    'soyuz': ('Soyuz Apollon', 'soyuz', [
        V('Special', 'Special', 'Особые', 2, '', rings=[(180, 30, 40)], ink=(160, 26, 34), label='SPECIAL',
          pack=dict(base=(244, 242, 236), accent=(176, 28, 36), ink=(30, 40, 80))),
        V('Lights', 'Lights', 'Лайтс', 1, '', filt='white', rings=[(60, 110, 190)], ink=(40, 90, 170), label='LIGHTS',
          pack=dict(base=(244, 242, 236), accent=(40, 90, 170), ink=(30, 40, 80))),
        V('Orbit', 'Orbit', 'Орбита', 0, '', filt='white', rings=[SILVER], ink=(110, 114, 124), label='ORBIT',
          pack=dict(base=(20, 24, 40), accent=(190, 194, 204), ink=(230, 232, 238))),
    ]),
    'rothhandel': ('Roth-Handel', 'rothhandel', [
        V('OhneFilter', 'Ohne Filter', 'Оне Фильтер', 3, 'warm', paper=CREAM, filt='none', fmt='nofilter', ink=(160, 26, 30), label='OHNE FILTER',
          pack=dict(base=(176, 28, 32), accent=(246, 240, 226), ink=(246, 240, 226))),
        V('Filter', 'Filter', 'Фильтер', 2, '', rings=[(170, 28, 32)], ink=(160, 26, 30), label='MIT FILTER',
          pack=dict(base=(246, 240, 226), accent=(176, 28, 32), ink=(30, 26, 26))),
        V('Blau', 'Blau', 'Блау', 1, '', filt='white', rings=[(40, 80, 160)], ink=(30, 64, 140), label='BLAU',
          pack=dict(base=(32, 66, 140), accent=(246, 240, 226), ink=(246, 240, 226))),
    ]),
    'doublehappy': ('Double Happyness', 'doublehappy', [
        V('Red', 'Red', 'Ред', 2, 'calm', filt='gold', rings=[GOLD, (180, 30, 36)], ink=(170, 26, 32), label='CLASSIC RED',
          pack=dict(base=(184, 24, 30), accent=(226, 190, 100), ink=(240, 210, 130))),
        V('Gold', 'Gold', 'Голд', 1, 'calm', filt='gold', rings=[GOLD], ink=(150, 116, 50), label='GOLDEN DRAGON',
          pack=dict(base=(214, 176, 86), accent=(170, 24, 30), ink=(120, 20, 24))),
        V('Jade', 'Jade', 'Джейд', 1, 'fresh', filt='white', rings=[(40, 150, 110)], ink=(30, 120, 86), label='JADE  ·  MENTHOL',
          pack=dict(base=(30, 116, 86), accent=(226, 190, 100), ink=(240, 220, 150))),
    ]),
    'moar': ('Moar', 'moar', [
        V('Red', 'Red 120', 'Ред 120', 2, '', paper=BROWN_PAPER, filt='brown', filter_color=(104, 64, 38), rings=[GOLD], ink=GOLD_LIGHT, fmt='long',
          label='RED  ·  120s', pack=dict(base=(170, 26, 32), accent=(226, 190, 100), ink=(244, 232, 200))),
        V('Menthol', 'Menthol 120', 'Ментол 120', 1, 'fresh', paper=BROWN_PAPER, filt='brown', filter_color=(104, 64, 38), rings=[(60, 170, 110)], ink=GOLD_LIGHT,
          fmt='long', label='MENTHOL  ·  120s', pack=dict(base=(26, 104, 66), accent=(226, 190, 100), ink=(244, 232, 200))),
        V('Beige', 'Beige 120', 'Бейдж 120', 1, 'sweet', paper=(214, 190, 150), filt='color', filter_color=(190, 160, 116), rings=[GOLD], ink=(110, 70, 36),
          fmt='long', label='BEIGE  ·  120s', pack=dict(base=(222, 200, 160), accent=(120, 70, 36), ink=(80, 46, 22))),
    ]),
    # three new shapes (cigvar.FORMATS): a very long mouthpiece, a clove cone, a leaf roll tied with a thread
    'cigaronni': ('Cigaronni', 'cigaronni', [
        V('Black', 'Black', 'Блэк', 2, 'calm', paper=BLACK, filt='black', rings=[GOLD], ink=GOLD_LIGHT, fmt='cigaronni', shape='slim', label='ROYAL BLACK',
          pack=dict(base=(20, 20, 22), accent=(214, 178, 92), ink=(232, 204, 130))),
        V('White', 'White', 'Уайт', 1, 'calm', filt='white', rings=[GOLD], ink=(150, 116, 50), fmt='cigaronni', shape='slim', label='ROYAL WHITE',
          pack=dict(base=(246, 244, 238), accent=(190, 150, 70), ink=(60, 44, 20))),
        V('Blue', 'Blue', 'Блю', 0, 'calm', filt='white', rings=[(60, 110, 190)], ink=(40, 90, 170), fmt='cigaronni', shape='slim', label='ULTRA SLIMS BLUE',
          pack=dict(base=(30, 60, 140), accent=(214, 216, 224), ink=(240, 240, 246))),
    ]),
    'gudang': ('Gudang Garram', 'gudang', [
        V('Merah', 'Merah', 'Мерах', 3, 'sweet', paper=(238, 232, 214), filt='none', fmt='kretek', ink=(160, 26, 30), label='MERAH  ·  KRETEK',
          pack=dict(base=(176, 26, 30), accent=(232, 196, 110), ink=(244, 228, 180))),
        V('Djaja', 'Djaja', 'Джая', 2, 'sweet', paper=(238, 232, 214), filt='none', fmt='kretek', ink=(30, 100, 60), label='DJAJA  ·  KRETEK',
          pack=dict(base=(26, 96, 60), accent=(232, 196, 110), ink=(244, 228, 180))),
        V('Klobot', 'Klobot', 'Клобот', 3, 'warm', paper=(214, 190, 130), filt='none', fmt='kretek', ink=(110, 70, 30), label='KLOBOT  ·  CORN HUSK',
          pack=dict(base=(222, 196, 130), accent=(120, 50, 26), ink=(80, 40, 20))),
    ]),
    'pattaka': ('Pattaka Bidi', 'pattaka', [
        V('RedThread', 'Red Thread', 'Красная нить', 3, 'warm', paper=(124, 112, 62), pattern='leaf', filt='none', fmt='bidi', rings=[(200, 36, 46)],
          ink=(170, 30, 36), label='RED THREAD', pack=dict(base=(236, 214, 120), accent=(196, 34, 40), ink=(120, 20, 24))),
        V('GreenThread', 'Green Thread', 'Зелёная нить', 2, 'fresh', paper=(112, 120, 66), pattern='leaf', filt='none', fmt='bidi', rings=[(40, 150, 80)],
          ink=(30, 110, 60), label='GREEN THREAD  ·  MINT', pack=dict(base=(206, 226, 150), accent=(30, 116, 66), ink=(20, 70, 40))),
        V('Mango', 'Mango', 'Манго', 2, 'sweet', paper=(132, 110, 58), pattern='leaf', filt='none', fmt='bidi', rings=[(240, 160, 40)],
          ink=(200, 110, 20), label='MANGO', pack=dict(base=(246, 180, 70), accent=(190, 50, 30), ink=(110, 40, 20))),
    ]),
})
BATCH7 = ['exporteh', 'dumorrier', 'playerz', 'kazbekk', 'gertsegovina', 'pomir', 'tu143', 'stewardesa', 'soyuz', 'rothhandel', 'doublehappy', 'moar',
          'cigaronni', 'gudang', 'pattaka']
VIGOR7 = {'exporteh', 'playerz', 'pomir', 'rothhandel', 'kazbekk', 'pattaka'}
STEADY7 = {'tu143', 'stewardesa', 'soyuz', 'moar'}
for _k in BATCH7:
    for _v in BRANDS[_k][2]:
        if not _v['bonus']:
            _v['bonus'] = 'hands' if _k in VIGOR7 else 'steady' if _k in STEADY7 else ''
        _v['side'] = 'dizzy' if _v['strength'] >= 3 else 'dry' if _v['strength'] == 2 else ''

# batch 8 (11-14): the owner liked Double Happyness and asked for many more Asian brands after real ones, the Japanese ones looking
# Japanese. Same scheme: three kinds each, a flavour of their own or hands / steady, side effects by strength.
def _A(key, en, ru, strength, bonus, base, accent, ink, label, **kw):
    dark = lambda c: .299 * c[0] + .587 * c[1] + .114 * c[2] < 150
    cink = kw.pop('cink', None) or (base if dark(base) else accent if dark(accent) else (70, 56, 40))   # the print on the cigarette
    kw.setdefault('rings', [GOLD] if kw.get('filt') == 'gold' else [cink] if kw.get('filt', C_CORK) not in ('none', 'papirosa') else [])
    return V(key, en, ru, strength, bonus, ink=cink, label=label, pack=dict(base=base, accent=accent, ink=ink), **kw)


_G, _GL, _CR, _RED = (232, 196, 104), (246, 222, 150), (244, 238, 222), (186, 24, 30)
_NF = dict(paper=CREAM, filt='none', fmt='nofilter')
_PA = dict(filt='papirosa', fmt='papirosa')
_KR = dict(paper=(238, 232, 214), filt='none', fmt='kretek')
BRANDS.update({
    # China
    'chunghwah': ('Chunghwah', 'chunghwah', [
        _A('Soft', 'Soft', 'Софт', 2, 'calm', (190, 22, 28), _G, _GL, 'SOFT RED', filt='gold'),
        _A('Hard', 'Hard', 'Хард', 3, 'calm', (146, 16, 24), _G, _GL, 'HARD RED', filt='gold'),
        _A('Golden', 'Golden', 'Голден', 1, 'calm', (216, 178, 88), (160, 20, 26), (110, 16, 20), 'GOLDEN GATE', filt='gold'),
    ]),
    'hongta': ('Hongta Hill', 'hongta', [
        _A('Classic', 'Classic', 'Классик', 2, '', (244, 240, 230), _RED, (150, 22, 26), 'CLASSIC 1956'),
        _A('Blue', 'Soft Blue', 'Софт Блю', 1, '', (34, 74, 150), _CR, _CR, 'SOFT BLUE', filt='white'),
        _A('Red', 'Red Pagoda', 'Ред Пагода', 3, '', (176, 24, 28), _G, _CR, 'RED PAGODA'),
    ]),
    'yellowcrane': ('Yelow Crane Tower', 'yellowcrane', [
        _A('N1916', '1916', '1916', 2, 'calm', (40, 34, 28), _G, _GL, 'No. 1916', filt='gold'),
        _A('Blue', 'Soft Blue', 'Софт Блю', 1, '', (36, 84, 150), _G, _CR, 'SOFT BLUE', filt='white'),
        _A('Gold', 'Gold', 'Голд', 2, 'sweet', (222, 184, 90), (120, 40, 24), (80, 30, 18), 'GOLDEN TOWER', filt='gold'),
    ]),
    'pandah': ('Pandah', 'pandah', [
        _A('Classic', 'Classic', 'Классик', 2, 'calm', (244, 242, 236), (24, 24, 24), (24, 24, 24), 'CLASSIC', filt='white'),
        _A('Bamboo', 'Bamboo', 'Бамбу', 1, 'fresh', (40, 120, 70), _CR, _CR, 'BAMBOO  ·  MENTHOL', filt='white'),
        _A('Night', 'Night', 'Найт', 3, '', (22, 22, 24), (240, 238, 230), (240, 238, 230), 'NIGHT', filt='black'),
    ]),
    'yuxee': ('Yuxee', 'yuxee', [
        _A('Classic', 'Classic', 'Классик', 2, '', (246, 242, 232), _RED, (150, 20, 24), 'CLASSIC'),
        _A('Jade', 'Jade', 'Джейд', 1, 'fresh', (30, 110, 90), _G, _CR, 'JADE STREAM', filt='white'),
        _A('Soft', 'Soft', 'Софт', 0, '', (232, 222, 196), (150, 110, 50), (90, 60, 24), 'SOFT', filt='white'),
    ]),
    'zhongnan': ('Zhongnan Sea', 'zhongnan', [
        _A('Blue8', 'Blue 8', 'Блю 8', 2, '', (30, 70, 150), (240, 240, 244), (240, 240, 244), 'BLUE  ·  8', filt='white'),
        _A('White5', 'White 5', 'Уайт 5', 1, '', (244, 244, 240), (30, 70, 150), (30, 70, 150), 'WHITE  ·  5', filt='white'),
        _A('Night1', 'Night 1', 'Найт 1', 0, '', (24, 26, 34), (206, 210, 220), (206, 210, 220), 'NIGHT  ·  1', filt='white'),
    ]),
    'sevenwolfs': ('Seven Wolfs', 'sevenwolfs', [
        _A('White', 'White', 'Уайт', 1, '', (240, 240, 238), (30, 60, 130), (24, 44, 100), 'WHITE WOLF', filt='white'),
        _A('Blue', 'Blue Moon', 'Блю Мун', 2, '', (22, 44, 100), (206, 210, 220), (206, 210, 220), 'BLUE MOON'),
        _A('Red', 'Red', 'Ред', 3, '', (150, 20, 26), (230, 226, 214), (230, 226, 214), 'RED PACK'),
    ]),
    'lotusking': ('Lotus Kingg', 'lotusking', [
        _A('Yellow', 'Yellow', 'Йеллоу', 2, 'sweet', (238, 196, 60), (176, 28, 30), (120, 20, 22), 'HARD YELLOW', filt='gold'),
        _A('Blue', 'Blue', 'Блю', 1, 'calm', (30, 60, 130), _G, _GL, 'BLUE LOTUS', filt='gold'),
        _A('Pink', 'Pink', 'Пинк', 1, 'sweet', (236, 180, 190), (170, 40, 80), (120, 24, 56), 'PINK BLOSSOM', filt='white'),
    ]),
    'goldenleef': ('Golden Leef', 'goldenleef', [
        _A('Imperial', 'Imperial', 'Империал', 3, 'warm', (120, 20, 24), _G, _GL, 'IMPERIAL', filt='gold'),
        _A('Harvest', 'Harvest', 'Харвест', 2, '', (214, 170, 70), (100, 50, 20), (70, 34, 14), 'HARVEST'),
        _A('Sky', 'Sky', 'Скай', 1, '', (60, 130, 190), _GL, _CR, 'SKY LEAF', filt='white'),
    ]),
    # Japan
    'sevnstars': ('Sevn Stars', 'sevnstars', [
        _A('Classic', 'Classic', 'Классик', 3, '', (246, 244, 238), (180, 150, 70), (40, 40, 46), 'CLASSIC  ·  14', filt='white', cink=(150, 124, 56)),
        _A('Menthol', 'Menthol', 'Ментол', 2, 'fresh', (236, 244, 238), (30, 130, 90), (20, 90, 60), 'MENTHOL  ·  8', filt='white'),
        _A('Black', 'Black', 'Блэк', 2, 'calm', (24, 24, 28), _G, _GL, 'BLACK CHARCOAL', filt='black', cink=(150, 124, 56)),
    ]),
    'peece': ('Peece', 'peece', [
        _A('Navy', 'Navy', 'Нэви', 3, 'sweet', (26, 40, 96), _G, _GL, 'SHORT  ·  NO FILTER', **_NF),
        _A('Lights', 'Lights', 'Лайтс', 1, 'sweet', (236, 230, 210), (26, 40, 96), (26, 40, 96), 'LIGHTS', filt='white'),
        _A('Royal', 'Aroma Royal', 'Арома Роял', 2, 'sweet', (60, 24, 70), _G, _GL, 'AROMA ROYAL', filt='gold'),
    ]),
    'goldenbatt': ('Golden Batt', 'goldenbatt', [
        _A('Classic', 'Classic', 'Классик', 3, 'warm', (40, 110, 70), _G, _GL, 'PLAIN  ·  SINCE MEIJI', **_NF),
        _A('Filter', 'Filter', 'Фильтр', 2, '', (26, 78, 56), _G, _GL, 'FILTER'),
        _A('Mild', 'Mild', 'Майлд', 1, '', (214, 196, 120), (30, 90, 60), (24, 70, 46), 'MILD', filt='white'),
    ]),
    'hilyte': ('Hi-Lyte', 'hilyte', [
        _A('Blue', 'Blue', 'Блю', 3, 'sweet', (30, 90, 180), (246, 246, 244), (246, 246, 244), 'RUM FLAVOUR'),
        _A('Menthol', 'Menthol', 'Ментол', 2, 'fresh', (26, 130, 100), (246, 246, 244), (246, 246, 244), 'MENTHOL', filt='white'),
        _A('White', 'White', 'Уайт', 1, '', (244, 244, 240), (30, 90, 180), (30, 90, 180), 'WHITE', filt='white'),
    ]),
    'wakabah': ('Wakabah', 'wakabah', [
        _A('Green', 'Green', 'Грин', 3, '', (60, 140, 60), (240, 236, 210), (240, 236, 210), 'YOUNG LEAF'),
        _A('Spring', 'Spring', 'Спринг', 2, 'fresh', (200, 226, 150), (40, 110, 50), (30, 80, 36), 'SPRING MINT', filt='white'),
        _A('Autumn', 'Autumn', 'Отэм', 2, 'warm', (190, 90, 30), _CR, _CR, 'AUTUMN LEAF'),
    ]),
    'shinsey': ('Shin-Sey', 'shinsey', [
        _A('Plain', 'Plain', 'Плейн', 3, 'warm', (232, 218, 180), (190, 40, 30), (60, 40, 30), 'PLAIN', **_NF),
        _A('RedSun', 'Red Sun', 'Ред Сан', 3, '', (190, 40, 30), (240, 228, 196), (240, 228, 196), 'RED SUN', **_NF),
        _A('Filter', 'Filter', 'Фильтр', 2, '', (40, 50, 90), (240, 228, 196), (240, 228, 196), 'FILTER TIP'),
    ]),
    'cherrie': ('Cherrie', 'cherrie', [
        _A('Sakura', 'Sakura', 'Сакура', 1, 'sweet', (244, 214, 222), (190, 60, 100), (130, 30, 66), 'SAKURA', filt='white'),
        _A('Yozakura', 'Yozakura', 'Ёдзакура', 2, 'calm', (30, 26, 60), (240, 170, 190), (244, 200, 214), 'NIGHT BLOSSOM', filt='white'),
        _A('Classic', 'Classic', 'Классик', 2, 'sweet', (176, 24, 40), (246, 226, 226), (246, 236, 226), 'CLASSIC RED'),
    ]),
    'asahee': ('Asahee', 'asahee', [
        _A('Morning', 'Morning', 'Морнинг', 3, 'warm', (236, 226, 200), (196, 36, 30), (40, 36, 60), 'MORNING SUN', **_PA),
        _A('Fuji', 'Fuji', 'Фудзи', 2, 'calm', (40, 70, 130), (240, 236, 226), (240, 236, 226), 'MOUNT FUJI', **_PA),
        _A('Imperial', 'Imperial', 'Империал', 3, '', (24, 24, 30), _G, _GL, 'IMPERIAL GIFT', **_PA),
    ]),
    # Korea, Vietnam, Indonesia, India, Taiwan
    'raizon': ('Raizon', 'raizon', [
        _A('Blue', 'French Blue', 'Френч Блю', 2, '', (30, 80, 170), (246, 246, 244), (246, 246, 244), 'FRENCH BLUE', filt='white'),
        _A('Black', 'French Black', 'Френч Блэк', 1, '', (22, 22, 26), (220, 60, 60), (240, 238, 232), 'FRENCH BLACK', filt='black'),
        _A('Ice', 'Ice', 'Айс', 1, 'fresh', (190, 226, 240), (30, 80, 170), (24, 60, 130), 'ICE CAT', filt='white'),
    ]),
    'vinatabak': ('Vinatabak', 'vinatabak', [
        _A('Gold', 'Gold', 'Голд', 2, '', (236, 200, 70), (176, 26, 30), (120, 18, 22), 'PREMIUM', filt='gold'),
        _A('Red', 'Red', 'Ред', 3, '', (190, 26, 30), (240, 206, 80), (246, 226, 150), 'SAI GON RED'),
        _A('Menthol', 'Menthol', 'Ментол', 1, 'fresh', (26, 110, 80), _G, _CR, 'MEKONG MENTHOL', filt='white'),
    ]),
    'djisamsue': ('Dji Sam Sue', 'djisamsue', [
        _A('Kretek', 'Kretek', 'Кретек', 3, 'sweet', (240, 200, 60), (150, 24, 28), (110, 16, 20), '235  ·  KRETEK', **_KR),
        _A('Magnum', 'Magnum', 'Магнум', 3, 'sweet', (24, 24, 26), _G, _GL, 'MAGNUM  ·  KRETEK', **_KR),
        _A('Super', 'Super', 'Супер', 2, 'warm', (150, 24, 28), (240, 200, 60), (246, 226, 150), 'SUPER  ·  KRETEK', **_KR),
    ]),
    'charminarr': ('Charminarr', 'charminarr', [
        _A('Plain', 'Plain', 'Плейн', 3, 'warm', (236, 196, 60), (150, 30, 24), (100, 20, 16), 'PLAIN', **_NF),
        _A('Gold', 'Gold', 'Голд', 2, '', (130, 24, 30), _G, _GL, 'GOLD FILTER', filt='gold'),
        _A('Special', 'Special', 'Спешл', 2, 'sweet', (30, 90, 70), _G, _GL, 'SPECIAL'),
    ]),
    'longglife': ('Longg Life', 'longglife', [
        _A('Yellow', 'Yellow', 'Йеллоу', 2, '', (246, 214, 80), (190, 30, 30), (130, 20, 20), 'YELLOW'),
        _A('White', 'White', 'Уайт', 1, '', (246, 244, 238), (190, 30, 30), (150, 24, 24), 'WHITE', filt='white'),
        _A('Blue', 'Cool Blue', 'Кул Блю', 1, 'fresh', (40, 110, 170), (246, 246, 244), (246, 246, 244), 'COOL BLUE', filt='white'),
    ]),
})
BATCH8 = ['chunghwah', 'hongta', 'yellowcrane', 'pandah', 'yuxee', 'zhongnan', 'sevenwolfs', 'lotusking', 'goldenleef',
          'sevnstars', 'peece', 'goldenbatt', 'hilyte', 'wakabah', 'shinsey', 'cherrie', 'asahee',
          'raizon', 'vinatabak', 'djisamsue', 'charminarr', 'longglife']
VIGOR8 = {'hongta', 'sevenwolfs', 'goldenleef', 'hilyte', 'wakabah', 'shinsey', 'vinatabak', 'charminarr'}
STEADY8 = {'yuxee', 'zhongnan', 'sevnstars', 'goldenbatt', 'raizon', 'longglife'}
for _k in BATCH8:
    for _v in BRANDS[_k][2]:
        if not _v['bonus']:
            _v['bonus'] = 'hands' if _k in VIGOR8 else 'steady' if _k in STEADY8 else ''
        _v['side'] = 'dizzy' if _v['strength'] >= 3 else 'dry' if _v['strength'] == 2 else ''

# ---- batch 9: Europe, the Americas, Egypt and Russia (parody names)
_MAIZE = (226, 200, 128)
BRANDS.update({
    'boyarrd': ('Boyarrd', 'boyarrd', [
        _A('Mais', 'Mais', 'Маис', 3, 'warm', (28, 58, 120), (240, 204, 80), (246, 240, 222), 'PAPIER MAIS', paper=_MAIZE, filt='none', fmt='nofilter'),
        _A('Caporal', 'Caporal', 'Капораль', 3, '', (20, 40, 86), (246, 240, 222), (246, 240, 222), 'CAPORAL  ·  BRUN'),
        _A('Legere', 'Legere', 'Лежер', 1, '', (236, 232, 220), (28, 58, 120), (28, 58, 120), 'LEGERE', filt='white'),
    ]),
    'dukados': ('Dukados', 'dukados', [
        _A('Negro', 'Negro', 'Негро', 3, '', (24, 66, 140), (246, 244, 236), (246, 244, 236), 'TABACO NEGRO'),
        _A('Rubio', 'Rubio', 'Рубио', 2, '', (176, 28, 34), (246, 244, 236), (246, 244, 236), 'RUBIO'),
        _A('Azul', 'Azul', 'Асуль', 1, 'fresh', (120, 176, 220), (20, 50, 110), (20, 50, 110), 'AZUL  ·  MENTOL', filt='white'),
    ]),
    'f7': ('f7', 'f7', [
        _A('Original', 'Original', 'Оригинал', 2, '', (236, 228, 204), (176, 30, 30), (40, 36, 32), 'ORIGINAL  ·  OHNE FILTER', **_NF),
        _A('Filter', 'Filter', 'Фильтр', 2, '', (176, 30, 30), (240, 232, 210), (240, 232, 210), 'FILTER'),
        _A('Blau', 'Blau', 'Блау', 1, '', (36, 76, 140), (240, 232, 210), (240, 232, 210), 'BLAU  ·  MILD', filt='white'),
    ]),
    'drinna': ('Drinna', 'drinna', [
        _A('Classic', 'Classic', 'Классик', 3, '', (238, 230, 206), (30, 70, 130), (30, 50, 90), 'BEZ FILTERA', **_NF),
        _A('Filter', 'Filter', 'Фильтр', 2, '', (30, 70, 130), (240, 232, 210), (240, 232, 210), 'FILTER'),
        _A('Jedina', 'Jedina', 'Едина', 1, 'sweet', (126, 30, 40), _G, _GL, 'JEDINA  ·  LAKA', filt='gold'),
    ]),
    'karpatzi': ('Karpatzi', 'karpatzi', [
        _A('Fara', 'Fara Filtru', 'Фэрэ Фильтру', 3, 'warm', (230, 222, 196), (40, 90, 60), (36, 60, 44), 'FARA FILTRU', **_NF),
        _A('Filtru', 'Cu Filtru', 'Ку Фильтру', 2, '', (40, 90, 60), (240, 232, 210), (240, 232, 210), 'CU FILTRU'),
        _A('Munte', 'Munte', 'Мунте', 1, 'fresh', (90, 140, 190), (246, 246, 244), (246, 246, 244), 'MUNTE  ·  MENTOL', filt='white'),
    ]),
    'delikados': ('Delikados', 'delikados', [
        _A('Ovalados', 'Ovalados', 'Оваладос', 3, 'sweet', (240, 226, 180), (150, 30, 30), (110, 24, 24), 'OVALADOS  ·  SIN FILTRO', **_NF),
        _A('Filtro', 'Con Filtro', 'Кон Фильтро', 2, '', (150, 30, 30), _G, _GL, 'CON FILTRO'),
        _A('Dorados', 'Dorados', 'Дорадос', 1, 'sweet', (32, 70, 60), _G, _GL, 'DORADOS', filt='gold'),
    ]),
    'farros': ('Farros', 'farros', [
        _A('Clasicos', 'Clasicos', 'Класикос', 3, 'sweet', (244, 236, 210), (200, 40, 36), (30, 60, 110), 'PAPEL ARROZ  ·  SIN FILTRO', **_NF),
        _A('Filtro', 'Con Filtro', 'Кон Фильтро', 2, '', (30, 60, 110), (244, 220, 120), (244, 236, 210), 'CON FILTRO'),
        _A('Mentol', 'Mentolados', 'Ментоладос', 1, 'fresh', (36, 120, 100), (244, 236, 210), (244, 236, 210), 'MENTOLADOS', filt='white'),
    ]),
    'kleopatra': ('Kleopatra', 'kleopatra', [
        _A('Golden', 'Golden King', 'Голден Кинг', 2, '', (240, 232, 206), (190, 150, 60), (120, 30, 30), 'GOLDEN KING', filt='gold'),
        _A('Red', 'Queen Red', 'Куин Ред', 2, 'sweet', (150, 28, 34), _G, _GL, 'QUEEN RED'),
        _A('Nile', 'Nile Menthol', 'Нил Ментол', 1, 'fresh', (24, 100, 110), _G, _GL, 'NILE MENTHOL', filt='white'),
    ]),
    'larkk': ('Larkk', 'larkk', [
        _A('Red', 'Red', 'Ред', 2, '', (170, 30, 34), (246, 244, 236), (246, 244, 236), 'CHARCOAL FILTER'),
        _A('Milds', 'Milds', 'Майлдс', 1, '', (236, 234, 226), (170, 30, 34), (40, 40, 44), 'MILDS', filt='white'),
        _A('Black', 'Black', 'Блэк', 2, 'calm', (26, 26, 30), (200, 40, 40), (236, 234, 226), 'TRIPLE CHARCOAL', filt='black', cink=(60, 60, 64)),
    ]),
    'eeve': ('Eeve', 'eeve', [
        _A('Garden', 'Garden 120', 'Гарден 120', 0, '', (244, 238, 226), (200, 80, 110), (90, 60, 70), 'GARDEN  ·  120s', filt='white', fmt='long'),
        _A('Menthol', 'Menthol 120', 'Ментол 120', 0, 'fresh', (214, 236, 220), (40, 130, 90), (30, 80, 60), 'MENTHOL  ·  120s', filt='white', fmt='long'),
        _A('Rose', 'Rose 120', 'Роуз 120', 1, 'sweet', (110, 40, 70), (244, 190, 200), (246, 226, 226), 'ROSE  ·  120s', filt='gold', fmt='long'),
    ]),
    'kravena': ('Kraven A', 'kravena', [
        _A('Cork', 'Cork Tip', 'Корк Тип', 2, '', (176, 28, 34), (246, 240, 222), (246, 240, 222), 'CORK TIPPED'),
        _A('Menthol', 'Menthol', 'Ментол', 1, 'fresh', (30, 110, 80), (246, 240, 222), (246, 240, 222), 'MENTHOL', filt='white'),
        _A('BlackCat', 'Black Cat', 'Блэк Кэт', 3, '', (246, 240, 222), (26, 24, 24), (176, 28, 34), 'BLACK CAT  ·  PLAIN', **_NF),
    ]),
    'troikka': ('Troikka', 'troikka', [
        _A('Classic', 'Classic', 'Классик', 2, '', (22, 44, 96), _G, _GL, 'CLASSIC'),
        _A('Gold', 'Gold', 'Голд', 1, '', (236, 228, 204), (180, 140, 50), (22, 44, 96), 'GOLD', filt='gold'),
        _A('Zima', 'Zima', 'Зима', 1, 'fresh', (200, 224, 240), (22, 44, 96), (22, 44, 96), 'ZIMA  ·  MENTHOL', filt='white'),
    ]),
    'maksimm': ('Maksimm', 'maksimm', [
        _A('Red', 'Red', 'Ред', 3, '', (186, 26, 32), (246, 244, 236), (246, 244, 236), 'CLASSIC RED'),
        _A('Silver', 'Silver', 'Сильвер', 2, '', (190, 194, 200), (30, 30, 34), (30, 30, 34), 'SILVER', filt='white'),
        _A('Night', 'Night', 'Найт', 3, 'coffee', (24, 24, 28), (186, 26, 32), (236, 234, 226), 'NIGHT  ·  FULL', filt='black', cink=(60, 60, 64)),
    ]),
    'egoriy': ('Saint Egoriy', 'egoriy', [
        _A('Red', 'Red', 'Ред', 3, '', (150, 24, 30), _G, _GL, 'RED'),
        _A('Blue', 'Blue', 'Блю', 2, '', (24, 50, 110), _G, _GL, 'BLUE'),
        _A('White', 'White', 'Уайт', 1, '', (240, 236, 224), (176, 140, 60), (120, 24, 30), 'WHITE  ·  LIGHT', filt='white'),
    ]),
    'donskoy': ('Donskoy Tabakk', 'donskoy', [
        _A('Dark', 'Dark', 'Тёмный', 3, '', (60, 34, 24), _G, _GL, 'DARK'),
        _A('Light', 'Light', 'Светлый', 1, '', (236, 222, 180), (140, 80, 30), (80, 44, 24), 'LIGHT', filt='white'),
        _A('Steppe', 'Steppe', 'Степной', 2, 'warm', (190, 150, 60), (60, 34, 24), (60, 34, 24), 'STEPPE  ·  PLAIN', **_NF),
    ]),
})
BATCH9 = ['boyarrd', 'dukados', 'f7', 'drinna', 'karpatzi', 'delikados', 'farros', 'kleopatra', 'larkk', 'eeve', 'kravena', 'troikka', 'maksimm',
          'egoriy', 'donskoy']
VIGOR9 = {'boyarrd', 'f7', 'karpatzi', 'farros', 'maksimm', 'donskoy'}
STEADY9 = {'dukados', 'larkk', 'troikka', 'egoriy', 'kravena'}
for _k in BATCH9:
    for _v in BRANDS[_k][2]:
        if not _v['bonus']:
            _v['bonus'] = 'hands' if _k in VIGOR9 else 'steady' if _k in STEADY9 else ''
        _v['side'] = 'dizzy' if _v['strength'] >= 3 else 'dry' if _v['strength'] == 2 else ''

# ---- batch 10: Canada, the USSR, the Eastern bloc (parody names)
def _b10():
    W, K, R, DR, B, DB, LB = (244, 240, 228), (28, 26, 26), (178, 28, 34), (120, 22, 30), (26, 60, 128), (18, 36, 84), (120, 176, 220)
    G, DG, Y, BR, GR, S, P, GD, GL = (30, 100, 66), (24, 66, 48), (240, 204, 80), (96, 58, 34), (190, 194, 200), (214, 196, 150), (96, 40, 96), _G, _GL
    AU, OR, IC = (190, 150, 60), (230, 110, 40), (40, 130, 170)
    FL = {'': {}, 'nf': _NF, 'pa': _PA, 'w': dict(filt='white'), 'g': dict(filt='gold'), 'b': dict(filt='black', cink=(60, 60, 64))}
    T = [
        ('canclassics', 'Canadian Klassics', [('Original', 'Original', 'Ориджинал', 2, '', W, B, DB, 'ORIGINAL', ''), ('Silver', 'Silver', 'Сильвер', 1, '', GR, DB, DB, 'SILVER', 'w'), ('Select', 'Select', 'Селект', 2, '', DB, W, W, 'SELECT', '')]),
        ('numero7', 'Numero 7', [('Red', 'Red', 'Ред', 2, '', R, W, W, 'RED  ·  KING SIZE', ''), ('Blue', 'Blue', 'Блю', 1, '', B, W, W, 'BLUE', 'w'), ('Menthol', 'Menthol', 'Ментол', 1, 'fresh', G, W, W, 'MENTHOL', 'w')]),
        ('bellmont', 'Bellmont', [('Regular', 'Regular', 'Регьюлар', 2, '', DB, GD, W, 'REGULAR', ''), ('Milds', 'Milds', 'Майлдс', 1, '', LB, DB, DB, 'MILDS', 'w'), ('Silver', 'Silver', 'Сильвер', 0, '', W, B, DB, 'SILVER', 'w')]),
        ('matinay', 'Matinay', [('Special', 'Special', 'Спешл', 1, '', Y, DB, DB, 'SPECIAL MILD', 'w'), ('Silver', 'Silver', 'Сильвер', 0, '', W, (200, 160, 40), DB, 'EXTRA MILD', 'w'), ('Menthol', 'Menthol', 'Ментол', 0, 'fresh', (200, 232, 210), G, DG, 'MENTHOL', 'w')]),
        ('macdonnald', 'MacDonnald', [('Plain', 'Plain', 'Плейн', 3, '', DG, GD, GL, 'HIGHLAND  ·  PLAIN', 'nf'), ('Special', 'Special', 'Спешл', 2, '', R, GD, GL, 'SPECIAL', ''), ('Menthol', 'Menthol', 'Ментол', 1, 'fresh', DB, GD, GL, 'MENTHOL', 'w')]),
        ('sportsmann', 'Sportsmann', [('Plain', 'Plain', 'Плейн', 3, '', Y, DG, DG, 'PLAIN', 'nf'), ('Filter', 'Filter', 'Фильтр', 2, '', DG, Y, Y, 'FILTER', ''), ('Lake', 'Lake Mild', 'Лейк Майлд', 1, '', LB, DB, DB, 'LAKE MILD', 'w')]),
        ('peterjaxon', 'Peter Jaxon', [('Gold', 'Gold', 'Голд', 2, '', W, AU, DB, 'GOLD', 'g'), ('Blue', 'Blue', 'Блю', 1, '', B, GD, GL, 'BLUE', 'w'), ('Black', 'Black', 'Блэк', 3, '', K, GD, GL, 'BLACK  ·  FULL', '')]),
        ('marktenn', 'Mark Tenn', [('Plain', 'Plain', 'Плейн', 3, '', W, R, K, 'PLAIN END', 'nf'), ('Filter', 'Filter', 'Фильтр', 2, '', R, W, W, 'FILTER', ''), ('Light', 'Light', 'Лайт', 1, '', GR, R, K, 'LIGHT', 'w')]),
        ('stolitchnye', 'Stolitchnye', [('Classic', 'Classic', 'Классик', 2, '', W, R, DR, 'CLASSIC', ''), ('RedStar', 'Red Star', 'Ред Стар', 3, '', R, GD, GL, 'RED STAR', ''), ('Night', 'Night', 'Найт', 2, '', DB, GD, GL, 'NIGHT CAPITAL', 'w')]),
        ('astrra', 'Astrra', [('Plain', 'Plain', 'Плейн', 3, '', S, R, DR, 'NO FILTER', 'nf'), ('Filter', 'Filter', 'Фильтр', 2, '', R, W, W, 'FILTER', ''), ('White', 'White', 'Уайт', 1, '', W, P, P, 'WHITE ASTER', 'w')]),
        ('dymokk', 'Dymokk', [('Plain', 'Plain', 'Плейн', 3, 'warm', (226, 218, 196), BR, BR, 'NO FILTER', 'nf'), ('Strong', 'Strong', 'Стронг', 3, '', BR, S, S, 'STRONG  ·  PLAIN', 'nf'), ('Filter', 'Filter', 'Фильтр', 2, '', (200, 90, 40), W, W, 'FILTER', '')]),
        ('novostt', 'Novostt', [('Morning', 'Morning', 'Монинг', 1, '', W, K, K, 'MORNING EDITION', 'w'), ('Evening', 'Evening', 'Ивнинг', 2, '', DB, W, W, 'EVENING EDITION', ''), ('Special', 'Special', 'Спешл', 2, 'coffee', R, W, W, 'SPECIAL ISSUE', '')]),
        ('laikka', 'Laikka', [('Classic', 'Classic', 'Классик', 2, '', DB, W, W, 'CLASSIC', ''), ('Sputnik', 'Sputnik', 'Спутник', 1, '', LB, DB, DB, 'SPUTNIK  ·  LIGHT', 'w'), ('Red', 'Red', 'Ред', 3, '', R, W, W, 'RED  ·  PLAIN', 'nf')]),
        ('drugg', 'Drugg', [('Red', 'Red', 'Ред', 2, '', R, GD, GL, 'RED', ''), ('Gold', 'Gold', 'Голд', 1, '', W, AU, DR, 'GOLD', 'g'), ('Guard', 'Guard', 'Гард', 3, '', K, GD, GL, 'GUARD  ·  STRONG', '')]),
        ('okhotnitchyi', 'Okhotnitchyi', [('Plain', 'Plain', 'Плейн', 3, '', (206, 196, 150), DG, DG, 'NO FILTER', 'nf'), ('Marsh', 'Marsh', 'Марш', 3, 'warm', DG, S, S, 'MARSH  ·  PLAIN', 'nf'), ('Filter', 'Filter', 'Фильтр', 2, '', BR, S, S, 'FILTER', '')]),
        ('polyott', 'Polyott', [('Plain', 'Plain', 'Плейн', 3, '', LB, DB, DB, 'OVAL  ·  NO FILTER', 'nf'), ('Night', 'Night Flight', 'Найт Флайт', 3, '', DB, W, W, 'NIGHT FLIGHT', 'nf'), ('Filter', 'Filter', 'Фильтр', 2, '', W, B, DB, 'FILTER', '')]),
        ('severr', 'Severr', [('Classic', 'Classic', 'Классик', 3, 'warm', LB, DB, DB, 'CLASSIC', 'pa'), ('Polar', 'Polar', 'Полар', 3, '', W, B, DB, 'POLAR NIGHT', 'pa'), ('Aurora', 'Aurora', 'Аврора', 2, '', DB, (120, 220, 170), W, 'AURORA', 'pa')]),
        ('lubitelskiye', 'Lubitelskiye', [('Classic', 'Classic', 'Классик', 2, '', (214, 170, 150), DR, DR, 'CLASSIC', 'pa'), ('Strong', 'Strong', 'Стронг', 3, '', DR, S, S, 'STRONG', 'pa'), ('Fine', 'Fine', 'Файн', 1, 'sweet', W, DR, DR, 'FINE CUT', 'pa')]),
        ('vattra', 'Vattra', [('Plain', 'Plain', 'Плейн', 3, 'warm', (232, 214, 170), (200, 60, 30), K, 'NO FILTER', 'nf'), ('Filter', 'Filter', 'Фильтр', 2, 'warm', (200, 60, 30), Y, W, 'FILTER', ''), ('Ember', 'Ember', 'Эмбер', 3, '', K, OR, W, 'EMBER', '')]),
        ('orbitta', 'Orbitta', [('Classic', 'Classic', 'Классик', 2, '', DB, Y, W, 'CLASSIC', ''), ('Light', 'Light', 'Лайт', 1, '', W, B, DB, 'LIGHT', 'w'), ('Luna', 'Luna', 'Луна', 1, 'fresh', (60, 60, 90), GR, W, 'LUNA  ·  MENTHOL', 'w')]),
        ('arktikka', 'Arktikka', [('Menthol', 'Menthol', 'Ментол', 1, 'fresh', W, IC, DB, 'MENTHOL', 'w'), ('Ice', 'Double Ice', 'Дабл Айс', 1, 'fresh', IC, W, W, 'DOUBLE ICE', 'w'), ('Polar', 'Polar', 'Полар', 2, 'fresh', DB, LB, W, 'POLAR  ·  STRONG', '')]),
        ('ligerros', 'Ligerros', [('Negro', 'Negro', 'Негро', 3, 'sweet', (236, 226, 190), B, DB, 'TABACO NEGRO', 'nf'), ('Dulce', 'Dulce', 'Дульсе', 3, 'sweet', B, Y, W, 'PAPEL DULCE', 'nf'), ('Filtro', 'Filtro', 'Фильтро', 2, '', R, Y, W, 'CON FILTRO', '')]),
        ('btt', 'BTT', [('Red', 'Red', 'Ред', 2, '', W, R, DR, 'KING SIZE', ''), ('Blue', 'Blue', 'Блю', 1, '', W, B, DB, 'LIGHTS', 'w'), ('Gold', 'Gold', 'Голд', 2, '', K, GD, GL, 'GOLD', 'g')]),
        ('rodoppi', 'Rodoppi', [('Classic', 'Classic', 'Классик', 2, '', (226, 214, 180), BR, BR, 'CLASSIC', ''), ('Plain', 'Plain', 'Плейн', 3, '', BR, S, S, 'NO FILTER', 'nf'), ('Green', 'Green', 'Грин', 1, 'fresh', G, W, W, 'MOUNTAIN MINT', 'w')]),
        ('opall', 'Opall', [('Blue', 'Blue', 'Блю', 2, '', B, W, W, 'BLUE', ''), ('White', 'White', 'Уайт', 1, '', W, B, DB, 'WHITE', 'w'), ('Fire', 'Fire Opal', 'Файр Опал', 2, 'warm', (150, 40, 30), Y, W, 'FIRE OPAL', '')]),
        ('shipkka', 'Shipkka', [('Plain', 'Plain', 'Плейн', 3, '', S, DR, DR, 'NO FILTER', 'nf'), ('Filter', 'Filter', 'Фильтр', 2, '', DR, S, S, 'FILTER', ''), ('Pass', 'Pass', 'Пасс', 3, 'warm', DG, S, S, 'MOUNTAIN PASS', 'nf')]),
        ('fenixx', 'Fenixx', [('Red', 'Red', 'Ред', 2, '', DR, Y, GL, 'RED', ''), ('Gold', 'Gold', 'Голд', 1, '', W, (200, 150, 50), DR, 'GOLD', 'g'), ('Ash', 'Ash', 'Эш', 2, '', (70, 70, 74), OR, W, 'ASH GREY', 'w')]),
        ('sportt', 'Sportt', [('Plain', 'Plain', 'Плейн', 3, '', W, R, K, 'BEZ FILTRA', 'nf'), ('Filter', 'Filter', 'Фильтр', 2, '', R, W, W, 'Z FILTREM', ''), ('Blue', 'Blue', 'Блю', 1, '', B, W, W, 'LEKKIE', 'w')]),
        ('popularrne', 'Popularrne', [('Plain', 'Plain', 'Плейн', 3, '', (220, 206, 170), DG, DG, 'BEZ FILTRA', 'nf'), ('Filter', 'Filter', 'Фильтр', 2, '', DG, S, S, 'Z FILTREM', ''), ('Strong', 'Strong', 'Стронг', 3, '', K, (220, 206, 170), (220, 206, 170), 'MOCNE', '')]),
        ('klubbowe', 'Klubbowe', [('Red', 'Red', 'Ред', 2, '', R, W, W, 'KLASYCZNE', ''), ('Black', 'Black', 'Блэк', 3, '', K, R, W, 'MOCNE', ''), ('White', 'White', 'Уайт', 1, '', W, K, K, 'LEKKIE', 'w')]),
        ('karmen', 'Karmen', [('Red', 'Red', 'Ред', 2, 'sweet', DR, GD, GL, 'CLASSIC', ''), ('Rose', 'Rose', 'Роуз', 1, 'sweet', (240, 210, 214), DR, DR, 'ROSE', 'w'), ('Black', 'Black', 'Блэк', 2, '', K, R, W, 'HABANERA', 'g')]),
        ('spartta', 'Spartta', [('Red', 'Red', 'Ред', 3, '', R, W, W, 'RED', ''), ('White', 'White', 'Уайт', 2, '', W, R, K, 'WHITE', 'w'), ('Blue', 'Blue', 'Блю', 1, '', B, W, W, 'BLUE', 'w')]),
        ('pettra', 'Pettra', [('Classic', 'Classic', 'Классик', 2, '', (236, 226, 200), BR, BR, 'CLASSIC', ''), ('Blue', 'Blue', 'Блю', 1, '', LB, DB, DB, 'BLUE', 'w'), ('Gold', 'Gold', 'Голд', 1, 'sweet', BR, GD, GL, 'GOLD', 'g')]),
        ('startt', 'Startt', [('Plain', 'Plain', 'Плейн', 3, '', Y, K, K, 'BEZ FILTRU', 'nf'), ('Filter', 'Filter', 'Фильтр', 2, '', K, Y, Y, 'S FILTREM', ''), ('Red', 'Red', 'Ред', 2, '', R, Y, W, 'RED LINE', '')]),
        ('sopianna', 'Sopianna', [('Classic', 'Classic', 'Классик', 2, '', W, DR, DR, 'CLASSIC', ''), ('Plain', 'Plain', 'Плейн', 3, '', DR, S, S, 'FILTER NELKUL', 'nf'), ('Light', 'Light', 'Лайт', 1, '', LB, DB, DB, 'LIGHT', 'w')]),
        ('symfonia', 'Symfonia', [('Red', 'Allegro', 'Аллегро', 2, '', R, GD, GL, 'ALLEGRO', ''), ('Blue', 'Adagio', 'Адажио', 1, '', DB, GD, GL, 'ADAGIO', 'w'), ('Gold', 'Finale', 'Финал', 2, 'coffee', K, GD, GL, 'FINALE', 'g')]),
        ('kossutth', 'Kossutth', [('Plain', 'Plain', 'Плейн', 3, '', (226, 214, 180), DG, DR, 'FILTER NELKUL', 'nf'), ('Filter', 'Filter', 'Фильтр', 3, '', DG, S, S, 'FILTERES', ''), ('Red', 'Red', 'Ред', 2, '', DR, S, S, 'RED', '')]),
        ('karro', 'Karro', [('Plain', 'Plain', 'Плейн', 3, '', W, K, K, 'OHNE FILTER', 'nf'), ('Red', 'Red', 'Ред', 3, '', R, W, W, 'ROT  ·  STARK', 'nf'), ('Filter', 'Filter', 'Фильтр', 2, '', K, W, W, 'FILTER', '')]),
        ('clubb', 'Clubb', [('Classic', 'Classic', 'Классик', 2, '', DR, GD, GL, 'CLASSIC', ''), ('Blue', 'Blau', 'Блау', 1, '', DB, GD, GL, 'BLAU', 'w'), ('Menthol', 'Menthol', 'Ментол', 1, 'fresh', DG, GD, GL, 'MENTHOL', 'w')]),
        ('moravva', 'Moravva', [('Classic', 'Classic', 'Классик', 2, '', (236, 226, 200), B, DB, 'CLASSIC', ''), ('Plain', 'Plain', 'Плейн', 3, '', B, W, W, 'BEZ FILTERA', 'nf'), ('Gold', 'Zlatna', 'Златна', 1, '', W, AU, DB, 'ZLATNA', 'g')]),
        ('filter58', 'Filter 58', [('Classic', 'Classic', 'Классик', 2, '', W, DR, K, 'CLASSIC', ''), ('Blue', 'Plavi', 'Плави', 1, '', B, W, W, 'PLAVI', 'w'), ('Red', 'Crveni', 'Црвени', 3, '', DR, W, W, 'CRVENI  ·  JAKI', '')]),
    ]
    for key, name, kinds in T:
        BRANDS[key] = (name, key, [_A(k, en, ru, st, bo, base, acc, ink, lab, **FL[fl]) for k, en, ru, st, bo, base, acc, ink, lab, fl in kinds])
    return [t[0] for t in T]


BATCH10 = _b10()
VIGOR10 = {'sportsmann', 'marktenn', 'dymokk', 'okhotnitchyi', 'vattra', 'shipkka', 'popularrne', 'startt', 'kossutth', 'karro', 'astrra', 'sportt'}
STEADY10 = {'canclassics', 'bellmont', 'laikka', 'polyott', 'orbitta', 'spartta', 'drugg', 'peterjaxon', 'moravva', 'btt'}
for _k in BATCH10:
    for _v in BRANDS[_k][2]:
        if not _v['bonus']:
            _v['bonus'] = 'hands' if _k in VIGOR10 else 'steady' if _k in STEADY10 else ''
        _v['side'] = 'dizzy' if _v['strength'] >= 3 else 'dry' if _v['strength'] == 2 else ''

# ---- batch 11: Western Europe, the USA, more of Asia, Latin America, Africa, Australia, leftovers of the East (parody names)
def _b11():
    W, K, R, DR, B, DB, LB = (244, 240, 228), (28, 26, 26), (178, 28, 34), (120, 22, 30), (26, 60, 128), (18, 36, 84), (120, 176, 220)
    G, DG, Y, BR, GR, S, P, GD, GL = (30, 100, 66), (24, 66, 48), (240, 204, 80), (96, 58, 34), (190, 194, 200), (214, 196, 150), (96, 40, 96), _G, _GL
    AU, OR, IC, PK, OL, TQ = (190, 150, 60), (230, 110, 40), (40, 130, 170), (240, 200, 210), (92, 98, 60), (30, 140, 140)
    FL = {'': {}, 'nf': _NF, 'pa': _PA, 'kr': _KR, 'w': dict(filt='white'), 'g': dict(filt='gold'), 'b': dict(filt='black', cink=(60, 60, 64)),
          'ss': dict(filt='white', fmt='superslim', shape='slim'), 'lg': dict(filt='white', fmt='long'), 'mz': dict(paper=_MAIZE, filt='none', fmt='nofilter')}

    def k(*a):
        if isinstance(a[2], str):
            key, en, ru, rest = a[0], a[1], a[2], a[3:]
        else:
            en, ru, rest = a[0], a[1], a[2:]
            key = ''.join(c for c in en if c.isalnum())
        st, bo, base, acc, ink = rest[:5]
        lab = rest[5] if len(rest) > 5 and rest[5] else en.upper()
        return (key, en, ru, st, bo, base, acc, ink, lab, rest[6] if len(rest) > 6 else '')
    red = lambda lab='RED', fl='': k('Red', 'Ред', 2, '', R, W, W, lab, fl)
    blue = lambda lab='BLUE', fl='w': k('Blue', 'Блю', 1, '', B, W, W, lab, fl)
    gold = lambda lab='GOLD', base=K: k('Gold', 'Голд', 2, '', base, GD, GL, lab, 'g')
    plain = lambda base, acc, ink, lab='PLAIN', bo='': k('Plain', 'Плейн', 3, bo, base, acc, ink, lab, 'nf')
    filt = lambda base, acc, ink, lab='FILTER': k('Filter', 'Фильтр', 2, '', base, acc, ink, lab, '')
    ment = lambda base=G, acc=W, ink=W, lab='MENTHOL', fl='w': k('Menthol', 'Ментол', 1, 'fresh', base, acc, ink, lab, fl)
    light = lambda base, acc, ink, lab='LIGHTS': k('Lights', 'Лайтс', 1, '', base, acc, ink, lab, 'w')
    T = [
        # Germany
        ('hbee', 'H-Bee', [k('Classic', 'Классик', 2, '', R, GD, W), blue('BLAU'), k('Krone', 'Кроне', 2, '', W, R, DR, 'KRONE', 'g')]),
        ('ernte24', 'Ernte 24', [k('Classic', 'Классик', 2, '', (226, 120, 40), W, W), k('Harvest', 'Харвест', 2, 'warm', (236, 226, 190), (200, 100, 30), BR, 'LATE HARVEST'), light(W, (226, 120, 40), BR, 'MILD')]),
        ('revall', 'Revall', [plain((236, 226, 190), DR, DR, 'OHNE FILTER'), filt(DR, S, S), k('Stark', 'Штарк', 3, '', K, S, S, 'STARK', 'nf')]),
        ('junno', 'Junno', [plain(W, G, DG, 'DICK UND RUND'), filt(G, W, W), k('Lang', 'Ланг', 2, '', DG, GD, GL, 'LANG', 'lg')]),
        # Britain and Ireland
        ('woodbyne', 'Woodbyne', [plain((206, 220, 170), DG, DG, 'PLAIN', 'warm'), filt(DG, (206, 220, 170), (206, 220, 170)), k('Export', 'Экспорт', 2, '', (236, 226, 190), G, DG, 'EXPORT')]),
        ('seniorservise', 'Senior Servise', [plain(W, DB, DB, 'PLAIN'), filt(DB, W, W), k('Extra', 'Экстра', 2, '', GR, DB, DB, 'EXTRA')]),
        ('kapstan', 'Kapstan', [k('Full', 'Full Strength', 'Фулл', 3, '', DB, GD, GL, 'FULL STRENGTH', 'nf'), k('Medium', 'Медиум', 2, '', B, GD, GL, 'MEDIUM', 'nf'), filt(W, DB, DB)]),
        ('embassey', 'Embassey', [k('NoOne', 'No. 1', 'Номер 1', 2, '', W, R, DR, 'No. 1'), k('Regal', 'Envoy', 'Энвой', 2, '', R, W, W, 'ENVOY'), light(GR, R, DR, 'MILD')]),
        ('regall', 'Regall', [k('King', 'King Size', 'Кинг', 2, '', B, GD, GL, 'KING SIZE'), k('Blue', 'Small', 'Смолл', 2, '', LB, DB, DB, 'SMALL'), gold('GOLD', DB)]),
        ('sweetaftonn', 'Sweet Aftonn', [plain((240, 226, 170), BR, BR, 'VIRGINIA  ·  PLAIN', 'sweet'), filt(BR, (240, 226, 170), (240, 226, 170)), k('Banks', 'Banks', 'Бэнкс', 1, 'sweet', (200, 224, 200), DG, DG, 'GREEN BANKS', 'w')]),
        ('majorr', 'Majorr', [k('Extra', 'Extra Size', 'Экстра', 3, '', W, G, K, 'EXTRA SIZE'), k('Green', 'Грин', 2, '', G, W, W, 'GREEN'), light(GR, G, K)]),
        # Italy
        ('emms', 'EMMS', [k('Filtro', 'Фильтро', 2, '', W, R, DR, 'FILTRO'), k('Blu', 'Блу', 1, '', W, B, DB, 'BLU', 'w'), k('Nazione', 'Rosse', 'Россе', 3, '', R, W, W, 'ROSSE')]),
        ('nazionalli', 'Nazionalli', [plain((226, 232, 236), DB, DB, 'SENZA FILTRO'), filt(DB, W, W, 'FILTRO'), k('Esportazione', 'Эспортационе', 2, '', G, W, W, 'ESPORTAZIONE')]),
        # Iberia
        ('fortunna', 'Fortunna', [red('ROJO'), blue('AZUL'), ment(G, W, W, 'MENTOL')]),
        ('celtass', 'Celtass', [k('Cortos', 'Кортос', 3, '', (236, 226, 190), B, DB, 'CORTOS', 'nf'), k('Largos', 'Ларгос', 3, '', B, (236, 226, 190), (236, 226, 190), 'LARGOS'), k('Extra', 'Экстра', 2, '', DB, GD, GL, 'EXTRA FILTRO')]),
        ('idealles', 'Idealles', [k('Amarillo', 'Амарильо', 3, 'warm', Y, K, K, 'PAPEL AMARILLO', 'mz'), k('Blanco', 'Бланко', 3, '', W, K, K, 'PAPEL BLANCO', 'nf'), k('Hebra', 'Эбра', 3, '', K, Y, Y, 'HEBRA', 'nf')]),
        ('sgventill', 'SG Ventill', [k('Ventil', 'Вентиль', 2, '', W, R, B, 'VENTIL'), k('Gigante', 'Жиганте', 2, '', R, W, W, 'GIGANTE'), k('Lights', 'Лайтс', 1, '', B, W, W, 'LIGHTS', 'w')]),
        ('suavve', 'Portugues Suavve', [plain((236, 226, 190), DR, DR, 'SEM FILTRO'), filt(DR, GD, GL, 'COM FILTRO'), k('Azul', 'Азул', 1, '', DB, GD, GL, 'AZUL', 'w')]),
        # Greece and Turkey
        ('karellia', 'Karellia', [k('Slims', 'Слимс', 1, '', W, B, DB, 'SLIMS', 'ss'), k('Royal', 'Роял', 2, '', B, GD, GL, 'ROYAL'), k('Leaf', 'Golden Leaf', 'Голден Лиф', 2, 'sweet', (236, 226, 190), AU, BR, 'GOLDEN LEAF', 'g')]),
        ('assoss', 'Assoss', [k('International', 'Интернешнл', 2, '', W, R, K, 'INTERNATIONAL'), k('Filter', 'Фильтр', 3, '', R, W, W, 'FILTER'), k('Plain', 'Плейн', 3, '', (236, 226, 190), R, K, 'AFILTRO', 'nf')]),
        ('samsunn', 'Samsunn', [k('Classic', 'Классик', 2, '', R, W, W), k('Black', 'Блэк', 3, '', K, R, W, 'BLACK SEA'), k('Soft', 'Софт', 1, '', W, R, DR, 'SOFT', 'w')]),
        ('maltepeh', 'Maltepeh', [k('Classic', 'Классик', 3, '', (236, 226, 190), R, DR), filt(R, (236, 226, 190), (236, 226, 190), 'FILTRELI'), k('Long', 'Лонг', 2, '', DB, W, W, 'UZUN', 'lg')]),
        ('bafrah', 'Bafrah', [plain((214, 200, 150), DG, DG, 'FILTRESIZ', 'warm'), k('Birinci', 'Биринджи', 3, '', DG, S, S, 'BIRINCI', 'nf'), filt(BR, S, S, 'FILTRELI')]),
        # the North
        ('princce', 'Princce', [k('Rich', 'Rich Taste', 'Рич', 3, '', R, W, W, 'RICH TASTE'), k('White', 'Уайт', 1, '', W, R, DR, 'WHITE', 'w'), k('Hundred', '100', 'Сотка', 2, '', K, R, W, '100  ·  LONG', 'lg')]),
        ('northstaate', 'North Staate', [plain(DB, W, W, 'PLAIN  ·  STRONG'), filt(B, W, W), k('Winter', 'Винтер', 1, 'fresh', LB, DB, DB, 'WINTER', 'w')]),
        ('klubbi', 'Klubbi', [k('K77', '77', '77', 3, '', (226, 60, 50), W, W, '77  ·  PLAIN', 'nf'), k('K78', '78', '78', 2, '', W, (226, 60, 50), K, '78  ·  FILTER'), k('K79', '79', '79', 1, '', K, (226, 60, 50), W, '79  ·  LIGHT', 'w')]),
        ('blennd', 'Blennd', [k('Yellow', 'Йеллоу', 2, '', Y, DB, DB, 'GUL'), k('Blue', 'Блю', 1, '', B, Y, W, 'BLA', 'w'), k('White', 'Уайт', 0, '', W, B, DB, 'VIT', 'w')]),
        ('johnsylver', 'John Sylver', [plain(W, DB, DB, 'UTAN FILTER'), filt(DB, GR, W), k('Special', 'Спешл', 2, '', GR, DB, DB, 'SPECIAL')]),
        # the Alps and the Low Countries
        ('parisienn', 'Parisienn', [k('Jaune', 'Жён', 2, '', Y, K, K, 'JAUNE'), k('Rouge', 'Руж', 2, '', R, Y, W, 'ROUGE'), k('Noir', 'Нуар', 3, '', K, Y, W, 'NOIR', 'nf')]),
        ('marylongg', 'Mary Longg', [k('Classic', 'Классик', 1, '', Y, DR, DR, 'CLASSIC', 'w'), k('Extra', 'Экстра', 1, '', W, (200, 150, 40), DR, 'EXTRA', 'w'), k('Long', 'Лонг', 1, 'sweet', PK, DR, DR, 'LONG', 'lg')]),
        ('memfis', 'Memfis', [k('Classic', 'Классик', 2, '', DB, GD, GL), k('Blue', 'Блю', 1, '', LB, DB, DB, 'BLUE', 'w'), k('Sand', 'Сэнд', 2, 'warm', S, DB, DB, 'SAND')]),
        ('belgga', 'Belgga', [k('Rouge', 'Руж', 3, '', R, Y, W, 'ROUGE'), k('Jaune', 'Жён', 2, '', Y, R, K, 'JAUNE'), k('Bleu', 'Блё', 1, '', B, Y, W, 'BLEU', 'w')]),
        ('bastoss', 'Bastoss', [plain((226, 214, 180), B, DB, 'SANS FILTRE'), filt(B, W, W, 'FILTRE'), k('Rouge', 'Руж', 3, '', R, W, W, 'ROUGE  ·  FORT')]),
        ('caballerro', 'Caballerro', [plain(W, R, K, 'ZONDER FILTER'), filt(R, W, W), k('Navy', 'Нэви', 2, '', DB, W, W, 'NAVY')]),
        # the USA
        ('oldegold', 'Olde Gold', [k('Straights', 'Стрейтс', 3, '', (226, 180, 70), DR, DR, 'STRAIGHTS', 'nf'), filt(W, AU, DR, 'FILTERS'), k('Lights', 'Лайтс', 1, '', (240, 226, 170), AU, DR, 'LIGHTS', 'w')]),
        ('raleygh', 'Raleygh', [plain(R, GD, GL, 'PLAIN END'), filt(W, R, DR, 'FILTER KINGS'), k('Coupon', 'Купон', 2, '', DB, GD, GL, 'EXTRA COUPON')]),
        ('tareytonn', 'Tareytonn', [k('Dual', 'Dual Filter', 'Дуал', 2, '', W, R, DB, 'DUAL FILTER', 'w'), k('Lights', 'Лайтс', 1, '', GR, B, DB, 'LIGHTS', 'w'), k('Long', '100', 'Сотка', 2, '', R, W, W, '100s', 'lg')]),
        ('slimms', 'Virginia Slimms', [k('Classic', 'Классик', 1, '', W, P, P, 'SLIMS', 'ss'), k('Menthol', 'Ментол', 1, 'fresh', (200, 232, 210), G, DG, 'MENTHOL', 'ss'), k('Gold', 'Голд', 0, '', K, GD, GL, 'SUPERSLIMS', 'ss')]),
        ('caprii', 'Caprii', [k('Magenta', 'Маджента', 0, '', (200, 40, 120), W, W, 'MAGENTA', 'ss'), k('Indigo', 'Индиго', 0, '', (60, 50, 140), W, W, 'INDIGO', 'ss'), k('Jade', 'Джейд', 0, 'fresh', TQ, W, W, 'JADE  ·  MENTHOL', 'ss')]),
        ('mistyy', 'Mistyy', [k('Blue', 'Блю', 0, '', (170, 200, 230), DB, DB, 'BLUE', 'ss'), k('Rose', 'Роуз', 0, 'sweet', PK, P, P, 'ROSE', 'ss'), k('Green', 'Грин', 0, 'fresh', (190, 226, 200), DG, DG, 'MENTHOL', 'ss')]),
        ('meritt', 'Meritt', [k('Gold', 'Голд', 1, '', W, AU, K, 'GOLD', 'w'), k('Bronze', 'Бронз', 2, '', (150, 100, 50), W, W, 'BRONZE'), k('Silver', 'Сильвер', 0, '', GR, K, K, 'ULTIMA', 'w')]),
        ('carltonn', 'Carltonn', [k('One', 'Уан', 0, '', W, DB, DB, '1 MG', 'w'), k('Hundred', '100', 'Сотка', 0, '', DB, GD, GL, '100s', 'lg'), ment(DG, GD, GL)]),
        ('trew', 'Trew', [k('Blue', 'Блю', 0, '', W, B, DB, 'BLUE', 'w'), k('Green', 'Грин', 0, 'fresh', W, G, DG, 'GREEN  ·  MENTHOL', 'w'), k('Gold', 'Голд', 1, '', W, AU, BR, 'GOLD', 'g')]),
        ('dorall', 'Dorall', [k('Full', 'Full Flavor', 'Фулл', 2, '', R, GD, GL, 'FULL FLAVOR'), light(GD, DB, DB), ment(DG, GD, GL)]),
        ('basik', 'Basik', [k('Full', 'Full Flavor', 'Фулл', 2, '', W, R, K, 'FULL FLAVOR'), light(W, B, K), ment(W, G, K)]),
        ('giration', 'G.I. Ration', [k('Four', 'Four Pack', 'Четвёрка', 3, '', OL, (226, 218, 180), (226, 218, 180), 'TYPE I  ·  PLAIN', 'nf'), k('Field', 'Филд', 3, 'warm', (120, 110, 80), K, K, 'FIELD ISSUE', 'nf'), filt((60, 70, 50), (226, 218, 180), (226, 218, 180), 'TYPE II  ·  FILTER')]),
        # Japan, China, Korea
        ('ekko', 'Ekko', [plain((240, 150, 50), K, K, 'PLAIN'), filt(K, (240, 150, 50), W), k('Mountain', 'Маунтин', 2, 'warm', (236, 226, 190), (200, 100, 30), K, 'MOUNTAIN')]),
        ('kabin', 'Kabin', [k('Red', 'Ред', 2, '', R, W, W, 'RED'), k('Mild', 'Майлд', 1, '', W, R, K, 'MILD', 'w'), k('Winter', 'Винтер', 1, 'fresh', DB, W, W, 'WINTER  ·  MENTHOL', 'w')]),
        ('castter', 'Castter', [k('Vanilla', 'Ванилла', 1, 'sweet', W, AU, BR, 'VANILLA', 'w'), k('Mild', 'Майлд', 1, 'sweet', (236, 226, 190), BR, BR, 'MILD', 'w'), k('Frontier', 'Фронтир', 0, 'sweet', LB, DB, DB, 'FRONTIER', 'w')]),
        ('pianissimmo', 'Pianissimmo', [k('Peche', 'Пеш', 0, 'sweet', PK, (220, 100, 120), DR, 'PECHE', 'ss'), k('Menthol', 'Ментол', 0, 'fresh', (200, 232, 210), G, DG, 'FRAN MENTHOL', 'ss'), k('Viv', 'Вив', 0, '', (220, 210, 240), P, P, 'VIV', 'ss')]),
        ('liquun', 'Liquun', [k('Classic', 'Классик', 2, '', R, GD, GL), k('Sunshine', 'Саншайн', 1, '', (240, 226, 170), R, DR, 'SUNSHINE', 'g'), k('Long', 'Лонг', 2, '', DB, GD, GL, 'WEST LAKE')]),
        ('baishah', 'Baishah', [k('Classic', 'Классик', 2, '', W, R, DR), k('Silver', 'Сильвер', 1, '', GR, DB, DB, 'SILVER CRANE', 'w'), k('Red', 'Ред', 2, '', R, W, W, 'RED CRANE')]),
        ('nanjinng', 'Nanjinng', [k('Red', 'Ред', 2, '', R, GD, GL, 'RED'), k('Purple', 'Пёрпл', 2, 'sweet', P, GD, GL, 'PURPLE TOWER', 'g'), k('Rain', 'Рейн', 1, 'fresh', (200, 226, 220), TQ, DG, 'RAIN FLOWER', 'ss')]),
        ('frontgatte', 'Front Gatte', [plain((226, 214, 180), DR, DR, 'PLAIN'), filt(DR, GD, GL), k('Soft', 'Софт', 1, '', W, DR, DR, 'SOFT', 'w')]),
        ('hatamenn', 'Hatamenn', [plain((200, 220, 200), DG, DG, 'PLAIN'), filt(DG, GD, GL), k('Old', 'Old Town', 'Олд Таун', 3, 'warm', BR, GD, GL, 'OLD TOWN', 'nf')]),
        ('hongmey', 'Hongmey', [k('Classic', 'Классик', 2, 'sweet', Y, R, DR), k('Soft', 'Софт', 1, 'sweet', W, R, DR, 'SOFT', 'w'), k('Winter', 'Винтер', 2, '', DR, W, W, 'WINTER BLOSSOM')]),
        ('thiss', 'Thiss', [k('Classic', 'Классик', 2, '', W, B, K), k('Plus', 'Плюс', 1, '', B, W, W, 'PLUS', 'w'), k('Africa', 'Wild', 'Вайлд', 2, 'coffee', K, OR, W, 'WILD')]),
        ('k89', '89', [k('Lights', 'Лайтс', 1, '', W, R, DB, 'LIGHTS', 'w'), k('Gold', 'Голд', 2, '', K, GD, GL, 'GOLD', 'g'), ment(DG, W, W)]),
        ('arirangg', 'Arirangg', [k('Classic', 'Классик', 2, '', W, B, R), plain(B, W, W, 'PLAIN'), k('Pass', 'Пасс', 2, 'warm', (236, 226, 190), R, DB, 'OVER THE PASS')]),
        # India and Pakistan
        ('goldflaik', 'Gold Flaik', [k('Kings', 'Кингс', 2, '', R, GD, GL, 'KINGS'), k('Lights', 'Лайтс', 1, '', W, AU, DR, 'LIGHTS', 'g'), plain(Y, DR, DR, 'SMALL  ·  PLAIN')]),
        ('wyllis', 'Wyllis Navy Kut', [k('Filter', 'Фильтр', 2, '', W, DB, R, 'FILTER TIPPED'), plain(DB, W, W, 'PLAIN'), k('Flake', 'Флейк', 3, '', (236, 226, 190), DB, DB, 'FLAKE', 'nf')]),
        ('fourskware', 'Four Skware', [k('Kings', 'Кингс', 2, '', R, W, W, 'KINGS'), k('Special', 'Спешл', 2, '', W, R, K, 'SPECIAL'), ment(G, W, W)]),
        ('k3', 'K3', [k('Filter', 'Фильтр', 3, '', R, W, W, 'FILTER'), plain((226, 214, 180), R, K, 'PLAIN'), k('Summit', 'Саммит', 2, 'fresh', LB, DB, DB, 'SUMMIT', 'w')]),
        # South-East Asia
        ('sampurna', 'Sampurna', [k('Mild', 'A Mild', 'Эй Майлд', 1, 'sweet', W, R, K, 'A MILD', 'w'), k('Hijau', 'Иджо', 3, 'sweet', G, GD, GL, 'KRETEK HIJAU', 'kr'), k('King', 'Кинг', 3, 'sweet', K, GD, GL, 'KING  ·  KRETEK', 'kr')]),
        ('bentoell', 'Bentoell', [k('Biru', 'Биру', 3, 'sweet', B, GD, GL, 'BIRU  ·  KRETEK', 'kr'), k('Merah', 'Мера', 3, 'sweet', R, GD, GL, 'MERAH  ·  KRETEK', 'kr'), k('Mild', 'Майлд', 1, 'sweet', W, B, DB, 'MILD', 'w')]),
        ('wismilakk', 'Wismilakk', [k('Spesial', 'Спесиал', 3, 'sweet', DG, GD, GL, 'SPESIAL  ·  KRETEK', 'kr'), k('Slim', 'Слим', 1, 'sweet', K, GD, GL, 'SLIM', 'ss'), k('Diplomat', 'Дипломат', 2, 'sweet', K, GR, W, 'DIPLOMAT')]),
        ('krongthipp', 'Krong Thipp', [k('Red', 'Ред', 3, '', R, GD, GL, 'RED  ·  90'), k('Green', 'Грин', 1, 'fresh', G, GD, GL, 'GREEN  ·  MENTHOL', 'w'), k('Gold', 'Голд', 2, '', (236, 226, 190), AU, DR, 'GOLD', 'g')]),
        ('fallingrainn', 'Falling Rainn', [k('Classic', 'Классик', 2, '', B, W, W), k('Monsoon', 'Монсун', 2, 'fresh', TQ, W, W, 'MONSOON  ·  MENTHOL', 'w'), k('Dry', 'Драй', 3, '', S, B, DB, 'DRY SEASON', 'nf')]),
        ('fortoon', 'Fortoon', [red('RED'), k('White', 'Уайт', 1, '', W, R, K, 'WHITE', 'w'), ment(G, Y, W)]),
        ('championn', 'Championn', [red('RED'), blue('BLUE'), k('Gold', 'Голд', 2, '', Y, R, K, 'GOLD MEDAL')]),
        # Latin America, Africa, Australia
        ('jockeyklub', 'Jockey Klub', [k('Rubios', 'Рубиос', 2, '', W, R, DB, 'RUBIOS'), k('Negros', 'Негрос', 3, '', K, R, W, 'NEGROS'), k('Suaves', 'Суавес', 1, '', LB, DB, DB, 'SUAVES', 'w')]),
        ('particularres', 'Particularres', [k('Fuertes', 'Фуэртес', 3, '', (60, 40, 30), S, S, 'FUERTES', 'nf'), filt(BR, S, S, 'CON FILTRO'), k('Treinta', '30', 'Трейнта', 3, 'warm', S, BR, BR, 'TREINTA', 'nf')]),
        ('k4371', '43/71', [plain(W, DB, DB, 'SIN FILTRO'), filt(DB, W, W, 'CON FILTRO'), k('Largo', 'Ларго', 2, '', R, W, W, 'LARGOS', 'lg')]),
        ('holywood', 'Holywood', [red('ORIGINAL'), blue('BLUE'), ment(G, W, W)]),
        ('derbby', 'Derbby', [k('Azul', 'Азул', 2, '', B, W, W, 'AZUL'), k('Vermelho', 'Вермельо', 2, '', R, W, W, 'VERMELHO'), k('Prata', 'Прата', 1, '', GR, DB, DB, 'PRATA', 'w')]),
        ('roosterr', 'Roosterr', [plain(Y, R, K, 'PLAIN'), filt(R, Y, W), k('Dawn', 'Доун', 3, 'coffee', K, OR, W, 'DAWN  ·  STRONG')]),
        ('gunstonn', 'Gunstonn', [plain((230, 120, 40), K, K, 'PLAIN'), filt(K, (230, 120, 40), W), k('Bush', 'Буш', 3, 'warm', OL, (236, 226, 190), (236, 226, 190), 'BUSH', 'nf')]),
        ('lexingtonn', 'Lexingtonn', [k('Classic', 'Классик', 2, '', W, R, DB), k('Special', 'Спешл', 2, '', R, GD, GL, 'SPECIAL'), light(DB, GD, GL)]),
        ('winfeeld', 'Winfeeld', [red('RED'), blue('BLUE'), gold('GOLD', W)]),
        ('longbeech', 'Long Beech', [k('Original', 'Ориджинал', 2, '', DB, Y, W, 'ORIGINAL'), k('Mild', 'Майлд', 1, '', LB, DB, DB, 'MILD', 'w'), k('Surf', 'Сёрф', 1, 'fresh', TQ, W, W, 'SURF  ·  MENTHOL', 'w')]),
        # leftovers of the East
        ('prilukki', 'Prilukki', [plain(S, DG, DG, 'NO FILTER'), filt(DG, GD, GL), k('Osoblivi', 'Особливи', 2, '', DB, GD, GL, 'OSOBLIVI')]),
        ('russkystyle', 'Russky Style', [k('Red', 'Ред', 2, '', DR, GD, GL, 'RED'), k('Blue', 'Блю', 1, '', DB, GD, GL, 'BLUE', 'w'), k('Black', 'Блэк', 2, '', K, GD, GL, 'BLACK', 'g')]),
        ('makhorrka', 'Makhorrka', [k('NoTwo', 'No. 2', 'Номер 2', 3, 'warm', (200, 180, 130), K, K, 'No. 2  ·  KREPKAYA', 'nf'), k('NoThree', 'No. 3', 'Номер 3', 3, '', (180, 170, 130), DG, DG, 'No. 3  ·  SREDNYAYA', 'nf'), k('Front', 'Фронт', 3, 'warm', OL, (226, 218, 180), (226, 218, 180), 'FRONTOVAYA', 'nf')]),
        ('vegga', 'Vegga', [k('Classic', 'Классик', 2, '', DB, W, W), k('Blue', 'Блю', 1, '', LB, DB, DB, 'BLUE STAR', 'w'), k('Nova', 'Нова', 2, '', K, Y, W, 'NOVA')]),
        ('juwell', 'Juwell', [k('Classic', 'Классик', 2, '', R, GD, GL), k('Alt', 'Альт', 3, '', (236, 226, 190), R, DR, 'ALT  ·  OHNE FILTER', 'nf'), k('72', '72', '72', 2, '', DB, GD, GL, 'JUWELL 72')]),
        ('kabinett', 'Kabinett', [k('Wurzig', 'Вюрциг', 2, '', BR, GD, GL, 'WURZIG'), k('Mild', 'Мильд', 1, '', (236, 226, 190), BR, BR, 'MILD', 'w'), k('Menthol', 'Ментол', 1, 'fresh', DG, GD, GL, 'MENTHOL', 'w')]),
        ('opatiya', 'Opatiya', [k('Classic', 'Классик', 2, '', W, B, DB), k('Adria', 'Адрия', 1, '', LB, DB, DB, 'ADRIA', 'w'), k('Gold', 'Голд', 2, '', DB, GD, GL, 'ZLATNA', 'g')]),
        ('munkass', 'Munkass', [plain((214, 200, 160), DR, K, 'FILTER NELKUL'), k('Double', 'Дабл', 3, '', DR, (214, 200, 160), (214, 200, 160), 'DUPLA MUSZAK', 'nf'), filt(K, (214, 200, 160), (214, 200, 160), 'FILTERES')]),
    ]
    for key, name, kinds in T:
        assert key not in BRANDS, key
        BRANDS[key] = (name, key, [_A(kk, en, ru, st, bo, base, acc, ink, lab, **FL[fl]) for kk, en, ru, st, bo, base, acc, ink, lab, fl in kinds])
    return [t[0] for t in T]


BATCH11 = _b11()
VIGOR11 = {'revall', 'junno', 'woodbyne', 'kapstan', 'celtass', 'idealles', 'assoss', 'bafrah', 'northstaate', 'bastoss', 'oldegold', 'giration', 'ekko',
           'frontgatte', 'hatamenn', 'k3', 'particularres', 'roosterr', 'gunstonn', 'prilukki', 'makhorrka', 'munkass', 'wyllis', 'maltepeh'}
STEADY11 = {'hbee', 'seniorservise', 'embassey', 'regall', 'majorr', 'emms', 'nazionalli', 'fortunna', 'princce', 'johnsylver', 'caballerro', 'raleygh',
            'tareytonn', 'liquun', 'baishah', 'thiss', 'arirangg', 'jockeyklub', 'k4371', 'derbby', 'lexingtonn', 'winfeeld', 'championn', 'vegga', 'opatiya'}
for _k in BATCH11:
    for _v in BRANDS[_k][2]:
        if not _v['bonus']:
            _v['bonus'] = 'hands' if _k in VIGOR11 else 'steady' if _k in STEADY11 else ''
        _v['side'] = 'dizzy' if _v['strength'] >= 3 else 'dry' if _v['strength'] == 2 else ''

STRENGTH = [
    # fatigue %, hunger minutes, health %, counts for shortness of breath
    (3.0, 20.0, 0.5, 0.7),
    (5.0, 25.0, 1.0, 1.0),
    (7.0, 30.0, 1.5, 1.3),
    (9.0, 35.0, 2.0, 1.6),
]


def all_varieties():
    for bkey, (bname, packcls, vs) in BRANDS.items():
        for v in vs:
            yield bkey, bname, packcls, v


def item_id(bname, v):
    return v['old_id'] or ('Pack' + ''.join(ch for ch in bname if ch.isalnum()) + '_' + v['key'])


def tex_key(bkey, v):
    return '%s_%s' % (bkey, v['key'].lower())


OLD_PACK_MESH = {'chapgirl': 'pack_chapgirl', 'milston': 'pack_milston', 'marlbrrro': 'pack_marlbrrro', 'parliamint': 'pack_parliamint',
                 'bent': 'pack_bent', 'caramel': 'pack_caramel'}


def pack_mesh(bkey, v):
    """Mesh of a variety's pack: the wide / slim boxes, else the brand's own tall box (new brands share the plain tall box)."""
    return {'wide': 'pack_wide', 'slim': 'pack_slim'}.get(v['shape']) or OLD_PACK_MESH.get(bkey, 'pack_milston')
