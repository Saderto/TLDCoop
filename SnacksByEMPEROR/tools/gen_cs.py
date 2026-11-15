"""Writes ../src/Catalog.g.cs from catalog.py."""
import os
from catalog import NOODLES, SNACKS, BRANDS
import jokes
import catalog_series as CS
HERE = os.path.dirname(os.path.abspath(__file__))
def q(s): return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'
def f(x): return f'{x}f'

HOT = 'hot spicy chilli chili jalapeno flamin wasabi horseradish fuego picante adjika masala peri mustard adobadas chile habanero curry kimchi pepperoni crayfish fiery mala sichuan schezwan chipotle sambal devil scorching buffalo hell wings'.split()
SWEETW = 'honey caramel chocolate toffee maple sugared strawberry banana vanilla nougat matcha milky cherry raisins kettle sugar condensed coconut cinnamon cookie cookies melon mango blueberry halva gingerbread yoghurt cocoa tangerine marshmallow glazed chestnut brittle cream'.split()
GARLIC = 'garlic aioli'.split()
SALTY = 'crisps ridged tortilla potsticks rings shrimp rolls puffs popcorn cones rice cup cracker pretzel sticks bagel croutons rusk seaweed'.split()
DRY = 'croutons rusk cracker pretzel sticks bagel popcorn'.split()
K_FX = dict(seeds='SeedHabit', nuts='Sinew', cornnuts='Sinew', coated='Sinew', mix='Sinew', namkeen='Sinew', drazhe='SugarDry',
            jerky='Sinew', fish='Sinew', seafood='Sinew', rinds='Sinew', strips='Sinew',
            choco='SugarDry', sweetsticks='SugarDry')
DRY = 'croutons rusk cracker pretzel sticks bagel'.split()
BI = {b['id']: i for i, b in enumerate(BRANDS)}
def fx(s):
    words = s['enf'].lower().replace(',', ' ').replace('&', ' ').split(); kind = s['brand']['kind']; out = []
    hot = any(w in HOT for w in words) or kind in ('strips', 'rolls')
    sw = [w for w in words if w in SWEETW]
    if any(w in words for w in ('sour', 'cheese', 'onion', 'herbs', 'dill', 'chive', 'salt', 'salted')): sw = [w for w in sw if w not in ('cream', 'kettle', 'yoghurt')]
    if kind != 'popcorn': sw = [w for w in sw if w != 'kettle']
    sweet = bool(sw) and not hot
    if sweet: out.append('SugarDry')
    elif kind in K_FX: out.append(K_FX[kind])
    elif kind in SALTY: out.append('SaltHold')
    if hot: out += ['PepperBlood', 'Sweat']
    if kind in DRY and not sweet: out.append('DryLump')
    return out
import jokes2
_N = {s['id']: n for n, s in enumerate(SNACKS)}
_PAIR = {}; _used = set()
def _pair(s):
    """Each snack its own pair of lines; a sweet one skips the lines about salt and beer."""
    if s['id'] in _PAIR: return _PAIR[s['id']]
    n = _N[s['id']]; i, j = n % len(jokes2.A), n % len(jokes2.B)
    sweet = 'SugarDry' in fx(s)
    while (i, j) in _used or (sweet and any(w in jokes2.B[j][0].lower() for w in ('сол', 'пив', 'хруст'))): j = (j + 1) % len(jokes2.B)
    _used.add((i, j)); _PAIR[s['id']] = (i, j); return i, j
_SHORT = {}; _sused = set()
def first0(s):
    b = s['brand']; return CS.SEASON[s['rare']][2] if s.get('rare') else (b['joke'][0] if b.get('joke') else jokes.J[b['id']][0])
def joke(s, k):
    b = s['brand']; i, j = _pair(s)
    first = CS.SEASON[s['rare']][2 + k] if s.get('rare') else (b['joke'][k] if b.get('joke') else jokes.J[b['id']][k])
    if False:   # effects are icons now, the text has room again
        if s['id'] not in _SHORT:
            while (first0(s), i) in _sused: i = (i + 1) % len(jokes2.A)
            _sused.add((first0(s), i)); _SHORT[s['id']] = i
        return first + ' ' + jokes2.A[_SHORT[s['id']]][k]
    return first + ' ' + jokes2.A[i][k] + ' ' + jokes2.B[j][k]
def arr(a): return 'new[] { ' + ', '.join(q(x) for x in a) + ' }' if a else 'Array.Empty<string>()'
rows = []
for s in SNACKS:
    rows.append(f'new Def {{ Id = {q(s["id"])}, Kind = Kind.Snack, Shape = {q(s["pack"])}, Mesh = {q(s["brand"]["id"])}, Texture = {q(s["id"])}, Kg = {f(s["kg"])}, Kcal = {f(s["kcal"])}, Thirst = {f(s["thirst"])}, Eat = {f(s["eat"])}, Ru = {q(s["ru"])}, En = {q(s["en"])}, RuF = {q(s["ruf"])}, EnF = {q(s["enf"])}, RuC = {q(s["country"][0])}, EnC = {q(s["country"][1])}, Fx = {arr(fx(s))}, RuJ = {q(joke(s, 0))}, EnJ = {q(joke(s, 1))}, Rare = {'true' if s.get('rare') else 'false'} }},')
for s in []:  # noodles live in NoodlesByEMPEROR since 0.1.1
    sh = s['shape']; cupish = sh in ('cup', 'bigcup', 'bowl')
    rows.append(f'new Def {{ Id = {q(s["id"])}, Kind = Kind.Noodle, Shape = {q(sh)}, Mesh = {q(s["id"])}, Texture = {q(s["id"])}, Kg = {f(s["kg"])}, Water = {f(s["water"])}, Kcal = {f(s["kcal"])}, Ru = {q(s["ru"])}, En = {q(s["en"])}, RuF = {q(s["ruf"])}, EnF = {q(s["enf"])}, Spicy = {s.get("spicy", 0)} }},')
    hot_mesh = s['id'] + 'Hot' if cupish else 'BowlHot'
    hot_tex = s['id'] if cupish else s['id'] + 'Hot'
    rows.append(f'new Def {{ Id = {q(s["id"] + "Hot")}, Kind = Kind.Brewed, Shape = {q(sh)}, Raw = {q(s["id"])}, Mesh = {q(hot_mesh)}, Texture = {q(hot_tex)}, Kg = {f(round(s["kg"] + s["water"] * .9, 3))}, Kcal = {f(s["kcal"])}, Thirst = {f(s["thirst"])}, Warm = {f(s["warm"])}, Ru = {q(s["ru"])}, En = {q(s["en"])}, RuF = {q(s["ruf"])}, EnF = {q(s["enf"])}, Spicy = {s.get("spicy", 0)} }},')
open(os.path.join(HERE, '..', 'src', 'Catalog.g.cs'), 'w').write(
    '// Generated by tools/gen_cs.py from tools/catalog.py: do not edit by hand.\nnamespace SnacksByEMPEROR;\n\npublic static partial class Defs\n{\n    public static readonly Def[] All =\n    {\n'
    + '\n'.join('        ' + r for r in rows) + '\n    };\n}\n')
print(len(rows), 'defs')

import collections
d = [joke(s, 0) for s in SNACKS]; print('descriptions', len(d), 'unique', len(set(d)), 'longest', max(map(len, d)))
c = collections.Counter(x for s in SNACKS for x in fx(s)); print(c, sum(1 for s in SNACKS if not fx(s)), 'without effects')
