# MortysDLP – Overview

Deutsche Version: [docs/de/README.md](../de/README.md)  
← Back to main page: [README.md](../../README.md)

**MortysDLP** is a Windows desktop application for downloading videos and audio from the web. It wraps [yt-dlp](https://github.com/yt-dlp/yt-dlp) and [ffmpeg](https://ffmpeg.org/) in a clean Fluent-style UI — no command line required.

---

## Table of Contents

- [Features](#features)
- [Installation & Setup](installation.md)
- [Usage Guide](usage.md)
- [Transcription](transcription.md)

---

## Features

### Download
- Download videos and audio from any URL supported by yt-dlp (YouTube, Twitch, and hundreds more)
- **Audio only** mode with selectable format: `aac`, `alac`, `flac`, `m4a`, `mp3`, `opus`, `vorbis`, `wav`
- Selectable audio **bitrate**: Highest, 320k, 256k, 192k, 160k, 128k, 96k, 64k
- **Video quality**: Best, 1440p, 1080p, 720p, 480p, 360p, 240p, 144p
- **Video format** (container): `mp4`, `mkv`, `mov`, `avi`
- **x264 mode** — re-encode to H.264 for maximum compatibility with editing software (DaVinci Resolve, Premiere Pro, etc.)
- **Time span** — download a specific section (`hh:mm:ss` or `mm:ss`); the Timeline button opens a visual picker
- **From start to** — download only the first N seconds of a video
- Set a custom output filename
- Real-time progress bar with download speed
- **History** — re-use previous download URLs
- **Transcription after download** — automatically transcribe the file after downloading

### Batch Download
- Queue multiple URLs and download them in one run
- Global bandwidth limit applies here as well

### Convert
- Convert local media files to a different format, multiple files at once
- Selectable target format, video quality, and audio quality

### Transcribe
- Transcribe any video or audio file to text — fully **offline**, no data leaves your PC
- Powered by [whisper.cpp](https://github.com/ggerganov/whisper.cpp) (OpenAI Whisper, running locally)
- Output formats: `.txt`, `.srt`, `.vtt`
- Automatic language detection or manual selection (19+ languages)
- Six model sizes to choose from (Tiny to Large-v3)
- Models are downloaded and managed inside the app

### Twitch VOD & Clip
- Video download via yt-dlp
- Chat download and rendering (MP4 overlay) via TwitchDownloaderCLI
- Both tools are managed separately

### App-wide
- **Global bandwidth limit** — cap download speed in MB/s, applied across all download types
- **Automatic tool management** — yt-dlp, ffmpeg, and ffprobe are downloaded on first launch; yt-dlp is kept up to date automatically
- **Opt-in updates** — a subtle banner with the full changelog appears when a new version is available
- Fluent design, Light / Dark mode (follows Windows system setting)
- German and English UI, switchable at runtime

---

Continue with [Installation & Setup](installation.md)
