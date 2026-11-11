# Seagull's mods launcher: a window in the game's own style to choose, install, update and remove the mods.
# The window's frames, buttons and fonts are read from the player's own game (game_ui.py); until then it draws plain shapes.
# Build: pyinstaller --onefile --windowed --name TLDModsLauncher --add-data "steps;steps" --add-data "own;own"
#        --add-data "catalog.json;." --collect-all UnityPy --collect-all fmod_toolkit --collect-all archspec launcher.py
# (without fmod_toolkit and archspec UnityPy cannot decode textures inside the exe)
import os, sys, threading, queue, tempfile, shutil, locale, subprocess, traceback, time
import tkinter as tk
from tkinter import filedialog
from PIL import Image, ImageTk

import launcher_core as core
import tld_assets
import game_ui as ui

def system_ru():
    try:
        import ctypes
        lang = ctypes.windll.kernel32.GetUserDefaultUILanguage() & 0x3FF
        return lang in (0x19, 0x22, 0x23)        # Russian, Ukrainian, Belarusian
    except Exception:
        return (locale.getlocale()[0] or '').lower().startswith(('ru', 'uk', 'be'))

RU = system_ru()
def T(ru, en): return ru if RU else en

W, H = 1060, 780
PX, PY, PW, PH = 30, 18, 1000, 744          # the window panel
L, R = PX + 40, PX + PW - 40                # content edges
LIST_Y, LIST_H = 236, 340                   # the mod list viewport
ROW_W = R - L

def size_txt(n):
    return ('%.1f ' % (n / 1048576) + T('МБ', 'MB')) if n >= 1048576 else ('%d ' % max(1, n // 1024) + T('КБ', 'KB'))

class App:
    def __init__(self, root):
        self.root = root
        self.q = queue.Queue()
        self.busy = False
        self.game = None; self.cat = None; self.manifest = {}
        self.checked = set(); self.scroll = 0; self.hover = None; self.frac = 0.0
        self.logs = []; self.areas = []; self.skin_dir = None
        root.title(T('Моды Seagull для The Long Dark', "Seagull's mods for The Long Dark"))
        root.geometry('%dx%d' % (W, H)); root.resizable(False, False)
        self.cv = tk.Canvas(root, width=W, height=H, highlightthickness=0, background='#1b2024')
        self.cv.pack()
        self.img_id = self.cv.create_image(0, 0, anchor='nw')
        self.cv.bind('<Motion>', self.on_move)
        self.cv.bind('<Leave>', lambda e: self.set_hover(None))
        self.cv.bind('<Button-1>', self.on_click)
        self.cv.bind('<MouseWheel>', lambda e: self.on_wheel(-1 if e.delta > 0 else 1))
        self.cv.bind('<Button-4>', lambda e: self.on_wheel(-1))
        self.cv.bind('<Button-5>', lambda e: self.on_wheel(1))
        self.make_skin()
        self.root.after(80, self.poll)
        self.run(self.load)

    # ---------- skin ----------
    def make_skin(self):
        self.skin = ui.Skin(self.skin_dir, RU)
        bg = ui.scene(W, H)
        p = ui.Painter(self.skin, bg)
        p.spr('vignetteFullscreen', W / 2, H / 2, W + 6, H + 4, (0, 0, 0, 0.6))
        ui.window(p, PX, PY, PW, PH)
        self.base = bg
        self.render()

    # ---------- plumbing ----------
    def say(self, s): self.q.put(('log', s))
    def progress(self, done, total): self.q.put(('prog', done / total if total else 0))
    def poll(self):
        dirty = False
        try:
            while True:
                k, v = self.q.get_nowait()
                if k == 'log':
                    for ln in str(v).splitlines() or ['']: self.logs.append(ln)
                    self.logs = self.logs[-200:]
                elif k == 'prog': self.frac = v
                elif k == 'call': v()
                dirty = True
        except queue.Empty:
            pass
        if dirty: self.render()
        self.root.after(80, self.poll)

    def run(self, fn, *a):
        if self.busy: return
        self.busy = True; self.render()
        def w():
            try: fn(*a)
            except Exception: self.say(traceback.format_exc())
            finally: self.q.put(('call', self.done))
        threading.Thread(target=w, daemon=True).start()

    def done(self):
        self.busy = False; self.frac = 0
        if self.game: self.manifest = core.read_manifest(self.game)
        self.render()

    # ---------- data ----------
    def load(self):
        self.say(T('Загружаю список модов…', 'Loading the mod list…'))
        self.cat = core.load_catalog(self.say)
        if self.cat['_source'] != 'online':
            self.say(T('Нет связи с GitHub, показываю встроенный список.', 'No connection to GitHub, showing the built-in list.'))
        if not self.game: self.game = core.find_game()
        if not self.game:
            self.say(T('Игра не найдена. Нажмите «Другая папка…» и выберите папку игры (где tld.exe).',
                       'Game not found. Press "Other folder…" and choose the game folder (with tld.exe).'))
            return
        self.manifest = core.read_manifest(self.game)
        self.say(T('Модов в списке: %d. Отметьте нужные и нажмите «Установить».',
                   'Mods in the list: %d. Tick the ones you want and press "Install".') % len(self.cat['mods']))
        self.q.put(('call', lambda: None))
        self.load_skin()

    def load_skin(self):
        try:
            d = ui.extract(self.game, lambda s: None)
        except Exception as e:
            d = None; self.say('ui: %s' % e)
        if d:
            self.skin_dir = d
            self.q.put(('call', self.make_skin))

    def pick_game(self):
        d = filedialog.askdirectory(title=T('Папка The Long Dark (где tld.exe)', 'The Long Dark folder (with tld.exe)'))
        if not d: return
        d = os.path.normpath(d)
        if not tld_assets.is_game(d):
            self.say(T('Это не папка игры: ', 'Not a game folder: ') + d); self.render(); return
        self.game = d; self.manifest = core.read_manifest(d)
        if not self.skin_dir: threading.Thread(target=self.load_skin, daemon=True).start()
        self.render()

    def toggle_lang(self):
        global RU
        RU = not RU; tld_assets.RU = RU
        self.root.title(T('Моды Seagull для The Long Dark', "Seagull's mods for The Long Dark"))
        self.make_skin()

    def mods(self):
        return [m for m in (self.cat or {}).get('mods', []) if not m.get('hidden')]

    # ---------- drawing ----------
    def area(self, key, x, y, w, h, fn):
        self.areas.append((key, x, y, w, h, fn))

    def btn(self, p, key, label, cx, cy, w, h, fn, enabled=True, size=15):
        st = 'd' if not enabled else ('h' if self.hover == key else 'n')
        ui.button(p, label, cx, cy, w, h, st, size)
        if enabled: self.area(key, cx - w / 2, cy - h / 2, w, h, fn)

    def row_layout(self, p, m):
        desc = p.wrap(m['desc']['ru' if RU else 'en'], 13, ROW_W - 60)
        extra = []
        if m.get('needs'): extra.append(T('Нужно: ', 'Needs: ') + ', '.join(m['needs']))
        if m.get('assets'): extra.append(T('файлы из сюжета (эпизод 5) соберутся сами', 'story files (episode 5) are built automatically'))
        h = 14 + 22 + len(desc) * 18 + (17 if extra else 0) + 10
        return desc, '   ·   '.join(extra), h

    def render(self):
        im = self.base.copy()
        p = ui.Painter(self.skin, im)
        self.areas = []
        en = not self.busy and bool(self.game)
        ui.title(p, T('МОДЫ SEAGULL', "SEAGULL'S MODS"), W / 2, PY + 46)
        p.text('v' + core.LAUNCHER_VERSION, R, PY + 40, 12, ui.TEAL, al='r')
        # game and loader
        y = PY + 104
        p.text(T('Игра', 'Game'), L, y, 13, ui.TEAL)
        g = self.game or T('не найдена', 'not found')
        maxw = (R - 170 - 75 - 16) - (L + 130)
        while p.tw(g, 15) > maxw and len(g) > 10: g = '…' + g[2:]
        p.text(g, L + 130, y, 15, ui.WHITE if self.game else ui.AMBER)
        self.btn(p, 'game', T('Другая папка…', 'Other folder…'), R - 170, y, 150, 30, self.pick_game, not self.busy, 13)
        self.btn(p, 'lang', 'EN' if RU else 'RU', R - 30, y, 52, 30, self.toggle_lang, not self.busy, 13)
        y += 36
        p.text('MelonLoader', L, y, 13, ui.TEAL)
        if self.game:
            v = core.melonloader_version(self.game)
            if v is None:
                p.text(T('не установлен: без него моды не работают', 'not installed: mods need it'), L + 130, y, 15, ui.AMBER)
                self.btn(p, 'ml', T('Установить', 'Install'), R - 100, y, 170, 30, self.do_ml, en, 13)
            else:
                p.text(v or T('установлен', 'installed'), L + 130, y, 15, ui.GREEN)
        ui.line(p, L, y + 26, ROW_W)
        # toolbar
        y = LIST_Y - 22
        self.btn(p, 'all', T('Отметить все', 'Check all'), L + 75, y, 150, 28, lambda: self.check_all(True), self.cat is not None, 13)
        self.btn(p, 'none', T('Снять все', 'Uncheck all'), L + 235, y, 150, 28, lambda: self.check_all(False), self.cat is not None, 13)
        ch = self.chosen()
        if ch:
            p.text(T('Отмечено: %d, %s', 'Checked: %d, %s') % (len(ch), size_txt(sum(m.get('size', 0) for m in ch))), L + 330, y, 13, ui.DIM)
        # list
        self.draw_list(p)
        # actions
        y = LIST_Y + LIST_H + 34
        ui.line(p, L, y - 20, ROW_W)
        bw = (ROW_W - 30) / 4
        acts = [('inst', T('Установить / обновить', 'Install / update'), self.do_install),
                ('rem', T('Удалить отмеченные', 'Remove checked'), self.do_remove),
                ('assets', T('Собрать файлы из сюжета', 'Rebuild story files'), self.do_assets),
                ('open', T('Папка Mods', 'Mods folder'), self.open_mods)]
        for i, (k, t, fn) in enumerate(acts):
            self.btn(p, k, t, L + bw / 2 + i * (bw + 10), y + 6, bw, 36, fn, en and self.cat is not None, 14)
        ui.progress(p, L, y + 36, ROW_W, self.frac if self.busy else 0)
        # log
        ly = y + 58
        lines = []
        for s in self.logs[-6:]:
            lines += p.wrap(s, 12, ROW_W) or ['']
        for s in lines[-5:]:
            p.text(s, L, ly, 12, ui.GREY, shadow=False); ly += 16
        self.photo = ImageTk.PhotoImage(im.convert('RGB'))
        self.cv.itemconfigure(self.img_id, image=self.photo)

    def draw_list(self, p):
        mods = self.mods()
        if not mods:
            p.text(T('Загружаю…', 'Loading…'), W / 2, LIST_Y + LIST_H / 2, 15, ui.DIM, al='c'); return
        lay = []; total = 0
        for m in mods:
            d, e, h = self.row_layout(p, m); lay.append((m, d, e, h, total)); total += h
        self.scroll = max(0, min(self.scroll, max(0, total - LIST_H)))
        sub = Image.new('RGBA', (ROW_W, LIST_H))
        sp = ui.Painter(self.skin, sub)
        sp.spr('inv_slotBack', ROW_W / 2, LIST_H / 2, ROW_W, LIST_H, (0.17, 0.21, 0.21, 0.44), True,
               fallback=lambda pp, cx, cy, w, h, c: pp.rect(0, 0, w, h, (0.1, 0.12, 0.12, 0.45)))
        for m, desc, extra, h, top in lay:
            y0 = top - self.scroll
            if y0 + h < 0 or y0 > LIST_H: continue
            key = 'row:' + m['id']
            if self.hover == key: sp.rect(0, max(0, y0), ROW_W, h, (0.43, 0.51, 0.49, 0.14))
            st, iv = core.mod_state(self.game, m, self.manifest) if self.game else ('none', None)
            ui.check(sp, 22, y0 + 24, m['id'] in self.checked)
            title = m['title']['ru' if RU else 'en']
            tw = sp.text(title, 44, y0 + 24, 17, ui.WHITE)
            txt, col = {
                'none': (T('не установлен', 'not installed'), ui.DIM),
                'installed': (T('установлен ', 'installed ') + m['version'], ui.GREEN),
                'update': (T('есть обновление: ', 'update: ') + (iv or '?') + ' → ' + m['version'], ui.AMBER),
                'unknown': (T('установлен не через лаунчер, новая ', 'installed by hand, latest ') + m['version'], ui.BLUE),
            }[st]
            sp.text(txt, 44 + tw + 14, y0 + 25, 13, col)
            sp.text(size_txt(m.get('size', 0)), ROW_W - 14, y0 + 24, 13, ui.DIM, al='r')
            yy = y0 + 46
            for ln in desc:
                sp.text(ln, 44, yy, 13, ui.GREY); yy += 18
            if extra: sp.text(extra, 44, yy, 11, ui.TEAL)
            ui.line(sp, 10, y0 + h - 1, ROW_W - 20, 0.35)
            vy0, vy1 = max(0, y0), min(LIST_H, y0 + h)
            if vy1 > vy0:
                self.area(key, L, LIST_Y + vy0, ROW_W, vy1 - vy0, (lambda mid=m['id']: self.toggle(mid)))
        if total > LIST_H:     # scroll bar
            bh = max(30, LIST_H * LIST_H / total); by = (LIST_H - bh) * self.scroll / (total - LIST_H)
            sp.rect(ROW_W - 5, by, 3, bh, (1, 1, 1, 0.35))
        p.im.alpha_composite(sub, (L, LIST_Y))

    # ---------- input ----------
    def hit(self, x, y):
        for k, ax, ay, aw, ah, fn in reversed(self.areas):
            if ax <= x <= ax + aw and ay <= y <= ay + ah: return k, fn
        return None, None
    def set_hover(self, k):
        if k != self.hover: self.hover = k; self.render()
    def on_move(self, e):
        k, _ = self.hit(e.x, e.y); self.set_hover(k)
        self.cv.configure(cursor='hand2' if k else '')
    def on_click(self, e):
        k, fn = self.hit(e.x, e.y)
        if fn: fn()
    def on_wheel(self, d):
        self.scroll += d * 60; self.render()
    def toggle(self, mid):
        if self.busy: return
        self.checked ^= {mid}; self.render()
    def check_all(self, on):
        if self.busy: return
        self.checked = {m['id'] for m in self.mods() if not m.get('extra')} if on else set(); self.render()
    def chosen(self):
        return [m for m in self.mods() if m['id'] in self.checked]

    # ---------- actions ----------
    def check_closed(self):
        if core.game_running():
            self.say(T('Закройте игру и повторите.', 'Close the game and try again.')); return False
        return True

    def do_ml(self):
        if not self.game or not self.check_closed(): return
        def w():
            tmp = tempfile.mkdtemp(prefix='tldmods_')
            try:
                self.say(T('Скачиваю MelonLoader…', 'Downloading MelonLoader…'))
                core.install_melonloader(self.game, self.cat['melonloader'], tmp, self.say, self.progress)
                self.say(T('MelonLoader установлен. Первый запуск игры после него долгий (несколько минут): так и должно быть.',
                           'MelonLoader installed. The first game start after it takes a few minutes: that is expected.'))
            finally: shutil.rmtree(tmp, ignore_errors=True)
        self.run(w)

    def do_install(self):
        mods = self.chosen()
        if not mods: self.say(T('Отметьте моды галочками.', 'Check the mods first.')); self.render(); return
        if not self.check_closed(): self.render(); return
        self.run(self.install, mods)

    def install(self, mods):
        g, cat = self.game, self.cat
        tmp = tempfile.mkdtemp(prefix='tldmods_')
        try:
            if core.melonloader_version(g) is None:
                self.say(T('Сначала ставлю MelonLoader…', 'Installing MelonLoader first…'))
                core.install_melonloader(g, cat['melonloader'], tmp, self.say, self.progress)
            for name, lib in core.needed_libs(cat, mods, g):
                if lib.get('manual'):
                    where = ('https://github.com/' + lib['repo']) if lib.get('repo') else T('из сборки модов, откуда вы его брали', 'from the mod pack you got it from')
                    self.say(T('Нужна библиотека %s: поставьте её сами (%s) в папку Mods.',
                               'Library %s is needed: install it yourself (%s) into Mods.') % (name, where))
                    continue
                self.say(T('Скачиваю библиотеку %s…', 'Downloading library %s…') % name)
                try:
                    core.install_lib(g, name, lib, tmp, self.say, self.progress, cat, self.manifest); core.write_manifest(g, self.manifest)
                except Exception as e: self.say(T('  не вышло: ', '  failed: ') + str(e))
            built = []
            for m in mods:
                title = m['title']['ru' if RU else 'en']
                self.say(T('Скачиваю «%s» (%s)…', 'Downloading "%s" (%s)…') % (title, size_txt(m.get('size', 0))))
                z = os.path.join(tmp, m['zip'])
                try:
                    core.download_any(core.mod_urls(cat, m), z, self.progress, log=self.say)
                except Exception as e:
                    self.say(T('  не скачалось: ', '  download failed: ') + str(e)); continue
                self.say(T('  ставлю…', '  installing…'))
                try:
                    core.install_zip(g, m, z, self.manifest, self.say)
                    core.write_manifest(g, self.manifest)
                except Exception as e:
                    self.say(T('  ошибка установки: ', '  install error: ') + str(e)); continue
                finally:
                    try: os.remove(z)
                    except OSError: pass
                if m.get('assets'): built.append(m['id'])
            if built:
                self.say(T('Собираю файлы из сюжета (около минуты)…', 'Building the story files (about a minute)…'))
                self.q.put(('prog', 0))
                code = core.build_assets(g, built, self.say)
                if code == 3:
                    self.say(T('Чтобы эти моды заработали, скачайте эпизод 5 WINTERMUTE (запустите его один раз в игре) и нажмите «Собрать файлы из сюжета».',
                               'For these mods, download WINTERMUTE episode 5 (start it once in the game), then press "Rebuild story files".'))
            self.say(T('Готово.', 'Done.'))
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

    def do_remove(self):
        mods = [m for m in self.chosen() if core.mod_state(self.game, m, self.manifest)[0] != 'none']
        if not mods: self.say(T('Отметьте установленные моды, которые удалить.', 'Check the installed mods to remove.')); self.render(); return
        if not self.check_closed(): self.render(); return
        def w():
            for m in mods:
                self.say(T('Удаляю «%s»…', 'Removing "%s"…') % m['title']['ru' if RU else 'en'])
                core.uninstall(self.game, m, self.manifest, self.say)
            core.write_manifest(self.game, self.manifest)
            self.say(T('Готово. Сохранения модов не тронуты.', 'Done. The mods\' saves were kept.'))
            self.q.put(('call', lambda: self.checked.clear()))
        self.run(w)

    def do_assets(self):
        if not self.check_closed(): self.render(); return
        ids = [m['id'] for m in self.mods() if m.get('assets')]
        self.run(lambda: core.build_assets(self.game, ids, self.say))

    def open_mods(self):
        p = os.path.join(self.game, 'Mods'); os.makedirs(p, exist_ok=True)
        try: os.startfile(p)
        except Exception: subprocess.Popen(['xdg-open', p])

def main():
    tld_assets.RU = RU
    root = tk.Tk()
    App(root)
    root.mainloop()

if __name__ == '__main__':
    main()
