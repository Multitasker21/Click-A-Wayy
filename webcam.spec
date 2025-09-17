# -*- mode: python ; coding: utf-8 -*-


a = Analysis(
    ['scripts\\webcam.py'],
    pathex=[],
    binaries=[],
    datas=[('scripts/util.py', '.'), ('hand_env/Lib/site-packages/mediapipe/modules', 'mediapipe/modules'), ('hand_env/Lib/site-packages/mediapipe/python/solutions', 'mediapipe/python/solutions')],
    hiddenimports=['msgpack', 'msgpack_numpy', 'mediapipe.python.solutions.hands', 'mediapipe.python.solutions.drawing_utils'],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.datas,
    [],
    name='webcam',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)
