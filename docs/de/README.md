# MortysDLP – Übersicht

English version: [docs/en/README.md](../en/README.md)  
← Zurück zur Hauptseite: [README.md](../../README.md)

**MortysDLP** ist eine Windows-Desktop-App zum Herunterladen von Videos und Audio aus dem Web. Im Hintergrund laufen [yt-dlp](https://github.com/yt-dlp/yt-dlp) und [ffmpeg](https://ffmpeg.org/) — eingebettet in eine Fluent-Oberfläche, ganz ohne Kommandozeile.

---

## Inhaltsverzeichnis

- [Funktionen](#funktionen)
- [Installation & Einrichtung](installation.md)
- [Benutzung](benutzung.md)
- [Transkription](transkription.md)

---

## Funktionen

### Download
- Videos und Audio von jeder von yt-dlp unterstützten URL herunterladen (YouTube, Twitch und hunderte weitere)
- **Nur Audio**-Modus mit wählbarem Format: `aac`, `alac`, `flac`, `m4a`, `mp3`, `opus`, `vorbis`, `wav`
- Wählbare **Bitrate**: Höchste, 320k, 256k, 192k, 160k, 128k, 96k, 64k
- **Videoqualität**: Beste, 1440p, 1080p, 720p, 480p, 360p, 240p, 144p
- **Videoformat** (Container): `mp4`, `mkv`, `mov`, `avi`
- **x264-Modus** — Re-Encoding nach H.264 für maximale Kompatibilität mit Schnittprogrammen (DaVinci Resolve, Premiere Pro usw.)
- **Zeitspanne** — nur einen Abschnitt herunterladen (`hh:mm:ss` oder `mm:ss`); der Timeline-Button öffnet eine visuelle Auswahl
- **Von Start bis** — nur die ersten N Sekunden eines Videos herunterladen
- Benutzerdefinierten Dateinamen festlegen
- Echtzeit-Fortschrittsanzeige mit Downloadgeschwindigkeit
- **Verlauf** — frühere URLs wiederverwenden
- **Transkription nach Download** — automatische Transkription im Anschluss

### Batch-Download
- Mehrere URLs gleichzeitig in einer Liste verwalten und herunterladen
- Globales Downloadlimit gilt auch hier

### Konvertieren
- Lokale Mediendateien in ein anderes Format umwandeln, mehrere Dateien auf einmal
- Wählbares Zielformat, Videoqualität und Audioqualität

### Transkribieren
- Video- oder Audiodateien vollständig **offline** in Text umwandeln — keine Daten verlassen den PC
- Basiert auf [whisper.cpp](https://github.com/ggerganov/whisper.cpp) (OpenAI Whisper, lokal ausgeführt)
- Ausgabeformate: `.txt`, `.srt`, `.vtt`
- Automatische Spracherkennung oder manuelle Auswahl (19+ Sprachen)
- Sechs Modellgrößen zur Auswahl (Tiny bis Large-v3)
- Modelle werden direkt in der App heruntergeladen und verwaltet

### Twitch VOD & Clip
- Video-Download via yt-dlp
- Chat-Download und -Rendering (MP4-Overlay) via TwitchDownloaderCLI
- Beide Tools werden separat verwaltet

### App-übergreifend
- **Globales Downloadlimit** — Bandbreite in MB/s begrenzen, wirkt auf alle Download-Typen
- **Automatische Tool-Verwaltung** — yt-dlp, ffmpeg und ffprobe werden beim ersten Start heruntergeladen; yt-dlp wird im Hintergrund aktuell gehalten
- **Optionale Updates** — dezenter Hinweisbanner mit vollständigem Changelog
- Fluent Design, Light-/Dark-Mode (folgt Windows-Systemeinstellung)
- Deutsch und Englisch, ohne Neustart umschaltbar

---

Weiter mit [Installation & Einrichtung](installation.md)
