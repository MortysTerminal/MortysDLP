# MortysDLP

**MortysDLP** is a modern WPF desktop application for downloading videos and audio from the internet on Windows. It uses [yt-dlp](https://github.com/yt-dlp/yt-dlp) and [ffmpeg](https://ffmpeg.org/) under the hood and wraps them in a clean, Fluent-style interface — no command line required.

<img width="702" alt="mortysdlp-overlay-example" src="https://raw.githubusercontent.com/MortysTerminal/MortysDLP/master/Pictures/mortysdlp-overlay-example.png" />

---

## 📚 Documentation

> 🇩🇪 **Deutsche Dokumentation:** [docs/de/](docs/de/README.md)  
> 🇬🇧 **English Documentation:** [docs/en/](docs/en/README.md)

| Topic | English | Deutsch |
|---|---|---|
| Overview & Features | [docs/en/README.md](docs/en/README.md) | [docs/de/README.md](docs/de/README.md) |
| Installation & Setup | [docs/en/installation.md](docs/en/installation.md) | [docs/de/installation.md](docs/de/installation.md) |
| Usage Guide | [docs/en/usage.md](docs/en/usage.md) | [docs/de/benutzung.md](docs/de/benutzung.md) |
| Transcription | [docs/en/transcription.md](docs/en/transcription.md) | [docs/de/transkription.md](docs/de/transkription.md) |

---

## Quick Start

1. Download the latest release from the [Releases page](https://github.com/MortysTerminal/MortysDLP/releases)
2. Extract the ZIP to any folder — no installer, no admin rights required
3. Run `MortysDLP.exe`
4. The app downloads all required tools automatically on first launch
5. That's it — paste a URL and start downloading

➡️ Full setup guide: [docs/en/installation.md](docs/en/installation.md)

---

## Features at a Glance

- **Download** — videos and audio from YouTube, Twitch, and hundreds of other platforms via yt-dlp
- **Convert** — convert local media files to a different format (batch-capable)
- **Transcribe** — speech-to-text, fully offline via whisper.cpp — no cloud, no API key
- **Automatic tool management** — yt-dlp, ffmpeg, and Whisper are managed inside the app
- **Light / Dark mode**, German & English UI, opt-in updates

---

## Requirements

- Windows 10 or 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) *(prompted automatically if missing)*

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

**Note:** MortysDLP is a private open-source project. Use at your own risk.  
Please respect the terms of service of the platforms you download from.

---

- [Issues & Feature Requests](https://github.com/MortysTerminal/MortysDLP/issues)
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) · [ffmpeg](https://ffmpeg.org/) · [whisper.cpp](https://github.com/ggerganov/whisper.cpp)