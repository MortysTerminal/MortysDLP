# MortysDLP

**MortysDLP** is a Windows desktop application for downloading and converting videos and audio from the web. It wraps [yt-dlp](https://github.com/yt-dlp/yt-dlp), [ffmpeg](https://ffmpeg.org/) and [whisper.cpp](https://github.com/ggml-org/whisper.cpp) in a clean Fluent-style UI, no command line required.

<img width="702" alt="mortysdlp-overlay-example" src="https://raw.githubusercontent.com/MortysTerminal/MortysDLP/master/Pictures/mortysdlp-overlay-example.png" />

---

## Why this exists

I make content, and I kept hitting the same wall. A clip I needed sat behind "sign up to download in HD". A quick format conversion wanted a monthly plan. Half the "free" download sites turn into a subscription trap the moment you click.

MortysDLP is my way around that. It does the download and the conversion on your own machine, with the tools that already do this job well, wrapped in something you do not need a terminal for. I built it to help in exactly those moments where content work grinds to a halt.

## What it does not do

- **No cloud.** Everything runs on your PC. Nothing is uploaded, nothing is processed on someone else's server.
- **No telemetry.** The app does not phone home, does not count your downloads, does not collect usage data.
- **No ads, no upsells, no paywalls.** There is no "Pro" version. There is nothing to unlock.
- **Nothing that gets in the way.** No account, no login, no pop-ups nagging you to upgrade.

Honest software. It helps, then it gets out of the way.

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
2. Extract the ZIP. No installer, no admin rights required.
3. Run `MortysDLP.exe`
4. On first launch, the required tools are downloaded automatically
5. Paste a URL and start downloading

Full setup guide: [docs/en/installation.md](docs/en/installation.md)

---

## Features

- **Download**: videos and audio from YouTube, Twitch, and hundreds of other platforms via yt-dlp
- **Batch download**: queue multiple URLs, download them in one run
- **Convert**: turn local media files into a different format (batch-capable)
- **Transcribe**: speech to text, fully offline via whisper.cpp, no cloud and no API key
- **Twitch VOD & chat**: download the video via yt-dlp, optionally render a chat overlay via TwitchDownloaderCLI
- **Automatic tool management**: yt-dlp, ffmpeg and the Whisper models are managed inside the app
- **Bandwidth limiting**: set a global download speed cap, applied live across all download types
- Light and dark mode, German and English UI, selectable interface font

---

## Requirements

- Windows 10 or 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (you are prompted automatically if it is missing)

---

## About the code and AI

I use [Claude](https://www.anthropic.com/claude) to review, tidy up and improve this codebase, and I would rather say so plainly. It is a tool, and a good one. The direction, the decisions and the responsibility are mine. Claude helps me get there with fewer rough edges.

---

## Open source & credits

MortysDLP builds on these open-source tools:

| Tool | Purpose | License |
|---|---|---|
| [yt-dlp](https://github.com/yt-dlp/yt-dlp) | Video and audio downloads | Unlicense |
| [ffmpeg](https://ffmpeg.org/) | Media processing, conversion | LGPL / GPL |
| [TwitchDownloaderCLI](https://github.com/lay295/TwitchDownloader) | Twitch chat download and rendering | MIT |
| [whisper.cpp](https://github.com/ggml-org/whisper.cpp) | Offline speech to text | MIT |
| [Inter / Inter Tight](https://github.com/rsms/inter) | Bundled UI font | SIL OFL 1.1 |

---

## License

MIT License, see [LICENSE](LICENSE) for details.

MortysDLP is a private open-source project. Please respect the terms of service of the platforms you download from.

---

[Issues & Feature Requests](https://github.com/MortysTerminal/MortysDLP/issues)
