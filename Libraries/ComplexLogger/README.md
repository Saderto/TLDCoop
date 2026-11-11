# ComplexLogger 1.0.9 (исправленная версия / fixed build)

Оригинал: ComplexLogger 1.0.8, автор The Illusion, https://github.com/Arkhorse/Complex-Logger (MIT).
Original: ComplexLogger 1.0.8 by The Illusion, https://github.com/Arkhorse/Complex-Logger (MIT).

Исправления Seagull для игры 2.58+ и MelonLoader 0.7 / Seagull's fixes for game 2.58+ and MelonLoader 0.7:
- `GearItem.LoadGearItemPrefab` убрали из игры в 2.58; предмет теперь грузится через Addressables прямо в библиотеке
  (раньше для этого нужна была прослойка TLDCompat258) / removed from the game in 2.58; the prefab is now loaded through
  Addressables inside the library (the TLDCompat258 shim is no longer needed for it).
- Цветные строки в журнале: `Pastel(string, System.Drawing.Color)` нет в MelonLoader 0.7, цвет переводится в `ColorARGB` /
  coloured log lines: MelonLoader 0.7 has no `Pastel(string, System.Drawing.Color)`, the colour is converted to `ColorARGB`.

Имя сборки и её версия (1.0.8.0) не изменены, поэтому моды, которым нужен ComplexLogger (например AfflictionComponent),
работают с ней как раньше. Код восстановлен из dll 1.0.8 и исправлен.
The assembly name and version (1.0.8.0) are unchanged, so mods that need ComplexLogger (e.g. AfflictionComponent) work
with it as before. The code was recovered from the 1.0.8 dll and fixed.
