# EMPEROR's mods launcher: logic without the window (the window is launcher.py).
# Finds the game, reads the mod list, downloads the chosen mods from the GitHub release of Saderto/TLDCoop,
# installs/removes them, fetches the libraries they need from their authors' own GitHub releases,
# and builds the game-derived files (tld_assets.build) for mods that need them.
import os, sys, json, zipfile, shutil, tempfile, urllib.request, urllib.error, urllib.parse, subprocess, hashlib, time

import tld_assets

HERE = getattr(sys, '_MEIPASS', os.path.dirname(os.path.abspath(__file__)))
LAUNCHER_VERSION = '1.0.6'
DISCORD_URL = 'https://discord.gg/ezT7PRZmuS'
CATALOG_URL = 'https://raw.githubusercontent.com/Saderto/TLDCoop/main/TLDAssets/catalog.json'
MANIFEST = 'Mods/EMPERORMods.json'   # what the launcher installed: {id: {"version": .., "files": [..]}}
OLD_MANIFEST = 'Mods/SeagullMods.json'   # its name before 1.0.6 (the mods were renamed ...BySeagull/TLD... -> ...ByEMPEROR)
UA = 'EMPERORModsLauncher/' + LAUNCHER_VERSION

def http(url, timeout=30):
    req = urllib.request.Request(url, headers={'User-Agent': UA, 'Accept': '*/*'})
    return urllib.request.urlopen(req, timeout=timeout)

# ---------------- catalog ----------------
def load_catalog(log=lambda s: None):
    """The list from GitHub (so new versions show up without a new launcher); the built-in copy if offline."""
    try:
        with http(CATALOG_URL, 15) as r:
            cat = json.loads(r.read().decode('utf-8'))
        cat['_source'] = 'online'
        return cat
    except Exception as e:
        log('catalog online: %s' % e)
    with open(os.path.join(HERE, 'catalog.json'), encoding='utf-8') as f:
        cat = json.load(f)
    cat['_source'] = 'builtin'
    return cat

# ---------------- game state ----------------
def find_game():
    return tld_assets.find_game(None)

def melonloader_version(game):
    """'0.7.3' etc., '' if installed but unknown, None if not installed."""
    ml = os.path.join(game, 'MelonLoader')
    if not os.path.isdir(ml) or not os.path.isfile(os.path.join(game, 'version.dll')):
        return None
    for p in (os.path.join(ml, 'net6', 'MelonLoader.dll'), os.path.join(ml, 'MelonLoader.dll')):
        if os.path.isfile(p):
            v = file_version(p)
            if v: return v
    return ''

def file_version(path):
    """ProductVersion from the PE version resource (Windows); '' elsewhere."""
    try:
        import ctypes
        from ctypes import wintypes
        ver = ctypes.WinDLL('version')
        size = ver.GetFileVersionInfoSizeW(path, None)
        if not size: return ''
        buf = ctypes.create_string_buffer(size)
        ver.GetFileVersionInfoW(path, 0, size, buf)
        p = ctypes.c_void_p(); n = wintypes.UINT()
        if ver.VerQueryValueW(buf, '\\', ctypes.byref(p), ctypes.byref(n)) and n.value:
            ffi = ctypes.cast(p, ctypes.POINTER(ctypes.c_uint32 * 13)).contents
            ms, ls = ffi[2], ffi[3]
            return '%d.%d.%d' % (ms >> 16, ms & 0xFFFF, ls >> 16)
    except Exception:
        pass
    return ''

def game_running():
    """True if tld.exe runs. Checks only this process name."""
    if os.name != 'nt': return False
    try:
        out = subprocess.run(['tasklist', '/FI', 'IMAGENAME eq tld.exe', '/FO', 'CSV', '/NH'], capture_output=True,
                             text=True, creationflags=0x08000000).stdout
        return 'tld.exe' in out.lower()
    except Exception:
        return False

def read_manifest(game):
    for name in (MANIFEST, OLD_MANIFEST):
        try:
            with open(os.path.join(game, name), encoding='utf-8') as f: return json.load(f)
        except Exception:
            pass
    return {}

def write_manifest(game, m):
    p = os.path.join(game, MANIFEST)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    tmp = p + '.tmp'
    with open(tmp, 'w', encoding='utf-8') as f: json.dump(m, f, ensure_ascii=False, indent=1)
    os.replace(tmp, p)
    try:
        old = os.path.join(game, OLD_MANIFEST)
        if os.path.isfile(old): os.remove(old)
    except OSError:
        pass

def old_dll(mod):
    """The mod's dll under its name before the rename (catalog 'legacy': {'dll': ..}), or None."""
    return (mod.get('legacy') or {}).get('dll')

def mod_state(game, mod, manifest):
    """('none'|'installed'|'update'|'unknown', installed_version)."""
    present = os.path.isfile(os.path.join(game, mod['detect']))
    old = old_dll(mod)
    renamed = bool(old) and os.path.isfile(os.path.join(game, old))   # still the version with the old name
    rec = manifest.get(mod['id'])
    if not present and not renamed: return 'none', None
    if not rec: return 'unknown', None
    if renamed and not present: return 'update', rec.get('version')
    return ('installed' if rec.get('version') == mod['version'] else 'update'), rec.get('version')

def lib_present(game, lib):
    return os.path.isfile(os.path.join(game, lib['path']))

_LATEST = {}
class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k): return None

def _tag_by_redirect(repo, asset):
    """github.com/<repo>/releases/latest/download/<asset> answers with a redirect that names the tag (no API limit)."""
    url = 'https://github.com/%s/releases/latest/download/%s' % (repo, asset)
    req = urllib.request.Request(url, headers={'User-Agent': UA}, method='HEAD')
    try:
        urllib.request.build_opener(_NoRedirect).open(req, timeout=20)
    except urllib.error.HTTPError as e:
        loc = e.headers.get('Location') or ''
        mark = '/releases/download/'
        if e.code in (301, 302, 303, 307, 308) and mark in loc:
            tag = loc.split(mark, 1)[1].split('/', 1)[0]
            return {'tag': urllib.parse.unquote(tag), 'assets': {asset: loc}}
    except Exception:
        pass
    return None

def latest_release(repo, asset=None):
    """{'tag':.., 'assets': {name: url}} of the author's latest GitHub release, None if GitHub can't be reached."""
    if repo not in _LATEST:
        rel = None
        try:
            with http('https://api.github.com/repos/%s/releases/latest' % repo, 20) as r:
                j = json.loads(r.read().decode('utf-8'))
            rel = {'tag': j.get('tag_name') or '',
                   'assets': {a.get('name'): a.get('browser_download_url') for a in j.get('assets', [])}}
        except Exception:
            pass
        if rel is None and asset: rel = _tag_by_redirect(repo, asset)
        _LATEST[repo] = rel
    return _LATEST[repo]

def lib_version(lib):
    """The version the launcher would install now: ours from the catalog, an author's = their latest release tag."""
    if lib.get('zip'): return lib.get('version') or lib.get('note', '')
    rel = latest_release(lib['repo'], lib.get('asset'))
    return rel['tag'] if rel else None

def mine(manifest, name, lib):
    """The manifest record if the launcher put this library there (zip libs from launcher 1.0 have no 'lib' mark yet)."""
    rec = manifest.get(name)
    if rec and (rec.get('lib') or (lib.get('zip') and rec.get('zip') == lib['zip'])): return rec
    return None

def lib_state(game, name, lib, manifest):
    """'none' | 'ok' | 'update' | 'hand' (put there by hand: the launcher leaves it alone)."""
    if not lib_present(game, lib): return 'none'
    rec = mine(manifest, name, lib)
    if not rec: return 'hand'
    v = lib_version(lib)
    if v is None: return 'ok'                  # offline: can't tell
    return 'ok' if rec.get('version') == v else 'update'

def lib_updates(cat, game, manifest):
    """Libraries the launcher installed earlier that have a newer version now: [(name, lib, old, new)]."""
    out = []
    for n, lib in cat.get('libs', {}).items():
        if lib.get('manual'): continue
        rec = mine(manifest, n, lib)
        if rec and lib_state(game, n, lib, manifest) == 'update':
            out.append((n, lib, rec.get('version'), lib_version(lib)))
    return out

# ---------------- downloading ----------------
def download(url, dest, progress=None, cancel=None):
    """progress(done, total). Writes to dest.part then renames."""
    part = dest + '.part'
    with http(url, 60) as r:
        total = int(r.headers.get('Content-Length') or 0)
        done = 0
        with open(part, 'wb') as f:
            while True:
                if cancel and cancel(): raise RuntimeError('cancelled')
                b = r.read(1 << 18)
                if not b: break
                f.write(b); done += len(b)
                if progress: progress(done, total)
    os.replace(part, dest)
    return dest

def release_asset_url(repo, asset, tag=None):
    """Direct download URL of a release asset: the given tag, else the latest release (asked through the API)."""
    if tag:
        return 'https://github.com/%s/releases/download/%s/%s' % (repo, tag, asset)
    try:
        with http('https://api.github.com/repos/%s/releases/latest' % repo, 20) as r:
            rel = json.loads(r.read().decode('utf-8'))
        for a in rel.get('assets', []):
            if a.get('name') == asset: return a['browser_download_url']
    except Exception:
        pass
    return 'https://github.com/%s/releases/latest/download/%s' % (repo, asset)

def mod_urls(cat, mod):
    """The tag named in the catalog first, then the latest release."""
    tag = mod.get('tag') or cat.get('tag')
    urls = []
    if tag: urls.append(release_asset_url(cat['repo'], mod['zip'], tag))
    urls.append('https://github.com/%s/releases/latest/download/%s' % (cat['repo'], mod['zip']))
    return urls

def download_any(urls, dest, progress=None, cancel=None, log=lambda s: None):
    last = None
    for u in urls:
        try:
            return download(u, dest, progress, cancel)
        except RuntimeError:
            raise
        except Exception as e:
            last = e; log('  %s: %s' % (u, e))
    raise RuntimeError('download failed: %s' % last)

# ---------------- installing ----------------
SKIP_PARTS = ('saves/', 'logs/')

def _move(src, dst, overwrite):
    """Moves a file; if dst exists: overwrite=True replaces it, False drops src (the new copy wins)."""
    if os.path.exists(dst):
        if not overwrite:
            os.remove(src); return
        os.remove(dst)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    try:
        os.replace(src, dst)
    except OSError:
        shutil.move(src, dst)

def migrate_legacy(game, mod, log=lambda s: None):
    """The mods were renamed (11.2026): ...BySeagull / TLD... -> ...ByEMPEROR. Before installing the new version, the
    player's files are carried over from the old folder (saves, settings, built story files), files that changed name
    are renamed, and the old dll is removed so the mod is not loaded twice. Catalog 'legacy':
      dir     old folder in Mods, moved into the new one (= 'dir_to'), files already there stay
      files   {old relative name: new name, '' = delete} inside that folder
      moves   {old path: new path} from the game folder, the old file wins (settings the player made)
      remove  old files to delete (the old dll and its plugins)
    The new dll does the same on its first start (mods installed by hand); here it runs first so the story files are
    found by their new names and are not built again."""
    lg = mod.get('legacy')
    if not lg: return
    n = 0
    try:
        od = os.path.join(game, 'Mods', lg['dir']) if lg.get('dir') else None
        if od and os.path.isdir(od):
            nd = os.path.join(game, 'Mods', lg['dir_to'])
            ren = lg.get('files', {})
            for root, dirs, files in os.walk(od):
                for f in files:
                    src = os.path.join(root, f)
                    rel = os.path.relpath(src, od).replace('\\', '/')
                    to = ren.get(rel, rel)
                    if to == '':
                        os.remove(src); n += 1; continue
                    _move(src, os.path.join(nd, *to.split('/')), overwrite=False); n += 1
            for root, dirs, files in sorted(os.walk(od), key=lambda x: len(x[0]), reverse=True):
                try: os.rmdir(root)
                except OSError: pass
        for o, to in lg.get('moves', {}).items():
            src = os.path.join(game, *o.split('/'))
            if not os.path.isfile(src): continue
            if to == '': os.remove(src)
            else: _move(src, os.path.join(game, *to.split('/')), overwrite=True)
            n += 1
        for o in ([lg['dll']] if lg.get('dll') else []) + lg.get('remove', []):
            p = os.path.join(game, *o.split('/'))
            if os.path.isfile(p): os.remove(p); n += 1
    except Exception as e:
        log('  %s: %s' % ('old name', e))
    if n: log('  ' + ('перенёс со старого имени: %d файлов' if tld_assets.RU else 'carried over from the old name: %d files') % n)

def target_of(mod, name):
    """Where a zip entry goes, relative to the game folder (forward slashes), or None to skip it."""
    n = name.replace('\\', '/')
    if n.endswith('/'): return None
    strip = mod.get('strip')
    if strip:
        if not n.startswith(strip): return None
        n = n[len(strip):]
        if not (n.startswith('Mods/') or n.startswith('Plugins/') or n.startswith('UserLibs/')):
            n = mod.get('other_to', '') + n
    if n.startswith('/') or '..' in n.split('/'): return None
    if any(s in n for s in SKIP_PARTS): return None
    return n

def install_zip(game, mod, zpath, manifest, log=lambda s: None):
    """Extracts the mod; keeps a list of its files for removal. Old files of the mod that the new version
    no longer has are removed (player data such as saves is never in the list)."""
    migrate_legacy(game, mod, log)
    files = []
    keep = set(mod.get('keep', []))     # the player's own settings: only written when missing
    with zipfile.ZipFile(zpath) as z:
        for info in z.infolist():
            rel = target_of(mod, info.filename)
            if not rel: continue
            dst = os.path.join(game, *rel.split('/'))
            if rel in keep and os.path.isfile(dst): continue
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            tmp = dst + '.new'
            with z.open(info) as src, open(tmp, 'wb') as out: shutil.copyfileobj(src, out, 1 << 20)
            os.replace(tmp, dst)
            if rel not in keep: files.append(rel)      # kept files are not in the list: removing the mod leaves them
    old = manifest.get(mod['id'], {}).get('files', [])
    for rel in set(old) - set(files):
        p = os.path.join(game, *rel.split('/'))
        try:
            if os.path.isfile(p): os.remove(p)
        except OSError: pass
    manifest[mod['id']] = {'version': mod['version'], 'files': sorted(files), 'time': time.strftime('%Y-%m-%d %H:%M')}
    log('  %d files' % len(files))
    return files

def uninstall(game, mod, manifest, log=lambda s: None):
    """Removes the files the launcher installed (or, for a mod installed by hand, only its dll).
    Saves and settings the mod wrote itself stay."""
    rec = manifest.get(mod['id'])
    files = list(rec['files'] if rec else [mod['detect']])
    if old_dll(mod): files.append(old_dll(mod))
    # files built from the game by TLDAssets for this mod
    for name, dll, fn, gen, _ in tld_assets.MODS:
        if name == mod['id']: files += ['Mods/' + f for f in gen]
    # a file another installed mod also brought (TLDAssets.exe) stays
    shared = {f for k, r in manifest.items() if k != mod['id'] for f in r.get('files', [])}
    files = [f for f in files if f not in shared]
    removed = 0
    dirs = set()
    for rel in files:
        p = os.path.join(game, *rel.split('/'))
        try:
            if os.path.isfile(p): os.remove(p); removed += 1
        except OSError as e:
            log('  %s: %s' % (rel, e))
        d = os.path.dirname(p)
        while d and os.path.normpath(d) != os.path.normpath(game) and os.path.basename(d) not in ('Mods', 'Plugins', 'UserLibs'):
            dirs.add(d); d = os.path.dirname(d)
    for d in sorted(dirs, key=len, reverse=True):
        try: os.rmdir(d)          # only if empty
        except OSError: pass
    manifest.pop(mod['id'], None)
    log('  %d files removed' % removed)

def install_lib(game, name, lib, tmpdir, log=lambda s: None, progress=None, cat=None, manifest=None):
    man = manifest if manifest is not None else {}
    if lib.get('zip'):          # a library we ship ourselves (MIT, authors kept in Licenses/)
        z = os.path.join(tmpdir, lib['zip'])
        download_any(mod_urls(cat, lib), z, progress, log=log)
        m = {'id': name, 'version': lib_version(lib), 'zip': lib['zip']}
        install_zip(game, m, z, man, log)
        man[name]['lib'] = True
        return
    rel = latest_release(lib['repo'], lib.get('asset'))
    url = (rel or {}).get('assets', {}).get(lib['asset']) or release_asset_url(lib['repo'], lib['asset'])
    dst = os.path.join(game, *lib['path'].split('/'))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    tmp = os.path.join(tmpdir, lib['asset'])
    download(url, tmp, progress)
    os.replace(tmp, dst) if os.path.splitdrive(tmp)[0] == os.path.splitdrive(dst)[0] else shutil.move(tmp, dst)
    man[name] = {'version': rel['tag'] if rel else '', 'files': [lib['path']], 'lib': True,
                 'time': time.strftime('%Y-%m-%d %H:%M')}
    log('  %s -> %s' % (url, lib['path']))

def install_melonloader(game, ml, tmpdir, log=lambda s: None, progress=None):
    url = release_asset_url(ml['repo'], ml['asset'], ml.get('tag'))
    z = os.path.join(tmpdir, ml['asset'])
    download(url, z, progress)
    with zipfile.ZipFile(z) as zf:
        for info in zf.infolist():
            n = info.filename.replace('\\', '/')
            if n.endswith('/') or n.startswith('/') or '..' in n.split('/'): continue
            dst = os.path.join(game, *n.split('/'))
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            with zf.open(info) as s, open(dst, 'wb') as o: shutil.copyfileobj(s, o)
    log('  MelonLoader %s' % ml.get('tag', ''))

def needed_libs(cat, mods, game, manifest=None):
    """Libraries the chosen mods need that are not in the game yet, plus updates of ones the launcher put there: [(name, lib)]."""
    out = [(n, lib) for n, lib, _, _ in lib_updates(cat, game, manifest)] if manifest is not None else []
    for m in mods:
        for n in m.get('needs', []):
            lib = cat['libs'].get(n)
            if lib and not lib_present(game, lib) and n not in [x[0] for x in out]:
                out.append((n, lib))
    return out

def build_assets(game, mod_ids, log):
    tld_assets.RU = True if tld_assets.RU is None else tld_assets.RU
    tld_assets.SINK = log
    try:
        return tld_assets.build(game, only=list(mod_ids))
    finally:
        tld_assets.SINK = None
