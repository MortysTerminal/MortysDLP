# Installation & Einrichtung

> 🇬🇧 English version: [../en/installation.md](../en/installation.md)  
> ← [Übersicht](README.md) | [Benutzung →](benutzung.md)

Die Einrichtung von MortysDLP ist unkompliziert. Die App führt dich beim ersten Start durch alles hindurch.

---

## Inhaltsverzeichnis

1. [Voraussetzungen](#1-voraussetzungen)
2. [Neueste Version herunterladen](#2-neueste-version-herunterladen)
3. [Entpacken & starten](#3-entpacken--starten)
4. [.NET 10-Runtime-Prüfung](#4-net-10-runtime-prüfung)
5. [Tool-Prüfung (yt-dlp & ffmpeg)](#5-tool-prüfung-yt-dlp--ffmpeg)
6. [Whisper einrichten (optional)](#6-whisper-einrichten-optional--für-transkription)
7. [Bereit](#7-bereit)

---

## 1. Voraussetzungen

| Voraussetzung | Hinweis |
|---|---|
| Windows 10 oder 11 | 64-Bit |
| [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) | Wird automatisch abgefragt, falls nicht installiert |

> **Was ist .NET 10?**  
> .NET ist Microsofts offizielle, kostenlose Laufzeitumgebung — dieselbe Art von Systemkomponente, auf die unzählige Windows-Anwendungen aufbauen. Sie wird von Microsoft veröffentlicht, signiert und gewartet und kann von [dotnet.microsoft.com](https://dotnet.microsoft.com/) heruntergeladen werden. Sie enthält keinen MortysDLP-spezifischen Code; sie ist schlicht die Grundlage, auf der die App läuft.  
> Die Installation ist ein einmaliger Schritt. Beim Download achte darauf, die **„.NET Desktop Runtime"** auszuwählen — nicht das SDK und nicht die ASP.NET Core Runtime.

---

## 2. Neueste Version herunterladen

Lade die aktuelle ZIP-Datei von der [Releases-Seite](https://github.com/MortysTerminal/MortysDLP/releases) herunter.

---

## 3. Entpacken & starten

Entpacke das Archiv in einen beliebigen Ordner, zum Beispiel:

```
C:\Tools\MortysDLP\
```

Kein Installer, keine Administrator-Rechte erforderlich. Dann doppelklicke auf **`MortysDLP.exe`**.

Beim ersten Start zeigt die App einen kurzen Vorbereitungsscreen (*„MortysDLP wird vorbereitet…"*) und führt einige Prüfungen durch, bevor das Hauptfenster geöffnet wird.

---

## 4. .NET 10-Runtime-Prüfung

Falls .NET 10 bereits auf deinem System installiert ist, geht es direkt weiter zum nächsten Schritt.

Andernfalls wirst du aufgefordert, die Runtime zu installieren. Nach der Installation starte `MortysDLP.exe` erneut.

---

## 5. Tool-Prüfung (yt-dlp & ffmpeg)

MortysDLP prüft, ob **yt-dlp** und **ffmpeg** im App-Ordner vorhanden sind. Bei einer Neuinstallation fehlen sie, und die App fragt:

> *„Das Tool 'yt-dlp' ist erforderlich, um Videos und Audios von verschiedenen Plattformen herunterzuladen. […] Möchtest du yt-dlp jetzt herunterladen?"*

Klicke auf **Ja**. MortysDLP lädt die aktuellen offiziellen Versionen herunter und speichert sie im eigenen Verzeichnis. Es wird nichts systemweit installiert; außerhalb des MortysDLP-Ordners wird nichts verändert.

- [yt-dlp](https://github.com/yt-dlp/yt-dlp) — der eigentliche Downloader, unterstützt hunderte Plattformen
- [ffmpeg](https://ffmpeg.org/) — verarbeitet und konvertiert Audio/Video

Ab diesem Zeitpunkt hält MortysDLP yt-dlp automatisch im Hintergrund aktuell.

> **Was passiert, wenn ich ablehne?**  
> yt-dlp ist für die Funktion der App zwingend erforderlich. Wenn du ablehnst, weist die App beim nächsten Start erneut darauf hin. Du kannst die Tools auch manuell in den App-Ordner legen — siehe die [yt-dlp-Dokumentation](https://github.com/yt-dlp/yt-dlp) für Details.

---

## 6. Whisper einrichten *(optional — für Transkription)*

Wenn du die eingebaute **Transkriptions**-Funktion nutzen möchtest, ist beim ersten Öffnen des Tabs eine kurze zweistufige Einrichtung nötig. Alle anderen Funktionen (Download, Konvertieren) funktionieren ohne sie.

### Schritt 1: Whisper installieren

Im Tab **Transkribieren** siehst du:

> *„Schritt 1: Whisper installieren"*

Klicke auf **Whisper installieren**. Die App lädt [whisper.cpp](https://github.com/ggerganov/whisper.cpp) herunter und installiert es im MortysDLP-Ordner. Kein Python, keine virtuellen Umgebungen, keine Kommandozeile nötig.

Nach erfolgreicher Installation ändert sich der Status zu:

> *„✔ Schritt 1: Whisper installiert"*

> **Was ist Whisper?**  
> Whisper ist ein quelloffenes Spracherkennungsmodell, das ursprünglich von OpenAI veröffentlicht wurde. MortysDLP führt es **vollständig lokal auf deinem PC** aus — kein Audio wird an einen Cloud-Dienst gesendet, kein API-Schlüssel wird benötigt, es entstehen keine Nutzungskosten.

### Schritt 2: Sprachmodell herunterladen

Klicke auf **Whisper verwalten**, um den Modell-Manager zu öffnen. Klicke dann neben dem gewünschten Modell auf **Herunterladen**. Sobald der Status **✔ Installiert** zeigt, ist alles bereit.

> *„✔ Schritt 2: Modell installiert"*

Verfügbare Modelle:

| Modell | Größe | Hinweis |
|---|---|---|
| Tiny | ~75 MB | Schnellstes, geringste Genauigkeit |
| Base | ~142 MB | Guter Einstiegspunkt ✔ |
| Small | ~466 MB | Bessere Genauigkeit |
| Medium | ~1,5 GB | Hohe Genauigkeit |
| Large-v3-Turbo | ~1,6 GB | Schnell & sehr genau |
| Large-v3 | ~3,1 GB | Höchste Genauigkeit |

Du kannst Modelle jederzeit hinzufügen oder löschen.

---

## 7. Bereit

Alle Funktionen stehen jetzt zur Verfügung:

- **Tab „Download"** — URL einfügen und Download starten
- **Tab „Konvertieren"** — lokale Mediendateien konvertieren
- **Tab „Transkribieren"** — Video oder Audio in Text umwandeln
- **Tab „Einstellungen"** — Pfade und Einstellungen anpassen

Falls beim Setup etwas schiefläuft, öffne bitte ein [Issue](https://github.com/MortysTerminal/MortysDLP/issues) mit möglichst vielen Details.

---

➡️ Weiter mit der [Benutzung](benutzung.md)
