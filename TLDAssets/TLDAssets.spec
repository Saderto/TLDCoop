# -*- mode: python ; coding: utf-8 -*-
# Build: pyinstaller TLDAssets.spec  (from this folder; versions in requirements-build.txt).
# The release exe is built by .github/workflows/build-exe.yml from this public source. No UPX packing
# (packed exes look suspicious to antivirus programs).
from PyInstaller.utils.hooks import collect_all

datas = [('steps', 'steps'), ('own', 'own')]
binaries = []
hiddenimports = []
for pkg in ('UnityPy', 'fmod_toolkit', 'archspec'):
    d, b, h = collect_all(pkg)
    datas += d; binaries += b; hiddenimports += h

a = Analysis(['tld_assets.py'], pathex=[], binaries=binaries, datas=datas, hiddenimports=hiddenimports,
             hookspath=[], hooksconfig={}, runtime_hooks=[], excludes=[], noarchive=False, optimize=0)
pyz = PYZ(a.pure)
exe = EXE(
    pyz, a.scripts, a.binaries, a.datas, [],
    name='TLDAssets',
    debug=False, bootloader_ignore_signals=False, strip=False,
    upx=False,
    runtime_tmpdir=None,
    console=True,
    disable_windowed_traceback=False, argv_emulation=False, target_arch=None,
    codesign_identity=None, entitlements_file=None,

)
