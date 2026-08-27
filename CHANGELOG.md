# Changelog

Alle nennenswerten Änderungen an MortysDLP werden hier dokumentiert.

Das Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.1.0/).
Versionen folgen dem Schema `JJJJ.MM.TT` (Release-Datum), Hotfixes am selben Tag erhalten
ein viertes Segment: `2026.06.01.1`.

**Regel für jede Änderung:** Wer Code ändert, trägt den Eintrag **im selben Arbeitsschritt**
unter `[Unreleased]` ein — nicht später, nicht gesammelt. Einträge sind aus Nutzersicht
formuliert: was sich für die Bedienung ändert, nicht welcher Code angefasst wurde.

**Kategorien:**
| Kategorie | Wofür |
|---|---|
| `Hinzugefügt` | Neue Funktionen |
| `Geändert` | Änderungen an bestehenden Funktionen |
| `Veraltet` | Funktionen, die bald entfernt werden |
| `Entfernt` | Entfernte Funktionen |
| `Behoben` | Fehlerbehebungen |
| `Sicherheit` | Alles mit Sicherheitsbezug |
| `Intern` | Refactorings, Tests, Doku — für Nutzer nicht sichtbar |

---

## [Unreleased]

### Sicherheit
- Härtung der Argumentübergabe an externe Werkzeuge (yt-dlp, ffmpeg, ffprobe,
  TwitchDownloaderCLI, whisper.cpp). Kopierte URLs oder Videotitel mit ungewöhnlichen
  Sonderzeichen können externen Werkzeugen nicht mehr zusätzliche, nicht beabsichtigte
  Befehlszeilenargumente unterschieben.

### Hinzugefügt
- MortysDLP erkennt beim Start, wenn es direkt aus der ZIP-Vorschau des Explorers gestartet
  wurde (also aus einem temporären Ordner, der beim Schließen verschwindet) und zeigt dann
  einen Hinweis mit der Möglichkeit, den Ordner zu öffnen oder trotzdem fortzufahren. So bleibt
  nicht mehr unklar, warum heruntergeladene Werkzeuge nach jedem Neustart erneut fehlen.
- Unerwartete Fehler beenden MortysDLP nicht mehr wortlos. Sie werden protokolliert
  (`%LOCALAPPDATA%\MortysDLP\logs\`, 14 Tage bzw. 10 MB je Datei) und in einem Dialog mit
  verständlichem Kurztext, ausklappbaren technischen Details, „Details kopieren" und
  „Protokollordner öffnen" angezeigt. Bei Fehlern, die nur eine einzelne Aktion betreffen,
  läuft die Anwendung danach normal weiter.

### Behoben
- Externe Werkzeuge (yt-dlp, ffmpeg, ffprobe, Whisper, TwitchDownloaderCLI) werden jetzt
  zuverlässig gefunden, unabhängig davon, wie MortysDLP gestartet wird — etwa über eine
  Verknüpfung mit abweichendem Arbeitsverzeichnis, als Administrator oder über die
  Aufgabenplanung. Bisher konnte es dabei zu „nicht gefunden"-Meldungen kommen, obwohl das
  Werkzeug vorhanden war.
- Der Download-Verlauf liegt jetzt an einem festen, vom Startort unabhängigen Ort
  (`%LOCALAPPDATA%\MortysDLP\`). Ein vorhandener Verlauf wird beim ersten Start nach dem
  Update automatisch übernommen.
- Dateinamen mit reservierten Windows-Namen (z. B. `NUL`, `CON`) oder abschließenden Punkten
  bzw. Leerzeichen führen nicht mehr zu Dateien, die sich nicht anlegen lassen.
- Einstellungen liegen jetzt einheitlich unter `%LOCALAPPDATA%\MortysDLP\` statt in einem
  abweichend benannten Ordner. Bereits gespeicherte Einstellungen aus früheren Versionen
  gehen dadurch einmalig verloren und werden beim nächsten Start neu mit den Standardwerten
  angelegt.
- Alle Aufrufe externer Werkzeuge haben jetzt ein Zeitlimit bzw. einen Leerlauf-Abbruch und
  verwenden durchgehend UTF-8 — vereinzelte hängende Vorgänge und kaputte Umlaute/CJK-Zeichen
  in Videotiteln bei bestimmten Abläufen sind damit ausgeschlossen. Beim Abbrechen wird jetzt
  überall auch der komplette Prozessbaum beendet, sodass ffmpeg nicht mehr im Hintergrund
  weiterlaufen und die Zieldatei gesperrt halten kann.
- Eine beschädigte, gesperrte oder aus einem schreibgeschützten Ordner geladene Verlaufsdatei
  lässt den Download-Verlauf nicht mehr abstürzen. Er öffnet stattdessen leer, eine defekte
  Datei wird zur Rettung als Sicherung abgelegt statt verworfen, und das Schreiben erfolgt
  jetzt so, dass ein Abbruch mittendrin nie eine bestehende, gültige Datei beschädigt.
- Die Debug-Ausgabe (Download-, Batch-, Twitch-, GIF- und Konvertieren-Seite) wächst nicht
  mehr unbegrenzt und bremst die Oberfläche bei langen Vorgängen (z. B. großen Playlists)
  nicht mehr spürbar aus. Scrollt man während einer laufenden Ausgabe nach oben, um etwas
  nachzulesen, reißt eine neue Zeile die Ansicht jetzt nicht mehr nach unten.
- Auf der Konvertieren-Seite bleiben Auswahl und Scrollposition der Dateiliste jetzt während
  einer laufenden Konvertierung erhalten. Bisher baute sich die gesamte Liste bei jeder
  Fortschrittszeile neu auf, wodurch die Auswahl verloren ging und die Ansicht bei mehreren
  gleichzeitigen Konvertierungen sichtbar flackerte.
- Auf der Twitch-Seite erschien jede Zeile der yt-dlp-Ausgabe doppelt im Debug-Protokoll.
  Jetzt erscheint sie genau einmal, stderr-Zeilen weiterhin erkennbar markiert.

### Intern
- Liegt MortysDLP auf einem Netzlaufwerk, das über einen UNC-Pfad (`\\server\share\…`)
  angesprochen wird, weist die Startprotokollzeile zum Installationsort dies jetzt als
  `Network` aus. Bisher fehlte diese Angabe ausgerechnet beim Installationsort, der am
  häufigsten Probleme macht.
- Testabdeckung für die Grundbausteine (Pfad-/Dateinamensbehandlung, Protokollierung,
  Debug-Puffer, Installationsort-Erkennung, Prozessausführung) erweitert und gegen die
  wichtigsten Randfälle abgesichert.
- Einen gelegentlich fehlschlagenden Test in der Protokollierung stabilisiert.
- `CHANGELOG.md` und `docs/FUNKTIONEN.md` eingeführt. Ab jetzt wird jede Änderung
  fortlaufend dokumentiert, und `docs/FUNKTIONEN.md` beschreibt jederzeit den tatsächlichen
  Funktionsumfang der Anwendung.
- `.gitignore` ergänzt (Werkzeug- und Verlaufsdateien werden nie versioniert) und
  `.editorconfig` angelegt, damit alle Werkzeuge Code einheitlich formatieren.
- Gemeinsame Projekteinstellungen (`Directory.Build.props`) eingeführt und .NET-Analyzer
  aktiviert, damit mögliche Fehler früher auffallen. Ungenutzte Paketabhängigkeit
  `System.Configuration.ConfigurationManager` entfernt.
- Testprojekt eingeführt: `dotnet test` prüft jetzt automatisch die Auswahl von
  Video-Qualität und -Codec beim Download. Erste zehn Testfälle decken das bestehende
  Verhalten ab.
- Vier bestehende Compilerwarnungen behoben (mögliche Nullzugriffe in den
  Twitch-Häkchen und ein ungenutztes Ereignis im Whisper-Modellfenster). Verhalten für
  Nutzer unverändert. Mögliche Nullzugriffe lösen jetzt projektweit einen Build-Fehler
  statt nur einer Warnung aus, damit neue Fälle sofort auffallen.

---

## [2026.06.01] – 2026-06-01

### Hinzugefügt
- Twitch-Download: Video über yt-dlp, Chat über TwitchDownloaderCLI (JSON oder gerendertes
  MP4-Overlay), mit Qualitätsstufen Standard/Hoch/Ultra.
- Globales Bandbreitenlimit, das sich auch **während** eines laufenden Downloads ändern
  lässt — der Download wird dazu mit `--continue` neu gestartet.

## [2026.05.13] – 2026-05-13

### Hinzugefügt
- Batch-Download: Warteschlange mehrerer URLs, Sammel-Eingabefenster, Kontextmenü,
  Gesamtfortschritt.

## [2026.05.11] – 2026-05-11

### Hinzugefügt
- GIF-Maker als eigene Seite und als Nachbearbeitungsschritt auf der Download-Seite.

## [2026.05.07] – 2026-05-07

### Hinzugefügt
- Whisper-Transkription (whisper.cpp, vollständig offline) inkl. Modellverwaltung.

## [2026.04.04] – 2026-04-04

### Hinzugefügt
- Timeline-Fenster zur grafischen Auswahl eines Zeitausschnitts.

### Geändert
- Playlist-Unterstützung, robusterer Update-Ablauf, H.264-Prüfung nach dem Download.

---

> Ältere Einträge wurden nicht rückwirkend erfasst. Die vollständige Historie steht in den
> [GitHub-Releases](https://github.com/MortysTerminal/MortysDLP/releases).

[Unreleased]: https://github.com/MortysTerminal/MortysDLP/compare/2026.06.01...HEAD
[2026.06.01]: https://github.com/MortysTerminal/MortysDLP/releases/tag/2026.06.01
[2026.05.13]: https://github.com/MortysTerminal/MortysDLP/releases/tag/2026.05.13
[2026.05.11]: https://github.com/MortysTerminal/MortysDLP/releases/tag/2026.05.11
[2026.05.07]: https://github.com/MortysTerminal/MortysDLP/releases/tag/2026.05.07
[2026.04.04]: https://github.com/MortysTerminal/MortysDLP/releases/tag/2026.04.04
