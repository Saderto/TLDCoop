"""Cigar brands into the mod: src/CigarCatalog.cs, data/text_cigar_{ru,en}.json, meshes (.fpmesh), textures (baked albedo -> jpg).
python3 gen_cigars.py <bakedir> <texdir> <srcdir> <distdir>"""
import os, sys, json, math, struct
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cigars as cg
import cigar_memes2 as m2

B, TEX, SRC, DIST = sys.argv[1:5]

# ---- meshes (npz_to_fpmesh conventions)


def rx(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]], np.float32)


def ry(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]], np.float32)


def write(path, v, n, uv, tris):
    U = np.stack([-v[:, 0], v[:, 2], -v[:, 1]], 1)
    N = np.stack([-n[:, 0], n[:, 2], -n[:, 1]], 1)
    t = tris[:, [0, 2, 1]].reshape(-1)
    with open(path, 'wb') as f:
        f.write(struct.pack('<ii', len(U), len(t)))
        f.write(U.astype('<f4').tobytes()); f.write(N.astype('<f4').tobytes())
        f.write(uv.astype('<f4').tobytes()); f.write(t.astype('<i4').tobytes())


def load(name):
    d = np.load(os.path.join(B, name + '.npz'))
    return d['verts'].astype(np.float32), d['normals'].astype(np.float32), d['uvs'].astype(np.float32), d['tris']


def rest(name, R, out=None):
    v, n, uv, t = load(name)
    v, n = v @ R.T, n @ R.T
    lo, hi = v.min(0), v.max(0)
    v = v - np.array([(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]], np.float32)
    write(os.path.join(DIST, 'meshes', (out or name) + '.fpmesh'), v, n, uv, t)


def held(name, out):
    v, n, uv, t = load(name)
    R = rx(math.pi)
    v, n = v @ R.T, n @ R.T
    v[:, 2] -= v[:, 2].min()
    R = rx(math.pi / 2)
    write(os.path.join(DIST, 'meshes', out + '.fpmesh'), v @ R.T, n @ R.T, uv, t)


along = ry(math.pi / 2)
for c in cg.CIGARS:
    k = cg.lc(c)
    for m in ('cigar_' + k, 'cigar_cut_' + k, 'tube_' + k):
        rest(m, along)
        Image.open(os.path.join(B, m + '_albedo.png')).convert('RGB').save(os.path.join(DIST, 'textures', m + '.jpg'), quality=90)
    held('cigar_cut_' + k, 'cigar_cut_%s_held' % k)
for c in cg.CIGARS + cg.OLD:
    k = cg.lc(c)
    rest('box_' + k, np.eye(3, dtype=np.float32))
    Image.open(os.path.join(TEX, 'box_%s.jpg' % k)).save(os.path.join(DIST, 'textures', 'box_%s.jpg' % k), quality=88)

# ---- texts
SHAPE = {'robusto': ('робусто', 'robusto'), 'churchill': ('черчилль', 'churchill'), 'corona': ('корона', 'corona'),
         'short': ('короткий робусто', 'short robusto'), 'torpedo': ('торпедо', 'torpedo'), 'lancero': ('тонкий лансеро', 'slim lancero'),
         'boxpress': ('бокс-пресс', 'box-pressed')}
COUNTRY = {'HABANA': ('Гаванская', 'A Havana'), 'REP. DOMINICANA': ('Доминиканская', 'A Dominican'), 'NICARAGUA': ('Никарагуанская', 'A Nicaraguan'),
           'HONDURAS': ('Гондурасская', 'A Honduran')}
MEME = {
    'Partagaz': ('Полная и тёмная, как полярная ночь. Курить медленно, думать о вечном, не думать о волках.',
                 'Full and dark as the polar night. Smoke it slowly, think of eternity, do not think of wolves.'),
    'Romero': ('Сигара для тех, кто ещё верит в любовь. Здесь, на Великом Медвежьем, это смелее, чем идти на медведя.',
               'For those who still believe in love. Out here on Great Bear that is braver than going after a bear.'),
    'Hoyo': ('Мягкая и светлая. Для утра, когда кофе кончился, а надежды нет.',
             'Mild and light. For a morning with no coffee left and no hope either.'),
    'Uppmann': ('Классика банкиров. Банков больше нет, а сигара осталась.', 'A bankers\' classic. The banks are gone, the cigar remains.'),
    'Pounch': ('Короткая, но с характером. Как ваш последний разговор с Серой Матерью.', 'Short but with character. Like your last talk with the Grey Mother.'),
    'Bolivarr': ('Очень крепкая. Освобождает от усталости, страха и лишнего здоровья.', 'Very strong. Frees you from fatigue, fear and spare health.'),
    'Trinidud': ('Длинная и тонкая, как путь от Милтона до Горного городка пешком.', 'Long and thin, like the walk from Milton to Mountain Town.'),
    'Arturo': ('Доминиканский черчилль. Курится дольше, чем держится костёр из газет.', 'A Dominican churchill. Burns longer than a fire of newspapers.'),
    'Padrone': ('Квадратная, чтобы не укатилась в сугроб. Полная, тёмная, серьёзная.', 'Square so it will not roll into a snowdrift. Full, dark, serious.'),
    'Macanuto': ('Светлая и сливочная. Её курят, когда вокруг тихо и ничего не хочет вас съесть.', 'Light and creamy. Smoked when it is quiet and nothing wants to eat you.'),
    'Olivva': ('С оливковой ветвью на кольце. Мир с волками всё равно не заключить.', 'An olive branch on the band. You still will not make peace with the wolves.'),
    'Rocky': ('Торпедо из Гондураса. Бьёт точно в усталость.', 'A torpedo from Honduras. Hits fatigue dead on.'),
    'Ashten': ('Мягкий черчилль для джентльменов, переживших конец света в твидовом пиджаке.', 'A mild churchill for gentlemen who outlived the end of the world in tweed.'),
    'Quai': ('Очень светлая гаванская корона. Почти как не курить, только приятнее.', 'A very light Havana corona. Almost like not smoking, only nicer.'),
    'Grandfather': ('Сигара деда. Дед прожил девяносто лет и ни разу не видел волка. Вам повезло меньше.',
                    'Grandfather\'s cigar. He lived to ninety and never saw a wolf. You were less lucky.'),
    'Cuabba': ('Двойная фигурадо, сужается с обоих концов, как запасы к концу зимы.', 'A figurado tapered at both ends, like your supplies at the end of winter.'),
    'VegasRobaino': ('С табачных плантаций Вуэльта-Абахо. Здесь из плантаций только ёлки.', 'From the Vuelta Abajo fields. The only plantations here are spruce.'),
    'JuanLopes': ('Мягкая корона. Хуан не знал, где вы её будете курить, и хорошо.', 'A mild corona. Juan never knew where you would smoke it, and just as well.'),
    'RamonAllonzo': ('Крепкая и густая. Первую сигарную коробку с картинкой придумали ради неё.', 'Strong and dense. The first picture cigar box was made for this one.'),
    'SanCristobaal': ('Названа в честь крепости. Ваша крепость из досок и сквозняков.', 'Named after a fortress. Yours is made of planks and draughts.'),
    'PorLarranya': ('Светлая и тонкая. Курить у окна и смотреть, как метёт.', 'Light and slim. Smoke it by the window and watch the snow blow.'),
    'ReyDelMondo': ('«Король мира». Мир теперь маленький: остров, лёд и вы.', '"King of the world". The world is small now: an island, ice and you.'),
    'SanchoPansa': ('Оруженосец, не рыцарь. Зато доживает до конца книги.', 'The squire, not the knight. But he makes it to the end of the book.'),
    'Diplomatikos': ('Для переговоров. С волками переговоры не идут.', 'For negotiations. Negotiations with wolves are not going well.'),
    'GloriaCubanna': ('Слава Кубы. Здесь слава в том, чтобы дожить до утра.', 'The glory of Cuba. Here glory is living until morning.'),
    'Fonsecka': ('В папиросной бумажке, как в шарфике. Вам бы тоже шарфик.', 'Wrapped in tissue paper like a scarf. You could use a scarf too.'),
    'MyUncle': ('Сигара дяди. Дядя был крепче деда, и сигара тоже.', 'Uncle\'s cigar. Uncle was tougher than grandfather, and so is the cigar.'),
    'Perdomio': ('Выдержана десять лет. Вы здесь пока меньше.', 'Aged ten years. You have been here less, so far.'),
    'LigaPrivatta': ('Тёмная, как ночь без луны. Курится долго, мыслей хватает.', 'Dark as a moonless night. Burns long, plenty of time to think.'),
    'AlecBadly': ('Алек сделал её плохо. Шутка: сделал хорошо, а вот погода плохая.', 'Alec made it badly. Joke: he made it well, the weather is what is bad.'),
    'FlorDominicanna': ('Двойной лихеро: самый крепкий лист. Для тех, кто уже ничего не боится.', 'Double ligero: the strongest leaf. For those who fear nothing anymore.'),
    'Kamacho': ('Тройной мадуро. Короткая, тёмная, кусается, как рысь.', 'Triple maduro. Short, dark, bites like a lynx.'),
    'JoyaNicaragwa': ('Сокровище Никарагуа. В Никарагуа тепло, в отличие от здесь.', 'The jewel of Nicaragua. Nicaragua is warm, unlike here.'),
    'Tatuajje': ('Татуировка на кольце. Вашу татуировку теперь рисует мороз.', 'A tattoo on the band. Frost draws your tattoos now.'),
    'CuestaRay': ('Длинный светлый лансеро. Хватит на целую метель.', 'A long light lancero. Lasts through a whole blizzard.'),
    'Cohibra': ('Коробка гаванских сигар. Кто-то копил на неё всю жизнь, а досталась вам.', 'A box of Havana cigars. Someone saved up for it all their life, and you got it.'),
    'Montecrusto': ('Коробка с скрещёнными топорами на этикетке. Топоры, к сожалению, нарисованные.', 'A box with crossed axes on the label. Sadly, the axes are only painted.'),
}
STATS = [(8, 1.5), (10, 2), (13, 2.5)]
BONUS = ['calm', '', 'warm']
BONUS_NEW = ['calm', 'hands', 'warm']
SIDE_NEW = ['', 'dry', 'dizzy']
NEW_FROM = 15   # cigars after the first 15 are the new batch
BON_TXT = {'hands': ('«Твёрдые пальцы»: шанс развести огонь +10% на 1 ч.', '"Steady fingers": fire-starting chance +10% for 1 h.'),
           'dry': ('Минус: «Сухость во рту» — жажда растёт на 15% быстрее 1 ч.', 'Downside: "Dry mouth": thirst builds 15% faster for 1 h.'),
           'dizzy': ('Минус: «Головокружение» — прицел качается на 30% сильнее 1 ч.', 'Downside: "Dizzy": aim sway 30% more for 1 h.'),
'warm': ('«Тёплый дым»: ощущаемая температура +1°C на 1 ч.', '"Warm smoke": feels-like temperature +1°C for 1 h.'),
           'calm': ('«Спокойствие»: прицел качается на 25% меньше 1 ч.', '"Calm": aim sway 25% less for 1 h.')}
CUT_RU = 'Перед курением кончик надо обрезать: гильотиной ZIKAR или ножом (ножом можно порвать лист, тогда эффект вдвое слабее).'
CUT_EN = 'Its cap must be cut first: with a ZIKAR cutter or a knife (a knife may tear the leaf, halving the effect).'
ru, en = {}, {}


def T(key, a, b):
    ru['SMOKES_' + key] = a
    en['SMOKES_' + key] = b


def bonus(i, s):
    return (BONUS_NEW[s], SIDE_NEW[s]) if i >= NEW_FROM else (BONUS[s], '')


for i, c in enumerate(cg.CIGARS):
    K, nm = c['key'], c['name']
    s = c['strength']
    bon, side = bonus(i, s)
    fat, hp = STATS[s]
    cr, ce = COUNTRY[c['country']]
    sr, se = SHAPE[c['shape']]
    mr, me = MEME[K]
    st_r = 'Сигара (%s): усталость −%g%%, голод на 10%% медленнее 45 мин, здоровье −%g%%, считается как три сигареты.' % (cg.STRENGTH_RU[s], fat, hp)
    st_e = 'A %s cigar: fatigue −%g%%, hunger 10%% slower for 45 min, condition −%g%%, counts as three cigarettes.' % (cg.STRENGTH_EN[s], fat, hp)
    for b in (bon, side):
        if b:
            st_r += ' ' + BON_TXT[b][0]; st_e += ' ' + BON_TXT[b][1]
    T('Tube%s_NAME' % K, 'Сигара %s в тубе' % nm, '%s cigar in a tube' % nm)
    T('Tube%s_DESC' % K, '%s сигара, %s, в алюминиевой тубе. %s «Открыть», чтобы достать.' % (cr, sr, mr),
      '%s %s cigar in an aluminium tube. %s «Open» to take it out.' % (ce, se, me))
    T('Cigar%s_NAME' % K, 'Сигара %s' % nm, '%s cigar' % nm)
    ur, ue = m2.UNCUT[K]
    T('Cigar%s_DESC' % K, '%s сигара, %s. %s %s' % (cr, sr, ur, CUT_RU), '%s %s cigar. %s %s' % (ce, se, ue, CUT_EN))
    T('CigarCut%s_NAME' % K, 'Сигара %s (обрезана)' % nm, '%s cigar (cut)' % nm)
    T('CigarCut%s_DESC' % K, m2.CUT[K][0] + ' ' + st_r, m2.CUT[K][1] + ' ' + st_e)
    T('LitCigar%s_NAME' % K, 'Сигара %s (курится)' % nm, '%s cigar (lit)' % nm)
    T('LitCigar%s_DESC' % K, 'Курится. H, чтобы выбросить.', 'Burning. H to throw it away.')
for c in cg.CIGARS + cg.OLD:
    K, nm, n = c['key'], c['name'], c['count']
    mr, me = MEME[K] if K in ('Cohibra', 'Montecrusto') else ('%s, %d сигар %s.' % (cg.STYLE_RU[cg.box_style(c)], n, nm), '%s of %d %s cigars.' % (cg.STYLE_EN[cg.box_style(c)], n, nm))
    if K not in ('Cohibra', 'Montecrusto'):
        mr += ' ' + MEME[K][0]; me += ' ' + MEME[K][1]
    else:
        mr = '%s, %d сигар. %s' % (cg.STYLE_RU[cg.box_style(c)], n, mr); me = '%s of %d cigars. %s' % (cg.STYLE_EN[cg.box_style(c)], n, me)
    T('Box%s_NAME' % K, 'Коробка сигар %s' % nm, 'Box of %s cigars' % nm)
    T('Box%s_DESC' % K, mr, me)
T('ACT_BOX', 'Достать сигару', 'Take a cigar')
T('MSG_BOX_TAKEN', 'Сигара достана из коробки', 'A cigar taken from the box')
for L, d in (('ru', ru), ('en', en)):
    with open(os.path.join(DIST, 'data', 'text_cigar_%s.json' % L), 'w', encoding='utf-8') as f:
        json.dump(d, f, ensure_ascii=False, indent=1)

# ---- C#
lines = ['namespace SmokesBySeagull;', '',
         '/// <summary>Generated by tools/gen_cigars.py from tools/cigars.py: the cigar brands (tube, cigar, cut cigar, lit cigar) and cigar boxes.</summary>',
         'public static class CigarCatalog', '{',
         '    public sealed record Cigar(string Key, int Strength, string Bonus, int BoxCount, bool New, string Side);', '',
         '    public static readonly Cigar[] All =', '    {']
for i, c in enumerate(cg.CIGARS):
    bon, side = bonus(i, c['strength'])
    lines.append('        new("%s", %d, "%s", %d, true, "%s"),' % (c['key'], c['strength'], bon, c['count'], side))
for c in cg.OLD:
    lines.append('        new("%s", 1, "", %d, false, ""),' % (c['key'], c['count']))
lines += ['    };', '}', '']
open(os.path.join(SRC, 'CigarCatalog.cs'), 'w').write('\n'.join(lines))
print('cigars', len(cg.CIGARS), 'boxes', len(cg.CIGARS) + len(cg.OLD), 'texts', len(ru))
