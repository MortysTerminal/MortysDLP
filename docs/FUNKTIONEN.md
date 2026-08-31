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
3. **Installationsort prüfen** — läuft MortysDLP erkennbar direkt aus der ZIP-Vorschau des
   Explorers (also aus einem temporären Ordner, der beim Schließen verschwindet), erscheint
   ein Hinweis mit den Optionen „Ordner öffnen" und „Trotzdem fortfahren". Der Hinweis blockt
   nicht — die Erkennung ist eine Heuristik, kein sicherer Nachweis.
4. **Werkzeuge prüfen** — für jedes verwaltete Werkzeug derselbe Ablauf (siehe Abschnitt 3):
   - Sind alle zugehörigen Dateien vorhanden und nicht leer? Wenn nicht: Nachfrage, dann
     Download mit Fortschrittsdialog. Lehnt der Nutzer ein für den Betrieb erforderliches
     Werkzeug ab, erklärt ein Dialog, wie es von Hand nachgeholt werden kann, und die
     Anwendung beendet sich.
   - Sonst: installierte Version auslesen und mit der Version der Bezugsquelle vergleichen.
     Gibt es etwas Neueres, wird ein Update **angeboten** — durchgeführt wird es nie ohne
     Zustimmung.
5. **Hauptfenster öffnen**, Startbildschirm schließen.
6. **Suche nach App-Update, jetzt im Hintergrund** — läuft erst an, nachdem das Hauptfenster
   bereits offen ist, und hält es an keiner Stelle auf. Höchstens alle 6 Stunden wird
   tatsächlich online geprüft; dazwischen kommt das Ergebnis aus einem Zwischenspeicher, ganz
   ohne Netzzugriff. Mehrere unabhängige Quellen werden der Reihe nach befragt, falls die erste
   ausfällt oder überlastet ist; ohne Internet gilt der zuletzt bekannte Stand weiter, ganz
   ohne Verzögerung oder Fehlermeldung. Ist eine neuere Version verfügbar, blendet sich der
   Update-Banner im Hauptfenster nachträglich ein, sobald das Ergebnis vorliegt.
7. Ebenfalls im Hintergrund werden alte Temp-Dateien früherer Downloads aufgeräumt
   (`ffmpeg_download_*.zip`, `extract_*`).

> **Bekannte Einschränkung:** Die Werkzeugprüfung (Schritt 4) läuft weiterhin vor dem
> Hauptfenster und ein Werkzeug nach dem anderen — das ist der größte verbleibende Anteil an
> der Startzeit. Die Versionsabfragen gehen jetzt höchstens alle zwölf Stunden über das Netz;
> das Auslesen der installierten Version startet aber bei jedem Start je Werkzeug einen
> kurzen Prozess. Das Nebenläufigmachen und Verlegen in den Hintergrund ist noch offen.

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
| **yt-dlp** | Alle Downloads von Videoplattformen | `%LOCALAPPDATA%\MortysDLP\Tools\yt-dlp.exe` | ja |
| **ffmpeg** | Zusammenführen, Konvertieren, GIF, Audio-Extraktion | `%LOCALAPPDATA%\MortysDLP\Tools\ffmpeg.exe` | ja |
| **ffprobe** | Metadaten lesen (Codec, Auflösung, Dauer, Samplerate) | `%LOCALAPPDATA%\MortysDLP\Tools\ffprobe.exe` | ja |
| **whisper.cpp** | Offline-Transkription | `%LOCALAPPDATA%\MortysDLP\Tools\Whisper\whisper.exe` | nein |
| **Whisper-Modelle** | Sprachmodelle für die Transkription | `%LOCALAPPDATA%\MortysDLP\Tools\Whisper\models\ggml-*.bin` | nein |
| **TwitchDownloaderCLI** | Twitch-Chat laden und rendern | `%LOCALAPPDATA%\MortysDLP\Tools\TwitchDownloaderCLI.exe` | nein |

yt-dlp und ffmpeg/ffprobe werden beim Start verwaltet. Whisper und TwitchDownloaderCLI
werden auf ihren jeweiligen Seiten installiert, aktualisiert und deinstalliert.

### Wie ein Werkzeug-Update abläuft

Für yt-dlp und ffmpeg/ffprobe gilt derselbe Ablauf:

1. **Version bestimmen.** Die installierte Version kommt aus dem Werkzeug selbst
   (`yt-dlp --version`, `ffmpeg -version`). Antwortet es nicht, überschreitet es das Zeitlimit
   von 15 Sekunden oder gibt es nichts Lesbares aus, gilt die Version als *unbekannt* — und
   dann wird **kein** Update angeboten, sondern das Nicht-Antworten protokolliert.
2. **Bezugsquelle fragen.** yt-dlp wird über mehrere unabhängige Quellen der Reihe nach
   abgefragt (GitHub-Release-API, Python-Paketindex, GitHub-Nachrichtenfeed, GitHub-Weiterleitung);
   für ffmpeg liefert ein eigener Versionsendpunkt des Anbieters die Nummer des Pakets, das
   MortysDLP auch herunterlädt. Das Ergebnis wird zwischengespeichert und höchstens alle zwölf
   Stunden erneuert.
3. **Vergleichen.** Bei yt-dlp ist die Version eine ordnende Zahlenfolge — ein Update wird nur
   angeboten, wenn die veröffentlichte Fassung nachweislich **neuer** ist. Bei ffmpeg ist die
   Version *keine* ordnende Zahl (sie trägt die Build-Bezeichnung des Anbieters mit), deshalb
   lautet die Frage dort nur „dieselbe Ausgabe oder eine andere?". Lässt sich „neuer als" nicht
   beantworten, führt das nie zu einem automatischen Update, höchstens zu einem Angebot.
4. **Herunterladen und prüfen.** Der Download läuft über eine Prüfsumme (soweit die Quelle eine
   nennt) und einen Größenabgleich, in eine Zwischendatei — erst nach bestandener Prüfung wird
   sie weiterverwendet. Fehlt eine Prüfsumme, steht das im Protokoll.
5. **Ersetzen mit Rückfallebene.** Die vorhandene Datei wird **umbenannt, nicht gelöscht**
   (`<name>.old`), dann die neue eingesetzt. Bei ffmpeg passiert das für beide Dateien
   gemeinsam.
6. **Erfolgskontrolle.** Das Werkzeug wird einmal aufgerufen und muss eine lesbare Version
   melden — bei ffmpeg beide Dateien, und beide dieselbe. Besteht es, wird die Sicherung
   gelöscht. Besteht es nicht, wird die vorherige Fassung zurückgeholt, der Vorgang gilt als
   fehlgeschlagen, und das Werkzeug ist unverändert einsatzbereit. Nach einem erfolgreichen
   Update bleibt keine `.old`- und keine unfertige Datei liegen.

Jeder dieser Schritte steht mit einer eigenen Zeile im Protokoll — auch die Erfolgsfälle,
nicht nur die Fehlschläge.

**Warum ffmpeg nie erzwungen wird:** ffmpeg ist die Komponente, bei der ein unnötiges Update am
meisten kaputtmachen kann, und die Versionsangabe der ausgelieferten Ausgabe erlaubt keine
verlässliche Aussage darüber, welche von zwei Fassungen neuer ist. Ein Update wird deshalb
angeboten, wenn es eine andere Ausgabe gibt — und nur dann.

Die Werkzeuge liegen bewusst **nicht** im Programmordner, sondern im Nutzerprofil: Wer
MortysDLP nach `C:\Program Files` entpackt, kann seine Werkzeuge trotzdem aktualisieren, weil
der Programmordner dafür schreibgeschützt sein darf. Der Nachteil: Ein USB-Stick mit MortysDLP
lädt die Werkzeuge auf jedem Rechner, an dem er verwendet wird, einmalig neu — sie werden nicht
mehr auf dem Stick mitgeführt. Werkzeuge aus einer Installation vor dieser Umstellung werden
beim ersten Start einmalig automatisch in den neuen Ordner übernommen.

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
| **Download-Fortschritt** | Balken mit Prozentanzeige und Abbrechen-Knopf; bei Werkzeug- **und** App-Updates verwendet |
| **Credits** | Übersicht der verwendeten Open-Source-Werkzeuge mit Lizenz und Link |
| **FluentMessageBox** | Einheitlicher Ersatz für Windows-Meldungsfenster, mit frei belegbaren Knöpfen |

---

## 12. Selbst-Update

1. Beim Start wird höchstens alle 6 Stunden online geprüft (mehrere unabhängige Quellen als
   Ausweiche) und die neueste Version mit der eigenen verglichen; dazwischen zählt der
   Zwischenspeicher.
2. Bei einer neueren Version prüft MortysDLP zuerst, ob am aktuellen Installationsort
   überhaupt aktualisiert werden kann:
   - **Geschützter Systemordner** (z. B. `C:\Program Files`): Der Banner erscheint mit einem
     Warnhinweis. Beim Klick auf „Jetzt aktualisieren" wird erklärt, dass die Installation an
     einen beschreibbaren Ort verschoben werden sollte und dass dabei alle gespeicherten
     Einstellungen verloren gehen. Das Update lässt sich danach über „Trotzdem versuchen"
     dennoch starten — scheitert es, bleibt die vorhandene Version unverändert lauffähig.
   - **Schreibgeschützter Ordner oder Start aus der ZIP-Vorschau des Explorers:** Es wird kein
     Download angeboten, weil er dort nichts bewirken könnte. Der Banner meldet trotzdem, dass
     es eine neue Version gibt; ein Klick erklärt, warum sie sich hier nicht einspielen lässt.
   - **Alles andere:** das gewohnte Banner.
3. Ein Klick öffnet ein Fenster mit den Release-Notizen und drei Möglichkeiten:
   - **Jetzt aktualisieren** — startet den Download (siehe unten).
   - **Später** — der Hinweis bleibt nur für den laufenden Programmlauf weg; beim nächsten
     Start erscheint er erneut. Dasselbe gilt für das „X" direkt am Banner.
   - **Diese Version überspringen** — der Hinweis bleibt dauerhaft weg, bis eine **neuere**
     Version als die gerade angebotene erscheint. Es gibt aktuell keinen Weg, eine
     übersprungene Version nachträglich wieder angezeigt zu bekommen — nur eine neuere fragt
     erneut.
4. Läuft gerade ein Download, eine Konvertierung oder eine Transkription, fragt MortysDLP an
   dieser Stelle nach, ob das Update jetzt trotzdem gestartet und die laufenden Vorgänge
   abgebrochen werden dürfen. „Ja" bricht sie ab und macht danach mit dem Update weiter,
   „Nein" bricht das Update vollständig ab, ohne irgendetwas zu verändern.
5. Enthält das Release mehrere Anhänge (z. B. zusätzlich eine Prüfsummenliste), wählt
   MortysDLP gezielt das richtige Update-Paket aus — nicht einfach den ersten Eintrag. Sind
   mehrere Pakete gleichermaßen passend und keines eindeutig das richtige, bricht die
   Anwendung mit einer verständlichen Meldung ab, statt zu raten.
6. Das Paket wird in einen beschreibbaren Temp-Ordner geladen (mit Wiederholversuchen und
   exponentiellem Backoff) und dabei laufend gegen eine Prüfsumme und die erwartete Größe
   geprüft — bevor es seinen endgültigen Namen erhält. Stimmt etwas nicht, wird die
   heruntergeladene Datei verworfen und das Update abgebrochen; die vorhandene Installation
   bleibt unangetastet. Ist ausnahmsweise keine Prüfsumme bekannt, bleibt das Update möglich
   und nur die Größe wird geprüft. Während des Herunterladens erscheint ein Fenster mit
   Fortschrittsbalken und Prozentanzeige; „Abbrechen" beendet den Download sofort — ohne
   Reste im Temp-Ordner und ohne die bestehende Installation anzufassen.
7. Zusätzlich wird geprüft, dass das Paket lesbar ist und tatsächlich die Hauptanwendung
   enthält.
8. Der Updater wird nach Temp kopiert und mit benannten Kommandozeilenargumenten gestartet.
   Bevor er etwas anfasst, prüft er, dass genug Platz frei ist — auf dem Datenträger der
   Installation *und* auf dem der Sicherungskopie, die unter `%LOCALAPPDATA%` liegt und
   durchaus auf einem anderen Laufwerk sein kann. Dann sichert er jede Datei, die er ersetzt,
   bevor er sie austauscht; scheitert das Update mittendrin (z. B. eine gesperrte Datei),
   spielt er alle bereits ersetzten Dateien automatisch aus der Sicherung zurück — die
   vorhandene Installation bleibt in jedem Fall lauffähig. Werkzeuge und Download-Verlauf
   liegen ohnehin außerhalb des Programmordners und werden von einem App-Update nie berührt.
9. Die App beendet sich; der Updater wartet auf das Prozessende, entpackt und startet die
   App neu.
10. **Beim nächsten Start prüft MortysDLP, ob das Update tatsächlich gewirkt hat:**
   - Läuft danach die neue Version, erscheint sofort eine Bestätigung mit der Möglichkeit,
     die Änderungen der neuen Version anzusehen — einmalig, danach nicht erneut. Der
     Zwischenspeicher der Update-Prüfung sowie eine übersprungene Version werden geleert.
   - Läuft weiterhin die alte Version, erscheint ein Hinweis mit dem Pfad zum Protokoll des
     Updaters — dort steht der Grund (z. B. eine gesperrte Datei oder ein unterbrochener
     Lauf). Das gilt auch dann, wenn der Updater die Anwendung zwar neu gestartet, aber
     tatsächlich nichts ersetzt hat: Maßgeblich ist die laufende Version, nicht der Neustart.
   - Bleibt **dasselbe** Update zweimal hintereinander ohne Wirkung, bietet MortysDLP es
     danach nicht mehr von selbst an. Ein Knopf „Trotzdem erneut versuchen" in der Meldung
     hebt das wieder auf.

---

## 13. Wo liegt was

| Inhalt | Ort (Stand heute) |
|---|---|
| Programmdateien | Entpackungsordner |
| Externe Werkzeuge | `%LOCALAPPDATA%\MortysDLP\Tools\` (aus einer älteren Installation vorhandene Werkzeuge werden beim ersten Start einmalig aus dem Entpackungsordner übernommen) |
| Whisper-Modelle | `%LOCALAPPDATA%\MortysDLP\Tools\Whisper\models\` |
| Download-Verlauf | `%LOCALAPPDATA%\MortysDLP\download_history.json` |
| Einstellungen | `%LOCALAPPDATA%\MortysDLP\MortysDLP.exe_*\<Version>\user.config` |
| Protokolle | `%LOCALAPPDATA%\MortysDLP\logs\mortysdlp-JJJJ-MM-TT.log` (14 Tage bzw. 10 MB je Datei) |
| Update-Zwischenspeicher | `%LOCALAPPDATA%\MortysDLP\cache\update-cache.json` |
| Update-Zustand (Erfolgskontrolle) | `%LOCALAPPDATA%\MortysDLP\update-state.json` (existiert nur zwischen einem angestoßenen Update und dessen Auswertung) |
| Temporäres | `%TEMP%` (`ffmpeg_download_*.zip`, `extract_*`, `whisper_audio_*.wav`) |
