# MortysDLP — Funktionsreferenz

**Lebendes Dokument.** Es beschreibt, was die Software **tatsächlich tut** — nicht, was
geplant ist. Bekannte Einschränkungen sind bewusst mit aufgeführt; sie werden nach und nach
behoben.

**Stand:** 2026-08-27 · beschreibt Version `2026.06.01`

---

## Inhalt

1. [Überblick](#1-überblick)
2. [Programmstart](#2-programmstart)
3. [Werkzeuge (externe Tools)](#3-werkzeuge-externe-tools)
4. [Tab: Download](#4-tab-download)
5. [Tab: Batch-Download](#5-tab-batch-download)
6. [Tab: Konvertieren](#6-tab-konvertieren)
7. [Tab: Transkribieren](#7-tab-transkribieren)
8. [Tab: GIF-Maker](#8-tab-gif-maker)
9. [Tab: Twitch-Download](#9-tab-twitch-download)
10. [Einstellungen](#10-einstellungen)
11. [Fenster & Dialoge](#11-fenster--dialoge)
12. [Selbst-Update](#12-selbst-update)
13. [Wo liegt was](#13-wo-liegt-was)

---

## 1. Überblick

MortysDLP ist eine Windows-Desktop-Anwendung (WPF, .NET 10), die yt-dlp und ffmpeg über eine
grafische Oberfläche bedienbar macht. Sie ist portabel: kein Installer, keine
Administratorrechte, Entpacken genügt. Benötigte Werkzeuge lädt sie beim ersten Start selbst
nach.

Die Oberfläche besteht aus einer Seitenleiste mit sechs Arbeitsbereichen plus Einstellungen,
einer Kopfzeile mit Bereichstitel und Version sowie einem Update-Banner, das nur bei
verfügbarem Update erscheint. Sprache: Deutsch oder Englisch, automatisch nach
Windows-Sprache oder manuell. Helles und dunkles Design folgen der Windows-Einstellung.

---

## 2. Programmstart

Ablauf beim Start (`App.OnStartup`):

1. **Sprache setzen** — aus der Einstellung `SelectedLanguage` (`auto`/`de`/`en`); bei `auto`
   aus der Windows-Anzeigesprache.
2. **Startbildschirm** mit rotierendem Ladesymbol und Statustext erscheint.
3. **Suche nach App-Update** — höchstens alle 6 Stunden wird tatsächlich online geprüft;
   dazwischen kommt das Ergebnis aus einem Zwischenspeicher, ganz ohne Netzzugriff. Mehrere
   unabhängige Quellen werden der Reihe nach befragt, falls die erste ausfällt oder überlastet
   ist; ohne Internet gilt der zuletzt bekannte Stand weiter. Ist eine neuere Version
   verfügbar, wird sie gemerkt (der Banner erscheint später im Hauptfenster).
4. **Installationsort prüfen** — läuft MortysDLP erkennbar direkt aus der ZIP-Vorschau des
   Explorers (also aus einem temporären Ordner, der beim Schließen verschwindet), erscheint
   ein Hinweis mit den Optionen „Ordner öffnen" und „Trotzdem fortfahren". Der Hinweis blockt
   nicht — die Erkennung ist eine Heuristik, kein sicherer Nachweis.
5. **Werkzeuge prüfen**
   - yt-dlp vorhanden? Wenn nicht: Nachfrage, dann Download mit Fortschrittsdialog.
     Lehnt der Nutzer ab, beendet sich die App.
   - yt-dlp-Version gegen GitHub prüfen; bei Abweichung Update anbieten.
   - ffmpeg und ffprobe vorhanden? Wenn nicht: Nachfrage, dann ZIP-Download und Entpacken.
     Lehnt der Nutzer ab, beendet sich die App.
6. **Hauptfenster öffnen**, Startbildschirm schließen.
7. Im Hintergrund werden alte Temp-Dateien früherer Downloads aufgeräumt
   (`ffmpeg_download_*.zip`, `extract_*`).

> **Bekannte Einschränkung:** Alle Prüfungen laufen nacheinander; die yt-dlp-Release-API wird
> dabei zweimal abgefragt. Die App-Update-Prüfung selbst ist seit Kurzem zwischengespeichert
> (siehe oben) und dadurch beim wiederholten Starten meist ein reiner Zwischenspeicher-Treffer
> ohne Netzzugriff.

**Fehlerbehandlung:** Ein unerwarteter Fehler beendet MortysDLP nicht mehr wortlos. Er wird in
einer Protokolldatei festgehalten (siehe Abschnitt 13) und in einem Dialog mit verständlichem
Kurztext und ausklappbaren technischen Details angezeigt — mit Knöpfen zum Kopieren der
Details und zum Öffnen des Protokollordners. Bei Fehlern, die nur eine einzelne Aktion
betreffen, läuft die Anwendung danach normal weiter. Scheitert bereits der Programmstart
selbst, bleibt nur „Beenden".

---

## 3. Werkzeuge (externe Tools)

| Werkzeug | Wofür | Ablageort | Pflicht |
|---|---|---|---|
| **yt-dlp** | Alle Downloads von Videoplattformen | `Tools\yt-dlp.exe` | ja |
| **ffmpeg** | Zusammenführen, Konvertieren, GIF, Audio-Extraktion | `Tools\ffmpeg.exe` | ja |
| **ffprobe** | Metadaten lesen (Codec, Auflösung, Dauer, Samplerate) | `Tools\ffprobe.exe` | ja |
| **whisper.cpp** | Offline-Transkription | `Tools\Whisper\whisper.exe` | nein |
| **Whisper-Modelle** | Sprachmodelle für die Transkription | `Tools\Whisper\models\ggml-*.bin` | nein |
| **TwitchDownloaderCLI** | Twitch-Chat laden und rendern | `Tools\TwitchDownloaderCLI.exe` | nein |

yt-dlp und ffmpeg/ffprobe werden beim Start verwaltet. Whisper und TwitchDownloaderCLI
werden auf ihren jeweiligen Seiten installiert, aktualisiert und deinstalliert.

---

## 4. Tab: Download

Der Hauptbereich für einzelne Videos und Playlists.

### Pfade
Zwei getrennte Zielordner: einer für Video, optional einer für „Nur Audio". Ein Klick auf den
angezeigten Pfad öffnet den Ordner im Explorer. „Pfad ändern" öffnet den Pfad-Dialog.

### Eingabe
- **URL** — jede von yt-dlp unterstützte Adresse. `Enter` startet den Download.
- **Verlauf** — öffnet die Liste früherer Downloads; ein Eintrag lässt sich übernehmen.
- **Benutzerdefinierter Videotitel** — ersetzt `%(title)s` im Dateinamen. Ungültige Zeichen
  werden ersetzt, Leerzeichen zu Bindestrichen, alles kleingeschrieben.

### Optionen
- **Zeitspanne von–bis** — lädt nur diesen Ausschnitt (`--download-sections`).
  Formate: `mm:ss` oder `hh:mm:ss`.
  *Der Schnitt erfolgt an Keyframe-Grenzen und kann daher um wenige Sekunden abweichen.*
- **Timeline** — grafische Auswahl des Ausschnitts. Ruft zuvor die Videodauer ab
  (Zeitlimit 15 s) und öffnet ein Fenster mit zwei Schiebereglern plus manueller Eingabe.
- **Von Start bis N Sekunden** — lädt nur die ersten N Sekunden (ffmpeg als Downloader).
  Schließt sich mit „Zeitspanne" gegenseitig aus.
- **Videoqualität** — Höchste, 2160p, 1440p, 1080p, 720p, 480p, 360p, 144p. Der Wert wird
  sprachunabhängig über ein `Tag` ausgewertet.
- **Videoformat (Container)** — mp4, mkv, mov, avi. Standardmäßig wird nur *remuxt*
  (Stream-Copy), nicht neu kodiert — das ist sehr schnell.
  - `mp4` → Filter `[ext=mp4]+[ext=m4a]`, AV1 erlaubt
  - `mov`/`avi` → Filter `[vcodec^=avc1]`, weil AV1/VP9 dort nicht zuverlässig laufen
  - `mkv` → kein Filter nötig, akzeptiert alles
- **Videoformat für Schnittprogramme (x264)** — erzwingt mp4 und prüft nach dem Download den
  Codec per ffprobe. Ist er nicht H.264, wird umkodiert. Der Encoder wird automatisch
  gewählt: NVIDIA NVENC → Intel QuickSync → AMD AMF → libx264 (CPU). Ab einer Kantenlänge
  über 4096 px wird immer die CPU verwendet, weil GPU-Encoder H.264 dort nicht unterstützen.
  Audio wird dabei immer zu AAC 48 kHz Stereo gewandelt.
- **Nur Audio** — extrahiert die Tonspur. Formate: mp3, m4a, aac, alac, flac, wav, opus,
  vorbis. Bitrate: Höchste, 320k, 256k, 192k, 128k, 96k, 64k.
  Ist die Quelle unter 44,1 kHz oder mono, wird automatisch auf 48 kHz Stereo hochgesetzt.
- **GIF-Maker** — wandelt das fertige Video zusätzlich in ein GIF um (Qualitätsstufen wie
  auf der GIF-Seite).

### Playlists
Enthält die URL einen `list=`-Parameter, fragt die App nach:
„Ganze Playlist laden" oder — falls auch eine Video-ID enthalten ist — „Einzelnes Video
laden". Bei der Playlist werden zuerst alle Video-IDs schnell aufgelöst (`--flat-playlist`),
dann nacheinander geladen. Die Metadatenabfrage für das jeweils **nächste** Video läuft
parallel zum aktuellen Download.

### Dateibenennung
```
<Titel><Varianten-Kürzel>_<Video-ID>.<Endung>
```
Die Varianten-Kürzel machen verschiedene Fassungen desselben Videos unterscheidbar:
`t<von>-<bis>` (Zeitspanne), `s<N>` (erste N Sekunden), `a` + Format + Bitrate (Audio),
`q<Qualität>` + Container (+ `x264`).
Beispiel: `mein-video_q1080_mp4_x264_dQw4w9WgXcQ.mp4`

### Fortschritt und Status
Ein Balken mit Prozent und Geschwindigkeit, ein Statustext („Lädt…", „Zusammenführen…",
„Audio extrahieren…", „Prüfe Video-Codec…", „Konvertiere zu H.264…") und ein Statussymbol,
das nach Abschluss den Zielordner öffnet.

> **Bekannte Einschränkung:** Bei getrennten Video-/Audio-Streams läuft der Balken zweimal von
> 0 auf 100 %.

### Debug-Ausgabe
Nur sichtbar, wenn in den Einstellungen der Debug-Modus aktiv ist. Zeigt die vollständige
yt-dlp- und ffmpeg-Ausgabe sowie die verwendete Kommandozeile.

---

## 5. Tab: Batch-Download

Warteschlange für mehrere URLs.

- **Einzeln hinzufügen** (`Enter`) oder **Sammel-Eingabe** über ein Fenster, in das eine URL
  pro Zeile eingefügt wird; ungültige Zeilen werden gefiltert, Duplikate erkannt.
- **Aus dem Verlauf übernehmen.**
- Pro Eintrag: URL, Titel (wird im Hintergrund per yt-dlp geholt), Status, Fortschritt und
  ein farbiges Statussymbol.
- **Kontextmenü**: „Zurück in die Warteschlange", „Nur Ausgewählte laden", „Entfernen".
  `Entf` entfernt die Auswahl.
- **Optionen**: Nur Audio (Format + Bitrate), Videoqualität, Container, x264-Modus.
  *Zeitspanne, eigener Dateiname, GIF-Nachlauf und Playlist-Abfrage gibt es hier nicht.*
- Abarbeitung streng nacheinander, mit Gesamtfortschritt (`erledigt/gesamt` und Prozent) und
  aktueller Geschwindigkeit.
- Abschlusszustände: Fertig, Abgebrochen, Teilweise abgebrochen, Teilweise fehlerhaft.
- Nach dem Lauf erscheint ein Knopf, der den Zielordner öffnet.

> **Bekannte Einschränkung:** Batch-Downloads landen nicht im Verlauf.

---

## 6. Tab: Konvertieren

Wandelt lokale Dateien um.

- Dateien über Dialog oder **Drag & Drop** hinzufügen (akzeptiert mov, mp4, mkv, avi, mp3,
  aac, wav, flac, opus).
- **Zielformat**: Video mp4/mkv/avi/mov oder Audio mp3/aac/wav.
- **Zielordner** frei wählbar; Schnellknöpfe übernehmen den Download- oder Audio-Pfad.
- **Videoqualität**: Original (Stream-Copy) oder Skalierung auf eine Höhe; dann libx264
  mit `-preset medium -crf 20`.
- **Audioqualität**: Original oder feste Bitrate. Mono-Quellen werden zu Stereo, Quellen
  unter 44,1 kHz (bei Video-Zielen unter 48 kHz) werden hochgesetzt.
- Existiert die Zieldatei bereits, wird die Datei als „Bereits konvertiert" übersprungen.
- **Parallelisierung**: gleichzeitig laufen `Prozessorkerne / 2`, höchstens 4 Konvertierungen.
- Fortschritt pro Datei; am Ende eine Zusammenfassung (erfolgreich / fehlgeschlagen /
  abgebrochen) in der Debug-Ausgabe.

> **Bekannte Einschränkung:** Die Liste wird bei jeder Fortschrittsmeldung komplett neu aufgebaut;
> Auswahl und Scrollposition gehen dabei verloren.

---

## 7. Tab: Transkribieren

Wandelt Sprache in Text — vollständig lokal, ohne Cloud und ohne API-Schlüssel.

- **Einrichtung**: Solange whisper.cpp oder ein Modell fehlt, zeigt die Seite eine
  zweistufige Anleitung; die Arbeitsbereiche sind ausgeblendet.
- **Modellverwaltung** (eigenes Fenster): Installieren/Deinstallieren der Whisper-Engine
  sowie Herunterladen und Löschen einzelner Modelle mit Fortschrittsanzeige.

  | Modell | Größe | Charakter |
  |---|---|---|
  | Tiny | ~75 MB | sehr schnell, ungenau |
  | Base | ~142 MB | schnell, brauchbar |
  | Small | ~466 MB | ausgewogen |
  | Medium | ~1,5 GB | genau, langsamer |
  | Large v3 Turbo | ~1,6 GB | sehr genau, moderat — Empfehlung |
  | Large v3 | ~3,1 GB | höchste Genauigkeit, sehr anspruchsvoll |

- **Eingabe**: Video- oder Audiodatei. Videos werden vorab per ffmpeg in eine temporäre
  WAV-Datei (16 kHz, mono) umgewandelt — das ist Whispers interne Arbeitsauflösung.
- **Sprache**: automatische Erkennung oder eine von 18 Sprachen. Die Sprache wird immer
  explizit übergeben, weil manche whisper.cpp-Versionen sonst nach Englisch **übersetzen**
  statt zu transkribieren.
- **Ausgabeformate**: TXT, SRT, VTT — beliebig kombinierbar.
- Fortschritt in Prozent (aus `--print-progress`), Abbruch jederzeit möglich, temporäre
  Dateien werden aufgeräumt.
- Threadzahl: `Prozessorkerne / 2`, mindestens 2, höchstens 8.

> **Bekannte Einschränkung:** Es wird nur ein CPU-Build geladen und die Threadzahl ist gedeckelt.
> Eine Beschleunigung ist geplant.

---

## 8. Tab: GIF-Maker

Wandelt Videos in animierte GIFs.

- Eingabedatei über Dialog oder Drag & Drop.
- **Qualitätsstufen** (Breite / Bildrate / Dither):

  | Stufe | Breite | fps | Zweck |
  |---|---|---|---|
  | Web / Discord | 480 px | 15 | Standard, unter 8 MB |
  | Niedrig | 320 px | 8 | sehr kleine Dateien |
  | Mittel | 480 px | 12 | ausgewogen |
  | Hoch | 640 px | 18 | beste Qualität |

- **Zeitbereich** optional (Start/Ende).
- Verfahren: ein einziger ffmpeg-Aufruf mit `palettegen` + `paletteuse`
  (`stats_mode=diff`, `dither=bayer`, `diff_mode=rectangle`) — deutlich bessere Qualität
  bei kleinerer Datei als eine einfache Umwandlung.
- Der Dateiname wird bei Bedarf durchnummeriert, damit nichts überschrieben wird.
- Bei Abbruch oder Fehler wird die unvollständige Datei gelöscht.

---

## 9. Tab: Twitch-Download

- **Werkzeugverwaltung** für TwitchDownloaderCLI: Installieren, Aktualisieren (mit stiller
  Hintergrundprüfung beim Öffnen der Seite), Deinstallieren, Anzeige der Dateigröße.
  Beim Aktualisieren wird die alte EXE erst gesichert und bei Fehler zurückgerollt.
- **Eingabe**: VOD-URL, Clip-URL, `clips.twitch.tv`-Adresse, reine VOD-ID oder Clip-Slug.
  Query-Parameter werden entfernt.
- **Video** wird über **yt-dlp** geladen (nicht über TwitchDownloaderCLI), inklusive
  Bandbreitenlimit und `--continue`.
- **Chat** wird über TwitchDownloaderCLI geladen:
  - als **JSON**, oder
  - als **gerendertes MP4-Overlay** in drei Stufen:

    | Stufe | Auflösung | fps | Schrift |
    |---|---|---|---|
    | Standard | 350 × 600 | 30 | 12 |
    | Hoch | 525 × 900 | 60 | 14 |
    | Ultra | 700 × 1200 | 60 | 16 + Kontur |

- Der Dateiname stammt aus dem echten Titel, der über die Twitch-GQL-Schnittstelle geholt
  wird; schlägt das fehl, dient die ID als Name.
- Mindestens Video **oder** Chat muss aktiv sein — die Oberfläche erzwingt das.

> **Bekannte Einschränkung:** Jede Ausgabezeile erscheint doppelt im Protokoll.

---

## 10. Einstellungen

- **Download-Pfade** — öffnet denselben Dialog wie auf der Download-Seite.
- **App** — GitHub-Seite öffnen, Anwendung beenden.
- **Debug-Modus** — blendet auf allen Seiten die Protokollbereiche ein.
- **Sprache** — Automatisch (zeigt die erkannte Sprache), Deutsch, Englisch. Wirkt sofort,
  ohne Neustart.
- **Bandbreitenlimit** — an/aus plus Wert in MB/s. Wirkt sofort auf laufende Downloads:
  der yt-dlp-Prozess wird beendet und mit dem neuen Limit sowie `--continue` fortgesetzt.
  Ist ein Limit aktiv, zeigen die Download-Seiten einen Hinweis.

---

## 11. Fenster & Dialoge

| Fenster | Zweck |
|---|---|
| **Startbildschirm** | Fortschritt der Startprüfungen |
| **Download-Pfade** | Standard-Zielordner und optionaler Audio-Zielordner; bei leerer Eingabe wird der Windows-Downloads-Ordner angeboten |
| **Verlauf** | Bis zu 30 Einträge mit Titel, Datum, Typ und Einstellungen; „Neu verwenden" übernimmt die URL; „Leeren" fragt nach |
| **Timeline** | Grafische Auswahl eines Zeitausschnitts |
| **Whisper-Modelle** | Engine- und Modellverwaltung |
| **Sammel-URLs** | Mehrere URLs auf einmal einfügen |
| **Update-Changelog** | Zeigt die Release-Notizen als formatierten Text (eigener Markdown-Renderer) |
| **Credits** | Übersicht der verwendeten Open-Source-Werkzeuge mit Lizenz und Link |
| **FluentMessageBox** | Einheitlicher Ersatz für Windows-Meldungsfenster, mit frei belegbaren Knöpfen |

---

## 12. Selbst-Update

1. Beim Start wird höchstens alle 6 Stunden online geprüft (mehrere unabhängige Quellen als
   Ausweiche) und die neueste Version mit der eigenen verglichen; dazwischen zählt der
   Zwischenspeicher.
2. Bei einer neueren Version erscheint im Hauptfenster ein Banner.
3. Ein Klick öffnet ein Fenster mit den Release-Notizen; dort „Jetzt aktualisieren" oder
   „Später".
4. Das ZIP wird in einen beschreibbaren Temp-Ordner geladen (mit Wiederholversuchen und
   exponentiellem Backoff) und geprüft, ob es eine EXE enthält.
5. Der Updater wird nach Temp kopiert und mit
   `<ExeName> <ZipPfad> <Zielordner> <ProzessId>` gestartet.
6. Die App beendet sich; der Updater wartet auf das Prozessende, entpackt und startet die
   App neu.

> **Bekannte Einschränkungen:** Beim Herunterladen des Updates gibt es keine Fortschrittsanzeige
> und keine Möglichkeit, ein fehlgeschlagenes Update zurückzunehmen. Eine überarbeitete
> Update-Mechanik ist in Vorbereitung.

---

## 13. Wo liegt was

| Inhalt | Ort (Stand heute) |
|---|---|
| Programmdateien | Entpackungsordner |
| Externe Werkzeuge | `<Entpackungsordner>\Tools\` |
| Whisper-Modelle | `<Entpackungsordner>\Tools\Whisper\models\` |
| Download-Verlauf | `%LOCALAPPDATA%\MortysDLP\download_history.json` |
| Einstellungen | `%LOCALAPPDATA%\MortysDLP\MortysDLP.exe_*\<Version>\user.config` |
| Protokolle | `%LOCALAPPDATA%\MortysDLP\logs\mortysdlp-JJJJ-MM-TT.log` (14 Tage bzw. 10 MB je Datei) |
| Update-Zwischenspeicher | `%LOCALAPPDATA%\MortysDLP\cache\update-cache.json` |
| Temporäres | `%TEMP%` (`ffmpeg_download_*.zip`, `extract_*`, `whisper_audio_*.wav`) |
