"""Meme descriptions (user 11-15): two per kind, for the dry pack and for the cooked bowl, RU and EN. Texts live in memes/m*.txt,
five lines per kind: id, raw ru, raw en, hot ru, hot en; "=" for raw means the catalog's own joke (ruf / enf)."""
import glob, os
from catalog import NOODLES
HERE = os.path.dirname(os.path.abspath(__file__))
def sent(s):
    s = s.strip(); s = s[0].upper() + s[1:]
    return s if s[-1] in '.!?' else s + '.'
MEME = {}
by = {s['id']: s for s in NOODLES}
for f in sorted(glob.glob(os.path.join(HERE, 'memes', 'm*.txt'))):
    for b in open(f, encoding='utf8').read().strip().split('\n\n'):
        l = [x.strip() for x in b.split('\n')]
        assert len(l) == 5 and l[0] in by and l[0] not in MEME, (f, l[0])
        s = by[l[0]]
        MEME[l[0]] = (sent(s['ruf']) if l[1] == '=' else l[1], sent(s['enf']) if l[2] == '=' else l[2], l[3], l[4])
assert len(MEME) == len(NOODLES)
if __name__ == '__main__':
    allr = [x for m in MEME.values() for x in m]
    assert len(set(allr)) == len(allr), 'repeated line'
    print(len(MEME), 'kinds; longest ru', max(len(m[i]) for m in MEME.values() for i in (0, 2)), 'en', max(len(m[i]) for m in MEME.values() for i in (1, 3)))
    for k, m in MEME.items():
        for x in m:
            if '  ' in x or not x[-1] in '.!?': print('check', k, x)
