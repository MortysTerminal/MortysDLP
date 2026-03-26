# MortysDLP

**MortysDLP** is a modern WPF desktop application for downloading videos and audio from the internet on Windows. It uses [yt-dlp](https://github.com/yt-dlp/yt-dlp) and [ffmpeg](https://ffmpeg.org/) under the hood and wraps them in a clean, Fluent-style interface — no command line required.

<img width="702" height="576" alt="Screenshot 2025-07-31 075442" src="https://github.com/user-attachments/assets/8522eba1-6fe8-4f86-af61-420d6c3df3eb" />

---

## Features

### Download
- Download videos and audio from any URL supported by yt-dlp (YouTube, Twitch, and many more)
- **Audio-only mode** with selectable format: `aac`, `alac`, `flac`, `m4a`, `mp3`, `opus`, `vorbis`, `wav`
- Selectable audio bitrate: Highest, 320k, 256k, 192k, 160k, 128k, 96k, 64k
- **Video quality** selection: Best, 1440p, 1080p, 720p, 480p, 360p, 240p, 144p
- **Video container** selection: `mp4`, `mkv`, `mov`, `avi`
- **Editing-friendly video format** (x264 / re-encode for editing software)
- **Time span** – download a specific section of a video (`hh:mm:ss` or `mm:ss`)
- **First N seconds** – download only the beginning of a video
- **Custom filename** – override the output file name
- Separate download paths for video and audio-only
- Progress bar and detailed status messages
- **Download history** – view and re-use previous downloads

### Convert
- Convert local media files to a different format
- Process multiple files at once
- Selectable target format, output folder, video and audio quality
- Per-file progress tracking

### App
- **Automatic tool management** – yt-dlp, ffmpeg, and ffprobe are downloaded automatically on first launch if missing; yt-dlp is kept up to date
- **Opt-in software updates** – a subtle banner appears at the top of the window when a new version is available; the changelog is shown before you decide to update
- **Multilingual UI** – German and English, with automatic detection of the system language; switchable at runtime without restart
- **Fluent design** with full Light / Dark mode support (follows Windows system setting)
- **Debug mode** – shows raw yt-dlp/ffmpeg output for troubleshooting

---

## Installation

1. **Requirements**
   - Windows 10 or 11
   - **.NET 10 Desktop Runtime** — required to run MortysDLP
     - If not installed, Windows will prompt you automatically when you launch the app
     - Or install it manually beforehand: [Download .NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
     - Make sure to select **".NET Desktop Runtime"** (not SDK, not ASP.NET Core Runtime)

2. **Download**  
   [Get the latest release on GitHub](https://github.com/MortysTerminal/MortysDLP/releases)

3. **Start**
   - Unzip the archive and run `MortysDLP.exe`
   - On first launch, yt-dlp and ffmpeg/ffprobe will be downloaded automatically if they are missing

---

## Usage

### Download tab
1. Paste the URL of the video or audio you want to save
2. Configure the options — format, quality, time range, audio-only, etc.
3. Click **Start Download**; progress and status are shown in real time
4. Use the **History** button to re-use a previous URL

### Convert tab
1. Add one or more local media files via **Add Files**
2. Choose the target format and output folder
3. Optionally adjust video and audio quality
4. Click **Start Conversion**

### Settings tab
- Change the default download path and the separate audio-only download path
- Switch the UI language (Auto / Deutsch / English)
- Enable Debug Mode to see raw tool output
- Open the GitHub project page or close the application

### Updates
When a new version of MortysDLP is available, a banner appears at the top of the main window.
Clicking it opens a changelog window with the full release notes.
You can then choose to **update now** or dismiss and continue working.

---

## Support & Documentation

- [yt-dlp Documentation](https://github.com/yt-dlp/yt-dlp)
- [ffmpeg Documentation](https://ffmpeg.org/documentation.html)
- [Issues & Feature Requests](https://github.com/MortysTerminal/MortysDLP/issues)

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

---

**Note:** MortysDLP is a private open-source project. Use at your own risk.  
Please respect the terms of service of the platforms you download from.

---

*This README was written with the assistance of [GitHub Copilot](https://github.com/features/copilot).*
*Because me stupid, when I try to explain everything in english*