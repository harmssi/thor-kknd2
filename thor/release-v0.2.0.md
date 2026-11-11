All Thor ports: [toravo.de/en/thor](https://toravo.de/en/thor/)

## KKND2 Thor 0.2.0: the deck in your faction's look, with a lot more on it

Unofficial fan port. You need your own copy of KKND2 Krossfire; the app contains no game data.

### New on the bottom screen
- **Game look:** the deck is drawn with the game's own interface art from your files: the sidebar button plates of your faction (Survivors metal, Evolved stone, Series 9 steel), the game's menu symbols for infantry, vehicles, buildings and towers, your faction's emblem and the game's bitmap font. It is read at runtime from `levels/surv.bpk`, `mute.bpk` and `robo.bpk`; nothing of it is part of the app.
- **Research page:** every building with its tech level and the price of the next one. A tap sends a free research lab, a second tap stops it. New units unlock in the build menus as before.
- **Oil overview:** drill rigs, tankers, power stations and the oil left under your rigs, next to the oil stock and the game clock.
- **Control groups 1 to 0, larger:** hold to store the selection, tap to pick it, tap again to look at it. Each tile shows the group's unit picture and size.
- **New commands:** Scatter, Stance (hold fire, return fire, defend, attack all), All army, Same type, Base, Alert (jump to the last attack), Pause, Speed (1×, 1.5×, 2×, 0.5×), Find (centre on the selection).
- **Minimap alerts:** attacks on your units blink on the minimap. The selection panel shows unit pictures.
- **Controller:** L2/R2 also reach the research page; L1/R1 and A work on its cards.

### Tested on the Thor
Skirmish as Survivors against the computer on map 01: deploy, build Barracks and Research Lab through the deck, send the mobile derrick to an oil patch (the header then shows 1 rig and 50,000 oil in the ground), train Machine Gunners (queue count, hold, resume), All army, store group 1 by holding, pick it and look at it, Stance through all four settings, Scatter, Same type, Base, Find, research on the Barracks (level 0 to 1, oil is charged), Pause, Speed (2× measured at about 1.7 to 2× real pace), Save. The deck was looked at in all three faction looks (Survivors, Evolved, Series 9). Not tested: the Alert button and the blinking minimap alerts (no enemy attack happened in the test games), tankers and power stations in the oil overview, the controller on the research page, the German texts, the fallback without game files.

### Updating
Install over 0.1.x; the imported game data and maps stay. Saved games are kept per version: saves made with 0.1.1 are not listed in 0.2.0.

### Known limitations
- Skirmish only: no campaigns, no aircraft; balance follows OpenKrush's unfinished KKND2 rules.
- Loading a saved game (main menu) can come back "out of sync" and paused once units were trained or research was done before saving. The deck therefore has Save but no Load button.
- Game speed from the deck applies to skirmish against the computer only and is back to 1× after loading.
- The build cards use the unit sprites on your faction's plate, not the game's own small cameo tiles.
- If the game files are missing, the deck falls back to plain plates and a system font.

`KKND2-Thor-v0.2.0.apk` · SHA-256 in `SHA256SUMS`

▶ Update video: https://youtu.be/bI8RbqBXaN0
