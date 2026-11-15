# TeaCoffeeByEMPEROR «Чай и кофе» 0.5.7

**RU.** 283 вида чая, кофе и других горячих напитков: прессованные пуэры, листовые чаи, кофе в зёрнах и растворимый, какао, травяные сборы и банки холодных напитков. Пачки находятся в кухонных шкафах и холодильниках (один бросок на предмет мебели, находка появляется при первом открытии). Пачку кладут в котелок или кастрюлю с водой над огнём, и получается напиток в своей посуде: керамические чашки, эспрессо-чашечки, походные кружки, юноми, пиалы и стаканы в подстаканниках разных металлов и узоров. У напитков свои эффекты, которые не повторяют эффекты игры и других модов; они показаны значками. Работает в коопе. Тексты на русском и английском.

**EN.** 283 kinds of tea, coffee and other hot drinks: pressed pu-erh, loose teas, beans and instant coffee, cocoa, herbal blends and cans of cold drinks. Packs turn up in kitchen cupboards and fridges (one roll per piece of furniture, the find is made when it is first opened). Put a pack in a pot with water over a fire to brew a drink in its own vessel: ceramic cups, espresso cups, camp mugs, yunomi, bowls and glasses in holders of different metals and patterns. Drinks have their own effects that do not repeat the game's or other mods' effects, shown as icons. Works in co-op. Russian and English texts.

Requirements / Нужно: MelonLoader 0.7.2 (0.7.3 also works), AfflictionComponent (+ ComplexLogger, ModData, ModSettings, required by AfflictionComponent itself). Install: unpack `Mods/` into the game folder.

Console: `add TC_<Id>`, e.g. `add TC_GoldenMelon`. Option `TestKeys` (off by default) gives every pack (Ctrl+Shift+F8) and every drink (Ctrl+Shift+F9).

Source: `src/` (C#, net6), art pipeline `tools/` (Python + Blender: catalog.py → models.py, labels.py, labels2.py, render.py, gen_cs.py → src/Catalog.cs).
