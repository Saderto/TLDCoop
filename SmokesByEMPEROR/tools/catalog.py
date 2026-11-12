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
