# Benutzung

> 🇬🇧 English version: [../en/usage.md](../en/usage.md)  
> ← [Installation](installation.md) | [Transkription →](transkription.md)

---

## Inhaltsverzeichnis

- [Tab „Download"](#tab-download)
- [Tab „Konvertieren"](#tab-konvertieren)
- [Tab „Transkribieren"](#tab-transkribieren)
- [Tab „Einstellungen"](#tab-einstellungen)
- [Updates](#updates)

---

## Tab „Download"

Der Tab **Download** ist die Hauptfunktion von MortysDLP.

### Grundablauf

1. URL in das Feld **URL eingeben:** einfügen
2. Optionen konfigurieren (siehe [Optionen](#optionen))
3. Auf **Download starten** klicken — Fortschritt und Status werden in Echtzeit angezeigt
4. Nach dem Download liegt die Datei im konfigurierten Download-Pfad

### Optionen

| Option | Beschreibung |
|---|---|
| **Download-Pfad** / **Download-Audio-Pfad** | Speicherort für heruntergeladene Dateien. Einstellbar unter **Einstellungen** → **Download-Pfad ändern**. |
| **NUR Audio** | Lädt nur den Audiostream herunter. Aktiviert die **Bitrate**-Auswahl. |
| **Bitrate** | Audioqualität bei aktiviertem *NUR Audio*: Höchste, 320k, 256k, 192k, 160k, 128k, 96k, 64k |
| **Videoqualität** | Zielauflösung: Höchste, 1440p, 1080p, 720p, 480p, 360p, 240p, 144p |
| **Videoformat** | Ausgabe-Container: `mp4`, `mkv`, `mov`, `avi` |
| **Videoformat für Schnittprogramme (x264)** | Re-Encoding nach H.264 — maximale Kompatibilität mit DaVinci Resolve, Premiere Pro usw. Nicht kombinierbar mit *NUR Audio*. |
| **Zeitspanne von … bis …** | Lädt nur einen Abschnitt des Videos herunter. Format: `hh:mm:ss` oder `mm:ss`. Der **Timeline**-Button öffnet eine visuelle Auswahl. |
| **Von Start bis:** | Nur die ersten N **Sekunden** herunterladen. |
| **Benutzerdefinierten Videotitel verwenden** | Legt den Dateinamen manuell fest. Ungültige Zeichen werden automatisch bereinigt. |
| **Nach dem Download automatisch transkribieren** | Transkribiert die heruntergeladene Datei nach dem Download automatisch mit Whisper. Erfordert installiertes Whisper und mindestens ein Modell. |

### Verlauf

Klicke auf **Verlauf**, um den Download-Verlauf zu öffnen. Wähle einen früheren Eintrag und klicke auf **Erneut herunterladen**, um eine URL mit ihren ursprünglichen Einstellungen wiederzuverwenden. Mit **Verlauf leeren** werden alle Einträge gelöscht.

### Playlist-Erkennung

Wenn du eine YouTube-Playlist-URL einfügst, fragt MortysDLP, ob du die **gesamte Playlist** oder nur das **einzelne Video** herunterladen möchtest. Der Fortschritt wird pro Video angezeigt (*„Video 1/12"* usw.).

### Download abbrechen

Klicke jederzeit auf **Download abbrechen**. Der laufende Download stoppt und der Status wechselt zu *„Abgebrochen"*.

### Einstellungen speichern

Klicke auf **Einstellungen speichern**, um die aktuellen Optionen als Standard zu übernehmen. Ein Bestätigungsdialog erscheint.

---

## Tab „Konvertieren"

Der Tab **Konvertieren** ermöglicht es, lokale Mediendateien mit ffmpeg in ein anderes Format umzuwandeln.

### Ablauf

1. Klicke auf **Dateien hinzufügen** und wähle eine oder mehrere Mediendateien (mp4, mkv, mov, avi, mp3, aac, wav, flac, opus, …)
2. Setze das **Zielformat** (z. B. `mp4`, `mp3`, `wav`, …)
3. Setze den **Zielordner** — oder nutze die Schnellschaltflächen **Downloadpfad** / **Audio-Only-Pfad** oder **Durchsuchen…**
4. Optional **Videoqualität** und **Audioqualität** anpassen (Standard: `Original (verlustfrei)`)
5. Klicke auf **Konvertierung starten** — jede Datei zeigt eigenen Fortschrittsbalken und Status

### Dateiverwaltung

| Schaltfläche | Funktion |
|---|---|
| **Dateien hinzufügen** | Mediendateien zur Liste hinzufügen |
| **Entfernen** | Ausgewählte Datei aus der Liste entfernen |
| **Liste leeren** | Alle Dateien aus der Liste entfernen |

### Nach der Konvertierung

- Jede Datei zeigt den Status: *Wird konvertiert…* → *Fertig* / *Fehler* / *Abgebrochen* / *Schon konvertiert*
- Klicke auf **Ordner öffnen**, um den Zielordner im Windows-Explorer zu öffnen
- Klicke auf **Konvertierung abbrechen**, um alle laufenden Konvertierungen zu stoppen

> **Hinweis:** Dateien, die bereits im Zielformat vorliegen, werden als *„Schon konvertiert"* markiert und übersprungen.

---

## Tab „Transkribieren"

Alle Details findest du in der dedizierten [Transkriptions-Anleitung](transkription.md).

**Kurzübersicht:**

1. **Whisper verwalten** öffnen — Whisper-Engine installieren und mindestens ein Modell herunterladen
2. Eingabedatei über **Durchsuchen…** (neben **Datei auswählen:**) wählen
3. **Whisper-Modell**, **Sprache** und Ausgabeformat(e) (`.txt`, `.srt`, `.vtt`) auswählen
4. **Ausgabeordner:** festlegen oder **Durchsuchen…** nutzen
5. Auf **Transkription starten** klicken — Fortschritt in Echtzeit
6. Nach Abschluss auf **Ordner öffnen** klicken

---

## Tab „Einstellungen"

| Einstellung | Beschreibung |
|---|---|
| **Download-Pfad ändern** | Öffnet einen Dialog zum Festlegen des Standard-Download-Ordners und eines optionalen separaten **Audio-Only-Pfads** |
| **Sprache auswählen** | Wechsel zwischen *Automatisch (Systemsprache)*, *Deutsch* und *Englisch*. Wird automatisch gespeichert. |
| **Debug-Modus aktivieren (zeigt Download-Details)** | Zeigt die rohe yt-dlp/ffmpeg-Ausgabe in einem Debug-Panel auf jedem Tab |
| **GitHub öffnen** | Öffnet die MortysDLP-Projektseite im Browser |
| **Programm schließen** | Beendet MortysDLP |

---

## Updates

Wenn eine neue Version von MortysDLP verfügbar ist, erscheint oben im Hauptfenster ein Banner:

> *„Neue Version X.Y verfügbar — Klicken für Details & Update"*

Ein Klick auf den Banner öffnet ein Changelog-Fenster mit den Release Notes. Von dort aus kannst du wählen:

- **Jetzt aktualisieren** — lädt das Update herunter und installiert es automatisch
- **Später** — schließt den Dialog; der Banner kann mit **Schließen** weggeklickt werden

Der Update-Prozess läuft im Hintergrund. MortysDLP startet nach dem Update automatisch neu.

---

← [Installation](installation.md) | [Transkription →](transkription.md)
