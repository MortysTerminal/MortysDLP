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
- Der Download von yt-dlp wird jetzt gegen die Prüfsummenliste des jeweiligen Releases
  geprüft, soweit sie vorliegt; fehlt sie, steht das ausdrücklich im Protokoll statt
  unbemerkt zu bleiben. Beim ffmpeg-Paket werden vor dem Entpacken Anzahl der Einträge,
  entpackte Größe und Kompressionsverhältnis gegen plausible Grenzen geprüft, und es werden
  ausschließlich die beiden benötigten Programmdateien herausgeholt — Einträge, die aus dem
  Zielordner ausbrechen wollen, können deshalb nichts ausrichten.
- Heruntergeladene Update-Pakete werden jetzt vor der Installation gegen eine Prüfsumme und
  die erwartete Größe geprüft — die Prüfung läuft mit, während die Datei geschrieben wird,
  nicht erst hinterher. Stimmt etwas nicht, bricht das Update ab, die vorhandene Installation
  bleibt unangetastet. Ist für ein Release ausnahmsweise keine Prüfsumme hinterlegt, bleibt
  das Update möglich (nur die Größe wird geprüft) — mit einer deutlichen Zeile im Protokoll.
- Härtung der Argumentübergabe an externe Werkzeuge (yt-dlp, ffmpeg, ffprobe,
  TwitchDownloaderCLI, whisper.cpp). Kopierte URLs oder Videotitel mit ungewöhnlichen
  Sonderzeichen können externen Werkzeugen nicht mehr zusätzliche, nicht beabsichtigte
  Befehlszeilenargumente unterschieben.

### Hinzugefügt
- MortysDLP prüft jetzt vor jedem Update-Angebot, ob der Installationsordner ein Update
  überhaupt zulässt. Liegt er in einem geschützten Systemordner, erscheint ein Hinweis mit der
  Empfehlung, die Installation zu verschieben (inklusive des Hinweises, dass dabei alle
  Einstellungen verloren gehen) — das Update lässt sich danach trotzdem versuchen, falls die
  Berechtigungen im Einzelfall doch ausreichen. Ist der Ordner schreibgeschützt oder läuft
  MortysDLP direkt aus einer ZIP-Vorschau, wird kein Download angeboten; der Banner sagt in
  diesem Fall, dass es eine neue Version gibt und warum sie sich hier nicht einspielen lässt.
- Nach einem erfolgreichen Update erscheint jetzt sofort nach dem Neustart eine Bestätigung
  mit der Möglichkeit, die Änderungen der neuen Version anzusehen — einmalig, danach nicht
  erneut.
- MortysDLP fragt jetzt nach, bevor ein Update gestartet wird, während im Hintergrund noch
  ein Download, eine Konvertierung oder eine Transkription läuft — bisher wurde ein solcher
  Vorgang beim Update kommentarlos abgebrochen.
- MortysDLP prüft nach einem Update beim nächsten Start, ob es tatsächlich gewirkt hat, und
  meldet das Ergebnis: eine kurze Bestätigung bei Erfolg, oder ein verständlicher Hinweis mit
  Protokollpfad, wenn die neue Version nicht läuft. Ein Update, das zweimal hintereinander
  ohne Wirkung blieb, wird danach nicht mehr automatisch angeboten — ein Knopf „Trotzdem
  erneut versuchen" bleibt verfügbar, falls doch gewünscht.
- Der Update-Hinweis lässt sich jetzt im Dialog dauerhaft für eine bestimmte Version
  überspringen („Diese Version überspringen") — der Banner erscheint erst wieder, wenn eine
  **neuere** Version veröffentlicht wird. „Später" und das „X" am Banner blenden den Hinweis
  weiterhin nur für die laufende Sitzung aus.
- Anwendungs-Updates zeigen jetzt einen Fortschrittsbalken mit Prozentangabe und lassen sich
  jederzeit abbrechen — bisher wirkte die Anwendung beim Herunterladen eines größeren Pakets
  über eine langsame Leitung wie eingefroren. Ein Abbruch räumt sauber auf: keine
  Reste im Temp-Ordner, die vorhandene Installation bleibt unangetastet.
- MortysDLP erkennt beim Start, wenn es direkt aus der ZIP-Vorschau des Explorers gestartet
  wurde (also aus einem temporären Ordner, der beim Schließen verschwindet) und zeigt dann
  einen Hinweis mit der Möglichkeit, den Ordner zu öffnen oder trotzdem fortzufahren. So bleibt
  nicht mehr unklar, warum heruntergeladene Werkzeuge nach jedem Neustart erneut fehlen.
- Unerwartete Fehler beenden MortysDLP nicht mehr wortlos. Sie werden protokolliert
  (`%LOCALAPPDATA%\MortysDLP\logs\`, 14 Tage bzw. 10 MB je Datei) und in einem Dialog mit
  verständlichem Kurztext, ausklappbaren technischen Details, „Details kopieren" und
  „Protokollordner öffnen" angezeigt. Bei Fehlern, die nur eine einzelne Aktion betreffen,
  läuft die Anwendung danach normal weiter.

### Geändert
- Werkzeug-Updates (yt-dlp, ffmpeg, ffprobe) sichern die vorhandene Fassung jetzt, bevor sie
  ersetzt wird, und rufen das Werkzeug danach einmal auf. Antwortet es nicht mehr brauchbar,
  wird die vorherige Fassung automatisch wiederhergestellt und der Vorgang als fehlgeschlagen
  gemeldet — das Werkzeug ist danach unverändert einsatzbereit. Bisher war ein Update, das die
  Datei ersetzte, aber ein unbrauchbares Werkzeug hinterließ, von einem erfolgreichen nicht zu
  unterscheiden. Jeder Schritt steht einzeln im Protokoll: gesichert, eingesetzt, geprüft,
  Sicherung entfernt — nicht nur die Fehlschläge.
- ffmpeg und ffprobe werden jetzt gemeinsam behandelt: Sie kommen aus demselben Paket und
  werden gemeinsam ersetzt oder gemeinsam zurückgeholt. Ein neues ffmpeg neben einem alten
  ffprobe kann dadurch nicht mehr entstehen. Ein ffmpeg-Update wird ausschließlich angeboten,
  nie ohne Zutun eingespielt.
- Die Versionsprüfung für yt-dlp nutzt jetzt mehrere voneinander unabhängige Quellen (unter
  anderem den Python-Paketindex, der ohne GitHub auskommt) und wird höchstens alle zwölf
  Stunden über das Netz erneuert. Ist GitHub nicht erreichbar oder das stündliche
  Anfragekontingent erschöpft, liefert eine der Ausweichquellen die Versionsnummer, statt dass
  die Prüfung ergebnislos bleibt.
- Aus dem ffmpeg-Paket werden jetzt nur noch `ffmpeg.exe` und `ffprobe.exe` herausgeholt,
  statt das gesamte Archiv in ein temporäres Verzeichnis zu entpacken. Das temporäre Paket
  wird nach dem Vorgang in jedem Fall gelöscht, und das Entpacken blockiert die Oberfläche
  nicht mehr.
- Die Suche nach einer neuen MortysDLP-Version hält den Start nicht mehr auf: Sie läuft jetzt
  im Hintergrund, nachdem das Hauptfenster bereits offen ist, und meldet ein gefundenes Update
  nachträglich über den gewohnten Banner. War diese Prüfung bisher langsam oder nicht erreichbar,
  ließ das den Start spürbar länger warten oder sogar hängen — beides kann jetzt nicht mehr
  passieren. Die Prüfung der externen Werkzeuge (yt-dlp, ffmpeg, ffprobe) bleibt vorerst vor dem
  Fenster und ist auf den meisten Rechnern weiterhin der größte Anteil der Startzeit.
- Externe Werkzeuge (yt-dlp, ffmpeg, ffprobe, whisper.cpp, TwitchDownloaderCLI, Whisper-Modelle)
  liegen jetzt im Nutzerprofil statt im Programmordner. Wer MortysDLP nach
  `C:\Program Files` entpackt, kann seine Werkzeuge dadurch trotzdem aktualisieren — bisher
  scheiterte das, weil der Programmordner dort schreibgeschützt ist. Vorhandene Werkzeuge
  einer älteren Installation werden beim ersten Start automatisch in den neuen Ordner
  übernommen; das Protokoll hält jede übernommene Datei fest. Wer MortysDLP von einem
  USB-Stick betreibt, muss die Werkzeuge dadurch pro Rechner einmalig neu laden — sie liegen
  nicht mehr auf dem Stick.
- Der Installations-Updater, der ein Update im Hintergrund einspielt, ist jetzt vollständig
  quelloffen und wird bei jedem Release neu gebaut — bisher wurde eine ältere, nicht mehr
  nachvollziehbare Programmdatei ausgeliefert.
- Die Suche nach einer neuen MortysDLP-Version läuft nicht mehr bei jedem Start online: Das
  Ergebnis wird zwischengespeichert und höchstens alle 6 Stunden erneuert. Ist gar keine
  Internetverbindung da, verwendet MortysDLP einfach den zuletzt bekannten Stand, statt zu
  hängen oder „kein Update" zu melden.
- Netzabfragen (Update-Prüfung, Werkzeug-Versionsprüfung, Downloads) melden Störungen jetzt
  verständlich statt lautlos zu scheitern: Vorübergehende Fehler werden automatisch mit
  steigender Wartezeit wiederholt, dauerhafte Fehler (z. B. „nicht gefunden") sofort gemeldet
  statt erst nach mehreren nutzlosen Versuchen. Ist das stündliche Anfragekontingent von
  GitHub erschöpft, erkennt MortysDLP das jetzt als solches, statt fälschlich „kein Update
  verfügbar" zu melden.

### Behoben
- Für yt-dlp wird kein Update mehr angeboten, wenn die installierte Fassung **neuer** ist als
  die veröffentlichte — etwa nach einem Zwischenbuild. Bisher genügte es, dass sich die beiden
  Versionsangaben unterschieden, und das Angebot war dann ein Rückschritt. Ebenso erscheint
  kein Angebot mehr, wenn yt-dlp auf die Versionsfrage überhaupt nicht geantwortet hat; das
  Nicht-Antworten steht stattdessen im Protokoll.
- Für ffmpeg kann kein dauerhaftes Update-Angebot mehr entstehen. Die installierte Fassung
  nennt ihre Version anders geschrieben als die Bezugsquelle sie meldet (mit angehängter
  Build-Bezeichnung gegenüber der reinen Nummer). Beides wird jetzt als dieselbe Ausgabe
  erkannt, sodass ein Angebot nur noch bei einer tatsächlich anderen Ausgabe erscheint.
- Schlägt ein Update fehl, nennt die Meldung jetzt das Protokoll des Installations-Updaters —
  dort steht der Grund. Bisher verwies sie auf das Protokoll der Anwendung, das genau an der
  Stelle endet, an der das Update beginnt.
- Startet MortysDLP nach einem Update neu, ohne dass sich die Version tatsächlich geändert
  hat, erscheint keine Erfolgsmeldung mehr, sondern das Update gilt korrekt als
  fehlgeschlagen.
- Enthält ein Release mehr als eine Datei (z. B. zusätzlich eine Prüfsummenliste), lädt
  MortysDLP jetzt zuverlässig das richtige Update-Paket — unabhängig davon, in welcher
  Reihenfolge GitHub die Anhänge nennt. Sind mehrere Pakete gleichermaßen passend und keines
  eindeutig das richtige, bricht MortysDLP mit einer verständlichen Meldung ab, statt zu raten.
- Nach der Installation eines Updates zeigt MortysDLP jetzt die neue Versionsnummer an, und
  der Update-Hinweis erscheint nicht mehr wiederholt, obwohl das Update längst installiert
  ist. Gespeicherte Einstellungen (Download-Pfad, Sprache, Bandbreitenlimit usw.) bleiben
  dabei erhalten. Auch die Dateieigenschaften der EXE zeigen jetzt die echte Version statt
  „1.0.0.0".
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
- Die Prüfung auf neue Versionen ist jetzt so gebaut, dass sie sich für beliebige externe
  Werkzeuge wiederverwenden lässt, nicht nur für MortysDLP selbst — Grundlage für künftige
  automatische Werkzeug-Updates. Für Nutzer ändert sich dadurch noch nichts.
- Aufräumende Vorgänge in der Update-Kette protokollieren jetzt auch ihren Erfolg, nicht nur
  einen Fehlschlag: geleerter Update-Zwischenspeicher, gelöschter Update-Zustand, gelöschte
  alte Protokolldateien. Der Installations-Updater bekommt außerdem erstmals eine eigene
  Rotation für seine Protokolldateien (Alter über 180 Tage oder mehr als 50 Dateien).
- Der neue Installations-Updater lässt eigene Werkzeuge (`Tools\`) und den Download-Verlauf
  jetzt grundsätzlich unangetastet, selbst wenn ein Release-Paket zufällig Dateien an
  denselben Pfaden enthält — die Programm-Konfigurationsdatei (`MortysDLP.dll.config`) wird
  davon ausdrücklich ausgenommen und weiterhin bei jedem Update ersetzt.
- Der neue Installations-Updater sichert jetzt jede zu ersetzende Datei, bevor er sie
  austauscht, und ersetzt sie atomar. Scheitert das Update mittendrin (z. B. weil eine Datei
  gesperrt ist), werden alle bereits ersetzten Dateien automatisch aus der Sicherung
  zurückgespielt — die vorhandene Installation bleibt in jedem Fall lauffähig. Sicherungen
  älterer, erfolgreicher Updates werden nach 7 Tagen automatisch entfernt.
- Der neue Installations-Updater prüft das heruntergeladene Update-Paket jetzt vollständig,
  bevor auch nur eine Datei entpackt wird: kein Eintrag kann das Zielverzeichnis verlassen
  (Schutz vor präparierten Archiven), keine absoluten Pfade, und eine Obergrenze für
  Eintragsanzahl, Gesamtgröße und Kompressionsverhältnis schützt vor einer „Zip-Bombe".
- Der neue Installations-Updater wartet jetzt geordnet darauf, dass sich MortysDLP selbst
  beendet, bevor Dateien getauscht werden — und bricht kontrolliert ab, statt die Anwendung
  jemals zwangsweise zu beenden, falls sie nicht rechtzeitig reagiert. Vorher wird außerdem
  geprüft, ob der Installationsordner beschreibbar ist und genug freier Speicherplatz zur
  Verfügung steht.
- Der Installations-Updater hat einen eigenen, vollständigen Quellcode (bislang ein Binary
  ohne Quellcode) und läuft auf derselben .NET-Version wie die Hauptanwendung: benannte
  Kommandozeilenargumente, eigenes Protokoll, klare Rückgabewerte.
- Ein beschädigtes oder unvollständiges Update-Paket lässt den Installations-Updater nicht
  mehr abstürzen, sondern führt zu einer regulären Fehlermeldung im Protokoll — die
  vorhandene Installation bleibt unangetastet.
- Der Installations-Updater lässt Werkzeuge und Verlauf auch dann in Ruhe, wenn ein
  Paket denselben Pfad in einer abweichenden Schreibweise enthält.
- Ein Update prüft jetzt vor dem Start, ob auf **beiden** beteiligten Datenträgern genug Platz
  ist — dem der Installation und dem der Sicherungskopie. Liegen sie auf verschiedenen
  Laufwerken, wurde bisher nur eines davon geprüft.
- Alle Netzabfragen laufen jetzt über eine gemeinsame Verbindungsverwaltung statt über fünf
  getrennte, jeweils eigene Verbindungen aufbauende Instanzen. Mit umfangreicher
  Testabdeckung für die Wiederholstrategie und die GitHub-Kontingent-Auswertung.
- Ein neuer, toleranterer Versionsvergleich ist als Grundbaustein vorbereitet (noch nicht im
  Update-Ablauf eingesetzt). Er erkennt künftig auch Hotfix-Tags am selben Tag
  (`2026.06.01.1`) und Vorab-Versionen (`2026.09.01-dev.1`) korrekt als neuer bzw. älter, statt
  Update-Hinweise für solche Tags stillschweigend zu unterdrücken. Mit umfangreicher
  Testabdeckung, auch unter fremdsprachigen Systemeinstellungen.
- Liegt MortysDLP auf einem Netzlaufwerk, das über einen UNC-Pfad (`\\server\share\…`)
  angesprochen wird, weist die Startprotokollzeile zum Installationsort dies jetzt als
  `Network` aus. Bisher fehlte diese Angabe ausgerechnet beim Installationsort, der am
  häufigsten Probleme macht.
- Die Update-Prüfung nutzt jetzt fünf voneinander unabhängige Wege, mit denen sich die neueste
  verfügbare Version ermitteln lässt (u. a. über die GitHub-Veröffentlichungsseite, einen
  Nachrichten-Feed und eine kleine, von Hand gepflegte Datei im Projekt-Repository), zu einer
  Ausweichkette verbunden: Fällt ein Weg aus oder ist erschöpft, übernimmt automatisch der
  nächste, und eine Quelle, die erkennbar veraltete Angaben liefert (z. B. über einen
  Zwischenspeicher-Dienst oder eine vergessene Pflege), kann ein tatsächlich vorhandenes
  Update nicht verdecken. Dazu eine Zielprüfung für Netzwerkanfragen, die nur bekannte,
  verschlüsselte Adressen zulässt. Mit umfangreicher Testabdeckung, ohne echten Netzzugriff.
- Toten, nie verwendeten Code eines älteren Zwischenspeicher-Ansatzes für die Startprüfungen
  entfernt; eine dabei zurückbleibende, bedeutungslose Datei wird beim nächsten Start
  automatisch aufgeräumt.
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
