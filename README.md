# MortysDLP

**MortysDLP** is a Windows desktop application for downloading videos and audio from the web. It wraps [yt-dlp](https://github.com/yt-dlp/yt-dlp) and [ffmpeg](https://ffmpeg.org/) in a clean Fluent-style UI — no command line required.

<img width="702" alt="mortysdlp-overlay-example" src="https://raw.githubusercontent.com/MortysTerminal/MortysDLP/master/Pictures/mortysdlp-overlay-example.png" />

---

## Documentation

German documentation: [docs/de/](docs/de/README.md)  
English documentation: [docs/en/](docs/en/README.md)

| Topic | English | Deutsch |
|---|---|---|
| Overview & Features | [docs/en/README.md](docs/en/README.md) | [docs/de/README.md](docs/de/README.md) |
| Installation & Setup | [docs/en/installation.md](docs/en/installation.md) | [docs/de/installation.md](docs/de/installation.md) |
| Usage Guide | [docs/en/usage.md](docs/en/usage.md) | [docs/de/benutzung.md](docs/de/benutzung.md) |
| Transcription | [docs/en/transcription.md](docs/en/transcription.md) | [docs/de/transkription.md](docs/de/transkription.md) |

---

## Quick Start

1. Download the latest release from the [Releases page](https://github.com/MortysTerminal/MortysDLP/releases)
2. Extract the ZIP — no installer, no admin rights required
3. Run `MortysDLP.exe`
4. On first launch, required tools are downloaded automatically
5. Paste a URL and start downloading

Full setup guide: [docs/en/installation.md](docs/en/installation.md)

---

## Features

- **Download** — videos and audio from YouTube, Twitch, and hundreds of other platforms via yt-dlp
- **Batch download** — queue multiple URLs, download in one run
- **Convert** — convert local media files to a different format (batch-capable)
- **Transcribe** — speech-to-text, fully offline via whisper.cpp — no cloud, no API key required
- **Twitch VOD & Chat** — download video via yt-dlp, optionally render chat overlay via TwitchDownloaderCLI
- **Automatic tool management** — yt-dlp, ffmpeg, and Whisper models are managed inside the app
- **Bandwidth limiting** — set a global download speed cap, applied live across all download types
- Light / Dark mode, German & English UI

---

## Requirements

- Windows 10 or 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) *(prompted automatically if missing)*

---

## Open Source & Credits

MortysDLP relies on the following open-source tools:

| Tool | Purpose | License |
|---|---|---|
| [yt-dlp](https://github.com/yt-dlp/yt-dlp) | Video & audio downloads | Unlicense |
| [ffmpeg](https://ffmpeg.org/) | Media processing, conversion | LGPL / GPL |
| [TwitchDownloaderCLI](https://github.com/lay295/TwitchDownloader) | Twitch chat download & rendering | MIT |
| [whisper.net / whisper.cpp](https://github.com/sandrohanea/whisper.net) | Offline speech-to-text | MIT |
| [Wpf.Ui](https://github.com/lepoco/wpfui) | Fluent-style WPF UI library | MIT |

---

## License

MIT License — see [LICENSE](LICENSE) for details.

MortysDLP is a private open-source project. Please respect the terms of service of the platforms you download from.

---

[Issues & Feature Requests](https://github.com/MortysTerminal/MortysDLP/issues)
