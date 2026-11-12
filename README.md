# The Long Dark: моды EMPEROR / EMPEROR's mods

Моды для The Long Dark (MelonLoader 0.7, игра 2.60). Все бесплатные.
Mods for The Long Dark (MelonLoader 0.7, game 2.60). All free.

**Discord:** https://discord.gg/ezT7PRZmuS — вопросы, баги, поиск напарника / questions, bugs, finding a partner.

**Проще всего / Easiest:** скачайте [TLDModsLauncher.exe](https://github.com/Saderto/TLDCoop/releases/latest/download/TLDModsLauncher.exe),
запустите, отметьте нужные моды и нажмите «Установить». Он сам поставит MelonLoader, библиотеки и соберёт файлы из сюжета.
Download the launcher, tick the mods and press "Install"; it sets up MelonLoader, the libraries and the story files itself.

До 11.2026 моды назывались по-старому (`TLDCoop`, `TLDShotgun`, `TLDMilitaryStim`, `TLDCrankLamp`, `TLDTrainer`, `...BySeagull`).
Новая версия сама переносит сохранения и настройки из старой папки при первом запуске и убирает старый dll; лаунчер 1.0.6
делает это при обновлении. Сохранения в игре не теряются: их ключи остались прежними.
Until 11.2026 the mods had their old names (`TLDCoop`, `TLDShotgun`, `TLDMilitaryStim`, `TLDCrankLamp`, `TLDTrainer`, `...BySeagull`).
The new version moves your saves and settings from the old folder on its first start and puts the old dll aside; launcher 1.0.6
does it when updating. Game saves are kept: their keys did not change.

| Папка / Folder | Что это / What it is |
|---|---|
| `CoopByEMPEROR` | Кооператив через Steam: общий мир хоста, звери, предметы, костры, погода, время / Co-op over Steam: the host's shared world, animals, items, fires, weather, time |
| `ShotgunByEMPEROR` | Дробовик из WINTERMUTE в выживании / The WINTERMUTE shotgun in survival |
| `MilitaryStimByEMPEROR` | Военный стимулятор из WINTERMUTE / The WINTERMUTE military stim |
| `CrankLampByEMPEROR` | Фонарь с ручкой (динамо) из WINTERMUTE / The WINTERMUTE crank lamp |
| `SmokesByEMPEROR` | Сигареты, сигары, трубки, зажигалки / Cigarettes, cigars, pipes, lighters |
| `SmokehouseByEMPEROR` | Коптильня, котёл для консервов и стол для колбас / Smokehouse, canning kettle and sausage table |
| `FeatsByEMPEROR` | Новые подвиги / New feats |
| `JellyfishByEMPEROR` | Медузы северного моря: банка, аквариум, свечение / Jellyfish of the northern sea: jar, aquarium, glow |
| `TrainerByEMPEROR` | Трейнер: окно + модуль в игре / Trainer: window + in-game module |
| `FoodPackByEMPEROR` | Новая еда и напитки с эффектами / New food and drinks with effects |
| `FoodBalanceByEMPEROR` | Ползунки: сколько еды разных модов на картах и в контейнерах / Sliders: how much food of each food mod spawns on maps and in containers |
| `ContainerLabelsByEMPEROR` | Подписи на контейнерах / Labels on containers |
| `InventorySearchByEMPEROR` | Поиск в инвентаре / Inventory search |
| `MarkersByEMPEROR` | Метки на тропах / Trail markers |
| `TLDAssets` | Сборщик файлов из игры (см. ниже) / Builder of game-derived files (see below) |

## Файлы из игры / Game files

Копий файлов игры в репозитории и в архивах нет. Персонажи коопа, дробовик, стимулятор и фонарь с ручкой сделаны из
файлов эпизода 5 WINTERMUTE: их собирает у игрока программа `TLDAssets.exe` из его собственной копии игры.
Часть картинок FoodPack, копчёное мясо и рыба Smokehouse и фон значков Feats нарисованы поверх игровых текстур.
Оформление лаунчера (рамки, кнопки, шрифты) он тоже берёт из установленной у игрока игры.

There are no copies of game files in this repository or in the archives. The co-op characters, the shotgun, the stim
and the crank lamp are made from WINTERMUTE episode 5 files; `TLDAssets.exe` builds them on the player's PC from the
player's own copy of the game. Some FoodPack images, the Smokehouse smoked meat and fish and the Feats badge background
are painted over game textures. The launcher also takes its look (frames, buttons, fonts) from the player's installed game.

Код FoodPack, FoodBalance, ContainerLabels, InventorySearch и Markers восстановлен из готовых dll (исходники были потеряны):
он собирается и работает так же, но без комментариев, и часть имён в нём машинные.
The code of FoodPack, FoodBalance, ContainerLabels, InventorySearch and Markers was recovered from the built dlls (the
original sources were lost): it builds and works the same, but has no comments and some names are machine-generated.

## Что нужно для работы / Requirements

Всем модам нужен MelonLoader 0.7 и игра 2.60. Кроме того / All mods need MelonLoader 0.7 and game 2.60. In addition:

| Мод / Mod | Нужно / Needs |
|---|---|
| `SmokesByEMPEROR` | AfflictionComponent (+ ComplexLogger, ModData и ModSettings, их требует сам AfflictionComponent / required by AfflictionComponent itself); плагин `SmokesAudioOnByEMPEROR.dll` в папке `Plugins` (есть в архиве) / the `SmokesAudioOnByEMPEROR.dll` plugin in `Plugins` (included in the archive) |
| `FoodPackByEMPEROR` | AfflictionComponent, ModData, ComplexLogger, ModSettings, AlcoholMod (+ ModComponent, его требует сам AlcoholMod / required by AlcoholMod itself); с AlcoholMod напитки пьянят / with AlcoholMod drinks make you drunk |
| `FoodBalanceByEMPEROR` | ModSettings |
| `ContainerLabelsByEMPEROR` | ModData |
| `CoopByEMPEROR`, `ShotgunByEMPEROR`, `MilitaryStimByEMPEROR`, `CrankLampByEMPEROR` | `TLDAssets.exe` (есть в их архивах, запустить один раз / included in their archives, run it once) |
| остальные / the rest | ничего / nothing |

`TLDCompat258.zip` (папка `UserLibs`, код в `TrainerByEMPEROR/Compat`): прослойка для старых чужих модов (ComplexLogger, FoodPackByTKG,
Personality), исправленных трейнером под игру 2.58+. Нашим модам напрямую не нужна, но нужна исправленному ComplexLogger 1.0.8.
`TLDCompat258.zip` (`UserLibs`, source in `TrainerByEMPEROR/Compat`): a shim for older third-party mods (ComplexLogger, FoodPackByTKG,
Personality) patched by the trainer for game 2.58+. Not used by these mods directly, but the patched ComplexLogger 1.0.8 needs it.

Плагин `SmokesAudioOnByEMPEROR` (исходник в `SmokesByEMPEROR/audioon`): игра выключает встроенный звук Unity, а SmokesByEMPEROR играет
свои звуки через него. Плагин перед запуском игры включает его (меняет один байт в `globalgamemanagers`, копию оригинала
оставляет рядом как `globalgamemanagers.smokes_bak`).
The `SmokesAudioOnByEMPEROR` plugin (source in `SmokesByEMPEROR/audioon`): the game switches Unity's own audio off, and SmokesByEMPEROR
plays its sounds through it. Before the game starts the plugin switches it on (one byte in `globalgamemanagers`; the original
is kept next to it as `globalgamemanagers.smokes_bak`).

### Библиотеки / Libraries

В папке `Libraries` и в релизе / in `Libraries` and in the release:
- `ComplexLogger.zip`: ComplexLogger 1.0.10, исправлен для игры 2.58+ и MelonLoader 0.7 (EMPEROR); оригинал The Illusion,
  https://github.com/Arkhorse/Complex-Logger, MIT / fixed for game 2.58+ and MelonLoader 0.7; original by The Illusion, MIT.
- `ModSettings.zip`: ModSettings 2.2.5 без изменений, авторы Digitalzombie и zeobviouslyfakeacc, MIT / unchanged, MIT.

AfflictionComponent (https://github.com/TLD-Mods/AfflictionComponent), ModData (https://github.com/dommrogers/ModData), ModComponent
(https://github.com/dommrogers/ModComponent) и AlcoholMod (https://github.com/No3371/AlcoholMod, версия 3.0.1 / version 3.0.1)
берите у авторов; лаунчер скачивает их оттуда сам и не трогает библиотеку, поставленную вами вручную /
get them from their authors; the launcher downloads them from there and never replaces a library you installed yourself.

## Лаунчер / Launcher

`TLDModsLauncher.exe`: выберите моды галочками, он скачает их из релиза, поставит нужные библиотеки и MelonLoader и сам
соберёт файлы из сюжета / tick the mods; it downloads them from the release, installs the libraries and MelonLoader it
needs, and builds the story files itself.

- Обновления: при запуске он читает список модов `TLDAssets/catalog.json` отсюда и показывает «есть обновление».
  Updates: on start it reads the mod list `TLDAssets/catalog.json` from here and shows "update available".
- «Удалить» убирает только файлы, которые поставил сам лаунчер; сохранения и настройки остаются. Что он поставил,
  записано в `Mods\EMPERORMods.json`. / "Remove" deletes only the files the launcher installed (listed in
  `Mods\EMPERORMods.json`); saves and settings stay.
- В сеть он ходит только на GitHub (этот репозиторий и страницы авторов библиотек). Картинки и шрифты для окна хранит в
  `%LOCALAPPDATA%\EMPERORMods\ui`. / It only connects to GitHub (this repository and the library authors' pages); the
  window art cache is in `%LOCALAPPDATA%\EMPERORMods\ui`.
- Exe не подписан, поэтому Windows может показать «Система Windows защитила ваш компьютер»: нажмите «Подробнее» →
  «Выполнить в любом случае». Код лаунчера лежит в `TLDAssets`. / The exe is unsigned, so Windows SmartScreen may warn:
  press "More info" → "Run anyway". The launcher's source is in `TLDAssets`.
- Каждый скачанный архив лаунчер сверяет с `SHA256SUMS.txt` из релиза и не ставит файл, который не совпал. /
  Every archive it downloads is checked against `SHA256SUMS.txt` of the release; a file that does not match is not installed.

### Как проверить exe / How to check the exe

`TLDModsLauncher.exe` и `TLDAssets.exe` собирает не автор у себя на ПК, а GitHub на своей чистой машине из открытого кода
этого репозитория ([Build exe](.github/workflows/build-exe.yml), настройки сборки `TLDAssets/*.spec`, точные версии
`TLDAssets/requirements-build.txt`). GitHub записывает, из какого коммита и какой сборкой получен файл.
Both exes are built not on the author's PC but by GitHub on its own clean machine from the public source here
([Build exe](.github/workflows/build-exe.yml), `TLDAssets/*.spec`, exact versions in `TLDAssets/requirements-build.txt`);
GitHub records which commit and which run produced each file.

1. Контрольная сумма / checksum: PowerShell `Get-FileHash TLDModsLauncher.exe` → сравните с `SHA256SUMS.txt` в релизе / compare with `SHA256SUMS.txt` in the release.
2. Происхождение / provenance: `gh attestation verify TLDModsLauncher.exe --repo Saderto/TLDCoop` (GitHub CLI) — должно
   ответить, что файл собран в этом репозитории / must say the file was built in this repository.
3. Антивирусы / antivirus: https://www.virustotal.com — загрузите файл или вставьте его SHA-256 / upload the file or paste its SHA-256.
   Отчёт для лаунчера 1.0.7 / report for launcher 1.0.7:
   https://www.virustotal.com/gui/file/7e3d96ea74e97acec20ca5a3b44159d593c6bee5277f16cc5937cef765e6d6c9
4. Без лаунчера / without the launcher: всё ставится и вручную, см. «Установка» ниже / everything can be installed by hand, see "Install" below.

## Установка / Install

1. MelonLoader 0.7.3 в папке игры (https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3, `MelonLoader.x64.zip`) /
   MelonLoader 0.7.3 in the game folder.
2. Распакуйте нужные архивы из Releases в папку игры (в них папка `Mods`) / Extract the archives you want from Releases into the game folder (they contain `Mods`).
3. Если ставили кооп, дробовик, стимулятор или фонарь, запустите `Mods\TLDAssets.exe` при закрытой игре. Он сам найдёт игру через Steam и соберёт файлы для установленных модов (около 30 с). Нужен скачанный эпизод 5 WINTERMUTE.
   If you installed the co-op, shotgun, stim or crank lamp, run `Mods\TLDAssets.exe` with the game closed. It finds the game through Steam and builds the files for the installed mods (about 30 s). WINTERMUTE episode 5 must be downloaded.
4. В коопе у обоих игроков должны стоять одни и те же моды / In co-op both players need the same mods.

## Сборка / Build

Моды: .NET 6 SDK, `dotnet build -c Release -p:RefsDir=<папка со сборками игры>` в папке `src` мода. В `RefsDir` нужны
сборки, которые MelonLoader создаёт у вас в `MelonLoader/Il2CppAssemblies` и `MelonLoader/net6`. Они здесь не лежат.
Mods: .NET 6 SDK, `dotnet build -c Release -p:RefsDir=<folder with the game assemblies>` in the mod's `src`. `RefsDir`
needs the assemblies MelonLoader generates in your `MelonLoader/Il2CppAssemblies` and `MelonLoader/net6`; they are not included.

TLDAssets и лаунчер / and the launcher: Python 3.13, в папке `TLDAssets` / in `TLDAssets`:
`pip install -r requirements-build.txt`, затем / then `pyinstaller TLDModsLauncher.spec` и / and `pyinstaller TLDAssets.spec`
(без exe / without an exe: `python launcher.py`, `python tld_assets.py`).

## Лицензия / License

Код модов: MIT (файл `LICENSE`), автор EMPEROR. Игра и всё, что из неё взято, к лицензии не относятся.
Mod code: MIT (`LICENSE`), by EMPEROR. The game and anything derived from it are not covered.

## Благодарности / Credits

- SkyCoop (Filigrani, MIT): кооп скачивает его модель игрока из официального релиза, если нет персонажей; некоторые приёмы запуска игры подсмотрены там / the co-op downloads its player model from the official release as a fallback; some game-start techniques were learned from it.
- Анимации приседа в коопе: Quaternius (CC0), перенесены на скелет игры / Co-op crouch animations: Quaternius (CC0), retargeted.
- Звуки SmokesByEMPEROR: Freesound (CC0), список в `CREDITS.txt` мода / SmokesByEMPEROR sounds: Freesound (CC0), see the mod's `CREDITS.txt`.
- Гравировки зажигалок нарисованы по иконкам game-icons.net (CC BY 3.0) / Lighter engravings drawn from game-icons.net icons (CC BY 3.0).
- The Long Dark © Hinterland Studio. Моды не связаны с Hinterland / These mods are not affiliated with Hinterland.
