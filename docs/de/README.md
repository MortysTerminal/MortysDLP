# MortysDLP – Übersicht

> 🇬🇧 English version: [docs/en/README.md](../en/README.md)  
> ← Zurück zur Hauptseite: [README.md](../../README.md)

**MortysDLP** ist eine moderne WPF-Desktop-App zum Herunterladen von Videos und Audio aus dem Internet unter Windows. Im Hintergrund laufen [yt-dlp](https://github.com/yt-dlp/yt-dlp) und [ffmpeg](https://ffmpeg.org/) — eingebettet in eine aufgeräumte Fluent-Oberfläche, ganz ohne Kommandozeile.

---

## 📖 Inhaltsverzeichnis

- [Funktionen](#funktionen)
- [Installation & Einrichtung](installation.md)
- [Benutzung](benutzung.md)
- [Transkription](transkription.md)

---

## Funktionen

### Tab „Download"
- Videos und Audio von jeder von yt-dlp unterstützten URL herunterladen (YouTube, Twitch und hunderte weitere)
- **NUR Audio**-Modus mit wählbarem Format: `aac`, `alac`, `flac`, `m4a`, `mp3`, `opus`, `vorbis`, `wav`
- Wählbare **Bitrate**: Höchste, 320k, 256k, 192k, 160k, 128k, 96k, 64k
- **Videoqualität**: Beste, 1440p, 1080p, 720p, 480p, 360p, 240p, 144p
- **Videoformat** (Container): `mp4`, `mkv`, `mov`, `avi`
- **Videoformat für Schnittprogramme (x264)** — Re-Encoding nach H.264 für maximale Kompatibilität mit DaVinci Resolve, Premiere Pro usw. (nicht kombinierbar mit *NUR Audio*)
- **Zeitspanne** — nur einen Abschnitt herunterladen (Format: `hh:mm:ss` oder `mm:ss`); der **Timeline**-Button öffnet eine visuelle Auswahl
- **Von Start bis:** — nur die ersten N **Sekunden** eines Videos herunterladen
- **Benutzerdefinierten Videotitel verwenden** — Dateinamen manuell festlegen
- Separate Download-Pfade für Video und Audio-Only
- Echtzeit-Fortschrittsanzeige und Statusmeldungen
- **Verlauf** — frühere Download-URLs wiederverwenden
- **Transkription nach Download** — automatische Transkription nach dem Download mit Whisper

### Tab „Konvertieren"
- Lokale Mediendateien in ein anderes Format umwandeln
- Mehrere Dateien gleichzeitig verarbeiten (**Dateien hinzufügen**)
- Wählbares Zielformat, Zielordner, **Videoqualität** und **Audioqualität**
- Einzelner Fortschrittsbalken pro Datei
- **Ordner öffnen** nach der Konvertierung

### Tab „Transkribieren"
- Video- oder Audiodateien in Text umwandeln — vollständig **offline**, keine Daten verlassen deinen PC
- Basiert auf [whisper.cpp](https://github.com/ggerganov/whisper.cpp) (OpenAI Whisper, lokal)
- Ausgabeformate: Textdatei (`.txt`), Untertitel (`.srt`), WebVTT (`.vtt`)
- SRT-Dateien funktionieren direkt in DaVinci Resolve, Premiere Pro und den meisten Schnittprogrammen
- Automatische Spracherkennung oder manuelle Auswahl (19+ Sprachen)
- **Sechs Modellgrößen** — Auswahl zwischen Geschwindigkeit und Genauigkeit: Tiny, Base, Small, Medium, Large-v3-Turbo, Large-v3
- Whisper-Engine und Sprachmodelle werden direkt in der App heruntergeladen und verwaltet

### Tab „Einstellungen"
- **Download-Pfad ändern** — Standard-Pfade für Video und Audio-Only festlegen
- **Sprache auswählen** — Automatisch (Systemsprache), Deutsch, Englisch; Auswahl wird automatisch gespeichert
- **Debug-Modus aktivieren (zeigt Download-Details)** — zeigt die rohe yt-dlp/ffmpeg-Ausgabe
- **GitHub öffnen** — öffnet die Projektseite im Browser
- **Programm schließen**

### App-übergreifend
- **Automatische Tool-Verwaltung** — yt-dlp, ffmpeg und ffprobe werden beim ersten Start automatisch heruntergeladen; yt-dlp wird im Hintergrund aktuell gehalten
- **Optionale Software-Updates** — ein dezenter Banner (*„Neue Version X.Y verfügbar"*) erscheint oben im Fenster; das vollständige Changelog wird angezeigt, bevor du dich entscheidest
- **Fluent Design** mit vollem Light-/Dark-Mode-Support (folgt der Windows-Systemeinstellung)
- Deutsch und Englisch, ohne Neustart umschaltbar

---

➡️ Weiter mit [Installation & Einrichtung](installation.md)
