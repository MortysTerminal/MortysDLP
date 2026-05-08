# MortysDLP – Overview

> 🇩🇪 Deutsche Version: [docs/de/README.md](../de/README.md)  
> ← Back to main page: [README.md](../../README.md)

**MortysDLP** is a modern WPF desktop application for downloading videos and audio from the internet on Windows. It uses [yt-dlp](https://github.com/yt-dlp/yt-dlp) and [ffmpeg](https://ffmpeg.org/) under the hood and wraps them in a clean, Fluent-style interface — no command line required.

---

## 📖 Table of Contents

- [Features](#features)
- [Installation & Setup](installation.md)
- [Usage Guide](usage.md)
- [Transcription](transcription.md)

---

## Features

### Download tab
- Download videos and audio from any URL supported by yt-dlp (YouTube, Twitch, and hundreds more)
- **Audio ONLY** mode with selectable format: `aac`, `alac`, `flac`, `m4a`, `mp3`, `opus`, `vorbis`, `wav`
- Selectable audio **Bitrate**: Highest, 320k, 256k, 192k, 160k, 128k, 96k, 64k
- **Video Quality** selection: Best, 1440p, 1080p, 720p, 480p, 360p, 240p, 144p
- **Video Format** (container) selection: `mp4`, `mkv`, `mov`, `avi`
- **Video format for editing software (x264)** — re-encode for maximum compatibility with DaVinci Resolve, Premiere Pro, etc.
- **Time span** — download a specific section of a video (format: `hh:mm:ss` or `mm:ss`); the **Timeline** button opens a visual picker
- **From start to:** — download only the first N seconds of a video
- **Use custom video title** — override the output file name
- Separate download paths for video and audio-only
- Real-time progress bar and status messages
- **History** — view and re-use previous download URLs
- **Transcription after download** — automatically transcribe the downloaded file with Whisper

### Convert tab
- Convert local media files to a different format
- Process multiple files at once (**Add Files**)
- Selectable target format, output folder, **Video Quality**, and **Audio Quality**
- Per-file progress tracking
- **Open Folder** button to access converted files immediately

### Transcribe tab
- Transcribe any video or audio file to text — fully **offline**, no data leaves your PC
- Powered by [whisper.cpp](https://github.com/ggerganov/whisper.cpp) (OpenAI Whisper, running locally)
- Output formats: text file (`.txt`), subtitles (`.srt`), WebVTT (`.vtt`)
- SRT files work directly in DaVinci Resolve, Premiere Pro, and most video editors
- Automatic language detection or manual language selection (19+ supported languages)
- **Six model sizes** — choose between speed and accuracy: Tiny, Base, Small, Medium, Large-v3-Turbo, Large-v3
- Whisper engine and language models are downloaded and managed entirely inside the app

### Settings tab
- **Change Download Path** — set default paths for video and audio-only downloads
- **Select Language** — Auto (system language), Deutsch, English
- **Enable Debug Mode (shows download details)** — shows raw yt-dlp/ffmpeg output
- **Open GitHub** — opens the project page
- **Close Application**

### App-wide
- **Automatic tool management** — yt-dlp, ffmpeg, and ffprobe are downloaded automatically on first launch; yt-dlp is kept up to date
- **Opt-in software updates** — a subtle banner (*"New version X.Y available"*) appears at the top of the window; the full changelog is shown before you decide
- **Fluent design** with full Light / Dark mode support (follows Windows system setting)
- German and English UI, switchable at runtime without restart

---

➡️ Continue with [Installation & Setup](installation.md)
