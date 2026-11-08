# Changelog

## v0.1.1 – 2026-11-08

**English**
- Sharp buildings: the GPU shader ran at medium (16-bit) precision on the Thor, which rounded texture positions on the large
  sprite sheets to every second pixel – buildings looked blocky and smeared. It now runs at high precision.
- Every building/wall sprite uses its own colour table (0.1.0 used the Outpost's for all); player colour on its blue entries.
- The battlefield is drawn at an integer 2x zoom (was 1.5x, which made pixels uneven). Deck pictures are scaled without filtering.
- Map preview (lobby, map chooser): start points now sit on the real start positions. The preview image is 1 pixel per cell
  of the map bounds, built from the level's ground tiles. Maps are rebuilt automatically on the first start after the update.
- Starting the app no longer asks for the game folder again when only the maps need rebuilding.

**Deutsch**
- Scharfe Gebäude: Der Grafik-Shader lief auf dem Thor mit mittlerer (16-Bit-)Genauigkeit und rundete Positionen auf den großen
  Sprite-Blättern auf jedes zweite Pixel – Gebäude wirkten klotzig und verschmiert. Jetzt hohe Genauigkeit.
- Jedes Gebäude bzw. jede Mauer nutzt die eigene Farbtabelle (0.1.0 überall die des Außenpostens); Spielerfarbe auf deren Blautöne.
- Das Spielfeld wird ganzzahlig 2-fach vergrößert (vorher 1,5-fach mit ungleich breiten Pixeln). Deck-Bilder ohne Weichzeichnen.
- Kartenvorschau (Lobby, Kartenauswahl): Die Startpunkte liegen jetzt auf den echten Startplätzen. Das Vorschaubild hat ein Pixel je
  Zelle der Kartengrenzen und entsteht aus den Bodenkacheln des Levels. Die Karten werden nach dem Update beim ersten Start neu gebaut.
- Fehlen nur die Karten, fragt die App nicht mehr erneut nach dem Spielordner.

## v0.1.0 – 2026-11-08 (skirmish version)

**English**
- First release: KKND2 Krossfire skirmish against the AI on the AYN Thor, native arm64 app (OpenRA + OpenKrush).
- Import your own Steam files in the app ("Choose Steam folder"); the 15 multiplayer maps are converted on the device.
- Survivors, Evolved and Series 9; original announcer, voices, effects, music and intro videos.
- Thor deck on the bottom screen: build menu with original pictures and queues, minimap, oil, selection,
  control groups, commands. Full controller support and touch on the top screen. Save/load. 60 fps.
- Not included yet: campaigns, aircraft, balance tuning.

**Deutsch**
- Erste Version: KKND2 Krossfire im Gefecht gegen die KI auf dem AYN Thor, native arm64-App (OpenRA + OpenKrush).
- Eigene Steam-Dateien in der App importieren („Steam-Ordner wählen“); die 15 Mehrspielerkarten werden auf dem Gerät umgewandelt.
- Survivors, Evolved und Series 9; Original-Ansagen, Stimmen, Effekte, Musik und Intro-Videos.
- Thor-Deck unten: Baumenü mit Original-Bildern und Warteschlangen, Minikarte, Öl, Auswahl, Kontrollgruppen,
  Befehle. Volle Controller-Steuerung und Touch oben. Speichern/Laden. 60 fps.
- Noch nicht enthalten: Kampagnen, Flugzeuge, Balance-Feinschliff.
