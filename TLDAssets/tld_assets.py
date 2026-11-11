# TLD Assets by Seagull: builds the game-derived files of the Seagull mods on the player's own PC, from the player's
# own copy of The Long Dark (WINTERMUTE episode 5 files). Nothing from the game is shipped with the mods.
#
#   TLDCoop          Mods/TLDCoop/characters.bin              player figures (ep5 characters + clips)
#   TLDShotgun       Mods/TLDShotgun/tldshotgun.bundle        the shotgun, its ammo and hands
#   TLDMilitaryStim  Mods/TLDMilitaryStim/tldmilitarystim.bundle
#   TLDCrankLamp     Mods/TLDCrankLamp/tldcranklamp.bundle + audio/*.bnk
#
# Usage: TLDAssets.exe [--game "<The Long Dark folder>"] [--all] [--out <folder>] [--keep-temp]
#   Without --game the folder is found next to the program, then through Steam.
#   Without --all only mods whose dll is in Mods are built.
#   --out writes into <folder>/Mods/... instead of the game (for checking).
import os, sys, json, hashlib, shutil, tempfile, runpy, traceback, time, glob, argparse, io

HERE = getattr(sys, '_MEIPASS', os.path.dirname(os.path.abspath(__file__)))
STEPS = os.path.join(HERE, 'steps')
OWN = os.path.join(HERE, 'own')
sys.path.insert(0, STEPS)
VERSION = '1.0.0'

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

LOG = None
SINK = None          # the launcher sets this to show the log in its window
def P(*a):
    s = ' '.join(str(x) for x in a)
    if SINK: SINK(s)
    else: print(s, flush=True)
    if LOG: LOG.write(s + '\n'); LOG.flush()

RU = None
def T(ru, en): return ru if RU else en

# Known-good results (built from game 2.60 and checked in game). A different hash is not an error by itself:
# after a game update the episode files may change, the result is then written and reported.
EXPECTED = {
    'TLDCoop/characters.bin': '012400562a2da92c465174d970ac7e1d',
    'TLDShotgun/tldshotgun.bundle': 'daac572eb2df67229af7418d176f6250',
    'TLDMilitaryStim/tldmilitarystim.bundle': 'd6cf257032ed32bfb744cb294e02dd4d',
    'TLDCrankLamp/tldcranklamp.bundle': '222aa23cfd0590339c8c27e4ec85e9d6',
    'TLDCrankLamp/audio/FlashlighCrankLamp.bnk': '6c5d3ea9aa2b423349234eb1800854db',
    'TLDCrankLamp/audio/_StoryEvents.bnk': '0a7422023a74f4e67c99783a344ea957',
    'TLDCrankLamp/audio/_StoryStructures.bnk': 'eb8c07791bfda6ae51abe5c433303245',
    'TLDCrankLamp/audio/TLDCrankLamp_Events.bnk': '706db7409f9b58d7c95d0c8591e21672',
}

def md5(path):
    h = hashlib.md5()
    with open(path, 'rb') as f:
        for b in iter(lambda: f.read(1 << 20), b''): h.update(b)
    return h.hexdigest()

# ---------------- finding the game ----------------
def is_game(d): return bool(d) and os.path.isdir(os.path.join(d, 'tld_Data')) and any(os.path.isfile(os.path.join(d, e)) for e in ('tld.exe', 'TheLongDark.exe'))

def steam_libraries():
    roots = []
    try:
        import winreg
        for hive, key, val in [(winreg.HKEY_CURRENT_USER, r'Software\Valve\Steam', 'SteamPath'),
                               (winreg.HKEY_LOCAL_MACHINE, r'SOFTWARE\WOW6432Node\Valve\Steam', 'InstallPath')]:
            try:
                with winreg.OpenKey(hive, key) as k: roots.append(winreg.QueryValueEx(k, val)[0])
            except OSError: pass
    except ImportError: pass
    libs = []
    for r in roots:
        r = os.path.normpath(r); libs.append(r)
        vdf = os.path.join(r, 'steamapps', 'libraryfolders.vdf')
        if os.path.isfile(vdf):
            for line in open(vdf, encoding='utf-8', errors='replace'):
                line = line.strip()
                if line.startswith('"path"'):
                    libs.append(line.split('"')[3].replace('\\\\', '\\'))
    return libs

def find_game(arg):
    if arg:
        return arg if is_game(arg) else None
    d = os.path.dirname(os.path.abspath(sys.executable if getattr(sys, 'frozen', False) else __file__))
    for _ in range(4):
        if is_game(d): return d
        d = os.path.dirname(d)
    for lib in steam_libraries():
        g = os.path.join(lib, 'steamapps', 'common', 'TheLongDark')
        if is_game(g): return g
    return None

# ---------------- steps ----------------
def run_script(name, argv=None, env=None):
    old_argv, old_env = sys.argv, dict(os.environ)
    sys.argv = [name] + (argv or [])
    os.environ.update(env or {})
    try:
        runpy.run_path(os.path.join(STEPS, name), run_name='__main__')
    finally:
        sys.argv = old_argv
        os.environ.clear(); os.environ.update(old_env)

def build_coop(g, tmp, out):
    import zlib, struct
    a = os.path.join(tmp, 'coop_base.bin'); b = os.path.join(tmp, 'coop_synth.bin')
    run_script('coop_ep5pack.py', [g['ep5'], a])
    run_script('coop_animsynth.py', [a, b])
    # our own clips (crouch walk/idle/side, retargeted from Quaternius CC0 animations)
    import coop_clips
    data = coop_clips.append(open(b, 'rb').read(), open(os.path.join(OWN, 'coop_own_clips.bin'), 'rb').read())
    open(os.path.join(out, 'TLDCoop', 'characters.bin'), 'wb').write(data)

def bundles(g, names): return [os.path.join(g['ep5'], n) for n in names]

def build_shotgun(g, tmp, out):
    import extract
    ext = os.path.join(tmp, 'shotgun_extract')
    extract.run(bundles(g, ['06fe64a087041e406a76f4539374d751.bundle']),
                {"CAB-71edd1c7bb1ddeb20c4ae08cfa2797c8": [-4090140187055288879, 6989890098493347211, -499929211162636386,
                                                         -1353090822767189807, 6596921083635724480, 3719040124481894599]}, ext, P)
    run_script('shotgun_build.py', env={'TLD_EP5': g['ep5'], 'TLD_SURV': g['surv'], 'TLD_EXT': ext,
                                        'TLD_OUT': os.path.join(out, 'TLDShotgun', 'tldshotgun.bundle')})

def build_syringe(g, tmp, out):
    import extract
    ext = os.path.join(tmp, 'syringe_extract')
    extract.run(bundles(g, ['06fe64a087041e406a76f4539374d751.bundle']),
                {"CAB-71edd1c7bb1ddeb20c4ae08cfa2797c8": [8747020503677287537]}, ext, P)
    run_script('syringe_build.py', env={'TLD_EP5': g['ep5'], 'TLD_SURV': g['surv'], 'TLD_EXT': ext,
                                        'TLD_OUT': os.path.join(out, 'TLDMilitaryStim', 'tldmilitarystim.bundle')})

ARM_CLIPS = [959415812501684677, 1756922127720572571, -3401301520838556575]

def build_cranklamp(g, tmp, out):
    import extract, crank_banks
    ext = os.path.join(tmp, 'cranklamp_extract')
    extract.run(bundles(g, ['0fdef26068bbfb5471e1d85601e34df1.bundle', '06fe64a087041e406a76f4539374d751.bundle']),
                {"CAB-71edd1c7bb1ddeb20c4ae08cfa2797c8": [425015589523740178],
                 "CAB-8c4acaa2cde04020e6d8730e6224ed50": [-2723685747808342017]}, ext, P)
    anims = os.path.join(tmp, 'cranklamp_anims'); os.makedirs(anims, exist_ok=True)
    env = extract.load(os.path.join(g['ep5'], '0fdef26068bbfb5471e1d85601e34df1.bundle'))
    want = set(ARM_CLIPS)
    for o in env.objects:
        if o.path_id in want and o.type.name == 'AnimationClip':
            json.dump(o.read_typetree(), open(os.path.join(anims, f'AnimationClip_{o.path_id}.json'), 'w', encoding='utf-8'),
                      default=str, ensure_ascii=False)
            want.discard(o.path_id)
    if want: raise RuntimeError('arm clips not found: %s' % sorted(want))
    run_script('cranklamp_build.py', env={'TLD_EP5': g['ep5'], 'TLD_SURV': g['surv'], 'TLD_EXT': ext, 'TLD_ANIMS': anims,
                                          'TLD_OUT': os.path.join(out, 'TLDCrankLamp', 'tldcranklamp.bundle')})
    wb = g['banks']; ad = os.path.join(out, 'TLDCrankLamp', 'audio'); os.makedirs(ad, exist_ok=True)
    st, ev = os.path.join(wb, 'English(US)', '_StoryStructures.bnk'), os.path.join(wb, '_StoryEvents.bnk')
    shutil.copyfile(os.path.join(wb, 'FlashlighCrankLamp.bnk'), os.path.join(ad, 'FlashlighCrankLamp.bnk'))
    shutil.copyfile(ev, os.path.join(ad, '_StoryEvents.bnk'))
    open(os.path.join(ad, '_StoryStructures.bnk'), 'wb').write(crank_banks.structures(st, ev))
    open(os.path.join(ad, 'TLDCrankLamp_Events.bnk'), 'wb').write(crank_banks.events_bank(st, ev))

MODS = [
    ('TLDCoop', 'TLDCoop.dll', build_coop, ['TLDCoop/characters.bin'], ('персонажи для коопа', 'co-op player figures')),
    ('TLDShotgun', 'TLDShotgun.dll', build_shotgun, ['TLDShotgun/tldshotgun.bundle'], ('дробовик', 'shotgun')),
    ('TLDMilitaryStim', 'TLDMilitaryStim.dll', build_syringe, ['TLDMilitaryStim/tldmilitarystim.bundle'], ('военный стимулятор', 'military stim')),
    ('TLDCrankLamp', 'TLDCrankLamp.dll', build_cranklamp,
     ['TLDCrankLamp/tldcranklamp.bundle', 'TLDCrankLamp/audio/FlashlighCrankLamp.bnk', 'TLDCrankLamp/audio/_StoryEvents.bnk',
      'TLDCrankLamp/audio/_StoryStructures.bnk', 'TLDCrankLamp/audio/TLDCrankLamp_Events.bnk'], ('фонарь с ручкой', 'crank lamp')),
]

def main():
    global LOG, RU
    ap = argparse.ArgumentParser()
    ap.add_argument('--game'); ap.add_argument('--out'); ap.add_argument('--all', action='store_true')
    ap.add_argument('--keep-temp', action='store_true'); ap.add_argument('--en', action='store_true')
    ap.add_argument('--no-pause', action='store_true')
    a = ap.parse_args()
    RU = not a.en
    game = find_game(a.game)
    if not game:
        print(T('Не нашёл папку игры The Long Dark.', 'The Long Dark folder not found.'))
        try: game = input(T('Вставьте путь к папке игры (где tld.exe): ', 'Paste the game folder (with tld.exe): ')).strip().strip('"')
        except EOFError: game = ''
        if not is_game(game): print(T('Это не папка игры.', 'Not a game folder.')); return 2
    return build(game, out=a.out, all_=a.all, keep_temp=a.keep_temp)

def build(game, out=None, all_=False, keep_temp=False, only=None):
    """Builds the game-derived files for the installed mods (or `only`, a list of mod names). 0 ok, 1 errors, 3 no ep5."""
    global LOG
    mods_dir = os.path.join(game, 'Mods')
    out_root = os.path.join(out, 'Mods') if out else mods_dir
    os.makedirs(out_root, exist_ok=True)
    LOG = open(os.path.join(out_root, 'TLDAssets.log'), 'w', encoding='utf-8')
    P(f'TLDAssets {VERSION}  {time.strftime("%Y-%m-%d %H:%M:%S")}')
    P(T('Игра: ', 'Game: ') + game)
    g = {'ep5': os.path.join(game, 'tld_dlc', 'episode5', 'tld_Data', 'StreamingAssets', 'aa_ep5', 'StandaloneWindows64'),
         'surv': os.path.join(game, 'tld_Data', 'StreamingAssets', 'aa', 'StandaloneWindows64'),
         'banks': os.path.join(game, 'tld_dlc', 'episode5', 'tld_Data', 'StreamingAssets', 'Audio', 'GeneratedSoundBanks', 'Windows')}
    if not os.path.isdir(g['ep5']):
        P(T('Нет файлов эпизода 5 WINTERMUTE (tld_dlc\\episode5). Запустите в игре WINTERMUTE, эпизод 5, чтобы Steam их скачал, и повторите.',
            'WINTERMUTE episode 5 files (tld_dlc\\episode5) are missing. Start WINTERMUTE episode 5 once so Steam downloads them, then run again.'))
        return 3
    tmp = tempfile.mkdtemp(prefix='tldassets_')
    ok = True
    try:
        for name, dll, fn, files, (ru, en) in MODS:
            if only is not None and name not in only: continue
            if not all_ and not os.path.isfile(os.path.join(mods_dir, dll)):
                P(f'- {T(ru, en)}: ' + T(f'мода {dll} нет, пропускаю', f'{dll} not installed, skipped')); continue
            have = [os.path.join(out_root, f) for f in files]
            if all(os.path.isfile(p) and md5(p) == EXPECTED[f] for p, f in zip(have, files)):
                P(f'- {T(ru, en)}: ' + T('уже собрано', 'already built')); continue
            P(f'- {T(ru, en)}: ' + T('собираю…', 'building…'))
            stage = os.path.join(tmp, 'out_' + name)
            for f in files: os.makedirs(os.path.dirname(os.path.join(stage, f)), exist_ok=True)
            t0 = time.time()
            try:
                fn(g, tmp, stage)
            except Exception:
                ok = False
                P(T('  ОШИБКА, файлы мода не тронуты:', '  ERROR, mod files left as they were:')); P(traceback.format_exc()); continue
            for f in files:
                src, dst = os.path.join(stage, f), os.path.join(out_root, f)
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                shutil.copyfile(src, dst)
                h = md5(dst)
                P(f'  {f}  {os.path.getsize(dst)} B  md5 {h}  ' +
                  (T('совпадает с проверенным', 'matches the checked build') if h == EXPECTED[f] else
                   T('ОТЛИЧАЕТСЯ от проверенного (другая версия игры?)', 'DIFFERS from the checked build (other game version?)')))
            P(T(f'  готово за {time.time() - t0:.0f} с', f'  done in {time.time() - t0:.0f} s'))
    finally:
        if keep_temp: P('temp: ' + tmp)
        else: shutil.rmtree(tmp, ignore_errors=True)
    P(T('Готово.' if ok else 'Готово, но с ошибками (см. выше и Mods\\TLDAssets.log).',
        'Done.' if ok else 'Done with errors (see above and Mods\\TLDAssets.log).'))
    if LOG: LOG.close(); LOG = None
    return 0 if ok else 1

if __name__ == '__main__':
    code = 1
    try: code = main()
    except Exception: traceback.print_exc()
    if getattr(sys, 'frozen', False) and '--no-pause' not in sys.argv:
        try: input('\nEnter…')
        except EOFError: pass
    sys.exit(code)
