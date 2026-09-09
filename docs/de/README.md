# MortysDLP - Übersicht

English version: [docs/en/README.md](../en/README.md)
← Zurück zur Hauptseite: [README.md](../../README.md)

**MortysDLP** ist eine Windows-Desktop-App zum Herunterladen und Umwandeln von Videos und Audio aus dem Web. Im Hintergrund laufen [yt-dlp](https://github.com/yt-dlp/yt-dlp), [ffmpeg](https://ffmpeg.org/) und [whisper.cpp](https://github.com/ggml-org/whisper.cpp), eingebettet in eine Fluent-Oberfläche, ganz ohne Kommandozeile.

---

## Warum es das gibt

Ich mache Content und hänge dabei immer wieder an derselben Stelle fest. Ein Clip, den ich brauche, gibt es nur gegen "Jetzt anmelden für den HD-Download". Eine schnelle Formatumwandlung will ein Monatsabo. Die Hälfte der "kostenlosen" Seiten wird zur Abo-Falle, sobald man klickt.

MortysDLP ist mein Ausweg daraus. Download und Umwandlung laufen auf dem eigenen Rechner, mit den Werkzeugen, die diese Aufgabe ohnehin gut können, verpackt in etwas, wofür man keine Kommandozeile braucht. Ich habe es für genau die Momente gebaut, in denen die Content-Arbeit ins Stocken gerät.

## Was die Software nicht tut

- **Keine Cloud.** Alles läuft auf deinem PC. Nichts wird hochgeladen, nichts auf einem fremden Server verarbeitet.
- **Keine Telemetrie.** Die App funkt nicht nach Hause, zählt keine Downloads, sammelt keine Nutzungsdaten.
- **Keine Werbung, keine Upsells, keine Bezahlschranken.** Es gibt keine "Pro"-Version. Es gibt nichts freizuschalten.
- **Nichts, was die Arbeit stört.** Kein Konto, kein Login, keine Pop-ups, die zum Upgrade drängen.

Ehrliche Software. Sie hilft und geht dann aus dem Weg.

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
- **x264-Modus**: Re-Encoding nach H.264 für maximale Kompatibilität mit Schnittprogrammen (DaVinci Resolve, Premiere Pro und andere)
- **Zeitspanne**: nur einen Abschnitt herunterladen (`hh:mm:ss` oder `mm:ss`); der Timeline-Knopf öffnet eine visuelle Auswahl
- **Von Start bis**: nur die ersten N Sekunden eines Videos herunterladen
- Benutzerdefinierten Dateinamen festlegen
- Echtzeit-Fortschrittsanzeige mit Downloadgeschwindigkeit
- **Verlauf**: frühere Downloads wiederverwenden, jeweils mit Zielordner
- **GIF-Maker**: aus dem geladenen Video zusätzlich ein GIF erzeugen
- **Transkription nach Download**: die fertige Datei im Anschluss automatisch transkribieren

### Batch-Download
- Mehrere URLs in einer Liste verwalten und nacheinander herunterladen
- Das globale Downloadlimit gilt auch hier
- Erfolgreiche Downloads landen ebenfalls im Verlauf

### Konvertieren
- Lokale Mediendateien in ein anderes Format umwandeln, mehrere Dateien auf einmal
- Wählbares Zielformat, Videoqualität und Audioqualität

### Transkribieren
- Video- oder Audiodateien vollständig **offline** in Text umwandeln, keine Daten verlassen den PC
- Basiert auf [whisper.cpp](https://github.com/ggml-org/whisper.cpp) (OpenAI Whisper, lokal ausgeführt)
- Ausgabeformate: `.txt`, `.srt`, `.vtt`
- Automatische Spracherkennung oder manuelle Auswahl (19+ Sprachen)
- Sechs Modellgrößen zur Auswahl (Tiny bis Large-v3)
- Modelle werden direkt in der App heruntergeladen und verwaltet

### Twitch VOD & Clip
- Video-Download via yt-dlp
- Chat-Download und -Rendering (MP4-Overlay) via TwitchDownloaderCLI
- Beide Werkzeuge werden separat verwaltet

### App-übergreifend
- **Globales Downloadlimit**: Bandbreite in MB/s begrenzen, wirkt auf alle Download-Typen
- **Automatische Werkzeug-Verwaltung**: yt-dlp, ffmpeg und ffprobe werden beim ersten Start heruntergeladen; yt-dlp wird im Hintergrund aktuell gehalten
- **Optionale Updates**: dezenter Hinweisbanner mit vollständigem Changelog
- Fluent Design, Light- und Dark-Mode (folgt der Windows-Systemeinstellung)
- Deutsch und Englisch, ohne Neustart umschaltbar, wählbare Oberflächenschrift

---

## Zum Code und zu KI

Ich setze [Claude](https://www.anthropic.com/claude) ein, um diesen Code zu prüfen, aufzuräumen und zu verbessern, und ich sage das lieber offen. Es ist ein Werkzeug, und ein gutes. Die Richtung, die Entscheidungen und die Verantwortung liegen bei mir. Claude hilft mir, mit weniger rauen Kanten dorthin zu kommen.

---

Weiter mit [Installation & Einrichtung](installation.md)
