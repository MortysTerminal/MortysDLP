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

### Intern
- `CHANGELOG.md` und `docs/FUNKTIONEN.md` eingeführt. Ab jetzt wird jede Änderung
  fortlaufend dokumentiert, und `docs/FUNKTIONEN.md` beschreibt jederzeit den tatsächlichen
  Funktionsumfang der Anwendung.

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
