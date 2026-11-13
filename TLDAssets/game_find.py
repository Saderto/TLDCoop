# Finds The Long Dark on this PC (next to the launcher, or in the Steam libraries) and holds the UI language.
import os, sys

RU = None
def T(ru, en): return ru if RU else en

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
