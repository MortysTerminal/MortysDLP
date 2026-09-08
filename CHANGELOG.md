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

### Hinzugefügt
- Neue Einstellung **Schrift**: Die Oberflächenschrift lässt sich jetzt auswählen — Arial
  (empfohlen), Inter und Inter kompakt/luftig (mitgeliefert), sowie Segoe UI, Verdana, Georgia,
  Tahoma und Comic Sans MS vom System. Wirkt sofort ohne Neustart und wird dauerhaft
  gespeichert. Die bisher fest verwendete enge Variante (Inter Tight) war für manche schwerer
  zu lesen.

### Behoben
- Der Hinweis auf ein verfügbares Update ist wieder deutlich sichtbar: Der Banner steht auf
  vollem Markengelb mit dunkler, fetter Schrift, statt sich fast unsichtbar in die Oberfläche
  einzufügen.
- Das Änderungen-Fenster („Was ist neu" bzw. der Update-Hinweis) zeigt jetzt auch dann sauber
  formatierte Überschriften, Listen und Hervorhebungen an, wenn die Release-Notiz HTML enthält.
  Vorher standen die HTML-Auszeichnungen (`<h2>`, `<li>`, …) als Text im Fenster.

### Intern
- Alle Analyzer-Warnungen aus dem Build beseitigt (Ausgangspunkt: 158). Explizite Kultur bei
  String-Vergleichen und Zahlen-Formatierung, aufgeräumte Hilfsmethoden, neuer Helfer
  `UITextDictionary.Format`. Kein Verhaltenswechsel. Der Build ist jetzt warnungsfrei.

## [2026.09.07] – 2026-09-07

Großes Reifungs-Release: der Schwerpunkt liegt auf Robustheit, einem verlässlichen
Selbst-Update, der Verwaltung der externen Werkzeuge und einer einheitlichen Oberfläche.

### Sicherheit
- Heruntergeladene Pakete (Anwendungs-Updates und yt-dlp) werden vor der Installation gegen
  Prüfsumme und erwartete Größe geprüft; fehlt eine Prüfsumme, steht das im Protokoll statt
  unbemerkt zu bleiben.
- Das ffmpeg-Archiv wird beim Entpacken gegen präparierte Archive und „Zip-Bomben“ abgesichert;
  es werden nur die beiden benötigten Programmdateien herausgeholt.
- Härtung der Argumentübergabe an alle externen Werkzeuge: ungewöhnliche Sonderzeichen in URLs
  oder Videotiteln können ihnen keine zusätzlichen Befehlszeilenargumente mehr unterschieben.
- Netzwerkanfragen gehen nur noch an bekannte, verschlüsselte Adressen. Die Internet-
  Kennzeichnung (Mark-of-the-Web) wird nur bei Werkzeugen entfernt, deren Prüfsumme gestimmt hat.

### Hinzugefügt
- **Neuer Tab „Werkzeuge“:** Zustand, Version, Speicherort und Größe aller externen Werkzeuge
  an einer Stelle, mit Reparieren, Aktualisieren, Deinstallieren und Ordner öffnen. Ein Bereich
  fasst den Stand der Whisper-Modelle zusammen, eine Zeile den Gesamtplatzbedarf.
- **Anwendungs-Updates** zeigen einen Fortschrittsbalken und lassen sich abbrechen; nach einem
  erfolgreichen Update erscheint einmalig eine Bestätigung mit „Was ist neu“. Vor dem Angebot
  wird geprüft, ob am Installationsort überhaupt aktualisiert werden kann (geschützter Ordner,
  ZIP-Vorschau). Läuft gerade ein Download, eine Konvertierung oder eine Transkription, wird
  nachgefragt.
- **Erfolgskontrolle nach dem Update:** Beim nächsten Start prüft MortysDLP, ob es gewirkt hat,
  und meldet das Ergebnis. Ein Update, das zweimal ohne Wirkung blieb, wird nicht mehr
  automatisch angeboten („Trotzdem erneut versuchen“ bleibt).
- Der Update-Hinweis lässt sich pro Version dauerhaft überspringen („Diese Version überspringen“).
- Unerwartete Fehler beenden MortysDLP nicht mehr wortlos: Sie landen in einer Protokolldatei
  (`%LOCALAPPDATA%\MortysDLP\logs\`) und in einem Dialog mit Details zum Kopieren.
- Die Download-Seite zeigt jetzt eine geschätzte Restzeit.
- Startet MortysDLP aus der ZIP-Vorschau des Explorers, erklärt ein Hinweis, warum
  heruntergeladene Werkzeuge nach jedem Neustart fehlen.
- Fehlt ein Werkzeug, das eine Seite zwingend braucht (yt-dlp oder ffmpeg), zeigt die Seite eine
  Karte mit dem fehlenden Werkzeug und einem Knopf zur Werkzeuge-Seite, statt einen Vorgang zu
  starten, der später abbricht.

### Geändert
- **Einheitliche Oberfläche über alle Tabs:** gemeinsames Raster für Abstände, gleiche Position
  der Eingabefelder, dieselben vier Statusfarben (hell und dunkel geprüft),
  Windows-11-Bedienelemente mit der Markenfarbe (der Tastatur-Fokusrahmen ist zurück).
- **Neues Branding:** wärmere, ruhigere Marken-Akzentfarben, eine mitgelieferte Schrift (Inter)
  statt der Systemschrift, ein flacher dunkelgrauer Fensterhintergrund statt des
  durchschimmernden Mica-Materials.
- Jede Seite hat oben eine kurze Zeile, die sagt, was sie tut; die ausführliche Erklärung sitzt
  hinter einem Info-Knopf. Die Start-/Abbrechen-Knöpfe stehen überall links.
- Die Fortschritts-/Aktionsleiste bleibt beim Scrollen sichtbar. Die Warteschlangen-Seite zeigt
  den Fortschritt jetzt mit zwei Balken wie die Download-Seite. Prozentangaben sind überall
  ganzzahlig.
- Die kleinste unterstützte Fenstergröße ist jetzt 1100 × 700 (passt auf 1366 × 768 und bei
  150 % Skalierung).
- **Externe Werkzeuge liegen jetzt im Nutzerprofil** statt im Programmordner. So lassen sie
  sich auch aktualisieren, wenn MortysDLP in `C:\Program Files` liegt. Vorhandene Werkzeuge
  einer älteren Installation werden beim ersten Start übernommen.
- **Der Start ist deutlich schneller:** Die yt-dlp-Version wird aus den Dateieigenschaften
  gelesen statt das Programm zu starten (spart mehrere Sekunden), Werkzeuge werden parallel
  geprüft, und alle Versions- und Update-Prüfungen laufen erst im Hintergrund, nachdem das
  Fenster offen ist.
- **Die Update-Prüfung** nutzt fünf voneinander unabhängige Quellen als Ausweichkette, prüft
  höchstens alle 6 Stunden online und verwendet ohne Internet den zuletzt bekannten Stand. Der
  Versionsvergleich erkennt jetzt auch Hotfix- und Vorab-Tags korrekt.
- **Werkzeug-Updates** sichern die alte Fassung, prüfen die neue nach dem Einsetzen und stellen
  bei einem Fehlschlag automatisch die alte wieder her. ffmpeg und ffprobe werden gemeinsam
  behandelt. Ein ffmpeg-Update wird nur angeboten, nie erzwungen; ein Downgrade oder ein
  dauerhaftes Angebot durch abweichende Schreibweisen kann nicht mehr entstehen.
- Der Installations-Updater ist jetzt vollständig quelloffen und wird bei jedem Release neu
  gebaut.
- Netzabfragen laufen über eine gemeinsame Verwaltung mit Wiederholstrategie; ein erschöpftes
  GitHub-Kontingent wird als solches erkannt statt als „kein Update“.
- Die Twitch-Seite zeigt den Download-Bereich erst, wenn TwitchDownloaderCLI installiert ist.
  Die Werkzeuge-Seite kennzeichnet erforderliche Werkzeuge und nennt die Funktionen, die sie
  brauchen.

### Behoben
- **Ein installiertes Update wurde nach dem Neustart nicht erkannt:** Die alte Versionsnummer
  blieb stehen und der Update-Banner erschien endlos wieder. Behoben; gespeicherte Einstellungen
  bleiben über ein Update erhalten.
- **Downloads von GitHub schlugen vollständig fehl** („Ziel nicht erlaubt“). Betroffen waren
  Werkzeuge und das Selbst-Update. GitHub hatte den Auslieferungsserver gewechselt.
- **Der Fortschrittsbalken beim Herunterladen** sprang mehrfach von 0 los, blieb bei manchen
  Downloads ganz stehen, zuckte vor und zurück oder sprang bei einem Bandbreitenwechsel. Er
  läuft jetzt einmal gleichmäßig über den gesamten Vorgang, auch über eine ganze Playlist.
  Geschwindigkeit und Restzeit werden geglättet und höchstens einmal pro Sekunde aktualisiert.
- **Das Rendern des Twitch-Chats als Video** brach immer mit „Unable to find FFmpeg“ ab.
  TwitchDownloaderCLI wusste nichts vom mitgelieferten ffmpeg.
- Eine fremde oder beschädigte Datei unter dem Namen eines Werkzeugs wird jetzt erkannt und
  abgelehnt, statt zu unerklärlichen Fehlschlägen zu führen.
- Externe Werkzeuge und der Download-Verlauf werden jetzt unabhängig vom Startort zuverlässig
  gefunden (Verknüpfung mit anderem Arbeitsverzeichnis, Administrator, Aufgabenplanung); beide
  liegen an einem festen Ort unter `%LOCALAPPDATA%\MortysDLP\`.
- Ein abgebrochener Whisper-Modell-Download zählt nicht mehr als installiertes Modell; die Liste
  zeigt jetzt „nicht vorhanden / unvollständig / vollständig“.
- Auf der Konvertieren-Seite bleiben Auswahl und Scrollposition während einer laufenden
  Konvertierung erhalten. Auf der Twitch-Seite erscheint jede Ausgabezeile nur noch einmal im
  Protokoll. Die Debug-Ausgaben wachsen nicht mehr unbegrenzt.
- Alle Werkzeugaufrufe haben jetzt ein Zeitlimit und verwenden UTF-8; beim Abbrechen wird der
  gesamte Prozessbaum beendet (kein ffmpeg mehr im Hintergrund, keine kaputten Umlaute).
- Dateinamen mit reservierten Windows-Namen (`NUL`, `CON`) oder abschließenden Punkten führen
  nicht mehr zu Fehlern. Eine beschädigte Verlaufsdatei lässt den Verlauf nicht mehr abstürzen.
- Schlägt nur die H.264-Nachkonvertierung fehl, nennt die Statuszeile diesen Schritt statt
  pauschal „Fehler beim Download“. Das Fortschrittsfenster friert bei 100 % nicht mehr scheinbar
  ein, und der Abbrechen-Knopf ist bei großer Anzeigeskalierung wieder erreichbar.
- Ein Video-Download im Schnittmodus (x264) startete unnötig langsam durch eine überflüssige
  Audio-Abfrage.

### Intern
- Wiederkehrende Abläufe stecken jetzt in gemeinsamen Bausteinen: Prozessausführung,
  Netzwerkverwaltung, Versionsvergleich, geprüfter Download, Werkzeug-Abstraktion. .NET-Analyzer
  aktiviert, mögliche Nullzugriffe sind projektweit ein Build-Fehler.
- Deutlich erweiterte Testabdeckung (Update-Kette, Werkzeugverwaltung, Fortschritt,
  Versionsvergleich, Prozess- und Netzschicht), überwiegend ohne echten Netzzugriff.
- Der Installations-Updater hat einen eigenen, vollständigen Quellcode mit eigenem Protokoll,
  Backup/Rollback, Zip-Prüfung und geordnetem Warten auf das Ende der Anwendung.

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

[Unreleased]: https://github.com/MortysTerminal/MortysDLP/compare/2026.09.07...HEAD
[2026.09.07]: https://github.com/MortysTerminal/MortysDLP/compare/2026.06.01...2026.09.07
[2026.06.01]: https://github.com/MortysTerminal/MortysDLP/releases/tag/2026.06.01
[2026.05.13]: https://github.com/MortysTerminal/MortysDLP/releases/tag/2026.05.13
[2026.05.11]: https://github.com/MortysTerminal/MortysDLP/releases/tag/2026.05.11
[2026.05.07]: https://github.com/MortysTerminal/MortysDLP/releases/tag/2026.05.07
[2026.04.04]: https://github.com/MortysTerminal/MortysDLP/releases/tag/2026.04.04
