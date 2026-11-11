# Seagull's mods launcher: a window to choose, install, update and remove the mods.
# Build: pyinstaller --onefile --windowed --name TLDModsLauncher --add-data "steps;steps" --add-data "own;own"
#        --add-data "catalog.json;." --collect-all UnityPy launcher.py
import os, sys, threading, queue, tempfile, shutil, locale, subprocess, traceback, time
import tkinter as tk
from tkinter import ttk, filedialog

import launcher_core as core
import tld_assets

def system_ru():
    try:
        import ctypes
        lang = ctypes.windll.kernel32.GetUserDefaultUILanguage() & 0x3FF
        return lang in (0x19, 0x22, 0x23)        # Russian, Ukrainian, Belarusian
    except Exception:
        return (locale.getlocale()[0] or '').lower().startswith(('ru', 'uk', 'be'))

RU = system_ru()
def T(ru, en): return ru if RU else en

def mb(n): return ('%.1f' % (n / 1048576)) if n >= 1048576 else ('%.0f' % max(1, n / 1024)) + T(' КБ', ' KB')

class App:
    def __init__(self, root):
        self.root = root
        self.q = queue.Queue()
        self.busy = False
        self.game = None
        self.cat = None
        self.manifest = {}
        self.vars = {}
        root.title(T('Моды Seagull для The Long Dark', "Seagull's mods for The Long Dark"))
        root.geometry('900x740'); root.minsize(700, 560)
        try: root.iconbitmap(default='')
        except Exception: pass
        st = ttk.Style()
        try: st.theme_use('vista' if os.name == 'nt' else 'clam')
        except Exception: pass
        st.configure('Title.TLabel', font=('Segoe UI', 11, 'bold'))
        st.configure('Small.TLabel', font=('Segoe UI', 9), foreground='#555')
        st.configure('Ok.TLabel', font=('Segoe UI', 9, 'bold'), foreground='#1f7a36')
        st.configure('Upd.TLabel', font=('Segoe UI', 9, 'bold'), foreground='#b26a00')
        st.configure('No.TLabel', font=('Segoe UI', 9), foreground='#777')

        top = ttk.Frame(root, padding=(12, 10, 12, 4)); top.pack(fill='x')
        self.lbl_game = ttk.Label(top, text='…', wraplength=640); self.lbl_game.grid(row=0, column=0, sticky='w')
        ttk.Button(top, text=T('Другая папка…', 'Other folder…'), command=self.pick_game).grid(row=0, column=1, padx=6)
        self.btn_lang = ttk.Button(top, text='EN' if RU else 'RU', width=4, command=self.toggle_lang)
        self.btn_lang.grid(row=0, column=2)
        self.lbl_ml = ttk.Label(top, text=''); self.lbl_ml.grid(row=1, column=0, sticky='w', pady=(4, 0))
        self.btn_ml = ttk.Button(top, text=T('Установить MelonLoader', 'Install MelonLoader'), command=self.do_ml)
        top.columnconfigure(0, weight=1)

        mid = ttk.Frame(root, padding=(12, 4)); mid.pack(fill='both', expand=True)
        self.canvas = tk.Canvas(mid, highlightthickness=0, background='white')
        sb = ttk.Scrollbar(mid, orient='vertical', command=self.canvas.yview)
        self.list = tk.Frame(self.canvas, background='white')
        self.list.bind('<Configure>', lambda e: self.canvas.configure(scrollregion=self.canvas.bbox('all')))
        self.win = self.canvas.create_window((0, 0), window=self.list, anchor='nw')
        self.canvas.bind('<Configure>', lambda e: self.canvas.itemconfigure(self.win, width=e.width))
        self.canvas.configure(yscrollcommand=sb.set)
        self.canvas.pack(side='left', fill='both', expand=True); sb.pack(side='right', fill='y')
        self.canvas.bind_all('<MouseWheel>', lambda e: self.canvas.yview_scroll(int(-e.delta / 120), 'units'))

        bar = ttk.Frame(root, padding=(12, 6)); bar.pack(fill='x')
        self.btn_install = ttk.Button(bar, text=T('Установить / обновить отмеченные', 'Install / update checked'), command=self.do_install)
        self.btn_remove = ttk.Button(bar, text=T('Удалить отмеченные', 'Remove checked'), command=self.do_remove)
        self.btn_assets = ttk.Button(bar, text=T('Собрать файлы из сюжета заново', 'Rebuild story files'), command=self.do_assets)
        self.btn_open = ttk.Button(bar, text=T('Папка Mods', 'Mods folder'), command=self.open_mods)
        for i, b in enumerate((self.btn_install, self.btn_remove, self.btn_assets, self.btn_open)): b.grid(row=0, column=i, padx=(0, 6))
        sel = ttk.Frame(root, padding=(12, 0)); sel.pack(fill='x', before=mid)
        ttk.Button(sel, text=T('Отметить все', 'Check all'), command=lambda: self.check_all(True)).pack(side='left')
        ttk.Button(sel, text=T('Снять все', 'Uncheck all'), command=lambda: self.check_all(False)).pack(side='left', padx=6)
        self.lbl_sel = ttk.Label(sel, text='', style='Small.TLabel'); self.lbl_sel.pack(side='left', padx=8)

        bot = ttk.Frame(root, padding=(12, 0, 12, 10)); bot.pack(fill='x')
        self.prog = ttk.Progressbar(bot, mode='determinate'); self.prog.pack(fill='x')
        self.log = tk.Text(bot, height=9, wrap='word', font=('Consolas', 9), state='disabled', background='#f7f7f5')
        self.log.pack(fill='x', pady=(6, 0))

        self.root.after(100, self.poll)
        self.run(self.load)

    # ---------- plumbing ----------
    def say(self, s): self.q.put(('log', s))
    def progress(self, done, total): self.q.put(('prog', (done, total)))
    def poll(self):
        try:
            while True:
                k, v = self.q.get_nowait()
                if k == 'log':
                    self.log.configure(state='normal'); self.log.insert('end', v + '\n'); self.log.see('end'); self.log.configure(state='disabled')
                elif k == 'prog':
                    d, t = v
                    if t: self.prog.configure(mode='determinate', maximum=t, value=d)
                elif k == 'call': v()
        except queue.Empty:
            pass
        self.root.after(80, self.poll)

    def run(self, fn, *a):
        if self.busy: return
        self.busy = True; self.set_buttons()
        def w():
            try: fn(*a)
            except Exception:
                self.say(traceback.format_exc())
            finally:
                self.q.put(('call', self.done))
        threading.Thread(target=w, daemon=True).start()

    def done(self):
        self.busy = False; self.prog.configure(value=0)
        self.refresh(); self.set_buttons()

    def set_buttons(self):
        s = 'disabled' if self.busy or not self.game else 'normal'
        for b in (self.btn_install, self.btn_remove, self.btn_assets, self.btn_open, self.btn_ml): b.configure(state=s)

    # ---------- data ----------
    def load(self):
        self.say(T('Загружаю список модов…', 'Loading the mod list…'))
        self.cat = core.load_catalog(self.say)
        if self.cat['_source'] != 'online':
            self.say(T('Нет связи с GitHub, показываю встроенный список.', 'No connection to GitHub, showing the built-in list.'))
        if not self.game:
            self.game = core.find_game()
        if not self.game:
            self.say(T('Игра не найдена. Нажмите «Другая папка…» и выберите папку игры (где tld.exe).',
                       'Game not found. Press "Other folder…" and choose the game folder (with tld.exe).'))

    def pick_game(self):
        d = filedialog.askdirectory(title=T('Папка The Long Dark (где tld.exe)', 'The Long Dark folder (with tld.exe)'))
        if not d: return
        d = os.path.normpath(d)
        if not tld_assets.is_game(d):
            self.say(T('Это не папка игры: ', 'Not a game folder: ') + d); return
        self.game = d; self.refresh(); self.set_buttons()

    def toggle_lang(self):
        global RU
        RU = not RU
        tld_assets.RU = RU
        for w in self.root.winfo_children(): w.destroy()
        game, cat = self.game, self.cat
        self.__init__(self.root)
        self.game, self.cat = game, cat

    def refresh(self):
        g = self.game
        if g:
            self.lbl_game.configure(text=T('Игра: ', 'Game: ') + g)
            self.manifest = core.read_manifest(g)
            v = core.melonloader_version(g)
            if v is None:
                self.lbl_ml.configure(text=T('MelonLoader не установлен: без него моды не работают.', 'MelonLoader is not installed: mods need it.'),
                                      style='Upd.TLabel')
                self.btn_ml.grid(row=1, column=1, padx=6, pady=(4, 0))
            else:
                self.lbl_ml.configure(text='MelonLoader ' + (v or T('установлен', 'installed')), style='Ok.TLabel')
                self.btn_ml.grid_remove()
        else:
            self.lbl_game.configure(text=T('Игра не найдена', 'Game not found'))
        self.draw_list()

    def draw_list(self):
        for w in self.list.winfo_children(): w.destroy()
        if not self.cat: return
        old = {k: v.get() for k, v in self.vars.items()}
        self.vars = {}
        bg = 'white'
        for i, m in enumerate(self.cat['mods']):
            if m.get('hidden'): continue
            st, iv = core.mod_state(self.game, m, self.manifest) if self.game else ('none', None)
            row = tk.Frame(self.list, background=bg, padx=6, pady=6)
            row.pack(fill='x')
            if i: ttk.Separator(self.list).pack(fill='x', before=row)
            var = tk.BooleanVar(value=old.get(m['id'], st == 'update'))
            var.trace_add('write', lambda *a: self.update_sel())
            self.vars[m['id']] = var
            tk.Checkbutton(row, variable=var, background=bg, activebackground=bg).grid(row=0, column=0, rowspan=3, sticky='n')
            title = m['title']['ru' if RU else 'en']
            tk.Label(row, text=title, font=('Segoe UI', 11, 'bold'), background=bg).grid(row=0, column=1, sticky='w')
            txt, colr = {
                'none': (T('не установлен', 'not installed'), '#777'),
                'installed': (T('установлен ', 'installed ') + m['version'], '#1f7a36'),
                'update': (T('есть обновление: ', 'update: ') + (iv or '?') + ' → ' + m['version'], '#b26a00'),
                'unknown': (T('установлен (не через лаунчер), новая ', 'installed (not by the launcher), latest ') + m['version'], '#1f5fa8'),
            }[st]
            tk.Label(row, text=txt, font=('Segoe UI', 9, 'bold'), foreground=colr, background=bg).grid(row=0, column=2, sticky='w', padx=10)
            tk.Label(row, text=mb(m.get('size', 0)) + (T(' МБ', ' MB') if m.get('size', 0) >= 1048576 else ''), font=('Segoe UI', 9),
                     foreground='#777', background=bg).grid(row=0, column=3, sticky='e')
            desc = m['desc']['ru' if RU else 'en']
            tk.Label(row, text=desc, wraplength=700, justify='left', background=bg, font=('Segoe UI', 9)).grid(row=1, column=1, columnspan=3, sticky='w')
            extra = []
            if m.get('needs'): extra.append(T('Нужно: ', 'Needs: ') + ', '.join(m['needs']))
            if m.get('assets'): extra.append(T('Файлы из сюжета (эпизод 5) соберутся сами', 'Story files (episode 5) are built automatically'))
            if extra:
                tk.Label(row, text='   ·   '.join(extra), background=bg, foreground='#555', font=('Segoe UI', 8)).grid(row=2, column=1, columnspan=3, sticky='w')
            row.columnconfigure(1, weight=0); row.columnconfigure(3, weight=1)
        self.update_sel()

    def check_all(self, on):
        for m in self.cat['mods'] if self.cat else []:
            if m['id'] in self.vars: self.vars[m['id']].set(on and not m.get('extra'))

    def chosen(self):
        return [m for m in self.cat['mods'] if self.vars.get(m['id']) and self.vars[m['id']].get()]

    def update_sel(self):
        ch = self.chosen() if self.cat else []
        size = sum(m.get('size', 0) for m in ch)
        self.lbl_sel.configure(text=(T('Отмечено: %d, %s МБ', 'Checked: %d, %s MB') % (len(ch), '%.0f' % (size / 1048576))) if ch else '')

    # ---------- actions ----------
    def check_closed(self):
        if core.game_running():
            self.say(T('Закройте игру и повторите.', 'Close the game and try again.'))
            return False
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
        if not mods: self.say(T('Отметьте моды галочками.', 'Check the mods first.')); return
        if not self.check_closed(): return
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
                try: core.install_lib(g, name, lib, tmp, self.say, self.progress)
                except Exception as e: self.say(T('  не вышло: ', '  failed: ') + str(e))
            built = []
            for m in mods:
                title = m['title']['ru' if RU else 'en']
                self.say(T('Скачиваю «%s» (%s МБ)…', 'Downloading "%s" (%s MB)…') % (title, '%.1f' % (m.get('size', 0) / 1048576)))
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
                self.say(T('Собираю файлы из сюжета…', 'Building the story files…'))
                self.q.put(('prog', (0, 0)))
                code = core.build_assets(g, built, self.say)
                if code == 3:
                    self.say(T('Чтобы эти моды заработали, скачайте эпизод 5 WINTERMUTE (запустите его один раз в игре) и нажмите «Собрать файлы из сюжета заново».',
                               'For these mods, download WINTERMUTE episode 5 (start it once in the game), then press "Rebuild story files".'))
            self.say(T('Готово.', 'Done.'))
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

    def do_remove(self):
        mods = [m for m in self.chosen() if core.mod_state(self.game, m, self.manifest)[0] != 'none']
        if not mods: self.say(T('Отметьте установленные моды, которые удалить.', 'Check the installed mods to remove.')); return
        if not self.check_closed(): return
        def w():
            for m in mods:
                self.say(T('Удаляю «%s»…', 'Removing "%s"…') % m['title']['ru' if RU else 'en'])
                core.uninstall(self.game, m, self.manifest, self.say)
            core.write_manifest(self.game, self.manifest)
            self.say(T('Готово. Сохранения модов не тронуты.', 'Done. The mods\' saves were kept.'))
            self.q.put(('call', lambda: [v.set(False) for v in self.vars.values()]))
        self.run(w)

    def do_assets(self):
        if not self.check_closed(): return
        ids = [m['id'] for m in self.cat['mods'] if m.get('assets')]
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
