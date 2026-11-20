# SnacksByEMPEROR

Снеки для The Long Dark: 1546 видов от 159 марок из 31 страны (чипсы, сухарики, семечки, орехи, крекеры, вяленое мясо и рыба, сладкие снеки), из них 177 редких сезонных пачек. У каждого вида своя пачка, значок и шуточное описание. Семь собственных эффектов со значками в карточке предмета и строками на экране состояния: «Соль держит воду», «Крепкая спина», «Сахарная печка», «Перец в крови», «Вместо сигареты» (вместе со SmokesByEMPEROR), «Пот градом», «Сухой ком». Снеки попадаются на кухнях, в других шкафах и ящиках, в карманах трупов и продаются у Торговца. Работает в кооперативе: находки одинаковы у всех игроков.

Snacks for The Long Dark: 1546 kinds from 159 brands of 31 countries (crisps, croutons, seeds, nuts, crackers, jerky and dried fish, sweet snacks), 177 of them rare seasonal packs. Every kind has its own pack, icon and a joke description. Seven own effects with icons on the item card and lines on the status screen: Salt Holds Water, Strong Back, Sugar Stove, Pepper in the Blood, Instead of a Smoke (with SmokesByEMPEROR), Dripping Sweat, Dry Lump. Snacks turn up in kitchens, in other cupboards and boxes, on bodies, and are sold by the Trader. Works in co-op: every player finds the same.

Версия / Version: **0.7.10**. Игра 2.60, MelonLoader 0.7.2 (0.7.3 тоже подходит) / Game 2.60, MelonLoader 0.7.2 (0.7.3 works too).

## Скачать и установить / Download and install

**Проще всего:** лаунчер [TLDModsLauncher.exe](https://github.com/Saderto/TLDCoop/releases/latest/download/TLDModsLauncher.exe). Отметьте **Снеки** и нажмите «Установить / обновить»: он сам
поставит MelonLoader, нужные библиотеки и будет обновлять мод.

**Вручную:** скачайте архив [SnacksByEMPEROR.zip](https://github.com/Saderto/TLDCoop/releases/download/v1.0/SnacksByEMPEROR.zip) и распакуйте его в папку игры `TheLongDark` с заменой файлов.

**Easiest:** the launcher [TLDModsLauncher.exe](https://github.com/Saderto/TLDCoop/releases/latest/download/TLDModsLauncher.exe). Tick **Snacks** and press "Install / update": it sets up
MelonLoader and the libraries needed, and keeps the mod updated.

**By hand:** download [SnacksByEMPEROR.zip](https://github.com/Saderto/TLDCoop/releases/download/v1.0/SnacksByEMPEROR.zip) and unpack it into the game folder `TheLongDark`, replacing files.

## Что нужно / Needs

- AfflictionComponent, а с ним ComplexLogger, ModData и ModSettings (лаунчер ставит их сам). / AfflictionComponent, with ComplexLogger, ModData and ModSettings that it needs (the launcher installs them).

## Настройки / Settings

`UserData/MelonPreferences.cfg`, раздел / section `[SnacksByEMPEROR]`:

| Настройка / Setting | По умолчанию / Default | Что делает / What it does |
|---|---|---|
| `KitchenChance` | 8 | Сколько % кухонных шкафов, холодильников и ящиков с едой получают снек / % of kitchen cupboards, fridges and food boxes with a find |
| `AnyContainerChance` | 3 | Сколько % прочих ящиков (на один предмет мебели; не аптечки, ящики с инструментами, сейфы, оружейные шкафы, почтовые ящики, стиральные и сушильные машины) / % of other containers (per piece of furniture; never first-aid kits, toolboxes, safes, gun lockers, mailboxes, washers, dryers) |
| `CorpseChance` | 4 | Сколько % трупов с пачкой в карманах / % of bodies with a pack in the pockets |
| `DebugLog` | false | Подробный лог / Detailed log |
| `TestKeys` | false | Тест: Ctrl+Shift+F8 кладёт все снеки в рюкзак / Test: Ctrl+Shift+F8 puts every snack into the pack |

Сколько снеков попадается, можно менять и ползунком «Snacks by EMPEROR» в [FoodBalanceByEMPEROR](../FoodBalanceByEMPEROR).
How many snacks turn up can also be set with the "Snacks by EMPEROR" slider of [FoodBalanceByEMPEROR](../FoodBalanceByEMPEROR).

## Файлы / Files

`Mods/SnacksByEMPEROR.dll` и папка / and the folder `Mods/SnacksByEMPEROR` (`assets1.pak`–`assets22.pak`).
Файлов игры внутри нет: модели, пачки и значки сделаны кодом (`tools/`). Тексты на русском и английском.
There are no game files inside: the models, packs and icons are made by code (`tools/`). Russian and English texts.

## Вопросы и баги / Questions and bugs

Discord: https://discord.gg/ezT7PRZmuS (раздел / category «🧩 Mods / Моды»).
