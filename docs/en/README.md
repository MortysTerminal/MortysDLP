# MortysDLP - Overview

Deutsche Version: [docs/de/README.md](../de/README.md)
← Back to main page: [README.md](../../README.md)

**MortysDLP** is a Windows desktop application for downloading and converting videos and audio from the web. It wraps [yt-dlp](https://github.com/yt-dlp/yt-dlp), [ffmpeg](https://ffmpeg.org/) and [whisper.cpp](https://github.com/ggml-org/whisper.cpp) in a clean Fluent-style UI, no command line required.

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
- **x264 mode**: re-encode to H.264 for maximum compatibility with editing software (DaVinci Resolve, Premiere Pro, and others)
- **Time span**: download a specific section (`hh:mm:ss` or `mm:ss`); the Timeline button opens a visual picker
- **From start to**: download only the first N seconds of a video
- Set a custom output filename
- Real-time progress bar with download speed
- **History**: re-use previous downloads, each with its target folder
- **GIF maker**: create a GIF from the downloaded video in the same run
- **Transcription after download**: automatically transcribe the file once it is done

### Batch Download
- Manage multiple URLs in a list and download them one after another
- The global bandwidth limit applies here as well
- Successful downloads are added to the history too

### Convert
- Convert local media files to a different format, multiple files at once
- Selectable target format, video quality, and audio quality

### Transcribe
- Transcribe any video or audio file to text, fully **offline**, no data leaves your PC
- Powered by [whisper.cpp](https://github.com/ggml-org/whisper.cpp) (OpenAI Whisper, running locally)
- Output formats: `.txt`, `.srt`, `.vtt`
- Automatic language detection or manual selection (19+ languages)
- Six model sizes to choose from (Tiny to Large-v3)
- Models are downloaded and managed inside the app

### Twitch VOD & Clip
- Video download via yt-dlp
- Chat download and rendering (MP4 overlay) via TwitchDownloaderCLI
- Both tools are managed separately

### App-wide
- **Global bandwidth limit**: cap download speed in MB/s, applied across all download types
- **Automatic tool management**: yt-dlp, ffmpeg, and ffprobe are downloaded on first launch; yt-dlp is kept up to date automatically
- **Opt-in updates**: a subtle banner with the full changelog appears when a new version is available
- Fluent design, light and dark mode (follows the Windows system setting)
- German and English UI, switchable at runtime, selectable interface font

---

## About the code and AI

I use [Claude](https://www.anthropic.com/claude) to review, tidy up and improve this codebase, and I would rather say so plainly. It is a tool, and a good one. The direction, the decisions and the responsibility are mine. Claude helps me get there with fewer rough edges.

---

Continue with [Installation & Setup](installation.md)
