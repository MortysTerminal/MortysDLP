# Transkription

> 🇬🇧 English version: [../en/transcription.md](../en/transcription.md)  
> ← [Benutzung](benutzung.md) | [Übersicht →](README.md)

MortysDLP enthält eine vollständig **offline** arbeitende Sprache-zu-Text-Funktion, die auf [whisper.cpp](https://github.com/ggerganov/whisper.cpp) basiert — einer optimierten lokalen Implementierung von OpenAIs Whisper-Modell. Es werden keinerlei Audiodaten an einen Cloud-Dienst übertragen. Kein API-Schlüssel, kein Abo, keine Internetverbindung während der Transkription nötig.

---

## Inhaltsverzeichnis

- [Was ist Transkription?](#was-ist-transkription)
- [Einrichtung](#einrichtung)
- [Den Tab „Transkribieren" nutzen](#den-tab-transkribieren-nutzen)
- [Ausgabeformate](#ausgabeformate)
- [Sprachmodelle](#sprachmodelle)
- [Whisper verwalten](#whisper-verwalten)
- [Transkription nach Download](#transkription-nach-download)

---

## Was ist Transkription?

Transkription wandelt gesprochene Sprache in einer Video- oder Audiodatei in Text um. MortysDLP verwendet dafür **Whisper** — ein quelloffenes KI-Modell, das ursprünglich von OpenAI veröffentlicht wurde. Die hier eingesetzte Variante (`whisper.cpp`) läuft vollständig auf der CPU/GPU, lokal auf deinem PC.

**Unterstützte Eingabeformate:** `mp4`, `mkv`, `mov`, `avi`, `mp3`, `wav`, `flac`, `m4a`, `ogg`, `opus` und weitere.

**Unterstützte Ausgabeformate:** `.txt` (reiner Text), `.srt` (Untertitel mit Zeitstempeln), `.vtt` (WebVTT)

---

## Einrichtung

Der Tab **Transkribieren** führt dich in zwei Schritten durch die Einrichtung:

### Schritt 1: Whisper installieren

Der Tab zeigt:
> *„Schritt 1: Whisper installieren"*

Klicke auf **Whisper installieren**. Die App lädt [whisper.cpp](https://github.com/ggerganov/whisper.cpp) herunter und installiert es im MortysDLP-Ordner. Kein Python, keine virtuellen Umgebungen, keine Kommandozeile nötig.

Nach der Installation ändert sich der Status zu:
> *„✔ Schritt 1: Whisper installiert"*

### Schritt 2: Sprachmodell herunterladen

Klicke auf **Whisper verwalten**, um den Modell-Manager zu öffnen. Klicke neben dem gewünschten Modell auf **Herunterladen**. Sobald der Status **✔ Installiert** anzeigt, ist alles bereit.

> *„✔ Schritt 2: Modell installiert"*

---

## Den Tab „Transkribieren" nutzen

Sobald Whisper und ein Modell installiert sind:

1. **Datei auswählen:** — auf **Durchsuchen…** klicken und eine Video- oder Audiodatei wählen
2. **Whisper-Modell** — gewünschtes Modell auswählen (Details [unten](#sprachmodelle))
3. **Sprache** — gesprochene Sprache wählen oder **Automatisch erkennen** für automatische Erkennung
4. **Ausgabeformat** — eines oder mehrere auswählen:
   - **Textdatei (.txt)** — reiner Text, keine Zeitstempel
   - **Untertitel (.srt) – für Schnittprogramme** — Untertitel mit Zeitstempeln, kompatibel mit DaVinci Resolve, Premiere Pro usw.
   - **WebVTT (.vtt)** — Untertitel für Web-Player
5. **Ausgabeordner:** — festlegen, wo die Ausgabedateien gespeichert werden, oder **Durchsuchen…** nutzen
6. Auf **Transkription starten** klicken

Der Fortschritt wird in Echtzeit angezeigt. Wenn die Transkription abgeschlossen ist, erscheint ein Dialog, der anbietet, den Ausgabeordner direkt zu öffnen. Du kannst auch jederzeit auf **Ordner öffnen** klicken.

Klicke auf **Abbrechen**, um die Transkription zu stoppen.

---

## Ausgabeformate

| Format | Beschreibung | Am besten für |
|---|---|---|
| `.txt` | Reiner Text — nur die gesprochenen Worte, keine Zeitstempel | Lesen, Kopieren, Notizen |
| `.srt` | SubRip-Untertitel — Zeilen mit Zeitstempeln | DaVinci Resolve, Premiere Pro, VLC, die meisten Schnittprogramme |
| `.vtt` | WebVTT — Zeitstempel, web-kompatibel | HTML5-Videoplayer, Streaming |

Mehrere Formate können gleichzeitig ausgewählt werden. Jedes erzeugt eine eigene Ausgabedatei.

---

## Sprachmodelle

Das Modell bestimmt den Kompromiss zwischen **Geschwindigkeit** und **Genauigkeit**. Größere Modelle benötigen mehr Arbeitsspeicher und Rechenzeit, liefern aber bessere Ergebnisse.

| Modell | Ca. Größe | Geschwindigkeit | Genauigkeit | Hinweis |
|---|---|---|---|---|
| Tiny | ~75 MB | ⚡⚡⚡⚡ | ★☆☆☆ | Sehr schnell, geringere Qualität |
| Base | ~142 MB | ⚡⚡⚡ | ★★☆☆ | **Guter Einstieg** |
| Small | ~466 MB | ⚡⚡ | ★★★☆ | Gute Balance |
| Medium | ~1,5 GB | ⚡ | ★★★★ | Hohe Genauigkeit |
| Large-v3-Turbo | ~1,6 GB | ⚡⚡ | ★★★★ | Schnell & sehr genau |
| Large-v3 | ~3,1 GB | langsam | ★★★★★ | Höchste Genauigkeit |

> **Empfehlung:** Starte mit **Base**. Wenn die Genauigkeit nicht ausreicht, probiere **Small** oder **Large-v3-Turbo**.

---

## Whisper verwalten

Klicke im Tab **Transkribieren** auf **Whisper verwalten**, um den Modell-Manager zu öffnen. Dort kannst du:

- Weitere Modelle **herunterladen**
- Modelle **löschen**, um Speicherplatz freizugeben
- **Whisper installieren / aktualisieren** — installiert oder aktualisiert die Whisper-Engine
- **Auf Updates prüfen** — prüft, ob eine neuere Version der Whisper-Engine verfügbar ist
- **Whisper deinstallieren** — entfernt die Whisper-Installation:
  - **Nur Tool entfernen** — löscht `whisper.exe` und alle DLLs, behält aber alle heruntergeladenen Sprachmodelle. Ideal zur Reparatur einer defekten Installation ohne erneutes Herunterladen der Modelle.
  - **Vollständig löschen** — entfernt alles inklusive aller Modelle. Gibt den meisten Speicherplatz frei.

Der Pfad des Modelle-Ordners wird unten im Manager-Fenster angezeigt.

---

## Transkription nach Download

Der Tab **Download** enthält die Option:
> **Nach dem Download automatisch transkribieren**

Wenn aktiviert, wird die heruntergeladene Datei nach dem Download automatisch an Whisper übergeben. **Modell**, **Sprache** und **Format** können direkt in den Download-Optionen gewählt werden. Die Ausgabedateien werden im selben Ordner wie das heruntergeladene Video gespeichert.

> Diese Option setzt voraus, dass Whisper installiert ist und mindestens ein Modell vorhanden ist. Falls Whisper fehlt, erscheint ein Hinweis:  
> *„Whisper nicht installiert – gehe zu 'Transkribieren' im Menü."*

Zusätzlich kann aktiviert werden:
> **Kapitel als Textdatei speichern (falls verfügbar)** — extrahiert Kapitelinformationen aus dem Video und speichert sie als Textdatei neben dem Download.

---

← [Benutzung](benutzung.md) | [Übersicht →](README.md)
