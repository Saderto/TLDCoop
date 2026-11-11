# The Long Dark: моды Seagull / Seagull's mods

Моды для The Long Dark (MelonLoader 0.7, игра 2.60). Все бесплатные.
Mods for The Long Dark (MelonLoader 0.7, game 2.60). All free.

| Папка / Folder | Что это / What it is |
|---|---|
| `TLDCoop` | Кооператив через Steam: общий мир хоста, звери, предметы, костры, погода, время / Co-op over Steam: the host's shared world, animals, items, fires, weather, time |
| `TLDShotgun` | Дробовик из WINTERMUTE в выживании / The WINTERMUTE shotgun in survival |
| `TLDMilitaryStim` | Военный стимулятор из WINTERMUTE / The WINTERMUTE military stim |
| `TLDCrankLamp` | Фонарь с ручкой (динамо) из WINTERMUTE / The WINTERMUTE crank lamp |
| `SmokesBySeagull` | Сигареты, сигары, трубки, зажигалки / Cigarettes, cigars, pipes, lighters |
| `SmokehouseBySeagull` | Коптильня для мяса и рыбы / Smokehouse for meat and fish |
| `FeatsBySeagull` | Новые подвиги / New feats |
| `TLDTrainer` | Трейнер: окно + модуль в игре / Trainer: window + in-game module |
| `FoodPackBySeagull` | Новая еда и напитки с эффектами / New food and drinks with effects |
| `FoodBalanceBySeagull` | Ползунки: сколько еды разных модов на картах и в контейнерах / Sliders: how much food of each food mod spawns on maps and in containers |
| `ContainerLabelsBySeagull` | Подписи на контейнерах / Labels on containers |
| `InventorySearchBySeagull` | Поиск в инвентаре / Inventory search |
| `MarkersBySeagull` | Метки на тропах / Trail markers |
| `TLDAssets` | Сборщик файлов из игры (см. ниже) / Builder of game-derived files (see below) |

## Файлы из игры / Game files

В репозитории и в архивах нет частей игры. Персонажи коопа, дробовик, стимулятор и фонарь с ручкой сделаны из файлов
эпизода 5 WINTERMUTE. Эти файлы собирает у игрока программа `TLDAssets.exe` из его собственной копии игры.

No part of the game is in this repository or in the release archives. The co-op characters, the shotgun, the stim and
the crank lamp are made from WINTERMUTE episode 5 files; `TLDAssets.exe` builds them on the player's PC from the
player's own copy of the game.

Код FoodPack, FoodBalance, ContainerLabels, InventorySearch и Markers восстановлен из готовых dll (исходники были потеряны):
он собирается и работает так же, но без комментариев, и часть имён в нём машинные.
The code of FoodPack, FoodBalance, ContainerLabels, InventorySearch and Markers was recovered from the built dlls (the
original sources were lost): it builds and works the same, but has no comments and some names are machine-generated.

## Что нужно для работы / Requirements

Всем модам нужен MelonLoader 0.7 и игра 2.60. Кроме того / All mods need MelonLoader 0.7 and game 2.60. In addition:

| Мод / Mod | Нужно / Needs |
|---|---|
| `SmokesBySeagull` | AfflictionComponent (+ ComplexLogger, ModData и ModSettings, их требует сам AfflictionComponent / required by AfflictionComponent itself); плагин `SmokesAudioOn.dll` в папке `Plugins` (есть в архиве) / the `SmokesAudioOn.dll` plugin in `Plugins` (included in the archive) |
| `FoodPackBySeagull` | AfflictionComponent, ModData, ComplexLogger, ModSettings. Если стоит AlcoholMod, напитки пьянят / With AlcoholMod installed, drinks make you drunk |
| `FoodBalanceBySeagull` | ModSettings |
| `ContainerLabelsBySeagull` | ModData |
| `TLDCoop`, `TLDShotgun`, `TLDMilitaryStim`, `TLDCrankLamp` | `TLDAssets.exe` (есть в их архивах, запустить один раз / included in their archives, run it once) |
| остальные / the rest | ничего / nothing |

`TLDCompat258.zip` (папка `UserLibs`, код в `TLDTrainer/Compat`): прослойка для старых чужих модов (ComplexLogger, FoodPackByTKG,
Personality), исправленных трейнером под игру 2.58+. Нашим модам напрямую не нужна, но нужна исправленному ComplexLogger 1.0.8.
`TLDCompat258.zip` (`UserLibs`, source in `TLDTrainer/Compat`): a shim for older third-party mods (ComplexLogger, FoodPackByTKG,
Personality) patched by the trainer for game 2.58+. Not used by these mods directly, but the patched ComplexLogger 1.0.8 needs it.

Плагин `SmokesAudioOn` (исходник в `SmokesBySeagull/audioon`): игра выключает встроенный звук Unity, а SmokesBySeagull играет
свои звуки через него. Плагин перед запуском игры включает его (меняет один байт в `globalgamemanagers`, копию оригинала
оставляет рядом как `globalgamemanagers.smokes_bak`).
The `SmokesAudioOn` plugin (source in `SmokesBySeagull/audioon`): the game switches Unity's own audio off, and SmokesBySeagull
plays its sounds through it. Before the game starts the plugin switches it on (one byte in `globalgamemanagers`; the original
is kept next to it as `globalgamemanagers.smokes_bak`).

## Установка / Install

1. MelonLoader 0.7 в папке игры / MelonLoader 0.7 in the game folder.
2. Распакуйте нужные архивы из Releases в папку игры (в них папка `Mods`) / Extract the archives you want from Releases into the game folder (they contain `Mods`).
3. Если ставили кооп, дробовик, стимулятор или фонарь, запустите `Mods\TLDAssets.exe` при закрытой игре. Он сам найдёт игру через Steam и соберёт файлы для установленных модов (около 30 с). Нужен скачанный эпизод 5 WINTERMUTE.
   If you installed the co-op, shotgun, stim or crank lamp, run `Mods\TLDAssets.exe` with the game closed. It finds the game through Steam and builds the files for the installed mods (about 30 s). WINTERMUTE episode 5 must be downloaded.
4. В коопе у обоих игроков должны стоять одни и те же моды / In co-op both players need the same mods.

## Сборка / Build

Моды: .NET 6 SDK, `dotnet build -c Release -p:RefsDir=<папка со сборками игры>` в папке `src` мода. В `RefsDir` нужны
сборки, которые MelonLoader создаёт у вас в `MelonLoader/Il2CppAssemblies` и `MelonLoader/net6`. Они здесь не лежат.
Mods: .NET 6 SDK, `dotnet build -c Release -p:RefsDir=<folder with the game assemblies>` in the mod's `src`. `RefsDir`
needs the assemblies MelonLoader generates in your `MelonLoader/Il2CppAssemblies` and `MelonLoader/net6`; they are not included.

TLDAssets: Python 3.13, `pip install UnityPy==1.25.4 Pillow==12.3.0 numpy==2.5.3`, `python tld_assets.py`;
exe: PyInstaller 6 (`--onefile`, `--add-data steps;steps --add-data own;own --collect-all UnityPy`).

## Лицензия / License

Код модов: MIT (файл `LICENSE`), автор Seagull. Игра и всё, что из неё взято, к лицензии не относятся.
Mod code: MIT (`LICENSE`), by Seagull. The game and anything derived from it are not covered.

## Благодарности / Credits

- SkyCoop (Filigrani, MIT): кооп скачивает его модель игрока из официального релиза, если нет персонажей; некоторые приёмы запуска игры подсмотрены там / the co-op downloads its player model from the official release as a fallback; some game-start techniques were learned from it.
- Анимации приседа в коопе: Quaternius (CC0), перенесены на скелет игры / Co-op crouch animations: Quaternius (CC0), retargeted.
- Звуки SmokesBySeagull: Freesound (CC0), список в `CREDITS.txt` мода / SmokesBySeagull sounds: Freesound (CC0), see the mod's `CREDITS.txt`.
- Гравировки зажигалок нарисованы по иконкам game-icons.net (CC BY 3.0) / Lighter engravings drawn from game-icons.net icons (CC BY 3.0).
- The Long Dark © Hinterland Studio. Моды не связаны с Hinterland / These mods are not affiliated with Hinterland.
