# MortysDLP

**MortysDLP** is a modern, user-friendly WPF application for conveniently downloading videos and audio from various platforms (e.g., YouTube, Twitch, and more) on Windows. The software uses [yt-dlp](https://github.com/yt-dlp/yt-dlp) and [ffmpeg](https://ffmpeg.org/) in the background and provides a graphical interface with many comfort features.

---

## Purpose and Benefits

- **Purpose:**  
  MortysDLP simplifies downloading and converting media content from the internet. It is designed for users who want to save videos or audio without using the command line or dealing with technical hurdles.

- **Benefits:**  
  - Easy to use with a modern interface  
  - Automatic management and updating of required tools (yt-dlp, ffmpeg, ffprobe)  
  - Download full videos, audio only, specific time sections, or just the first seconds  
  - Download history and individual settings  
  - Support for many platforms thanks to yt-dlp

---

## Features

- Download videos and audio by URL
- Select audio formats (e.g., mp3, m4a, etc.)
- Download specific time sections or only the first seconds
- Progress bar and status messages
- Automatic updates for the software and tools
- Download history
- Multilanguage support (currently: English, extendable)
- Easy configuration of download paths

---

## Installation

1. **Requirements:**  
   - Windows 10/11  
   - .NET 9 Runtime (included or installed automatically)

2. **Download:**  
   - [Get the latest release on GitHub](https://github.com/MortysTerminal/MortysDLP/releases)

3. **Start:**  
   - Unzip the application and start `MortysDLP.exe`  
   - On first launch, yt-dlp and ffmpeg/ffprobe will be downloaded automatically if missing

---

## Usage

1. **Paste URL:**  
   Copy the desired video/audio URL into the input field.

2. **Choose options:**  
   - Select audio-only, video format, time section, or first seconds
   - Adjust download path if needed

3. **Start download:**  
   Click “Start Download”. Progress and status will be displayed.

4. **History:**  
   Use the history menu to view previous downloads.

---

## Support & Documentation

- [yt-dlp Documentation](https://github.com/yt-dlp/yt-dlp)
- [ffmpeg Documentation](https://ffmpeg.org/documentation.html)
- [Project page & Issues](https://github.com/MortysTerminal/MortysDLP/issues)

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

---

**Note:**  
MortysDLP is a private open-source project. Use at your own risk. Please respect the terms of service of the respective platforms.
