# -*- mode: python ; coding: utf-8 -*-
# Build: pyinstaller TLDModsLauncher.spec  (from this folder; versions in requirements-build.txt).
# The release exe is built by .github/workflows/build-exe.yml from this public source. No UPX packing
# (packed exes look suspicious to antivirus programs).

datas = [('catalog.json', '.'), ('icon.ico', '.'), ('icon.png', '.')]
binaries = []
hiddenimports = []

a = Analysis(['launcher.py'], pathex=[], binaries=binaries, datas=datas, hiddenimports=hiddenimports,
             hookspath=[], hooksconfig={}, runtime_hooks=[], excludes=[], noarchive=False, optimize=0)
pyz = PYZ(a.pure)
exe = EXE(
    pyz, a.scripts, a.binaries, a.datas, [],
    name='TLDModsLauncher',
    debug=False, bootloader_ignore_signals=False, strip=False,
    upx=False,
    runtime_tmpdir=None,
    console=False,
    disable_windowed_traceback=False, argv_emulation=False, target_arch=None,
    codesign_identity=None, entitlements_file=None,
    icon=['icon.ico'],
)
